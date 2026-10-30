namespace MMV.Domain.Stock;

public static class InventoryStatuses
{
    public const string Open = "open";
    public const string Validated = "validated";
    public const string Abandoned = "abandoned";
}

public static class InventoryScopes
{
    public const string Full = "full";
    public const string Family = "family";
    public const string Selection = "selection";
}

/// <summary>Traitement d'une ligne à la validation (D-05-10, QR-04 appliquée : lignes non comptées exclues ou mises à 0 sur choix explicite).</summary>
public static class InventoryResolutions
{
    public const string Applied = "applied";
    public const string NoDifference = "no_difference";
    public const string Excluded = "excluded";
    public const string SetToZero = "set_to_zero";
}

/// <summary>
/// Session d'inventaire (Q10, D-05-10) : un magasin, un ou plusieurs de ses emplacements, périmètre complet / famille /
/// sélection, comptage à l'aveugle en option. Les mouvements restent AUTORISÉS pendant la session (pas de gel) : chaque
/// ligne mémorise la quantité théorique au moment du comptage, et la validation applique l'ÉCART constaté au stock
/// COURANT (jamais une écrasante « quantité constatée ») — un mouvement intervenu pendant le comptage n'est jamais
/// effacé. Close ou abandonnée = immuable (déclencheur SQL).
/// </summary>
public sealed class InventorySession
{
    public const int ReasonMaxLength = 500;
    public const int MaxLines = 5000;

    private readonly List<InventoryLine> _lines = [];

    private InventorySession()
    {
        Number = string.Empty;
        ScopeType = InventoryScopes.Full;
        Status = InventoryStatuses.Open;
        LocationIds = [];
    }

    public Guid Id { get; private set; }

    /// <summary>Numéro lisible attribué par PostgreSQL (<c>INV-000001</c>).</summary>
    public string Number { get; private set; }

    public Guid StoreId { get; private set; }

    public Guid[] LocationIds { get; private set; }

    public string ScopeType { get; private set; }

    public string? ScopeFamilyCode { get; private set; }

    public bool Blind { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset OpenedAtUtc { get; private set; }

    public Guid OpenedByUserId { get; private set; }

    public DateTimeOffset? ValidatedAtUtc { get; private set; }

    public Guid? ValidatedByUserId { get; private set; }

    public string? UncountedResolution { get; private set; }

    public DateTimeOffset? AbandonedAtUtc { get; private set; }

    public Guid? AbandonedByUserId { get; private set; }

    public string? AbandonReason { get; private set; }

    public Guid CreationRequestId { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyList<InventoryLine> Lines => _lines;

    public bool IsOpen => Status == InventoryStatuses.Open;

    public static InventorySession Open(
        Guid id, Guid storeId, Guid[] locationIds, string scopeType, string? familyCode, bool blind, Guid userId, DateTimeOffset now, Guid requestId) =>
        new()
        {
            Id = id,
            StoreId = storeId,
            LocationIds = locationIds,
            ScopeType = scopeType,
            ScopeFamilyCode = familyCode,
            Blind = blind,
            OpenedAtUtc = now,
            OpenedByUserId = userId,
            CreationRequestId = requestId,
            LastRequestId = requestId,
        };

    public void AddLine(InventoryLine line) => _lines.Add(line);

    public void MarkValidated(string uncounted, Guid userId, DateTimeOffset now, Guid requestId)
    {
        RequireOpen();
        Status = InventoryStatuses.Validated;
        UncountedResolution = uncounted;
        ValidatedAtUtc = now;
        ValidatedByUserId = userId;
        LastRequestId = requestId;
    }

    public void Abandon(string reason, Guid userId, DateTimeOffset now, Guid requestId)
    {
        RequireOpen();
        Status = InventoryStatuses.Abandoned;
        AbandonReason = reason;
        AbandonedAtUtc = now;
        AbandonedByUserId = userId;
        LastRequestId = requestId;
    }

    private void RequireOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Session d'inventaire close.");
        }
    }
}

/// <summary>Ligne de comptage (variante, emplacement) d'une session.</summary>
public sealed class InventoryLine
{
    public const int CommentMaxLength = 500;
    public const int MaxQuantity = 1_000_000;

    private InventoryLine()
    {
    }

    public Guid SessionId { get; private set; }

    public Guid VariantId { get; private set; }

    public Guid LocationId { get; private set; }

    /// <summary>Quantité en main à l'ouverture (information ; le contrôle utilise <see cref="ExpectedAtCount"/>).</summary>
    public int ExpectedAtOpen { get; private set; }

    /// <summary>Quantité en main lue SOUS VERROU au moment de la saisie du comptage.</summary>
    public int? ExpectedAtCount { get; private set; }

    public int? CountedQuantity { get; private set; }

    public Guid? CountedByUserId { get; private set; }

    public DateTimeOffset? CountedAtUtc { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public string? ReasonCode { get; private set; }

    public string? Comment { get; private set; }

    public string? Resolution { get; private set; }

    public int? AppliedDelta { get; private set; }

    public Guid? MovementId { get; private set; }

    public uint Version { get; private set; }

    public int? Difference => CountedQuantity is { } counted && ExpectedAtCount is { } expected ? counted - expected : null;

    public static InventoryLine Create(Guid sessionId, Guid variantId, Guid locationId, int expectedAtOpen) =>
        new() { SessionId = sessionId, VariantId = variantId, LocationId = locationId, ExpectedAtOpen = expectedAtOpen };

    /// <summary>Comptage (ou recomptage) : la quantité théorique est relue au même instant, sous verrou du solde.</summary>
    public void Count(int counted, int expectedNow, Guid userId, DateTimeOffset now, Guid requestId)
    {
        CountedQuantity = counted;
        ExpectedAtCount = expectedNow;
        CountedByUserId = userId;
        CountedAtUtc = now;
        LastRequestId = requestId;
    }

    public void Justify(string reasonCode, string? comment, Guid requestId)
    {
        ReasonCode = reasonCode;
        Comment = comment;
        LastRequestId = requestId;
    }

    public void Resolve(string resolution, int? appliedDelta, Guid? movementId)
    {
        Resolution = resolution;
        AppliedDelta = appliedDelta;
        MovementId = movementId;
    }
}

/// <summary>Décision de validation d'une ligne (calcul pur, appliqué sous verrou des soldes).</summary>
public enum InventoryLineOutcome
{
    NoDifference,
    Apply,
    Excluded,
    ChangedSinceCount,
    ReasonMissing,
    StockInsufficient,
}

public static class InventoryRules
{
    /// <summary>
    /// Ligne comptée : écart = constaté − théorique au comptage, appliqué au stock COURANT. Si le stock a bougé depuis le
    /// comptage, la ligne exige un recomptage ou une acceptation explicite. Ligne non comptée : exclue, ou mise à 0
    /// (écart = −stock courant) sur choix explicite. Un écart non nul exige un motif justifié (Q10).
    /// </summary>
    public static (InventoryLineOutcome Outcome, int Delta) Decide(InventoryLine line, int currentOnHand, int reserved, bool setUncountedToZero, bool acceptChanged)
    {
        int delta;
        if (line.CountedQuantity is not { } counted || line.ExpectedAtCount is not { } expected)
        {
            if (!setUncountedToZero)
            {
                return (InventoryLineOutcome.Excluded, 0);
            }

            delta = -currentOnHand;
        }
        else
        {
            if (currentOnHand != expected && !acceptChanged)
            {
                return (InventoryLineOutcome.ChangedSinceCount, 0);
            }

            delta = counted - expected;
        }

        if (delta == 0)
        {
            return (InventoryLineOutcome.NoDifference, 0);
        }

        if (line.ReasonCode is null)
        {
            return (InventoryLineOutcome.ReasonMissing, delta);
        }

        return StockRules.CheckDelta(currentOnHand, reserved, delta) == StockCheck.Ok
            ? (InventoryLineOutcome.Apply, delta)
            : (InventoryLineOutcome.StockInsufficient, delta);
    }
}
