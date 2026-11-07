using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// COR-05-02 — temps réel SignalR avec de VRAIS clients SignalR (.NET) sur le pipeline HTTPS de MMV.Api : notification
/// après écriture confirmée seulement, groupes filtrés par droits et par magasin (serveur), aucune valeur métier dans
/// l'événement, connexion refusée sans session, fermeture après révocation (contrôle sans glissement).
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05RealtimeTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static Guid NewId() => Guid.NewGuid();

    private sealed class Station : IAsyncDisposable
    {
        public required HubConnection Connection { get; init; }

        public Channel<JsonElement> Changes { get; } = Channel.CreateUnbounded<JsonElement>();

        public TaskCompletionSource<JsonElement> Hello { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<JsonElement> NextAsync()
        {
            using var timeout = new CancellationTokenSource(Wait);
            return await Changes.Reader.ReadAsync(timeout.Token);
        }

        public async Task<bool> QuietAsync(TimeSpan duration)
        {
            using var timeout = new CancellationTokenSource(duration);
            try
            {
                await Changes.Reader.ReadAsync(timeout.Token);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
    }

    private static async Task<Station> ConnectAsync(WebApplicationFactory<Program> factory, string? token, HttpTransportType transport)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost/hubs/changes"), options =>
            {
                options.Transports = transport;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                // Comme le client JavaScript d'un navigateur (WebView2) : jeton en paramètre access_token du WebSocket.
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = factory.Server.CreateWebSocketClient();
                    var separator = context.Uri.Query.Length > 0 ? "&" : "?";
                    return await client.ConnectAsync(new Uri($"{context.Uri}{separator}access_token={Uri.EscapeDataString(token ?? string.Empty)}"), cancellationToken);
                };
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
        var station = new Station { Connection = connection };
        connection.On<JsonElement>("changed", change => station.Changes.Writer.TryWrite(change));
        connection.On<JsonElement>("hello", hello => station.Hello.TrySetResult(hello));
        connection.Closed += _ =>
        {
            station.Closed.TrySetResult();
            return Task.CompletedTask;
        };
        await connection.StartAsync();
        return station;
    }

    [Phase05Fact]
    public async Task Stations_ReceiveConfirmedChanges_FilteredByRightsAndStore_WithoutAmounts()
    {
        await using var bench = await Phase05Bench.CreateAsync(output, new Dictionary<string, string?> { ["Realtime:SessionCheckSeconds"] = "1" });
        var writer = await bench.UserAsync("ecriture", Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.CostsRead, Permissions.CostsWrite, Permissions.PricingWrite,
            Permissions.StockRead, Permissions.StockMovementsWrite, Permissions.StockAdjust, Permissions.SuppliersRead);
        var seller = await bench.UserAsync("vendeur", Permissions.CatalogRead);
        var stockist = await bench.UserAsync("stock", Permissions.StockRead);
        var revoked = await bench.UserAsync("revoque", Permissions.CatalogRead);

        // Sans session : connexion refusée.
        await Assert.ThrowsAnyAsync<Exception>(async () => await ConnectAsync(bench.Factory, "jeton-invalide", HttpTransportType.LongPolling));

        await using var a = await ConnectAsync(bench.Factory, bench.TokenOf(writer), HttpTransportType.WebSockets); // jeton en paramètre (WebSocket)
        await using var b = await ConnectAsync(bench.Factory, bench.TokenOf(seller), HttpTransportType.LongPolling); // jeton en en-tête
        await using var c = await ConnectAsync(bench.Factory, bench.TokenOf(stockist), HttpTransportType.WebSockets);
        await using var ma = await ConnectAsync(bench.FactoryMa, bench.TokenOf(writer), HttpTransportType.WebSockets); // autre magasin, autre serveur
        var helloB = await b.Hello.Task.WaitAsync(Wait);
        Assert.Equal(bench.StoreBe, helloB.GetProperty("storeId").GetGuid());
        Assert.Equal(["catalog", "pricing"], helloB.GetProperty("topics").EnumerateArray().Select(t => t.GetString()!).Order().ToArray());
        Assert.Equal(["inventory", "stock"], (await c.Hello.Task.WaitAsync(Wait)).GetProperty("topics").EnumerateArray().Select(t => t.GetString()!).Order().ToArray());

        var created = await bench.PostAsync(writer, "/api/catalog/models", new
        {
            requestId = NewId(), familyCode = "accessories", name = $"Étui temps réel {bench.Suffix}", variants = new[] { new { sku = $"RT-{bench.Suffix}", label = "Étui", stockPolicy = "stocked" } },
        });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var modelId = created["id"].GetGuid();
        var variant = (await bench.GetAsync(writer, $"/api/catalog/models/{modelId}"))["variants"][0].GetProperty("row").GetProperty("variantId").GetGuid();
        var first = await b.NextAsync();
        Assert.Contains("catalog", first.GetProperty("topics").EnumerateArray().Select(t => t.GetString()));
        Assert.StartsWith("POST", first.GetProperty("kind").GetString(), StringComparison.Ordinal);
        var stockEvent = await c.NextAsync(); // la création notifie aussi « stock » (nouvelle ligne de stock)
        Assert.Contains("stock", stockEvent.GetProperty("topics").EnumerateArray().Select(t => t.GetString()));
        await a.NextAsync();

        // Poste A : mouvement de stock → B (catalogue : disponibilité) et C (stock) notifiés ; magasin MA jamais.
        var movement = await bench.PostAsync(writer, "/api/stock/movements", new { requestId = NewId(), variantId = variant, locationId = bench.LocationBe, type = "manual_in", quantity = 4, reasonCode = "found" });
        Assert.Equal(HttpStatusCode.Created, movement.Status);
        var forC = await c.NextAsync();
        Assert.Contains("stock", forC.GetProperty("topics").EnumerateArray().Select(t => t.GetString()));
        var forB = await b.NextAsync();
        Assert.True(forB.GetProperty("seq").GetInt64() > first.GetProperty("seq").GetInt64());
        await a.NextAsync();

        // Prix (avec coût) : B notifié SANS aucune valeur ; C (sans catalog.read) jamais.
        var price = await bench.PutAsync(writer, $"/api/pricing/variants/{variant}", new { requestId = NewId(), purchasePriceHt = "12.3456", coefficient = "2.5", expectedSalePriceHt = "30.86" });
        Assert.True(price.Status == HttpStatusCode.OK, price.Raw);
        var priceEvent = await b.NextAsync();
        var raw = priceEvent.GetRawText();
        Assert.Contains("pricing", raw, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "12.3456", "30.86", "2.5", "purchase", "coefficient", "price\":", "salePrice" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }

        await a.NextAsync();
        Assert.True(await c.QuietAsync(TimeSpan.FromSeconds(1)));

        // Écriture refusée (stock négatif) ou conflit de version : AUCUNE notification.
        var refused = await bench.PostAsync(writer, "/api/stock/movements", new { requestId = NewId(), variantId = variant, locationId = bench.LocationBe, type = "manual_out", quantity = 999, reasonCode = "breakage" });
        Assert.NotEqual(HttpStatusCode.Created, refused.Status);
        var stale = await bench.PutAsync(writer, $"/api/catalog/models/{modelId}", new { requestId = NewId(), version = 999u, name = "Conflit" });
        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.True(await b.QuietAsync(TimeSpan.FromSeconds(1)));
        Assert.True(await c.QuietAsync(TimeSpan.FromMilliseconds(200)));

        // Écritures concurrentes sur la même version : une seule réussit, l'autre est refusée (jamais écrasée en silence).
        var version = (await bench.GetAsync(writer, $"/api/catalog/models/{modelId}"))["version"].GetUInt32();
        var race = await Task.WhenAll(
            bench.PutAsync(writer, $"/api/catalog/models/{modelId}", new { requestId = NewId(), version, name = $"Poste A {bench.Suffix}" }),
            bench.PutAsync(writer, $"/api/catalog/models/{modelId}", new { requestId = NewId(), version, name = $"Poste B {bench.Suffix}" }));
        Assert.Single(race, r => r.Status == HttpStatusCode.OK);
        Assert.Single(race, r => r.Status == HttpStatusCode.Conflict);

        // Le serveur du magasin Maroc n'a rien diffusé (autre installation).
        Assert.True(await ma.QuietAsync(TimeSpan.FromMilliseconds(300)));

        // Révocation : déconnexion de la session → connexion temps réel fermée par le contrôle périodique.
        await using var r = await ConnectAsync(bench.Factory, bench.TokenOf(revoked), HttpTransportType.WebSockets);
        await r.Hello.Task.WaitAsync(Wait);
        Assert.Equal(HttpStatusCode.NoContent, (await bench.PostAsync(revoked, "/api/auth/logout", new { })).Status);
        await r.Closed.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
