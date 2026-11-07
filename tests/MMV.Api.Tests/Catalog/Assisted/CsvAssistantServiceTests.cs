using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using MMV.Api.Installation;
using MMV.Application.Catalog.Assisted;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;

namespace MMV.Api.Tests.Catalog.Assisted;

/// <summary>
/// CSV-IMPORT-AI-01 (C1.4, C1.6, C2) sans base : client Ollama du nouveau parcours (requête, confirmation du modèle,
/// lecture stricte, délai, boucle locale, aucune trace de raisonnement), matérialisation (montants, libellés, champs non
/// écrits), inspection et destination suggérée, couche de décisions.
/// </summary>
public sealed class CsvAssistantServiceTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return await respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (OllamaCatalogInterpreter Interpreter, Handler Handler) Interpreter(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, string endpoint = "http://127.0.0.1:11434", int timeout = 120)
    {
        var handler = new Handler(respond);
        var options = new AssistedImportOptions { Endpoint = endpoint, Model = "qwen3.5:4b", TimeoutSeconds = timeout };
        return (new OllamaCatalogInterpreter(new HttpClient(handler), options, NullLogger<OllamaCatalogInterpreter>.Instance), handler);
    }

    private static InterpreterRequest Request(CsvDocument document)
    {
        var sample = CatalogInterpretationContract.Sample(document);
        return new InterpreterRequest(document, sample, CatalogInterpretationContract.SystemPrompt(), CatalogInterpretationContract.UserMessage(document, sample), CatalogInterpretationContract.Schema(document));
    }

    private static readonly CsvDocument Doc = TolerantCsvReader.Parse("Marque;Modèle\nRay;A1\nRay;A2\n", "utf-8");

    private const string ValidContent = """{"header":"present","columns":{"c0":{"field":"brand","confidence":"high","issue":"none"},"c1":{"field":"modelName","confidence":"high","issue":"none"}},"family":{"mode":"unknown","single":"unknown","column":"none"}}""";

    private static string Reply(string model, string content, string doneReason = "stop") =>
        new JsonObject { ["model"] = model, ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content, ["thinking"] = "trace interne" }, ["done_reason"] = doneReason, ["eval_count"] = 42 }.ToJsonString();

    [Fact]
    public async Task Interpreter_sends_the_b0_contract_and_confirms_the_model_from_the_reply()
    {
        var (interpreter, handler) = Interpreter((_, _) => Task.FromResult(Json(Reply("qwen3.5:4b", ValidContent))));
        var result = await interpreter.InterpretAsync(Request(Doc), CancellationToken.None);
        Assert.Equal(AnalysisStatuses.Done, result.Status);
        Assert.Equal("qwen3.5:4b", result.Model);
        Assert.Equal(42, result.EvalCount);
        Assert.Equal("brand", result.Proposal!.Columns[0].Field);
        Assert.DoesNotContain("trace interne", result.RawOutput, StringComparison.Ordinal);
        var body = JsonNode.Parse(handler.Bodies.Single())!;
        Assert.False(body["think"]!.GetValue<bool>());
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.Equal("qwen3.5:4b", body["model"]!.GetValue<string>());
        Assert.Equal(0, body["options"]!["temperature"]!.GetValue<int>());
        Assert.Equal(7, body["options"]!["seed"]!.GetValue<int>());
        Assert.Equal("object", body["format"]!["type"]!.GetValue<string>());
        Assert.Contains("<data>", body["messages"]![1]!["content"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Interpreter_never_reports_another_model_as_active()
    {
        var (interpreter, _) = Interpreter((_, _) => Task.FromResult(Json(Reply("llama3.2:latest", ValidContent))));
        var result = await interpreter.InterpretAsync(Request(Doc), CancellationToken.None);
        Assert.Equal((AnalysisStatuses.InvalidOutput, "model_mismatch"), (result.Status, result.Error));
        Assert.Null(result.Proposal);
    }

    [Fact]
    public async Task Interpreter_rejects_truncated_or_off_contract_output_entirely()
    {
        var (truncated, _) = Interpreter((_, _) => Task.FromResult(Json(Reply("qwen3.5:4b", ValidContent, "length"))));
        Assert.Equal((AnalysisStatuses.InvalidOutput, "truncated"), ((await truncated.InterpretAsync(Request(Doc), CancellationToken.None)) is var r1 ? (r1.Status, r1.Error) : default));
        var (extra, _) = Interpreter((_, _) => Task.FromResult(Json(Reply("qwen3.5:4b", ValidContent.Replace("\"header\":\"present\"", "\"header\":\"present\",\"note\":\"x\"", StringComparison.Ordinal)))));
        Assert.Equal(AnalysisStatuses.InvalidOutput, (await extra.InterpretAsync(Request(Doc), CancellationToken.None)).Status);
        var (garbage, _) = Interpreter((_, _) => Task.FromResult(Json("<html>")));
        Assert.Equal(AnalysisStatuses.InvalidOutput, (await garbage.InterpretAsync(Request(Doc), CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Interpreter_reports_unavailable_timeout_and_refuses_non_local_endpoints()
    {
        var (down, _) = Interpreter((_, _) => throw new HttpRequestException("refused"));
        Assert.Equal(AnalysisStatuses.Unavailable, (await down.InterpretAsync(Request(Doc), CancellationToken.None)).Status);
        var (error, _) = Interpreter((_, _) => Task.FromResult(Json("{}", HttpStatusCode.InternalServerError)));
        Assert.Equal((AnalysisStatuses.Unavailable, "http_500"), ((await error.InterpretAsync(Request(Doc), CancellationToken.None)) is var r ? (r.Status, r.Error) : default));
        var (slow, _) = Interpreter(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(Reply("qwen3.5:4b", ValidContent));
        }, timeout: 5);
        var timedOut = await slow.InterpretAsync(Request(Doc), CancellationToken.None);
        Assert.Equal(AnalysisStatuses.Timeout, timedOut.Status);
        Assert.InRange(timedOut.ElapsedMs, 4000, 15000);
        var (remote, handler) = Interpreter((_, _) => Task.FromResult(Json(Reply("qwen3.5:4b", ValidContent))), endpoint: "http://192.168.1.20:11434");
        Assert.Equal((AnalysisStatuses.Unavailable, "endpoint_not_local"), ((await remote.InterpretAsync(Request(Doc), CancellationToken.None)) is var x ? (x.Status, x.Error) : default));
        Assert.Empty(handler.Bodies);
        Assert.False((await remote.ProbeAsync(CancellationToken.None)).Reachable);
    }

    [Fact]
    public async Task Interpreter_user_cancellation_propagates_and_is_not_a_timeout()
    {
        var (slow, _) = Interpreter(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(Reply("qwen3.5:4b", ValidContent));
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slow.InterpretAsync(Request(Doc), cts.Token));
    }

    [Fact]
    public async Task Probe_reads_version_and_installed_model_without_downloading()
    {
        var (interpreter, handler) = Interpreter((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/version" => Json("{\"version\":\"0.40.14\"}"),
            "/api/tags" => Json("{\"models\":[{\"name\":\"qwen3.5:4b\",\"digest\":\"d8b0f5e9760c\"},{\"name\":\"llama3.2:latest\",\"digest\":\"a80c\"}]}"),
            _ => Json("{}", HttpStatusCode.NotFound),
        }));
        var probe = await interpreter.ProbeAsync(CancellationToken.None);
        Assert.Equal(new InterpreterProbe(true, "0.40.14", true, "d8b0f5e9760c"), probe);
        Assert.Empty(handler.Bodies); // GET seulement : aucun /api/pull
    }

    // ------------------------------------------------------------------ matérialisation

    [Fact]
    public void Money_examples_cover_all_rows_and_their_digest_changes_with_the_values()
    {
        var document = TolerantCsvReader.Parse("Modèle;Prix\nA;12,50\nB;9\nC;illisible\nD;\nE;140,00 €\n", "utf-8");
        var d = CatalogInterpretationValidator.Validate(document, null);
        var (examples, digest) = CatalogMaterializer.MoneyExamples(d, 1, "EUR");
        Assert.Equal(3, examples["readable"]!.GetValue<int>());
        Assert.Equal(1, examples["unreadable"]!.GetValue<int>());
        Assert.Equal(1, examples["empty"]!.GetValue<int>());
        Assert.Equal("9", examples["min"]!.GetValue<string>());
        Assert.Equal("140.00", examples["max"]!.GetValue<string>());
        Assert.False(examples["conversion"]!.GetValue<bool>());
        var roles = examples["examples"]!.AsArray().Select(e => e!["role"]!.GetValue<string>()).ToList();
        Assert.Contains("unreadable", roles);
        Assert.Contains("warning", roles);
        var corrected = AssistantUserLayer.From([CsvAssistantCoreTests.Decision(2, DecisionTypes.CellValue, """{"line":4,"column":1}""", """{"value":"15"}""")]);
        var (_, after) = CatalogMaterializer.MoneyExamples(CatalogInterpretationValidator.Validate(document, null, corrected), 1, "EUR");
        Assert.NotEqual(digest, after);
        Assert.Equal(digest, CatalogMaterializer.MoneyExamples(CatalogInterpretationValidator.Validate(document, null), 1, "EUR").Digest);
    }

    [Fact]
    public void Money_column_issue_offers_no_preselection_and_never_ttc()
    {
        var document = TolerantCsvReader.Parse("Modèle;Prix\nA;12,50\nB;13\n", "utf-8");
        var d = CatalogInterpretationValidator.Validate(document, null);
        var issue = CatalogMaterializer.Issues(d, AssistantUserLayer.Empty, new CatalogMaterializer.IssueContext(AssistantTargets.Products, "EUR")).Single(i => i.Key == "col:1");
        Assert.Equal("money_column", issue.Code);
        Assert.Null(issue.Suggestion);
        Assert.Equal(["purchasePriceHt", "salePriceHt", "ignore"], issue.Actions.Select(a => a.Constraints!["field"]!.GetValue<string>()));
        Assert.All(issue.Actions.Take(2), a => Assert.Equal(issue.ExamplesDigest, a.Constraints!["examplesDigest"]!.GetValue<string>()));
        Assert.Contains(issue.NotOffered, n => n.Type == "confirm_all");
        Assert.True(issue.Grouped);
    }

    [Fact]
    public void Product_rows_take_values_from_cells_only_and_name_missing_label_sources()
    {
        var document = TolerantCsvReader.Parse("Marque;Modèle;Coloris;Calibre;Pont;Branche;Famille\nRay;A1;Noir;52;18;140;Monture\nRay;A2;;50;20;145;Monture\n", "utf-8");
        var layer = AssistantUserLayer.From([CsvAssistantCoreTests.Decision(2, DecisionTypes.FileDefault, """{"field":"stockPolicy"}""", """{"value":"stocked"}""")]);
        var d = CatalogInterpretationValidator.Validate(document, null, layer);
        var rows = CatalogMaterializer.ProductRows(d, layer, "EUR");
        var first = rows.Single(r => r.LineNo == 2);
        Assert.Equal(Families.OpticalFrames, first.Values["familyCode"]);
        Assert.Equal("Noir", first.Values["variantLabel"]);
        Assert.Equal("52", first.Values["attr:eyeSize"]);
        Assert.Equal("stocked", first.Values["stockPolicy"]);
        Assert.Empty(first.PreErrors);
        var second = rows.Single(r => r.LineNo == 3);
        Assert.Contains(second.PreErrors, e => e.Code == "variant_label_source_missing");
    }

    [Fact]
    public void Recognised_but_unwritten_fields_are_named_with_a_reason()
    {
        Assert.Contains("TTC", CatalogMaterializer.NotWrittenReason("salePriceTtc", AssistantTargets.Products), StringComparison.Ordinal);
        Assert.Contains("mouvement", CatalogMaterializer.NotWrittenReason("quantity", AssistantTargets.Products), StringComparison.Ordinal);
        Assert.Contains("verres", CatalogMaterializer.NotWrittenReason("sphereMin", AssistantTargets.Products), StringComparison.Ordinal);
        Assert.Null(CatalogMaterializer.NotWrittenReason("sphereMin", AssistantTargets.Lenses));
        Assert.Null(CatalogMaterializer.NotWrittenReason("brand", AssistantTargets.Products));
    }

    [Fact]
    public void Inspection_suggests_lenses_only_on_lens_evidence()
    {
        var lens = AssistedImportService.SuggestTarget(TolerantCsvReader.Parse("Gamme;Indice;Sph min;Sph max\nX;1,6;-6;4\n", "utf-8"));
        Assert.Equal(AssistantTargets.Lenses, lens.Target);
        Assert.Equal(4, lens.Evidence.Count);
        Assert.Equal(AssistantTargets.Products, AssistedImportService.SuggestTarget(TolerantCsvReader.Parse("Marque;Modèle\nRay;A\n", "utf-8")).Target);
        var refused = AssistedImportService.Inspect(Convert.ToBase64String([0x4D, 0x5A, 0x00, 0x01]));
        Assert.Equal("binary_content", refused.Code);
        Assert.Equal("invalid_base64", AssistedImportService.Inspect("%%%").Code);
        var preview = AssistedImportService.Inspect(Convert.ToBase64String(Encoding.UTF8.GetBytes("a;b\n" + new string('x', 400) + ";y\n"))).Value!;
        Assert.Equal(160, preview.Preview[1][0].Length);
    }

    [Theory]
    [InlineData("Unifocal", LensTypes.SingleVision)]
    [InlineData("Progressif", LensTypes.Progressive)]
    [InlineData("progressive lens", LensTypes.Progressive)]
    [InlineData("Dégressif", LensTypes.Degressive)]
    [InlineData("Bifocal", LensTypes.Bifocal)]
    [InlineData("Teinté", null)]
    public void Lens_type_is_mapped_only_from_explicit_words(string text, string? expected) => Assert.Equal(expected, CatalogLexicon.LensTypeOf(text));

    [Fact]
    public void Revoked_decisions_and_restored_rows_rebuild_the_previous_layer()
    {
        var layer = AssistantUserLayer.From(
        [
            CsvAssistantCoreTests.Decision(2, DecisionTypes.RowSkip, """{"line":5}""", "{}"),
            CsvAssistantCoreTests.Decision(3, DecisionTypes.RowSkip, """{"line":6}""", "{}"),
            CsvAssistantCoreTests.Decision(4, DecisionTypes.RowRestore, """{"line":5}""", "{}"),
            CsvAssistantCoreTests.Decision(5, DecisionTypes.Revoke, """{"revision":3}""", "{}"),
            CsvAssistantCoreTests.Decision(6, DecisionTypes.CreateReference, """{"kind":"range","name":"Gamme X","manufacturer":"Verrier"}""", """{"create":true}"""),
        ]);
        Assert.Empty(layer.SkippedLines);
        Assert.True(layer.CreateReferences.ContainsKey(AssistantUserLayer.ReferenceKey("range", "gamme x", "VERRIER")));
    }
}
