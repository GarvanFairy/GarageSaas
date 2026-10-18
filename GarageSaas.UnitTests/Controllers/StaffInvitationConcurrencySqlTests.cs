using GarageSaas.Services;
using GarageSaas.Controllers;
using GarageSaas.UnitTests.Controllers;
using GarageSaas.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SignupAPI.Models;
namespace GarageSaas.UnitTests.Controllers;

[TestClass, DoNotParallelize, TestCategory("SqlServer"), TestCategory("SqlStaffInvitations")]
public class StaffInvitationConcurrencySqlTests
{
    private DisposableSqlDatabase? _database; private static bool _unsafeCleanup;
    public TestContext TestContext { get; set; } = null!;
    [TestInitialize]
    public void Initialize()
    {
        if (_unsafeCleanup) Assert.Inconclusive("Uncertain cleanup; refuse further database creation.");
        _database = DisposableSqlDatabase.Start("GARAGESAAS_RUN_STAFF_INVITATION_SQL_TESTS", "QuoteCoordination");
        _database.Create(); TestContext.WriteLine($"Owned disposable database: {_database.DatabaseName}");
        using var connection = _database.OpenOwnedConnection(); using var command = connection.CreateCommand();
        command.CommandText = @"SELECT t.name,i.name,i.is_unique,c.name,ic.key_ordinal
            FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE ic.key_ordinal>0 AND i.name IN ('UX_GarageUserInvitation_TokenHash','UX_GarageBusinessUser_Garage_User','UX_Users_ExternalUserId')";
        using var reader = command.ExecuteReader(); var keys = new List<string>();
        while (reader.Read()) { Assert.IsTrue(reader.GetBoolean(2)); keys.Add($"{reader.GetString(0)}:{reader.GetString(3)}:{reader.GetByte(4)}"); }
        CollectionAssert.AreEquivalent(new[] { "GarageUserInvitation:InvitationTokenHash:1", "GarageBusinessUser:GarageBusinessId:1", "GarageBusinessUser:UserId:2", "Users:ExternalUserId:1" }, keys);
    }
    [TestCleanup]
    public async Task Cleanup()
    {
        if (_database == null) return;
        try { await _database.DeleteAsync(); TestContext.WriteLine($"Guarded deletion completed: {_database.DatabaseName}"); }
        catch { _unsafeCleanup = true; throw; }
    }
    private sealed record Seeded(int Garage, int Invite, int Admin, int? User, string Token, string Subject);
    private Seeded Seed(string subject = "invitee", bool newUser = false)
    {
        using var db = new SignupContext(_database!.Options()); var now = DateTime.UtcNow;
        var g = new GarageBusiness { GarageBusinessName = "Disposable invitation garage", Active = true, CreatedDate = now };
        db.Add(g); db.SaveChanges();
        var admin = new Users { ExternalUserId = "admin" + g.Id, FirstName = "Admin", LastName = "Test", EmailAddress = "admin@test.invalid", Active = true, CreatedDate = now };
        Users? user = newUser ? null : new Users { ExternalUserId = subject, FirstName = "Staff", LastName = "Test", EmailAddress = subject + "@test.invalid", Active = true, CreatedDate = now };
        db.Add(admin); if (user != null) db.Add(user); db.SaveChanges();
        db.Add(new GarageBusinessUser { GarageBusinessId = g.Id, UserId = admin.Id, IsActive = true, Role = "Administrator", CreatedDate = now });
        var token = GarageInvitationTokenService.GenerateToken(); var invite = new GarageUserInvitation { GarageBusinessId = g.Id, EmailAddress = subject + "@test.invalid", FirstName = "Staff", LastName = "Test", Role = "Staff", InvitedByUserId = admin.Id, InvitationTokenHash = GarageInvitationTokenService.HashToken(token), ExpiresDate = now.AddDays(1), CreatedDate = now };
        db.Add(invite); db.SaveChanges(); return new(g.Id,invite.Id,admin.Id,user?.Id,token,subject);
    }
    private GarageTeamController Team(SignupContext db, Seeded s, List<string> urls)
    {
        var accessor = new HttpContextAccessor { HttpContext = StaffInvitationData.Http("admin" + s.Garage) };
        return StaffInvitationData.Team(db, accessor, new CurrentGarageUserService(db, accessor), StaffInvitationData.Email(urls).Object);
    }
    private async Task Pair(Action first, Action second, Action verify)
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        _gate = () => { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("First worker gate"); };
        var a = _database!.Track(Task.Run(first));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(8)), "First worker reached protected save");
            var b = _database.Track(Task.Run(second));
            var observed=false; var deadline=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<deadline&&!b.IsCompleted)
            {
                using var c=_database.OpenOwnedConnection();using var q=c.CreateCommand();
                q.CommandText="SELECT TOP(1) wait_type FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id>0 AND session_id<>@@SPID";
                var wait=q.ExecuteScalar();if(wait!=null){TestContext.WriteLine("Observed competing SQL wait: "+wait);observed=true;break;}
                await Task.Delay(50);
            }
            Assert.IsTrue(observed,"Competing SQL request reached a real lock wait");Assert.IsFalse(b.IsCompleted, "Competing operation must wait");
            release.Set(); await Task.WhenAll(a,b).WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally { release.Set(); await _database.DrainAsync(); _gate = null; }
        verify();
    }
    private Action? _gate;
    private IActionResult Accept(SignupContext db, Seeded s) => StaffInvitationData.Invitation(db,s.Subject).Complete(s.Token);
    private Task<IActionResult> Mutate(SignupContext db, Seeded s, bool resend, List<string> urls) => resend ? Team(db,s,urls).ResendInvitation(s.Invite) : Team(db,s,urls).RevokeInvitation(s.Invite);
    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public async Task AcceptanceVersusMutation(bool acceptanceFirst, bool resend)
    {
        var s=Seed(); var urls=new List<string>(); IActionResult? accepted=null, mutated=null;
        await Pair(() => { using var db=new GateContext(_database!.Options(),()=>_gate!()); if(acceptanceFirst) accepted=Accept(db,s); else mutated=Mutate(db,s,resend,urls).GetAwaiter().GetResult(); },
            () => { using var db=new SignupContext(_database!.Options()); if(acceptanceFirst) mutated=Mutate(db,s,resend,urls).GetAwaiter().GetResult(); else accepted=Accept(db,s); },
            () => {
                using var db=new SignupContext(_database!.Options()); var i=db.GarageUserInvitation.Single();
                Assert.AreEqual(acceptanceFirst,i.Accepted); Assert.AreEqual(!acceptanceFirst&&!resend,i.Revoked);
                Assert.AreEqual(acceptanceFirst?1:0,db.GarageBusinessUser.Count(x=>x.UserId==s.User));
                if(acceptanceFirst) { Assert.IsInstanceOfType<RedirectToActionResult>(accepted); Assert.IsInstanceOfType<NotFoundResult>(mutated); Assert.AreEqual(0,urls.Count); }
                else { Assert.AreEqual("InvalidInvitation",((ViewResult)accepted!).ViewName); Assert.AreEqual(resend?1:0,urls.Count); }
            });
        if(!acceptanceFirst&&resend)
        {
            using var db=new SignupContext(_database!.Options());
            var token=Uri.UnescapeDataString(new Uri(urls.Single()).Query.Substring("?token=".Length));
            Assert.IsInstanceOfType<RedirectToActionResult>(StaffInvitationData.Invitation(db,s.Subject).Complete(token));
        }
    }
    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public async Task AcceptanceVersusGarageStatus(bool acceptanceFirst,bool block)
    {
        var s=Seed(); IActionResult? accepted=null;
        void Status(bool gate)
        {
            using var db=new SignupContext(_database!.Options()); using var tx=db.Database.BeginTransaction();
            var g=db.GarageBusiness.Single(x=>x.Id==s.Garage); if(block)g.Blocked=true;else g.Active=false;
            db.SaveChanges(); if(gate)_gate!(); tx.Commit();
        }
        await Pair(() => { if(acceptanceFirst) {using var db=new GateContext(_database!.Options(),()=>_gate!()); accepted=Accept(db,s);}else Status(true); },
            ()=> {if(acceptanceFirst) Status(false);else {using var db=new SignupContext(_database!.Options()); accepted=Accept(db,s);}},
            ()=> {using var db=new SignupContext(_database!.Options()); Assert.AreEqual(acceptanceFirst,db.GarageUserInvitation.Single().Accepted); Assert.AreEqual(acceptanceFirst?1:0,db.GarageBusinessUser.Count(x=>x.UserId==s.User)); if(acceptanceFirst)Assert.IsInstanceOfType<RedirectToActionResult>(accepted);else Assert.AreEqual("InvalidInvitation",((ViewResult)accepted!).ViewName);});
    }
    [TestMethod]
    public async Task SimultaneousAcceptanceCreatesOneMembership()
    {
        var s=Seed(); IActionResult? a=null,b=null;
        await Pair(()=> {using var db=new GateContext(_database!.Options(),()=>_gate!());a=Accept(db,s);},()=>{using var db=new SignupContext(_database!.Options());b=Accept(db,s);},()=>{
            Assert.IsInstanceOfType<RedirectToActionResult>(a);Assert.AreEqual("InvalidInvitation",((ViewResult)b!).ViewName);
            using var db=new SignupContext(_database!.Options());Assert.AreEqual(1,db.GarageBusinessUser.Count(x=>x.UserId==s.User));});
    }
    [DataTestMethod] [DataRow(1)] [DataRow(2)]
    public void SaveFailureRollsBackNewUserAndMembership(int failSave)
    {
        var s=Seed(newUser:true); var calls=0;
        using(var db=new FaultContext(_database!.Options(),()=> {if(++calls==failSave)throw new DbUpdateException("Injected after save");}))
        {
            var controller=StaffInvitationData.Invitation(db); Assert.AreEqual("InvitationUnavailable",((ViewResult)controller.Complete(s.Token)).ViewName);
            Assert.AreEqual(503,controller.Response.StatusCode); Assert.IsFalse(db.ChangeTracker.HasChanges()); db.SaveChanges();
        }
        using var check=new SignupContext(_database.Options()); Assert.IsFalse(check.Users.Any(x=>x.ExternalUserId==s.Subject)); Assert.IsFalse(check.GarageUserInvitation.Single().Accepted);
        Assert.AreEqual(1,check.GarageBusinessUser.Count());
    }
    [TestMethod]
    public void InactiveMembershipNotReactivated()
    {
        var s=Seed(); using(var db=new SignupContext(_database!.Options())){db.Add(new GarageBusinessUser{GarageBusinessId=s.Garage,UserId=s.User!.Value,Role="Staff",IsActive=false,CreatedDate=DateTime.UtcNow});db.SaveChanges();}
        using(var db=new SignupContext(_database.Options()))Assert.IsInstanceOfType<ForbidResult>(Accept(db,s));
        using var check=new SignupContext(_database.Options());Assert.IsFalse(check.GarageUserInvitation.Single().Accepted);Assert.IsFalse(check.GarageBusinessUser.Single(x=>x.UserId==s.User).IsActive);
    }
    [TestMethod]
    public void SecondMembershipPreservesExplicitSelection()
    {
        var s=Seed();using(var db=new SignupContext(_database!.Options())){
            var g=new GarageBusiness{Active=true,GarageBusinessName="Other",CreatedDate=DateTime.UtcNow};db.Add(g);db.SaveChanges();db.Add(new GarageBusinessUser{GarageBusinessId=g.Id,UserId=s.User!.Value,IsActive=true,Role="Staff",CreatedDate=DateTime.UtcNow});db.SaveChanges();}
        using(var db=new SignupContext(_database.Options()))Assert.IsInstanceOfType<RedirectToActionResult>(Accept(db,s));
        using var check=new SignupContext(_database.Options());var accessor=new HttpContextAccessor{HttpContext=StaffInvitationData.Http(s.Subject)};
        Assert.ThrowsException<GarageSaas.Exceptions.GarageAccessException>(()=>new CurrentGarageUserService(check,accessor).GetCurrentUser()); Assert.AreEqual(2,check.GarageBusinessUser.Count(x=>x.UserId==s.User));
    }
    [TestMethod]
    public async Task UnrelatedGarageAcceptanceContinues()
    {
        var a=Seed("first");var b=Seed("second");using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var first=_database!.Track(Task.Run(()=>{using var db=new GateContext(_database.Options(),()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException();});return Accept(db,a);}));
        try {Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(8)));var second=_database.Track(Task.Run(()=>{using var db=new SignupContext(_database.Options());return Accept(db,b);}));await Task.Delay(300); if(!second.IsCompleted) { using var c=_database.OpenOwnedConnection(); using var q=c.CreateCommand(); q.CommandText="SELECT r.wait_type,r.blocking_session_id,t.text FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.database_id=DB_ID() AND r.session_id<>@@SPID"; using var r=q.ExecuteReader(); while(r.Read())TestContext.WriteLine($"WAIT: {r[0]} blocker {r[1]} SQL {r[2]}"); } Assert.IsInstanceOfType<RedirectToActionResult>(await second.WaitAsync(TimeSpan.FromSeconds(5)));}
        finally{release.Set();await _database.DrainAsync();}Assert.IsInstanceOfType<RedirectToActionResult>(await first);
    }
    [TestMethod]
    public async Task ForeignInvitationIdsAreNotMutable()
    {
        var a=Seed("first");var b=Seed("second");using var db=new SignupContext(_database!.Options());var urls=new List<string>();
        Assert.IsInstanceOfType<NotFoundResult>(await Team(db,a,urls).RevokeInvitation(b.Invite));Assert.IsInstanceOfType<NotFoundResult>(await Team(db,a,urls).ResendInvitation(b.Invite));
        Assert.AreEqual(0,urls.Count);Assert.IsFalse(db.GarageUserInvitation.Single(x=>x.Id==b.Invite).Revoked);
    }
    [TestMethod]
    public async Task SeparateInvitationsForSameIdentityCreateOneMembership()
    {
        var s=Seed(newUser:true);int otherId;string otherToken=GarageInvitationTokenService.GenerateToken();
        using(var db=new SignupContext(_database!.Options())){var i=db.GarageUserInvitation.AsNoTracking().Single();i.Id=0;i.InvitationTokenHash=GarageInvitationTokenService.HashToken(otherToken);db.Add(i);db.SaveChanges();otherId=i.Id;}
        var second=s with {Invite=otherId,Token=otherToken};IActionResult? a=null,b=null;
        await Pair(()=>{using var db=new GateContext(_database!.Options(),()=>_gate!());a=Accept(db,s);},()=>{using var db=new SignupContext(_database!.Options());b=Accept(db,second);},()=>{
            Assert.IsInstanceOfType<RedirectToActionResult>(a);Assert.IsInstanceOfType<RedirectToActionResult>(b);
            using var db=new SignupContext(_database!.Options());var u=db.Users.Single(x=>x.ExternalUserId==s.Subject);Assert.AreEqual(1,db.GarageBusinessUser.Count(x=>x.UserId==u.Id));Assert.AreEqual(2,db.GarageUserInvitation.Count(x=>x.Accepted));});
    }
    [TestMethod]
    public void CommitAcknowledgementLossIsNotRetried()
    {
        var s=Seed();using var db=new CommitFaultContext(_database!.Options());var c=StaffInvitationData.Invitation(db);var result=c.Complete(s.Token);
        Assert.AreEqual("InvitationUnavailable",((ViewResult)result).ViewName);Assert.AreEqual(503,c.Response.StatusCode);Assert.AreEqual(1,db.Commits);Assert.AreEqual(1,db.Saves);Assert.IsFalse(db.ChangeTracker.HasChanges());
        using var check=new SignupContext(_database.Options());Assert.IsTrue(check.GarageUserInvitation.Single().Accepted);Assert.AreEqual(1,check.GarageBusinessUser.Count(x=>x.UserId==s.User));
    }
    [TestMethod]
    public void StaleTrackedInvitationCannotAcceptRevokedState()
    {
        var s=Seed();using var stale=new SignupContext(_database!.Options());var tracked=stale.GarageUserInvitation.Single();
        using(var change=new SignupContext(_database.Options())){var i=change.GarageUserInvitation.Single();i.Revoked=true;change.SaveChanges();}
        Assert.AreEqual("InvalidInvitation",((ViewResult)Accept(stale,s)).ViewName);Assert.IsTrue(tracked.Revoked);Assert.IsFalse(stale.ChangeTracker.HasChanges());
    }
    [TestMethod]
    public void PendingUnrelatedChangesArePreservedAndNotSaved()
    {
        var s=Seed();using var db=new SignupContext(_database!.Options());var g=db.GarageBusiness.Single();g.GarageBusinessName="Unsaved";
        Assert.AreEqual("InvitationUnavailable",((ViewResult)Accept(db,s)).ViewName);Assert.AreEqual(EntityState.Modified,db.Entry(g).State);
        using var check=new SignupContext(_database.Options());Assert.AreNotEqual("Unsaved",check.GarageBusiness.Single().GarageBusinessName);Assert.IsFalse(check.GarageUserInvitation.Single().Accepted);
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public async Task MutationSaveFailureRollsBack(bool resend)
    {
        var s=Seed();using var db=new AsyncFaultContext(_database!.Options());var urls=new List<string>();var before=StaffInvitationData.Snapshot(db);
        Assert.IsInstanceOfType<RedirectToActionResult>(await Mutate(db,s,resend,urls));Assert.AreEqual(0,urls.Count);Assert.IsFalse(db.ChangeTracker.HasChanges());
        using var check=new SignupContext(_database.Options());Assert.AreEqual(before,StaffInvitationData.Snapshot(check));
    }
    [TestMethod]
    public void ExistingActiveMembershipPreservesRoleAndOwnership()
    {
        var s=Seed();using(var db=new SignupContext(_database!.Options())){db.Add(new GarageBusinessUser{GarageBusinessId=s.Garage,UserId=s.User!.Value,Role="Administrator",IsOwner=true,IsActive=true,CreatedDate=DateTime.UtcNow});db.SaveChanges();}
        using(var db=new SignupContext(_database.Options()))Assert.IsInstanceOfType<RedirectToActionResult>(Accept(db,s));
        using var check=new SignupContext(_database.Options());var m=check.GarageBusinessUser.Single(x=>x.UserId==s.User);Assert.AreEqual("Administrator",m.Role);Assert.IsTrue(m.IsOwner);Assert.IsTrue(m.IsActive);
    }
    [TestMethod]
    public async Task ExpirationCommittedFirstIsRevalidatedAfterWait()
    {
        var s=Seed();IActionResult? accepted=null;
        await Pair(()=>{using var db=new SignupContext(_database!.Options());using var tx=db.Database.BeginTransaction();var i=db.GarageUserInvitation.Single();i.ExpiresDate=DateTime.UtcNow.AddMinutes(-1);db.SaveChanges();_gate!();tx.Commit();},
            ()=>{using var db=new SignupContext(_database!.Options());accepted=Accept(db,s);},
            ()=>{Assert.AreEqual("InvalidInvitation",((ViewResult)accepted!).ViewName);using var db=new SignupContext(_database!.Options());Assert.IsFalse(db.GarageUserInvitation.Single().Accepted);Assert.AreEqual(0,db.GarageBusinessUser.Count(x=>x.UserId==s.User));});
    }    private GarageTeamController CreationController(SignupContext db,Seeded s,List<string> urls)
    {
        var email=new Moq.Mock<GarageSaas.Services.Interfaces.IEmailService>();
        email.Setup(x=>x.SendGarageInvitationAsync(Moq.It.IsAny<string>(),Moq.It.IsAny<string>(),Moq.It.IsAny<string>(),Moq.It.IsAny<string>()))
            .Callback<string,string,string,string>((address,_,_,url)=>{
                Assert.IsNull(db.Database.CurrentTransaction,"Email must be outside the completed transaction");
                using var check=new SignupContext(_database!.Options());Assert.IsTrue(check.GarageUserInvitation.Any(x=>x.EmailAddress==address),"Independent connection sees committed invitation before email");urls.Add(url);
            }).Returns(Task.CompletedTask);
        var accessor=new HttpContextAccessor{HttpContext=StaffInvitationData.Http("admin"+s.Garage)};
        return StaffInvitationData.Team(db,accessor,new CurrentGarageUserService(db,accessor),email.Object);
    }
    private static GarageSaas.Models.InviteGarageUserViewModel CreationInput(string email)
    {var model=StaffInvitationData.Input();model.EmailAddress=email;return model;}

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public async Task ConcurrentCreationForNormalizedRecipientCreatesOnePending(bool varyCase)
    {
        var s=Seed();var urls=new List<string>();IActionResult? a=null,b=null;
        await Pair(()=>{using var db=new GateContext(_database!.Options(),()=>_gate!());a=CreationController(db,s,urls).Invite(CreationInput("Recipient@Test.invalid")).GetAwaiter().GetResult();},
            ()=>{using var db=new SignupContext(_database!.Options());b=CreationController(db,s,urls).Invite(CreationInput(varyCase?"  RECIPIENT@test.INVALID  ":"Recipient@Test.invalid")).GetAwaiter().GetResult();},
            ()=>{Assert.IsInstanceOfType<RedirectToActionResult>(a);Assert.IsInstanceOfType<ViewResult>(b);Assert.AreEqual(1,urls.Count);using var db=new SignupContext(_database!.Options());Assert.AreEqual(1,db.GarageUserInvitation.Count(x=>x.EmailAddress=="Recipient@Test.invalid"&&!x.Accepted&&!x.Revoked));});
    }
    [TestMethod]
    public async Task CreationForSameRecipientInDifferentGaragesContinues()
    {
        var a=Seed("first");var b=Seed("second");var urlsA=new List<string>();var urlsB=new List<string>();
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var first=_database!.Track(Task.Run(async()=>{using var db=new GateContext(_database.Options(),()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException();});return await CreationController(db,a,urlsA).Invite(CreationInput("recipient@test.invalid"));}));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(8)));var second=_database.Track(Task.Run(async()=>{using var db=new SignupContext(_database.Options());return await CreationController(db,b,urlsB).Invite(CreationInput("recipient@test.invalid"));}));
            Assert.IsInstanceOfType<RedirectToActionResult>(await second.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally {release.Set();await _database.DrainAsync();}
        Assert.IsInstanceOfType<RedirectToActionResult>(await first);Assert.AreEqual(1,urlsA.Count);Assert.AreEqual(1,urlsB.Count);
        using var check=new SignupContext(_database.Options());Assert.AreEqual(2,check.GarageUserInvitation.Count(x=>x.EmailAddress=="recipient@test.invalid"));
    }
    [TestMethod]
    public async Task CreationFailureAfterSaveRollsBackAndSendsNoEmail()
    {
        var s=Seed();using var db=new CreationFaultContext(_database!.Options());var urls=new List<string>();
        Assert.IsInstanceOfType<ViewResult>(await CreationController(db,s,urls).Invite(CreationInput("recipient@test.invalid")));
        Assert.AreEqual(0,urls.Count);Assert.IsFalse(db.ChangeTracker.HasChanges());
        using var check=new SignupContext(_database.Options());Assert.AreEqual(1,check.GarageUserInvitation.Count());
        Assert.IsNotNull(db.Created);Assert.AreEqual(0,db.Created!.Id);
        db.Fail=false;await db.SaveChangesAsync();Assert.AreEqual(1,check.GarageUserInvitation.Count());
    }    [TestMethod]
    public async Task CreationCommitUncertaintyDoesNotRetryOrSendEmail()
    {
        var s=Seed();using var db=new CommitFaultContext(_database!.Options());var urls=new List<string>();
        Assert.IsInstanceOfType<ViewResult>(await CreationController(db,s,urls).Invite(CreationInput("recipient@test.invalid")));
        Assert.AreEqual(1,db.Commits);Assert.AreEqual(0,urls.Count);Assert.IsFalse(db.ChangeTracker.HasChanges());
        using var check=new SignupContext(_database.Options());Assert.AreEqual(1,check.GarageUserInvitation.Count(x=>x.EmailAddress=="recipient@test.invalid"));
    }
    private sealed class CreationFaultContext(DbContextOptions<SignupContext> options):SignupContext(options)
    {
        internal bool Fail=true;internal GarageUserInvitation? Created;
        public override async Task<int> SaveChangesAsync(CancellationToken ct=default)
        {
            if(Fail)Created=ChangeTracker.Entries<GarageUserInvitation>().Single(x=>x.State==EntityState.Added).Entity;
            var n=await base.SaveChangesAsync(ct);if(Fail)throw new DbUpdateException("Injected after creation save");return n;
        }
    }    private void EnablePlatform(Seeded s,string? second=null)
    {
        using var db=new SignupContext(_database!.Options());db.Users.Single(x=>x.Id==s.Admin).IsPlatformAdministrator=true;
        if(second!=null)db.Add(new Users{ExternalUserId=second,EmailAddress=second+"@test.invalid",FirstName="Platform",LastName="Two",Active=true,IsPlatformAdministrator=true,CreatedDate=DateTime.UtcNow});db.SaveChanges();
    }
    [TestMethod]
    public async Task PlatformStatusConcurrentAdministratorsRejectStaleConfirmation()
    {
        var s=Seed();EnablePlatform(s,"platform2");GarageSaas.ViewModels.PlatformGarageStatusViewModel a,b;
        using(var db=new SignupContext(_database!.Options())){var c=PlatformGarageStatusTests.Controller(db,"admin"+s.Garage);a=await PlatformGarageStatusTests.Confirmation(c,s.Garage,false);b=await PlatformGarageStatusTests.Confirmation(c,s.Garage,false);}
        IActionResult? first=null,second=null;int secondStatus=0;
        await Pair(()=>{using var db=new AfterSaveGateContext(_database!.Options(),()=>_gate!());first=PlatformGarageStatusTests.Controller(db,"admin"+s.Garage).Deactivate(a).GetAwaiter().GetResult();},
            ()=>{using var db=new SignupContext(_database!.Options());var c=PlatformGarageStatusTests.Controller(db,"platform2");second=c.Deactivate(b).GetAwaiter().GetResult();secondStatus=c.Response.StatusCode;},
            ()=>{Assert.IsInstanceOfType<RedirectToActionResult>(first);Assert.IsInstanceOfType<ViewResult>(second);Assert.AreEqual(409,secondStatus);using var db=new SignupContext(_database!.Options());Assert.IsFalse(db.GarageBusiness.Single().Active);Assert.AreEqual("admin@test.invalid",db.GarageBusiness.Single().UpdatedBy);});
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public async Task PlatformStatusDeactivationVersusAcceptance(bool acceptanceFirst)
    {
        var s=Seed();EnablePlatform(s);GarageSaas.ViewModels.PlatformGarageStatusViewModel model;
        using(var db=new SignupContext(_database!.Options()))model=await PlatformGarageStatusTests.Confirmation(PlatformGarageStatusTests.Controller(db,"admin"+s.Garage),s.Garage,false);
        IActionResult? accepted=null,deactivated=null;
        await Pair(()=>{if(acceptanceFirst){using var db=new GateContext(_database!.Options(),()=>_gate!());accepted=Accept(db,s);}else{using var db=new AfterSaveGateContext(_database!.Options(),()=>_gate!());deactivated=PlatformGarageStatusTests.Controller(db,"admin"+s.Garage).Deactivate(model).GetAwaiter().GetResult();}},
            ()=>{using var db=new SignupContext(_database!.Options());if(acceptanceFirst)deactivated=PlatformGarageStatusTests.Controller(db,"admin"+s.Garage).Deactivate(model).GetAwaiter().GetResult();else accepted=Accept(db,s);},
            ()=>{Assert.IsInstanceOfType<RedirectToActionResult>(deactivated);using var db=new SignupContext(_database!.Options());Assert.IsFalse(db.GarageBusiness.Single().Active);Assert.AreEqual(acceptanceFirst,db.GarageUserInvitation.Single().Accepted);Assert.AreEqual(acceptanceFirst?1:0,db.GarageBusinessUser.Count(x=>x.UserId==s.User));if(acceptanceFirst)Assert.IsInstanceOfType<RedirectToActionResult>(accepted);else Assert.AreEqual("InvalidInvitation",((ViewResult)accepted!).ViewName);});
    }
    private sealed class AfterSaveGateContext(DbContextOptions<SignupContext> options,Action gate):SignupContext(options)
    {public override async Task<int> SaveChangesAsync(CancellationToken ct=default){var n=await base.SaveChangesAsync(ct);gate();return n;}}    private sealed class AsyncFaultContext(DbContextOptions<SignupContext> options):SignupContext(options)
    {public override async Task<int> SaveChangesAsync(CancellationToken ct=default){await base.SaveChangesAsync(ct);throw new DbUpdateException("Injected after mutation save");}}
    private sealed class CommitFaultContext:SignupContext
    {
        private readonly Moq.Mock<Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade> _facade;
        internal int Saves,Commits;
        public override Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database=>_facade?.Object??base.Database;
        internal CommitFaultContext(DbContextOptions<SignupContext> options):base(options)
        {
            _facade=new(this){CallBase=true};_facade.Setup(x=>x.BeginTransaction()).Returns(()=>{
                var real=base.Database.BeginTransaction();var proxy=new Moq.Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
                proxy.As<Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<System.Data.Common.DbTransaction>>().SetupGet(x=>x.Instance).Returns(Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(real));
                proxy.Setup(x=>x.Commit()).Callback(()=>{Commits++;real.Commit();throw new IOException("Injected lost acknowledgement");});
                proxy.Setup(x=>x.Rollback()).Callback(()=>real.Rollback());proxy.Setup(x=>x.Dispose()).Callback(()=>real.Dispose());return proxy.Object;});
        }
        public override int SaveChanges(){Saves++;return base.SaveChanges();}
    }    private sealed class FaultContext(DbContextOptions<SignupContext> options,Action after) : SignupContext(options)
    {public override int SaveChanges(){var n=base.SaveChanges();after();return n;}}
    private sealed class GateContext(DbContextOptions<SignupContext> options, Action gate) : SignupContext(options)
    {
        public override int SaveChanges() { gate(); return base.SaveChanges(); }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) { gate(); return base.SaveChangesAsync(ct); }
    }
}
