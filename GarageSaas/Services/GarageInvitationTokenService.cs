using System;
using System.Security.Cryptography;
using System.Text;

namespace GarageSaas.Services
{
    public static class GarageInvitationTokenService
    {
        public static string GenerateToken()
        {
            var tokenBytes = new byte[32];

            using (var rng =
                RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }

            return Convert
                .ToBase64String(tokenBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static string HashToken(string token)
        {
            using (var sha = SHA256.Create())
            {
                var hashBytes =
                    sha.ComputeHash(
                        Encoding.UTF8.GetBytes(token));

                return BitConverter
                    .ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
        }
    }
}