using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GarageSaas.Middleware
{
    public sealed class ProductionExceptionLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ProductionExceptionLoggingMiddleware> _logger;
        public ProductionExceptionLoggingMiddleware(RequestDelegate next, ILogger<ProductionExceptionLoggingMiddleware> logger)
        { _next = next; _logger = logger; }
        public async Task InvokeAsync(HttpContext context)
        {
            try { await _next(context); }
            catch (Exception error)
            {
                _logger.LogError("Request failed ({FailureType}); reference {TraceId}.", error.GetType().Name, context.TraceIdentifier);
                throw;
            }
        }
    }
}
