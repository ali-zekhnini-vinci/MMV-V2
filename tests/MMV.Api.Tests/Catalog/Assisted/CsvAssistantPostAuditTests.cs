using MMV.Application.Catalog;
using MMV.Application.Catalog.Assisted;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Catalog.Assisted;

/// <summary>
/// Corrections post-audit CSV-IMPORT-AI-01 (06/11/2026), sans base : devise jamais supposée (COR-CSVAI-03), identité et
/// mise à jour des offres de verres (COR-CSVAI-01), statut « fiche complète » après modification (COR-CSVAI-02).
/// Le rapprochement réel, le rejeu idempotent (COR-CSVAI-04) et la transaction sont prouvés sur PostgreSQL
/// (<c>Phase05AssistedImportTests</c>).
/// </summary>
public sealed class CsvAssistantPostAuditTests
{
    private static readonly CsvDocument Priced = TolerantCsvReader.Parse(
        "Marque;Modèle;Coloris;Prix achat HT;Famille\nRay;A1;Noir;42,50;Monture\nRay;A2;Bleu;44,00;Monture\n", "utf-8");

    private static AssistantUserLayer Layer(params (string Field, string Value)[] defaults) =>
        AssistantUserLayer.From(defaults.Select((x, i) => CsvAssistantCoreTests.Decision(i + 2, DecisionTypes.FileDefault, $$"""{"field":"{{x.Field}}"}""", $$"""{"value":"{{x.Value}}"}""")));

    // ------------------------------------------------------------------ COR-CSVAI-03 : devise

    [Fact]
    public void Missing_currency_is_never_taken_as_provided_and_store_currency_is_only_proposed()
    {
        var layer = Layer(("stockPolicy", "stocked"));
        var d = CatalogInterpretationValidator.Validate(Priced, null, layer);
        Assert.Equal("purchasePriceHt", d.Columns[3].Field);
        var rows = CatalogMaterializer.ProductRows(d, layer, "EUR");
        Assert.All(rows, r => Assert.Contains(r.PreErrors, e => e == new ImportError("currency", CatalogMaterializer.CurrencyUnconfirmed)));
        Assert.All(rows, r => Assert.False(r.Values.ContainsKey("price:currency")));

        var issue = CatalogMaterializer.Issues(d, layer, new CatalogMaterializer.IssueContext(AssistantTargets.Products, "EUR")).Single(i => i.Key == "default:currency");
        Assert.Equal(("currency_confirmation_required", CatalogStatuses.Ambiguous), (issue.Code, issue.Severity));
        Assert.Equal([2, 3], issue.Lines);
        var confirm = issue.Actions.Single(a => a.Type == DecisionTypes.FileDefault);
        Assert.Equal("EUR", confirm.Constraints!["value"]!.GetValue<string>());
        Assert.Equal([Permissions.CostsWrite], confirm.Requires);
        Assert.Contains(issue.Actions, a => a.Type == DecisionTypes.ColumnField && a.Constraints!["field"]!.GetValue<string>() == CatalogMaterializer.Ignore);
        Assert.Contains(issue.NotOffered, n => n.Type == "conversion");
    }

    [Fact]
    public void Confirmed_store_currency_is_applied_to_unqualified_amounts_only()
    {
        var layer = Layer(("stockPolicy", "stocked"), ("currency", "EUR"));
        var d = CatalogInterpretationValidator.Validate(Priced, null, layer);
        var rows = CatalogMaterializer.ProductRows(d, layer, "EUR");
        Assert.All(rows, r => Assert.Equal("EUR", r.Values["price:currency"]));
        Assert.All(rows, r => Assert.Empty(r.PreErrors));
        Assert.DoesNotContain(CatalogMaterializer.Issues(d, layer, new CatalogMaterializer.IssueContext(AssistantTargets.Products, "EUR")), i => i.Key == "default:currency");
    }

    [Fact]
    public void Store_currency_changed_after_confirmation_requires_a_new_confirmation()
    {
        var layer = Layer(("stockPolicy", "stocked"), ("currency", "EUR"));
        var d = CatalogInterpretationValidator.Validate(Priced, null, layer);
        Assert.Null(CatalogMaterializer.ConfirmedCurrency(layer, "CHF"));
        Assert.All(CatalogMaterializer.ProductRows(d, layer, "CHF"), r => Assert.Contains(r.PreErrors, e => e.Code == CatalogMaterializer.CurrencyUnconfirmed));
        var issue = CatalogMaterializer.Issues(d, layer, new CatalogMaterializer.IssueContext(AssistantTargets.Products, "CHF")).Single(i => i.Key == "default:currency");
        Assert.Contains("n'est plus celle du magasin", issue.Explanation, StringComparison.Ordinal);
        Assert.Equal("CHF", issue.Actions.Single(a => a.Type == DecisionTypes.FileDefault).Constraints!["value"]!.GetValue<string>());
    }

    [Fact]
    public void Unknown_store_currency_never_falls_back_to_eur()
    {
        var layer = Layer(("stockPolicy", "stocked"), ("currency", "EUR"));
        var d = CatalogInterpretationValidator.Validate(Priced, null, layer);
        Assert.Null(CatalogMaterializer.ConfirmedCurrency(layer, null));
        Assert.All(CatalogMaterializer.ProductRows(d, layer, null), r => Assert.Contains(r.PreErrors, e => e.Code == CatalogMaterializer.StoreCurrencyUnknown));
        var issue = CatalogMaterializer.Issues(d, layer, new CatalogMaterializer.IssueContext(AssistantTargets.Products, null)).Single(i => i.Key == "default:currency");
        Assert.Equal((CatalogMaterializer.StoreCurrencyUnknown, CatalogStatuses.Invalid), (issue.Code, issue.Severity));
        Assert.DoesNotContain(issue.Actions, a => a.Type == DecisionTypes.FileDefault);
    }

    [Fact]
    public void Explicit_file_currency_needs_no_confirmation_and_is_kept_as_given()
    {
        var document = TolerantCsvReader.Parse("Marque;Modèle;Coloris;Prix achat HT;Devise;Famille\nRay;A1;Noir;42,50;CHF;Monture\n", "utf-8");
        var layer = Layer(("stockPolicy", "stocked"));
        var d = CatalogInterpretationValidator.Validate(document, null, layer);
        Assert.Empty(CatalogMaterializer.LinesWithoutCurrency(d, AssistantTargets.Products));
        var row = Assert.Single(CatalogMaterializer.ProductRows(d, layer, "EUR"));
        Assert.Equal("CHF", row.Values["price:currency"]); // refusé ensuite au plan (currency_mismatch) : aucune conversion
        Assert.DoesNotContain(row.PreErrors, e => e.Field == "currency");
    }

    // ------------------------------------------------------------------ COR-CSVAI-01 / 02 : offres de verres

    private static readonly Guid Index = Guid.NewGuid();
    private static readonly Guid Treatment = Guid.NewGuid();

    private static LensOfferDetails Offer(
        DiopterRange? sphere = null, IReadOnlyList<int>? diameters = null, IReadOnlyList<LensRule>? rules = null, bool complete = false, decimal? price = 89.90m,
        string? notes = "note", int? lead = 7) =>
        new(LensTypes.Progressive, "organique", Index, LensProduction.OnOrder, lead, price, price is null ? null : "EUR", diameters ?? [65, 70], sphere ?? new DiopterRange(-10.00m, 6.00m),
            new DiopterRange(-4.00m, 0.00m), new DiopterRange(0.75m, 3.50m), [Treatment], rules ?? [], complete, "Fiche labo 2026", new DateOnly(2026, 1, 1), null, notes);

    private static readonly AssistedLensPlanner.Provided Everything = new(true, true, true, true, false, true, true, true);
    private static readonly AssistedLensPlanner.Provided Nothing = new(false, false, false, false, false, false, false, false);

    [Fact]
    public void Same_values_with_another_decimal_scale_are_unchanged()
    {
        var before = Offer(complete: true);
        var file = Offer(sphere: new DiopterRange(-10m, 6m)) with { Cylinder = new DiopterRange(-4m, 0m), Addition = new DiopterRange(0.75m, 3.5m), PriceHt = 89.9m };
        var merge = AssistedLensPlanner.Merge(before, file, Everything);
        Assert.Empty(merge.Changed);
        Assert.False(merge.Revalidation);
        Assert.True(merge.Details.RulesComplete);
    }

    [Fact]
    public void Characteristics_absent_from_the_file_are_never_erased()
    {
        var advanced = new LensRule(LensRuleKinds.SphereRange, LensRuleEffects.Allow, LensManufacturing.Standard, new LensRuleConditions([Treatment]), new DiopterRange(-8m, 4m));
        var before = Offer(rules: [advanced], complete: true);
        var file = Offer(diameters: [], price: null, notes: null, lead: null) with { Sphere = null, Cylinder = null, Addition = null, Rules = [] };
        var merge = AssistedLensPlanner.Merge(before, file, Nothing);
        Assert.Empty(merge.Changed);
        Assert.Equal(before, merge.Details);
        Assert.Single(merge.Details.Rules);
    }

    [Fact]
    public void Price_or_lead_time_update_keeps_a_complete_sheet_complete()
    {
        var before = Offer(complete: true);
        var merge = AssistedLensPlanner.Merge(before, Offer(price: 95m, lead: 10), Everything);
        Assert.Equal(["leadTimeDays", "priceHt"], merge.Changed);
        Assert.False(merge.Revalidation);
        Assert.True(merge.Details.RulesComplete);
        Assert.Equal("Fiche labo 2026", merge.Details.SourceReference);
        Assert.Equal(new DateOnly(2026, 1, 1), merge.Details.ValidFrom);
    }

    [Theory]
    [InlineData("sphere")]
    [InlineData("diameters")]
    [InlineData("combination")]
    public void Capability_update_turns_a_complete_sheet_back_to_incomplete(string what)
    {
        var before = Offer(complete: true, rules: [new LensRule(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Standard, null, null, null, 12m)]);
        var file = what switch
        {
            "sphere" => Offer(sphere: new DiopterRange(-8m, 6m)),
            "diameters" => Offer(diameters: [65]),
            _ => Offer(rules: [new LensRule(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Standard, null, null, null, 10m, Note: "import CSV")]),
        };
        var merge = AssistedLensPlanner.Merge(before, file, Everything with { Combination = what == "combination" });
        Assert.True(merge.Revalidation);
        Assert.False(merge.Details.RulesComplete);
        Assert.Contains("rulesComplete", merge.Changed);
        Assert.Contains(what == "combination" ? "rules" : what, merge.Changed);
    }

    [Fact]
    public void Imported_combination_replaces_only_the_simple_restriction_and_keeps_advanced_rules()
    {
        var conditioned = new LensRule(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Special, null, null, null, 8m);
        var simple = new LensRule(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Standard, null, null, null, 12m);
        var imported = new LensRule(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Standard, null, null, null, 10m, Note: "import CSV");
        var merge = AssistedLensPlanner.Merge(Offer(rules: [conditioned, simple]), Offer(rules: [imported]), Everything with { Combination = true });
        Assert.Equal([conditioned, imported], merge.Details.Rules);

        var twoSimple = AssistedLensPlanner.Merge(Offer(rules: [simple, simple with { MaxSphereCylinderSum = 9m }]), Offer(rules: [imported]), Everything with { Combination = true });
        Assert.Equal(new ImportError("rules", "lens_offer_rules_ambiguous"), twoSimple.Error);
    }

    [Fact]
    public void Geometry_tie_break_uses_provided_geometry_only()
    {
        var stock = Offer(diameters: [65], sphere: new DiopterRange(-4m, 4m));
        var rx = Offer(diameters: [70], sphere: new DiopterRange(-10m, 6m));
        var file = Offer(diameters: [70], sphere: new DiopterRange(-10.00m, 6.00m));
        Assert.False(AssistedLensPlanner.SameGeometry(stock, file, Everything));
        Assert.True(AssistedLensPlanner.SameGeometry(rx, file, Everything));
        Assert.False(AssistedLensPlanner.SameGeometry(rx, file, Nothing)); // sans géométrie fournie : aucun départage
    }
}
