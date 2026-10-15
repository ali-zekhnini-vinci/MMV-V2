# 02 — GATES_AND_DECISIONS

## GO et bornes

- 16/10/2026 : « GO OFFICIEL PHASE 02 » du propriétaire. GO pour développer, générer les migrations d'identité et tester sur doubles non persistants.
- **Borne unique :** appliquer la migration d'identité à `mmv_v2_dev` / `mmv_v2_test` et exécuter le bootstrap administrateur réel exigent **un seul** dossier de décision (O-02-01), puis un GO unique du propriétaire. Aucune modification de PG17/5432, des rôles `postgres`, des autres bases ni des certificats système.

## Gates Phase 02

| Gate | Critère | État |
|---|---|---|
| G-02-A | Login/Shell conformes aux références, états, clavier, thème, tests | implémenté ; tests DOM PASS ; preuve native `NOT_TESTED` (02-C) |
| G-02-B | Identité serveur, hachage Identity, policies, 401/403, bootstrap local refusé la 2e fois | implémenté ; tests sur doubles non persistants PASS ; partie PostgreSQL `BLOCKED_OWNER_APPROVAL` (O-02-01) |
| G-02-C | Tauri → HTTPS → API → PG réel, login/logout/révocation natifs, captures, résilience | banc natif prêt ; login natif (états sans base) PASS ; parcours authentifié `BLOCKED_OWNER_APPROVAL` (O-02-01) |
| G-02-DOC | DOCUMENTATION GATE | en cours |

## Décisions ouvertes

| ID | Sujet | État | Bloque |
|---|---|---|---|
| O-02-01 | GO unique : migration d'identité sur `mmv_v2_dev` + `mmv_v2_test` et bootstrap administrateur réel | OPEN — dossier ci-dessous, présenté le 16/10/2026 | G-02-B (partie PG), G-02-C |
| O-02-09 | Zoom clavier WebView2 activé (`zoomHotkeysEnabled`, Ctrl +/−) : accessibilité et preuve native des zooms 125/150 % | PROPOSED | aucune |
| O-02-10 | Écoute HTTP 5022 (O-B10) : les routes `/api` refusent le HTTP (403 `https_required`) ; santé inchangée | PROPOSED | aucune |
| O-02-02 | Variante de session dev (jeton opaque volatile) — ADR-007 | PROPOSED (voir ci-dessous) | ratification ADR-007, distribution |
| O-02-03 | Login : mot de passe vidé après échec (variante recommandée §13) ; bandeau sous le sous-titre ; validation non « live » | PROPOSED | aucune (réversible) |
| O-02-04 | Bouton « Afficher le mot de passe » dans l'ordre de tabulation (écart à §11 du handoff) | PROPOSED | aucune |
| O-02-05 | Embarquement des polices Inter / Source Sans 3 (fichiers OFL locaux) | OPEN — non embarquées, repli Segoe UI | fidélité typographique |
| O-02-06 | Versionnement des 4 logos officiels dans `apps/desktop/src/assets/brand/` (droits de redistribution) | OPEN — versionnés localement, non poussés | push |
| O-02-07 | Recherche globale et notifications : contrôles visibles mais désactivés (« Disponible dans une phase suivante ») | PROPOSED | aucune |
| O-02-08 | Libellé « Accueil » (au lieu de « Tableau de bord ») tant qu'aucun tableau de bord réel n'existe ; 5 variantes Lot 1 au backlog | PROPOSED | aucune |

Héritées de 01B, inchangées : O-B04 (rôle unique propriétaire), O-B10 (écoute HTTP 5022), O-B11 (adresse serveur en production), O-B12 ; ADR-004/006/007 non ratifiés.

## O-02-02 — Session : comparaison et variante retenue pour le POC local (PROPOSED, provisoire dev)

| Option | Avantages | Risques / coûts |
|---|---|---|
| **A. Jeton opaque aléatoire (256 bits), session serveur révocable, en mémoire volatile du client** (retenue) | aucun cookie → pas de CSRF ni de cookie tiers (origine Tauri ≠ origine API) ; révocation immédiate (ligne de session) ; seul un **hachage SHA-256** du jeton est stocké en base ; aucune persistance poste ; CORS sans `credentials` | jeton lisible par du JavaScript exécuté dans la WebView (XSS) → atténué par CSP stricte `script-src 'self'` ; reconnexion à chaque lancement ; requêtes protégées = préflight CORS (`Authorization`) |
| B. Cookie `HttpOnly; Secure; SameSite=None` + session serveur + anti-CSRF | jeton inaccessible au JavaScript | cookie **tiers** entre `tauri.localhost`/`localhost:1420` et `localhost:7267` : comportement WebView2 à prouver ; CORS `AllowCredentials` + jeton anti-CSRF ; persistance dans le profil WebView2 |
| C. Jeton de rafraîchissement dans le coffre OS (Credential Manager) | session survivant au redémarrage, secret protégé par l'OS | plugin Tauri/Rust supplémentaire, persistance longue durée, rotation/révocation à concevoir ; hors POC 02 |

Paramètres proposés (dev) : durée absolue **8 h**, inactivité **30 min** (glissement à chaque requête authentifiée), aucun rafraîchissement ; révocation à la déconnexion, à la désactivation du compte (vérifiée à chaque requête) et par expiration. **Ce choix n'est pas une session distribuable approuvée** ; ADR-007 reste `PROPOSED`.

## O-02-01 — Dossier de décision unique : migration d'identité + bootstrap (16/10/2026, NON EXÉCUTÉ)

**Demande :** un GO unique couvrant (1) l'application de `0002_AuthIdentity.idempotent.sql` à `mmv_v2_dev` puis `mmv_v2_test`, (2) les tests d'identité réels sur `mmv_v2_test`, (3) le bootstrap du premier administrateur réel sur `mmv_v2_dev` par le propriétaire.

### Scripts SQL (générés hors connexion par `dotnet ef`, fabrique design-time sans secret)

Normalisation (comme 01B) : BOM UTF-8 retiré, fins de ligne LF — empreinte identique dans Git et dans la copie de travail ; contenu SQL inchangé (ASCII).

| Fichier | Rôle | SHA-256 |
|---|---|---|
| `docs/phase/02/sql/0002_AuthIdentity.sql` | lecture (non idempotent, `InitialBaseline` → `AuthIdentity`) | `A5F40575C04B1E25F7B356558F2203B13CCB5BF7936E0C72077132682DF10192` |
| `docs/phase/02/sql/0002_AuthIdentity.idempotent.sql` | **script à appliquer** (gardes `__EFMigrationsHistory`, une transaction) | `67C15100A8D27FD91D4CD869B927774CC537E4275F60C3FCDCA28161495F5A41` |
| `docs/phase/02/sql/0002_AuthIdentity.rollback.sql` | retour arrière (`AuthIdentity` → `InitialBaseline`) | `7DEAB41DD479035E7E3E8F3849E926404679B2B005C0B1463BC6748730DF682E` |

Migration EF Core : `src/MMV.Infrastructure/Persistence/Migrations/20261015223849_AuthIdentity.cs` (+ `Designer`, snapshot). La migration `20261015125000_InitialBaseline` est **inchangée**.

### Impact (schéma `auth`, propriétaire = rôle exécutant, aucun `GRANT`/`REVOKE`)

| Objet | Contenu |
|---|---|
| `CREATE SCHEMA auth` | dans la base possédée par le rôle (droit `CREATE` du propriétaire) |
| `auth.users` | `id uuid` PK, `user_name varchar(64)`, `normalized_user_name varchar(64)` **unique**, `display_name varchar(128)`, `password_hash text` (format Identity V3), `is_active`, `failed_login_count` (CHECK ≥ 0), `lockout_end_utc`, `created_at_utc` |
| `auth.user_permissions` | PK (`user_id`, `permission`), FK → users **ON DELETE CASCADE**, CHECK `permission IN ('system.admin')` |
| `auth.sessions` | `id uuid` PK, FK `user_id` → users CASCADE, `token_hash bytea` **unique** (CHECK 32 octets), `created_at_utc`, `last_seen_at_utc`, `expires_at_utc` (CHECK > création), `revoked_at_utc`, `revocation_reason varchar(32)`, index `ix_sessions_user_id` |
| `auth.bootstrap_record` | `id smallint` PK + CHECK `id = 1` (singleton : second bootstrap impossible, même concurrent), `administrator_id`, `completed_at_utc` ; sans FK (trace conservée) |
| `public."__EFMigrationsHistory"` | + 1 ligne `20261015223849_AuthIdentity / 10.0.4` |

Aucune donnée insérée par le script (aucun compte, aucun mot de passe). Aucune table métier.

### Vérification de cible (avant toute écriture)

`scripts/Invoke-MmvSqlScript.ps1` : refus **sans connexion** si l'empreinte du script diffère, si la chaîne des user-secrets ne vise pas `localhost|127.0.0.1|::1`, port 5433, base **et** rôle `mmv_v2_dev` / `mmv_v2_test` ; puis contrôle **côté serveur** `current_database()`, `current_user`, `inet_server_port()`, `server_version_num ≥ 180000` ; mot de passe lu dans les user-secrets existants, passé au seul processus `psql` (`PGPASSWORD`, effacé ensuite), jamais affiché ni journalisé. Chemins de refus testés (empreinte fausse, base non autorisée).

État constaté avant GO (lecture seule, `docs/phase/02/evidence/P02_preGO_state_readonly_20261016T0050.log`) : les deux bases = **baseline 01B seule** (`*_ContainsOnlyAKnownState` PASS) ; `MMV.Maintenance status` : 1 migration en attente (`AuthIdentity`) ; `bootstrap-admin` refusé (code 2, migration en attente).

### Procédure proposée après GO (exécutée par l'agent, sauf l'étape 4)

1. `./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev -Script docs/phase/02/sql/0002_AuthIdentity.idempotent.sql -Sha256 67C15100…5F5A41`
2. Idem `-SecretsId mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Database mmv_v2_test`.
3. Tests : `dotnet test MMV.slnx` (dont `Stage=PostMigration` sur les deux bases en lecture seule, et `Stage=IdentityWrite` sur `mmv_v2_test` uniquement : transactions annulées + test HTTP avec 2 comptes éphémères supprimés en fin de test). Aucune écriture de test sur `mmv_v2_dev`.
4. **Propriétaire**, dans son propre terminal (saisie masquée, rien en argument) : `dotnet run --project src/MMV.Maintenance -- bootstrap-admin` → confirmer le nom de la base (`mmv_v2_dev`), saisir identifiant, nom affiché, mot de passe (≥ 12 caractères) deux fois. Puis `dotnet run --project src/MMV.Maintenance -- bootstrap-admin` une seconde fois → **refus attendu, code 3** (preuve reproductible).
5. Agent : `MMV.Maintenance status` (lecture seule) ; E2E natif Tauri → HTTPS → API → **`mmv_v2_test`** avec comptes éphémères (aucune connexion au compte réel du propriétaire) ; captures.

### Approche de test et reprise

- **Échec pendant le script** : `ON_ERROR_STOP` + transaction unique → rien n'est appliqué ; correction puis relance (idempotent).
- **Retour arrière** (sur décision) : `0002_AuthIdentity.rollback.sql` par le même script de contrôle (empreinte ci-dessus), puis `DROP SCHEMA auth;` (le rollback EF ne supprime pas le schéma vide). Les sessions et comptes sont supprimés avec les tables.
- **Bootstrap raté** (saisie invalide, annulation) : aucun objet créé ; relancer. Bootstrap réussi sur un mauvais compte : suppression manuelle par le propriétaire (`DELETE FROM auth.users …; DELETE FROM auth.bootstrap_record;`) — non automatisé, volontairement.
- Les tests `Stage=PostMigration` échouent **avant** application (constaté) et doivent passer après.
