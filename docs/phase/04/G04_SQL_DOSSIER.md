# 04-B — Dossier G04-SQL : migration `0006_OpticalModule`

**Statut (27/10/2026) : `PHASE04_B_READY — OWNER_SQL_REVIEW_REQUIRED`.** SQL **préparé, NON APPLIQUÉ** : aucune connexion d'écriture à `mmv_v2_dev` ni à `mmv_v2_test`, aucune simulation dans une transaction annulée de ces bases. Mandat : [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md) § 5, étape A. Seul le **script exact** ci-dessous, à l'empreinte indiquée, pourra être appliqué après GO G04-SQL explicite (étape B).

## 1. Script à approuver

| Fichier | Rôle | SHA-256 (LF, sans BOM) | Taille |
|---|---|---|---|
| [`sql/0006_OpticalModule.idempotent.sql`](sql/0006_OpticalModule.idempotent.sql) | **script à appliquer** (gardé par `__EFMigrationsHistory`, ré-exécution sans effet) | `927ed7ccb975b15eed18f6b2bcee41faf08ecb23c1c4f9f1f80a2efa1706c013` | 31 183 octets |
| [`sql/0006_OpticalModule.sql`](sql/0006_OpticalModule.sql) | variante directe, pour lecture | `7c5aba78035c39bf834311cc089c4eabc71e1f69363b685a41c3c9fb78aa221b` | 23 090 octets |
| [`sql/0006_OpticalModule.verify.sql`](sql/0006_OpticalModule.verify.sql) | contrôle de schéma **en lecture seule** après application | voir [`sql/0006_MANIFEST.sha256`](sql/0006_MANIFEST.sha256) | — |
| [`sql/0006_OpticalModule.schema-diff.txt`](sql/0006_OpticalModule.schema-diff.txt) | diff `pg_dump --schema-only` 0005 → 0006 (cluster jetable) | idem | — |

Manifeste complet (scripts, diff, migration EF Core, modèle, instantané) : [`sql/0006_MANIFEST.sha256`](sql/0006_MANIFEST.sha256) — vérification `sha256sum -c docs/phase/04/sql/0006_MANIFEST.sha256` depuis la racine (Git Bash).

**Aucun script de retour arrière** (mandat § 2 : « ne pas écrire de migration de rollback destructif à exécuter automatiquement ») : `Down()` de la migration lève `NotSupportedException` ; le retour arrière est la **restauration de la sauvegarde** prise juste avant l'application (§ 6), décidée par le propriétaire.

## 2. Contenu exact (une seule transaction `START TRANSACTION … COMMIT`)

| Ordre | Instruction | Effet |
|---|---|---|
| 1 | `ALTER TABLE auth.user_permissions DROP CONSTRAINT ck_user_permissions_permission` | **seule instruction `DROP`** du script ; contrainte recréée en 5 dans la même transaction (procédé O-03-24 accepté pour 0004 / 0005) |
| 2 | `CREATE SCHEMA optical` (si absent) | nouveau schéma |
| 3 | 2 séquences `optical.prescription_reference_seq`, `optical.measurement_reference_seq` | références `RX-000001`, `MES-000001` (6 chiffres au moins, jamais tronquées, jamais réutilisées) |
| 4 | 4 tables : `optical.prescriptions`, `optical.prescription_revisions`, `optical.measurements`, `optical.measurement_revisions` | **deux familles distinctes** (D-04-01) ; en-tête + révisions ; aucune table commune, aucun discriminant, aucune table de pièces (D-04-16) |
| 5 | `ADD CONSTRAINT ck_user_permissions_permission` | 8 droits existants **inchangés** + `optical.read`, `optical.prescriptions.write`, `optical.measurements.write`, `optical.invalidate` ; aucun droit documentaire |
| 6 | 31 index (dont 4 uniques partiels : un seul brouillon ouvert par enregistrement, numéros de révision uniques ; 2 uniques sur `creation_request_id`) | intégrité et requêtes du dossier client |
| 7 | 2 fonctions + 8 déclencheurs `optical.*` | révision confirmée ou abandonnée **immuable** ; enregistrement invalidé figé ; **aucune suppression ni troncature** (SQLSTATE 42501) |
| 8 | `INSERT INTO "__EFMigrationsHistory"` `20261027134615_OpticalModule` / `10.0.4` | historique EF Core |

Contraintes (46 `CHECK`, 19 clés étrangères **toutes `RESTRICT`**, aucune cascade) :

- **Valeurs d'œil** (14 colonnes `varchar(16)` nullables une à une par révision) : texte EXACT tel que saisi (signe, virgule ou point, zéros de fin, signe moins typographique) — `NULL` = non renseigné, jamais `0`. Pour Sphère, Cylindre, Axe, Addition, Prisme : contrainte de **forme** décimale `^[-+−]? ?[0-9]+([.,][0-9]+)?$` et **colonne générée** `…_value numeric` (sans précision ni échelle) = valeur décimale exacte. Justification : un `numeric(p, s)` arrondirait **en silence** une saisie libre à l'échelle `s` ; un format clinique supposé deviendrait une contrainte cachée. Aucune borne clinique, aucun pas en base.
- **Cycle** : statut `draft` / `confirmed` / `discarded` ; numéro attribué à la confirmation seulement (`revision_no` ≥ 1, > révision de base) ; seule une rectification (`base_revision_no` renseigné, motif obligatoire) peut être abandonnée ; dates de confirmation / abandon et auteurs renseignés ensemble.
- **Provenance exigée pour CONFIRMER** (garde de base, en plus du service) : ordonnance `prescribed_on` + `prescriber_name` ; mesure `measured_on` + `measured_by_user_id`. Facultative au brouillon.
- **Invalidation** : date, auteur, motif renseignés ensemble ; commentaire seulement si invalidé ; codes de motifs au format contrôlé (la liste vit dans le code).
- Dates métier `date` (sans fuseau, ≥ 1900-01-01) ; instants `timestamp with time zone` horodatés par le serveur.

**Ce que le script ne fait pas** : il ne lit, ne modifie ni ne supprime **aucune donnée** existante ; aucune table, colonne, index ou contrainte existants n'est modifié hormis la contrainte des permissions recréée ; aucune extension ; aucun rôle ni droit SQL ; aucun droit `optical.*` attribué à un compte ; aucun objet hors `optical` et `auth.user_permissions`. Le diff de schéma (§ 1) ne compte qu'**une** ligne retirée (l'ancienne contrainte des permissions) pour 689 lignes ajoutées.

## 3. Correspondance avec le code

- Migration EF Core [`src/MMV.Infrastructure/Persistence/Migrations/20261027134615_OpticalModule.cs`](../../../src/MMV.Infrastructure/Persistence/Migrations/20261027134615_OpticalModule.cs) (déclencheurs ajoutés à la main dans `Up`, `Down` refusé) ; modèle [`src/MMV.Infrastructure/Optical/OpticalModel.cs`](../../../src/MMV.Infrastructure/Optical/OpticalModel.cs) ; 0001 à 0005 **inchangées** (aucun diff Git).
- Régénération hors `src/` (27/10/2026) : `dotnet tool run dotnet-ef migrations script 20261018163108_AuditJournal 20261027134615_OpticalModule [--idempotent] --no-build --project src/MMV.Infrastructure --startup-project src/MMV.Infrastructure -o <hors dépôt>` avec `UseArtifactsOutput=true` et `ArtifactsPath=<dépôt>/.agent-artifacts-ef`, puis BOM et CR retirés : **octet pour octet identique** aux deux fichiers du dépôt. `migrations has-pending-model-changes` : « No changes have been made to the model since the last migration ». `migrations list --no-connect` : 6 migrations.
- Inventaire exact attendu après 0006 : `SchemaState.OpticalModuleDifferences` ([`tests/MMV.PostgreSql.Tests/SchemaInspection.cs`](../../../tests/MMV.PostgreSql.Tests/SchemaInspection.cs)).

## 4. Pré-essais hors bases du propriétaire (PAS une preuve d'application)

Cluster PostgreSQL **18.6 jetable** créé par l'agent (`initdb` dans son répertoire temporaire, `localhost:55433`, authentification locale `trust`, données fictives, arrêté et supprimé en fin de session) — **jamais** le serveur du propriétaire (5433), jamais PostgreSQL 17 (5432) :

| Essai | Résultat |
|---|---|
| 0001 → 0005 (scripts idempotents approuvés) puis `0006_OpticalModule.idempotent.sql` | COMMIT ; 47 blocs gardés |
| ré-exécution du même script idempotent | aucun changement (tous les blocs gardés) |
| `0006_OpticalModule.sql` (direct) sur une seconde base 0001 → 0005 | COMMIT ; 2 `ALTER TABLE`, 2 fonctions, 27 index (+ 4 clés primaires), 2 séquences, 4 tables, 8 déclencheurs |
| inventaire `SchemaState.OpticalModuleDifferences` sur les deux bases | **0 différence** (direct ≡ idempotent ≡ attendu) |
| `0006_OpticalModule.verify.sql` | 17 / 18 OK ; seul écart attendu : port 55433 au lieu de 5433 |
| API réelle (`MMV.Api`, HTTPS `localhost:7567`, environnement `Scratch`) + harnais HTTP | **27 / 27** contrôles (création + confirmation atomiques, réémission, double requête simultanée → un seul enregistrement, 422 sphère seule à zéro / cylindre sans axe / acuité seule d'ordonnance, rectification, deux postes → un 200 + un 409, historique selon `audit.read`, droits, client archivé, invalidation) ; journaux de l'API : **0** ligne `fail`, **0** valeur optique, nom ou note — [`evidence/P04B_PRE_scratch_http_20261027.log`](evidence/P04B_PRE_scratch_http_20261027.log) |

## 5. Politique de mise à niveau et compatibilité

- **Ordre** : 0006 sur `mmv_v2_dev` puis `mmv_v2_test` (comme 0002 à 0005), API arrêtée ou inactive pendant l'application (verrou `ACCESS EXCLUSIVE` bref sur `auth.user_permissions` ; `lock_timeout` 5 s transmis par [`scripts/Invoke-MmvSqlScript.ps1`](../../../scripts/Invoke-MmvSqlScript.ps1) `-LockTimeout`, défaut 5s : un verrou non obtenu fait échouer le script, transaction annulée).
- **Code de cette branche sur une base 0005** (avant G04-SQL) : routes existantes inchangées (Clients, Rendez-vous, Historique — les événements `optical` en sont exclus sans `optical.read`) ; routes `/api/optical/*` en erreur 500 si appelées (tables absentes), **jamais appelées** par l'interface tant que l'interrupteur `VITE_MMV_OPTICAL_MODULE` est absent ; attribution d'un droit `optical.*` refusée par la contrainte actuelle (attendu).
- **Code antérieur sur une base 0006** : inchangé (tables et droits nouveaux ignorés).
- **Compatibilité de l'API** : routes nouvelles seulement ; `GET /api/audit/clients/{id}` accepte en plus `module=optical` (403 sans `optical.read`) ; aucun champ existant modifié. Extrait OpenAPI : [`apps/desktop/src/api/contract/optical.openapi.json`](../../../apps/desktop/src/api/contract/optical.openapi.json), [`audit.openapi.json`](../../../apps/desktop/src/api/contract/audit.openapi.json) (enum `module` + `optical`).

## 6. Commandes de l'étape B (après GO G04-SQL explicite seulement)

1. **Contrôles préalables (lecture seule)** : PostgreSQL 18.6 port 5433, bases `mmv_v2_dev` / `mmv_v2_test` et rôles homonymes, migrations 0001–0005 exactement, dérive nulle (`dotnet test tests/MMV.PostgreSql.Tests --filter "FullyQualifiedName~SchemaStateTests|FullyQualifiedName~TargetIdentityTests"` : à ce stade `…AllMigrationsApplied` échoue **par construction** — 6 migrations attendues, 5 appliquées — et `KnownStateDifferences` doit être vide). Jamais le port 5432.
2. **Sauvegarde de `mmv_v2_dev`** hors dépôt (`C:\Users\Ali Zekhnini\Documents\MMV-V2-sauvegardes\`, comme avant 0005) : `pg_dump -Fc -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -f mmv_v2_dev_avant_0006_<horodatage>.dump` (mot de passe par `PGPASSWORD` éphémère lu dans les user-secrets, jamais affiché) ; lisibilité `pg_restore --list` (objets attendus) ; empreinte SHA-256 consignée. **Restauration complète non éprouvée** si aucune base indépendante n'est autorisée : constat honnête, prérequis avant pilote (O-04-09) ; aucune base créée ni droit élargi sans accord.
3. **Empreinte** : `Get-FileHash -Algorithm SHA256 docs/phase/04/sql/0006_OpticalModule.idempotent.sql` = `927ED7CC…706C013`.
4. **Application** (une base à la fois) :
   `./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev -Script docs/phase/04/sql/0006_OpticalModule.idempotent.sql -Sha256 927ed7ccb975b15eed18f6b2bcee41faf08ecb23c1c4f9f1f80a2efa1706c013`
   puis la même commande avec `-SecretsId mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Database mmv_v2_test` (clés de secrets à reconfirmer au préflight). Attendu : `PRECHECK OK`, `LOCK_TIMEOUT : 5s`, `APPLIQUE : exit 0`, `MIGRATIONS : … 20261027134615_OpticalModule`. **Écart ou erreur : STOP**, aucun retour arrière destructif, rapport au propriétaire.
5. **Contrôle de schéma** : `psql … -f docs/phase/04/sql/0006_OpticalModule.verify.sql` sur chaque base (18 lignes `OK`), puis `dotnet test tests/MMV.PostgreSql.Tests` (inventaire exact, tests `Stage=OpticalWrite` — transaction annulée et concurrence réelle à deux connexions avec nettoyage démontré, résidu 0).
6. **Matrices réelles** : HTTP/HTTPS (API de test sur `mmv_v2_test`, comptes fictifs dotés des droits optiques **sur `mmv_v2_test` seulement**), Tauri natif (parcours du mandat § 4). Aucun droit attribué aux comptes du propriétaire sans GO spécifique.

## 7. Points à examiner par l'auditeur

1. Représentation des valeurs : texte exact + `numeric` généré sans échelle (§ 2) plutôt que `numeric(p, s)`.
2. Codes de motifs (rectification : `transcription_error`, `additional_information`, `other` ; invalidation : `entry_error`, `duplicate`, `unacceptable_document`, `other`) contrôlés par le code, format seul en base (évolution sans migration).
3. Déclencheurs : protection contre une erreur **applicative**, pas contre le rôle propriétaire (même limite que 0005, O-B04 : rôle SQL applicatif distinct = prérequis pilote).
4. Index uniques partiels comme garantie « un seul brouillon ouvert » et « numéros sans doublon » sous concurrence (en plus du jeton `xmin` de l'en-tête et du verrou `FOR UPDATE` pris par le service avant écriture).
5. Absence de `Down` exécutable : retour arrière = restauration de sauvegarde.
