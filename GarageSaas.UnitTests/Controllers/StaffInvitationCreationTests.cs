using Moq;
using GarageSaas.Models;
using GarageSaas.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class StaffInvitationCreationTests
{
    [DataTestMethod]
    [DataRow("none",true)] [DataRow("pending",false)] [DataRow("expired",false)]
    [DataRow("revoked",true)] [DataRow("accepted",true)]
    [DataRow("active-member",false)] [DataRow("inactive-member",false)]
    public async Task CreationPreservesLifecycleAndNormalizesRecipient(string state,bool allowed)
    {
        var options=StaffInvitationData.Options();
        StaffInvitationData.Seed(options,invitation:state,userState:state=="active-member"?"existing-membership":state=="inactive-member"?"inactive-membership":"active");
        using var db=new SignupContext(options);
        if(state=="none"){db.Remove(db.GarageUserInvitation.Single());db.SaveChanges();}
        // Historical values and submitted values use the same trim/case normalization.
        else {db.GarageUserInvitation.Single().EmailAddress="  INVITEE@test.invalid  ";db.Users.Single(x=>x.Id==2).EmailAddress=" INVITEE@Test.Invalid ";db.SaveChanges();}
        var before=StaffInvitationData.Snapshot(db);var urls=new List<string>();
        var accessor=new HttpContextAccessor{HttpContext=StaffInvitationData.Http("admin")};
        var c=StaffInvitationData.Team(db,accessor,new CurrentGarageUserService(db,accessor),StaffInvitationData.Email(urls).Object);
        var input=StaffInvitationData.Input();input.EmailAddress="  invitee@TEST.invalid ";
        var result=await c.Invite(input);
        Assert.AreEqual(allowed,result is RedirectToActionResult);Assert.AreEqual(allowed?1:0,urls.Count);
        if(!allowed){Assert.AreEqual(before,StaffInvitationData.Snapshot(db));Assert.IsFalse(c.ModelState.IsValid);Assert.AreSame(input,((ViewResult)result).Model);}
        else
        {
            var created=db.GarageUserInvitation.OrderByDescending(x=>x.Id).First();Assert.AreEqual(1,created.GarageBusinessId);Assert.AreEqual("invitee@TEST.invalid",created.EmailAddress);
            var token=Uri.UnescapeDataString(new Uri(urls.Single()).Query.Substring("?token=".Length));Assert.AreEqual(GarageInvitationTokenService.HashToken(token),created.InvitationTokenHash);
            Assert.AreEqual("trusted.example",new Uri(urls.Single()).Authority);Assert.IsFalse(created.Accepted);Assert.IsFalse(created.Revoked);
        }
    }

    [TestMethod]
    public async Task EmailFailureKeepsCommittedInvitationForResend()
    {
        var options=StaffInvitationData.Options();StaffInvitationData.Seed(options);using var db=new SignupContext(options);
        var accessor=new HttpContextAccessor{HttpContext=StaffInvitationData.Http("admin")};var email=new Moq.Mock<GarageSaas.Services.Interfaces.IEmailService>();
        email.Setup(x=>x.SendGarageInvitationAsync(Moq.It.IsAny<string>(),Moq.It.IsAny<string>(),Moq.It.IsAny<string>(),Moq.It.IsAny<string>())).ThrowsAsync(new IOException("Injected email failure"));
        var c=StaffInvitationData.Team(db,accessor,new CurrentGarageUserService(db,accessor),email.Object);
        Assert.IsInstanceOfType<RedirectToActionResult>(await c.Invite(StaffInvitationData.Input()));
        using var check=new SignupContext(options);Assert.AreEqual(1,check.GarageUserInvitation.Count(x=>x.EmailAddress=="new@test.invalid"));StringAssert.Contains((string)c.TempData["ErrorMessage"]!,"created, but the email could not be sent");
    }
}
