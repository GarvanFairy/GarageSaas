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
                context.Response.Headers["Cache-Control"] = "no-store";
                // Redirect only explicit HTML navigations. Fetch defaults to */*;
                // returning HTML for those calls would break existing JSON callers.
                var request = context.Request;
                var htmlNavigation = HttpMethods.IsGet(request.Method) &&
                    request.Headers["Accept"].ToString().Contains("text/html") &&
                    !request.Headers["Accept"].ToString().Contains("application/json") &&
                    request.Headers["X-Requested-With"] != "XMLHttpRequest";
                if (htmlNavigation && request.Path != "/Home/NoGarageAccess" &&
                    !request.Path.StartsWithSegments("/GarageSelection"))
                {
                    context.Response.Redirect(ex.SelectionRequired ? "/GarageSelection" : "/Home/NoGarageAccess");
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    status = "Error",
                    code = ex.SelectionRequired ? "GarageSelectionRequired" : "GarageAccessUnavailable",
                    message = ex.SelectionRequired ? "Garage selection is required." : "Garage access is unavailable."
                });
            }
        }
    }
}
