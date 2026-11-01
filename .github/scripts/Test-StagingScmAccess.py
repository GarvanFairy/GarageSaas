"""Offline cases, invoked by the existing required Staging security suite."""
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import ssl
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("scm_probe", ROOT / "StagingScmAccess.py")
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
CANARY = "synthetic-secret-not-for-export-5619"
TOKEN = "a" * 200
WORKFLOW = ROOT / "../workflows/staging-oidc-diagnostic.yml"


def environment():
    return {"GITHUB_REPOSITORY": "GarvanFairy/GarageSaas", "GITHUB_REF": "refs/heads/master",
        "GITHUB_WORKFLOW_REF": "GarvanFairy/GarageSaas/.github/workflows/staging-oidc-diagnostic.yml@refs/heads/master",
        "GITHUB_EVENT_NAME": "workflow_dispatch", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_ENVIRONMENT": "github-hosted",
        "GITHUB_SHA": "a" * 40, "STAGING_CLIENT_ID": probe.CLIENT, "STAGING_TENANT_ID": probe.TENANT,
        "STAGING_SUBSCRIPTION_ID": probe.SUBSCRIPTION, "STAGING_RESOURCE_ID": probe.RESOURCE}


def completed(stdout="", code=0):
    return subprocess.CompletedProcess([], code, stdout, CANARY)


def account():
    return {"tenant": probe.TENANT, "subscription": probe.SUBSCRIPTION, "client": probe.CLIENT, "type": "servicePrincipal"}


def auth():
    return {"token": TOKEN, "tenant": probe.TENANT, "subscription": probe.SUBSCRIPTION, "expiry": "9999999999"}


class Response:
    def __init__(self, status=200, body=None, headers=None):
        self.status = status
        self.body = json.dumps({"id": probe.DEPLOYMENT, "status": 4, "active": True, "complete": True}).encode() if body is None else body
        self.headers = [("Content-Type", "application/json")] if headers is None else headers
        self.limit = None

    def getheaders(self):
        return self.headers

    def read(self, limit):
        self.limit = limit
        return self.body[:limit]

    def close(self):
        pass


class Connection:
    def __init__(self, response):
        self.response = response
        self.requests = []

    def request(self, *args, **kwargs):
        self.requests.append((args, kwargs))

    def getresponse(self):
        return self.response

    def close(self):
        pass


def workflow_valid(path):
    try:
        r = subprocess.run([sys.executable, "-I", str(ROOT / "Read-WorkflowYaml.py"), str(path)],
                           capture_output=True, text=True, timeout=10)
        if r.returncode:
            return False
        w = json.loads(r.stdout)
        j = w["jobs"]["verify-oidc"]
        s = j["steps"]
        hashes = [hashlib.sha256(s[i]["run"].replace("\r\n", "\n").encode()).hexdigest() for i in (0, 3)]
        return (set(w["on"]) == {"workflow_dispatch"} and w["on"]["workflow_dispatch"] == ""
            and set(w["jobs"]) == {"verify-oidc"} and w["permissions"] == {"contents": "read"}
            and w["concurrency"] == {"group": "garagesaas-staging-oidc-diagnostic", "cancel-in-progress": "false"}
            and j["name"] == "Verify staging OIDC and App Service read access"
            and j["if"] == "${{ github.repository == 'GarvanFairy/GarageSaas' && github.ref == 'refs/heads/master' }}"
            and j["runs-on"] == "ubuntu-24.04" and j["timeout-minutes"] == "10" and j["environment"] == "staging"
            and j["permissions"] == {"contents": "read", "id-token": "write"}
            and set(j) == {"name", "if", "runs-on", "timeout-minutes", "environment", "permissions", "env", "steps"}
            and j["env"] == {k: "${{ vars." + v + " }}" for k, v in {
                "STAGING_CLIENT_ID": "AZURE_STAGING_CLIENT_ID", "STAGING_TENANT_ID": "AZURE_STAGING_TENANT_ID",
                "STAGING_SUBSCRIPTION_ID": "AZURE_STAGING_SUBSCRIPTION_ID", "STAGING_RESOURCE_ID": "AZURE_STAGING_RESOURCE_ID"}.items()}
            and len(s) == 5 and all("if" not in step and "continue-on-error" not in step for step in s)
            and hashes == ["2d4dbe2b0c2f20280980373151dcf643ae2a36bef68bd7fc6db299728fd45804",
                           "e7850d20f46ca46d01a1c7fdb1f946af16c13ee95439d823036bad6744bf9bd9"]
            and s[0]["shell"] == "bash" and s[3]["shell"] == "bash"
            and set(s[0]) == {"name", "shell", "run"} and set(s[3]) == {"name", "shell", "run"}
            and s[1] == {"name": "Checkout reviewed diagnostic source", "uses": "actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803",
                         "with": {"ref": "${{ github.sha }}", "persist-credentials": "false"}}
            and s[2] == {"name": "Authenticate with Azure via GitHub OIDC (no secret)", "uses": "azure/login@8216e11d8cd9b42fe925c852af8e76311ff067ac",
                         "with": {"client-id": "${{ env.STAGING_CLIENT_ID }}", "tenant-id": "${{ env.STAGING_TENANT_ID }}", "subscription-id": "${{ env.STAGING_SUBSCRIPTION_ID }}"}}
            and s[4] == {"name": "Verify staging SCM Entra read access (one GET)", "shell": "bash", "run": "python3 -I ./.github/scripts/StagingScmAccess.py"})
    except Exception:
        return False


class Security(unittest.TestCase):
    def check_response(self, response):
        c = Connection(response)
        self.factory = lambda *a, **k: c
        result = probe.scm_get(TOKEN, self.factory)
        self.assertEqual(len(c.requests), 1)
        self.assertTrue(probe.valid_receipt(result))
        self.assertNotIn(CANARY, json.dumps(result))
        self.assertNotIn(TOKEN, json.dumps(result))
        return result

    def test_01_context(self):
        self.assertTrue(probe.context_valid(environment()))

    def test_02_context_rejections(self):
        for key in environment():
            with self.subTest(key=key):
                e = environment(); e[key] = CANARY
                self.assertFalse(probe.context_valid(e))

    def test_03_checkout_mismatch(self):
        with patch.object(probe, "private_run", return_value=completed("b" * 40)) as p:
            self.assertEqual(probe.run(environment())["reason"], "ContextRejected")
            self.assertEqual(p.call_count, 1)

    def test_04_acquire(self):
        with patch.object(probe, "private_run", side_effect=[completed(json.dumps(account())), completed(json.dumps(auth()))]) as p:
            self.assertEqual(probe.acquire_token(), TOKEN)
            self.assertIn("https://management.azure.com/", p.call_args.args[0])

    def test_05_identity(self):
        for key in account():
            with self.subTest(key=key):
                value = account(); value[key] = CANARY
                with patch.object(probe, "private_run", return_value=completed(json.dumps(value))):
                    with self.assertRaises(PermissionError): probe.acquire_token()

    def test_06_invalid_token(self):
        for key in auth():
            with self.subTest(key=key):
                value = auth(); value[key] = CANARY
                with patch.object(probe, "private_run", side_effect=[completed(json.dumps(account())), completed(json.dumps(value))]):
                    with self.assertRaises(ValueError): probe.acquire_token()
        expired = auth(); expired["expiry"] = "1000000000"
        with patch.object(probe, "private_run", side_effect=[completed(json.dumps(account())), completed(json.dumps(expired))]):
            with self.assertRaises(ValueError): probe.acquire_token()

    def test_07_auth_failures(self):
        for value in (completed(CANARY, 1), completed("x" * 65537), completed("{}")):
            with patch.object(probe, "private_run", return_value=value):
                with self.assertRaises((ValueError, PermissionError)): probe.acquire_token()

    def test_08_exact_get_tls(self):
        c = Connection(Response())
        def factory(host, **kwargs):
            self.assertEqual(host, "garagesaas-stg-d49b2f61-weu.scm.azurewebsites.net")
            self.assertEqual(kwargs["port"], 443); self.assertEqual(kwargs["timeout"], 5)
            self.assertTrue(kwargs["context"].check_hostname)
            self.assertEqual(kwargs["context"].verify_mode, ssl.CERT_REQUIRED)
            return c
        self.assertEqual(probe.scm_get(TOKEN, factory)["outcome"], "Success")
        self.assertEqual(c.requests[0][0], ("GET", "/api/deployments/4b4367727dbe4d6b9a5e9fe0bd87568d"))
        self.assertEqual(c.requests[0][1]["headers"]["Authorization"], "Bearer " + TOKEN)
        self.assertEqual(len(c.requests), 1)

    def test_09_unauthorized(self):
        self.assertEqual(self.check_response(Response(401))["reason"], "Unauthorized")

    def test_10_forbidden(self):
        self.assertEqual(self.check_response(Response(403))["reason"], "Forbidden")

    def test_11_redirects(self):
        for status in (301, 302, 303, 307, 308):
            self.assertEqual(self.check_response(Response(status, headers=[("Location", "https://evil.invalid")]))["reason"], "RedirectRejected")

    def test_12_unexpected_status(self):
        for status in (201, 204, 404, 429, 500, 503):
            self.assertEqual(self.check_response(Response(status))["outcome"], "Failure")

    def test_13_content_length_bound(self):
        r = Response(headers=[("Content-Type", "application/json"), ("Content-Length", "65537")])
        self.assertEqual(self.check_response(r)["reason"], "ResponseTooLarge")
        self.assertIsNone(r.limit)

    def test_14_stream_bound(self):
        r = Response(body=b"x" * 100000)
        self.assertEqual(self.check_response(r)["reason"], "ResponseTooLarge")
        self.assertEqual(r.limit, 65537)

    def test_15_invalid_headers(self):
        for extra in ([('Content-Length', '-1')], [('Content-Length', '1'), ('Content-Length', '2')], [('Content-Type', 'application/json')]):
            self.assertEqual(self.check_response(Response(headers=[('Content-Type', 'application/json')] + extra))["outcome"], "Failure")
        self.assertEqual(self.check_response(Response(headers=[('Content-Type', 'application/json'), ('X-Extra', 'x' * 16385)]))["reason"], "ResponseTooLarge")

    def test_16_html(self):
        self.assertEqual(self.check_response(Response(body=CANARY.encode(), headers=[('Content-Type', 'text/html')]))["reason"], "MalformedResponse")

    def test_17_wrong_deployment(self):
        body = json.dumps({"id": "b" * 32, "status": 4, "active": True, "complete": True}).encode()
        self.assertEqual(self.check_response(Response(body=body))["reason"], "MalformedResponse")

    def test_18_invalid_types(self):
        for key, value in (("status", True), ("status", "4"), ("status", 99), ("active", 1), ("complete", "true")):
            data = {"id": probe.DEPLOYMENT, "status": 4, "active": True, "complete": True}; data[key] = value
            self.assertEqual(self.check_response(Response(body=json.dumps(data).encode()))["reason"], "MalformedResponse")

    def test_19_malformed_json(self):
        for body in (b"{", b"[]", b"null", b"\xff", b'{"id":"a","ID":"b"}', b'{"x":{"a":1,"a":2}}'):
            self.assertEqual(self.check_response(Response(body=body))["reason"], "MalformedResponse")

    def test_20_exception_canary(self):
        def fail(*a, **k): raise RuntimeError(CANARY)
        value = probe.scm_get(TOKEN, fail)
        self.assertEqual(value["reason"], "TransportFailure"); self.assertNotIn(CANARY, json.dumps(value))

    def test_21_network_timeout(self):
        def fail(*a, **k): raise TimeoutError(CANARY)
        self.assertEqual(probe.scm_get(TOKEN, fail)["reason"], "Timeout")

    def test_22_tls_failure(self):
        def fail(*a, **k): raise ssl.SSLCertVerificationError(CANARY)
        self.assertEqual(probe.scm_get(TOKEN, fail)["outcome"], "Failure")

    def test_23_private_metadata(self):
        data = {"id": probe.DEPLOYMENT, "status": 4, "active": True, "complete": True, "message": CANARY, "log_url": "https://evil.invalid"}
        self.assertEqual(self.check_response(Response(body=json.dumps(data).encode()))["outcome"], "Success")

    def test_24_total_deadline(self):
        with patch.object(probe, "acquire_token", return_value=TOKEN), patch.object(probe, "private_run", side_effect=[completed("a" * 40), subprocess.TimeoutExpired("private", 10)]) as p:
            self.assertEqual(probe.run(environment())["reason"], "Timeout")
            self.assertEqual(p.call_args.kwargs["timeout"], 10); self.assertEqual(p.call_count, 2)

    def test_25_private_stdin(self):
        with patch.object(probe, "acquire_token", return_value=TOKEN), patch.object(probe, "private_run", side_effect=[completed("a" * 40), completed(json.dumps(probe.receipt("VerifiedRead", 200)))]) as p:
            self.assertEqual(probe.run(environment())["outcome"], "Success")
            self.assertEqual(p.call_args.kwargs["input"], TOKEN)
            self.assertNotIn(TOKEN, repr(p.call_args.args))

    def test_26_child_auth_failure(self):
        with patch.object(probe, "acquire_token", return_value=TOKEN), patch.object(probe, "private_run", side_effect=[completed("a" * 40), completed(json.dumps(probe.receipt("Forbidden", 403)), 1)]):
            self.assertEqual(probe.run(environment())["reason"], "Forbidden")

    def test_27_child_malicious(self):
        bad = probe.receipt("VerifiedRead", 200); bad["token"] = CANARY
        for text in (json.dumps(bad), CANARY, "x" * 1025, '{"schema":1,"schema":1}'):
            with patch.object(probe, "acquire_token", return_value=TOKEN), patch.object(probe, "private_run", side_effect=[completed("a" * 40), completed(text)]):
                value = probe.run(environment()); self.assertEqual(value["outcome"], "Failure"); self.assertNotIn(CANARY, json.dumps(value))

    def test_28_child_exit_mismatch(self):
        with patch.object(probe, "acquire_token", return_value=TOKEN), patch.object(probe, "private_run", side_effect=[completed("a" * 40), completed(json.dumps(probe.receipt("VerifiedRead", 200)), 1)]):
            self.assertEqual(probe.run(environment())["reason"], "ProbeFailed")

    def test_29_summary_failure(self):
        output = io.StringIO()
        with patch.object(probe, "run", return_value=probe.receipt("VerifiedRead", 200)), patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": CANARY}), patch("builtins.open", side_effect=OSError(CANARY)), contextlib.redirect_stdout(output):
            self.assertEqual(probe.main([]), 1)
        self.assertNotIn(CANARY, output.getvalue()); self.assertIn("EvidenceUnavailable", output.getvalue())

    def test_30_no_alternate_target(self):
        with patch.object(probe, "run") as p, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(probe.main(["--url", CANARY]), 1)
            p.assert_not_called()
        with patch.object(probe.sys, "stdin", io.StringIO("a" * 32769)), patch.object(probe, "scm_get") as p, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(probe.main(["--http-probe"]), 1)
            p.assert_not_called()

    def test_31_workflow_reviewed(self):
        self.assertTrue(workflow_valid(WORKFLOW))

    def test_32_workflow_mutations(self):
        text = WORKFLOW.read_text(encoding="utf-8").replace("\r\n", "\n")
        mutations = [text.replace("workflow_dispatch:", "push:"), text.replace("refs/heads/master", "refs/heads/feature"),
            text.replace("persist-credentials: false", "persist-credentials: true"), text.replace("ref: ${{ github.sha }}", "ref: master"),
            text.replace("id-token: write", "id-token: read"), text.replace("contents: read", "contents: write"),
            text.replace("python3 -I ./.github/scripts/StagingScmAccess.py", "echo skipped"),
            text.replace("      - name: Verify staging SCM Entra read access (one GET)", "      - if: ${{ false }}\n        name: Verify staging SCM Entra read access (one GET)"),
            text.replace("--api-version 2024-11-01", "--api-version 2020-01-01"),
            text.replace(probe.CLIENT, "b" * 36), text.replace("cancel-in-progress: false", "cancel-in-progress: true")]
        repository = ROOT.parents[1].resolve()
        test_root = repository / "artifacts/scm-security-tests"
        self.assertTrue(test_root.resolve().is_relative_to(repository))
        test_root.mkdir(parents=True, exist_ok=True)
        directory = Path(tempfile.mkdtemp(prefix="offline-", dir=test_root)).resolve()
        self.assertEqual(directory.parent, test_root.resolve())
        path = directory / "workflow.yml"
        try:
            for i, mutation in enumerate(mutations):
                self.assertNotEqual(text, mutation)
                path.write_text(mutation, encoding="utf-8")
                with self.subTest(case=i): self.assertFalse(workflow_valid(path))
        finally:
            # One known synthetic file; no recursive delete or unrelated paths.
            path.unlink(missing_ok=True)
            directory.rmdir()


if __name__ == "__main__":
    result = unittest.TestResult()
    unittest.defaultTestLoader.loadTestsFromTestCase(Security).run(result)
    if result.testsRun != 32 or result.failures or result.errors or result.skipped:
        print("FAIL: required offline SCM security regressions.")
        for test, _ in result.failures + result.errors:
            name = test.id().split(".")[-1]
            if name.startswith("test_") and len(name) < 80:
                print(name)
        raise SystemExit(1)
    print("PASS: 32 offline SCM authentication/security regressions; no Azure or network access.")
