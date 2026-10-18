using System;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace GarageSaas.Services
{
    // A per-browser, per-user preference, never an authorization grant.
    public sealed class GarageSelectionService
    {
        public const string CookieName = "__Host-GarageSaas.LastGarage";
        private readonly IHttpContextAccessor _http;
        private readonly ITimeLimitedDataProtector _preference;
        private readonly ITimeLimitedDataProtector _context;
        public GarageSelectionService(IHttpContextAccessor http, IDataProtectionProvider protection)
        {
            _http = http;
            _preference = protection.CreateProtector("GarageSaas.GaragePreference.v1").ToTimeLimitedDataProtector();
            _context = protection.CreateProtector("GarageSaas.GarageFormContext.v1").ToTimeLimitedDataProtector();
        }
        public (bool Present, int? GarageId) Read(int userId)
        {
            var cookies = _http.HttpContext.Request.Cookies;
            if (!cookies.TryGetValue(CookieName, out var value)) return (false, null);
            var garage = ReadGarage(_preference, value, userId, out var otherUser);
            return otherUser ? (false, null) : (true, garage);
        }
        public void Remember(int userId, int garageId)
        {
            _http.HttpContext.Response.Cookies.Append(CookieName,
                _preference.Protect(Payload(userId, garageId), TimeSpan.FromDays(30)), new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                    IsEssential = true, Path = "/", Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
        }
        public string ContextToken(int userId, int garageId) =>
            _context.Protect(Payload(userId, garageId), TimeSpan.FromHours(8));
        public bool MatchesContext(string token, int userId, int garageId) =>
            !string.IsNullOrEmpty(token) && ReadGarage(_context, token, userId, out _) == garageId;
        private static string Payload(int userId, int garageId) =>
            userId.ToString(CultureInfo.InvariantCulture) + ":" + garageId.ToString(CultureInfo.InvariantCulture);
        private static int? ReadGarage(ITimeLimitedDataProtector protector, string value, int userId, out bool otherUser)
        {
            otherUser = false;
            try
            {
                var parts = protector.Unprotect(value).Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out var previousOwner) && previousOwner != userId)
                { otherUser = true; return null; }
                return parts.Length == 2 && int.TryParse(parts[0], out var owner) && owner == userId &&
                    int.TryParse(parts[1], out var garage) && garage > 0 ? (int?)garage : null;
            }
            catch (CryptographicException) { return null; }
            catch (ArgumentException) { return null; }
        }
    }
}
