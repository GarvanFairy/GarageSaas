using System.Threading.Tasks;

using GarageSaas.Exceptions;

using Microsoft.AspNetCore.Http;

namespace GarageSaas.Middleware
{
    public class GarageAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public GarageAccessMiddleware(
            RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (GarageAccessException)
            {
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