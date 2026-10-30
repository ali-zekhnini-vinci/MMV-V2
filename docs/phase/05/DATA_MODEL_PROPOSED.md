# 05 — Modèle de données proposé (aucune migration)

**PROPOSED — révision du 30/10/2026 après la validation métier (G05-METIER).** Aucune table, aucun schéma, aucune migration n'existe ; aucune migration `0007` n'est générée. Le modèle dérive **exclusivement** des décisions approuvées ([`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md) : D-05-02, 03, 04, 05, 06, 14 à 21) et de l'architecture MMV V2 ; aucun nom, champ ou énumération n'est repris du Lot 6 ni de l'ancien logiciel (D-05-01). Les points marqués **(QR-nn)** dépendent d'une question encore ouverte.

*Historique : la version du 29/10/2026 (article unique, prix TTC, devise de l'installation, multi-magasin hors périmètre) est conservée au commit `6c207f9` ; elle est remplacée par celle-ci.*

Conventions reprises des Phases 03-04 : identifiants `uuid`, horodatages `*_at_utc timestamptz`, auteurs `*_by uuid → auth.users`, archivage `archived_at_utc` / `archived_by` / `archive_reason`, concurrence optimiste sur la colonne système `xmin` (aucune colonne de version), idempotence `creation_request_id` unique + `last_request_id`, PostgreSQL 18 seul sans extension (ADR-003), migrations jamais au démarrage, script SQL idempotent soumis à revue (procédé G04-SQL). **Montants en `numeric`, jamais en `real` / `double precision`.**

## 0. Vue d'ensemble

| Schéma | Tables | Sous-lot / migration |
|---|---|---|
| `org` | `stores`, `locations` | 05-A / `0007` |
| `catalog` | `families`, `lens_manufacturers`, `lens_ranges`, `lens_indices`, `lens_treatments`, `product_models`, `product_variants`, `variant_lens_treatments`, `suppliers`, `variant_suppliers` | 05-A / `0007` |
| `pricing` | `variant_store_prices`, `price_history`, `tax_categories` | 05-A / `0007` |
| `stock` | `variant_store_settings`, `balances`, `movements` | 05-B / `0008` |
| `stock` | `inventory_sessions`, `inventory_lines` | 05-C / `0009` |
| `imports` | `batches`, `rows` | 05-D / `0010` |

## 1. Schéma `org` — magasins et emplacements (D-05-15, D-05-20)

### `org.stores`

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `code` | `text` | unique sur `lower(btrim(code))`, ≤ 32 |
| `name` | `text` | obligatoire, ≤ 120 |
| `country_code` | `char(2)` | ISO 3166-1 alpha-2 (`BE`, `MA` pour les pilotes) |
| `currency_code` | `char(3)` | ISO 4217, `CHECK (currency_code IN ('EUR','MAD'))` en Phase 05 ; **non modifiable** dès qu'un prix ou un mouvement existe (déclencheur) |
| `fiscal_profile_status` | `text` | `CHECK IN ('not_validated','validated')`, défaut `not_validated` |
| `fiscal_validated_at_utc`, `fiscal_validated_by` | null | renseignés seulement au passage `validated` |
| `archived_*`, `created_*` | | |

Contrainte `UNIQUE (id, currency_code)` : cible des clés étrangères composites (§ 3) qui interdisent tout prix dans une autre devise que celle du magasin. **Aucune ligne insérée par la migration** ; magasins créés par `MMV.Maintenance` (pilotes fictifs BE-EUR / MA-MAD : seulement dans les tests, après G05-SQL — D-05-20).

### `org.locations`

`id` PK, `store_id` FK, `code` (unique par magasin), `name`, `is_default bool`, `archived_*`, `created_*`. Index unique partiel `(store_id) WHERE is_default AND archived_at_utc IS NULL` : un seul emplacement par défaut actif par magasin.

## 2. Schéma `catalog` (D-05-04, D-05-05, D-05-06, D-05-14, D-05-21)

### `catalog.families` — liste fermée de six

| `code` | Libellé |
|---|---|
| `optical_frames` | Montures optiques |
| `sunglasses` | Lunettes de soleil |
| `ophthalmic_lenses` | Verres correcteurs |
| `contact_lenses_solutions` | Lentilles et solutions |
| `accessories` | Accessoires |
| `services` | Prestations |

Colonnes : `code` PK (`^[a-z][a-z_]{0,31}$`), `label_fr`, `attribute_set` (jeu d'attributs défini dans le code), `allowed_policies text[]` (proposé : `services` → `{service}`, autres → `{stocked,order_only}`), `sort_order`. Lignes insérées par la migration ; aucune écriture par l'API.

### Référentiel verres (D-05-21) — configurable, aucune valeur pré-remplie

| Table | Colonnes | Règle |
|---|---|---|
| `catalog.lens_manufacturers` | `id`, `name`, `archived_*`, `created_*` | nom unique (insensible à la casse) |
| `catalog.lens_ranges` | `id`, `manufacturer_id` FK, `name`, `archived_*` | unique `(manufacturer_id, lower(name))` |
| `catalog.lens_indices` | `id`, `value numeric(4,3)`, `label` null, `archived_*` | `value` unique, `CHECK (value > 1 AND value < 2)` (borne de saisie, aucune liste imposée) |
| `catalog.lens_treatments` | `id`, `manufacturer_id` null FK, `name`, `archived_*` | index unique `(manufacturer_id, lower(name)) NULLS NOT DISTINCT` |

Écriture : `catalog.configure`. Aucun tarif fabricant ici (prix = variantes, § 3).

### `catalog.product_models`

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `family_code` | `text` FK | |
| `brand` | `text` null | ≤ 100 |
| `name` | `text` | obligatoire, ≤ 200 |
| `description` | `text` null | ≤ 2000 |
| `lens_range_id` | `uuid` null FK | obligatoire si famille `ophthalmic_lenses` (QR-07), interdit sinon |
| `attributes` | `jsonb` | objet ; clés communes du modèle validées par le serveur |
| `created_*`, `updated_*`, `archived_*`, `creation_request_id`, `last_request_id` | | |

Aucune quantité ni prix au niveau modèle.

### `catalog.product_variants`

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `model_id` | `uuid` FK | |
| `sku` | `text` | obligatoire, ≤ 64, index unique sur `lower(btrim(sku))` (toute l'installation) |
| `manufacturer_reference` | `text` null | ≤ 64 |
| `barcode` | `text` null | 8, 12, 13 ou 14 chiffres, clé vérifiée par le serveur ; index unique partiel `WHERE barcode IS NOT NULL` |
| `label` | `text` | libellé de la variante (ex. coloris / calibre), ≤ 200 |
| `attributes` | `jsonb` | objet ; clés validées selon la famille |
| `lens_index_id` | `uuid` null FK | verres seulement |
| `stock_policy` | `text` | `CHECK IN ('stocked','order_only','service')` ; contrôlé contre `families.allowed_policies` |
| `tax_category_code` | `text` null | rattachement fiscal, renseigné seulement après validation (§ 3.3) |
| `created_*`, `updated_*`, `archived_*`, `creation_request_id`, `last_request_id` | | |

`catalog.variant_lens_treatments (variant_id, treatment_id)` PK composite : traitements d'une variante de verre (QR-07).

Indicateur calculé (non stocké) `is_incomplete` : `order_only` actif sans fournisseur principal actif (QR-01).

### `catalog.suppliers`

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `code` | `text` | unique sur `lower(btrim(code))` |
| `name` | `text` | obligatoire |
| `country_code` | `char(2)` | ISO 3166-1 |
| `contact_name`, `email`, `phone` | `text` null | contact unique, minimal |
| `address_line`, `postal_code`, `city` | `text` null | |
| `legal_identifiers` | `jsonb` | objet ; clés admises selon le pays (Maroc : ICE, IF, RC ; Belgique : numéro d'entreprise) — **contrôle de format seulement**, aucune règle juridique déclarée |
| `notes` | `text` null | ≤ 2000 ; aucune donnée sensible |
| horodatages, auteurs, archivage, idempotence | | |

### `catalog.variant_suppliers`

| Colonne | Type | Règle |
|---|---|---|
| `variant_id`, `supplier_id` | `uuid` | PK composite |
| `supplier_reference` | `text` null | |
| `supplier_price_ht` | `numeric(14,4)` null | **protégé** (`costs.*`) |
| `supplier_currency` | `char(3)` null | obligatoire si prix ; aucune conversion |
| `lead_time_days` | `int` null | `0..365` |
| `is_primary` | `bool` | index unique partiel `(variant_id) WHERE is_primary` |
| `created_*`, `updated_*` | | retrait audité, pas d'archivage |

Un fournisseur archivé ne peut pas devenir principal. Un prix fournisseur ne modifie jamais de lui-même le prix d'achat HT du magasin (D-05-16).

## 3. Schéma `pricing` (D-05-15, D-05-16)

### `pricing.variant_store_prices`

| Colonne | Type | Règle |
|---|---|---|
| `variant_id` | `uuid` FK | PK `(variant_id, store_id)` |
| `store_id` | `uuid` | FK composite `(store_id, currency_code) → org.stores (id, currency_code)` |
| `currency_code` | `char(3)` | = devise du magasin (garanti par la FK composite) |
| `purchase_price_ht` | `numeric(14,4)` null | `>= 0` ; **protégé** |
| `coefficient` | `numeric(8,4)` null | `> 0 AND <= 100` ; **protégé** (révèle le prix d'achat) |
| `pricing_mode` | `text` | `CHECK IN ('coefficient','manual')` |
| `sale_price_ht` | `numeric(12,2)` null | `>= 0` ; stocké |
| `CHECK` mode | | `pricing_mode = 'manual' OR sale_price_ht IS NULL OR (purchase_price_ht IS NOT NULL AND coefficient IS NOT NULL)` |
| `updated_at_utc`, `updated_by`, `last_request_id` | | |

L'égalité `sale_price_ht = round(purchase_price_ht * coefficient, 2)` n'est **pas** une contrainte : un écart en mode `coefficient` est l'état `recalculation_pending` (changement de prix d'achat sans recalcul, D-05-16), signalé par le serveur. Requête de contrôle fournie pour la revue SQL. Prix requis pour vendre (Phase 06), pas pour enregistrer la variante.

### `pricing.price_history` — ajout seul

`id`, `variant_id`, `store_id`, `currency_code`, `field` (`purchase_price_ht` / `coefficient` / `pricing_mode` / `sale_price_ht` / `supplier_price_ht`), `supplier_id` null, `old_value numeric(14,4) null`, `new_value numeric(14,4) null`, `old_mode` / `new_mode` null, `origin` (`edit` / `recalculation` / `manual_price` / `import`), `changed_at_utc`, `changed_by`, `request_id`. Déclencheurs refusant `UPDATE` / `DELETE` (procédé de `audit.events`). Lignes `purchase_price_ht`, `coefficient`, `supplier_price_ht` lisibles avec `costs.read` seulement.

### `pricing.tax_categories` — structure seulement, **non ratifiée**

| Colonne | Type | Règle |
|---|---|---|
| `store_id`, `code` | | PK composite |
| `label` | `text` | |
| `rate` | `numeric(7,4)` null | **vide** tant que non validé ; `CHECK (rate IS NULL OR (rate >= 0 AND rate < 1))` |
| `legal_reference` | `text` null | |
| `validated_at_utc`, `validated_by` | null | `CHECK ((rate IS NULL) = (validated_at_utc IS NULL))` |

**Aucune ligne insérée** par la migration ; aucun taux, code ou libellé fiscal proposé pour la Belgique ou le Maroc (ADR-010 `OPEN DECISION`). Écriture par `MMV.Maintenance` seulement, sur décision du propriétaire appuyée par un avis fiscal. TTC calculé à la lecture, jamais stocké au catalogue.

## 4. Schéma `stock` (05-B, `0008`)

### `stock.variant_store_settings`

`variant_id`, `store_id` PK composite ; `low_stock_threshold int null` (`>= 0`, variantes `stocked` seulement — D-05-19) ; `updated_*`.

### `stock.balances`

| Colonne | Type | Règle |
|---|---|---|
| `variant_id`, `location_id` | `uuid` | PK composite ; variante `stocked` |
| `on_hand` | `int` | `CHECK (on_hand >= 0)` (D-05-03) |
| `reserved` | `int` | défaut 0 ; `CHECK (reserved >= 0 AND reserved <= on_hand)` ; **toujours 0 en Phase 05** (écrit par la Phase 06, D-05-17, QR-10) |
| `last_movement_seq` | `bigint` null | |
| `updated_at_utc` | | |

Invariant vérifié par test : `on_hand = Σ quantity_delta` des mouvements de (variante, emplacement).

### `stock.movements` — journal en ajout seul

| Colonne | Type | Règle |
|---|---|---|
| `id` | `uuid` PK | |
| `seq` | `bigint` identité | ordre total |
| `variant_id`, `location_id` | `uuid` FK | |
| `movement_type` | `text` | 05-B : `receipt`, `manual_in`, `manual_out`, `adjustment`, `inventory_adjustment` ; réservés (contrainte étendue plus tard) : `sale`, `sale_reversal`, `customer_return`, `supplier_return`, `transfer_out`, `transfer_in` |
| `quantity_delta` | `int` | `<> 0` ; signe cohérent avec le type |
| `quantity_before`, `quantity_after` | `int` | `after = before + delta`, `after >= 0` |
| `reason_code` | `text` | liste fermée (QR-03) |
| `comment` | `text` null | ≤ 500 ; obligatoire si `reason_code = 'other'` |
| `supplier_id` | `uuid` null FK | `receipt` |
| `unit_cost_ht` | `numeric(14,4)` null | **protégé** ; `receipt` seulement ; devise = magasin de l'emplacement |
| `source_type`, `source_id` | `text`, `uuid` null | `inventory_session` (05-C) ; `sale`, `supplier_order` plus tard |
| `reverses_movement_id` | `uuid` null FK unique | contre-mouvement (Phase 06) |
| `performed_by`, `performed_at_utc` | | |
| `request_id` | `uuid` | unique `(request_id, variant_id, location_id)` |

Index : `(variant_id, location_id, seq DESC)`, `(performed_at_utc)`, `(source_type, source_id)`. Déclencheurs refusant `UPDATE` / `DELETE`.

## 5. Inventaires (05-C, `0009`)

### `stock.inventory_sessions`

`id`, `number` unique (`INV-AAAA-NNNN`), `store_id` FK, `location_ids uuid[]` (emplacements du magasin), `scope_type` (`full` / `family` / `selection`), `scope_family_code` null, `blind bool` (QR-04), `status` (`open` / `validated` / `abandoned`), `opened_*`, `validated_*`, `abandoned_*`, `abandon_reason`, idempotence ; cohérence statut ↔ horodatages par `CHECK`.

### `stock.inventory_lines`

`session_id`, `variant_id`, `location_id` PK composite ; `expected_at_count int null` ; `counted_qty int null` (`>= 0`) ; `counted_by`, `counted_at_utc` ; `reason_code`, `comment` (obligatoires à la validation si écart ≠ 0 — Q10) ; `resolution` (`applied` / `no_difference` / `excluded` / `set_to_zero`) ; `movement_id` null FK.

Un seul inventaire ouvert par (variante, emplacement) : contrôle sous `pg_advisory_xact_lock` dédié à l'ouverture. Session close : lignes figées (déclencheur).

## 6. Import (05-D, `0010`) — D-05-18

`imports.batches` (`id`, `kind`, `store_id` null, `file_sha256`, `file_name`, `row_count`, `status` `uploaded` / `checked` / `applied` / `rejected`, `checked_*`, `applied_*`, `request_id`) ; `imports.rows` (`batch_id`, `line_no`, `raw jsonb` (valeurs en chaînes), `outcome`, `errors jsonb`). Les lignes contenant des coûts ne sont lisibles qu'avec `costs.read`. Purge des fichiers après application : à décider avec G05-LEGAL.

## 7. Réservations (Phase 06 — contrat seulement)

`stock.reservations` (Phase 06) : `id`, `variant_id`, `location_id`, `quantity > 0`, `source_type` / `source_id` (ligne de vente ou de commande **validée**, jamais un devis — D-05-17), `status` (`active` / `consumed` / `released`), auteurs, dates ; `stock.balances.reserved` maintenu dans la même transaction.

## 8. Permissions et audit

- Contrainte `auth.user_permissions` étendue (`DROP CONSTRAINT` + `ADD CONSTRAINT`, procédé O-03-24) aux permissions de D-05-11 (liste exacte QR-05) : `0007` → `catalog.read|write|archive|configure`, `pricing.write|manual`, `costs.read|write`, `suppliers.read|write` ; `0008` → `stock.read|movements.write|adjust` ; `0009` → `inventory.count|validate` ; `0010` → `catalog.import`.
- Journal `audit.events` existant, aucune migration de l'audit ; codes conformes à `ck_events_action_format` (`^[a-z][a-z_]{0,30}\.[a-z][a-z_]{0,31}$`) : `model.created|updated|archived|reactivated`, `variant.created|updated|policy_changed|archived|reactivated`, `price.changed`, `price.recalculated`, `price.manual_set`, `lens_reference.changed`, `supplier.created|updated|archived|reactivated`, `supplier_link.added|updated|removed`, `stock_movement.recorded`, `stock_adjustment.recorded`, `inventory.opened|validated|abandoned`, `import.checked|applied`. La liste `changes` ne contient **jamais** de valeur de coût, de coefficient ni de prix d'achat : seulement le nom du champ ; les valeurs vivent dans `pricing.price_history`, sous permission.

## 9. Points à valider à la revue SQL de chaque sous-lot

Empreinte imposée du script, `lock_timeout`, sauvegarde vérifiée de `mmv_v2_dev` avant application, contrôle de schéma (`verify.sql`), inventaire exact des objets, absence d'extension, aucun type flottant pour un montant, aucune ligne fiscale ni magasin insérés par la migration, rôle SQL applicatif distinct toujours en réserve (BL-04-04).
