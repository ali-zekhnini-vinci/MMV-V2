using Microsoft.EntityFrameworkCore;
using MMV.Application.Catalog.Assisted;
using MMV.Domain.CsvImport;
using MMV.Infrastructure.Persistence;

namespace MMV.Infrastructure.Catalog;

/// <summary>
/// Persistance de l'assistant d'import (0009). Tentatives lues SUIVIES (comparaison-échange par <c>xmin</c>) ; originaux
/// lus sans leurs octets ; événements et décisions en ajout seul.
/// </summary>
internal sealed class ImportAssistantStore(MmvDbContext context) : IImportAssistantStore
{
    private static readonly string[] Active = [AnalysisStatuses.Queued, AnalysisStatuses.Running];
    private static readonly string[] Usable = [AnalysisStatuses.Done, AnalysisStatuses.Incomplete];

    public void Add(BatchOriginal original) => context.BatchOriginals.Add(original);

    public void Add(BatchAssistant assistant) => context.BatchAssistants.Add(assistant);

    public void Add(BatchAnalysis analysis) => context.BatchAnalyses.Add(analysis);

    public void Add(BatchAnalysisEvent analysisEvent) => context.BatchAnalysisEvents.Add(analysisEvent);

    public void Add(BatchDecision decision) => context.BatchDecisions.Add(decision);

    public Task<BatchAssistant?> FindAssistantAsync(Guid batchId, CancellationToken cancellationToken) =>
        context.BatchAssistants.SingleOrDefaultAsync(a => a.BatchId == batchId, cancellationToken);

    public Task<OriginalInfo?> FindOriginalAsync(Guid batchId, CancellationToken cancellationToken) =>
        context.BatchOriginals.AsNoTracking().Where(o => o.BatchId == batchId)
            .Select(o => new OriginalInfo(o.OriginalSha256, o.ByteLength, o.Encoding, o.Delimiter, o.PurgedAtUtc != null))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<BatchAnalysis?> FindAnalysisAsync(Guid id, CancellationToken cancellationToken) =>
        context.BatchAnalyses.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<BatchAnalysis?> LatestAnalysisAsync(Guid batchId, CancellationToken cancellationToken) =>
        context.BatchAnalyses.Where(a => a.BatchId == batchId).OrderByDescending(a => a.Attempt).FirstOrDefaultAsync(cancellationToken);

    public Task<BatchAnalysis?> LatestUsableAnalysisAsync(Guid batchId, CancellationToken cancellationToken) =>
        context.BatchAnalyses.AsNoTracking().Where(a => a.BatchId == batchId && Usable.Contains(a.Status)).OrderByDescending(a => a.Attempt).FirstOrDefaultAsync(cancellationToken);

    public Task<int> AnalysisCountAsync(Guid batchId, CancellationToken cancellationToken) =>
        context.BatchAnalyses.CountAsync(a => a.BatchId == batchId, cancellationToken);

    public async Task<IReadOnlyList<BatchAnalysisEvent>> EventsAsync(Guid analysisId, long after, int limit, CancellationToken cancellationToken) =>
        await context.BatchAnalysisEvents.AsNoTracking().Where(e => e.AnalysisId == analysisId && e.Id > after).OrderBy(e => e.Id).Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BatchAnalysisEvent>> BatchEventsAsync(Guid batchId, CancellationToken cancellationToken) =>
        await context.BatchAnalysisEvents.AsNoTracking().Where(e => e.BatchId == batchId && e.AnalysisId == null).OrderBy(e => e.Id).Take(50).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BatchAnalysis>> ActiveAnalysesAsync(CancellationToken cancellationToken) =>
        await context.BatchAnalyses.Where(a => Active.Contains(a.Status)).OrderBy(a => a.QueuedAtUtc).ToListAsync(cancellationToken);

    public Task<int> QueuePositionAsync(BatchAnalysis analysis, CancellationToken cancellationToken) =>
        context.BatchAnalyses.CountAsync(a => Active.Contains(a.Status) && a.Id != analysis.Id && a.QueuedAtUtc <= analysis.QueuedAtUtc, cancellationToken);

    public async Task<IReadOnlyList<BatchDecision>> DecisionsAsync(Guid batchId, CancellationToken cancellationToken) =>
        await context.BatchDecisions.AsNoTracking().Where(d => d.BatchId == batchId).OrderBy(d => d.Revision).ToListAsync(cancellationToken);

    public Task<BatchDecision?> FindDecisionByRequestAsync(Guid batchId, Guid requestId, CancellationToken cancellationToken) =>
        context.BatchDecisions.AsNoTracking().SingleOrDefaultAsync(d => d.BatchId == batchId && d.RequestId == requestId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, BatchAnalysis>> LatestAnalysesAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken)
    {
        var analyses = await context.BatchAnalyses.AsNoTracking().Where(a => batchIds.Contains(a.BatchId)).ToListAsync(cancellationToken);
        return analyses.GroupBy(a => a.BatchId).ToDictionary(g => g.Key, g => g.MaxBy(a => a.Attempt)!);
    }

    public Task<bool> IsAssistedAsync(Guid batchId, CancellationToken cancellationToken) => context.BatchAssistants.AnyAsync(a => a.BatchId == batchId, cancellationToken);

    public async Task<(int Total, DateTimeOffset? LastUploadedAtUtc, int Resumable)> BatchSummaryAsync(CancellationToken cancellationToken)
    {
        var total = await context.ImportBatches.CountAsync(cancellationToken);
        var last = await context.ImportBatches.MaxAsync(b => (DateTimeOffset?)b.UploadedAtUtc, cancellationToken);
        var resumable = await context.ImportBatches.CountAsync(b => b.Status == ImportStatuses.Uploaded || b.Status == ImportStatuses.Checked, cancellationToken);
        return (total, last, resumable);
    }
}
