# MMV V2 — GO PHASE 03-B3 — MIGRATION ET VALIDATION RÉELLE

La Phase 03-B2 a fait l'objet d'un audit indépendant de l'archive complète f1af9ad.

**VERDICT : GO CONDITIONNEL — MIGRATION 0004 ET CORRECTIONS CIBLÉES.**

## 1. Décision SQL O-03-24

J'accepte le remplacement de la contrainte `auth.user_permissions.ck_user_permissions_permission` par DROP CONSTRAINT puis ADD CONSTRAINT, dans la même transaction PostgreSQL.

Aucun DROP TABLE ni DROP SCHEMA n'est autorisé.

Script autorisé :
`docs/phase/03/sql/0004_AppointmentsModule.idempotent.sql`

SHA-256 obligatoire :
`9bd07d90b2578e95226c290e9a3ef1fd3bb83f79a7cee24b0e00cbcd07122399`

Avant application, vérifier :
- Intégrité et empreinte du SQL.
- Concordance avec la migration EF Core versionnée.
- PostgreSQL 18 sur le port 5433.
- Identité exacte du rôle et de la base.
- Présence exclusive des migrations 0001, 0002 et 0003.
- Absence d'objets inattendus et de dérive du schéma.

Si les contrôles réussissent, appliquer la migration sur `mmv_v2_dev`, puis `mmv_v2_test`, avec le script sécurisé existant.

En cas d'anomalie : STOP, rapport et aucune tentative de réparation destructive.

PostgreSQL 17 est strictement interdit.

Aucun rollback destructif.

## 2. Corriger les anomalies de l'audit

### B2-REV-01 — Collaborateur inéligible

Dans `AppointmentService.UpdateAsync`, corriger le contrôle d'éligibilité.

Un déplacement vers un nouveau créneau doit revérifier que le responsable est toujours actif et explicitement éligible, même lorsque son identifiant n'a pas changé.

Ajouter les tests de régression.

### B2-REV-02 — Réattribution du passé

Interdire le changement de responsable d'un rendez-vous dont le début est passé.

Ne pas changer ses informations historiques de réservation.

Ajouter les tests correspondants.

### B2-REV-03 — Fuseaux horaires

Étudier O-03-22 et mettre en place une stratégie vérifiable de cohérence entre les conversions horaires du backend Windows et celles de React/WebView2.

Tester notamment Africa/Casablanca et Europe/Brussels, les transitions d'heure et les dates futures.

Ne pas déclarer le problème résolu par un simple test réussi sur une période sans transition.

Si une divergence peut modifier l'heure réelle d'un rendez-vous, ne pas autoriser l'exploitation du magasin concerné tant que le problème n'est pas maîtrisé.

### B2-REV-04 — Réservations simultanées

Ajouter un vrai test PostgreSQL avec deux connexions :

- Poste A et poste B tentent de réserver le même créneau.
- La transaction du poste A est réellement validée par COMMIT.
- Le poste B attend la libération du verrou.
- Le poste B reçoit un refus de conflit ou de capacité.
- Un seul rendez-vous planifié subsiste avant le nettoyage du test.

Tester le pool d'accueil et le même responsable.

Exécuter exclusivement sur mmv_v2_test, avec données fictives identifiables et nettoyage contrôlé.

## 3. Clarifications O-03-23

Retenir provisoirement ces règles documentées :

- Rétablissement refusé lorsque l'heure de début est passée.
- Changement de créneau ou de responsable dans le passé interdit.
- Correction des notes et du motif d'un ancien rendez-vous permise, avec auteur et date de dernière modification.
- Annulation d'un rendez-vous passé permise.
- Fin exactement à 00:00 acceptée.
- Rendez-vous traversant un changement d'heure problématique refusé.

Conserver comme dette produit l'historique détaillé des modifications, annulations et rétablissements. Ne pas annoncer une traçabilité exhaustive avant son implémentation.

## 4. Tests après migration

Exécuter :
- PostMigration sur les deux bases.
- AppointmentsWrite sur mmv_v2_test.
- ClientsWrite et IdentityWrite en non-régression.
- Toutes les suites .NET.
- Typage, lint, tests frontend et build.
- Matrice HTTP des droits et conflits.
- Tests de sécurité et de validation des entrées.

Distinguer PASS, FAIL corrigé, NOT_TESTED et NOT_RERUN.

## 5. Configuration et test natif

Préparer le test manuel dans Tauri.

Ne choisir aucun fuseau ou horaire fictif à la place du propriétaire.

Une fois le fuseau réel du magasin fourni, procéder à la configuration puis tester la chaîne :

Tauri → HTTPS → API → PostgreSQL 18.

Scénarios : création, modification, déplacement, glisser-déposer, annulation, rétablissement, capacité d'accueil, responsable inéligible, et création depuis la fiche client sans seconde recherche.

Ne pas attribuer de nouvelles permissions à un compte réel sans accord du propriétaire.

## 6. Documentation Gate

Mettre à jour tous les documents concernés, notamment :
README, décisions 03-B, QA_MATRIX, GATES_AND_DECISIONS, IMPLEMENTATION_SPEC, SOURCE_MAP, architecture et backlog.

Conserver l'historique des anomalies et de leurs corrections.

## 7. Livraison

Travaille sur `feat/phase-03-clients-rdv`.

Commits locaux autorisés.

Aucun push, aucune PR, aucun merge.

Ne commence pas la Phase 04.

Livrer une archive git archive du HEAD, les résultats des tests réels et un rapport consolidé.

N'interromps le travail que pour une anomalie bloquante ou une intervention du propriétaire indispensable.

**PHASE03_B3_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED**
