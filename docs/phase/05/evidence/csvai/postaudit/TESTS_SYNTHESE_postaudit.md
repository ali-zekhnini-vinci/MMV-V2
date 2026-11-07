# Synthèse des tests — corrections post-audit CSV-IMPORT-AI-01 (06/11/2026)

Toutes les commandes ont été relancées **après la dernière modification du code**. Branche `spike/csv-import-ollama-benchmark`, HEAD `55f021d`, rien n'est commité. Cluster PostgreSQL 18.6 **jetable** sur le port 55434, base et rôle `mmv_v2_scratch`, reconstruit par les scripts versionnés 0001 → 0009 (0009 ×2, `verify.sql` 14/14 OK).

| Suite | Commande | Résultat |
| --- | --- | --- |
| Build | `dotnet build MMV.slnx --artifacts-path bin/csvai -p:NuGetAudit=false` | 0 avertissement, 0 erreur |
| API | `dotnet test tests/MMV.Api.Tests --artifacts-path bin/csvai -p:NuGetAudit=false --no-build` | **669/669** (656 avant ; +13 : `CsvAssistantPostAuditTests`) |
| Maintenance | `dotnet test tests/MMV.Maintenance.Tests …` | **50/50** |
| PostgreSQL | `MMV_PG05_SCRATCH="Host=127.0.0.1;Port=55434;Database=mmv_v2_scratch;Username=mmv_v2_scratch;Password=scratch" MMV_CSVAI_REAL_OLLAMA=http://127.0.0.1:11434 dotnet test tests/MMV.PostgreSql.Tests …` | **106 : 103 PASS** (dont Ollama réel, `Phase05SchemaTests` 3/3, assistant 7/7), 2 **BLOCKED** (`*_AllMigrationsApplied`, 0009 absente des bases du propriétaire), 1 NOT_TESTED (`Phase05RealHttpsTests`) |
| Vitest | `npx vitest run` (apps/desktop) | **337/337**, 31 fichiers (+5 : clavier, contrastes ×4) |
| TypeScript | `npx tsc --noEmit -p .` | PASS |
| ESLint | `npx eslint src` | 0 erreur, 1 avertissement préexistant (`apps/desktop/src/clients/ClientsTable.tsx`, `react-hooks/incompatible-library`) |
| Build du poste | `npx vite build` | PASS |
| Empreintes SQL | `sha256sum -c docs/phase/05/sql/0009_MANIFEST.sha256` ; `sha256sum -c docs/phase/05/sql/0009_POST_AUDIT_MANIFEST.sha256` | 7/7 OK ; 2/2 OK |
| Restauration (données de l'assistant) | `restore_trial.sh` | PASS — [`RESTAURATION_essai2_20261106.log`](RESTAURATION_essai2_20261106.log) |
| Prévalidation des sauvegardes réelles | `prevalidation_owner_dumps.ps1` (lecture seule) | PASS — [`BASES_PROPRIETAIRE_lecture_seule_20261106.txt`](BASES_PROPRIETAIRE_lecture_seule_20261106.txt) |

Incident de banc, consigné : après l'essai de bascule, la base jetable `mmv_v2_scratch` était une **copie restaurée**. Lancée sur cette copie, la suite PostgreSQL a donné `Phase05Schemas_MatchTheReviewedInventory` en échec, parce que `pg_restore` réécrit le texte de 25 CHECK `IN (...)` sans en changer le sens : 100 PASS, 3 FAIL, 2 NOT_TESTED. La base jetable a été reconstruite par les scripts versionnés, puis la suite relancée : 101 PASS, 3 FAIL, 1 NOT_TESTED. Le même test échouait encore, cette fois sur les empreintes des 4 fonctions de garde de 0009 : l'inventaire de revue les porte en CRLF, le script approuvé (LF) en produit d'autres. Correction côté dossier, script inchangé : inventaire appliqué + test des fins de ligne (`Phase05SchemaTests` 3/3). Suite finale : ligne PostgreSQL ci-dessus.

Non exécuté : parcours Tauri natif deux postes, clavier et contrastes natifs, suite de l'assistant sur `mmv_v2_test` (NOT_TESTED, 0009 non appliquée) ; processus HTTPS réel (NOT_TESTED, non désigné).
