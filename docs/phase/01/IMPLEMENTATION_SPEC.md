# 01A — IMPLEMENTATION_SPEC

## Structure

```text
apps/desktop/                 Tauri 2 + React 19 + TS strict + Vite 8 (npm)
  src/App.tsx                 écran « socle technique » sans donnée ni appel réseau
  src-tauri/src/lib.rs        Builder Tauri nu : aucune commande, aucun plugin
  src-tauri/capabilities/     core:default uniquement
src/MMV.Domain/               vide (aucun domaine métier en 01A)
src/MMV.Application/          vide, référence Domain
src/MMV.Infrastructure/       MmvDbContext (sans entité) + AddInfrastructure (Npgsql)
src/MMV.Api/                  hôte unique : ProblemDetails, OpenAPI (dev), /health/live
tests/MMV.Api.Tests/          xUnit + WebApplicationFactory
```

## Références de projets (unidirectionnelles, testées)

```text
Api ──> Application ──> Domain
 └────> Infrastructure ──> Application
```

Domain ne référence rien. Test : `ProjectReferenceRulesTests` lit les `.csproj` réels.

## Choix techniques

- **Hôte unique** `MMV.Api`. `MMV.Server.Host` non créé : décision ouverte jusqu'au POC Windows Service (P01-10).
- **Vivacité** : `GET /health/live` = health check ASP.NET tagué `live`, JSON `{status, scope:"process", processId, startedAtUtc, uptimeSeconds, checkedAtUtc}`. Ne touche pas PostgreSQL ; ce n'est **pas** une preuve de disponibilité de la base.
- **Erreurs** : `AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages` (RFC 9457).
- **Journaux** : logging Microsoft par défaut ; EF Core en `Warning` ; aucune chaîne de connexion journalisée.
- **Persistance** : `ConnectionStrings:Mmv` (variable d'environnement `ConnectionStrings__Mmv` ou `dotnet user-secrets`), lue **à la première résolution** du `MmvDbContext`, jamais au démarrage. Absente → `InvalidOperationException` explicite. Aucune chaîne dans les `appsettings*.json`.
- **OpenAPI** : `Microsoft.AspNetCore.OpenApi`, document `/openapi/v1.json` en Development (vide en 01A, aucun CRUD fictif). Stratégie proposée pour 01B+ : génération du document au build (`Microsoft.Extensions.ApiDescription.Server`) puis `openapi-typescript` dans `packages/api-client/`. Non installé.
- **Tauri** : CSP stricte (`default-src 'self'`), pas d'origine HTTP distante autorisée tant que TLS (ADR-006) n'est pas décidé.
- **Polices** : Inter / Source Sans 3 non embarquées (repli `Segoe UI`) ; embarquement local à faire en phase 02.
- **Qualité .NET** : `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`. CA1707 désactivé dans `tests/` seulement.
- **Fins de ligne** : `.gitattributes` `* text=auto eol=lf` (les générateurs Windows produisent du CRLF).

## Versions épinglées

| Composant | Version | Verrou |
|---|---|---|
| .NET SDK | 10.0.402 (`rollForward: latestFeature`) | `global.json` |
| Microsoft.AspNetCore.OpenApi / Mvc.Testing | 10.0.13 | `Directory.Packages.props` + `packages.lock.json` |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 (EF Core 10.0.4, Npgsql résolus) | idem |
| xUnit / runner / Test SDK / coverlet | 2.9.3 / 3.1.4 / 17.14.1 / 6.0.4 | idem |
| Node / npm | 22.23.3 / 10.9.9 | environnement |
| React / React DOM | 19.3.1 | `package-lock.json` |
| TypeScript / Vite / plugin-react | 6.0.3 / 8.3.4 / 6.1.2 | idem |
| ESLint / typescript-eslint | 10.12.0 / 8.71.2 | idem |
| @tauri-apps/api / cli | 2.12.4 / 2.12.3 | idem |
| Rust / Cargo | 1.99.0 stable-x86_64-pc-windows-msvc | environnement |
| tauri / tauri-build / wry / webview2-com | 2.12.4 / 2.7.3 / 0.57.0 / 0.39.1 | `Cargo.lock` |
| WebView2 Runtime | 155.0.4283.45 | environnement |
| PostgreSQL (non utilisé) | 18.6 sur 5433 | environnement |

## Reproduction

```sh
# Backend (racine du dépôt)
dotnet restore MMV.slnx --locked-mode
dotnet build MMV.slnx
dotnet test MMV.slnx
dotnet run --project src/MMV.Api --launch-profile http   # http://localhost:5022/health/live

# Client
cd apps/desktop
npm ci && npm run lint && npm run build
npx tauri build --debug --no-bundle   # src-tauri/target/debug/mmv-desktop.exe

# Graphe local
graphify update .
graphify path "Program.cs" "MmvDbContext"
```
