using System.Globalization;
using System.Text.RegularExpressions;

namespace MMV.Domain.Pricing;

/// <summary>Mode de prix d'une variante dans un magasin (D-05-16).</summary>
public enum PricingMode
{
    /// <summary>Prix de vente HT = arrondi(prix d'achat HT × coefficient), stocké, recalculé seulement sur action explicite.</summary>
    Coefficient,

    /// <summary>Prix de vente HT saisi (permission <c>pricing.manual</c>) ; le coefficient enregistré est conservé, non appliqué.</summary>
    Manual,
}

public static class PricingModes
{
    public const string Coefficient = "coefficient";
    public const string Manual = "manual";

    public static string ToCode(PricingMode mode) => mode == PricingMode.Manual ? Manual : Coefficient;

    public static PricingMode FromCode(string code) => code == Manual ? PricingMode.Manual : PricingMode.Coefficient;
}

/// <summary>État de prix calculé à la lecture (jamais stocké, jamais corrigé automatiquement).</summary>
public enum PriceStatus
{
    UpToDate,

    /// <summary>Mode coefficient et prix stocké ≠ prix calculé à partir des valeurs courantes (recalcul explicite attendu).</summary>
    RecalculationPending,

    /// <summary>Prix de vente absent, ou mode coefficient sans prix d'achat ou sans coefficient.</summary>
    Incomplete,
}

/// <summary>
/// Calcul du prix de vente HT (D-05-16, QR-09 appliquée selon la règle conservatrice du mandat du 30/10/2026) : produit
/// EXACT en <see cref="decimal"/>, puis UN SEUL arrondi au centime, demi-unité arrondie en s'éloignant de zéro. Aucun
/// flottant binaire. PostgreSQL <c>round(numeric, 2)</c> applique la même règle (contrôle croisé en test).
/// </summary>
public static class SalePriceCalculator
{
    public const string RoundingRule = "cent_half_away_from_zero";

    /// <summary>Plus grand prix de vente HT représentable (<c>numeric(12,2)</c>).</summary>
    public const decimal MaxSalePrice = 9_999_999_999.99m;

    public static decimal Compute(decimal purchasePriceHt, decimal coefficient) =>
        Math.Round(purchasePriceHt * coefficient, 2, MidpointRounding.AwayFromZero);

    /// <summary>Coefficient effectif d'un prix manuel (prix HT ÷ prix d'achat, 4 décimales) ; absent si prix d'achat nul.</summary>
    public static decimal? EffectiveCoefficient(decimal? salePriceHt, decimal? purchasePriceHt) =>
        salePriceHt is { } sale && purchasePriceHt is { } purchase && purchase > 0
            ? Math.Round(sale / purchase, 4, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>Marge HT (prix de vente HT − prix d'achat HT), même devise ; absente si l'un manque.</summary>
    public static decimal? Margin(decimal? salePriceHt, decimal? purchasePriceHt) =>
        salePriceHt is { } sale && purchasePriceHt is { } purchase ? sale - purchase : null;

    public static PriceStatus Status(PricingMode mode, decimal? purchasePriceHt, decimal? coefficient, decimal? salePriceHt)
    {
        if (salePriceHt is null)
        {
            return PriceStatus.Incomplete;
        }

        if (mode == PricingMode.Manual)
        {
            return PriceStatus.UpToDate;
        }

        if (purchasePriceHt is not { } purchase || coefficient is not { } coef)
        {
            return PriceStatus.Incomplete;
        }

        return Compute(purchase, coef) == salePriceHt ? PriceStatus.UpToDate : PriceStatus.RecalculationPending;
    }
}

/// <summary>
/// Montants échangés en CHAÎNES décimales à échelle fixe (<c>"103.13"</c>, prix d'achat <c>"41.2500"</c>, coefficient
/// <c>"2.5000"</c>) : jamais de nombre JSON, jamais de flottant. Lecture stricte (point décimal, sans signe, sans
/// séparateur de milliers).
/// </summary>
public static partial class MoneyText
{
    public const int PurchaseScale = 4;
    public const int CoefficientScale = 4;
    public const int SaleScale = 2;

    /// <summary>Prix d'achat HT maximal admis à la saisie (× coefficient ≤ 100 reste ≤ <see cref="SalePriceCalculator.MaxSalePrice"/>).</summary>
    public const decimal MaxPurchasePrice = 99_999_999.9999m;

    public const decimal MaxCoefficient = 100m;

    [GeneratedRegex("^[0-9]{1,12}([.][0-9]{1,4})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static string Format(decimal value, int scale) => value.ToString("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static string? Format(decimal? value, int scale) => value is { } v ? Format(v, scale) : null;

    /// <summary>Valeur ≥ 0 d'au plus <paramref name="maxScale"/> décimales, ou <c>null</c> si la chaîne est invalide.</summary>
    public static decimal? TryParse(string? text, int maxScale)
    {
        if (text is null || !Pattern().IsMatch(text))
        {
            return null;
        }

        var dot = text.IndexOf('.', StringComparison.Ordinal);
        if (dot >= 0 && text.Length - dot - 1 > maxScale)
        {
            return null;
        }

        return decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Tarif d'une variante dans un magasin (D-05-15, D-05-16) : montants dans la devise du magasin (garantie par clé
/// étrangère composite). Prix d'achat HT et coefficient PROTÉGÉS (le coefficient révèle le prix d'achat).
/// </summary>
public sealed class VariantStorePrice
{
    private VariantStorePrice()
    {
        CurrencyCode = string.Empty;
        PricingModeCode = PricingModes.Coefficient;
    }

    public Guid VariantId { get; private set; }

    public Guid StoreId { get; private set; }

    public string CurrencyCode { get; private set; }

    public decimal? PurchasePriceHt { get; private set; }

    public decimal? Coefficient { get; private set; }

    public string PricingModeCode { get; private set; }

    public decimal? SalePriceHt { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public Guid? LastRequestId { get; private set; }

    public uint Version { get; private set; }

    public PricingMode Mode => PricingModes.FromCode(PricingModeCode);

    public PriceStatus Status => SalePriceCalculator.Status(Mode, PurchasePriceHt, Coefficient, SalePriceHt);

    /// <summary>Prix calculé à partir des valeurs courantes (null si incomplet).</summary>
    public decimal? ComputedSalePriceHt => PurchasePriceHt is { } p && Coefficient is { } c ? SalePriceCalculator.Compute(p, c) : null;

    public static VariantStorePrice Create(Guid variantId, Guid storeId, string currencyCode) =>
        new() { VariantId = variantId, StoreId = storeId, CurrencyCode = currencyCode };

    public void SetPurchasePrice(decimal? value, Guid userId, DateTimeOffset now, Guid requestId)
    {
        PurchasePriceHt = value;
        Touch(userId, now, requestId);
    }

    public void SetCoefficientPrice(decimal? purchasePriceHt, decimal coefficient, decimal salePriceHt, Guid userId, DateTimeOffset now, Guid requestId)
    {
        PurchasePriceHt = purchasePriceHt;
        Coefficient = coefficient;
        PricingModeCode = PricingModes.Coefficient;
        SalePriceHt = salePriceHt;
        Touch(userId, now, requestId);
    }

    public void SetManualPrice(decimal salePriceHt, Guid userId, DateTimeOffset now, Guid requestId)
    {
        PricingModeCode = PricingModes.Manual;
        SalePriceHt = salePriceHt;
        Touch(userId, now, requestId);
    }

    private void Touch(Guid userId, DateTimeOffset now, Guid requestId)
    {
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        LastRequestId = requestId;
    }
}

/// <summary>Champs historisés (D-05-16).</summary>
public static class PriceFields
{
    public const string PurchasePriceHt = "purchase_price_ht";
    public const string Coefficient = "coefficient";
    public const string PricingMode = "pricing_mode";
    public const string SalePriceHt = "sale_price_ht";
    public const string SupplierPriceHt = "supplier_price_ht";

    /// <summary>Lignes lisibles seulement avec <c>costs.read</c>.</summary>
    public static bool IsCost(string field) => field is PurchasePriceHt or Coefficient or SupplierPriceHt;
}

public static class PriceOrigins
{
    public const string Edit = "edit";
    public const string Recalculation = "recalculation";
    public const string ManualPrice = "manual_price";
    public const string Import = "import";
    public const string Receipt = "receipt";
}

/// <summary>Journal des prix, en AJOUT SEUL (déclencheur SQL) : ancienne et nouvelle valeur d'un champ, origine, auteur.</summary>
public sealed class PriceHistoryEntry
{
    private PriceHistoryEntry()
    {
        CurrencyCode = string.Empty;
        Field = string.Empty;
        Origin = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid VariantId { get; private set; }

    /// <summary>Magasin (null pour un prix fournisseur, qui n'appartient à aucun magasin).</summary>
    public Guid? StoreId { get; private set; }

    public string CurrencyCode { get; private set; }

    public string Field { get; private set; }

    public Guid? SupplierId { get; private set; }

    public decimal? OldValue { get; private set; }

    public decimal? NewValue { get; private set; }

    public string? OldMode { get; private set; }

    public string? NewMode { get; private set; }

    public string Origin { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public Guid RequestId { get; private set; }

    public static PriceHistoryEntry Value(
        Guid id, Guid variantId, Guid? storeId, Guid? supplierId, string currency, string field, decimal? oldValue, decimal? newValue,
        string origin, string? reason, Guid userId, DateTimeOffset now, Guid requestId) =>
        new()
        {
            Id = id,
            VariantId = variantId,
            StoreId = storeId,
            SupplierId = supplierId,
            CurrencyCode = currency,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Origin = origin,
            Reason = reason,
            ChangedAtUtc = now,
            ChangedByUserId = userId,
            RequestId = requestId,
        };

    public static PriceHistoryEntry ModeChange(
        Guid id, Guid variantId, Guid storeId, string currency, string? oldMode, string newMode, string origin, string? reason, Guid userId, DateTimeOffset now, Guid requestId) =>
        new()
        {
            Id = id,
            VariantId = variantId,
            StoreId = storeId,
            CurrencyCode = currency,
            Field = PriceFields.PricingMode,
            OldMode = oldMode,
            NewMode = newMode,
            Origin = origin,
            Reason = reason,
            ChangedAtUtc = now,
            ChangedByUserId = userId,
            RequestId = requestId,
        };
}

/// <summary>
/// Catégorie fiscale d'un magasin — STRUCTURE SEULEMENT (ADR-010 ouvert, G05-FISCAL) : aucune ligne créée par la
/// migration, aucun taux inventé ; un taux n'existe qu'avec sa date de validation.
/// </summary>
public sealed class TaxCategory
{
    private TaxCategory()
    {
        Code = string.Empty;
        Label = string.Empty;
    }

    public Guid StoreId { get; private set; }

    public string Code { get; private set; }

    public string Label { get; private set; }

    public decimal? Rate { get; private set; }

    public string? LegalReference { get; private set; }

    public DateTimeOffset? ValidatedAtUtc { get; private set; }

    public Guid? ValidatedByUserId { get; private set; }

    public bool IsValidated => Rate is not null && ValidatedAtUtc is not null;
}

/// <summary>
/// TTC (D-05-16) : calculé à la lecture SEULEMENT si le profil fiscal du magasin est validé ET la variante est rattachée à
/// une catégorie au taux validé. La règle d'arrondi fiscale n'est pas ratifiée (ADR-010) : en attendant, le calcul n'est
/// JAMAIS servi comme prix de vente réel (aucune vente en Phase 05) ; l'arrondi appliqué est celui du centime.
/// </summary>
public static class TaxCalculator
{
    public const string TaxNotValidated = "tax_not_validated";
    public const string Available = "available";

    public static (decimal? Ttc, string Status) Compute(decimal? salePriceHt, bool storeValidated, TaxCategory? category)
    {
        if (salePriceHt is not { } ht || !storeValidated || category is not { IsValidated: true, Rate: { } rate })
        {
            return (null, TaxNotValidated);
        }

        return (Math.Round(ht * (1 + rate), 2, MidpointRounding.AwayFromZero), Available);
    }
}
