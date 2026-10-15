# 01A — QA_MATRIX

Exécutions réelles du 15/10/2026, poste Windows 11 Home 10.0.26200 x64. Aucun résultat simulé.

Q-01 à Q-23 : livraison initiale de 01A, **PASS/FAIL selon exécution rapportée** à cette date ; ils sont conservés tels quels comme historique. Les entrées Q-24 et suivantes ont été ajoutées après la publication de la branche.

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-01 | Dépôt neuf | `git rev-parse --show-toplevel`, remote, SHA | PASS | `MMV V2`, `origin ali-zekhnini-vinci/MMV-V2`, base `bbf1749` |
| Q-02 | Outils | `node -v`, `dotnet --list-sdks`, `rustc -V`, vswhere, registre WebView2 | PASS | Node 22.23.3, SDK 10.0.402, Rust 1.99.0 MSVC, VS Community 2026 (VC x64), SDK Windows 10.0.26100.0, WebView2 155.0.4283.45 |
| Q-03 | PostgreSQL lecture seule | `Get-Service`, `Get-NetTCPConnection`, `pg_isready -p 5433`, `postgres --version` | PASS | PG18 18.6 sur 5433 `accepting connections` ; PG17 sur 5432 non touché |
| Q-04 | npm install / ci | `npm install`, puis `npm ci` | PASS | `found 0 vulnerabilities` |
| Q-05 | Lint | `npm run lint` | PASS | exit 0 ; fichiers : App.tsx, main.tsx, vite-env.d.ts, vite.config.ts, eslint.config.js |
| Q-06 | TypeScript strict + build | `npm run build` | PASS | `tsc` x2 puis `vite build` ; `dist/assets/index-*.js 220.78 kB` |
| Q-07 | Build Tauri natif | `npx tauri build --debug --no-bundle` | PASS | `Finished dev profile in 1m 35s` → `target\debug\mmv-desktop.exe` |
| Q-08 | Fenêtre Tauri Windows | lancement de l'exe, capture GDI de la fenêtre | PASS | PID 4208, titre `MMV V2 — socle technique`, 1296×839, processus `msedgewebview2.exe` avec `--user-data-dir=...\com.mmv.v2.dev\EBWebView` ; capture `evidence/P01-02_tauri_window_20261015T095419Z.png` SHA-256 `6687E91F…434DBB0F` |
| Q-09 | Contenu de la capture | analyse de pixels (lecture d'image bloquée par un hook local) | PASS | `#0066FF` 187 px, `#00C2A8` 507 px, `#0B1D3A` fond, `#11264A` carte : rendu thème sombre effectif |
| Q-10 | dotnet restore verrouillé | `dotnet restore MMV.slnx --locked-mode` | PASS | exit 0 |
| Q-11 | dotnet build | `dotnet build MMV.slnx` | PASS | `0 Avertissement(s) 0 Erreur(s)` |
| Q-12 | dotnet test | `dotnet test MMV.slnx` | PASS | `échec : 0, réussite : 10, total : 10` (1er essai FAIL : CA1707 noms de tests, corrigé par `tests/.editorconfig`) |
| Q-13 | `/health/live` HTTP | `dotnet run --launch-profile https` + `curl -i http://localhost:5022/health/live` | PASS | `HTTP/1.1 200 OK` `{"status":"Healthy","scope":"process","processId":20212,...}` ; PID 20212 = `MMV.Api` écoutant 5022/7267 |
| Q-14 | `/health/live` HTTPS | `curl -i https://localhost:7267/health/live` (validation TLS active) | NOT_VERIFIED | `SEC_E_UNTRUSTED_ROOT` : certificat de dev non approuvé. Aucun contournement, aucune approbation de certificat sans accord |
| Q-15 | OpenAPI | `curl http://localhost:5022/openapi/v1.json` | PASS | OpenAPI 3.1.1, `"paths": {}` (aucun CRUD fictif) |
| Q-16 | Fournisseur EF | `PersistenceProviderTests` | PASS | `Npgsql.EntityFrameworkCore.PostgreSQL`, aucune connexion ouverte |
| Q-17 | Persistance PostgreSQL réelle | — | NOT_TESTED | aucune base V2 isolée approuvée |
| Q-18 | Zéro SQLite/InMemory | grep code + lockfiles NuGet/npm/Cargo ; test `NoProjectReferencesPersistenceProviderOtherThanNpgsql` | PASS | aucun résultat ; seul `AddInMemoryCollection` (configuration, pas un provider) |
| Q-19 | Secrets | grep password/pwd/secret/api key/clé privée sur fichiers à committer | PASS | aucun résultat |
| Q-20 | `git diff --check` | après `.gitattributes` | PASS | exit 0 (FAIL initial : CRLF des générateurs) |
| Q-21 | Graphify | `graphify update .` puis `query`, `path`, `explain` | PASS | 292 nœuds, 298 arêtes, 30 communautés ; `Program.cs → MMV.Infrastructure → DependencyInjection → .AddInfrastructure() → MmvDbContext` |
| Q-22 | Serena | `claude mcp list` | BLOCKED | `serena: ... ⏸ Pending approval` ; aucun appel MCP réel possible |
| Q-23 | npm audit | `npm audit --omit=dev` | PASS | `found 0 vulnerabilities` |

### Vérifications ajoutées le 15/10/2026 (session de corrections, après publication de `f111dc0`)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-24 | Serena MCP — navigation symbolique C# | `mcp__serena__find_symbol(name_path_pattern="MmvDbContext", max_matches=3)` ; `mcp__serena__get_symbols_overview(relative_path="src/MMV.Infrastructure/DependencyInjection.cs")` ; `mcp__serena__list_memories()` | PASS (C# seulement) | `find_symbol` → `MMV.Infrastructure.Persistence/MmvDbContext` (Class) dans `src\MMV.Infrastructure\Persistence\MmvDbContext.cs` ; `get_symbols_overview` → `{"Namespace": ["MMV.Infrastructure"]}` ; `list_memories` → `{}`. Limites : `get_current_config` et `list_dir` non exposés dans le contexte `claude-code` (non appelés) ; TypeScript `NOT_VERIFIED`. Remplace Q-22 pour le statut courant ; appels non rejoués depuis |
| Q-25 | Graphify CLI / MCP | `graphify --version` ; `graphify path "Program.cs" "MmvDbContext"` sur le graphe AST local existant | CLI PASS ; MCP `NOT_CONFIGURED` | `graphify 0.9.77` ; chemin en 5 étapes `Program.cs → MMV.Infrastructure → DependencyInjection.cs → DependencyInjection → .AddInfrastructure() → MmvDbContext`. Aucune extraction LLM/cloud. `.mcp.json` ne déclare que `serena` |
| Q-26 | Rejeu backend | `dotnet restore MMV.slnx --locked-mode`, `dotnet build MMV.slnx`, `dotnet test MMV.slnx` | PASS (rejoué) | exit 0 ×3 ; `0 Avertissement(s)`, `0 Erreur(s)` ; `échec : 0, réussite : 10, total : 10` |
| Q-27 | Rejeu frontend | `npm ci`, `npm run lint`, `npm run build` (depuis `apps/desktop`) | PASS (rejoué) | exit 0 ×3 ; `found 0 vulnerabilities` ; `dist/assets/index-*.js 220.78 kB` |
| Q-28 | Build Tauri, fenêtre native, `/health/live`, OpenAPI, `npm audit` (Q-07, Q-08, Q-13, Q-15, Q-23) | — | `NOT_RERUN` | aucun code applicatif ni configuration de build modifié depuis ; résultats initiaux conservés |

## Non testé dans 01A (volontairement)

TLS LAN / CA, appairage, sessions, multi-postes, Windows Service, packaging/installateur, WebView2 hors ligne, sauvegarde/restauration, concurrence/idempotence, versionnement API, captures 1440×900/1920×1080.
