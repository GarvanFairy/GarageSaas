using System.Threading.Tasks;
using GarageSaas.Configuration;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GarageSaas.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class QuoteSuggestionController : Controller
    {
        private readonly ICurrentGarageUserService _current;
        private readonly IWorkQuoteService _quotes;
        private readonly IQuoteSuggestionService _suggestions;
        private readonly QuoteSuggestionRateLimiter _limiter;
        private readonly OpenAIOptions _options;
        private readonly QuoteSuggestionUsage _usage;
        public QuoteSuggestionController(ICurrentGarageUserService current, IWorkQuoteService quotes,
            IQuoteSuggestionService suggestions, QuoteSuggestionRateLimiter limiter, IOptions<OpenAIOptions> options, QuoteSuggestionUsage usage)
        { _current = current; _quotes = quotes; _suggestions = suggestions; _limiter = limiter; _options = options.Value; _usage = usage; }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> Suggest([FromBody] QuoteSuggestionRequest request)
        {
            var user = _current.GetCurrentUser();
            var garageId = user.GarageBusinessId;
            if (request == null || !ModelState.IsValid || request.WorkQuoteId < 0)
                return BadRequest(new { status = "Error", code = "InvalidDraft", message = "A valid work-description request is required." });
            string customer = null, registration = null;
            if (request.WorkQuoteId > 0)
            {
                var quote = _quotes.GetWorkQuote(request.WorkQuoteId, user.GarageBusinessId);
                if (!quote.Success) return NotFound(new { status = "Error", code = "QuoteNotFound", message = "Quote not found." });
                customer = quote.Data.CustomerName; registration = quote.Data.VehicleRegistration;
            }
            var invalid = QuoteSuggestionInput.Validate(request.Description, _options, customer, registration);
            if (invalid != null) return Error(invalid);
            if (!_limiter.TryAcquire(user.UserId, garageId))
            {
                _usage.Limited(garageId);
                return Error(QuoteSuggestionResult.Fail("SuggestionRateLimited", "The user or garage AI limit was reached. Please wait a minute; you can still edit manually.", 429));
            }
            _usage.Started(garageId);
            QuoteSuggestionResult result;
            try { result = await _suggestions.SuggestAsync(request.Description.Trim(), HttpContext.RequestAborted)
                ?? QuoteSuggestionResult.Fail("AiUnavailable", "AI suggestions are unavailable. You can continue editing manually."); }
            catch (System.Exception)
            {
                // Do not expose or log an unexpected provider exception containing another request's content.
                result = QuoteSuggestionResult.Fail("AiUnavailable", "AI suggestions are unavailable. You can continue editing manually.");
            }
            _usage.Completed(garageId, result);
            if (!result.Success) return Error(result);
            return Json(new { status = "Success", data = new { suggestion = result.Suggestion } });
        }
        private IActionResult Error(QuoteSuggestionResult error) => StatusCode(error.StatusCode,
            new { status = "Error", code = error.Code, message = error.Message });
    }
}
