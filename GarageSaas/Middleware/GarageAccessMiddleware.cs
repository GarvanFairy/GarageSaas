using GarageSaas.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace GarageSaas.Middleware
{
    public class GarageAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GarageAccessMiddleware> _logger;

        public GarageAccessMiddleware(
            RequestDelegate next, ILogger<GarageAccessMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (GarageAccessException ex)
            {

                _logger.LogWarning(
    ex,
    "Garage access denied for request {Path}",
    context.Request.Path);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();

                context.Response.Redirect(
                    "/Home/NoGarageAccess");
            }
        }
    }
}