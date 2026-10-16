using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MMV.Api.Tests.Clients;

/// <summary>
/// Contrat OpenAPI des routes Clients (O-03-01) : le document réellement généré par MMV.Api (/openapi/v1.json,
/// Development) doit être identique à l'instantané versionné <c>apps/desktop/src/api/contract/clients.openapi.json</c>,
/// lu aussi par les tests TypeScript (<c>apps/desktop/src/api/clientsContract.test.ts</c>) pour vérifier les types
/// et parseurs de l'interface. Mise à jour volontaire : variable <c>MMV_UPDATE_OPENAPI_SNAPSHOT=1</c>, puis relecture du diff.
/// </summary>
public sealed class ClientsOpenApiContractTests
{
    private const string SnapshotRelativePath = "apps/desktop/src/api/contract/clients.openapi.json";

    private static readonly JsonSerializerOptions SnapshotFormat = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public async Task GeneratedClientsContract_MatchesTheVersionedSnapshot()
    {
        var actual = await GenerateClientsContractAsync();
        var path = Path.Combine(FindRepositoryRoot(), SnapshotRelativePath);

        if (Environment.GetEnvironmentVariable("MMV_UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
        }

        Assert.True(File.Exists(path), $"Instantané absent : {SnapshotRelativePath} (MMV_UPDATE_OPENAPI_SNAPSHOT=1 pour le créer).");
        var expected = (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ClientsContract_DeclaresTypedResponsesAndNoDelete()
    {
        using var document = JsonDocument.Parse(await GenerateClientsContractAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(
            ["/api/clients", "/api/clients/{id}", "/api/clients/{id}/archive", "/api/clients/{id}/reactivate"],
            paths.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["get", "post"], Methods(paths.GetProperty("/api/clients")));
        Assert.Equal(["get", "put"], Methods(paths.GetProperty("/api/clients/{id}")));

        string[] Statuses(string path, string method) =>
            [.. paths.GetProperty(path).GetProperty(method).GetProperty("responses").EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["200", "400", "401", "403"], Statuses("/api/clients", "get"));
        Assert.Equal(["201", "400", "401", "403", "422"], Statuses("/api/clients", "post"));
        Assert.Equal(["200", "400", "401", "403", "404", "409", "422"], Statuses("/api/clients/{id}", "put"));
        Assert.Equal(["200", "400", "401", "403", "404", "409"], Statuses("/api/clients/{id}/archive", "post"));

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "ClientSummaryResponse", "ClientDetailResponse", "ClientPageResponse", "ClientInputRequest", "ClientUpdateRequest", "ClientVersionRequest", "ErrorResponse", "ValidationErrorResponse" })
        {
            Assert.True(schemas.TryGetProperty(name, out _), $"Schéma absent : {name}");
        }
    }

    private static string[] Methods(JsonElement pathItem) =>
        [.. pathItem.EnumerateObject().Select(p => p.Name).Where(n => n is "get" or "post" or "put" or "patch" or "delete").Order(StringComparer.Ordinal)];

    /// <summary>Extrait du document : routes /api/clients et schémas qu'elles référencent (transitivement).</summary>
    private static async Task<string> GenerateClientsContractAsync()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mmv"] = null })));
        using var client = factory.CreateClient();
        var root = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!.AsObject();

        var paths = new JsonObject();
        foreach (var (name, item) in root["paths"]!.AsObject())
        {
            if (name.StartsWith("/api/clients", StringComparison.Ordinal))
            {
                paths[name] = item!.DeepClone();
            }
        }

        var allSchemas = root["components"]?["schemas"]?.AsObject() ?? [];
        var referenced = new SortedSet<string>(StringComparer.Ordinal);
        var queue = new Queue<JsonNode>([paths]);
        while (queue.TryDequeue(out var node))
        {
            foreach (var reference in References(node))
            {
                if (referenced.Add(reference) && allSchemas[reference] is { } schema)
                {
                    queue.Enqueue(schema);
                }
            }
        }

        var schemas = new JsonObject();
        foreach (var name in referenced)
        {
            schemas[name] = allSchemas[name]?.DeepClone();
        }

        var contract = new JsonObject
        {
            ["openapi"] = root["openapi"]!.DeepClone(),
            ["paths"] = paths,
            ["components"] = new JsonObject { ["schemas"] = schemas },
        };
        return contract.ToJsonString(SnapshotFormat).ReplaceLineEndings("\n") + "\n";
    }

    private static IEnumerable<string> References(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value?.GetValue<string>() is { } target && target.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                    {
                        yield return target["#/components/schemas/".Length..];
                    }
                    else if (value is not null)
                    {
                        foreach (var nested in References(value))
                        {
                            yield return nested;
                        }
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array.OfType<JsonNode>())
                {
                    foreach (var nested in References(item))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MMV.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Racine du dépôt (MMV.slnx) introuvable.");
    }
}
