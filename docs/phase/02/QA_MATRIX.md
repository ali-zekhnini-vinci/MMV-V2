# 02 — QA_MATRIX

Exécutions réelles, poste Windows 11 Home 10.0.26200 x64. Statuts : PASS / FAIL / BLOCKED / NOT_TESTED / NOT_RERUN. Aucun résultat simulé.

## Préflight (16/10/2026)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-02-01 | Merge 01B | `git fetch origin` ; `git rev-parse origin/main` ; `git log -1 --format='%H %P' origin/main` ; `git merge-base --is-ancestor 5c2fec6 origin/main` ; `git diff --stat HEAD origin/main` (depuis `5c2fec6`) | PASS | `origin/main` = `c0c3d6ba15e57b49db350278c5a174735243ccb5`, parents `4ed20a8…`, `5c2fec6…` ; ancêtre : oui ; diff d'arbre vide |
| Q-02-02 | Branche | `git merge-base --is-ancestor main origin/main` ; `git switch main && git merge --ff-only origin/main` ; `git switch -c feat/phase-02-auth-shell origin/main` | PASS | `main` local `bbf1749` → `c0c3d6b` (fast-forward, aucune modification suivie locale) ; `.claude/`, `.serena/`, `prompts/PHASE02_PROMPT_CLAUDE_CODE.md`, `references/` non suivis préservés |
| Q-02-03 | Outils | `dotnet --version`, `node --version`, `npm --version`, `cargo --version`, `dotnet tool list`, `graphify --version` | PASS | SDK 10.0.402, Node 22.23.3, npm 10.9.9, cargo 1.99.0, dotnet-ef 10.0.4 (local), graphify 0.9.77 |
| Q-02-04 | PostgreSQL (sans connexion) | `Get-Service postgresql*`, `Get-NetTCPConnection -LocalPort 5432,5433` | PASS | PG17 et PG18 Running ; PG18 `0.0.0.0:5433`/`[::]:5433` (PID 8848) ; PG17 `127.0.0.1:5432` non touché |
| Q-02-05 | Secrets (noms de clés seulement) | lecture des clés de `%APPDATA%\Microsoft\UserSecrets\{mmv-v2-api,mmv-v2-postgresql-tests}\secrets.json` | PASS | `ConnectionStrings:Mmv` ; `ConnectionStrings:MmvTest` (valeurs non lues) |
| Q-02-06 | Non-régression 01B (.NET) | `dotnet test MMV.slnx` | PASS | `MMV.Api.Tests` 26/26 ; `MMV.PostgreSql.Tests` 12/12 (identité + écriture éphémère `ROLLBACK` sur `mmv_v2_test`) |
| Q-02-07 | Non-régression 01B (frontend) | `npm ci` ; `npm test` ; `npm run lint` | PASS | 25/25 ; lint 0 problème |
| Q-02-08 | Serena MCP C# | `find_referencing_symbols("DesktopCors/AddDesktopCors", "src/MMV.Api/Cors/DesktopCors.cs")` | PASS | 2 références : `src\MMV.Api\Program.cs` l.10 ; `DesktopCorsTests.AddDesktopCors_RejectsWildcardOrNonExactOrigin` |
| Q-02-09 | Serena MCP TypeScript | `get_symbols_overview("apps/desktop/src/api/health.ts")` | BLOCKED (session MCP) | « Cannot extract symbols … Active language servers: ['csharp'] » — même limite qu'en 01B ; `.serena/project.yml` déclare pourtant `csharp` et `typescript`. Reconnexion MCP requise ; preuve CLI prévue (Q-02-2x) |

## 02-A — Interface (16/10/2026)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-02-10 | Typage | `npm run check` (3 `tsconfig`) | PASS | exit 0 |
| Q-02-11 | Lint | `npm run lint` | PASS | 0 problème (avertissement Fast Refresh corrigé en séparant `authContext.ts`) |
| Q-02-12 | Tests frontend | `npm test` | PASS | 8 fichiers, **77/77** : 25 historiques 01B + contrats auth (parseurs, login/session/comptes/logout, 401/403/429), formulaire, navigation/garde, barre d'état, **11 tests Login** (focus initial, tabulation aller/retour, Entrée champs vides sans requête, identifiants incorrects, chargement, 500, API injoignable, visibilité, aucun jeton en stockage/cookie, serveur non configuré), **13 tests Shell** (structure, module futur, Comptes admin, non-admin, 403 en cours de session, réduction clavier, tabulation, thème, déconnexion ×2, révocation → session expirée, redémarrage sans session, serveur inaccessible) |
| Q-02-13 | Build | `npm run build` ; `grep -c localhost:7267 dist/assets/*.js` ; recherche de `fake-token`/`fakeMmvApi` dans `dist/` | PASS | build OK ; 0 occurrence de l'adresse de dev ; aucun double de test dans le bundle |

## 02-B — Identité serveur, sans écriture PostgreSQL (16/10/2026)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-02-14 | Build | `dotnet restore MMV.slnx` ; `dotnet build MMV.slnx` | PASS | 0 avertissement / 0 erreur (analyseurs `latest-recommended`, warnings = erreurs ; CA1711 corrigé par `PermissionGrant`). NU1605 corrigé : `Configuration.EnvironmentVariables`/`UserSecrets` 10.0.4 → 10.0.13 |
| Q-02-15 | Tests API + domaine | `dotnet test tests/MMV.Api.Tests` | PASS | **70/70** (26 historiques + règle de référence `MMV.Maintenance` + 43 identité) : hachage V3, session opaque 43 car. stockée en SHA-256, refus neutres (inconnu/mauvais/désactivé), verrouillage 5 échecs/15 min, inactivité 30 min, durée absolue 8 h, déconnexion, désactivation, jetons malformés ; bootstrap créé une fois puis `AlreadyCompleted`, saisies invalides ; HTTP : 401 anonyme ×3 (`WWW-Authenticate: Bearer`), jeton forgé, 400 champs vides, 200 sans cookie ni hachage, admin 200, non-admin 200/403, permission retirée → 403, logout → 401, désactivation → 401, **HTTP simple → 403 `https_required`**, routes register/bootstrap/reset/clients → 404, 429 après 3 essais, préflight CORS `/api` (POST + `Authorization`, sans credentials), origine étrangère refusée, `/health/live` anonyme. Store = double en mémoire écrit à la main (pas EF InMemory) |
| Q-02-16 | Migration générée hors connexion | `dotnet ef migrations add AuthIdentity` ; `dotnet ef migrations list --no-connect` ; `dotnet ef migrations script` (×3) | PASS | `20261015223849_AuthIdentity` ; baseline 01B inchangée ; scripts et empreintes : `GATES_AND_DECISIONS.md` § O-02-01 |
| Q-02-17 | Tests PostgreSQL autorisés avant GO | `dotnet test MMV.slnx --filter "Stage!=PostMigration&Stage!=IdentityWrite"` | PASS | `MMV.PostgreSql.Tests` **11/11** (garde de cible ×5, identité dev/test, état connu dev/test = baseline seule, migrations de l'assembly, écriture temporaire + ROLLBACK 01B) |
| Q-02-18 | Tests post-migration avant GO | `dotnet test tests/MMV.PostgreSql.Tests --filter Stage=PostMigration` | FAIL attendu (`BLOCKED_OWNER_APPROVAL`) | « schémas : attendu [auth], obtenu [] » sur les deux bases |
| Q-02-19 | Tests d'identité PostgreSQL | `--filter Stage=IdentityWrite` | NOT_TESTED | écrits (`IdentityPostgreSqlTests`, `AuthHttpPostgreSqlTests`), non exécutés avant GO O-02-01 |
| Q-02-20 | Maintenance (lecture seule) | `dotnet run --project src/MMV.Maintenance -- status` ; `… -- bootstrap-admin < /dev/null` | PASS | `mmv_v2_dev`, 5433, PG 18.6 ; 1 migration en attente ; bootstrap refusé **code 2** (précondition) |
| Q-02-21 | Script d'application contrôlée | `scripts/Invoke-MmvSqlScript.ps1` avec empreinte fausse / base `postgres` | PASS (refus) | refus avant toute connexion |
| Q-02-22 | Serena MCP C# (nouveau code) | `find_referencing_symbols("…/AuthService/AuthenticateAsync")`, `("…/IIdentityStore")` | PASS | seul appelant de production : `SessionAuthenticationHandler.HandleAuthenticateAsync` (aucun contournement) ; consommateurs de `IIdentityStore` : `AuthService`, `BootstrapService`, `IdentityStore`, DI, `AuthEndpoints.ListAccountsAsync`, double de test |
| Q-02-23 | Serena TypeScript (CLI) | `serena project health-check .` | PASS (CLI) / MCP BLOCKED | LS TypeScript démarré (0,18 s) ; `GetSymbolsOverview(apps/desktop/vite.config.ts)` → `default`, `host`, **`version`** (constante ajoutée en 02-A) ; `evidence/P02_serena_typescript_cli_20261016T0046.log` |
| Q-02-24 | Graphify CLI | `graphify update .` ; `graphify path` ×3 | PASS | `SessionAuthenticationHandler → .HandleAuthenticateAsync() → AuthService → IIdentityStore ← IdentityStore` ; `LoginScreen() → useAuth() ← authContext.ts → auth.ts → requestJson()` ; `evidence/P02_graphify_paths_20261016T0045.log` |

## 02-C — Preuve native, partie indépendante de la base (16/10/2026)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-02-25 | Login natif : focus, tabulation, validation, zoom | `npx tauri dev` (WebView2 `--remote-debugging-port=9222`, loopback) ; `node e2e/run-native.mjs login-states` ; captures `PrintWindow` du processus `mmv-desktop` | PASS | focus initial Identifiant ; Tab ×3 + Maj+Tab ×3 ; Entrée champs vides → 2 messages ; zone cliente exacte 1920×1080 et 1440×900 ; **zoom WebView2 réel** 125 % et 150 % (`devicePixelRatio`) sans défilement horizontal. Écart constaté puis corrigé : l'émulation CDP (`setDeviceMetricsOverride`) ne se reflète pas dans la fenêtre native → abandonnée au profit de Ctrl +/− réels (O-02-09) |
| Q-02-26 | Login natif, API arrêtée | `node e2e/run-native.mjs login-api-down` | PASS | bandeau « Connexion impossible pour le moment. Veuillez réessayer. » |

Limite : les tests jsdom valident les états et le clavier **au niveau DOM**, pas le rendu WebView2 ni les cotes au pixel (preuve native : 02-C).
