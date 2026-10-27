# Post-code-flow sign-in failure — 27 October 2026

Read-only log investigation after user sign-in attempts. The previous AADB2C90057 pre-login provider error was replaced by a callback/token-response validation failure in ASP.NET Core's OpenID Connect middleware.

Recent event times 2026-10-26T23:58:36Z and 2026-10-27T00:00:42Z identify IDX21336 and Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectProtocolException. Sanitized description: Both id_token and access_token should be present in OpenIdConnectProtocolValidationContext.ProtocolMessage received from Token Endpoint. Cannot process the message.

This establishes progress to processing a token endpoint response, NOT a successfully authenticated/validated application session. The error check alone does not prove which token is missing. Current emitted scopes are openid profile; B2C's known code-flow behavior with only OIDC scopes can omit an access token. The current configuration is consistent with that known cause. The approved NameIdentifier remains unverified and no SQL binding should occur.

Primary implementation discussion: https://github.com/dotnet/aspnetcore/issues/23284 describes requesting the application's own client ID as an additional B2C scope to obtain the access token required by middleware. A focused potential correction is to add only the staging client ID 6a6b59d3-6db1-4124-86b1-d43c457dd17e to existing openid/profile scopes, retaining code + PKCE and all validation. Confirm named-option binding locally and obtain explicit approval before applying a staging setting or code change. Do not disable token validation, revert to implicit flow, add Graph User.Read permissions or modify existing B2C registrations/policies/users.

No configuration, secrets, account, database, firewall or application changes performed in this investigation. No passwords, token values, cookies or authorization codes read/output/saved. Further callback, validated identity and garage tests remain blocked; synthetic user 1 was not bound.
