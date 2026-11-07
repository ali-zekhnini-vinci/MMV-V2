using System.Globalization;
using System.Text;

namespace MMV.Application.Catalog;

/// <summary>
/// Lecture CSV stricte (D-05-18) : texte UTF-8 (marque d'ordre d'octets retirée), séparateur détecté sur l'en-tête parmi
/// <c>;</c> (attendu), <c>,</c> et tabulation, guillemets doubles RFC 4180. Toutes les valeurs restent des CHAÎNES
/// (aucune conversion numérique ici, jamais de flottant). Lignes entièrement vides ignorées.
/// </summary>
public sealed class CsvTable
{
    /// <summary>Séparateurs admis, par ordre de préférence (le point-virgule l'emporte à égalité).</summary>
    // UX-IA : barre verticale « | » ajoutée (exports de caisse ou d'ERP fournisseurs), dernière par préférence.
    private static readonly char[] Delimiters = [';', ',', '\t', '|'];

    private CsvTable(char delimiter, IReadOnlyList<string> headers, IReadOnlyList<(int LineNo, string[] Cells)> rows)
    {
        Delimiter = delimiter;
        Headers = headers;
        Rows = rows;
    }

    public char Delimiter { get; }

    public IReadOnlyList<string> Headers { get; }

    /// <summary>Lignes de données ; <c>LineNo</c> = numéro de ligne du fichier (l'en-tête est la ligne 1).</summary>
    public IReadOnlyList<(int LineNo, string[] Cells)> Rows { get; }

    public static (CsvTable? Table, string? Error) Parse(string content, int maxRows)
    {
        var text = content.Length > 0 && content[0] == '﻿' ? content[1..] : content;
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, "empty_file");
        }

        var delimiter = DetectDelimiter(text);
        var records = new List<(int LineNo, List<string> Cells)>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var line = 1;
        var recordLine = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0)
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\r')
            {
                continue;
            }
            else if (c == '\n')
            {
                cells.Add(cell.ToString());
                cell.Clear();
                records.Add((recordLine, cells));
                cells = [];
                line++;
                recordLine = line;
                if (records.Count > maxRows + 1)
                {
                    return (null, "too_many_rows");
                }
            }
            else
            {
                cell.Append(c);
            }
        }

        if (quoted)
        {
            return (null, "unterminated_quote");
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            records.Add((recordLine, cells));
        }

        records.RemoveAll(r => r.Cells.All(string.IsNullOrWhiteSpace));
        if (records.Count == 0)
        {
            return (null, "empty_file");
        }

        var headers = records[0].Cells.Select(h => h.Trim()).ToList();
        if (headers.Any(string.IsNullOrEmpty) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Count)
        {
            return (null, "invalid_header");
        }

        if (records.Count - 1 > maxRows)
        {
            return (null, "too_many_rows");
        }

        var rows = records.Skip(1).Select(r => (r.LineNo, r.Cells.ToArray())).ToList();
        if (rows.Any(r => r.Item2.Length > headers.Count))
        {
            return (null, "too_many_columns");
        }

        return (new CsvTable(delimiter, headers, rows), null);
    }

    /// <summary>
    /// Séparateur (COR-05-04, UX-IA) : parmi « ; », « , », tabulation et « | », celui qui découpe les 20 premières lignes en un nombre
    /// de colonnes CONSTANT et le plus grand (guillemets respectés) ; à égalité, l'ordre de préférence. Un fichier d'une
    /// seule colonne garde « ; ».
    /// </summary>
    public static char DetectDelimiter(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).Take(20).ToList();
        static int Count(string line, char delimiter)
        {
            var quoted = false;
            var count = 0;
            foreach (var c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == delimiter && !quoted)
                {
                    count++;
                }
            }

            return count;
        }

        var best = Delimiters[0];
        var bestScore = -1;
        foreach (var delimiter in Delimiters)
        {
            var counts = lines.Select(l => Count(l, delimiter)).ToList();
            if (counts.Count == 0 || counts[0] == 0)
            {
                continue;
            }

            var score = (counts.Count(c => c == counts[0]) * 1000) + counts[0];
            if (score > bestScore)
            {
                (best, bestScore) = (delimiter, score);
            }
        }

        return best;
    }

    /// <summary>Normalisation d'un nom de colonne pour la correspondance automatique (minuscules, sans accents ni séparateurs).</summary>
    public static string NormalizeHeader(string header)
    {
        var decomposed = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Cellule pouvant être interprétée comme une formule par un tableur (<c>=</c>, <c>@</c>, ou <c>+</c> / <c>-</c> suivi
    /// d'une lettre) : refusée à l'import (neutralisation, D-05-18).
    /// </summary>
    public static bool LooksLikeFormula(string value) =>
        value.Length > 0 && (value[0] is '=' or '@' || (value[0] is '+' or '-' && value.Length > 1 && char.IsLetter(value[1])));
}
