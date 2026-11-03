# Phase 05 — Livraison des corrections après G05-SQL-0008 (03/11/2026)

**Statut : `PHASE05_CORRECTIONS_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** La Phase 05 n'est **pas** déclarée fusionnable : audit final du propriétaire requis. Branche locale `feat/phase-05-catalogue-stock`, commits locaux uniquement ; aucun push, aucune PR, aucun merge ; Phase 06 non commencée ; `apps/desktop/.env.development` du propriétaire intact (SHA-256 `a215e1f0…2e4e`), jamais indexé.

Mandat : [`prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md`](../../../prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md). Registre : [`CORRECTIONS_05.md`](CORRECTIONS_05.md). Dossier SQL : [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md). Preuves : [`evidence/sql0008/`](evidence/sql0008/).

## 1. Preuves de sauvegarde

`pg_dump` 18.6, format personnalisé, hors dépôt, une sauvegarde par base, prise juste avant son application :

| Base | Fichier | Taille | SHA-256 | `pg_restore --list` |
| --- | --- | --- | --- | --- |
| `mmv_v2_dev` | `C:\Users\Ali Zekhnini\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0008_20261103_153503.dump` | 219 440 o | `6848B40FFEF7DD86008B4992EC6E97EEC48ACA58144BBE8222381F3DE7778230` | exit 0, 408 entrées, 34 TABLE DATA, 10 schémas, 0 objet 0008 |
| `mmv_v2_test` | `C:\Users\Ali Zekhnini\Documents\MMV-V2-sauvegardes\mmv_v2_test_avant_0008_20261103_153559.dump` | 198 014 o | `FD9F2CEE4152CA12F951B3FF50652DD69E647DA2E96E0AD24005F6CA3C8BB821` | exit 0, 408 entrées, 34 TABLE DATA, 10 schémas, 0 objet 0008 |

Listes complètes enregistrées à côté (`.list.txt`, SHA-256 `927A65E6…7DB6FDB2` et `6F2C9440…D790149EA`). **Restauration complète non éprouvée** (seul le catalogue des sauvegardes a été vérifié). Détail : [`P0508_preflight_20261103.log`](evidence/sql0008/P0508_preflight_20261103.log) § 6.

## 2. Journal d'application

- Précontrôles : branche correcte ; manifeste 0008 **8 / 8** dans le dépôt ; scripts idempotent et direct **régénérés depuis EF Core identiques octet pour octet** ; aucun changement de modèle en attente ; PostgreSQL 18.6 sur 5433 (5432 jamais contacté) ; bases exactes `mmv_v2_dev` / `mmv_v2_test` ; 0001–0007 présentes, 0008 absente ; aucune dérive de schéma.
- **Écriture concurrente détectée** à 15:30 : l'API du propriétaire (7267) et `tauri dev` avaient 2 connexions sur `mmv_v2_dev`. STOP avant toute écriture, question au propriétaire ; il a arrêté lui-même ses processus. Revérifié : 0 session à 15:34:14 et juste avant chaque application. L'agent n'a arrêté ni relancé aucun processus du propriétaire.
- `mmv_v2_dev` : `Invoke-MmvSqlScript.ps1 … -Sha256 9a75acea…287e715c -LockTimeout 5s`, 15:35:18 → **COMMIT**, exit 0, 8 migrations dont `20261102183203_CatalogCorrections`.
- `mmv_v2_test` (après contrôle entièrement conforme de dev) : 15:35:59 → **COMMIT**, exit 0, 8 migrations.

Journaux : [`P0508_apply_mmv_v2_dev_20261103.log`](evidence/sql0008/P0508_apply_mmv_v2_dev_20261103.log), [`P0508_apply_mmv_v2_test_20261103.log`](evidence/sql0008/P0508_apply_mmv_v2_test_20261103.log).

## 3. Contrôles SQL

`0008_CatalogCorrections.verify.sql` : **14 / 14 OK sur `mmv_v2_dev`, 14 / 14 OK sur `mmv_v2_test`**, 0 écart ([`P0508_verify_20261103.log`](evidence/sql0008/P0508_verify_20261103.log)). Schéma exact : `SchemaStateTests` (8 migrations, rien en attente, 0001–0006 inchangées, schémas Phase 05 conformes) et `Phase05SchemaTests` (inventaire exact 0001–0008) **PASS**.

## 4. Intégrité des données existantes

Empreintes md5 de chaque table préexistante (34, dont `imports.batches` hachée sans la colonne ajoutée), et des relations (200), colonnes (432), contraintes (507), déclencheurs (50) et fonctions (11) de 0001–0007, avant et après : **identiques sur les deux bases** ; seule `__EFMigrationsHistory` passe de 7 à 8 lignes ([`P0508_fingerprints_20261103.log`](evidence/sql0008/P0508_fingerprints_20261103.log)). Sur `mmv_v2_dev`, aucune écriture de l'agent hors du script 0008.

## 5. Suites complètes (arbre livré)

| Suite | Résultat |
| --- | --- |
| `MMV.Api.Tests` | **527 / 527** |
| `MMV.Maintenance.Tests` | **50 / 50** |
| `MMV.PostgreSql.Tests` (Phases 02–05, serveur en mémoire) | **88 / 89** + 1 `NOT_TESTED` (processus réel non désigné dans ce mode) |
| `MMV.PostgreSql.Tests` via le **processus MMV.Api réel** (HTTPS 7367 sur `mmv_v2_test`) | **89 / 89** — 18 bancs Phase 05 par le processus réel (dont import CSV, profils, réémissions, transactions, offres de verres, plages, restrictions, révisions, compatibilité, permissions, magasin, assistant, recherche), 1 en mémoire (SignalR à réglage propre) ; HTTP en clair refusé `403 https_required` ; 19 purges à résidu 0 |
| Vitest ; `npm run check` ; ESLint ; `vite build` | **325 / 325** ; OK ; 0 erreur (1 avertissement préexistant) ; OK |

Journaux : [`P0508_dotnet_20261103.log`](evidence/sql0008/P0508_dotnet_20261103.log), [`P0508_frontend_20261103.log`](evidence/sql0008/P0508_frontend_20261103.log).

## 6. Natif Tauri (deux postes) et captures complémentaires

Deux vraies fenêtres Tauri / WebView2 (build de banc du front livré), API de banc réelle sur `mmv_v2_test`, données fictives : **69 / 69 contrôles PASS** ([`COR05_native_run_0008_20261103.log`](evidence/sql0008/COR05_native_run_0008_20261103.log)) — COR-05-01 (magasin de l'installation), COR-05-07 (recherche), COR-05-02 (SignalR entre deux clients : stock et prix A → B sans rechargement, coût jamais servi au vendeur, coupure / reprise de l'API), COR-05-06 / 08 (assistant, combobox), COR-05-03 (tableaux, quatre résolutions, deux thèmes, zoom 125 %), et deux étapes nouvelles :

- **Import CSV** (captures 12 à 18) : fichier Windows-1252 réel, tabulation, modèle d'import du fournisseur, contrôle à blanc, application tout ou rien, tarifs du même fichier en EUR, réémission sans doublon (1 / 1 / 2), erreur corrigée à l'écran.
- **Offres de verres** (captures 19 à 24) : saisie simple, testeur « à vérifier » / « incompatible » / « compatible », mode avancé et révision n° 2, poste vendeur sans prix ni création.

34 captures JPEG réduites dans [`evidence/sql0008/`](evidence/sql0008/).

## 7. Problèmes réellement restants

- **O-05-24 corrigé pendant cette livraison** (défaut réel, poste) : la combobox fournisseur acceptait Entrée sur des suggestions périmées (une frappe rapide rattachait un autre fournisseur). Corrigé et prouvé en natif ; aucune autre régression constatée.
- O-05-23 et O-05-26 : défauts **de tests** corrigés (liste des migrations ; mode processus réel du banc). Aucun SQL modifié.
- O-05-25 : rapport d'audit du propriétaire `MMV_V2_AUDIT_G05_SQL_0008_2026-11-03.md` absent de l'espace de travail ; ses éventuelles réserves n'ont pas pu être vérifiées point par point.
- O-05-27 / BL-05-C4 : à 1366 px, le détail d'une offre de verres se lit dans le défilement horizontal du tableau (pas de débordement de page) — lisibilité à revoir.
- Restauration complète des sauvegardes : non éprouvée.
- Toujours ouverts (inchangés) : G05-DESIGN (revue visuelle, dont les nouveaux écrans), BL-05-C2 logo sombre (O-05-17), BL-05-C3 offres sans `costs.write`, G05-FISCAL / G05-LEGAL, exports CSV (R-F-07), offres de verres non reliées aux ventes (Phase 06 hors mandat).
- Processus du propriétaire : arrêtés par lui avant l'application, **non relancés** par l'agent.

## 8. Archive d'audit

`MMV-V2-phase05-corrections-final.zip` (racine du dépôt, non versionnée) : documents de la Phase 05 modifiés, dossier `evidence/sql0008/`, mandat, et correctif `git format-patch` des commits de cette livraison (sans images). Empreinte communiquée dans le compte rendu de livraison.
