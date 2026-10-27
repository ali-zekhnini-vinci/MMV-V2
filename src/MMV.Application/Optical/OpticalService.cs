using MMV.Application.Audit;
using MMV.Application.Clients;
using MMV.Domain.Audit;
using MMV.Domain.Optical;

namespace MMV.Application.Optical;

public enum OpticalWriteStatus
{
    Success,
    NotFound,
    ClientNotFound,
    ValidationFailed,
    Incomplete,
    VersionConflict,
    ClientArchived,
    Invalidated,
    AlreadyInvalidated,
    NotDraft,
    NotRectification,
    DraftExists,
    RevisionConflict,
    RequestIdReused,
}

/// <summary>
/// Résultat d'une écriture. <see cref="Replayed"/> : la requête (même identifiant) avait déjà été appliquée — l'état
/// courant est renvoyé, RIEN n'est réécrit (aucune nouvelle version, aucun événement d'audit).
/// </summary>
public sealed record OpticalWriteResult<TRecord>(
    OpticalWriteStatus Status, TRecord? Record = default, IReadOnlyDictionary<string, string[]>? Errors = null, bool Replayed = false)
    where TRecord : class;

public enum OpticalCommitResult
{
    Saved,
    VersionConflict,

    /// <summary>Insertion refusée par l'unicité de l'identifiant de requête de création (réémission concurrente).</summary>
    DuplicateCreationRequest,
}

/// <summary>Persistance d'un type d'enregistrement optique (implémentée par MMV.Infrastructure sur PostgreSQL).</summary>
public interface IOpticalRecordStore<TRecord>
    where TRecord : class
{
    /// <summary>Enregistrement suivi pour modification, avec toutes ses révisions, ou <c>null</c>.</summary>
    Task<TRecord?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Enregistrement créé par cette requête (identifiant de requête de création), ou <c>null</c>.</summary>
    Task<TRecord?> FindByCreationRequestAsync(Guid requestId, CancellationToken cancellationToken);

    void Add(TRecord record);

    /// <summary>
    /// Enregistre en UNE transaction (en-tête, révisions, événements d'audit) ; en cas de conflit ou de doublon, rien
    /// n'est écrit et le suivi est vidé.
    /// </summary>
    Task<OpticalCommitResult> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Comptes MMV cités par le dossier optique (collaborateurs pouvant être désignés comme ayant mesuré).</summary>
public interface IOpticalDirectory
{
    /// <summary>Compte actif détenant <c>optical.measurements.write</c> (habilitation à mesurer, D-04-03.b).</summary>
    Task<bool> IsEligibleMeasurerAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OpticalActorView>> ListMeasurersAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Cas d'usage communs aux deux types (mécanismes partagés : brouillon unique, révisions immuables, confirmation
/// atomique, rectification, abandon, invalidation, idempotence, audit dans la même transaction). Chaque écriture :
/// <list type="bullet">
/// <item>porte un identifiant de requête : sa réémission (double clic, délai réseau, nouvelle tentative) est reconnue et
/// renvoie l'état courant sans rien écrire — jamais un doublon ni une seconde révision confirmée ;</item>
/// <item>porte la version lue (<c>xmin</c>) : périmée → 409 sans aucune écriture ;</item>
/// <item>n'écrit rien si le contenu est inchangé (aucune version, aucun auteur, aucun événement) ;</item>
/// <item>enregistre ses événements d'audit dans le même <c>SaveChanges</c> que la modification (une transaction).</item>
/// </list>
/// Un client archivé est consultable mais gelé : seule l'invalidation reste possible.
/// </summary>
public abstract class OpticalRecordService<TRecord, TRevision, TContent, TInput>(
    IOpticalRecordStore<TRecord> store, IClientStore clients, TimeProvider clock, IAuditRecorder audit)
    where TRecord : OpticalRecord<TRevision, TContent>
    where TRevision : OpticalRevision<TContent>
    where TContent : OpticalContent
{
    /// <summary>Entité d'audit (<c>prescription</c> | <c>measurement</c>).</summary>
    protected abstract string EntityType { get; }

    protected abstract OpticalValidation<TContent> Validate(TInput input);

    protected abstract IReadOnlyDictionary<string, string[]> CheckConfirmation(TContent content);

    protected abstract TRecord CreateRecord(Guid id, Guid clientId, TContent content, Guid revisionId, Guid requestId, Guid userId, DateTimeOffset now);

    /// <summary>Contrôles de références (comptes cités) d'un contenu dont la provenance a changé ; erreurs par champ.</summary>
    protected virtual Task<IReadOnlyDictionary<string, string[]>> CheckReferencesAsync(TContent content, TContent? previous, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string[]>>(new Dictionary<string, string[]>());

    protected abstract bool ProvenanceEquals(TContent left, TContent right);

    protected abstract bool HasProvenance(TContent content);

    /// <summary>Création d'un brouillon, ou création ET confirmation atomiques (« Enregistrer », D-04-17).</summary>
    public async Task<OpticalWriteResult<TRecord>> CreateAsync(
        Guid clientId, TInput input, bool confirm, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        if (await ReplayCreationAsync(clientId, requestId, userId, cancellationToken) is { } replay)
        {
            return replay;
        }

        var client = await clients.GetAsync(clientId, cancellationToken);
        if (client is null)
        {
            return new(OpticalWriteStatus.ClientNotFound);
        }

        if (client.IsArchived)
        {
            return new(OpticalWriteStatus.ClientArchived);
        }

        var validation = Validate(input);
        if (!validation.IsValid)
        {
            return new(OpticalWriteStatus.ValidationFailed, Errors: validation.Errors);
        }

        var content = validation.Content!;
        var references = await CheckReferencesAsync(content, null, cancellationToken);
        if (references.Count > 0)
        {
            return new(OpticalWriteStatus.ValidationFailed, Errors: references);
        }

        if (confirm && CheckConfirmation(content) is { Count: > 0 } incomplete)
        {
            return new(OpticalWriteStatus.Incomplete, Errors: incomplete);
        }

        var now = Now();
        var record = CreateRecord(Guid.CreateVersion7(now), clientId, content, Guid.CreateVersion7(now), requestId, userId, now);
        store.Add(record);
        Record(record, "created", ContentGroups(null, content), userId, now);
        if (confirm)
        {
            var confirmed = record.ConfirmDraft(userId, now, requestId);
            Record(record, "confirmed", [RevisionNo(confirmed.RevisionNo)], userId, now);
        }

        return await store.SaveChangesAsync(cancellationToken) switch
        {
            OpticalCommitResult.Saved => new(OpticalWriteStatus.Success, record),
            // Même requête réémise en parallèle : la première l'a emporté, son résultat est relu (aucun doublon).
            OpticalCommitResult.DuplicateCreationRequest =>
                await ReplayCreationAsync(clientId, requestId, userId, cancellationToken) ?? new(OpticalWriteStatus.VersionConflict),
            _ => new(OpticalWriteStatus.VersionConflict),
        };
    }

    /// <summary>
    /// Modifie le brouillon ouvert (<paramref name="input"/> fourni) et/ou le confirme (<paramref name="confirm"/>) en UNE
    /// transaction : un refus de confirmation n'enregistre rien (aucun brouillon modifié à moitié).
    /// </summary>
    public async Task<OpticalWriteResult<TRecord>> SaveDraftAsync(
        Guid id, TInput? input, bool confirm, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var (record, refusal) = await WritableAsync(id, version, requestId, cancellationToken);
        if (record is null)
        {
            return refusal!;
        }

        var draft = record.OpenDraft;
        if (draft is null)
        {
            return new(OpticalWriteStatus.NotDraft, record);
        }

        if (await IsClientArchivedAsync(record, cancellationToken))
        {
            return new(OpticalWriteStatus.ClientArchived, record);
        }

        var current = draft.Content;
        var content = current;
        if (input is not null)
        {
            var validation = Validate(input);
            if (!validation.IsValid)
            {
                return new(OpticalWriteStatus.ValidationFailed, Errors: validation.Errors);
            }

            content = validation.Content!;
            if (!ProvenanceEquals(content, current))
            {
                var references = await CheckReferencesAsync(content, current, cancellationToken);
                if (references.Count > 0)
                {
                    return new(OpticalWriteStatus.ValidationFailed, Errors: references);
                }
            }
        }

        if (confirm)
        {
            if (draft.BaseRevisionNo != record.LastConfirmed?.RevisionNo)
            {
                return new(OpticalWriteStatus.RevisionConflict, record);
            }

            if (CheckConfirmation(content) is { Count: > 0 } incomplete)
            {
                return new(OpticalWriteStatus.Incomplete, record, incomplete);
            }
        }

        var changes = ContentGroups(current, content);
        // Contenu inchangé et aucune confirmation : rien n'est écrit (ni version, ni auteur, ni événement).
        if (changes.Count == 0 && !confirm)
        {
            return new(OpticalWriteStatus.Success, record);
        }

        var now = Now();
        if (changes.Count > 0)
        {
            record.EditDraft(content, userId, now, requestId);
            Record(record, "draft_updated", changes, userId, now);
        }

        if (confirm)
        {
            var confirmed = record.ConfirmDraft(userId, now, requestId);
            Record(record, "confirmed", [RevisionNo(confirmed.RevisionNo)], userId, now);
        }

        return await CommitAsync(record, cancellationToken);
    }

    /// <summary>Ouvre une rectification de la dernière révision confirmée (motif obligatoire ; un seul brouillon à la fois).</summary>
    public async Task<OpticalWriteResult<TRecord>> StartRectificationAsync(
        Guid id, int expectedRevisionNo, string? reasonCode, string? comment, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var (record, refusal) = await WritableAsync(id, version, requestId, cancellationToken);
        if (record is null)
        {
            return refusal!;
        }

        if (record.OpenDraft is not null)
        {
            return new(OpticalWriteStatus.DraftExists, record);
        }

        if (await IsClientArchivedAsync(record, cancellationToken))
        {
            return new(OpticalWriteStatus.ClientArchived, record);
        }

        if (record.LastConfirmed is not { } basis || basis.RevisionNo != expectedRevisionNo)
        {
            return new(OpticalWriteStatus.RevisionConflict, record);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (code, text) = OpticalInputValidator.Reason(reasonCode, comment, OpticalReasons.Rectification, errors);
        if (errors.Count > 0)
        {
            return new(OpticalWriteStatus.ValidationFailed, Errors: errors);
        }

        var now = Now();
        record.StartRectification(Guid.CreateVersion7(now), code!, text, userId, now, requestId);
        Record(record, "rectification_started", [BaseRevisionNo(basis.RevisionNo), Kept("rectificationReasonCode", code), .. Masked("comment", text)], userId, now);
        return await CommitAsync(record, cancellationToken);
    }

    /// <summary>Abandonne la rectification ouverte (conservée) ; une première saisie s'invalide, elle ne s'abandonne pas.</summary>
    public async Task<OpticalWriteResult<TRecord>> DiscardRectificationAsync(
        Guid id, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var (record, refusal) = await WritableAsync(id, version, requestId, cancellationToken);
        if (record is null)
        {
            return refusal!;
        }

        if (await IsClientArchivedAsync(record, cancellationToken))
        {
            return new(OpticalWriteStatus.ClientArchived, record);
        }

        if (record.OpenDraft is not { IsRectification: true } draft)
        {
            return new(OpticalWriteStatus.NotRectification, record);
        }

        var now = Now();
        record.DiscardRectification(userId, now, requestId);
        Record(record, "rectification_discarded", [BaseRevisionNo(draft.BaseRevisionNo)], userId, now);
        return await CommitAsync(record, cancellationToken);
    }

    /// <summary>Invalide l'enregistrement (motif obligatoire, aucune suppression) ; possible aussi pour un client archivé.</summary>
    public async Task<OpticalWriteResult<TRecord>> InvalidateAsync(
        Guid id, string? reasonCode, string? comment, uint version, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var record = await store.FindForUpdateAsync(id, cancellationToken);
        if (record is null)
        {
            return new(OpticalWriteStatus.NotFound);
        }

        if (record.LastRequestId == requestId)
        {
            return new(OpticalWriteStatus.Success, record, Replayed: true);
        }

        if (record.IsInvalidated)
        {
            return new(OpticalWriteStatus.AlreadyInvalidated, record);
        }

        if (record.Version != version)
        {
            return new(OpticalWriteStatus.VersionConflict, record);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (code, text) = OpticalInputValidator.Reason(reasonCode, comment, OpticalReasons.Invalidation, errors);
        if (errors.Count > 0)
        {
            return new(OpticalWriteStatus.ValidationFailed, Errors: errors);
        }

        var now = Now();
        var discarded = record.Invalidate(code!, text, userId, now, requestId);
        if (discarded is not null)
        {
            Record(record, "rectification_discarded", [BaseRevisionNo(discarded.BaseRevisionNo)], userId, now);
        }

        Record(record, "invalidated", [Kept("invalidationReasonCode", code), .. Masked("comment", text)], userId, now);
        return await CommitAsync(record, cancellationToken);
    }

    /// <summary>Préalables communs : existence, réémission reconnue, enregistrement actif, version attendue.</summary>
    private async Task<(TRecord? Record, OpticalWriteResult<TRecord>? Refusal)> WritableAsync(
        Guid id, uint version, Guid requestId, CancellationToken cancellationToken)
    {
        var record = await store.FindForUpdateAsync(id, cancellationToken);
        if (record is null)
        {
            return (null, new(OpticalWriteStatus.NotFound));
        }

        if (record.LastRequestId == requestId)
        {
            return (null, new(OpticalWriteStatus.Success, record, Replayed: true));
        }

        if (record.IsInvalidated)
        {
            return (null, new(OpticalWriteStatus.Invalidated, record));
        }

        return record.Version != version ? (null, new(OpticalWriteStatus.VersionConflict, record)) : (record, null);
    }

    private async Task<OpticalWriteResult<TRecord>?> ReplayCreationAsync(Guid clientId, Guid requestId, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await store.FindByCreationRequestAsync(requestId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        // Identifiant réutilisé pour une AUTRE création (autre client ou autre auteur) : refus, rien n'est lu ni écrit.
        return existing.ClientId == clientId && existing.CreatedByUserId == userId
            ? new(OpticalWriteStatus.Success, existing, Replayed: true)
            : new(OpticalWriteStatus.RequestIdReused);
    }

    private async Task<bool> IsClientArchivedAsync(TRecord record, CancellationToken cancellationToken) =>
        (await clients.GetAsync(record.ClientId, cancellationToken))?.IsArchived ?? false;

    private async Task<OpticalWriteResult<TRecord>> CommitAsync(TRecord record, CancellationToken cancellationToken) =>
        await store.SaveChangesAsync(cancellationToken) == OpticalCommitResult.Saved
            ? new(OpticalWriteStatus.Success, record)
            : new(OpticalWriteStatus.VersionConflict);

    /// <summary>Groupes modifiés, MASQUÉS : jamais un nom de champ optique, une valeur, un prescripteur ni une note.</summary>
    private List<AuditChange> ContentGroups(TContent? before, TContent after)
    {
        var groups = new List<AuditChange>();
        if (before is null ? HasProvenance(after) : !ProvenanceEquals(before, after))
        {
            groups.Add(AuditFieldPolicy.Change("provenance", null, null));
        }

        if (before is null ? !after.Values.IsEmpty : before.Values != after.Values)
        {
            groups.Add(AuditFieldPolicy.Change("values", null, null));
        }

        if (before is null ? after.Notes is not null : before.Notes != after.Notes)
        {
            groups.Add(AuditFieldPolicy.Change("notes", null, null));
        }

        return groups;
    }

    private static AuditChange RevisionNo(int? value) => Kept("revisionNo", value?.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static AuditChange BaseRevisionNo(int? value) => Kept("baseRevisionNo", value?.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static AuditChange Kept(string field, string? value) => AuditFieldPolicy.Change(field, null, value);

    private static AuditChange[] Masked(string field, string? value) => value is null ? [] : [AuditFieldPolicy.Change(field, null, null)];

    private void Record(TRecord record, string verb, IReadOnlyList<AuditChange> changes, Guid userId, DateTimeOffset now) =>
        audit.Record(new AuditRecord(
            userId, now, record.ClientId, AuditActions.OpticalModule, AuditActions.Optical(EntityType, verb), EntityType, record.Id, changes));

    // Précision PostgreSQL (microseconde) : la valeur renvoyée est identique à celle relue ensuite.
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }
}

/// <summary>Ordonnances médicales (droit <c>optical.prescriptions.write</c>, contrôlé par MMV.Api).</summary>
public sealed class PrescriptionService(IOpticalRecordStore<Prescription> store, IClientStore clients, TimeProvider clock, IAuditRecorder audit)
    : OpticalRecordService<Prescription, PrescriptionRevision, PrescriptionContent, PrescriptionInput>(store, clients, clock, audit)
{
    protected override string EntityType => AuditActions.PrescriptionEntity;

    protected override OpticalValidation<PrescriptionContent> Validate(PrescriptionInput input) => OpticalInputValidator.Validate(input);

    protected override IReadOnlyDictionary<string, string[]> CheckConfirmation(PrescriptionContent content) => OpticalConfirmationRules.Check(content);

    protected override Prescription CreateRecord(Guid id, Guid clientId, PrescriptionContent content, Guid revisionId, Guid requestId, Guid userId, DateTimeOffset now) =>
        Prescription.Create(id, clientId, content, revisionId, requestId, userId, now);

    protected override bool ProvenanceEquals(PrescriptionContent left, PrescriptionContent right) => left.Provenance == right.Provenance;

    protected override bool HasProvenance(PrescriptionContent content) => content.Provenance != PrescriptionProvenance.Empty;
}

/// <summary>
/// Mesures magasin (droit <c>optical.measurements.write</c>). Le collaborateur désigné comme ayant mesuré doit être un
/// compte actif détenant <c>optical.measurements.write</c> au moment où il est désigné (D-04-03.b) ; l'auteur de
/// l'enregistrement reste le compte connecté.
/// </summary>
public sealed class MeasurementService(
    IOpticalRecordStore<Measurement> store, IClientStore clients, IOpticalDirectory directory, TimeProvider clock, IAuditRecorder audit)
    : OpticalRecordService<Measurement, MeasurementRevision, MeasurementContent, MeasurementInput>(store, clients, clock, audit)
{
    protected override string EntityType => AuditActions.MeasurementEntity;

    protected override OpticalValidation<MeasurementContent> Validate(MeasurementInput input) => OpticalInputValidator.Validate(input);

    protected override IReadOnlyDictionary<string, string[]> CheckConfirmation(MeasurementContent content) => OpticalConfirmationRules.Check(content);

    protected override Measurement CreateRecord(Guid id, Guid clientId, MeasurementContent content, Guid revisionId, Guid requestId, Guid userId, DateTimeOffset now) =>
        Measurement.Create(id, clientId, content, revisionId, requestId, userId, now);

    protected override bool ProvenanceEquals(MeasurementContent left, MeasurementContent right) => left.Provenance == right.Provenance;

    protected override bool HasProvenance(MeasurementContent content) => content.Provenance != MeasurementProvenance.Empty;

    protected override async Task<IReadOnlyDictionary<string, string[]>> CheckReferencesAsync(
        MeasurementContent content, MeasurementContent? previous, CancellationToken cancellationToken)
    {
        var measurer = content.Provenance.MeasuredByUserId;
        // Collaborateur inchangé (rectification d'une mesure ancienne) : sa désignation passée n'est pas remise en cause.
        if (measurer is null || measurer == previous?.Provenance.MeasuredByUserId || await directory.IsEligibleMeasurerAsync(measurer.Value, cancellationToken))
        {
            return new Dictionary<string, string[]>();
        }

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["provenance.measuredByUserId"] = [OpticalFieldErrors.NotEligible] };
    }
}
