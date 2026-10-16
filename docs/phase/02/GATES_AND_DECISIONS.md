# 02 — GATES_AND_DECISIONS

## GO et bornes

- 16/10/2026 : « GO OFFICIEL PHASE 02 » du propriétaire. GO pour développer, générer les migrations d'identité et tester sur doubles non persistants.
- **Borne unique :** appliquer la migration d'identité à `mmv_v2_dev` / `mmv_v2_test` et exécuter le bootstrap administrateur réel exigent **un seul** dossier de décision (O-02-01), puis un GO unique du propriétaire. Aucune modification de PG17/5432, des rôles `postgres`, des autres bases ni des certificats système.

## Gates Phase 02

| Gate | Critère | État |
|---|---|---|
| G-02-A | Login/Shell conformes aux références, états, clavier, thème, tests | implémenté ; tests DOM PASS ; preuve native `NOT_TESTED` (02-C). **16/10/2026 (corrections finales)** : pile Tailwind CSS 4 / shadcn/ui + Radix / React Hook Form + Zod / React Router / polices locales intégrée ; 81/81 PASS ; non-régression visuelle PASS (Q-02-40) ; natif sans base PASS (Q-02-41). **16/10/2026 (finalisation)** : Login/Shell natifs sur API et PostgreSQL réels PASS (Q-02-52) ; 81/81 PASS, lint sans exclusion après suppression du banc `.visual` (Q-02-58, Q-02-59) → **PASS** |
| G-02-B | Identité serveur, hachage Identity, policies, 401/403, bootstrap local refusé la 2e fois | implémenté ; tests sur doubles non persistants PASS ; partie PostgreSQL `BLOCKED_OWNER_APPROVAL` (O-02-01). **16/10/2026** : architecture jugée conforme par la revue ; contrôle strict de cible de `bootstrap-admin` PASS (Q-02-28 à Q-02-30). **16/10/2026 (GO O-02-01 exécuté)** : migration appliquée aux deux bases, `PostMigration` 2/2, `IdentityWrite` 5/5 (Q-02-46 à Q-02-49) ; bootstrap réel par le propriétaire (exit 0, Q-02-50) ; second bootstrap refusé **code 3** (Q-02-51) ; matrice 401/403 sur le processus API réel 19/19 (Q-02-53) → **PASS** |
| G-02-C | Tauri → HTTPS → API → PG réel, login/logout/révocation natifs, captures, résilience | banc natif prêt ; login natif (états sans base) PASS ; parcours authentifié `BLOCKED_OWNER_APPROVAL` (O-02-01). **16/10/2026 (finalisation)** : Tauri → HTTPS → MMV.Api → `mmv_v2_test` avec comptes éphémères : login, refus neutre, session, permissions (non-admin, 403 après retrait), déconnexion, révocation serveur, désactivation, résilience API et PostgreSQL, redémarrage client, 27 captures (Q-02-52 à Q-02-55) ; journaux sans secret (Q-02-56) → **PASS** (limites : Q-02-62) |
| G-02-DOC | DOCUMENTATION GATE | synchronisé le 16/10/2026 (corrections finales) ; à reprendre après O-02-01. **16/10/2026 (finalisation)** : resynchronisé après exécution d'O-02-01 et preuve native (README, `02.md`, QA, spec, source-map, gates, ADR, périmètre) → **PASS** |

## Décisions ouvertes

| ID | Sujet | État | Bloque |
|---|---|---|---|
| O-02-01 | GO unique : migration d'identité sur `mmv_v2_dev` + `mmv_v2_test` et bootstrap administrateur réel | OPEN — dossier ci-dessous, présenté le 16/10/2026. **GO accordé le 16/10/2026 (« GO O-02-01 ACCORDÉ ») et exécuté** : § Exécution O-02-01 | — (levé) |
| SEC-02-01 | **Incident** : mots de passe PostgreSQL de `mmv_v2_dev` et `mmv_v2_test` affichés dans une session agent le 16/10/2026 | OPEN — **rotation des deux mots de passe requise** (action propriétaire) : § Incident SEC-02-01 | distribution, revue de sécurité |
| O-02-14 | Base injoignable : une requête `/api` authentifiée aboutit à une exception non gérée (500 générique, journalisée sans secret) au lieu d'un 503 explicite ; le client n'affiche aucun faux succès | PROPOSED (16/10/2026) — à traiter avec la résilience des modules métier | aucune |
| O-02-15 | `Invoke-MmvSqlScript.ps1` : la requête d'**affichage** finale (après `COMMIT`) échoue sous Windows PowerShell 5.1 (guillemets de `"__EFMigrationsHistory"` retirés au passage à `psql.exe`) ; sans effet sur l'application | PROPOSED (16/10/2026) — correction à soumettre au propriétaire (le script approuvé n'est pas modifié sans GO) | aucune |
| O-02-09 | Zoom clavier WebView2 activé (`zoomHotkeysEnabled`, Ctrl +/−) : accessibilité et preuve native des zooms 125/150 % | PROPOSED | aucune |
| O-02-10 | Écoute HTTP 5022 (O-B10) : les routes `/api` refusent le HTTP (403 `https_required`) ; santé inchangée | PROPOSED | aucune |
| O-02-02 | Variante de session dev (jeton opaque volatile) — ADR-007 | PROPOSED (voir ci-dessous) | ratification ADR-007, distribution |
| O-02-03 | Login : mot de passe vidé après échec (variante recommandée §13) ; bandeau sous le sous-titre ; validation non « live » | PROPOSED | aucune (réversible) |
| O-02-04 | Bouton « Afficher le mot de passe » dans l'ordre de tabulation (écart à §11 du handoff) | PROPOSED | aucune |
| O-02-05 | Embarquement des polices Inter / Source Sans 3 (fichiers OFL locaux) | **RESOLVED 16/10/2026** (consigne du propriétaire, corrections finales) : embarquées localement, SIL OFL 1.1, paquets `@fontsource-variable/inter` 5.3.0 et `…/source-sans-3` 5.3.1, licences dans `apps/desktop/public/licenses/`. Historique : OPEN, non embarquées (repli Segoe UI) jusqu'au 16/10/2026 | — |
| O-02-06 | Versionnement des 4 logos officiels dans `apps/desktop/src/assets/brand/` (droits de redistribution) | OPEN — versionnés localement, non poussés | push |
| O-02-07 | Recherche globale et notifications : contrôles visibles mais désactivés (« Disponible dans une phase suivante ») | PROPOSED | aucune |
| O-02-11 | Navigation du shell par React Router en **routeur mémoire** (client de bureau sans barre d'adresse ; chaque session démarre sur l'Accueil ; aucune route restaurée ; pas de lien profond) | PROPOSED (16/10/2026) ; **ACCEPTED pour le POC desktop** le 16/10/2026 (message GO O-02-01 du propriétaire) | aucune (réversible : `createHashRouter`) |
| O-02-12 | Menu profil sur Radix DropdownMenu : flèches/Entrée/Échap/clic extérieur ; Tab ne quitte plus le menu (comportement Radix ; avant : Tab fermait le menu) ; ouverture à la souris = focus sur le menu, au clavier = sur « Se déconnecter » | PROPOSED (16/10/2026) ; documenté, **non bloquant** pour la phase (message GO O-02-01) | aucune |
| O-02-13 | Bundle JS 516 kB (> seuil indicatif Vite de 500 kB) : accepté pour un client local chargé depuis le disque ; découpage à réévaluer avec les modules métier | PROPOSED (16/10/2026) ; documenté, **non bloquant** pour la phase (message GO O-02-01) | aucune |
| O-02-08 | Libellé « Accueil » (au lieu de « Tableau de bord ») tant qu'aucun tableau de bord réel n'existe ; 5 variantes Lot 1 au backlog | PROPOSED | aucune |

Héritées de 01B, inchangées : O-B04 (rôle unique propriétaire), O-B10 (écoute HTTP 5022), O-B11 (adresse serveur en production), O-B12 ; ADR-004/006/007 non ratifiés.

## Corrections finales avant migration (16/10/2026)

Revue externe du frontend **et** des sources .NET : **architecture d'authentification conforme**, conservée sans refonte (couches Domain/Application/Infrastructure/Api, authentification ASP.NET Core, schéma `auth` et ses quatre tables, sessions révocables à jeton opaque, permission `system.admin` contrôlée par l'API, bootstrap unique sans endpoint public). Corrections appliquées :

1. **`MMV.Maintenance` — cible stricte avant toute écriture de `bootstrap-admin`** (`src/MMV.Maintenance/BootstrapTargetPolicy.cs`) : seule cible admise `mmv_v2_dev` / rôle `mmv_v2_dev` / port 5433 / PostgreSQL 18 / connexion locale (boucle locale TCP). Contrôle de la chaîne **avant connexion**, de l'identité relue **côté serveur** après connexion (`current_database`, `current_user`, `session_user`, `inet_server_port`, `server_version_num`, `inet_server_addr`/`inet_client_addr`), puis **à nouveau juste avant l'écriture**. Tout écart : refus sans modification, **code 5**. `status` reste en lecture seule (session `default_transaction_read_only=on`) et affiche la conformité. Tests : Q-02-28 à Q-02-30.
2. **Frontend — pile prévue réellement intégrée** : Tailwind CSS 4 (Vite), shadcn/ui adaptés aux jetons MMV (aucun thème shadcn par défaut), primitives Radix (Slot, Label, DropdownMenu), React Hook Form + Zod (Login), React Router (Shell), Lucide React ; polices Inter et Source Sans 3 locales. Maquettes, jetons et cotes 260/72/64/32 conservés ; CSS spécifique conservé. TanStack Query et autres bibliothèques non installées (aucun cas d'usage réel). Détail : `IMPLEMENTATION_SPEC.md` § Pile frontend.
3. **Dossier de migration** : empreintes revérifiées, scripts régénérés identiques, manifeste SHA-256 `docs/phase/02/O-02-01_MANIFEST.sha256` (voir O-02-01 ci-dessous).

**État : `PHASE02_STACK_AND_AUTH_READY — OWNER_SQL_GO_REQUIRED`.** Aucune application SQL, aucun bootstrap réel, aucun push, aucune PR, aucun merge, aucun développement Phase 03.

> État remplacé le 16/10/2026 : voir § Finalisation Phase 02.

## O-02-02 — Session : comparaison et variante retenue pour le POC local (PROPOSED, provisoire dev)

| Option | Avantages | Risques / coûts |
|---|---|---|
| **A. Jeton opaque aléatoire (256 bits), session serveur révocable, en mémoire volatile du client** (retenue) | aucun cookie → pas de CSRF ni de cookie tiers (origine Tauri ≠ origine API) ; révocation immédiate (ligne de session) ; seul un **hachage SHA-256** du jeton est stocké en base ; aucune persistance poste ; CORS sans `credentials` | jeton lisible par du JavaScript exécuté dans la WebView (XSS) → atténué par CSP stricte `script-src 'self'` ; reconnexion à chaque lancement ; requêtes protégées = préflight CORS (`Authorization`) |
| B. Cookie `HttpOnly; Secure; SameSite=None` + session serveur + anti-CSRF | jeton inaccessible au JavaScript | cookie **tiers** entre `tauri.localhost`/`localhost:1420` et `localhost:7267` : comportement WebView2 à prouver ; CORS `AllowCredentials` + jeton anti-CSRF ; persistance dans le profil WebView2 |
| C. Jeton de rafraîchissement dans le coffre OS (Credential Manager) | session survivant au redémarrage, secret protégé par l'OS | plugin Tauri/Rust supplémentaire, persistance longue durée, rotation/révocation à concevoir ; hors POC 02 |

Paramètres proposés (dev) : durée absolue **8 h**, inactivité **30 min** (glissement à chaque requête authentifiée), aucun rafraîchissement ; révocation à la déconnexion, à la désactivation du compte (vérifiée à chaque requête) et par expiration. **Ce choix n'est pas une session distribuable approuvée** ; ADR-007 reste `PROPOSED`.

## O-02-01 — Dossier de décision unique : migration d'identité + bootstrap (16/10/2026, NON EXÉCUTÉ)

> Mise à jour datée 16/10/2026 : dossier **exécuté** après le GO du propriétaire — voir § Exécution O-02-01 ci-dessous. Le texte de cette section est conservé tel que présenté.

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

### Mise à jour 16/10/2026 — corrections finales (dossier toujours NON EXÉCUTÉ)

- **Empreintes revérifiées** (Q-02-34) : les trois scripts SQL ont les empreintes ci-dessus, inchangées ; régénérés hors connexion depuis la migration C# (`dotnet ef migrations script`, ×3), ils sont **octet pour octet identiques**. Migration C# et snapshot inchangés depuis `e32aee5`. Inventaire des instructions : aucune opération inattendue (Q-02-35).
- **Étape 4 renforcée** : `bootstrap-admin` refuse désormais (code 5, sans modification) toute cible autre que `mmv_v2_dev` / `mmv_v2_dev` / 5433 / PostgreSQL 18 / boucle locale, avant connexion, après connexion et juste avant l'écriture. Codes de sortie : 0 succès, 1 usage, 2 précondition (dont migration en attente), 3 bootstrap déjà effectué, 4 saisie invalide/annulation, **5 cible non autorisée**. Le second lancement attendu reste refusé **code 3**.
- **Dossier léger** : manifeste `docs/phase/02/O-02-01_MANIFEST.sha256` (21 fichiers : 3 scripts SQL, migration C# + Designer + snapshot, `scripts/Invoke-MmvSqlScript.ps1` `b48bc673…dbba55` inchangé, code final de `MMV.Maintenance`, `BootstrapService`, tests concernés), empreintes du contenu Git (LF, identique à `git archive`) ; vérification : `sha256sum -c` (lignes non commentées) ou `Get-FileHash`. Les fichiers sont livrés dans l'archive `git archive` de la branche.
- La procédure « après GO » ci-dessus est inchangée ; elle n'est **pas** exécutée tant que le propriétaire n'a pas donné le GO O-02-01.

## Exécution O-02-01 (16/10/2026)

**GO :** message du propriétaire « GO O-02-01 ACCORDÉ » (session, non versionné) : application de `docs/phase/02/sql/0002_AuthIdentity.idempotent.sql` (SHA-256 `67C15100…5F5A41` obligatoire) à `mmv_v2_dev` puis `mmv_v2_test` (PostgreSQL 18, 5433, local) par `Invoke-MmvSqlScript.ps1` ; tests post-migration et d'identité sur `mmv_v2_test` exclusivement ; bootstrap réel **par le propriétaire** ; second bootstrap refusé code 3 ; tests natifs avec comptes éphémères de `mmv_v2_test` (compte réel exclu) ; suppression du banc `apps/desktop/.visual/` ; DOCUMENTATION GATE ; commits locaux ; aucun push, PR, merge ni Phase 03. Message suivant du propriétaire : bootstrap réussi (exit 0), finalisation autorisée dans ce périmètre.

| Étape | Résultat | Preuve |
|---|---|---|
| Précontrôle : défaut de `scripts/Invoke-MmvSqlScript.ps1` | le script refusait **aussi la bonne cible**, avant toute connexion : sous Windows PowerShell 5.1, `$b.ConnectionString = …` sur un `DbConnectionStringBuilder` (un `IDictionary`) crée une clé au lieu d'analyser la chaîne. Correction d'une ligne (`$b.set_ConnectionString($raw)`), appliquée avec le GO du propriétaire. Empreinte LF : `b48bc673…dbba55` (version présentée, conservée dans `O-02-01_MANIFEST.sha256`) → **`6b3d3a414b8c373fafd55af10935ef52e6ed7edfd46c013eb056659b6a5fb03d`** (version exécutée et committée) | en-têtes des journaux d'application ; Q-02-45 |
| Application `mmv_v2_dev` (08:54Z) et `mmv_v2_test` (08:55Z) | `PRECHECK OK` (base, rôle, 5433, `server_version_num` 180006), `COMMIT`, exit 0. La requête d'affichage exécutée **après** le `COMMIT` échoue (O-02-15) ; sans effet | `evidence/P02_O-02-01_apply_mmv_v2_dev_20261016T0854Z.log`, `…_apply_mmv_v2_test_20261016T0855Z.log` |
| Inspection en lecture seule | 2 migrations (`InitialBaseline`, `AuthIdentity` / 10.0.4) ; schéma `auth` et ses 4 tables, index et contraintes conformes au dossier ; structures dev et test identiques | mêmes journaux |
| Tests PostgreSQL | `PostMigration` 2/2, `IdentityWrite` 5/5 (ROLLBACK ; comptes HTTP éphémères supprimés), suite 128/128 ; résidus 0 dans les deux bases | `evidence/P02_O-02-01_postgresql_tests_20261016T0855Z.log` |
| Bootstrap réel (propriétaire, son terminal, saisie masquée) | exit 0 **rapporté par le propriétaire** ; corroboré en lecture seule : `Bootstrap : déjà effectué`, 1 compte actif, `system.admin`, `bootstrap_record` → ce compte, 0 session | `evidence/P02_second_bootstrap_refused_20261016T2102Z.log`, `evidence/P02_final_db_state_readonly_20261016T2117Z.log` |
| Second bootstrap | **refusé code 3** avant toute saisie ou écriture ; état inchangé | `evidence/P02_second_bootstrap_refused_20261016T2102Z.log` |

Le compte administrateur réel n'a été utilisé par aucun test (0 session sur `mmv_v2_dev` en fin de phase). Aucune autre base, aucun rôle, aucun certificat modifié.

## Finalisation Phase 02 (16/10/2026)

- Preuve native authentifiée et matrice HTTP sur le processus API réel, contre `mmv_v2_test`, avec deux jeux de 3 comptes éphémères (administrateur, sans droit, désactivé) créés par un outil local hors dépôt et supprimés en fin de preuve (0 résidu) : QA Q-02-52 à Q-02-57.
- Banc temporaire `apps/desktop/.visual/` (non versionné, exclu localement) **supprimé** (47 fichiers) ; `npm run lint` sur la configuration inchangée, sans exclusion : 0 problème (Q-02-58).
- Banc versionné `apps/desktop/e2e/run-native.mjs` : scénarios `user-no-admin`, `login-disabled`, option `sign-in --account` ; attente explicite des boutons avant clic (défaut du banc constaté pendant la preuve, corrigé).
- Tests finaux : .NET 128/128, frontend 81/81, lint, build (Q-02-59).
- Limites et non testé : Q-02-62.

**État : `PHASE02_IMPLEMENTED — READY_FOR_OWNER_REVIEW`, avec action propriétaire ouverte SEC-02-01 (rotation des mots de passe PostgreSQL).** Ni `APPROVED` ni `MERGED`. Aucun push, aucune PR, aucun merge, aucun travail Phase 03.

## Incident SEC-02-01 — exposition des mots de passe PostgreSQL (16/10/2026)

**Faits.** Le 16/10/2026 à 08:50:18Z, pendant la session agent d'application d'O-02-01 (avant la migration), une commande de diagnostic **en lecture seule** destinée à lister les clés des chaînes de connexion **en excluant le mot de passe** a utilisé `$b.ConnectionString = …` sous Windows PowerShell 5.1. Même cause que le défaut du script ci-dessus : l'affectation crée une clé `ConnectionString` contenant la chaîne brute ; le filtre par nom de clé ne s'est pas appliqué. La sortie contenait les **deux chaînes complètes, mots de passe compris**, des rôles `mmv_v2_dev` et `mmv_v2_test`.

**Portée.** Sortie affichée dans la session, donc transmise au fournisseur du modèle d'IA avec le contexte de la conversation, et enregistrée dans le transcript local de la session (profil utilisateur, hors dépôt). Vérifié le 16/10/2026 par recherche des valeurs exactes, sans affichage (compteurs seuls) : **0 occurrence** dans les fichiers suivis, dans l'historique Git (toutes branches, `git log -S`), dans les fichiers non suivis du dépôt (sources, docs, scripts), dans les 4 archives ZIP locales du dépôt, dans les preuves `evidence/` et les journaux du banc (Q-02-56, Q-02-60). Rien n'a été poussé. Facteur aggravant : PostgreSQL 18 écoute sur `0.0.0.0:5433` / `[::]:5433` (Q-02-04) ; selon `pg_hba.conf` et le pare-feu, ces rôles, propriétaires de leur base, peuvent être joignables depuis le réseau local.

**Non concernés.** Mot de passe du compte administrateur réel (saisi masqué par le propriétaire, jamais affiché) ; mots de passe des comptes éphémères (aléatoires, comptes supprimés) ; aucun jeton de session réel.

**Actions requises (propriétaire ; l'agent ne modifie pas les rôles PostgreSQL) :**
1. Changer les deux mots de passe dans `psql` en superutilisateur, par saisie masquée : `\password mmv_v2_dev` puis `\password mmv_v2_test` (pas d'`ALTER ROLE … PASSWORD '…'` en clair, qui resterait dans l'historique). Valeurs aléatoires longues (≥ 24 caractères), distinctes.
2. Mettre à jour les user-secrets par saisie masquée : `. .\scripts\Set-MmvSecret.ps1` puis `Set-MmvSecret -Id mmv-v2-api -Key ConnectionStrings:Mmv -Name mmv_v2_dev` et `Set-MmvSecret -Id mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Name mmv_v2_test` (procédure `docs/phase/01b/IMPLEMENTATION_SPEC.md` § Secrets).
3. Vérifier : `dotnet run --project src/MMV.Maintenance -- status` puis `dotnet test MMV.slnx`.
4. Facultatif : purger le transcript local de la session concernée ; revoir `listen_addresses` / `pg_hba.conf` (relève d'O-B12 / réseau).

**Mesures déjà prises.** Accesseur explicite `set_ConnectionString` dans le script ; diagnostics ultérieurs limités aux noms de clés après analyse effective ; contrôle des journaux par recherche des valeurs exactes (Q-02-56). **État : OPEN** jusqu'à confirmation de la rotation par le propriétaire.
