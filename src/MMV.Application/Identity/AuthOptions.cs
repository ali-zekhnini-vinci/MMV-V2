namespace MMV.Application.Identity;

/// <summary>
/// Paramètres de session et de verrouillage (variante DEV PROVISOIRE, ADR-007 PROPOSED).
/// Section de configuration : <c>Auth</c>.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Durée de vie absolue d'une session.</summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Inactivité au-delà de laquelle la session est refusée.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Écart minimal entre deux mises à jour de <c>last_seen_at_utc</c> (limite les écritures).</summary>
    public TimeSpan TouchThreshold { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Échecs consécutifs avant verrouillage temporaire du compte.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
