using System.Text.Json;
using MMV.Api.Tests.Clients;

namespace MMV.Api.Tests.Audit;

/// <summary>
/// Contrat OpenAPI de l'historique (phase 03-D2) : document réellement généré par MMV.Api identique à l'instantané
/// versionné <c>apps/desktop/src/api/contract/audit.openapi.json</c>, lu aussi par les tests TypeScript. Mise à jour
/// volontaire : <c>MMV_UPDATE_OPENAPI_SNAPSHOT=1</c>, puis relecture du diff.
/// </summary>
public sealed class AuditOpenApiContractTests
{
    private const string SnapshotRelativePath = "apps/desktop/src/api/contract/audit.openapi.json";

    [Fact]
    public async Task GeneratedAuditContract_MatchesTheVersionedSnapshot()
    {
        var actual = await ClientsOpenApiContractTests.GenerateContractAsync("/api/audit");
        var path = Path.Combine(ClientsOpenApiContractTests.FindRepositoryRoot(), SnapshotRelativePath);

        if (Environment.GetEnvironmentVariable("MMV_UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            await File.WriteAllTextAsync(path, actual);
        }

        Assert.True(File.Exists(path), $"Instantané absent : {SnapshotRelativePath} (MMV_UPDATE_OPENAPI_SNAPSHOT=1 pour le créer).");
        Assert.Equal((await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public async Task AuditContract_IsReadOnly_WithTypedResponses()
    {
        using var document = JsonDocument.Parse(await ClientsOpenApiContractTests.GenerateContractAsync("/api/audit"));
        var paths = document.RootElement.GetProperty("paths");

        var path = Assert.Single(paths.EnumerateObject());
        Assert.Equal("/api/audit/clients/{clientId}", path.Name);
        Assert.Equal(["get"], path.Value.EnumerateObject().Select(m => m.Name));
        Assert.Equal(["200", "400", "401", "403"],
            path.Value.GetProperty("get").GetProperty("responses").EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "AuditEventPageResponse", "AuditEventResponse", "AuditChangeResponse", "ErrorResponse" })
        {
            Assert.True(schemas.TryGetProperty(name, out _), name);
        }
    }
}
