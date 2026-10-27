# Staging artifact checker correction — 27 October 2026

Local preparation only. No commit/push, GitHub setting change, Azure access,
deployment, SQL operation or authentication-code change occurred. Staging deploy
remains hard-disabled. The original identity tests and full CI gates are unchanged.

## Causes and corrections

The original JSON-key regex matched the substring `password` in the public policy
identifier `AzureAdB2C.ResetPasswordPolicyId`. The corrected checker accepts only
that case-sensitive JSON path, only with `B2C_` policy-id syntax. Lookalike property
names, the same name at another location and non-policy values remain rejected.
No general Password-named-property exemption exists. Passwords, API keys, client
secrets, access/refresh/ID/bearer tokens, private keys and nonempty connection-string
settings remain prohibited. Public numeric `MaxOutputTokens` is not an access token.
Every JSON file receives structural inspection; XML environment name/value pairs
are also inspected. UTF-8 BOM handling permits the SDK's valid web.config without
ignoring any XML content. The reference project's two existing signupappsettings
files are explicitly named and receive the same configuration checks; arbitrary
appsettings variants remain rejected.

The original checker decoded every binary as UTF-8 and UTF-16 and applied a broad
`Password=...` expression. Six native MSAL assets contain static formatting strings
which match it. They are not trusted just because the filenames contain MSAL:

- Package: **Microsoft.Identity.Client.NativeInterop/0.20.6**.
- Exact six paths and SHA-256 values are in
  `.github/staging-native-dependencies.json`.
- `dotnet nuget verify --all` passed for the cached NuGet archive, with Microsoft
  author and NuGet repository signatures. The archive's SHA-512 was independently
  verified. All six ZIP asset hashes match entries of that signed archive, the
  restored cache and the published files.
- The dependency graph's NuGet content SHA-512 is also pinned. It is distinct from
  the raw signed-archive SHA-512; both are recorded, not treated as interchangeable.

Only these six exact path/hash/package matches omit broad credential-assignment
heuristics. Any byte change fails the hash gate first. Diagnostic/private-key
scanning still applies. Other MSAL assemblies, application DLLs, apphosts and all
other files retain credential-assignment scans, including UTF-8 and UTF-16 content.
No binary is loaded/executed to inspect it.

Once the earlier false positives were removed, complete validation also reached
`Azure.Security.KeyVault.Secrets.dll`: the old filename rule classified it as a
secret file. A separate **exact path/hash/package/asset exception to that filename
keyword only** covers this legitimate dependency. Its 4.1.0 package passed NuGet
signature verification, and its published hash matches the signed archive entry.
All content checks remain enabled for it. This does not retrieve Key Vault secrets
or add any Azure permission. Other secret-named files remain forbidden.

Binary files must appear in the actual .NET 10 deps.json runtime/native/resource
inventory; only the two known SDK-generated application hosts are added explicitly.
All expected binary assets must be present. An extra DLL, disguised PE/ELF file,
renamed known library, changed pinned asset or changed pinned package is rejected.
Forbidden paths, hidden files, duplicate/case-colliding entries, traversal, key
files, uploads, local settings, diagnostics and required runtime files are checked
independently. No new file is removed from packaging to make validation succeed;
the prior documented PDB-only exclusion remains unchanged.

## Regression validation

`.github/scripts/Test-StagingReleaseArtifactChecks.ps1 -Path <actual candidate ZIP>`
uses generated nonfunctional fixture values and isolated ZIP copies, never Azure.
It validates an accepted complete baseline and exercises 37 mutation fixtures:

- Public policy ID, output-token limit and empty password accepted.
- Nonempty/numeric passwords, SQL credentials, API keys, client secrets, access and
  refresh tokens, private keys, policy lookalikes/wrong scope/wrong values rejected.
- Private-key files/content, Development/Local/hidden files, uploads and unexpected
  settings files rejected.
- ASCII/UTF-16 diagnostic strings and embedded application password/API-key
  assignments rejected.
- Modified MSAL native asset, modified named library, unexpected/disguised binary,
  changed package provenance, missing app assembly, duplicate/case-colliding paths
  and traversal rejected.
- Sensitive text, arbitrary JSON and XML environment configuration rejected.

The accepted baseline additionally requires all six real hash-pinned native assets
and checks that each contains the previously problematic formatting marker. Thus
the positive native test cannot pass simply by omitting those dependencies.

The staging preparation workflow now requires these regression checks after
package validation and before candidate upload. This adds a safety check only;
CI/master/environment/OIDC/hash/target restrictions and disabled deployment remain.

## Actual archive verification

The unchanged packager accepted the diagnostic-free local publish:

- **408 ZIP entries**, matching all 408 publish files after the pre-existing PDB
  exclusion; every decompressed entry independently matched its publish-file SHA-256.
- Required net10.0 runtime, DLLs, dependency graph, web.config, public settings and
  static/license content present. No diagnostic or prohibited configuration found.
- ZIP SHA-256 independently recomputed through a separate streaming hash path and
  matched release.json:

```text
8011a94177e6ce97d04a835df7530f6fa50fc287f0bf705fc9bb68d1c95e0669
```

Local ignored path:
`artifacts/staging-cd/checker-correction/local-validation-package/`.
The ZIP/manifest use an all-zero source SHA deliberately: this is an uncommitted
working-tree validation artifact, **not** a CI-approved master release candidate.
The prerequisite gate cannot select that SHA for deployment. Do not upload or
deploy this local artifact. A real release must be built/fully tested by the
reviewed workflow from the eventual approved master commit.

## Residual limits and next prerequisites

Pattern scanning cannot prove absence of encrypted, obfuscated, fragmented or
otherwise unknown secrets. Dependency-graph validation is structural, not a
cryptographic allowlist of every package/static file. Reviewed source/dependency
changes and protected master remain essential. Native/named-library upgrades
require explicit review and new provenance/hash verification, not automatic
regeneration of exemptions. Signatures establish provenance/integrity, not freedom
from vulnerabilities or a universal absence of embedded credentials.

No full dependency lock/reproducible-build guarantee has been added. Existing
dependency-version/lock, target resource-group discrepancy, live Azure read-only
verification, recoverable previous artifact, capacity, protected staging
environment, dedicated OIDC/app-scoped RBAC and authenticated staging acceptance
prerequisites remain in STAGING-CD-PREPARATION.md. No deployment is authorized by
passing this checker. Commit/push/PR and any Azure work need separate approval.
