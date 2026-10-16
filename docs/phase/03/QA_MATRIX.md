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

## 03-A2 — Préflight et environnement (17/10/2026)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-50 | Processus du propriétaire | `Get-CimInstance Win32_Process` (node, dotnet, MMV, tauri) | PASS | aucun `MMV.Api`, Vite ni Tauri actif (seuls les serveurs de langage Serena) ; aucun processus arrêté ni modifié par l'agent |
| Q-03-51 | `npm ci` (relance de Q-03-11) | `npm ci` ; `cmp package-lock.json` | PASS | 0 vulnérabilité ; verrou identique |
| Q-03-52 | Dépendances | `git status` des verrous npm / NuGet | PASS | aucune dépendance ajoutée (ni npm, ni NuGet) ; `packages.lock.json` et `package-lock.json` inchangés |

## 03-A2 — Backend (sans migration)

| ID | Vérification | Commande | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-53 | Règles D-03-01 à D-03-05 | `ClientRulesTests` | PASS | 47/47 : obligatoires, longueurs, téléphone 8–15 chiffres, email (motif de Zod 4), date civile 1900→aujourd'hui, code postal, caractères de contrôle, repliement accents/casse, motifs de recherche échappés, cycle de vie |
| Q-03-54 | Cas d'usage | `ClientServiceTests`, `PermissionAdministrationServiceTests` | PASS | 9/9 et 7/7 : UUID v7, auteur, précision µs, version périmée → conflit sans écriture, conflit à l'enregistrement, client archivé non modifiable, aucune suppression ; `system.admin` et permissions inconnues non attribuables |
| Q-03-55 | Pipeline HTTP `/api/clients` | `ClientsEndpointsTests` | PASS | 46/46 : 401 ×6, 403 pour `system.admin` seul et compte sans droit ×6, chaque écriture exige sa permission, cycle complet, 422 à codes bornés, 400 `invalid_body` (champ inconnu `socialSecurityNumber`, JSON illisible, type), 415, `version_required`, 404, `invalid_query` ×16, pagination serveur, DELETE → 405, HTTP → 403 `https_required`, CORS PUT admis / DELETE refusé |
| Q-03-56 | Confidentialité des journaux (D-03-10) | `Logs_ContainTechnicalIdentifiersOnly` | PASS | identifiant du client présent ; aucun nom, téléphone, email, adresse, date de naissance, note ni terme de recherche |
| Q-03-57 | Contrat OpenAPI réel | `ClientsOpenApiContractTests` (2) ; instantané `apps/desktop/src/api/contract/clients.openapi.json` | PASS | 4 chemins, aucun DELETE ; statuts déclarés (200/201/400/401/403/404/409/422) ; 9 schémas dont `ClientStatusValue` ; entiers stricts ; paramètres de liste à énumérations et bornes |
| Q-03-58 | `MMV.Maintenance permissions` | `BootstrapCommandProcessTests` (12 nouveaux) | PASS | permission non attribuable → code 1 sans connexion ; cible étrangère → code 5 « aucune connexion n'a été ouverte » ; arguments mal formés → 1 ; `permissions list` en lecture seule non refusée par la politique |
| Q-03-59 | Non-régression Phase 02 | `AuthEndpointsTests` | PASS | `GET /api/clients` (route absente en 02) remplacé par `/api/appointments` et `/api/prescriptions` (toujours 404) ; aucun autre test modifié |
| Q-03-60 | Build complet (relance de Q-03-32) | `dotnet build MMV.slnx --no-incremental` | PASS | 0 avertissement / 0 erreur |
| Q-03-61 | Suite .NET hors écriture | `dotnet test MMV.slnx --filter "Stage!=IdentityWrite&Stage!=ClientsWrite&Stage!=PostMigration"` | PASS | `MMV.Api.Tests` 182/182, `MMV.Maintenance.Tests` 50/50, `MMV.PostgreSql.Tests` 14/14 |
| Q-03-62 | Expression de référence sur PostgreSQL 18 réel | `ClientReferenceFormatPostgreSqlTests` (session lecture seule, `mmv_v2_test`) | PASS | `CLI-000001`, `CLI-000042`, `CLI-999999`, `CLI-1000000`, `CLI-123456789`, `CLI-999999999999999999` ; jamais tronqué |
| Q-03-63 | État connu des bases | `SchemaStateTests.*_ContainsOnlyAKnownState` (lecture seule) | PASS | `mmv_v2_dev` et `mmv_v2_test` à l'état baseline + identité (aucun objet `clients`) |
| Q-03-64 | Toutes migrations appliquées | `--filter "Stage=PostMigration"` | **FAIL (attendu)** | 2/2 échecs : schémas `[auth]` au lieu de `[auth, clients]`, historique limité à `InitialBaseline`, `AuthIdentity` — constat que 0003 n'est **pas** appliquée ; à relancer après G03-SQL (`evidence/P03A2_postgresql_20261017.log`) |
| Q-03-65 | Écriture Clients sur PostgreSQL | `--filter "Stage=ClientsWrite"` (5 tests : cycle complet, conflit `xmin` entre deux contextes, recherche, contraintes, matrice HTTP) | **NOT_RUN** | exige 0003 appliquée sur `mmv_v2_test` ; **non déclaré réussi** |
| Q-03-66 | Étape `IdentityWrite` | — | NOT_RERUN | 5 tests ; exemple de permission inconnue remplacé (`clients.read` devient admise par 0003 → `clients.delete`) ; non exécutés (aucune écriture sans GO) |
| Q-03-67 | Migration et SQL hors connexion | `dotnet ef migrations add` / `script` / `list --no-connect` | PASS | `20261016230630_ClientsModule` ; 3 scripts, empreintes dans `GATES_AND_DECISIONS.md` ; diff avec la proposition 03-A limité à la référence ; **aucune connexion, aucune application** |
| Q-03-68 | Base de développement | revue des commandes exécutées | PASS | aucune écriture persistante sur `mmv_v2_dev` ni `mmv_v2_test`, aucun client fictif, aucune permission attribuée (seules les sessions en lecture seule et la transaction annulée de `TransactionalRollbackTests` ont touché `mmv_v2_test`) |

## 03-A2 — Frontend

| ID | Vérification | Commande | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-70 | Typage | `npm run check` | PASS | aucune erreur |
| Q-03-71 | Lint | `npm run lint` | PASS (avertissement) | 0 erreur, 1 avertissement O-03-02 inchangé |
| Q-03-72 | Tests | `npm test` | PASS | **134/134**, 13 fichiers (03-A : 128 ; + 5 contrat ; Shell : test « non activé » remplacé par « `system.admin` seul → 403 serveur » + « interrupteur de repli », aucun test supprimé sans remplacement) |
| Q-03-73 | Types ↔ contrat | `apps/desktop/src/api/clientsContract.test.ts` | PASS | 5/5 : propriétés et nullabilité (tables vérifiées à la compilation), `status`, entiers, requêtes, routes et statuts, listes blanches et bornes = `clientListQuery`, parseurs stricts sur exemple généré du schéma |
| Q-03-74 | Build | `npm run build` ; recherche dans `dist/` | PASS (avertissement) | `ClientsModule` 112,04 kB ; principal 548,44 kB (O-03-03) ; aucun double, aucune donnée fictive, aucun contrat de test dans le bundle |
| Q-03-75 | Parcours natif sur la vraie API | `npx tauri dev` | **NOT_TESTED** | exige la migration 0003 et des droits `clients.*` attribués ; sans migration, l'écran afficherait l'erreur technique bornée |

## 03-A2 — Outils agent

| ID | Vérification | Appel réel | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-76 | Graphify CLI | `graphify update .` ; `graphify query "Comment les permissions clients.write protègent la modification d'un client ?"` | PASS | 2327 nœuds ; sous-graphe : `Permissions`, `Client`, `ClientService`, `ClientRulesTests`, `MmvDbContext` |
| Q-03-77 | Serena MCP | — | NOT_RUN | non utilisé dans cette session (lecture directe et Graphify suffisants) |
