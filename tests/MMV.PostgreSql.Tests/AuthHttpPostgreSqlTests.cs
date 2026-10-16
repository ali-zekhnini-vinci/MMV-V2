using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Identity;
using MMV.Domain.Identity;
using MMV.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Matrice 401/403/révocation de MMV.Api sur PostgreSQL RÉEL (<c>mmv_v2_test</c> uniquement) : pipeline
/// HTTP complet, store EF Core et hachage Identity réels. Deux comptes éphémères (identifiants aléatoires,
/// mots de passe générés en mémoire, jamais affichés) sont créés puis SUPPRIMÉS en fin de test (sessions
/// supprimées en cascade). Exige la migration AuthIdentity (GO O-02-01).
/// </summary>
[Trait("Stage", "IdentityWrite")]
public sealed class AuthHttpPostgreSqlTests(ITestOutputHelper output)
{
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task AuthMatrix_OnRealPostgreSql()
    {
        var connectionString = PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Dernière source : remplace le user-secret de l'API (mmv_v2_dev) par la base de TEST.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = connectionString }));
        });

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var adminPassword = SessionTokens.NewToken();
        var userPassword = SessionTokens.NewToken();
        Guid[] created;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
            Assert.Equal("mmv_v2_test", context.Database.GetDbConnection().Database);
            var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashing>();
            var admin = NewUser(hashing, $"e2e-admin-{suffix}", adminPassword, Permissions.SystemAdmin);
            var user = NewUser(hashing, $"e2e-user-{suffix}", userPassword);
            context.Users.AddRange(admin, user);
            await context.SaveChangesAsync();
            created = [admin.Id, user.Id];
        }

        output.WriteLine($"comptes éphémères créés sur mmv_v2_test (suffixe {suffix})");
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

            // Anonyme -> 401
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/api/auth/session", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/api/admin/accounts", null)).StatusCode);

            // Mauvais mot de passe -> 401 neutre
            using (var bad = await client.PostAsJsonAsync("/api/auth/login", new { userName = $"e2e-admin-{suffix}", password = "mauvais" }))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
            }

            // Admin -> 200 sur /api/admin/accounts
            var adminToken = await Login(client, $"e2e-admin-{suffix}", adminPassword);
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/api/admin/accounts", adminToken)).StatusCode);

            // Non-admin -> 200 sur la session, 403 sur l'administration
            var userToken = await Login(client, $"e2e-user-{suffix}", userPassword);
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/api/auth/session", userToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Get, "/api/admin/accounts", userToken)).StatusCode);

            // Déconnexion -> jeton révoqué
            Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Post, "/api/auth/logout", adminToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/api/auth/session", adminToken)).StatusCode);

            // Désactivation (écrite en base) -> session existante refusée
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
                var user = await context.Users.SingleAsync(u => u.Id == created[1]);
                user.Deactivate();
                await context.SaveChangesAsync();
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/api/auth/session", userToken)).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
                var reasons = await context.Sessions.AsNoTracking()
                    .Where(s => created.Contains(s.UserId))
                    .Select(s => s.RevocationReason)
                    .ToListAsync();
                Assert.Equal(["account-disabled", "logout"], reasons.Order(StringComparer.Ordinal));
            }
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
            var deleted = await context.Users.Where(u => created.Contains(u.Id)).ExecuteDeleteAsync();
            var remainingSessions = await context.Sessions.CountAsync(s => created.Contains(s.UserId));
            output.WriteLine($"nettoyage : {deleted} compte(s) supprimé(s), sessions restantes = {remainingSessions}");
            Assert.Equal(2, deleted);
            Assert.Equal(0, remainingSessions);
        }
    }

    private static UserAccount NewUser(IPasswordHashing hashing, string userName, string password, params string[] permissions)
    {
        var user = new UserAccount(Guid.CreateVersion7(), userName, $"E2E {userName}", DateTimeOffset.UtcNow);
        user.SetPasswordHash(hashing.Hash(user, password));
        foreach (var permission in permissions)
        {
            user.Grant(permission);
        }

        return user;
    }

    private static async Task<string> Login(HttpClient client, string userName, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }
}
