using MMV.Domain.Audit;
using MMV.Domain.Optical;

namespace MMV.Application.Optical;

public sealed record OpticalActorView(Guid Id, string DisplayName);

/// <summary>Client d'un enregistrement : référence et nom affiché seulement (aucune autre donnée de la fiche).</summary>
public sealed record OpticalClientView(Guid Id, string Reference, string DisplayName, bool IsArchived);

/// <summary>Statut affiché, DÉRIVÉ du cycle et des révisions (jamais stocké).</summary>
public enum OpticalDisplayStatus
{
    Draft,
    Confirmed,
    Rectification,
    Invalidated,
}

public enum OpticalStatusFilter
{
    All,
    Draft,
    Confirmed,
    Rectification,
}

public enum OpticalSortField
{
    Date,
    Client,
    Recorded,
}

/// <summary>Critères de liste déjà validés (liste blanche appliquée par MMV.Api).</summary>
public sealed record OpticalListQuery(
    string? Search, OpticalKind? Kind, OpticalStatusFilter Status, bool IncludeInvalidated, OpticalSortField Sort, bool Descending, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 100_000;
    public const int SearchMaxLength = 100;

    /// <summary>Mots de recherche pris en compte (deux caractères au moins, comme le prototype 04-A).</summary>
    public const int SearchWordMinLength = 2;
}

/// <summary>
/// Ligne de liste : révision AFFICHÉE = dernière confirmée, sinon brouillon de première saisie. Date et source de cette
/// révision (prescription / prescripteur, ou mesure / collaborateur ayant mesuré), valeurs OD/OG telles que saisies.
/// </summary>
public sealed record OpticalSummaryView(
    OpticalKind Kind,
    Guid Id,
    string Reference,
    OpticalClientView Client,
    OpticalDisplayStatus Status,
    int? RevisionNo,
    DateOnly? Date,
    string? Source,
    OpticalValues Values,
    DateTimeOffset RecordedAtUtc);

public sealed record OpticalSummaryPage(IReadOnlyList<OpticalSummaryView> Items, int Page, int PageSize, int TotalCount);

/// <summary>Lecture du dossier optique (droits <c>clients.read</c> + <c>optical.read</c>, contrôlés par MMV.Api).</summary>
public interface IOpticalReadStore
{
    Task<OpticalSummaryPage> ListAsync(OpticalListQuery query, CancellationToken cancellationToken);

    /// <summary>Enregistrements d'un client, du plus récent au plus ancien (révision affichée).</summary>
    Task<IReadOnlyList<OpticalSummaryView>> ListForClientAsync(Guid clientId, bool includeInvalidated, CancellationToken cancellationToken);

    /// <summary>Lecture seule, avec toutes les révisions.</summary>
    Task<Prescription?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken);

    Task<Measurement?> GetMeasurementAsync(Guid id, CancellationToken cancellationToken);

    Task<OpticalClientView?> GetClientAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>Noms affichés ACTUELS des comptes cités (auteurs, collaborateurs ayant mesuré).</summary>
    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Événements d'audit d'un enregistrement, dans l'ordre chronologique (servis avec <c>audit.read</c> seulement).</summary>
    Task<IReadOnlyList<AuditEvent>> EventsAsync(string entityType, Guid entityId, CancellationToken cancellationToken);
}

/// <summary>Règles de présentation communes (statut dérivé, révision affichée).</summary>
public static class OpticalPresentation
{
    public static OpticalDisplayStatus Status(bool invalidated, bool hasConfirmed, bool hasDraft) =>
        invalidated ? OpticalDisplayStatus.Invalidated
        : !hasConfirmed ? OpticalDisplayStatus.Draft
        : hasDraft ? OpticalDisplayStatus.Rectification
        : OpticalDisplayStatus.Confirmed;

    public static bool Matches(OpticalDisplayStatus status, OpticalStatusFilter filter, bool includeInvalidated) =>
        status == OpticalDisplayStatus.Invalidated
            ? includeInvalidated
            : filter switch
            {
                OpticalStatusFilter.Draft => status == OpticalDisplayStatus.Draft,
                OpticalStatusFilter.Confirmed => status == OpticalDisplayStatus.Confirmed,
                OpticalStatusFilter.Rectification => status == OpticalDisplayStatus.Rectification,
                _ => true,
            };

    /// <summary>Nom affiché d'un client, comme le poste (<c>NOM Prénom</c>).</summary>
    public static string ClientDisplayName(string firstName, string lastName) => $"{lastName.ToUpperInvariant()} {firstName}";
}

/// <summary>Lecture du dossier optique.</summary>
public sealed class OpticalQueryService(IOpticalReadStore store, IOpticalDirectory directory)
{
    public Task<OpticalSummaryPage> ListAsync(OpticalListQuery query, CancellationToken cancellationToken) => store.ListAsync(query, cancellationToken);

    public Task<IReadOnlyList<OpticalSummaryView>> ListForClientAsync(Guid clientId, bool includeInvalidated, CancellationToken cancellationToken) =>
        store.ListForClientAsync(clientId, includeInvalidated, cancellationToken);

    public Task<Prescription?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken) => store.GetPrescriptionAsync(id, cancellationToken);

    public Task<Measurement?> GetMeasurementAsync(Guid id, CancellationToken cancellationToken) => store.GetMeasurementAsync(id, cancellationToken);

    public Task<OpticalClientView?> GetClientAsync(Guid clientId, CancellationToken cancellationToken) => store.GetClientAsync(clientId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        store.DisplayNamesAsync(userIds, cancellationToken);

    public Task<IReadOnlyList<AuditEvent>> EventsAsync(string entityType, Guid entityId, CancellationToken cancellationToken) =>
        store.EventsAsync(entityType, entityId, cancellationToken);

    public Task<IReadOnlyList<OpticalActorView>> ListMeasurersAsync(CancellationToken cancellationToken) => directory.ListMeasurersAsync(cancellationToken);
}
