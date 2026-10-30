# 05 — Modèle de données proposé (aucune migration)

**PROPOSED, 29/10/2026.** Aucune table, aucun schéma, aucune migration n'existe. Ce modèle suit les recommandations A de [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md) ; une autre option change les points signalés. Conventions reprises des Phases 03-04 : identifiants `uuid`, horodatages `*_at_utc timestamptz`, auteurs `*_by uuid → auth.users`, archivage `archived_at_utc` / `archived_by`, concurrence optimiste sur la colonne système `xmin` (aucune colonne de version créée), idempotence `creation_request_id` unique + `last_request_id`, PostgreSQL 18 seul sans extension (ADR-003), migrations jamais au démarrage, script SQL idempotent soumis à revue (procédé G04-SQL).

## 1. Schéma `catalog` (05-A, migration `0007`)

### `catalog.categories` — liste fermée (D-05-05)

| Colonne | Type | Règle |
|---|---|---|
| `code` | `text` PK | `^[a-z][a-z_]{0,31}$`, ex. `frames`, `sunglasses`, `lenses`, `contact_lenses`, `care_products`, `accessories`, `services` |
| `label_fr` | `text` | libellé affiché |
| `attribute_set` | `text` | jeu d'attributs validé par le serveur (défini dans le code) |
| `allowed_policies` | `text[]` | ex. `services` → `{service}` ; `lenses` → `{order_only}` |
| `sort_order` | `int` | |

Lignes insérées par la migration (liste ratifiée) ; aucune écriture par l'API en Phase 05.

### `catalog.product_models` — regroupement facultatif (D-05-04)

`id`, `category_code` FK, `brand text`, `name text`, `created_*`, `archived_at_utc`. Unicité `(category_code, lower(brand), lower(name))`. Aucune quantité ni prix.

### `catalog.products`

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `reference` | `text` | obligatoire, ≤ 64 ; index unique sur `lower(btrim(reference))` |
| `gtin` | `text` null | 8, 12, 13 ou 14 chiffres, clé vérifiée par le serveur ; index unique partiel `WHERE gtin IS NOT NULL` |
| `name` | `text` | obligatoire, ≤ 200 |
| `brand` | `text` null | ≤ 100 |
| `description` | `text` null | ≤ 2000 |
| `category_code` | `text` FK | |
| `model_id` | `uuid` null FK | même catégorie (contrôle serveur) |
| `attributes` | `jsonb` | objet ; clés et types validés par le serveur selon `attribute_set` ; `CHECK (jsonb_typeof(attributes) = 'object')` |
| `stock_policy` | `text` | `CHECK IN ('stocked','order_only','service')` |
| `sale_price` | `numeric(12,2)` null | `>= 0` ; `CHECK (sale_price IS NOT NULL OR stock_policy = 'order_only')` |
| `currency` | `char(3)` | `MAD` ou `EUR` ; égal à la devise de l'installation (contrôle serveur) |
| `tax_category` | `text` null | renseignée après ADR-010 |
| `reference_cost` | `numeric(12,2)` null | **protégé** ; `>= 0` |
| `low_stock_threshold` | `int` null | `>= 0` ; `CHECK (low_stock_threshold IS NULL OR stock_policy = 'stocked')` |
| `created_at_utc`, `created_by`, `updated_at_utc`, `updated_by` | | |
| `archived_at_utc`, `archived_by`, `archive_reason` | null | `archived_at_utc >= created_at_utc` |
| `creation_request_id` | `uuid` unique | idempotence |
| `last_request_id` | `uuid` null | |

Index : `(category_code, archived_at_utc)`, `lower(name)`, `lower(brand)` (recherche par préfixe / `ILIKE` sans extension, comme Clients).

### `catalog.suppliers` (D-05-06)

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `code` | `text` | unique sur `lower(btrim(code))` |
| `name` | `text` | obligatoire |
| `country_code` | `char(2)` | `MA`, `BE` ou autre code ISO 3166-1 |
| `contact_name`, `email`, `phone` | `text` null | contact unique, minimal |
| `address_line`, `postal_code`, `city` | `text` null | |
| `ma_ice`, `ma_if`, `ma_rc` | `text` null | Maroc ; ICE = 15 chiffres (contrôle de format seulement) |
| `be_enterprise_number` | `text` null | Belgique ; format `0123.456.789` / `BE0123456789` normalisé |
| `notes` | `text` null | ≤ 2000 ; aucune donnée sensible |
| horodatages, auteurs, archivage, idempotence | | comme `products` |

### `catalog.supplier_products`

| Colonne | Type | Règle |
|---|---|---|
| `supplier_id`, `product_id` | `uuid` | PK composite |
| `supplier_reference` | `text` null | |
| `supplier_cost` | `numeric(12,2)` null | **protégé** |
| `lead_time_days` | `int` null | `0..365` |
| `is_primary` | `bool` | index unique partiel `(product_id) WHERE is_primary` |
| `created_*`, `updated_*` | | aucun archivage : on retire le rattachement (audité) |

Contrôles serveur : article `order_only` actif ⇒ un principal non archivé ; fournisseur archivé ⇒ ne peut pas devenir principal.

### `catalog.price_history` — ajout seul

`id`, `product_id`, `kind` (`sale_price` / `reference_cost` / `supplier_cost`), `supplier_id` null (pour `supplier_cost`), `old_value numeric(12,2) null`, `new_value numeric(12,2) null`, `currency`, `changed_at_utc`, `changed_by`, `request_id`. Déclencheurs refusant `UPDATE` / `DELETE` (procédé de `audit.events`). Lecture des lignes `reference_cost` / `supplier_cost` réservée à `catalog.costs.read`.

## 2. Schéma `stock` (05-B, migration `0008`)

### `stock.balances`

| Colonne | Type | Règle |
|---|---|---|
| `product_id` | `uuid` PK FK | article `stocked` (ligne créée à la première écriture ou au passage en `stocked`) |
| `on_hand` | `int` | `CHECK (on_hand >= 0)` (D-05-03 A) |
| `last_movement_seq` | `bigint` null | dernier mouvement appliqué |
| `updated_at_utc` | | |

Pas de colonne `reserved` en Phase 05 : la Phase 06 l'ajoutera avec `stock.reservations` (contrat § 4). Invariant vérifié par test et par la requête de contrôle : `on_hand = Σ quantity_delta` des mouvements de l'article.

### `stock.movements` — journal en ajout seul

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `seq` | `bigint` identité | ordre total |
| `product_id` | `uuid` FK | |
| `movement_type` | `text` | 05-B : `receipt`, `manual_in`, `manual_out`, `adjustment`, `inventory_adjustment` ; réservés (contrainte étendue plus tard par `DROP` + `ADD CONSTRAINT`) : `sale`, `sale_reversal`, `customer_return`, `supplier_return` |
| `quantity_delta` | `int` | `<> 0` ; signe cohérent avec le type (`receipt` / `manual_in` > 0, `manual_out` < 0) |
| `quantity_before`, `quantity_after` | `int` | `quantity_after = quantity_before + quantity_delta`, `quantity_after >= 0` |
| `reason_code` | `text` | liste fermée (D-05-09) ; obligatoire sauf `sale` / `sale_reversal` |
| `comment` | `text` null | ≤ 500 ; obligatoire si `reason_code = 'other'` |
| `supplier_id` | `uuid` null FK | `receipt`, `supplier_return` |
| `unit_cost` | `numeric(12,2)` null | **protégé** ; `receipt` seulement |
| `source_type`, `source_id` | `text`, `uuid` null | `inventory_session` en 05-C ; `sale`, `supplier_order` plus tard |
| `reverses_movement_id` | `uuid` null FK unique | annulation par contre-mouvement (Phase 06) |
| `performed_by`, `performed_at_utc` | | |
| `request_id` | `uuid` | unique `(request_id, product_id)` : idempotence ligne par ligne d'un même document |

Index : `(product_id, seq DESC)`, `(performed_at_utc)`, `(source_type, source_id)`. Déclencheurs refusant `UPDATE` / `DELETE`.

## 3. Inventaires (05-C, migration `0009`)

### `stock.inventory_sessions`

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `number` | `text` unique | `INV-AAAA-NNNN`, attribué dans la transaction d'ouverture |
| `scope_type` | `text` | `all` / `category` / `selection` |
| `scope_category_code` | `text` null | |
| `blind` | `bool` | comptage à l'aveugle |
| `status` | `text` | `open` / `validated` / `abandoned` |
| `opened_*`, `validated_*`, `abandoned_*`, `abandon_reason` | | cohérence statut ↔ horodatages par `CHECK` |
| `creation_request_id`, `last_request_id` | | |

### `stock.inventory_lines`

| Colonne | Type | Règle |
|---|---|---|
| `session_id`, `product_id` | | PK composite |
| `expected_at_count` | `int` null | quantité théorique lue sous verrou lors de la saisie |
| `counted_qty` | `int` null | `>= 0` ; null = non comptée |
| `counted_by`, `counted_at_utc` | null | |
| `reason_code`, `comment` | null | obligatoires à la validation si écart ≠ 0 |
| `resolution` | `text` null | `applied` / `no_difference` / `excluded` / `set_to_zero` |
| `movement_id` | `uuid` null FK | mouvement `inventory_adjustment` créé |

Un seul inventaire ouvert par article : contrôle dans la transaction d'ouverture sous `pg_advisory_xact_lock` dédié (ouvertures rares), plus vérification à chaque ajout de ligne. Session `validated` / `abandoned` : lignes figées (déclencheur refusant les modifications).

## 4. Réservations (Phase 06 — contrat seulement, aucune table en Phase 05)

`stock.reservations` (proposée pour la Phase 06) : `id`, `product_id`, `quantity > 0`, `source_type` / `source_id` (ligne de vente ou de commande), `status` (`active` / `consumed` / `released`), auteurs, dates ; `stock.balances.reserved` maintenu dans la même transaction, `CHECK (reserved >= 0 AND reserved <= on_hand)` à arbitrer avec la Phase 06 (réservation d'un article en rupture).

## 5. Permissions et audit

- Contrainte `auth.user_permissions` étendue aux douze permissions de D-05-11 (`DROP CONSTRAINT` + `ADD CONSTRAINT` dans la même transaction, procédé O-03-24), répartie entre `0007` (`catalog.*`, `suppliers.*`), `0008` (`stock.read`, `stock.movements.write`, `stock.adjust`) et `0009` (`stock.inventory.*`).
- Journal `audit.events` existant, aucune migration de l'audit : modules `catalog`, `suppliers`, `stock` ; codes conformes à `ck_events_action_format` (`^[a-z][a-z_]{0,30}\.[a-z][a-z_]{0,31}$`) : `product.created`, `product.updated`, `product.policy_changed`, `product.price_changed`, `product.archived`, `product.reactivated`, `supplier.created`, `supplier.updated`, `supplier.archived`, `supplier.reactivated`, `supplier_link.added`, `supplier_link.updated`, `supplier_link.removed`, `stock_movement.recorded`, `stock_adjustment.recorded`, `inventory.opened`, `inventory.validated`, `inventory.abandoned`. La liste `changes` ne contient **jamais** de coût (`reference_cost`, `supplier_cost`, `unit_cost`) : seulement le nom du champ modifié ; les valeurs vivent dans `catalog.price_history`, sous permission.

## 6. Points à valider à la revue SQL de chaque sous-lot

Empreinte imposée du script, `lock_timeout`, sauvegarde vérifiée de `mmv_v2_dev` avant application, contrôle de schéma (`verify.sql`), inventaire exact des objets, absence d'extension, rôle SQL applicatif distinct toujours en réserve (BL-04-04).
