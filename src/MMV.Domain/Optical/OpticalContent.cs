namespace MMV.Domain.Optical;

/// <summary>
/// Contenu saisissable d'une révision, déjà rogné et validé dans sa FORME (MMV.Application) ; champs facultatifs à
/// <c>null</c>. Deux contenus distincts (D-04-01 APPROVED) : une ordonnance médicale reçue n'est jamais une mesure
/// réalisée au magasin, et aucune opération ne convertit l'une en l'autre.
/// </summary>
public abstract record OpticalContent(OpticalValues Values, string? Notes);

/// <summary>
/// Provenance d'une ordonnance médicale : nom et date de prescription exigés pour CONFIRMER, facultatifs au brouillon ;
/// qualité du prescripteur facultative et libre (aucune liste fermée) ; date de réception au magasin distincte de la
/// date de prescription. L'auteur de la saisie (compte MMV) est enregistré par le serveur, jamais saisi.
/// </summary>
public sealed record PrescriptionProvenance(
    string? PrescriberName,
    string? PrescriberQualification,
    string? PrescriberIdentifier,
    DateOnly? PrescribedOn,
    DateOnly? ReceivedOn)
{
    public static PrescriptionProvenance Empty { get; } = new(null, null, null, null, null);
}

/// <summary>
/// Provenance d'une mesure magasin : date de mesure et collaborateur l'ayant RÉELLEMENT réalisée exigés pour CONFIRMER,
/// facultatifs au brouillon ; catégorie facultative et libre (aucune liste fermée). L'auteur de l'enregistrement, rempli
/// par le serveur, peut différer du collaborateur ayant mesuré.
/// </summary>
public sealed record MeasurementProvenance(DateOnly? MeasuredOn, Guid? MeasuredByUserId, string? Category)
{
    public static MeasurementProvenance Empty { get; } = new(null, null, null);
}

public sealed record PrescriptionContent(PrescriptionProvenance Provenance, OpticalValues Values, string? Notes)
    : OpticalContent(Values, Notes);

public sealed record MeasurementContent(MeasurementProvenance Provenance, OpticalValues Values, string? Notes)
    : OpticalContent(Values, Notes);

/// <summary>Bornes techniques des textes (colonnes) ; aucune n'est une règle clinique.</summary>
public static class OpticalLimits
{
    public const int ReferenceMaxLength = 24;
    public const int TextMaxLength = 120;
    public const int NotesMaxLength = 2000;
    public const int CommentMaxLength = 500;
    public const int CodeMaxLength = 32;
    public const int StatusMaxLength = 16;

    /// <summary>Date métier la plus ancienne admise (garde technique contre une saisie aberrante).</summary>
    public static readonly DateOnly MinDate = new(1900, 1, 1);
}
