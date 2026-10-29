using MMV.Api.Tests.Audit;
using MMV.Api.Tests.Clients;
using MMV.Api.Tests.Identity;
using MMV.Application.Optical;
using MMV.Domain.Audit;
using MMV.Domain.Optical;
using static MMV.Api.Tests.Optical.OpticalRulesTests;

namespace MMV.Api.Tests.Optical;

/// <summary>
/// Cas d'usage du dossier optique sur doubles non persistants : brouillon, confirmation ATOMIQUE, absence de fausse
/// mise à jour, idempotence, conflits, rectification, abandon, invalidation, client archivé, audit sans valeur optique.
/// </summary>
public sealed class OpticalServiceTests
{
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly Guid Measurer = Guid.CreateVersion7();

    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryAuditRecorder _audit = new();
    private readonly InMemoryClientStore _clients;
    private readonly InMemoryOpticalStore<Prescription, PrescriptionRevision, PrescriptionContent> _prescriptions;
    private readonly InMemoryOpticalStore<Measurement, MeasurementRevision, MeasurementContent> _measurements;
    private readonly InMemoryOpticalDirectory _directory = new();
    private readonly PrescriptionService _service;
    private readonly MeasurementService _measurementService;

    public OpticalServiceTests()
    {
        _clients = new InMemoryClientStore(_clock);
        _prescriptions = new("RX", _audit);
        _measurements = new("MES", _audit);
        _directory.Measurers.Add(new OpticalActorView(Measurer, "Fictif-Opticien"));
        _service = new PrescriptionService(_prescriptions, _clients, _clock, _audit);
        _measurementService = new MeasurementService(_measurements, _clients, _directory, _clock, _audit);
    }

    private Guid ActiveClient() => _clients.Seed("Fictif", "Client").Id;

    private async Task<Prescription> Draft(Guid? clientId = null, PrescriptionInput? input = null) =>
        (await _service.CreateAsync(clientId ?? ActiveClient(), input ?? Prescription(new EyeInput(Sphere: "-1,25")), confirm: false, Guid.CreateVersion7(), User, CancellationToken.None)).Record!;

    private async Task<Prescription> Confirmed(Guid? clientId = null) =>
        (await _service.CreateAsync(clientId ?? ActiveClient(), Prescription(new EyeInput(Sphere: "-1,25")), confirm: true, Guid.CreateVersion7(), User, CancellationToken.None)).Record!;

    private Task<OpticalWriteResult<Prescription>> Save(Prescription record, PrescriptionInput? input, bool confirm, Guid? requestId = null) =>
        _service.SaveDraftAsync(record.Id, input, confirm, record.Version, requestId ?? Guid.CreateVersion7(), User, CancellationToken.None);

    [Fact]
    public async Task Create_Draft_RecordsAuditGroupsWithoutAnyOpticalValue()
    {
        var record = await Draft(input: Prescription(new EyeInput(Sphere: "-1,25"), notes: "note fictive"));

        Assert.NotNull(record.OpenDraft);
        Assert.Null(record.LastConfirmed);
        var entry = Assert.Single(_audit.Committed);
        Assert.Equal("prescription.created", entry.Action);
        Assert.Equal(AuditActions.OpticalModule, entry.Module);
        Assert.Equal(["provenance", "values", "notes"], entry.Changes.Select(c => c.Field));
        Assert.All(entry.Changes, c => Assert.True(c.Masked));
        Assert.DoesNotContain(entry.Changes, c => c.Old is not null || c.New is not null);
    }

    [Fact]
    public async Task CreateAndConfirm_IsAtomic_RefusalWritesNothing()
    {
        var clientId = ActiveClient();

        var refused = await _service.CreateAsync(clientId, Prescription(new EyeInput(Sphere: "0,00")), confirm: true, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Incomplete, refused.Status);
        Assert.Equal([OpticalFieldErrors.SphereZeroOnly], refused.Errors![OpticalFieldErrors.Correction]);
        Assert.Empty(_prescriptions.Records);
        Assert.Empty(_audit.Committed);
        Assert.Equal(0, _prescriptions.SaveCount);
    }

    [Fact]
    public async Task CreateAndConfirm_ConfirmsRevisionOne_InOneSave()
    {
        var result = await _service.CreateAsync(ActiveClient(), Prescription(new EyeInput(Sphere: "-1,25")), confirm: true, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, result.Status);
        Assert.Equal(1, result.Record!.LastConfirmed!.RevisionNo);
        Assert.Null(result.Record.OpenDraft);
        Assert.Equal(1, _prescriptions.SaveCount);
        Assert.Equal(["prescription.created", "prescription.confirmed"], _audit.Committed.Select(e => e.Action));
        Assert.Equal("1", _audit.Committed[1].Changes.Single(c => c.Field == "revisionNo").New);
    }

    [Fact]
    public async Task Create_ReplayedRequest_ReturnsTheSameRecord_WithoutDuplicate()
    {
        var clientId = ActiveClient();
        var requestId = Guid.CreateVersion7();
        var first = await _service.CreateAsync(clientId, Prescription(new EyeInput(Sphere: "-1")), true, requestId, User, CancellationToken.None);

        var second = await _service.CreateAsync(clientId, Prescription(new EyeInput(Sphere: "-1")), true, requestId, User, CancellationToken.None);

        Assert.True(second.Replayed);
        Assert.Same(first.Record, second.Record);
        Assert.Single(_prescriptions.Records);
        Assert.Equal(2, _audit.Committed.Count);
        Assert.Equal(OpticalWriteStatus.RequestIdReused,
            (await _service.CreateAsync(ActiveClient(), Prescription(new EyeInput(Sphere: "-1")), true, requestId, User, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Create_ArchivedOrUnknownClient_IsRefused()
    {
        var archived = _clients.Seed("Fictif", "Archive", archived: true).Id;

        Assert.Equal(OpticalWriteStatus.ClientArchived, (await _service.CreateAsync(archived, Prescription(), false, Guid.CreateVersion7(), User, CancellationToken.None)).Status);
        Assert.Equal(OpticalWriteStatus.ClientNotFound, (await _service.CreateAsync(Guid.CreateVersion7(), Prescription(), false, Guid.CreateVersion7(), User, CancellationToken.None)).Status);
        Assert.Empty(_prescriptions.Records);
    }

    [Fact]
    public async Task SaveDraft_Unchanged_WritesNothing()
    {
        var record = await Draft();
        var version = record.Version;
        var audits = _audit.Committed.Count;

        var result = await Save(record, Prescription(new EyeInput(Sphere: " -1,25 ")), confirm: false);

        Assert.Equal(OpticalWriteStatus.Success, result.Status);
        Assert.Equal(version, record.Version);
        Assert.Equal(audits, _audit.Committed.Count);
        Assert.Equal(1, _prescriptions.SaveCount);
    }

    [Fact]
    public async Task SaveAndConfirm_Incomplete_LeavesTheDraftUnchanged()
    {
        var record = await Draft();
        var version = record.Version;

        var result = await Save(record, Prescription(new EyeInput(Sphere: "-1,25", Cylinder: "-0,50")), confirm: true);

        Assert.Equal(OpticalWriteStatus.Incomplete, result.Status);
        Assert.Equal([OpticalFieldErrors.AxisRequired], result.Errors!["od.axis"]);
        Assert.Null(record.OpenDraft!.OdCylinder);
        Assert.Equal(version, record.Version);
    }

    [Fact]
    public async Task SaveAndConfirm_EditsAndConfirmsInOneSave()
    {
        var record = await Draft();

        var result = await Save(record, Prescription(new EyeInput(Sphere: "-1,25", Cylinder: "-0,50", Axis: "180")), confirm: true);

        Assert.Equal(OpticalWriteStatus.Success, result.Status);
        Assert.Equal("180", record.LastConfirmed!.OdAxis);
        Assert.Equal(["prescription.created", "prescription.draft_updated", "prescription.confirmed"], _audit.Committed.Select(e => e.Action));
        Assert.Equal(["values"], _audit.Committed[1].Changes.Select(c => c.Field));
    }

    [Fact]
    public async Task Write_ReplayedRequest_DoesNotConfirmTwice()
    {
        var record = await Draft();
        var requestId = Guid.CreateVersion7();
        var staleVersion = record.Version;
        await Save(record, null, confirm: true, requestId);
        var audits = _audit.Committed.Count;

        var replay = await _service.SaveDraftAsync(record.Id, null, true, staleVersion, requestId, User, CancellationToken.None);

        Assert.True(replay.Replayed);
        Assert.Single(record.Revisions, r => r.Status == OpticalRevisionStatus.Confirmed);
        Assert.Equal(audits, _audit.Committed.Count);
    }

    [Fact]
    public async Task Write_StaleVersion_IsAConflict_WithoutWrite()
    {
        var record = await Draft();

        var result = await _service.SaveDraftAsync(record.Id, Prescription(new EyeInput(Sphere: "-2")), false, record.Version - 1, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.VersionConflict, result.Status);
        Assert.Equal("-1,25", record.OpenDraft!.OdSphere);
    }

    [Fact]
    public async Task Write_ConcurrentSave_FailsWithoutAuditEvent()
    {
        var record = await Draft();
        var audits = _audit.Committed.Count;
        _prescriptions.FailNextSave = true;

        var result = await Save(record, Prescription(new EyeInput(Sphere: "-2")), confirm: true);

        Assert.Equal(OpticalWriteStatus.VersionConflict, result.Status);
        Assert.Equal(audits, _audit.Committed.Count);
    }

    [Fact]
    public async Task Rectification_KeepsConfirmedRevisionImmutable_UntilNextConfirmation()
    {
        var record = await Confirmed();

        var started = await _service.StartRectificationAsync(record.Id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        Assert.Equal(OpticalWriteStatus.Success, started.Status);
        Assert.Equal(1, record.OpenDraft!.BaseRevisionNo);
        Assert.Equal(OpticalWriteStatus.DraftExists,
            (await _service.StartRectificationAsync(record.Id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None)).Status);

        await Save(record, Prescription(new EyeInput(Sphere: "-1,50")), confirm: false);
        Assert.Equal("-1,25", record.LastConfirmed!.OdSphere);

        await Save(record, null, confirm: true);
        Assert.Equal(2, record.LastConfirmed!.RevisionNo);
        Assert.Equal("-1,50", record.LastConfirmed.OdSphere);
        Assert.Equal("-1,25", record.Revisions.Single(r => r.RevisionNo == 1).OdSphere);
        Assert.Throws<InvalidOperationException>(() => record.DiscardRectification(User, _clock.GetUtcNow(), Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Rectification_RequiresExpectedRevisionAndReason()
    {
        var record = await Confirmed();

        Assert.Equal(OpticalWriteStatus.RevisionConflict,
            (await _service.StartRectificationAsync(record.Id, 2, "other", "x", record.Version, Guid.CreateVersion7(), User, CancellationToken.None)).Status);
        Assert.Equal(OpticalWriteStatus.ValidationFailed,
            (await _service.StartRectificationAsync(record.Id, 1, "other", " ", record.Version, Guid.CreateVersion7(), User, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Discard_OnlyARectification_AndKeepsIt()
    {
        var draft = await Draft();
        Assert.Equal(OpticalWriteStatus.NotRectification, (await _service.DiscardRectificationAsync(draft.Id, draft.Version, Guid.CreateVersion7(), User, CancellationToken.None)).Status);

        var record = await Confirmed();
        await _service.StartRectificationAsync(record.Id, 1, "additional_information", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        var result = await _service.DiscardRectificationAsync(record.Id, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, result.Status);
        Assert.Null(record.OpenDraft);
        Assert.Single(record.Revisions, r => r.Status == OpticalRevisionStatus.Discarded);
        Assert.Equal(1, record.LastConfirmed!.RevisionNo);
    }

    [Fact]
    public async Task Invalidate_DiscardsOpenRectification_AllowedForArchivedClient_ThenFrozen()
    {
        var client = _clients.Seed("Fictif", "Bientot-Archive");
        var record = await Confirmed(client.Id);
        await _service.StartRectificationAsync(record.Id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        client.Archive(User, _clock.GetUtcNow());

        Assert.Equal(OpticalWriteStatus.ClientArchived, (await Save(record, Prescription(new EyeInput(Sphere: "-3")), false)).Status);
        var result = await _service.InvalidateAsync(record.Id, "duplicate", "commentaire fictif", record.Version, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, result.Status);
        Assert.True(record.IsInvalidated);
        Assert.Null(record.OpenDraft);
        Assert.Equal(["rectification_discarded", "invalidated"], _audit.Committed.TakeLast(2).Select(e => e.Action.Split('.')[1]));
        var invalidated = _audit.Committed[^1];
        Assert.Equal("duplicate", invalidated.Changes.Single(c => c.Field == "invalidationReasonCode").New);
        Assert.True(invalidated.Changes.Single(c => c.Field == "comment").Masked);
        Assert.Equal(OpticalWriteStatus.AlreadyInvalidated,
            (await _service.InvalidateAsync(record.Id, "duplicate", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None)).Status);
        Assert.Equal(OpticalWriteStatus.Invalidated,
            (await _service.StartRectificationAsync(record.Id, 1, "other", "x", record.Version, Guid.CreateVersion7(), User, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Measurement_MeasurerMustBeEligible_AndAcuityAloneSuffices()
    {
        var clientId = ActiveClient();
        var refused = await _measurementService.CreateAsync(clientId, Measurement(Guid.CreateVersion7(), new EyeInput(Acuity: "10/10")), true, Guid.CreateVersion7(), User, CancellationToken.None);
        Assert.Equal([OpticalFieldErrors.NotEligible], refused.Errors!["provenance.measuredByUserId"]);

        var accepted = await _measurementService.CreateAsync(clientId, Measurement(Measurer, new EyeInput(Acuity: "10/10")), true, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, accepted.Status);
        Assert.Equal(Measurer, accepted.Record!.LastConfirmed!.MeasuredByUserId);
        Assert.Equal(User, accepted.Record.CreatedByUserId);
        Assert.Equal("measurement.confirmed", _audit.Committed[^1].Action);
    }

    [Fact]
    public async Task Writes_PassTheGuardRecheckedUnderLock_InvalidationOnlyWithoutClientRequirement()
    {
        var clientId = ActiveClient();
        var record = await Confirmed(clientId);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: true), _prescriptions.LastGuard);

        await _service.StartRectificationAsync(record.Id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: true), _prescriptions.LastGuard);
        await Save(record, Prescription(new EyeInput(Sphere: "-1,50")), confirm: false);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: true), _prescriptions.LastGuard);
        await _service.DiscardRectificationAsync(record.Id, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: true), _prescriptions.LastGuard);
        await _service.InvalidateAsync(record.Id, "duplicate", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: false), _prescriptions.LastGuard);
    }

    [Fact]
    public async Task Write_ClientArchivedUnderLock_IsRefused_WithoutWriteNorAuditEvent()
    {
        var clientId = ActiveClient();
        var audits = _audit.Committed.Count;
        _prescriptions.NextGuardRefusal = OpticalCommitResult.ClientArchived;

        var created = await _service.CreateAsync(clientId, Prescription(new EyeInput(Sphere: "-1,25")), confirm: true, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.ClientArchived, created.Status);
        Assert.Null(created.Record);
        Assert.Empty(_prescriptions.Records);
        Assert.Equal(audits, _audit.Committed.Count);

        var draft = await Draft(clientId);
        audits = _audit.Committed.Count;
        _prescriptions.NextGuardRefusal = OpticalCommitResult.ClientArchived;
        Assert.Equal(OpticalWriteStatus.ClientArchived, (await Save(draft, Prescription(new EyeInput(Sphere: "-2")), confirm: true)).Status);
        Assert.Equal(audits, _audit.Committed.Count);
    }

    [Fact]
    public async Task Measurement_DesignatedMeasurerRecheckedUnderLock_KeptMeasurerIsNot()
    {
        var clientId = ActiveClient();
        _measurements.NextGuardRefusal = OpticalCommitResult.MeasurerNotEligible;

        var refused = await _measurementService.CreateAsync(clientId, Measurement(Measurer, new EyeInput(Acuity: "10/10")), true, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.ValidationFailed, refused.Status);
        Assert.Equal([OpticalFieldErrors.NotEligible], refused.Errors!["provenance.measuredByUserId"]);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: true, Measurer), _measurements.LastGuard);
        Assert.Empty(_measurements.Records);

        var record = (await _measurementService.CreateAsync(clientId, Measurement(Measurer, new EyeInput(Acuity: "10/10")), true, Guid.CreateVersion7(), User, CancellationToken.None)).Record!;
        await _measurementService.StartRectificationAsync(record.Id, 1, "transcription_error", null, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);
        // Collaborateur historique conservé par la rectification : ni contrôlé, ni verrouillé (désignation passée).
        _directory.Measurers.Clear();
        var kept = await _measurementService.SaveDraftAsync(record.Id, Measurement(Measurer, new EyeInput(Acuity: "9/10")), true, record.Version, Guid.CreateVersion7(), User, CancellationToken.None);

        Assert.Equal(OpticalWriteStatus.Success, kept.Status);
        Assert.Equal(new OpticalCommitGuard(clientId, RequireActiveClient: true), _measurements.LastGuard);
    }
}
