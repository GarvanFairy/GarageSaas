using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.IO;
using GarageSaas.Models;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Controllers
{


    public class AdminController : Controller
    {
        private static Lazy<X509SigningCredentials> SigningCredentials;
        private readonly AppSettingsModel AppSettings;
        private readonly IWebHostEnvironment HostingEnvironment;
        private readonly ILogger<HomeController> _logger;
        private readonly SignupContext _context;
        private int GarageBusinessId;

        // Sample: Inject an instance of an AppSettingsModel class into the constructor of the consuming class, 
        // and let dependency injection handle the rest
        public AdminController(ILogger<HomeController> logger, IOptions<AppSettingsModel> appSettings, IWebHostEnvironment hostingEnvironment, SignupContext context)
        {
            this.AppSettings = appSettings.Value;
            this.HostingEnvironment = hostingEnvironment;
            this._logger = logger;
            _context = context;

            var x509 = new X509Certificate2("SelfSignedForInvite.pfx","Password1");

            if (x509 != null)
            {
                var x509_SigningCredentials = new X509SigningCredentials(x509);
                SigningCredentials = new Lazy<X509SigningCredentials>(x509_SigningCredentials);
            }

            // Sample: Load the certificate with a private key (must be pfx file)
            SigningCredentials = new Lazy<X509SigningCredentials>(() =>
            {
                X509Store certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                certStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = certStore.Certificates.Find(
                                            X509FindType.FindByThumbprint,
                                            this.AppSettings.SigningCertThumbprint,
                                            false);
                // Get the first cert with the thumb-print
                if (certCollection.Count > 0)
                {
                    return new X509SigningCredentials(certCollection[0]);
                }

                throw new Exception("Certificate not found");
            });
        }

        [HttpGet]
        public ActionResult Index(string Name, string email, string phone)
        {
            
            if (string.IsNullOrEmpty(email))
            {
                ViewData["Message"] = "";
                return View();
            }

            string token = BuildIdToken(Name, email, phone);
            string link = BuildUrl(token);

            _logger.LogInformation("token:" + token);
            _logger.LogInformation("UrlLink:" + link);

           /* string Body = string.Empty;

            string htmlTemplate = System.IO.File.ReadAllText(Path.Combine(this.HostingEnvironment.ContentRootPath, "App_Data\\Template.html"));

            try
            {
                MailMessage mailMessage = new MailMessage();
                mailMessage.To.Add(email);
                mailMessage.From = new MailAddress(AppSettings.SMTPFromAddress);
                mailMessage.Subject = AppSettings.SMTPSubject;
                mailMessage.Body = string.Format(htmlTemplate, email, link);
                mailMessage.IsBodyHtml = true;
                SmtpClient smtpClient = new SmtpClient(AppSettings.SMTPServer, AppSettings.SMTPPort);
                smtpClient.Credentials = new NetworkCredential(AppSettings.SMTPUsername, AppSettings.SMTPPassword);
                smtpClient.EnableSsl = AppSettings.SMTPUseSSL;
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.Send(mailMessage);

                ViewData["Message"] = $"Email sent to {email}";

            }
            catch (Exception ex)
            {
                throw ex;
            }
           */

            return View();
        }


        private string BuildIdToken(string Name, string email, string phone)
        {
            GarageBusiness garageBusiness = new GarageBusiness();
            string issuer = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase.Value}/";

            // All parameters send to Azure AD B2C needs to be sent as claims

            string ownerEmail = string.Empty;
            int garageBusinessId = 0;
            foreach (var ident in User.Claims)
            {
                

                if (ident.Type == "emails")
                    ownerEmail = ident.Value;

                
                if (ident.Type == "extension_GarageBusinessID")
                {
                    if (string.IsNullOrEmpty(ident.Value))
                    {
                        Users userOwner = ((IQueryable<Users>)_context.Users).FirstOrDefault(u => u.EmailAddress == ownerEmail);
                        if (userOwner != null)
                        {
                            garageBusinessId = userOwner.GarageBusinessId;
                            GarageBusinessId = garageBusinessId;
                        }
                    }
                    else
                    {
                        if (int.TryParse(ident.Value, out garageBusinessId))
                        {
                            GarageBusinessId = garageBusinessId;
                        }
                    }

                    // Query directly for the GarageBusiness without creating an intermediate list
                    garageBusiness = ((IQueryable<GarageBusiness>)_context.GarageBusiness).FirstOrDefault(g => g.Id == GarageBusinessId);
                    TempData["GarageBusinessId"] = garageBusinessId;
                }
            }

            IList<System.Security.Claims.Claim> claims = new List<System.Security.Claims.Claim>();
            claims.Add(new System.Security.Claims.Claim("name", Name, System.Security.Claims.ClaimValueTypes.String, issuer));
            claims.Add(new System.Security.Claims.Claim("email", email, System.Security.Claims.ClaimValueTypes.String, issuer));
            claims.Add(new System.Security.Claims.Claim("extension_GarageBusinessID", GarageBusinessId.ToString(), System.Security.Claims.ClaimValueTypes.String, issuer));
            claims.Add(new System.Security.Claims.Claim("GarageBusinessName", garageBusiness.GarageBusinessName, System.Security.Claims.ClaimValueTypes.String, issuer));

            if (!string.IsNullOrEmpty(phone))
            {
                claims.Add(new System.Security.Claims.Claim("phone", phone, System.Security.Claims.ClaimValueTypes.String, issuer));
            }

            // Create the token
            JwtSecurityToken token = new JwtSecurityToken(
                    issuer,
                    this.AppSettings.B2CClientId,
                    claims,
                    DateTime.Now,
                    DateTime.Now.AddDays(7),
                    AdminController.SigningCredentials.Value);

            // Get the representation of the signed token
            JwtSecurityTokenHandler jwtHandler = new JwtSecurityTokenHandler();

            return jwtHandler.WriteToken(token);
        }

        private string BuildUrl(string token)
        {
            string nonce = Guid.NewGuid().ToString("n");

            return string.Format(this.AppSettings.B2CSignUpUrl,
                    this.AppSettings.B2CTenant,
                    this.AppSettings.B2CPolicy,
                    this.AppSettings.B2CClientId,
                    Uri.EscapeDataString(this.AppSettings.B2CRedirectUri),
                    nonce) + "&id_token_hint=" + token;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

