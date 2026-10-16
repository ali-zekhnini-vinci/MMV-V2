using System.Globalization;
using System.Text;

namespace MMV.Domain.Clients;

/// <summary>
/// Recherche Clients insensible à la casse et aux accents (D-03-04), sans extension PostgreSQL : le texte
/// est normalisé par l'application (décomposition NFD, marques diacritiques retirées, minuscules
/// invariantes) dans la colonne <c>search_text</c>, puis comparé par <c>LIKE</c> à la requête normalisée
/// de la même manière. Même repliement que la présentation côté interface (<c>apps/desktop/src/clients/searchText.ts</c>).
/// </summary>
public static class ClientSearch
{
    /// <summary>Repliement : sans accents, en minuscules, espaces multiples réduits, rogné.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Chiffres seuls (recherche par téléphone quel que soit le format saisi).</summary>
    public static string Digits(string? value) =>
        value is null ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    /// <summary>Contenu de <c>search_text</c> : prénom, nom, email repliés, puis chiffres du téléphone.</summary>
    public static string BuildSearchText(string firstName, string lastName, string? email, string? phone)
    {
        string[] parts = [Fold(firstName), Fold(lastName), Fold(email), Digits(phone)];
        var text = string.Join(' ', parts.Where(p => p.Length > 0));
        return text.Length <= Client.SearchTextMaxLength ? text : text[..Client.SearchTextMaxLength];
    }
}
