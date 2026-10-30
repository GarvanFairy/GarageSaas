# Staging deployment execution hardening

## State: publishing blocked, including after removal of the workflow false guard

No deployment is authorized. The existing unconditional `if: ${{ false }}` and
the exact operational-readiness throw before Azure login remain unchanged.
Prepare/promote provenance, artifact 12933496952, source run 40953823134,
application 665d8d9a91235c73d1d41185d3c2df2ddb545f58 and independently approved
ZIP SHA-256 remain unchanged. No application build is part of this hardening.

This is a single-developer workflow. The developer approves merge, activation,
deployment and recovery separately. This change introduces no second-person
approval, reviewer requirement, branch protection, environment or RBAC change.

## CLI version and distribution

Use only the existing official Windows Azure CLI MSI installation's Python at
`%ProgramFiles%\Microsoft SDKs\Azure\CLI2\python.exe`, invoked with `-IBm azure.cli`
to avoid shell/batch interpretation of arguments. Require exactly Azure CLI 2.91.0;
fail if absent, older or newer. No download, installation, update or PATH fallback.
The published windows-2025 image inventory reviewed on 30 October 2026 lists this
version. The image label is mutable, so version drift deliberately blocks execution.
This checks installation path/version, not a cryptographic distribution attestation.

Sources:
- https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md
- https://github.com/Azure/azure-cli/blob/azure-cli-2.91.0/src/azure-cli/azure/cli/command_modules/appservice/custom.py

## Additional confirmed upstream blocker

In reviewed CLI 2.91.0, `_make_onedeploy_request` sends a local Windows ZIP then
polls through `_check_zip_deployment_status`, including when `--async true` is set.
That helper calls `_configure_default_logging` on failed deployment or timeout.
The latter attempts application/web-server/filesystem logging configuration changes.
It also polls `/api/deployments/latest`, which is not exact-request correlation.
No reviewed supported flag removes these problems. Consequently the wrapper
constructs and tests the intended command but NEVER invokes it. It emits only
ConfirmedFailure / PublishingPathBlocked, with no deployment ID, and fails.
This is a known pre-submission rejection, not a claim that Azure rejected a deploy.

Do not monkey-patch CLI, rely on a permission denial to prevent side effects,
expand RBAC, or exploit timeout values to evade the unsupported path. A separately
reviewed upstream fix or supported publishing mechanism is needed before execution.
The current PR must not be described as ready to activate deployment.

## Implemented execution and status primitives

The private process collector uses argument arrays, no shell, concurrent stdout
and stderr draining, 64 Ki-character limits per stream and a bounded deadline.
Limits/timeouts terminate the local process tree only, never Azure processes or
resources. Killing a client cannot cancel an already submitted server operation.
Raw streams/exceptions are never written to summaries, reports or artifact files.
Only a three-field allowlisted receipt may reach stdout/step summary.

The command plan fixes target, identity, ZIP filename and approved hash, reuses
existing package validation and preserves ZIP/clean/restart semantics. The process
collector is tested using local synthetic processes, not Azure CLI deployments.

Outcome model:
- ConfirmedSuccess: a trusted exact-request deployment ID, a different known baseline
  deployment, and an independently read matching completed/active status 4.
- ConfirmedFailure: exact correlated terminal status 3, or an explicitly identified
  pre-submission rejection. A nonzero CLI exit is not proof of failed activation.
- Unknown: missing correlation, timeout, interruption, oversized output, nonzero
  process exit, malformed/duplicate/unexpected fields, inconsistent status or no
  terminal evidence. Missing receipt after runner termination is also Unknown.

The strict projected status contract is `{id,status,active,complete}`. It is not a
claim that unmodified raw ARM/Kudu responses have that schema. The future supported
adapter must validate/project its documented response and prove request correlation.
No live adapter is enabled. `/health` alone never proves activation or database,
B2C, membership, customer/vehicle, quote, invoice or isolation acceptance.

## Read-only reconciliation runbook (not an execution script)

1. Freeze further mutations. Identify the exact staging resource, application SHA,
   approved ZIP hash, controller commit and original promotion run/attempt.
2. Record the last known active deployment as baseline before an approved attempt.
3. Prefer an identifier obtained from that exact publishing request's response.
   Do not substitute `/latest`, timestamp proximity, a healthy endpoint or a user-
   entered ID as proof of package/request correspondence.
4. An authorized operator reads deployment metadata for the exact ID, and current
   active deployment identity, using existing read permissions. Emit IDs and fixed
   statuses only; never logs, headers, tokens, messages or application data.
5. Compare baseline, correlated ID, completion and activation evidence. Missing,
   contradictory or uncorrelated evidence remains Unknown. Do not poll or mutate
   automatically after uncertainty; the developer authorizes further read checks.
6. Only after activation is established perform existing liveness checking. Any
   synthetic authenticated acceptance remains separately authorized.
7. Require the developer's explicit approval before retry, restart, restore or
   rollback. Use the retained exact rollback ZIP only after persistent-state and
   schema compatibility checks; no rebuild or automatic database restore.

## Offline tests and remaining work

70 deterministic execution/status regressions exercise version gates, command
identity, strict JSON, conflicting/historical IDs, both bounded pipes, oversized
output, nonzero/terminated/timeout processes, exception canaries, summaries and
workflow mutation rejection. The existing hardening suite invokes them in hosted
artifact-security CI alongside its original 160 and the 62 promotion regressions.

Before activation: resolve the upstream publishing/correlation blocker, review a
real response adapter with deterministic fixtures, and confirm the existing runtime,
recovery, capacity, SCM/network and developer authorization prerequisites. No live
deployment or restore is part of this change.
