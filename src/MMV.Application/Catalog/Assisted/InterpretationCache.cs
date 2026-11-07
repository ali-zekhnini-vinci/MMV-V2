using System.Collections.Concurrent;
using MMV.Domain.CsvImport;

namespace MMV.Application.Catalog.Assisted;

/// <summary>
/// Cache borné des revalidations (suivi de l'analyse interrogé chaque seconde) : clé = lot, révision des décisions,
/// tentative exploitable, destination. Toute nouvelle décision ou tentative change la clé : jamais de résultat périmé.
/// Le document et l'interprétation ne sont jamais modifiés par leurs lecteurs.
/// </summary>
internal static class InterpretationCache
{
    private const int Capacity = 16;
    private static readonly ConcurrentDictionary<(Guid Batch, int Revision, Guid? Analysis, string Target), (long Tick, CsvDocument Document, CatalogInterpretation Interpretation)> Entries = new();
    private static long _tick;

    public static (CsvDocument Document, CatalogInterpretation Interpretation) Get(ImportBatch batch, BatchAssistant state, string encoding, BatchAnalysis? usable, AssistantUserLayer user)
    {
        var key = (batch.Id, state.Revision, usable?.Id, state.Target);
        if (Entries.TryGetValue(key, out var hit))
        {
            Entries[key] = hit with { Tick = Interlocked.Increment(ref _tick) };
            return (hit.Document, hit.Interpretation);
        }

        var document = TolerantCsvReader.Parse(batch.Content, encoding);
        var proposal = usable is null ? null : CatalogInterpretationContract.Deserialize(usable.ProposalJson);
        var interpretation = CatalogInterpretationValidator.Validate(document, proposal, user, state.Target);
        Entries[key] = (Interlocked.Increment(ref _tick), document, interpretation);
        while (Entries.Count > Capacity && Entries.OrderBy(e => e.Value.Tick).FirstOrDefault() is { Key: var oldest })
        {
            Entries.TryRemove(oldest, out _);
        }

        return (document, interpretation);
    }
}
