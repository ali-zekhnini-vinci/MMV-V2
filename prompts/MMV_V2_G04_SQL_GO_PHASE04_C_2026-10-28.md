# MMV V2 — G04-SQL APPROVED — GO MIGRATION 0006 ET PHASE 04-C

L'archive de la Phase 04-B a été auditée indépendamment.

**VERDICT : G04-SQL — GO CONDITIONNEL POUR L'APPLICATION LOCALE.**

Script exclusivement autorisé :

`docs/phase/04/sql/0006_OpticalModule.idempotent.sql`

SHA-256 exact :

`927ed7ccb975b15eed18f6b2bcee41faf08ecb23c1c4f9f1f80a2efa1706c013`

## 1. Précontrôles obligatoires

Avant toute écriture :

- Vérifier l'empreinte complète du script.
- Confirmer PostgreSQL 18.6 sur le port 5433.
- Vérifier les noms exacts des bases et des rôles `mmv_v2_dev` et `mmv_v2_test`.
- Vérifier les migrations 0001 à 0005 présentes et 0006 absente.
- Confirmer l'absence de dérive du schéma.
- Vérifier que les trois scripts SQL correspondent à la migration EF Core.
- Vérifier l'état des processus du propriétaire et préparer une fenêtre de maintenance.
- Réaliser une sauvegarde vérifiée de `mmv_v2_dev`, hors dépôt, avec empreinte et inventaire des objets.

Ne jamais contacter PostgreSQL 17 sur le port 5432.

Si un précontrôle échoue : STOP, aucune écriture.

## 2. Application

Si tous les précontrôles passent :

1. Appliquer exclusivement le script approuvé sur `mmv_v2_dev`.
2. Confirmer le COMMIT et l'historique des migrations.
3. Exécuter les contrôles de schéma en lecture seule.
4. Si tout est conforme, appliquer le même script sur `mmv_v2_test`.
5. Vérifier à nouveau le COMMIT, les tables, les index, les contraintes, les déclencheurs et les permissions.

Utiliser `Invoke-MmvSqlScript.ps1` avec l'empreinte imposée et un délai d'attente de verrou de 5 secondes.

Aucun script alternatif, aucune modification improvisée du SQL et aucun rollback destructif.

En cas d'anomalie : STOP et rapport précis.

## 3. Tests réels de la Phase 04-C

Après migration, exécuter les suites PostgreSQL 18, .NET et frontend complètes, puis les parcours HTTP/HTTPS et Tauri natifs sur `mmv_v2_test` avec des comptes et données fictifs.

Tester :

- Création et confirmation atomiques d'une ordonnance.
- Création et confirmation d'une mesure magasin.
- Enregistrement et reprise d'un brouillon.
- Modification et conservation des révisions confirmées.
- Invalidation motivée.
- Contrôles des quatre permissions optiques.
- Accès aux anciennes révisions avec `audit.read`.
- Refus sans permission et absence de fuite des valeurs optiques.
- Deux postes modifiant le même enregistrement.
- Réémission d'une commande après réponse réseau incertaine.
- Archivage du client simultané à une écriture optique.
- Désactivation d'un collaborateur simultanée à une confirmation de mesure.
- Cohérence de l'interface avec les réponses réelles de l'API.
- Parcours OD/OG et boutons ± dans Tauri natif.

Les tests doivent distinguer clairement PASS, FAIL et NOT_TESTED. Corriger tout défaut critique identifié.

Ne pas activer le module avec des données médicales réelles.

## 4. Réserves produit

Conserver dans le backlog :

- Validation professionnelle des règles optiques et des cas limites, notamment les deux sphères à zéro.
- Composantes prismatiques, bases et conventions.
- Compte PostgreSQL applicatif distinct du compte de migration.
- Restauration complète de sauvegarde sur base indépendante.
- Confidentialité et conformité Maroc/Belgique.
- Robustesse des réémissions tardives et de l'archivage concurrent.

Ces réserves ne doivent pas être déclarées résolues sans preuve.

## 5. Documentation et livraison

Respecter le DOCUMENTATION GATE complet.

Préserver les décisions approuvées et les historiques.

Commits locaux autorisés. Aucun push, aucune PR, aucun merge.

Ne commencer aucun module supplémentaire.

Produire un ZIP d'audit final contenant les preuves de migration, les tests réels, les captures Tauri et les correctifs éventuels.

**Statut attendu : PHASE04_C_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED.**

La clôture globale de la Phase 04 reste soumise à mon audit final.
