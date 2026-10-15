# GarageSaas

Garage management system built with ASP.NET Core MVC.

## Features
- Customer management
- Vehicle management
- Garage operations

## Tech Stack
- ASP.NET Core MVC
- Entity Framework
- SQL Server

## Owner onboarding invitations

Set `Onboarding__PublicOrigin` in the deployment environment (or `Onboarding:PublicOrigin`
in configuration) to the application's trusted HTTPS origin, for example
`https://garage.example.com`. Use an origin only: no path, credentials, query, or fragment.
There is deliberately no production default or fallback to the request Host header.
Approval and resend fail before changing the request if this setting is missing or invalid.
Configure production SMTP to require TLS. The existing local development SMTP option is unchanged.

Registration decisions, resend, and completion use SQL Server update/range locks in a
short transaction. Email is sent after commit. Resend rotates the token, invalidating
previous links, with a one-minute cooldown and a stale-form expiry check. Revoked and
completed invitations cannot be resent. Original approval audit fields are preserved.

An SMTP timeout can mean delivery is unknown. A failed send never restores the old
token or rolls back approval. Refresh details, wait one minute, and resend if needed.
Only the latest link works; email ordering/delivery is not guaranteed. There is no
durable email outbox or delivery history. Do not log invitation URLs or request query
strings at the reverse proxy/APM layer. No raw tokens or token hashes are logged by
the invitation services. Database/commit failures are not automatically retried;
refresh details to establish the current state before repeating a decision.

To include the SQL Server concurrency tests, set `GARAGESAAS_TEST_SQLSERVER` to an
isolated server name such as `(localdb)\MSSQLLocalDB`, then run `dotnet test GarageSaas.sln`.
These tests create and delete a randomly named `GarageSaas_Test_*` database using
integrated authentication. They never use the application's connection string.
Without that environment variable, the SQL tests are marked inconclusive; ordinary
unit tests still run. No production migrations or schema changes are required.
