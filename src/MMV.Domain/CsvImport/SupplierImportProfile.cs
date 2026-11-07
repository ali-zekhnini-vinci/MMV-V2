namespace MMV.Domain.CsvImport;

/// <summary>
/// Modèle d'import d'un fournisseur (COR-05-04) : correspondance « colonne du fichier → champ MMV » et options
/// (séparateur décimal, valeurs par défaut explicitement choisies) validées une première fois par l'utilisateur, puis
/// reproposées pour tout nouveau fichier du même fournisseur dont les en-têtes concordent. Un profil par (fournisseur,
/// type d'import) ; jamais supprimé (remplacé par mise à jour, version <c>xmin</c>).
/// </summary>
public sealed class SupplierImportProfile
{
    private SupplierImportProfile()
    {
        Kind = ImportKinds.Catalog;
        MappingJson = "{}";
        HeadersJson = "[]";
    }

    public Guid Id { get; private set; }

    public Guid SupplierId { get; private set; }

    public string Kind { get; private set; }

    /// <summary>Champ MMV → intitulé de colonne, plus options « @… » (ex. <c>@decimal</c>, <c>@familyCode</c>).</summary>
    public string MappingJson { get; private set; }

    /// <summary>En-têtes normalisés du fichier de référence (concordance d'un nouveau fichier).</summary>
    public string HeadersJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public uint Version { get; private set; }

    public static SupplierImportProfile Create(Guid id, Guid supplierId, string kind, string mappingJson, string headersJson, Guid userId, DateTimeOffset now, Guid requestId) =>
        new()
        {
            Id = id, SupplierId = supplierId, Kind = kind, MappingJson = mappingJson, HeadersJson = headersJson,
            CreatedAtUtc = now, CreatedByUserId = userId, UpdatedAtUtc = now, UpdatedByUserId = userId, LastRequestId = requestId,
        };

    public void Update(string mappingJson, string headersJson, Guid userId, DateTimeOffset now, Guid requestId)
    {
        MappingJson = mappingJson;
        HeadersJson = headersJson;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }
}
