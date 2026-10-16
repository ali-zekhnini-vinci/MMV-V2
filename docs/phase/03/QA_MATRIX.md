# 03 — QA_MATRIX

Exécutions réelles, poste Windows 11 Home 10.0.26200 x64, 17/10/2026. Statuts : PASS / FAIL / BLOCKED / NOT_TESTED / NOT_RERUN. Aucun résultat simulé. Journaux : `docs/phase/03/evidence/`.

## Préflight G03-0

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-01 | Merge Phase 02 | `git fetch origin` ; `git log -1 origin/main` ; `git rev-parse origin/main^1 origin/main^2` ; `git merge-base --is-ancestor 7d3966b origin/main` ; `git diff --stat 7d3966b cb79d1f` | PASS | `origin/main` = `cb79d1f951aa68e9523642384648424dee61836d` (« Merge pull request #3 from …/feat/phase-02-auth-shell », 16/10/2026 23:49 +02:00), parents `c0c3d6b` et `7d3966b` ; aucun écart d'arbre entre `7d3966b` et `cb79d1f` |
| Q-03-02 | Branche | `git switch -c feat/phase-03-clients-rdv origin/main --no-track` | PASS | branche créée depuis `cb79d1f` ; fichiers non suivis préexistants conservés intacts (`.claude/`, `.claude.zip`, `.serena/`, `MMV-V2-phase02-final.zip`, `apps/desktop/desktop.zip`, `prompts/PHASE03_GO_CLAUDE_CODE.md`, `references/`) |
| Q-03-03 | Outils | `node --version`, `npm --version`, `dotnet --version`, `graphify --version` | PASS | Node 22.23.3, npm 10.9.9, SDK .NET 10.0.402, dotnet-ef 10.0.4 (local), graphify 0.9.77 |
| Q-03-04 | Pile présente | `npm ls --depth=0` ; `Directory.Packages.props` | PASS | Tailwind CSS 4.3.3, shadcn/ui (copie locale) + Radix, React Router 8.4.0, React Hook Form 7.90.0 + Zod 4.6.7, Vitest 5.0.3, React 19.3.1 ; ASP.NET Core 10.0.13, Npgsql EF 10.0.3 / EF Core 10.0.4 ; PostgreSQL 18 (base MMV via tests en lecture/transaction annulée seulement) |
| Q-03-05 | Migrations d'identité présentes | `dotnet ef migrations list --no-connect` (worktree) | PASS | `InitialBaseline`, `AuthIdentity` dans `origin/main` |
| Q-03-06 | Sources Lot 2 | `sha256sum` ; `registres/DESIGN_SOURCES_SHA256.csv` | PASS | HTML maître `26df60b6…c39fd7` = empreinte du registre (lot 2, 21 groupes, 47 états) ; légendes historiques mentionnant l'ancien dépôt **ignorées**, aucun accès |

## 03-A — Frontend

| ID | Vérification | Commande | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-10 | Installation figée | `npm install --save-exact …` ; `npm ls` | PASS | `@tanstack/react-query` 5.104.1, `@tanstack/react-table` 8.21.3, `@radix-ui/react-alert-dialog` 1.2.1, `@radix-ui/react-tabs` 1.2.1 ; 0 vulnérabilité signalée |
| Q-03-11 | `npm ci` | `npm ci` | **BLOCKED** | EPERM sur `node_modules/@tailwindcss/oxide-win32-x64-msvc/*.node`, verrouillé par un serveur Vite **externe à la session** (PID 23448, démarré le 17/10/2026 00:28, non arrêté) ; `node_modules` restauré par `npm install`, `package-lock.json` identique (`cmp`). À relancer serveur Vite arrêté |
| Q-03-12 | Typage | `npm run check` (3 tsconfig) | PASS | aucune erreur |
| Q-03-13 | Lint sans exclusion | `npm run lint` | PASS (avertissement) | 0 erreur, 1 avertissement `react-hooks/incompatible-library` sur `useReactTable` (attendu avec TanStack Table v8 : React Compiler ignore ce composant) — O-03-02 |
| Q-03-14 | Tests | `npm test` | PASS | **128/128**, 12 fichiers (Phase 02 : 81 ; 4 tests Phase 02 adaptés à la disponibilité de Clients, aucun supprimé sans remplacement) |
| Q-03-15 | Build | `npm run build` ; recherche dans `dist/` | PASS (avertissement) | chunk `ClientsModule` 112 kB (chargé à la demande) ; bundle principal 548 kB (> 500 kB, O-02-13 / O-03-03) ; 0 fichier contenant un double de test ou une donnée fictive ; 0 adresse de dev |
| Q-03-16 | Service non activé = aucune donnée | `Shell.test.tsx` « Clients (service non activé)… » | PASS | état « Service Clients non activé », aucune table, recherche désactivée, **aucune requête `/api/clients`** |
| Q-03-17 | Liste, tri, recherche, filtres, pagination, états | `ClientsModule.test.tsx` (double HTTP réel) | PASS | paramètres serveur vérifiés dans les requêtes reçues ; `aria-sort` ; surlignage ; 20/page ; chargement, vide, aucun résultat, 500 + Réessayer, 403 |
| Q-03-18 | Formulaire | idem | PASS | validation (focus 1er champ, `aria-invalid`, aucune requête), Ctrl+S, 422 sous le champ, confirmation de sortie (Annuler et menu du Shell), sortie libre si non modifié |
| Q-03-19 | Concurrence et cycle de vie | idem + `clients.test.ts` | PASS | 409 à l'archivage et à la modification → message métier, aucune écriture ; archivage/réactivation versionnés ; `system.admin` seul → 403 |
| Q-03-20 | Fiche, clavier, session | idem | PASS | onglets aux flèches ; Ordonnances/Ventes « phase ultérieure » sans compteur ; archivé en lecture seule ; critères conservés au retour ; cache vidé à la déconnexion |
| Q-03-21 | Clair / sombre | `styles.test.ts` | PASS | CSS Clients et dialogues sans couleur littérale ; chaque jeton couleur utilisé redéfini dans `[data-theme="dark"]` |
| Q-03-22 | Captures natives / comparaison aux planches | `e2e/run-native.mjs` | **NOT_TESTED** | écran Clients derrière l'authentification : exige une session du propriétaire ou des comptes éphémères sur `mmv_v2_test` (écriture en base non autorisée avant GO). Écarts structurels énumérés dans `STATE_MAP_47.md` ; procédure : `npx tauri dev`, se connecter, ouvrir Clients (état « non activé ») et Nouveau client, en clair et sombre, 1440×900 et 1920×1080 |

## 03-A — Backend, SQL et contrat (sans écriture en base)

| ID | Vérification | Commande | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-30 | Non-régression .NET | `dotnet build MMV.slnx` ; `dotnet test MMV.slnx --filter "Stage!=IdentityWrite"` | PASS | build incrémental 0 avertissement / 0 erreur ; `MMV.Api.Tests` 70/70, `MMV.Maintenance.Tests` 38/38, `MMV.PostgreSql.Tests` 15/15 (lecture seule et transactions annulées) |
| Q-03-31 | Étape `IdentityWrite` | — | NOT_RERUN | 5 tests (comptes éphémères sur `mmv_v2_test`) non relancés : aucune écriture en base avant GO ; code backend inchangé depuis `cb79d1f` |
| Q-03-32 | Build complet | `dotnet build MMV.slnx --no-incremental` | BLOCKED | MSB3027 : binaires de `src/MMV.Api` verrouillés par un processus `MMV.Api` **externe à la session** (PID 26332, démarré le 17/10/2026 00:22, non arrêté) |
| Q-03-33 | Migration proposée générée hors connexion | worktree jetable : `dotnet ef migrations add` / `script` / `list --no-connect` | PASS | `20261016223616_ClientsModule` ; scripts `docs/phase/03/sql/0003_ClientsModule.PROPOSED.{sql,idempotent.sql,rollback.sql}` ; build worktree 0/0, `MMV.Api.Tests` 70/70 ; **aucune connexion, aucune application** |
| Q-03-34 | OpenAPI | `GET /openapi/v1.json` (instance de dev déjà lancée, lecture seule) | PASS (constat) | OpenAPI 3.1.1 ; 4 routes ; réponses 200 seulement, sans schéma → génération de types non retenue (O-03-01) ; `evidence/P03_openapi_summary_20261017.txt` |
| Q-03-35 | Bases PostgreSQL | — | PASS | aucune migration, aucun DDL, aucun compte ni permission créés ; PG17/5432 non touché |
| Q-03-36 | Secrets | `git diff` de la branche : recherche de mots de passe / chaînes de connexion | PASS | aucun secret ; données personnelles uniquement fictives dans les tests |

## Outils agent

| ID | Vérification | Appel réel | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-40 | Graphify CLI | `graphify update .` ; `graphify query "Comment le module Clients appelle-t-il l'API ?"` | PASS | 1862 nœuds, 3553 arêtes, 119 communautés ; sous-graphe incluant `ClientsModule.tsx`, `clientQueries.ts`, `api/clients.ts` |
| Q-03-41 | Serena MCP TypeScript | `get_symbols_overview("apps/desktop/src/clients/clientQueries.ts")` | PASS | `clientKeys`, `useClientList`, `useClientDetail`, `useClientWrite`, mutations (limite TypeScript de 01B/02 levée) |
| Q-03-42 | Serena MCP C# | `find_symbol("Permissions/All", "src/MMV.Domain/Identity/Permissions.cs", include_body)` | PASS | `[SystemAdmin]` (branche inchangée côté C#) |
