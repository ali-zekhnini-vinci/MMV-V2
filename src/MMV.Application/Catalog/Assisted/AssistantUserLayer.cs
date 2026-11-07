using System.Globalization;
using System.Text.Json.Nodes;
using MMV.Domain.CsvImport;

namespace MMV.Application.Catalog.Assisted;

/// <summary>
/// Couche « décisions de l'utilisateur » effective à une révision donnée (priorité structurelle : décision utilisateur
/// &gt; règle &gt; IA). Reconstituée à chaque revalidation à partir des révisions append-only : les révisions révoquées sont
/// écartées, puis les décisions restantes appliquées dans l'ordre (la plus récente l'emporte pour une même cible).
/// Chaque entrée conserve la révision qui l'a posée (provenance affichée).
/// </summary>
public sealed class AssistantUserLayer
{
    public static readonly AssistantUserLayer Empty = new();

    public (bool Present, int Revision)? HeaderPresent { get; private set; }

    /// <summary>Colonne → champ choisi (<c>ignore</c> = ne pas importer).</summary>
    public Dictionary<int, (string Field, int Revision)> ColumnFields { get; } = [];

    public Dictionary<string, (string Family, int Revision)> FamilyClasses { get; } = new(StringComparer.Ordinal);

    public (bool Accept, int Revision)? Continuation { get; private set; }

    /// <summary>Continuation refusée pour une ligne précise.</summary>
    public Dictionary<int, int> ContinuationRefused { get; } = [];

    public Dictionary<(int Line, int Column), (string Value, int Revision)> CellValues { get; } = [];

    public Dictionary<int, int> SkippedLines { get; } = [];

    public (char Separator, int Revision)? DecimalSeparator { get; private set; }

    public Dictionary<string, (string Value, int Revision)> FileDefaults { get; } = new(StringComparer.Ordinal);

    /// <summary>Références de verres à créer (fabricant ou gamme) : clé <c>manufacturer|nom</c> ou <c>range|fabricant|nom</c>.</summary>
    public Dictionary<string, int> CreateReferences { get; } = new(StringComparer.Ordinal);

    public int? RulesOnlyRevision { get; private set; }

    public static string ReferenceKey(string kind, string name, string? manufacturer = null) =>
        kind == "range" ? $"range|{CatalogLexicon.Norm(manufacturer ?? string.Empty)}|{CatalogLexicon.Norm(name)}" : $"manufacturer|{CatalogLexicon.Norm(name)}";

    public static AssistantUserLayer From(IEnumerable<BatchDecision> decisions)
    {
        var ordered = decisions.OrderBy(d => d.Revision).ToList();
        var revoked = new HashSet<int>();
        foreach (var d in ordered.Where(d => d.Type == DecisionTypes.Revoke))
        {
            if (Int(Parse(d.TargetJson), "revision") is { } r)
            {
                revoked.Add(r);
            }
        }

        var layer = new AssistantUserLayer();
        foreach (var d in ordered.Where(d => d.Type != DecisionTypes.Revoke && !revoked.Contains(d.Revision)))
        {
            layer.Apply(d.Type, Parse(d.TargetJson), Parse(d.ValueJson), d.Revision);
        }

        return layer;
    }

    private void Apply(string type, JsonObject target, JsonObject value, int revision)
    {
        switch (type)
        {
            case DecisionTypes.ColumnField when Int(target, "column") is { } c && Text(value, "field") is { } field:
                ColumnFields[c] = (field, revision);
                break;
            case DecisionTypes.HeaderPresence when Bool(value, "present") is { } present:
                HeaderPresent = (present, revision);
                break;
            case DecisionTypes.FamilyClass when Text(target, "classKey") is { } key && Text(value, "family") is { } family:
                FamilyClasses[key] = (family, revision);
                break;
            case DecisionTypes.Continuation when Bool(value, "accept") is { } accept:
                if (Int(target, "line") is { } line)
                {
                    if (accept)
                    {
                        ContinuationRefused.Remove(line);
                    }
                    else
                    {
                        ContinuationRefused[line] = revision;
                    }
                }
                else
                {
                    Continuation = (accept, revision);
                }

                break;
            case DecisionTypes.CellValue when Int(target, "line") is { } l && Int(target, "column") is { } col && Text(value, "value") is { } v:
                CellValues[(l, col)] = (v, revision);
                break;
            case DecisionTypes.RowSkip when Int(target, "line") is { } skip:
                SkippedLines[skip] = revision;
                break;
            case DecisionTypes.RowRestore when Int(target, "line") is { } restore:
                SkippedLines.Remove(restore);
                break;
            case DecisionTypes.DecimalSeparator when Text(value, "separator") is "," or ".":
                DecimalSeparator = (Text(value, "separator")![0], revision);
                break;
            case DecisionTypes.FileDefault when Text(target, "field") is { } f && Text(value, "value") is { } dv:
                FileDefaults[f] = (dv, revision);
                break;
            case DecisionTypes.CreateReference when Text(target, "kind") is "manufacturer" or "range" && Text(target, "name") is { } name:
                CreateReferences[ReferenceKey(Text(target, "kind")!, name, Text(target, "manufacturer"))] = revision;
                break;
            case DecisionTypes.RulesOnly:
                RulesOnlyRevision ??= revision;
                break;
        }
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? [];

    internal static int? Int(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i
        : o[key] is JsonValue s && s.TryGetValue<string>(out var t) && int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var j) ? j : null;

    internal static string? Text(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    internal static bool? Bool(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
}
