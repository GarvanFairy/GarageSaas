using GarageSaas.Exceptions;
using GarageSaas.Services;
using GarageSaas.UnitTests.Controllers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Services;

[TestClass]
public class GarageSelectionTests
{
    private readonly EphemeralDataProtectionProvider _protection=new();
    private static DbContextOptions<SignupContext> Seed(bool multi=true)
    {
        var options=StaffInvitationData.Options();using var db=new SignupContext(options);CurrentGarageUserServiceTests.Seed(db);
        if(multi){db.Add(new GarageBusiness{Id=2,GarageBusinessName="Garage Two",Active=true});db.Add(new GarageBusinessUser{Id=2,UserId=1,GarageBusinessId=2,IsActive=true,Role="Staff"});db.SaveChanges();}return options;
    }
    private static HttpContextAccessor Http(string? cookie=null,string identity="identity")
    {var a=new HttpContextAccessor{HttpContext=StaffInvitationData.Http(identity)};a.HttpContext.Request.Method="GET";if(cookie!=null)a.HttpContext.Request.Headers.Cookie=cookie;return a;}
    private string Cookie(int garage,int user=1)
    {var context=new DefaultHttpContext();var a=new Moq.Mock<IHttpContextAccessor>();a.SetupGet(x=>x.HttpContext).Returns(context);new GarageSelectionService(a.Object,_protection).Remember(user,garage);return context.Response.Headers.SetCookie.ToString().Split(';')[0];}
    [TestMethod]
    public void SingleGaragePreservedAndPreferenceIsSecureAndRestorable()
    {
        var options=Seed(false);using var db=new SignupContext(options);var a=Http();var preference=new GarageSelectionService(a,_protection);
        Assert.AreEqual(1,new CurrentGarageUserService(db,a,preference).GetCurrentUser().GarageBusinessId);
        var header=a.HttpContext!.Response.Headers.SetCookie.ToString();StringAssert.Contains(header,"secure");StringAssert.Contains(header,"httponly");StringAssert.Contains(header,"samesite=lax");
        var next=Http(header.Split(';')[0]);using var other=new SignupContext(options);Assert.AreEqual(1,new CurrentGarageUserService(other,next,new GarageSelectionService(next,_protection)).GetCurrentUser().GarageBusinessId);
    }
    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void MultipleMembershipsWithoutPreferenceRequireSelection(bool secondActive)
    {
        var options=Seed();using var db=new SignupContext(options);db.GarageBusinessUser.Single(x=>x.Id==2).IsActive=secondActive;db.SaveChanges();var a=Http();
        Assert.IsTrue(Assert.ThrowsException<GarageAccessException>(()=>new CurrentGarageUserService(db,a,new GarageSelectionService(a,_protection)).GetCurrentUser()).SelectionRequired);
    }
    [DataTestMethod] [DataRow("membership")] [DataRow("inactive")] [DataRow("blocked")] [DataRow("missing")]
    public void RememberedUnavailableGarageNeverFallsBack(string change)
    {
        var options=Seed();var cookie=Cookie(2);using var db=new SignupContext(options);
        if(change=="membership")db.GarageBusinessUser.Single(x=>x.Id==2).IsActive=false;
        else if(change=="missing")db.Remove(db.GarageBusiness.Single(x=>x.Id==2));
        else {var g=db.GarageBusiness.Single(x=>x.Id==2);g.Active=change!="inactive";g.Blocked=change=="blocked";}db.SaveChanges();
        var a=Http(cookie);Assert.IsTrue(Assert.ThrowsException<GarageAccessException>(()=>new CurrentGarageUserService(db,a,new GarageSelectionService(a,_protection)).GetCurrentUser()).SelectionRequired);
    }
    [TestMethod]
    public async Task RolesFollowSelectionAndIdentityIsCachedOnlyForRequest()
    {
        var options=Seed();using var db=new SignupContext(options);var a=Http(Cookie(1));var preference=new GarageSelectionService(a,_protection);var current=new CurrentGarageUserService(db,a,preference);var auth=new GarageAuthorizationService(current,a);
        Assert.IsTrue(await auth.CanManageGarageAsync());a.HttpContext!.Request.Headers.Cookie=Cookie(2);Assert.AreEqual(1,current.GetCurrentUser().GarageBusinessId);
        a.HttpContext=Http(Cookie(2)).HttpContext;Assert.AreEqual(2,current.GetCurrentUser().GarageBusinessId);Assert.IsFalse(await auth.CanManageGarageAsync());Assert.IsFalse(await auth.CanManageTeamAsync());Assert.AreEqual("Staff",current.GetCurrentUser().Role);
    }
    [DataTestMethod] [DataRow("tampered")] [DataRow("foreign-user")] [DataRow("expired")]
    public void InvalidRememberedPreferenceRequiresSelection(string kind)
    {
        var options=Seed();using var db=new SignupContext(options);var cookie=kind=="tampered"?GarageSelectionService.CookieName+"=corrupt":kind=="foreign-user"?Cookie(2,999):GarageSelectionService.CookieName+"="+_protection.CreateProtector("GarageSaas.GaragePreference.v1").ToTimeLimitedDataProtector().Protect("1:2",DateTimeOffset.UtcNow.AddSeconds(-1));
        var a=Http(cookie);Assert.IsTrue(Assert.ThrowsException<GarageAccessException>(()=>new CurrentGarageUserService(db,a,new GarageSelectionService(a,_protection)).GetCurrentUser()).SelectionRequired);
    }
    [TestMethod]
    public void FormContextsAreBoundToBothUserAndGarage()
    {
        var service=new GarageSelectionService(Http(),_protection);var token=service.ContextToken(1,2);
        Assert.IsTrue(service.MatchesContext(token,1,2));Assert.IsFalse(service.MatchesContext(token,1,1));Assert.IsFalse(service.MatchesContext(token,2,2));Assert.IsFalse(service.MatchesContext("corrupt",1,2));
    }
    [TestMethod]
    public void DifferentAccountDoesNotInheritPreferenceOrLoseSingleGarageAccess()
    {
        var options=Seed(false);using var db=new SignupContext(options);var a=Http(Cookie(2,999));
        Assert.AreEqual(1,new CurrentGarageUserService(db,a,new GarageSelectionService(a,_protection)).GetCurrentUser().GarageBusinessId);
    }}
