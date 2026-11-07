using System.Globalization;
using System.Text.Json;
using MMV.Domain.Catalog;
using MMV.Domain.CsvImport;
using MMV.Domain.Identity;
using MMV.Domain.Pricing;

namespace MMV.Application.Catalog.Assisted;

/// <summary>Offre de verres planifiée depuis une ligne du fichier (saisie validée par <see cref="LensOfferService"/>).</summary>
/// <param name="ChangedFields">Champs modifiés d'une offre existante (vide pour une création).</param>
/// <param name="RevalidationRequired">Fiche déclarée complète dont une capacité change : la mise à jour la repasse « incomplète ».</param>
/// <param name="Basis">
/// GATE D (protection contre les plans périmés) : offre existante rapprochée, état ACTUEL de chaque caractéristique FOURNIE
/// par la ligne, statut « fiche complète » et valeurs qui seront écrites ; comparé entre le contrôle et l'application.
/// </param>
public sealed record PlannedLensOfferRow(
    int Line, string Outcome, IReadOnlyList<ImportError> Errors, IReadOnlyList<string> Omitted, string? Supplier, string? Manufacturer, string? Range, string? LensType,
    string? Index, IReadOnlyList<string> Treatments, string? OfferKey, Guid? ExistingOfferId, IReadOnlyList<string> ChangedFields, bool RevalidationRequired = false,
    string? Basis = null);

/// <summary>Référence de verres à créer sur DÉCISION explicite (fabricant ou gamme absents du référentiel).</summary>
public sealed record PlannedLensReference(string Kind, string Name, string? Manufacturer, IReadOnlyList<int> Lines);

public sealed record LensImportPlan(
    IReadOnlyList<PlannedLensOfferRow> Rows, IReadOnlyList<PlannedLensReference> References, bool ContainsCosts,
    IReadOnlyList<Func<CatalogActor, DateTimeOffset, Guid, CancellationToken, Task<Outcome<Guid>>>> Actions);

/// <summary>
/// Référentiel des verres (C3) : une ligne du fichier = une OFFRE d'un fournisseur pour une gamme (jamais une variante de
/// monture). Fabricant et gamme : références existantes, ou création sur décision explicite de l'utilisateur ; indice et
/// traitements : références existantes SEULEMENT. Plages SPH / CYL / ADD retenues seulement si min ET max sont donnés
/// (sinon omises, affichées, jamais complétées) ; restriction combinée |SPH| + |CYL| → règle structurée
/// <c>combination_limit</c> ; fiche JAMAIS déclarée complète par un import. Création / mise à jour par
/// <see cref="LensOfferService"/> (révision, instantané, audit).
/// <para>
/// IDENTITÉ d'une offre (COR-CSVAI-01, contrat COR-05-05 « capacités d'UN fournisseur pour UNE gamme ») : fournisseur,
/// gamme, type, indice, matériau, traitements et mode d'approvisionnement (stocké / sur commande : deux circuits distincts,
/// chacun avec ses plages, délais et prix). Diamètres, plages SPH / CYL / ADD, restriction combinée, délai, prix et note
/// sont des CARACTÉRISTIQUES de l'offre (le mode avancé exprime les variations de géométrie par des règles). Deux lignes
/// d'une même identité aux caractéristiques différentes ne sont ni fusionnées ni créées comme deux offres : elles sont
/// bloquées (<c>lens_offer_conflict</c>, champs en conflit cités). Plusieurs offres existantes de même identité : celle
/// de géométrie identique si elle est unique, sinon blocage (<c>lens_offer_ambiguous_existing</c>).
/// </para>
/// <para>
/// MISE À JOUR : seules les caractéristiques FOURNIES par le fichier et réellement différentes sont modifiées ; une
/// caractéristique absente du fichier (colonne absente, cellule vide, plage à une borne) conserve sa valeur, les règles
/// avancées saisies à la main sont conservées. COR-CSVAI-02 : si une capacité (diamètres, plages, règles) change, une fiche
/// déclarée complète repasse « incomplète » et doit être revalidée dans le référentiel ; sinon le statut est conservé.
/// </para>
/// </summary>
public sealed class AssistedLensPlanner(ICatalogStore store, IImportStore imports, ILensOfferStore offers, LensOfferService offerService, CatalogService catalog)
{
    /// <summary>
    /// GATE D : verrouille, dans la transaction d'application, les offres existantes rapprochées par ce plan (mise à jour ou
    /// inchangée), avant sa relecture.
    /// </summary>
    public Task LockExistingAsync(LensImportPlan plan, CancellationToken cancellationToken) =>
        offers.LockAsync([.. plan.Rows.Where(r => r.Outcome is ImportOutcomes.Update or ImportOutcomes.Unchanged && r.ExistingOfferId is not null).Select(r => r.ExistingOfferId!.Value)], cancellationToken);

    /// <summary>Caractéristiques comparées entre deux lignes d'une même identité (conflit dans le fichier).</summary>
    private static readonly string[] CharacteristicFields = ["diameters", "sphere", "cylinder", "addition", "rules", "leadTimeDays", "priceHt", "notes"];

    public async Task<LensImportPlan> PlanAsync(
        CatalogInterpretation d, AssistantUserLayer user, Guid? batchSupplierId, string fileName, string? storeCurrency, CancellationToken cancellationToken)
    {
        var references = await store.ListLensReferencesAsync(cancellationToken);
        var rows = d.DataRows.Where(CatalogInterpretation.RowImportable).ToList();
        var supplierCodes = rows.Select(r => r.Cell("supplierCode")?.Value).OfType<string>().Select(CatalogKeys.Normalize).Distinct().ToList();
        var suppliers = supplierCodes.Count == 0 ? new Dictionary<string, Domain.Suppliers.Supplier>(StringComparer.Ordinal) : await imports.SuppliersByCodeAsync(supplierCodes, cancellationToken);
        var batchSupplier = batchSupplierId is { } bs ? await store.FindSupplierAsync(bs, cancellationToken) : null;
        var plannedReferences = new Dictionary<string, (string Kind, string Name, string? Manufacturer, List<int> Lines)>(StringComparer.Ordinal);
        var existingByRange = new Dictionary<(Guid Range, Guid Supplier), IReadOnlyList<LensOffer>>();
        var result = new List<PlannedLensOfferRow>();
        var seenKeys = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var fileDetails = new Dictionary<int, LensOfferDetails>();
        var actions = new List<Func<CatalogActor, DateTimeOffset, Guid, CancellationToken, Task<Outcome<Guid>>>>();
        var pending = new List<(int Line, string Key, LensOfferInput Input, Guid? RangeId, string RangeKey, Guid SupplierId, Guid? ExistingId, uint? Version)>();
        var costs = false;
        var defaultType = user.FileDefaults.TryGetValue("lensType", out var t) ? t.Value : null;
        var defaultProduction = user.FileDefaults.TryGetValue("stockPolicy", out var p) ? Production(p.Value) : null;
        var confirmedCurrency = CatalogMaterializer.ConfirmedCurrency(user, storeCurrency);
        foreach (var r in rows)
        {
            var errors = new List<ImportError>();
            var omitted = new List<string>();
            string? V(string field) => r.Cells.FirstOrDefault(c => c.Field == field && d.Columns[c.C].Importable)?.Value;

            // Fournisseur de l'offre.
            Domain.Suppliers.Supplier? supplier = null;
            if (V("supplierCode") is { } code)
            {
                if (!suppliers.TryGetValue(CatalogKeys.Normalize(code), out supplier) || supplier.IsArchived)
                {
                    errors.Add(new("supplierCode", supplier is null ? CatalogFieldErrors.Unknown : CatalogFieldErrors.Archived));
                    supplier = null;
                }
            }
            else if (batchSupplier is { IsArchived: false })
            {
                supplier = batchSupplier;
            }
            else
            {
                errors.Add(new("supplierCode", "supplier_required"));
            }

            // Fabricant et gamme (références ; création seulement sur décision).
            var manufacturerName = V("lensManufacturer") ?? V("brand");
            var rangeName = V("lensRange") ?? V("modelName");
            var manufacturer = manufacturerName is null ? null : references.FirstOrDefault(x => x.Kind == LensReferenceKind.Manufacturer && !x.IsArchived && Same(x.Name, manufacturerName));
            if (manufacturerName is null)
            {
                errors.Add(new("lensManufacturer", CatalogFieldErrors.Required));
            }
            else if (manufacturer is null)
            {
                var key = AssistantUserLayer.ReferenceKey("manufacturer", manufacturerName);
                Track(key, "manufacturer", manufacturerName, null, r.Line);
                if (!user.CreateReferences.ContainsKey(key))
                {
                    errors.Add(new("lensManufacturer", "lens_reference_unknown"));
                }
            }

            LensReference? range = null;
            if (rangeName is null)
            {
                errors.Add(new("lensRange", CatalogFieldErrors.Required));
            }
            else if (manufacturer is not null)
            {
                range = references.FirstOrDefault(x => x.Kind == LensReferenceKind.Range && !x.IsArchived && x.ManufacturerId == manufacturer.Id && Same(x.Name, rangeName));
            }

            var rangeKey = AssistantUserLayer.ReferenceKey("range", rangeName ?? string.Empty, manufacturerName);
            if (rangeName is not null && range is null)
            {
                Track(rangeKey, "range", rangeName, manufacturerName, r.Line);
                if (!user.CreateReferences.ContainsKey(rangeKey))
                {
                    errors.Add(new("lensRange", "lens_reference_unknown"));
                }
            }

            // Géométrie, indice, traitements (référentiel existant seulement).
            var typeText = V("lensType");
            var lensType = typeText is null ? defaultType : CatalogLexicon.LensTypeOf(typeText);
            if (lensType is null)
            {
                errors.Add(new("lensType", typeText is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown));
            }

            Guid? indexId = null;
            if (V("lensIndex") is { } indexText)
            {
                var indexValue = MoneyText.TryParse(indexText.Replace(',', '.'), 3);
                indexId = references.FirstOrDefault(x => x.Kind == LensReferenceKind.Index && !x.IsArchived && x.IndexValue == indexValue)?.Id;
                if (indexId is null)
                {
                    errors.Add(new("lensIndex", CatalogFieldErrors.Unknown));
                }
            }

            var treatmentNames = (V("lensTreatments") ?? string.Empty).Split(['|', ',', ';', '+', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var treatmentIds = new List<Guid>();
            foreach (var name in treatmentNames)
            {
                var treatment = references.FirstOrDefault(x => x.Kind == LensReferenceKind.Treatment && !x.IsArchived && (x.ManufacturerId is null || x.ManufacturerId == manufacturer?.Id) && Same(x.Name, name));
                if (treatment is null)
                {
                    errors.Add(new("lensTreatments", CatalogFieldErrors.Unknown));
                }
                else
                {
                    treatmentIds.Add(treatment.Id);
                }
            }

            // Plages : jamais complétées (une borne seule = omise et affichée).
            (string? Min, string? Max) RangeOf(string name, string minField, string maxField)
            {
                var (min, max) = (V(minField), V(maxField));
                if ((min is null) != (max is null))
                {
                    omitted.Add($"{name} : borne {(min is null ? "minimale" : "maximale")} absente, plage non importée");
                    return (null, null);
                }

                return (min, max);
            }

            var sphere = RangeOf("sphère", "sphereMin", "sphereMax");
            var cylinder = RangeOf("cylindre", "cylinderMin", "cylinderMax");
            var addition = RangeOf("addition", "additionMin", "additionMax");
            if (lensType is not null && addition.Min is not null && !LensTypes.UsesAddition(lensType))
            {
                errors.Add(new("additionMin", "addition_not_applicable"));
            }

            var diameters = (V("diameter") ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => decimal.TryParse(x, NumberStyles.Number, CultureInfo.InvariantCulture, out var dm) && dm == Math.Truncate(dm) ? (int?)dm : null).ToList();
            if (diameters.Any(x => x is null))
            {
                errors.Add(new("diameter", CatalogFieldErrors.InvalidFormat));
            }

            // Devise (COR-CSVAI-03) : celle du fichier, sinon la devise du magasin CONFIRMÉE ; jamais supposée, aucune conversion.
            var price = V("purchasePriceHt");
            costs |= price is not null;
            var currency = price is null ? null : V("currency") ?? confirmedCurrency;
            if (price is not null && currency is null)
            {
                errors.Add(new("currency", CatalogMaterializer.MissingCurrency(storeCurrency)));
            }

            var production = V("stockPolicy") is { } policy ? Production(policy) : defaultProduction;
            if (production is null)
            {
                errors.Add(new("stockPolicy", V("stockPolicy") is null ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat));
            }

            var rules = new List<LensRuleInput>();
            if (V("combinationMax") is { } combination)
            {
                rules.Add(new LensRuleInput(LensRuleKinds.CombinationLimit, LensRuleEffects.Deny, LensManufacturing.Standard, null, null, null, combination, null, null, "import CSV"));
            }

            var provided = new Provided(
                V("diameter") is not null, sphere.Min is not null, cylinder.Min is not null, addition.Min is not null, V("combinationMax") is not null,
                V("leadTimeDays") is not null, price is not null && currency is not null, V("notes") is not null);
            var source = $"Import CSV {fileName} L{r.Line.ToString(CultureInfo.InvariantCulture)}";
            var input = new LensOfferInput(
                lensType, V("lensMaterial"), indexId, production, V("leadTimeDays") is { } lead ? int.Parse(lead, CultureInfo.InvariantCulture) : null, price, currency,
                [.. diameters.OfType<int>()], sphere.Min, sphere.Max, cylinder.Min, cylinder.Max, addition.Min, addition.Max, treatmentIds, rules,
                RulesComplete: false, source.Length <= LensOffer.SourceMaxLength ? source : source[..LensOffer.SourceMaxLength], null, null, V("notes"), "Import CSV (assistant)");
            var validation = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var details = await offerService.ValidateInputAsync(input, validation, cancellationToken);
            errors.AddRange(validation.Where(e => !(e.Key is "lensType" or "production" && errors.Any(x => x.Field is "lensType" or "stockPolicy"))
                && !(e.Key == "priceCurrency" && errors.Any(x => x.Field == "currency"))).Select(e => new ImportError(e.Key, e.Value[0])));
            // Caractéristiques comparables même si la ligne a d'autres erreurs (devise à confirmer…) : conflit ou doublon exact.
            var comparable = details;
            if (comparable is null)
            {
                comparable = await offerService.ValidateInputAsync(input with { PriceHt = null, PriceCurrency = null }, new Dictionary<string, string[]>(StringComparer.Ordinal), cancellationToken);
                comparable = comparable is null ? null : comparable with { PriceHt = price is null ? null : MoneyText.TryParse(price.Replace(',', '.'), MoneyText.PurchaseScale), PriceCurrency = currency };
            }

            if (comparable is not null)
            {
                fileDetails[r.Line] = comparable;
            }

            // Identité : fournisseur, gamme, type, indice, matériau, traitements, approvisionnement (jamais la géométrie).
            var offerKey = supplier is null ? null
                : $"{supplier.Id:N}|{rangeKey}|{lensType}|{indexId}|{CatalogLexicon.Norm(V("lensMaterial") ?? string.Empty)}|{string.Join(",", treatmentIds.Order())}|{production}";
            if (offerKey is not null)
            {
                if (!seenKeys.TryGetValue(offerKey, out var lines))
                {
                    seenKeys[offerKey] = lines = [];
                }

                lines.Add(r.Line);
            }

            Guid? existingId = null;
            uint? version = null;
            IReadOnlyList<string> changed = [];
            var revalidation = false;
            string? basis = null;
            var outcome = errors.Count > 0 ? ImportOutcomes.Error : ImportOutcomes.Create;
            if (errors.Count == 0 && range is not null && supplier is not null && details is not null)
            {
                if (!existingByRange.TryGetValue((range.Id, supplier.Id), out var existingOffers))
                {
                    existingByRange[(range.Id, supplier.Id)] = existingOffers = await offers.ListAsync(range.Id, supplier.Id, includeArchived: false, cancellationToken);
                }

                var matches = existingOffers.Where(o => o.LensTypeCode == lensType && o.IndexId == indexId && o.ProductionCode == production
                    && CatalogLexicon.Norm(o.Material ?? string.Empty) == CatalogLexicon.Norm(V("lensMaterial") ?? string.Empty)
                    && o.Treatments.Select(x => x.TreatmentId).Order().SequenceEqual(treatmentIds.Order())).ToList();
                if (matches.Count > 1)
                {
                    // Plusieurs offres de même identité (saisies à la main) : seule une géométrie identique les départage.
                    var exact = matches.Where(o => SameGeometry(o.Snapshot(), details, provided)).ToList();
                    matches = exact.Count == 1 ? exact : matches;
                }

                if (matches.Count > 1)
                {
                    errors.Add(new("offer", "lens_offer_ambiguous_existing"));
                    outcome = ImportOutcomes.Error;
                }
                else if (matches.Count == 1)
                {
                    var before = matches[0].Snapshot();
                    var merge = Merge(before, details, provided);
                    if (merge.Error is { } mergeError)
                    {
                        errors.Add(mergeError);
                        outcome = ImportOutcomes.Error;
                    }
                    else
                    {
                        existingId = matches[0].Id;
                        version = matches[0].Version;
                        changed = merge.Changed;
                        revalidation = merge.Revalidation;
                        basis = Basis(matches[0].Id, before, merge, provided);
                        outcome = changed.Count == 0 ? ImportOutcomes.Unchanged : ImportOutcomes.Update;
                        input = ToInput(merge.Details, revalidation ? "Import CSV (assistant) : capacités modifiées, fiche à revalider" : "Import CSV (assistant)");
                        var check = new Dictionary<string, string[]>(StringComparer.Ordinal);
                        if (outcome == ImportOutcomes.Update && await offerService.ValidateInputAsync(input, check, cancellationToken) is null)
                        {
                            // Offre existante devenue invalide (référence archivée…) : corriger dans le référentiel, jamais en silence.
                            errors.AddRange(check.Select(e => new ImportError("existing:" + e.Key, e.Value[0])));
                            outcome = ImportOutcomes.Error;
                        }
                    }
                }
            }

            result.Add(new PlannedLensOfferRow(
                r.Line, outcome, errors, omitted, supplier?.Code, manufacturerName, rangeName, lensType, V("lensIndex"), treatmentNames, offerKey, existingId, changed, revalidation, basis));
            if (outcome is ImportOutcomes.Create or ImportOutcomes.Update)
            {
                pending.Add((r.Line, offerKey!, input, range?.Id, rangeKey, supplier!.Id, existingId, version));
            }
        }

        // Lignes d'une même identité dans le fichier : doublon exact, ou conflit de caractéristiques (jamais fusionnées).
        foreach (var (_, lines) in seenKeys.Where(k => k.Value.Count > 1))
        {
            var known = lines.Where(fileDetails.ContainsKey).Select(l => fileDetails[l]).ToList();
            List<string> conflicts = known.Count < 2 ? [] : CharacteristicFields.Where(f => known.Skip(1).Any(x => !SameField(f, known[0], x))).ToList();
            var error = conflicts.Count == 0 ? new ImportError("offer", "lens_offer_duplicate") : new ImportError("offer:" + string.Join("+", conflicts), "lens_offer_conflict");
            for (var i = 0; i < result.Count; i++)
            {
                if (lines.Contains(result[i].Line))
                {
                    result[i] = result[i] with { Outcome = ImportOutcomes.Error, Errors = [.. result[i].Errors, error] };
                }
            }

            pending.RemoveAll(x => lines.Contains(x.Line));
        }

        // Actions (exécutées dans LA transaction d'application) : références décidées, puis offres.
        var createdReferences = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var neededReferences = plannedReferences.Where(x => user.CreateReferences.ContainsKey(x.Key) && pending.Count > 0).ToList();
        foreach (var (key, reference) in neededReferences.OrderBy(x => x.Value.Kind == "manufacturer" ? 0 : 1))
        {
            actions.Add(async (actor, now, requestId, ct) =>
            {
                Guid? manufacturerId = null;
                if (reference.Kind == "range")
                {
                    manufacturerId = createdReferences.GetValueOrDefault(AssistantUserLayer.ReferenceKey("manufacturer", reference.Manufacturer ?? string.Empty)) is { } created && created != Guid.Empty
                        ? created
                        : (await store.ListLensReferencesAsync(ct)).FirstOrDefault(x => x.Kind == LensReferenceKind.Manufacturer && !x.IsArchived && Same(x.Name, reference.Manufacturer ?? string.Empty))?.Id;
                }

                var outcome = await catalog.CreateLensReferenceAsync(
                    reference.Kind == "range" ? LensReferenceKind.Range : LensReferenceKind.Manufacturer, reference.Name, manufacturerId, null, ProductCreationService.Derive(requestId, "ref:" + key), actor.UserId, ct);
                if (outcome.IsSuccess)
                {
                    createdReferences[key] = outcome.Value;
                }

                return outcome;
            });
        }

        for (var i = 0; i < pending.Count; i++)
        {
            var item = pending[i];
            var index = i;
            actions.Add(async (actor, now, requestId, ct) =>
            {
                if (item.ExistingId is { } id)
                {
                    return await offerService.UpdateAsync(id, item.Input, item.Version!.Value, ProductCreationService.Derive(requestId, $"offer:{index}"), actor, ct);
                }

                var rangeId = item.RangeId ?? createdReferences.GetValueOrDefault(item.RangeKey);
                return await offerService.CreateAsync(rangeId, item.SupplierId, item.Input, ProductCreationService.Derive(requestId, $"offer:{index}"), actor, ct);
            });
        }

        return new LensImportPlan(
            result, [.. plannedReferences.Values.Select(x => new PlannedLensReference(x.Kind, x.Name, x.Manufacturer, x.Lines))], costs, actions);

        void Track(string key, string kind, string name, string? manufacturerName, int line)
        {
            if (!plannedReferences.TryGetValue(key, out var entry))
            {
                plannedReferences[key] = entry = (kind, name, manufacturerName, []);
            }

            entry.Lines.Add(line);
        }
    }

    /// <summary>Caractéristiques réellement FOURNIES par la ligne (une caractéristique absente n'efface jamais l'existant).</summary>
    public sealed record Provided(bool Diameters, bool Sphere, bool Cylinder, bool Addition, bool Combination, bool LeadTime, bool Price, bool Notes);

    /// <summary>Contenu fusionné, champs modifiés, revalidation requise (fiche complète devenue incomplète), refus éventuel.</summary>
    public sealed record MergeResult(LensOfferDetails Details, IReadOnlyList<string> Changed, bool Revalidation, ImportError? Error);

    /// <summary>
    /// Contenu mis à jour d'une offre existante : identité et provenance conservées ; caractéristiques fournies et
    /// différentes remplacées ; règles avancées conservées (seule la restriction combinée simple issue d'un import est
    /// remplacée par celle du fichier) ; fiche complète repassée incomplète si une capacité change (COR-CSVAI-02).
    /// </summary>
    public static MergeResult Merge(LensOfferDetails before, LensOfferDetails file, Provided provided)
    {
        var diameters = provided.Diameters && !before.Diameters.SequenceEqual(file.Diameters) ? file.Diameters : before.Diameters;
        var sphere = provided.Sphere && before.Sphere != file.Sphere ? file.Sphere : before.Sphere;
        var cylinder = provided.Cylinder && before.Cylinder != file.Cylinder ? file.Cylinder : before.Cylinder;
        var addition = provided.Addition && before.Addition != file.Addition ? file.Addition : before.Addition;
        var lead = provided.LeadTime ? file.LeadTimeDays : before.LeadTimeDays;
        var priceChanged = provided.Price && (before.PriceHt != file.PriceHt || !string.Equals(before.PriceCurrency, file.PriceCurrency, StringComparison.OrdinalIgnoreCase));
        var notes = provided.Notes && !string.Equals(before.Notes, file.Notes, StringComparison.Ordinal) ? file.Notes : before.Notes;
        var rules = before.Rules;
        if (provided.Combination && file.Rules.FirstOrDefault(x => x.Kind == LensRuleKinds.CombinationLimit) is { } imported)
        {
            var simple = before.Rules.Where(IsSimpleCombination).ToList();
            if (simple.Count > 1)
            {
                return new MergeResult(before, [], false, new ImportError("rules", "lens_offer_rules_ambiguous"));
            }

            if (simple.Count == 0 || simple[0].MaxSphereCylinderSum != imported.MaxSphereCylinderSum)
            {
                rules = [.. before.Rules.Where(x => !IsSimpleCombination(x)), imported];
            }
        }

        var capabilities = !before.Diameters.SequenceEqual(diameters) || before.Sphere != sphere || before.Cylinder != cylinder || before.Addition != addition || !SameRules(before.Rules, rules);
        var merged = before with
        {
            Diameters = diameters, Sphere = sphere, Cylinder = cylinder, Addition = addition, LeadTimeDays = lead, Rules = rules, Notes = notes,
            PriceHt = priceChanged ? file.PriceHt : before.PriceHt, PriceCurrency = priceChanged ? file.PriceCurrency : before.PriceCurrency,
            RulesComplete = before.RulesComplete && !capabilities,
            SourceReference = before.SourceReference ?? file.SourceReference,
        };
        var changed = new List<string>();
        foreach (var field in CharacteristicFields)
        {
            if (!SameField(field, before, merged))
            {
                changed.Add(field);
            }
        }

        if (before.RulesComplete != merged.RulesComplete)
        {
            changed.Add("rulesComplete");
        }

        if (changed.Count > 0 && !string.Equals(before.SourceReference, merged.SourceReference, StringComparison.Ordinal))
        {
            changed.Add("sourceReference");
        }

        return new MergeResult(changed.Count == 0 ? before : merged, changed, before.RulesComplete && !merged.RulesComplete, null);
    }

    /// <summary>
    /// GATE D : base du plan d'une offre existante (voir <see cref="PlannedLensOfferRow.Basis"/>) : identifiant, état actuel
    /// des caractéristiques FOURNIES par la ligne, statut « fiche complète », valeurs écrites des champs modifiés. Une
    /// caractéristique que le fichier ne fournit pas n'y figure pas : sa modification par un autre poste est sans conséquence
    /// (la mise à jour relit et conserve sa valeur).
    /// </summary>
    public static string Basis(Guid offerId, LensOfferDetails before, MergeResult merge, Provided provided)
    {
        var state = new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["offer"] = offerId.ToString("N"), ["rulesComplete"] = before.RulesComplete };
        if (provided.Diameters)
        {
            state["diameters"] = before.Diameters;
        }

        if (provided.Sphere)
        {
            state["sphere"] = before.Sphere;
        }

        if (provided.Cylinder)
        {
            state["cylinder"] = before.Cylinder;
        }

        if (provided.Addition)
        {
            state["addition"] = before.Addition;
        }

        if (provided.Combination)
        {
            state["rules"] = before.Rules;
        }

        if (provided.LeadTime)
        {
            state["leadTimeDays"] = before.LeadTimeDays;
        }

        if (provided.Price)
        {
            state["price"] = $"{before.PriceHt?.ToString(CultureInfo.InvariantCulture)}|{before.PriceCurrency}";
        }

        if (provided.Notes)
        {
            state["notes"] = before.Notes;
        }

        var written = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in merge.Changed)
        {
            written[field] = field switch
            {
                "diameters" => merge.Details.Diameters,
                "sphere" => merge.Details.Sphere,
                "cylinder" => merge.Details.Cylinder,
                "addition" => merge.Details.Addition,
                "rules" => merge.Details.Rules,
                "leadTimeDays" => merge.Details.LeadTimeDays,
                "priceHt" => $"{merge.Details.PriceHt?.ToString(CultureInfo.InvariantCulture)}|{merge.Details.PriceCurrency}",
                "notes" => merge.Details.Notes,
                "rulesComplete" => merge.Details.RulesComplete,
                "sourceReference" => merge.Details.SourceReference,
                _ => null,
            };
        }

        state["written"] = written;
        return JsonSerializer.Serialize(state);
    }

    /// <summary>Restriction |SPH| + |CYL| simple (sans condition, fabrication standard, interdiction) : la forme qu'écrit un import.</summary>
    private static bool IsSimpleCombination(LensRule rule) =>
        rule.Kind == LensRuleKinds.CombinationLimit && rule.Effect == LensRuleEffects.Deny && rule.Manufacturing == LensManufacturing.Standard && rule.Conditions is null;

    /// <summary>Géométrie FOURNIE identique (départage de plusieurs offres existantes de même identité).</summary>
    public static bool SameGeometry(LensOfferDetails existing, LensOfferDetails file, Provided provided) =>
        (provided.Diameters || provided.Sphere || provided.Cylinder || provided.Addition)
        && (!provided.Diameters || existing.Diameters.SequenceEqual(file.Diameters))
        && (!provided.Sphere || existing.Sphere == file.Sphere)
        && (!provided.Cylinder || existing.Cylinder == file.Cylinder)
        && (!provided.Addition || existing.Addition == file.Addition);

    /// <summary>Comparaison SÉMANTIQUE (décimaux par valeur : 6 = 6,00 ; listes par contenu).</summary>
    private static bool SameField(string field, LensOfferDetails a, LensOfferDetails b) => field switch
    {
        "diameters" => a.Diameters.SequenceEqual(b.Diameters),
        "sphere" => a.Sphere == b.Sphere,
        "cylinder" => a.Cylinder == b.Cylinder,
        "addition" => a.Addition == b.Addition,
        "rules" => SameRules(a.Rules, b.Rules),
        "leadTimeDays" => a.LeadTimeDays == b.LeadTimeDays,
        "priceHt" => a.PriceHt == b.PriceHt && string.Equals(a.PriceCurrency, b.PriceCurrency, StringComparison.OrdinalIgnoreCase),
        "notes" => string.Equals(a.Notes, b.Notes, StringComparison.Ordinal),
        _ => true,
    };

    private static bool SameRules(IReadOnlyList<LensRule> a, IReadOnlyList<LensRule> b) => a.Count == b.Count && a.Zip(b).All(x => SameRule(x.First, x.Second));

    private static bool SameRule(LensRule a, LensRule b) =>
        a.Kind == b.Kind && a.Effect == b.Effect && a.Manufacturing == b.Manufacturing && a.Range == b.Range && SameInts(a.Diameters, b.Diameters)
        && a.MaxSphereCylinderSum == b.MaxSphereCylinderSum && a.SurchargeHt == b.SurchargeHt && a.ExtraLeadDays == b.ExtraLeadDays && a.Note == b.Note
        && (a.Conditions is null ? b.Conditions is null : b.Conditions is not null
            && SameGuids(a.Conditions.TreatmentIds, b.Conditions.TreatmentIds) && SameGuids(a.Conditions.IndexIds, b.Conditions.IndexIds)
            && a.Conditions.Material == b.Conditions.Material && a.Conditions.Sphere == b.Conditions.Sphere && a.Conditions.Cylinder == b.Conditions.Cylinder
            && a.Conditions.Addition == b.Conditions.Addition);

    private static bool SameInts(IReadOnlyList<int>? a, IReadOnlyList<int>? b) => (a ?? []).SequenceEqual(b ?? []);

    private static bool SameGuids(IReadOnlyList<Guid>? a, IReadOnlyList<Guid>? b) => (a ?? []).Order().SequenceEqual((b ?? []).Order());

    /// <summary>Saisie équivalente à un contenu d'offre (revalidée par <see cref="LensOfferService.UpdateAsync"/>).</summary>
    private static LensOfferInput ToInput(LensOfferDetails x, string reason)
    {
        static string? D(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
        static LensRangeInput? R(DiopterRange? range) => range is null ? null : new LensRangeInput(D(range.Min), D(range.Max));
        return new LensOfferInput(
            x.LensType, x.Material, x.IndexId, x.Production, x.LeadTimeDays, D(x.PriceHt), x.PriceHt is null ? null : x.PriceCurrency, [.. x.Diameters],
            D(x.Sphere?.Min), D(x.Sphere?.Max), D(x.Cylinder?.Min), D(x.Cylinder?.Max), D(x.Addition?.Min), D(x.Addition?.Max), [.. x.TreatmentIds],
            [
                .. x.Rules.Select(rule => new LensRuleInput(
                    rule.Kind, rule.Effect, rule.Manufacturing,
                    rule.Conditions is { } c ? new LensConditionsInput(c.TreatmentIds, c.IndexIds, c.Material, R(c.Sphere), R(c.Cylinder), R(c.Addition)) : null,
                    R(rule.Range), rule.Diameters, D(rule.MaxSphereCylinderSum), D(rule.SurchargeHt), rule.ExtraLeadDays, rule.Note)),
            ],
            x.RulesComplete, x.SourceReference, x.ValidFrom, x.ValidUntil, x.Notes, reason);
    }

    private static string? Production(string value) => CatalogLexicon.Norm(value) switch
    {
        "stocked" or "stocke" or "stock" or "en stock" or "stocks" => LensProduction.Stocked,
        "on order" or "on_order" or "order only" or "order_only" or "sur commande" or "commande" or "fabrication" or "rx" => LensProduction.OnOrder,
        _ => null,
    };

    private static bool Same(string left, string right) => CatalogKeys.Normalize(left) == CatalogKeys.Normalize(right);

    /// <summary>Permissions requises pour appliquer un import de verres.</summary>
    public static IReadOnlyList<string> RequiredPermissions(bool containsCosts) =>
        containsCosts ? [Permissions.CatalogImport, Permissions.CatalogConfigure, Permissions.CostsWrite] : [Permissions.CatalogImport, Permissions.CatalogConfigure];
}
