using System.Globalization;
using GarageSaas.Controllers;
using GarageSaas.Exceptions;
using GarageSaas.Services;
using GarageSaas.Services.Interfaces;
using GarageSaas.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignupAPI.Models;

namespace GarageSaas.UnitTests.Controllers;

[TestClass]
public class PlatformGarageStatusTests
{
    internal static PlatformAdminController Controller(SignupContext db,string subject="admin",ILogger<PlatformAdminController>? logger=null)
        => new(db,Mock.Of<IRegistrationRequestLock>(),new OnboardingInvitationService(new ConfigurationBuilder().Build(),Mock.Of<IEmailService>(),NullLogger<OnboardingInvitationService>.Instance),logger!)
        {ControllerContext=new ControllerContext{HttpContext=StaffInvitationData.Http(subject)}};
    internal static async Task<PlatformGarageStatusViewModel> Confirmation(PlatformAdminController c,int id,bool activate)
        => (PlatformGarageStatusViewModel)((ViewResult)(activate?await c.Activate(id):await c.Deactivate(id))).Model!;
    private static DbContextOptions<SignupContext> Seed(bool active=true,bool blocked=false)
    {
        var options=StaffInvitationData.Options();StaffInvitationData.Seed(options,userState:"inactive-membership");using var db=new SignupContext(options);
        var g=db.GarageBusiness.Single();g.Active=active;g.Blocked=blocked;db.Users.Single(x=>x.Id==1).IsPlatformAdministrator=true;
        var user=db.Users.Single(x=>x.Id==2);user.Active=false;user.Blocked=true;db.SaveChanges();return options;
    }
    [DataTestMethod] [DataRow(true,false)] [DataRow(false,false)] [DataRow(true,true)] [DataRow(false,true)]
    public async Task ToggleChangesOnlyGarageActiveAndAudit(bool active,bool blocked)
    {
        var options=Seed(active,blocked);using var db=new SignupContext(options);var log=new Mock<ILogger<PlatformAdminController>>();var c=Controller(db,logger:log.Object);
        var before=StaffInvitationData.Snapshot(db);var model=await Confirmation(c,1,!active);
        Assert.AreEqual(active,db.GarageBusiness.Single().Active,"GET confirmation must not mutate");
        var result=active?await c.Deactivate(model):await c.Activate(model);Assert.IsInstanceOfType<RedirectToActionResult>(result);
        using var check=new SignupContext(options);var g=check.GarageBusiness.Single();Assert.AreEqual(!active,g.Active);Assert.AreEqual(blocked,g.Blocked);
        Assert.AreEqual("admin@test.invalid",g.UpdatedBy);Assert.IsNotNull(g.UpdatedDate);Assert.AreEqual(before,StaffInvitationData.Snapshot(check));
        Assert.IsTrue(log.Invocations.Any(x=>x.Method.Name=="Log"&&x.Arguments[2].ToString()!.Contains("Committed")));
    }
    [DataTestMethod] [DataRow("active")] [DataRow("blocked")] [DataRow("audit")]
    public async Task StaleConfirmationReturnsConflictWithoutWrites(string change)
    {
        var options=Seed();using var db=new SignupContext(options);var c=Controller(db);var model=await Confirmation(c,1,false);
        using(var other=new SignupContext(options)){var g=other.GarageBusiness.Single();if(change=="active")g.Active=false;else if(change=="blocked")g.Blocked=true;else g.UpdatedDate=DateTime.UtcNow;other.SaveChanges();}
        using var before=new SignupContext(options);var expected=before.GarageBusiness.AsNoTracking().Single();
        Assert.IsInstanceOfType<ViewResult>(await c.Deactivate(model));Assert.AreEqual(409,c.Response.StatusCode);StringAssert.Contains(model.Error,"changed after confirmation");
        using var check=new SignupContext(options);var saved=check.GarageBusiness.Single();Assert.AreEqual(expected.Active,saved.Active);Assert.AreEqual(expected.Blocked,saved.Blocked);Assert.AreEqual(expected.UpdatedDate,saved.UpdatedDate);
    }
    [TestMethod]
    public async Task StatusCycleCannotReuseOldConfirmation()
    {
        var options=Seed();using var db=new SignupContext(options);var c=Controller(db);var stale=await Confirmation(c,1,false);
        Assert.IsInstanceOfType<RedirectToActionResult>(await c.Deactivate(await Confirmation(c,1,false)));
        Assert.IsInstanceOfType<RedirectToActionResult>(await c.Activate(await Confirmation(c,1,true)));
        await c.Deactivate(stale);Assert.AreEqual(409,c.Response.StatusCode);using var check=new SignupContext(options);Assert.IsTrue(check.GarageBusiness.Single().Active);
    }
    [TestMethod]
    public async Task DeactivationDeniesFreshTenantRequestWithoutLosingPlatformAccess()
    {
        var options=Seed();using var db=new SignupContext(options);var c=Controller(db);
        var accessor=new HttpContextAccessor{HttpContext=StaffInvitationData.Http("admin")};Assert.IsNotNull(new CurrentGarageUserService(db,accessor).GetCurrentUser());
        await c.Deactivate(await Confirmation(c,1,false));using var next=new SignupContext(options);
        Assert.ThrowsException<GarageAccessException>(()=>new CurrentGarageUserService(next,new HttpContextAccessor{HttpContext=StaffInvitationData.Http("admin")}).GetCurrentUser());
        Assert.IsInstanceOfType<ViewResult>(await Controller(next).Garages());
    }
    [TestMethod]
    public async Task MissingAndInvalidConfirmationAreSafe()
    {
        var options=Seed();using var db=new SignupContext(options);var c=Controller(db);
        Assert.IsInstanceOfType<NotFoundResult>(await c.Activate(999));Assert.IsInstanceOfType<BadRequestObjectResult>(await c.Deactivate(new PlatformGarageStatusViewModel{Id=1}));
        var missing=await Confirmation(c,1,false);missing.Id=999;Assert.IsInstanceOfType<NotFoundResult>(await c.Deactivate(missing));Assert.IsTrue(db.GarageBusiness.Single().Active);
    }
    [TestMethod]
    public async Task UncertainCommitDoesNotRetryAndReturnsSafeResult()
    {
        var options=Seed();using var db=new CommitFaultContext(options);var c=Controller(db);
        var result=await c.Deactivate(await Confirmation(c,1,false));Assert.IsInstanceOfType<ViewResult>(result);Assert.AreEqual(503,c.Response.StatusCode);
        Assert.AreEqual(1,db.Commits);Assert.AreEqual(1,db.Saves);Assert.IsFalse(db.ChangeTracker.HasChanges());
        using var check=new SignupContext(options);Assert.IsFalse(check.GarageBusiness.Single().Active,"The simulated lost acknowledgement must not claim that rollback undid a commit");
    }
    private sealed class CommitFaultContext:SignupContext
    {
        private readonly Mock<Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade> _facade;
        internal int Saves,Commits;
        public override Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database=>_facade?.Object??base.Database;
        internal CommitFaultContext(DbContextOptions<SignupContext> options):base(options)
        {
            _facade=new(this){CallBase=true};_facade.Setup(x=>x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(async(CancellationToken ct)=>{
                var real=await base.Database.BeginTransactionAsync(ct);var proxy=new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
                proxy.Setup(x=>x.CommitAsync(It.IsAny<CancellationToken>())).Returns(async(CancellationToken cancel)=>{Commits++;await real.CommitAsync(cancel);throw new IOException("Injected acknowledgement loss");});
                proxy.Setup(x=>x.Dispose()).Callback(()=>real.Dispose());return proxy.Object;});
        }
        public override Task<int> SaveChangesAsync(CancellationToken ct=default){Saves++;return base.SaveChangesAsync(ct);}
    }}
