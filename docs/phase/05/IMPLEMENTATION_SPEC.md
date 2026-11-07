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

## 9. Interface — composition du Lot 6 (G05-DESIGN, 31/10/2026)

- Écrans traduits du Lot 6 en réutilisant le vocabulaire V2 des Lots 2–3 (`Clients.css`) et les composants V2 ; `Catalog.css` n'ajoute que les compléments du Lot 6 (jetons MMV, clair / sombre). Kit : `CatalogUi.tsx` (en-tête, barre d'outils, puces, table en carte à hauteur d'écran, cartes, sections de formulaire, pied d'actions, modale Radix, bandeaux, KPI, fraîcheur, toast).
- Magasin actif : sélecteur unique dans la barre de contexte du module ; shell commun inchangé.
- SKU interne **généré** sur le poste (`proposeSku` : préfixe de famille + 6 symboles de `crypto.getRandomValues`), personnalisable ; unicité contrôlée par le serveur (inchangé).
- Prix : édition dans le bloc « Prix & disponibilité » ; enregistrement seulement d'une valeur **aperçue par le serveur** ; aucun calcul ni arrondi monétaire sur le poste (les seules sommes du poste sont des quantités entières des KPI de stock).
- Détail : [`LIVRAISON_05_DESIGN.md`](LIVRAISON_05_DESIGN.md) § 2.

## Note datée — 02–03/11/2026 (corrections COR-05-01 à COR-05-08)

- **Mono-magasin** : `Installation:StoreCode` (sinon magasin unique de la base, sinon `503 store_not_configured` motivé) ; `storeId` des routes facultatif et simple assertion (`404 store_not_found` s'il désigne un autre magasin) ; contrôles d'appartenance des emplacements, réceptions, mouvements, inventaires et lots d'import ; `GET /api/store`.
- **Temps réel** : hub `/hubs/changes` (SignalR, HTTPS, session MMV, `access_token` admis sur ce seul chemin), filtre `PublishesChanges` sur les groupes d'écriture (succès 2xx seulement ; routes POST de lecture marquées `NoChangeNotification`), événements sans valeur métier filtrés par droits, contrôle de révocation sans glissement (`Realtime:SessionCheckSeconds`). Poste : reconnexion sans fin, resynchronisation, indicateur, formulaires à version figée (`useEditBase`). CSP : `wss://<api>` à autoriser (ajouté au `devCsp` pour 7267 ; la CSP de production devra porter l'origine réelle).
- **Création** : `POST /api/catalog/products` (une transaction, sous-requêtes dérivées, droits par section), SKU générés par le serveur (`SkuGenerator`), motif de stock `initial_stock`.
- **Recherche** : `CatalogSearch` (catalogue, stock, fournisseurs) ; poste : délai commun `SEARCH_DEBOUNCE_MS`, page précédente conservée, surlignage.
- **Import CSV** : détection d'encodage (poste) et de séparateur, `ImportMapper`, `NumberText`, options `@decimal` / `@familyCode` / `@stockPolicy`, profils `GET|PUT /api/imports/profiles/{supplierId}/{kind}`, rapprochement par référence fournisseur et code-barres, lot de corrections.
- **Verres** : `GET|POST /api/catalog/lens-offers`, `GET|PUT /api/catalog/lens-offers/{id}`, `…/archive`, `…/reactivate`, `POST …/check` ; règles structurées ; contrat `LensPrescription` / `LensCompatibilityEvaluator` pour la Phase 06.
- Migration additive 0008 **non appliquée** ; dossier [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md).


## Note datée — 03/11/2026 (G05-SQL-0008)

- Migration 0008 **appliquée** à `mmv_v2_dev` et `mmv_v2_test` (script `9a75acea…287e715c` inchangé) ; les routes `/api/imports` (colonne `supplier_id`, profils) et `/api/catalog/lens-offers` sont opérationnelles sur les bases du propriétaire.
- Combobox fournisseur : Entrée ne sélectionne rien tant que les suggestions affichées sont celles d'une saisie antérieure (temporisation ou requête en cours) — O-05-24.
- Banc PostgreSQL en mode processus réel : magasin fixe `MMV_PG05_API_STORE` (défaut `T05BEREEL`), à configurer comme `Installation__StoreCode` du processus ; bancs à réglages serveur propres maintenus en mémoire — O-05-26.

## Note datée — 03/11/2026 (lot UX-IA)

- Assistant produit : `ProductCreationService` exige un tarif par variante et enregistre le seuil d'alerte (variante, magasin) dans la transaction de création ; route inchangée (`POST /api/catalog/products`), champ `lowStockThreshold` ajouté.
- Import : `IImportMappingAssistant` (Application), `OllamaImportAssistant` (API, `src/MMV.Api/Installation/ImportAssistantService.cs`), validation et fusion `ImportAssistantMerge` (`src/MMV.Application/Catalog/ImportAssistantAi.cs`) ; analyse au dépôt hors transaction ; apprentissage du modèle fournisseur à l'application ; `GET /api/imports/assistant`.
- Verres : `LensCatalogService` + `POST /api/catalog/lens-catalog` (gamme et offres en une transaction) ; écrans `LensReferencesView` (« Catalogue des verres ») et `LensRangeWizard`.
- Aucune migration. Détail : [`UX_IA_05.md`](UX_IA_05.md).

## Note datée — 06/11/2026 (lot CSV-IMPORT-AI-01, implémentation)

- Nouvel assistant d'import catalogue en cinq étapes (routes additives `/api/imports/assistant…`, `/api/imports/{id}/assistant…`, `/api/imports/summary`) ; contrats et transitions : [`CSV_IA_05_GATE_B.md`](CSV_IA_05_GATE_B.md) § 7–9 ; écarts : [`LIVRAISON_05_CSVAI.md`](LIVRAISON_05_CSVAI.md).
- Configuration `CatalogInterpreter` (`qwen3.5:4b`, `Mode`, `TimeoutSeconds` 120, `WizardEnabled` faux par défaut) séparée de `ImportAssistant` (parcours actuel inchangé).
- Migration `0009_CsvImportAssistant` préparée, **non appliquée** : [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md).

## Note datée — 06/11/2026 (lot CSV-IMPORT-AI-01, corrections post-audit)

- Offres de verres importées : identité = fournisseur + gamme + type + indice + matériau + traitements + approvisionnement ; diamètres, plages, restriction combinée, délai, prix, note = caractéristiques ; mise à jour des seules caractéristiques fournies et différentes ; fiche complète repassée incomplète si une capacité change (ADR-CSVAI-09, `PROPOSED`).
- Montants sans devise dans le fichier : confirmation de la devise du magasin (`file_default`, champ `currency`) exigée ; aucune conversion ; jamais d'EUR par défaut.
- Application : résultat enregistré dans `check_summary.applied` avant la clôture du lot (rejeu identique) ; plan recalculé identique au contrôle, sinon `409 check_stale` (`catalog_changed`).
- Détail : [`LIVRAISON_05_CSVAI_POST_AUDIT.md`](LIVRAISON_05_CSVAI_POST_AUDIT.md).

## Note datée — 07/11/2026 (GO conditionnel G05-SQL-0009, GATE D)

- Migration `0009_CsvImportAssistant` **appliquée** à `mmv_v2_dev` et `mmv_v2_test` (verify 14/14 ×2) ; la mention « préparée, non appliquée » ci-dessus est historique.
- Plan périmé : empreinte SHA-256 du plan métier enregistrée au contrôle (`check_summary`, clé interne `planFingerprint`, jamais servie) et recalculée à l'application ; base = état actuel des champs **fournis** (articles : libellé, références, code-barres, attributs, rattachement, tarif achat / vente / coefficient / mode / devise ; offres : caractéristiques fournies, fiche complète, valeurs écrites) ; écart → `409 check_stale` (`catalog_changed`) ; contrôle sans empreinte → `check_stale` (`check_outdated`).
- Verrous pris avant la relecture du plan d'application : variantes `FOR NO KEY UPDATE` + tarifs `FOR UPDATE`, offres de verres `FOR NO KEY UPDATE` ; conflit de version → `check_stale`.
- Mise à jour d'un article existant : référence fabricant, code-barres et traitements non fournis par le fichier conservent la valeur de la fiche (GD-01).
- Annulation d'une analyse depuis le poste : `POST /api/imports/{id}/assistant/analysis/cancel` (la politique CORS du poste n'admet jamais `DELETE`) ; `DELETE` conservé hors navigateur (GD-02).
- Détail : [`LIVRAISON_05_CSVAI_GATE_D.md`](LIVRAISON_05_CSVAI_GATE_D.md).

## Note datée — 07/11/2026 (clôture contrôlée de la Phase 05)

- Nouvel assistant d'import : **validation UX refusée par le propriétaire** ; il reste fermé côté serveur tant que `CatalogInterpreter:WizardEnabled` vaut `false` (valeur versionnée) : les 13 routes du nouveau parcours répondent `404 assistant_disabled` avant tout accès aux données, la file d'analyse n'accepte rien, seul l'état du service est servi et le poste présente l'ancien import. Garde prouvée par `tests/MMV.Api.Tests/Catalog/Assisted/CsvAssistantDisabledGateTests.cs` (toute route `/assistant` ajoutée sans garde fait échouer le test).
- Offres de verres : une offre **à créer** n'a pas de ligne à verrouiller ; deux applications concurrentes peuvent créer deux offres de même identité (état permis par 0008 pour la saisie manuelle ; import ultérieur refusé par `lens_offer_ambiguous_existing`). Correction sans migration reportée (BL-05-15).
- Aucune modification du code produit, aucune migration. Détail : [`RAPPORT_CLOTURE_PHASE05.md`](RAPPORT_CLOTURE_PHASE05.md) §§ 6 et 7.
