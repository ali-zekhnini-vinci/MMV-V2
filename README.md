# MMV V2 — logiciel neuf de gestion d'un magasin d'optique

## Statut actuel — phase 03 (17/10/2026)

**`PHASE03_B2_READY — OWNER_SQL_REVIEW_REQUIRED`** sur la branche locale `feat/phase-03-clients-rdv` (aucun push, PR ni merge). GO métier 03-B2 du propriétaire (« Rendez-vous », [`prompts/PHASE03_B2_GO_CLAUDE_CODE.md`](prompts/PHASE03_B2_GO_CLAUDE_CODE.md)) : décisions **D-03-13 à D-03-18 approuvées** ([`docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md`](docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md) § Décision).

- **Rendez-vous — serveur :** routes `/api/appointments` (policies `appointments.read|write|cancel`, `system.admin` sans droit), sept motifs approuvés, deux modes d'attribution (personnalisé : responsable obligatoire ; accueil : capacité commune sans responsable), éligibilité **explicite** des collaborateurs (`MMV.Maintenance responsibles`), chevauchements refusés (bout à bout admis), capacité d'accueil calculée à chaque instant, réservations simultanées sérialisées par verrous transactionnels PostgreSQL pris dans un ordre stable, heures inexistantes ou ambiguës refusées (Maroc, Belgique), annulation logique et rétablissement contrôlé. Contrat : [`docs/phase/03/API_APPOINTMENTS.md`](docs/phase/03/API_APPOINTMENTS.md).
- **Rendez-vous — interface :** alignée sur les décisions ; « Prendre un rendez-vous » depuis la fiche client (client conservé, retour à la fiche) ; service activé, qui affiche « Agenda non configuré » tant que le serveur n'a pas les paramètres du magasin.
- **À revoir avant application :** migration `0004_AppointmentsModule` et SQL exact ([`docs/phase/03/sql/0004_AppointmentsModule.sql`](docs/phase/03/sql/0004_AppointmentsModule.sql), `.idempotent.sql`, `.rollback.sql` ; empreintes et justifications dans [`GATES_AND_DECISIONS.md`](docs/phase/03/GATES_AND_DECISIONS.md) § Dossier 0004 — version 03-B2 ; point O-03-24 sur le `DROP CONSTRAINT`) — **non appliquée**, aucune base modifiée.
- **À renseigner par le propriétaire :** fuseau et plage affichée du magasin, capacité d'accueil éventuelle, collaborateurs éligibles, droits — [`docs/phase/03/PROCEDURE_RENDEZ_VOUS_ACTIVATION.md`](docs/phase/03/PROCEDURE_RENDEZ_VOUS_ACTIVATION.md).
- **Tests :** .NET `MMV.Api.Tests` **286/286**, `MMV.Maintenance.Tests` **50/50** ; PostgreSQL 18 en lecture seule **13/13** ; `Stage=AppointmentsWrite` (6) et `PostMigration` (2) **NOT_TESTED** (exigent 0004) ; frontend **185/185** ; parcours natif `NOT_TESTED`. Détail : [`docs/phase/03/QA_MATRIX.md`](docs/phase/03/QA_MATRIX.md) Q-03-120 à Q-03-138.
- Restent : revue SQL et GO d'application de 0004, configuration du magasin, parcours natif, 03-C. Action propriétaire toujours ouverte : droits `clients.*` sur son compte ([`PROCEDURE_PERMISSIONS_CLIENTS.md`](docs/phase/03/PROCEDURE_PERMISSIONS_CLIENTS.md)).

*Historique — statut 03-B (17/10/2026) :* **`PHASE03_B_UI_READY — OWNER_BUSINESS_REVIEW_REQUIRED`** sur la branche locale `feat/phase-03-clients-rdv` (aucun push, PR ni merge). GO 03-B du propriétaire (« Rendez-vous », [`prompts/PHASE03_B_GO_CLAUDE_CODE.md`](prompts/PHASE03_B_GO_CLAUDE_CODE.md)).

- **Clients :** notice de confirmation qui persistait après navigation corrigée (O-03-15) ; module inchangé par ailleurs, permissions non régressées.
- **Rendez-vous (interface initiale) :** écran `/rendez-vous` — FullCalendar Standard **7.1.0** (MIT), vues Jour et Semaine, panneau détail, formulaire (React Hook Form + Zod), sélecteur de client, annulation / rétablissement, conflits, états, création et déplacement **au clavier**. Éprouvé sur double de test et banc visuel. **Service désactivé** : aucun rendez-vous n'est créé ni affiché tant que les règles ne sont pas validées.
- **Décision du propriétaire requise :** proposition métier consolidée D-03-13 à D-03-18 (fuseau du magasin, durées, responsable, conflits, annulation, droits, motifs) — [`docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md`](docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md). Contrat et migration `0004_AppointmentsModule` **proposés, non appliqués** ([`docs/phase/03/API_APPOINTMENTS_PROPOSED.md`](docs/phase/03/API_APPOINTMENTS_PROPOSED.md)).
- **Tests :** frontend **171/171** ; backend `src/` inchangé (suites .NET `NOT_RERUN`) ; code serveur proposé compilé en worktree, `MMV.Api.Tests` 182/182. Aucune base contactée. Détail : [`docs/phase/03/QA_MATRIX.md`](docs/phase/03/QA_MATRIX.md) Q-03-100 à Q-03-116.
- Restent : intégration serveur 03-B après décision, 03-C (fiche client ↔ rendez-vous). Action propriétaire toujours ouverte : droits `clients.*` sur son compte ([`PROCEDURE_PERMISSIONS_CLIENTS.md`](docs/phase/03/PROCEDURE_PERMISSIONS_CLIENTS.md)).

*Historique — statut 03-A3 (17/10/2026) :* **`PHASE03_A3_COMPLETE — OWNER_REVIEW_REQUIRED`** sur la branche locale `feat/phase-03-clients-rdv` (aucun push, PR ni merge). GO 03-A3 du propriétaire (« intégration réelle Clients », [`prompts/PHASE03_A3_GO_CLAUDE_CODE.md`](prompts/PHASE03_A3_GO_CLAUDE_CODE.md)).

- **Migration `0003_ClientsModule` appliquée** le 17/10/2026 à `mmv_v2_dev` puis `mmv_v2_test` (script idempotent à l'empreinte `4dbb0d84…d3db1c3b`, contrôles préalables en lecture seule conformes) : 3 migrations par base. `mmv_v2_dev` ne contient **aucun client** ; aucun compte réel modifié.
- **Tests réels :** `PostMigration` 2/2, `ClientsWrite` 7/7, `IdentityWrite` 5/5 sur `mmv_v2_test` ; .NET **260/260** ; frontend **137/137** ; matrice HTTP sur le processus MMV.Api réel **31/31** ; parcours natif Tauri → HTTPS → API → PostgreSQL 18 PASS (22 captures, comparées aux planches Lot 2) ; résidus 0. Détail : [`docs/phase/03/QA_MATRIX.md`](docs/phase/03/QA_MATRIX.md) Q-03-80 à Q-03-99.
- **Interface :** actions Clients proposées selon les permissions de la session (lecture seule : actions masquées ou désactivées avec motif) ; le serveur reste l'autorité, `system.admin` n'accorde aucun droit Clients.
- **Action du propriétaire :** attribuer `clients.read`, `clients.write`, `clients.archive` à son compte — [`docs/phase/03/PROCEDURE_PERMISSIONS_CLIENTS.md`](docs/phase/03/PROCEDURE_PERMISSIONS_CLIENTS.md). Jusque-là, l'écran Clients affiche « Accès non autorisé » pour ce compte.
- 03-B (Rendez-vous) et 03-C non commencés.

*Historique — statut 03-A2 (17/10/2026) :* **`PHASE03_A2_READY — OWNER_SQL_REVIEW_REQUIRED`** sur la branche locale `feat/phase-03-clients-rdv` (aucun push, PR ni merge). GO 03-A2 du propriétaire : G03-METIER **A et B acceptés**, **C accepté sur le principe** (SQL non autorisé à l'application), D (Rendez-vous) en attente de 03-B.

- **03-A2 implémenté :** domaine Clients, validation serveur, API `/api/clients` (6 routes, policies `clients.read` / `clients.write` / `clients.archive`, `system.admin` sans droit Clients), persistance EF Core 10 / Npgsql (référence `CLI` par séquence PostgreSQL, concurrence `xmin`), OpenAPI à schémas réels contrôlé contre les types TypeScript, commande `MMV.Maintenance permissions list|grant|revoke`, interface branchée sur la vraie API. Contrat : [`docs/phase/03/API_CLIENTS.md`](docs/phase/03/API_CLIENTS.md).
- **À revoir avant application :** migration `0003_ClientsModule` et SQL exact ([`docs/phase/03/sql/0003_ClientsModule.sql`](docs/phase/03/sql/0003_ClientsModule.sql), `.idempotent.sql`, `.rollback.sql` ; empreintes dans [`GATES_AND_DECISIONS.md`](docs/phase/03/GATES_AND_DECISIONS.md)) — **non appliquée**, aucun client ni droit en base.
- Tests : .NET 246/246 hors écriture (API 182, Maintenance 50, PostgreSQL 14), frontend 134/134 ; `Stage=ClientsWrite` (5) **NOT_RUN** et `Stage=PostMigration` en échec attendu tant que 0003 n'est pas appliquée ; parcours natif `NOT_TESTED`. 03-B et 03-C non commencés.

*Historique — statut 03-A (17/10/2026) :* **`PHASE03_A_UI_READY — OWNER_BUSINESS_RULES_AND_SQL_REVIEW_REQUIRED`** sur la branche locale `feat/phase-03-clients-rdv`, créée depuis `origin/main` = `cb79d1f951aa68e9523642384648424dee61836d` (merge de la PR #3, **Phase 02 MERGED**, vérifié par Git). Aucun push, PR ni merge.

- **03-A1 implémenté :** TanStack Query 5.104.1 et TanStack Table 8.21.3 intégrés ; module Clients (`/clients`) : liste à pagination/tri/recherche serveur, filtres, formulaire unique (validation, état modifié, confirmation de sortie, Ctrl+S), fiche à onglets (Ordonnances / Ventes explicitement reportés), archivage/réactivation, conflits de version. Tant que le dossier G03-METIER n'est pas ratifié, le service est **désactivé** : l'écran l'indique, aucune requête ni donnée fictive (seuls les tests activent le service contre un double HTTP). Tests frontend 128/128 ; .NET 123/123 hors étape d'écriture (NOT_RERUN) ; captures natives `NOT_TESTED`.
- **À valider (une seule fois)** *(validé le 17/10/2026 pour A, B ; C sur le principe)* **:** [`docs/phase/03/DECISIONS_G03_METIER.md`](docs/phase/03/DECISIONS_G03_METIER.md) — champs clients, cycle de vie, permissions `clients.*`, contrat [`API_CLIENTS_PROPOSED.md`](docs/phase/03/API_CLIENTS_PROPOSED.md), migration `0003_ClientsModule` générée hors connexion **non appliquée** (`docs/phase/03/sql/`).
- 03-A2, 03-B (Rendez-vous, cadrage [`RDV_03B_PREPARATION.md`](docs/phase/03/RDV_03B_PREPARATION.md)) et 03-C non commencés. Détail : [`docs/phase/03.md`](docs/phase/03.md).

## Statut phase 02 (16/10/2026, historique)

**Amendement daté 16/10/2026 — clôture : `PHASE02_READY_FOR_MANUAL_MERGE`.** Audit externe final : GO technique pour la publication et le merge manuel. **SEC-02-01 CLOSED** : rotation des deux mots de passe confirmée par le propriétaire et vérifiée sans affichage (connexions OK, anciennes valeurs refusées, `dotnet test MMV.slnx` 128/128). Branche `feat/phase-02-auth-shell` publiée sur `origin` ; PR et merge réservés au propriétaire ; Phase 03 non commencée. Le paragraphe suivant est conservé tel qu'à la livraison.

**Phase 02 `PHASE02_IMPLEMENTED — READY_FOR_OWNER_REVIEW`** sur la branche locale `feat/phase-02-auth-shell` (aucun push, PR ni merge ; ni `APPROVED` ni `MERGED`). **Action propriétaire ouverte SEC-02-01 : rotation des mots de passe PostgreSQL de `mmv_v2_dev` et `mmv_v2_test`**, affichés dans une session agent le 16/10/2026 (absents du dépôt et de son historique) — procédure : [`docs/phase/02/GATES_AND_DECISIONS.md`](docs/phase/02/GATES_AND_DECISIONS.md) § Incident SEC-02-01. **01B MERGED** dans `main` : `origin/main` = `c0c3d6ba15e57b49db350278c5a174735243ccb5` (merge de PR #2, vérifié par Git le 16/10/2026).

Objectif : écran de connexion et Shell React/Tauri conformes aux références visuelles DRAFT, identité/authentification/permissions minimales côté MMV.Api + PostgreSQL. **16/10/2026, après GO O-02-01 :** migration d'identité appliquée à `mmv_v2_dev` et `mmv_v2_test` ; premier administrateur créé par le propriétaire, second bootstrap refusé (code 3) ; parcours natif Tauri → HTTPS → MMV.Api → PostgreSQL (`mmv_v2_test`, comptes éphémères) PASS : connexion, permissions 401/403, déconnexion, révocation, désactivation, résilience API/base, redémarrage client ; journaux sans secret. Tests : .NET 128/128 (dont PostgreSQL 20/20), frontend 81/81. Non testé : `QA_MATRIX.md` Q-02-62.

*Historique — corrections finales (16/10/2026) : `PHASE02_STACK_AND_AUTH_READY — OWNER_SQL_GO_REQUIRED`.* Architecture d'authentification jugée conforme par la revue et conservée ; `MMV.Maintenance bootstrap-admin` refuse (code 5, sans modification) toute cible autre que `mmv_v2_dev` / rôle `mmv_v2_dev` / port 5433 / PostgreSQL 18 / connexion locale ; pile frontend **installée et utilisée** : Tailwind CSS 4 (Vite), shadcn/ui adaptés aux jetons MMV, Radix (Slot, Label, DropdownMenu), React Hook Form + Zod (Login), React Router (Shell), Lucide React, polices Inter et Source Sans 3 locales (OFL). Tests : .NET 70 + 38 + 13 (avant GO), frontend 81/81 ; non-régression visuelle et natif sans base PASS. Dossier de migration : `docs/phase/02/O-02-01_MANIFEST.sha256`. Détail et état des gates : [`docs/phase/02.md`](docs/phase/02.md), [`docs/phase/02/QA_MATRIX.md`](docs/phase/02/QA_MATRIX.md), [`docs/phase/02/GATES_AND_DECISIONS.md`](docs/phase/02/GATES_AND_DECISIONS.md).

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

**Nature du code :** socle technique minimal, **non** destiné à la production. Aucun module métier, aucune donnée de magasin. *(Note datée 17/10/2026 : phrase rédigée en 01B ; depuis 03-A2, le module Clients existe dans le code, sans aucune donnée en base.)*

## Contenu du dépôt

| Élément | Chemin |
|---|---|
| Client Windows Tauri 2 + React 19 + TypeScript strict + Vite (npm) ; Tailwind CSS 4, shadcn/ui + Radix, React Hook Form + Zod, React Router, Lucide, TanStack Query/Table (phase 03) (détail : `apps/desktop/README.md`) | `apps/desktop/` (client API : `apps/desktop/src/api/`, composants : `apps/desktop/src/components/ui/`, module Clients : `apps/desktop/src/clients/`) |
| Solution .NET 10 | `MMV.slnx`, `src/MMV.Domain`, `src/MMV.Application`, `src/MMV.Infrastructure`, `src/MMV.Api` |
| Santé (`/health/live`, `/health/ready`), CORS restreint | `src/MMV.Api/Health/`, `src/MMV.Api/Cors/DesktopCors.cs` |
| Migration baseline vide et script SQL relu | `src/MMV.Infrastructure/Persistence/Migrations/`, `docs/phase/01b/sql/` |
| Commandes de maintenance locales (état, bootstrap administrateur et attribution des droits Clients à cible stricte) | `src/MMV.Maintenance/` |
| Module Clients (phase 03-A2) : domaine, cas d'usage, persistance, routes `/api/clients` | `src/MMV.Domain/Clients/`, `src/MMV.Application/Clients/`, `src/MMV.Infrastructure/Clients/`, `src/MMV.Api/Clients/` |
| Tests | `tests/MMV.Api.Tests/`, `tests/MMV.Maintenance.Tests/`, `tests/MMV.PostgreSql.Tests/`, `apps/desktop/src/**/*.test.{ts,tsx}` |
| Documentation de phase | `docs/phase/`, preuves dans `docs/phase/01/`, `docs/phase/01b/evidence/` et `docs/phase/02/evidence/` |

## Exécuter localement

Prérequis (versions exactes : [`docs/phase/01/IMPLEMENTATION_SPEC.md`](docs/phase/01/IMPLEMENTATION_SPEC.md)) : Windows 11 x64, SDK .NET 10.0.402, Node 22 (≥ 22.22 depuis l'ajout de React Router 8) / npm 10, Rust stable MSVC x64, WebView2.

```sh
# Backend (racine du dépôt)
dotnet tool restore                    # dotnet-ef 10.0.4 local
dotnet restore MMV.slnx --locked-mode
dotnet build MMV.slnx
dotnet test MMV.slnx                                                          # suite complète (migration d'identité appliquée le 16/10/2026)
dotnet test MMV.slnx --filter "Stage!=PostMigration&Stage!=IdentityWrite"   # base encore non migrée uniquement
dotnet test MMV.slnx --filter "Stage!=PostMigration&Stage!=IdentityWrite&Stage!=ClientsWrite"   # avant application de 0003_ClientsModule (03-A2)
dotnet run --project src/MMV.Maintenance -- status                            # état et conformité de la base cible (session en lecture seule)
# bootstrap-admin : effectué par le propriétaire le 16/10/2026 sur mmv_v2_dev ; tout nouveau lancement est refusé (code 3)
dotnet run --project src/MMV.Maintenance -- permissions list                  # comptes et permissions (lecture seule)
# permissions grant|revoke <identifiant> clients.read|clients.write|clients.archive : après application de 0003 uniquement
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
- `docs/phase/03.md` et `docs/phase/03/` : phase 03 (`PHASE03_A3_COMPLETE — OWNER_REVIEW_REQUIRED` ; historique : 03-A2 `PHASE03_A2_READY — OWNER_SQL_REVIEW_REQUIRED`, 03-A `PHASE03_A_UI_READY — OWNER_BUSINESS_RULES_AND_SQL_REVIEW_REQUIRED`) — décision G03-METIER, correspondance des 47 états, contrat Clients implémenté (`API_CLIENTS.md`) et proposé (historique), SQL 0003 appliqué le 17/10/2026, procédure de permissions, cadrage Rendez-vous, spécification, QA, gates, source-map.
- `docs/phase/02.md` et `docs/phase/02/` : phase 02 (**MERGED** dans `main`, `cb79d1f` ; état de livraison `PHASE02_READY_FOR_MANUAL_MERGE`, SEC-02-01 clos) — spécification (dont pile frontend), source-map (provenance des références, logos, polices, dépendances), QA, gates et décisions, manifeste du dossier O-02-01.
- `docs/ARCHITECTURE_ET_STACK.md` : architecture et comparaison des technologies.
- `docs/INSTALLATION_RESEAU_SECURITE.md` : parcours serveur/poste, TLS, auth, sauvegardes.
- `docs/ADR_ET_DECISIONS.md` : registre avec statuts distincts (ADR réseau non ratifiés).
- `docs/PERIMETRE_ET_SEQUENCE.md` : découpage MVP, phases et backlog.
- `docs/PLAN_POC_ET_PREUVES.md` : tests de faisabilité obligatoires.
- `registres/DESIGN_SOURCES_SHA256.csv` : six sources de design principales, seules autorisées pour la traçabilité visuelle.
- `prompts/PHASE00_PREFLIGHT_ENVIRONNEMENT_AUDIT_ONLY.md` : audit facultatif de l'environnement Windows **sans accès à l'ancien dépôt**.

Le pack de phase 00 est un **addendum de remplacement** du dossier maître du 14 octobre ; les documents originaux faisant référence au passé technique de MMV sont **obsolètes** et ne doivent pas être transmis tels quels à Claude Code.
