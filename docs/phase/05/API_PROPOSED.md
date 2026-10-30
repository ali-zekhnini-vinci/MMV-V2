# 05 — Contrat d'API proposé (`/api/catalog`, `/api/pricing`, `/api/suppliers`, `/api/stock`, `/api/inventories`)

> **Mise à jour 30/10/2026 (implémentation)** : ce document reste la proposition du cadrage (historique). L'implémentation réelle et ses écarts (migration unique 0007 au lieu de 0007–0010, table unique `catalog.lens_references`, réceptions `stock.receipts` / `receipt_lines`, exports CSV non livrés, routes `/api/imports/{id}/cancel` et `/api/pricing/recalculations` ajoutées) sont décrits dans [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md), [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md) et [`LIVRAISON_05.md`](LIVRAISON_05.md).

**PROPOSED — révision du 30/10/2026 après la validation métier — rien n'est implémenté.** Dérivé des décisions approuvées de [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md) et du modèle [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md). *Historique : version du 29/10/2026 (articles uniques, prix TTC) au commit `6c207f9`.*

## 1. Règles transverses (reprises de `/api/clients` et `/api/optical`)

- HTTPS obligatoire (`403 https_required`), session `Authorization: Bearer`, `Cache-Control: no-store`, corps JSON stricts (`400 invalid_body`), paramètres de liste en liste blanche (`400 invalid_query`), pagination serveur (`page`, `pageSize` ≤ 200).
- **Droits vérifiés avant toute lecture** ; aucune permission n'en implique une autre ; `system.admin` n'en donne aucune. Une route peut exiger **plusieurs** permissions.
- **Magasin** : les routes de prix, de stock et d'inventaire prennent `storeId` (paramètre ou corps) ; magasin inconnu ou archivé → `404` / `409 archived`.
- **Montants** : chaînes décimales à échelle fixe (`"103.13"`, prix d'achat `"41.2500"`, coefficient `"2.5000"`), toujours accompagnées de `currency` ; jamais de nombre JSON pour un montant ; un montant dont la devise diffère de celle du magasin → `422 currency_mismatch` (aucune conversion).
- **Coûts** (`purchasePriceHt`, `coefficient`, `effectiveCoefficient`, `margin`, `supplierPriceHt`, `unitCostHt`, historique des coûts) : **absents de la réponse** sans `costs.read` (clé non émise) ; tout champ de coût dans un corps d'écriture sans `costs.write` → `403 costs_forbidden`, rien n'est écrit. Indicateur `costsVisible`.
- **TTC** : `salePriceTtc` émis seulement si la fiscalité du magasin est validée et la variante rattachée à une catégorie au taux validé ; sinon `ttcStatus: "tax_not_validated"` et aucune clé `salePriceTtc`.
- Écritures : `requestId` (uuid) obligatoire ; `version` (`xmin`) obligatoire sauf création ; réémission = état courant ; `409 request_id_reused` si le même `requestId` porte un autre contenu.
- Erreurs à codes bornés : `404 not_found` ; `409 version_conflict`, `archived`, `stock_changed`, `inventory_open`, `request_id_reused`, `price_preview_stale` ; `422 validation_failed` (`errors[]` par champ), `stock_insufficient`, `stock_reserved`, `currency_mismatch`, `policy_change_refused`, `archive_refused` (`reason`), `primary_supplier_required`, `coefficient_below_one_unconfirmed`.
- Aucune route `DELETE`.

## 2. Référentiels et catalogue (05-A)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/stores` | `catalog.read` | magasins actifs : code, nom, pays, devise, statut fiscal (aucune écriture par l'API en Phase 05) |
| `GET /api/catalog/families` | `catalog.read` | six familles + jeux d'attributs + politiques admises |
| `GET /api/catalog/lens-references` | `catalog.read` | fabricants, gammes, indices, traitements |
| `POST/PUT /api/catalog/lens-references/{kind}[/{id}]` · `…/{id}/archive` | `catalog.configure` | `kind` = `manufacturers` / `ranges` / `indices` / `treatments` |
| `GET /api/catalog/models?q&familyCode&supplierId&policy&availability&incomplete&status&storeId&sort&dir&page&pageSize` | `catalog.read` | modèles avec résumé des variantes ; prix et disponibilité du magasin `storeId` ; quantités si `stock.read` |
| `GET /api/catalog/variants?…` | `catalog.read` | même liste au niveau variante (SKU, code-barres, politique, prix HT, TTC ou `ttcStatus`, disponibilité) |
| `GET /api/catalog/models/{id}` | `catalog.read` | modèle + variantes + `version` |
| `POST /api/catalog/models` | `catalog.write` | `{ requestId, familyCode, brand?, name, description?, lensRangeId?, attributes, variants: [{ sku, manufacturerReference?, barcode?, label, attributes, lensIndexId?, treatmentIds?, stockPolicy }] }` → 201 |
| `PUT /api/catalog/models/{id}` | `catalog.write` | champs du modèle + `version` ; sans changement = 200 sans écriture (O-03-44) |
| `POST /api/catalog/models/{id}/variants` · `PUT /api/catalog/variants/{id}` | `catalog.write` | ajout / modification d'une variante (politique exclue) |
| `POST /api/catalog/variants/{id}/policy` | `catalog.write` | `{ requestId, version, stockPolicy }` ; `422 policy_change_refused` (`stock_on_hand`, `stock_reserved`, `inventory_open`) |
| `POST /api/catalog/{models\|variants}/{id}/archive` · `/reactivate` | `catalog.archive` | `{ requestId, version, reason? }` ; `422 archive_refused` (QR-02) |
| `GET /api/catalog/variants/{id}/suppliers` | `catalog.read` + `suppliers.read` | rattachements (prix fournisseur si `costs.read`) |
| `PUT /api/catalog/variants/{id}/suppliers/{supplierId}` | `catalog.write` + `suppliers.read` | `{ requestId, version, supplierReference?, supplierPriceHt?, supplierCurrency?, leadTimeDays?, isPrimary }` (prix → `costs.write`) |
| `POST /api/catalog/variants/{id}/suppliers/{supplierId}/remove` | `catalog.write` | retrait ; si dernier principal d'un `order_only` actif : accepté, variante signalée incomplète (QR-01) |

## 3. Tarifs (05-A) — D-05-16

| Route | Permission | Objet |
|---|---|---|
| `GET /api/pricing/variants/{id}?storeId` | `catalog.read` (+ `costs.read` pour coûts et coefficient) | `salePriceHt`, `pricingMode`, `priceStatus` (`up_to_date` / `recalculation_pending` / `incomplete`), TTC ou `ttcStatus`, `currency` ; avec `costs.read` : `purchasePriceHt`, `coefficient`, `computedSalePriceHt`, `effectiveCoefficient`, `margin` |
| `POST /api/pricing/preview` | `pricing.write` + `costs.read` | `{ storeId, purchasePriceHt, coefficient }` → `{ computedSalePriceHt, currency, roundingRule }` ; calcul pur, **aucune écriture** |
| `PUT /api/pricing/variants/{id}` | `pricing.write` + `costs.read` (+ `costs.write` si le prix d'achat change) | `{ requestId, version, storeId, purchasePriceHt?, coefficient, expectedSalePriceHt, acceptBelowCost? }` ; serveur recalcule ; écart → `409 price_preview_stale` ; coefficient < 1 sans `acceptBelowCost` → `422 coefficient_below_one_unconfirmed` ; passe le mode à `coefficient` |
| `PUT /api/pricing/variants/{id}/purchase-price` | `costs.write` | `{ requestId, version, storeId, purchasePriceHt }` ; **ne modifie pas** le prix de vente ; état `recalculation_pending` si mode coefficient |
| `POST /api/pricing/variants/{id}/recalculate` | `pricing.write` + `costs.read` | `{ requestId, version, storeId, expectedSalePriceHt }` ; applique le prix calculé |
| `POST /api/pricing/recalculations/preview` · `/apply` | `pricing.write` + `costs.read` | recalcul par lot : liste d'aperçu (ancien / nouveau prix) puis application confirmée de la liste exacte (`409 price_preview_stale` si un élément a changé) |
| `PUT /api/pricing/variants/{id}/manual-price` | `pricing.manual` | `{ requestId, version, storeId, salePriceHt, reason? }` ; mode `manual` ; réponse sans coût si absence de `costs.read` |
| `GET /api/pricing/variants/{id}/history?storeId` | `catalog.read` (+ `costs.read`) | lignes de prix de vente et de mode ; lignes de prix d'achat / coefficient / prix fournisseur avec `costs.read` |

## 4. Fournisseurs (05-A)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/suppliers?q&countryCode&hasProducts&status&sort&dir&page&pageSize` | `suppliers.read` | page de fournisseurs |
| `GET /api/suppliers/{id}` · `GET /api/suppliers/{id}/variants` | `suppliers.read` (+ `catalog.read`) | fiche, variantes rattachées (prix fournisseur si `costs.read`) |
| `POST /api/suppliers` · `PUT /api/suppliers/{id}` | `suppliers.write` | fiche ; identifiants légaux : formats selon `countryCode` |
| `POST /api/suppliers/{id}/archive` · `/reactivate` | `suppliers.write` | avertissement listant les variantes `order_only` qui deviennent incomplètes ; confirmation `acceptIncompleteVariants` |

## 5. Stock (05-B)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/stock/balances?storeId&locationId&q&familyCode&availability&groupBy=family&sort&dir&page&pageSize` | `stock.read` | variantes `stocked` actives : en main, réservé, disponible, seuil, disponibilité, `version` ; `asOf` |
| `GET /api/stock/alerts?storeId` | `stock.read` | variantes en stock faible / rupture (D-05-19) ; compteur |
| `PUT /api/stock/thresholds/{variantId}` | `stock.adjust` | `{ requestId, version, storeId, lowStockThreshold? }` |
| `GET /api/stock/movements?storeId&locationId&variantId&type&from&to&performedBy&page&pageSize` | `stock.read` | journal : date, variante, emplacement, type, écart, avant, après, motif, auteur |
| `GET /api/stock/movements/{id}` | `stock.read` (+ `costs.read`) | détail, source, fournisseur, coût unitaire HT si autorisé |
| `POST /api/stock/movements` | `stock.movements.write` (+ `costs.write` si `unitCostHt`) | `{ requestId, variantId, locationId, type: receipt\|manual_in\|manual_out, quantity > 0, reasonCode, comment?, supplierId?, unitCostHt? }` ; `422 stock_insufficient` / `stock_reserved` ; `409 archived` ; politique ≠ `stocked` → `422 validation_failed` |
| `POST /api/stock/adjustments` | `stock.adjust` | `{ requestId, variantId, locationId, expectedOnHand, countedQuantity ≥ 0, reasonCode, comment? }` ; `409 stock_changed` ; écart nul → 200 sans mouvement |
| `GET /api/catalog/variants/{id}/availability?storeId` | `catalog.read` | disponibilité calculée (CADRAGE § 8.4) |

## 6. Inventaires (05-C)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/inventories?storeId&status&page&pageSize` | `stock.read` | sessions |
| `POST /api/inventories` | `inventory.count` | `{ requestId, storeId, locationIds, scopeType: full\|family\|selection, scopeFamilyCode?, variantIds?, blind }` → 201 ; `409 inventory_open` |
| `GET /api/inventories/{id}?onlyDifferences&page&pageSize` | `stock.read` | session, lignes ; quantité théorique masquée si `blind` et session ouverte |
| `PUT /api/inventories/{id}/lines/{variantId}/{locationId}` | `inventory.count` | `{ requestId, version, countedQuantity }` |
| `PUT /api/inventories/{id}/lines/{variantId}/{locationId}/reason` | `inventory.validate` | `{ requestId, version, reasonCode, comment? }` (écart justifié) |
| `POST /api/inventories/{id}/validate` | `inventory.validate` | `{ requestId, version, uncounted: exclude\|set_to_zero, acceptChangedLines }` ; tout ou rien |
| `POST /api/inventories/{id}/abandon` | `inventory.validate` | `{ requestId, version, reason }` |

## 7. Exports CSV (05-C) et import (05-D)

Exports `GET …/export.csv` sur modèles / variantes, prix (par magasin), fournisseurs, soldes, mouvements, inventaire : mêmes filtres et **mêmes droits** que la liste ; colonnes de coût seulement avec `costs.read` ; montants en décimal avec la devise de chaque ligne, jamais additionnés entre devises ; UTF-8 avec BOM, séparateur `;` ; neutralisation des cellules commençant par `=`, `+`, `-`, `@` ; export audité.

Import (D-05-18) : `POST /api/imports` (dépôt, `catalog.import`) → `POST /api/imports/{id}/check` (contrôle à blanc, rapport) → `GET /api/imports/{id}/report` → `POST /api/imports/{id}/apply` (`catalog.import` + `costs.write` / `pricing.manual` selon le contenu ; tout ou rien).

## 8. Contrats internes pour la Phase 06 (Application, aucune route)

```csharp
// Lecture pour le panier — aucune écriture.
interface ICatalogQuery
{
    Task<IReadOnlyList<SellableVariant>> GetSellableVariantsAsync(Guid storeId, IReadOnlyCollection<Guid> variantIds, CancellationToken ct);
}
// SellableVariant : VariantId, Sku, Label, FamilyCode, StockPolicy, IsIncomplete, SalePriceHt?, PricingMode, Currency,
//                   TaxCategoryCode?, TaxRate? (validé seulement), SalePriceTtc?, TtcStatus, Availability,
//                   AvailableQuantity? (stocked), IsArchived, Version.

// Écritures de stock dans la transaction de l'appelant.
interface IStockLedger
{
    // Verrouille les soldes dans l'ordre croissant (VariantId, LocationId), applique toutes les lignes ou aucune.
    // Lignes order_only / service ignorées. Erreurs par ligne : StockInsufficient, StockReserved, Archived, NotStocked.
    Task<StockApplyResult> ApplyDocumentMovementsAsync(StockSource source, IReadOnlyList<StockLine> lines, Guid requestId, CancellationToken ct);
    Task<StockApplyResult> ReverseDocumentMovementsAsync(StockSource source, string reasonCode, Guid requestId, CancellationToken ct);
}

// Défini en 05-B, implémenté par la Phase 06 : réservation à la VALIDATION d'une vente ou commande, jamais au devis (D-05-17).
interface IStockReservations
{
    Task ReserveAsync(StockSource validatedDocument, IReadOnlyList<StockLine> lines, Guid requestId, CancellationToken ct);
    Task ReleaseAsync(StockSource source, Guid requestId, CancellationToken ct);
    Task ConsumeAsync(StockSource source, Guid requestId, CancellationToken ct);
}
```

Règles du contrat : la Phase 06 **copie** SKU, libellé, politique, prix HT, mode, devise, magasin, taux validé et TTC sur la ligne de vente (D-05-16, non-rétroactivité) ; elle refuse toute vente réelle sans fiscalité validée et toute validation contenant une variante `order_only` incomplète (QR-01) ; elle n'appelle jamais `POST /api/stock/movements`.
