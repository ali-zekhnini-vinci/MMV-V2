using System.Text.Json;
using MMV.Api.Tests.Clients;

namespace MMV.Api.Tests.Appointments;

/// <summary>
/// Contrat OpenAPI des routes Rendez-vous (phase 03-B2) : le document réellement généré par MMV.Api doit être
/// identique à l'instantané versionné <c>apps/desktop/src/api/contract/appointments.openapi.json</c>, lu aussi par
/// les tests TypeScript (<c>apps/desktop/src/api/appointmentsContract.test.ts</c>). Mise à jour volontaire :
/// <c>MMV_UPDATE_OPENAPI_SNAPSHOT=1</c>, puis relecture du diff.
/// </summary>
public sealed class AppointmentsOpenApiContractTests
{
    private const string SnapshotRelativePath = "apps/desktop/src/api/contract/appointments.openapi.json";

    [Fact]
    public async Task GeneratedAppointmentsContract_MatchesTheVersionedSnapshot()
    {
        var actual = await ClientsOpenApiContractTests.GenerateContractAsync("/api/appointments");
        var path = Path.Combine(ClientsOpenApiContractTests.FindRepositoryRoot(), SnapshotRelativePath);

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
    public async Task AppointmentsContract_DeclaresTypedResponsesAndNoDelete()
    {
        using var document = JsonDocument.Parse(await ClientsOpenApiContractTests.GenerateContractAsync("/api/appointments"));
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(
            ["/api/appointments", "/api/appointments/options", "/api/appointments/{id}", "/api/appointments/{id}/cancel", "/api/appointments/{id}/restore"],
            paths.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["get", "post"], Methods(paths.GetProperty("/api/appointments")));
        Assert.Equal(["get", "put"], Methods(paths.GetProperty("/api/appointments/{id}")));

        string[] Statuses(string path, string method) =>
            [.. paths.GetProperty(path).GetProperty(method).GetProperty("responses").EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["200", "401", "403", "503"], Statuses("/api/appointments/options", "get"));
        Assert.Equal(["200", "400", "401", "403", "503"], Statuses("/api/appointments", "get"));
        Assert.Equal(["201", "400", "401", "403", "409", "422", "503"], Statuses("/api/appointments", "post"));
        Assert.Equal(["200", "400", "401", "403", "404", "409", "422", "503"], Statuses("/api/appointments/{id}", "put"));
        Assert.Equal(["200", "400", "401", "403", "404", "409", "422", "503"], Statuses("/api/appointments/{id}/restore", "post"));

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[]
                 {
                     "AppointmentOptionsResponse", "AppointmentReasonResponse", "AppointmentResponsibleResponse", "AppointmentSummaryResponse",
                     "AppointmentDetailResponse", "AppointmentListResponse", "AppointmentInputRequest", "AppointmentUpdateRequest",
                     "AppointmentCancelRequest", "AppointmentVersionRequest", "ErrorResponse", "ValidationErrorResponse", "TimeZoneOffsetResponse",
                 })
        {
            Assert.True(schemas.TryGetProperty(name, out _), $"Schéma absent : {name}");
        }
    }

    private static string[] Methods(JsonElement pathItem) =>
        [.. pathItem.EnumerateObject().Select(p => p.Name).Where(n => n is "get" or "post" or "put" or "patch" or "delete").Order(StringComparer.Ordinal)];
}
