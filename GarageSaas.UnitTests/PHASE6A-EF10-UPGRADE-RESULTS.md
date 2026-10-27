# Phase 6A — EF Core 10 upgrade

## Changes

- GarageSaas, SignupAPI and tests retain net10.0.
- SQL Server, Relational, Tools and InMemory references move from 3.1.32 to 10.0.13 (latest stable servicing release checked on 20 October 2026).
- Resolved Microsoft.Data.SqlClient moves from 1.1.3 to 6.1.7.
- SignupAPI Roslyn references move from 4.4.0 to 5.0.0 because EF Tools/Design requires CSharp.Workspaces >=5.0.0.
- SignupAPI JsonWebTokens moves from 6.14.1 to 7.7.1 to satisfy SqlClient. Resolved Tokens, Protocols and System.IdentityModel.Tokens.Jwt are also 7.7.1.
- SignupAPI explicitly uses System.Security.Cryptography.Pkcs 10.0.13, matching GarageSaas, rather than the vulnerable 9.0.11 newly pulled through SqlClient.
- SignupContext.OnModelCreating replaces five HasName calls with HasDatabaseName, preserving every literal physical index name.
- Eleven existing test files supply omitted required fixture values: garage names, user first/last names and GarageOwned=false. EF InMemory null validation remains enabled.
- New EfUpgradeAuthenticationTests checks existing Microsoft.Identity.Web/OIDC registration and synthetic signed JWT validation with the resolved dependencies.
- New EfUpgradeWorkflowSqlTests exercises customer and vehicle creation, work, quote, conversion, financial persistence, payment and history using the guarded disposable SQL fixture.

No application LINQ, ToList boundaries, table/column names, relationships, monetary precision/calculations, transactions, lock SQL or rollback implementation changed. No migrations were generated/run; existing migrations and snapshots are untouched. No existing Garagesaas database was accessed or changed. No deployed indexes changed. No commits made.

## Verification

Baseline: EF 3.1.32 Release regression run: 1,077 passed, 0 failed, 212 skipped.
EF 10 final verification after PKCS alignment: 1,079 passed, 0 failed, 212 skipped. See .test-results/ef10-verified.trx and ef10-verified.log.
All 1,077 previously passing cases also passed under EF 10 (comparison of named TRX outcomes).
Release build: 0 errors; existing compiler/package warnings remain.
JavaScript checks: CoreWorkflowClient, GarageContextClient and QuoteSuggestionsClient passed.

Disposable SQL cases (32):
- CustomerVehicleTenantReadSqlTests: 13 ownership/list scenarios, including missing lookup rows, duplicates, foreign and dangling associations.
- InvoiceUpdateSqlTests: 13 existing persistence/rollback/coordination cases.
- QuoteConversionSqlTests: 5 existing conversion/numbering/rollback cases.
- EfUpgradeWorkflowSqlTests: full workflow, 100.00 labour => 123.50 total persisted; Paid status/date reloaded; service history contains the correct work; foreign garage reads/payment rejected.

The same 31 pre-existing SQL cases passed on EF 3.1 and EF 10 against independently created disposable schemas. This compares expected service/query results, not byte-for-byte generated SQL or performance. The broader opt-in SQL/index/concurrency programmes remain disabled.

Existing HTTP authorization, tenant switching, customer-document PDF/logo/multipage and mocked email success/failure tests are included in the regression suite. No real customer email, live OpenAI request or external B2C sign-in was performed.

## SQL behavior and deployment acceptance

EF 10 generates OUTPUT INSERTED for SQL Server writes: the new SQL test captures and verifies this, as well as existing UPDLOCK statements. No OUTPUT failure occurred on the disposable model schema. Existing rollback tests pass.
SqlClient now requests encryption by default. The new SQL test verifies the non-Optional encryption setting; disposable LocalDB uses TrustServerCertificate=true deliberately. Application connection strings and certificate-validation settings were not weakened or changed.

The existing deployed database was not inspected. Consequently its certificate trust, server compatibility level, triggers and computed columns are not certified by these tests. Before deployment, verify these against an approved environment with matching schema: trusted TLS certificate and encrypted connectivity; SaveChanges on relevant tables. Tables with incompatible triggers/computed columns may need table-specific UseSqlOutputClause(false) mapping configuration (no physical schema change); do not add it speculatively. No such incompatibility was demonstrated here.

Retained package warnings for legacy browser assets (jQuery/Moment) and redundant older framework references are outside this focused upgrade. Full real-provider B2C and SMTP acceptance remains a deployment check.

## Reproduce disposable checks

Set GARAGESAAS_TEST_SQLSERVER=(localdb)\GarageSaasIntegrationTests and these process-local flags to 1:
GARAGESAAS_RUN_CUSTOMER_VEHICLE_READ_SQL_TESTS
GARAGESAAS_RUN_QUOTE_CONVERSION_SQL_TESTS
GARAGESAAS_RUN_INVOICE_UPDATE_SQL_TESTS
GARAGESAAS_RUN_EF_UPGRADE_SQL_TESTS

Run dotnet test GarageSaas.UnitTests --no-restore -c Release -m:1 -p:UseSharedCompilation=false.
The fixture verifies the dedicated local instance, rejects Garagesaas, creates only generated GarageSaas_*_Test_<guid> catalogs, and checks ownership before cleanup. Its EnsureCreated is used only inside these owned disposable catalogs, not as a migration.
