namespace MMV.Domain.Appointments;

/// <summary>
/// Mode d'organisation imposé par le motif (D-03-14) : <see cref="Personal"/> = rendez-vous personnalisé,
/// responsable obligatoire ; <see cref="Reception"/> = rendez-vous d'accueil, responsable facultatif (sans
/// responsable, le rendez-vous est imputé sur la capacité d'accueil du magasin, D-03-15). L'accueil est un mode,
/// jamais un motif.
/// </summary>
public enum ResponsibleMode
{
    Personal,
    Reception,
}

public sealed record AppointmentReason(string Code, string Label, ResponsibleMode Mode)
{
    public bool ResponsibleRequired => Mode == ResponsibleMode.Personal;
}

/// <summary>
/// Liste fermée des motifs APPROUVÉE (D-03-18, 17/10/2026) : codes stables, libellés servis par l'API, motif
/// obligatoire, ordre d'affichage = ordre de la liste. Aucune table de personnalisation en V1. Répartition des modes
/// approuvée par D-03-14.
/// </summary>
public static class AppointmentReasons
{
    public static IReadOnlyList<AppointmentReason> All { get; } =
    [
        new("conseil_devis", "Conseil / Devis", ResponsibleMode.Personal),
        new("choix_equipement", "Choix de monture et verres", ResponsibleMode.Personal),
        new("prise_mesures", "Prise de mesures", ResponsibleMode.Personal),
        new("essayage_ajustement", "Essayage / Ajustement", ResponsibleMode.Personal),
        new("retrait_commande", "Retrait de commande", ResponsibleMode.Reception),
        new("sav_reparation", "SAV / Réparation", ResponsibleMode.Reception),
        new("autre", "Autre motif", ResponsibleMode.Personal),
    ];

    public static AppointmentReason? Find(string? code) =>
        code is null ? null : All.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.Ordinal));
}

/// <summary>Règles fixes approuvées par D-03-13 (indépendantes du magasin).</summary>
public static class AppointmentRules
{
    /// <summary>Durées autorisées, en minutes, sans valeur par défaut.</summary>
    public static IReadOnlyList<int> DurationsMinutes { get; } = [15, 30, 45, 60, 90, 120];

    /// <summary>Le début d'un rendez-vous tombe sur un multiple de cinq minutes (heure du magasin).</summary>
    public const int StartStepMinutes = 5;
}
