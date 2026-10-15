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
