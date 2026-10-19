# MMV V2 — GO PHASE 03-D3 — MIGRATION 0005 ET CLÔTURE

L'archive du commit b0f7abc a fait l'objet d'un audit indépendant.

VERDICT : 03-D1 VALIDÉE TECHNIQUEMENT. 03-D2 : GO CONDITIONNEL pour l'intégration locale.

Aucune refonte générale n'est demandée.

## 1. Migration PostgreSQL 0005

Script autorisé :

`docs/phase/03/sql/0005_AuditJournal.idempotent.sql`

SHA-256 obligatoire :

`1b42d82c85d21d18b58f465f681258641dbd2116d073ae58bbdf69cc8d41daee`

Avant toute écriture :
- Vérifier le SHA-256 complet.
- Vérifier la correspondance avec la migration EF Core.
- Vérifier PostgreSQL 18, port 5433, rôles et bases exacts.
- Vérifier que les migrations 0001 à 0004 sont présentes et que 0005 est absente.
- Vérifier l'absence de dérive du schéma.
- Préparer une sauvegarde vérifiée de mmv_v2_dev avant l'opération.
- Respecter les processus ouverts par le propriétaire.

Si tous les contrôles sont conformes, appliquer séparément sur mmv_v2_dev puis mmv_v2_test avec Invoke-MmvSqlScript.ps1.

Vérifier le COMMIT, l'historique EF, les contraintes, les index et les déclencheurs.

En cas d'anomalie, STOP.

Aucun rollback destructif et aucune intervention sur PostgreSQL 17.

## 2. Correction de traçabilité

Corriger ClientService.UpdateAsync.

Lorsqu'aucune information métier n'a réellement changé :
- Ne pas actualiser UpdatedAtUtc ou UpdatedByUserId.
- Ne pas modifier inutilement la version xmin.
- Ne pas enregistrer d'événement d'audit fictif.
- Renvoyer un résultat API cohérent.

Ajouter des tests unitaires et PostgreSQL.

Examiner les autres opérations Clients et Rendez-vous pour détecter une incohérence équivalente.

Ne pas modifier les migrations précédentes pour ce correctif.

## 3. Vérifications réelles après 0005

Exécuter :
- PostMigration sur dev et test.
- AuditWrite réel.
- ClientsWrite.
- AppointmentsWrite.
- IdentityWrite.
- Tests de concurrence à deux connexions.
- Suites .NET complètes.
- Vérification des types TypeScript, lint, Vitest et build.
- Build release Tauri.

Documenter les résultats réels, sans transformer NOT_TESTED en PASS.

## 4. Parcours natif de l'historique

Utiliser mmv_v2_test avec des données fictives et des comptes temporaires.

Tester la chaîne :

Tauri → HTTPS → API → PostgreSQL 18 → audit.events.

Tester :
- Création d'un client.
- Modification de ses informations.
- Archivage et réactivation.
- Création d'un rendez-vous.
- Déplacement et changement de responsable.
- Annulation et rétablissement.
- Chronologie correcte sur la fiche client.
- Affichage de l'auteur réel.
- Champs modifiés.
- Refus de consultation sans audit.read.
- Absence d'événement de succès après échec ou rollback.

Vérifier les deux thèmes et les zooms utiles.

Nettoyer exclusivement les données fictives de mmv_v2_test et vérifier l'absence de résidus.

Ne pas attribuer audit.read à un compte réel sans confirmation du propriétaire.

## 5. Politique d'audit

Documenter les décisions D-03-19 à D-03-23.

Conserver :
- Le schéma audit centralisé.
- Les écritures atomiques.
- La permission audit.read séparée.
- La minimisation des informations.

Ne pas présenter les déclencheurs comme une protection contre les superutilisateurs PostgreSQL.

D-03-22 reste ouverte pour les durées de conservation, les obligations RGPD/APD en Belgique et la loi 09-08/CNDP au Maroc.

Prévoir à terme un rôle SQL applicatif distinct du rôle de migration et une politique de rétention contrôlée.

## 6. Incident desktop.zip

Conserver desktop.zip et desktop.recovered.zip.

Aucun remplacement, suppression ou écrasement automatique.

Ne pas retraiter les archives, images ou fichiers non suivis par des commandes de normalisation textuelle.

Conserver O-03-41 dans le registre.

## 7. Documentation Gate

Synchroniser README, Phase 03, QA_MATRIX, SOURCE_MAP, GATES_AND_DECISIONS, IMPLEMENTATION_SPEC, architecture et ADR concernés.

Préserver l'historique des décisions, migrations et incidents.

## 8. Livraison

Rester sur feat/phase-03-clients-rdv.

Commits locaux autorisés.

Aucun push, aucune PR, aucun merge.

Ne pas commencer Phase 04.

Livrer un ZIP git archive actualisé, les résultats PostgreSQL après migration, les preuves natives et un rapport de clôture.

Ne pas demander de validation intermédiaire si les précontrôles autorisés sont tous satisfaits.

PHASE03_D3 — OWNER_FINAL_REVIEW_REQUIRED
