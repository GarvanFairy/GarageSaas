using System;
using System.Net;
using System.Threading.Tasks;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GarageSaas.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class CustomerDocumentController : Controller
    {
        private readonly ICurrentGarageUserService _current;
        private readonly CustomerDocumentService _documents;
        private readonly ICustomerDocumentPdfRenderer _pdf;
        private readonly IEmailService _email;
        private readonly DocumentSendTokens _tokens;
        private readonly ILogger<CustomerDocumentController> _logger;
        public CustomerDocumentController(ICurrentGarageUserService current, CustomerDocumentService documents,
            ICustomerDocumentPdfRenderer pdf, IEmailService email, DocumentSendTokens tokens, ILogger<CustomerDocumentController> logger)
        { _current = current; _documents = documents; _pdf = pdf; _email = email; _tokens = tokens; _logger = logger; }

        [HttpGet]
        public IActionResult Download(string kind, int id)
        {
            var user = _current.GetCurrentUser();
            var result = _documents.Get(kind, id, user.GarageBusinessId);
            if (!result.Success) return NotFound();
            try { return File(Generate(result.Data), "application/pdf", result.Data.FileName); }
            catch (Exception error)
            {
                _logger.LogError(error, "PDF generation failed for {Kind} {DocumentId} in garage {GarageId}", kind, id, user.GarageBusinessId);
                TempData["Error"] = "The PDF could not be generated. Please try again or contact support.";
                return Back(kind, id);
            }
        }

        [HttpGet]
        public IActionResult Send(string kind, int id)
        {
            var user = _current.GetCurrentUser();
            var result = _documents.Get(kind, id, user.GarageBusinessId);
            if (!result.Success) return NotFound();
            var document = result.Data;
            var model = new SendCustomerDocumentModel { Document = document };
            if (!DocumentSendTokens.ValidRecipient(document.Recipient))
                model.Error = "This customer has no valid email address. Update the customer record before sending.";
            else model.SendToken = _tokens.Issue(user.UserId, user.GarageBusinessId, kind, id, document.Recipient);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(string kind, int id, string sendToken)
        {
            var user = _current.GetCurrentUser();
            var result = _documents.Get(kind, id, user.GarageBusinessId);
            if (!result.Success) return NotFound();
            var document = result.Data;
            if (!DocumentSendTokens.ValidRecipient(document.Recipient))
            {
                TempData["Error"] = "The customer email address is invalid. Update the customer record before sending.";
                return Back(kind, id);
            }
            if (!_tokens.Consume(sendToken, user.UserId, user.GarageBusinessId, kind, id, document.Recipient))
            {
                TempData["Error"] = "This send request was already used, expired, or customer details changed. Open Send to Customer again to confirm a new request.";
                return Back(kind, id);
            }
            byte[] bytes;
            try { bytes = Generate(document); }
            catch (Exception error)
            {
                _logger.LogError(error, "PDF generation failed before email for {Kind} {DocumentId}", kind, id);
                TempData["Error"] = "The PDF could not be generated. No email was sent.";
                return Back(kind, id);
            }
            try
            {
                await _email.SendDocumentAsync(document.Recipient, $"{document.Title} {document.Number}",
                    $"<p>Please find your {document.Title.ToLowerInvariant()} {WebUtility.HtmlEncode(document.Number)} attached.</p>" +
                    $"<p>{WebUtility.HtmlEncode(document.Garage.GarageBusinessName)}</p>", document.FileName, bytes);
                TempData["Success"] = $"{document.Title} sent to {document.Recipient}.";
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Document email delivery could not be confirmed for {Kind} {DocumentId}", kind, id);
                TempData["Error"] = "Email delivery could not be confirmed. Check with the customer before sending again; the provider may have accepted it.";
            }
            return Back(kind, id);
        }

        private byte[] Generate(CustomerDocumentModel document)
        {
            var bytes = _pdf.Generate(document);
            if (bytes == null || bytes.Length < 5 || System.Text.Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
                throw new InvalidOperationException("PDF generation returned no valid document.");
            return bytes;
        }
        private IActionResult Back(string kind, int id) => RedirectToAction("AddEdit", kind == "quote" ? "WorkQuote" : "VehicleInvoice", new { id });
    }
}
