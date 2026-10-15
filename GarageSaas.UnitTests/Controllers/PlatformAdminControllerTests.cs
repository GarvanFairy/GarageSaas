using GarageSaas.Controllers;
using GarageSaas.Models;
using GarageSaas.Services.Interfaces;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class PlatformAdminControllerTests
{
    [DataTestMethod]
    [DataRow("Pending", "Pending", 1)]
    [DataRow("Approved", "Approved", 1)]
    [DataRow("Rejected", "Rejected", 1)]
    [DataRow("Completed", "Completed", 1)]
    [DataRow("All", "All", 4)]
    [DataRow(" approved ", "Approved", 1)]
    [DataRow("invalid", "Pending", 1)]
    [DataRow("", "Pending", 1)]
    [DataRow(null, "Pending", 1)]
    public async Task List_AllowlistsAndAppliesStatus(string? filter, string expected, int count)
    {
        using var context = DbContextFactory.Create();
        foreach (var status in new[] { "Pending", "Approved", "Rejected", "Completed" })
            context.GarageRegistrationRequest.Add(Request(status));
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = (ViewResult)await controller.RegistrationRequests(filter!);
        var model = (GarageRegistrationRequestsViewModel)result.Model!;

        Assert.AreEqual(expected, model.SelectedStatus);
        Assert.AreEqual(count, model.Requests.Count);
        Assert.IsTrue(model.Requests.All(x => expected == "All" || x.Status == expected));
        var defaultResult = (ViewResult)await controller.RegistrationRequests();
        Assert.AreEqual("Pending", ((GarageRegistrationRequestsViewModel)defaultResult.Model!).SelectedStatus);
    }

    [TestMethod]
    public async Task Details_ProjectsMetadataAndPreservesFilter()
    {
        using var context = DbContextFactory.Create();
        var request = Request("Completed");
        request.ReviewedDate = DateTime.UtcNow.AddDays(-2);
        request.ReviewedBy = "admin@example.com";
        request.CompletedDate = DateTime.UtcNow.AddDays(-1);
        request.OnboardingTokenExpiresDate = DateTime.UtcNow.AddDays(5);
        request.OnboardingRevoked = true;
        context.GarageRegistrationRequest.Add(request);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = (ViewResult)await controller.RegistrationRequest(request.Id, "All");
        var model = (GarageRegistrationRequestDetailsViewModel)result.Model!;

        Assert.AreEqual("All", model.SelectedStatus);
        Assert.AreEqual(request.ReviewedDate, model.ReviewedDate);
        Assert.AreEqual(request.ReviewedBy, model.ReviewedBy);
        Assert.AreEqual(request.CompletedDate, model.CompletedDate);
        Assert.AreEqual(request.OnboardingTokenExpiresDate, model.OnboardingTokenExpiresDate);
        Assert.AreEqual("Consumed", model.InvitationStatus);
        Assert.IsInstanceOfType(await controller.RegistrationRequest(-1), typeof(NotFoundResult));
    }

    [DataTestMethod]
    [DataRow("Pending", false, 1, "Not issued")]
    [DataRow("Rejected", false, 1, "Not issued")]
    [DataRow("Approved", false, 1, "Awaiting owner completion")]
    [DataRow("Approved", false, -1, "Expired")]
    [DataRow("Approved", true, 1, "Revoked")]
    [DataRow("Completed", true, -1, "Consumed")]
    public void Details_DescribesInvitationState(string status, bool revoked, int days, string expected)
    {
        var model = new GarageRegistrationRequestDetailsViewModel
        {
            Status = status,
            OnboardingRevoked = revoked,
            OnboardingTokenExpiresDate = DateTime.UtcNow.AddDays(days)
        };
        Assert.AreEqual(expected, model.InvitationStatus);
        model.Status = "Approved";
        model.OnboardingRevoked = false;
        model.OnboardingTokenExpiresDate = null;
        Assert.AreEqual("Unavailable", model.InvitationStatus);
    }

    private static PlatformAdminController CreateController(SignupContext context) => new(
        context, Mock.Of<IRegistrationRequestLock>(),
        new GarageSaas.Services.OnboardingInvitationService(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Mock.Of<IEmailService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GarageSaas.Services.OnboardingInvitationService>.Instance));

    private static GarageRegistrationRequest Request(string status) => new()
    {
        GarageBusinessName = status + " Garage",
        AddressLine1 = "Main Street", TownOrCity = "Dublin", County = "Dublin",
        BusinessPhone = "123456", BusinessType = "Garage",
        ContactFirstName = "Test", ContactLastName = "Owner",
        ContactEmail = "owner@example.com", ContactMobile = "123456",
        Status = status, CreatedDate = DateTime.UtcNow
    };
}


