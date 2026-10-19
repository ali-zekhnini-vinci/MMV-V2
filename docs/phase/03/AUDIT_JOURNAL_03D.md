# 03-D2 — Journal d'audit métier (historique des actions sur le dossier client)

**Statut (19/10/2026, 03-D3) : `APPLIED — OWNER_FINAL_REVIEW_REQUIRED`.** Migration `0005_AuditJournal` **appliquée** à `mmv_v2_dev` puis `mmv_v2_test` (GO [`prompts/PHASE03_D3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_D3_GO_CLAUDE_CODE.md), § 9) ; D-03-19, D-03-20, D-03-21 retenues, D-03-22 **ouverte**, D-03-23 exécutée (§ 6) ; preuves réelles et parcours natif de l'onglet « Historique » PASS (§ 7).

*Historique — statut 18/10/2026 :* **Statut (18/10/2026) : `IMPLEMENTED — OWNER_SQL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE03_D_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_D_GO_CLAUDE_CODE.md) § LOT 03-D2. Code, contrat, interface, tests et migration `0005_AuditJournal` **préparés** ; migration **NON appliquée** (ni `mmv_v2_dev` ni `mmv_v2_test`) : revue du propriétaire requise avant toute application. Décisions proposées D-03-19 à D-03-23 (§ 6), soumises au propriétaire.

> *Historique 18/10/2026 — résolu le 19/10/2026 (0005 appliquée aux deux bases, O-03-40) :* **Conséquence immédiate (à lire avant de relancer MMV.Api sur ce commit) :** toute écriture Clients ou Rendez-vous insère désormais son événement d'audit dans la même transaction. Tant que 0005 n'est pas appliquée à la base visée, ces écritures **échouent** (500, relation `audit.events` absente) et **rien n'est écrit** (atomicité). Les lectures restent possibles. Pour continuer à utiliser l'application sur `mmv_v2_dev` avant la revue, rester sur le commit 03-D1 (§ 8).

## 1. Architecture

| Couche | Élément | Rôle |
|---|---|---|
| PostgreSQL | schéma `audit`, table `audit.events` | journal unique pour tous les modules ; ajout seul (déclencheurs `trg_events_append_only` — `UPDATE`/`DELETE` — et `trg_events_no_truncate` refusés, SQLSTATE 42501) ; clés étrangères `RESTRICT` vers `auth.users` (auteur) et `clients.clients` (dossier) ; `changes` en `jsonb` (tableau) |
| MMV.Domain | [`Audit/AuditEvent.cs`](../../../src/MMV.Domain/Audit/AuditEvent.cs) | entité, catalogue `AuditActions` (10 actions), politique `AuditFieldPolicy` (liste blanche des valeurs conservées) |
| MMV.Application | [`Audit/AuditJournal.cs`](../../../src/MMV.Application/Audit/AuditJournal.cs) | `IAuditRecorder` (enregistrement dans l'unité de travail en cours), `IAuditContext` (session, corrélation), calcul des champs modifiés (`ClientAuditChanges`, `AppointmentAuditChanges`), lecture `AuditService` |
| MMV.Infrastructure | [`Audit/AuditModel.cs`](../../../src/MMV.Infrastructure/Audit/AuditModel.cs), [`Audit/AuditStore.cs`](../../../src/MMV.Infrastructure/Audit/AuditStore.cs) | modèle EF ; `AuditRecorder` ajoute l'événement au **même** `MmvDbContext` (portée de la requête) ; identifiants UUIDv7 strictement croissants dans l'unité de travail (ordre des événements d'une même opération) ; `AuditStore` (lecture triée, nom affiché de l'auteur) |
| MMV.Api | [`Audit/AuditEndpoints.cs`](../../../src/MMV.Api/Audit/AuditEndpoints.cs) | `GET /api/audit/clients/{clientId}` (policy `AuditRead`), `HttpAuditContext` (revendication `mmv:sid`, `TraceIdentifier`) |
| Interface | [`audit/ClientHistoryTab.tsx`](../../../apps/desktop/src/audit/ClientHistoryTab.tsx), [`api/audit.ts`](../../../apps/desktop/src/api/audit.ts) | onglet « Historique » de la fiche (présent avec `audit.read` seulement) |

Pas de microservice, pas de file de messages : un seul processus MMV.Api, une seule base. **Fiabilité** : les services métier (`ClientService`, `AppointmentService`) appellent `IAuditRecorder.Record` **après** tous les contrôles et **juste avant** l'enregistrement ; l'événement est écrit par le même `SaveChanges` (Clients) ou dans la même transaction `READ COMMITTED` sous verrous (Rendez-vous). Refus, conflit de version (`xmin`, `ChangeTracker.Clear()`), contrainte ou panne : ni la modification ni l'événement ne sont écrits. Aucun historique n'est produit par React.

**Extensibilité** : `module`, `action` (`entité.verbe`) et `entity_type` sont des codes ouverts au format contrôlé par la base ; un module futur (Ordonnances, Ventes, Commandes, Stock) ajoute ses codes au catalogue `AuditActions` et sa politique de champs, **sans migration** ; `client_id` est facultatif pour les futurs événements sans dossier client. Aucun modèle métier futur n'est présumé.

**Limite des déclencheurs (19/10/2026, GO 03-D3).** Les déclencheurs d'ajout seul protègent contre une erreur **applicative** (aucune route, aucun service ne modifie ni ne supprime un événement) ; ils ne protègent **pas** contre un superutilisateur PostgreSQL, ni contre le rôle **propriétaire** de la table, qui peut les désactiver (`ALTER TABLE … DISABLE TRIGGER`) — ce que fait, sur `mmv_v2_test` seulement, la purge des données fictives des tests (O-03-42). Aujourd'hui MMV.Api se connecte avec le rôle de migration, propriétaire des schémas. À prévoir : un rôle SQL **applicatif** distinct (droits `INSERT` / `SELECT` sur `audit.events`, ni propriété, ni `UPDATE`, ni `DELETE`, ni `TRUNCATE`), le rôle de migration restant réservé aux migrations (O-B04), et une politique de rétention contrôlée (D-03-22).

## 2. Événements initiaux (10)

| Action | Quand | Champs (`changes`) |
|---|---|---|
| `client.created` | création réussie | noms des champs renseignés (masqués) |
| `client.updated` | modification réussie **avec** au moins un changement | noms des champs modifiés (masqués) |
| `client.archived` / `client.reactivated` | archivage / réactivation réussis | — |
| `appointment.created` | création réussie | `startsAtUtc`, `durationMinutes`, `reasonCode`, `responsibleUserId` (valeurs) ; `notes` (masqué, si renseigné) |
| `appointment.moved` | début ou durée modifiés | `startsAtUtc`, `durationMinutes` (avant → après) |
| `appointment.responsible_changed` | responsable modifié | `responsibleUserId` (avant → après) |
| `appointment.updated` | motif, notes ou client modifiés | `reasonCode` (valeurs) ; `notes`, `clientId` (masqués) ; changement de client : événement porté aux **deux** dossiers |
| `appointment.cancelled` | annulation réussie | `cancellationReason` (masqué, si renseigné) |
| `appointment.restored` | rétablissement réussi | — |

Une même saisie qui déplace, change le responsable et le motif produit trois événements, dans cet ordre. Une modification sans changement réel n'en produit aucun.

**Auteur ≠ responsable** : `actor_user_id` est toujours le compte connecté qui a exécuté l'action (identifiant transmis par MMV.Api) ; le collaborateur responsable d'un rendez-vous n'apparaît que comme **valeur** du champ `responsibleUserId`.

Chaque événement porte : identifiant unique (`id`, UUIDv7), dossier client (`client_id`), type d'action (`module`, `action`), date UTC (`occurred_at_utc`), auteur (`actor_user_id`), entité (`entity_type`, `entity_id`), champs modifiés (`changes`), identifiants techniques (`session_id` — identifiant de session, jamais le jeton —, `trace_id` — corrélation avec les journaux techniques).

## 3. Politique de conservation des valeurs (minimisation) — proposition D-03-20

| Champ | Valeurs | Raison |
|---|---|---|
| `startsAtUtc`, `durationMinutes`, `reasonCode`, `responsibleUserId` | **conservées** (avant / après) | données opérationnelles de l'agenda, non personnelles du client ; nécessaires pour comprendre un déplacement ou un changement de responsable |
| `firstName`, `lastName`, `birthDate`, `phone`, `email`, `addressLine`, `city`, `postalCode` | **masquées** (nom du champ seulement) | données personnelles d'identification et de contact ; la valeur courante reste sur la fiche, selon les droits Clients |
| `notes` (client, rendez-vous), `cancellationReason`, `clientId` (changement de client) | **masquées** | texte libre susceptible de contenir des informations sensibles ; identité d'un autre client |
| tout autre champ | **masqué par défaut** | liste blanche : un champ futur n'est jamais conservé sans décision |

Jamais dans le journal : mot de passe, empreinte de mot de passe, jeton de session, secret, chaîne de connexion, donnée médicale ou optique (Phase 04 hors périmètre). Vérifié par les tests (§ 7).

## 4. Accès — proposition D-03-21

- Permission **dédiée** `audit.read`, ajoutée à la contrainte `ck_user_permissions_permission` par 0005, attribuable par `MMV.Maintenance permissions grant <identifiant> audit.read` (confirmation interactive) et révocable par `revoke`. Jamais impliquée par `system.admin`, ni par `clients.*` ou `appointments.*` : lire un dossier ou l'agenda ne donne pas son historique.
- Séparation explicite : **rôle visuel** (libellé du menu profil, sans effet), **permissions effectives** (revendications `mmv:perm` de la session, contrôlées par les policies serveur), **droit d'audit** (`audit.read`, policy `AuditRead`). L'interface masque l'onglet sans le droit ; le serveur répond 403 de toute façon.
- Lecture seule : aucune route d'écriture, de modification ou de suppression ; le journal n'est alimenté que par les services métier.
- Contenu servi : valeurs conservées et leur libellé calculé par le serveur (heure murale du magasin si l'agenda est configuré, nom affiché **actuel** du collaborateur, libellé du motif) ; `session_id` et `trace_id` **non servis** (enquêtes techniques en base seulement).

## 5. Conservation et confidentialité — marchés marocain et belge — proposition D-03-22 (À VALIDER, avec un conseil juridique)

| Sujet | Proposition | Statut |
|---|---|---|
| Base légale / finalité | traçabilité des actions sur les dossiers clients (sécurité, responsabilité, preuve en cas de litige) ; journal distinct des données métier | à valider |
| Minimisation | § 3 : aucune valeur personnelle client, aucune donnée de santé | implémenté |
| Durée de conservation | **aucune purge automatique implémentée** ; durée à fixer par le propriétaire avec son conseil (Belgique : RGPD et Autorité de protection des données ; Maroc : loi 09-08 et CNDP). La suppression éventuelle exigera une procédure dédiée, revue, exécutée par le propriétaire (les déclencheurs d'ajout seul interdisent toute suppression applicative) | **OUVERT** ; *19/10/2026 (03-D3) : **toujours OUVERT** (GO 03-D3) ; politique de rétention contrôlée et procédure de purge à concevoir, exécutées par un rôle distinct du rôle applicatif* |
| Droits des personnes | le journal ne contient que des identifiants techniques et des noms de champs ; l'accès d'une personne à ses données passe par la fiche ; à préciser avec le conseil | à valider |
| Accès | § 4 : `audit.read` sur décision explicite, attribution tracée par la commande de maintenance | proposé |
| Sous-traitance / hébergement | installation locale au magasin (PostgreSQL 18 local) ; aucun envoi externe | constat |
| Antériorité | aucun historique n'est reconstitué pour les opérations antérieures à l'application de 0005 ; l'interface l'indique | implémenté |

## 6. Décisions soumises au propriétaire

| ID | Objet | Proposition | État |
|---|---|---|---|
| D-03-19 | Architecture : schéma `audit` unique, événements écrits par le backend dans la transaction métier, ajout seul par déclencheurs | § 1 | **RETENUE** (GO 03-D3, 19/10/2026 : schéma centralisé, écritures atomiques) ; déclencheurs = protection contre l'erreur applicative seulement (§ 1, limite) ; *historique : PROPOSED* |
| D-03-20 | Politique de conservation des valeurs (liste blanche) | § 3 | **RETENUE** (GO 03-D3 : minimisation) ; *historique : PROPOSED* |
| D-03-21 | Permission dédiée `audit.read`, non impliquée par d'autres droits | § 4 | **RETENUE** (GO 03-D3 : permission séparée) ; attribuée à **aucun** compte réel (confirmation du propriétaire requise) ; *historique : PROPOSED* |
| D-03-22 | Conservation et confidentialité (durée **à fixer**) | § 5 | **OUVERTE** (GO 03-D3) : durées de conservation, obligations RGPD / APD (Belgique), loi 09-08 / CNDP (Maroc) ; rétention contrôlée à prévoir ; *historique : PROPOSED — durée OUVERTE* |
| D-03-23 | Migration `0005_AuditJournal` : revue du SQL exact puis GO d'application (§ 8) | § 8 | **EXÉCUTÉE** — GO 03-D3, appliquée le 19/10/2026 (§ 9) ; *historique : `OWNER_SQL_REVIEW_REQUIRED`* |

## 7. Preuves (18/10/2026)

- Unitaires et HTTP (`tests/MMV.Api.Tests/Audit/`) : un événement par action réussie, auteur = compte connecté (responsable distinct), aucun événement après refus, conflit de version ou échec d'enregistrement, valeurs personnelles absentes du JSON, codes invalides refusés, identifiants croissants dans l'ordre des uuid PostgreSQL ; `/api/audit` : 401 anonyme, 403 pour `system.admin`, `clients.*`, `appointments.*`, 403 en HTTP, 200 avec `audit.read` seul, ordre, libellés serveur, aucune valeur masquée ni identifiant technique servis, filtres, pagination, 400 hors liste blanche ; contrat OpenAPI (`audit.openapi.json`).
- PostgreSQL 18 réel (`tests/MMV.PostgreSql.Tests/AuditPostgreSqlTests.cs`, `mmv_v2_test`, transaction annulée ; 0005 **simulée dans la transaction**, jamais conservée) : écritures Clients et Rendez-vous auditées dans le même enregistrement ; conflit `xmin` sans événement ; échec métier (clé dupliquée `23505`) ⇒ aucun événement, échec d'audit (auteur inexistant `23503`) ⇒ aucune modification métier ; `UPDATE` / `DELETE` / `TRUNCATE` refusés (`42501`) ; formats contrôlés (`23514`) ; `audit.read` admis, `audit.write` refusé ; le script idempotent produit **exactement** l'état attendu par l'inventaire `SchemaState.AuditJournalDifferences` et sa ré-exécution ne change rien.
- Non testé tant que 0005 n'est pas appliquée : tests à données validées ou à deux connexions (5, `NOT_TESTED`), `PostMigration` (2, échec attendu), parcours natif de l'onglet Historique.
- *19/10/2026 (03-D3) :* 0005 appliquée ; les 5 tests `NOT_TESTED` et les 2 `PostMigration` exécutés : **PASS** (PostgreSQL réel 47/47) ; parcours natif de l'onglet « Historique » **PASS** (3 × 81 contrôles) ; modifications sans changement : aucun événement ni écriture (O-03-44). Détail : [`QA_MATRIX.md`](QA_MATRIX.md) § 03-D3.

Détail : [`QA_MATRIX.md`](QA_MATRIX.md) § 03-D.

## 8. Dossier de migration 0005 — revue du propriétaire

| Fichier | SHA-256 | Usage |
|---|---|---|
| [`sql/0005_AuditJournal.sql`](sql/0005_AuditJournal.sql) | `12f98d1fbae28d66d3dd584127fcd8c16094203ebe979a73f1cc66a102baed92` | lecture (script direct) |
| [`sql/0005_AuditJournal.idempotent.sql`](sql/0005_AuditJournal.idempotent.sql) | `1b42d82c85d21d18b58f465f681258641dbd2116d073ae58bbdf69cc8d41daee` | **à appliquer** après GO ; *appliqué le 19/10/2026 (§ 9)* |
| [`sql/0005_AuditJournal.rollback.sql`](sql/0005_AuditJournal.rollback.sql) | `19f3f10bcf125bc679f5745ff9e66a0e9ab76794f1f876bab6ee64d9d3d080a2` | **non exécuté** — détruirait le journal (`DROP TABLE`) ; échoue tant qu'un compte détient `audit.read` (révoquer d'abord) |

Migration EF : [`20261018163108_AuditJournal.cs`](../../../src/MMV.Infrastructure/Persistence/Migrations/20261018163108_AuditJournal.cs) (générée par `dotnet ef`, déclencheurs ajoutés à la main), instantané du modèle mis à jour ; 0001 à 0004 **inchangées**. Aucune migration au démarrage.

**Points de revue.** (1) `ALTER TABLE auth.user_permissions DROP CONSTRAINT` puis `ADD CONSTRAINT` (même principe qu'O-03-24, accepté pour 0004), dans la même transaction ; la nouvelle liste ajoute `audit.read`. (2) `CREATE SCHEMA audit`, `CREATE TABLE audit.events` (4 contraintes `CHECK`, 2 clés étrangères `RESTRICT`), 3 index dont `ix_events_client_occurred (client_id, occurred_at_utc DESC, id DESC)`. (3) Fonction PL/pgSQL `audit.reject_event_mutation()` et deux déclencheurs d'ajout seul. (4) Aucune donnée lue, copiée ni reprise ; aucune extension.

**Procédure proposée (après GO, par le propriétaire ou sur son instruction), MMV.Api arrêté :**

```powershell
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev -Script docs/phase/03/sql/0005_AuditJournal.idempotent.sql -Sha256 1b42d82c85d21d18b58f465f681258641dbd2116d073ae58bbdf69cc8d41daee
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Database mmv_v2_test -Script docs/phase/03/sql/0005_AuditJournal.idempotent.sql -Sha256 1b42d82c85d21d18b58f465f681258641dbd2116d073ae58bbdf69cc8d41daee
dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=PostMigration|Stage=AuditWrite"   # puis la suite complète (les 5 NOT_TESTED deviennent exécutables)
dotnet run --project src/MMV.Maintenance -- permissions grant <identifiant> audit.read   # comptes habilités seulement
```

**Avant la revue** : pour utiliser l'application sur `mmv_v2_dev`, rester sur le commit 03-D1 (sélecteurs, onglet Rendez-vous, corrections O-03-33 à O-03-36 ; aucune migration requise) — voir [`../03.md`](../03.md).

## 9. Exécution 03-D3 (19/10/2026)

- **Précontrôles** (tous conformes) : empreinte imposée ; régénération EF identique ; PostgreSQL 18.6 / 5433, bases et rôles exacts ; 0001–0004 seules, 0005 absente ; aucune dérive (empreintes = référence 03-B3) ; sauvegarde vérifiée de `mmv_v2_dev` hors dépôt ; processus du propriétaire respectés.
- **Application** : `Invoke-MmvSqlScript.ps1` sur `mmv_v2_dev` puis `mmv_v2_test` (`lock_timeout` 10 s, API du propriétaire en marche) : `COMMIT`, 5 migrations ; contraintes, index, fonction et déclencheurs conformes ; aucun `audit.read` attribué. Preuves : `evidence/P03D3_0005_*.log` ; registre : [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Exécution 03-D3.
- **Données de test validées** : la purge des événements fictifs exige la désactivation transactionnelle du déclencheur par le rôle propriétaire, sur `mmv_v2_test` seulement (O-03-42) — illustration de la limite du § 1.
- **Affichage** : une création ne sert plus de libellé « avant » (O-03-45).
- **Restes** : D-03-22 (durées), rôle SQL applicatif distinct, rétention contrôlée ; `audit.read` à attribuer par le propriétaire s'il le souhaite (`MMV.Maintenance permissions grant <identifiant> audit.read`). Rapport : [`RAPPORT_CLOTURE_03D3.md`](RAPPORT_CLOTURE_03D3.md).
