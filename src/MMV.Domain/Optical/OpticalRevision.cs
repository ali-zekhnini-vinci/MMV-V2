namespace MMV.Domain.Optical;

/// <summary>Statut d'une révision : brouillon (au plus un par enregistrement), confirmée (immuable), abandonnée (conservée).</summary>
public enum OpticalRevisionStatus
{
    Draft,
    Confirmed,
    Discarded,
}

/// <summary>Motifs codifiés (listes proposées D-04-04, reprises du prototype 04-A) ; le commentaire est exigé pour « other ».</summary>
public static class OpticalReasons
{
    public const string Other = "other";

    public static IReadOnlyList<string> Rectification { get; } = ["transcription_error", "additional_information", Other];

    public static IReadOnlyList<string> Invalidation { get; } = ["entry_error", "duplicate", "unacceptable_document", Other];

    /// <summary>Format des codes en base (contraintes <c>ck_*_reason_code_format</c>) : la liste vit dans le code.</summary>
    public const string CodeRegex = "^[a-z][a-z_]{0,31}$";
}

/// <summary>
/// Révision d'un enregistrement optique (mécanisme partagé par les deux types). Le numéro n'est attribué qu'à la
/// confirmation (1, 2, 3… sans trou) ; une révision confirmée ou abandonnée n'est plus jamais modifiée (déclencheur
/// PostgreSQL <c>trg_*_revisions_immutable</c>). Une rectification part d'une révision confirmée (<see cref="BaseRevisionNo"/>)
/// et porte un motif ; la révision de base reste intacte et reste la référence jusqu'à la confirmation suivante.
/// </summary>
public abstract class OpticalRevision
{
    protected OpticalRevision()
    {
    }

    public Guid Id { get; protected set; }

    public Guid RecordId { get; protected set; }

    public OpticalRevisionStatus Status { get; protected set; }

    public int? RevisionNo { get; protected set; }

    public int? BaseRevisionNo { get; protected set; }

    public string? ReasonCode { get; protected set; }

    public string? ReasonComment { get; protected set; }

    // Valeurs par œil, conservées telles que saisies (colonnes nullables une à une).
    public string? OdSphere { get; protected set; }

    public string? OdCylinder { get; protected set; }

    public string? OdAxis { get; protected set; }

    public string? OdAddition { get; protected set; }

    public string? OdPrism { get; protected set; }

    public string? OdBase { get; protected set; }

    public string? OdAcuity { get; protected set; }

    public string? OgSphere { get; protected set; }

    public string? OgCylinder { get; protected set; }

    public string? OgAxis { get; protected set; }

    public string? OgAddition { get; protected set; }

    public string? OgPrism { get; protected set; }

    public string? OgBase { get; protected set; }

    public string? OgAcuity { get; protected set; }

    public string? Notes { get; protected set; }

    public DateTimeOffset StartedAtUtc { get; protected set; }

    public Guid StartedByUserId { get; protected set; }

    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    public Guid UpdatedByUserId { get; protected set; }

    public DateTimeOffset? ConfirmedAtUtc { get; protected set; }

    public Guid? ConfirmedByUserId { get; protected set; }

    public DateTimeOffset? DiscardedAtUtc { get; protected set; }

    public Guid? DiscardedByUserId { get; protected set; }

    public bool IsDraft => Status == OpticalRevisionStatus.Draft;

    public bool IsRectification => BaseRevisionNo is not null;

    public OpticalValues Values => new(
        new EyeValues(OdSphere, OdCylinder, OdAxis, OdAddition, OdPrism, OdBase, OdAcuity),
        new EyeValues(OgSphere, OgCylinder, OgAxis, OgAddition, OgPrism, OgBase, OgAcuity));

    protected void Start(Guid id, Guid recordId, int? baseRevisionNo, string? reasonCode, string? reasonComment, Guid userId, DateTimeOffset now)
    {
        Id = id;
        RecordId = recordId;
        Status = OpticalRevisionStatus.Draft;
        BaseRevisionNo = baseRevisionNo;
        ReasonCode = reasonCode;
        ReasonComment = reasonComment;
        StartedAtUtc = now;
        StartedByUserId = userId;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
    }

    protected void ApplyCommon(OpticalContent content, Guid userId, DateTimeOffset now)
    {
        EnsureDraft();
        (OdSphere, OdCylinder, OdAxis, OdAddition, OdPrism, OdBase, OdAcuity) = content.Values.Od;
        (OgSphere, OgCylinder, OgAxis, OgAddition, OgPrism, OgBase, OgAcuity) = content.Values.Og;
        Notes = content.Notes;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
    }

    internal void Confirm(int revisionNo, Guid userId, DateTimeOffset now)
    {
        EnsureDraft();
        Status = OpticalRevisionStatus.Confirmed;
        RevisionNo = revisionNo;
        ConfirmedAtUtc = now;
        ConfirmedByUserId = userId;
    }

    internal void Discard(Guid userId, DateTimeOffset now)
    {
        EnsureDraft();
        if (!IsRectification)
        {
            throw new InvalidOperationException("Une première saisie ne s'abandonne pas : elle s'invalide.");
        }

        Status = OpticalRevisionStatus.Discarded;
        DiscardedAtUtc = now;
        DiscardedByUserId = userId;
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("Révision figée : seule une révision en brouillon est modifiable.");
        }
    }
}

/// <summary>Révision typée par son contenu (ordonnance ou mesure).</summary>
public abstract class OpticalRevision<TContent> : OpticalRevision
    where TContent : OpticalContent
{
    public abstract TContent Content { get; }

    internal void Edit(TContent content, Guid userId, DateTimeOffset now)
    {
        ApplyCommon(content, userId, now);
        ApplyProvenance(content);
    }

    protected abstract void ApplyProvenance(TContent content);
}

/// <summary>Révision d'une ordonnance médicale (provenance du prescripteur).</summary>
public sealed class PrescriptionRevision : OpticalRevision<PrescriptionContent>
{
    private PrescriptionRevision()
    {
    }

    public string? PrescriberName { get; private set; }

    public string? PrescriberQualification { get; private set; }

    public string? PrescriberIdentifier { get; private set; }

    public DateOnly? PrescribedOn { get; private set; }

    public DateOnly? ReceivedOn { get; private set; }

    public override PrescriptionContent Content =>
        new(new PrescriptionProvenance(PrescriberName, PrescriberQualification, PrescriberIdentifier, PrescribedOn, ReceivedOn), Values, Notes);

    internal static PrescriptionRevision Create(
        Guid id, Guid recordId, PrescriptionContent content, int? baseRevisionNo, string? reasonCode, string? reasonComment, Guid userId, DateTimeOffset now)
    {
        var revision = new PrescriptionRevision();
        revision.Start(id, recordId, baseRevisionNo, reasonCode, reasonComment, userId, now);
        revision.Edit(content, userId, now);
        return revision;
    }

    protected override void ApplyProvenance(PrescriptionContent content) =>
        (PrescriberName, PrescriberQualification, PrescriberIdentifier, PrescribedOn, ReceivedOn) = content.Provenance;
}

/// <summary>Révision d'une mesure magasin (date et collaborateur ayant mesuré).</summary>
public sealed class MeasurementRevision : OpticalRevision<MeasurementContent>
{
    private MeasurementRevision()
    {
    }

    public DateOnly? MeasuredOn { get; private set; }

    public Guid? MeasuredByUserId { get; private set; }

    public string? Category { get; private set; }

    public override MeasurementContent Content => new(new MeasurementProvenance(MeasuredOn, MeasuredByUserId, Category), Values, Notes);

    internal static MeasurementRevision Create(
        Guid id, Guid recordId, MeasurementContent content, int? baseRevisionNo, string? reasonCode, string? reasonComment, Guid userId, DateTimeOffset now)
    {
        var revision = new MeasurementRevision();
        revision.Start(id, recordId, baseRevisionNo, reasonCode, reasonComment, userId, now);
        revision.Edit(content, userId, now);
        return revision;
    }

    protected override void ApplyProvenance(MeasurementContent content) =>
        (MeasuredOn, MeasuredByUserId, Category) = content.Provenance;
}

/// <summary>Codes stockés du statut d'une révision (contrainte <c>ck_*_revisions_status</c>).</summary>
public static class OpticalRevisionStatusCodes
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
    public const string Discarded = "discarded";

    public static string ToCode(OpticalRevisionStatus status) =>
        status == OpticalRevisionStatus.Confirmed ? Confirmed : status == OpticalRevisionStatus.Discarded ? Discarded : Draft;

    public static OpticalRevisionStatus FromCode(string code) =>
        code == Confirmed ? OpticalRevisionStatus.Confirmed
        : code == Discarded ? OpticalRevisionStatus.Discarded
        : code == Draft ? OpticalRevisionStatus.Draft
        : throw new FormatException("Statut de révision inconnu.");
}
