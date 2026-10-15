# 01B — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 uniquement)

| Source | Usage |
|---|---|
| `prompts/PHASE01B_PROMPT_CLAUDE_CODE.md` | Mandat 01B (GO du 15/10/2026), versionné avec la phase |
| `README.md`, `CLAUDE.md`, `.mcp.json` | État, DOCUMENTATION GATE, déclaration MCP (Serena seul) |
| `docs/phase/00.md`, `docs/phase/01.md`, `docs/phase/01/*` | Gates et décisions 00/01A (O-01 base V2, O-03 certificat, O-02/O-04 outillage) |
| `docs/ADR_ET_DECISIONS.md` | D-003/ADR-003 PostgreSQL seul ; ADR-006 TLS LAN `OPEN DECISION` ; ADR-007 sessions `PROPOSED` |
| `docs/ARCHITECTURE_ET_STACK.md` | Topologie client → API → PG, pas d'accès DB côté client |
| `docs/INSTALLATION_RESEAU_SECURITE.md` | TLS validé sans contournement ; migrations jamais automatiques au démarrage ; `dotnet dev-certs` ≠ certificat de production |
| `docs/PLAN_POC_ET_PREUVES.md` | IDs P01-04, P01-05, P01-07, P01-15, P01-16 |
| `docs/PERIMETRE_ET_SEQUENCE.md` | Phase 01 : slice minimal client ↔ API ↔ PostgreSQL réel |
| Sources du socle 01A | `src/MMV.Api/**`, `src/MMV.Infrastructure/**`, `tests/MMV.Api.Tests/**`, `apps/desktop/src/**`, `apps/desktop/src-tauri/**` |

Aucune lecture de l'ancien dépôt, code, base ou règles MMV.

## Traçabilité exigence → chemin (complétée au fil de l'implémentation)

| Exigence 01B | Chemin(s) | Statut |
|---|---|---|
| Liveness indépendant de PG | `src/MMV.Api/Health/LivenessEndpoint.cs` | existant (01A) |
| `GET /health/ready` sur vraie connexion PG18 | à créer : `src/MMV.Api/Health/ReadinessEndpoint.cs`, check dans `src/MMV.Infrastructure/` | NOT_STARTED |
| Migration EF Core initiale (technique) | à créer : `src/MMV.Infrastructure/Persistence/Migrations/` | BLOCKED_OWNER_APPROVAL |
| Tests PG réels sur `mmv_v2_test` | à créer : projet de tests d'intégration PostgreSQL | BLOCKED_OWNER_APPROVAL |
| HTTPS local `https://localhost:7267` | `src/MMV.Api/Properties/launchSettings.json` | certificat non approuvé (Q-B05) |
| Client API typé Tauri | à créer : `apps/desktop/src/api/` | NOT_STARTED |
| CSP `devCsp` / CORS | `apps/desktop/src-tauri/tauri.conf.json`, `src/MMV.Api/Program.cs` | NOT_STARTED |

## Outils utilisés (CLI vs MCP distingués)

| Outil | Mode | Usage 01B |
|---|---|---|
| Serena | MCP (`.mcp.json`, serveur `serena`) | C# : `find_referencing_symbols` (Q-B06). TypeScript : non déclaré dans `.serena/project.yml` local |
| Graphify | CLI 0.9.77, graphe AST local | `graphify path` (Q-B07). Graphify MCP `NOT_CONFIGURED` |

## Documentation officielle de référence

- ASP.NET Core health checks : https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
- ASP.NET Core CORS : https://learn.microsoft.com/aspnet/core/security/cors
- `dotnet dev-certs` : https://learn.microsoft.com/dotnet/core/tools/dotnet-dev-certs
- EF Core migrations (scripts SQL) : https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying
- Tauri 2 CSP / `devCsp` : https://v2.tauri.app/security/csp/

## Amendement daté — 15/10/2026 : traçabilité après implémentation sans écriture PostgreSQL

Mandat complémentaire : [`prompts/PHASE01B_VALIDATION_POSTGRESQL_SQL_REVIEW.md`](../../../prompts/PHASE01B_VALIDATION_POSTGRESQL_SQL_REVIEW.md) (O-B01/O-B03 déclarés PASS par le propriétaire). Le tableau « Traçabilité » ci-dessus reste l'état du préflight.

| Exigence 01B | Chemin(s) réel(s) | Statut | Preuve |
|---|---|---|---|
| Liveness indépendant de PG | `src/MMV.Api/Health/LivenessEndpoint.cs` | PASS | Q-B08, Q-B13 |
| `GET /health/ready` borné, sans fuite | `src/MMV.Api/Health/ReadinessEndpoint.cs`, `src/MMV.Infrastructure/Persistence/DatabaseProbe.cs`, `tests/MMV.Api.Tests/ReadinessEndpointTests.cs` | PASS sans base (503) ; 200 sur PG réel NOT_TESTED | Q-B08, Q-B13 |
| CORS restreint | `src/MMV.Api/Cors/DesktopCors.cs`, `src/MMV.Api/Program.cs`, `src/MMV.Api/appsettings.Development.json`, `tests/MMV.Api.Tests/DesktopCorsTests.cs` | PASS (tests) ; Origin WebView2 réel NOT_TESTED | Q-B08, Q-B13 |
| Secrets hors Git | `src/MMV.Api/MMV.Api.csproj` (`UserSecretsId` `mmv-v2-api`), `tests/MMV.PostgreSql.Tests/MMV.PostgreSql.Tests.csproj` (`mmv-v2-postgresql-tests`) ; procédure : `IMPLEMENTATION_SPEC.md` § Secrets | procédure PASS (valeur factice) ; saisie réelle en attente du propriétaire | Q-B11 |
| Vérification d'identité avant écriture | `tests/MMV.PostgreSql.Tests/PostgreSqlTarget.cs`, `tests/MMV.PostgreSql.Tests/TargetIdentityTests.cs` | garde PASS ; identité NOT_TESTED | Q-B09 |
| Migration EF Core initiale vide | `src/MMV.Infrastructure/Persistence/MmvDesignTimeDbContextFactory.cs`, `src/MMV.Infrastructure/Persistence/Migrations/` | générée, **non appliquée** (O-B07) | Q-B10 |
| Script SQL pour revue | `docs/phase/01b/sql/0001_InitialBaseline.idempotent.sql`, `docs/phase/01b/sql/0001_InitialBaseline.sql` | OWNER_APPROVAL_REQUIRED | Q-B10, `GATES_AND_DECISIONS.md` § O-B07 |
| HTTPS local `https://localhost:7267` | `src/MMV.Api/Properties/launchSettings.json` (inchangé) | PASS hors Tauri | Q-B12, Q-B13 |
| Client API typé Tauri | `apps/desktop/src/api/client.ts`, `apps/desktop/src/api/health.ts`, `apps/desktop/src/api/config.ts`, `apps/desktop/src/App.tsx`, `apps/desktop/src/vite-env.d.ts`, `apps/desktop/.env.development` | PASS (tests locaux) ; fenêtre native NOT_TESTED | Q-B14 |
| Tests frontend | `apps/desktop/src/api/health.test.ts`, `apps/desktop/tsconfig.test.json`, script `npm test` | PASS 17/17 | Q-B14 |
| CSP dev | `apps/desktop/src-tauri/tauri.conf.json` (`devCsp`) | configurée ; effet en fenêtre NOT_TESTED | — |

Dépendances ajoutées (NuGet, versions centralisées dans `Directory.Packages.props`, licences MIT) : `Microsoft.EntityFrameworkCore.Design` 10.0.4 (`PrivateAssets=all`, design-time seulement), `Microsoft.Extensions.Configuration.UserSecrets` 10.0.4 et `Microsoft.Extensions.Configuration.EnvironmentVariables` 10.0.4 (projet de tests PG uniquement). Aucune dépendance npm ajoutée.

| Outil | Mode | Usage (amendement) |
|---|---|---|
| Serena | MCP | C# : Q-B15 PASS. TypeScript : Q-B16 BLOCKED (redémarrage MCP requis) |
| Graphify | CLI 0.9.77 | `graphify update .` + 2 `graphify path` sur le nouveau flux (Q-B17). MCP `NOT_CONFIGURED` |
| `dotnet-ef` | CLI global 10.0.2 | génération hors connexion (Q-B10) |

## Amendement daté — 15/10/2026 (soir) : traçabilité finale 01B

Mandat : [`prompts/PHASE01B_GO_FINAL_TERMINAISON.md`](../../../prompts/PHASE01B_GO_FINAL_TERMINAISON.md). Les tableaux précédents restent l'état historique. Statuts détaillés : `QA_MATRIX.md` Q-B18..Q-B28.

| Exigence 01B | Chemin(s) réel(s) | Statut | Preuve |
|---|---|---|---|
| Liveness indépendant de PG | `src/MMV.Api/Health/LivenessEndpoint.cs` | PASS (200 même PG injoignable) | Q-B20 |
| `GET /health/ready` réel, borné, sans fuite | `src/MMV.Api/Health/ReadinessEndpoint.cs`, `src/MMV.Infrastructure/Persistence/DatabaseProbe.cs` | PASS : 200 sur PG18, 503 port fermé / serveur muet (~2,3 s) | Q-B20 |
| Migration baseline appliquée (propriétaire) et cohérente EF Core | `src/MMV.Infrastructure/Persistence/Migrations/`, `docs/phase/01b/sql/0001_InitialBaseline.idempotent.sql` | PASS (1 ligne / base, `pending=[]`) | Q-B18 |
| Aucune migration au démarrage | `src/MMV.Api/Program.cs` (aucun `Migrate`/`EnsureCreated`) | PASS | Q-B21 |
| Tests PG réels (identité, schéma, écriture + `ROLLBACK`) | `tests/MMV.PostgreSql.Tests/PostgreSqlTarget.cs`, `SchemaInspection.cs`, `SchemaStateTests.cs`, `TargetIdentityTests.cs`, `TransactionalRollbackTests.cs` | PASS 12/12 | Q-B18, Q-B19, Q-B25 |
| CORS restreint, Origin WebView2 réel | `src/MMV.Api/Cors/DesktopCors.cs`, `src/MMV.Api/appsettings.Development.json` | PASS (`http://localhost:1420` relevé ; refus tracé) | Q-B20, Q-B22 |
| Client API + écran socle | `apps/desktop/src/api/client.ts`, `health.ts`, `config.ts`, `apps/desktop/src/technicalStatusView.ts`, `apps/desktop/src/App.tsx` | PASS (fenêtre native, 4 états) | Q-B22 |
| CSP dev / prod | `apps/desktop/src-tauri/tauri.conf.json` (`devCsp` / `csp`) | PASS (dev : appel autorisé ; release : aucune URL d'API) | Q-B22, Q-B23 |
| Tests frontend (Vitest) | `apps/desktop/src/api/health.test.ts`, `apps/desktop/src/technicalStatusView.test.ts`, `apps/desktop/vite.config.ts` (`test`), `apps/desktop/package.json` | PASS 25/25 | Q-B24 |
| Exécutable Windows | `apps/desktop/src-tauri/target/release/mmv-desktop.exe` (non versionné) | PASS (x64 GUI, lancé) | Q-B23 |

Dépendance npm ajoutée : `vitest` **5.0.3** (devDependency épinglée, MIT) + `overrides.vitest = 5.0.3` (nécessaire avec npm 10.9.9, voir Q-B24). Aucune dépendance NuGet ajoutée par cet amendement.

| Outil | Mode | Usage (terminaison) |
|---|---|---|
| Serena | MCP + CLI | C# MCP PASS ; TypeScript PASS via `serena project health-check` ; MCP TS à reconnecter (Q-B26) |
| Graphify | CLI 0.9.77 | `graphify update .` + 6 `graphify path` (Q-B27). MCP `NOT_CONFIGURED` |
| `psql` | — | non utilisé par l'agent (migration appliquée par le propriétaire) |

## Amendement daté — 15/10/2026 (nuit) : corrections après audit externe

| Élément | Chemin | Statut | Preuve |
|---|---|---|---|
| Preuve native release | `docs/phase/01b/evidence/P01-05_tauri-native_release_not-configured_20261015T214925Z.png` (remplace la capture `…212901Z.png`, invalide, retirée) | PASS | Q-B23-C |
| Journaux de preuve anonymisés | `docs/phase/01b/evidence/*.log` (`<REPO>`, `<SCRATCHPAD>`) | PASS | Q-B29 |
| Outil local `dotnet-ef` | `.config/dotnet-tools.json` (10.0.4, versionné ; remplace l'usage du CLI global 10.0.2 noté plus haut, qui reste installé) | PASS | Q-B30 |
