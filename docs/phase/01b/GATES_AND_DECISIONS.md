# 01B — GATES_AND_DECISIONS

## GO et périmètre

- 15/10/2026 : « GO OFFICIEL PHASE 01B » (après merge 01A `4ed20a8`). Couvre le développement et les tests bornés de 01B. **Ne couvre pas** sans accord ciblé supplémentaire : `CREATE ROLE`/`CREATE DATABASE`/`GRANT`, migrations, écritures de tests sur PostgreSQL, modification du magasin de certificats Windows.

| IN | OUT |
|---|---|
| PG18 `localhost:5433`, nouvelles bases `mmv_v2_dev` / `mmv_v2_test` (après accord) | PG17/5432, autres bases, `postgresql.conf`, `pg_hba.conf`, pare-feu, rôle `postgres`, `DROP`, `ALTER SYSTEM` |
| `GET /health/ready` réel, `/health/live` inchangé | API métier, CRUD exposé, données de démonstration |
| Migration EF Core baseline technique (vide), test transactionnel avec rollback | Table métier ou fictive, `EnsureCreated()`, migration au démarrage |
| HTTPS `localhost:7267` avec certificat de dev approuvé (après accord) | TLS LAN, CA privée, certificat de production (ADR-006 OPEN) |
| Client API typé Tauri, écran socle technique, CSP dev, CORS restreint | Login/Shell, RBAC, appairage, installateur, Windows Service, multi-postes |

## Gates 01B

| Gate | Critère | Statut au 15/10/2026 |
|---|---|---|
| G1B-0 Préflight | merge 01A vérifié, branche dédiée, outils | PASS (QA Q-B01..Q-B07) |
| P01-04 PG exclusif | bases V2 isolées, migration + aller-retour transactionnel réel | BLOCKED_OWNER_APPROVAL (B-SQL) |
| P01-05 Monoposte | Tauri → HTTPS → API → PG18, capture réelle | NOT_TESTED |
| P01-07 TLS confiance | **volet local seulement** : HTTPS vérifié sur `localhost`; volet LAN/CA/rotation | local : BLOCKED_OWNER_APPROVAL (B-TLS) ; LAN : NOT_TESTED (hors 01B) |
| P01-15 Graphify | CLI + requête utile du nouveau flux | partiel (préflight PASS) |
| P01-16 Serena | C# et TypeScript | C# PASS (Q-B06) ; TS NOT_TESTED (O-B05) |

## B-SQL — plan exact soumis à autorisation (NON EXÉCUTÉ)

**Étape 1 — lecture seule** (à exécuter par le propriétaire avec `psql -h localhost -p 5433 -U postgres -d postgres`, l'agent n'ayant pas d'identifiant) :

```sql
SELECT version(); SHOW port; SHOW data_directory; SHOW listen_addresses;
SELECT current_user, inet_server_port();
SELECT datname FROM pg_database ORDER BY 1;
SELECT rolname FROM pg_roles WHERE rolname LIKE 'mmv%' ORDER BY 1;
```

Si `mmv_v2_dev` ou `mmv_v2_test` (base ou rôle) existe déjà : STOP, inspection de la propriété, aucune modification.

**Étape 2 — création** (même session administrateur, mots de passe saisis par le propriétaire via `\password`, jamais transmis à l'agent) :

```sql
CREATE ROLE mmv_v2_dev  LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 10;
CREATE ROLE mmv_v2_test LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 10;
\password mmv_v2_dev
\password mmv_v2_test
CREATE DATABASE mmv_v2_dev  OWNER mmv_v2_dev  TEMPLATE template0 ENCODING 'UTF8';
CREATE DATABASE mmv_v2_test OWNER mmv_v2_test TEMPLATE template0 ENCODING 'UTF8';
REVOKE ALL ON DATABASE mmv_v2_dev  FROM PUBLIC;
REVOKE ALL ON DATABASE mmv_v2_test FROM PUBLIC;
```

Aucun `GRANT` supplémentaire : chaque rôle est propriétaire de sa seule base (et, en PG ≥ 15, du schéma `public` via `pg_database_owner`).

**Étape 3 — secrets (propriétaire)** : `dotnet user-secrets` pour l'API (`mmv_v2_dev`) et configuration séparée pour les tests (`mmv_v2_test`) ; commandes exactes fournies au moment voulu.

**Étape 4 — migrations** : migration EF Core baseline **vide** (seule `__EFMigrationsHistory` est créée), script SQL généré et relu, appliqué à `mmv_v2_dev` puis `mmv_v2_test` avec le rôle propriétaire.

**Étape 5 — tests** : tests d'intégration sur `mmv_v2_test` uniquement (garde dbname/port/version), table temporaire dans une transaction annulée.

**Limites connues et assumées pour le POC (à arbitrer) :**
- B-01 : rôle unique propriétaire = exécution **et** migration, limité à la phase de développement. Séparation `mmv_migrator` / `mmv_app` (DML seul) recommandée avant tout produit.
- B-02 : PostgreSQL accorde par défaut `CONNECT` à `PUBLIC` sur les bases existantes ; les nouveaux rôles pourraient donc s'y **connecter** (sans droit sur leurs objets au-delà de PUBLIC). Le restreindre exige de modifier d'autres bases ou `pg_hba.conf`, ce qui est **hors périmètre** : non fait.
- B-03 : PG18 écoute sur `0.0.0.0:5433` / `[::]:5433` (constat Q-B04). Non modifié ; l'API utilisera `localhost`. Le durcissement réseau de PG relève de l'installation serveur (phase ultérieure).

## B-TLS — plan soumis à autorisation (NON EXÉCUTÉ)

`dotnet dev-certs https --trust` : ajoute le certificat existant `6C48AE5E0BD7B0E67032257F893C94EC27CC3252` (`CN=localhost`) au magasin **Racines de confiance de l'utilisateur courant** (boîte de dialogue Windows). Aucune suppression/régénération (`--clean` exclu). Réversible par le propriétaire. Ce certificat n'est **pas** un certificat déployable (ADR-006 OPEN).

## Décisions ouvertes 01B

| ID | Question | Bloque |
|---|---|---|
| O-B01 | Autoriser B-SQL étapes 1→2 (rôles + bases) | P01-04, P01-05 |
| O-B02 | Autoriser migrations baseline (étape 4) et tests d'écriture éphémère sur `mmv_v2_test` (étape 5) | P01-04 |
| O-B03 | Autoriser B-TLS (`dotnet dev-certs https --trust`) | P01-05, P01-07 local |
| O-B04 | Valider le choix B-01 (rôle unique dev) et prendre acte de B-02/B-03 | P01-04 |
| O-B05 | Serena TypeScript : ajouter `typescript` aux `language_servers` de `.serena/project.yml` (fichier local non versionné) | P01-16 volet TS |
| O-B06 | Graphify MCP : reste `NOT_CONFIGURED` (non essentiel à 01B) sauf demande | — |

ADR-004, ADR-006, ADR-007 restent non ratifiés ; 01B ne les approuve pas.

## Amendement daté — 15/10/2026, après validation PostgreSQL par le propriétaire

Source : [`prompts/PHASE01B_VALIDATION_POSTGRESQL_SQL_REVIEW.md`](../../../prompts/PHASE01B_VALIDATION_POSTGRESQL_SQL_REVIEW.md). Les tableaux ci-dessus restent l'état historique du préflight.

### Décisions du propriétaire enregistrées

| ID | Décision | Preuve |
|---|---|---|
| O-B01 | **PASS (déclaré par le propriétaire)** : B-SQL étapes 1→2 exécutées par le propriétaire. PG 18.6 / 5433 ; rôles `mmv_v2_dev`, `mmv_v2_test` authentifiés sur leur base ; aucun privilège administrateur global ; `PUBLIC` révoqué sur les deux bases | déclaration du propriétaire ; **vérification agent en lecture seule préparée** (`TargetIdentityTests`), `NOT_TESTED` tant que les user-secrets ne sont pas saisis |
| O-B03 | **PASS (confiance locale)** : certificat `6C48AE5E0BD7B0E67032257F893C94EC27CC3252` approuvé par le propriétaire | `dotnet dev-certs https --check --trust` → « A trusted certificate was found » (Q-B12) |
| O-B05 | **Accordé** (« Utilise Serena C#/TypeScript ») : `typescript` ajouté à `.serena/project.yml` (local, non versionné) | effectif au **redémarrage** du serveur MCP Serena (Q-B16) |
| O-B02 | **Redéfini** : couvre désormais uniquement les **tests d'écriture** (table temporaire + `ROLLBACK`) sur `mmv_v2_test`. L'application de la migration relève de O-B07 | instruction du propriétaire du 15/10/2026 |

### Gates 01B — état mis à jour

| Gate | Statut au 15/10/2026 (amendement) |
|---|---|
| P01-04 PG exclusif | bases et rôles créés (O-B01) ; identité vérifiée par l'agent `NOT_TESTED` (secrets non saisis) ; migration **générée, non appliquée** → `BLOCKED_OWNER_APPROVAL` (O-B07) ; aller-retour transactionnel `BLOCKED_OWNER_APPROVAL` (O-B02) |
| P01-05 Monoposte | `NOT_TESTED` (client, CSP, CORS implémentés ; fenêtre Tauri + PG réel non exécutés) |
| P01-07 TLS local | HTTPS réel `https://localhost:7267` validé par curl/Schannel sans contournement (Q-B13) ; depuis Tauri `NOT_TESTED` ; LAN `NOT_TESTED` (hors 01B, ADR-006 OPEN) |
| P01-15 Graphify | CLI PASS sur le nouveau flux (Q-B17) ; MCP `NOT_CONFIGURED` |
| P01-16 Serena | C# PASS (Q-B15) ; TypeScript `BLOCKED` : redémarrage MCP requis (Q-B16) |

### O-B07 — revue du script SQL de la migration initiale (NON APPLIQUÉ)

**Script à approuver :** [`docs/phase/01b/sql/0001_InitialBaseline.idempotent.sql`](sql/0001_InitialBaseline.idempotent.sql) — SHA-256 `9c8077a5f016002e3e2e69373cbbaf8ac86da1b14d1ee16e90678564c657c784` (544 octets, ASCII, LF). Variante non idempotente pour lecture : [`0001_InitialBaseline.sql`](sql/0001_InitialBaseline.sql) — SHA-256 `b3798b80dab442a37e541f5ef3bb197e1f3adc97e25c96db0c00c78a2fab7fb1`. Générés hors connexion par `dotnet ef migrations script` (Q-B10) ; seule normalisation : suppression du BOM UTF-8 et fins de ligne LF (contenu SQL inchangé).

```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261015125000_InitialBaseline') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261015125000_InitialBaseline', '10.0.4');
    END IF;
END $EF$;
COMMIT;
```

**Analyse d'impact :**

| Axe | Effet |
|---|---|
| Objets créés | 1 table technique `public."__EFMigrationsHistory"` (2 colonnes) + son index de clé primaire `PK___EFMigrationsHistory`. Aucune table métier ou fictive, aucune séquence, extension, fonction persistante (le bloc `DO` est anonyme), vue, trigger ni type |
| Données | 1 ligne d'historique (`20261015125000_InitialBaseline`, `10.0.4`). Aucune donnée métier ou de démonstration |
| Propriété / privilèges | objets possédés par le rôle exécutant (`mmv_v2_dev` ou `mmv_v2_test`), dans le schéma `public` dont le rôle est propriétaire via `pg_database_owner` (PG ≥ 15). Aucun `GRANT`/`REVOKE`, aucun changement de rôle |
| Périmètre | une seule base par exécution (`-d`), instance 5433 uniquement ; PG17/5432, `postgres` et autres bases non concernés |
| Verrous / disponibilité | verrou exclusif sur la seule table créée ; bases neuves sans utilisateur |
| Idempotence | relance sans effet (`IF NOT EXISTS` pour la table et la ligne). Le `CREATE TABLE` est hors transaction : en cas d'échec du bloc `DO`, la table vide reste et une relance termine l'opération |
| Réversibilité | `DROP TABLE "__EFMigrationsHistory";` par le rôle propriétaire (retour à la base vide) ; non exécuté sans décision |
| Ce que cela ne prouve pas | `/health/ready` ne vérifie que la connexion, pas l'état des migrations ; aucune migration n'est déclenchée au démarrage de l'API (aucun `Migrate()`/`EnsureCreated()` dans `src/`, vérifié par recherche) |
| Outillage | `dotnet-ef` global 10.0.2 < runtime EF Core 10.0.4 (avertissement non bloquant, sans effet sur ce script) ; `ProductVersion` = 10.0.4 |

**Procédure proposée après GO O-B07 (non exécutée) :**

1. Propriétaire : saisir les user-secrets (`IMPLEMENTATION_SPEC.md` § Secrets) ; agent : `dotnet test tests/MMV.PostgreSql.Tests` → `DevDatabase_IdentityMatches` et `TestDatabase_IdentityMatches` doivent être **PASS** (lecture seule) avant toute écriture.
2. Propriétaire (détient les mots de passe ; `psql` les demande sans écho) :
   ```powershell
   (Get-FileHash docs\phase\01b\sql\0001_InitialBaseline.idempotent.sql -Algorithm SHA256).Hash   # doit valoir 9C8077A5…C784
   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -p 5433 -U mmv_v2_dev  -d mmv_v2_dev  -v ON_ERROR_STOP=1 -f docs\phase\01b\sql\0001_InitialBaseline.idempotent.sql
   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -p 5433 -U mmv_v2_test -d mmv_v2_test -v ON_ERROR_STOP=1 -f docs\phase\01b\sql\0001_InitialBaseline.idempotent.sql
   ```
3. Contrôle en lecture seule sur chaque base : `SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory";` → une ligne ; `\dt` → seule `__EFMigrationsHistory`.

### Décisions ouvertes (ajoutées)

| ID | Question | Bloque |
|---|---|---|
| O-B07 | GO sur le script `0001_InitialBaseline.idempotent.sql` (SHA-256 ci-dessus) et sa procédure d'application à `mmv_v2_dev` puis `mmv_v2_test` | P01-04, P01-05 (`ready` = Healthy possible sans migration, mais P01-04 l'exige) |
| O-B02 | GO tests d'écriture éphémère (table temporaire, `ROLLBACK`) sur `mmv_v2_test` uniquement | P01-04 |
| O-B04 | Toujours ouverte : validation écrite du rôle unique dev (B-01), prise d'acte B-02/B-03 | P01-04 |
| O-B08 | Prendre acte : tests frontend avec `node:test` natif (Node 22, aucune dépendance) au lieu de Vitest, npm 10.9.9 plantant à la résolution (`Cannot read properties of null (reading 'edgesOut')`, Q-B14) | — |
| O-B09 | Optionnel : épingler `dotnet-ef` 10.0.4 en outil local (`.config/dotnet-tools.json`) au lieu de l'outil global 10.0.2 | — |

## Amendement daté — 15/10/2026 (soir), GO final et terminaison 01B

Source : [`prompts/PHASE01B_GO_FINAL_TERMINAISON.md`](../../../prompts/PHASE01B_GO_FINAL_TERMINAISON.md). Les tableaux et amendements précédents restent l'état historique.

### Décisions et faits du propriétaire enregistrés

| ID | Décision / fait | Preuve |
|---|---|---|
| O-B07 | **Exécuté par le propriétaire** : `0001_InitialBaseline.idempotent.sql` (SHA-256 `9C8077A5…C784` approuvé) appliqué avec `psql` sur `mmv_v2_dev` (rôle `mmv_v2_dev`) puis `mmv_v2_test` (rôle `mmv_v2_test`), PG18/5433 ; sorties `CREATE TABLE`, `START TRANSACTION`, `DO`, `COMMIT`, **DEV exit=0**, **TEST exit=0** | déclaration du propriétaire ; état vérifié par l'agent en lecture seule (Q-B18) |
| NOTICE TEST | `__EFMigrationsHistory` « existe déjà » sur `mmv_v2_test` : une première tentative avait réussi sur TEST (mot de passe DEV erroné, TEST correct) ; la relance du script idempotent n'a rien dupliqué. **Pas d'enquête supplémentaire** (instruction du propriétaire) | Q-B18 : une seule ligne d'historique sur chaque base, clé primaire présente |
| O-B02 | **Exercé** : écriture éphémère (table temporaire + `ROLLBACK`) sur `mmv_v2_test` uniquement | Q-B19 |
| O-B08 | **Remplacé** : retour à Vitest demandé par le propriétaire ; adopté proprement (Vitest 5.0.3 + `overrides`), `node --test` retiré | Q-B24 |
| O-B05 | Configuration Serena TypeScript : fonctionnelle (CLI) ; session MCP de Claude Code à reconnecter | Q-B26 |

### Gates 01B — état final proposé (revue propriétaire requise)

| Gate | Statut au 15/10/2026 (terminaison) | Preuve |
|---|---|---|
| G1B-0 Préflight | PASS (inchangé) | Q-B01..Q-B07 |
| P01-04 PG exclusif | **PASS (périmètre 01B)** : bases isolées, identité/version/port vérifiés, migration baseline appliquée et cohérente avec EF Core, aller-retour transactionnel réel avec `ROLLBACK` et sans résidu. CRUD EF Core sur une entité réelle `NOT_TESTED` (aucune table métier autorisée en 01B) | Q-B18, Q-B19, Q-B21 |
| P01-05 Monoposte | **PASS (développement local)** : fenêtre Tauri native → HTTPS → MMV.Api → PG18, 4 états réels capturés, Origin WebView2 relevé, aucun accès direct client → PG | Q-B20, Q-B22, Q-B23 |
| P01-07 TLS confiance | **local : PASS** (WebView2 et Schannel valident le certificat de dev approuvé, aucune exception) ; refus d'un certificat non approuvé depuis WebView2 `NOT_TESTED` (exigerait de modifier le magasin) ; **LAN/CA/rotation : NOT_TESTED** (hors 01B, ADR-006 OPEN) | Q-B13, Q-B20, Q-B22 |
| P01-15 Graphify | PASS (CLI) ; MCP `NOT_CONFIGURED` | Q-B27 |
| P01-16 Serena | C# PASS ; TypeScript PASS via `serena project health-check` ; session MCP Claude Code : `BLOCKED` jusqu'à reconnexion (`/mcp` → `serena` → Reconnect) | Q-B26 |

**Statut de phase proposé : `01B IMPLEMENTED — OWNER_REVIEW_REQUIRED`** (aucun push, PR ni merge).

### Décisions restant ouvertes après 01B

| ID | Question | Impact |
|---|---|---|
| O-B04 | Toujours ouverte : rôle unique propriétaire (B-01) accepté pour le développement ; prise d'acte B-02 (`CONNECT` PUBLIC sur les autres bases) et B-03 (PG18 écoute `0.0.0.0:5433`) | à trancher avant tout environnement non-dev ; séparation `mmv_migrator`/`mmv_app` recommandée |
| O-B09 | Épingler `dotnet-ef` 10.0.4 en outil local : un `dotnet-tools.json` (racine) existe dans l'arbre de travail mais **n'est pas commité** faute de décision | outillage seulement |
| O-B10 | Profil `https` : écoute aussi `http://localhost:5022` (loopback, hérité du modèle 01A). Le client n'utilise que HTTPS (`config.ts` refuse `http:`). Retirer l'écoute HTTP du profil `https` ? | durcissement, non bloquant |
| O-B11 | Adresse du serveur en production (bundle sans `VITE_MMV_API_BASE_URL` → « non configuré ») et `csp` de production sans origine d'API : à définir avec ADR-006 / appairage | prérequis du premier poste réel |
| O-B12 | Planification des éléments de « Phase 01 » de `docs/PERIMETRE_ET_SEQUENCE.md` non couverts par 01A/01B : réseau sécurisé LAN/TLS, Windows Service | à placer avant ou pendant 02 (décision propriétaire) |

ADR-004, ADR-006, ADR-007 restent non ratifiés ; 01B ne les approuve pas.
