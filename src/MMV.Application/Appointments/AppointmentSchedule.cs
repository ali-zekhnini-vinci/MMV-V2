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

    /// <summary>
    /// Heure murale du magasin <c>AAAA-MM-JJTHH:mm</c> que le SERVEUR associe à <paramref name="instant"/> (données de
    /// fuseaux de Windows). Comparée à l'heure saisie par le poste (B2-REV-03) : un écart signale des données de
    /// fuseaux divergentes entre le serveur et le poste (O-03-22), qui décaleraient l'heure réelle du rendez-vous.
    /// </summary>
    public static string LocalWallTime(TimeZoneInfo zone, DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, zone).ToString(LocalWallTimeFormat, CultureInfo.InvariantCulture);

    public const string LocalWallTimeFormat = "yyyy-MM-dd'T'HH:mm";

    public const string TimeZoneMismatch = "time_zone_mismatch";

    /// <summary>Heure murale stricte <c>AAAA-MM-JJTHH:mm</c> (ni secondes, ni décalage, ni 24:00).</summary>
    public static bool IsLocalWallTime(string? value) =>
        value is { Length: 16 }
        && DateTime.TryParseExact(value, LocalWallTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static readonly TimeSpan OffsetScanStep = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Décalages UTC du magasin sur [<paramref name="fromUtc"/>, <paramref name="toUtc"/>[ selon le SERVEUR : segments
    /// contigus, triés, chacun à décalage constant, bornes exactes (recherche dichotomique au tick près autour de chaque
    /// changement détecté par pas de 15 minutes). Servis avec la liste des rendez-vous : l'interface vérifie que ses
    /// propres données de fuseaux (ICU de WebView2) donnent les mêmes décalages sur la période affichée, sinon elle
    /// bloque l'agenda de cette période (B2-REV-03).
    /// </summary>
    public static IReadOnlyList<UtcOffsetSegment> OffsetSegments(TimeZoneInfo zone, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fromUtc, toUtc);
        var segments = new List<UtcOffsetSegment>();
        var segmentStart = fromUtc;
        var current = zone.GetUtcOffset(fromUtc);
        var cursor = fromUtc;
        while (cursor < toUtc)
        {
            var next = cursor + OffsetScanStep < toUtc ? cursor + OffsetScanStep : toUtc;
            var probe = next < toUtc ? next : toUtc.AddTicks(-1);
            if (zone.GetUtcOffset(probe) == current)
            {
                cursor = next;
                continue;
            }

            // Premier instant de ]cursor, probe] au nouveau décalage.
            var low = cursor;
            var high = probe;
            while (high - low > TimeSpan.FromTicks(1))
            {
                var middle = low + TimeSpan.FromTicks((high - low).Ticks / 2);
                if (zone.GetUtcOffset(middle) == current)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            segments.Add(new UtcOffsetSegment(segmentStart, high, (int)current.TotalMinutes));
            segmentStart = high;
            current = zone.GetUtcOffset(high);
            cursor = high;
        }

        segments.Add(new UtcOffsetSegment(segmentStart, toUtc, (int)current.TotalMinutes));
        return segments;
    }
}

/// <summary>Décalage UTC constant du magasin sur [<see cref="FromUtc"/>, <see cref="ToUtc"/>[, en minutes.</summary>
public readonly record struct UtcOffsetSegment(DateTimeOffset FromUtc, DateTimeOffset ToUtc, int OffsetMinutes);
