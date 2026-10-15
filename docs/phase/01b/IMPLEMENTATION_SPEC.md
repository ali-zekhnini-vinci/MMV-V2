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
