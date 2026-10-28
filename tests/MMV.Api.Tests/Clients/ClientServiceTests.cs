using MMV.Api.Tests.Identity;
using MMV.Application.Clients;
using MMV.Application.Audit;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Clients;

/// <summary>Cas d'usage Clients : création, modification versionnée, archivage, réactivation (D-03-05, D-03-06).</summary>
public sealed class ClientServiceTests
{
    private static readonly Guid Author = Guid.CreateVersion7();

    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 17, 9, 30, 0, TimeSpan.Zero).AddTicks(1234567));
    private readonly InMemoryClientStore _store;
    private readonly ClientService _service;

    public ClientServiceTests()
    {
        // 03-A3 : le store sème sur l'horloge du test (auparavant l'heure réelle : échec après 09:35 Z le 17/10/2026).
        _store = new InMemoryClientStore(_clock);
        _service = new ClientService(_store, _clock, _store.Audit);
    }

    private static ClientInput Input(string firstName = "Jeanne", string lastName = "Martin", string? city = null) =>
        new(firstName, lastName, null, null, null, null, city, null, null);

    [Fact]
    public async Task Create_AssignsIdAuthorTimestampsAndReference()
    {
        var result = await _service.CreateAsync(Input(), Author, CancellationToken.None);

        Assert.Equal(ClientWriteStatus.Success, result.Status);
        var client = result.Client!;
        Assert.Equal(7, client.Id.Version);
        Assert.Equal("CLI-000001", client.Reference);
        Assert.Equal(Author, client.CreatedByUserId);
        Assert.Equal(Author, client.UpdatedByUserId);
        // Précision PostgreSQL : microseconde.
        Assert.Equal(0, client.CreatedAtUtc.Ticks % 10);
        Assert.Equal(client.CreatedAtUtc, client.UpdatedAtUtc);
        Assert.False(client.IsArchived);
    }

    [Fact]
    public async Task Create_Invalid_WritesNothing()
    {
        var result = await _service.CreateAsync(Input(firstName: " "), Author, CancellationToken.None);

        Assert.Equal(ClientWriteStatus.ValidationFailed, result.Status);
        Assert.Equal(["required"], result.Errors!["firstName"]);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Update_WithCurrentVersion_ChangesDataAndVersion()
    {
        var client = _store.Seed("Jeanne", "Martin");
        var version = client.Version;
        var editor = Guid.CreateVersion7();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await _service.UpdateAsync(client.Id, Input(city: "Namur"), version, editor, CancellationToken.None);

        Assert.Equal(ClientWriteStatus.Success, result.Status);
        Assert.Equal("Namur", result.Client!.City);
        Assert.NotEqual(version, result.Client.Version);
        Assert.Equal(editor, result.Client.UpdatedByUserId);
        Assert.True(result.Client.UpdatedAtUtc > result.Client.CreatedAtUtc);
    }

    [Fact]
    public async Task Update_WithStaleVersion_IsAConflictAndWritesNothing()
    {
        var client = _store.Seed("Jeanne", "Martin");
        var saves = _store.SaveCount;

        var result = await _service.UpdateAsync(client.Id, Input(city: "Namur"), client.Version - 1, Author, CancellationToken.None);

        Assert.Equal(ClientWriteStatus.VersionConflict, result.Status);
        Assert.Null(client.City);
        Assert.Equal(saves, _store.SaveCount);
    }

    [Fact]
    public async Task Update_ConcurrentWriteDetectedAtSave_IsAConflict()
    {
        var client = _store.Seed("Jeanne", "Martin");
        _store.FailNextSave = true;

        var result = await _service.UpdateAsync(client.Id, Input(city: "Namur"), client.Version, Author, CancellationToken.None);

        Assert.Equal(ClientWriteStatus.VersionConflict, result.Status);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task Update_ArchivedClient_IsRefused()
    {
        var client = _store.Seed("Jeanne", "Martin", archived: true);

        var result = await _service.UpdateAsync(client.Id, Input(city: "Namur"), client.Version, Author, CancellationToken.None);

        Assert.Equal(ClientWriteStatus.ClientArchived, result.Status);
        Assert.Null(client.City);
    }

    [Fact]
    public async Task UnknownClient_IsNotFound()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal(ClientWriteStatus.NotFound, (await _service.UpdateAsync(id, Input(), 1, Author, CancellationToken.None)).Status);
        Assert.Equal(ClientWriteStatus.NotFound, (await _service.ArchiveAsync(id, 1, Author, CancellationToken.None)).Status);
        Assert.Equal(ClientWriteStatus.NotFound, (await _service.ReactivateAsync(id, 1, Author, CancellationToken.None)).Status);
        Assert.Null(await _service.GetAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task ArchiveThenReactivate_AreVersionedAndIdempotencyIsExplicit()
    {
        var client = _store.Seed("Jeanne", "Martin");

        Assert.Equal(ClientWriteStatus.VersionConflict, (await _service.ArchiveAsync(client.Id, client.Version + 1, Author, CancellationToken.None)).Status);
        Assert.Equal(ClientWriteStatus.AlreadyActive, (await _service.ReactivateAsync(client.Id, client.Version, Author, CancellationToken.None)).Status);

        var archived = await _service.ArchiveAsync(client.Id, client.Version, Author, CancellationToken.None);
        Assert.Equal(ClientWriteStatus.Success, archived.Status);
        Assert.NotNull(archived.Client!.ArchivedAtUtc);
        Assert.Equal(ClientWriteStatus.AlreadyArchived, (await _service.ArchiveAsync(client.Id, client.Version, Author, CancellationToken.None)).Status);

        var reactivated = await _service.ReactivateAsync(client.Id, client.Version, Author, CancellationToken.None);
        Assert.Equal(ClientWriteStatus.Success, reactivated.Status);
        Assert.Null(reactivated.Client!.ArchivedAtUtc);
        // O-04-53 : chaque archivage / réactivation verrouille d'abord le client (attente d'une écriture optique en cours).
        Assert.Equal(5, _store.LifecycleLocks);
        // Jamais de suppression physique.
        Assert.Single(_store.Clients);
    }

    [Fact]
    public async Task List_DefaultsToActiveClients_SearchIsAccentAndCaseInsensitive()
    {
        _store.Seed("Éloïse", "Lefèvre", phone: "0470 12 34 56");
        _store.Seed("Jeanne", "Martin", email: "jeanne@exemple.be");
        _store.Seed("Paul", "Lefevre", archived: true);

        var active = await _service.ListAsync(new ClientListQuery(null, ClientStatusFilter.Active, ClientSortField.Name, false, 1, 20), CancellationToken.None);
        Assert.Equal(["Lefèvre", "Martin"], active.Items.Select(c => c.LastName));

        var byName = await _service.ListAsync(new ClientListQuery("LEFEVRE", ClientStatusFilter.All, ClientSortField.Name, false, 1, 20), CancellationToken.None);
        Assert.Equal(["Paul", "Éloïse"], byName.Items.Select(c => c.FirstName).Order(StringComparer.Ordinal));

        var byPhone = await _service.ListAsync(new ClientListQuery("12 34", ClientStatusFilter.Active, ClientSortField.Name, false, 1, 20), CancellationToken.None);
        Assert.Equal("Éloïse", Assert.Single(byPhone.Items).FirstName);
    }
}

/// <summary>Attribution des droits métier par la commande de maintenance (D-03-09).</summary>
public sealed class PermissionAdministrationServiceTests
{
    private readonly InMemoryIdentityStore _store = new();
    private readonly PermissionAdministrationService _service;

    public PermissionAdministrationServiceTests()
    {
        _service = new PermissionAdministrationService(_store);
    }

    [Fact]
    public async Task GrantAndRevoke_ClientsPermissions()
    {
        var user = _store.AddUser(RealHashing.Create(), "caisse", "Mot-de-passe-caisse-1");

        var granted = await _service.GrantAsync("CAISSE", Permissions.ClientsRead, CancellationToken.None);
        Assert.Equal(PermissionChangeStatus.Granted, granted.Status);
        Assert.Equal(user.Id, granted.UserId);
        Assert.True(user.HasPermission(Permissions.ClientsRead));
        Assert.Equal(PermissionChangeStatus.Unchanged, (await _service.GrantAsync("caisse", Permissions.ClientsRead, CancellationToken.None)).Status);

        var revoked = await _service.RevokeAsync("caisse", Permissions.ClientsRead, CancellationToken.None);
        Assert.Equal(PermissionChangeStatus.Revoked, revoked.Status);
        Assert.False(user.HasPermission(Permissions.ClientsRead));
        Assert.Equal(2, _store.SaveCount);
    }

    [Theory]
    [InlineData("system.admin")]
    [InlineData("clients.delete")]
    [InlineData("Clients.Read")]
    [InlineData("")]
    public async Task SystemAdminAndUnknownPermissions_AreNotGrantable(string permission)
    {
        var admin = _store.AddUser(RealHashing.Create(), "admin", "Mot-de-passe-admin-1", Permissions.SystemAdmin);

        Assert.Equal(PermissionChangeStatus.NotGrantable, (await _service.GrantAsync("admin", permission, CancellationToken.None)).Status);
        Assert.Equal(PermissionChangeStatus.NotGrantable, (await _service.RevokeAsync("admin", permission, CancellationToken.None)).Status);
        Assert.True(admin.HasPermission(Permissions.SystemAdmin));
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task UnknownAccount_ChangesNothing()
    {
        Assert.Equal(PermissionChangeStatus.UnknownAccount, (await _service.GrantAsync("inconnu", Permissions.ClientsWrite, CancellationToken.None)).Status);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void SystemAdmin_ImpliesNoClientsPermission()
    {
        // 17/10/2026 (03-B2, D-03-17) : + appointments.read|write|cancel ; 18/10/2026 (03-D2) : + audit.read ;
        // 27/10/2026 (04-B) : + 4 droits optiques ; system.admin reste non attribuable.
        Assert.Equal(
            [Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsArchive,
             Permissions.AppointmentsRead, Permissions.AppointmentsWrite, Permissions.AppointmentsCancel, Permissions.AuditRead,
             Permissions.OpticalRead, Permissions.OpticalPrescriptionsWrite, Permissions.OpticalMeasurementsWrite, Permissions.OpticalInvalidate],
            Permissions.Grantable);
        Assert.DoesNotContain(Permissions.SystemAdmin, Permissions.Grantable);
    }
}
