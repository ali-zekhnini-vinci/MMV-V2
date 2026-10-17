# 03 — GATES_AND_DECISIONS

## GO et bornes

- 17/10/2026 : « GO OFFICIEL PHASE 03 » du propriétaire (message de session), mandat `prompts/PHASE03_GO_CLAUDE_CODE.md` (versionné avec la phase, comme les mandats précédents) et cadrage `references/phase03/` (copie de travail **non versionnée**, empreintes dans `SOURCE_MAP.md`). Couvre : branche dédiée, TanStack Query v5 et Table v8, premiers écrans Clients, propositions de modèle / contrat / permissions / SQL, cadrage technique des Rendez-vous (FullCalendar Standard), tests, DOCUMENTATION GATE, commits locaux.
- **Bornes :** aucune modification de base PostgreSQL, aucune migration métier ni permission persistante avant validation G03-METIER ; aucun push, PR, merge ni Phase 04 ; aucun accès à l'ancien MMV.
- 17/10/2026 : « PHASE 03-A2 — GO MÉTIER CLIENTS » ([`prompts/PHASE03_A2_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_A2_GO_CLAUDE_CODE.md)) : G03-METIER A et B **acceptés**, C **accepté sur le principe**, D en attente de 03-B. **Bornes 03-A2 :** migration 0003 et SQL exact préparés **sans application** avant revue ; aucun client fictif en base de développement ; processus Tauri/Vite/API du propriétaire non modifiés sans accord ; tests PostgreSQL d'écriture non déclarés réussis sans exécution réelle ; Rendez-vous non développé ; aucun push, PR ni merge.
- 17/10/2026 : « GO PHASE 03-A3 — INTÉGRATION RÉELLE CLIENTS » ([`prompts/PHASE03_A3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_A3_GO_CLAUDE_CODE.md)) : application de `0003_ClientsModule.idempotent.sql` (empreinte imposée) à `mmv_v2_dev` puis `mmv_v2_test` après contrôles ; tests réels ; procédure d'attribution des permissions ; masquage/désactivation des actions interdites ; chaîne native Tauri → HTTPS → API → PostgreSQL 18 ; captures et comparaison aux planches. **Bornes 03-A3 :** STOP sur tout état inattendu, aucun retour arrière ni nettoyage automatique ; PostgreSQL 17 et autres bases intouchés ; aucun compte réel modifié sans accord ; aucune donnée fictive en base de développement (comptes et clients éphémères sur `mmv_v2_test` seulement) ; ni 03-B ni Phase 04 ; aucun push, PR ni merge.

## Gates Phase 03

| Gate | Critère | État (17/10/2026) |
|---|---|---|
| G03-0 | Branche propre depuis `origin/main` fusionné, Phase 02 confirmée, sources Lot 2 recensées, environnement, non-régression | **PASS** — `cb79d1f` ; QA Q-03-01 à Q-03-06, Q-03-30 ; réserves environnementales externes : Q-03-11 (`npm ci`), Q-03-32 (build complet) |
| G03-A1 | Écrans Clients (liste, recherche, filtres, formulaire, fiche, archivage/réactivation) avec TanStack Query/Table, états testables, aucune donnée fictive en fonctionnement normal | **IMPLEMENTED** — captures natives **PASS** le 17/10/2026 (03-A3, Q-03-90, Q-03-91) ; *historique : tests 128/128 ; service désactivé jusqu'à G03-METIER ; captures natives `NOT_TESTED` (Q-03-22)* |
| G03-METIER | Validation unique : champs clients, cycle de vie, permissions, contrat, migration SQL (+ section Rendez-vous facultative à ce stade) | **A, B ACCEPTED ; C ACCEPTED sur le principe** (17/10/2026) ; **D PENDING** (03-B) — [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) ; *historique : OPEN — dossier présenté* |
| G03-SQL | Revue du SQL exact de `0003_ClientsModule` par le propriétaire, puis GO d'application | **PASS — GO accordé et migration APPLIQUÉE** le 17/10/2026 (« GO PHASE 03-A3 », [`prompts/PHASE03_A3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_A3_GO_CLAUDE_CODE.md)) : `mmv_v2_dev` puis `mmv_v2_test` (Q-03-80 à Q-03-84, § Exécution 03-A3) ; *historique : OPEN — `OWNER_SQL_REVIEW_REQUIRED`* |
| G03-A2 | Domaine, API contrôlée serveur, persistance, migration appliquée, tests d'intégration `mmv_v2_test` | **PASS** le 17/10/2026 (03-A3) : migration appliquée, `PostMigration` 2/2, `ClientsWrite` 7/7, `IdentityWrite` 5/5, .NET 260/260, matrice HTTP réelle 31/31, parcours natif PASS (Q-03-85 à Q-03-96) ; *historique : IMPLEMENTED — migration non appliquée (Q-03-50 à Q-03-77) ; `NOT_STARTED` — bloqué par G03-METIER* |
| G03-A3 | Intégration réelle Clients : Tauri → HTTPS → API → PostgreSQL 18, permissions de session, procédure d'attribution | **PASS** (17/10/2026) — [`PROCEDURE_PERMISSIONS_CLIENTS.md`](PROCEDURE_PERMISSIONS_CLIENTS.md) ; compte réel non modifié (action du propriétaire) ; `PHASE03_A3_COMPLETE — OWNER_REVIEW_REQUIRED` |
| G03-B | Rendez-vous | `NOT_STARTED` — cadrage [`RDV_03B_PREPARATION.md`](RDV_03B_PREPARATION.md) ; décisions D-03-13 à D-03-17 |
| G03-QA | Livraison Phase 03 (03-A, 03-B, 03-C) | `NOT_STARTED` — la Phase 03 n'est **pas** achevée |
| G03-DOC | DOCUMENTATION GATE | synchronisé le 17/10/2026 (`README.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/ADR_ET_DECISIONS.md`, `apps/desktop/README.md`, `docs/phase/02.md` (amendement MERGED), `docs/phase/03.md`, `docs/phase/03/*`) ; **re-synchronisé le 17/10/2026 pour 03-A2** (mêmes documents + `API_CLIENTS.md`, `prompts/PHASE03_A2_GO_CLAUDE_CODE.md`) ; **re-synchronisé le 17/10/2026 pour 03-A3** (mêmes documents + `PROCEDURE_PERMISSIONS_CLIENTS.md`, `STATE_MAP_47.md`, `prompts/PHASE03_A3_GO_CLAUDE_CODE.md`) |

## Décisions métier ouvertes

17/10/2026 (03-A2) : D-03-01 à D-03-10 **ACCEPTED** (sections A, B) ; D-03-11, D-03-12 **ACCEPTED sur le principe** (section C, SQL soumis à revue) ; D-03-13 à D-03-17 **PROPOSED** (section D, 03-B). Détail et précisions de mise en œuvre : [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md). *Historique 03-A : D-03-01 à D-03-17 PROPOSED, aucune approuvée.*

## Décisions techniques ouvertes (non bloquantes pour le dossier métier)

| ID | Sujet | État | Bloque |
|---|---|---|---|
| O-03-01 | OpenAPI incomplet (réponses 200 sans schéma) : `openapi-typescript` / `openapi-fetch` non installés ; déclarer les réponses (`TypedResults`, `.Produces`) en 03-A2 puis réévaluer la génération | **03-A2 : routes Clients typées** (`TypedResults`, schémas de requête, énumérations, bornes, nombres stricts) ; instantané `apps/desktop/src/api/contract/clients.openapi.json` contrôlé des deux côtés ; génération de types toujours non installée (aucune dépendance) ; routes d'authentification Phase 02 non retypées | aucune |
| O-03-02 | Avertissement lint `react-hooks/incompatible-library` sur `useReactTable` (React Compiler ignore `ClientsTable`) : conservé visible, aucune suppression de règle | PROPOSED | aucune |
| O-03-03 | Bundle principal 548 kB (> 500 kB) malgré le chunk séparé `ClientsModule` (112 kB) : TanStack Query reste dans le bundle initial (Shell) ; prolonge O-02-13 | PROPOSED | aucune |
| O-03-04 | Service Clients activé par une constante de compilation (`CLIENTS_API_ENABLED`) ; alternative : détection d'une capacité serveur à l'exécution | **03-A2 : `true`** ; état « non activé » conservé comme interrupteur de repli (testé) ; détection à l'exécution toujours PROPOSED | aucune |
| O-03-05 | Garde de sortie par registre maison (`shell/navigationGuard.ts`) car le routeur mémoire n'est pas un « data router » ; alternative : `createMemoryRouter` + `useBlocker` (refonte du Shell) | PROPOSED | aucune |
| O-03-06 | `@radix-ui/react-alert-dialog` / `react-tabs` 1.2.1 publiés le 12/10/2026 (< 2 semaines) pour rester sur la vague déjà installée de `react-dropdown-menu` 2.2.1 (primitives partagées dédupliquées) | PROPOSED | aucune |
| O-03-07 | Recherche « aperçu » : la recherche, les filtres et le tri sont désactivés tant que le service n'est pas activé ; seul le formulaire est consultable | PROPOSED | aucune |
| O-03-08 | Processus externes à la session encore actifs : `MMV.Api` (PID 26332) et serveur Vite (PID 23448) ; ils ont empêché `npm ci` et le build complet ; le serveur Vite a pu être perturbé pendant la restauration de `node_modules` (à redémarrer) | **RESOLVED** 17/10/2026 (03-A2) : processus absents au démarrage de 03-A2 (aucun arrêt par l'agent) ; `npm ci` et `dotnet build --no-incremental` relancés PASS (Q-03-51, Q-03-60) ; *historique : OPEN — action propriétaire* | — |
| O-03-09 | Actions d'écriture affichées sans tenir compte des permissions de la session (un compte `clients.read` seul voit « Nouveau client » et reçoit « Action non autorisée » à l'enregistrement) ; le serveur reste l'autorité. Proposition : masquer/désactiver Nouveau/Modifier/Archiver selon `session.user.permissions` | **RESOLVED** 17/10/2026 (03-A3, demandé par le GO) : `clients/clientPermissions.ts` ; liste : actions masquées + motif ; fiche : actions désactivées avec motif ; routes `nouveau`/`modifier` gardées ; garde d'affichage seulement (Q-03-90, Q-03-93) ; *historique : PROPOSED* | — |
| O-03-10 | Référence `CLI` : expression corrigée par rapport au SQL proposé (troncature `lpad` au-delà de 999 999) — voir `DECISIONS_G03_METIER.md` § Décision | **CLOSED** 17/10/2026 : SQL examiné puis GO d'application du propriétaire ; formats vérifiés sur PostgreSQL 18.6 (Q-03-62, Q-03-81) ; *historique : À VALIDER à la revue SQL* | — |
| O-03-11 | Recherche `LIKE '%…%'` sans index (aucune extension `pg_trgm`) : balayage séquentiel de `clients.clients` ; adapté au volume d'un magasin, à mesurer au-delà de quelques dizaines de milliers de fiches | PROPOSED | aucune |
| O-03-12 | Séquence de référence non transactionnelle : chaque insertion annulée (tests `ClientsWrite` sur `mmv_v2_test`, erreurs) consomme un numéro ; trous possibles, jamais de réutilisation (conforme à D-03-01) | INFORMATION | aucune |
| O-03-13 | Le script de retour arrière **supprime la table `clients.clients` et toutes ses données** et échoue tant que des attributions `clients.*` existent ; à n'utiliser qu'avant toute donnée réelle | INFORMATION — **non exécuté** (03-A3 : aucun retour arrière, aucun nettoyage de schéma) ; *historique : à relire en revue SQL* | — |
| O-03-14 | `ON DELETE RESTRICT` est signalé par PostgreSQL 18.6 avec `23001 restrict_violation` (et non `23503`) : toute future traduction d'erreur SQL en code métier doit traiter les deux | INFORMATION (03-A3, Q-03-86) | aucune |
| O-03-15 | Le message de confirmation (« Client archivé », « Client réactivé ») reste affiché après navigation (liste → fiche) jusqu'à sa fermeture ; sur la fiche d'un client archivé il précède le bandeau « Client archivé » (doublon visuel, absent des planches 24/26) | PROPOSED — effacer la notice au changement d'écran | aucune |
| O-03-16 | O-02-15 toujours présent : la requête d'affichage finale de `Invoke-MmvSqlScript.ps1` échoue après `COMMIT` (application réussie, contrôlée en lecture seule) ; le script approuvé n'a pas été modifié | OPEN — correction à soumettre au propriétaire | aucune |

Héritées, inchangées : O-02-13 (bundle), O-02-14 (503 sur `/api` si base injoignable : l'interface Clients traite 500/503 comme erreur technique bornée), O-02-15 (script PowerShell), O-B04, O-B10, O-B11, O-B12 ; ADR-004/006/007 non ratifiés.

## Exécution 03-A3 — migration 0003 appliquée (17/10/2026)

GO « PHASE 03-A3 — INTÉGRATION RÉELLE CLIENTS » du propriétaire ([`prompts/PHASE03_A3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_A3_GO_CLAUDE_CODE.md)) : script autorisé `docs/phase/03/sql/0003_ClientsModule.idempotent.sql`, SHA-256 `4dbb0d84940d0891915adce2538fb0fee66e054bf4b3e9c347429c18d3db1c3b`.

| Étape | Constat | Preuve |
|---|---|---|
| Empreinte, correspondance | identique ; 3 scripts régénérés hors connexion octet pour octet identiques ; idempotent = direct + gardes | Q-03-80 |
| Précontrôle (lecture seule) | 2 bases : base = rôle, `::1`:5433, PostgreSQL 18.6 ; 0001 et 0002 seules ; aucun objet `clients` ; références 1 / 999999 / 1000000 conformes sans toucher la séquence | Q-03-81 |
| `mmv_v2_dev` (10:55Z) | `PRECHECK OK`, `COMMIT`, exit 0 ; O-02-15 après `COMMIT` | Q-03-82, `evidence/P03A3_0003_apply_mmv_v2_dev_20261017T1055Z.log` |
| `mmv_v2_test` (10:55Z) | idem | Q-03-83, `evidence/P03A3_0003_apply_mmv_v2_test_20261017T1055Z.log` |
| Post-migration | 3 migrations ; objets conformes au SQL revu ; structures dev = test ; `mmv_v2_dev` : 0 client, séquence jamais appelée | Q-03-84, Q-03-96, `evidence/P03A3_postmigration_readonly_20261017T1056Z.log` |

Aucun état inattendu, aucun échec d'application, aucun retour arrière ni nettoyage de schéma. PostgreSQL 17 et autres bases non contactés. Migrations appliquées par base : `20261015125000_InitialBaseline`, `20261015223849_AuthIdentity`, `20261016230630_ClientsModule` (`10.0.4`).

## Dossier de migration 0003 — version 03-A2 à revoir (préparée ; *appliquée le 17/10/2026, voir ci-dessus*)

Migration EF `20261016230630_ClientsModule` (`src/MMV.Infrastructure/Persistence/Migrations/`), générée hors connexion (fabrique design-time sans chaîne de connexion). Scripts (BOM et CRLF retirés, comme 0002) :

| Fichier | Usage | SHA-256 |
|---|---|---|
| [`sql/0003_ClientsModule.sql`](sql/0003_ClientsModule.sql) | direct depuis `20261015223849_AuthIdentity` (relecture) | `7f61f0a826d6337a489ca988a8ffe6f185361395458674ef16c8b341eb3ce756` |
| [`sql/0003_ClientsModule.idempotent.sql`](sql/0003_ClientsModule.idempotent.sql) | **à appliquer** par `scripts/Invoke-MmvSqlScript.ps1` (comme O-02-01) | `4dbb0d84940d0891915adce2538fb0fee66e054bf4b3e9c347429c18d3db1c3b` |
| [`sql/0003_ClientsModule.rollback.sql`](sql/0003_ClientsModule.rollback.sql) | retour arrière vers `AuthIdentity` (destructif, O-03-13) | `8481adb044bc33a62ace3993ff65b4ea726016d4b577c6cdbd125f13f508a600` |

Écarts avec le SQL proposé en 03-A (diff réel) : séquence `MAXVALUE 999999999999999999` ; `reference varchar(24)` et défaut `to_char(…, 'FM999999999999000000')` au lieu de `varchar(16)` / `lpad(…, 6, '0')` ; contrainte `^CLI-[0-9]{6,18}$` au lieu de `^CLI-[0-9]{6,}$` ; identifiant de migration `20261016230630_ClientsModule`. Tout le reste est identique (schéma, 17 colonnes, 6 `CHECK`, 2 FK `RESTRICT`, 5 index, contrainte de permissions étendue, aucune colonne `xmin` créée).

*Amendement 17/10/2026 (03-A3) : les deux applications et les tests ci-dessous ont été exécutés (§ Exécution 03-A3) ; l'attribution des droits reste à faire par le propriétaire ([`PROCEDURE_PERMISSIONS_CLIENTS.md`](PROCEDURE_PERMISSIONS_CLIENTS.md)).* Procédure proposée après GO G03-SQL (non exécutée à la date de rédaction) : `./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev -Script docs/phase/03/sql/0003_ClientsModule.idempotent.sql -Sha256 4dbb0d84…` puis la même commande sur `mmv_v2_test` (secret `mmv-v2-postgresql-tests`, clé `ConnectionStrings:MmvTest`) ; `dotnet run --project src/MMV.Maintenance -- status` (3 migrations appliquées) ; `dotnet test MMV.slnx` complet (dont `Stage=PostMigration`, `IdentityWrite`, `ClientsWrite`) ; puis attribution des droits par le propriétaire : `dotnet run --project src/MMV.Maintenance -- permissions grant <identifiant> clients.read` (etc.). Jamais au démarrage, jamais `EnsureCreated()`.

### Historique — dossier proposé en 03-A (caduc)

Scripts : `docs/phase/03/sql/0003_ClientsModule.PROPOSED.sql` (direct depuis `AuthIdentity`), `.idempotent.sql`, `.rollback.sql`. Empreintes SHA-256 :

| Fichier | SHA-256 |
|---|---|
| `0003_ClientsModule.PROPOSED.sql` | `94545b84bd5ecfcf87a6b6d9f1dd4f361bb5024698bea7c968da3f5b91e03224` |
| `0003_ClientsModule.PROPOSED.idempotent.sql` | `8ea1f48af9ee2f09966e6034b6e5cbb37def4064cbbb1b60c75b34b6122d82f8` |
| `0003_ClientsModule.PROPOSED.rollback.sql` | `15fb8230722103e6f40068031a6a6950ccd7dc09d7e72f42407138806e91b78f` |

Le code C# correspondant n'est **pas** dans `src/` : il sera intégré en 03-A2 après GO, la migration étant alors régénérée si le modèle ratifié diffère (le nom horodaté changera). Procédure d'application prévue, identique à O-02-01 : `scripts/Invoke-MmvSqlScript.ps1` sur `mmv_v2_dev` puis `mmv_v2_test`, contrôle `__EFMigrationsHistory`, tests PostgreSQL complets, jamais au démarrage, jamais `EnsureCreated()`.

## Journal

- 17/10/2026 : G03-0 PASS ; 03-A1 implémenté (UI + tests) ; dossier G03-METIER, contrat, SQL proposé et cadrage 03-B présentés. État : **`PHASE03_A_UI_READY — OWNER_BUSINESS_RULES_AND_SQL_REVIEW_REQUIRED`**.
- 17/10/2026 : GO 03-A2 (G03-METIER A, B acceptés ; C sur le principe ; D en attente). 03-A2 implémenté : backend Clients, OpenAPI typé, commande `permissions`, interface sur la vraie API, migration 0003 et SQL exact préparés **non appliqués** ; tests sans migration PASS, écriture Clients `NOT_RUN`. État : **`PHASE03_A2_READY — OWNER_SQL_REVIEW_REQUIRED`**.
- 17/10/2026 : GO 03-A3. Migration 0003 appliquée (`mmv_v2_dev`, puis `mmv_v2_test`) ; tests réels PASS après deux défauts **de tests** corrigés (attente `23001`, horloge du double) ; permissions de session dans l'interface (O-03-09) ; procédure d'attribution prête, compte réel non modifié ; parcours natif et comparaison aux planches PASS. État : **`PHASE03_A3_COMPLETE — OWNER_REVIEW_REQUIRED`**. La Phase 03 n'est pas achevée (03-B, 03-C non commencés).
