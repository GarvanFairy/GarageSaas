using System;
using System.Text.RegularExpressions;
using GarageSaas.Configuration;
using GarageSaas.Services.Models;

namespace GarageSaas.Services
{
    public static class QuoteSuggestionInput
    {
        // A conservative pilot check, not a claim that arbitrary free text is anonymous.
        private static readonly Regex Identifiers = new Regex(
            @"[^\s@]+@[^\s@]+|https?://|www\.|\b\d(?:[\s()+.-]*\d){6,}\b|" +
            @"\b\d{2,3}[ -][A-Z]{1,2}[ -]\d{1,6}\b|\b[A-Z]{2}\d{2}\s?[A-Z]{3}\b|" +
            @"\b[A-HJ-NPR-Z0-9]{17}\b|\b(?:[A-Z]\d{2}|D6W)\s?[A-Z0-9]{4}\b|" +
            @"\b(?:customer|name|address|email|phone|mobile|telephone|registration|reg|vin|eircode|postcode)\s*[:=]|" +
            @"\b(?:Mr|Mrs|Ms|Miss|Dr)\.?\s+\p{L}+|\b\d+\s+\p{L}+(?:\s+\p{L}+){0,3}\s+(?:Street|Road|Avenue|Drive|Lane|Crescent)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        public static QuoteSuggestionResult Validate(string draft, OpenAIOptions options, params string[] knownIdentifiers)
        {
            if (!options.ValidLimits) return QuoteSuggestionResult.Fail("AiUnavailable", "AI suggestions are not configured correctly. You can continue editing manually.");
            if (string.IsNullOrWhiteSpace(draft)) return QuoteSuggestionResult.Fail("InvalidDraft", "Enter a short work description first.", 400);
            if (draft.Trim().Length > options.MaxInputCharacters)
                return QuoteSuggestionResult.Fail("InvalidDraft", $"Use no more than {options.MaxInputCharacters} characters for an AI suggestion.", 400);
            try
            {
                if (Identifiers.IsMatch(draft)) return PrivateDraft();
                foreach (var identifier in knownIdentifiers)
                    if (!string.IsNullOrWhiteSpace(identifier) && identifier.Trim().Length >= 3 &&
                        draft.IndexOf(identifier.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return PrivateDraft();
            }
            catch (RegexMatchTimeoutException) { return PrivateDraft(); }
            return null;
        }
        private static QuoteSuggestionResult PrivateDraft() => QuoteSuggestionResult.Fail("IdentifyingInformation",
            "Remove customer names, contact details, addresses and vehicle identifiers before requesting a suggestion.", 400);
    }
}
