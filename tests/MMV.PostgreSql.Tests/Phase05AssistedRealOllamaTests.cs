using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MMV.Domain.Identity;
using Xunit.Abstractions;

namespace MMV.PostgreSql.Tests;

/// <summary>
/// CSV-IMPORT-AI-01 — parcours réel avec Ollama RÉEL (aucun faux service) : <c>qwen3.5:4b</c> déjà installé (aucun
/// téléchargement), API réelle en mémoire, PostgreSQL 18 jetable avec 0009, corpus SYNTHÉTIQUE des bancs (F01, F07, F17).
/// Activé seulement par <c>MMV_CSVAI_REAL_OLLAMA</c> (adresse locale d'Ollama) ; sinon NOT_TESTED. Mesures indicatives
/// sur le poste de développement, sans valeur de SLA (BL-05-U2).
/// </summary>
[Trait("Stage", "Phase05")]
public sealed class Phase05AssistedRealOllamaTests(ITestOutputHelper output)
{
    public const string Variable = "MMV_CSVAI_REAL_OLLAMA";

    private static readonly string[] Rights = [Permissions.CatalogRead, Permissions.CatalogWrite, Permissions.CatalogImport, Permissions.CostsRead, Permissions.CostsWrite];

    public sealed class RealOllamaFactAttribute : FactAttribute
    {
        public RealOllamaFactAttribute() =>
            Skip = Phase05Target.AssistantSkipReason ?? (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)) ? $"NOT_TESTED : {Variable} absente (Ollama réel non demandé)." : null);
    }

    [RealOllamaFact]
    public async Task RealQwen_AnalysesCorpusFiles_ModelConfirmed_ProposalRevalidated_NothingWrittenBeforeConfirmation()
    {
        var endpoint = Environment.GetEnvironmentVariable(Variable)!;
        await using var bench = await Phase05Bench.CreateAsync(output, new Dictionary<string, string?>
        {
            ["CatalogInterpreter:Enabled"] = "true", ["CatalogInterpreter:WizardEnabled"] = "true", ["CatalogInterpreter:Mode"] = "always",
            ["CatalogInterpreter:Endpoint"] = endpoint, ["CatalogInterpreter:Model"] = "qwen3.5:4b", ["CatalogInterpreter:TimeoutSeconds"] = "120",
        });
        var admin = await bench.UserAsync("ollama", Rights);
        var root = Phase05SchemaTests.FindRepositoryRoot();
        var status = await bench.GetAsync(admin, "/api/imports/assistant/status");
        output.WriteLine($"état : {status.Raw}");
        Assert.True(status["ollamaReachable"].GetBoolean());
        Assert.True(status["modelInstalled"].GetBoolean());
        foreach (var (id, path) in new[] { ("F01", "docs/phase/05/evidence/csvai/corpus/F01.csv"), ("F07", "docs/phase/05/evidence/csvai/corpus/F07.csv"), ("F17", "docs/phase/05/evidence/csvai/b0/corpus/F17.csv") })
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, path));
            var watch = Stopwatch.StartNew();
            var created = await bench.PostAsync(admin, "/api/imports/assistant", new { requestId = Guid.NewGuid(), fileName = $"{id}.csv", contentBase64 = Convert.ToBase64String(bytes), target = "products" });
            Assert.True(created.Status == HttpStatusCode.Created, created.Raw);
            var batch = created["batchId"].GetGuid();
            JsonElement analysis = default;
            for (var i = 0; i < 600; i++)
            {
                var state = await bench.GetAsync(admin, $"/api/imports/{batch}/assistant/analysis");
                analysis = state.Body;
                if (analysis.GetProperty("status").GetString() is not ("queued" or "running"))
                {
                    break;
                }

                await Task.Delay(500);
            }

            var final = analysis.GetProperty("status").GetString();
            var view = await bench.GetAsync(admin, $"/api/imports/{batch}/assistant");
            var columns = string.Join(", ", view["review"].GetProperty("columns").EnumerateArray().Select(c => $"{c.GetProperty("header").GetString()}→{c.GetProperty("field").GetString()}/{c.GetProperty("status").GetString()}/{c.GetProperty("origin").GetString()}"));
            output.WriteLine($"{id} : {final} en {watch.ElapsedMilliseconds} ms (serveur {analysis.GetProperty("elapsedMs").GetInt64()} ms), modèle actif {analysis.GetProperty("model").GetProperty("active")}, "
                + $"couverture {analysis.GetProperty("coverage")}, confirmations {view["review"].GetProperty("confirmations").GetInt32()}");
            output.WriteLine($"{id} colonnes : {columns}");
            output.WriteLine($"{id} événements : {string.Join(" > ", analysis.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("code").GetString()))}");
            Assert.Equal("done", final);
            Assert.Equal("qwen3.5:4b", analysis.GetProperty("model").GetProperty("active").GetString());
            Assert.False(string.IsNullOrEmpty(analysis.GetProperty("model").GetProperty("digest").GetString()));
            Assert.Equal(0L, await bench.ScalarAsync<long>("SELECT count(*) FROM catalog.product_variants WHERE created_by_user_id IN (SELECT uploaded_by_user_id FROM imports.batches WHERE id = @b)", ("b", batch)));
            Assert.Equal(1L, await bench.ScalarAsync<long>("SELECT count(*) FROM imports.batch_analyses WHERE batch_id = @b AND status = 'done' AND proposal IS NOT NULL AND model = 'qwen3.5:4b'", ("b", batch)));
        }
    }
}
