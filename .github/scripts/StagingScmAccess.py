"""One read-only SCM GET; CLI/token/HTTP streams never reach workflow output."""
import contextlib
import http.client
import json
import os
from pathlib import Path
import re
import socket
import ssl
import subprocess
import sys
import time

HOST = "garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net"
DEPLOYMENT = "4b4367727dbe4d6b9a5e9fe0bd87568d"
PATH = "/api/deployments/" + DEPLOYMENT
CLIENT = "dd6047f8-4557-4401-aab5-d7fd9e7d40a8"
TENANT = "90589b9f-6a39-4285-a798-5d72f6937826"
SUBSCRIPTION = "d49b2f61-156a-489f-ba4c-acba574d30a0"
RESOURCE = "/subscriptions/" + SUBSCRIPTION + "/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu"
MAX_RESPONSE = 65536
MAX_TOKEN = 32768
HTTP_DEADLINE = 10
REASONS = frozenset(("VerifiedRead", "ContextRejected", "IdentityRejected", "TokenUnavailable",
    "Timeout", "TransportFailure", "RedirectRejected", "Unauthorized", "Forbidden",
    "UnexpectedStatus", "ResponseTooLarge", "MalformedResponse", "ProbeFailed", "EvidenceUnavailable"))


def receipt(reason, status=None):
    return {"schema": 1, "outcome": "Success" if reason == "VerifiedRead" else "Failure",
            "reason": reason, "httpStatus": status}


def strict_json(raw):
    def pairs(items):
        result, seen = {}, set()
        for key, value in items:
            if key.casefold() in seen:
                raise ValueError()
            seen.add(key.casefold())
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=pairs)


def valid_receipt(value):
    return (type(value) is dict and set(value) == {"schema", "outcome", "reason", "httpStatus"}
        and type(value["schema"]) is int and value["schema"] == 1
        and type(value["reason"]) is str and value["reason"] in REASONS
        and value["outcome"] == ("Success" if value["reason"] == "VerifiedRead" else "Failure")
        and (value["httpStatus"] is None or (type(value["httpStatus"]) is int and 100 <= value["httpStatus"] <= 599))
        and (value["reason"] != "VerifiedRead" or value["httpStatus"] == 200))


def context_valid(env):
    expected = {"GITHUB_REPOSITORY": "GarvanFairy/GarageSaas", "GITHUB_REF": "refs/heads/master",
        "GITHUB_WORKFLOW_REF": "GarvanFairy/GarageSaas/.github/workflows/staging-oidc-diagnostic.yml@refs/heads/master",
        "GITHUB_EVENT_NAME": "workflow_dispatch", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_ENVIRONMENT": "github-hosted",
        "STAGING_CLIENT_ID": CLIENT, "STAGING_TENANT_ID": TENANT,
        "STAGING_SUBSCRIPTION_ID": SUBSCRIPTION, "STAGING_RESOURCE_ID": RESOURCE}
    return all(env.get(k) == v for k, v in expected.items()) and re.fullmatch(r"[a-f0-9]{40}", env.get("GITHUB_SHA", "")) is not None


def private_run(arguments, **kwargs):
    env = os.environ.copy()
    for key in ("AZURE_LOG_LEVEL", "AZURE_CORE_LOGGING_ENABLE_LOG_FILE", "AZURE_CLI_DISABLE_CONNECTION_VERIFICATION",
                "REQUESTS_CA_BUNDLE", "CURL_CA_BUNDLE"):
        env.pop(key, None)
    env.update(AZURE_CORE_COLLECT_TELEMETRY="false", AZURE_CORE_LOGGING_ENABLE_LOG_FILE="false",
               AZURE_LOGGING_ENABLE_LOG_FILE="false")
    return subprocess.run(arguments, capture_output=True, text=True, encoding="utf-8",
                          errors="strict", env=env, **kwargs)


def acquire_token():
    account = private_run(["az", "account", "show", "--query",
        "{tenant:tenantId,subscription:id,client:user.name,type:user.type}", "--output", "json", "--only-show-errors"], timeout=20)
    if account.returncode or len(account.stdout) > 4096:
        raise ValueError()
    info = strict_json(account.stdout)
    if info != {"tenant": TENANT, "subscription": SUBSCRIPTION, "client": CLIENT, "type": "servicePrincipal"}:
        raise PermissionError()
    result = private_run(["az", "account", "get-access-token", "--resource", "https://management.azure.com/",
        "--subscription", SUBSCRIPTION, "--query", "{token:accessToken,tenant:tenant,subscription:subscription,expiry:expires_on}",
        "--output", "json", "--only-show-errors"], timeout=20)
    if result.returncode or len(result.stdout) > 65536:
        raise ValueError()
    auth = strict_json(result.stdout)
    if (set(auth) != {"token", "tenant", "subscription", "expiry"} or auth["tenant"] != TENANT
        or auth["subscription"] != SUBSCRIPTION or type(auth["token"]) is not str
        or not re.fullmatch(r"[A-Za-z0-9_.-]{100,32768}", auth["token"])
        or not re.fullmatch(r"[0-9]{10}", str(auth["expiry"])) or int(auth["expiry"]) < time.time() + 60):
        raise ValueError()
    return auth["token"]


def scm_get(token, factory=http.client.HTTPSConnection):
    connection = response = None
    status = None
    try:
        # http.client has no redirect, proxy, authentication fallback or retry handler.
        connection = factory(HOST, port=443, timeout=5, context=ssl.create_default_context())
        connection.request("GET", PATH, headers={"Authorization": "Bearer " + token, "Accept": "application/json"})
        response = connection.getresponse()
        status = response.status
        if status == 401:
            return receipt("Unauthorized", status)
        if status == 403:
            return receipt("Forbidden", status)
        if 300 <= status < 400:
            return receipt("RedirectRejected", status)
        if status != 200:
            return receipt("UnexpectedStatus", status)
        headers = response.getheaders()
        if sum(len(k) + len(v) + 4 for k, v in headers) > 16384:
            return receipt("ResponseTooLarge", status)
        lengths = [v for k, v in headers if k.lower() == "content-length"]
        types = [v for k, v in headers if k.lower() == "content-type"]
        if len(lengths) > 1 or len(types) != 1 or types[0].split(";")[0].strip().lower() != "application/json":
            return receipt("MalformedResponse", status)
        if lengths and (not re.fullmatch(r"[0-9]{1,10}", lengths[0]) or int(lengths[0]) > MAX_RESPONSE):
            return receipt("ResponseTooLarge", status)
        raw = response.read(MAX_RESPONSE + 1)
        if len(raw) > MAX_RESPONSE:
            return receipt("ResponseTooLarge", status)
        info = strict_json(raw.decode("utf-8", errors="strict"))
        if (type(info) is not dict or info.get("id") != DEPLOYMENT or type(info.get("status")) is not int
            or info["status"] not in range(5) or type(info.get("active")) is not bool or type(info.get("complete")) is not bool):
            return receipt("MalformedResponse", status)
        # This verifies authenticated metadata access, not publication or acceptance.
        return receipt("VerifiedRead", status)
    except (TimeoutError, socket.timeout):
        return receipt("Timeout", status)
    except (ValueError, UnicodeError, RecursionError):
        return receipt("MalformedResponse", status)
    except Exception:
        return receipt("TransportFailure", status)
    finally:
        for resource in (response, connection):
            if resource:
                with contextlib.suppress(Exception):
                    resource.close()


def run(env):
    if not context_valid(env):
        return receipt("ContextRejected")
    stage = "Authentication"
    try:
        checkout = private_run(["git", "rev-parse", "HEAD"], timeout=5)
        if checkout.returncode or checkout.stdout.strip() != env["GITHUB_SHA"]:
            return receipt("ContextRejected")
        token = acquire_token()
        stage = "Probe"
        # A separate, killed-on-deadline child bounds DNS, TLS and slow response reads.
        # Token travels over private stdin, never argv/files/environment/workflow output.
        result = private_run([sys.executable, "-I", str(Path(__file__).resolve()), "--http-probe"],
                             input=token, timeout=HTTP_DEADLINE)
        token = None
        if len(result.stdout) > 1024:
            return receipt("ProbeFailed")
        value = strict_json(result.stdout)
        if not valid_receipt(value) or result.returncode != (0 if value["outcome"] == "Success" else 1):
            return receipt("ProbeFailed")
        return value
    except PermissionError:
        return receipt("IdentityRejected")
    except subprocess.TimeoutExpired:
        return receipt("Timeout")
    except Exception:
        return receipt("ProbeFailed" if stage == "Probe" else "TokenUnavailable")


def main(arguments):
    if arguments == ["--http-probe"]:
        token = sys.stdin.read(MAX_TOKEN + 1)
        value = scm_get(token) if re.fullmatch(r"[A-Za-z0-9_.-]{100,32768}", token) else receipt("TokenUnavailable")
        print(json.dumps(value, separators=(",", ":")))
        return 0 if value["outcome"] == "Success" else 1
    value = run(os.environ) if not arguments else receipt("ContextRejected")
    try:
        if not valid_receipt(value):
            raise ValueError()
        text = json.dumps(value, separators=(",", ":"))
        summary = os.environ.get("GITHUB_STEP_SUMMARY")
        if summary:
            with open(summary, "a", encoding="utf-8") as output:
                output.write(text + "\n")
    except Exception:
        value = receipt("EvidenceUnavailable")
    print(json.dumps(value, separators=(",", ":")))
    return 0 if value["outcome"] == "Success" else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
