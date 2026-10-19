using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Identity;
using MMV.Domain.Identity;
using MMV.Infrastructure;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Identité sur PostgreSQL réel, base <c>mmv_v2_test</c> UNIQUEMENT, chaque test dans une transaction
/// annulée (ROLLBACK) : aucune ligne ne subsiste. Exige la migration AuthIdentity appliquée (GO O-02-01).
/// Exécution : <c>dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=IdentityWrite"</c>.
/// </summary>
[Trait("Stage", "IdentityWrite")]
public sealed class IdentityPostgreSqlTests(ITestOutputHelper output)
{
    private const string Password = "Mot-de-passe-pg-test-1";

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task LoginSessionLogoutAndDeactivation_RoundTripOnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var hashing = fixture.Get<IPasswordHashing>();
        var user = new UserAccount(Guid.CreateVersion7(), $"pg-{Guid.NewGuid():N}"[..20], "Compte de test PG", DateTimeOffset.UtcNow);
        user.SetPasswordHash(hashing.Hash(user, Password));
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var auth = fixture.Get<AuthService>();
        Assert.Null(await auth.LoginAsync(user.UserName, "mauvais", CancellationToken.None));
        var login = await auth.LoginAsync(user.UserName.ToUpperInvariant(), Password, CancellationToken.None);
        Assert.NotNull(login);

        // Seul le SHA-256 du jeton (32 octets, contrainte ck_sessions_token_hash_length) est stocké.
        var stored = await fixture.Context.Sessions.AsNoTracking().SingleAsync(s => s.UserId == user.Id);
        Assert.Equal(SessionTokens.TryHash(login.AccessToken), stored.TokenHash);
        // L'échec précédent est remis à zéro par la connexion réussie.
        Assert.Equal(0, (await fixture.Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).FailedLoginCount);

        Assert.NotNull(await auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));
        await auth.LogoutAsync(login.Session.SessionId, CancellationToken.None);
        Assert.Null(await auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));

        var second = (await auth.LoginAsync(user.UserName, Password, CancellationToken.None))!;
        user.Deactivate();
        await fixture.Context.SaveChangesAsync();
        Assert.Null(await auth.AuthenticateAsync(second.AccessToken, CancellationToken.None));
        Assert.Equal(
            SessionRevocationReasons.AccountDisabled,
            (await fixture.Context.Sessions.AsNoTracking().SingleAsync(s => s.Id == second.Session.SessionId)).RevocationReason);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Bootstrap_SucceedsOnce_ThenIsRefused()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var bootstrap = fixture.Get<BootstrapService>();
        if (await bootstrap.IsCompletedAsync(CancellationToken.None))
        {
            // Une trace persistante sur la base de TEST serait une anomalie (le bootstrap réel vise mmv_v2_dev).
            Assert.Fail("auth.bootstrap_record non vide sur mmv_v2_test.");
        }

        var first = await bootstrap.CreateInitialAdministratorAsync($"admin-{Guid.NewGuid():N}"[..20], "Admin PG", Password, CancellationToken.None);
        var second = await bootstrap.CreateInitialAdministratorAsync($"autre-{Guid.NewGuid():N}"[..20], "Autre", Password, CancellationToken.None);

        Assert.Equal(BootstrapStatus.Created, first.Status);
        Assert.Equal(BootstrapStatus.AlreadyCompleted, second.Status);
        var admin = await fixture.Context.Users.Include(u => u.Permissions).SingleAsync(u => u.Id == first.AdministratorId);
        Assert.True(admin.HasPermission(Permissions.SystemAdmin));
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task BootstrapRecord_IsASingletonEnforcedByPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);

        await fixture.ExecuteAsync("INSERT INTO auth.bootstrap_record (id, administrator_id, completed_at_utc) VALUES (1, gen_random_uuid(), now())");
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.UniqueViolation,
            "INSERT INTO auth.bootstrap_record (id, administrator_id, completed_at_utc) VALUES (1, gen_random_uuid(), now())");
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "INSERT INTO auth.bootstrap_record (id, administrator_id, completed_at_utc) VALUES (2, gen_random_uuid(), now())");
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task UnknownPermissionAndDuplicateUserName_AreRejectedByPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var id = Guid.NewGuid();
        await fixture.ExecuteAsync($"""
            INSERT INTO auth.users (id, user_name, normalized_user_name, display_name, password_hash, is_active, failed_login_count, created_at_utc)
            VALUES ('{id}', 'pg-dup', 'PG-DUP', 'x', 'h', true, 0, now())
            """);

        // Phase 02 : 'clients.read' servait d'exemple de permission inconnue ; elle est admise depuis la migration
        // 0003_ClientsModule (G03-METIER B). Exemple remplacé par une permission jamais définie.
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation,
            $"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{id}', 'clients.delete')");
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.UniqueViolation, $"""
            INSERT INTO auth.users (id, user_name, normalized_user_name, display_name, password_hash, is_active, failed_login_count, created_at_utc)
            VALUES ('{Guid.NewGuid()}', 'pg-DUP', 'PG-DUP', 'y', 'h', true, 0, now())
            """);
    }
}
