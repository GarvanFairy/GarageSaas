using System;
using System.Linq;
using System.Threading.Tasks;
using GarageSaas.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignupAPI.Models;

namespace GarageSaas.Services
{
    public sealed class RegistrationRequestUnavailableException : Exception
    {
        public RegistrationRequestUnavailableException()
            : base("The registration could not be updated. Refresh its details before trying again.") { }
    }

    public sealed class RegistrationRequestLock : IRegistrationRequestLock
    {
        private readonly SignupContext _context;
        private readonly ILogger<RegistrationRequestLock> _logger;

        public RegistrationRequestLock(SignupContext context, ILogger<RegistrationRequestLock> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(int requestId, Func<GarageRegistrationRequest, Task<T>> action)
        {
            // Fail closed on providers that cannot enforce the SQL Server locking contract.
            if (!_context.Database.IsSqlServer())
                throw new InvalidOperationException("Registration mutations require SQL Server.");

            try
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    // Parameterized Id; HOLDLOCK retains the lock through commit/rollback.
                    // Use a fresh snapshot even if this context previously read the request.
                    var request = await _context.GarageRegistrationRequest
                        .FromSqlInterpolated($"SELECT * FROM [GarageRegistrationRequest] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {requestId}")
                        .AsNoTracking().SingleOrDefaultAsync();

                    var tracked = _context.ChangeTracker.Entries<GarageRegistrationRequest>()
                        .FirstOrDefault(entry => entry.Entity.Id == requestId);
                    if (tracked != null) tracked.State = EntityState.Detached;
                    if (request != null) _context.Attach(request);

                    var result = await action(request);
                    await transaction.CommitAsync();
                    return result;
                }
            }
            catch (Exception exception) when (exception is SqlException ||
                exception is DbUpdateException || exception is DbUpdateConcurrencyException)
            {
                // Do not log exception messages/SQL parameters: they may contain token hashes.
                // Never retry automatically: a lost commit acknowledgement has an unknown outcome.
                _logger.LogWarning("Registration mutation failed for request {RequestId} ({FailureType}).",
                    requestId, exception.GetType().Name);
                foreach (var entry in _context.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;
                throw new RegistrationRequestUnavailableException();
            }
        }
    }
}
