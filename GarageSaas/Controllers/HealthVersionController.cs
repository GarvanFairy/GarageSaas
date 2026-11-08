using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{
    [AllowAnonymous]
    [Route("health/version")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class HealthVersionController : ControllerBase
    {
        // Metadata comes from the assemblies loaded into THIS worker. Never use
        // configuration, a request parameter, a manifest or FileVersionInfo here.
        private static readonly string Commit = MatchLoadedCommits(
            typeof(Startup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            typeof(SignupContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        [NonAction]
        public static string MatchLoadedCommits(string application, string dependency)
        {
            string Read(string value)
            {
                if (value == null || value.Length > 170) return null;
                var match = Regex.Match(value, @"\A[^+\r\n]{1,128}\+([a-f0-9]{40})\z", RegexOptions.CultureInvariant);
                return match.Success && match.Groups[1].Value != new string('0', 40) ? match.Groups[1].Value : null;
            }
            var app = Read(application);
            return app != null && app == Read(dependency) ? app : null;
        }

        [HttpGet]
        public IActionResult Get()
        {
            if (Commit == null) return StatusCode(503, new { status = "Unavailable" });
            return new JsonResult(new { commit = Commit });
        }
    }
}
