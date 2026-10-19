using System;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace GarageSaas.Services
{
    // Practical accidental-submit protection within one app instance; not a durable email outbox.
    public sealed class DocumentSendTokens
    {
        private readonly IMemoryCache _cache;
        private readonly object _gate = new object();
        public DocumentSendTokens(IMemoryCache cache) { _cache = cache; }
        public static bool ValidRecipient(string value) => !string.IsNullOrWhiteSpace(value) &&
            !value.Contains('\r') && !value.Contains('\n') && MailAddress.TryCreate(value, out var address) &&
            string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase) && address.Host.Contains('.');
        public string Issue(int userId, int garageId, string kind, int id, string recipient)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _cache.Set("document-send:" + token, new Ticket(userId, garageId, kind, id, recipient), TimeSpan.FromMinutes(15));
            return token;
        }
        public bool Consume(string token, int userId, int garageId, string kind, int id, string recipient)
        {
            if (string.IsNullOrEmpty(token) || token.Length != 64) return false;
            lock (_gate)
            {
                var key = "document-send:" + token;
                if (!_cache.TryGetValue(key, out Ticket ticket) || ticket.User != userId || ticket.Garage != garageId ||
                    ticket.Kind != kind || ticket.Id != id || !string.Equals(ticket.Recipient, recipient, StringComparison.OrdinalIgnoreCase)) return false;
                _cache.Remove(key);
                return true;
            }
        }
        private sealed record Ticket(int User, int Garage, string Kind, int Id, string Recipient);
    }
}
