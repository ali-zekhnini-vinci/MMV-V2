# 05 — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 et maquette Lot 6 uniquement)

| Source | Usage |
|---|---|
| `prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md` | mandat 05-00 (copie versionnée du message du propriétaire) |
| `prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md` | réponses Q01–Q20 et mandat de clôture 05-00 (copie versionnée du message du propriétaire, 30/10/2026) |
| `CLAUDE.md`, `README.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/phase/00.md` | gouvernance, DOCUMENTATION GATE, registre des décisions, backlog et séquence (Phase 05 « avant le premier encaissement réel ») |
| `docs/phase/04.md`, `docs/phase/04/GATES_AND_DECISIONS.md`, `SOURCE_MAP.md`, `QA_MATRIX.md`, `API_OPTICAL.md`, `IMPLEMENTATION_SPEC.md` (impacts Ventes) | conventions de documentation, d'API, d'idempotence, de concurrence et de verrous (O-04-53 / O-04-54) |
| `docs/phase/03/sql/0005_AuditJournal.sql` | formats des codes d'audit |
| `src/MMV.Domain/Identity/Permissions.cs` | permissions existantes (format `domaine.action`, jusqu'à trois segments) |
| `src/MMV.Infrastructure/*/…Model.cs`, `…Store.cs` (recherche ciblée) | schémas existants (`auth`, `clients`, `appointments`, `audit`, `optical`), `xmin`, `archived_at_utc`, verrous |
| `registres/DESIGN_SOURCES_SHA256.csv` | empreinte attendue du Lot 6 |
| `references/phase04/catalogue/MMV_V2_Registre_134_Groupes.csv`, `MMV_V2_Registre_324_Etats.csv` | 37 groupes B-098 à B-134, 107 états `MMV-L06-*` (non dupliqués ; lignes Lot 6 extraites dans `references/phase05/INDEX_LOT6.md`) |
| `Downloads/MMV initialisation projet V1/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` (dossier du propriétaire, lecture seule) | sections, légendes de planches, notes de conception ; **référence visuelle et ergonomique seulement** (D-05-01 `APPROVED`) |
| `Downloads/MMV initialisation projet V1/` : `support.js`, `_ds/…`, `scraps/lot6-*.png`, liste des entrées de `MMV.zip` | ressources de consultation du Lot 6 et recherche d'autres références Lot 6 (30/10/2026) |

Aucune lecture de l'ancien dépôt, de son code, de ses DTO, de son modèle de données ni de ses migrations (les mentions de l'ancien code figurent dans le texte même de la maquette, O-05-01). Aucune connexion à `mmv_v2_dev` ni `mmv_v2_test`.

## Maquette Lot 6

| Fichier | SHA-256 | Taille | Registre |
|---|---|---|---|
| `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` | `6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7` | 175 994 octets | identique à `registres/DESIGN_SOURCES_SHA256.csv` (37 groupes, 107 états, `DRAFT_NON_APPROVED`) |

*29/10/2026 :* la maquette n'avait pas été copiée dans le dépôt ; un rendu de contrôle sera fait avec G05-DESIGN.

## Références Phase 05 — `references/phase05/` (30/10/2026)

**Copie de travail non versionnée**, comme `references/phase03/` et `references/phase04/` (O-05-08) ; les empreintes ci-dessous, versionnées, en sont la preuve. Copies **octet pour octet** depuis `Downloads/MMV initialisation projet V1/`, chaque empreinte comparée à l'original (Q-05-017) ; manifeste `references/phase05/SHA256SUMS.txt` (`sha256sum -c` : 15 OK, Q-05-018).

| Fichier (sous `references/phase05/`) | Octets | SHA-256 |
|---|---:|---|
| `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` | 175 994 | `6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7` |
| `lot6/support.js` | 64 222 | `ae4f0ac8449655e17cca1e3b179effcb6817a3b0d8dc47f112a9c39c25c39fd7` |
| `lot6/_ds/mmv-managemyvision-design-system-90800da0-d730-42af-b51e-c94dbc7d7135/styles.css` | 512 | `99d81fe93bb0fbf3019f816d52cb7fe1c894c3c1ae6b7f307d41097e86fbc859` |
| `lot6/_ds/…/_ds_bundle.js` | 126 785 | `88cf0aa328630a56fc5c47b763df7820615e967f27aab8c5b7bb2500f67e2eda` |
| `lot6/_ds/…/assets/logo-dark.png` | 66 344 | `3df57decabf76455374f0051b6dec006dffe8b55ad7aeabd6b6e3cce8e3856cb` |
| `lot6/_ds/…/assets/logo-horizontal.png` | 104 999 | `3570c6f9f0a08f60428e8fe1be6d426c0d9e4f9c5ce65eb7b128a311f1913a71` |
| `lot6/_ds/…/tokens/base.css` | 1 015 | `d5ca8e35701776c514e19d03f19f27c97fbe2594c6afd4b86a3c5d70ac40c320` |
| `lot6/_ds/…/tokens/colors.css` | 4 201 | `1a9bf7858d61e17b4aca196ec3e2469254dc4724dba378c78de04d48157ff92c` |
| `lot6/_ds/…/tokens/fonts.css` | 359 | `6d834a2bfbbcbe9fe38ffa7828775309bd8a1e5155c94b6a43d83b0b60c3918b` |
| `lot6/_ds/…/tokens/radii.css` | 647 | `0861f3679dcead974a2baa82b06b8eda07f3ef74ffd27e9f3397afc0419b4a66` |
| `lot6/_ds/…/tokens/shadows.css` | 903 | `aabd33b441a5937b34ef162729103d11466b750da4848a02707d007b961b8bdb` |
| `lot6/_ds/…/tokens/spacing.css` | 658 | `589f06b2d380ccbfe4ace5d0ee3a82edf2170ee3140604b0d30dc07787f5a70f` |
| `lot6/_ds/…/tokens/typography.css` | 1 465 | `da0dea77a4462e93ea66937c23f2ef822c54db4a60840166f91c28fae5132d7f` |
| `lot6/scraps/lot6-check.png` | 31 177 | `eb6cac0de244e7eea2d636303984d08624e954ace8242efd77b2521b44c420a2` |
| `lot6/scraps/lot6-s1.png` | 26 898 | `2a2d2ee09170beb7f4d19c13cb80705d6fff6924a48f91cf5628e186914b9a20` |

Fichiers générés (non sources) : `README.md` (contenu, statut, limites, consultation), `INDEX_LOT6.md` (37 groupes, 107 états, `data-mount`, module V2 indicatif), `SHA256SUMS.txt`.

Écartés : `MMV.zip` (3,6 Mo, archive des Lots 1 à 6 ; ses entrées Lot 6 ont les mêmes empreintes que les copies), `scraps/check-list.png` (non rattaché au Lot 6), `_ds/…/readme.md`, `_ds_manifest.json`, `_adherence.oxlintrc.json`, `assets/logo-monogram.png` (non chargés par la maquette). `support.js` et les fichiers `_ds` copiés sont identiques à ceux de `references/phase03/lot2/` (doublon de ~ 370 Ko accepté pour que la maquette reste consultable par chemins relatifs). Aucune autre référence Lot 6 trouvée dans `Downloads/`. Aucun état sans planche fabriqué.

## Mise à jour 30/10/2026 (implémentation) — versionnement partiel de `references/phase05/`

Versionnés : `README.md`, `INDEX_LOT6.md`, `SHA256SUMS.txt`, `.gitignore`, `lot6/support.js`, `lot6/_ds/…`. **Non versionnés** (identifiants de l'ancien logiciel et coordonnées de fournisseurs dont le caractère fictif n'est pas établi) : `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`, `lot6/scraps/*.png` — récupération et contrôle d'empreinte : `references/phase05/README.md` § « Versionnement ». Empreintes inchangées. Aucune source supplémentaire lue pour l'implémentation ; ancien dépôt non consulté.

## Mise à jour 30/10/2026 (G05-SQL accordée, intégration finale)

| Source | Usage |
|---|---|
| `prompts/MMV_V2_G05_SQL_GO_INTEGRATION_FINALE_2026-10-30.md` | mandat (copie versionnée du message du propriétaire) |
| `MMV_V2_AUDIT_G05_SQL_0007_2026-10-30.md` | **non disponible** dans l'espace de travail, non lu (O-05-18) |
| `docs/phase/05/sql/0007_MANIFEST.sha256` et les 12 fichiers qu'il couvre | précontrôle 12 / 12 OK ; aucun de ces fichiers modifié |
| `mmv_v2_dev`, `mmv_v2_test` (PostgreSQL 18.6, 5433) | lectures de contrôle en session lecture seule ; écritures : script approuvé (dev, test), données fictives de banc (test seulement), purgées |
| `docs/phase/04/LIVRAISON_04C.md`, `evidence/P04C_harness_c04http.cs.txt`, `apps/desktop/e2e/run-native.mjs` | procédés de banc reconduits (API copiée hors dépôt, Tauri renommé, CDP) |

Produits (nouveaux, versionnés) : [`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md) ; `sql/0007_CatalogStockModule.schema-inventory.applied.txt` (relu sur `mmv_v2_test` après application, SHA-256 `5bf96490ef341638ae2072e3a4ffaeff788b9b9970134f6ac4972dde153c3d63`, **hors manifeste 0007**, O-05-13) ; `evidence/P05F_*` (précontrôles, application, `verify.sql`, empreintes de contenu, PostgreSQL, .NET, frontend, natif, journal de l'API de banc, outil de banc, 16 captures).

## Sources de la refonte G05-DESIGN (31/10/2026)

| Source | Usage | SHA-256 |
|---|---|---|
| `prompts/MMV_V2_PROMPT_CLAUDE_PHASE05_REDESIGN_LOT6.md` | mandat (copie versionnée, identique à celle du ZIP de comparaison) | `c5b7cd6310a057d5b2550426bd292b60fd16d56d56bdffa7eab030f83c431360` |
| `MMV_V2_LOT6_COMPARAISON_VISUELLE.zip` (racine, **non suivi**) | audit `MMV_V2_PHASE05_AUDIT_FIDELITE_LOT6.md` (`359e5219e84deed7a1dd5bedc2265b8e41e94483a45f29917e0ef0db5bbe1dfe`), rendus de référence et comparaisons du propriétaire | `5b36c347dd31922e9242396584778907eba91f13b8a2b0198546e5d872e852f8` |
| `MMV initialisation projet V1 (7).zip` (racine, **non suivi**) | `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` (`6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7`, **identique** au fichier déjà enregistré), `_ds/`, `support.js` | `d214daa703cd8f8c4f2cdf6c44750c5a746f93387da61a4f292e624a271b9c52` |

Rendus des 107 cadres (Edge sans interface, [`evidence/design/G05D_render-lot6.mjs.txt`](evidence/design/G05D_render-lot6.mjs.txt)) et planches côte à côte : **non versionnés** (O-05-10 : identifiants de l'ancien logiciel et coordonnées de fournisseurs de la maquette) ; archive d'audit `MMV-V2-phase05-design-lot6.zip`. Versionnés : captures natives, journaux et correspondances dans [`evidence/design/`](evidence/design/).

## Mise à jour 02–03/11/2026 (corrections COR-05-01 à COR-05-08)

- Mandat : [`prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md`](../../../prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md) ; registre : [`CORRECTIONS_05.md`](CORRECTIONS_05.md) ; revue SQL : [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md) ; manifeste [`sql/0008_MANIFEST.sha256`](sql/0008_MANIFEST.sha256).
- Code serveur nouveau : `src/MMV.Api/Installation/ActiveStore.cs`, `src/MMV.Api/Realtime/Realtime.cs`, `src/MMV.Application/Catalog/ProductCreationService.cs`, `ImportAssistant.cs`, `LensOfferService.cs`, `src/MMV.Domain/Catalog/SkuGenerator.cs`, `LensOffer.cs`, `LensOffers.cs`, `src/MMV.Domain/CsvImport/SupplierImportProfile.cs`, `src/MMV.Infrastructure/Catalog/CatalogSearch.cs`, `CorrectionStores.cs`, migration `src/MMV.Infrastructure/Persistence/Migrations/20261102183203_CatalogCorrections.cs`.
- Poste nouveau : `apps/desktop/src/realtime/` (client SignalR, fournisseur, contexte), `apps/desktop/src/catalog/ProductWizard.tsx`, `productForm.ts`, `SupplierCombobox.tsx`, `catalogRealtime.ts`, `csvFile.ts`, `lensOffers.ts`, `LensOffersView.tsx`, `corrections.test.ts`.
- Outils de preuve : `apps/desktop/e2e/native-cor05.mjs` (scénario natif deux postes) ; preuves légères `docs/phase/05/evidence/corrections/` (42 captures JPEG réduites, journal de l'exécution complète, banc d'amorçage `COR05_harness_seed.cs.txt` sans secret).
- Référence visuelle consultée : `references/phase05/lot6/_ds/…/assets/logo-dark.png` (copie de travail non versionnée, empreinte `3df57dec…`, identique à l'actif du Shell-Handoff) pour O-05-17 ; aucune copie versionnée.
- Dépendances ajoutées : `@microsoft/signalr` 10.0.11 (poste, chargé à la demande) ; `Microsoft.AspNetCore.SignalR.Client` 10.0.13 (tests PostgreSQL seulement). Serveur : SignalR du cadre partagé ASP.NET Core (aucun paquet).


## Mise à jour 03/11/2026 (G05-SQL-0008 appliquée)

- Mandat : [`prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md`](../../../prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md) ; rapport : [`LIVRAISON_05_CORRECTIONS.md`](LIVRAISON_05_CORRECTIONS.md).
- Preuves : `docs/phase/05/evidence/sql0008/` (journaux `P0508_*`, journal natif `COR05_native_run_0008_20261103.log`, 34 captures JPEG réduites `COR05_native_*_20261103.jpg`, banc d'amorçage `P0508_harness_seed.cs.txt` et purge `P0508_purge-test.ps1.txt`, sans secret).
- Code modifié : `apps/desktop/src/catalog/SupplierCombobox.tsx` (O-05-24) ; tests `tests/MMV.PostgreSql.Tests/SchemaInspection.cs` (O-05-23), `Phase05Harness.cs` (O-05-26) ; scénario `apps/desktop/e2e/native-cor05.mjs` (étapes `importcsv`, `offres`, option `--csv-dir`).
- Aucun fichier SQL ni migration modifié (manifeste 0008 : 8 / 8).

## Mise à jour 03/11/2026 (lot UX-IA)

- Mandat : [`prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md`](../../../prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md).
- Code : `src/MMV.Application/Catalog/ProductCreationService.cs`, `ImportAssistantAi.cs` (nouveau), `ImportAssistant.cs`, `ImportService.cs`, `CsvTable.cs`, `LensCatalogService.cs` (nouveau) ; `src/MMV.Api/Installation/ImportAssistantService.cs` (nouveau), `src/MMV.Api/Catalog/CatalogEndpoints.cs`, `StockEndpoints.cs`, `src/MMV.Api/Program.cs`, `appsettings.json` ; `src/MMV.Domain/Audit/AuditEvent.cs` (liste blanche) ; `src/MMV.Infrastructure/DependencyInjection.cs`.
- Poste : `apps/desktop/src/catalog/ProductWizard.tsx`, `ImportView.tsx`, `LensRangeWizard.tsx` (nouveau), `LensReferencesView.tsx`, `LensOffersView.tsx`, `lensOffers.ts`, `ModelViews.tsx`, `StockViews.tsx`, `productForm.ts`, `csvFile.ts`, `CatalogModules.tsx`, `Catalog.css`, `apps/desktop/src/api/catalog.ts`.
- Tests : `tests/MMV.Api.Tests/Catalog/ImportAssistantTests.cs`, `SingleStoreRoutesTests.cs` ; `tests/MMV.PostgreSql.Tests/Phase05ImportAssistantTests.cs`, `Phase05LensCatalogTests.cs`, `Phase05ProductWizardTests.cs`, `Phase05StoreContextTests.cs`, `Phase05CorrectionsSchemaTests.cs` (inchangé), `Phase05Harness.cs` ; `apps/desktop/src/catalog/corrections.test.ts`.
- Preuves : scénario natif `apps/desktop/e2e/native-uxai.mjs` ; `docs/phase/05/evidence/uxai/` (journal natif, banc Ollama, amorçage du banc sans secret, 26 captures JPEG réduites).

## Mise à jour 04/11/2026 (lot CSV-IMPORT-AI-01, Lot A)

- Mandat : [`prompts/MMV_V2_MANDAT_CSV_IMPORT_AI_01_2026-11-04.md`](../../../prompts/MMV_V2_MANDAT_CSV_IMPORT_AI_01_2026-11-04.md) ; registre `docs/phase/05/CSV_IA_05.md` ; conception du Lot B (brouillon) `docs/phase/05/CSV_IA_05_CONCEPTION.md`.
- Code applicatif : **aucun changement**.
- Banc : `docs/phase/05/evidence/csvai/` — harnais `bench/CsvAiBench.cs` (fichier unique .NET 10), consigne `bench/system_prompt.txt`, options `bench/inference_options.json`, exemples `bench/example_*_F01.*` ; corpus synthétique `corpus/F01.csv` à `F15.csv` + `MANIFEST.json` (empreintes) + `.gitattributes` (`-text`) ; attendus `expected/F01.json` à `F15.json` ; sorties brutes `runs/llama3.2_latest.jsonl`, `runs/qwen3.5_2b.jsonl`, `runs/qwen3.5_4b.jsonl`, console `runs/RUN_CONSOLE_20261104.log`, environnement `runs/ENV_before_run_20261104.txt`, pilote exclu `runs/pilot-v0/` ; résultats `results/RESULTS.md`, `results/scores_by_call.csv`, `results/summary.json` ; rapport `RAPPORT_BENCHMARK.md`.

## Mise à jour 04/11/2026 (lot CSV-IMPORT-AI-01, Lot B0)

- Décision du propriétaire : [`prompts/MMV_V2_GO_CONDITIONNEL_GATE_A_CSV_IMPORT_AI_01_B0_2026-11-04.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_GATE_A_CSV_IMPORT_AI_01_B0_2026-11-04.md) ; registre `docs/phase/05/CSV_IA_05.md` (décisions, gates) ; conception révisée `docs/phase/05/CSV_IA_05_CONCEPTION.md`.
- Code applicatif : **aucun changement**. Preuves du Lot A sous `docs/phase/05/evidence/csvai/` : **inchangées**.
- B0 : `docs/phase/05/evidence/csvai/b0/` — rapport `RAPPORT_B0.md` ; harnais `bench/CsvAiBenchV2.cs` ; consigne `bench/system_prompt_v2.txt` ; options `bench/inference_options_v2.json` ; exemples `bench/example_user_message_F01.txt`, `bench/example_user_message_F17.txt`, `bench/example_schema_F01.json`, `bench/example_schema_F17.json` ; corpus `corpus/F16.csv` à `F20.csv` + `MANIFEST_B0.json` + `.gitattributes` ; attendus `expected/F16.json` à `F20.json` ; sorties `runs/main__qwen3.5_4b.jsonl`, `runs/light__qwen3.5_2b.jsonl`, `runs/light__llama3.2_latest.jsonl`, consoles `runs/RUN_CONSOLE_B0_main_20261104.log`, `runs/RUN_CONSOLE_B0_light_20261104.log`, gel `runs/ENV_before_B0_20261104.txt`, pilotes exclus `runs/pilot-b0/` ; contrôle v1 `v1-control/` (copie du corpus et des attendus du Lot A, sorties `v1-control/runs/qwen3.5_4b.jsonl`, résultats `v1-control/results/`) ; résultats `results/RESULTS_B0.md`, `results/summary_b0.json`, `results/scores_by_call_b0.csv`, `results/selfcheck_b0.json`, `results/decisions/`.

## Mise à jour 04/11/2026 (lot CSV-IMPORT-AI-01, Gate B)

- Mandat de refonte : [`prompts/MMV_V2_MANDAT_REFONTE_CSV_IMPORT_AI_01_GATE_B_2026-11-04.md`](../../../prompts/MMV_V2_MANDAT_REFONTE_CSV_IMPORT_AI_01_GATE_B_2026-11-04.md) ; dossier `docs/phase/05/CSV_IA_05_GATE_B.md` ; registre `docs/phase/05/CSV_IA_05.md` ; conception `docs/phase/05/CSV_IA_05_CONCEPTION.md` (notes datées).
- Code applicatif : **aucun changement** (`ImportView.tsx`, API, Application, Infrastructure, `appsettings.json`, migrations inchangés).
- Banc isolé (développement seulement, hors `vite build`) : `apps/desktop/import-wizard-bench.html` → `apps/desktop/src/bench/importWizard/main.tsx`, `ImportWizardBench.tsx` (maquette des cinq étapes et de l'historique), `fixtures.ts` (données fictives typées selon les contrats proposés), `importWizard.css` (jetons MMV). Servi par `apps/desktop/e2e/bench-04a/serve.mjs`, capturé par `apps/desktop/e2e/bench-04a/shoot.mjs`.
- Preuves : `docs/phase/05/evidence/csvai/gateb/` — `captures/` (38 JPEG, `plan.json`, `report.json`), `B0_FREEZE_SHA256_20261104.txt`.

## Mise à jour 06/11/2026 (lot CSV-IMPORT-AI-01, implémentation)

- Domaine : `src/MMV.Domain/CsvImport/ImportAssistant.cs` (original, état de l'assistant, tentatives, événements, décisions) ; `src/MMV.Domain/Audit/AuditEvent.cs` (actions et champs d'audit de l'assistant).
- Application : `src/MMV.Application/Catalog/Assisted/` — `TolerantCsvReader.cs`, `CatalogLexicon.cs`, `CatalogInterpretationContract.cs`, `CatalogInterpretationValidator.cs`, `AssistantUserLayer.cs`, `CatalogMaterializer.cs`, `AssistedAnalysis.cs`, `AssistedLensPlanner.cs`, `AssistedImportService.cs`, `InterpretationCache.cs` ; `src/MMV.Application/Catalog/ImportService.Assisted.cs` (+ `ImportService.cs` partiel, garde `pipeline_mismatch`) ; `LensOfferService.cs` (validation à blanc exposée).
- Infrastructure : `src/MMV.Infrastructure/Catalog/AssistantModel.cs`, `AssistantStores.cs` ; migration `src/MMV.Infrastructure/Persistence/Migrations/20261106154105_CsvImportAssistant.cs` (+ `.Designer.cs`, instantané).
- API : `src/MMV.Api/Installation/CatalogInterpreterService.cs` (interpréteur Ollama, file d'analyse), `src/MMV.Api/Catalog/AssistedImportEndpoints.cs` ; `StockEndpoints.cs` (routes additives, colonne d'historique) ; `appsettings.json` (section `CatalogInterpreter`, écran désactivé par défaut) ; `Program.cs`.
- Poste : `apps/desktop/src/catalog/importWizard/` (`assistantApi.ts`, `ImportWizard.tsx`, `ImportEntry.tsx`, `importWizard.css`, `importWizard.test.tsx`) ; `CatalogModules.tsx` (route « Import ») ; `src/api/client.ts` (`DELETE`) ; page de captures `apps/desktop/import-wizard-real.html` → `src/bench/importWizard/realMain.tsx` (développement seulement).
- SQL : `docs/phase/05/sql/0009_CsvImportAssistant.{sql,idempotent.sql,verify.sql,schema-inventory.txt}`, `0009_MANIFEST.sha256` ; dossier `docs/phase/05/SQL_0009_REVUE.md`.
- Tests : `tests/MMV.Api.Tests/Catalog/Assisted/` (+ `Fixtures/`, octets exacts, `.gitattributes`) ; `tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests.cs`, `Phase05AssistedRealOllamaTests.cs` ; harnais (`Phase05Harness.cs`, `SchemaInspection.cs`, `Phase05SchemaTests.cs`).
- Preuves : `docs/phase/05/evidence/csvai/implementation/` ; rapport `docs/phase/05/LIVRAISON_05_CSVAI.md`.

## Mise à jour 06/11/2026 (lot CSV-IMPORT-AI-01, corrections post-audit)

- Application : `src/MMV.Application/Catalog/Assisted/AssistedLensPlanner.cs` (identité d'offre, fusion sans effacement, complétude, devise), `CatalogMaterializer.cs` (`ConfirmedCurrency`, `LinesWithoutCurrency`, confirmation `default:currency`), `AssistedImportService.cs` (devise sans EUR par défaut, décision `file_default` « currency », garde « plan = contrôle », résultat d'application enregistré, rejeu) ; `src/MMV.Application/Catalog/LensOfferService.cs` (`ChangedFields`).
- Domaine : `src/MMV.Domain/CsvImport/ImportAssistant.cs` (`BatchAssistant.RecordApplied`).
- Poste : `apps/desktop/src/catalog/importWizard/` (`assistantApi.ts`, `ImportWizard.tsx`, `importWizard.css`, `importWizard.test.tsx`, `importWizard.contrast.test.ts`).
- Tests : `tests/MMV.Api.Tests/Catalog/Assisted/CsvAssistantPostAuditTests.cs` ; `tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests.cs`, `Phase05Harness.cs` (`MMV_PG05_KEEP_SYNTHETIC`).
- SQL : `docs/phase/05/sql/0009_restore_check.sql`, `0009_CsvImportAssistant.schema-inventory.applied.txt`, `0009_POST_AUDIT_MANIFEST.sha256` ; scripts 0009, inventaire de revue et manifeste 0009 inchangés ; `tests/MMV.PostgreSql.Tests/Phase05SchemaTests.cs` (inventaire appliqué).
- Preuves : `docs/phase/05/evidence/csvai/postaudit/` ; rapport `docs/phase/05/LIVRAISON_05_CSVAI_POST_AUDIT.md`.

## GO conditionnel G05-SQL-0009 et GATE D (07/11/2026)

- Application : `src/MMV.Application/Catalog/Assisted/AssistedImportService.cs` (`PlanFingerprintKey`, `LensFingerprint`, `ProductFingerprint`, `CheckedFingerprint`, verrous et relecture dans `ApplyAsync`), `AssistedLensPlanner.cs` (`Basis`, `LockExistingAsync`), `src/MMV.Application/Catalog/ImportService.Assisted.cs` (`BasisAsync`, `LockAssistedTargetsAsync`), `ImportService.cs` (champs non fournis conservés), `LensOfferService.cs` (`ILensOfferStore.LockAsync`).
- Infrastructure : `src/MMV.Infrastructure/Catalog/CorrectionStores.cs` (`LensOfferStore.LockAsync`).
- API : `src/MMV.Api/Catalog/AssistedImportEndpoints.cs` (`POST /{id}/assistant/analysis/cancel`).
- Poste : `apps/desktop/src/catalog/importWizard/` (`assistantApi.ts` GET / POST seulement, `ImportWizard.tsx` classe `iw-screen`, `importWizard.css` contrastes, `importWizard.contrast.test.ts`, `assistantApi.cors.test.ts`) ; recette native `apps/desktop/e2e/native-csvai.mjs`.
- Tests : `tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests.cs` (`Products_GateD_…`, `Lenses_GateD_…`) ; `tests/MMV.Api.Tests/DesktopCorsTests.cs` (`Preflight_AssistantAnalysisCancel_AllowsPost_NeverDelete`).
- Preuves : `docs/phase/05/evidence/csvai/gate_d/` (sauvegardes, application, verify, empreintes, banc, journal natif, contre-épreuves, suites, captures) ; mandat `prompts/MMV_V2_GO_CONDITIONNEL_G05_SQL_0009_GATE_D_2026-11-07.md` ; rapport `docs/phase/05/LIVRAISON_05_CSVAI_GATE_D.md`.

## Clôture contrôlée (07/11/2026)

- Mandat : `prompts/MMV_V2_CLOTURE_CONTROLEE_PHASE05_TRANSITION_PHASE06_2026-11-07.md` ; rapport consolidé `docs/phase/05/RAPPORT_CLOTURE_PHASE05.md`.
- Code relu (aucune modification) : `src/MMV.Api/Catalog/AssistedImportEndpoints.cs` (garde `WizardEnabled`, `Disabled()`), `src/MMV.Api/Catalog/StockEndpoints.cs` (groupe `/api/imports`), `src/MMV.Api/Installation/ActiveStore.cs` (`ActiveStoreFilter`, `InstallationStoreResolver`), `src/MMV.Api/Installation/CatalogInterpreterService.cs` (`TryEnqueue`), `src/MMV.Application/Catalog/Assisted/AssistedLensPlanner.cs` (identité, `LockExistingAsync`, `lens_offer_ambiguous_existing`), `src/MMV.Application/Catalog/ImportService.cs` (aucune création d'offre de verres), `src/MMV.Api/appsettings.json`.
- Test ajouté : `tests/MMV.Api.Tests/Catalog/Assisted/CsvAssistantDisabledGateTests.cs`.
- Preuves : `docs/phase/05/evidence/cloture/` (`CLOTURE_precontrole_20261107.txt`, `CLOTURE_suites_20261107.txt`, `CLOTURE_contre_epreuves_assistant_desactive_20261107.log`, `CLOTURE_post_consolidation_20261107.txt`).
- Arbre de comparaison : archive GATE D `be60f20f3dca760b11a28bd132aa68e71439f904` (23 fichiers de code, tests et SQL : blobs identiques).
