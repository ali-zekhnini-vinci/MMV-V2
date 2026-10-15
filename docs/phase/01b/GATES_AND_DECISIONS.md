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
