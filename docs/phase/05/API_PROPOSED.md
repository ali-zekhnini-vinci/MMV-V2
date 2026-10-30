# 05 — Contrat d'API proposé (`/api/catalog`, `/api/suppliers`, `/api/stock`)

**PROPOSED, 29/10/2026 — rien n'est implémenté.** Recommandations A de [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md).

## 1. Règles transverses (reprises de `/api/clients` et `/api/optical`)

- HTTPS obligatoire (`403 https_required`), session `Authorization: Bearer`, `Cache-Control: no-store`, corps JSON stricts (`400 invalid_body`), paramètres de liste en liste blanche (`400 invalid_query`), pagination serveur (`page`, `pageSize` ≤ 200).
- **Droits vérifiés avant toute lecture** ; aucune permission n'en implique une autre ; `system.admin` n'en donne aucune.
- **Coûts** (`referenceCost`, `supplierCost`, `unitCost`, `marginRate`, historique des coûts) : **absents de la réponse** sans `catalog.costs.read` (la clé n'est pas émise, pas de `null`) ; tout champ de coût dans un corps d'écriture sans `catalog.costs.write` → `403 costs_forbidden`, rien n'est écrit. Indicateur `costsVisible` dans les réponses pour l'interface.
- Écritures : `requestId` (uuid) obligatoire ; `version` (`xmin`) obligatoire sauf création ; réémission = état courant, aucune réécriture ; `409 request_id_reused` si le même `requestId` porte un autre contenu.
- Erreurs à codes bornés : `404 not_found` ; `409 version_conflict`, `archived`, `stock_changed`, `inventory_open`, `request_id_reused` ; `422 validation_failed` (`errors[]` par champ), `stock_insufficient`, `policy_change_refused`, `archive_refused` (`reason`), `primary_supplier_required`.
- Aucune route `DELETE`.

## 2. Catalogue et fournisseurs (05-A)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/catalog/categories` | `catalog.read` | liste fermée + jeux d'attributs (pour le formulaire) |
| `GET /api/catalog/products?q&categoryCode&supplierId&policy&availability&status=active\|archived\|all&sort&dir&page&pageSize` | `catalog.read` | page d'articles : référence, désignation, marque, catégorie, politique, fournisseur principal, prix de vente, disponibilité, quantité (si `stocked` **et** `stock.read`), statut ; coûts si autorisés ; `asOf` |
| `GET /api/catalog/products/{id}` | `catalog.read` | fiche + `version` |
| `POST /api/catalog/products` | `catalog.write` | `{ requestId, reference, gtin?, name, brand?, description?, categoryCode, modelId?, attributes, stockPolicy, salePrice?, lowStockThreshold?, referenceCost? }` → 201 |
| `PUT /api/catalog/products/{id}` | `catalog.write` | mêmes champs sauf politique + `version` ; modification sans changement = 200 sans écriture (O-03-44) |
| `POST /api/catalog/products/{id}/policy` | `catalog.write` | `{ requestId, version, stockPolicy }` ; `422 policy_change_refused` (`stock_on_hand`, `inventory_open`, `primary_supplier_required`) |
| `POST /api/catalog/products/{id}/archive` · `/reactivate` | `catalog.archive` | `{ requestId, version, reason? }` ; `422 archive_refused` (`stock_on_hand`, `inventory_open`) |
| `GET /api/catalog/products/{id}/price-history` | `catalog.read` (+ coûts) | lignes `sale_price` ; lignes de coût avec `catalog.costs.read` |
| `GET /api/catalog/products/{id}/suppliers` | `catalog.read` + `suppliers.read` | rattachements (coût fournisseur si autorisé) |
| `PUT /api/catalog/products/{id}/suppliers/{supplierId}` | `catalog.write` + `suppliers.read` | `{ requestId, version, supplierReference?, supplierCost?, leadTimeDays?, isPrimary }` (ajout ou mise à jour) |
| `POST /api/catalog/products/{id}/suppliers/{supplierId}/remove` | `catalog.write` | `{ requestId, version }` ; refus si dernier principal d'un `order_only` actif |
| `GET/POST /api/catalog/models` | `catalog.read` / `catalog.write` | modèles (D-05-04) |
| `GET /api/suppliers?q&countryCode&hasProducts&status&sort&dir&page&pageSize` | `suppliers.read` | page de fournisseurs |
| `GET /api/suppliers/{id}` · `GET /api/suppliers/{id}/products` | `suppliers.read` (+ `catalog.read` pour les articles) | fiche, articles rattachés |
| `POST /api/suppliers` · `PUT /api/suppliers/{id}` | `suppliers.write` | champs de D-05-06 ; identifiants légaux validés selon `countryCode` |
| `POST /api/suppliers/{id}/archive` · `/reactivate` | `suppliers.write` | `422 archive_refused` (`sole_primary_supplier`) |

## 3. Stock (05-B)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/stock/balances?q&categoryCode&availability&groupBy=category&sort&dir&page&pageSize` | `stock.read` | articles `stocked` actifs : quantité, seuil, disponibilité, `version` de solde ; `asOf` |
| `GET /api/stock/movements?productId&type&from&to&performedBy&page&pageSize` | `stock.read` | journal : date, article, type, écart, avant, après, motif, auteur (nom affichable) |
| `GET /api/stock/movements/{id}` | `stock.read` (+ coûts) | détail, source, fournisseur, coût unitaire si autorisé |
| `POST /api/stock/movements` | `stock.movements.write` | `{ requestId, productId, type: receipt\|manual_in\|manual_out, quantity > 0, reasonCode, comment?, supplierId?, unitCost? }` ; `422 stock_insufficient` ; `409 archived` ; politique ≠ `stocked` → `422 validation_failed` |
| `POST /api/stock/adjustments` | `stock.adjust` | `{ requestId, productId, expectedOnHand, countedQuantity ≥ 0, reasonCode, comment? }` ; `409 stock_changed` (`currentOnHand`) si la quantité verrouillée ≠ `expectedOnHand` ; écart nul → 200 sans mouvement |
| `GET /api/catalog/products/{id}/availability` | `catalog.read` | disponibilité calculée (§ 3.5 du cadrage) |

## 4. Inventaires (05-C)

| Route | Permission | Objet |
|---|---|---|
| `GET /api/stock/inventories?status&page&pageSize` | `stock.read` | sessions |
| `POST /api/stock/inventories` | `stock.inventory.count` | `{ requestId, scopeType, scopeCategoryCode?, productIds?, blind }` → 201 ; `409 inventory_open` (liste des articles en conflit) |
| `GET /api/stock/inventories/{id}?onlyDifferences&page&pageSize` | `stock.read` | session, lignes ; quantité théorique masquée si `blind` et session ouverte |
| `PUT /api/stock/inventories/{id}/lines/{productId}` | `stock.inventory.count` | `{ requestId, version, countedQuantity }` ; mémorise `expectedAtCount` sous verrou |
| `PUT /api/stock/inventories/{id}/lines/{productId}/reason` | `stock.inventory.validate` | `{ requestId, version, reasonCode, comment? }` |
| `POST /api/stock/inventories/{id}/validate` | `stock.inventory.validate` | `{ requestId, version, uncounted: exclude\|set_to_zero, acceptChangedLines: [productId] }` ; `409 stock_changed` (lignes modifiées depuis le comptage non acceptées) ; `422 validation_failed` (motif manquant) ; tout ou rien |
| `POST /api/stock/inventories/{id}/abandon` | `stock.inventory.validate` | `{ requestId, version, reason }` |

## 5. Exports CSV (05-C)

`GET …/export.csv` sur produits, fournisseurs, soldes, mouvements, inventaire : mêmes filtres et **mêmes droits** que la liste ; colonnes de coût seulement avec `catalog.costs.read` ; UTF-8 avec BOM, séparateur `;`, nombres au format de l'installation ; neutralisation des cellules commençant par `=`, `+`, `-`, `@` (injection de formules) ; export audité (`*.exported`, sans contenu).

## 6. Contrats internes pour la Phase 06 (Application, aucune route)

```csharp
// Lecture pour le panier — aucune écriture.
interface ICatalogQuery
{
    Task<IReadOnlyList<SellableItem>> GetSellableItemsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
}
// SellableItem : ProductId, Reference, Name, CategoryCode, StockPolicy, SalePrice?, Currency, TaxCategory?,
//                Availability, AvailableQuantity? (stocked), IsArchived, Version.

// Écritures de stock dans la transaction de l'appelant (même DbContext / même transaction PostgreSQL).
interface IStockLedger
{
    // Verrouille les soldes dans l'ordre croissant des ProductId, applique toutes les lignes ou aucune.
    // Lignes order_only / service ignorées (aucun mouvement). Erreur par ligne : StockInsufficient, Archived, NotStocked.
    Task<StockApplyResult> ApplyDocumentMovementsAsync(StockSource source, IReadOnlyList<StockLine> lines, Guid requestId, CancellationToken ct);

    // Contre-mouvements liés (reverses_movement_id) de tous les mouvements d'une source ; motif obligatoire.
    Task<StockApplyResult> ReverseDocumentMovementsAsync(StockSource source, string reasonCode, Guid requestId, CancellationToken ct);
}

// Défini en 05-B, implémenté par la Phase 06 (table stock.reservations).
interface IStockReservations
{
    Task ReserveAsync(StockSource source, IReadOnlyList<StockLine> lines, Guid requestId, CancellationToken ct);
    Task ReleaseAsync(StockSource source, Guid requestId, CancellationToken ct);
    Task ConsumeAsync(StockSource source, Guid requestId, CancellationToken ct); // réservé → sortie de vente
}
```

Règles du contrat : la Phase 06 **copie** référence, désignation, politique, prix, devise et catégorie fiscale sur la ligne de vente ; elle n'appelle jamais `POST /api/stock/movements` ; elle exige `stock.read` pour afficher les quantités, pas `stock.movements.write` pour vendre (le mouvement de vente est un effet du serveur, autorisé par la permission de vente). `IStockLedger` est livré et testé en 05-B (tests d'intégration à barrières), sans appelant en production.
