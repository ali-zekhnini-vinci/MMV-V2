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

Limite : les tests jsdom valident les états et le clavier **au niveau DOM**, pas le rendu WebView2 ni les cotes au pixel (preuve native : 02-C).
