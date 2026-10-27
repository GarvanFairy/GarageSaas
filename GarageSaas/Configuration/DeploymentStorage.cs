using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace GarageSaas.Configuration
{
    public static class DeploymentStorage
    {
        public static string DirectoryPath(IConfiguration configuration, string key)
        {
            var value = configuration?[key];
            if (string.IsNullOrWhiteSpace(value)) return null;
            var path = Environment.ExpandEnvironmentVariables(value);
            if (path.Contains('%') || !Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("Configure an absolute persistent directory: " + key);
            return Path.GetFullPath(path);
        }
        public static string LogoDirectory(IWebHostEnvironment environment, IConfiguration configuration) =>
            DirectoryPath(configuration, "Storage:GarageLogoDirectory") ??
            (environment?.WebRootPath == null ? null : Path.Combine(environment.WebRootPath, "uploads", "garage-logos"));
        public static void EnsureDirectory(string path, string key)
        {
            try { Directory.CreateDirectory(path); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { throw new InvalidOperationException("Persistent directory is unavailable: " + key); }
        }
        public static void ValidatePrivateKeys(string keyDirectory, string logoDirectory, string webRoot)
        {
            if (keyDirectory == null) return;
            foreach (var publicDirectory in new[] { logoDirectory, webRoot })
            {
                if (string.IsNullOrWhiteSpace(publicDirectory)) continue;
                var relative = Path.GetRelativePath(Path.GetFullPath(publicDirectory), keyDirectory);
                if (!Path.IsPathFullyQualified(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidOperationException("Data Protection keys must be outside public file directories.");
            }
        }
    }
}
