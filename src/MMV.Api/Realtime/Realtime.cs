using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MMV.Api.Auth;
using MMV.Api.Installation;
using MMV.Application.Catalog;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Realtime;

/// <summary>
/// Sujets de notification (COR-05-02) et droit de lecture exigé pour les recevoir. Architecture réutilisable : un
/// module futur (ventes, commandes…) déclare son sujet ici et marque ses routes d'écriture par
/// <see cref="RealtimeSetup.PublishesChanges{TBuilder}"/> ; rien d'autre n'est requis côté serveur.
/// </summary>
public static class ChangeTopics
{
    public const string Catalog = "catalog";
    public const string Pricing = "pricing";
    public const string Stock = "stock";
    public const string Suppliers = "suppliers";
    public const string Inventory = "inventory";
    public const string Imports = "imports";

    /// <summary>Droit nécessaire par sujet : un poste ne reçoit que les sujets qu'il a le droit de lire.</summary>
    public static readonly IReadOnlyDictionary<string, string> ReadPermission = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Catalog] = Permissions.CatalogRead,
        [Pricing] = Permissions.CatalogRead,
        [Stock] = Permissions.StockRead,
        [Suppliers] = Permissions.SuppliersRead,
        [Inventory] = Permissions.StockRead,
        [Imports] = Permissions.CatalogImport,
    };
}

/// <summary>
/// Notification émise APRÈS une écriture confirmée (transaction validée) : sujets touchés, route, identifiants de la
/// route. AUCUNE valeur métier (ni prix, ni coût, ni coefficient, ni quantité) : le poste relit l'API, qui applique les
/// droits (<c>costs.read</c>…). <c>Seq</c> croît strictement pour une instance de serveur (<c>Server</c>) : un poste
/// qui voit changer <c>Server</c> (redémarrage) ou se reconnecte resynchronise tout.
/// </summary>
public sealed record ChangeEvent(long Seq, Guid Server, IReadOnlyList<string> Topics, string Kind, IReadOnlyDictionary<string, string> Ids, DateTimeOffset AtUtc);

/// <summary>Message d'accueil : instance du serveur, dernier numéro émis, sujets rejoints.</summary>
public sealed record HelloMessage(Guid Server, long Seq, Guid StoreId, IReadOnlyList<string> Topics);

/// <summary>
/// Diffusion aux connexions du magasin. UN envoi par connexion (jamais de doublon), sujets réduits à ceux que la
/// session a le droit de lire ; une connexion sans sujet commun ne reçoit rien. Singleton ; aucune écriture en base.
/// </summary>
public sealed class ChangeNotifier(IHubContext<ChangesHub> hub, RealtimeConnections connections, TimeProvider clock)
{
    private long _seq;

    public Guid Server { get; } = Guid.NewGuid();

    public long LastSeq => Interlocked.Read(ref _seq);

    public async Task PublishAsync(Guid storeId, IReadOnlyList<string> topics, string kind, IReadOnlyDictionary<string, string> ids, CancellationToken cancellationToken)
    {
        var seq = Interlocked.Increment(ref _seq);
        var now = clock.GetUtcNow();
        foreach (var connection in connections.Snapshot())
        {
            var visible = topics.Where(connection.Topics.Contains).ToList();
            if (connection.StoreId == storeId && visible.Count > 0)
            {
                await hub.Clients.Client(connection.Context.ConnectionId).SendAsync("changed", new ChangeEvent(seq, Server, visible, kind, ids, now), cancellationToken);
            }
        }
    }
}

/// <summary>Connexion ouverte : session d'origine (révocation), magasin et sujets autorisés (filtrage de diffusion).</summary>
public sealed record RealtimeConnection(HubCallerContext Context, Guid SessionId, Guid StoreId, IReadOnlySet<string> Topics);

/// <summary>Connexions ouvertes de cette instance de serveur.</summary>
public sealed class RealtimeConnections
{
    private readonly ConcurrentDictionary<string, RealtimeConnection> _connections = new(StringComparer.Ordinal);

    public int Count => _connections.Count;

    public void Add(RealtimeConnection connection) => _connections[connection.Context.ConnectionId] = connection;

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyList<RealtimeConnection> Snapshot() => [.. _connections.Values];
}

/// <summary>
/// Hub « /hubs/changes » : authentifié (jeton de session ; en WebSocket, paramètre <c>access_token</c> réservé à ce
/// chemin), HTTPS seulement, magasin de l'installation résolu. Le poste ne fait que RECEVOIR : aucune méthode
/// invocable ne modifie quoi que ce soit, et les sujets reçus sont choisis par le serveur selon les droits de la session.
/// </summary>
[Authorize]
public sealed class ChangesHub(InstallationStoreResolver resolver, ICatalogStore store, ChangeNotifier notifier, RealtimeConnections connections) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var user = Context.User;
        if (http is null || !http.Request.IsHttps || user?.FindFirstValue(SessionAuthenticationHandler.SessionIdClaim) is not { } sid || !Guid.TryParse(sid, out var sessionId))
        {
            Context.Abort();
            return;
        }

        var resolution = await resolver.ResolveAsync(store, Context.ConnectionAborted);
        if (!resolution.IsResolved)
        {
            Context.Abort();
            return;
        }

        var storeId = resolution.Store!.Id;
        var topics = ChangeTopics.ReadPermission
            .Where(p => user.HasClaim(SessionAuthenticationHandler.PermissionClaim, p.Value))
            .Select(p => p.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
        connections.Add(new RealtimeConnection(Context, sessionId, storeId, topics.ToHashSet(StringComparer.Ordinal)));
        await Clients.Caller.SendAsync("hello", new HelloMessage(notifier.Server, notifier.LastSeq, storeId, topics), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Révocation : toutes les 30 s (<c>Realtime:SessionCheckSeconds</c>, 1 à 300), chaque connexion dont la session n'est plus valide (révoquée, expirée, inactive,
/// compte désactivé) est fermée. Contrôle en LECTURE seule : une connexion ouverte ne prolonge jamais la session.
/// </summary>
internal sealed partial class RealtimeSessionMonitor(
    RealtimeConnections connections, IServiceScopeFactory scopes, IConfiguration configuration, ILogger<RealtimeSessionMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Clamp(configuration.GetValue("Realtime:SessionCheckSeconds", 30), 1, 300);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = connections.Snapshot();
            if (snapshot.Count == 0)
            {
                continue;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                foreach (var connection in snapshot)
                {
                    if (!await auth.IsSessionValidAsync(connection.SessionId, stoppingToken))
                    {
                        connections.Remove(connection.Context.ConnectionId);
                        connection.Context.Abort();
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSweepFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(EventId = 5200, Level = LogLevel.Warning, Message = "Temps réel : contrôle des sessions impossible (nouvel essai au prochain cycle).")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}

/// <summary>Marque une route POST de LECTURE (aperçu, évaluation) : jamais de notification.</summary>
public sealed class NoChangeNotification
{
    public static readonly NoChangeNotification Instance = new();
}

public static class RealtimeSetup
{
    public const string HubPath = "/hubs/changes";
    public const string CorsPolicyName = "desktop-realtime";

    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.MaximumReceiveMessageSize = 4 * 1024;
        });
        services.AddSingleton<ChangeNotifier>();
        services.AddSingleton<RealtimeConnections>();
        services.AddHostedService<RealtimeSessionMonitor>();
        return services;
    }

    public static void MapRealtime(this WebApplication app) =>
        app.MapHub<ChangesHub>(HubPath).RequireCors(CorsPolicyName);

    /// <summary>
    /// Route d'écriture notifiante : après le gestionnaire, SEULEMENT si la réponse est un succès 2xx (transaction
    /// validée par le service avant de répondre ; un refus, un conflit ou une annulation ne notifie jamais), diffusion
    /// des sujets aux postes du magasin de l'installation. Une réémission idempotente renotifie sans effet de bord.
    /// </summary>
    public static TBuilder PublishesChanges<TBuilder>(this TBuilder builder, params string[] topics)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            var http = context.HttpContext;
            if (HttpMethods.IsGet(http.Request.Method) || result is not IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }
                || http.GetEndpoint()?.Metadata.GetMetadata<NoChangeNotification>() is not null)
            {
                return result;
            }

            var notifier = http.RequestServices.GetRequiredService<ChangeNotifier>();
            var ids = http.Request.RouteValues
                .Where(v => v.Key is "id" or "variantId" or "supplierId" or "locationId" && v.Value is string)
                .ToDictionary(v => v.Key, v => (string)v.Value!, StringComparer.Ordinal);
            var kind = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? string.Empty;
            try
            {
                // Hors du jeton de la requête : la réponse est déjà décidée, la diffusion ne doit pas être annulée par le poste.
                await notifier.PublishAsync(ActiveStore.Of(http).Id, topics, $"{http.Request.Method} {kind}", ids, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                RealtimeLog.PublishFailed(http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MMV.Api.Realtime"), exception);
            }

            return result;
        });
}

internal static partial class RealtimeLog
{
    [LoggerMessage(EventId = 5201, Level = LogLevel.Warning, Message = "Temps réel : diffusion impossible (les postes se resynchronisent à la reconnexion ou par actualisation).")]
    public static partial void PublishFailed(ILogger logger, Exception exception);
}
