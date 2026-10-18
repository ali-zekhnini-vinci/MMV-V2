using MMV.Api.Tests.Identity;
using MMV.Application.Appointments;
using MMV.Domain.Appointments;

namespace MMV.Api.Tests.Appointments;

/// <summary>
/// Cas d'usage Rendez-vous (D-03-13 à D-03-16) sur le double transactionnel <see cref="InMemoryAppointmentStore"/> :
/// deux modes d'attribution, chevauchements, créneaux consécutifs, capacité d'accueil instant par instant,
/// réservations simultanées, annulation et rétablissement, client archivé, collaborateur désactivé, passé.
/// Magasin fictif : Europe/Brussels (UTC+2 en juillet), capacité d'accueil 1 sauf mention contraire.
/// </summary>
public sealed class AppointmentServiceTests
{
    private static readonly Guid Author = Guid.CreateVersion7();

    // Mercredi 15/07/2026 10:00 à Bruxelles ; les créneaux testés sont le jeudi 16/07.
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryAppointmentStore _store = new();
    private readonly Guid _client;
    private readonly Guid _otherClient;
    private readonly Guid _alice;
    private readonly Guid _bruno;

    public AppointmentServiceTests()
    {
        _client = _store.AddClient("Jeanne", "Martin");
        _otherClient = _store.AddClient("Omar", "Benali");
        _alice = _store.AddUser("Alice Opticienne");
        _bruno = _store.AddUser("Bruno Opticien");
    }

    private AppointmentService Service(int? capacity = 1, string timeZone = "Europe/Brussels") =>
        new(_store, AppointmentTestSettings.Configured(timeZone, capacity), _clock, _store.Audit);

    /// <summary>Jeudi 16/07/2026 à <paramref name="hour"/>:<paramref name="minute"/> heure de Bruxelles (UTC+2).</summary>
    private static string At(int hour, int minute = 0) => new DateTimeOffset(2026, 7, 16, hour - 2, minute, 0, TimeSpan.Zero).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset Instant(int hour, int minute = 0) => new(2026, 7, 16, hour - 2, minute, 0, TimeSpan.Zero);

    private AppointmentInput Personal(Guid? responsible, string start, int minutes = 30, Guid? client = null, string reason = "conseil_devis", string? notes = null, string zone = "Europe/Brussels") =>
        new(client ?? _client, responsible, start, Local(start, zone), minutes, reason, notes);

    private AppointmentInput Reception(string start, int minutes = 30, Guid? client = null, string reason = "retrait_commande") =>
        new(client ?? _client, null, start, Local(start), minutes, reason, null);

    /// <summary>Heure murale annoncée par un poste dont les données de fuseaux concordent avec celles du serveur.</summary>
    private static string Local(string utc, string zone = "Europe/Brussels") =>
        AppointmentSchedule.LocalWallTime(Zones.GetOrAdd(zone, TimeZoneInfo.FindSystemTimeZoneById), DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TimeZoneInfo> Zones = new(StringComparer.Ordinal);

    private static void AssertField(AppointmentWriteResult result, string field, string code)
    {
        Assert.Equal(AppointmentWriteStatus.ValidationFailed, result.Status);
        Assert.Equal([code], result.Errors![field]);
    }

    [Fact]
    public async Task Create_WithResponsible_StoresUtcSlotAuthorAndClientView()
    {
        var result = await Service().CreateAsync(Personal(_alice, At(10, 30), 45, notes: "  Note opérationnelle  "), Author, CancellationToken.None);

        Assert.Equal(AppointmentWriteStatus.Success, result.Status);
        var view = result.Appointment!;
        Assert.Equal(Instant(10, 30), view.Appointment.StartsAtUtc);
        Assert.Equal(Instant(11, 15), view.Appointment.EndsAtUtc);
        Assert.Equal(_alice, view.Appointment.ResponsibleUserId);
        Assert.Equal("Alice Opticienne", view.ResponsibleDisplayName);
        Assert.Equal("Note opérationnelle", view.Appointment.Notes);
        Assert.Equal(Author, view.Appointment.CreatedByUserId);
        Assert.Equal(Appointment.StatusPlanned, view.Appointment.Status);
        Assert.Equal(("CLI-000001", "Jeanne", "Martin", false), (view.ClientReference, view.ClientFirstName, view.ClientLastName, view.ClientArchived));
    }

    [Theory]
    [InlineData("conseil_devis")]
    [InlineData("choix_equipement")]
    [InlineData("prise_mesures")]
    [InlineData("essayage_ajustement")]
    [InlineData("autre")]
    public async Task PersonalReason_RequiresAResponsibleEvenWithReceptionCapacity(string reason)
    {
        var result = await Service(capacity: 5).CreateAsync(new AppointmentInput(_client, null, At(10), Local(At(10)), 30, reason, null), Author, CancellationToken.None);

        AssertField(result, "responsibleUserId", "required");
        Assert.Empty(_store.Appointments);
    }

    [Theory]
    [InlineData("retrait_commande")]
    [InlineData("sav_reparation")]
    public async Task ReceptionReason_WithoutResponsible_UsesTheReceptionCapacity(string reason)
    {
        var result = await Service().CreateAsync(Reception(At(10), reason: reason), Author, CancellationToken.None);

        Assert.Equal(AppointmentWriteStatus.Success, result.Status);
        Assert.Null(result.Appointment!.Appointment.ResponsibleUserId);
        Assert.Null(result.Appointment.ResponsibleDisplayName);
        Assert.Contains(AppointmentLock.Reception.Key, _store.LockLog);
    }

    [Fact]
    public async Task ReceptionReason_WithResponsible_IsCheckedOnTheCollaboratorAgendaOnly()
    {
        _store.Seed(_client, null, Instant(10), 30, "retrait_commande");

        var result = await Service().CreateAsync(new AppointmentInput(_otherClient, _alice, At(10), Local(At(10)), 30, "sav_reparation", null), Author, CancellationToken.None);

        Assert.Equal(AppointmentWriteStatus.Success, result.Status);
    }

    [Fact]
    public async Task WithoutConfiguredCapacity_UnassignedBookingsAreRefused()
    {
        var result = await Service(capacity: null).CreateAsync(Reception(At(10)), Author, CancellationToken.None);

        AssertField(result, "responsibleUserId", "reception_not_configured");
        Assert.Empty(_store.Appointments);
    }

    [Fact]
    public async Task ReceptionCapacity_Saturated_RefusesAndConsecutiveSlotsAreFree()
    {
        var service = Service(capacity: 1);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Reception(At(10)), Author, CancellationToken.None)).Status);

        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull, (await service.CreateAsync(Reception(At(10, 15), client: _otherClient), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Reception(At(10, 30), client: _otherClient), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Reception(At(9, 30), client: _otherClient), Author, CancellationToken.None)).Status);
        Assert.Equal(3, _store.Appointments.Count);
    }

    [Fact]
    public async Task ReceptionCapacity_IsComputedAtEachInstantNotByCountingOverlaps()
    {
        // Capacité 2 ; 10:00–10:30 et 10:30–11:00 déjà réservés : ils recoupent 10:00–11:00 mais ne sont jamais
        // simultanés, donc l'occupation maximale est 1 et une 2e place reste libre sur toute l'heure.
        var service = Service(capacity: 2);
        _store.Seed(_client, null, Instant(10), 30, "retrait_commande");
        _store.Seed(_client, null, Instant(10, 30), 30, "retrait_commande");

        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Reception(At(10), 60, client: _otherClient), Author, CancellationToken.None)).Status);
        // Désormais 2 simultanés en tout instant de 10:00 à 11:00 : plus aucune place.
        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull, (await service.CreateAsync(Reception(At(10, 45), 15, client: _otherClient), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Reception(At(11), 15, client: _otherClient), Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Overlap_SameCollaborator_IsAConflictButConsecutiveAndOtherCollaboratorsAreNot()
    {
        var service = Service();
        _store.Seed(_otherClient, _alice, Instant(10), 30);

        Assert.Equal(AppointmentWriteStatus.SlotConflict, (await service.CreateAsync(Personal(_alice, At(10, 15)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.SlotConflict, (await service.CreateAsync(Personal(_alice, At(9, 45), 60), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Personal(_alice, At(10, 30)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Personal(_alice, At(9, 30)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Personal(_bruno, At(10, 15)), Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task CancelledAppointments_DoNotBlockTheSlot()
    {
        _store.Seed(_otherClient, _alice, Instant(10), 30, cancelled: true);
        _store.Seed(_otherClient, null, Instant(10), 30, "retrait_commande", cancelled: true);

        Assert.Equal(AppointmentWriteStatus.Success, (await Service().CreateAsync(Personal(_alice, At(10)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await Service().CreateAsync(Reception(At(10)), Author, CancellationToken.None)).Status);
    }

    [Theory]
    [InlineData(25, "durationMinutes", "not_allowed")]
    [InlineData(180, "durationMinutes", "not_allowed")]
    public async Task Duration_OutsideTheApprovedListIsRefused(int minutes, string field, string code) =>
        AssertField(await Service().CreateAsync(Personal(_alice, At(10), minutes), Author, CancellationToken.None), field, code);

    [Fact]
    public async Task Input_IsValidatedPerFieldWithoutDefaults()
    {
        var result = await Service().CreateAsync(new AppointmentInput(null, _alice, null, null, null, null, new string('x', 1001)), Author, CancellationToken.None);

        Assert.Equal(AppointmentWriteStatus.ValidationFailed, result.Status);
        Assert.Equal(["required"], result.Errors!["clientId"]);
        Assert.Equal(["required"], result.Errors["startsAtUtc"]);
        Assert.Equal(["required"], result.Errors["startsAtLocal"]);
        Assert.Equal(["required"], result.Errors["durationMinutes"]);
        Assert.Equal(["required"], result.Errors["reasonCode"]);
        Assert.Equal(["too_long"], result.Errors["notes"]);

        AssertField(await Service().CreateAsync(Personal(_alice, At(10), reason: "controle_vue"), Author, CancellationToken.None), "reasonCode", "not_allowed");
        AssertField(await Service().CreateAsync(Personal(_alice, At(10, 7)), Author, CancellationToken.None), "startsAtUtc", "invalid_step");
        AssertField(await Service().CreateAsync(Personal(_alice, "2026-07-16T10:00:00+02:00"), Author, CancellationToken.None), "startsAtUtc", "invalid_format");
        AssertField(await Service().CreateAsync(Personal(_alice, At(23, 30), 60), Author, CancellationToken.None), "startsAtUtc", "out_of_range");
    }

    [Fact]
    public async Task Past_CreationIsRefused()
    {
        _clock.Now = Instant(10, 2);

        AssertField(await Service().CreateAsync(Personal(_alice, At(10)), Author, CancellationToken.None), "startsAtUtc", "in_past");
        Assert.Equal(AppointmentWriteStatus.Success, (await Service().CreateAsync(Personal(_alice, At(10, 5)), Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Brussels_AmbiguousAutumnHour_IsRefusedAtCreation()
    {
        _clock.Now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        // 25/10/2026 02:15 CEST (00:15 Z) : heure murale ambiguë (02:00–02:59 existe deux fois).
        var result = await Service().CreateAsync(Personal(_alice, "2026-10-25T00:15:00Z"), Author, CancellationToken.None);

        AssertField(result, "startsAtUtc", "time_change");
    }

    [Fact]
    public async Task Casablanca_SlotOverTheMissingHour_IsRefusedAtCreation()
    {
        _clock.Now = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        // 22/03/2026 01:30 (UTC+0) pendant 60 min : enjambe 02:00 → 03:00.
        var result = await Service(timeZone: "Africa/Casablanca").CreateAsync(Personal(_alice, "2026-03-22T01:30:00Z", 60, zone: "Africa/Casablanca"), Author, CancellationToken.None);

        AssertField(result, "startsAtUtc", "time_change");
    }

    [Fact]
    public async Task ArchivedClient_NoNewAppointmentButExistingOnesStayCancellable()
    {
        var existing = _store.Seed(_client, _alice, Instant(10), 30);
        _store.ArchiveClient(_client);
        var service = Service();

        Assert.Equal(AppointmentWriteStatus.ClientArchived, (await service.CreateAsync(Personal(_bruno, At(14)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.ClientArchived,
            (await service.UpdateAsync(existing.Id, Personal(_alice, At(11)), existing.Version, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.ClientArchived,
            (await service.UpdateAsync(existing.Id, Personal(_alice, At(10), client: _otherClient), existing.Version, Author, CancellationToken.None)).Status);

        var cancelled = await service.CancelAsync(existing.Id, existing.Version, null, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, cancelled.Status);
        Assert.True(cancelled.Appointment!.ClientArchived);

        Assert.Equal(AppointmentWriteStatus.ClientArchived,
            (await service.RestoreAsync(existing.Id, cancelled.Appointment.Appointment.Version, Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task UnknownClient_IsAFieldError()
    {
        var result = await Service().CreateAsync(Personal(_alice, At(10), client: Guid.CreateVersion7()), Author, CancellationToken.None);

        AssertField(result, "clientId", "not_found");
    }

    [Fact]
    public async Task DeactivatedOrNonEligibleCollaborator_CannotBeAssigned()
    {
        var service = Service();
        _store.SetUser(_bruno, active: false, eligible: true);
        AssertField(await service.CreateAsync(Personal(_bruno, At(10)), Author, CancellationToken.None), "responsibleUserId", "not_eligible");

        _store.SetUser(_bruno, active: true, eligible: false);
        AssertField(await service.CreateAsync(Personal(_bruno, At(10)), Author, CancellationToken.None), "responsibleUserId", "not_eligible");
        AssertField(await service.CreateAsync(Personal(Guid.CreateVersion7(), At(10)), Author, CancellationToken.None), "responsibleUserId", "not_eligible");

        Assert.Equal([_alice], (await service.ListResponsiblesAsync(CancellationToken.None)).Select(r => r.Id));
    }

    [Fact]
    public async Task DeactivatedCollaborator_ExistingAppointmentNotesCanStillBeEditedButNotMoved()
    {
        var existing = _store.Seed(_client, _bruno, Instant(10), 30);
        _store.SetUser(_bruno, active: false, eligible: true);
        var service = Service();

        var notes = await service.UpdateAsync(existing.Id, Personal(_bruno, At(10), notes: "Préparer la commande"), existing.Version, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, notes.Status);
        Assert.Equal("Bruno Opticien", notes.Appointment!.ResponsibleDisplayName);

        // B2-REV-01 (03-B3) : le même collaborateur désactivé ne reçoit pas de nouveau créneau.
        AssertField(await service.UpdateAsync(existing.Id, Personal(_bruno, At(11)), notes.Appointment.Appointment.Version, Author, CancellationToken.None),
            "responsibleUserId", "not_eligible");
        Assert.Equal(Instant(10), existing.StartsAtUtc);

        // Réattribution à un collaborateur actif et éligible : permise.
        var moved = await service.UpdateAsync(existing.Id, Personal(_alice, At(10)), notes.Appointment.Appointment.Version, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, moved.Status);
    }

    /// <summary>
    /// B2-REV-01 (audit 03-B2, corrigé en 03-B3) : un déplacement ou un changement de durée revérifie l'éligibilité
    /// du responsable même quand son identifiant ne change pas. Avant correction, ces écritures étaient acceptées.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Move_KeepingAResponsibleNoLongerActiveOrEligible_IsRefused(bool active, bool eligible)
    {
        var existing = _store.Seed(_client, _bruno, Instant(10), 30);
        _store.SetUser(_bruno, active, eligible);
        var service = Service();

        AssertField(await service.UpdateAsync(existing.Id, Personal(_bruno, At(14)), existing.Version, Author, CancellationToken.None), "responsibleUserId", "not_eligible");
        AssertField(await service.UpdateAsync(existing.Id, Personal(_bruno, At(10), 60), existing.Version, Author, CancellationToken.None), "responsibleUserId", "not_eligible");
        Assert.Equal((Instant(10), Instant(10, 30), (Guid?)_bruno), (existing.StartsAtUtc, existing.EndsAtUtc, existing.ResponsibleUserId));
        Assert.Equal(0, _store.CommitCount);

        // Motif et notes seulement : aucune nouvelle réservation, écriture permise.
        var notes = await service.UpdateAsync(existing.Id, Personal(_bruno, At(10), reason: "prise_mesures", notes: "Rappeler"), existing.Version, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, notes.Status);
    }

    [Fact]
    public async Task Update_MovesWithinItsOwnSlotAndRefusesAnOccupiedOne()
    {
        var service = Service();
        var mine = _store.Seed(_client, _alice, Instant(10), 30);
        _store.Seed(_otherClient, _alice, Instant(11), 30);

        // Décalage de 15 min qui recoupe son propre ancien créneau : pas un conflit avec lui-même.
        var shifted = await service.UpdateAsync(mine.Id, Personal(_alice, At(10, 15)), mine.Version, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, shifted.Status);
        Assert.Equal(Instant(10, 15), shifted.Appointment!.Appointment.StartsAtUtc);

        var version = shifted.Appointment.Appointment.Version;
        Assert.Equal(AppointmentWriteStatus.SlotConflict, (await service.UpdateAsync(mine.Id, Personal(_alice, At(10, 45)), version, Author, CancellationToken.None)).Status);
        Assert.Equal(Instant(10, 15), mine.StartsAtUtc);
        // Bout à bout avec 11:00 : admis.
        Assert.Equal(AppointmentWriteStatus.Success, (await service.UpdateAsync(mine.Id, Personal(_alice, At(10, 30)), version, Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Update_FromPersonalToReception_ConsumesCapacity()
    {
        var service = Service(capacity: 1);
        _store.Seed(_otherClient, null, Instant(10), 30, "retrait_commande");
        var mine = _store.Seed(_client, _alice, Instant(10), 30, "sav_reparation");

        var result = await service.UpdateAsync(mine.Id, Reception(At(10), reason: "sav_reparation"), mine.Version, Author, CancellationToken.None);

        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull, result.Status);
        Assert.Equal(_alice, mine.ResponsibleUserId);
    }

    /// <summary>
    /// Modifié le 17/10/2026 (03-B3, B2-REV-02 et O-03-23 retenue par le propriétaire). Version 03-B2 :
    /// <c>Update_MoveIntoThePastIsRefusedButNotesOfAPastAppointmentCanChange</c> acceptait le déplacement d'un
    /// rendez-vous passé vers un créneau futur (10:00 → 14:00), ce que la règle retenue interdit (voir le test suivant).
    /// </summary>
    [Fact]
    public async Task Update_FutureAppointmentCannotMoveIntoThePast()
    {
        var future = _store.Seed(_client, _alice, Instant(14), 30);
        _clock.Now = Instant(12);

        AssertField(await Service().UpdateAsync(future.Id, Personal(_alice, At(11)), future.Version, Author, CancellationToken.None), "startsAtUtc", "in_past");
        Assert.Equal(Instant(14), future.StartsAtUtc);
    }

    /// <summary>
    /// B2-REV-02 (03-B3) : un rendez-vous commencé — même en cours — garde ses informations de réservation (client,
    /// créneau, durée, responsable, auteur et date de création) ; seuls le motif et les notes se corrigent, l'auteur
    /// et la date de la dernière modification étant enregistrés.
    /// </summary>
    [Fact]
    public async Task StartedAppointment_KeepsItsBooking_OnlyReasonAndNotesCanBeCorrected()
    {
        var started = _store.Seed(_client, _bruno, Instant(10), 30, "sav_reparation");
        var (createdAt, createdBy) = (started.CreatedAtUtc, started.CreatedByUserId);
        _clock.Now = Instant(10, 10);
        var service = Service();
        var editor = Guid.CreateVersion7();

        var refusals = new[]
        {
            Personal(_alice, At(10), reason: "sav_reparation"),                        // réattribution
            Personal(_bruno, At(14), reason: "sav_reparation"),                        // déplacement vers le futur
            Personal(_bruno, At(10), 60, reason: "sav_reparation"),                    // durée
            Personal(_bruno, At(10), client: _otherClient, reason: "sav_reparation"),  // client
            Reception(At(10), reason: "sav_reparation"),                               // responsable retiré (accueil)
        };
        foreach (var input in refusals)
        {
            Assert.Equal(AppointmentWriteStatus.AppointmentPast, (await service.UpdateAsync(started.Id, input, started.Version, editor, CancellationToken.None)).Status);
        }

        Assert.Equal(0, _store.CommitCount);
        Assert.Equal((_client, (Guid?)_bruno, Instant(10), Instant(10, 30)), (started.ClientId, started.ResponsibleUserId, started.StartsAtUtc, started.EndsAtUtc));

        // Même un collaborateur désactivé depuis n'empêche pas la correction du motif et des notes.
        _store.SetUser(_bruno, active: false, eligible: false);
        var corrected = await service.UpdateAsync(started.Id, Personal(_bruno, At(10), reason: "choix_equipement", notes: "Monture choisie"), started.Version, editor, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, corrected.Status);
        var a = corrected.Appointment!.Appointment;
        Assert.Equal(("choix_equipement", "Monture choisie"), (a.ReasonCode, a.Notes));
        Assert.Equal((_client, (Guid?)_bruno, Instant(10), Instant(10, 30)), (a.ClientId, a.ResponsibleUserId, a.StartsAtUtc, a.EndsAtUtc));
        Assert.Equal((createdAt, createdBy), (a.CreatedAtUtc, a.CreatedByUserId));
        Assert.Equal((editor, Instant(10, 10)), (a.UpdatedByUserId, a.UpdatedAtUtc));
    }

    /// <summary>B2-REV-03 (03-B3) : l'heure murale annoncée par le poste doit être celle du serveur pour l'instant UTC.</summary>
    [Theory]
    [InlineData("Europe/Brussels", "2026-10-25T02:00:00Z", "2026-10-25T03:00", null)]                   // après le recul d'heure : UTC+1
    [InlineData("Europe/Brussels", "2026-10-25T02:00:00Z", "2026-10-25T04:00", "time_zone_mismatch")]   // poste resté à UTC+2
    [InlineData("Europe/Brussels", "2027-03-28T01:00:00Z", "2027-03-28T03:00", null)]                   // avance d'heure 2027 : UTC+2
    [InlineData("Europe/Brussels", "2027-03-28T01:00:00Z", "2027-03-28T02:00", "time_zone_mismatch")]
    [InlineData("Europe/Brussels", "2028-10-29T08:00:00Z", "2028-10-29T09:00", null)]                   // date future lointaine
    [InlineData("Africa/Casablanca", "2026-09-20T02:00:00Z", "2026-09-20T02:00", null)]                 // après le passage à UTC+0
    [InlineData("Africa/Casablanca", "2026-09-20T02:00:00Z", "2026-09-20T03:00", "time_zone_mismatch")] // poste resté à UTC+1
    [InlineData("Africa/Casablanca", "2027-02-10T09:00:00Z", "2027-02-10T09:00", null)]
    [InlineData("Africa/Casablanca", "2027-02-10T09:00:00Z", "2027-02-10T10:00", "time_zone_mismatch")]
    [InlineData("Europe/Brussels", "2026-10-26T08:00:00Z", "2026-10-26T09:00:00", "invalid_format")]
    [InlineData("Europe/Brussels", "2026-10-26T08:00:00Z", "2026-10-26 09:00", "invalid_format")]
    [InlineData("Europe/Brussels", "2026-10-26T08:00:00Z", "2026-10-26T24:00", "invalid_format")]
    public async Task StartsAtLocal_MustMatchTheServerConversion(string zone, string utc, string local, string? code)
    {
        _clock.Now = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var input = new AppointmentInput(_client, _alice, utc, local, 30, "conseil_devis", null);

        var result = await Service(timeZone: zone).CreateAsync(input, Author, CancellationToken.None);

        if (code is null)
        {
            Assert.Equal(AppointmentWriteStatus.Success, result.Status);
        }
        else
        {
            AssertField(result, "startsAtLocal", code);
            Assert.Equal(0, _store.CommitCount);
        }
    }

    [Fact]
    public async Task Update_StaleVersionOrCancelled_WritesNothing()
    {
        var service = Service();
        var appointment = _store.Seed(_client, _alice, Instant(10), 30);

        Assert.Equal(AppointmentWriteStatus.VersionConflict,
            (await service.UpdateAsync(appointment.Id, Personal(_alice, At(11)), appointment.Version - 1, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.NotFound,
            (await service.UpdateAsync(Guid.CreateVersion7(), Personal(_alice, At(11)), 1, Author, CancellationToken.None)).Status);

        _store.FailNextCommit = true;
        Assert.Equal(AppointmentWriteStatus.VersionConflict,
            (await service.UpdateAsync(appointment.Id, Personal(_alice, At(10), notes: "x"), appointment.Version, Author, CancellationToken.None)).Status);

        var cancelled = _store.Seed(_client, _alice, Instant(15), 30, cancelled: true);
        Assert.Equal(AppointmentWriteStatus.AppointmentCancelled,
            (await service.UpdateAsync(cancelled.Id, Personal(_alice, At(16)), cancelled.Version, Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Cancel_IsLogicalKeepsAuthorDateReasonAndFreesTheSlot()
    {
        var service = Service();
        var appointment = _store.Seed(_client, _alice, Instant(10), 30);

        AssertField(await service.CancelAsync(appointment.Id, appointment.Version, new string('r', 201), Author, CancellationToken.None), "reason", "too_long");

        var result = await service.CancelAsync(appointment.Id, appointment.Version, "  Client indisponible  ", Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, result.Status);
        var cancelled = result.Appointment!.Appointment;
        Assert.Equal(Appointment.StatusCancelled, cancelled.Status);
        Assert.Equal("Client indisponible", cancelled.CancellationReason);
        Assert.Equal(Author, cancelled.CancelledByUserId);
        Assert.Equal(_clock.Now, cancelled.CancelledAtUtc);
        Assert.Single(_store.Appointments, a => a.Id == appointment.Id);

        Assert.Equal(AppointmentWriteStatus.AppointmentCancelled, (await service.CancelAsync(appointment.Id, cancelled.Version, null, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.Success, (await service.CreateAsync(Personal(_alice, At(10), client: _otherClient), Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Cancel_WithoutReasonStoresNull()
    {
        var appointment = _store.Seed(_client, _alice, Instant(10), 30);

        var result = await Service().CancelAsync(appointment.Id, appointment.Version, "   ", Author, CancellationToken.None);

        Assert.Null(result.Appointment!.Appointment.CancellationReason);
    }

    [Fact]
    public async Task Restore_RechecksConflictsCapacityCollaboratorAndPast()
    {
        var service = Service(capacity: 1);
        var personal = _store.Seed(_client, _alice, Instant(10), 30, cancelled: true);
        var reception = _store.Seed(_client, null, Instant(14), 30, "retrait_commande", cancelled: true);
        var taken = _store.Seed(_otherClient, _alice, Instant(10, 15), 30);
        _store.Seed(_otherClient, null, Instant(14), 15, "retrait_commande");

        Assert.Equal(AppointmentWriteStatus.SlotConflict, (await service.RestoreAsync(personal.Id, personal.Version, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.ReceptionCapacityFull, (await service.RestoreAsync(reception.Id, reception.Version, Author, CancellationToken.None)).Status);

        // Le créneau se libère : rétablissement possible.
        var freed = await service.CancelAsync(taken.Id, taken.Version, null, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, freed.Status);
        var restored = await service.RestoreAsync(personal.Id, personal.Version, Author, CancellationToken.None);
        Assert.Equal(AppointmentWriteStatus.Success, restored.Status);
        Assert.Equal(Appointment.StatusPlanned, restored.Appointment!.Appointment.Status);
        Assert.Null(restored.Appointment.Appointment.CancelledAtUtc);
        Assert.Equal(AppointmentWriteStatus.AppointmentNotCancelled,
            (await service.RestoreAsync(personal.Id, restored.Appointment.Appointment.Version, Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Restore_RefusedForADeactivatedCollaboratorTheMissingCapacityOrAStartedSlot()
    {
        var withBruno = _store.Seed(_client, _bruno, Instant(10), 30, cancelled: true);
        var unassigned = _store.Seed(_client, null, Instant(12), 30, "retrait_commande", cancelled: true);
        _store.SetUser(_bruno, active: false, eligible: true);

        Assert.Equal(AppointmentWriteStatus.ResponsibleUnavailable, (await Service().RestoreAsync(withBruno.Id, withBruno.Version, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.ReceptionNotConfigured, (await Service(capacity: null).RestoreAsync(unassigned.Id, unassigned.Version, Author, CancellationToken.None)).Status);

        _clock.Now = Instant(12, 5);
        Assert.Equal(AppointmentWriteStatus.AppointmentPast, (await Service().RestoreAsync(unassigned.Id, unassigned.Version, Author, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task NotConfigured_EveryWriteIsRefused()
    {
        var service = new AppointmentService(_store, AppointmentSettings.From(null), _clock, _store.Audit);
        var appointment = _store.Seed(_client, _alice, Instant(10), 30);

        Assert.Equal(AppointmentWriteStatus.NotConfigured, (await service.CreateAsync(Personal(_alice, At(10)), Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.NotConfigured, (await service.UpdateAsync(appointment.Id, Personal(_alice, At(11)), appointment.Version, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.NotConfigured, (await service.CancelAsync(appointment.Id, appointment.Version, null, Author, CancellationToken.None)).Status);
        Assert.Equal(AppointmentWriteStatus.NotConfigured, (await service.RestoreAsync(appointment.Id, appointment.Version, Author, CancellationToken.None)).Status);
        Assert.Equal(0, _store.CommitCount);
    }

    [Fact]
    public async Task SimultaneousBookings_SameCollaborator_ExactlyOneSucceeds()
    {
        _store.DelayAfterLock = TimeSpan.FromMilliseconds(50);
        var service = Service();

        var results = await Task.WhenAll(
            Task.Run(() => service.CreateAsync(Personal(_alice, At(10)), Author, CancellationToken.None)),
            Task.Run(() => service.CreateAsync(Personal(_alice, At(10, 15), client: _otherClient), Author, CancellationToken.None)));

        Assert.Single(results, r => r.Status == AppointmentWriteStatus.Success);
        Assert.Single(results, r => r.Status == AppointmentWriteStatus.SlotConflict);
        Assert.Single(_store.Appointments);
    }

    [Fact]
    public async Task SimultaneousBookings_ReceptionPool_NeverExceedTheCapacity()
    {
        _store.DelayAfterLock = TimeSpan.FromMilliseconds(20);
        var service = Service(capacity: 2);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            Task.Run(() => service.CreateAsync(Reception(At(10)), Author, CancellationToken.None))));

        Assert.Equal(2, results.Count(r => r.Status == AppointmentWriteStatus.Success));
        Assert.Equal(3, results.Count(r => r.Status == AppointmentWriteStatus.ReceptionCapacityFull));
        Assert.Equal(2, ReceptionOccupancy.MaxConcurrent(_store.Appointments.Select(a => (a.StartsAtUtc, a.EndsAtUtc)), Instant(10), Instant(10, 30)));
    }

    [Fact]
    public async Task SimultaneousBookings_WithoutTheLock_WouldDoubleBook()
    {
        // Témoin négatif : sans verrou, la même fenêtre de concurrence laisse passer deux réservations du même
        // créneau. La garantie des deux tests précédents vient donc bien du verrou par ressource.
        // Modifié le 17/10/2026 (03-B3) : la fenêtre reposait sur un délai de 50 ms ; si le second poste démarrait plus
        // tard, il voyait déjà la réservation du premier (échec intermittent constaté sur le HEAD 03-B2 : 4 sur 8).
        // Les deux sessions, contrôles faits, se rejoignent désormais juste avant d'enregistrer.
        _store.IgnoreLocks = true;
        _store.RendezvousBeforeCommit = 2;
        var service = Service();

        // Saisies préparées hors des tâches (03-B3) : les deux postes entrent ensemble dans la fenêtre de concurrence.
        var first = Personal(_alice, At(10));
        var second = Personal(_alice, At(10), client: _otherClient);
        var results = await Task.WhenAll(
            Task.Run(() => service.CreateAsync(first, Author, CancellationToken.None)),
            Task.Run(() => service.CreateAsync(second, Author, CancellationToken.None)));

        Assert.All(results, r => Assert.Equal(AppointmentWriteStatus.Success, r.Status));
    }

    [Fact]
    public async Task SimultaneousRestoreAndBooking_OnTheSameSlot_ExactlyOneWins()
    {
        _store.DelayAfterLock = TimeSpan.FromMilliseconds(50);
        var cancelled = _store.Seed(_client, _alice, Instant(10), 30, cancelled: true);
        var service = Service();

        var results = await Task.WhenAll(
            Task.Run(() => service.RestoreAsync(cancelled.Id, cancelled.Version, Author, CancellationToken.None)),
            Task.Run(() => service.CreateAsync(Personal(_alice, At(10), client: _otherClient), Author, CancellationToken.None)));

        Assert.Single(results, r => r.Status == AppointmentWriteStatus.Success);
        Assert.Single(results, r => r.Status == AppointmentWriteStatus.SlotConflict);
    }
}
