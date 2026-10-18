# Three-index deployment package rehearsal — 18 October 2026

**Result: ready for a separately approved deployment on the reviewed target,
subject to fresh preflight, maintenance/resource/backup checks and the limits below.
Nothing has been deployed to the existing Garagesaas database.**

## Execution and environment

- SQL Server: local default GARVAN_ASUS, version 17.0.1145.4, Standard Developer
  Edition (64-bit), EngineEdition=2. Machine/physical host/registered/resolved
  identities verified; all test connections forced shared-memory transport.
- Windows: GARVAN_ASUS\ggall. No application connection string was used.
- Existing hardened DisposableSqlDatabase harness: unique generated names,
  pre-creation collision check, random ownership marker, fresh ownership/identity
  verification before DDL, bounded workers, active-session checks and guarded DROP.
- A separate fresh owned database was created for every case, including all
  destructive/failure-injection cases. Package verification never contacted the
  application database. Schema comparisons used fixed deployed sys-catalog SELECTs
  only; no application business records were read.
- Enabled only GARAGESAAS_RUN_INDEX_PACKAGE_REHEARSAL_SQL_TESTS=1 plus
  GARAGESAAS_TEST_SQLSERVER=GARVAN_ASUS and GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER=1.
  All other SQL fixture opt-ins were unset. Flags were cleared after execution.
- Used serial GarageSaas.UnitTests/SqlIntegration.runsettings. First ran only
  IndexPackageRehearsalSqlTests.SmokeRuntimeBinding: 1 passed. Then ran the
  IndexPackageRehearsalSqlTests.Rehearse data cases: 19 passed in 54 seconds.
  Combined: **20 passed, 0 failed, 0 skipped, 0 timed out**.

Rehearsal command (executed only with the three explicit process-scoped variables):

```text
dotnet test GarageSaas.UnitTests/GarageSaas.UnitTests.csproj -c Release --no-build --settings GarageSaas.UnitTests/SqlIntegration.runsettings --filter "FullyQualifiedName~IndexPackageRehearsalSqlTests.Rehearse" --logger "trx;LogFileName=index-package-rehearsal.trx"
```

## Exact adaptation boundary

The four reviewed SQL files were checksum-verified before and after each case;
**none required a correction or guard weakening**. Their SQL SHA256 values are:

| File | Tested SHA256 |
|---|---|
| 00-preflight.sql | 2e690bec82ec2f9746437c71edadfe4c4009cb5801e2e0e715b57e1ff7b3f041 |
| 01-deploy.sql | 96e37ba76d58dd13b7bae6ccae53ac7d20800e29f1509084d8c249d46426ab7b |
| 02-rollback.sql | 5feca759ea649c55e93494d3fa0e2e204a410b2aebeac3cac9d70b7bee44e299 |
| 03-verify.sql | 0a31033df3f44d8be2339c32c5acc42b1a694e9c4fe58e550367eb2675e8b6e4 |

The source scripts are intentionally pinned to Garagesaas, so submitting them
unchanged to a test catalog would reject it. The test adapter replaced that one
exact guard block **in memory only** with a stricter guard requiring the current
harness-generated database name and exact ownership token; it explicitly rejected
Garagesaas. Every connection was already verified by OpenOwnedConnection. Server,
transport, edition, schema, permissions, DDL-hook, index and package-ownership
checks remained intact. Execution flags and recorded GUID were set in the test
copy. The source guard was independently tested negatively, unchanged, against an
owned disposable catalog. A mismatched harness expected-name guard also rejected.

Fault injection modified only in-memory copies: THROW before the second CREATE,
and THROW after the first CREATE but before adding ownership markers. No source
SQL or application service was changed to obtain passing outcomes. This proves
the execution bodies and negative original-target guard on the approved local
engine, not an actual application-target execution.

## Schema alignment

Reused the experiment's reviewed shape-checked alignment: removed only the two
EF-only VehicleInvoice FKs/two indexes, two CustomerVehicle FKs and five parent
indexes, all inside each owned database. Actual object names were discovered from
metadata. Compared the five core tables plus four parents for column types,
nullability, identities, defaults/checks, PK/index definitions, FKs/trust/delete
behavior, triggers and relevant database/isolation/optimizer settings.

Only the three approved GarageBusiness widths differed: BusinessRegistrationNumber
and VatNumber nvarchar(255) vs deployed nvarchar(50), LogoImage nvarchar(255) vs
nvarchar(500). All other attributes matched. NON-MATERIAL for package DDL on the
reviewed native int columns of VehicleInvoice/InvoiceWorkQuote; no measured package
statement uses those strings, and seeded values fit both definitions. This is
experiment-relevant parity, not complete database-schema parity. The wrong-column
case deliberately changed InvoiceWorkQuote.WorkQuoteId to bigint only in its own
test database, and preflight rejected it before any candidate creation.

## Required case coverage

| Requirement | Executed scenario(s) and actual result |
|---|---|
| 1. Preview validation, no schema changes | preview: standalone preflight, deployment preview and rollback preview completed; index/ownership inventory unchanged. |
| 2. Exactly three indexes created | clean-and-verify, runtime smoke: exactly three NCIs on target tables, each with the reviewed shape. |
| 3. Verification/ownership | clean-and-verify: 03-verify completed; independent catalog assertions checked all eight key/include entries, ascending/nonunique/enabled/unfiltered flags and three matching deployment-ID markers. |
| 4. Same-GUID rerun | same-guid-rerun: full inventory, index IDs and CreatedUtc markers unchanged; no recreation/adoption. |
| 5. Partial recovery | partial-and-resume: injected 51991 before CREATE #2 left precisely one marked index; unmodified execution body with the same GUID completed the remaining two; rollback then removed all three. |
| 6. Existing equivalents | preexisting-equivalents: one candidate-name equivalent and one alternate-name equivalent with extra INCLUDE were preserved/unmarked; only missing third index was owned. Rollback and its repeat retained both pre-existing indexes. |
| 7. Incompatible definitions | incompatible-name: later candidate name had wrong definition; incompatible-overlap: alternate descending leading-key index. Both failed before candidate DDL and preserved the complete prior inventory. |
| 8. Wrong target | wrong-target: original application guard rejected disposable catalog; adapted guard rejected a different expected catalog. No application connection was opened. |
| 9. Missing/altered ownership | missing-package-marker: unclaimed index preserved, other owned indexes removed. missing-deployment-marker: corrupt-owned preflight failed, all inventory preserved. different-deployment-marker: other-GUID index preserved, matching owned indexes removed. |
| 10. Exact-shape guarded rollback | clean-and-verify removed only three owned candidates. changed-owned-definition disabled an owned candidate; rollback rejected safely without changing any inventory. |
| 11. Repeated rollback | repeated-rollback: second rollback was a no-op; an unrelated CustomerId index survived both. Equivalent-index case also repeated rollback safely. |
| 12. Deployment failure atomicity | create-before-marker-failure: injected 51992 after CREATE/pre-marker rolled back the new index; no candidate or ownership marker remained; normal same-GUID deployment then succeeded. |
| 13. Offline build behavior | offline-build-blocking: real CREATE waited behind a deliberately held UPDLOCK/HOLDLOCK test transaction, then succeeded after its release. Details below. |
| 14. Business behavior retained | business-regression: WorkItem -> Quote -> Invoice conversion, duplicate protection, linked invoice/quote deletion protection, payment, numbering, ordinary manual CREATE/UPDATE and unlinked paid/unpaid DELETE passed through existing services. |
| Additional approval/ownership gates | approval-gates rejected missing GUID and unconfirmed window; different-guid-rerun rejected adoption by a new deployment GUID; wrong-column-type rejected schema drift. |

## Runtime errors, transactions and offline wait

There were **no unexpected SQL binding, DDL, deadlock, timeout or rollback errors**.
Twelve expected custom errors were asserted: ten 51000 preflight/ownership/target
rejections, one injected 51991 (partial deployment), one injected 51992 (before
marker). They are passed negative-test outcomes, not failed tests.

The offline case held a synthetic table transaction before executing the package.
The captured blocked request was the actual CREATE:

```sql
CREATE NONCLUSTERED INDEX [IX_VehicleInvoice_WorkQuoteId]
ON dbo.[VehicleInvoice] ([WorkQuoteId]) INCLUDE ([GarageBusinessId])
WITH (ONLINE=OFF, MAXDOP=1, SORT_IN_TEMPDB=OFF)
```

Reader/build SPID 97, blocker SPID 79; wait LCK_M_S, snapshot wait_time 6 ms,
resource `OBJECT: 6:18099105:19`. These IDs are ephemeral test-only identifiers.
Diagnostics were restricted to the known worker/blocker and owned DB_ID. The
blocker was released in finally, workers were awaited/drained, and creation
completed. This establishes that the offline builder can wait behind existing
application-style locks. It does not measure a large build's duration or all
reader/writer blocking directions. A maintenance window is still required.

Both SQL timeout configuration and bounded synchronization remained intact; no
lock hints, indexes beyond the approved/test fault setup, isolation options or
business rules were changed in the application database.

## Files and other validation

Added IndexPackageRehearsalSqlTests.cs with an explicit opt-in and durable reports;
made only the existing test alignment/schema inspection methods internal for reuse.
Updated SQL-TESTS.md, package README, this report and package checksums. The four
SQL files and validator remain unchanged from the tested package. No production
services, EF mappings/entities, controllers, views, configuration or migrations
were modified. No commits were made.

Release build: 0 errors, 37 existing warnings. Default (all SQL opt-ins removed)
regression: **698 passed, 0 failed, 90 skipped**. This includes the 20 new cases
skipping before SQL setup. Offline package validation remains 10 parses with
zero syntax errors and 74 structural checks.

Detailed case reports persist beneath
`GarageSaas.UnitTests/TestResults/IndexPackageRehearsal/<database>/rehearsal.json`.
Test results: `index-package-runtime-smoke.trx`, `index-package-rehearsal.trx`,
and `index-package-rehearsal-non-sql.trx` in the UnitTests TestResults directory.
Per-case source hashes, SQL output, expected error messages and cleanup status are
retained. Reports from execution preserve the original README hash; subsequent
README/report documentation updates are reflected in the refreshed manifest and
do not change the tested SQL file hashes above.

## Disposable databases and cleanup

All 20 used the fixed prefix `GarageSaas_InvoiceAtomic_Test_` and a random GUID.
Every row below reported successful guarded deletion. A separate final master
catalog query confirmed **zero remaining harness-named databases**. No sessions
were forcibly killed and no database was adopted or reused.

| Scenario | Exact disposable database | Outcome / cleanup |
|---|---|---|
| approval-gates | GarageSaas_InvoiceAtomic_Test_d5b919028b644676b14bb6a404079940 | PASSED / guarded deletion succeeded |
| business-regression | GarageSaas_InvoiceAtomic_Test_b80405769bcd4a999d1c0bfac406af70 | PASSED / guarded deletion succeeded |
| changed-owned-definition | GarageSaas_InvoiceAtomic_Test_070f8dbccd8f4ce686339c9fb7c3c404 | PASSED / guarded deletion succeeded |
| clean-and-verify | GarageSaas_InvoiceAtomic_Test_be7cd945d26547988b69c9d2b3cf45bb | PASSED / guarded deletion succeeded |
| clean-and-verify | GarageSaas_InvoiceAtomic_Test_c913d259b60a4cdabe07f28fb15c7531 | PASSED / guarded deletion succeeded |
| create-before-marker-failure | GarageSaas_InvoiceAtomic_Test_fa694bee3904418db2a71e60ddaebb22 | PASSED / guarded deletion succeeded |
| different-deployment-marker | GarageSaas_InvoiceAtomic_Test_7f1b0996f9b84e5e9d890ca22892305c | PASSED / guarded deletion succeeded |
| different-guid-rerun | GarageSaas_InvoiceAtomic_Test_f1242cd20ed94e4bb1187b82b2140942 | PASSED / guarded deletion succeeded |
| incompatible-name | GarageSaas_InvoiceAtomic_Test_b3ee51af676247af85e8ecbcfd28068c | PASSED / guarded deletion succeeded |
| incompatible-overlap | GarageSaas_InvoiceAtomic_Test_e4f7c67f296c44fbac7c6dbfc557263a | PASSED / guarded deletion succeeded |
| missing-deployment-marker | GarageSaas_InvoiceAtomic_Test_6880708df8c24606b21d1803aba475f0 | PASSED / guarded deletion succeeded |
| missing-package-marker | GarageSaas_InvoiceAtomic_Test_8bf8e006af5a4febbb391ff0bd5e507c | PASSED / guarded deletion succeeded |
| offline-build-blocking | GarageSaas_InvoiceAtomic_Test_13441245dd744307a2fcaafef48165ca | PASSED / guarded deletion succeeded |
| partial-and-resume | GarageSaas_InvoiceAtomic_Test_09e8c8eb4ade4970b807938bf8a6e6b3 | PASSED / guarded deletion succeeded |
| preexisting-equivalents | GarageSaas_InvoiceAtomic_Test_021be9cbfeaf4cbaae1f4324d8563acf | PASSED / guarded deletion succeeded |
| preview | GarageSaas_InvoiceAtomic_Test_a33fc185d7ba4c62b91aac1e768fd9f0 | PASSED / guarded deletion succeeded |
| repeated-rollback | GarageSaas_InvoiceAtomic_Test_f1e2e066d2864897b414a59fa1a56f90 | PASSED / guarded deletion succeeded |
| same-guid-rerun | GarageSaas_InvoiceAtomic_Test_e14313eab12347f1b6112f76a98ef7ef | PASSED / guarded deletion succeeded |
| wrong-column-type | GarageSaas_InvoiceAtomic_Test_bb951f5f058e4179a81a53e786f2fcc5 | PASSED / guarded deletion succeeded |
| wrong-target | GarageSaas_InvoiceAtomic_Test_bbb55a027b12431aaa45dd8c4e334979 | PASSED / guarded deletion succeeded |
## Remaining limits and readiness

No deployment/preflight/rollback package was submitted to the existing Garagesaas
database. It received catalog SELECTs only for schema comparison, not business
queries, DDL or DML. The application-target guard is still unchanged and every
future application deployment requires separate explicit approval.

The package is ready for that approval process on this reviewed target, subject
to fresh preflight, current backup/recovery readiness, capacity/log headroom and
a coordinated maintenance window. Standard Developer is for development/test;
licensed production targets require their own identity/edition review.

Not exercised: real connection loss or commit-acknowledgement uncertainty,
permission-denial identities, disk/log exhaustion, arbitrary privileged concurrent
DDL/marker tampering, large production tables or all historical business variants.
The partial/marker tests do not establish behavior for every commit-uncertainty
schedule. Never automatically retry after an uncertain outcome. No live UI/B2C
login or production financial records were used. Schema and DDL coordination
freeze requirements and strict target guards remain mandatory.
