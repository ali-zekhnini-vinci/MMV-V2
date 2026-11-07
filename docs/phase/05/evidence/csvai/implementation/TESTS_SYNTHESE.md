# CSV-IMPORT-AI-01 — synthèse des tests (06/11/2026, passage final après la dernière modification de code)

Poste de développement Windows 11, .NET 10.0.402, Node 22.23.3, PostgreSQL 18.6, Ollama 0.40.14. Cible PostgreSQL de la Phase 05 : cluster **jetable** `mmv_v2_scratch` (port 55434, 0001 → 0009) ; les tests d'état des bases du propriétaire (`SchemaStateTests`) les lisent en **lecture seule**.

| Suite | Commande | Résultat |
| --- | --- | --- |
| Build | `dotnet build MMV.slnx --artifacts-path bin/csvai -p:NuGetAudit=false` | **0 avertissement, 0 erreur** (analyseurs en erreur) |
| API | `dotnet test tests/MMV.Api.Tests --no-build` | **656 / 656 PASS** (dont 95 `Catalog/Assisted/*`) |
| Maintenance | `dotnet test tests/MMV.Maintenance.Tests --no-build` | **50 / 50 PASS** |
| PostgreSQL | `MMV_PG05_SCRATCH=… MMV_CSVAI_REAL_OLLAMA=http://127.0.0.1:11434 dotnet test tests/MMV.PostgreSql.Tests --no-build` | **104 : 101 PASS, 2 en échec ATTENDU, 1 NOT_TESTED** |
| — échecs attendus | `SchemaStateTests.DevDatabase_AllMigrationsApplied`, `TestDatabase_AllMigrationsApplied` | **BLOCKED** : 0009 absente de `mmv_v2_dev` / `mmv_v2_test` (GO SQL requis) ; aucune autre différence |
| — NOT_TESTED | `Phase05RealHttpsTests.RealProcess_…` | processus HTTPS réel non désigné (inchangé depuis UX-IA) |
| — assistant | `Phase05AssistedImportTests` (6) | PASS : produits bout en bout (doublon de SKU accepté puis diagnostiqué, montant confirmé avec empreinte, révisions, contrôle, application, gardes en base), résultat tardif après annulation, échecs jamais repliés en silence, décisions concurrentes, refus techniques seulement et injection, offres de verres |
| — Ollama réel | `Phase05AssistedRealOllamaTests` (1) | PASS ([résultats](OLLAMA_REEL_qwen3.5-4b_20261106.txt)) |
| — schéma | `Phase05SchemaTests` | PASS (inventaire 0009) |
| Vitest | `npx vitest run` (apps/desktop) | **331 / 331 PASS** (30 fichiers ; dont 5 de l'assistant). Un passage antérieur, machine chargée par la suite PostgreSQL, avait montré 1 échec de minutage dans `AppointmentsModule.test.tsx` (fichier non modifié) : 36/36 à deux reprises en isolement, puis 331/331 au passage final |
| TypeScript | `npm run check` (3 configurations) | **PASS** |
| ESLint | `npx eslint .` | **0 erreur**, 1 avertissement préexistant (`src/clients/ClientsTable.tsx`, hors lot) |
| SQL 0009 | [`SQL_0009_ESSAIS.md`](SQL_0009_ESSAIS.md) | PASS (cluster jetable) |
| Captures | `node e2e/bench-04a/shoot.mjs captures/plan.json captures <profil>` | **12 / 12**, 0 erreur console, 0 débordement horizontal ([`captures/report.json`](captures/report.json)) — composant RÉEL, réponses serveur scriptées, Edge sans interface : simulation navigateur, **pas** une preuve Tauri |

Non exécuté : parcours Tauri natif deux postes (NOT_TESTED : exige 0009 sur une base servie et l'écran activé) ; tests de l'assistant sur `mmv_v2_test` (NOT_TESTED : 0009 non appliquée) ; accessibilité clavier et contrastes mesurés (NOT_TESTED).
