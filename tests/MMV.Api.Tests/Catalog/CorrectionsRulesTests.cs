using MMV.Application.Catalog;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;

#pragma warning disable CA1861 // tableaux littéraux de cas de test

namespace MMV.Api.Tests.Catalog;

/// <summary>Règles pures des corrections COR-05-04 (import), COR-05-05 (verres), COR-05-06 (assistant) — sans base.</summary>
public sealed class CorrectionsRulesTests
{
    private static readonly Guid Blue = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static LensOfferCapabilities Offer(bool complete = true, DiopterRange? cylinder = null, IReadOnlyList<LensRule>? rules = null, string type = LensTypes.SingleVision) =>
        new(type, null, null, [Blue], new DiopterRange(-10m, 5m), cylinder, type == LensTypes.SingleVision ? null : new DiopterRange(0.75m, 3.5m), [65, 70], rules ?? [], complete, null);

    [Theory]
    [InlineData("-6", LensCompatibilityStatus.Compatible)]
    [InlineData("-11", LensCompatibilityStatus.Incompatible)]
    [InlineData("6", LensCompatibilityStatus.Incompatible)]
    [InlineData("-10", LensCompatibilityStatus.Compatible)]
    [InlineData("5", LensCompatibilityStatus.Compatible)]
    public void SphereRange_MandateExample_BlueLightMinus10ToPlus5(string sphere, LensCompatibilityStatus expected)
    {
        var result = LensCompatibilityEvaluator.Evaluate(Offer(), new LensPrescription(decimal.Parse(sphere, System.Globalization.CultureInfo.InvariantCulture), null, null, null, [Blue]), Today);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void InRange_ButIncompleteSheet_IsToVerify_NeverCompatible()
    {
        var result = LensCompatibilityEvaluator.Evaluate(Offer(complete: false), new LensPrescription(-6m, null, null, null, [Blue]), Today);
        Assert.Equal(LensCompatibilityStatus.ToVerify, result.Status);
        Assert.Contains(result.Reasons, r => r.Code == "rules_incomplete");
    }

    [Fact]
    public void MissingData_IsToVerify_ExplicitRule_IsIncompatible()
    {
        Assert.Equal(LensCompatibilityStatus.ToVerify, LensCompatibilityEvaluator.Evaluate(Offer(), new LensPrescription(null, null, null, null, []), Today).Status);
        Assert.Equal(LensCompatibilityStatus.ToVerify, LensCompatibilityEvaluator.Evaluate(Offer(), new LensPrescription(-2m, -1m, null, null, []), Today).Status); // pas de plage CYL
        Assert.Equal(LensCompatibilityStatus.Incompatible, LensCompatibilityEvaluator.Evaluate(Offer(), new LensPrescription(-2m, null, null, 60, []), Today).Status);
        Assert.Equal(LensCompatibilityStatus.Incompatible, LensCompatibilityEvaluator.Evaluate(Offer(), new LensPrescription(-2m, null, null, null, [Guid.NewGuid()]), Today).Status);
        Assert.Equal(LensCompatibilityStatus.ToVerify,
            LensCompatibilityEvaluator.Evaluate(Offer(type: LensTypes.Progressive), new LensPrescription(-2m, null, null, null, []), Today).Status); // addition manquante
    }

    [Fact]
    public void SpecificRules_OverrideBase_DenyAlwaysWins_SpecialManufacturingCarriesSurcharge()
    {
        var rules = new[]
        {
            // Avec anti-lumière bleue : plage resserrée −8 à +4 (règle plus spécifique que la plage de base).
            new LensRule(LensRuleKinds.SphereRange, LensRuleEffects.Allow, LensManufacturing.Standard, new LensRuleConditions(TreatmentIds: [Blue]), new DiopterRange(-8m, 4m)),
            new LensRule(LensRuleKinds.SphereRange, LensRuleEffects.Allow, LensManufacturing.Special, null, new DiopterRange(-12m, -10.25m), SurchargeHt: 15m, ExtraLeadDays: 5),
            new LensRule(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Standard, MaxSphereCylinderSum: 12m),
        };
        var offer = Offer(cylinder: new DiopterRange(-4m, 0m), rules: rules);
        Assert.Equal(LensCompatibilityStatus.Incompatible, LensCompatibilityEvaluator.Evaluate(offer, new LensPrescription(-9m, null, null, null, [Blue]), Today).Status);
        Assert.Equal(LensCompatibilityStatus.Compatible, LensCompatibilityEvaluator.Evaluate(offer, new LensPrescription(-9m, null, null, null, []), Today).Status);
        var special = LensCompatibilityEvaluator.Evaluate(offer, new LensPrescription(-11m, -0.5m, null, null, []), Today);
        Assert.Equal((LensCompatibilityStatus.Compatible, LensManufacturing.Special, 15m, 5), (special.Status, special.Manufacturing, special.SurchargeHt, special.ExtraLeadDays));
        Assert.Equal(LensCompatibilityStatus.Incompatible, LensCompatibilityEvaluator.Evaluate(offer, new LensPrescription(-10m, -3m, null, null, []), Today).Status);
    }

    [Fact]
    public void ExpiredSheet_IsToVerify()
    {
        var offer = Offer() with { ValidUntil = new DateOnly(2026, 1, 1) };
        Assert.Equal(LensCompatibilityStatus.ToVerify, LensCompatibilityEvaluator.Evaluate(offer, new LensPrescription(-2m, null, null, null, []), Today).Status);
    }

    [Theory]
    [InlineData("12,50", null, "12.50")]
    [InlineData("1 234,00", null, "1234.00")]
    [InlineData("1.234,56", null, "1234.56")]
    [InlineData("1,234.56", null, "1234.56")]
    [InlineData("€ 42,5", null, "42.5")]
    [InlineData("42,50 EUR", null, "42.50")]
    [InlineData("1,234", ',', "1.234")]
    [InlineData("1,234", '.', "1234")]
    [InlineData("10", null, "10")]
    public void Amounts_AreNormalized_WithoutFloats(string raw, char? separator, string expected) =>
        Assert.Equal((expected, (string?)null), NumberText.Normalize(raw, separator, "EUR"));

    [Fact]
    public void AmbiguousAmounts_AndForeignCurrency_AreRefused()
    {
        Assert.Equal(NumberText.AmbiguousNumber, NumberText.Normalize("1,234", null, "EUR").Error);
        Assert.Equal(NumberText.AmbiguousNumber, NumberText.Normalize("1.234", null, "EUR").Error);
        Assert.Equal(NumberText.CurrencyMismatch, NumberText.Normalize("10 MAD", null, "EUR").Error);
        Assert.Equal(NumberText.CurrencyMismatch, NumberText.Normalize("10 €", null, "MAD").Error);
        Assert.Equal((',', false), NumberText.DetectColumn(["12,50", "1 234,00", "3"]));
        Assert.Equal(((char?)null, true), NumberText.DetectColumn(["1,234", "7"]));
        Assert.Equal(((char?)null, true), NumberText.DetectColumn(["1,50", "2.50"]));
    }

    [Fact]
    public void Mapper_RecognisesSynonyms_ContextForRef_AndFlagsAmbiguity()
    {
        var headers = new[] { "PA HT", "Prix achat", "Purchase Price", "Ref", "Product Code" };
        var prices = ImportMapper.Suggest(ImportKinds.Prices, headers, supplierKnown: true, profile: null);
        Assert.Contains(prices, s => s is { Field: "purchasePriceHt", Header: "PA HT", Confidence: MatchConfidence.Exact });
        Assert.Contains(prices, s => s is { Field: "supplierReference", Header: "Ref", Confidence: MatchConfidence.Probable });
        var noSupplier = ImportMapper.Suggest(ImportKinds.Catalog, ["Référence", "Désignation", "Couleur"], supplierKnown: false, profile: null);
        Assert.Contains(noSupplier, s => s is { Header: "Référence", Confidence: MatchConfidence.Ambiguous });
        Assert.DoesNotContain("Référence", ImportMapper.Applied(noSupplier).Values);
        Assert.Contains(noSupplier, s => s is { Field: "modelName", Header: "Désignation" });
        var ambiguousPrice = ImportMapper.Suggest(ImportKinds.Prices, ["SKU", "Prix"], supplierKnown: false, profile: null);
        Assert.Contains(ambiguousPrice, s => s is { Header: "Prix", Confidence: MatchConfidence.Ambiguous });
        var profiled = ImportMapper.Suggest(ImportKinds.Catalog, ["Col A", "Col B"], supplierKnown: true, new Dictionary<string, string> { ["modelName"] = "Col A", ["@familyCode"] = "sunglasses" });
        Assert.Contains(profiled, s => s is { Field: "modelName", Header: "Col A", Confidence: MatchConfidence.Profile });
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3\n", ';')]
    [InlineData("a,b,c\n1,2,3\n", ',')]
    [InlineData("a\tb\tc\n1\t2\t3\n", '\t')]
    [InlineData("nom,prix\n\"Dupont; Fils\",\"12,50\"\n", ',')]
    [InlineData("Ref;Désignation\n000123;Monture, acétate\n", ';')]
    public void Delimiter_IsDetected_ByConsistency(string content, char expected) => Assert.Equal(expected, CsvTable.DetectDelimiter(content));

    [Fact]
    public void LeadingZeros_StayText_InParsedCells()
    {
        var (table, _) = CsvTable.Parse("Ref;PA HT\n000123;12,50\n", 10);
        Assert.Equal("000123", table!.Rows[0].Cells[0]);
    }

    [Fact]
    public void GeneratedSku_HasFamilyPrefix_AndUnambiguousSymbols()
    {
        var skus = Enumerable.Range(0, 300).Select(_ => SkuGenerator.Propose("optical_frames")).ToList();
        Assert.All(skus, s => Assert.Matches("^MON-[2-9A-HJKMNP-Z]{6}$", s));
        Assert.True(skus.Distinct().Count() > 290);
        Assert.StartsWith("ART-", SkuGenerator.Propose(null), StringComparison.Ordinal);
    }

    [Fact]
    public void WizardSubRequestIds_AreDeterministic_AndDistinctPerStep()
    {
        var request = Guid.NewGuid();
        Assert.Equal(ProductCreationService.Derive(request, "price:0"), ProductCreationService.Derive(request, "price:0"));
        Assert.NotEqual(ProductCreationService.Derive(request, "price:0"), ProductCreationService.Derive(request, "price:1"));
        Assert.NotEqual(ProductCreationService.Derive(request, "price:0"), ProductCreationService.Derive(Guid.NewGuid(), "price:0"));
    }
}
