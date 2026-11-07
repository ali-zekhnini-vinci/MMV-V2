# Phase 05 — Livraison finale : migration 0007 appliquée, intégration réelle, correctifs

**Statut (30/10/2026) : `PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** Mandat : [`prompts/MMV_V2_G05_SQL_GO_INTEGRATION_FINALE_2026-10-30.md`](../../../prompts/MMV_V2_G05_SQL_GO_INTEGRATION_FINALE_2026-10-30.md) — **G05-SQL : GO conditionnel** pour l'application locale du seul script `docs/phase/05/sql/0007_CatalogStockModule.idempotent.sql` (SHA-256 `39c9c22400a3319d80724cd63110977ec39de912c3f60f3f2bc0bdbb426e1eb6`). Branche locale `feat/phase-05-catalogue-stock` ; commits locaux seulement, **aucun push, PR ni merge** ; Phase 06 non commencée. **La clôture et l'autorisation de PR restent soumises à l'audit final du propriétaire.** Toutes les données d'essai sont fictives et purgées (résidu 0).

Le rapport d'audit `MMV_V2_AUDIT_G05_SQL_0007_2026-10-30.md` annoncé comme joint **n'était pas présent** dans l'espace de travail (dépôt, Téléchargements, Documents, Bureau) : il n'a pas pu être lu (O-05-18). Le mandat lui-même (verdict, script, empreinte, conditions) a été appliqué tel quel.

Rapport de l'implémentation (historique, inchangé) : [`LIVRAISON_05.md`](LIVRAISON_05.md) ; dossier SQL revu (sous empreinte, inchangé) : [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md).

## 1. Précontrôles (tous conformes avant toute écriture)

Preuve : [`evidence/P05F_preflight_20261030.log`](evidence/P05F_preflight_20261030.log).

| Précontrôle | Résultat |
|---|---|
| Dépôt | HEAD `61742cc`, branche `feat/phase-05-catalogue-stock` ; seule modification suivie : `apps/desktop/.env.development` du propriétaire (SHA-256 `58d1eb06…c3481d`), **jamais modifiée ni indexée** |
| Empreintes | manifeste [`sql/0007_MANIFEST.sha256`](sql/0007_MANIFEST.sha256) **12 / 12 OK** dans le dépôt, dont les trois fichiers complets absents du ZIP d'audit (Designer, instantané du modèle, `CatalogStockModel.cs`) |
| Scripts ↔ migration EF Core | idempotent et direct **régénérés octet pour octet identiques** (`39c9c224…1eb6`, `908c708d…dd06`) ; `has-pending-model-changes` : aucun changement |
| Serveur | PostgreSQL **18.6** sur **5433** (relu côté serveur) ; PostgreSQL 17 (5432) **jamais contacté** |
| Bases et rôles | `mmv_v2_dev` / `mmv_v2_dev` et `mmv_v2_test` / `mmv_v2_test`, non superutilisateurs |
| Historique | 0001 à 0006 présentes, **0007 absente**, sur les deux bases |
| Dérive de schéma | `ContainsOnlyAKnownState` PASS ×2 ; `AllMigrationsApplied` en échec **attendu par construction** avant application |
| Processus du propriétaire | `MMV.Api` (7267, PID 41616, démarré le 29/10) **en marche, jamais arrêté ni redémarré** ; aucune session ouverte sur les deux bases ; verrou borné (`lock_timeout` 5 s) en garde |
| Sauvegardes (hors dépôt) | `C:\Users\Ali Zekhnini\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0007_20261030_151450.dump` (80 256 octets, SHA-256 `415A2592…BD4133`, `pg_restore --list` 139 entrées, 13 tables avec données, aucun objet Phase 05) ; `mmv_v2_test_avant_0007_20261030_151450.dump` (74 983 octets, SHA-256 `C5D827BD…9D7BBE7`, base vide prise par prudence). **Sauvegardes lisibles ; restauration complète NON éprouvée** (BL-04-05) |

## 2. Application (script approuvé seulement)

| Base | Horodatage | Résultat | Contrôles |
|---|---|---|---|
| `mmv_v2_dev` | 30/10/2026 15:15:41 (+01:00) | `PRECHECK OK` (18.6, 5433), empreinte imposée, `LOCK_TIMEOUT : 5s`, **`COMMIT`**, `APPLIQUE : exit 0`, 7 migrations dont `20261030005557_CatalogStockModule` — [`evidence/P05F_0007_apply_mmv_v2_dev_20261030T1415Z.log`](evidence/P05F_0007_apply_mmv_v2_dev_20261030T1415Z.log) | `verify.sql` **27 / 27 OK** ; 13 tables antérieures : **empreinte de contenu identique** (md5 ligne à ligne) ; objets, contraintes (hors contrainte des permissions recréée) et déclencheurs des schémas 0001–0006 identiques |
| `mmv_v2_test` (après dev conforme) | 30/10/2026 15:16:07 (+01:00) | idem — [`evidence/P05F_0007_apply_mmv_v2_test_20261030T1416Z.log`](evidence/P05F_0007_apply_mmv_v2_test_20261030T1416Z.log) | `verify.sql` **27 / 27 OK** ; mêmes contrôles |

Preuves : [`evidence/P05F_0007_verify_20261030.log`](evidence/P05F_0007_verify_20261030.log), [`evidence/P05F_fingerprints_20261030.log`](evidence/P05F_fingerprints_20261030.log) (avant / après / fin de session : `mmv_v2_dev` **inchangée** depuis l'application, l'API du propriétaire n'y a rien écrit pendant la session). Aucun script alternatif, aucune modification du SQL approuvé, aucun retour arrière. Aucun droit de la Phase 05 attribué à un compte réel ; aucun magasin créé sur `mmv_v2_dev`.

**Inventaire exact du schéma rejoué (O-05-13).** Sur la base réelle, 699 des 707 lignes de l'inventaire revu sont identiques (colonnes, contraintes, index, séquences, déclencheurs, propriétaires, droits, six familles, contrainte des permissions) ; **seules les 8 empreintes md5 des corps de fonction diffèrent**. Cause établie : l'inventaire revu avait été relevé sur le cluster jetable à partir d'une sortie EF Core encore en **CRLF** ; le script versionné et approuvé est en LF. Preuve calculée : chaque empreinte « revue » = md5 du corps `$fn$…$fn$` du script approuvé converti en CRLF ; chaque empreinte « appliquée » = md5 du même corps en LF. La base est donc **conforme au script approuvé** ; l'écart est dans l'instantané de test. Traitement **sans toucher au SQL ni au manifeste** : l'inventaire revu [`sql/0007_CatalogStockModule.schema-inventory.txt`](sql/0007_CatalogStockModule.schema-inventory.txt) reste tel quel (empreinte `b163e223…` intacte) ; l'inventaire relu sur `mmv_v2_test` est ajouté ([`sql/0007_CatalogStockModule.schema-inventory.applied.txt`](sql/0007_CatalogStockModule.schema-inventory.applied.txt), SHA-256 `5bf96490…3c3d63`) et sert désormais de référence au test ; un nouveau test sans connexion (`ReviewedInventory_DiffersOnlyByTheFunctionBodiesLineEndings`) vérifie que les deux fichiers ne diffèrent que par ces 8 lignes et que chaque empreinte dérive du script approuvé. Diff : [`evidence/P05F_schema-inventory_reviewed-vs-applied.diff`](evidence/P05F_schema-inventory_reviewed-vs-applied.diff). *À relire par l'auditeur (nouvel instantané hors manifeste).*

## 3. Résultats (arbre livré)

| Contrôle | Résultat |
|---|---|
| `dotnet build MMV.slnx` (avertissements = erreurs) | **PASS** — 0 avertissement, 0 erreur (rejoué après la dernière modification) |
| `MMV.Api.Tests` | **PASS 499 / 499** |
| `MMV.Maintenance.Tests` | **PASS 50 / 50** |
| `MMV.PostgreSql.Tests` complet (Phases 02 à 05) sur `mmv_v2_test`, **requêtes HTTP du banc Phase 05 servies par le processus MMV.Api réel** (Kestrel HTTPS 7367) | **PASS 82 / 82** — dont `AllMigrationsApplied` ×2, inventaire exact, 16 tests de course optique, 7 + 4 scénarios Phase 05, contrôles du processus réel ; **12 purges, résidu 0** — [`evidence/P05F_postgresql_20261030.log`](evidence/P05F_postgresql_20261030.log) |
| Même suite rejouée après la dernière modification (commentaires) | **PASS 81 / 82, 1 ignoré** (`Phase05RealHttpsTests`, processus réel arrêté) — [`evidence/P05F_dotnet_20261030.log`](evidence/P05F_dotnet_20261030.log) |
| HTTP en clair vers le processus réel | **PASS** — 8 lectures et 1 écriture Phase 05 refusées `403 https_required`, **rien écrit** ; connexion en HTTP refusée sans jeton ; même jeton accepté en HTTPS |
| `scripts/tests/Invoke-MmvSqlScript.Tests.ps1` | **PASS 16 / 16** |
| Frontend `npm run check` / `eslint .` / `vitest run` ×2 / `vite build` | **PASS** — **311 / 311** à chaque passage ; lint : 0 erreur, 1 avertissement préexistant (`react-hooks/incompatible-library`, tableau des clients) — [`evidence/P05F_frontend_20261030.log`](evidence/P05F_frontend_20261030.log) |
| Parcours Tauri natif (WebView2, exécutable de banc) | **PASS — 79 contrôles, 16 captures** — [`evidence/P05F_native_catalogue-05_20261030.log`](evidence/P05F_native_catalogue-05_20261030.log) |
| Journal de l'API de banc | 0 erreur, 0 exception ; aucun nom de compte, produit ni montant ; 1 avertissement EF Core (O-05-16) — [`evidence/P05F_api_bench_20261030.log`](evidence/P05F_api_bench_20261030.log) |

## 4. Matrice du mandat § 3 (mmv_v2_test, données fictives)

| Exigence | PostgreSQL réel + HTTPS réel | Tauri natif | Statut |
|---|---|---|---|
| Six familles, modèles et variantes | `SixFamilies_ModelsAndVariants_LensCatalog` (un modèle par famille, politiques admises, prestation `service` seulement, gamme et indice exigés pour un verre), SKU unique, GTIN, politique refusée | catalogue : 6 familles listées ; création d'un produit (Accessoires) relue par l'API | **PASS** |
| Deux magasins Belgique EUR / Maroc MAD | banc à deux magasins fictifs par test (devise immuable) | magasins « Magasin fictif Bruxelles — EUR » / « Casablanca — MAD » | **PASS** |
| Catalogue des verres, fournisseurs multiples | fabricant → gamme → indice, traitement, droit `catalog.configure` ; fournisseurs BE / MA (ICE contrôlé), principal unique, archivage du principal avec confirmation | référentiel verres et fournisseurs affichés | **PASS** |
| Prix HT × coefficient, arrondis, prix manuel, protection des coûts | 41,25 × 2,5 = 103,13 ; contrôle croisé `round(numeric,2)` ; aperçu périmé 409 ; MAD sans conversion ; coefficient < 1 confirmé ; prix manuel `pricing.manual` ; vendeur sans `costs.read` : aucune clé de coût (liste, fiche, historique) ; audit sans montant | aperçu et enregistrement 103,13 € puis 43,98 MAD (BE inchangé), prix manuel 99,90 € (coefficient conservé) ; compte vendeur : aucun prix d'achat, coefficient ni marge, ni à l'écran ni dans l'API | **PASS** (après correctif O-05-14) |
| Entrées, sorties, ajustements, réceptions partielles | réception 10 annoncés / 6 reçus puis complément ; idempotence ; ajustement à quantité attendue | entrée +2 relue ; réception partielle et « Recevoir le reliquat » | **PASS** |
| Stock négatif et stock réservé interdits | `stock_insufficient` sous verrou + contrainte ; deux sorties concurrentes (une seule) ; **`ReservedStock_CannotBeTakenOut_NorAdjustedBelow`** : sortie et ajustement sous la quantité réservée refusés `stock_reserved`, rien écrit ; réservé > en main refusé par la base | sortie supérieure au stock : « Stock insuffisant : le stock ne peut pas devenir négatif. », stock inchangé | **PASS** — réservation posée par SQL (aucune API de réservation avant la Phase 06, O-05-19) |
| Inventaires complets, partiels, concurrents | **`Inventories_Full_And_Partial_UncountedRules`** : complet sur deux emplacements (variantes stockées seules), partiel par famille, recouvrement refusé `inventory_open`, non comptés exclus ou mis à zéro avec motif exigé (`reason_required`), abandon sans effet ; sélection à l'aveugle, comptages concurrents (409), mouvement pendant le comptage, validation atomique | comptage d'une ligne d'un inventaire ouvert relu par l'API | **PASS** |
| CSV : prévisualisation, contrôle à blanc, import, rejet atomique, aucun doublon | fournisseurs, catalogue, prix ; formule refusée ; tout ou rien ; réémission ; fichier déjà appliqué refusé ; coûts sous `costs.write` ; devise différente refusée | dépôt par le sélecteur de fichier, aperçu et correspondance, contrôle à blanc (rien écrit), application (2 créés), réimport : 2 inchangés puis refus, **toujours 2** ; catalogue avec formule : « 1 erreur(s) », application impossible, rien importé | **PASS** |
| 16 permissions | **`SixteenPermissions_EachIsRequiredByItsRoute`** : pour chacun des 16 droits, sonde acceptée avec l'ensemble requis et **403 dès qu'il manque** (15 autres droits + `system.admin` + `audit.read`), aucune écriture ; `system.admin` seul ou aucun droit → 403 partout | vendeur : ni « Nouveau produit » ni entrée / sortie | **PASS** |
| Suites PostgreSQL Phases 02 à 05 | 82 / 82 (identité, auth, clients, rendez-vous, audit, optique, Phase 05) | — | **PASS** |
| HTTPS réel et Tauri natif | processus Kestrel réel ; HTTP en clair refusé | Tauri → HTTPS → MMV.Api → PostgreSQL | **PASS** |
| Thèmes clair / sombre, design Lot 6 | — | 12 captures clair 1920×1080, 3 sombres (1920×1080, 1366×768) ; rendu cohérent sur les captures relues (03, 10, 14, 15), aucun débordement constaté ; logo sur fond blanc en thème sombre (O-05-17, préexistant) | **PASS technique** ; conformité visuelle au Lot 6 : **revue du propriétaire** (G05-DESIGN OPEN, maquette non entièrement versionnée) |
| Purge et résidu | 12 bancs purgés (déclencheurs désactivés dans la seule transaction de purge), résidu 0 ; banc natif : 4 jeux purgés, **résidu `mmv_v2_test` = 0** (seules restent les 6 familles et l'historique EF) | — | **PASS** |

Captures (`evidence/P05F_native_p05_*.jpg`) : 01 catalogue six familles ; 02 nouveau produit ; 03 prix EUR ; 04 prix MAD ; 05 prix manuel ; 06 sortie refusée ; 07 réceptions ; 08 inventaire ; 09 import contrôle à blanc ; 10 import rejeté ; 11 fournisseurs ; 12 référentiel verres ; 13 catalogue sombre ; 14 fiche sombre ; 15 fiche sombre 1366×768 ; 16 vendeur sans coûts.

## 5. Correctifs

| ID | Défaut | Correctif | Preuve |
|---|---|---|---|
| **O-05-11** (mandat § 4) | `realAppIsolation.test.tsx` lisait la **copie de travail** de `apps/desktop/.env.development` : la modification locale du propriétaire le faisait échouer | le contrôle « aucune activation par défaut » lit les `.env*` **versionnés (index Git)** — repli sur le disque hors dépôt Git — et couvre les deux interrupteurs ; `src/test/setup.ts` neutralise `VITE_MMV_OPTICAL_MODULE` et `VITE_MMV_CATALOG_MODULE` avant chaque test (un test qui active un module le fait explicitement). `.env.development` **non touché** | 6 / 6 avec le fichier du propriétaire ; témoin `.env.test.local` temporaire activant les deux modules : 59 / 59 ; sans la neutralisation : 4 échecs (nécessité prouvée) ; suite complète 311 / 311 ×2, **aucun échec de délai** sur quatre passages (Vitest seul) |
| **O-05-14** (trouvé par le parcours natif) | **l'aperçu de prix de l'interface n'aboutissait jamais sur l'API réelle** : le poste ajoutait un `requestId` à `POST /api/pricing/preview`, que `PreviewRequest` ne déclare pas ; les membres inconnus étant refusés, réponse `400 invalid_body`. Non détecté par les tests (aucun double strict de cette route) | aperçu envoyé par `previewCatalog` (sans `requestId`, aucune invalidation) — `apps/desktop/src/api/catalog.ts`, `useCatalogPreview` (`catalogData.ts`), `ModelViews.tsx` ; **API et SQL inchangés** | `apps/desktop/src/api/catalog.test.ts` (double `node:http` refusant tout membre inconnu, comme le serveur) ; parcours natif : aperçus 103,13 € / 43,98 MAD et enregistrements relus par l'API. Seule route concernée (contrôle de tous les corps de requête de l'API Catalogue) |

## 6. Ajouts aux tests (aucun changement de comportement de l'API)

- `tests/MMV.PostgreSql.Tests/Phase05Harness.cs` : variable `MMV_PG05_API_BASE` (refusée hors `https://localhost:<port>` et sur 7267) pour faire passer les requêtes du banc par un processus MMV.Api réel.
- `Phase05RealHttpsTests.cs` (processus réel, HTTP en clair), `Phase05FinalIntegrationTests.cs` (familles, verres, réservé, inventaires, 16 droits), `Phase05SchemaTests.cs` (inventaire appliqué + cohérence avec l'inventaire revu).
- `apps/desktop/e2e/run-native.mjs` : scénario `catalogue-05`. Outil de banc jetable (comptes, magasins, données fictives, purge, résidu) versionné en preuve : [`evidence/P05F_harness_p05native.cs.txt`](evidence/P05F_harness_p05native.cs.txt).

## 7. Réserves maintenues (aucune déclarée résolue, aucun schéma modifié)

| ID | Réserve | État |
|---|---|---|
| R-F-01 | **Rattachement des emplacements aux magasins dans les contraintes PostgreSQL** : `stock.receipts` porte `store_id` et `location_id` avec deux FK distinctes ; `stock.inventory_sessions.location_ids` est un tableau sans FK ; l'appartenance de l'emplacement au magasin est garantie par l'application seulement (et l'emplacement ne change jamais de magasin, déclencheur) | **OPEN** — toute contrainte supplémentaire exige une nouvelle revue SQL (BL-05-11) |
| R-F-02 | **Conservation des contenus CSV** (`imports.batches.content`) et politique de purge | **OPEN** — G05-LEGAL (BL-05-12) |
| R-F-03 | **Rôle applicatif SQL à privilèges limités** (le rôle est propriétaire des tables ; déclencheurs « ajout seul » contournables par lui) | **OPEN** — R-05-17, BL-04-04 |
| R-F-04 | **Restauration complète d'une sauvegarde** | **OPEN** — BL-04-05 ; sauvegardes du 30/10 lisibles seulement |
| R-F-05 | **Fiscalité EUR / MAD, G05-LEGAL, validations réglementaires** | **OPEN** — ADR-010, G05-FISCAL, G05-LEGAL ; TTC indisponible ; aucune vente ni donnée réelle |
| R-F-06 | **Choix techniques non ratifiés du registre D-05** | **OPEN** — [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md) § 3, D-05-07 / 09 / 13, QR-01 à QR-13 |
| R-F-07 | **Exports CSV différés** | **OPEN** — BL-05-09 |
| R-F-08 | **Références Lot 6 partiellement versionnées** | **OPEN** — O-05-08, O-05-10 ; conformité visuelle à confirmer par le propriétaire (G05-DESIGN) |

Observations nouvelles : O-05-13 (inventaire CRLF, § 2), O-05-14 (aperçu, § 5), O-05-15 à O-05-19 — [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md).

## 8. Bancs (reproductibles, hors dépôt sauf les outils versionnés)

- API de banc : build du dépôt (`--artifacts-path bin/g05`) **copié hors dépôt**, `https://localhost:7367` / `http://localhost:5367`, environnement `Development`, `ConnectionStrings__Mmv` = `mmv_v2_test` passée au seul processus enfant, `Auth__LoginAttemptsPerMinute=10000` (banc seulement ; défaut 10 inchangé).
- Tests réels : `MMV_PG05_API_BASE=https://localhost:7367/ MMV_PG05_API_PLAIN=http://localhost:5367/ dotnet test tests/MMV.PostgreSql.Tests --artifacts-path bin/g05 -p:NuGetAudit=false`.
- Natif : `npx tauri build --debug --no-bundle --config <banc>/tauri.p05test.json` (identifiant `com.mmv.v2.p05test`, CSP `connect-src` 7367) avec `VITE_MMV_API_BASE_URL=https://localhost:7367` et `VITE_MMV_CATALOG_MODULE=enabled` **au build de banc seulement**, `CARGO_TARGET_DIR` hors dépôt ; exécutable renommé `mmv-p05test.exe`, `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9223` ; `MMV_E2E_CREDENTIALS_FILE=<banc>/creds.json node --use-system-ca e2e/run-native.mjs catalogue-05 --port 9223 --api https://localhost:7367 --process mmv-p05test --page-url http://tauri.localhost --prefix P05F_native`. L'exécutable du passage retenu inclut le correctif O-05-14 (seuls des commentaires ont changé ensuite).
- Processus de banc (API 7367, fenêtre Tauri) arrêtés en fin de session ; **processus du propriétaire jamais touchés** (MMV.Api 7267 toujours en marche).

## 9. Étape suivante

**STOP — audit final du propriétaire.** Statut : `PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`. Aucun push, aucune PR, aucun merge ; Phase 06 non commencée. G05-FISCAL, G05-LEGAL et G05-DESIGN restent **OPEN**.
