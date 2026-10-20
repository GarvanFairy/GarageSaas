using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GarageSaas.Filters
{
    // Only existing active tenant MVC workflows. Platform/onboarding/invitations
    // and B1's no-data retired endpoints keep their existing contracts.
    public sealed class GarageContextFilter : IAsyncResourceFilter
    {
        private static readonly HashSet<string> TenantControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "GarageBusiness", "GarageCustomers", "CustomerVehicle", "WorkItem", "WorkQuote", "VehicleInvoice", "GarageTeam", "CustomerDocument", "QuoteSuggestion" };
        public static bool IsTenantController(string name) => name != null && TenantControllers.Contains(name);
        private readonly ICurrentGarageUserService _current;
        private readonly GarageSelectionService _selection;
        public GarageContextFilter(ICurrentGarageUserService current, GarageSelectionService selection)
        { _current = current; _selection = selection; }
        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                context.HttpContext.Response.Headers["Cache-Control"] = "no-store";
            var historyRead = string.Equals(controller, "ServiceHistory", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.RouteData.Values["action"]?.ToString(), "Index", StringComparison.OrdinalIgnoreCase);
            if (!IsTenantController(controller) && !historyRead) { await next(); return; }
            var request = context.HttpContext.Request;
            context.HttpContext.Response.Headers["Cache-Control"] = "no-store";
            var user = _current.GetCurrentUser();
            var token = request.Headers["X-Garage-Context"].ToString();
            var safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
            if (!safe && string.IsNullOrEmpty(token) && request.HasFormContentType)
                token = (await request.ReadFormAsync())["__GarageContext"].ToString();
            if ((!safe || !string.IsNullOrEmpty(token)) && !_selection.MatchesContext(token, user.UserId, user.GarageBusinessId))
            {
                context.Result = new ObjectResult(new { status = "Error", code = "GarageContextChanged", message = "Garage context changed or expired. Reload this page before continuing." }) { StatusCode = 409 };
                return;
            }
            await next();
        }
    }
}
