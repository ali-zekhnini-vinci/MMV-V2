# MMV V2 — logiciel neuf de gestion d'un magasin d'optique

**État au 15 octobre 2026 (soir, terminaison 01B) :** phase 00 (cadrage) livrée ; **phase 01A MERGED** dans `main` (commit `4ed20a8e38b9db7519c0f56e24afaaaa4d6556ed`) ; **phase 01B `IMPLEMENTED — OWNER_REVIEW_REQUIRED`** sur la branche locale `feat/phase-01b-integration` (commits locaux, non poussée, non mergée) : chaîne **fenêtre Tauri → HTTPS → MMV.Api → PostgreSQL 18** démontrée en **développement local monoposte**. Phase 02 `NOT_STARTED`. Les ADR réseau (ADR-004/006/007) restent **non ratifiés**.

> Historique : avant le merge, ce paragraphe indiquait « 01A implémentée sur `feat/phase-01-socle`, publiée, non fusionnée ; 01B non commencée (aucun GO) » ; puis, jusqu'à la terminaison, « 01B `IN_PROGRESS` ».

**Nature du code :** socle technique minimal, **non** destiné à la production. Aucun module métier, aucune donnée de magasin.

## Ce que contient le dépôt (01A)

| Élément | Chemin | État |
|---|---|---|
| Client Windows Tauri 2 + React 19 + TypeScript strict + Vite (npm) | `apps/desktop/` | écran « socle technique » sans donnée ni appel réseau |
| Solution .NET 10 | `MMV.slnx`, `src/MMV.Domain`, `src/MMV.Application`, `src/MMV.Infrastructure`, `src/MMV.Api` | Domain/Application vides ; Infrastructure : `MmvDbContext` sans entité (Npgsql) |
| Endpoint de vivacité | `GET /health/live` | état du **processus** uniquement, pas de PostgreSQL |
| Tests automatiques | `tests/MMV.Api.Tests/` | xUnit, 10 tests (vivacité, règles de références, fournisseur Npgsql) |
| Documentation de phase | `docs/phase/00.md`, `docs/phase/01.md`, `docs/phase/01/` | gates, spécification, matrice QA, preuves |

Résultats rapportés (détail et extraits : [`docs/phase/01/QA_MATRIX.md`](docs/phase/01/QA_MATRIX.md)) : `npm run lint` / `npm run build` PASS, build Tauri debug et fenêtre native PASS (capture), `dotnet build` 0 avertissement, `dotnet test` 10/10, `GET /health/live` en **HTTP local** → 200.

## Ajouts 01B en cours (branche locale `feat/phase-01b-integration`, non mergée)

| Élément | Chemin | État au 15/10/2026 |
|---|---|---|
| Disponibilité base | `GET /health/ready` (`src/MMV.Api/Health/ReadinessEndpoint.cs`) | 503 vérifié sans base ; 200 sur PG18 réel `NOT_TESTED` |
| CORS restreint (dev) | `src/MMV.Api/Cors/DesktopCors.cs` | testé (préflight autorisé/refusé) |
| Client API Tauri typé | `apps/desktop/src/api/` | tests `node --test` 17/17 ; fenêtre native `NOT_TESTED` |
| Garde de cible PostgreSQL | `tests/MMV.PostgreSql.Tests/` | garde PASS ; identité réelle `NOT_TESTED` (secrets non saisis) |
| Migration initiale vide | `src/MMV.Infrastructure/Persistence/Migrations/`, script `docs/phase/01b/sql/` | **générée, non appliquée** — revue propriétaire (O-B07) |

**Mise à jour 15/10/2026 (soir, terminaison 01B)** — le tableau ci-dessus reste l'état intermédiaire ; état final (détail : [`docs/phase/01b/QA_MATRIX.md`](docs/phase/01b/QA_MATRIX.md) Q-B18..Q-B28) :

| Élément | État final 01B |
|---|---|
| PostgreSQL 18.6 `localhost:5433` | bases/rôles `mmv_v2_dev` et `mmv_v2_test` créés par le propriétaire ; identité, version, port et privilèges vérifiés en lecture seule |
| Migration baseline | **appliquée par le propriétaire** sur les deux bases ; 1 ligne d'historique par base, cohérente avec EF Core (`pending=[]`) ; jamais au démarrage |
| Tests PostgreSQL réels | 12/12 : identité, schéma, écriture éphémère + `ROLLBACK` sur `mmv_v2_test` uniquement |
| `GET /health/live` / `/health/ready` | HTTPS réel : 200 / 200 avec PG18 ; 200 / 503 si PG injoignable ou muet (borné ~2,3 s) ; aucune fuite |
| Fenêtre Tauri native | 4 états réels capturés (ready, base indisponible, CORS refusé, API arrêtée) ; Origin WebView2 `http://localhost:1420` ; exécutable release x64 construit (sans installateur) |
| Tests frontend | **Vitest 5.0.3**, 25/25 (client HTTP + états de l'écran) |

## Limites — non validé à ce jour

> Mis à jour le 15/10/2026 au soir (terminaison 01B) : HTTPS local depuis Tauri, PostgreSQL réel, migration et parcours client → API → PG sont désormais **vérifiés en développement local** (voir tableau ci-dessus). Les puces suivantes restent l'état intermédiaire du 15/10/2026, à lire avec ces corrections. Restent non validés : TLS LAN/CA/rotation (ADR-006 OPEN), adresse du serveur pour un poste en production (O-B11), multi-postes, installateur, Windows Service, sauvegardes, rôle applicatif séparé du rôle de migration (O-B04). Outils : Serena TypeScript PASS via `serena project health-check`, session MCP à reconnecter (`/mcp`) ; Graphify CLI PASS, MCP `NOT_CONFIGURED`.

> Mis à jour le 15/10/2026 (01B). Historique : jusqu'à cette date, HTTPS était `NOT_VERIFIED`, aucune base V2 n'existait et le client n'appelait aucune API.

- **HTTPS / TLS** : HTTPS **local** `https://localhost:7267` vérifié sans contournement (certificat de dev approuvé par le propriétaire, Q-B13) ; depuis la fenêtre Tauri `NOT_TESTED` ; TLS LAN et appairage non commencés (ADR-006 ouvert).
- **PostgreSQL réel** : bases/rôles `mmv_v2_dev` / `mmv_v2_test` créés par le propriétaire sur PG18:5433 (O-B01, déclaré) ; vérification d'identité par l'agent, migration et aller-retour transactionnel : `NOT_TESTED` / en attente des GO O-B07 et O-B02.
- **Connexion client → API** : client implémenté (`devCsp` limité à `https://localhost:7267`, développement uniquement) ; parcours réel Tauri → API → PG18 `NOT_TESTED`. Le bundle de production ne contient aucune adresse de serveur.
- **Installateur, Windows Service, multi-postes, sauvegardes** : non commencés (01B+). Aucun fonctionnement multi-postes n'est revendiqué.
- **Outils agent** (détail : [`docs/phase/01/GATES_AND_DECISIONS.md`](docs/phase/01/GATES_AND_DECISIONS.md)) : Serena MCP PASS pour la **navigation symbolique C# uniquement** (TypeScript : autorisé et déclaré localement le 15/10/2026, `BLOCKED` jusqu'au redémarrage du serveur MCP — Q-B16 de [`docs/phase/01b/QA_MATRIX.md`](docs/phase/01b/QA_MATRIX.md)) ; Graphify CLI + graphe AST local PASS ; Graphify MCP `NOT_CONFIGURED`.

## Exécuter localement

Prérequis (versions exactes : [`docs/phase/01/IMPLEMENTATION_SPEC.md`](docs/phase/01/IMPLEMENTATION_SPEC.md)) : Windows 11 x64, SDK .NET 10.0.402, Node 22 / npm 10, Rust stable MSVC x64, WebView2.

```sh
# Backend (racine du dépôt)
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
- `docs/phase/01b/` : phase 01B (IMPLEMENTED — OWNER_REVIEW_REQUIRED) — source-map, spécification, QA, gates, scripts SQL et preuves (`evidence/`).
- `docs/ARCHITECTURE_ET_STACK.md` : architecture et comparaison des technologies.
- `docs/INSTALLATION_RESEAU_SECURITE.md` : parcours serveur/poste, TLS, auth, sauvegardes.
- `docs/ADR_ET_DECISIONS.md` : registre avec statuts distincts (ADR réseau non ratifiés).
- `docs/PERIMETRE_ET_SEQUENCE.md` : découpage MVP, phases et backlog.
- `docs/PLAN_POC_ET_PREUVES.md` : tests de faisabilité obligatoires.
- `registres/DESIGN_SOURCES_SHA256.csv` : six sources de design principales, seules autorisées pour la traçabilité visuelle.
- `prompts/PHASE00_PREFLIGHT_ENVIRONNEMENT_AUDIT_ONLY.md` : audit facultatif de l'environnement Windows **sans accès à l'ancien dépôt**.

Le pack de phase 00 est un **addendum de remplacement** du dossier maître du 14 octobre ; les documents originaux faisant référence au passé technique de MMV sont **obsolètes** et ne doivent pas être transmis tels quels à Claude Code.
