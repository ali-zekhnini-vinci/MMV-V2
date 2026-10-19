namespace MMV.Domain.Appointments;

/// <summary>
/// Occupation de la capacité d'accueil (D-03-15) : nombre MAXIMAL de rendez-vous non attribués simultanés à un
/// instant quelconque d'un créneau. Un simple décompte des rendez-vous qui recoupent le créneau serait faux :
/// 10:00–10:30 et 10:30–11:00 recoupent tous deux 10:00–11:00 sans jamais être simultanés (occupation 1, pas 2).
/// Balayage des bornes, intervalles semi-ouverts : à un même instant, une fin est traitée avant un début.
/// </summary>
public static class ReceptionOccupancy
{
    public static int MaxConcurrent(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> intervals, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var events = new List<(DateTimeOffset At, int Delta)>();
        foreach (var (start, end) in intervals)
        {
            // Partie de l'intervalle comprise dans le créneau ; rien si l'intersection est vide.
            var from = start > windowStart ? start : windowStart;
            var to = end < windowEnd ? end : windowEnd;
            if (from < to)
            {
                events.Add((from, +1));
                events.Add((to, -1));
            }
        }

        events.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Delta.CompareTo(b.Delta));
        int current = 0, max = 0;
        foreach (var (_, delta) in events)
        {
            current += delta;
            max = Math.Max(max, current);
        }

        return max;
    }
}
