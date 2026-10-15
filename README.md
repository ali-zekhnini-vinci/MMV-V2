# MMV V2 — logiciel neuf de gestion d'un magasin d'optique

**État au 15 octobre 2026 (mis à jour après le merge de 01A) :** phase 00 (cadrage) livrée ; **phase 01A MERGED** dans `main` par le propriétaire — commit de merge `4ed20a8e38b9db7519c0f56e24afaaaa4d6556ed` (parents `bbf1749` + `79aa226`, arbre identique à `79aa226`, constaté sur `origin/main`) ; **phase 01B `IN_PROGRESS`** (GO du 15/10/2026) sur la branche locale `feat/phase-01b-integration` : PostgreSQL 18 réel, `GET /health/ready`, HTTPS local et appel réel depuis Tauri. Les ADR réseau (ADR-004/006/007) restent **non ratifiés**.

> Historique : avant le merge, ce paragraphe indiquait « 01A implémentée sur `feat/phase-01-socle`, publiée, non fusionnée ; 01B non commencée (aucun GO) ».

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

## Limites — non validé à ce jour

- **HTTPS / TLS** : `NOT_VERIFIED` (certificat de développement non approuvé, aucun contournement) ; TLS LAN et appairage non commencés (ADR-006 ouvert).
- **PostgreSQL réel** : `NOT_TESTED` — aucune base, aucun rôle, aucune migration ; la base de développement V2 attend un accord ciblé (décision O-01).
- **Connexion client → API** : inexistante ; la CSP du client n'autorise aucune origine serveur.
- **Installateur, Windows Service, multi-postes, sauvegardes** : non commencés (01B+). Aucun fonctionnement multi-postes n'est revendiqué.
- **Outils agent** (détail : [`docs/phase/01/GATES_AND_DECISIONS.md`](docs/phase/01/GATES_AND_DECISIONS.md)) : Serena MCP PASS pour la **navigation symbolique C# uniquement** (TypeScript `NOT_VERIFIED`) ; Graphify CLI + graphe AST local PASS ; Graphify MCP `NOT_CONFIGURED`.

## Exécuter localement

Prérequis (versions exactes : [`docs/phase/01/IMPLEMENTATION_SPEC.md`](docs/phase/01/IMPLEMENTATION_SPEC.md)) : Windows 11 x64, SDK .NET 10.0.402, Node 22 / npm 10, Rust stable MSVC x64, WebView2.

```sh
# Backend (racine du dépôt)
dotnet restore MMV.slnx --locked-mode
dotnet build MMV.slnx
dotnet test MMV.slnx
dotnet run --project src/MMV.Api --launch-profile http   # puis http://localhost:5022/health/live

# Client (depuis apps/desktop)
npm ci
npm run lint
npm run build
npx tauri dev                          # fenêtre native en développement
npx tauri build --debug --no-bundle    # binaire debug, sans installateur
```

La chaîne de connexion PostgreSQL (`ConnectionStrings__Mmv`) n'est **pas** requise pour démarrer l'API ni pour les tests ; elle ne doit jamais être écrite dans les `appsettings*.json` ni dans Git.

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
- `docs/phase/01b/` : phase 01B (IN_PROGRESS) — source-map, spécification, QA, gates et autorisations ciblées SQL/TLS.
- `docs/ARCHITECTURE_ET_STACK.md` : architecture et comparaison des technologies.
- `docs/INSTALLATION_RESEAU_SECURITE.md` : parcours serveur/poste, TLS, auth, sauvegardes.
- `docs/ADR_ET_DECISIONS.md` : registre avec statuts distincts (ADR réseau non ratifiés).
- `docs/PERIMETRE_ET_SEQUENCE.md` : découpage MVP, phases et backlog.
- `docs/PLAN_POC_ET_PREUVES.md` : tests de faisabilité obligatoires.
- `registres/DESIGN_SOURCES_SHA256.csv` : six sources de design principales, seules autorisées pour la traçabilité visuelle.
- `prompts/PHASE00_PREFLIGHT_ENVIRONNEMENT_AUDIT_ONLY.md` : audit facultatif de l'environnement Windows **sans accès à l'ancien dépôt**.

Le pack de phase 00 est un **addendum de remplacement** du dossier maître du 14 octobre ; les documents originaux faisant référence au passé technique de MMV sont **obsolètes** et ne doivent pas être transmis tels quels à Claude Code.
