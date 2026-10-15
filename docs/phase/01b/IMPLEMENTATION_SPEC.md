# 01B — IMPLEMENTATION_SPEC

**Statut :** spécification **proposée** avant codage (15/10/2026). Les sections marquées « après approbation » ne sont pas exécutées.

## Architecture visée (POC monoposte local, P01-05)

```text
Fenêtre Tauri (WebView2)  --HTTPS vérifié-->  MMV.Api (https://localhost:7267, loopback)
  apps/desktop/src/api/                         GET /health/live   (processus seul)
  aucun accès PostgreSQL                        GET /health/ready  (connexion PG réelle)
                                                       │ Npgsql / EF Core 10
                                                       ▼
                                          PostgreSQL 18.6 localhost:5433
                                          bases mmv_v2_dev (API) / mmv_v2_test (tests)
```

## Endpoints

| Endpoint | Contrat | Dépendance |
|---|---|---|
| `GET /health/live` | inchangé (01A) : `{status, scope:"process", processId, startedAtUtc, uptimeSeconds, checkedAtUtc}` | aucune ; reste 200 si PG tombe |
| `GET /health/ready` | 200 `{status:"Healthy", scope:"database", checkedAtUtc, durationMs}` ; 503 `{status:"Unhealthy", scope:"database", …}` | `MmvDbContext.Database.CanConnectAsync` (ouverture réelle de connexion Npgsql), timeout borné (3 s), annulation propagée. **Aucun** message d'exception, nom de base, hôte, chaîne de connexion dans la réponse. Ne prouve pas l'état des migrations |

Choix : health check personnalisé (`IHealthCheck`) plutôt que le package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` — même mécanisme (`CanConnectAsync`), aucune dépendance ajoutée. Chaîne absente → `Unhealthy` (503), pas de plantage.

## Configuration et secrets

- API (dev) : `ConnectionStrings:Mmv` via `dotnet user-secrets` du projet `MMV.Api` (saisi **par le propriétaire**, jamais vu ni journalisé par l'agent).
- Tests PG : clé distincte, ciblant **uniquement** `mmv_v2_test` ; le setup vérifie `current_database() = 'mmv_v2_test'`, `inet_server_port() = 5433`, `server_version_num` ≥ 180000 et refuse toute autre cible.
- Aucun secret dans Git, `appsettings*`, preuves, captures, `VITE_*`.

## PostgreSQL — après approbation uniquement

Plan SQL exact et privilèges : `GATES_AND_DECISIONS.md` § B-SQL. Schéma : migration EF Core initiale **vide** (baseline technique, ne crée que `__EFMigrationsHistory`) — aucune table métier ni fictive. Test transactionnel : table **temporaire** créée dans une transaction sur `mmv_v2_test`, écriture/lecture puis `ROLLBACK` (aucune donnée persistante). Migrations : script SQL généré (`dotnet ef migrations script --idempotent`) relu avant application ; jamais `EnsureCreated()`, jamais au démarrage de l'API.

## HTTPS local (pas TLS LAN)

Profil `https` réel : `https://localhost:7267;http://localhost:5022`. Certificat `dotnet dev-certs` `CN=localhost`, empreinte `6C48AE5E0BD7B0E67032257F893C94EC27CC3252`, valide jusqu'au 2027-02-08, **non approuvé** au préflight. Aucun contournement TLS. ADR-006 reste OPEN.

## Client Tauri, CSP, CORS (à implémenter)

- `apps/desktop/src/api/` : client typé, `AbortController`, timeout, erreurs bornées ; base `VITE_MMV_API_BASE_URL` (dev uniquement, non secrète).
- CSP : `app.security.devCsp` ajoute `https://localhost:7267` à `connect-src` **en dev seulement** ; `csp` de production inchangée.
- CORS : origines réelles WebView2 relevées puis autorisées explicitement, GET seulement, environnement Development.

## Versions

Inchangées par rapport à `docs/phase/01/IMPLEMENTATION_SPEC.md` au préflight 01B (SDK 10.0.402, Node 22.23.3 / npm 10.9.9, Rust 1.99.0, PG 18.6). `dotnet-ef` global 10.0.2 présent ; tout nouvel outil/package sera justifié ici.

## Amendement daté — 15/10/2026 : implémentation (sans écriture PostgreSQL)

La spécification ci-dessus reste l'état proposé au préflight ; cette section décrit ce qui est **réellement implémenté et testé**.

### Readiness

- `src/MMV.Api/Health/ReadinessEndpoint.cs` : `GET /health/ready`, check `DatabaseReadinessCheck` (tag `ready`, timeout du check 3 s). Réponse `{status, scope:"database", checkedAtUtc, durationMs}` ; 200 si `Healthy`, 503 sinon ; aucun autre champ (testé).
- Le check est dans `MMV.Api` (pas `MMV.Infrastructure`) : `IHealthCheck` est fourni par le framework ASP.NET Core, l'ajouter à Infrastructure exigerait une dépendance supplémentaire.
- `src/MMV.Infrastructure/Persistence/DatabaseProbe.cs` : `CanConnectWithinAsync` plafonne le `Timeout` de connexion Npgsql à 2 s pour la sonde. **Constat** : le seul timeout du check ne coupait pas l'ouverture Npgsql face à un serveur muet (30 s mesurées, Q-B08). Une chaîne absente → 503 (pas de 500).
- `/health/live` inchangé dans son contrat ; `MapLiveness` retourne désormais le builder d'endpoint (pour `RequireCors`).

### CORS (`src/MMV.Api/Cors/DesktopCors.cs`)

Politique `desktop` appliquée **uniquement** à `/health/live` et `/health/ready` : origines exactes de `Cors:AllowedOrigins`, GET seul, sans identifiants. Origines **candidates** en `appsettings.Development.json` : `http://localhost:1420` (Vite dev) et `http://tauri.localhost` (bundle Tauri 2 Windows). Aucune origine hors Development. Démarrage refusé pour `*`, joker ou origine non exacte. L'Origin réel émis par WebView2 reste à relever depuis la fenêtre native (`NOT_TESTED`).

### Client Tauri (`apps/desktop/src/api/`)

- `client.ts` : `getJson` (GET, `Accept: application/json`, `cache: no-store`, `credentials: omit`, `redirect: error`), `AbortController` + timeout 8 s, erreurs `not-configured | network | timeout | aborted | http | invalid-response` à messages fixes (aucun contenu serveur relayé).
- `health.ts` : contrats `LiveStatus` / `ReadyStatus` ; `parseReady` refuse 200 non `Healthy` et 503 non `Unhealthy` ; `checkTechnicalStatus` → `api-unreachable | database-unavailable | ready` (live puis ready).
- `config.ts` : `VITE_MMV_API_BASE_URL` doit être une origine HTTPS seule (pas de repli HTTP). Valeur de dev non secrète dans `apps/desktop/.env.development` (exception explicite dans `.gitignore`), chargée par `vite` en mode dev, **absente du bundle de production** (vérifié : 0 occurrence de `localhost:7267` dans `dist/assets/*.js`). En production, l'écran affiche « Serveur MMV non configuré » ; le choix DNS/IP n'est pas implémenté.
- `App.tsx` : écran socle temporaire : chargement, non configuré, API injoignable, API joignable + base indisponible, API + base disponibles, bouton « Revérifier » ; une vérification au montage puis uniquement à la demande (pas de sondage).
- CSP : `app.security.devCsp` = CSP de production + `https://localhost:7267` dans `connect-src` ; `csp` de production **inchangée**.

### Tests

- `tests/MMV.Api.Tests/ReadinessEndpointTests.cs` : chaîne absente, port fermé (aucune fuite d'hôte/port/base/mot de passe), serveur muet (borne ≤ 5 s), liveness découplée, annulation. Chaque test fixe `ConnectionStrings:Mmv` lui-même : un user-secret de dev n'est jamais utilisé.
- `tests/MMV.Api.Tests/DesktopCorsTests.cs` : préflight autorisé, origines étrangères, méthode non GET, refus des jokers.
- `tests/MMV.PostgreSql.Tests/` (nouveau) : `PostgreSqlTarget` refuse **sans se connecter** toute chaîne hors `localhost|127.0.0.1|::1`, port 5433, base et rôle attendus ; `TargetIdentityTests` relit en **lecture seule** `current_database()`, `current_user`, `inet_server_port()`, `server_version_num`, attributs du rôle, propriété de la base et `CONNECT` de `PUBLIC`. Sans secret, ces tests sont **ignorés (Skipped = NOT_TESTED)**, jamais « réussis ». Aucun test d'écriture (O-B02).
- `apps/desktop/src/api/health.test.ts` : `node --test` (Node 22, type-stripping natif) contre un vrai serveur `node:http` local ; pas de mock de `fetch`. Ne constitue pas une preuve E2E.

### Migration EF Core

- `src/MMV.Infrastructure/Persistence/MmvDesignTimeDbContextFactory.cs` : contexte design-time **sans chaîne de connexion** → `dotnet ef` ne lit aucun secret et ne se connecte jamais.
- `src/MMV.Infrastructure/Persistence/Migrations/20261015125000_InitialBaseline*.cs` + `MmvDbContextModelSnapshot.cs` : migration vide (`Up`/`Down` vides), EF Core 10.0.4.
- Scripts pour revue : `docs/phase/01b/sql/` ; analyse d'impact et procédure : `GATES_AND_DECISIONS.md` § O-B07. **Non appliqués.**

Commandes (hors connexion) :

```sh
dotnet ef migrations add InitialBaseline --project src/MMV.Infrastructure --startup-project src/MMV.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations list --project src/MMV.Infrastructure --startup-project src/MMV.Infrastructure --no-connect
dotnet ef migrations script --idempotent --project src/MMV.Infrastructure --startup-project src/MMV.Infrastructure -o docs/phase/01b/sql/0001_InitialBaseline.idempotent.sql
```

Piège rencontré : `migrations script --no-build` juste après `migrations add` produit un script **sans** la migration (assembly non recompilé) ; toujours laisser `dotnet ef` reconstruire.

### Secrets — procédure du propriétaire

Emplacements (hors dépôt) : `%APPDATA%\Microsoft\UserSecrets\mmv-v2-api\secrets.json` (clé `ConnectionStrings:Mmv`, base `mmv_v2_dev`, lue par l'API en Development) et `…\mmv-v2-postgresql-tests\secrets.json` (clé `ConnectionStrings:MmvTest`, base `mmv_v2_test`, lue par `MMV.PostgreSql.Tests` seulement). Alternative CI : variables d'environnement `ConnectionStrings__Mmv` / `ConnectionStrings__MmvTest`.

À exécuter **par le propriétaire** dans son propre terminal PowerShell. Le mot de passe est saisi masqué et n'apparaît ni dans une ligne de commande, ni dans l'historique PSReadLine, ni dans les arguments ou l'entrée standard d'un processus : la fonction écrit directement le fichier `secrets.json` géré par `dotnet user-secrets` (UTF-8 sans BOM), en conservant les clés existantes.

```powershell
function Set-MmvSecret([string]$Id, [string]$Key, [string]$Name) {
    $secure = Read-Host -AsSecureString "Mot de passe du rôle $Name"
    $b = [System.Data.Common.DbConnectionStringBuilder]::new()
    $b['Host'] = 'localhost'; $b['Port'] = '5433'; $b['Database'] = $Name; $b['Username'] = $Name
    $b['Password'] = [System.Net.NetworkCredential]::new('', $secure).Password
    $dir = Join-Path $env:APPDATA "Microsoft\UserSecrets\$Id"
    $file = Join-Path $dir 'secrets.json'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $data = [ordered]@{}
    if (Test-Path $file) { (Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $data[$_.Name] = $_.Value } }
    $data[$Key] = $b.ConnectionString
    [System.IO.File]::WriteAllText($file, ($data | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))
    $b.Clear(); Remove-Variable secure, b, data
}
Set-MmvSecret 'mmv-v2-api' 'ConnectionStrings:Mmv' 'mmv_v2_dev'
Set-MmvSecret 'mmv-v2-postgresql-tests' 'ConnectionStrings:MmvTest' 'mmv_v2_test'

# Contrôle sans afficher de valeur : noms de clés uniquement
foreach ($id in 'mmv-v2-api', 'mmv-v2-postgresql-tests') {
    "$id : " + ((Get-Content "$env:APPDATA\Microsoft\UserSecrets\$id\secrets.json" -Raw | ConvertFrom-Json).PSObject.Properties.Name -join ', ')
}
```

Écart constaté (Q-B11) : `… | dotnet user-secrets set` (JSON par l'entrée standard) échoue sur ce poste (`'0xEF' is an invalid start of a value` : BOM ajouté au flux), d'où l'écriture directe. La procédure retenue a été validée sur un identifiant jetable `mmv-v2-scratch-probe` avec une valeur factice contenant `;`, `'`, `"` et `é` (aller-retour exact, clés relues par `dotnet user-secrets list`, puis suppression). Le fichier `secrets.json` est en clair dans le profil Windows de l'utilisateur (comportement standard des user-secrets, développement uniquement).

Ne pas utiliser `dotnet user-secrets list` (affiche les valeurs) ni `dotnet user-secrets set <clé> <valeur>` (valeur dans l'historique). Puis, côté agent : `dotnet test tests/MMV.PostgreSql.Tests` (lecture seule).

## Amendement daté — 15/10/2026 (soir) : terminaison 01B

Les sections précédentes restent historiques (dont « Non appliqués » et `node --test`, désormais remplacés comme suit).

### PostgreSQL

- Migration `20261015125000_InitialBaseline` **appliquée par le propriétaire** sur `mmv_v2_dev` et `mmv_v2_test` (script idempotent approuvé, exit 0 sur chaque base ; NOTICE « existe déjà » sur TEST expliqué dans `GATES_AND_DECISIONS.md`). L'agent n'a rien rejoué.
- PostgreSQL 18 enregistre les contraintes `NOT NULL` dans `pg_constraint` (`<table>_<colonne>_not_null`) : l'inventaire attendu de `SchemaInspection.BaselineDifferences` les inclut.
- Ordre d'exécution des tests réels : `--filter "Stage!=Write"` (lecture seule, session `default_transaction_read_only=on`) puis `--filter "Stage=Write"` (`mmv_v2_test` uniquement). `dotnet test MMV.slnx` exécute l'ensemble, écriture éphémère comprise, dès que `ConnectionStrings:MmvTest` est configurée.

### API

- Profil `https` : `https://localhost:7267` **et** `http://localhost:5022` (loopback, hérité de 01A) ; le client n'emploie que HTTPS (O-B10 pour retirer l'écoute HTTP).
- `/health/live` expose `processId`, `startedAtUtc`, `uptimeSeconds` (contrat 01A, aucune donnée d'infrastructure ni secret).
- Journalisation CORS détaillée possible pour diagnostic : variable d'environnement `Logging__LogLevel__Microsoft.AspNetCore.Cors=Debug` (non versionnée).

### Client Tauri

- `apps/desktop/src/technicalStatusView.ts` : libellés de l'écran (résumé + détails live/ready) extraits de `App.tsx` sans changement visuel, pour être testés hors DOM.
- Origin réel de WebView2 en développement : `http://localhost:1420` (`devUrl`). `http://tauri.localhost` reste une origine **candidate** pour un bundle : non exercée, le bundle n'ayant pas d'adresse d'API (O-B11).

### Tests frontend — Vitest

- `npm test` = `vitest run` (configuration `test` dans `apps/desktop/vite.config.ts`, `defineConfig` importé de `vitest/config`, environnement `node`, `src/**/*.test.ts`).
- `vitest` 5.0.3 épinglé + `"overrides": { "vitest": "5.0.3" }` : sans cet override, npm 10.9.9 plante (`edgesOut`) en résolvant `vitest@*` demandé en pair optionnel par `@vitejs/devtools` (pair optionnel de `vite` 8.3.4). Vitest 5.0.3 : `vite ^6.4 || ^7 || ^8`, Node `^22.12 || ^24 || >=26` (poste : Node 22.23.3).
- Les tests de `health.test.ts` sont inchangés sur le fond (vrai serveur `node:http`, `node:assert/strict`) ; seuls les imports de cycle de vie passent de `node:test` à `vitest`.

### Build Windows

- `npx tauri build --no-bundle` (via `npm run tauri build -- --no-bundle`) produit `src-tauri/target/release/mmv-desktop.exe` sans installateur (installateur hors 01B).
