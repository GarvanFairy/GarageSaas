using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;
using GarageSaas.Models;

namespace GarageSaas
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
                // Handling SameSite cookie according to https://docs.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-3.1
                options.HandleSameSiteCookieCompatibility();
            });

            // Configuration to sign-in users with Azure AD B2C
            services.AddMicrosoftIdentityWebAppAuthentication(Configuration, Microsoft.Identity.Web.Constants.AzureAdB2C);

            services.AddControllersWithViews()
                .AddMicrosoftIdentityUI();

            services.AddRazorPages();

            //Configuring appsettings section AzureAdB2C, into IOptions
            services.AddOptions();
            services.Configure<OpenIdConnectOptions>(Configuration.GetSection("AzureAdB2C"));
            

            /*
             *   app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions
  {
       ClientId = Configuration["AzureAd:ClientId"],
       Authority = string.Format(Configuration["AzureAd:AadInstance"], Configuration["AzureAd:TenantId"]),
       CallbackPath = Configuration["AzureAd:AuthCallback"]
  });
             * */
            services.Configure<AppSettingsModel>(Configuration.GetSection("AppSettings"));

            services.AddDbContext<SignupContext>(opt =>
            opt.UseSqlServer(Configuration.GetConnectionString("GarageSaasDBConnection")));
            //opt.UseSqlServer("Data Source=tcp:garagesaasdbserver.database.windows.net,1433;Initial Catalog=GarageSaas_db;User Id=sqladmin@garagesaasdbserver;Password=Password1"));
            services.AddControllers().AddNewtonsoftJson();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
