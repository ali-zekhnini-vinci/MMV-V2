namespace MMV.Domain.Optical;

/// <summary>Type d'enregistrement optique (D-04-01 APPROVED) : jamais un simple champ modifiable, aucune conversion.</summary>
public enum OpticalKind
{
    Prescription,
    Measurement,
}

/// <summary>
/// En-tête d'un enregistrement optique (mécanisme partagé ; tables distinctes par type). Cycle : actif ou invalidé
/// (motif obligatoire, aucune suppression physique). Statut affiché DÉRIVÉ des révisions : brouillon (aucune révision
/// confirmée), confirmé, rectification en cours (brouillon ouvert après une confirmation), invalidé.
/// Concurrence : <see cref="Version"/> = colonne système <c>xmin</c> ; CHAQUE écriture touche l'en-tête (auteur, date,
/// <see cref="LastRequestId"/>), donc toute écriture concurrente sur le même enregistrement est détectée.
/// Idempotence : <see cref="CreationRequestId"/> (unique) et <see cref="LastRequestId"/> identifient la dernière requête
/// appliquée ; sa réémission (double clic, délai réseau) relit l'état sans rien réécrire.
/// </summary>
public abstract class OpticalRecord<TRevision, TContent>
    where TRevision : OpticalRevision<TContent>
    where TContent : OpticalContent
{
    private readonly List<TRevision> _revisions = [];

    protected OpticalRecord()
    {
        Reference = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Référence lisible attribuée par PostgreSQL à l'insertion (séquence) ; vide avant l'enregistrement.</summary>
    public string Reference { get; private set; }

    public Guid ClientId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset? InvalidatedAtUtc { get; private set; }

    public Guid? InvalidatedByUserId { get; private set; }

    public string? InvalidationReasonCode { get; private set; }

    public string? InvalidationComment { get; private set; }

    public Guid CreationRequestId { get; private set; }

    public Guid LastRequestId { get; private set; }

    /// <summary>Jeton de concurrence optimiste : colonne système <c>xmin</c> (aucune colonne créée).</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<TRevision> Revisions => _revisions;

    public bool IsInvalidated => InvalidatedAtUtc is not null;

    public TRevision? OpenDraft => _revisions.SingleOrDefault(r => r.IsDraft);

    public TRevision? LastConfirmed =>
        _revisions.Where(r => r.Status == OpticalRevisionStatus.Confirmed).MaxBy(r => r.RevisionNo);

    /// <summary>Révisions dans l'ordre chronologique (début, puis identifiant UUIDv7).</summary>
    public IReadOnlyList<TRevision> Chronological => [.. _revisions.OrderBy(r => r.StartedAtUtc).ThenBy(r => r.Id)];

    protected abstract TRevision NewRevision(
        Guid id, TContent content, int? baseRevisionNo, string? reasonCode, string? reasonComment, Guid userId, DateTimeOffset now);

    protected void Initialize(Guid id, Guid clientId, TContent content, Guid revisionId, Guid requestId, Guid userId, DateTimeOffset now)
    {
        Id = id;
        ClientId = clientId;
        CreatedAtUtc = now;
        CreatedByUserId = userId;
        CreationRequestId = requestId;
        Touch(userId, now, requestId);
        _revisions.Add(NewRevision(revisionId, content, null, null, null, userId, now));
    }

    /// <summary>Remplace le contenu du brouillon ouvert.</summary>
    public TRevision EditDraft(TContent content, Guid userId, DateTimeOffset now, Guid requestId)
    {
        EnsureActive();
        var draft = OpenDraft ?? throw new InvalidOperationException("Aucun brouillon ouvert.");
        draft.Edit(content, userId, now);
        Touch(userId, now, requestId);
        return draft;
    }

    /// <summary>Confirme le brouillon ouvert : numéro = dernière révision confirmée + 1 (séquence sans trou).</summary>
    public TRevision ConfirmDraft(Guid userId, DateTimeOffset now, Guid requestId)
    {
        EnsureActive();
        var draft = OpenDraft ?? throw new InvalidOperationException("Aucun brouillon ouvert.");
        if (draft.BaseRevisionNo != LastConfirmed?.RevisionNo)
        {
            throw new InvalidOperationException("La révision de base n'est plus la dernière révision confirmée.");
        }

        draft.Confirm((LastConfirmed?.RevisionNo ?? 0) + 1, userId, now);
        Touch(userId, now, requestId);
        return draft;
    }

    /// <summary>Ouvre une rectification : nouveau brouillon reprenant le contenu de la dernière révision confirmée.</summary>
    public TRevision StartRectification(Guid revisionId, string reasonCode, string? reasonComment, Guid userId, DateTimeOffset now, Guid requestId)
    {
        EnsureActive();
        if (OpenDraft is not null)
        {
            throw new InvalidOperationException("Un brouillon est déjà ouvert.");
        }

        var basis = LastConfirmed ?? throw new InvalidOperationException("Aucune révision confirmée à rectifier.");
        var draft = NewRevision(revisionId, basis.Content, basis.RevisionNo, reasonCode, reasonComment, userId, now);
        _revisions.Add(draft);
        Touch(userId, now, requestId);
        return draft;
    }

    /// <summary>Abandonne la rectification ouverte (conservée, statut « abandonnée ») ; la référence est inchangée.</summary>
    public TRevision DiscardRectification(Guid userId, DateTimeOffset now, Guid requestId)
    {
        EnsureActive();
        var draft = OpenDraft ?? throw new InvalidOperationException("Aucun brouillon ouvert.");
        draft.Discard(userId, now);
        Touch(userId, now, requestId);
        return draft;
    }

    /// <summary>
    /// Invalide l'enregistrement (motif obligatoire, aucune suppression). Une rectification ouverte est abandonnée dans la
    /// même opération et renvoyée ; un brouillon de première saisie reste conservé tel quel, figé avec l'enregistrement.
    /// </summary>
    public TRevision? Invalidate(string reasonCode, string? comment, Guid userId, DateTimeOffset now, Guid requestId)
    {
        EnsureActive();
        var draft = OpenDraft;
        var discarded = draft is { IsRectification: true } ? draft : null;
        discarded?.Discard(userId, now);
        InvalidatedAtUtc = now;
        InvalidatedByUserId = userId;
        InvalidationReasonCode = reasonCode;
        InvalidationComment = comment;
        Touch(userId, now, requestId);
        return discarded;
    }

    private void EnsureActive()
    {
        if (IsInvalidated)
        {
            throw new InvalidOperationException("Enregistrement invalidé : aucune modification.");
        }
    }

    private void Touch(Guid userId, DateTimeOffset now, Guid requestId)
    {
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }
}

/// <summary>Ordonnance médicale reçue et transcrite (D-04-01). L'auteur de la saisie n'en devient pas le prescripteur.</summary>
public sealed class Prescription : OpticalRecord<PrescriptionRevision, PrescriptionContent>
{
    private Prescription()
    {
    }

    public static Prescription Create(Guid id, Guid clientId, PrescriptionContent content, Guid revisionId, Guid requestId, Guid userId, DateTimeOffset now)
    {
        var record = new Prescription();
        record.Initialize(id, clientId, content, revisionId, requestId, userId, now);
        return record;
    }

    protected override PrescriptionRevision NewRevision(
        Guid id, PrescriptionContent content, int? baseRevisionNo, string? reasonCode, string? reasonComment, Guid userId, DateTimeOffset now) =>
        PrescriptionRevision.Create(id, Id, content, baseRevisionNo, reasonCode, reasonComment, userId, now);
}

/// <summary>Mesure optique réalisée au magasin (D-04-01) : jamais présentée comme une prescription médicale.</summary>
public sealed class Measurement : OpticalRecord<MeasurementRevision, MeasurementContent>
{
    private Measurement()
    {
    }

    public static Measurement Create(Guid id, Guid clientId, MeasurementContent content, Guid revisionId, Guid requestId, Guid userId, DateTimeOffset now)
    {
        var record = new Measurement();
        record.Initialize(id, clientId, content, revisionId, requestId, userId, now);
        return record;
    }

    protected override MeasurementRevision NewRevision(
        Guid id, MeasurementContent content, int? baseRevisionNo, string? reasonCode, string? reasonComment, Guid userId, DateTimeOffset now) =>
        MeasurementRevision.Create(id, Id, content, baseRevisionNo, reasonCode, reasonComment, userId, now);
}
