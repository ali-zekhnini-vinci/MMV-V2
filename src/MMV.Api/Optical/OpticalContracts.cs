using System.Text.Json.Serialization;
using MMV.Application.Optical;
using MMV.Domain.Optical;

namespace MMV.Api.Optical;

// ---------- requêtes ----------

/// <summary>Valeurs d'un œil : chaînes telles que saisies ; absente, <c>null</c> ou vide = non renseignée (jamais 0).</summary>
internal sealed record EyeValuesRequest(
    string? Sphere = null,
    string? Cylinder = null,
    string? Axis = null,
    string? Addition = null,
    string? Prism = null,
    string? Base = null,
    string? Acuity = null)
{
    public EyeInput ToInput() => new(Sphere, Cylinder, Axis, Addition, Prism, Base, Acuity);
}

/// <summary>Provenance d'une ordonnance (dates <c>AAAA-MM-JJ</c>) ; tout est facultatif au brouillon.</summary>
internal sealed record PrescriptionProvenanceRequest(
    string? PrescriberName = null,
    string? PrescriberQualification = null,
    string? PrescriberIdentifier = null,
    string? PrescribedOn = null,
    string? ReceivedOn = null);

/// <summary>Provenance d'une mesure magasin (date <c>AAAA-MM-JJ</c>, compte du collaborateur ayant mesuré).</summary>
internal sealed record MeasurementProvenanceRequest(string? MeasuredOn = null, Guid? MeasuredByUserId = null, string? Category = null);

/// <summary>Contenu d'une ordonnance médicale (aucun champ « type » : la route le porte).</summary>
internal sealed record PrescriptionContentRequest(
    PrescriptionProvenanceRequest? Provenance = null, EyeValuesRequest? Od = null, EyeValuesRequest? Og = null, string? Notes = null)
{
    public PrescriptionInput ToInput() => new(
        Provenance is null ? null : new PrescriptionProvenanceInput(
            Provenance.PrescriberName, Provenance.PrescriberQualification, Provenance.PrescriberIdentifier, Provenance.PrescribedOn, Provenance.ReceivedOn),
        Od?.ToInput(),
        Og?.ToInput(),
        Notes);
}

/// <summary>Contenu d'une mesure magasin.</summary>
internal sealed record MeasurementContentRequest(
    MeasurementProvenanceRequest? Provenance = null, EyeValuesRequest? Od = null, EyeValuesRequest? Og = null, string? Notes = null)
{
    public MeasurementInput ToInput() => new(
        Provenance is null ? null : new MeasurementProvenanceInput(Provenance.MeasuredOn, Provenance.MeasuredByUserId, Provenance.Category),
        Od?.ToInput(),
        Og?.ToInput(),
        Notes);
}

/// <summary>
/// Création : identifiant de requête (idempotence, obligatoire), client, contenu ; <c>confirm</c> vrai = créer ET
/// confirmer en une transaction (« Enregistrer ») — refus de confirmation = rien n'est créé.
/// </summary>
internal sealed record OpticalCreateRequest<TContent>(Guid? RequestId = null, Guid? ClientId = null, bool? Confirm = null, TContent? Content = null)
    where TContent : class;

/// <summary>Brouillon : identifiant de requête, version lue, contenu (facultatif pour une confirmation sans modification).</summary>
internal sealed record OpticalDraftRequest<TContent>(Guid? RequestId = null, uint? Version = null, TContent? Content = null)
    where TContent : class;

/// <summary>Ouverture d'une rectification de la révision confirmée attendue (motif codifié, commentaire exigé pour « other »).</summary>
internal sealed record OpticalRectificationRequest(
    Guid? RequestId = null, uint? Version = null, int? ExpectedRevisionNo = null, string? ReasonCode = null, string? Comment = null);

/// <summary>Abandon de la rectification ouverte.</summary>
internal sealed record OpticalVersionRequest(Guid? RequestId = null, uint? Version = null);

/// <summary>Invalidation motivée (aucune suppression).</summary>
internal sealed record OpticalInvalidationRequest(Guid? RequestId = null, uint? Version = null, string? ReasonCode = null, string? Comment = null);

// ---------- réponses ----------

internal sealed record OpticalActorResponse(Guid Id, string DisplayName);

internal sealed record OpticalClientResponse(Guid Id, string Reference, string DisplayName, OpticalClientStatusValue Status);

internal sealed record EyeValuesResponse(
    string? Sphere, string? Cylinder, string? Axis, string? Addition, string? Prism, string? Base, string? Acuity)
{
    public static EyeValuesResponse From(EyeValues values) =>
        new(values.Sphere, values.Cylinder, values.Axis, values.Addition, values.Prism, values.Base, values.Acuity);
}

internal sealed record PrescriptionProvenanceResponse(
    string? PrescriberName, string? PrescriberQualification, string? PrescriberIdentifier, DateOnly? PrescribedOn, DateOnly? ReceivedOn)
{
    public static PrescriptionProvenanceResponse From(PrescriptionProvenance p) =>
        new(p.PrescriberName, p.PrescriberQualification, p.PrescriberIdentifier, p.PrescribedOn, p.ReceivedOn);
}

internal sealed record MeasurementProvenanceResponse(DateOnly? MeasuredOn, Guid? MeasuredByUserId, string? Category)
{
    public static MeasurementProvenanceResponse From(MeasurementProvenance p) => new(p.MeasuredOn, p.MeasuredByUserId, p.Category);
}

internal sealed record OpticalContentResponse<TProvenance>(TProvenance Provenance, EyeValuesResponse Od, EyeValuesResponse Og, string? Notes);

/// <summary>Révision : numéro attribué à la confirmation seulement ; auteurs et dates de chaque étape.</summary>
internal sealed record OpticalRevisionResponse<TProvenance>(
    Guid Id,
    RevisionStatusValue Status,
    int? RevisionNo,
    int? BaseRevisionNo,
    string? ReasonCode,
    string? ReasonComment,
    OpticalContentResponse<TProvenance> Content,
    DateTimeOffset StartedAtUtc,
    OpticalActorResponse StartedBy,
    DateTimeOffset UpdatedAtUtc,
    OpticalActorResponse UpdatedBy,
    DateTimeOffset? ConfirmedAtUtc,
    OpticalActorResponse? ConfirmedBy,
    DateTimeOffset? DiscardedAtUtc,
    OpticalActorResponse? DiscardedBy);

internal sealed record OpticalInvalidationResponse(DateTimeOffset AtUtc, OpticalActorResponse By, string ReasonCode, string? Comment, bool DiscardedDraft);

/// <summary>Événement de gestion (code d'action, auteur, numéro de révision ou motif codifié ; aucune valeur optique).</summary>
internal sealed record OpticalEventResponse(Guid Id, DateTimeOffset AtUtc, string Code, OpticalActorResponse Actor, int? RevisionNo, string? ReasonCode);

/// <summary>
/// Enregistrement. <c>historyAvailable</c> faux (session sans <c>audit.read</c>) : <c>revisions</c> se limite à la
/// dernière révision confirmée et au brouillon ouvert, <c>events</c> est vide.
/// <c>citedUsers</c> (04-C, O-04-48) : comptes cités dans la provenance des révisions servies (collaborateur ayant mesuré),
/// avec leur nom, y compris s'ils ne sont plus actifs ni habilités (la liste <c>/measurers</c> ne contient que les actifs).
/// </summary>
internal sealed record OpticalRecordResponse<TProvenance>(
    OpticalKindValue Kind,
    Guid Id,
    string Reference,
    OpticalClientResponse Client,
    OpticalLifecycleValue Lifecycle,
    OpticalInvalidationResponse? Invalidation,
    OpticalRevisionResponse<TProvenance>? LastConfirmed,
    OpticalRevisionResponse<TProvenance>? OpenDraft,
    IReadOnlyList<OpticalRevisionResponse<TProvenance>> Revisions,
    bool HistoryAvailable,
    IReadOnlyList<OpticalEventResponse> Events,
    IReadOnlyList<OpticalActorResponse> CitedUsers,
    DateTimeOffset CreatedAtUtc,
    OpticalActorResponse CreatedBy,
    uint Version);

/// <summary>Ligne de liste : révision affichée (dernière confirmée, sinon brouillon), statut dérivé.</summary>
internal sealed record OpticalSummaryResponse(
    OpticalKindValue Kind,
    Guid Id,
    string Reference,
    OpticalClientResponse Client,
    DisplayStatusValue Status,
    int? RevisionNo,
    DateOnly? Date,
    string? Source,
    EyeValuesResponse Od,
    EyeValuesResponse Og,
    DateTimeOffset RecordedAtUtc);

internal sealed record OpticalSummaryPageResponse(IReadOnlyList<OpticalSummaryResponse> Items, int Page, int PageSize, int TotalCount);

[JsonConverter(typeof(JsonStringEnumConverter<OpticalKindValue>))]
internal enum OpticalKindValue
{
    [JsonStringEnumMemberName("prescription")]
    Prescription,

    [JsonStringEnumMemberName("measurement")]
    Measurement,
}

[JsonConverter(typeof(JsonStringEnumConverter<OpticalLifecycleValue>))]
internal enum OpticalLifecycleValue
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("invalidated")]
    Invalidated,
}

[JsonConverter(typeof(JsonStringEnumConverter<RevisionStatusValue>))]
internal enum RevisionStatusValue
{
    [JsonStringEnumMemberName("draft")]
    Draft,

    [JsonStringEnumMemberName("confirmed")]
    Confirmed,

    [JsonStringEnumMemberName("discarded")]
    Discarded,
}

[JsonConverter(typeof(JsonStringEnumConverter<DisplayStatusValue>))]
internal enum DisplayStatusValue
{
    [JsonStringEnumMemberName("draft")]
    Draft,

    [JsonStringEnumMemberName("confirmed")]
    Confirmed,

    [JsonStringEnumMemberName("rectification")]
    Rectification,

    [JsonStringEnumMemberName("invalidated")]
    Invalidated,
}

[JsonConverter(typeof(JsonStringEnumConverter<OpticalClientStatusValue>))]
internal enum OpticalClientStatusValue
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("archived")]
    Archived,
}
