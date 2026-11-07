using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MMV.Application.Catalog;
using MMV.Application.Catalog.Assisted;
using MMV.Domain.CsvImport;

namespace MMV.Api.Installation;

/// <summary>
/// Interpréteur de catalogue du NOUVEAU parcours (contrat <c>mmv.csvai.v2</c>, modèle configuré <c>qwen3.5:4b</c>,
/// section <c>CatalogInterpreter</c>) : boucle locale obligatoire (contrôle <c>IsLoopback</c> de l'assistant actuel),
/// paramètres d'inférence du banc B0, <c>think: false</c> (aucune trace de raisonnement demandée ni conservée : le champ
/// <c>thinking</c> d'une réponse est ignoré), délai configurable (120 s provisoires, sans SLA), annulation. Le modèle
/// affiché comme ACTIF est celui que rapporte la réponse d'Ollama ; une réponse d'un autre modèle est hors contrat.
/// Aucun téléchargement de modèle : un modèle absent rend l'analyse indisponible.
/// </summary>
public sealed partial class OllamaCatalogInterpreter(HttpClient http, AssistedImportOptions options, ILogger<OllamaCatalogInterpreter> logger) : ICatalogInterpreter
{
    public const string HttpClientName = "mmv-catalog-interpreter";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private (DateTimeOffset At, InterpreterProbe Probe)? _probe;

    public string ConfiguredModel => options.Model;

    public bool EndpointIsLocal => OllamaImportAssistant.IsLoopback(options.Endpoint);

    public async Task<InterpreterProbe> ProbeAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled || !EndpointIsLocal)
        {
            return new InterpreterProbe(false, null, false, null);
        }

        if (_probe is { } cached && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromSeconds(10))
        {
            return cached.Probe;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        InterpreterProbe probe;
        try
        {
            var version = (await http.GetFromJsonAsync<JsonObject>(Url("/api/version"), Json, timeout.Token))?["version"]?.GetValue<string>();
            var tags = await http.GetFromJsonAsync<JsonObject>(Url("/api/tags"), Json, timeout.Token);
            var model = (tags?["models"] as JsonArray)?.FirstOrDefault(m => SameModel(m?["name"]?.GetValue<string>(), options.Model));
            probe = new InterpreterProbe(true, version, model is not null, model?["digest"]?.GetValue<string>());
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            probe = new InterpreterProbe(false, null, false, null);
        }

        _probe = (DateTimeOffset.UtcNow, probe);
        return probe;
    }

    public async Task<InterpreterResult> InterpretAsync(InterpreterRequest request, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        if (!options.Enabled || !EndpointIsLocal)
        {
            return new InterpreterResult(AnalysisStatuses.Unavailable, null, null, null, null, "endpoint_not_local", 0);
        }

        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["stream"] = false,
            ["think"] = false,
            ["keep_alive"] = options.KeepAlive,
            ["format"] = request.Schema,
            ["options"] = CatalogInterpretationContract.Options(),
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = request.UserMessage },
            },
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 600)));
        try
        {
            using var response = await http.PostAsJsonAsync(Url("/api/chat"), body, Json, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogFailure(logger, AnalysisStatuses.Unavailable, (int)response.StatusCode);
                return new InterpreterResult(AnalysisStatuses.Unavailable, null, null, null, null, $"http_{(int)response.StatusCode}", watch.ElapsedMilliseconds);
            }

            var reply = await response.Content.ReadFromJsonAsync<JsonObject>(Json, timeout.Token);
            var content = reply?["message"]?["content"]?.GetValue<string>();
            var model = reply?["model"]?.GetValue<string>();
            var evalCount = reply?["eval_count"] is JsonValue ev && ev.TryGetValue<int>(out var count) ? count : (int?)null;
            var raw = content is null ? null : content.Length <= BatchAnalysis.RawOutputMaxLength ? content : content[..BatchAnalysis.RawOutputMaxLength];
            if (!SameModel(model, options.Model))
            {
                // Le modèle affiché comme actif doit être celui configuré ET confirmé par la réponse.
                LogFailure(logger, AnalysisStatuses.InvalidOutput, 0);
                return new InterpreterResult(AnalysisStatuses.InvalidOutput, model, raw, null, evalCount, "model_mismatch", watch.ElapsedMilliseconds);
            }

            var (proposal, error) = CatalogInterpretationContract.Parse(content, request.Document, reply?["done_reason"]?.GetValue<string>());
            LogAnalysis(logger, proposal is null ? AnalysisStatuses.InvalidOutput : AnalysisStatuses.Done, model!, request.Sample.Indexes.Count, watch.ElapsedMilliseconds);
            return proposal is null
                ? new InterpreterResult(AnalysisStatuses.InvalidOutput, model, raw, null, evalCount, error, watch.ElapsedMilliseconds)
                : new InterpreterResult(AnalysisStatuses.Done, model, raw, proposal, evalCount, null, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(logger, AnalysisStatuses.Timeout, 0);
            return new InterpreterResult(AnalysisStatuses.Timeout, null, null, null, null, $"timeout_{options.TimeoutSeconds}s", watch.ElapsedMilliseconds);
        }
        catch (HttpRequestException)
        {
            LogFailure(logger, AnalysisStatuses.Unavailable, 0);
            return new InterpreterResult(AnalysisStatuses.Unavailable, null, null, null, null, "ollama_unreachable", watch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            LogFailure(logger, AnalysisStatuses.InvalidOutput, 0);
            return new InterpreterResult(AnalysisStatuses.InvalidOutput, null, null, null, null, "invalid_response", watch.ElapsedMilliseconds);
        }
    }

    internal static bool SameModel(string? reported, string configured) =>
        reported is not null && (string.Equals(reported, configured, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reported, configured + ":latest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reported + ":latest", configured, StringComparison.OrdinalIgnoreCase));

    private Uri Url(string path) => new(new Uri(options.Endpoint.TrimEnd('/') + "/"), path.TrimStart('/'));

    [LoggerMessage(EventId = 5210, Level = LogLevel.Information, Message = "Interpréteur de catalogue : {Status} (modèle {Model}, {Sampled} lignes vues, {ElapsedMs} ms).")]
    private static partial void LogAnalysis(ILogger logger, string status, string model, int sampled, long elapsedMs);

    [LoggerMessage(EventId = 5211, Level = LogLevel.Warning, Message = "Interpréteur de catalogue : échec {Status} (HTTP {HttpStatus}) ; choix explicite proposé (relancer ou vérifier sans IA).")]
    private static partial void LogFailure(ILogger logger, string status, int httpStatus);
}

/// <summary>
/// File d'analyse (C1.5) : UNE analyse à la fois par serveur, canal borné (position réelle, file pleine refusée),
/// annulation de la requête Ollama d'une tentative annulée, reprise au redémarrage (tentatives actives → <c>interrupted</c>,
/// T14). Inactive si l'écran n'est pas activé (<c>WizardEnabled</c>) : aucune requête vers les tables de 0009.
/// </summary>
public sealed partial class AnalysisScheduler(IServiceScopeFactory scopes, AssistedImportOptions options, TimeProvider clock, ILogger<AnalysisScheduler> logger)
    : BackgroundService, IAnalysisScheduler
{
    private readonly Channel<Guid> _queue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Math.Clamp(options.QueueCapacity, 1, 100)) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    // Canal borné en mode « attente » : TryWrite échoue (file pleine) au lieu de bloquer ou de perdre la demande.
    public bool TryEnqueue(Guid analysisId) => options.WizardEnabled && _queue.Writer.TryWrite(analysisId);

    public void Cancel(Guid analysisId)
    {
        if (_running.TryGetValue(analysisId, out var cts))
        {
            cts.Cancel();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.WizardEnabled)
        {
            return;
        }

        await InterruptActiveAsync(stoppingToken);
        await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _running[id] = cts;
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AssistedAnalysisRunner>().RunAsync(id, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Annulée par l'utilisateur (état déjà « cancelled ») ou arrêt du serveur (« interrupted » au redémarrage).
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRunFailed(logger, id, exception.GetType().Name);
            }
            finally
            {
                _running.TryRemove(id, out _);
            }
        }
    }

    /// <summary>T14 : au démarrage, toute tentative restée en file ou en cours est close « interrupted » (choix explicite proposé).</summary>
    private async Task InterruptActiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IImportAssistantStore>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<ICatalogUnitOfWork>();
            var now = clock.GetUtcNow();
            var active = await store.ActiveAnalysesAsync(cancellationToken);
            foreach (var analysis in active)
            {
                analysis.Interrupt(now);
                store.Add(BatchAnalysisEvent.Create(analysis.Id, analysis.BatchId, now, AnalysisEventCodes.AnalysisInterrupted, new JsonObject { ["reason"] = "server_restart" }.ToJsonString()));
            }

            if (active.Count > 0)
            {
                await unitOfWork.SaveAsync(cancellationToken);
                LogInterrupted(logger, active.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRunFailed(logger, Guid.Empty, exception.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 5212, Level = LogLevel.Warning, Message = "File d'analyse : tentative {AnalysisId} en échec technique ({Error}).")]
    private static partial void LogRunFailed(ILogger logger, Guid analysisId, string error);

    [LoggerMessage(EventId = 5213, Level = LogLevel.Information, Message = "File d'analyse : {Count} tentative(s) interrompue(s) par le redémarrage du serveur.")]
    private static partial void LogInterrupted(ILogger logger, int count);
}

public static class CatalogInterpreterSetup
{
    public static IServiceCollection AddCatalogInterpreter(this IServiceCollection services)
    {
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection(AssistedImportOptions.SectionName).Get<AssistedImportOptions>() ?? new AssistedImportOptions());
        services.AddHttpClient(OllamaCatalogInterpreter.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<ICatalogInterpreter>(sp => new OllamaCatalogInterpreter(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(OllamaCatalogInterpreter.HttpClientName),
            sp.GetRequiredService<AssistedImportOptions>(), sp.GetRequiredService<ILogger<OllamaCatalogInterpreter>>()));
        services.AddSingleton<AnalysisScheduler>();
        services.AddSingleton<IAnalysisScheduler>(sp => sp.GetRequiredService<AnalysisScheduler>());
        services.AddHostedService(sp => sp.GetRequiredService<AnalysisScheduler>());
        services.AddScoped<AssistedAnalysisRunner>();
        services.AddScoped<AssistedImportService>();
        return services;
    }
}
