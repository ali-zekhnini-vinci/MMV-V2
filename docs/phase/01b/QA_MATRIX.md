# 01B — QA_MATRIX

Exécutions réelles, poste Windows 11 Home 10.0.26200 x64. Statuts : PASS / FAIL / BLOCKED / NOT_TESTED / NOT_RERUN. Aucun résultat simulé.

## Préflight GATE 0 (15/10/2026)

| ID | Vérification | Commande / méthode | Résultat | Extrait |
|---|---|---|---|---|
| Q-B01 | Dépôt et merge 01A | `git rev-parse --show-toplevel`, `git remote -v`, `git fetch origin`, `git log origin/main`, `git merge-base --is-ancestor 79aa226 origin/main`, `git diff --stat 79aa226 origin/main` | PASS | `origin/main` = `4ed20a8e38b9db7519c0f56e24afaaaa4d6556ed` (parents `bbf1749`, `79aa226`) ; ancêtre : YES ; diff vide. `main` local était resté `bbf1749` (non modifié) |
| Q-B02 | Branche 01B | `git switch -c feat/phase-01b-integration --no-track 4ed20a8` | PASS | branche inexistante localement et sur `origin` avant création ; non-suivis `.claude/`, `.serena/`, prompt 01B préservés |
| Q-B03 | Outils | `ver`, `dotnet --version`, `rustc -V`, `node -v`, `npm -v`, `graphify --version`, `dotnet ef --version` | PASS | Windows 10.0.26200.9711, SDK 10.0.402, Rust 1.99.0, Node 22.23.3, npm 10.9.9, graphify 0.9.77, dotnet-ef 10.0.2 (global) |
| Q-B04 | PostgreSQL lecture seule (sans connexion) | `Get-Service postgresql*`, `Get-NetTCPConnection -LocalPort 5432,5433`, `pg_isready -h localhost -p 5433`, `postgres --version` | PASS | services `postgresql-x64-17` et `-18` Running ; PG18 écoute `0.0.0.0:5433` et `[::]:5433` (PID 8848, data `C:\Program Files\PostgreSQL\18\data`) ; PG17 `127.0.0.1:5432` non touché ; `accepting connections` ; 18.6 |
| Q-B04b | Catalogues PG18 (`SHOW port`, `current_user`, noms de bases/rôles) | `psql` | BLOCKED | aucun identifiant administrateur disponible pour l'agent (pas de pgpass ni `PGPASSWORD`) ; requiert le propriétaire |
| Q-B05 | Certificat HTTPS dev | `dotnet dev-certs https --check --trust` | BLOCKED (non approuvé) | exit 7 ; `6C48AE5E…3252 - CN=localhost - Valid 2026-02-08 → 2027-02-08 - IsHttpsDevelopmentCertificate: true` ; « none of them is trusted ». Aucune installation |
| Q-B06 | Serena MCP C# | `mcp__serena__find_referencing_symbols("DependencyInjection/AddInfrastructure", "src/MMV.Infrastructure/DependencyInjection.cs")` | PASS (C#) | 3 références : `src\MMV.Api\Program.cs` l.8 ; `PersistenceProviderTests.DbContextUsesNpgsqlProvider` ; `…MissingConnectionStringFailsExplicitlyOnResolution`. Serveur actif : `Active language servers: csharp` |
| Q-B06b | Serena MCP TypeScript | — | NOT_TESTED | `.serena/project.yml` (local) ne déclare que `csharp` ; décision O-B05 |
| Q-B07 | Graphify CLI | `graphify path "Program.cs" "MmvDbContext"` (graphe AST local 01A) | PASS (CLI) | 5 sauts `Program.cs → MMV.Infrastructure ← DependencyInjection.cs → DependencyInjection → .AddInfrastructure() → MmvDbContext`. MCP `NOT_CONFIGURED` |

## Implémentation (à venir)

Readiness, migrations, tests PG réels, HTTPS, CORS/CSP, client Tauri, captures : `NOT_TESTED` à ce stade.
