using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMV.Application.Catalog;
using MMV.Application.Catalog.Assisted;
using MMV.Domain.Stores;

namespace MMV.Api.Tests.Catalog.Assisted;

/// <summary>
/// Clôture de la Phase 05 (07/11/2026) : validation UX de l'assistant CSV REFUSÉE par le propriétaire, aucun GO de
/// déploiement. Preuve que le chemin d'exécution du nouvel assistant est fermé côté SERVEUR quand
/// <c>CatalogInterpreter:WizardEnabled</c> est faux (valeur versionnée) : chaque route du nouveau parcours répond
/// <c>404 assistant_disabled</c> avant tout accès au catalogue (seul le magasin de l'installation est lu par le filtre de
/// groupe) ; seul l'état du service reste servi. Sans base : le faux magasin refuse tout autre appel.
/// </summary>
public sealed class CsvAssistantDisabledGateTests
{
    /// <summary>Routes du nouveau parcours (méthode, gabarit) : toutes doivent être fermées.</summary>
    public static TheoryData<string, string> WizardRoutes => new()
    {
        { "GET", "/api/imports/summary" },
        { "POST", "/api/imports/assistant/inspect" },
        { "POST", "/api/imports/assistant" },
        { "GET", "/api/imports/{id:guid}/assistant" },
        { "GET", "/api/imports/{id:guid}/assistant/analysis" },
        { "POST", "/api/imports/{id:guid}/assistant/analysis" },
        { "DELETE", "/api/imports/{id:guid}/assistant/analysis" },
        { "POST", "/api/imports/{id:guid}/assistant/analysis/cancel" },
        { "GET", "/api/imports/{id:guid}/assistant/rows" },
        { "GET", "/api/imports/{id:guid}/assistant/issues/{key}" },
        { "POST", "/api/imports/{id:guid}/assistant/decisions" },
        { "POST", "/api/imports/{id:guid}/assistant/check" },
        { "POST", "/api/imports/{id:guid}/assistant/apply" },
    };

    [Fact]
    public void VersionedDefault_KeepsTheWizardDisabled()
    {
        // Fichier VERSIONNÉ du dépôt (pas une copie de sortie de build).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MMV.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "src", "MMV.Api", "appsettings.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.False(root["CatalogInterpreter"]!["WizardEnabled"]!.GetValue<bool>());
    }

    [Theory]
    [MemberData(nameof(WizardRoutes))]
    public async Task WizardDisabled_EveryRoute_Returns404AssistantDisabled_WithoutTouchingTheCatalog(string method, string pattern)
    {
        var calls = new ConcurrentQueue<string>();
        using var factory = Factory(calls);
        var endpoint = Assert.Single(Endpoints(factory), e =>
            e.RoutePattern.RawText == pattern && (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Contains(method));

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<AssistedImportOptions>().WizardEnabled);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Path = pattern.Replace("{id:guid}", Guid.NewGuid().ToString(), StringComparison.Ordinal).Replace("{key}", "k", StringComparison.Ordinal);
        if (pattern.Contains("{id:guid}", StringComparison.Ordinal))
        {
            context.Request.RouteValues["id"] = Guid.NewGuid().ToString();
        }

        if (pattern.Contains("{key}", StringComparison.Ordinal))
        {
            context.Request.RouteValues["key"] = "k";
        }

        if (method is "POST")
        {
            var body = Encoding.UTF8.GetBytes("""{"fileName":"a.csv","content":"YTtiCjE7Mgo=","requestId":"00000000-0000-0000-0000-000000000001"}""");
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = body.Length;
            context.Request.Body = new MemoryStream(body);
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"));
        var response = new MemoryStream();
        context.Response.Body = response;

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Contains("assistant_disabled", Encoding.UTF8.GetString(response.ToArray()), StringComparison.Ordinal);
        Assert.All(calls, c => Assert.True(c is nameof(ICatalogStore.ListStoresAsync) or nameof(ICatalogStore.GetStoreAsync), $"appel inattendu : {c}"));
    }

    [Fact]
    public void EveryAssistantRoute_IsCoveredByTheGateTest()
    {
        using var factory = Factory(new ConcurrentQueue<string>());
        var covered = WizardRoutes.Select(r => $"{r[0]} {r[1]}").ToHashSet(StringComparer.Ordinal);
        // État du service : toujours servi (le poste choisit l'écran) ; /api/imports/assistant GET = état de l'assistant UX-IA.
        string[] served = ["GET /api/imports/assistant/status", "GET /api/imports/assistant"];
        var actual = Endpoints(factory)
            .Where(e => e.RoutePattern.RawText is { } p && (p.Contains("/assistant", StringComparison.Ordinal) || p == "/api/imports/summary"))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => $"{m} {e.RoutePattern.RawText}"))
            .Where(r => !served.Contains(r))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(covered.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    private static WebApplicationFactory<Program> Factory(ConcurrentQueue<string> calls) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Base INJOIGNABLE : les services se construisent, mais tout accès réel à PostgreSQL échouerait (le test prouve
                // que la réponse est rendue avant toute requête).
                ["ConnectionStrings:Mmv"] = "Host=127.0.0.1;Port=1;Database=mmv_none;Username=none;Password=none;Timeout=1;Pooling=false",
                ["CatalogInterpreter:WizardEnabled"] = "false",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICatalogStore>();
                services.AddScoped(_ => StoreOnlyCatalog.Create(calls));
            });
        });

    private static List<RouteEndpoint> Endpoints(WebApplicationFactory<Program> factory) =>
        [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    /// <summary>Faux catalogue : sert le magasin unique de l'installation, refuse (et consigne) tout autre appel.</summary>
    public class StoreOnlyCatalog : DispatchProxy
    {
        private static readonly Store Shop = Store.Create(Guid.NewGuid(), "T05GATE", "Magasin fictif", "BE", "EUR", DateTimeOffset.UnixEpoch);
        private ConcurrentQueue<string> _calls = new();

        public static ICatalogStore Create(ConcurrentQueue<string> calls)
        {
            var proxy = Create<ICatalogStore, StoreOnlyCatalog>();
            ((StoreOnlyCatalog)(object)proxy)._calls = calls;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod?.Name ?? "?";
            _calls.Enqueue(name);
            return name switch
            {
                nameof(ICatalogStore.ListStoresAsync) => Task.FromResult<IReadOnlyList<Store>>([Shop]),
                nameof(ICatalogStore.GetStoreAsync) => Task.FromResult<Store?>(Shop),
                _ => throw new InvalidOperationException($"Accès au catalogue interdit quand l'assistant est désactivé : {name}"),
            };
        }
    }
}
