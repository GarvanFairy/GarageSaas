using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace GarageSaas.Middleware
{
    // Temporary, default-off diagnostic. No endpoint, response output or logger.
    public sealed class StagingSubjectVerificationMiddleware
    {
        private const string StagingSite = "garagesaas-stg-d49b2f61-weu";
        // Independently confirmed directory identity; NEVER used as the binding value.
        private const string ApprovedObjectId = "64c45703-ef4f-4119-a4a5-addff7909b69";
        private const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public StagingSubjectVerificationMiddleware(RequestDelegate next, IConfiguration configuration, IWebHostEnvironment environment)
        { _next = next; _configuration = configuration; _environment = environment; }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
            if (_configuration["StagingVerification:CaptureOwnSubject"] != "true" ||
                _configuration["WEBSITE_SITE_NAME"] != StagingSite ||
                _environment.EnvironmentName != "Production" ||
                !context.Request.IsHttps || !HttpMethods.IsGet(context.Request.Method) ||
                context.Request.Path != "/Home/NoGarageAccess" || context.Response.StatusCode != 200 ||
                !string.Equals(context.Request.Host.Host, StagingSite + ".azurewebsites.net", StringComparison.OrdinalIgnoreCase)) return;

            // Use the application's validated authentication ticket, not request inputs
            // or an email claim. Fail closed if this policy doesn't emit an object ID.
            var ticket = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!ticket.Succeeded || ticket.Principal?.Identity?.IsAuthenticated != true) return;
            var principal = ticket.Principal;
            var objectIds = principal.Claims.Where(c => c.Type == "oid" || c.Type == ObjectIdClaim).ToArray();
            if (objectIds.Length == 0 || objectIds.Any(c => !string.Equals(c.Value, ApprovedObjectId, StringComparison.OrdinalIgnoreCase))) return;
            var subjects = principal.FindAll(ClaimTypes.NameIdentifier).ToArray();
            if (subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0].Value) ||
                subjects[0].Value.Length > 100 || subjects[0].Value.Any(char.IsControl)) return;
            // The resolver reads this same claim from HttpContext.User. Reject a
            // transformed/different request principal rather than recording another value.
            if (context.User.Identity?.IsAuthenticated != true ||
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value != subjects[0].Value) return;

            var home = _configuration["HOME"];
            if (string.IsNullOrWhiteSpace(home) || !Path.IsPathFullyQualified(home)) return;
            var directory = Path.GetFullPath(Path.Combine(home, "data", "staging-identity-verification"));
            var webRoot = _environment.WebRootPath;
            if (!string.IsNullOrEmpty(webRoot) &&
                (directory + Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(webRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                // App Service HOME/data inherits private application/SCM access.
                // One record only; never overwrite it or copy it into wwwroot.
                Directory.CreateDirectory(directory);
                using var file = new FileStream(Path.Combine(directory, "nameidentifier.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(file);
                await writer.WriteAsync(subjects[0].Value);
            }
            catch (IOException) { /* Already captured or unavailable: no output/log. */ }
            catch (UnauthorizedAccessException) { /* Fail closed; no output/log. */ }
        }
    }
}
