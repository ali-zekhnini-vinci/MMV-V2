# Phase 05 — Dossier G05-SQL-0009 : migration `0009_CsvImportAssistant` (lot CSV-IMPORT-AI-01)

Mandat : [`prompts/MMV_V2_GO_CONDITIONNEL_GATE_B_CSV_IMPORT_AI_01_2026-11-06.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_GATE_B_CSV_IMPORT_AI_01_2026-11-06.md) § 5. Conception : [`CSV_IA_05_GATE_B.md`](CSV_IA_05_GATE_B.md) § 12. Livraison : [`LIVRAISON_05_CSVAI.md`](LIVRAISON_05_CSVAI.md).

**Statut (06/11/2026) : `G05-SQL-0009 — PRÉPARÉE, NON APPLIQUÉE — GO SQL DISTINCT DU PROPRIÉTAIRE REQUIS`.** Aucune exécution sur `mmv_v2_dev` ni `mmv_v2_test` ; aucune migration automatique ; aucune écriture directe. Essais sur un cluster PostgreSQL 18 **jetable** seulement (§ 5).

> **Note datée 06/11/2026 (mandat post-audit [`prompts/MMV_V2_MANDAT_POST_AUDIT_CSV_IMPORT_AI_01_2026-11-06.md`](../../../prompts/MMV_V2_MANDAT_POST_AUDIT_CSV_IMPORT_AI_01_2026-11-06.md), COR-CSVAI-05).** Revue externe : SQL favorable sous conditions. Les corrections applicatives COR-CSVAI-01 à 04 n'exigent **aucun changement de schéma** : script idempotent **inchangé** (`30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0`, `sha256sum -c sql/0009_MANIFEST.sha256` : 7/7 OK). La commande de retour arrière `pg_restore --clean` sur la base ACTIVE, proposée au § 6 initial, est **retirée** : remplacée par une restauration sur base **neuve distincte**, contrôlée, puis une **bascule explicite** (§ 6.2), éprouvée le 06/11 avec des données fictives de l'assistant (§ 5, essai 7). Nouveau script en lecture seule : [`sql/0009_restore_check.sql`](sql/0009_restore_check.sql). **Constat sur le dossier** : l'inventaire de revue `sql/0009_CsvImportAssistant.schema-inventory.txt` porte l'empreinte des 4 corps de fonctions de garde **en CRLF**. Il a été relevé sur une base bâtie avant la mise en LF des scripts. Appliqué tel que Git le stocke (LF), le script produit d'autres empreintes, et `Phase05Schemas_MatchTheReviewedInventory` aurait échoué après une application conforme. Le script et l'inventaire de revue ne sont **pas modifiés**. Comme pour 0007 (O-05-13), un inventaire **appliqué** est ajouté : [`sql/0009_CsvImportAssistant.schema-inventory.applied.txt`](sql/0009_CsvImportAssistant.schema-inventory.applied.txt) (`1cdb101c…ec4e49`). Le test hors connexion `AssistantReviewedInventory_DiffersOnlyByTheGuardFunctionBodiesLineEndings` prouve que les deux inventaires ne diffèrent que par ces 4 lignes, chacune égale au md5 LF (resp. CRLF) du corps `$fn$…$fn$` du script approuvé. Les deux ajouts ont leurs empreintes dans [`sql/0009_POST_AUDIT_MANIFEST.sha256`](sql/0009_POST_AUDIT_MANIFEST.sha256) : **dossier révisé, à revoir avec le script inchangé**. Contrôles en lecture seule des bases persistantes : § 6.0. Statut inchangé : `PRÉPARÉE, NON APPLIQUÉE` ; aucune autorisation d'application n'est déduite du mandat.

> **Note datée 07/11/2026 (mandat [`prompts/MMV_V2_GO_CONDITIONNEL_G05_SQL_0009_GATE_D_2026-11-07.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_G05_SQL_0009_GATE_D_2026-11-07.md)) : `G05-SQL-0009 — APPLIQUÉE` sur `mmv_v2_dev` puis `mmv_v2_test`.** Précontrôles § 6.0 refaits (18.6, 5433, cluster `7696807267651395228`, 0009 absente, aucune dérive, écrivains arrêtés par le propriétaire, 0 session) ; sauvegardes définitives `*_avant_0009_20261106_231005.dump` (dev `1d247078…40cf7f`, test `bc785974…00cbee`), `pg_restore --list` et restauration sur base neuve du cluster jetable : IDENTIQUE ; application par `scripts/Invoke-MmvSqlScript.ps1` (empreinte `30dc6e0a…a2e7e0`, `lock_timeout` 5 s), COMMIT, verify **14/14** sur chaque base ; 9 migrations ; données et structures antérieures conservées. Script et manifestes **inchangés**. Aucun retour arrière (§ 6.2 disponible sur décision distincte). Détail : [`LIVRAISON_05_CSVAI_GATE_D.md`](LIVRAISON_05_CSVAI_GATE_D.md) § 1 à 3 ; preuves [`evidence/csvai/gate_d/`](evidence/csvai/gate_d/). Le statut du paragraphe d'en-tête (`PRÉPARÉE, NON APPLIQUÉE`) est historique.

## 1. Pourquoi une migration, et pourquoi une seule

Le nouveau parcours doit conserver l'original exact (octets, empreinte), les tentatives d'analyse et leur activité réelle, les décisions de l'utilisateur en révisions append-only et l'état de l'assistant (révision courante, révision contrôlée, choix « sans IA »). Aucune table existante ne le permet sans modification ; une migration **additive unique** les crée toutes. Elle ne modifie **aucune** table, colonne, contrainte ni index existant : `imports.batches` (dont `file_sha256`, `ck_batches_kind` et `ux_batches_applied_file`) est inchangé, l'idempotence et le refus du réimport restent ceux d'aujourd'hui. Le texte décodé des lots reste dans `imports.batches.content` (aucune donnée historique modifiée, décision du 06/11/2026).

## 2. Fichiers (manifeste [`sql/0009_MANIFEST.sha256`](sql/0009_MANIFEST.sha256))

| Fichier | Rôle |
| --- | --- |
| [`src/MMV.Infrastructure/Persistence/Migrations/20261106154105_CsvImportAssistant.cs`](../../../src/MMV.Infrastructure/Persistence/Migrations/20261106154105_CsvImportAssistant.cs) (+ `.Designer.cs`, `MmvDbContextModelSnapshot.cs`) | migration EF Core (5 tables, 13 index, 4 fonctions et 11 déclencheurs de garde écrits à la main) |
| [`sql/0009_CsvImportAssistant.sql`](sql/0009_CsvImportAssistant.sql) | script SQL (0008 → 0009), généré par `dotnet ef migrations script` |
| [`sql/0009_CsvImportAssistant.idempotent.sql`](sql/0009_CsvImportAssistant.idempotent.sql) | script **idempotent** à appliquer (après GO), généré par `dotnet ef migrations script --idempotent` |
| [`sql/0009_CsvImportAssistant.verify.sql`](sql/0009_CsvImportAssistant.verify.sql) | contrôle en **lecture seule** après application (14 lignes `OK` attendues) |
| [`sql/0009_CsvImportAssistant.schema-inventory.txt`](sql/0009_CsvImportAssistant.schema-inventory.txt) | inventaire exact 0001–0009 (comparé par `Phase05SchemaTests`) ; contient **toutes** les lignes de l'inventaire 0008, inchangées (+159 lignes) |
| [`sql/0009_CsvImportAssistant.schema-inventory.applied.txt`](sql/0009_CsvImportAssistant.schema-inventory.applied.txt) (ajout 06/11 post-audit, hors manifeste 0009) | inventaire 0001–0009 tel que produit par le script **approuvé** (LF) ; comparé par `Phase05SchemaTests` après application ; ne diffère de l'inventaire de revue que par les empreintes des 4 fonctions de garde (CRLF / LF) |
| [`sql/0009_restore_check.sql`](sql/0009_restore_check.sql) (ajout 06/11 post-audit, hors manifeste 0009) | empreinte de cohérence **en lecture seule** d'une base (migrations, lignes et empreinte md5 de chaque table, séquences, déclencheurs et leur état, contraintes, index, fonctions, propriétaires et droits) : comparaison source / restauration et contrôle de dérive |

## 3. Objets créés (schéma `imports`)

| Table | Contenu | Gardes |
| --- | --- | --- |
| `batch_originals` | octets d'origine (`bytea`, ≤ 2 Mio), SHA-256 des octets, taille, encodage, séparateur, date ; purge **tracée** (date, auteur) | `trg_batch_originals_guard` : seule la purge des octets est permise (aucune purge automatique, Q-CSVAI-07) ; aucune suppression |
| `batch_assistant` | parcours, destination (`products` / `lenses`), révision courante, révision contrôlée, choix « sans IA » (raison, date, auteur), magasin du tarif, synthèse du dernier contrôle (`jsonb`), `xmin` | `trg_batch_assistant_guard` : champs d'identité immuables, révision croissante, lot clos figé ; aucune suppression |
| `batch_analyses` | une ligne par tentative : contrat, modèle configuré, modèle **confirmé par la réponse**, empreinte du modèle, version d'Ollama, empreintes de la consigne et de l'échantillon, statut, dates, durée, jetons, couverture, sortie brute (≤ 64 Kio, sans trace de raisonnement), proposition, erreur, résultat tardif, `xmin` | `trg_batch_analyses_guard` : `queued → running → terminal` seulement ; terminal figé, sauf l'enregistrement UNIQUE d'un résultat tardif sur une tentative annulée ou interrompue ; `ux_batch_analyses_active` : une seule tentative active par lot |
| `batch_analysis_events` | activité réelle (identifiant croissant, tentative ou lot, code borné, données ≤ 4 Kio) | ajout seul |
| `batch_decisions` | révisions des décisions (numéro unique par lot ≥ 2, type borné, cible, valeur, empreinte des exemples, révision remplacée, auteur, date, requête) | ajout seul ; `trg_batch_decisions_open_batch` : aucune décision sur un lot clos ; `ux_batch_decisions_batch_revision` : deux décisions concurrentes sur la même révision → une seule passe |

Fonctions nouvelles : `imports.guard_batch_original()`, `imports.guard_batch_assistant()`, `imports.guard_batch_analysis()`, `imports.reject_closed_batch_decision()` ; fonctions existantes réutilisées : `catalog.reject_delete()`, `catalog.reject_change()`. Code d'erreur des gardes : `42501`.

## 4. Compatibilité avec 0007, 0008 et les données existantes

- 0007 et 0008 restent appliquées et inchangées (contrôlé par `verify` et par l'inventaire, qui contient toutes les lignes de l'inventaire 0008).
- Aucune donnée existante n'est lue ni modifiée ; les tables nouvelles sont vides après application (contrôle `verify`).
- Les lots antérieurs n'ont ni original ni état d'assistant (absence assumée) ; ils restent servis par le parcours actuel.
- Application **sans arrêt fonctionnel du parcours actuel** : l'API de ce lot fonctionne avec ou sans 0009 tant que l'écran n'est pas activé (`CatalogInterpreter:WizardEnabled=false` par défaut). Le contrôle `TestDatabase_AllMigrationsApplied` (`Stage=PostMigration`) échoue par construction sur une base sans 0009 : `BLOCKED` jusqu'au GO SQL, jamais `PASS`.
- `Down` : supprime seulement les déclencheurs, fonctions et tables de 0009 (perte des données de l'assistant) ; sur une base du propriétaire, le retour arrière est la **restauration de la sauvegarde** (§ 6).

## 5. Essais hors bases du propriétaire (pas une preuve d'application)

Cluster PostgreSQL **18.6** jetable (`initdb` dans le répertoire temporaire de la session, port **55434**, base et rôle `mmv_v2_scratch`, jamais 5432 / 5433), créé de zéro le 06/11/2026 :

1. 0001 → 0008 (scripts idempotents versionnés) puis **0009 idempotent appliqué deux fois** : aucune erreur, 9 migrations enregistrées.
2. `0009_CsvImportAssistant.verify.sql` : **14 / 14 `OK`**, 0 `ECART` (deux valeurs attendues mal comptées dans le premier jet du script — 21 colonnes de `imports.batches`, 8 contraintes de `batch_analyses` — corrigées après vérification contre l'inventaire 0008 ; aucune différence de schéma).
3. Inventaire relevé (`MMV_UPDATE_PHASE05_SCHEMA=1`) puis comparé : `Phase05SchemaTests` PASS.
4. Gardes prouvées par test (`Phase05AssistedImportTests`) : réécriture d'un original, réouverture d'une tentative terminée, suppression d'une décision, décision sur un lot clos → `42501`.
5. Forme finale des scripts (LF, **sans BOM** comme 0008 : le BOM ajouté par `dotnet ef` faisait échouer `psql` après normalisation LF — constat corrigé et consigné) rejouée sur une base neuve : 0001 → 0009 ×2, verify 14/14.
6. Sauvegarde puis restauration éprouvées sur ce cluster : voir [`evidence/csvai/implementation/SQL_0009_ESSAIS.md`](evidence/csvai/implementation/SQL_0009_ESSAIS.md).
7. **06/11/2026, post-audit — restauration avec des données de l'assistant** (fictives), version finale du script de contrôle : base `mmv_v2_scratch` peuplée par la suite de l'assistant (`MMV_PG05_KEEP_SYNTHETIC=1` : 10 originaux, 10 états, 11 analyses, 95 événements, 16 décisions, 3 lots appliqués avec résultat enregistré, 5 offres de verres). Précontrôles : 18.6, port 55434, identité, 9 migrations, 0 autre session. `pg_dump -Fc` : SHA-256 `388849ac…22817a`, 511 entrées, 43 `TABLE DATA` dont les 5 tables de 0009, 67 déclencheurs. Restauration dans une base **neuve** `mmv_v2_scratch_rest_pa` (même propriétaire, `template0`, UTF-8, collation `C`) par `pg_restore --exit-on-error --single-transaction` (rc 0). Empreinte `0009_restore_check.sql` de la source = celle de la base restaurée = celle de la base basculée : **IDENTIQUE** (43 tables, 67 déclencheurs tous actifs, 9 migrations ; 1 034 lignes, SHA-256 `cc7ac51c…b6e929c`). Bascule par renommage avec 0 session, ancienne base conservée sous `mmv_v2_scratch_avant_pa`. Sur la base basculée, suite de l'assistant **7/7 PASS** (gardes `42501`, rejeu), après purge des données conservées. Un premier passage avait donné 5/7 : unicité des valeurs d'indice des données conservées, pas un défaut de restauration. Journaux : [`RESTAURATION_essai2_20261106.log`](evidence/csvai/postaudit/RESTAURATION_essai2_20261106.log) (version finale) et [`RESTAURATION_essai_20261106.log`](evidence/csvai/postaudit/RESTAURATION_essai_20261106.log) (premier passage, même résultat). Script : [`restore_trial.sh`](evidence/csvai/postaudit/restore_trial.sh).
8. **06/11/2026, prévalidation sur les sauvegardes RÉELLES (lecture seule)** : `pg_dump -Fc` de `mmv_v2_dev` et de `mmv_v2_test`, session forcée en lecture seule. Ce **ne sont pas** les sauvegardes définitives d'avant application. Chaque sauvegarde est restaurée sur une base **neuve** du cluster **jetable** (rôles propriétaires recréés en `NOLOGIN`), comparée à l'empreinte de la base source, puis supprimée du jetable avec ses rôles. Un premier passage a montré que `pg_dump` / `pg_restore` réécrit le texte de 25 contraintes CHECK `IN (...)` sans en changer le sens. C'est pourquoi le script compare les définitions **normalisées**. Avec la version finale : **IDENTIQUE** pour les deux bases (38 tables, 56 déclencheurs tous actifs, 911 lignes d'empreinte). Sortie : [`BASES_PROPRIETAIRE_lecture_seule_20261106.txt`](evidence/csvai/postaudit/BASES_PROPRIETAIRE_lecture_seule_20261106.txt) ; script : [`prevalidation_owner_dumps.ps1`](evidence/csvai/postaudit/prevalidation_owner_dumps.ps1).

## 6. Plan de sauvegarde, d'application et de restauration — APRÈS GO SQL explicite seulement

Préalable : le propriétaire arrête de son propre chef les postes et l'API qui écrivent sur la base visée (aucun processus du propriétaire n'est arrêté par l'agent).

### 6.0 Contrôles préalables en lecture seule (relevés le 06/11/2026, à refaire juste avant l'application)

Relevé du 06/11/2026 (session forcée `default_transaction_read_only=on`, mot de passe lu dans les user-secrets, jamais affiché ; script [`evidence/csvai/postaudit/probe_owner_readonly.ps1`](evidence/csvai/postaudit/probe_owner_readonly.ps1), sortie [`evidence/csvai/postaudit/BASES_PROPRIETAIRE_lecture_seule_20261106.txt`](evidence/csvai/postaudit/BASES_PROPRIETAIRE_lecture_seule_20261106.txt)) :

| Contrôle | `mmv_v2_dev` | `mmv_v2_test` |
| --- | --- | --- |
| Instance, base, rôle | PostgreSQL **18.6**, port **5433**, base et rôle `mmv_v2_dev`, cluster `7696807267651395228` | 18.6, 5433, `mmv_v2_test`, même cluster |
| Migrations | 8 : 0001 → 0008 (`20261102183203_CatalogCorrections`) ; **0009 absente**, 0 table de 0009 | idem |
| Dérive de schéma | **aucune** : 821 lignes de structure (migrations, déclencheurs, contraintes, index, fonctions) identiques à une base de référence 0001 → 0008 bâtie sur le cluster jetable ; propriétaires et droits identiques | **aucune** (idem) |
| Écriture concurrente | 2 sessions **inactives** (vraisemblablement l'API du propriétaire, port 7267), 0 active, 0 en transaction ; aucune ligne insérée / modifiée / supprimée en 5 s | 0 session ; aucune écriture en 5 s |
| Sauvegarde de **prévalidation** (pas la sauvegarde définitive) | `mmv_v2_dev_prevalidation_0009_20261106_223601.dump`, SHA-256 `effd978ba46ae72401b66430835cd14dbd73edcd8d14959c93f56bb5228826f7`, 256 187 octets, 454 entrées, 38 `TABLE DATA` ; restaurée sur base neuve jetable : **IDENTIQUE** | `mmv_v2_test_prevalidation_0009_20261106_223601.dump`, SHA-256 `574c50821176b80d1cca2551a34766030449b190bff3a32a61c1f4dc55c006d6`, 219 587 octets, 454 entrées, 38 `TABLE DATA` ; **IDENTIQUE** |

Les sauvegardes de prévalidation sont rangées dans `%USERPROFILE%\Documents\MMV-V2-sauvegardes\`, avec leur liste `.list.txt`. Elles prouvent que la sauvegarde et la restauration des bases réelles sont faisables et fidèles. Elles **ne remplacent pas** les sauvegardes de l'étape 1, à reprendre au moment du GO, API et postes arrêtés.

Juste avant l'application, le propriétaire refait ces contrôles : **0** autre session sur la base visée (API et postes arrêtés), 0009 absente, `PGCLIENTENCODING=UTF8` dans la console (le script 0009 est entièrement ASCII ; `verify.sql` affiche des libellés accentués).

```powershell
# 1. Sauvegarde de mmv_v2_dev (format personnalisé), empreinte notée, liste relue,
#    puis EMPREINTE DE COHÉRENCE de la base sauvegardée (référence d'une éventuelle restauration, § 6.2)
$env:PGCLIENTENCODING = 'UTF8'
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$dir = "$env:USERPROFILE\Documents\MMV-V2-sauvegardes"
& 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -Fc `
    -f "$dir\mmv_v2_dev_avant_0009_$stamp.dump"
Get-FileHash "$dir\mmv_v2_dev_avant_0009_$stamp.dump" -Algorithm SHA256
& 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe' --list "$dir\mmv_v2_dev_avant_0009_$stamp.dump" | Select-String 'TABLE DATA' | Measure-Object
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -X -A -t -v ON_ERROR_STOP=1 `
    -f docs/phase/05/sql/0009_restore_check.sql | Out-File -Encoding utf8 "$dir\mmv_v2_dev_avant_0009_$stamp.check.txt"
Get-FileHash "$dir\mmv_v2_dev_avant_0009_$stamp.check.txt" -Algorithm SHA256

# 2. Application sur mmv_v2_dev (empreinte imposée = ligne du script idempotent dans le manifeste, lock_timeout borné)
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev `
    -Script docs/phase/05/sql/0009_CsvImportAssistant.idempotent.sql `
    -Sha256 30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0 -LockTimeout 5s

# 3. Contrôle (14 lignes OK attendues, toute ligne ECART = arrêt, aucune correction improvisée)
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -X -A -t -v ON_ERROR_STOP=1 `
    -f docs/phase/05/sql/0009_CsvImportAssistant.verify.sql

# 4. Même séquence pour mmv_v2_test (sauvegarde + empreinte, application, contrôle), puis suite PostgreSQL complète
#    dotnet test tests/MMV.PostgreSql.Tests -p:NuGetAudit=false --filter Stage=Phase05
# 5. Activation de l'écran (décision du propriétaire, configuration du serveur) : CatalogInterpreter:WizardEnabled=true
```

### 6.1 Ce qui n'est PAS une procédure de retour arrière (retiré le 06/11/2026)

- `pg_restore --clean` sur la base **active** : supprime puis recrée les objets dans la base en service. Une interruption laisse une base partiellement vidée, aucun moyen de comparer l'avant et l'après, et les écritures intervenues depuis la sauvegarde disparaissent sans trace. Option **retirée** du § 6 initial.
- `pg_restore --no-owner` : perd les propriétaires et les droits (le contrôle « propriétaires et droits » de `0009_restore_check.sql` le détecterait).
- `pg_restore --data-only` dans un schéma existant : les gardes de 0009 et `trg_rows_frozen` (`BEFORE INSERT`) refuseraient les lignes des lots clos. Une restauration complète, elle, crée les déclencheurs **après** le chargement des données.

### 6.2 Retour arrière : restauration sur une base NEUVE distincte, contrôles, puis bascule explicite

Décision du propriétaire seulement. Toute écriture postérieure à la sauvegarde de l'étape 1 sera **absente** de la base restaurée. La base écartée est **conservée** : jamais supprimée sans une décision distincte. Commandes à lancer avec un rôle autorisé à créer et renommer des bases (le superutilisateur local `postgres` : propriétaires et droits restaurés à l'identique), en console `PGCLIENTENCODING=UTF8`. Procédure éprouvée le 06/11/2026 sur le cluster jetable, avec des données de l'assistant (§ 5, essai 7).

```powershell
$env:PGCLIENTENCODING = 'UTF8'
$bin = 'C:\Program Files\PostgreSQL\18\bin'; $dir = "$env:USERPROFILE\Documents\MMV-V2-sauvegardes"
$stamp = '<horodatage de la sauvegarde>'; $dump = "$dir\mmv_v2_dev_avant_0009_$stamp.dump"; $fresh = "mmv_v2_dev_restore_$stamp"

# R1. Sauvegarde intacte et lisible : empreinte = celle notée à l'étape 1 ; liste relue
Get-FileHash $dump -Algorithm SHA256
& "$bin\pg_restore.exe" --list $dump | Select-String 'TABLE DATA' | Measure-Object

# R2. Base NEUVE distincte (jamais la base active) : même propriétaire, encodage et collation que la source
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d postgres -X -A -t -v ON_ERROR_STOP=1 `
    -c "SELECT datcollate, datctype, pg_get_userbyid(datdba) FROM pg_database WHERE datname = 'mmv_v2_dev'"
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d postgres -X -v ON_ERROR_STOP=1 `
    -c "CREATE DATABASE $fresh WITH OWNER mmv_v2_dev TEMPLATE template0 ENCODING 'UTF8' LC_COLLATE '<datcollate>' LC_CTYPE '<datctype>'"

# R3. Restauration : transaction unique, arrêt à la première erreur, propriétaires et droits conservés
#     (ni --clean, ni --no-owner, ni --data-only) ; code de sortie 0 exigé
& "$bin\pg_restore.exe" -h localhost -p 5433 -U postgres -d $fresh --exit-on-error --single-transaction $dump

# R4. Contrôles de cohérence AVANT toute bascule : l'empreinte de la base restaurée doit être IDENTIQUE à celle
#     relevée à l'étape 1 (migrations, chaque table : lignes + md5, séquences, déclencheurs TOUS actifs, contraintes,
#     index, fonctions, propriétaires et droits). Tout écart = arrêt, aucune bascule.
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d $fresh -X -A -t -v ON_ERROR_STOP=1 `
    -f docs/phase/05/sql/0009_restore_check.sql | Out-File -Encoding utf8 "$dir\$fresh.check.txt"
fc.exe /N "$dir\mmv_v2_dev_avant_0009_$stamp.check.txt" "$dir\$fresh.check.txt"   # « aucune différence » attendu
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d $fresh -X -A -t -v ON_ERROR_STOP=1 -f docs/phase/05/sql/0008_CatalogCorrections.verify.sql

# R5. Bascule EXPLICITE (décision du propriétaire) : API et postes arrêtés ; 0 session sur les deux bases ;
#     renommage (chaîne de connexion inchangée) ; la base écartée est conservée
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d postgres -X -A -t -v ON_ERROR_STOP=1 `
    -c "SELECT datname, count(*) FROM pg_stat_activity WHERE datname IN ('mmv_v2_dev', '$fresh') GROUP BY datname"   # aucune ligne attendue
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d postgres -X -v ON_ERROR_STOP=1 -c "ALTER DATABASE mmv_v2_dev RENAME TO mmv_v2_dev_ecartee_$stamp"
& "$bin\psql.exe" -h localhost -p 5433 -U postgres -d postgres -X -v ON_ERROR_STOP=1 -c "ALTER DATABASE $fresh RENAME TO mmv_v2_dev"

# R6. Après bascule : identité et migrations relues en lecture seule (0001 → 0008, 0009 absente si la sauvegarde
#     précède 0009) ; empreinte de nouveau IDENTIQUE ; écran de l'assistant DÉSACTIVÉ (WizardEnabled=false) tant que
#     0009 est absente ; redémarrage de l'API par le propriétaire. Même procédure, séparément, pour mmv_v2_test.
```

## 7. Risques

| Risque | Mesure |
| --- | --- |
| Volume des originaux (`bytea`) et des événements | bornes en base (2 Mio, 64 Kio, 4 Kio) ; tables séparées ; aucune purge automatique (Q-CSVAI-07) ; procédure de purge tracée à écrire après décision |
| Verrous pendant l'application | création de tables vides et d'index sur tables vides ; aucune réécriture de table existante ; `lock_timeout` borné |
| Écart entre le code et la base (base sans 0009) | écran désactivé par défaut ; routes du nouveau parcours en `404 assistant_disabled` ; aucune requête vers les tables de 0009 hors écran activé ; `PostMigration` `BLOCKED` |
| Déclencheurs trop stricts pour une opération future (purge, archivage) | gardes nommées et documentées ; toute évolution = nouvelle migration revue |
| Restauration | éprouvée sur le cluster jetable (§ 5) ; non éprouvée sur les bases du propriétaire |
| Retour arrière (06/11 post-audit) | jamais sur la base active : base neuve distincte, empreinte comparée, bascule par renommage, base écartée conservée (§ 6.2) ; éprouvé avec des données fictives de l'assistant (§ 5, essai 7) ; perte assumée des écritures postérieures à la sauvegarde (décision du propriétaire) |
| Texte des contraintes après restauration | `pg_restore` réécrit 25 CHECK `IN (...)`, sans en changer le sens. `0009_restore_check.sql` les compare donc sous forme normalisée. En revanche, l'inventaire **textuel** de `Phase05SchemaTests` diffère sur une base restaurée (constaté le 06/11 sur la base jetable basculée, reconstruite ensuite par les scripts). Après un retour arrière réel, il faudra un contrôle par `verify.sql` et une revue de cet écart, pas un échec à corriger en base. |
| Encodage du client `psql` sous Windows | constaté le 06/11 sur une base de référence jetable : un script accentué appliqué sans `PGCLIENTENCODING=UTF8` enregistre un texte mal encodé ; 0009 est entièrement ASCII (sans effet) ; `PGCLIENTENCODING=UTF8` prescrit aux § 6.0 à 6.2 ; `scripts/Invoke-MmvSqlScript.ps1` ne le fixe pas (à durcir avant un futur script accentué) |

## 8. Décision attendue

GO / NO-GO SQL du propriétaire sur ce dossier (script idempotent `30dc6e0a…a2e7e0`, empreinte complète dans [`sql/0009_MANIFEST.sha256`](sql/0009_MANIFEST.sha256) ; contrôle de restauration [`sql/0009_POST_AUDIT_MANIFEST.sha256`](sql/0009_POST_AUDIT_MANIFEST.sha256)). Après GO : sauvegardes, application dev puis test, contrôle 14/14, suite PostgreSQL complète sur `mmv_v2_test`, activation de l'écran, parcours natif deux postes avec Ollama réel (Gate D).
