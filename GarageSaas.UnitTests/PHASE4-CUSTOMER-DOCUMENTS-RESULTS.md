# Phase 4 — Customer documents

Completed 19 October 2026. No application-database changes or migrations, no
indexes, no commits and no Phase 5 work. All test customers and recipients are
synthetic; no real customer email was sent.

## Existing infrastructure reused

- WorkQuoteService.GetWorkQuote and VehicleInvoiceService.GetVehicleInvoice retain
  existing tenant/relationship validation. CustomerDocumentService builds a detached
  read-only document model after these checks, with garage-scoped customer/vehicle
  lookups. Existing malformed relationship behavior is retained.
- Existing Q-{ID} quote references and persisted invoice numbers are reused.
- Quotes use stored subtotal/VAT/total without recalculating or inventing missing
  values. Invoices use the existing invoice-service calculation result. Existing
  price categories are itemised separately from work descriptions; no new line-item
  pricing model or tax policy was introduced. Missing legacy amounts say Not recorded.
- Existing MailKitEmailService SMTP configuration and delivery are reused. The new
  attachment method shares the existing message/SMTP path; invitation and ordinary
  email contracts and behavior remain available.
- Existing authorization, GarageContextFilter, automatic form context tag helper,
  antiforgery and Bootstrap/Razor layout are reused.

## PDF implementation and delivery

PDFsharp/MigraDoc 6.2.4 Core is the single PDF implementation. It is compatible with
this .NET application and MIT licensed. Noto Sans Regular/Bold are embedded under
OFL (license included) so deployment needs neither installed fonts nor browser
binaries. Its cryptography dependency is explicitly pinned to patched
System.Security.Cryptography.Pkcs 10.0.13 rather than the vulnerable minimum.

Primary references:
- https://docs.pdfsharp.net/General/Overview/NuGet-Packages.html
- https://www.nuget.org/packages/PDFsharp-MigraDoc/6.2.4
- https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.13
- https://github.com/notofonts/noto-fonts

A4 PDFs include garage identity/contact/VAT-registration details where stored,
customer contact/address, vehicle registration/make/model where available, correct
Quote/Invoice title and reference/date, work descriptions, existing charge categories,
VAT breakdown, totals, notes, due date and invoice payment status. Descriptions and
notes flow over pages; headers and page numbers repeat. Long unspaced codes are
broken for layout. Work descriptions are plain text, not interpreted HTML.

Actions appear on quote list/editor and invoice list/editor/printable-detail screens:
- GET /CustomerDocument/Download?kind=quote&id={quoteId}
- GET /CustomerDocument/Download?kind=invoice&id={invoiceId}
- GET /CustomerDocument/Send?kind={quote|invoice}&id={id}
- POST /CustomerDocument/Send with rendered antiforgery, garage context and send token.

Download returns application/pdf with a safe server-generated filename and no-store
caching. Open the downloaded file in the normal browser PDF viewer and use Print or
Ctrl+P. No print restrictions or encryption are applied. Downloads/sending use saved
data; users are told to save edits first. The email confirmation notes that the PDF
is generated from the latest saved document details when sending.

## Email behavior

Send to Customer opens a confirmation page displaying the stored customer and email.
No email is sent by GET. Missing/invalid addresses disable sending and offer the
existing customer-edit link. Posted arbitrary recipient fields are ignored: the
address is reloaded and validated on POST. Changed customer email requires a new
confirmation.

A random token is bound to user, selected garage, document kind/ID and recipient for
15 minutes. It is consumed once before PDF generation/delivery. The button disables
on submit. Repeated POSTs cannot resend from that confirmation. Authorization,
foreign/missing documents, antiforgery/context failures and PDF-generation failures
prevent email delivery. An SMTP exception produces an explicit uncertain-delivery
message, because provider acceptance followed by an acknowledgement failure is
possible. There are no automatic delivery retries. Success means the SMTP server
accepted the send; it is not an inbox-delivery receipt.

Tokens are in-memory, within one app instance. For multiple replicas use session
affinity for confirmation/submission; application restart or cache expiry requires
reopening confirmation. This is accidental-resubmission protection, not a durable
outbox or deduplication across newly opened confirmations.

## Changed files and methods

| File | Methods / changes |
| --- | --- |
| GarageSaas/Controllers/CustomerDocumentController.cs | New Download GET, Send GET/POST; shared Generate validation and Back redirect; safe feedback/logging. |
| GarageSaas/Services/CustomerDocumentService.cs | New Get, garage-scoped read-only document assembly and existing charge mapping. |
| GarageSaas/Services/Models/CustomerDocumentModel.cs | Document, charge and confirmation display models; safe generated filenames. |
| GarageSaas/Services/Interfaces/ICustomerDocumentPdfRenderer.cs | Generate renderer contract for focused failure tests. |
| GarageSaas/Services/CustomerDocumentPdfRenderer.cs | Generate A4 document with pagination, embedded font resolver and formatting helpers. |
| GarageSaas/Services/DocumentSendTokens.cs | Issue/Consume once-only confirmation tokens and ValidRecipient validation. |
| GarageSaas/Services/Interfaces/IEmailService.cs | Adds SendDocumentAsync; existing methods retained. |
| GarageSaas/Services/MailKitEmailService.cs | Adds SendDocumentAsync; SendEmailAsync shares extracted SendMessageAsync SMTP path. |
| GarageSaas/Filters/GarageContextFilter.cs | Registers CustomerDocument as a tenant controller, also enabling automatic form tokens. |
| GarageSaas/Startup.cs | Registers document service/renderer and singleton token service. |
| GarageSaas/GarageSaas.csproj | PDFsharp/MigraDoc and patched cryptography dependency; embedded font resources and published font license. |
| GarageSaas/Resources/Fonts/ | NotoSans-Regular.ttf, NotoSans-Bold.ttf, OFL.txt. |
| GarageSaas/Views/CustomerDocument/Send.cshtml | New recipient confirmation/error view with automatic form tokens and submit guard. |
| GarageSaas/Views/WorkQuote/Index.cshtml | Download PDF / Send to Customer links. |
| GarageSaas/Views/WorkQuote/AddEdit.cshtml | Saved quote document links and saved-data explanation. |
| GarageSaas/Views/VehicleInvoice/Index.cshtml | Document links. |
| GarageSaas/Views/VehicleInvoice/AddEdit.cshtml | Saved invoice document links and saved-data explanation. |
| GarageSaas/Views/VehicleInvoice/Detail_print.cshtml | Document links outside printable content. |
| GarageSaas.UnitTests/Services/CustomerDocumentTests.cs | PDF/model, VAT/totals, identity, tenant, null amounts, long descriptions, recipient and token tests. |
| GarageSaas.UnitTests/Controllers/CustomerDocumentHttpTests.cs | Real HTTP download/confirmation/send, authorization, foreign/missing IDs, feedback, duplicate submission, bad/changed recipient, PDF/provider failure and stale-garage tests. |
| GarageSaas.UnitTests/Services/CustomerDocumentEmailTests.cs | Actual MailKit loopback SMTP test verifies MIME attachment filename/type/decoded bytes. |
| GarageSaas.UnitTests/Controllers/GarageSwitchingHttpTests.cs | Reuses existing synthetic host; adds document dependencies, optional renderer/mail overrides and TempData-cookie support. |
| GarageSaas.UnitTests/TestHelpers/VerifyCustomerPdfs.py | Independent pypdf text/A4/pagination sample verification. |
| GarageSaas/Readme.md | Phase 4 operation/configuration notes. |

## Verification results

- Final solution build: succeeded, zero errors. Existing unrelated NuGet vulnerability,
  pruning and older test-source warnings remain; the new cryptography dependency's
  vulnerable minimum was patched. No new-source compiler warnings remain.
- Focused document + garage-switch HTTP tests: 47 passed, zero failed/skipped.
- Functional regression: 849 passed, zero failed/skipped. Existing quote/invoice,
  invitation, authorization and other functional coverage remains intact.
- Existing CoreWorkflowClient.test.cjs and GarageContextClient.test.cjs: passed.
- VerifyCustomerPdfs.py: passed for all three generated samples, confirming garage,
  customer with Irish diacritics, registration, references, VAT/currency amounts,
  A4 size, page counts, all 80 numbered descriptions and end-of-content markers.
- Poppler rendered every page. Visual inspection of the one-page quote, one-page
  invoice and all six pages of the long quote confirmed readable layout, no overlap
  or clipping, repeated headers, totals and correct page-number footers.
- git diff --check: passed.

Evidence:
- .test-results/phase4-build.log
- .test-results/phase4-focused.trx
- .test-results/phase4-regression.trx
- .test-results/phase4-pdf.trx
- output/pdf/quote.pdf
- output/pdf/invoice.pdf
- output/pdf/multipage-quote.pdf

Focused command:

```powershell
dotnet test GarageSaas.UnitTests --no-build --no-restore --filter 'FullyQualifiedName~CustomerDocument|FullyQualifiedName~GarageSwitchingHttpTests' --logger 'trx;LogFileName=phase4-focused.trx' --results-directory .test-results
```

Regression command:

```powershell
dotnet test GarageSaas.UnitTests --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName!~Sql&FullyQualifiedName!~Coordination&FullyQualifiedName!~Interleaving&FullyQualifiedName!~Transaction&FullyQualifiedName!~Concurrency&FullyQualifiedName!~IndexPackage' --logger 'trx;LogFileName=phase4-regression.trx' --results-directory .test-results
```

SQL and concurrency programmes were excluded. This phase adds read-only EF queries
and email/PDF behavior; no database-specific change required another disposable SQL
programme. The existing Garagesaas database was not accessed or modified.

## Manual acceptance and configuration

No implementation blocker remains. External provider delivery/inbox acceptance and
interactive browser/device acceptance require the configured running application.

1. Restore packages, rebuild and restart the application. No migrations or extra PDF
   executable/browser service is required. Fonts are embedded in the application.
2. Use HTTPS and the existing authenticated garage selection. Choose a saved quote
   or invoice, download the PDF and open/print it in the browser's PDF viewer. Check
   against the stored document amounts; save pending edits before generating.
3. For development the existing EmailSettings are localhost:1025, no TLS/credentials.
   Run your development SMTP capture service there and inspect the attached PDF.
4. For a real provider configure EmailSettings__SmtpHost, __SmtpPort, __SenderName,
   __SenderEmail, __Username, __Password and __UseStartTls through the established
   environment/secret configuration. Use provider-authorized sender credentials and
   the existing production STARTTLS policy. No secrets were added to source.
5. In Send to Customer, verify the stored recipient, confirm once and check feedback.
   Inspect the inbox or mail catcher and print the attachment. Check Spam/provider
   logs if the server accepts mail but inbox delivery is delayed.
6. Use one app instance or session affinity for the short-lived send confirmation.
   If the application restarts, open a new confirmation. After uncertain delivery,
   check with the recipient/provider before deliberately trying again.

No real-provider or browser-extension configuration was changed. The PDF samples
are synthetic verification artifacts, not actual customer documents.

## Garage logo follow-up

CustomerDocumentPdfRenderer now embeds the garage profile's existing uploaded
JPG/PNG beside the title on both quote and invoice PDFs, including email attachments.
The header preserves the image aspect ratio within a 4 cm x 1 cm area. Standard
synthetic documents still fit one page. Only local uploads matching the current
garage ID and existing upload filename format are eligible; remote URLs, arbitrary
paths and other garages' upload filenames are ignored. Missing/corrupt images leave
the PDF usable. No upload, database or financial behavior was changed.

Changes: CustomerDocumentPdfRenderer constructor/AddTitle/ReadLogo, new
CustomerDocumentLogoTests, two synthetic image fixtures and test output-copy entries.
Build succeeded; 38 focused customer-document tests passed. PNG and JPG embedding,
missing/corrupt images, foreign image references, traversal and remote references
were covered. Both logo samples were rendered and visually inspected; independent
PDF inspection confirmed one embedded image and one A4 page per sample.
Evidence: .test-results/phase4-logo.trx; output/pdf/quote-with-logo.pdf and
output/pdf/invoice-with-logo.pdf. Restart the running application to use the rebuilt
renderer. Use the garage profile's existing logo upload to select/change the image.
