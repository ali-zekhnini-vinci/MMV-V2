# MMV V2 — logiciel neuf de gestion d'un magasin d'optique

## Statut actuel — phase 02 (16/10/2026)

**Phase 02 `IN_PROGRESS`** sur la branche locale `feat/phase-02-auth-shell` (aucun push, PR ni merge). **01B MERGED** dans `main` : `origin/main` = `c0c3d6ba15e57b49db350278c5a174735243ccb5` (merge de PR #2, vérifié par Git le 16/10/2026).

Objectif : écran de connexion et Shell React/Tauri conformes aux références visuelles DRAFT, identité/authentification/permissions minimales côté MMV.Api + PostgreSQL. **Migration d'identité générée, non appliquée** : GO unique du propriétaire attendu (O-02-01) avant application aux bases et bootstrap du premier administrateur.

**Corrections finales (16/10/2026) : `PHASE02_STACK_AND_AUTH_READY — OWNER_SQL_GO_REQUIRED`.** Architecture d'authentification jugée conforme par la revue et conservée ; `MMV.Maintenance bootstrap-admin` refuse (code 5, sans modification) toute cible autre que `mmv_v2_dev` / rôle `mmv_v2_dev` / port 5433 / PostgreSQL 18 / connexion locale ; pile frontend **installée et utilisée** : Tailwind CSS 4 (Vite), shadcn/ui adaptés aux jetons MMV, Radix (Slot, Label, DropdownMenu), React Hook Form + Zod (Login), React Router (Shell), Lucide React, polices Inter et Source Sans 3 locales (OFL). Tests : .NET 70 + 38 + 13 (avant GO), frontend 81/81 ; non-régression visuelle et natif sans base PASS. Dossier de migration : `docs/phase/02/O-02-01_MANIFEST.sha256`. Détail et état des gates : [`docs/phase/02.md`](docs/phase/02.md), [`docs/phase/02/QA_MATRIX.md`](docs/phase/02/QA_MATRIX.md), [`docs/phase/02/GATES_AND_DECISIONS.md`](docs/phase/02/GATES_AND_DECISIONS.md).

## Statut phase 01B (15/10/2026, historique)


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
| Client Windows Tauri 2 + React 19 + TypeScript strict + Vite (npm) ; Tailwind CSS 4, shadcn/ui + Radix, React Hook Form + Zod, React Router, Lucide (détail : `apps/desktop/README.md`) | `apps/desktop/` (client API : `apps/desktop/src/api/`, composants : `apps/desktop/src/components/ui/`) |
| Solution .NET 10 | `MMV.slnx`, `src/MMV.Domain`, `src/MMV.Application`, `src/MMV.Infrastructure`, `src/MMV.Api` |
| Santé (`/health/live`, `/health/ready`), CORS restreint | `src/MMV.Api/Health/`, `src/MMV.Api/Cors/DesktopCors.cs` |
| Migration baseline vide et script SQL relu | `src/MMV.Infrastructure/Persistence/Migrations/`, `docs/phase/01b/sql/` |
| Commandes de maintenance locales (état, bootstrap administrateur à cible stricte) | `src/MMV.Maintenance/` |
| Tests | `tests/MMV.Api.Tests/`, `tests/MMV.Maintenance.Tests/`, `tests/MMV.PostgreSql.Tests/`, `apps/desktop/src/**/*.test.{ts,tsx}` |
| Documentation de phase | `docs/phase/`, preuves dans `docs/phase/01/` et `docs/phase/01b/evidence/` |

## Exécuter localement

Prérequis (versions exactes : [`docs/phase/01/IMPLEMENTATION_SPEC.md`](docs/phase/01/IMPLEMENTATION_SPEC.md)) : Windows 11 x64, SDK .NET 10.0.402, Node 22 (≥ 22.22 depuis l'ajout de React Router 8) / npm 10, Rust stable MSVC x64, WebView2.

```sh
# Backend (racine du dépôt)
dotnet tool restore                    # dotnet-ef 10.0.4 local
dotnet restore MMV.slnx --locked-mode
dotnet build MMV.slnx
dotnet test MMV.slnx --filter "Stage!=PostMigration&Stage!=IdentityWrite"   # avant application de la migration d'identité (phase 02)
dotnet test MMV.slnx                                                          # après O-02-01 : suite complète
dotnet run --project src/MMV.Maintenance -- status                            # état et conformité de la base cible (session en lecture seule)
# bootstrap-admin : uniquement après GO O-02-01, par le propriétaire ; cible imposée mmv_v2_dev/5433/PG18/locale (sinon code 5)
dotnet run --project src/MMV.Api --launch-profile http   # puis http://localhost:5022/health/live
dotnet run --project src/MMV.Api --launch-profile https  # puis https://localhost:7267/health/live et /health/ready (01B)

# Client (depuis apps/desktop)
npm ci
npm run lint
npm test                               # Vitest : client API contre un serveur HTTP local, écrans Login/Shell, conformité de style
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
- `docs/phase/02.md` et `docs/phase/02/` : phase 02 (IN_PROGRESS, `OWNER_SQL_GO_REQUIRED`) — spécification (dont pile frontend), source-map (provenance des références, logos, polices, dépendances), QA, gates et décisions, manifeste du dossier O-02-01.
- `docs/ARCHITECTURE_ET_STACK.md` : architecture et comparaison des technologies.
- `docs/INSTALLATION_RESEAU_SECURITE.md` : parcours serveur/poste, TLS, auth, sauvegardes.
- `docs/ADR_ET_DECISIONS.md` : registre avec statuts distincts (ADR réseau non ratifiés).
- `docs/PERIMETRE_ET_SEQUENCE.md` : découpage MVP, phases et backlog.
- `docs/PLAN_POC_ET_PREUVES.md` : tests de faisabilité obligatoires.
- `registres/DESIGN_SOURCES_SHA256.csv` : six sources de design principales, seules autorisées pour la traçabilité visuelle.
- `prompts/PHASE00_PREFLIGHT_ENVIRONNEMENT_AUDIT_ONLY.md` : audit facultatif de l'environnement Windows **sans accès à l'ancien dépôt**.

Le pack de phase 00 est un **addendum de remplacement** du dossier maître du 14 octobre ; les documents originaux faisant référence au passé technique de MMV sont **obsolètes** et ne doivent pas être transmis tels quels à Claude Code.
