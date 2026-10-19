# MMV V2 — GO PHASE 03-A3 — INTÉGRATION RÉELLE CLIENTS

Le rapport 03-A2 et le SQL normal de la migration ClientsModule ont été examinés.

GO pour finaliser l'intégration réelle du module Clients, sous les contrôles ci-dessous.

## 1. Migration PostgreSQL

Script autorisé :
docs/phase/03/sql/0003_ClientsModule.idempotent.sql

SHA-256 attendu :
4dbb0d84940d0891915adce2538fb0fee66e054bf4b3e9c347429c18d3db1c3b

Avant toute écriture :

* Vérifier l'empreinte complète.
* Vérifier que le script idempotent correspond aux opérations approuvées de ClientsModule.
* Vérifier que seules les migrations 0001 et 0002 sont appliquées.
* Contrôler réellement la base, le rôle, l'hôte local, le port 5433 et PostgreSQL 18.
* Vérifier le format des références à 1, 999999 et 1000000 sans modifier la séquence réelle.

Si tout est conforme, appliquer séparément à mmv_v2_dev, puis mmv_v2_test, avec les rôles propriétaires correspondants et le script sécurisé existant.

En cas d'échec ou d'état inattendu, STOP. Aucun rollback destructif automatique, aucun nettoyage.

Ne jamais toucher à PostgreSQL 17 ou aux autres bases.

## 2. Contrôles et tests réels

Après application :

* Vérifier les trois migrations EF Core et les objets créés.
* Exécuter les tests ClientsWrite sur mmv_v2_test uniquement.
* Tester recherche sans accents, doublons autorisés, archivage, réactivation, concurrence 409 et permissions.
* Tester le refus des champs API inconnus.
* Vérifier l'absence de résidus des tests.
* Exécuter les tests backend et frontend complets.

## 3. Permissions

Préparer la procédure de maintenance permettant au propriétaire d'attribuer clients.read, clients.write et clients.archive à son compte.

Ne modifier aucun compte réel sans son accord.

Pour les utilisateurs en lecture seule, masquer ou désactiver clairement les actions interdites.

Le serveur reste l'autorité définitive : aucune permission n'est accordée implicitement par system.admin.

## 4. Intégration Tauri

Tester la chaîne réelle :

Tauri → HTTPS → API Clients → PostgreSQL 18.

Vérifier les écrans, la création, la modification, la recherche, l'archivage et les états de refus et de conflit.

Aucune donnée fictive dans la base de développement. Utiliser des comptes et clients éphémères exclusivement dans mmv_v2_test pour les tests d'intégration.

Produire les captures natives utiles et comparer aux maquettes Clients.

## 5. Documentation Gate

Synchroniser tous les documents concernés :
README, phase 03, QA_MATRIX, GATES_AND_DECISIONS, IMPLEMENTATION_SPEC, SOURCE_MAP, décisions métier, architecture et ADR si nécessaire.

Préserver l'historique, consigner les migrations réellement appliquées, les résultats des tests et les limites restantes.

## 6. Livraison

Réaliser des commits locaux sur feat/phase-03-clients-rdv.

Aucun push, aucune PR, aucun merge.

Ne pas commencer Rendez-vous 03-B ni la Phase 04.

Travailler en autonomie dans le périmètre autorisé et fournir un seul rapport consolidé, sauf anomalie réellement bloquante ou action du propriétaire indispensable.

Livrer un ZIP léger via git archive pour revue du code complet.

PHASE03_A3_COMPLETE — OWNER_REVIEW_REQUIRED
