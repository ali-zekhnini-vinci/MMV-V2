using System.Globalization;
using MMV.Domain.Appointments;

namespace MMV.Application.Appointments;

/// <summary>
/// Règles horaires D-03-13, évaluées dans le fuseau du MAGASIN (jamais celui du poste) : début au pas de cinq
/// minutes, aucun rendez-vous à cheval sur minuit, aucun créneau touché par un changement d'heure (heure locale
/// inexistante ou ambiguë). Les instants échangés sont UTC ; un instant UTC désigne toujours une heure murale
/// unique, mais un créneau peut commencer ou finir dans une heure ambiguë (retour à l'heure d'hiver) ou enjamber
/// une heure inexistante (passage à l'heure d'été) : il est alors refusé.
/// </summary>
public static class AppointmentSchedule
{
    public const string InvalidStep = "invalid_step";
    public const string OutOfRange = "out_of_range";
    public const string TimeChange = "time_change";

    private static readonly string[] InstantFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
    ];

    /// <summary>Instant ISO 8601 explicitement UTC (<c>…Z</c> ou <c>…+00:00</c>) ; tout autre décalage est refusé.</summary>
    public static bool TryParseUtcInstant(string? value, out DateTimeOffset instant)
    {
        instant = default;
        if (value is null || !(value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal)))
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(value, InstantFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            return false;
        }

        instant = parsed;
        return true;
    }

    /// <summary>Code de refus du créneau [<paramref name="start"/>, <paramref name="end"/>[, ou <c>null</c>.</summary>
    public static string? CheckSlot(TimeZoneInfo zone, DateTimeOffset start, DateTimeOffset end)
    {
        var localStart = TimeZoneInfo.ConvertTime(start, zone);
        if (localStart.Ticks % TimeSpan.TicksPerMinute != 0 || localStart.Minute % AppointmentRules.StartStepMinutes != 0)
        {
            return InvalidStep;
        }

        if (zone.IsAmbiguousTime(start) || zone.IsAmbiguousTime(end) || zone.GetUtcOffset(start) != zone.GetUtcOffset(end))
        {
            return TimeChange;
        }

        var localEnd = TimeZoneInfo.ConvertTime(end, zone);
        var sameDay = localEnd.Date == localStart.Date;
        var endsAtMidnight = localEnd.Date == localStart.Date.AddDays(1) && localEnd.TimeOfDay == TimeSpan.Zero;
        return sameDay || endsAtMidnight ? null : OutOfRange;
    }
}
