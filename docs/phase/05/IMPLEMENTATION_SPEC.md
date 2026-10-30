# 05 — Spécification d'implémentation proposée

> **Mise à jour 30/10/2026 (implémentation)** : ce document reste la proposition du cadrage (historique). L'implémentation réelle et ses écarts (migration unique 0007 au lieu de 0007–0010, table unique `catalog.lens_references`, réceptions `stock.receipts` / `receipt_lines`, exports CSV non livrés, routes `/api/imports/{id}/cancel` et `/api/pricing/recalculations` ajoutées) sont décrits dans [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md), [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md) et [`LIVRAISON_05.md`](LIVRAISON_05.md).

**PROPOSED — révision du 30/10/2026 après la validation métier ; aucune implémentation avant le GO de 05-A.** *Historique : version du 29/10/2026 au commit `6c207f9`.*

## 1. Architecture (ADR-001, ADR-003, inchangés)

Monolithe modulaire existant, mêmes couches que Clients / Rendez-vous / Optique ; aucun microservice, aucune extension PostgreSQL, **aucune dépendance nouvelle** (ADR-014 : calcul décimal natif `decimal` côté serveur, aucune bibliothèque décimale côté poste puisque le poste ne calcule aucun montant).

| Couche | Ajouts proposés (dossiers **à créer**, inexistants au 30/10/2026) |
|---|---|
| `src/MMV.Domain/Stores/`, `Catalog/`, `Pricing/`, `Suppliers/`, `Stock/` | magasin et devise, familles, modèles / variantes, politiques, calcul de prix (`SalePriceCalculator` : produit exact + arrondi unique), état de prix, disponibilité, règles d'archivage et de changement de politique |
| `src/MMV.Application/…` | validateurs (attributs par famille, SKU, code-barres, identifiants légaux, devise du magasin), cas d'usage, aperçu de prix, `ICatalogQuery`, `IStockLedger`, `IStockReservations` (interface) |
| `src/MMV.Infrastructure/…` | modèles EF Core 10 (schémas `org`, `catalog`, `pricing`, `stock`, `imports`), stores, verrous SQL, migrations `0007` à `0010` (jamais au démarrage) |
| `src/MMV.Api/…` | routes minimales, policies multi-permissions, projections avec / sans coûts, sérialisation des montants en chaînes, exports CSV |
| `src/MMV.Maintenance` | commandes `stores create|archive`, `locations create`, fiscalité (`tax-categories set` seulement sur décision validée), permissions `grant|revoke` (existant) |
| `src/MMV.Domain/Identity/Permissions.cs` | permissions de D-05-11 (liste QR-05) ajoutées à `All` et `Grantable` |
| `apps/desktop/src/catalog/`, `pricing/`, `stock/`, `inventories/` | écrans Lot 6 + états GAP ; sélecteur de magasin actif ; montants formatés depuis les chaînes reçues, jamais calculés ; entrée de menu derrière `VITE_MMV_CATALOG_MODULE` |

## 2. Permissions et modèles d'attribution

Droits effectifs = permissions serveur. Liste **PROPOSED** (QR-05) ; les profils du Lot 1 ne sont que des modèles d'attribution suggérés :

| Permission | Propriétaire | Responsable | Opticien | Vendeur | Technicien / accueil |
|---|:-:|:-:|:-:|:-:|:-:|
| `catalog.read` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `catalog.write` | ✓ | ✓ | ✓ | | |
| `catalog.archive` | ✓ | ✓ | | | |
| `catalog.configure` | ✓ | ✓ | | | |
| `catalog.import` | ✓ | | | | |
| `pricing.write` | ✓ | ✓ | | | |
| `pricing.manual` | ✓ | ✓ | | | |
| `costs.read` | ✓ | ✓ | ✓ | | |
| `costs.write` | ✓ | ✓ | | | |
| `suppliers.read` | ✓ | ✓ | ✓ | | |
| `suppliers.write` | ✓ | ✓ | | | |
| `stock.read` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `stock.movements.write` | ✓ | ✓ | ✓ | | |
| `stock.adjust` | ✓ | ✓ | | | |
| `inventory.count` | ✓ | ✓ | ✓ | ✓ | |
| `inventory.validate` | ✓ | ✓ | | | |

Attribution par `MMV.Maintenance permissions grant|revoke`, après application de la migration correspondante.

## 3. Protection des prix d'achat et du coefficient

1. Projection serveur distincte selon `costs.read` : prix d'achat, **coefficient** (prix HT ÷ coefficient = prix d'achat), coefficient effectif, marge, prix fournisseurs, coûts de réception ne sont **jamais sérialisés** sans le droit.
2. Écriture d'un coût sans `costs.write` → `403 costs_forbidden`, transaction annulée.
3. Exports CSV et rapports d'import : mêmes projections.
4. Audit : noms de champs seulement ; valeurs dans `pricing.price_history` sous permission.
5. Journaux techniques : aucune journalisation des corps (constat Q-04-010 reconduit).
6. Poste : aucun coût en cache persistant ; purge à la déconnexion (comportement existant).
7. Tests : absence des clés de coût sans le droit, pour chaque route et export (Q-05-108).

## 4. Calcul des prix (D-05-16)

- Domaine : `SalePriceCalculator.Compute(decimal purchaseHt, decimal coefficient, RoundingRule rule)` → produit exact puis **un seul arrondi** au centime (règle QR-09 : demi vers l'extérieur) ; aucune conversion `double`.
- Aperçu et enregistrement appellent la même fonction ; l'enregistrement compare au `expectedSalePriceHt` (`409 price_preview_stale`).
  *Note datée 30/10/2026 (intégration finale, O-05-14) :* le poste envoie l'aperçu **sans `requestId`** (`previewCatalog`, `apps/desktop/src/api/catalog.ts`), conformément au contrat `{ storeId, purchasePriceHt, coefficient }` ([`API_PROPOSED.md`](API_PROPOSED.md)) ; le serveur refuse tout membre inconnu.
- État `recalculation_pending` calculé à la lecture (mode coefficient et prix stocké ≠ calcul courant) ; jamais corrigé automatiquement.
- Contrôle croisé en test : résultat C# = `round(purchase * coefficient, 2)` PostgreSQL sur un jeu de cas limites (demi-centimes, grands montants, coefficient < 1, 4 décimales).
- TTC : `TaxCalculator` appelé seulement avec un taux **validé** du magasin ; sinon `tax_not_validated`. Aucun taux en dur dans le code ni dans les tests (les tests utilisent des taux fictifs explicitement nommés comme tels, injectés dans la base de test).

## 5. Transactions et concurrence multi-postes

| Opération | Mécanisme |
|---|---|
| Modification de modèle / variante / fournisseur / prix | concurrence optimiste `xmin`, `409 version_conflict` |
| Mouvement manuel, réception | transaction : variante `FOR SHARE` (statut, politique) → solde (variante, emplacement) `FOR UPDATE` → contrôles `on_hand + delta >= 0` et `on_hand + delta >= reserved` → mouvement (avant / après) → solde → audit. Tout ou rien |
| Ajustement | idem + comparaison `expectedOnHand` → `409 stock_changed` |
| Archivage / changement de politique | variante `FOR NO KEY UPDATE` **avant** lecture des soldes et inventaires ouverts (leçon O-04-53) |
| Plusieurs lignes (inventaire, futures ventes) | verrous dans l'ordre croissant (`variant_id`, `location_id`) ; ordre unique documenté pour toutes les phases |
| Ouverture d'inventaire | `pg_advisory_xact_lock` dédié + contrôle « un seul inventaire ouvert par (variante, emplacement) » |
| Validation d'inventaire | session `FOR UPDATE` → soldes ordonnés → comparaison à `expected_at_count` → mouvements `inventory_adjustment` → statut → audit ; `lock_timeout` borné |
| Recalcul de prix par lot | prix concernés verrouillés dans l'ordre (`variant_id`) ; liste confirmée comparée à l'état courant |
| Idempotence | `creation_request_id` / `last_request_id` ; `(request_id, variant_id, location_id)` unique (mouvements) |

Niveau d'isolation : `READ COMMITTED` + verrous explicites (comme Phase 04).

## 6. Disponibilité

Calcul unique dans le domaine (`Availability.From(policy, archived, incomplete, onHand, reserved, threshold)`) utilisé par toutes les listes, la fiche, la vue stock, `ICatalogQuery` et les exports ; jamais recalculé par le poste.

## 7. Magasins, EUR / MAD et données sensibles

- Devise portée par le magasin, garantie par clé étrangère composite ; aucune conversion, aucun total multi-devises.
- Formats d'affichage français : `1 234,50 €` / `1 234,50 MAD`, depuis la chaîne décimale reçue.
- Pilotes fictifs BE-EUR / MA-MAD : **aucune création à ce stade** ; jeux de test créés et supprimés par les tests réels après chaque G05-SQL (résidu 0).
- Identifiants légaux fournisseurs : contrôles de format seulement.
- Aucune donnée client ni de santé dans les modules 05.
- G05-LEGAL / ADR-010 : fiscalité, traçabilité des dispositifs médicaux, conservation.

## 8. Outillage de preuve (reconduit de la Phase 04)

Tests .NET unitaires (dont tests de propriétés du calcul de prix) et d'API ; PostgreSQL 18 réel sur `mmv_v2_test` après chaque G05-SQL, **barrières déterministes** pour la concurrence ; HTTPS réel ; Tauri natif ; captures comparées aux planches Lot 6 (`references/phase05/lot6/`) en clair / sombre, 1920×1080 et 1024×700 ; résidu de données de test = 0.
