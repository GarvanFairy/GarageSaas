using GarageSaas.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class PlatformAdministratorAttributeTests
{
    [TestMethod]
    public void AdministratorActions_KeepPolicyAndAntiforgeryProtection()
    {
        Assert.AreEqual("PlatformAdministrator", ((AuthorizeAttribute)Attribute.GetCustomAttribute(
            typeof(PlatformAdminController), typeof(AuthorizeAttribute))!).Policy);
        foreach (var method in new[] { "ApproveRegistration", "RejectRegistration", "ResendOnboardingInvitation" })
        {
            var action = typeof(PlatformAdminController).GetMethod(method)!;
            Assert.IsTrue(Attribute.IsDefined(action, typeof(HttpPostAttribute)));
            Assert.IsTrue(Attribute.IsDefined(action, typeof(ValidateAntiForgeryTokenAttribute)));
        }
    }
}
