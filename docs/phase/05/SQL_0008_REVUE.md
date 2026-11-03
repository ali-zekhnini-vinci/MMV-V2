# Phase 05 — Revue SQL consolidée des corrections : migration 0008_CatalogCorrections

> **Note datée 03/11/2026 — APPLIQUÉE.** GO conditionnel du propriétaire ([`prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md`](../../../prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md)). Script `9a75acea…287e715c` appliqué tel quel (aucune modification de 0007 ni de 0008, manifeste 8 / 8) via `Invoke-MmvSqlScript.ps1 -LockTimeout 5s` : `mmv_v2_dev` 15:35:18 puis `mmv_v2_test` 15:35:59, COMMIT, `verify.sql` 14 / 14 sur chaque base. Sauvegardes : `Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0008_20261103_153503.dump` (SHA-256 `6848B40F…6778230`) et `mmv_v2_test_avant_0008_20261103_153559.dump` (`FD9F2CEE…3C8BB821`), restauration complète non éprouvée. Preuves : [`evidence/sql0008/`](evidence/sql0008/). Le texte ci-dessous est conservé tel que relu.

Statut : **`PHASE05_CORRECTIONS — OWNER_SQL_REVIEW_REQUIRED`** (02/11/2026). Migration **préparée, NON appliquée** à `mmv_v2_dev` ni à `mmv_v2_test`. Seul un cluster PostgreSQL 18 **jetable** l'a reçue (voir § 5). STOP obligatoire avant toute application (mandat du 02/11/2026, § 11).

## 1. Pourquoi une migration, et pourquoi une seule

Deux corrections exigent de la persistance nouvelle ; tout le reste (COR-05-01, 02, 03, 06, 07, 08) a été réalisé **sans schéma**.

| Correction | Besoin | Choix |
| --- | --- | --- |
| COR-05-04 import CSV | modèle d'import par fournisseur, fichier rattaché à son fournisseur | table `imports.supplier_profiles`, colonne nullable `imports.batches.supplier_id` |
| COR-05-05 verres | offres fournisseur par gamme (capacités, restrictions, règles avancées), historique | tables `catalog.lens_offers`, `catalog.lens_offer_treatments`, `catalog.lens_offer_revisions` |

Un import catalogue comportant des prix n'a **pas** nécessité d'élargir `ck_batches_store` : les prix passent par un second lot « Prix du magasin » rapproché par la référence fournisseur (décision documentée dans `CORRECTIONS_05.md`). Migration **strictement additive** : aucune table, colonne, contrainte, fonction ou déclencheur existant n'est modifié ni supprimé ; **0007 inchangée** (scripts et migration identiques à l'octet près, manifeste `0007_MANIFEST.sha256` : seules divergent les deux entrées partagées qui évoluent nécessairement avec toute migration — `MmvDbContextModelSnapshot.cs` et `CatalogStockModel.cs` — comme le manifeste 0006 après 0007).

## 2. Fichiers (manifeste [`sql/0008_MANIFEST.sha256`](sql/0008_MANIFEST.sha256))

| Fichier | Rôle | SHA-256 |
| --- | --- | --- |
| [`sql/0008_CatalogCorrections.idempotent.sql`](sql/0008_CatalogCorrections.idempotent.sql) | **script à appliquer** (garde `__EFMigrationsHistory`) | `9a75acea…287e715c` |
| [`sql/0008_CatalogCorrections.sql`](sql/0008_CatalogCorrections.sql) | même contenu, non idempotent (lecture) | `b21057be…a7b96ba9` |
| [`sql/0008_CatalogCorrections.verify.sql`](sql/0008_CatalogCorrections.verify.sql) | contrôle après application, lecture seule, 14 lignes `OK` attendues | `492eb834…ecca4401d34` |
| [`sql/0008_CatalogCorrections.schema-diff.txt`](sql/0008_CatalogCorrections.schema-diff.txt) | diff exact de l'inventaire 0007 → 0008 : **0 ligne retirée, 129 ajoutées** | `990872f1…c385163e` |
| [`sql/0008_CatalogCorrections.schema-inventory.txt`](sql/0008_CatalogCorrections.schema-inventory.txt) | inventaire complet 0001–0008 (comparé par `Phase05SchemaTests`) | `91241c93…44f132` |
| `src/MMV.Infrastructure/Persistence/Migrations/20261102183203_CatalogCorrections.cs` (+ `.Designer.cs`) | migration EF Core (déclencheurs écrits à la main) | voir manifeste |

Empreintes complètes : manifeste. Vérification : `sha256sum -c docs/phase/05/sql/0008_MANIFEST.sha256` (8 × `OK` au 02/11/2026).

## 3. Objets créés

- **`imports.supplier_profiles`** : `id` (PK), `supplier_id` (FK `catalog.suppliers`, RESTRICT), `kind` (`catalog` | `prices`), `mapping` jsonb objet, `headers` jsonb tableau, auteurs/dates de création et de mise à jour (FK `auth.users`), `last_request_id`, concurrence `xmin`. Index unique (`supplier_id`, `kind`) ; index des FK. Déclencheurs : aucune suppression, aucun `TRUNCATE`.
- **`imports.batches.supplier_id`** : uuid **nullable** (FK `catalog.suppliers`, RESTRICT, index). Lignes existantes : `NULL`, aucun effet.
- **`catalog.lens_offers`** (archivable, `xmin`) : `range_id` (FK `lens_references`), `supplier_id` (FK `suppliers`), `lens_type` (5 valeurs), `material`, `index_id` (FK `lens_references`, nullable), `production` (`stocked` | `on_order`), `lead_time_days` (0–365), `price_ht numeric(12,4)` + `price_currency char(3)` (ensemble ou rien), `diameters` jsonb tableau, plages `sphere_*`/`cylinder_* numeric(5,2)` et `addition_* numeric(4,2)` (min ≤ max, ensemble ou rien), `rules` jsonb tableau (règles STRUCTURÉES validées par le serveur — jamais d'expression), `rules_complete`, `source_reference`, `valid_from`/`valid_until` (ordre contrôlé), `notes`, `revision` ≥ 1, `creation_request_id` (unique). **8 contraintes CHECK**. Déclencheurs : aucune suppression, aucun `TRUNCATE`.
- **`catalog.lens_offer_treatments`** : (`offer_id`, `treatment_id`) PK, FK offre (CASCADE, la liaison suit l'offre — l'offre elle-même n'est jamais supprimée) et FK traitement (RESTRICT). Remplaçable comme `variant_lens_treatments`.
- **`catalog.lens_offer_revisions`** : instantané jsonb objet par révision (`offer_id`, `revision` unique), auteur, date, motif — **ajout seul** (`UPDATE`/`DELETE`/`TRUNCATE` refusés par `catalog.reject_change()`).
- Fonctions de garde : **aucune nouvelle** (réutilisation de `catalog.reject_delete()` et `catalog.reject_change()` de 0007). Droits : propriétaire = rôle applicatif, droits par défaut (comme 0007). Permissions : **aucune nouvelle** (écriture `catalog.configure`, import `catalog.import`, coûts `costs.read` / `costs.write`).

## 4. Compatibilité avec les données existantes

Additive : aucune réécriture de ligne, aucune valeur par défaut calculée, aucun verrou long hors `ALTER TABLE imports.batches ADD COLUMN supplier_id uuid` (colonne nullable sans défaut : métadonnée seule) et la création de la FK (`fk_batches_suppliers`, validation d'une colonne entièrement `NULL`). Le code des corrections **suppose 0008 appliquée** : tant qu'elle ne l'est pas, les routes `/api/imports` (colonne `supplier_id`) et `/api/catalog/lens-offers` échouent ; les autres routes, dont tout le lot A, fonctionnent sur une base 0007. Les tests PostgreSQL de 0008 (`Phase05CorrectionsSchemaTests`) sont `NOT_TESTED` (ignorés) sur une cible sans 0008 ; le banc ne purge les nouvelles tables que si elles existent.

## 5. Essais hors bases du propriétaire (pas une preuve d'application)

Cluster PostgreSQL 18 jetable (`initdb` dans le répertoire temporaire de la session, port 55433, base et rôle `mmv_v2_scratch`, jamais 5432 / 5433), reconstruit de zéro le 02/11/2026 :

1. 0001 → 0007 (scripts idempotents versionnés) puis **0008 idempotent appliqué deux fois** : aucune erreur.
2. `0008_CatalogCorrections.verify.sql` : **14 / 14 `OK`**, 0 `ECART`.
3. `MMV.PostgreSql.Tests --filter Stage=Phase05` : **20 / 20 PASS** (+1 `NOT_TESTED` : processus HTTPS réel), dont les deux tests 0008 et `Phase05SchemaTests` (inventaire identique à l'instantané 0008) ; résidu 0 après chaque test.
4. Déclencheurs prouvés : `UPDATE` d'une révision et `DELETE` d'une offre refusés (test).

## 6. Plan de sauvegarde et d'application — APRÈS GO explicite seulement

Préalable : le propriétaire arrête de son propre chef les postes et l'API qui écrivent sur la base visée (aucun processus du propriétaire n'est arrêté par l'agent).

```powershell
# 1. Sauvegarde de mmv_v2_dev (format personnalisé), empreinte notée, liste relue
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
& 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -Fc `
    -f "$env:USERPROFILE\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0008_$stamp.dump"
Get-FileHash "$env:USERPROFILE\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0008_$stamp.dump" -Algorithm SHA256
& 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe' --list "$env:USERPROFILE\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0008_$stamp.dump" | Select-Object -First 5

# 2. Application sur mmv_v2_dev (empreinte imposée = 1re ligne du manifeste, lock_timeout borné)
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev `
    -Script docs/phase/05/sql/0008_CatalogCorrections.idempotent.sql `
    -Sha256 9a75acea0173a5019b520362cf04a5ca10e1de814753603f21d46c1c287e715c -LockTimeout 5s

# 3. Contrôle (14 lignes OK attendues, toute ligne ECART = arrêt)
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -X -A -t -v ON_ERROR_STOP=1 `
    -f docs/phase/05/sql/0008_CatalogCorrections.verify.sql

# 4. Même séquence pour mmv_v2_test (sauvegarde, application, contrôle), puis suite PostgreSQL complète
#    dotnet test tests/MMV.PostgreSql.Tests -p:NuGetAudit=false --filter Stage=Phase05
```

Retour arrière : la migration EF `Down` supprime les déclencheurs, les tables et la colonne ajoutées (perte des offres et modèles saisis entre-temps) ; à défaut, restauration de la sauvegarde. Aucun retour arrière n'est exécuté sans décision du propriétaire.

## 7. Décision attendue

GO / NO-GO du propriétaire sur ce dossier (script idempotent `9a75acea…287e715c`). Après GO : sauvegardes, application dev puis test, contrôle 14/14, suite PostgreSQL complète sur `mmv_v2_test`, preuves natives restantes, puis livraison finale `PHASE05_CORRECTIONS_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.
