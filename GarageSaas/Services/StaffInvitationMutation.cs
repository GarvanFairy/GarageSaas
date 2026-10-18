using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    // Staff only. No owner-onboarding changes, external calls or automatic retries.
    internal sealed class StaffInvitationMutation : IDisposable
    {
        private readonly SignupContext _db;
        private readonly IDbContextTransaction _transaction;
        private readonly Dictionary<object, PropertyValues> _original = new Dictionary<object, PropertyValues>();
        private readonly List<object> _created = new List<object>();
        private bool _committed;
        private readonly int? _timeout;
        internal GarageBusiness Garage { get; private set; }
        internal StaffInvitationMutation(SignupContext db)
        {
            _db = db;
            if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Save or discard pending changes before updating an invitation.");
            if (db.Database.IsSqlServer())
            {
                _timeout = db.Database.GetCommandTimeout();
                if (!_timeout.HasValue || _timeout == 0 || _timeout > 30) db.Database.SetCommandTimeout(30);
            }
            else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                throw new NotSupportedException("Staff invitation mutations require SQL Server key locking.");
            try { _transaction = db.Database.BeginTransaction(); }
            catch { if (db.Database.IsSqlServer()) db.Database.SetCommandTimeout(_timeout); throw; }
        }
        private bool LoadGarage(int garageId)
        {
            // A normal UPDATE of Active/Blocked needs an incompatible exclusive key
            // even if its writer does not call this helper. No exclusive garage mutex.
            var garages = _db.Database.IsSqlServer()
                ? _db.GarageBusiness.FromSqlInterpolated($@"SELECT * FROM [GarageBusiness] WITH (HOLDLOCK, ROWLOCK) WHERE [Id] = {garageId}").AsNoTracking()
                : _db.GarageBusiness.AsNoTracking();
            Garage = GarageAvailability.Eligible(garages, garageId).SingleOrDefault();
            return Garage != null;
        }
        internal GarageUserInvitation Load(int garageId, int invitationId)
        {
            if (!LoadGarage(garageId)) return null;
            var invitation = _db.Database.IsSqlServer()
                ? _db.GarageUserInvitation.FromSqlInterpolated($@"SELECT * FROM [GarageUserInvitation] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE [Id] = {invitationId} AND [GarageBusinessId] = {garageId}").AsNoTracking().SingleOrDefault()
                : _db.GarageUserInvitation.AsNoTracking().SingleOrDefault(x => x.Id == invitationId && x.GarageBusinessId == garageId);
            return invitation == null ? null : Fresh(invitation);
        }
        internal static string NormalizeRecipient(string email) => email?.Trim().ToUpperInvariant();

        internal bool ForCreation(int garageId, string email)
        {
            if (!LoadGarage(garageId)) return false;
            // An absent invitation has no row to lock. Coordinate only this garage/recipient.
            LockResource("GarageSaas:StaffInviteCreate:" + garageId + ":" +
                GarageInvitationTokenService.HashToken(NormalizeRecipient(email)));
            return true;
        }

        private void LockResource(string resource)
        {
            if (!_db.Database.IsSqlServer()) return; // Functional InMemory tests only.
            using var command = _db.Database.GetDbConnection().CreateCommand();
            command.Transaction = _transaction.GetDbTransaction();
            command.CommandTimeout = _db.Database.GetCommandTimeout() ?? 30;
            command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@key, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; SELECT @result;";
            var key = command.CreateParameter(); key.ParameterName = "@key"; key.Value = resource;
            command.Parameters.Add(key);
            if (Convert.ToInt32(command.ExecuteScalar()) < 0)
                throw new InvalidOperationException("Invitation coordination was unavailable.");
        }

        internal Users User(string externalId, string email)
        {
            if (_db.Database.IsSqlServer())
            {
                // Email has no unique index. A narrow staff-acceptance email mutex
                // preserves the staff account-collision check without a table/range scan lock.
                foreach (var resource in new[] { "email:" + email.Trim().ToUpperInvariant(), "identity:" + externalId.ToUpperInvariant() })
                    LockResource("GarageSaas:StaffInviteAccount:" + GarageInvitationTokenService.HashToken(resource));
            }
            // Classify without update-lock scans, then lock the concrete primary key.
            var userId = _db.Users.AsNoTracking().Where(x => x.ExternalUserId == externalId).Select(x => (int?)x.Id).SingleOrDefault();
            if (!userId.HasValue) return null;
            var user = _db.Database.IsSqlServer()
                ? _db.Users.FromSqlInterpolated($@"SELECT * FROM [Users] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE [Id] = {userId.Value}").AsNoTracking().SingleOrDefault()
                : _db.Users.AsNoTracking().SingleOrDefault(x => x.Id == userId.Value);
            if (user == null || !string.Equals(user.ExternalUserId, externalId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invitation identity changed during validation.");
            return Fresh(user);
        }
        // The held Users update key serializes membership creation for this identity. Avoid
        // absent composite-key range locks spanning unrelated garage memberships.
        internal GarageBusinessUser Membership(int garageId, int userId)
        {
            var member = _db.Database.IsSqlServer()
                ? _db.GarageBusinessUser.FromSqlInterpolated($@"SELECT * FROM [GarageBusinessUser] WITH (UPDLOCK, ROWLOCK)
                    WHERE [GarageBusinessId] = {garageId} AND [UserId] = {userId}").AsNoTracking().SingleOrDefault()
                : _db.GarageBusinessUser.AsNoTracking().SingleOrDefault(x => x.GarageBusinessId == garageId && x.UserId == userId);
            return member == null ? null : Fresh(member);
        }
        private T Fresh<T>(T snapshot) where T : class
        {
            var id = _db.Entry(snapshot).Property("Id").CurrentValue;
            var tracked = _db.ChangeTracker.Entries<T>().SingleOrDefault(x => Equals(x.Property("Id").CurrentValue, id));
            if (tracked == null) { _db.Attach(snapshot); _original[snapshot] = _db.Entry(snapshot).CurrentValues.Clone(); return snapshot; }
            tracked.CurrentValues.SetValues(snapshot); tracked.OriginalValues.SetValues(snapshot);
            _original[tracked.Entity] = tracked.CurrentValues.Clone(); return tracked.Entity;
        }
        internal void Add(object entity) { _created.Add(entity); _db.Add(entity); }
        internal void Commit()
        {
            try { _transaction.Commit(); _committed = true; }
            catch (Exception error) { throw new InvalidOperationException("Invitation update could not be confirmed. Refresh before trying again; do not automatically retry.", error); }
        }
        public void Dispose()
        {
            if (!_committed)
            {
                try { _transaction.Rollback(); } catch (Exception) { /* Cannot prove the outcome of a failed commit acknowledgement. */ }
                foreach (var entity in _created.AsEnumerable().Reverse())
                {
                    _db.Entry(entity).State = EntityState.Detached;
                    if (entity is GarageBusinessUser m) { m.Id = 0; m.UserId = 0; }
                    if (entity is Users u) u.Id = 0;
                    if (entity is GarageUserInvitation invitation) invitation.Id = 0;
                }
                foreach (var entry in _original)
                { var tracked = _db.Entry(entry.Key); tracked.CurrentValues.SetValues(entry.Value); tracked.State = EntityState.Detached; }
            }
            try { _transaction.Dispose(); } catch (Exception) { /* Preserve known/uncertain result. */ }
            if (_db.Database.IsSqlServer()) _db.Database.SetCommandTimeout(_timeout);
        }
    }
}
