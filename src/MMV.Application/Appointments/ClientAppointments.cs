using System.Linq.Expressions;
using MMV.Domain.Appointments;

namespace MMV.Application.Appointments;

/// <summary>
/// Historique des rendez-vous d'un client (03-D1, onglet « Rendez-vous » de la fiche). À venir : planifiés non encore
/// terminés (en cours compris) ; passés : planifiés terminés ; annulés ; tous. « Maintenant » est l'horloge du serveur.
/// </summary>
public enum ClientAppointmentScope
{
    Upcoming,
    Past,
    Cancelled,
    All,
}

/// <summary>Critères déjà validés (liste blanche appliquée par MMV.Api). Tri par début puis identifiant.</summary>
public sealed record ClientAppointmentQuery(Guid ClientId, ClientAppointmentScope Scope, bool Descending, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 100_000;

    /// <summary>Ordre par défaut : à venir du plus proche au plus lointain ; passés, annulés et tous du plus récent au plus ancien.</summary>
    public static bool DefaultDescending(ClientAppointmentScope scope) => scope != ClientAppointmentScope.Upcoming;
}

public sealed record ClientAppointmentCounts(int Upcoming, int Past, int Cancelled);

public sealed record ClientAppointmentPage(IReadOnlyList<AppointmentView> Items, int Page, int PageSize, int TotalCount, ClientAppointmentCounts Counts);

/// <summary>Filtres partagés par la persistance PostgreSQL (traduits en SQL) et les doubles de test (compilés).</summary>
public static class ClientAppointmentFilters
{
    public static Expression<Func<Appointment, bool>> For(Guid clientId, ClientAppointmentScope scope, DateTimeOffset now) => scope switch
    {
        ClientAppointmentScope.Upcoming => a => a.ClientId == clientId && a.Status == Appointment.StatusPlanned && a.EndsAtUtc > now,
        ClientAppointmentScope.Past => a => a.ClientId == clientId && a.Status == Appointment.StatusPlanned && a.EndsAtUtc <= now,
        ClientAppointmentScope.Cancelled => a => a.ClientId == clientId && a.Status == Appointment.StatusCancelled,
        _ => a => a.ClientId == clientId,
    };
}
