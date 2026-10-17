using System.Globalization;

namespace MMV.Application.Appointments;

/// <summary>
/// Paramètres du magasin pour les rendez-vous (section de configuration <c>Appointments</c>), propres à
/// l'installation : une installation MMV = un magasin en V1. Aucun n'a de valeur par défaut inventée (D-03-13) :
/// tant que le fuseau et la plage affichée ne sont pas explicitement configurés, le service Rendez-vous répond
/// 503 <c>appointments_not_configured</c> (les autres modules restent disponibles).
/// </summary>
public sealed class AppointmentsOptions
{
    public const string SectionName = "Appointments";

    /// <summary>Identifiant IANA du fuseau du magasin (<see cref="AppointmentSettings.SupportedTimeZones"/>).</summary>
    public string? TimeZone { get; set; }

    /// <summary>Début de la plage AFFICHÉE de l'agenda (<c>HH:mm</c>), non bloquant.</summary>
    public string? DisplayStartTime { get; set; }

    /// <summary>Fin de la plage AFFICHÉE (<c>HH:mm</c>, <c>24:00</c> admis), non bloquante.</summary>
    public string? DisplayEndTime { get; set; }

    /// <summary>Jours masqués de la grille (0 = dimanche … 6 = samedi), affichage seulement ; aucun par défaut.</summary>
    public int[]? HiddenWeekdays { get; set; }

    /// <summary>
    /// Capacité d'accueil (rendez-vous non attribués simultanés, D-03-15). Absente : aucune réservation sans
    /// responsable. Jamais illimitée.
    /// </summary>
    public int? ReceptionCapacity { get; set; }
}

/// <summary>Paramètres validés une fois au démarrage ; <see cref="Problems"/> non vide = service non configuré.</summary>
public sealed class AppointmentSettings
{
    /// <summary>Fuseaux pris en charge (D-03-13 : Maroc et Belgique).</summary>
    public static IReadOnlyList<string> SupportedTimeZones { get; } = ["Africa/Casablanca", "Europe/Brussels"];

    private AppointmentSettings(
        IReadOnlyList<string> problems, TimeZoneInfo? timeZone, string displayStartTime, string displayEndTime,
        IReadOnlyList<int> hiddenWeekdays, int? receptionCapacity)
    {
        Problems = problems;
        TimeZone = timeZone;
        DisplayStartTime = displayStartTime;
        DisplayEndTime = displayEndTime;
        HiddenWeekdays = hiddenWeekdays;
        ReceptionCapacity = receptionCapacity;
    }

    public IReadOnlyList<string> Problems { get; }

    public bool IsConfigured => Problems.Count == 0;

    /// <summary>Fuseau du magasin ; non nul si <see cref="IsConfigured"/>.</summary>
    public TimeZoneInfo? TimeZone { get; }

    public string TimeZoneId => TimeZone?.Id ?? string.Empty;

    public string DisplayStartTime { get; }

    public string DisplayEndTime { get; }

    public IReadOnlyList<int> HiddenWeekdays { get; }

    public int? ReceptionCapacity { get; }

    public static AppointmentSettings NotConfigured(string problem) => new([problem], null, string.Empty, string.Empty, [], null);

    public static AppointmentSettings From(AppointmentsOptions? options)
    {
        var problems = new List<string>();
        options ??= new AppointmentsOptions();

        TimeZoneInfo? zone = null;
        var zoneId = options.TimeZone?.Trim();
        if (string.IsNullOrEmpty(zoneId))
        {
            problems.Add("Appointments:TimeZone absent (fuseau IANA du magasin à configurer explicitement).");
        }
        else if (!SupportedTimeZones.Contains(zoneId, StringComparer.Ordinal))
        {
            problems.Add($"Appointments:TimeZone non pris en charge (admis : {string.Join(", ", SupportedTimeZones)}).");
        }
        else
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                problems.Add("Appointments:TimeZone inconnu du système (données de fuseaux absentes).");
            }
        }

        var start = ParseDisplayTime(options.DisplayStartTime, allowEndOfDay: false);
        var end = ParseDisplayTime(options.DisplayEndTime, allowEndOfDay: true);
        if (start is null)
        {
            problems.Add("Appointments:DisplayStartTime absent ou invalide (HH:mm).");
        }

        if (end is null)
        {
            problems.Add("Appointments:DisplayEndTime absent ou invalide (HH:mm, 24:00 admis).");
        }

        if (start is not null && end is not null && end <= start)
        {
            problems.Add("Appointments:DisplayEndTime doit suivre DisplayStartTime.");
        }

        var hidden = options.HiddenWeekdays ?? [];
        if (hidden.Any(day => day is < 0 or > 6) || hidden.Distinct().Count() != hidden.Length || hidden.Length >= 7)
        {
            problems.Add("Appointments:HiddenWeekdays invalide (jours 0 à 6, distincts, au moins un jour affiché).");
        }

        if (options.ReceptionCapacity is < 1)
        {
            problems.Add("Appointments:ReceptionCapacity invalide (entier ≥ 1, ou absente pour refuser les rendez-vous sans responsable).");
        }

        return new AppointmentSettings(
            problems, problems.Count == 0 ? zone : null,
            options.DisplayStartTime?.Trim() ?? string.Empty, options.DisplayEndTime?.Trim() ?? string.Empty,
            [.. hidden.Order()], options.ReceptionCapacity);
    }

    private static TimeSpan? ParseDisplayTime(string? value, bool allowEndOfDay)
    {
        var text = value?.Trim();
        if (allowEndOfDay && text == "24:00")
        {
            return TimeSpan.FromHours(24);
        }

        return text is { Length: 5 } && TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out var time)
            ? time
            : null;
    }
}
