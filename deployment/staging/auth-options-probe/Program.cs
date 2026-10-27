using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
foreach(var variant in new[]{"current","own-client-scope"}) {
 using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"appsettings.pending-review.json")));
 var values=json.RootElement.EnumerateObject().ToDictionary(p=>p.Name.Replace("__",":"),p=>(string?)p.Value.GetString());
 values["AzureAdB2C:ClientSecret"]="offline-options-validation-only";
 values["DataProtection:KeyDirectory"]=null;
 if(variant=="own-client-scope"){values["AzureAdB2C:Scope:0"]="6a6b59d3-6db1-4124-86b1-d43c457dd17e";}
 var configuration=new ConfigurationBuilder().AddInMemoryCollection(values).Build();
 var env=new ProbeEnvironment();
 var services=new ServiceCollection();services.AddSingleton<IWebHostEnvironment>(env);services.AddSingleton<IHostEnvironment>(env);
 new GarageSaas.Startup(configuration,env).ConfigureServices(services);
 using var provider=services.BuildServiceProvider();
 var options=provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
 Console.WriteLine(JsonSerializer.Serialize(new{variant,options.ResponseType,options.ResponseMode,options.UsePkce,scopes=options.Scope.ToArray(),requireSignedTokens=options.TokenValidationParameters.RequireSignedTokens,validateLifetime=options.TokenValidationParameters.ValidateLifetime,options.Authority,options.MetadataAddress,callback=options.CallbackPath.Value,clientId=options.ClientId,clientSecretConfigured=!string.IsNullOrEmpty(options.ClientSecret)}));
}
class ProbeEnvironment:IWebHostEnvironment {
 public string EnvironmentName{get;set;}="Production";
 public string ApplicationName{get;set;}=typeof(GarageSaas.Startup).Assembly.GetName().Name!;
 public string WebRootPath{get;set;}=Path.GetFullPath("GarageSaas/wwwroot");
 public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
 public string ContentRootPath{get;set;}=Path.GetFullPath("GarageSaas");
 public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
}

