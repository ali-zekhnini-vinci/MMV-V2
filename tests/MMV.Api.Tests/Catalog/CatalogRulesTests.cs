using MMV.Application.Catalog;
using MMV.Domain.Catalog;
using MMV.Domain.Pricing;
using MMV.Domain.Stock;

namespace MMV.Api.Tests.Catalog;

/// <summary>
/// Règles pures de la Phase 05 (aucune base) : calcul du prix de vente HT (produit exact, UN arrondi au centime, demi vers
/// l'extérieur), lecture stricte des montants, état de prix, GTIN, disponibilité, stock négatif et réservé, décision
/// d'inventaire, lecture CSV, validation des fiches.
/// </summary>
public sealed class CatalogRulesTests
{
    [Theory]
    [InlineData("41.2500", "2.5000", "103.13")]
    [InlineData("19.9900", "2.2000", "43.98")]
    [InlineData("12.0000", "2.0000", "24.00")]
    [InlineData("0.0050", "1.0000", "0.01")]
    [InlineData("0.0049", "1.0000", "0.00")]
    [InlineData("10.0050", "1.0000", "10.01")]
    [InlineData("33.3333", "0.3333", "11.11")]
    [InlineData("99999999.9999", "100", "9999999999.99")]
    public void SalePrice_ExactProductThenSingleHalfAwayFromZeroRounding(string purchase, string coefficient, string expected)
    {
        var p = MoneyText.TryParse(purchase, MoneyText.PurchaseScale)!.Value;
        var c = MoneyText.TryParse(coefficient, MoneyText.CoefficientScale)!.Value;
        Assert.Equal(expected, MoneyText.Format(SalePriceCalculator.Compute(p, c), MoneyText.SaleScale));
    }

    [Fact]
    public void SalePrice_NeverRoundsTwice()
    {
        // 0,4999 × 2,0099 = 1,00474901 : arrondi en deux temps (3 décimales → 1,005, puis 2) donnerait 1,01 ; un seul arrondi donne 1,00.
        Assert.Equal(1.00m, SalePriceCalculator.Compute(0.4999m, 2.0099m));
    }

    [Theory]
    [InlineData("12.5", 4, true)]
    [InlineData("12.12345", 4, false)]
    [InlineData("-1", 4, false)]
    [InlineData("1,5", 4, false)]
    [InlineData("1e3", 4, false)]
    [InlineData(" 1", 4, false)]
    [InlineData("1.234", 2, false)]
    [InlineData("0", 2, true)]
    public void MoneyText_StrictDecimalStrings(string text, int scale, bool valid) =>
        Assert.Equal(valid, MoneyText.TryParse(text, scale) is not null);

    [Fact]
    public void PriceStatus_PendingWhenStoredDiffersFromComputed_ManualAlwaysUpToDate()
    {
        Assert.Equal(PriceStatus.UpToDate, SalePriceCalculator.Status(PricingMode.Coefficient, 10m, 2m, 20m));
        Assert.Equal(PriceStatus.RecalculationPending, SalePriceCalculator.Status(PricingMode.Coefficient, 11m, 2m, 20m));
        Assert.Equal(PriceStatus.Incomplete, SalePriceCalculator.Status(PricingMode.Coefficient, null, 2m, 20m));
        Assert.Equal(PriceStatus.Incomplete, SalePriceCalculator.Status(PricingMode.Coefficient, 10m, 2m, null));
        Assert.Equal(PriceStatus.UpToDate, SalePriceCalculator.Status(PricingMode.Manual, 11m, 2m, 19.9m));
    }

    [Fact]
    public void Ttc_OnlyWithValidatedStoreAndCategory_NoRateInvented()
    {
        Assert.Equal((null, TaxCalculator.TaxNotValidated), TaxCalculator.Compute(100m, storeValidated: false, category: null));
        Assert.Equal((null, TaxCalculator.TaxNotValidated), TaxCalculator.Compute(100m, storeValidated: true, category: null));
    }

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("4006381333932", false)]
    [InlineData("96385074", true)]
    [InlineData("036000291452", true)]
    [InlineData("12345", false)]
    [InlineData("40063813339ab", false)]
    public void Gtin_ChecksLengthDigitsAndCheckDigit(string value, bool valid) => Assert.Equal(valid, Gtin.IsValid(value));

    [Fact]
    public void Families_AreTheSixApproved_ServicesOnlyAsService()
    {
        Assert.Equal(
            ["optical_frames", "sunglasses", "ophthalmic_lenses", "contact_lenses_solutions", "accessories", "services"],
            Families.All.Select(f => f.Code));
        Assert.Equal([StockPolicy.Service], Families.Find("services")!.AllowedPolicies);
        Assert.All(Families.All.Where(f => f.Code != "services"), f => Assert.DoesNotContain(StockPolicy.Service, f.AllowedPolicies));
    }

    [Theory]
    [InlineData(StockPolicy.Stocked, false, false, 0, null, Availability.OutOfStock)]
    [InlineData(StockPolicy.Stocked, false, false, 5, null, Availability.InStock)]
    [InlineData(StockPolicy.Stocked, false, false, 5, 5, Availability.LowStock)]
    [InlineData(StockPolicy.Stocked, false, false, 6, 5, Availability.InStock)]
    [InlineData(StockPolicy.OrderOnly, false, false, 0, null, Availability.OnOrder)]
    [InlineData(StockPolicy.OrderOnly, false, true, 0, null, Availability.Incomplete)]
    [InlineData(StockPolicy.Service, false, false, 0, null, Availability.Available)]
    [InlineData(StockPolicy.Stocked, true, false, 9, null, Availability.Archived)]
    public void Availability_ComputedOnce(StockPolicy policy, bool archived, bool incomplete, int available, int? threshold, Availability expected) =>
        Assert.Equal(expected, Availabilities.Compute(policy, archived, incomplete, available, threshold));

    [Theory]
    [InlineData(5, 0, -5, StockCheck.Ok)]
    [InlineData(5, 0, -6, StockCheck.Insufficient)]
    [InlineData(5, 2, -4, StockCheck.Reserved)]
    [InlineData(5, 2, -3, StockCheck.Ok)]
    [InlineData(0, 0, 3, StockCheck.Ok)]
    public void Stock_NegativeForbidden_ReservedUntouchable(int onHand, int reserved, int delta, StockCheck expected) =>
        Assert.Equal(expected, StockRules.CheckDelta(onHand, reserved, delta));

    [Fact]
    public void Balance_RefusesNegative()
    {
        var balance = StockBalance.Empty(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => balance.Apply(-1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Inventory_AppliesTheCountedDifferenceToCurrentStock_NeverOverwrites()
    {
        var line = InventoryLine.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);
        line.Count(8, 10, Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid());

        // Stock modifié depuis le comptage (10 → 9) : refus sans acceptation explicite.
        Assert.Equal(InventoryLineOutcome.ChangedSinceCount, InventoryRules.Decide(line, 9, 0, false, acceptChanged: false).Outcome);
        Assert.Equal(InventoryLineOutcome.ReasonMissing, InventoryRules.Decide(line, 9, 0, false, acceptChanged: true).Outcome);
        line.Justify("loss", null, Guid.NewGuid());
        Assert.Equal((InventoryLineOutcome.Apply, -2), InventoryRules.Decide(line, 9, 0, false, acceptChanged: true));
        Assert.Equal(InventoryLineOutcome.StockInsufficient, InventoryRules.Decide(line, 1, 0, false, acceptChanged: true).Outcome);

        var uncounted = InventoryLine.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4);
        Assert.Equal(InventoryLineOutcome.Excluded, InventoryRules.Decide(uncounted, 4, 0, setUncountedToZero: false, acceptChanged: false).Outcome);
        Assert.Equal(InventoryLineOutcome.ReasonMissing, InventoryRules.Decide(uncounted, 4, 0, setUncountedToZero: true, acceptChanged: false).Outcome);
    }

    [Fact]
    public void Csv_SemicolonQuotesBomAndFormulaDetection()
    {
        var (table, error) = CsvTable.Parse("﻿sku;libellé\n\"A;1\";\"dit \"\"x\"\"\"\n\nB;2\n", 10);
        Assert.Null(error);
        Assert.Equal(';', table!.Delimiter);
        Assert.Equal(["sku", "libellé"], table.Headers);
        Assert.Equal(["A;1", "dit \"x\""], table.Rows[0].Cells);
        Assert.Equal(4, table.Rows[1].LineNo);
        Assert.Equal("too_many_rows", CsvTable.Parse("a\n1\n2\n3\n", 2).Error);
        Assert.Equal("unterminated_quote", CsvTable.Parse("a\n\"1\n", 2).Error);
        Assert.True(CsvTable.LooksLikeFormula("=SUM(A1)"));
        Assert.True(CsvTable.LooksLikeFormula("@cmd"));
        Assert.True(CsvTable.LooksLikeFormula("+cmd"));
        Assert.False(CsvTable.LooksLikeFormula("-5"));
        Assert.False(CsvTable.LooksLikeFormula("+32 470 00 00 00"));
    }

    [Fact]
    public void ImportMapping_SuggestsFromFrenchHeaders_AndRequiresMandatoryFields()
    {
        var mapping = ImportService.Suggest("prices", ["SKU", "Prix achat HT", "Coef."]);
        Assert.Equal("SKU", mapping["sku"]);
        Assert.Equal("Prix achat HT", mapping["purchasePriceHt"]);
        Assert.Equal("Coef.", mapping["coefficient"]);
        Assert.Contains("mapping.sku", ImportService.CheckMapping("prices", new Dictionary<string, string>(), ["SKU"]).Keys);
    }

    [Fact]
    public void VariantValidation_FamilyAttributes_LensRules_PolicyByFamily()
    {
        var errors = new Dictionary<string, string[]>();
        var frames = Families.Find("optical_frames")!;
        var (details, policy) = CatalogInputValidator.Variant(
            new VariantInput(" SKU-1 ", null, "4006381333931", "Noir", new Dictionary<string, string?> { ["eyeSize"] = "52", ["color"] = "Noir" }, null, null, "stocked"),
            frames, requirePolicy: true, errors);
        Assert.Empty(errors);
        Assert.Equal("SKU-1", details!.Sku);
        Assert.Equal(StockPolicy.Stocked, policy);

        errors.Clear();
        CatalogInputValidator.Variant(
            new VariantInput("SKU-2", null, null, "X", new Dictionary<string, string?> { ["eyeSize"] = "500", ["unknown"] = "x" }, Guid.NewGuid(), null, "service"),
            frames, requirePolicy: true, errors);
        Assert.Equal(["attributes.eyeSize", "attributes.unknown", "lensIndexId", "stockPolicy"], errors.Keys.Order());

        errors.Clear();
        CatalogInputValidator.Model(new ModelInput("ophthalmic_lenses", null, "Gamme", null, null, null), errors);
        Assert.Equal(["required"], errors["lensRangeId"]);
    }
}
