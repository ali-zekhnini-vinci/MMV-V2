# 04 — QA_MATRIX

Statuts : `PASS` (exécuté, preuve citée) ; `NOT_TESTED` (prévu, rien d'exécuté) ; `NOT_RERUN` (non rejoué depuis le dernier changement). Aucun test applicatif n'a été exécuté en 04-00 : seul le code de documentation a changé.

## 04-00 — contrôles exécutés le 19/10/2026

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-04-001 | `origin/main` à jour | `git fetch origin` : `cb79d1f..6924860 main` | PASS — `origin/main` = `6924860ee0fb77678cdd959a1de9b39735785fcc` (merge PR #4) |
| Q-04-002 | `93df475` inclus | `git merge-base --is-ancestor 93df475 origin/main` | PASS (et `abd0352` inclus) |
| Q-04-003 | merge sans écart | `git diff --stat abd0352 6924860` | PASS — vide (arbre identique à la tête de la branche Phase 03) |
| Q-04-004 | arbre de travail propre | `git status --short` | PASS — aucune modification suivie ; non suivis du propriétaire présents et intacts (zips de phase, `.claude*`, `.serena/`, `apps/desktop/desktop.zip`, `apps/desktop/desktop.recovered.zip`, `docs/phase/04/` pack, `references/`) |
| Q-04-005 | branche dédiée | `git fetch origin main:main` (avance rapide `c0c3d6b..6924860`) puis `git switch -c feat/phase-04-00-cadrage main` | PASS |
| Q-04-006 | maquette Lot 3 authentique | `sha256sum` sur `Downloads/MMV-Lot3-Ordonnances.dc.html`, sa copie `MMV initialisation projet V1/`, l'entrée du dossier maître | PASS — `c103df9e…c06c213` (attendu), 93 625 octets = registre |
| Q-04-007 | catalogue Lot 3 | `MMV_V2_Registre_324_Etats.csv` : 36 lignes `MMV-L03-*` ; `MMV_V2_Registre_134_Groupes.csv` : `B-033`…`B-049` | PASS — 36 états, 17 groupes ; `data-mount` de la maquette : 36 + `notes` |
| Q-04-008 | couverture | [`STATE_MAP_36.md`](STATE_MAP_36.md) | PASS — 36 / 36 cartographiés ; 12 lacunes identifiées (GAP-04-01 à 12) |
| Q-04-009 | codes d'audit proposés compatibles | lecture de `ck_events_action_format`, `ck_events_module_format`, `ck_events_entity_type_format` (`docs/phase/03/sql/0005_AuditJournal.sql`) | PASS — tous les codes de [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) § 4 respectent les formats |
| Q-04-010 | constat journaux techniques | recherche `UseHttpLogging`, `EnableSensitiveDataLogging`, journalisation de corps dans `src/` ; `appsettings.json` | PASS — aucun ; EF Core `Warning` |
| Q-04-011 | aucune modification de code, SQL ou base | `git diff --stat main` limité à `docs/`, `prompts/`, `README.md` | PASS (à la livraison) |
| Q-04-012 | liens internes des documents 04 | vérification de chaque chemin relatif cité | PASS (à la livraison, voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |
| Q-04-013 | aucune connexion PostgreSQL ni ancien dépôt | aucune commande `psql`, `dotnet test`, ni lecture du dépôt `mmv-desktop` | PASS |

Suites existantes (.NET, PostgreSQL réel, frontend, natif) : **`NOT_RERUN`** en 04-00 — aucun code modifié depuis `6924860` (dernière exécution : préparation du merge Phase 03, [`../03/QA_MATRIX.md`](../03/QA_MATRIX.md) Q-03-259 à Q-03-266).

## Tests prévus (tous `NOT_TESTED`)

### Accès et permissions (04-B / 04-C)

| ID | Cas | Attendu |
|---|---|---|
| Q-04-100 | anonyme sur chaque route `/api/optical/*` | 401 |
| Q-04-101 | `system.admin` seul ; `clients.*` seul ; `appointments.*` seul ; `audit.read` seul | 403 partout |
| Q-04-102 | `optical.read` sans `clients.read` | 403 |
| Q-04-103 | lecture avec `clients.read` + `optical.read` | 200 ; aucune écriture possible (403) |
| Q-04-104 | écriture RX avec `optical.measurements.write` seul, et inversement | 403 (D-04-01) |
| Q-04-105 | pièce sans `optical.documents.read` | 403 ; métadonnées sans nom de fichier |
| Q-04-106 | identifiant inexistant vs existant sans droit | réponses indiscernables |
| Q-04-107 | historique : `audit.read` sans `optical.read` | aucun événement `optical` servi |
| Q-04-108 | HTTP non chiffré | `https_required` |

### Cycle de vie, intégrité, concurrence

| ID | Cas | Attendu |
|---|---|---|
| Q-04-110 | brouillon incomplet | enregistré `draft` ; non proposable à la vente |
| Q-04-111 | confirmation sans le critère minimal | 422 `incomplete` |
| Q-04-112 | rectification d'un confirmé | révision n+1 ; révision n inchangée octet pour octet |
| Q-04-113 | deux postes, même version | un 200, un 409 ; un seul événement ; aucune perte |
| Q-04-114 | client archivé | création refusée (409) ; consultation permise |
| Q-04-115 | invalidation | conservée, exclue par défaut, visible avec filtre ; aucune route `DELETE` |
| Q-04-116 | valeurs | aucune normalisation ; signe et décimales préservés ; absence ≠ 0 |
| Q-04-117 | règles D-04-06 | une ligne de test par règle **ratifiée** (aucune avant ratification) |
| Q-04-118 | pièce : format par signature, taille, type déclaré trompeur | 415 / 413 ; aucun stockage partiel |
| Q-04-119 | pièce conservée lors d'une nouvelle révision | rattachement intact |

### Audit et confidentialité

| ID | Cas | Attendu |
|---|---|---|
| Q-04-130 | chaque écriture réussie | un événement dans la même transaction ; aucune valeur optique, aucun nom de prescripteur, aucune note dans `changes` (recherche de toutes les valeurs fictives saisies dans le JSON) |
| Q-04-131 | échec (validation, conflit, contrainte, panne) | ni écriture métier ni événement |
| Q-04-132 | `document.viewed` | écrit avant l'envoi ; échec d'audit ⇒ pièce non servie |
| Q-04-140 | journaux techniques pendant un parcours complet | aucune valeur saisie, aucun contenu de pièce, aucun jeton |
| Q-04-141 | réponses optiques | `Cache-Control: no-store` ; pièces `nosniff` |
| Q-04-142 | déconnexion | cache du poste vidé ; aucune donnée optique en stockage local |

### Interface et natif (04-A / 04-C)

| ID | Cas | Attendu |
|---|---|---|
| Q-04-150 | 36 états + 12 états GAP | conformes à [`STATE_MAP_36.md`](STATE_MAP_36.md) (double de test) |
| Q-04-151 | type toujours visible | badge sur ligne, détail, formulaire, historique |
| Q-04-152 | J-04-01 sans seconde recherche | retour sur l'onglet « Dossier optique » |
| Q-04-153 | clavier et focus | ordre de Tab, erreurs sous champ, Échap, aucune souris |
| Q-04-154 | fenêtre réduite | OD sur OG, aucun défilement horizontal |
| Q-04-155 | natif 2 thèmes × 1440×900 / 1920×1080 × 100 / 125 / 150 % | captures après le dernier changement, sinon `NOT_TESTED` |
| Q-04-156 | « Préparer une vente » | désactivée, aucun effet |
| Q-04-160 | non-régression Clients, Rendez-vous, Historique | suites existantes PASS |

## Complément 04-00 — contrôles exécutés le 19/10/2026 (suite de la revue)

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-04-014 | aucun code, SQL ni base touchés | `git status --short` / `git diff --stat` : seuls `docs/`, `README.md` et `prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md` modifiés ; aucune commande `psql`, `dotnet`, `npm` | PASS |
| Q-04-015 | fichiers non suivis du propriétaire intacts | `git status --short` : mêmes non suivis qu'au départ (zips, `.claude*`, `.serena/`, `apps/desktop/desktop*.zip`, pack `docs/phase/04/`, `references/`) ; aucun écrit | PASS |
| Q-04-016 | liens internes des documents 04 et des documents modifiés | résolution de chaque chemin relatif Markdown | PASS (voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |
| Q-04-017 | statuts cohérents | D-04-01 APPROVED ; D-04-02 à D-04-14 PROPOSED ; aucun état GAP déclaré conçu ; 04-A non commencé dans tous les documents | PASS |
| Q-04-018 | composants cités par la préparation 04-A existants | lecture de `apps/desktop/src/` (`components/ui/*`, `clients/ClientStates.tsx`, `ClientNoticeBanner.tsx`, `ClientsTable.tsx`, `views/ForbiddenView.tsx`, `appointments/ClientPicker.tsx`, `appointmentLaunch.ts`, `shell/navigationGuard.ts`) | PASS |

Suites existantes : **`NOT_RERUN`** (aucun code modifié depuis `6924860`).

### Tests prévus ajoutés (tous `NOT_TESTED`, sous réserve de D-04-10 à D-04-14)

| ID | Cas | Attendu |
|---|---|---|
| Q-04-121 | pièce liée à la révision 1, verso ajouté au brouillon puis confirmé | révision 1 cite le recto seul, révision 2 recto + verso ; contenu stocké une fois |
| Q-04-122 | rectification ouverte | détail et listes servent la dernière révision confirmée ; bandeau « rectification en cours » |
| Q-04-123 | deuxième rectification simultanée | `409 draft_exists`, aucune écriture, aucun événement |
| Q-04-124 | confirmation d'un brouillon dont la base n'est plus la dernière confirmée | `409 revision_conflict` |
| Q-04-125 | abandon d'une rectification | `discarded` conservé, numéro non consommé, révision n inchangée |
| Q-04-126 | invalidation avec brouillon ouvert | brouillon abandonné dans la même transaction ; deux événements ou aucun |
| Q-04-127 | client archivé avec brouillon ouvert | modification et confirmation refusées, abandon permis |
| Q-04-133 | `document.content_served` | validé avant le premier octet ; aucun événement pour la liste des pièces, un refus 403 ou un préchargement ; libellé « transmis » |
| Q-04-143 | fichiers : signature ≠ extension, SVG / HTML renommés, PDF à marqueurs actifs, image hors dimensions, 11ᵉ pièce, doublon d'empreinte | 415 / 415 / selon D-04-14.c / 415 / `409 too_many_documents` / `409 duplicate_document` ; aucun stockage partiel, aucun fichier temporaire |
| Q-04-144 | `Content-Type` servi | type détecté par le serveur, `nosniff`, `no-store` |
| Q-04-145 | sauvegarde / restauration avec pièces synthétiques | restauration complète, empreintes identiques, durées et tailles consignées (04-C, O-04-19) |
| Q-04-157 | 04-A : aucun changement visible sans `optical.read` | onglet reporté et entrée `later` inchangés (O-04-16) |
| Q-04-158 | 04-A : grille neutre | valeur saisie restituée à l'identique ; aucune borne, aucun pas, aucun champ désactivé, aucune normalisation |

## 04-A — contrôles exécutés le 19/10/2026 (GO G04-A — UI ONLY)

Prototype sur banc isolé ([`LIVRAISON_04A.md`](LIVRAISON_04A.md)). Toutes les preuves ci-dessous portent sur le **banc** et le **double en mémoire** : aucune n'est une preuve serveur, PostgreSQL ou native.

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-04-019 | préflight Git | `git log`, `git merge-base --is-ancestor origin/main HEAD`, `git status --short` | PASS — HEAD `1d18b81`, `origin/main` `6924860` ancêtre, aucune modification suivie, non suivis du propriétaire intacts |
| Q-04-020 | référence avant code | `npm run check`, `eslint .`, `vitest run` | PASS — 226 / 226, lint 0 erreur (1 avertissement connu O-03-02) |
| Q-04-021 | typage | `npm run check` (3 projets TypeScript) | PASS ([`evidence/P04A_frontend_20261019.log`](evidence/P04A_frontend_20261019.log)) |
| Q-04-022 | lint | `eslint .` | PASS — 0 erreur, 1 avertissement connu |
| Q-04-023 | tests frontend | `vitest run` | **PASS au rejeu 265 / 265** ([`evidence/P04A_frontend_rejeu_20261019.log`](evidence/P04A_frontend_rejeu_20261019.log)) ; exécution précédente 264 / 265 — échec intermittent d'un test Phase 03 non modifié (O-04-26), PASS seul 3 × puis en suite complète |
| Q-04-024 | build | `vite build --outDir <hors dépôt>` | PASS (avertissement connu de taille de bloc) |
| Q-04-025 | banc absent du bundle | recherche de `BANC DE D`, `optical-bench`, `RX-FICTIF`, `MES-FICTIF`, `Fictif-`, `InMemoryOpticalGateway`, `banc-04a`, `OpticalModule`, `/optique` dans la sortie | PASS — aucune occurrence ; libellé « Dossier optique » seul présent (branche facultative inutilisée, O-04-24) ([`evidence/P04A_bundle_20261019.log`](evidence/P04A_bundle_20261019.log)) |
| Q-04-026 | double optique (cycle, pièces, droits, valeurs) | `src/bench/inMemoryOpticalGateway.test.ts` | PASS — 14 tests |
| Q-04-027 | parcours sur le banc | `src/bench/OpticalBench.test.tsx` (jsdom, garde réseau active, `blocked = []` vérifié à chaque test) | PASS — 16 tests |
| Q-04-028 | application réelle inchangée | `src/optical/realAppIsolation.test.tsx` (imports, `index.html`, `vite.config.ts`, navigation, parcours réel contre le double `node:http`) | PASS — 4 tests |
| Q-04-029 | outils d'écran | `src/optical/opticalUi.test.ts` | PASS — 5 tests |
| Q-04-030 | captures navigateur | `e2e/bench-04a/plan.mjs` + `shoot.mjs` (Edge sans interface, CDP, clics réels du moteur) après le dernier changement de code | PASS — 54 captures, 0 erreur console, 0 défilement horizontal de page ([`evidence/P04A_captures_rapport_20261019.json`](evidence/P04A_captures_rapport_20261019.json)) |
| Q-04-031 | aucun SQL, migration, base, route ou permission optique | `git diff --stat` : seuls `apps/desktop/` (prototype, banc, tests, `ClientDetailView.tsx`), `docs/`, `prompts/`, `README.md` ; aucune commande `psql` / `dotnet` | PASS |
| Q-04-032 | tests .NET et PostgreSQL réel | — | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié depuis `6924860`) |

### Statut des tests prévus de l'interface (04-A)

| ID | Statut au 19/10/2026 | Preuve |
|---|---|---|
| Q-04-150 | PASS sur le banc pour les états réalisés / adaptés ; 4 états EN ATTENTE (16, 17, 21, 36) — validation visuelle **requise** | [`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md), captures |
| Q-04-151 | PASS (badge sur ligne, détail, formulaire ; journal du banc) — historique réel : 04-B | J-04-01, J-04-02 |
| Q-04-152 | PASS (banc) | J-04-01 : onglet rouvert, aucune recherche |
| Q-04-153 | PASS partiel (banc, jsdom) : ordre OD → OG, erreurs sous champ, Échap, focus du choix du type, focus du premier champ en erreur ; ordre exact après D-04-03 / D-04-06 | parcours ; capture `43-etat34-focus-visible` |
| Q-04-154 | PASS (navigateur) : OD sur OG à 1024 px, aucun défilement horizontal de page | captures `50`–`52` |
| Q-04-155 | **NOT_TESTED** (natif) ; navigateur : 2 thèmes, 1440×900 / 1920×1080 / 1024×700, zooms 125 / 150 % simulés | captures |
| Q-04-156 | PASS (banc) | J-04-04 |
| Q-04-157 | PASS — application réelle inchangée, prototypes dans le banc seulement | Q-04-028 |
| Q-04-158 | PASS (banc) | grille neutre ; double : valeurs |
| Q-04-160 | PASS (au rejeu) | Q-04-023 |
| Q-04-100 à Q-04-145 | **NOT_TESTED** (serveur, PostgreSQL, journal, fichiers réels : 04-B / 04-C) ; le double en illustre les refus attendus sans les prouver | — |

## 04-A — refonte UX Lot 3 (D-04-15) — contrôles exécutés le 20/10/2026

Mandat : [`prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md). Les résultats du 19/10/2026 ci-dessus sont conservés tels quels ; les lignes suivantes les complètent après le dernier changement de code.

| ID | Contrôle | Commande / méthode | Statut |
|---|---|---|---|
| Q-04-033 | préflight Git | `git log`, `git status --short` | PASS — HEAD `26e515f`, aucune modification suivie, non suivis du propriétaire intacts |
| Q-04-034 | maquette Lot 3 authentique | `sha256sum references/phase04/lot3/MMV-Lot3-Ordonnances.dc.html` ; même empreinte pour la copie du dossier de conception servie en lecture seule | PASS — `c103df9e…c06c213`, 93 625 octets |
| Q-04-035 | typage strict | `npm run check` | PASS |
| Q-04-036 | lint | `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02) |
| Q-04-037 | tests frontend | `vitest run` | **PASS 279 / 279** (25 fichiers) — [`evidence/P04A_UX_frontend_20261020.log`](evidence/P04A_UX_frontend_20261020.log) |
| Q-04-038 | build | `npm run build` (sortie `apps/desktop/dist`, ignorée par Git) | PASS (avertissement connu de taille de bloc) |
| Q-04-039 | banc et composants ± absents du bundle | recherche de `BANC DE D`, `optical-bench`, `RX-FICTIF`, `MES-FICTIF`, `Fictif-`, `InMemoryOpticalGateway`, `banc-04a`, `OpticalModule`, `/optique`, `optical-stepper`, `OpticalStepContext`, `Diminuer `, `pas de démonstration` | PASS — aucune occurrence ; « Dossier optique » seul présent (O-04-24) — [`evidence/P04A_UX_bundle_20261020.log`](evidence/P04A_UX_bundle_20261020.log) |
| Q-04-040 | application réelle inchangée | `src/optical/realAppIsolation.test.tsx` | PASS — 4 tests (inclus dans Q-04-037) |
| Q-04-041 | test intermittent « erreurs techniques bornées » (O-04-27) | 4 exécutions isolées sur `26e515f` (2 échecs), puis 4 après correction du délai d'attente | PASS — 4 / 4 après correction |
| Q-04-042 | aucun SQL, migration, base, route ou permission optique | `git diff --stat` : `apps/desktop/src/optical/`, `apps/desktop/src/bench/`, `apps/desktop/e2e/bench-04a/`, `docs/`, `prompts/`, `README.md` seulement ; aucune commande `psql` / `dotnet` | PASS |
| Q-04-043 | tests .NET et PostgreSQL réel | — | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié) |
| Q-04-044 | interface Tauri native | — | **NOT_TESTED** (banc hors application, O-04-16) |

### Tests de la refonte (banc, jsdom, garde réseau active)

| ID | Exigence du mandat (§ 8) | Test | Statut |
|---|---|---|---|
| Q-04-170 | 1, 5, 9 — chaque champ ± de OD et OG (− à gauche, + à droite), mise à jour immédiate, annonce, yeux indépendants ; Axe / Base / Acuité sans ± | `OpticalBench.test.tsx` « chaque champ ± de OD et OG… » | PASS |
| Q-04-171 | 2, 3, 4 — valeurs positives, négatives, nulles, absentes ; alternance clavier / clics ; valeur hors pas conservée, aucun arrondi ; aucune transposition ; axe non normalisé | « valeurs positives, négatives, nulles et absentes… » | PASS |
| Q-04-172 | valeur non numérique jamais modifiée, boutons inactifs, aide | « valeur non numérique… » | PASS |
| Q-04-173 | 7 — réinitialisation explicite d'un champ | « réinitialisation explicite d'un champ… » | PASS |
| Q-04-174 | 8 — Tab / Maj+Tab sur les seuls champs, ↑ / ↓, Alt+↓ (liste de bases), saisie libre de la base | « clavier : Tab / Maj+Tab… » | PASS |
| Q-04-175 | 6 — conservation : garde de sortie après un clic, valeurs enregistrées telles qu'affichées | « conservation : un clic ± rend le formulaire modifié… » | PASS |
| Q-04-176 | pas paramétrable ; mesure magasin : mêmes composants, provenance propre, jamais une prescription | « pas de démonstration paramétrable… » | PASS |
| Q-04-177 | calcul ± (vide, signes, hors pas, séparateur, pas paramétrable, non numérique) | `src/optical/opticalStep.test.ts` (7) | PASS |
| Q-04-178 | captures 2 types × 2 thèmes × 1920×1080 / 1024×700 × 100 / 125 / 150 % + états ± ; comparaison avec la maquette | `e2e/bench-04a/plan-lot3.mjs` + `shoot.mjs` (Edge sans interface) ; maquette rendue en lecture seule | PASS (navigateur) — 34 captures, 0 erreur console, 0 défilement horizontal ; planches `P04A_UX_compare_*`, `P04A_UX_avant_apres_*` — [`evidence/P04A_UX_captures_rapport_20261020.json`](evidence/P04A_UX_captures_rapport_20261020.json) ; natif NOT_TESTED |
| Q-04-179 | 10 — absence de régression des autres écrans optiques | suites `OpticalBench.test.tsx` (16 parcours d'origine) et `inMemoryOpticalGateway.test.ts` ; plan de captures d'origine `plan.mjs` rejoué | PASS — 54 captures, 0 erreur, 0 défilement horizontal — [`evidence/P04A_UX_regression_captures_rapport_20261020.json`](evidence/P04A_UX_regression_captures_rapport_20261020.json) |

### Statut des tests prévus de l'interface — mise à jour du 20/10/2026

| ID | Statut au 20/10/2026 | Preuve |
|---|---|---|
| Q-04-153 | PASS partiel (banc, jsdom) : Tab / Maj+Tab sur les champs de la grille, ↑ / ↓, Alt+↓, focus conservé lors d'un clic ± ; ordre exact après D-04-03 / D-04-06 | Q-04-174 ; capture `P04A_UX_navigateur_apres-etat34-focus-stepper` |
| Q-04-154 | PASS (navigateur) : OD sur OG selon la largeur du formulaire (< 940 px : 1024 px × 125 / 150 %), aucun défilement horizontal | Q-04-178 |
| Q-04-155 | **NOT_TESTED** (natif) ; navigateur : 2 thèmes × 1920×1080 / 1024×700 × 100 / 125 / 150 % | Q-04-178 |
| Q-04-158 | PASS (banc), **amendé par D-04-15** : saisie au clavier restituée à l'identique, aucune borne, aucun champ désactivé, aucune normalisation ; un pas de **démonstration** paramétrable n'existe que pour les boutons ± et ↑ / ↓, sans arrondi ni alignement | Q-04-171, Q-04-177 |
| Q-04-160 | PASS | Q-04-037 |

## 04-A — simplification (D-04-16) — contrôles exécutés le 20/10/2026

Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). Preuves sur le **banc** et le **double en mémoire** seulement. Statut ajouté : `REPORTÉ (BL-04-01)` — test prévu sorti du périmètre de la Phase 04 avec la gestion documentaire, à reprendre avec le backlog permanent.

| ID | Contrôle | Commande / méthode | Statut |
|---|---|---|---|
| Q-04-045 | préflight Git | `git log`, `git status --short` | PASS — HEAD `9be2fda`, aucune modification suivie, non suivis du propriétaire intacts |
| Q-04-046 | typage strict | `npm run check` | PASS |
| Q-04-047 | lint | `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02) |
| Q-04-048 | tests frontend | `vitest run` | **PASS 279 / 279** (25 fichiers) — [`evidence/P04A_SIMPL_frontend_20261020.log`](evidence/P04A_SIMPL_frontend_20261020.log) |
| Q-04-049 | build | `vite build` (sortie `apps/desktop/dist`, ignorée par Git) | PASS (avertissement connu de taille de bloc) |
| Q-04-050 | banc, pièces et état de l'œil absents du bundle | recherche des chaînes de Q-04-039 + `Pièces source`, `Joindre une pièce`, `attachDocument`, `fetchDocumentContent`, `optical.documents`, `État de l`, `not_transcribed` | PASS — aucune occurrence ; « Dossier optique » seul présent (O-04-24) — [`evidence/P04A_SIMPL_bundle_20261020.log`](evidence/P04A_SIMPL_bundle_20261020.log) |
| Q-04-051 | application réelle inchangée | `src/optical/realAppIsolation.test.tsx` | PASS — 4 tests (inclus dans Q-04-048) |
| Q-04-052 | aucun SQL, migration `0006`, backend, route ou permission optique ; aucune table ni API de pièces | `git diff --stat` : `apps/desktop/src/optical/`, `apps/desktop/src/bench/`, `apps/desktop/e2e/bench-04a/plan.mjs`, `docs/`, `prompts/`, `README.md` seulement ; aucune commande `psql` / `dotnet` | PASS |
| Q-04-053 | tests .NET et PostgreSQL réel | — | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié) |
| Q-04-054 | interface Tauri native | — | **NOT_TESTED** (banc hors application, O-04-16) |
| Q-04-055 | liens internes et statuts cohérents des documents modifiés | résolution de chaque chemin relatif Markdown ; recherche des statuts courants | PASS (voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |

### Tests de la simplification (banc, jsdom, garde réseau active)

| ID | Exigence du mandat | Test | Statut |
|---|---|---|---|
| Q-04-180 | 1, 2, 4 — formulaire sans « État de l'œil » ni pièces ; chaque champ peut rester vide (« — », jamais 0) ; prescripteur et date de prescription conservés | `OpticalBench.test.tsx` « formulaire : aucun état de l'œil ni pièce… » | PASS |
| Q-04-181 | 2, 3 — détail sans pièce source ; révisions, motif, auteurs et comparaison des valeurs conservés | « détail : aucune pièce source ; révisions, motif… » | PASS |
| Q-04-182 | 1, 5 — ordre de Tab : Acuité OD → Sphère OG (aucun état intercalé) ; boutons ± inchangés | « ordre de Tab : OD puis directement OG… » ; « clavier : Tab / Maj+Tab… » | PASS |
| Q-04-183 | 1, 8 — `422 incomplete` sans critère sur les yeux : OG vide accepté, seule la provenance (démonstration) est exigée | « confirmation refusée par le service (422 incomplete, provenance)… » ; `inMemoryOpticalGateway.test.ts` « brouillon → 422 incomplete… OG laissé vide » | PASS |
| Q-04-184 | 1, 6 — double : contenu = provenance + OD / OG + notes, aucun champ « état », aucune opération ni donnée de pièce | `inMemoryOpticalGateway.test.ts` « ni état d'œil ni pièce source… » | PASS |
| Q-04-185 | 3 — révisions : base, motif, commentaire, auteurs de saisie et de confirmation ; comparaison limitée aux valeurs changées | « révisions conservées : comparaison des valeurs… » | PASS |
| Q-04-186 | 6 — aucun droit simulé sur des pièces ; écriture par type conservée | « droit d'écriture par TYPE ; aucun droit sur des pièces sources » | PASS |
| Q-04-187 | 1 — résumé OD / OG : œil vide « non renseigné », `0` restitué `0` | `opticalUi.test.ts` « résumé OD/OG… » | PASS |
| Q-04-188 | non-régression : au changement de profil, première lecture avec les droits du nouveau profil (lecture seule) | « changement de profil : la première lecture… » | PASS |
| Q-04-189 | 5 — design Lot 3 et autres écrans optiques sans régression | `plan-lot3.mjs` (34 captures) et `plan.mjs` (51 captures) + `shoot.mjs` (Edge sans interface) | PASS (navigateur) — 0 erreur console, 0 défilement horizontal — [`evidence/P04A_SIMPL_captures_rapport_20261020.json`](evidence/P04A_SIMPL_captures_rapport_20261020.json) ; natif NOT_TESTED |

### Statut des tests prévus — mise à jour du 20/10/2026 (D-04-16)

| ID | Statut au 20/10/2026 |
|---|---|
| Q-04-105, Q-04-118, Q-04-119, Q-04-121, Q-04-132, Q-04-133, Q-04-143, Q-04-144 | **REPORTÉ (BL-04-01)** — pièces sources hors Phase 04 |
| Q-04-140 | NOT_TESTED (04-B) ; volet « aucun contenu de pièce » sans objet en 04 |
| Q-04-141 | NOT_TESTED (04-B) ; `no-store` maintenu, volet `nosniff` des pièces **REPORTÉ (BL-04-01)** |
| Q-04-145 | **REPORTÉ (BL-04-01)** pour les pièces ; restauration sans pièces toujours exigée avant pilote (O-04-09) |
| Q-04-026 | PASS — 13 tests (double : cycle, simplification, droits, valeurs) |
| Q-04-027 | PASS — 24 tests (parcours du banc) |
| Q-04-178, Q-04-179 | rejoués après la simplification : voir Q-04-189 (`plan.mjs` : 51 captures, les 3 captures de pièces retirées) |

## 04-A — enregistrement simplifié et clôture (D-04-17) — contrôles exécutés le 20/10/2026

Mandat : [`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md). Preuves sur le **banc** et le **double en mémoire** seulement ; exécution finale après le dernier changement de code. Les sections précédentes sont conservées comme historique.

| ID | Contrôle | Commande / méthode | Statut |
|---|---|---|---|
| Q-04-056 | préflight Git | `git log`, `git status --short` | PASS — HEAD `64cedc4`, aucune modification suivie, non suivis du propriétaire intacts |
| Q-04-057 | typage strict | `npm run check` | PASS |
| Q-04-058 | lint | `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02) |
| Q-04-059 | tests frontend (suite complète, composants communs inclus) | `vitest run` | **PASS 288 / 288** (25 fichiers) — [`evidence/P04A_CLOT_frontend_20261020.log`](evidence/P04A_CLOT_frontend_20261020.log) |
| Q-04-060 | build | `vite build` | PASS |
| Q-04-061 | banc absent du bundle | chaînes de Q-04-050 + `Enregistrer comme brouillon`, `Laisser en brouillon`, `confirmation refusée`, `optical_values_missing`, `nextConfirmFault`, `Échec de la prochaine confirmation` | PASS — aucune occurrence ; « Dossier optique » : 1 fichier (onglet réservé au banc dans `ClientDetailView`, inchangé) — [`evidence/P04A_CLOT_bundle_20261020.log`](evidence/P04A_CLOT_bundle_20261020.log) |
| Q-04-062 | application réelle inchangée, prototype isolé | `realAppIsolation.test.tsx` (4 tests, inclus dans Q-04-059) ; `git diff --stat` limité à `src/optical/`, `src/bench/`, `e2e/bench-04a/`, `docs/`, `prompts/`, `README.md` | PASS |
| Q-04-063 | aucun SQL, migration `0006`, backend, route, permission ni compte | `git diff --stat` | PASS |
| Q-04-064 | tests .NET et PostgreSQL réel | — | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié) |
| Q-04-065 | interface Tauri native | — | **NOT_TESTED** (banc hors application, O-04-16) |
| Q-04-066 | captures : 1920×1080 et 1024×700, zoom 100 / 125 / 150 %, clair / sombre, deux formulaires, états D-04-17 | `plan-lot3.mjs` (34) + `plan.mjs` (58) + `shoot.mjs` (Edge sans interface) | PASS (navigateur) — 0 erreur console, 0 défilement horizontal — [`evidence/P04A_CLOT_captures_rapport_20261020.json`](evidence/P04A_CLOT_captures_rapport_20261020.json) |
| Q-04-067 | liens internes et statuts cohérents des documents modifiés ; absence de secrets dans le diff | résolution de chaque chemin relatif Markdown ; recherche des statuts courants ; recherche de motifs de secrets dans `git diff` | PASS (voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |

### Tests de l'enregistrement simplifié (banc, jsdom, garde réseau active)

| ID | Exigence du mandat | Test | Statut |
|---|---|---|---|
| Q-04-190 | 2 — un seul bouton principal bleu « Enregistrer » (`Control+S`), brouillon en action secondaire moins visible ; ± sur OD et OG puis « Enregistrer » → révision 1 confirmée, sans récapitulatif | `OpticalBench.test.tsx` « une action principale « Enregistrer »… » | PASS |
| Q-04-191 | 4 — Ctrl+S = « Enregistrer » ; mesure magasin, provenance distincte | « Ctrl+S = « Enregistrer »… » | PASS |
| Q-04-192 | 2, 4 — 422 (aucune valeur optique) : « Brouillon enregistré — confirmation refusée », saisie conservée, « Laisser en brouillon » proposé, reprise sur le même enregistrement ; `0` est une valeur | « confirmation refusée (422, aucune valeur optique)… » | PASS |
| Q-04-193 | 4 — erreur de communication à la confirmation : brouillon annoncé enregistré, jamais « rien n'a été enregistré » | « erreur de communication à la confirmation… » | PASS |
| Q-04-194 | 2, 4 — modification d'un enregistrement confirmé, 409 à la confirmation : dernière révision confirmée préservée, aucun faux avertissement de sortie, reprise sans doublon → révision 2 | « modification d'un enregistrement confirmé, conflit 409… » | PASS |
| Q-04-195 | 4 — double clic et Ctrl+S répété pendant l'enregistrement : un seul enregistrement | « double clic et Ctrl+S répété… » | PASS |
| Q-04-196 | 2, 7 (6–7) — brouillon incomplet enregistré au clavier, repris (« Modifier ») puis confirmé | « brouillon incomplet enregistré au clavier… » | PASS |
| Q-04-197 | 2 — règle minimale de démonstration du double (aucune valeur → 422 `optical_values_missing` ; un œil suffit ; `0` présent ; espaces = absence) ; panne de la confirmation seule sans écriture partielle | `inMemoryOpticalGateway.test.ts` (3 tests) | PASS |
| Q-04-198 | 3 — libellés « Modifier », « Historique des modifications », « Dernière révision confirmée » ; aucun « en vigueur » | J-04-04 « modifier → brouillon… », « détail : aucune pièce source… » | PASS |
| Q-04-199 | 4 — 409 à l'enregistrement lui-même (autre poste) : « rien n'a été enregistré », pas de « Laisser en brouillon » | J-04-06 | PASS |

### Statut des tests prévus — mise à jour du 20/10/2026 (D-04-17)

| ID | Statut au 20/10/2026 |
|---|---|
| Q-04-183 | **remplacé** : la règle de démonstration sur la provenance n'existe plus (O-04-36) ; voir Q-04-192, Q-04-197 et le test J-04-04 « un seul œil suffit… » |
| Q-04-026 | PASS — 15 tests (double) |
| Q-04-027 | PASS — 31 tests (parcours du banc) |
| Q-04-178, Q-04-179, Q-04-189 | rejoués : voir Q-04-066 (`plan.mjs` : 58 captures, dont 7 nouvelles `70`–`76`) |

