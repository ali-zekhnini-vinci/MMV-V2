using System.Security.Cryptography;
using System.Text;
using MMV.Domain.CsvImport;

namespace MMV.Application.Catalog.Assisted;

/// <summary>Enregistrement brut d'un fichier : numéro de sa PREMIÈRE ligne physique, cellules brutes, anomalies de lecture.</summary>
public sealed record CsvRecord(int Line, string[] Cells, IReadOnlyList<string> Anomalies)
{
    public bool Empty => Cells.All(string.IsNullOrWhiteSpace);

    public string Cell(int column) => column < Cells.Length ? Cells[column] : string.Empty;
}

/// <summary>
/// Document lu par <see cref="TolerantCsvReader"/> : TOUS les enregistrements (aucune ligne retirée, vides comprises),
/// cellules brutes non décalées, anomalies par ligne (<c>fewer_cells</c>, <c>more_cells</c>, <c>empty_line</c>,
/// <c>multiline_cell</c>, <c>stray_quote</c>, <c>unterminated_quote</c>). <see cref="Columns"/> = largeur maximale,
/// <see cref="ModalColumns"/> = largeur la plus fréquente (référence d'alignement).
/// </summary>
public sealed record CsvDocument(string Text, string Encoding, char Delimiter, IReadOnlyList<CsvRecord> Records, int Columns, int ModalColumns, IReadOnlyList<string> Anomalies)
{
    public string DelimiterLabel => Delimiter == '\t' ? "tab" : Delimiter.ToString();

    /// <summary>Empreinte du TEXTE décodé (UTF-8) : identité métier du fichier (<c>imports.batches.file_sha256</c>, refus du réimport).</summary>
    public string TextSha256 => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Text)));
}

/// <summary>Résultat de l'inspection d'octets : document lu, ou refus TECHNIQUE (jamais métier).</summary>
public sealed record CsvInspection(CsvDocument? Document, string? Refusal, string Sha256, int ByteLength)
{
    public bool Accepted => Document is not null;
}

/// <summary>Refus techniques du dépôt (Gate B § 4.1) ; aucune anomalie métier n'en fait partie.</summary>
public static class CsvRefusals
{
    public const string TooLarge = "too_large";
    public const string TooManyRows = "too_many_rows";
    public const string EmptyFile = "empty_file";
    public const string BinaryContent = "binary_content";
    public const string SpreadsheetNotCsv = "spreadsheet_not_csv";
    public const string Undecodable = "undecodable";
}

/// <summary>
/// Lecteur CSV TOLÉRANT (C1.1), porté à l'identique du prototype des bancs Lot A / B0 (<c>TolerantReader</c>) : décodage
/// équivalent à <c>decodeCsv</c> du poste (BOM UTF-16, BOM UTF-8 retiré, UTF-8 strict, sinon Windows-1252), séparateur
/// <c>;</c> <c>,</c> tabulation <c>|</c>, guillemets RFC 4180 et cellules multilignes. Différence avec
/// <see cref="CsvTable"/> (conservé pour les fournisseurs et les prix) : rien n'est refusé pour une raison de contenu,
/// aucune ligne n'est retirée, aucune cellule n'est décalée. Seules les limites techniques refusent le fichier.
/// </summary>
public static class TolerantCsvReader
{
    private static readonly char[] Delimiters = [';', ',', '\t', '|'];

    static TolerantCsvReader() => System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Inspection des octets d'origine : limites techniques, décodage, lecture. Aucune écriture.</summary>
    public static CsvInspection Inspect(byte[] bytes)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (bytes.Length > ImportBatch.MaxBytes)
        {
            return new(null, CsvRefusals.TooLarge, sha, bytes.Length);
        }

        if (bytes.Length == 0)
        {
            return new(null, CsvRefusals.EmptyFile, sha, 0);
        }

        if (SpreadsheetSignature(bytes))
        {
            return new(null, CsvRefusals.SpreadsheetNotCsv, sha, bytes.Length);
        }

        var utf16 = bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF));
        if (!utf16 && (Array.IndexOf(bytes, (byte)0) >= 0 || ExecutableSignature(bytes)))
        {
            return new(null, CsvRefusals.BinaryContent, sha, bytes.Length);
        }

        if (utf16 && (bytes.Length % 2 != 0))
        {
            return new(null, CsvRefusals.Undecodable, sha, bytes.Length);
        }

        var (text, encoding) = Decode(bytes);
        if (utf16 && text.Contains('�', StringComparison.Ordinal))
        {
            return new(null, CsvRefusals.Undecodable, sha, bytes.Length);
        }

        if (ControlShare(text) > 0.01)
        {
            return new(null, CsvRefusals.BinaryContent, sha, bytes.Length);
        }

        if (text.Trim().Length == 0)
        {
            return new(null, CsvRefusals.EmptyFile, sha, bytes.Length);
        }

        if (System.Text.Encoding.UTF8.GetByteCount(text) > ImportBatch.MaxBytes)
        {
            // Le texte conservé (imports.batches.content) est lui aussi borné à 2 Mio (ck_batches_size).
            return new(null, CsvRefusals.TooLarge, sha, bytes.Length);
        }

        var document = Parse(text, encoding);
        return document.Records.Count > ImportBatch.MaxRows ? new(null, CsvRefusals.TooManyRows, sha, bytes.Length) : new(document, null, sha, bytes.Length);
    }

    /// <summary>Décodage identique au poste : BOM UTF-16 (LE/BE), BOM UTF-8 retiré, UTF-8 strict, sinon Windows-1252.</summary>
    public static (string Text, string Encoding) Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "utf-16le");
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "utf-16be");
        }

        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            return (text, bom ? "utf-8-bom" : "utf-8");
        }
        catch (DecoderFallbackException)
        {
            return (System.Text.Encoding.GetEncoding(1252).GetString(bytes), "windows-1252");
        }
    }

    /// <summary>Lecture tolérante d'un texte déjà décodé (texte conservé d'un lot : relecture déterministe).</summary>
    public static CsvDocument Parse(string text, string encoding)
    {
        var delimiter = DetectDelimiter(text);
        var records = new List<(int Line, string[] Cells, List<string> Anomalies)>();
        var anomalies = new List<string>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var line = 1;
        var start = 1;
        var recordAnomalies = new List<string>();
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
                        recordAnomalies.Add("multiline_cell");
                    }

                    if (c != '\r')
                    {
                        cell.Append(c);
                    }
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
            }
            else if (c == '\n')
            {
                cells.Add(cell.ToString());
                cell.Clear();
                records.Add((start, [.. cells], [.. recordAnomalies.Distinct()]));
                cells = [];
                recordAnomalies = [];
                line++;
                start = line;
            }
            else
            {
                if (c == '"')
                {
                    recordAnomalies.Add("stray_quote");
                }

                cell.Append(c);
            }
        }

        if (quoted)
        {
            anomalies.Add("unterminated_quote");
            recordAnomalies.Add("unterminated_quote");
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            records.Add((start, [.. cells], [.. recordAnomalies.Distinct()]));
        }

        static bool IsEmpty(string[] c) => c.All(string.IsNullOrWhiteSpace);
        var nonEmpty = records.Where(r => !IsEmpty(r.Cells)).ToList();
        var modal = nonEmpty.Count == 0 ? 0 : nonEmpty.GroupBy(r => r.Cells.Length).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
        var result = new List<CsvRecord>(records.Count);
        foreach (var (lineNo, recordCells, recordList) in records)
        {
            if (IsEmpty(recordCells))
            {
                recordList.Add("empty_line");
            }
            else if (recordCells.Length < modal)
            {
                recordList.Add("fewer_cells");
            }
            else if (recordCells.Length > modal)
            {
                recordList.Add("more_cells");
            }

            result.Add(new CsvRecord(lineNo, recordCells, recordList));
        }

        return new CsvDocument(text, encoding, delimiter, result, result.Count == 0 ? 0 : result.Max(r => r.Cells.Length), modal, anomalies);
    }

    /// <summary>Séparateur : mode du nombre de séparateurs par ligne (20 premières lignes non vides), le plus fréquent puis le plus grand.</summary>
    public static char DetectDelimiter(string text)
    {
        var lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Where(l => l.Trim().Length > 0).Take(20).ToList();
        var best = ';';
        var bestScore = (-1, -1);
        foreach (var d in Delimiters)
        {
            var counts = lines.Select(l => Count(l, d)).Where(n => n > 0).ToList();
            if (counts.Count == 0)
            {
                continue;
            }

            var mode = counts.GroupBy(n => n).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First();
            var score = (mode.Count(), mode.Key);
            if (score.Item1 > bestScore.Item1 || (score.Item1 == bestScore.Item1 && score.Item2 > bestScore.Item2))
            {
                best = d;
                bestScore = score;
            }
        }

        return best;

        static int Count(string line, char d)
        {
            var quoted = false;
            var n = 0;
            foreach (var c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == d && !quoted)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>Classeur ZIP (XLSX, ODS) ou composé OLE (XLS) : « exportez en CSV ».</summary>
    private static bool SpreadsheetSignature(byte[] bytes) =>
        (bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] is 0x03 or 0x05 or 0x07 && bytes[3] is 0x04 or 0x06 or 0x08)
        || (bytes.Length >= 8 && bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0 && bytes[4] == 0xA1 && bytes[5] == 0xB1 && bytes[6] == 0x1A && bytes[7] == 0xE1);

    /// <summary>Exécutable (MZ, ELF), PDF ou image : contenu non textuel.</summary>
    private static bool ExecutableSignature(byte[] bytes) =>
        (bytes.Length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A)
        || (bytes.Length >= 4 && bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46)
        || (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
        || (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47);

    /// <summary>Part des caractères de contrôle (hors tabulation, retour chariot, saut de ligne).</summary>
    private static double ControlShare(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var control = 0;
        foreach (var c in text)
        {
            if (char.IsControl(c) && c is not ('\t' or '\r' or '\n') && c is < '\u0080' or > '\u009F')
            {
                control++;
            }
        }

        return control / (double)text.Length;
    }
}
