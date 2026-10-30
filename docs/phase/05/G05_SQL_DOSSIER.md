# Phase 05 — Dossier G05-SQL : migration `0007_CatalogStockModule`

**Statut : `NOT_APPLIED` — revue du propriétaire requise.** Rien n'a été exécuté sur `mmv_v2_dev` ni sur `mmv_v2_test` ; aucune connexion à ces bases n'a été ouverte pendant la Phase 05. Mandat : [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md) § 9 (« STOP OBLIGATOIRE À G05-SQL »). Empreintes : [`sql/0007_MANIFEST.sha256`](sql/0007_MANIFEST.sha256).

## 1. Script à approuver

| Fichier | Rôle |
|---|---|
| [`sql/0007_CatalogStockModule.idempotent.sql`](sql/0007_CatalogStockModule.idempotent.sql) | **script à appliquer** (EF Core `--idempotent`, de `20261027134615_OpticalModule` à `20261030005557_CatalogStockModule`) ; rejouable sans effet |
| [`sql/0007_CatalogStockModule.sql`](sql/0007_CatalogStockModule.sql) | même contenu, forme non idempotente (lecture) |
| [`sql/0007_CatalogStockModule.verify.sql`](sql/0007_CatalogStockModule.verify.sql) | contrôle après application, lecture seule, 27 lignes `OK` attendues |
| [`sql/0007_CatalogStockModule.schema-diff.txt`](sql/0007_CatalogStockModule.schema-diff.txt) | diff `pg_dump --schema-only` 0006 → 0007 (bases jetables) : **une seule ligne retirée** (contrainte des permissions, recréée élargie), tout le reste ajouté |
| [`sql/0007_CatalogStockModule.schema-inventory.txt`](sql/0007_CatalogStockModule.schema-inventory.txt) | inventaire exact des cinq schémas (colonnes, contraintes, index, séquences, fonctions, déclencheurs, familles), comparé par `Phase05SchemaTests` |

Une seule migration pour les sous-lots 05-A à 05-D (mandat § 9 : pas de découpage artificiel ; [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md) QR-06).

## 2. Contenu exact (une transaction `START TRANSACTION … COMMIT`)

- `auth.user_permissions` : `ck_user_permissions_permission` supprimée puis recréée sous le **même nom** avec les 12 droits existants + 16 droits de la Phase 05 (procédé O-03-24). Aucune ligne de droit écrite.
- Schémas créés : `org`, `catalog`, `pricing`, `stock`, `imports`.
- 21 tables : `org.stores`, `org.locations` ; `catalog.families`, `lens_references`, `product_models`, `product_variants`, `variant_lens_treatments`, `suppliers`, `variant_suppliers` ; `pricing.variant_store_prices`, `price_history`, `tax_categories` ; `stock.variant_store_settings`, `balances`, `movements`, `receipts`, `receipt_lines`, `inventory_sessions`, `inventory_lines` ; `imports.batches`, `rows`.
- 102 index, 77 contraintes `CHECK`, 66 clés étrangères (65 `RESTRICT`, 1 `CASCADE` : `variant_lens_treatments → product_variants`, association supprimée avec la variante — or une variante ne se supprime jamais, déclencheur), 3 séquences (`receipt_number_seq`, `inventory_number_seq`, identité `movements.seq`).
- 8 fonctions PL/pgSQL et 40 déclencheurs : aucune suppression (12 tables), journaux en ajout seul (`price_history`, `movements`, `receipts`, `receipt_lines`), devise de magasin et rattachement d'emplacement immuables, session d'inventaire close figée, ligne résolue figée, lot d'import clos figé, aucun `TRUNCATE`. Code d'erreur `42501`.
- **Données insérées : les six familles seulement** (`catalog.families`). Aucun magasin, emplacement, taux, catégorie fiscale, produit, fournisseur, compte ni droit.
- Garanties de base : `on_hand >= 0` ; `0 <= reserved <= on_hand` ; mouvement `after = before + delta`, signe cohérent avec le type ; FK composite (magasin, devise) pour tarifs et réceptions ; devises `EUR` / `MAD` ; montants `numeric(14,4)`, `numeric(12,2)`, coefficient `numeric(8,4)` `> 0 AND <= 100` ; aucun type flottant.
- `Down()` : `NotSupportedException` (retour arrière = restauration de la sauvegarde).

## 3. Correspondance avec le code

`src/MMV.Infrastructure/Catalog/CatalogStockModel.cs` (modèle EF), `src/MMV.Infrastructure/Persistence/Migrations/20261030005557_CatalogStockModule*.cs` (migration ; déclencheurs écrits à la main), `MmvDbContextModelSnapshot.cs`, `src/MMV.Domain/Identity/Permissions.cs`. `dotnet ef migrations has-pending-model-changes` : « No changes have been made to the model since the last migration » (30/10/2026).

## 4. Pré-essais hors bases du propriétaire (PAS une preuve d'application)

Cluster PostgreSQL 18.6 **jetable** (`initdb` dans le répertoire temporaire de la session, port 55433, base et rôle `mmv_v2_scratch`, jamais 5432 / 5433) :

1. 0001 → 0006 (scripts idempotents versionnés) puis 0007 idempotent appliqué **deux fois** : aucune erreur (idempotence).
2. `0007_CatalogStockModule.verify.sql` (port adapté à 55433) : 27 / 27 `OK`.
3. `MMV.PostgreSql.Tests --filter Stage=Phase05` : 8 / 8 PASS (pipeline HTTPS de MMV.Api sur PostgreSQL réel, deux magasins fictifs EUR / MAD, résidu 0 vérifié après chaque test) ; `Phase05SchemaTests` : inventaire identique à l'instantané, objets 0001–0006 inchangés.

## 5. Politique de mise à niveau et compatibilité

Aucune table existante modifiée hormis la contrainte des permissions (élargie). Le code de la Phase 05 suppose 0007 appliquée ; tant qu'elle ne l'est pas, les routes `/api/stores`, `/api/catalog`, `/api/pricing`, `/api/suppliers`, `/api/stock`, `/api/inventories`, `/api/imports` échouent (500) mais **l'interface ne les appelle pas** sans `VITE_MMV_CATALOG_MODULE=enabled`, et les modules des Phases 02 à 04 sont inchangés. Les tests `SchemaStateTests` « PostMigration » exigent désormais 0007 : ils restent `NOT_TESTED` / en échec par construction tant que 0007 n'est pas appliquée.

## 6. Commandes après GO G05-SQL explicite seulement

Préalable : arrêter de son propre chef les postes / l'API qui écrivent sur la base visée (aucun processus du propriétaire n'est arrêté par l'agent).

```powershell
# 1. Sauvegarde de mmv_v2_dev (format personnalisé), empreinte notée
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
& 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -Fc `
    -f "$env:USERPROFILE\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0007_$stamp.dump"
Get-FileHash "$env:USERPROFILE\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0007_$stamp.dump" -Algorithm SHA256
& 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe' --list "$env:USERPROFILE\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0007_$stamp.dump" | Select-Object -First 5

# 2. Application sur mmv_v2_dev (empreinte imposée = 1re ligne du manifeste, lock_timeout borné)
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev `
    -Script docs/phase/05/sql/0007_CatalogStockModule.idempotent.sql -Sha256 <empreinte du manifeste> -LockTimeout 5s

# 3. Contrôle (27 lignes OK attendues)
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -X -A -t -v ON_ERROR_STOP=1 `
    -f docs/phase/05/sql/0007_CatalogStockModule.verify.sql

# 4. Mêmes étapes 2–3 sur mmv_v2_test (secret mmv-v2-postgresql-tests, clé ConnectionStrings:MmvTest)

# 5. Tests réels (mmv_v2_test) : Phase 05 + non-régression de toutes les suites PostgreSQL
dotnet test tests/MMV.PostgreSql.Tests --artifacts-path bin/g05 -p:NuGetAudit=false
```

Toute ligne `ECART`, tout échec de script ou de test = arrêt et rapport, sans correction improvisée ; toute modification d'un script approuvé exige une nouvelle revue de son empreinte.

## 7. Points à examiner par l'auditeur

1. La liste des 16 droits (QR-05) et l'élargissement de `ck_user_permissions_permission`.
2. Les six familles insérées et leurs politiques admises (`services` → `{service}`).
3. Le rôle applicatif est propriétaire des tables : les déclencheurs « aucune suppression / ajout seul » ne protègent pas contre lui (R-05-17, BL-04-04, inchangé).
4. Le seul `ON DELETE CASCADE` (`variant_lens_treatments`).
5. L'absence de tout taux fiscal (G05-FISCAL / ADR-010 ouvert) et de toute donnée pilote.
6. `imports.batches.content` conserve le texte des fichiers importés (purge à décider avec G05-LEGAL).
