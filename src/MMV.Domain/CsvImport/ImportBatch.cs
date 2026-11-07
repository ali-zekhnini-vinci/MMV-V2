namespace MMV.Domain.CsvImport;

/// <summary>Types d'import CSV (D-05-18) : catalogue (modèles / variantes, rattachement fournisseur facultatif), fournisseurs, prix par magasin.</summary>
public static class ImportKinds
{
    public const string Catalog = "catalog";
    public const string Suppliers = "suppliers";
    public const string Prices = "prices";

    public static IReadOnlyList<string> All { get; } = [Catalog, Suppliers, Prices];
}

public static class ImportStatuses
{
    /// <summary>Déposé : aperçu et correspondance des colonnes proposés ; rien n'est contrôlé.</summary>
    public const string Uploaded = "uploaded";

    /// <summary>Contrôle à blanc effectué (rapport par ligne) ; aucune écriture au catalogue.</summary>
    public const string Checked = "checked";

    /// <summary>Appliqué, tout ou rien ; définitif.</summary>
    public const string Applied = "applied";

    /// <summary>Abandonné par l'utilisateur ; définitif, sans effet.</summary>
    public const string Cancelled = "cancelled";
}

public static class ImportOutcomes
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Unchanged = "unchanged";
    public const string Error = "error";
}

/// <summary>
/// Lot d'import (D-05-18) : fichier CONSERVÉ en zone de préparation (texte, jamais exécuté), empreinte SHA-256,
/// correspondance des colonnes, rapport du contrôle à blanc, application explicite, tout ou rien, idempotente. Un même
/// fichier (même empreinte, même type, même magasin) ne peut être appliqué qu'une fois.
/// </summary>
public sealed class ImportBatch
{
    public const int FileNameMaxLength = 200;
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxRows = 5000;

    private ImportBatch()
    {
        Kind = string.Empty;
        FileName = string.Empty;
        FileSha256 = string.Empty;
        Content = string.Empty;
        Status = ImportStatuses.Uploaded;
        MappingJson = "{}";
    }

    public Guid Id { get; private set; }

    public string Kind { get; private set; }

    public Guid? StoreId { get; private set; }

    /// <summary>Fournisseur du fichier (COR-05-04) : contexte de correspondance et profil d'import ; facultatif.</summary>
    public Guid? SupplierId { get; private set; }

    public string FileName { get; private set; }

    public string FileSha256 { get; private set; }

    /// <summary>Contenu texte du fichier (UTF-8), conservé pour le contrôle et l'application ; jamais exécuté.</summary>
    public string Content { get; private set; }

    public int RowCount { get; private set; }

    public string MappingJson { get; private set; }

    public string Status { get; private set; }

    /// <summary>Le fichier contient des prix d'achat, coefficients ou prix fournisseurs (application : <c>costs.write</c>).</summary>
    public bool ContainsCosts { get; private set; }

    /// <summary>Le fichier contient des prix manuels (application : <c>pricing.manual</c>).</summary>
    public bool ContainsManualPrices { get; private set; }

    public int? ErrorCount { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset? CheckedAtUtc { get; private set; }

    public Guid? CheckedByUserId { get; private set; }

    public DateTimeOffset? AppliedAtUtc { get; private set; }

    public Guid? AppliedByUserId { get; private set; }

    public Guid CreationRequestId { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public uint Version { get; private set; }

    public static ImportBatch Upload(
        Guid id, string kind, Guid? storeId, string fileName, string sha256, string content, int rowCount, string mappingJson, Guid userId, DateTimeOffset now, Guid requestId,
        Guid? supplierId = null) =>
        new()
        {
            Id = id,
            Kind = kind,
            StoreId = storeId,
            SupplierId = supplierId,
            FileName = fileName,
            FileSha256 = sha256,
            Content = content,
            RowCount = rowCount,
            MappingJson = mappingJson,
            UploadedAtUtc = now,
            UploadedByUserId = userId,
            CreationRequestId = requestId,
            LastRequestId = requestId,
        };

    public void MarkChecked(string mappingJson, int errorCount, bool containsCosts, bool containsManualPrices, Guid userId, DateTimeOffset now, Guid requestId)
    {
        RequireEditable();
        MappingJson = mappingJson;
        ErrorCount = errorCount;
        ContainsCosts = containsCosts;
        ContainsManualPrices = containsManualPrices;
        Status = ImportStatuses.Checked;
        CheckedAtUtc = now;
        CheckedByUserId = userId;
        LastRequestId = requestId;
    }

    public void MarkApplied(Guid userId, DateTimeOffset now, Guid requestId)
    {
        if (Status != ImportStatuses.Checked)
        {
            throw new InvalidOperationException("Lot non contrôlé.");
        }

        Status = ImportStatuses.Applied;
        AppliedAtUtc = now;
        AppliedByUserId = userId;
        LastRequestId = requestId;
    }

    public void Cancel(Guid requestId)
    {
        RequireEditable();
        Status = ImportStatuses.Cancelled;
        LastRequestId = requestId;
    }

    private void RequireEditable()
    {
        if (Status is ImportStatuses.Applied or ImportStatuses.Cancelled)
        {
            throw new InvalidOperationException("Lot clos.");
        }
    }
}

/// <summary>Ligne du rapport de contrôle (valeurs en chaînes ; les lignes contenant des coûts ne sont servies qu'avec <c>costs.read</c>).</summary>
public sealed class ImportRow
{
    private ImportRow()
    {
        Outcome = string.Empty;
        ValuesJson = "{}";
        ErrorsJson = "[]";
    }

    public Guid BatchId { get; private set; }

    public int LineNo { get; private set; }

    public string ValuesJson { get; private set; }

    public string Outcome { get; private set; }

    public string ErrorsJson { get; private set; }

    public string? TargetKey { get; private set; }

    public static ImportRow Create(Guid batchId, int lineNo, string valuesJson, string outcome, string errorsJson, string? targetKey) =>
        new()
        {
            BatchId = batchId,
            LineNo = lineNo,
            ValuesJson = valuesJson,
            Outcome = outcome,
            ErrorsJson = errorsJson,
            TargetKey = targetKey,
        };
}
