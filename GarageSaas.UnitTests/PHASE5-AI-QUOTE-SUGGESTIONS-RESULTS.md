# Phase 5 — AI work-quote description assistance

Completed 19 October 2026. Scope: a click-to-generate writing suggestion in the
existing Work Quote work-item modal. No live/billable OpenAI request was made.
No existing Garagesaas database access or changes, migrations, indexes, commits,
new tables, autonomous agents or Phase 6 work.

## Integration and model

The application uses a typed IHttpClientFactory client in C# to call the OpenAI
Responses API at the fixed HTTPS endpoint https://api.openai.com/v1/responses.
No new SDK package, separate backend, tool calls or quote-business-logic changes.
The model is configurable and defaults to the requested gpt-5-mini. Reasoning is
minimal and text verbosity low. The request contains only a controlled instruction
and the trimmed mechanic's draft in a user input message. store=false is set; no
quote/customer/garage IDs, contact records, histories or conversation IDs are sent.
store=false is not a claim of zero provider retention.

Official documentation checked on 19 October 2026:
- https://developers.openai.com/api/docs/models/gpt-5-mini
- https://developers.openai.com/api/docs/guides/text
- https://developers.openai.com/api/docs/guides/reasoning
- https://developers.openai.com/api/reference/resources/responses/methods/create

The model page lists gpt-5-mini and its pricing but marks it deprecated. The explicit
requested default was retained; no replacement model was silently selected. Access
for the deployment's API account, actual latency and output quality remain live
acceptance checks. If changing the configured model, select one compatible with the
minimal-reasoning/low-verbosity Responses request, and verify its pricing/access.

## Configuration and key setup

Non-secret defaults are in GarageSaas/appsettings.json:

```json
"OpenAI": {
  "Model": "gpt-5-mini",
  "TimeoutSeconds": 15,
  "MaxOutputTokens": 200,
  "MaxInputCharacters": 1000,
  "MaxSuggestionCharacters": 1000,
  "RequestsPerMinute": 5,
  "GarageRequestsPerMinute": 20
}
```

OpenAI:ApiKey is supplied separately through existing secure configuration. It is
not written to source, JavaScript, Razor, response bodies or public settings. A
UserSecretsId was added to the application project so default Development hosting
can load local .NET User Secrets:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>" --project GarageSaas/GarageSaas.csproj
```

Alternatively set OpenAI__ApiKey in the application process environment. On Azure,
use the existing secure App Service application settings or a Key Vault reference
for OpenAI__ApiKey. Do not place keys in wwwroot or tracked appsettings files.
No key is required for startup, quote editing, builds or automated tests.

Limits are validated when requesting a suggestion, not at application startup.
Supported pilot configuration ranges: timeout 1–60 seconds, output 16–1000 tokens,
input 1–4000 characters, suggestion 1–2000 characters, requests 1–20 per minute.
Invalid settings disable suggestions safely without blocking normal editing.

## UI and saving behavior

Open Work Quotes, create/edit a quote, select the customer/vehicle as usual and
open Add Work Item or edit an existing work item. Beside Repair Instructions:

1. Enter a draft and click Suggest Description.
2. A spinner/status appears and repeat clicks are blocked while waiting.
3. The suggestion appears separately; the original remains unchanged.
4. Choose Use Suggestion to insert it, or Dismiss to keep the original.
5. Edit the inserted wording if needed.
6. Save Work Item and save the quote through the existing workflow.

There is no request on keystrokes. Editing the draft, dismissing/closing/reopening
the modal invalidates pending results. Late results cannot replace another draft.
The text is displayed with textContent, not HTML. No totals, rates, parts, VAT or
invoice relationships are modified. An AI error does not disable manual editing
or Save Work Item. Generating/accepting a suggestion itself performs no database
write; only existing explicit save actions persist the selected wording.

The existing responsive Bootstrap modal and wrapping controls are retained. Desktop
and mobile interaction/viewport acceptance is still a manual browser check; automated
UI checks exercise the JavaScript state transitions without claiming device testing.

## Endpoint protection, privacy and failures

POST /QuoteSuggestion/Suggest accepts description and optional workQuoteId (0 for
new quotes). It requires authentication, current eligible garage membership and
availability, the existing garage-context token and antiforgery validation. A
client-supplied garage ID is ignored. Existing quote IDs must pass the established
selected-garage quote-service validation before the provider can be called. The
existing resource filter blocks stale AJAX after garage switching. The request
body is limited to 16 KB and responses are no-store.

Whitespace-only and excessive drafts fail before provider access. Conservative
checks reject likely emails, phone numbers, Irish/UK registrations, VINs, Eircodes,
common address patterns, identifying labels and honorific/name patterns. Existing
quotes also reject their known customer name or vehicle registration appearing in
the draft. The UI explicitly asks users to supply repair details only. These checks
are proportionate pilot safeguards, NOT a guarantee that arbitrary text contains
no personal information; unlabelled names or unusual identifiers may go undetected.
No separate customer/garage records are intentionally included in the prompt.

The instruction requests concise Irish/British English and prohibits invented
findings, completed work, additional services, parts, labour, prices, guarantees or
estimates. The draft is a lower-priority user message, treated as data rather than
application instructions. Money/labour-hour estimates in returned text are also
rejected. The mechanic must review accuracy; prompt rules are not a factual warranty.

Only completed assistant output_text content is accepted. Empty, malformed,
refused, oversized, overlength and incomplete results fail without returning partial
suggestions. Provider response buffers are capped at 64 KB. Missing credentials,
auth failures, rate limits, timeouts/cancellation, network and provider failures
return safe feedback without forwarding provider error bodies. No API key, prompt
or identifying draft text is logged by the new code. HTTP redirects are disabled
so credentials are not forwarded to another host. No application retries are added.

Rate limiting uses existing IMemoryCache with a fixed one-minute per-user window
(default five attempts across selected garages). Invalid/unauthorized requests do
not consume provider calls. Valid attempts, including failures, consume the local
allowance. Limits reset on app restart and apply per process; a future multi-replica
pilot would need an agreed shared limit if a global cap is required. This is a simple
pilot safeguard, not a durable billing system.

## Current pricing and estimated cost

The official gpt-5-mini model page lists standard text pricing at USD $0.25 per
million input tokens and $2.00 per million output tokens. Cached input is $0.025
per million; estimates here assume no cache savings.

Illustrative estimates (not measured live usage):
- 200 input + 80 total output tokens: about $0.00021 per suggestion.
- 500 input + the full 200-token output allowance: about $0.000525 per attempt,
  or about $0.525 for 1,000 similarly sized attempts.

Actual tokens vary, including instruction text and reasoning tokens, which count
within max_output_tokens and are billed as output. These figures are estimates,
not a monthly cost guarantee or a tokenization bound for arbitrary Unicode text.
Timeout/cancellation/incomplete responses can still incur provider usage. No
automatic retry follows a failure. If live tests repeatedly hit the 200-token cap,
review output usage before deliberately increasing MaxOutputTokens (e.g. 400–512);
partial output is never inserted and the application does not raise the cap itself.

## Files and methods changed

| File | Methods / changes |
| --- | --- |
| GarageSaas/Configuration/OpenAIOptions.cs | New configuration defaults and ValidLimits. |
| GarageSaas/Services/Interfaces/IQuoteSuggestionService.cs | SuggestAsync abstraction. |
| GarageSaas/Services/OpenAIQuoteSuggestionService.cs | SuggestAsync builds a bounded Responses request, extracts completed text and maps safe errors; parsing helpers. |
| GarageSaas/Services/Models/QuoteSuggestionResult.cs | Request/result models and safe Ok/Fail factories. |
| GarageSaas/Services/QuoteSuggestionInput.cs | Validate for input bounds, likely identifiers and known saved-quote identifiers. |
| GarageSaas/Services/QuoteSuggestionRateLimiter.cs | TryAcquire using a per-user cached fixed window. |
| GarageSaas/Controllers/QuoteSuggestionController.cs | New protected Suggest POST and Error response helper. No persistence. |
| GarageSaas/Filters/GarageContextFilter.cs | Registers QuoteSuggestion as an affected tenant controller. |
| GarageSaas/Startup.cs | ConfigureServices registers options, singleton limiter and typed HttpClient with redirects disabled. |
| GarageSaas/GarageSaas.csproj | UserSecretsId enables local Development secret configuration. No new packages. |
| GarageSaas/appsettings.json | Non-secret OpenAI defaults only. |
| GarageSaas/Views/WorkQuote/AddEdit.cshtml | Existing work-item modal gets suggest/loading/preview/accept/dismiss controls, antiforgery token, privacy/review notice and script reference. |
| GarageSaas/wwwroot/js/quote-suggestions.js | Explicit-request UI, stale-result cancellation, safe preview, acceptance and failure handling. |
| GarageSaas.UnitTests/Services/QuoteSuggestionServiceTests.cs | Mock HttpMessageHandler provider-contract, validation, privacy, errors, timeout, output and cost-setting tests. |
| GarageSaas.UnitTests/Controllers/QuoteSuggestionHttpTests.cs | Real loopback MVC tests for new/existing quotes, auth, eligibility, foreign IDs, antiforgery, stale contexts, limits, no persistence and missing configuration. |
| GarageSaas.UnitTests/Controllers/GarageSwitchingHttpTests.cs | Existing host gains injectable suggestion mock/options/limiter and identity-correct JSON antiforgery support. |
| GarageSaas.UnitTests/TestHelpers/QuoteSuggestionsClient.test.cjs | Mock DOM/fetch checks for UI states, accept/edit/dismiss, stale results, safe text and manual fallback. |
| GarageSaas/Readme.md | Pilot usage/configuration notes. |

## Build and test evidence

- Solution Release build: succeeded, zero errors. Existing dependency warnings and
  older test-source warnings remain outside this phase; no new dependency added.
- Selected Release unit/HTTP/integration regressions: **182 passed, zero failed,
  zero skipped**. Selection includes AI tests, full customer-to-quote-to-invoice
  workflow, quote HTTP/service/validation/modification and garage switching.
- QuoteSuggestionsClient.test.cjs: passed.
- CoreWorkflowClient.test.cjs: passed.
- GarageContextClient.test.cjs: passed.
- git diff --check: passed.
- No live OpenAI requests, SQL fixtures or concurrency programme executed.

Evidence: .test-results/phase5-build.log, .test-results/phase5-focused.log and
.test-results/phase5-focused.trx.

```powershell
dotnet build GarageSaas.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false
dotnet test GarageSaas.UnitTests --no-restore -c Release -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~QuoteSuggestion|FullyQualifiedName~CoreGarageWorkflowTests|FullyQualifiedName~WorkQuoteTenantHttpTests|FullyQualifiedName~GarageSwitchingHttpTests|FullyQualifiedName~WorkQuoteServiceTests|FullyQualifiedName~WorkQuoteModificationTests|FullyQualifiedName~WorkQuoteTenantValidationTests' --logger 'trx;LogFileName=phase5-focused.trx' --results-directory .test-results
node GarageSaas.UnitTests/TestHelpers/QuoteSuggestionsClient.test.cjs
node GarageSaas.UnitTests/TestHelpers/CoreWorkflowClient.test.cjs
node GarageSaas.UnitTests/TestHelpers/GarageContextClient.test.cjs
```

## Manual acceptance with a real account

1. Confirm the project/account can use the configured gpt-5-mini model in the OpenAI
   dashboard. It is deprecated in current documentation; account access was not
   verified. Configure an available compatible model explicitly if needed, reviewing
   its pricing rather than silently falling back.
2. Set the API key using User Secrets above or OpenAI__ApiKey in secure configuration.
   Restart the running app so it loads the settings. No migration is required.
3. Sign into an eligible garage. Open a new quote and Add Work Item. Enter only a
   short non-identifying draft such as Replace front brake pads.
4. Click Suggest Description once. Verify a useful concise future-work suggestion,
   original text unchanged, and disabled/loading state during the request.
5. Use Suggestion, edit the inserted wording, Save Work Item, then save/reopen the
   quote normally. Check existing totals, VAT and invoice relationships unchanged.
6. Repeat on an existing quote/work item. Generate and Dismiss, checking the original
   remains. Change the draft/close the modal while waiting and ensure stale output
   cannot appear in another work item.
7. Remove the key or temporarily make the API unavailable; confirm safe feedback and
   ordinary manual work-item/quote editing and saving remain available.
8. Inspect browser Network requests: only the local QuoteSuggestion endpoint receives
   draft + quote ID, antiforgery and garage-context data. The OpenAI API key and
   Authorization bearer header must never appear in browser traffic or page source.
9. Check desktop and narrow mobile viewport: modal scrolling, wrapping buttons,
   readable preview and keyboard/focus behavior. Confirm a switched-garage stale
   request is rejected, with no quote changes caused by suggestion generation.

No implementation blocker remains. Genuine outstanding acceptance items are API
account/model access, live suggestion quality/latency at the 200-token cap and
interactive browser/mobile testing. Arbitrary-text privacy detection and per-process
rate limits are deliberately documented pilot limitations. No Phase 6 work started.


## Multi-tenant shared-account isolation follow-up

The MVP uses one centrally configured server-side OpenAI API key. No garage API
accounts, per-garage credentials, tables, conversation stores or AI infrastructure
were introduced. The text-only provider service still receives no garage ID.

Request isolation:
- The controller resolves the selected garage from CurrentGarageUserService on each
  authenticated request. Existing membership/availability, antiforgery, quote-graph
  tenant checks and stale-context protections remain mandatory.
- Client garage/user IDs cannot choose authorization, quota or metrics scope. The
  validated garage ID is captured before awaiting the provider call and retained
  for completion accounting.
- Every provider request contains the fixed writing instructions and one current
  draft only, with store=false. There are no previous_response_id, conversation,
  assistant/thread references, metadata, tenant IDs or accumulated output messages.
  HTTP cookie sharing is now explicitly disabled on the provider client as well as
  redirects. The shared API key is the only account credential used.
- Suggestions are never cached, reused or saved by the AI endpoint. Only numeric
  quota/usage counters use IMemoryCache. Matching draft text still makes a fresh
  provider call. No customer records, vehicle registrations or quote histories are
  automatically included; the existing privacy checks and a confidential-information
  reminder remain in the editor. Arbitrary free text is not guaranteed anonymous.
- Provider errors remain sanitized. Unexpected provider-service exceptions are
  converted to generic feedback without logging exception content. Usage snapshots
  contain no prompt, suggestion, response ID, customer or provider-error strings.

Quotas:
- OpenAI:RequestsPerMinute retains the existing five-per-user limit across garages.
- New OpenAI:GarageRequestsPerMinute defaults to 20 (valid range 1–1000), shared by
  all users of that garage in a fixed one-minute window. Both limits must allow a
  request before either counter is incremented. A garage hitting its limit does not
  consume another garage's allowance. All identities are server resolved.

Internal metrics:
- New QuoteSuggestionUsage records Started, Succeeded, Failed, Limited, InputTokens,
  OutputTokens and WithReportedUsage separately for each validated garage ID.
- Started counts accepted application attempts, not a guarantee the provider was
  reached or charged. Provider-reported token counts are collected when available,
  including incomplete responses whose text is rejected. Missing token usage is not
  invented; WithReportedUsage indicates how many responses supplied token counts.
- Counters expire after 24 hours from their first observation. They are per-process,
  best-effort diagnostic counters, reset by restart/eviction, not a billing ledger.
  There is no metrics HTTP endpoint or UI exposing another garage's counters.
- The quota and metrics limits are process local. A single-instance pilot is covered;
  multiple app replicas would need a shared quota store for an installation-wide
  hard cap. No distributed infrastructure was added in this lightweight change.
- Stateless request assembly and store=false are not a promise of zero provider
  retention. The provider's account data policies still apply.

Changed methods/files for this follow-up:
- OpenAIOptions / appsettings: GarageRequestsPerMinute and configuration validation.
- QuoteSuggestionRateLimiter.TryAcquire(userId, garageId): atomic user + garage caps.
- QuoteSuggestionUsage: numeric per-garage Started/Limited/Completed/Snapshot methods.
- QuoteSuggestionResult.WithUsage and OpenAIQuoteSuggestionService.Tokens/Finish:
  preserve optional reported input/output counts without changing browser responses.
- QuoteSuggestionController.Suggest: trusted garage attribution, limits, outcome
  accounting and safe unexpected-provider failure handling.
- Startup: singleton usage service and provider UseCookies=false.
- Quote editor: adds the confidential garage-information reminder.
- QuoteSuggestionIsolationTests: five focused tests for shared garage quota, retained
  user quota, separate immutable usage snapshots, stateless shared-key calls without
  suggestion caching and incomplete-response token accounting.
- QuoteSuggestionHttpTests: three additional real HTTP cases for separate results /
  metrics despite spoofed IDs, safe unexpected exceptions and independent garage caps.
- GarageSwitchingHttpTests host: registers usage service, exposes internal test-only
  snapshots and permits a test-only quota configuration callback.

Final verification: **190 selected Release tests passed, zero failed/skipped**.
This includes the prior AI, tenant authorization, stale-context and quote workflow
regressions. All three JavaScript compatibility checks pass. The Release solution
build succeeds; existing dependency/test warnings remain unrelated. No SQL tests,
live OpenAI requests, database changes, migrations, commits or Phase 6 work.
Evidence: .test-results/phase5-tenant-isolation.trx and
.test-results/phase5-tenant-isolation.log. The same Phase 5 regression filter above
selects these newly added isolation tests through FullyQualifiedName~QuoteSuggestion.

## Model configuration update — 20 October 2026

Supersedes the original gpt-5-mini model, reasoning and pricing discussion above.
The active appsettings model and OpenAIOptions default are now gpt-6-luna.
SuggestAsync uses reasoning.effort=none (minimal is unsupported by Luna), retaining
low verbosity, the 200-token output cap, stateless input and store=false.
Official compatibility: https://developers.openai.com/api/docs/models/gpt-6-luna
The original pricing examples above apply only to the original model.

The existing Host.CreateDefaultBuilder and project UserSecretsId automatically
load OpenAI:ApiKey from User Secrets in Development. Startup binds the OpenAI
section to OpenAIOptions; the service uses that server-side key in the Authorization
header and the configured model in the request body. No key was read, displayed,
committed or sent to the live API during this update. Restart the application after
configuration changes. Production must supply the key through secure deployment
configuration. Account access and real output quality require manual acceptance.
