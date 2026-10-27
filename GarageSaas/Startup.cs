using GarageSaas.Authorization;
using GarageSaas.Configuration;
using GarageSaas.Middleware;
using GarageSaas.Models;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using SignupAPI.Models;



namespace GarageSaas
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        private IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (!Environment.IsDevelopment()) ProductionConfiguration.Validate(Configuration);
            if (!Environment.IsDevelopment()) services.AddLogging(logging =>
            {
                foreach (var category in new[] { "Microsoft.AspNetCore.Hosting.Diagnostics", "Microsoft.AspNetCore.Diagnostics", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Authentication", "Microsoft.Identity", "Microsoft.Identity.Web", "Microsoft.EntityFrameworkCore", "System.Net.Http.HttpClient" })
                    logging.AddFilter(category, LogLevel.None);
            });
            services.AddAntiforgery(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            });
            services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            });
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.SaveTokens = false;
                options.RequireHttpsMetadata = true;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            });
            services.Configure<CookieTempDataProviderOptions>(options =>
            {
                options.Cookie.SecurePolicy = Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true; // Required for existing save/send success and failure feedback.
            });
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

            var protection = services.AddDataProtection();
            var applicationName = Configuration["DataProtection:ApplicationName"];
            if (!string.IsNullOrWhiteSpace(applicationName)) protection.SetApplicationName(applicationName);
            var keyDirectory = DeploymentStorage.DirectoryPath(Configuration, "DataProtection:KeyDirectory");
            if (keyDirectory != null)
            {
                DeploymentStorage.ValidatePrivateKeys(keyDirectory, DeploymentStorage.LogoDirectory(Environment, Configuration), Environment.WebRootPath);
                if (string.IsNullOrWhiteSpace(applicationName))
                    throw new System.InvalidOperationException("Configure DataProtection:ApplicationName for persistent keys.");
                DeploymentStorage.EnsureDirectory(keyDirectory, "DataProtection:KeyDirectory");
                protection.PersistKeysToFileSystem(new System.IO.DirectoryInfo(keyDirectory));
            }
            services.AddHealthChecks();
            services.AddScoped<GarageSelectionService>();
            services.AddScoped<GarageSaas.Filters.GarageContextFilter>();
            services.AddControllersWithViews(options =>
            {
                options.Filters.AddService<GarageSaas.Filters.GarageContextFilter>();
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            })
                .AddMicrosoftIdentityUI();

            services.AddRazorPages();

            //Configuring appsettings section AzureAdB2C, into IOptions
            services.AddOptions();
            services.Configure<OpenIdConnectOptions>(Configuration.GetSection("AzureAdB2C"));

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    "PlatformAdministrator",
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();

                        policy.AddRequirements(
                            new PlatformAdministratorRequirement());
                    });
            });


            /*
             *   app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions
  {
       ClientId = Configuration["AzureAd:ClientId"],
       Authority = string.Format(Configuration["AzureAd:AadInstance"], Configuration["AzureAd:TenantId"]),
       CallbackPath = Configuration["AzureAd:AuthCallback"]
  });
             * */
            //services.Configure<AppSettingsModel>(Configuration.GetSection("AppSettings"));
            services.Configure<EmailSettings>(Configuration.GetSection("EmailSettings"));

            services.AddDbContext<SignupContext>(opt =>
            opt.UseSqlServer(Configuration.GetConnectionString("GarageSaasDBConnection")));
            services.AddControllers().AddNewtonsoftJson();

            // 🔹 Cache
            services.AddMemoryCache();
            services.Configure<OpenAIOptions>(Configuration.GetSection("OpenAI"));
            services.AddSingleton<QuoteSuggestionRateLimiter>();
            services.AddSingleton<QuoteSuggestionUsage>();
            services.AddHttpClient<IQuoteSuggestionService, OpenAIQuoteSuggestionService>(client =>
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });

            // 🔹 Vehicle lookup service
            services.AddScoped<ICustomerVehicleService, CustomerVehicleService>();
            services.AddScoped<IVehicleLookupService, VehicleLookupService>();

            services.AddHttpContextAccessor();

            services.AddScoped<ICurrentGarageUserService, CurrentGarageUserService>();

            services.AddScoped<IGarageBusinessService, GarageBusinessService>();
            services.AddScoped<IGarageCustomersService, GarageCustomersService>();
            services.AddScoped<IWorkItemService, WorkItemService>();
            services.AddScoped<VehicleServiceHistoryService>();
            services.AddScoped<CustomerDocumentService>();
            services.AddScoped<ICustomerDocumentPdfRenderer, CustomerDocumentPdfRenderer>();
            services.AddSingleton<DocumentSendTokens>();
            services.AddScoped<IWorkQuoteService, WorkQuoteService>();
            services.AddScoped<IVehicleInvoiceService, VehicleInvoiceService>();
            services.AddScoped<IVehiclePartService, VehiclePartService>();
            services.AddScoped<IEmailService, MailKitEmailService>();
            services.AddScoped<IRegistrationRequestLock, RegistrationRequestLock>();
            services.AddScoped<OnboardingInvitationService>();
            services.AddScoped<IInvitationIdentityVerificationService,InvitationIdentityVerificationService>();
            services.AddScoped<IGarageAuthorizationService,GarageAuthorizationService>();
            services.AddScoped<IAuthorizationHandler,PlatformAdministratorHandler>();

            // 🔹 EF DbContext (example)
            //services.AddDbContext<SignupContext>(options =>
            //    options.UseSqlServer(
            //        Configuration.GetConnectionString("DefaultConnection")));

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
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    ExceptionHandlingPath = "/Home/Error",
                    SuppressDiagnosticsCallback = _ => true
                });
                app.UseMiddleware<ProductionExceptionLoggingMiddleware>();
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCookiePolicy();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                await next();
            });
            var logoDirectory = DeploymentStorage.DirectoryPath(Configuration, "Storage:GarageLogoDirectory");
            if (logoDirectory != null)
            {
                DeploymentStorage.EnsureDirectory(logoDirectory, "Storage:GarageLogoDirectory");
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(logoDirectory),
                    RequestPath = "/uploads/garage-logos"
                });
            }
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<StagingSubjectVerificationMiddleware>();
            app.UseMiddleware<GarageAccessMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health").AllowAnonymous();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                //endpoints.MapControllerRoute(
                //    name: "AddGarageCustomer",
                //    pattern: "{controller=GarageCustomers}/{action=AddGarageCustomer}/{garageBusinessId}/{userId}");
                endpoints.MapControllerRoute(
                    name: "GetModelsByMake",
                    pattern: "{controller=CustomerVehicle}/{action=GetModelsByMake}/{makeId}");
                endpoints.MapRazorPages();
            });
        }
    }
}
