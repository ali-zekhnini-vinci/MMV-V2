# MMV V2 — logiciel neuf de gestion d'un magasin d'optique

## Statut actuel — phase 01B (15/10/2026)

**`01B IMPLEMENTED — OWNER_REVIEW_REQUIRED`** sur la branche `feat/phase-01b-integration` (publiée sur `origin` jusqu'à `6cf6e88` ; commit de corrections d'audit local, non poussé ; aucune PR, non mergée). Audit externe : socle technique **conforme pour le développement local monoposte** ; corrections d'audit appliquées (preuve release native, confidentialité des journaux, outil `dotnet-ef` local).

Chaîne démontrée en développement local : **fenêtre Tauri → HTTPS → MMV.Api → PostgreSQL 18**.

| Élément | État final 01B |
|---|---|
| PostgreSQL 18.6 `localhost:5433` | bases/rôles `mmv_v2_dev` et `mmv_v2_test` (créés par le propriétaire) ; migration baseline appliquée par le propriétaire, cohérente avec EF Core ; jamais au démarrage |
| Tests | .NET : `MMV.Api.Tests` 26/26, `MMV.PostgreSql.Tests` 12/12 (identité, schéma, écriture éphémère + `ROLLBACK` sur `mmv_v2_test`) ; frontend : Vitest 5.0.3, 25/25 |
| `GET /health/live` / `/health/ready` | HTTPS réel : 200 / 200 avec PG18 ; 200 / 503 si PG injoignable ou muet (borné ~2,3 s) ; aucune fuite |
| Fenêtre Tauri native | 4 états réels (ready, base indisponible, CORS refusé, API arrêtée) ; exécutable release x64 sans installateur : « Serveur MMV non configuré » (aucune adresse de production) |
| Outillage | `dotnet-ef` 10.0.4 en outil local (`.config/dotnet-tools.json`, `dotnet tool restore`) |

**Non validé :** TLS LAN/CA/rotation (ADR-006 ouvert), adresse du serveur en production (O-B11), multi-postes, installateur, Windows Service, sauvegardes, rôle applicatif séparé (O-B04). Session MCP Serena TypeScript à reconnecter ; Graphify MCP `NOT_CONFIGURED`. ADR-004/006/007 non ratifiés. Phase 02 `NOT_STARTED`.

Détail, preuves et états historiques : [`docs/phase/01.md`](docs/phase/01.md), [`docs/phase/01b/QA_MATRIX.md`](docs/phase/01b/QA_MATRIX.md), [`docs/phase/01b/GATES_AND_DECISIONS.md`](docs/phase/01b/GATES_AND_DECISIONS.md). Phases antérieures : 00 (cadrage) livrée ; 01A **MERGED** dans `main` (`4ed20a8e38b9db7519c0f56e24afaaaa4d6556ed`), voir [`docs/phase/01/QA_MATRIX.md`](docs/phase/01/QA_MATRIX.md).

**Nature du code :** socle technique minimal, **non** destiné à la production. Aucun module métier, aucune donnée de magasin.

## Contenu du dépôt

| Élément | Chemin |
|---|---|
| Client Windows Tauri 2 + React 19 + TypeScript strict + Vite (npm) | `apps/desktop/` (client API : `apps/desktop/src/api/`) |
| Solution .NET 10 | `MMV.slnx`, `src/MMV.Domain`, `src/MMV.Application`, `src/MMV.Infrastructure`, `src/MMV.Api` |
| Santé (`/health/live`, `/health/ready`), CORS restreint | `src/MMV.Api/Health/`, `src/MMV.Api/Cors/DesktopCors.cs` |
| Migration baseline vide et script SQL relu | `src/MMV.Infrastructure/Persistence/Migrations/`, `docs/phase/01b/sql/` |
| Tests | `tests/MMV.Api.Tests/`, `tests/MMV.PostgreSql.Tests/`, `apps/desktop/src/**/*.test.ts` |
| Documentation de phase | `docs/phase/`, preuves dans `docs/phase/01/` et `docs/phase/01b/evidence/` |

## Exécuter localement

Prérequis (versions exactes : [`docs/phase/01/IMPLEMENTATION_SPEC.md`](docs/phase/01/IMPLEMENTATION_SPEC.md)) : Windows 11 x64, SDK .NET 10.0.402, Node 22 / npm 10, Rust stable MSVC x64, WebView2.

```sh
# Backend (racine du dépôt)
dotnet tool restore                    # dotnet-ef 10.0.4 local
dotnet restore MMV.slnx --locked-mode
dotnet build MMV.slnx
dotnet test MMV.slnx
dotnet run --project src/MMV.Api --launch-profile http   # puis http://localhost:5022/health/live
dotnet run --project src/MMV.Api --launch-profile https  # puis https://localhost:7267/health/live et /health/ready (01B)

# Client (depuis apps/desktop)
npm ci
npm run lint
npm test                               # Vitest : client API contre un serveur HTTP local + états de l'écran (01B)
npm run build
npx tauri dev                          # fenêtre native en développement
npx tauri build --debug --no-bundle    # binaire debug, sans installateur
npx tauri build --no-bundle            # binaire release x64, sans installateur (01B)
```

La chaîne de connexion PostgreSQL (`ConnectionStrings__Mmv`) n'est **pas** requise pour démarrer l'API ni pour les tests ; elle ne doit jamais être écrite dans les `appsettings*.json` ni dans Git. Sans elle, `/health/ready` répond 503 et les tests PostgreSQL réels sont **ignorés** (`NOT_TESTED`). Saisie locale par le propriétaire via user-secrets : [`docs/phase/01b/IMPLEMENTATION_SPEC.md`](docs/phase/01b/IMPLEMENTATION_SPEC.md) § Secrets.

## Autorité des sources

1. Dernières décisions explicites du propriétaire.
2. Cahier des charges MMV V2 neuf, validé progressivement.
3. Maquettes Claude Design et handoffs : sources de **design** et d'hypothèses fonctionnelles, non contrats métier approuvés.
4. ADR MMV V2 ratifiés et documentation officielle.
5. Code du **nouveau** dépôt ; ce code ne valide pas à lui seul une règle métier.

### Décisions acquises (phase 00)

- `D-000 APPROVED` : reconstruire MMV comme un produit **entièrement neuf**. Ne jamais consulter/réutiliser ancien code, ancien dépôt, règles, données ou migration. Pas de mapping legacy.
- `D-003 APPROVED` : **PostgreSQL est le seul SGBD**, partout, tests compris. Aucun SQLite ou remplacement « temporaire ».
- `D-004 APPROVED` : client/serveur réel, option **Serveur MMV** ou **Poste de travail MMV** et mode serveur+client pour une seule machine.
- `D-005 APPROVED` : **153 nouvelles vues et 23 compléments** au backlog des phases ultérieures, sans blocage automatique de la fondation technique. Les fonctionnalités critiques sous-jacentes ne peuvent être omises.
- `D-006 APPROVED` : aucun codage ni projet initialisé avant validation explicite de la phase 00. *(Levée pour le seul périmètre 01A par le GO du 15/10/2026, voir `docs/phase/01.md`.)*

## Documents

- `docs/phase/00.md` : mission de phase 00, IN/OUT, portes de contrôle (historique).
- `docs/phase/01.md` et `docs/phase/01/` : phase 01A — périmètre, spécification, QA, gates, preuves.
- `docs/phase/01b/` : phase 01B (IMPLEMENTED — OWNER_REVIEW_REQUIRED, corrections d'audit appliquées) — source-map, spécification, QA, gates, scripts SQL et preuves (`evidence/`).
- `docs/ARCHITECTURE_ET_STACK.md` : architecture et comparaison des technologies.
- `docs/INSTALLATION_RESEAU_SECURITE.md` : parcours serveur/poste, TLS, auth, sauvegardes.
- `docs/ADR_ET_DECISIONS.md` : registre avec statuts distincts (ADR réseau non ratifiés).
- `docs/PERIMETRE_ET_SEQUENCE.md` : découpage MVP, phases et backlog.
- `docs/PLAN_POC_ET_PREUVES.md` : tests de faisabilité obligatoires.
- `registres/DESIGN_SOURCES_SHA256.csv` : six sources de design principales, seules autorisées pour la traçabilité visuelle.
- `prompts/PHASE00_PREFLIGHT_ENVIRONNEMENT_AUDIT_ONLY.md` : audit facultatif de l'environnement Windows **sans accès à l'ancien dépôt**.

Le pack de phase 00 est un **addendum de remplacement** du dossier maître du 14 octobre ; les documents originaux faisant référence au passé technique de MMV sont **obsolètes** et ne doivent pas être transmis tels quels à Claude Code.
