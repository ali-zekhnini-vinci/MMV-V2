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
using MMV.Domain.Stores;
using MMV.Infrastructure.Persistence;
using Npgsql;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// Cible des tests de la Phase 05 : (1) cluster JETABLE de travail, variable <c>MMV_PG05_SCRATCH</c> — refusée si l'hôte
/// n'est pas local, si le port est 5432 ou 5433 (clusters du propriétaire) ou si la base / le rôle ne sont pas
/// <c>mmv_v2_scratch</c> ; (2) sinon <c>mmv_v2_test</c> (cible officielle) SEULEMENT si la migration 0007 y est appliquée
/// (après G05-SQL). Aucune autre cible ; aucune écriture tant qu'aucune n'est résolue (tests « Skipped » = NOT_TESTED).
/// </summary>
internal static class Phase05Target
{
    public const string ScratchVariable = "MMV_PG05_SCRATCH";
    public const string ScratchDatabase = "mmv_v2_scratch";

    private static readonly Lazy<(string? ConnectionString, string Name, string? Skip)> Resolved = new(Resolve);

    public static string? ConnectionString => Resolved.Value.ConnectionString;

    public static string Name => Resolved.Value.Name;

    public static string? SkipReason => Resolved.Value.Skip;

    private static (string?, string, string?) Resolve()
    {
        var scratch = Environment.GetEnvironmentVariable(ScratchVariable);
        if (!string.IsNullOrWhiteSpace(scratch))
        {
            var builder = new NpgsqlConnectionStringBuilder(scratch);
            if (builder.Host is not ("127.0.0.1" or "localhost" or "::1") || builder.Port is 5432 or 5433
                || builder.Database != ScratchDatabase || builder.Username != ScratchDatabase)
            {
                throw new InvalidOperationException($"{ScratchVariable} refusée : cluster jetable local, port hors 5432/5433, base et rôle {ScratchDatabase} attendus.");
            }

            return (builder.ConnectionString, $"{ScratchDatabase} (cluster jetable, port {builder.Port})", null);
        }

        if (!PostgreSqlTarget.Test.IsConfigured)
        {
            return (null, "aucune", "NOT_TESTED : ni cluster jetable (MMV_PG05_SCRATCH) ni mmv_v2_test configurés.");
        }

        try
        {
            using var connection = new NpgsqlConnection(PostgreSqlTarget.Test.ReadOnlyConnectionString());
            connection.Open();
            using var command = new NpgsqlCommand("SELECT to_regclass('catalog.product_variants') IS NOT NULL", connection);
            return (bool)command.ExecuteScalar()!
                ? (PostgreSqlTarget.Test.ValidatedBuilder().ConnectionString, "mmv_v2_test", null)
                : (null, "aucune", "NOT_TESTED : migration 0007_CatalogStockModule non appliquée à mmv_v2_test (revue G05-SQL du propriétaire requise).");
        }
        catch (NpgsqlException)
        {
            return (null, "aucune", "NOT_TESTED : mmv_v2_test injoignable.");
        }
    }
}

public sealed class Phase05FactAttribute : FactAttribute
{
    public Phase05FactAttribute() => Skip = Phase05Target.SkipReason;
}

public sealed class Phase05TheoryAttribute : TheoryAttribute
{
    public Phase05TheoryAttribute() => Skip = Phase05Target.SkipReason;
}

/// <summary>Réponse HTTP lue : statut et corps JSON (ou null).</summary>
internal sealed record ApiResult(HttpStatusCode Status, JsonElement Body)
{
    public string Code => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("code", out var c) ? c.GetString() ?? string.Empty : string.Empty;

    public string Raw => Body.ValueKind == JsonValueKind.Undefined ? string.Empty : Body.GetRawText();

    public JsonElement this[string property] => Body.GetProperty(property);
}

/// <summary>
/// Banc de la Phase 05 : pipeline HTTP RÉEL de MMV.Api (HTTPS, sessions, policies) sur PostgreSQL 18 réel, deux magasins
/// FICTIFS (Belgique — EUR, Maroc — MAD, D-05-20), comptes éphémères à droits exacts, données purgées en fin de test
/// (résidu contrôlé = 0, déclencheurs « aucune suppression » désactivés dans la seule transaction de purge).
/// </summary>
internal sealed class Phase05Bench : IAsyncDisposable
{
    public const string Password = "Mot-de-passe-banc-05";

    private readonly ITestOutputHelper _output;
    private readonly string _connection = Phase05Target.ConnectionString!;
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);

    private Phase05Bench(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        Factory = factory;
        _output = output;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient Client { get; }

    public string Suffix { get; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public List<Guid> Users { get; } = [];

    public Dictionary<string, Guid> UserIds { get; } = new(StringComparer.Ordinal);

    public List<Guid> Stores { get; } = [];

    public Guid StoreBe { get; private set; }

    public Guid StoreMa { get; private set; }

    public Guid LocationBe { get; private set; }

    public Guid LocationBeBack { get; private set; }

    public Guid LocationMa { get; private set; }

    public int Residue { get; private set; } = -1;

    public static async Task<Phase05Bench> CreateAsync(ITestOutputHelper output)
    {
        var connection = Phase05Target.ConnectionString!;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Auth:LoginAttemptsPerMinute", "10000");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = connection }));
        });
        var bench = new Phase05Bench(factory, output);
        await bench.SeedStoresAsync();
        output.WriteLine($"banc Phase 05 sur {Phase05Target.Name}, suffixe {bench.Suffix}");
        return bench;
    }

    /// <summary>Compte éphémère avec EXACTEMENT ces droits.</summary>
    public async Task<string> UserAsync(string name, params string[] permissions)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
        var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashing>();
        var userName = $"p05-{name}-{Suffix}".ToLowerInvariant();
        var user = new UserAccount(Guid.CreateVersion7(), userName, $"Banc {name}", DateTimeOffset.UtcNow);
        user.SetPasswordHash(hashing.Hash(user, Password));
        foreach (var permission in permissions)
        {
            user.Grant(permission);
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        Users.Add(user.Id);
        UserIds[name] = user.Id;
        using var response = await Client.PostAsJsonAsync("/api/auth/login", new { userName, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        _tokens[name] = body.RootElement.GetProperty("accessToken").GetString()!;
        return name;
    }

    public Task<ApiResult> GetAsync(string user, string path) => SendAsync(user, HttpMethod.Get, path, null);

    public Task<ApiResult> PostAsync(string user, string path, object body) => SendAsync(user, HttpMethod.Post, path, body);

    public Task<ApiResult> PutAsync(string user, string path, object body) => SendAsync(user, HttpMethod.Put, path, body);

    public async Task<ApiResult> SendAsync(string user, HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens[user]);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        return new ApiResult(response.StatusCode, json);
    }

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedStoresAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();
        var now = DateTimeOffset.UtcNow;
        var be = Store.Create(Guid.CreateVersion7(), $"T05BE{Suffix}", "Banc fictif Belgique", "BE", Currencies.Euro, now);
        var ma = Store.Create(Guid.CreateVersion7(), $"T05MA{Suffix}", "Banc fictif Maroc", "MA", Currencies.Dirham, now);
        context.Stores.AddRange(be, ma);
        var beDefault = Location.Create(Guid.CreateVersion7(), be.Id, "MAGASIN", "Magasin", true, now);
        var beBack = Location.Create(Guid.CreateVersion7(), be.Id, "RESERVE", "Réserve", false, now);
        var maDefault = Location.Create(Guid.CreateVersion7(), ma.Id, "MAGASIN", "Magasin", true, now);
        context.Locations.AddRange(beDefault, beBack, maDefault);
        await context.SaveChangesAsync();
        (StoreBe, StoreMa, LocationBe, LocationBeBack, LocationMa) = (be.Id, ma.Id, beDefault.Id, beBack.Id, maDefault.Id);
        Stores.AddRange([be.Id, ma.Id]);
    }

    private static readonly string[] GuardedTables =
    [
        "audit.events", "imports.rows", "imports.batches", "stock.inventory_lines", "stock.inventory_sessions", "stock.receipt_lines", "stock.receipts",
        "stock.movements", "stock.balances", "stock.variant_store_settings", "pricing.price_history", "pricing.variant_store_prices", "pricing.tax_categories",
        "catalog.variant_suppliers", "catalog.product_variants", "catalog.product_models", "catalog.lens_references", "catalog.suppliers", "org.locations", "org.stores",
    ];

    /// <summary>
    /// Purge en UNE transaction : déclencheurs utilisateur des tables gardées désactivés (droit du propriétaire des tables,
    /// procédé de <c>CommittedAuditCleanup</c>), suppression des seules lignes du banc, réactivation, contrôle de résidu.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(Phase05Target.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        async Task Run(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("u", Users.ToArray());
            command.Parameters.AddWithValue("s", Stores.ToArray());
            await command.ExecuteNonQueryAsync();
        }

        foreach (var table in GuardedTables)
        {
            await Run($"ALTER TABLE {table} DISABLE TRIGGER USER");
        }

        await Run("DELETE FROM imports.rows WHERE batch_id IN (SELECT id FROM imports.batches WHERE uploaded_by_user_id = ANY(@u))");
        await Run("DELETE FROM imports.batches WHERE uploaded_by_user_id = ANY(@u)");
        await Run("DELETE FROM stock.inventory_lines WHERE session_id IN (SELECT id FROM stock.inventory_sessions WHERE store_id = ANY(@s))");
        await Run("DELETE FROM stock.inventory_sessions WHERE store_id = ANY(@s)");
        await Run("DELETE FROM stock.receipt_lines WHERE receipt_id IN (SELECT id FROM stock.receipts WHERE store_id = ANY(@s))");
        await Run("DELETE FROM stock.receipts WHERE store_id = ANY(@s)");
        await Run("DELETE FROM stock.movements WHERE location_id IN (SELECT id FROM org.locations WHERE store_id = ANY(@s))");
        await Run("DELETE FROM stock.balances WHERE location_id IN (SELECT id FROM org.locations WHERE store_id = ANY(@s))");
        await Run("DELETE FROM stock.variant_store_settings WHERE store_id = ANY(@s)");
        await Run("DELETE FROM pricing.price_history WHERE changed_by_user_id = ANY(@u)");
        await Run("DELETE FROM pricing.variant_store_prices WHERE store_id = ANY(@s)");
        await Run("DELETE FROM pricing.tax_categories WHERE store_id = ANY(@s)");
        await Run("DELETE FROM catalog.variant_suppliers WHERE created_by_user_id = ANY(@u)");
        await Run("DELETE FROM catalog.variant_lens_treatments WHERE variant_id IN (SELECT id FROM catalog.product_variants WHERE created_by_user_id = ANY(@u))");
        await Run("DELETE FROM catalog.product_variants WHERE created_by_user_id = ANY(@u)");
        await Run("DELETE FROM catalog.product_models WHERE created_by_user_id = ANY(@u)");
        await Run("DELETE FROM catalog.lens_references WHERE created_by_user_id = ANY(@u) AND kind <> 'manufacturer'");
        await Run("DELETE FROM catalog.lens_references WHERE created_by_user_id = ANY(@u)");
        await Run("DELETE FROM catalog.suppliers WHERE created_by_user_id = ANY(@u)");
        await Run("DELETE FROM org.locations WHERE store_id = ANY(@s)");
        await Run("DELETE FROM org.stores WHERE id = ANY(@s)");
        await Run("DELETE FROM audit.events WHERE actor_user_id = ANY(@u)");
        await Run("DELETE FROM auth.users WHERE id = ANY(@u)");
        foreach (var table in GuardedTables)
        {
            await Run($"ALTER TABLE {table} ENABLE TRIGGER USER");
        }

        await transaction.CommitAsync();
        await using var residue = new NpgsqlCommand(
            """
            SELECT (SELECT count(*) FROM org.stores WHERE id = ANY(@s)) + (SELECT count(*) FROM auth.users WHERE id = ANY(@u))
                 + (SELECT count(*) FROM catalog.product_variants WHERE created_by_user_id = ANY(@u)) + (SELECT count(*) FROM audit.events WHERE actor_user_id = ANY(@u))
                 + (SELECT count(*) FROM stock.movements m JOIN org.locations l ON l.id = m.location_id WHERE l.store_id = ANY(@s))
            """,
            connection);
        residue.Parameters.AddWithValue("u", Users.ToArray());
        residue.Parameters.AddWithValue("s", Stores.ToArray());
        Residue = (int)(long)(await residue.ExecuteScalarAsync())!;
        _output.WriteLine($"purge du banc {Suffix} : résidu = {Residue}");
        Client.Dispose();
        await Factory.DisposeAsync();
        Assert.Equal(0, Residue);
    }
}
