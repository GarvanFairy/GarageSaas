using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    internal static class CustomerReferenceLock
    {
        // Financial writers share customer keys. Deletion owns the same key exclusively.
        // Take distinct customer keys ascending BEFORE quote/invoice/vehicle/work-item locks.
        internal static GarageBusinessCustomer Read(SignupContext db, int id, int garage, bool deleting = false)
        {
            if (db.Database.IsSqlServer())
            {
                if (db.Database.CurrentTransaction == null)
                    throw new InvalidOperationException("Customer coordination requires an active transaction.");
                return deleting
                    ? db.GarageBusinessCustomer.FromSqlInterpolated($@"SELECT * FROM [GarageBusinessCustomer] WITH (XLOCK, HOLDLOCK, ROWLOCK)
                        WHERE [Id] = {id} AND [GarageBusinessId] = {garage}").AsNoTracking().SingleOrDefault()
                    : db.GarageBusinessCustomer.FromSqlInterpolated($@"SELECT * FROM [GarageBusinessCustomer] WITH (HOLDLOCK, ROWLOCK)
                        WHERE [Id] = {id} AND [GarageBusinessId] = {garage}").AsNoTracking().SingleOrDefault();
            }
            if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                throw new NotSupportedException("Customer coordination requires SQL Server key locking.");
            // Functional tests only; no relational/concurrency guarantee.
            return db.GarageBusinessCustomer.AsNoTracking().SingleOrDefault(x => x.Id == id && x.GarageBusinessId == garage);
        }

        internal static bool Acquire(SignupContext db, int garage, IEnumerable<int?> ids)
        {
            // Existing validation still decides zero/null semantics and error messages.
            foreach (var id in ids.Where(x => x > 0).Select(x => x.Value).Distinct().OrderBy(x => x))
                if (Read(db, id, garage) == null) return false;
            return true;
        }
    }
}
