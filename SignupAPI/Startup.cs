using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;


namespace SignupAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
            if (!environment.IsDevelopment()) SignupAPI.Configuration.ProductionDatabaseConfiguration.Validate(configuration);
            if (!environment.IsDevelopment() && (string.IsNullOrWhiteSpace(configuration["AllowedHosts"]) || configuration["AllowedHosts"].Contains('*')))
                throw new InvalidOperationException("Missing or invalid production configuration: AllowedHosts.");
        }

        public IConfiguration Configuration { get; }
        private IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<SignupContext>(opt =>
            opt.UseSqlServer(Configuration.GetConnectionString("GarageSaasDBConnection")));
            services.AddControllers().AddNewtonsoftJson();
            if (!Environment.IsDevelopment()) services.AddLogging(logging =>
            {
                foreach (var category in new[] { "Microsoft.AspNetCore.Hosting.Diagnostics", "Microsoft.AspNetCore.Diagnostics", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.EntityFrameworkCore", "System.Net.Http.HttpClient" })
                    logging.AddFilter(category, LogLevel.None);
            });
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
                    SuppressDiagnosticsCallback = _ => true,
                    ExceptionHandler = async context =>
                    {
                        var failure = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                        context.RequestServices.GetRequiredService<ILogger<Startup>>().LogError("Request failed ({FailureType}).", failure?.Error.GetType().Name);
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
                    }
                });
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
