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

## 03-A3 — Intégration réelle Clients (17/10/2026, GO « PHASE 03-A3 »)

Mandat : [`prompts/PHASE03_A3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_A3_GO_CLAUDE_CODE.md). Horodatages UTC. Écritures : migration sur `mmv_v2_dev` puis `mmv_v2_test` (autorisée) ; toute autre écriture sur `mmv_v2_test` **uniquement** (comptes et clients éphémères, supprimés). PostgreSQL 17 (5432) jamais contacté. Compte réel jamais utilisé ni modifié.

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-03-80 | Empreinte et correspondance du script | `sha256sum` ; `dotnet ef migrations script` ×3 hors connexion (`--no-build`, BOM/CRLF retirés) ; `diff` direct ↔ idempotent | PASS | `4dbb0d84…d3db1c3b` = empreinte approuvée ; scripts régénérés **octet pour octet identiques** (`7f61f0a8…`, `4dbb0d84…`, `8481adb0…`) ; l'idempotent n'ajoute que les gardes `IF NOT EXISTS` (11 blocs) |
| Q-03-81 | Précontrôle des deux bases | `psql` en session `default_transaction_read_only=on` (secrets lus comme `Invoke-MmvSqlScript.ps1`) | PASS | `mmv_v2_dev`/`mmv_v2_dev` et `mmv_v2_test`/`mmv_v2_test`, `::1`:5433, PostgreSQL 18.6 (180006) ; migrations : `InitialBaseline`, `AuthIdentity` seulement ; aucun objet `clients` ; contrainte `ck_user_permissions_permission` = `system.admin` ; rôle propriétaire de la base (`CREATE` accordé) ; format de référence par expression littérale (séquence **non** touchée) : 1 → `CLI-000001`, 999999 → `CLI-999999`, 1000000 → `CLI-1000000`, motif `^CLI-[0-9]{6,18}$` vérifié |
| Q-03-82 | Application `mmv_v2_dev` | `./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev -Script docs/phase/03/sql/0003_ClientsModule.idempotent.sql -Sha256 4dbb0d84…` | PASS | `PRECHECK OK : mmv_v2_dev / mmv_v2_dev / port 5433 / server_version_num 180006` ; 11 `DO` ; `COMMIT` ; `APPLIQUE : exit 0` ; erreur de la requête d'**affichage** après `COMMIT` (O-02-15, connue, sans effet). `evidence/P03A3_0003_apply_mmv_v2_dev_20261017T1055Z.log` |
| Q-03-83 | Application `mmv_v2_test` | idem, `-SecretsId mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Database mmv_v2_test` | PASS | idem ; `evidence/P03A3_0003_apply_mmv_v2_test_20261017T1055Z.log` |
| Q-03-84 | Inspection post-migration | `psql` lecture seule (catalogues) | PASS | 3 migrations (`…ClientsModule`, `10.0.4`) ; schéma `clients` (propriétaire = rôle) ; table 17 colonnes, 6 `CHECK`, 2 FK `RESTRICT`, PK, 5 index (dont partiel `ix_clients_active_name`, unique `ux_clients_reference`) ; séquence `MAXVALUE 999999999999999999`, `NO CYCLE`, jamais appelée ; contrainte de permissions étendue aux 3 droits Clients ; structures dev = test. `evidence/P03A3_postmigration_readonly_20261017T1056Z.log` |
| Q-03-85 | `Stage=PostMigration` (relance de Q-03-64) | `dotnet test tests/MMV.PostgreSql.Tests --filter Stage=PostMigration` | PASS | 2/2 (`TestDatabase_AllMigrationsApplied`, `DevDatabase_AllMigrationsApplied`) |
| Q-03-86 | `Stage=ClientsWrite` (relance de Q-03-65) | `--filter Stage=ClientsWrite` | **FAIL puis PASS** | 1re exécution (tests 03-A2 inchangés) : **4/5**, `Constraints_AreEnforcedByPostgreSql` attendait `23503` ; PostgreSQL 18.6 renvoie `23001 restrict_violation` pour `ON DELETE RESTRICT` (suppression bien refusée : défaut de l'attente, pas du schéma). Attente corrigée, tests ajoutés (`Duplicates_AreAllowed_WithDistinctReferences`, `ClientsHttpPermissionsAndLifecycle_OnRealPostgreSql` : 4 comptes, 403 pour `system.admin` seul / lecture / écriture sans archivage, champ inconnu → 400, doublons → 2 × 201, recherche sans accents, 409 `version_conflict` / `already_archived` / `client_archived` / `already_active`, DELETE → 405, résidus = 0) : **7/7** |
| Q-03-87 | `Stage=IdentityWrite` (relance de Q-03-66) | `--filter Stage=IdentityWrite` | PASS | 5/5 (exemple `clients.delete` refusé par la contrainte étendue) |
| Q-03-88 | Procédure de permissions (sans écriture) | `MMV.Maintenance status` ; `permissions list` ; `permissions grant x system.admin` ; `permissions grant … clients.read` entrée redirigée | PASS | 3 migrations, 0 en attente ; 1 compte réel, `system.admin` seul ; refus exit 1 **sans connexion** ; refus exit 4 avant écriture ; **aucun compte réel modifié**. Procédure : [`PROCEDURE_PERMISSIONS_CLIENTS.md`](PROCEDURE_PERMISSIONS_CLIENTS.md). `evidence/P03A3_maintenance_permissions_readonly_20261017T1110Z.log` |
| Q-03-89 | Matrice HTTP sur le processus réel | MMV.Api `--launch-profile https` (chaîne `mmv_v2_test` passée au seul processus par `ConnectionStrings__Mmv`) ; script Node local, TLS système (`--use-system-ca`), 4 comptes éphémères | PASS **31/31** | 401 anonyme / jeton forgé ; 403 `system.admin` seul (GET, POST) ; HTTP 5022 → 403 `https_required` ; lecture 200 / POST 403 ; `socialSecurityNumber` → 400 `invalid_body` ; 422 `lastName: required` ; doublons 2 × 201 + `Location` ; recherche « ELOISE lefevre-… » → 2, téléphone partiel → 2 ; `invalid_query` ; 409 `version_conflict` (autre poste) **sans écriture** ; `version_required` ; archivage 403 sans `clients.archive`, 409, 200, `client_archived`, `already_archived` ; filtres actifs/archivés ; réactivation 403/409/200 ; DELETE 405 ; 404 `client_not_found` ; CORS sans DELETE ; 401 après déconnexion. `evidence/P03A3_native_clients_e2e_20261017T1120Z.log` |
| Q-03-90 | Parcours natif Tauri → HTTPS → API → PostgreSQL 18 (relance de Q-03-75 et Q-03-22) | `npx tauri dev` (WebView2, CDP 127.0.0.1:9222) ; `node --use-system-ca e2e/run-native.mjs <scénario> --prefix P03_native`, scénarios `clients-admin-only`, `clients-full`, `clients-read`, `clients-write` | PASS | `system.admin` seul → « Accès non autorisé », aucune table ; compte complet : validation (focus Prénom), création (référence serveur), « Client créé », fiche, recherche sans accents **surlignée sur 2 mots**, aucun résultat, archivage confirmé, inclusion des archivés, fiche archivée en lecture seule, réactivation, **conflit** (autre poste via l'API → dialogue « Action impossible », rechargement, version périmée non écrite), sombre, 1440×900 sans défilement horizontal ; lecture seule : motif « Consultation seule », « Nouveau client » et actions de ligne absents, Modifier/Archiver désactivés ; écriture sans archivage : Archiver absent/désactivé. 22 captures `evidence/P03_native_clients_*_20261017T1115*.png`. Défauts du **banc** corrigés avant la passe retenue (champ de recherche global du Shell ciblé par erreur ; thème sombre mémorisé par la WebView → thème désormais imposé) ; passes antérieures écartées, données éphémères remises à zéro avant chaque passe |
| Q-03-91 | Comparaison aux planches Lot 2 | 17 planches rendues par `data-mount` (Edge headless 155, profil temporaire, fichier `references/` non versionné) et mises côte à côte avec les captures natives (hors dépôt) | PASS (constat) | conformes en structure : en-têtes, barre d'outils, colonnes, badges, pagination, formulaire 2 colonnes et sections, bandeaux, fiche et onglets, dialogues ; écarts listés dans [`STATE_MAP_47.md`](STATE_MAP_47.md) § Amendement 03-A3 (issus de décisions ou du Shell Phase 02) ; écart fonctionnel trouvé et **corrigé** : surlignage absent pour une requête à plusieurs mots |
| Q-03-92 | Confidentialité | recherche des **valeurs exactes** des 4 mots de passe éphémères et du mot de passe PostgreSQL de test + motifs `Bearer `, `accessToken`, `Password=`, `"password":`, hachage Identity, dans 8 journaux ; recherche de noms, téléphones, emails, villes, NSS, identifiants de compte dans le journal MMV.Api (207 lignes) | PASS | 0 occurrence ; événements 3001/3002 avec identifiants techniques seulement (D-03-10) |
| Q-03-93 | Permissions de session côté interface (O-03-09) | `ClientsModule.test.tsx` (2 nouveaux tests) | PASS | lecture seule : aucune action d'écriture, motif, fiche désactivée, **aucune requête non-GET** ; écriture sans archivage : création/modification seules |
| Q-03-94 | Suite .NET complète | `dotnet build MMV.slnx --no-incremental` ; `dotnet test MMV.slnx` (toutes étapes) | **FAIL puis PASS** | 1re exécution : 259/260, `ClientServiceTests.Update_WithCurrentVersion_ChangesDataAndVersion` — le double `InMemoryClientStore.Seed` datait les clients à l'heure **réelle** et le service à une horloge fixe (17/10/2026 09:35 Z) : échec déterministe passé 09:35 Z (défaut latent du test 03-A2, aucun code applicatif en cause). Double corrigé (horloge du test) : **260/260** (API 182, Maintenance 50, PostgreSQL 28), 0 ignoré, build 0 avertissement / 0 erreur. `evidence/P03A3_dotnet_20261017.log` |
| Q-03-95 | Frontend complet | `npm run check` ; `npm run lint` ; `npm test` ; `npm run build` | **FAIL puis PASS** | 1re exécution : `check`/`build` en erreur TS2550 (`Array.prototype.at` hors cible ES dans `searchText.ts`, ajout 03-A3) ; remplacé par un indice : check 0 erreur ; lint 0 erreur / 1 avertissement (O-03-02) ; **137/137** (13 fichiers) ; build OK, `ClientsModule` 113,51 kB, principal 548,55 kB (O-03-03) ; aucune donnée fictive ni double dans `dist/`. Le parcours natif Q-03-90 a tourné **avant** ce remplacement équivalent (`NOT_RERUN` natif ; couvert par les tests unitaires). `evidence/P03A3_frontend_20261017.log` |
| Q-03-96 | Résidus et état final des bases | outil local `residue` ; `psql` lecture seule | PASS | `mmv_v2_test` : 0 compte, 0 session, 0 client, 0 bootstrap, 0 permission (après tests et après preuve native) ; `mmv_v2_dev` : **0 client**, séquence jamais appelée (`is_called = f`), 1 compte réel (`system.admin` seul), 3 migrations. Séquence de `mmv_v2_test` avancée par les insertions annulées (O-03-12, attendu) |
| Q-03-97 | Processus et ports | `Get-CimInstance Win32_Process` ; `Get-NetTCPConnection` | PASS | au départ : seuls les serveurs Serena ; en fin : MMV.Api, Vite, Tauri et Edge headless **lancés par l'agent** arrêtés ; ports 7267, 5022, 1420, 9222, 9333 libres |
| Q-03-98 | Graphify CLI | `graphify update .` ; `graphify query "useClientPermissions clientPermissions"` | PASS | 2382 nœuds ; sous-graphe `useClientPermissions()` → `ClientsListView`, `ClientDetailView`, `ClientsModule` (`CreateRoute`, `ClientRoute`), `useAuth()` |
| Q-03-99 | Non testé / limites | — | NOT_TESTED | compte réel du propriétaire : **volontairement** non utilisé (permissions non attribuées, procédure fournie) ; `npx tauri build` `NOT_RERUN` ; captures 1440×900 limitées à la liste ; zoom 125/150 % non rejoué sur Clients ; Serena MCP non utilisé (Q-03-77 inchangé) |

Q-03-22, Q-03-64, Q-03-65, Q-03-66 et Q-03-75 (états `NOT_TESTED`, `FAIL (attendu)`, `NOT_RUN`, `NOT_RERUN` d'avant la migration) sont couverts par Q-03-85 à Q-03-87 et Q-03-90 ; leurs lignes datées restent inchangées.
