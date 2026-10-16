# 03 — GATES_AND_DECISIONS

## GO et bornes

- 17/10/2026 : « GO OFFICIEL PHASE 03 » du propriétaire (message de session), mandat `prompts/PHASE03_GO_CLAUDE_CODE.md` (versionné avec la phase, comme les mandats précédents) et cadrage `references/phase03/` (copie de travail **non versionnée**, empreintes dans `SOURCE_MAP.md`). Couvre : branche dédiée, TanStack Query v5 et Table v8, premiers écrans Clients, propositions de modèle / contrat / permissions / SQL, cadrage technique des Rendez-vous (FullCalendar Standard), tests, DOCUMENTATION GATE, commits locaux.
- **Bornes :** aucune modification de base PostgreSQL, aucune migration métier ni permission persistante avant validation G03-METIER ; aucun push, PR, merge ni Phase 04 ; aucun accès à l'ancien MMV.

## Gates Phase 03

| Gate | Critère | État (17/10/2026) |
|---|---|---|
| G03-0 | Branche propre depuis `origin/main` fusionné, Phase 02 confirmée, sources Lot 2 recensées, environnement, non-régression | **PASS** — `cb79d1f` ; QA Q-03-01 à Q-03-06, Q-03-30 ; réserves environnementales externes : Q-03-11 (`npm ci`), Q-03-32 (build complet) |
| G03-A1 | Écrans Clients (liste, recherche, filtres, formulaire, fiche, archivage/réactivation) avec TanStack Query/Table, états testables, aucune donnée fictive en fonctionnement normal | **IMPLEMENTED** — tests 128/128 ; service désactivé jusqu'à G03-METIER ; captures natives `NOT_TESTED` (Q-03-22) |
| G03-METIER | Validation unique : champs clients, cycle de vie, permissions, contrat, migration SQL (+ section Rendez-vous facultative à ce stade) | **OPEN — dossier présenté** : [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) |
| G03-A2 | Domaine, API contrôlée serveur, persistance, migration appliquée, tests d'intégration `mmv_v2_test` | `NOT_STARTED` — bloqué par G03-METIER |
| G03-B | Rendez-vous | `NOT_STARTED` — cadrage [`RDV_03B_PREPARATION.md`](RDV_03B_PREPARATION.md) ; décisions D-03-13 à D-03-17 |
| G03-QA | Livraison Phase 03 (03-A, 03-B, 03-C) | `NOT_STARTED` — la Phase 03 n'est **pas** achevée |
| G03-DOC | DOCUMENTATION GATE | synchronisé le 17/10/2026 (`README.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/ADR_ET_DECISIONS.md`, `apps/desktop/README.md`, `docs/phase/02.md` (amendement MERGED), `docs/phase/03.md`, `docs/phase/03/*`) |

## Décisions métier ouvertes

D-03-01 à D-03-17 : **PROPOSED**, détaillées dans [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md). Aucune n'est approuvée.

## Décisions techniques ouvertes (non bloquantes pour le dossier métier)

| ID | Sujet | État | Bloque |
|---|---|---|---|
| O-03-01 | OpenAPI incomplet (réponses 200 sans schéma) : `openapi-typescript` / `openapi-fetch` non installés ; déclarer les réponses (`TypedResults`, `.Produces`) en 03-A2 puis réévaluer la génération | PROPOSED | aucune |
| O-03-02 | Avertissement lint `react-hooks/incompatible-library` sur `useReactTable` (React Compiler ignore `ClientsTable`) : conservé visible, aucune suppression de règle | PROPOSED | aucune |
| O-03-03 | Bundle principal 548 kB (> 500 kB) malgré le chunk séparé `ClientsModule` (112 kB) : TanStack Query reste dans le bundle initial (Shell) ; prolonge O-02-13 | PROPOSED | aucune |
| O-03-04 | Service Clients activé par une constante de compilation (`CLIENTS_API_ENABLED`) ; alternative : détection d'une capacité serveur à l'exécution | PROPOSED | 03-A2 |
| O-03-05 | Garde de sortie par registre maison (`shell/navigationGuard.ts`) car le routeur mémoire n'est pas un « data router » ; alternative : `createMemoryRouter` + `useBlocker` (refonte du Shell) | PROPOSED | aucune |
| O-03-06 | `@radix-ui/react-alert-dialog` / `react-tabs` 1.2.1 publiés le 12/10/2026 (< 2 semaines) pour rester sur la vague déjà installée de `react-dropdown-menu` 2.2.1 (primitives partagées dédupliquées) | PROPOSED | aucune |
| O-03-07 | Recherche « aperçu » : la recherche, les filtres et le tri sont désactivés tant que le service n'est pas activé ; seul le formulaire est consultable | PROPOSED | aucune |
| O-03-08 | Processus externes à la session encore actifs : `MMV.Api` (PID 26332) et serveur Vite (PID 23448) ; ils ont empêché `npm ci` et le build complet ; le serveur Vite a pu être perturbé pendant la restauration de `node_modules` (à redémarrer) | OPEN — action propriétaire | relance Q-03-11, Q-03-32 |

Héritées, inchangées : O-02-13 (bundle), O-02-14 (503 sur `/api` si base injoignable : l'interface Clients traite 500/503 comme erreur technique bornée), O-02-15 (script PowerShell), O-B04, O-B10, O-B11, O-B12 ; ADR-004/006/007 non ratifiés.

## Dossier de migration 0003 (préparé, non appliqué)

Scripts : `docs/phase/03/sql/0003_ClientsModule.PROPOSED.sql` (direct depuis `AuthIdentity`), `.idempotent.sql`, `.rollback.sql`. Empreintes SHA-256 :

| Fichier | SHA-256 |
|---|---|
| `0003_ClientsModule.PROPOSED.sql` | `94545b84bd5ecfcf87a6b6d9f1dd4f361bb5024698bea7c968da3f5b91e03224` |
| `0003_ClientsModule.PROPOSED.idempotent.sql` | `8ea1f48af9ee2f09966e6034b6e5cbb37def4064cbbb1b60c75b34b6122d82f8` |
| `0003_ClientsModule.PROPOSED.rollback.sql` | `15fb8230722103e6f40068031a6a6950ccd7dc09d7e72f42407138806e91b78f` |

Le code C# correspondant n'est **pas** dans `src/` : il sera intégré en 03-A2 après GO, la migration étant alors régénérée si le modèle ratifié diffère (le nom horodaté changera). Procédure d'application prévue, identique à O-02-01 : `scripts/Invoke-MmvSqlScript.ps1` sur `mmv_v2_dev` puis `mmv_v2_test`, contrôle `__EFMigrationsHistory`, tests PostgreSQL complets, jamais au démarrage, jamais `EnsureCreated()`.

## Journal

- 17/10/2026 : G03-0 PASS ; 03-A1 implémenté (UI + tests) ; dossier G03-METIER, contrat, SQL proposé et cadrage 03-B présentés. État : **`PHASE03_A_UI_READY — OWNER_BUSINESS_RULES_AND_SQL_REVIEW_REQUIRED`**.
