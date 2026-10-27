using System.Globalization;
using System.Text.RegularExpressions;

namespace MMV.Domain.Optical;

/// <summary>
/// Valeurs optiques d'un œil (phase 04-B ; D-04-16 APPROVED : aucun « état de l'œil »). Chaque valeur est facultative :
/// <c>null</c> = non renseignée, JAMAIS confondue avec <c>0</c> (<c>"0,00"</c> est une valeur explicite). Les valeurs
/// sont conservées EXACTEMENT telles que saisies (signe, séparateur, zéros de fin) : aucune normalisation, aucun arrondi,
/// aucune transposition, aucune conversion d'axe (0° ↔ 180°), aucun remplissage de l'autre œil.
/// </summary>
public sealed record EyeValues(
    string? Sphere,
    string? Cylinder,
    string? Axis,
    string? Addition,
    string? Prism,
    string? Base,
    string? Acuity)
{
    public static EyeValues Empty { get; } = new(null, null, null, null, null, null, null);

    /// <summary>Valeurs dans l'ordre de <see cref="OpticalFields.Names"/>.</summary>
    public IReadOnlyList<string?> InOrder => [Sphere, Cylinder, Axis, Addition, Prism, Base, Acuity];

    public bool IsEmpty => InOrder.All(value => value is null);

    public static EyeValues FromOrdered(IReadOnlyList<string?> values) =>
        values.Count == OpticalFields.Names.Count
            ? new(values[0], values[1], values[2], values[3], values[4], values[5], values[6])
            : throw new ArgumentException("Nombre de valeurs d'œil inattendu.", nameof(values));
}

/// <summary>Valeurs des deux yeux : OD (œil droit) et OG (œil gauche), indépendantes l'une de l'autre.</summary>
public sealed record OpticalValues(EyeValues Od, EyeValues Og)
{
    public static OpticalValues Empty { get; } = new(EyeValues.Empty, EyeValues.Empty);

    public bool IsEmpty => Od.IsEmpty && Og.IsEmpty;

    public EyeValues Of(EyeSide side) => side == EyeSide.Od ? Od : Og;
}

public enum EyeSide
{
    Od,
    Og,
}

/// <summary>
/// Champs d'un œil et forme TECHNIQUE des valeurs (jamais une règle clinique : aucune borne, aucun pas, aucun format
/// d'ordonnance présumé — D-04-06, avis d'un opticien requis). Valeurs numériques (Sphère, Cylindre, Axe, Addition,
/// Prisme) : nombre décimal signé, point ou virgule, sans exposant ni séparateur de milliers ; Base et Acuité : texte
/// court libre (conventions à vérifier avec un opticien).
/// </summary>
public static partial class OpticalFields
{
    public const string Sphere = "sphere";
    public const string Cylinder = "cylinder";
    public const string Axis = "axis";
    public const string Addition = "addition";
    public const string Prism = "prism";
    public const string Base = "base";
    public const string Acuity = "acuity";

    /// <summary>Longueur maximale d'une valeur d'œil (contrainte technique de colonne, pas une borne clinique).</summary>
    public const int ValueMaxLength = 16;

    public static IReadOnlyList<string> Names { get; } = [Sphere, Cylinder, Axis, Addition, Prism, Base, Acuity];

    /// <summary>Champs dont la valeur doit être un nombre décimal (colonne numérique exacte dérivée en base).</summary>
    public static IReadOnlySet<string> Numeric { get; } = new HashSet<string>(StringComparer.Ordinal) { Sphere, Cylinder, Axis, Addition, Prism };

    public static string SideCode(EyeSide side) => side == EyeSide.Od ? "od" : "og";

    /// <summary>
    /// Forme d'un nombre : signe facultatif (<c>+</c>, <c>-</c> ou le signe moins typographique U+2212), une espace
    /// facultative, des chiffres ASCII, une partie décimale facultative après <c>.</c> ou <c>,</c>. Même expression que
    /// les contraintes <c>ck_*_number_format</c> de PostgreSQL.
    /// </summary>
    public const string NumberRegex = "^[-+−]? ?[0-9]+([.,][0-9]+)?$";

    [GeneratedRegex(NumberRegex, RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    public static bool IsNumber(string value) => NumberPattern().IsMatch(value);

    /// <summary>
    /// Valeur décimale EXACTE d'une saisie de forme valide (aucun arrondi : 16 caractères au plus, donc 15 chiffres au
    /// plus, dans la précision de <see cref="decimal"/>). Utilisée par les contrôles de confirmation (non nul).
    /// </summary>
    public static decimal ToDecimal(string value)
    {
        if (!IsNumber(value))
        {
            throw new FormatException("Valeur optique non numérique.");
        }

        var invariant = value.Replace(',', '.').Replace('−', '-').Replace(" ", string.Empty, StringComparison.Ordinal);
        return decimal.Parse(invariant, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }
}
