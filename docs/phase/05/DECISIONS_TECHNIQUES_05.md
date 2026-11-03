# 05 — Choix techniques et hypothèses de l'implémentation (30/10/2026)

Mandat : [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md) (§ 1 « Questions ouvertes », § 2 « Règles d'implémentation retenues »). Pour chaque point ouvert du registre [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md) : réponse déjà donnée par Q01–Q20 (appliquée), règle du mandat § 2 (appliquée), ou règle technique conservatrice (appliquée **et signalée comme non ratifiée**). Les statuts du registre métier ne sont **pas** modifiés : ce document n'approuve rien, il trace ce qui a été codé.

Légende : **Q** = tranché par Q01–Q20 · **M** = règle du mandat du 30/10/2026 § 2 · **T** = choix technique conservateur, non ratifié (révisable par le propriétaire).

## 1. Questions ouvertes du registre

| Point | Source | Règle implémentée | Où |
|---|---|---|---|
| QR-01 fournisseur principal `order_only` | **M** | Fiche enregistrable sans principal, signalée `isIncomplete` / disponibilité `incomplete` (calcul serveur) ; archiver l'unique principal exige `acceptIncompleteVariants` ; principal exigé à la commande effective (phase Commandes) et à la validation d'une vente (contrat Phase 06) | `CatalogReadStore.EnrichAsync`, `SupplierService.ArchiveAsync`, `Phase06Contracts.cs` |
| QR-02 / D-05-07 archivage | **T** (option A adaptée) | Aucune suppression physique (déclencheurs `reject_delete` sur 12 tables) ; archivage modèle / variante / fournisseur / référentiel verres ; variante refusée si en main > 0, réservé > 0 ou inventaire ouvert (`archive_refused` + `reason`) ; archiver un modèle archive ses variantes, tout ou rien ; réactiver un modèle ne réactive pas ses variantes ; rattachement fournisseur « retiré » (marqué, conservé) | `CatalogService`, migration 0007 |
| QR-03 / D-05-09 motifs et ajustement | **T** (option A) | Motifs codés par type (`StockReasons.ByType`) ; `other` ⇒ commentaire obligatoire ; ajustement = quantité **constatée** + quantité **attendue** (409 `stock_changed` si le stock a bougé) ; `supplier_return` admis comme motif de sortie manuelle (écran de retour fournisseur OUT) | `Stock.cs`, `StockService` |
| QR-04 / D-05-10 inventaires | **Q10** + **T** | Sessions numérotées `INV-…` par magasin et emplacements ; périmètre complet / famille / sélection ; **pas de gel** ; comptage à l'aveugle optionnel ; quantité théorique relue **sous verrou** au comptage ; validation : écart constaté appliqué au stock **courant**, ligne modifiée depuis le comptage ⇒ 409 sauf acceptation explicite ; lignes non comptées exclues ou mises à 0 sur choix explicite ; tout écart non nul exige un motif ; abandon motivé irréversible | `InventoryService`, `InventoryRules` |
| QR-05 / D-05-11 permissions | **Q16** + **M** | Les 16 droits de la liste proposée ; `inventory.count` ≠ `inventory.validate` (M) ; aucun droit n'en implique un autre ; `system.admin` n'en donne aucun. **Non imposé** : qu'une même personne ne puisse pas compter et valider (bloquerait un magasin à un seul responsable) | `Permissions.cs`, `AuthSetup` |
| QR-06 / D-05-13 découpage | **M** (« ne pas créer artificiellement quatre migrations ») | **Une seule migration** `20261030005557_CatalogStockModule` (= « 0007 ») pour 05-A à 05-D, une seule revue G05-SQL. Les sous-lots restent des étapes internes | `docs/phase/05/sql/` |
| QR-07 lentilles / traitements | **Q03** + **T** | Gamme = modèle de la famille « verres correcteurs » (gamme obligatoire) ; variante = indice (obligatoire) + traitements **inclus** (aucun supplément) ; lentilles : attributs facultatifs `power`, `baseCurve`, `diameter`, `packSize`, `volumeMl`, `replacement` (chaînes contrôlées, aucune grille) | `Families`, `CatalogInputValidator` |
| QR-08 SKU | **Q05** + **T** | SKU **saisi**, obligatoire, unique sur `lower(btrim(sku))` ; aucune numérotation automatique | `ProductVariant.SkuKey`, `ux_product_variants_sku` |
| QR-09 arrondi | **M** | Produit exact `decimal`, UN arrondi au centime, demi vers l'extérieur ; contrôle croisé avec `round(numeric, 2)` PostgreSQL ; aucun arrondi commercial (BL-05-08) | `SalePriceCalculator` |
| QR-10 réservé | **Q09** + **T** | Colonne `stock.balances.reserved` (0, `reserved <= on_hand`) ; sortie manuelle / ajustement / inventaire ne peuvent entamer le réservé (`stock_reserved`) ; écritures de réservation : Phase 06 (`IStockReservations`, interface seulement) | `StockRules`, `Phase06Contracts.cs` |
| QR-11 pilotes | **Q20** + **T** | Aucun magasin créé par la migration ; magasins par `MMV.Maintenance stores create` ; tests : deux magasins fictifs BE-EUR / MA-MAD créés puis purgés (résidu 0) | `Phase05Harness.cs` |
| QR-12 coefficient | **M** + **T** | Coefficient strictement positif, ≤ 100, 4 décimales ; aucun coefficient par défaut ; < 1 accepté seulement avec `acceptBelowCost` (et refusé par import) | `PricingService` |
| QR-13 valorisation | **T** | Aucune valorisation (CUMP / FIFO) ; coût unitaire de réception conservé, protégé (`costs.read`) | `StockMovement.UnitCostHt` |

## 2. Choix d'architecture et de données (T)

| Sujet | Choix | Motif |
|---|---|---|
| Référentiel verres | une table `catalog.lens_references` discriminée (`kind` = manufacturer / range / index / treatment) au lieu de quatre | même cycle de vie, unicité `(kind, fabricant, nom)` `NULLS NOT DISTINCT` |
| Familles | table `catalog.families` (6 lignes insérées par la migration, aucune écriture API) ; jeux d'attributs dans le code | intégrité référentielle sans paramétrage libre |
| Attributs | `jsonb` objet de chaînes, clés validées par famille | aucun flottant, extensible sans migration |
| Tarifs | ligne `(variante, magasin)` créée à la première écriture sous verrou (`INSERT … ON CONFLICT DO NOTHING` + `FOR UPDATE`) ; version 0 = « aucune ligne » | concurrence sur un tarif inexistant |
| Devise | FK composite `(store_id, currency_code) → org.stores(id, currency_code)` sur tarifs et réceptions ; devise de magasin immuable (déclencheur) ; `EUR`, `MAD` seulement | aucune conversion possible par construction |
| Fiscalité | `pricing.tax_categories` vide (structure) ; TTC servi seulement si magasin `validated` ET catégorie au taux validé ; aucune écriture de taux par l'API ni la maintenance en Phase 05 | ADR-010 ouvert, aucun taux inventé |
| Réceptions | `stock.receipts` / `receipt_lines` (numéro `REC-…`) ; quantité annoncée facultative ⇒ réception partielle ; reliquat reçu par une réception « de complément » liée ; aucune commande fournisseur | Q12 + mandat § 5 « partielles lorsqu'elles sont pertinentes » |
| Ordre des verrous (unique) | magasin `FOR SHARE` → fournisseur `FOR SHARE` → variantes `FOR SHARE` (ordre PostgreSQL des identifiants) → soldes `FOR UPDATE` (ordre `(variante, emplacement)`) ; archivage / politique : variante `FOR NO KEY UPDATE` **avant** lecture des soldes (leçon O-04-53) ; inventaire : session `FOR SHARE` (comptage) / `FOR UPDATE` (validation) | absence d'interblocage, sorties concurrentes sérialisées |
| Idempotence | `creation_request_id` uniques (modèles, variantes, fournisseurs, sessions, lots d'import) ; `last_request_id` ; mouvements `(request_id, variante, emplacement)` unique ; réception `request_id` unique ; réémission ⇒ état courant (200), autre contenu ⇒ 409 `request_id_reused` | réseau incertain, double clic |
| Import CSV | contenu texte conservé dans `imports.batches` (≤ 2 Mo, ≤ 5 000 lignes) ; rapport `imports.rows` remplacé à chaque contrôle tant que le lot n'est pas clos ; application = plan **recalculé** contre l'état courant dans la transaction, tout ou rien ; même fichier (empreinte, type, magasin) appliqué une seule fois (index unique partiel) ; cellules `=`, `@`, `+lettre`, `-lettre` refusées ; aucune suppression, aucun prix fournisseur par import de catalogue | D-05-18 |
| Interface | module derrière `VITE_MMV_CATALOG_MODULE=enabled` ET `catalog.read` ou `stock.read` ; entrées « Produits » et « Stock & inventaires » remplacent les entrées reportées ; le poste ne calcule aucun montant (aperçu serveur) | convention du module optique |
| Exports CSV | **non livrés** en Phase 05 (prévus en 05-C par le cadrage, absents du mandat du 30/10) — voir backlog BL-05-09 | périmètre du mandat |

## 3. Hypothèses non approuvées à confirmer par le propriétaire

1. Jeux d'attributs par famille (§ 1, QR-07) et politiques admises (`services` → `service` seulement ; autres → `stocked` / `order_only`).
2. Liste des motifs (QR-03), dont `supplier_return` en sortie manuelle et `customer_return_unsold` en entrée.
3. Lignes non comptées mises à 0 : motif exigé comme pour tout écart.
4. Réactivation d'un modèle sans ses variantes.
5. Coefficient < 1 refusé par import (confirmation impossible en lot).
6. Pas d'interdiction « compteur ≠ validateur » au niveau personne.
7. Formats d'identifiants légaux : Maroc ICE 15 chiffres, IF 7–10, RC 1–10 ; Belgique numéro d'entreprise `(BE)?[01]\d{9}` — contrôles de format seulement, aucune règle juridique.
8. Plafonds : 200 variantes par modèle, 200 lignes par réception, 5 000 lignes par inventaire et par import, 2 Mo par fichier.

## Mise à jour 31/10/2026 (G05-DESIGN)

- **QR-08 SKU** : le mandat du propriétaire du 31/10/2026 demande un SKU interne **généré automatiquement**, jamais exigé pour une création normale (« correctif Q05 »). Appliqué dans l'interface : SKU proposé (préfixe de famille + 6 symboles aléatoires), lecture seule par défaut, « Personnaliser » facultatif. Le serveur reste juge de l'unicité (`ux_product_variants_sku`) ; API, schéma et migration inchangés. La ligne QR-08 ci-dessus (30/10) est conservée telle quelle (O-05-20).
- Fournisseur principal choisi à la création : rattaché à chaque variante par la route existante `PUT /api/catalog/variants/{id}/suppliers/{supplierId}` après la création (échec signalé, fiche incomplète autorisée).

## Mise à jour 03/11/2026 (lot UX-IA)

Choix techniques du lot : voir [`UX_IA_05.md`](UX_IA_05.md) § 2 (D-UX-01 à D-UX-10). En résumé : IA locale Ollama sur le serveur du magasin, point d'accès limité à la boucle locale, modèle `llama3.2` retenu après banc (`llama3` 8 B plus lent et moins précis), schéma JSON à clés fixes, une inférence par fichier, validation serveur des colonnes, des champs et des valeurs, montants toujours à confirmer, repli déterministe ; création atomique gamme + offres de verres par composition des services existants ; seuil d'alerte par (variante, magasin) réutilisant `stock.variant_store_settings`. Aucune nouvelle table.
