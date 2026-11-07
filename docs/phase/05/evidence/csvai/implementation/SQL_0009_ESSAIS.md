# G05-SQL-0009 — essais sur cluster PostgreSQL 18 JETABLE (06/11/2026)

Aucune base du propriétaire touchée (`mmv_v2_dev`, `mmv_v2_test` : 0009 **non appliquée**). Dossier : [`../../../SQL_0009_REVUE.md`](../../../SQL_0009_REVUE.md).

| # | Essai | Commande (résumé) | Résultat |
| --- | --- | --- | --- |
| 1 | Cluster neuf | `initdb -D <scratchpad>/pg18 -U postgres -A trust -E UTF8 --locale=C` ; `listen_addresses=127.0.0.1`, `port=55434` ; PostgreSQL **18.6** ; rôle et base `mmv_v2_scratch` | PASS |
| 2 | Historique 0001 → 0008 | scripts idempotents versionnés `docs/phase/{01b,02,03,04,05}/sql/000{1..8}_*.idempotent.sql`, `psql -v ON_ERROR_STOP=1` | PASS (rc 0 ×8) |
| 3 | 0009 idempotent, **deux fois** | `psql -f docs/phase/05/sql/0009_CsvImportAssistant.idempotent.sql` ×2 | PASS (rc 0 ×2) ; `__EFMigrationsHistory` = 9 lignes, dernière `20261106154105_CsvImportAssistant` |
| 4 | Contrôle en lecture seule | `psql -X -A -t -f docs/phase/05/sql/0009_CsvImportAssistant.verify.sql` | **14/14 OK** ([détail](SQL_0009_VERIFY_scratch_et_restauration.txt)) — premier jet : 2 `ECART` dus à des valeurs attendues mal comptées dans le script (21 colonnes de `imports.batches`, 8 contraintes de `batch_analyses`), corrigées après vérification contre l'inventaire 0008 ; aucune différence de schéma |
| 5 | Inventaire exact | `MMV_UPDATE_PHASE05_SCHEMA=1 dotnet test … --filter Phase05SchemaTests` puis sans la variable | PASS ; inventaire 0009 = inventaire 0008 **intégralement conservé** + 159 lignes |
| 6 | Sauvegarde | `pg_dump -Fc mmv_v2_scratch` → `scratch_0009.dump` (SHA-256 `b3f71e58bb6cfad6fe286ed5d73a5a21116bb4ff036e97a59da83948bb24a81a`, 58 entrées `batch_*` dans `pg_restore --list`) | PASS |
| 7 | Restauration | `CREATE DATABASE mmv_v2_restore` ; `pg_restore --no-owner -d mmv_v2_restore` | PASS (rc 0) |
| 8 | Contrôle de la base restaurée | même `verify.sql` | **14/14 OK** — un premier passage avait montré 1 `ECART` TEXTUEL (`ck_batches_kind` réécrite par `pg_restore` sous la forme `ARRAY[('catalog'::character varying)::text, …]`, sens identique, même phénomène que O-05-13) ; contrôle rendu **sémantique** (trois valeurs, rien d'autre), repassé 14/14 sur les deux bases |
| 9 | Gardes en base | `Phase05AssistedImportTests` : réécriture d'un original, réouverture d'une tentative terminée, suppression d'une décision, décision sur un lot clos | PASS (`42501` ×4) |
| 9b | **Forme finale des scripts** (fins de ligne LF, comme Git les stocke ; **sans BOM**, comme 0008) | base neuve `mmv_v2_lf3` : 0001 → 0008 puis 0009 idempotent ×2, puis `verify.sql` | **PASS** (rc 0 ×10, 9 migrations, **14/14 OK**). Constat consigné : les scripts générés par `dotnet ef` commençaient par un BOM UTF-8 et mêlaient CRLF / LF ; après normalisation LF seule, `psql` refusait la première instruction (`syntax error at or near "U+FEFF START"`, base `mmv_v2_lf2`) ; BOM retiré (convention de 0008), contenu SQL inchangé. Les essais 2–8 ont porté sur la forme générée (CRLF + BOM), identique en contenu |
| 10 | Suite PostgreSQL complète | `dotnet test tests/MMV.PostgreSql.Tests` (cluster jetable pour la Phase 05) | voir [`TESTS_SYNTHESE.md`](TESTS_SYNTHESE.md) |

Limites : restauration éprouvée sur le cluster jetable seulement (schéma, gardes, contrôles) ; **non éprouvée** sur les bases du propriétaire. Les tables de 0009 étaient vides au moment de la sauvegarde (le banc purge ses données) : l'aller-retour des **données** de l'assistant par `pg_dump` / `pg_restore` est `NOT_TESTED`.
