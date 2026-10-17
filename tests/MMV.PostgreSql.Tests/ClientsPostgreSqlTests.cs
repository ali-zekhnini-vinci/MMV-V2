using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Clients;
using MMV.Application.Identity;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Infrastructure.Clients;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Expression de la référence client (ClientModel.ReferenceDefaultSql) évaluée par PostgreSQL 18 RÉEL, session
/// en lecture seule : aucun objet requis, aucune écriture — exécutable AVANT la migration 0003.
/// </summary>
public sealed class ClientReferenceFormatPostgreSqlTests(ITestOutputHelper output)
{
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ReferenceExpression_PadsToSixDigitsAndNeverTruncates()
    {
        const string sequenceCall = "nextval('clients.client_reference_seq')";
        Assert.Contains(sequenceCall, ClientModel.ReferenceDefaultSql, StringComparison.Ordinal);
        var expression = ClientModel.ReferenceDefaultSql.Replace(sequenceCall, "v", StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT {expression} FROM unnest(ARRAY[1, 42, 999999, 1000000, 123456789, 999999999999999999]::bigint[]) WITH ORDINALITY AS t(v, n) ORDER BY n",
            connection);
        var references = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                references.Add(reader.GetString(0));
            }
        }

        output.WriteLine(string.Join(" | ", references));
        Assert.Equal(
            ["CLI-000001", "CLI-000042", "CLI-999999", "CLI-1000000", "CLI-123456789", "CLI-999999999999999999"],
            references);
        Assert.All(references, r => Assert.Matches("^CLI-[0-9]{6,18}$", r));
        Assert.All(references, r => Assert.True(r.Length <= Client.ReferenceMaxLength));
    }
}

/// <summary>
/// Module Clients sur PostgreSQL RÉEL, base <c>mmv_v2_test</c> UNIQUEMENT. Exige la migration 0003_ClientsModule
/// appliquée (GO SQL du propriétaire) : à lancer APRÈS l'application, jamais avant.
/// Exécution : <c>dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=ClientsWrite"</c>.
/// Données exclusivement fictives. Tests transactionnels : ROLLBACK, aucune ligne ne subsiste (la séquence de
/// référence avance néanmoins : PostgreSQL ne l'annule jamais, ce qui garantit qu'une référence n'est pas réutilisée).
/// Test HTTP : comptes et clients éphémères SUPPRIMÉS en fin de test (nettoyage de test, aucune route de suppression).
/// </summary>
[Trait("Stage", "ClientsWrite")]
public sealed class ClientsPostgreSqlTests(ITestOutputHelper output)
{
    private static readonly ClientInput Eloise = new("Éloïse", "Lefèvre", "1985-04-12", "0470 12 34 56", "eloise@exemple.be", null, "Namur", "5000", null);

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task CreateUpdateArchiveReactivate_RoundTripOnPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture);
        var service = fixture.Get<ClientService>();

        var created = await service.CreateAsync(Eloise, author.Id, CancellationToken.None);
        Assert.Equal(ClientWriteStatus.Success, created.Status);
        var client = created.Client!;
        Assert.Matches("^CLI-[0-9]{6,18}$", client.Reference);
        Assert.NotEqual(0u, client.Version);
        output.WriteLine($"référence attribuée par la séquence : {client.Reference}, xmin={client.Version}");

        fixture.Context.ChangeTracker.Clear();
        var stored = await fixture.Context.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Equal(client.Reference, stored.Reference);
        Assert.Equal(client.Version, stored.Version);
        Assert.Equal(new DateOnly(1985, 4, 12), stored.BirthDate);
        Assert.Equal(client.CreatedAtUtc, stored.CreatedAtUtc);
        Assert.Equal("eloise lefevre eloise@exemple.be 0470123456", stored.SearchText);

        fixture.Context.ChangeTracker.Clear();
        var updated = await service.UpdateAsync(client.Id, Eloise with { City = "Liège" }, client.Version, author.Id, CancellationToken.None);
        Assert.Equal(ClientWriteStatus.Success, updated.Status);
        Assert.NotEqual(client.Version, updated.Client!.Version);

        fixture.Context.ChangeTracker.Clear();
        var stale = await service.UpdateAsync(client.Id, Eloise with { City = "Mons" }, client.Version, author.Id, CancellationToken.None);
        Assert.Equal(ClientWriteStatus.VersionConflict, stale.Status);

        fixture.Context.ChangeTracker.Clear();
        var archived = await service.ArchiveAsync(client.Id, updated.Client.Version, author.Id, CancellationToken.None);
        Assert.Equal(ClientWriteStatus.Success, archived.Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(ClientWriteStatus.ClientArchived, (await service.UpdateAsync(client.Id, Eloise, archived.Client!.Version, author.Id, CancellationToken.None)).Status);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(ClientWriteStatus.Success, (await service.ReactivateAsync(client.Id, archived.Client.Version, author.Id, CancellationToken.None)).Status);

        fixture.Context.ChangeTracker.Clear();
        var final = await fixture.Context.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Equal("Liège", final.City);
        Assert.Null(final.ArchivedAtUtc);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ConcurrentWriter_IsDetectedByXmin_WithoutOverwrite()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture);
        var client = (await fixture.Get<ClientService>().CreateAsync(Eloise, author.Id, CancellationToken.None)).Client!;

        // Deux postes lisent la même version.
        fixture.Context.ChangeTracker.Clear();
        await using var otherPost = await fixture.SecondContextAsync();
        var firstStore = fixture.Get<IClientStore>();
        var mine = (await firstStore.FindForUpdateAsync(client.Id, CancellationToken.None))!;
        var theirs = await otherPost.Clients.SingleAsync(c => c.Id == client.Id);
        Assert.Equal(mine.Version, theirs.Version);

        theirs.Update(new ClientDetails("Éloïse", "Lefèvre", null, null, null, null, "Arlon", null, null), author.Id, DateTimeOffset.UtcNow);
        await otherPost.SaveChangesAsync();

        mine.Update(new ClientDetails("Éloïse", "Lefèvre", null, null, null, null, "Mons", null, null), author.Id, DateTimeOffset.UtcNow);
        Assert.False(await firstStore.SaveChangesAsync(CancellationToken.None));

        var stored = await otherPost.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Equal("Arlon", stored.City);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Search_IsAccentAndCaseInsensitive_AndArchivedAreFiltered()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture);
        var service = fixture.Get<ClientService>();
        var marker = $"Zq{Guid.NewGuid():N}"[..12];
        var eloise = (await service.CreateAsync(Eloise with { LastName = $"Lefèvre{marker}" }, author.Id, CancellationToken.None)).Client!;
        var paul = (await service.CreateAsync(new ClientInput("Paul", $"Lefevre{marker}", null, null, null, null, null, null, null), author.Id, CancellationToken.None)).Client!;
        await service.ArchiveAsync(paul.Id, paul.Version, author.Id, CancellationToken.None);

        async Task<string[]> SearchAsync(string search, ClientStatusFilter status) =>
            [.. (await service.ListAsync(new ClientListQuery(search, status, ClientSortField.Name, false, 1, 20), CancellationToken.None))
                .Items.Select(c => c.FirstName).Order(StringComparer.Ordinal)];

        Assert.Equal(["Paul", "Éloïse"], await SearchAsync($"LEFEVRE{marker}".ToUpperInvariant(), ClientStatusFilter.All));
        Assert.Equal(["Éloïse"], await SearchAsync($"eloise lefèvre{marker}", ClientStatusFilter.Active));
        Assert.Equal(["Paul"], await SearchAsync($"lefevre{marker}", ClientStatusFilter.Archived));
        Assert.Contains(eloise.Id, (await service.ListAsync(new ClientListQuery("0470 12", ClientStatusFilter.Active, ClientSortField.Name, false, 1, 100), CancellationToken.None)).Items.Select(c => c.Id));
        Assert.Empty(await SearchAsync($"{marker}%", ClientStatusFilter.All));
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Duplicates_AreAllowed_WithDistinctReferences()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture);
        var service = fixture.Get<ClientService>();
        var twin = Eloise with { LastName = $"Lefèvre{Guid.NewGuid():N}"[..20] };

        var first = await service.CreateAsync(twin, author.Id, CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();
        var second = await service.CreateAsync(twin, author.Id, CancellationToken.None);

        // D-03-03 : aucun refus ni alerte de doublon ; deux fiches distinctes, deux références distinctes.
        Assert.Equal(ClientWriteStatus.Success, first.Status);
        Assert.Equal(ClientWriteStatus.Success, second.Status);
        Assert.NotEqual(first.Client!.Id, second.Client!.Id);
        Assert.NotEqual(first.Client.Reference, second.Client.Reference);
        var page = await service.ListAsync(new ClientListQuery(twin.LastName, ClientStatusFilter.All, ClientSortField.Name, false, 1, 20), CancellationToken.None);
        Assert.Equal(2, page.TotalCount);
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task Constraints_AreEnforcedByPostgreSql()
    {
        await using var fixture = await RolledBackScope.OpenAsync(output);
        var author = await AddUserAsync(fixture);
        var columns = "id, first_name, last_name, search_text, created_at_utc, created_by_user_id, updated_at_utc, updated_by_user_id";
        string Values(string firstName, string lastName, string extraColumns = "", string extraValues = "") =>
            $"INSERT INTO clients.clients ({columns}{extraColumns}) VALUES (gen_random_uuid(), '{firstName}', '{lastName}', 'x', now(), '{author.Id}', now(), '{author.Id}'{extraValues})";

        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation, Values(" ", "Martin"));
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation, Values("Jeanne", ""));
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation, Values("Jeanne", "Martin", ", birth_date", ", DATE '1899-12-31'"));
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation, Values("Jeanne", "Martin", ", reference", ", 'CLI-12'"));
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation, Values("Jeanne", "Martin", ", archived_at_utc", ", now() - interval '1 day'"));
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            Values("Jeanne", "Martin").Replace($"'{author.Id}', now(), '{author.Id}'", $"'{Guid.NewGuid()}', now(), '{author.Id}'", StringComparison.Ordinal));

        await fixture.ExecuteAsync(Values("Jeanne", "Martin", ", reference", ", 'CLI-000000'"));
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.UniqueViolation, Values("Jeanne", "Martin", ", reference", ", 'CLI-000000'"));
        // Restriction : un compte auteur ne peut être supprimé tant que ses clients existent. ON DELETE RESTRICT est
        // signalé par 23001 (restrict_violation), pas 23503 (constat réel PostgreSQL 18.6, 03-A3).
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.RestrictViolation, $"DELETE FROM auth.users WHERE id = '{author.Id}'");

        // Permissions Clients admises par la contrainte étendue ; permission inconnue toujours refusée.
        await fixture.ExecuteAsync($"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author.Id}', 'clients.read'), ('{author.Id}', 'clients.write'), ('{author.Id}', 'clients.archive')");
        await fixture.ExpectSqlStateAsync(PostgresErrorCodes.CheckViolation, $"INSERT INTO auth.user_permissions (user_id, permission) VALUES ('{author.Id}', 'clients.delete')");
    }

    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ClientsHttpMatrix_OnRealPostgreSql()
    {
        var connectionString = PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = connectionString }));
        });

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var password = SessionTokens.NewToken();
        Guid[] users;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
            Assert.Equal("mmv_v2_test", context.Database.GetDbConnection().Database);
            var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashing>();
            var admin = NewUser(hashing, $"e2e-cadmin-{suffix}", password, Permissions.SystemAdmin);
            var full = NewUser(hashing, $"e2e-cfull-{suffix}", password, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive);
            context.Users.AddRange(admin, full);
            await context.SaveChangesAsync();
            users = [admin.Id, full.Id];
        }

        output.WriteLine($"comptes éphémères créés sur mmv_v2_test (suffixe {suffix})");
        try
        {
            using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            var adminToken = await LoginAsync(http, $"e2e-cadmin-{suffix}", password);
            var token = await LoginAsync(http, $"e2e-cfull-{suffix}", password);

            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(http, HttpMethod.Get, "/api/clients", adminToken)).StatusCode);

            using var created = await SendAsync(http, HttpMethod.Post, "/api/clients", token, new
            {
                firstName = "Éloïse", lastName = $"Lefèvre-{suffix}", birthDate = "1985-04-12", phone = (string?)null, email = (string?)null,
                addressLine = (string?)null, city = "Namur", postalCode = (string?)null, notes = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var detail = await ReadAsync(created);
            var id = detail.GetProperty("id").GetString();
            var version = detail.GetProperty("version").GetUInt32();
            Assert.Matches("^CLI-[0-9]{6,18}$", detail.GetProperty("reference").GetString());

            var page = await ReadAsync(await SendAsync(http, HttpMethod.Get, $"/api/clients?q=LEFEVRE-{suffix}", token));
            Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

            var body = new { firstName = "Éloïse", lastName = $"Lefèvre-{suffix}", birthDate = (string?)null, phone = (string?)null, email = (string?)null, addressLine = (string?)null, city = "Liège", postalCode = (string?)null, notes = (string?)null, version };
            using var updated = await SendAsync(http, HttpMethod.Put, $"/api/clients/{id}", token, body);
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            using var stale = await SendAsync(http, HttpMethod.Put, $"/api/clients/{id}", token, body);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("""{"code":"version_conflict"}""", await stale.Content.ReadAsStringAsync());

            var newVersion = (await ReadAsync(updated)).GetProperty("version").GetUInt32();
            using var archived = await SendAsync(http, HttpMethod.Post, $"/api/clients/{id}/archive", token, new { version = newVersion });
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
            Assert.Equal("archived", (await ReadAsync(archived)).GetProperty("status").GetString());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
            var clients = await context.Clients.Where(c => users.Contains(c.CreatedByUserId) || users.Contains(c.UpdatedByUserId)).ExecuteDeleteAsync();
            var deleted = await context.Users.Where(u => users.Contains(u.Id)).ExecuteDeleteAsync();
            output.WriteLine($"nettoyage : {clients} client(s) fictif(s) et {deleted} compte(s) supprimés");
            Assert.Equal(2, deleted);
        }
    }

    /// <summary>
    /// Matrice des permissions, cycle de vie, doublons, recherche sans accents et corps stricts sur le pipeline HTTP
    /// réel et PostgreSQL réel (03-A3). Quatre comptes éphémères : <c>system.admin</c> seul, lecture seule,
    /// lecture + écriture, lecture + écriture + archivage. Nettoyage et contrôle de résidus en fin de test.
    /// </summary>
    [PostgreSqlFact(nameof(PostgreSqlTarget.Test))]
    public async Task ClientsHttpPermissionsAndLifecycle_OnRealPostgreSql()
    {
        var connectionString = PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = connectionString }));
        });

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var password = SessionTokens.NewToken();
        Guid[] users;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
            Assert.Equal("mmv_v2_test", context.Database.GetDbConnection().Database);
            var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashing>();
            UserAccount[] accounts =
            [
                NewUser(hashing, $"e2e-padmin-{suffix}", password, Permissions.SystemAdmin),
                NewUser(hashing, $"e2e-pread-{suffix}", password, Permissions.ClientsRead),
                NewUser(hashing, $"e2e-pwrite-{suffix}", password, Permissions.ClientsRead, Permissions.ClientsWrite),
                NewUser(hashing, $"e2e-pfull-{suffix}", password, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive),
            ];
            context.Users.AddRange(accounts);
            await context.SaveChangesAsync();
            users = [.. accounts.Select(a => a.Id)];
        }

        output.WriteLine($"4 comptes éphémères créés sur mmv_v2_test (suffixe {suffix})");
        try
        {
            using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            var admin = await LoginAsync(http, $"e2e-padmin-{suffix}", password);
            var reader = await LoginAsync(http, $"e2e-pread-{suffix}", password);
            var writer = await LoginAsync(http, $"e2e-pwrite-{suffix}", password);
            var full = await LoginAsync(http, $"e2e-pfull-{suffix}", password);
            var lastName = $"Lefèvre-{suffix}";
            object Input(string city) => new
            {
                firstName = "Éloïse", lastName, birthDate = "1985-04-12", phone = "0470 12 34 56", email = (string?)null,
                addressLine = (string?)null, city, postalCode = (string?)null, notes = (string?)null,
            };

            async Task ExpectAsync(HttpStatusCode status, string? code, HttpMethod method, string path, string token, object? body = null)
            {
                using var response = await SendAsync(http, method, path, token, body);
                Assert.Equal(status, response.StatusCode);
                if (code is not null)
                {
                    Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
                }
            }

            // system.admin n'accorde aucune permission Clients ; aucune écriture sans clients.write.
            await ExpectAsync(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Get, "/api/clients", admin);
            await ExpectAsync(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, "/api/clients", admin, Input("Namur"));
            await ExpectAsync(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, "/api/clients", reader, Input("Namur"));

            // Champ inconnu (donnée hors périmètre) refusé avant toute écriture.
            await ExpectAsync(HttpStatusCode.BadRequest, "invalid_body", HttpMethod.Post, "/api/clients", writer, new
            {
                firstName = "Éloïse", lastName, socialSecurityNumber = "85041212345",
            });
            await ExpectAsync(HttpStatusCode.UnprocessableEntity, "validation_failed", HttpMethod.Post, "/api/clients", writer, new { firstName = "Éloïse" });

            // Doublons autorisés (D-03-03) : deux créations identiques, deux références.
            using var first = await SendAsync(http, HttpMethod.Post, "/api/clients", writer, Input("Namur"));
            using var twin = await SendAsync(http, HttpMethod.Post, "/api/clients", writer, Input("Namur"));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, twin.StatusCode);
            var detail = await ReadAsync(first);
            var id = detail.GetProperty("id").GetString();
            Assert.NotEqual(detail.GetProperty("reference").GetString(), (await ReadAsync(twin)).GetProperty("reference").GetString());

            // Recherche sans accents ni casse, en lecture seule.
            var page = await ReadAsync(await SendAsync(http, HttpMethod.Get, $"/api/clients?q=ELOISE%20lefevre-{suffix}", reader));
            Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
            await ExpectAsync(HttpStatusCode.OK, null, HttpMethod.Get, $"/api/clients/{id}", reader);
            var version = detail.GetProperty("version").GetUInt32();

            await ExpectAsync(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Put, $"/api/clients/{id}", reader, new { version, firstName = "Éloïse", lastName });
            using var updated = await SendAsync(http, HttpMethod.Put, $"/api/clients/{id}", writer, new
            {
                firstName = "Éloïse", lastName, birthDate = "1985-04-12", phone = (string?)null, email = (string?)null,
                addressLine = (string?)null, city = "Liège", postalCode = (string?)null, notes = (string?)null, version,
            });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var current = (await ReadAsync(updated)).GetProperty("version").GetUInt32();
            Assert.NotEqual(version, current);

            // Archivage : permission dédiée, version exigée, conflit sur version périmée.
            await ExpectAsync(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, $"/api/clients/{id}/archive", writer, new { version = current });
            await ExpectAsync(HttpStatusCode.Conflict, "version_conflict", HttpMethod.Post, $"/api/clients/{id}/archive", full, new { version });
            using var archived = await SendAsync(http, HttpMethod.Post, $"/api/clients/{id}/archive", full, new { version = current });
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
            var archivedDetail = await ReadAsync(archived);
            Assert.Equal("archived", archivedDetail.GetProperty("status").GetString());
            current = archivedDetail.GetProperty("version").GetUInt32();
            await ExpectAsync(HttpStatusCode.Conflict, "already_archived", HttpMethod.Post, $"/api/clients/{id}/archive", full, new { version = current });
            await ExpectAsync(HttpStatusCode.Conflict, "client_archived", HttpMethod.Put, $"/api/clients/{id}", writer, new { version = current, firstName = "Éloïse", lastName });
            var activeOnly = await ReadAsync(await SendAsync(http, HttpMethod.Get, $"/api/clients?q=lefevre-{suffix}&status=active", reader));
            Assert.Equal(1, activeOnly.GetProperty("totalCount").GetInt32());

            // Réactivation.
            await ExpectAsync(HttpStatusCode.Forbidden, "forbidden", HttpMethod.Post, $"/api/clients/{id}/reactivate", reader, new { version = current });
            await ExpectAsync(HttpStatusCode.Conflict, "version_conflict", HttpMethod.Post, $"/api/clients/{id}/reactivate", full, new { version });
            using var reactivated = await SendAsync(http, HttpMethod.Post, $"/api/clients/{id}/reactivate", full, new { version = current });
            Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
            var reactivatedDetail = await ReadAsync(reactivated);
            Assert.Equal("active", reactivatedDetail.GetProperty("status").GetString());
            await ExpectAsync(HttpStatusCode.Conflict, "already_active", HttpMethod.Post, $"/api/clients/{id}/reactivate", full,
                new { version = reactivatedDetail.GetProperty("version").GetUInt32() });

            // Aucune suppression physique exposée.
            await ExpectAsync(HttpStatusCode.MethodNotAllowed, null, HttpMethod.Delete, $"/api/clients/{id}", full);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
            var clients = await context.Clients.Where(c => users.Contains(c.CreatedByUserId) || users.Contains(c.UpdatedByUserId)).ExecuteDeleteAsync();
            var deleted = await context.Users.Where(u => users.Contains(u.Id)).ExecuteDeleteAsync();
            output.WriteLine($"nettoyage : {clients} client(s) fictif(s) et {deleted} compte(s) supprimés");
            Assert.Equal(4, deleted);

            var residue = await context.Database.SqlQuery<int>(
                $"""
                SELECT ((SELECT count(*) FROM clients.clients WHERE last_name LIKE {"%" + suffix})
                      + (SELECT count(*) FROM auth.users WHERE user_name LIKE {"%" + suffix})
                      + (SELECT count(*) FROM auth.sessions WHERE user_id = ANY({users})))::int AS "Value"
                """).SingleAsync();
            output.WriteLine($"résidus : {residue}");
            Assert.Equal(0, residue);
        }
    }

    private static async Task<UserAccount> AddUserAsync(RolledBackScope fixture)
    {
        var user = new UserAccount(Guid.CreateVersion7(), $"pg-c-{Guid.NewGuid():N}"[..20], "Auteur de test PG", DateTimeOffset.UtcNow);
        user.SetPasswordHash("hachage-factice");
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();
        return user;
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

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
