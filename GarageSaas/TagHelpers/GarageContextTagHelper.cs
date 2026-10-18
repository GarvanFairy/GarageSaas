using System;
using System.Threading.Tasks;
using GarageSaas.Exceptions;
using GarageSaas.Filters;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace GarageSaas.TagHelpers
{
    [HtmlTargetElement("form", Attributes = "method")]
    public sealed class GarageContextTagHelper : TagHelper
    {
        [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; }
        public override int Order => int.MaxValue;
        public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var controller = context.AllAttributes["asp-controller"]?.Value?.ToString()
                ?? ViewContext.RouteData.Values["controller"]?.ToString();
            if (!string.Equals(context.AllAttributes["method"]?.Value?.ToString(), "post", StringComparison.OrdinalIgnoreCase) ||
                !GarageContextFilter.IsTenantController(controller)) return Task.CompletedTask;
            var services = ViewContext.HttpContext.RequestServices;
            var selection = services.GetService<GarageSelectionService>();
            if (selection == null) return Task.CompletedTask;
            try
            {
                var user = services.GetRequiredService<ICurrentGarageUserService>().GetCurrentUser();
                var field = new TagBuilder("input"); field.Attributes["type"] = "hidden"; field.Attributes["name"] = "__GarageContext";
                field.Attributes["value"] = selection.ContextToken(user.UserId, user.GarageBusinessId);
                output.PostContent.AppendHtml(field);
            }
            catch (GarageAccessException) { }
            return Task.CompletedTask;
        }
    }
}
