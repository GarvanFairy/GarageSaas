# Phase 6C Application Control investigation — 27 October 2026

## Finding

The current blocker is the unsigned application dependency `GarageSaas.UnitTests/bin/Release/net10.0/GarageSaas.dll`, loaded by the Microsoft-signed `testhost.exe`. Code Integrity identifies the enforced policy precisely:

| Field | Observed value |
| --- | --- |
| Policy name | VerifiedAndReputableDesktop |
| Policy GUID | {0283AC0F-FFF1-49AE-ADA1-8A933130CAD6} |
| Policy friendly ID/version | 27555.1000.240208 |
| Policy hash | 2668895A5B233A80432D00D67251D7B7F52686A3FB13780F4B242C5A1F937A01 |
| Enforced block event | 3077, record 7750, 07:42:52.8397931 UTC |
| Companion load failure | 3033, record 7748 |
| Status | 0xc0e90002 |
| Requested / validated signing levels | 2 / 1 |
| Signature event | 3089, record 7751; TotalSignatureCount=0, publisher/issuer Unknown |
| Blocked file SHA256 flat hash | FA84DD8F043FFC98F3A772B1D7D409484ECA26A438141D32C4FC9155784C0F4B |

The matching active `.cip` file exists beneath Windows/System32/CodeIntegrity/CiPolicies/Active. The read-only registry value `VerifiedAndReputablePolicyState` is 1. These findings identify Smart App Control enforcement. This is a policy trust rejection of unsigned code; the event does not provide a separate per-file deny-rule ID or establish a malware detection. The policy, file and signature evidence are sufficient to identify the blocking control without changing or decoding the policy.

## Evidence scope and limitations

Read the Code Integrity Operational log for the previous test window, 07:41:30–07:43:30 UTC. There were 261 event-3077 blocks for this same GarageSaas.dll and no other blocked filename in that window. Saved a representative correlated 3033/3077/3089 event group and counts in `PHASE6C-APPLICATION-CONTROL-EVENTS.json`, rather than retaining hundreds of duplicates. Its flat hash matches the current file's SHA256.

The test host started, discovered the focused tests and reported their load exceptions. It is not the blocked image in these events. There is no evidence here of a dynamically generated assembly block; the application DLL failed before the diagnostic test bodies could complete. Later dependencies or generated mocks could reveal additional issues once this first block is resolved; their behavior is not yet established.

Authenticode checks: GarageSaas.dll, GarageSaas.UnitTests.dll and SignupAPI.dll are NotSigned; testhost.exe and the installed dotnet.exe have Valid Microsoft signatures. Details and current file hashes are in `PHASE6C-BINARY-SIGNATURES.json`. Unsigned test/API binaries are potential later trust dependencies, but this window only proves GarageSaas.dll was blocked.

Read all available AppLocker EXE/DLL events (32) and MSI/Script events (574). Neither log contained matches for GarageSaas, testhost or DynamicProxyGenAssembly2. Their newest events were 15 October and 26 October respectively. No AppLocker rule is implicated by the available evidence. No log was enabled or cleared.

Initial unprivileged log discovery was denied, but subsequent read-only event queries succeeded using the tool's approved outside-sandbox execution. `CiTool -lp -json` still returned access denied (0x80070005). Outside-sandbox tool authorization is not Windows UAC elevation; full policy enumeration remains unavailable. No elevation process, policy modification or enforcement workaround was attempted.

## Existing trusted execution/signing availability

No code-signing certificates were found in CurrentUser/My or LocalMachine/My. No signtool executable was found through PATH or the conventional Windows Kits 10/bin directory. Inspected repository build/script configuration has no Authenticode signing integration, and `.github/workflows` contains no workflow files. No Azure Pipelines or Jenkins configuration was found by the targeted filename search. The repository has a PFX named SelfSignedForInvite; it was not opened, imported or repurposed and is not evidence of an approved trusted code-signing process.

An external/HSM signing service or another approved development machine could exist outside the inspected environment. None has been identified or authorized for use in this session. An available tool, certificate, VM or hosted runner alone would not establish administrative approval.

## Simplest route and exact administrator action

Preferred: the workstation/security administrator identifies an **existing approved development/test machine or runner** whose established policy permits this project's locally built .NET assemblies, and approves transferring the reviewed source snapshot and running the focused command below there. Provide the machine/runner identity and approved access method; do not change this workstation's enforcement. Use the same source snapshot, record its provenance and retain the TRX. No new infrastructure or paid CI is needed. Do not substitute an alternate local execution mechanism to evade the block.

If an existing trusted signing process is already available, the administrator can instead authorize its use and identify the accepted signing identity/provider and procedure. Sign the reviewed first-party executable assemblies through that process, validate the signatures and policy acceptance, then run the tests without rebuilding unsigned copies over them. Do not create/import a self-signed certificate, add trust roots or assume .NET strong-name signing supplies Authenticode trust. Signing availability and acceptance must be established before treating this as an executable route. This investigation does not authorize purchasing a certificate or provisioning a signing service.

Microsoft documents that Smart App Control accepts signatures from trusted providers; see [code signing for Smart App Control](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control). The event interpretation follows Microsoft's [App Control troubleshooting guide](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/appcontrol-debugging-and-troubleshooting). [Smart App Control overview](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/overview) describes its reputation/signature enforcement model.

## Test command and current result

On the approved environment, run from the repository root:

```powershell
dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~StagingSubjectVerificationTests --logger 'trx;LogFileName=phase6c-approved-route.trx' --results-directory .test-results
```

An approved clean build environment must first restore the repository's dependencies through its normal process; the existing prepared local outputs already have restored packages. A signing route requires building first, signing the necessary outputs, and adding `--no-build` to the command so those signed outputs are tested intact.

Expected acceptance: all 19 focused cases execute and pass; compilation alone and synthetic test discovery are insufficient. Current result remains the previous normal-runner attempt: 19 failed due to Application Control, zero passed (`phase6c-current-verification.trx`). No route became available during this investigation, so an unchanged blocked test run was not repeated. The diagnostic remains undeployed.

No Azure calls, B2C changes, SQL connections, existing application changes, App Service settings changes, security policy changes, antivirus changes or infrastructure creation occurred in this investigation. SMTP/OpenAI configuration was left unchanged in its previously confirmed disabled state.
