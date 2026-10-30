using System.Globalization;
using System.Text.RegularExpressions;

namespace MMV.Domain.Catalog;

/// <summary>Politique de stock d'une VARIANTE (D-05-02 corrigée par Q04).</summary>
public enum StockPolicy
{
    /// <summary>Stock suivi par emplacement (quantités, mouvements, seuils, inventaires).</summary>
    Stocked,

    /// <summary>Commandé à la demande : aucune quantité, aucun mouvement, exclu des inventaires.</summary>
    OrderOnly,

    /// <summary>Prestation non stockable.</summary>
    Service,
}

public static class StockPolicies
{
    public const string Stocked = "stocked";
    public const string OrderOnly = "order_only";
    public const string Service = "service";

    public static string ToCode(StockPolicy policy) => policy switch
    {
        StockPolicy.Stocked => Stocked,
        StockPolicy.OrderOnly => OrderOnly,
        _ => Service,
    };

    public static StockPolicy FromCode(string code) =>
        TryParse(code) ?? throw new ArgumentException("Politique de stock inconnue.", nameof(code));

    public static StockPolicy? TryParse(string? code) => code switch
    {
        Stocked => StockPolicy.Stocked,
        OrderOnly => StockPolicy.OrderOnly,
        Service => StockPolicy.Service,
        _ => null,
    };
}

/// <summary>Nature d'un attribut de famille ; toutes les valeurs sont conservées en chaînes (jamais de flottant).</summary>
public enum AttributeKind
{
    Text,
    WholeNumber,
    DecimalNumber,
}

/// <summary>Attribut défini dans le code (D-05-05 : jeux d'attributs définis dans le code, validés par le serveur).</summary>
public sealed record AttributeDefinition(string Key, string Label, AttributeKind Kind, int MaxLength = 60, int Min = 0, int Max = 0);

/// <summary>Famille de produits (liste fermée de six, Q01).</summary>
public sealed record FamilyDefinition(
    string Code,
    string Label,
    int SortOrder,
    IReadOnlyList<StockPolicy> AllowedPolicies,
    IReadOnlyList<AttributeDefinition> ModelAttributes,
    IReadOnlyList<AttributeDefinition> VariantAttributes)
{
    /// <summary>Verres correcteurs : gamme obligatoire au modèle, indice et traitements portés par la variante (D-05-21).</summary>
    public bool IsOphthalmicLens => Code == Families.OphthalmicLenses;
}

/// <summary>
/// Les six familles approuvées (Q01, D-05-05). Les jeux d'attributs et les politiques admises par famille sont un choix
/// TECHNIQUE conservateur (documenté dans docs/phase/05/DECISIONS_TECHNIQUES_05.md, non ratifié) : tous les attributs
/// sont facultatifs ; aucune valeur pré-remplie.
/// </summary>
public static class Families
{
    public const string OpticalFrames = "optical_frames";
    public const string Sunglasses = "sunglasses";
    public const string OphthalmicLenses = "ophthalmic_lenses";
    public const string ContactLensesSolutions = "contact_lenses_solutions";
    public const string Accessories = "accessories";
    public const string Services = "services";

    private static readonly StockPolicy[] Goods = [StockPolicy.Stocked, StockPolicy.OrderOnly];

    private static readonly AttributeDefinition[] FrameVariant =
    [
        new("color", "Coloris", AttributeKind.Text, MaxLength: 60),
        new("eyeSize", "Calibre (mm)", AttributeKind.WholeNumber, Min: 20, Max: 80),
        new("bridgeSize", "Pont (mm)", AttributeKind.WholeNumber, Min: 5, Max: 40),
        new("templeLength", "Branche (mm)", AttributeKind.WholeNumber, Min: 80, Max: 180),
    ];

    public static IReadOnlyList<FamilyDefinition> All { get; } =
    [
        new(OpticalFrames, "Montures optiques", 1, Goods,
            [new("material", "Matière", AttributeKind.Text), new("shape", "Forme", AttributeKind.Text)],
            FrameVariant),
        new(Sunglasses, "Lunettes de soleil", 2, Goods,
            [new("material", "Matière", AttributeKind.Text), new("shape", "Forme", AttributeKind.Text)],
            [.. FrameVariant, new("lensTint", "Teinte des verres", AttributeKind.Text)]),
        new(OphthalmicLenses, "Verres correcteurs", 3, Goods,
            [],
            [new("diameter", "Diamètre (mm)", AttributeKind.WholeNumber, Min: 40, Max: 90)]),
        new(ContactLensesSolutions, "Lentilles et solutions", 4, Goods,
            [new("replacement", "Renouvellement", AttributeKind.Text, MaxLength: 40)],
            [
                new("power", "Puissance", AttributeKind.DecimalNumber),
                new("baseCurve", "Rayon (mm)", AttributeKind.DecimalNumber),
                new("diameter", "Diamètre (mm)", AttributeKind.DecimalNumber),
                new("packSize", "Conditionnement (unités)", AttributeKind.WholeNumber, Min: 1, Max: 1000),
                new("volumeMl", "Volume (ml)", AttributeKind.WholeNumber, Min: 1, Max: 5000),
            ]),
        new(Accessories, "Accessoires", 5, Goods,
            [],
            [new("color", "Coloris", AttributeKind.Text)]),
        new(Services, "Prestations", 6, [StockPolicy.Service],
            [],
            []),
    ];

    public static FamilyDefinition? Find(string? code) => All.FirstOrDefault(f => f.Code == code);
}

/// <summary>Contrôle des valeurs d'attributs (chaînes ; entier ou décimal signé à 2 décimales au plus).</summary>
public static partial class AttributeValues
{
    [GeneratedRegex("^[-+]?[0-9]{1,4}([.][0-9]{1,2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalPattern();

    /// <summary>Valeur normalisée (rognée) ou <c>null</c> si invalide ; la chaîne vide n'est jamais admise ici.</summary>
    public static string? Normalize(AttributeDefinition definition, string value)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.Length > definition.MaxLength)
        {
            return null;
        }

        return definition.Kind switch
        {
            AttributeKind.WholeNumber => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number >= definition.Min && number <= definition.Max
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : null,
            AttributeKind.DecimalNumber => DecimalPattern().IsMatch(text) ? text : null,
            _ => text,
        };
    }
}

/// <summary>Code-barres GTIN facultatif (EAN-8, UPC-A, EAN-13, GTIN-14) : chiffres et clé de contrôle (D-05-14).</summary>
public static class Gtin
{
    public static bool IsValid(string? value)
    {
        if (value is not { Length: 8 or 12 or 13 or 14 } || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        // Pondération 3/1 en partant du chiffre précédant la clé (à droite).
        for (var i = value.Length - 2; i >= 0; i--)
        {
            var weight = (value.Length - 2 - i) % 2 == 0 ? 3 : 1;
            sum += (value[i] - '0') * weight;
        }

        return (10 - (sum % 10)) % 10 == value[^1] - '0';
    }
}

/// <summary>Normalisation des identifiants uniques (SKU, codes) : comparaison insensible à la casse et aux espaces de bord.</summary>
public static class CatalogKeys
{
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
