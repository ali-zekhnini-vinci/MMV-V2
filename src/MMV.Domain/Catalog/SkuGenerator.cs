using System.Security.Cryptography;

namespace MMV.Domain.Catalog;

/// <summary>
/// SKU interne généré par le SERVEUR quand aucune saisie n'est fournie (COR-05-06, clôt O-05-20) : préfixe de famille
/// + 6 symboles sans caractères ambigus (0/O, 1/I/L exclus) tirés par le générateur cryptographique. L'unicité reste
/// garantie par la contrainte <c>ux_product_variants_sku</c> ; l'appelant vérifie les SKU déjà pris avant d'écrire.
/// </summary>
public static class SkuGenerator
{
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static readonly IReadOnlyDictionary<string, string> Prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["optical_frames"] = "MON",
        ["sunglasses"] = "SOL",
        ["ophthalmic_lenses"] = "VER",
        ["contact_lenses_solutions"] = "LEN",
        ["accessories"] = "ACC",
        ["services"] = "PRE",
    };

    public static string Propose(string? familyCode)
    {
        Span<char> body = stackalloc char[6];
        for (var i = 0; i < body.Length; i++)
        {
            body[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{(familyCode is not null && Prefixes.TryGetValue(familyCode, out var prefix) ? prefix : "ART")}-{body}";
    }
}
