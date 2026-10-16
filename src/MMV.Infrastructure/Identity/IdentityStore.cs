using Microsoft.EntityFrameworkCore;
using MMV.Application.Identity;
using MMV.Domain.Identity;
using MMV.Infrastructure.Persistence;
using Npgsql;

namespace MMV.Infrastructure.Identity;

internal sealed class IdentityStore(MmvDbContext context) : IIdentityStore
{
    public Task<UserAccount?> FindUserByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.Permissions)
            .SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);

    public async Task<(UserSession Session, UserAccount User)?> FindSessionByTokenHashAsync(
        byte[] tokenHash, CancellationToken cancellationToken)
    {
        var session = await context.Sessions.SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var user = await context.Users
            .Include(u => u.Permissions)
            .SingleAsync(u => u.Id == session.UserId, cancellationToken);
        return (session, user);
    }

    public Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public void AddSession(UserSession session) => context.Sessions.Add(session);

    public async Task<IReadOnlyList<UserAccount>> ListUsersAsync(CancellationToken cancellationToken) =>
        await context.Users
            .AsNoTracking()
            .Include(u => u.Permissions)
            .OrderBy(u => u.NormalizedUserName)
            .ToListAsync(cancellationToken);

    public Task<bool> IsBootstrapCompletedAsync(CancellationToken cancellationToken) =>
        context.BootstrapRecords.AnyAsync(cancellationToken);

    public async Task<bool> TryCreateInitialAdministratorAsync(
        UserAccount administrator, BootstrapRecord record, CancellationToken cancellationToken)
    {
        // Transaction propre si l'appelant n'en a pas ouvert (tests : transaction englobante + ROLLBACK).
        var ownTransaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            context.BootstrapRecords.Add(record);
            context.Users.Add(administrator);
            await context.SaveChangesAsync(cancellationToken);
            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_bootstrap_record",
        })
        {
            // Bootstrap concurrent déjà validé : rien n'est créé.
            context.ChangeTracker.Clear();
            return false;
        }
        finally
        {
            if (ownTransaction is not null)
            {
                await ownTransaction.DisposeAsync();
            }
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
