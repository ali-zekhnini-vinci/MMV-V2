using System.Text.Json;
using MMV.Api.Tests.Clients;

namespace MMV.Api.Tests.Optical;

/// <summary>
/// Contrat OpenAPI du dossier optique (phase 04-B) : document réellement généré par MMV.Api identique à l'instantané
/// versionné <c>apps/desktop/src/api/contract/optical.openapi.json</c>, lu aussi par les tests TypeScript. Mise à jour
/// volontaire : <c>MMV_UPDATE_OPENAPI_SNAPSHOT=1</c>, puis relecture du diff.
/// </summary>
public sealed class OpticalOpenApiContractTests
{
    private const string SnapshotRelativePath = "apps/desktop/src/api/contract/optical.openapi.json";

    [Fact]
    public async Task GeneratedOpticalContract_MatchesTheVersionedSnapshot()
    {
        var actual = await ClientsOpenApiContractTests.GenerateContractAsync("/api/optical");
        var path = Path.Combine(ClientsOpenApiContractTests.FindRepositoryRoot(), SnapshotRelativePath);

        if (Environment.GetEnvironmentVariable("MMV_UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            await File.WriteAllTextAsync(path, actual);
        }

        Assert.True(File.Exists(path), $"Instantané absent : {SnapshotRelativePath} (MMV_UPDATE_OPENAPI_SNAPSHOT=1 pour le créer).");
        Assert.Equal((await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public async Task OpticalContract_HasTwoDistinctWriteFamilies_AndNoDelete()
    {
        using var document = JsonDocument.Parse(await ClientsOpenApiContractTests.GenerateContractAsync("/api/optical"));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().ToList();

        Assert.DoesNotContain(paths, p => p.Value.TryGetProperty("delete", out _));
        Assert.DoesNotContain(paths, p => p.Name.Contains("document", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("convert", StringComparison.OrdinalIgnoreCase));
        foreach (var family in new[] { "prescriptions", "measurements" })
        {
            Assert.Equal(
                [
                    $"/api/optical/{family}",
                    $"/api/optical/{family}/{{id}}",
                    $"/api/optical/{family}/{{id}}/draft",
                    $"/api/optical/{family}/{{id}}/draft/confirm",
                    $"/api/optical/{family}/{{id}}/draft/discard",
                    $"/api/optical/{family}/{{id}}/invalidate",
                    $"/api/optical/{family}/{{id}}/rectifications",
                ],
                paths.Select(p => p.Name).Where(n => n.StartsWith($"/api/optical/{family}", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        }

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "OpticalSummaryPageResponse", "OpticalSummaryResponse", "EyeValuesResponse", "ErrorResponse", "ValidationErrorResponse" })
        {
            Assert.True(schemas.TryGetProperty(name, out _), name);
        }
    }
}
