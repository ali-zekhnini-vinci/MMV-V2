# MMV V2 — G05-SQL APPROVED — GO MIGRATION 0007 ET INTÉGRATION FINALE

L'archive `MMV-V2-phase05-G05SQL.zip` a été auditée.

**Verdict : GO CONDITIONNEL pour l'application locale de la migration 0007.**

Script exclusivement autorisé :

`docs/phase/05/sql/0007_CatalogStockModule.idempotent.sql`

SHA-256 obligatoire :

`39c9c22400a3319d80724cd63110977ec39de912c3f60f3f2bc0bdbb426e1eb6`

Lire également le rapport d'audit `MMV_V2_AUDIT_G05_SQL_0007_2026-10-30.md`, que je joins.

## 1. Précontrôles

Avant toute écriture :

- Vérifier le HEAD, la branche `feat/phase-05-catalogue-stock` et le diff.
- Vérifier les 12 empreintes du manifeste directement dans le dépôt, y compris les trois fichiers complets absents du ZIP d'audit.
- Confirmer PostgreSQL 18 sur le port 5433. Ne jamais contacter PostgreSQL 17 sur 5432.
- Vérifier les identités exactes des bases, rôles et instances.
- Confirmer que les migrations 0001 à 0006 sont appliquées et que 0007 est absente.
- Contrôler toute dérive de schéma.
- Vérifier les scripts générés et l'absence de modifications imprévues.
- Préserver intégralement mes fichiers locaux et mes processus.

Réaliser une sauvegarde vérifiée de `mmv_v2_dev`, hors dépôt, avec SHA-256 et contrôle `pg_restore --list`. Sauvegarder également `mmv_v2_test` si elle contient des données à préserver.

Ne pas présenter une sauvegarde simplement lisible comme une restauration complète testée.

**Si un précontrôle échoue : STOP, aucune écriture.**

## 2. Application des migrations

Si tous les précontrôles sont conformes :

1. Appliquer uniquement le script approuvé sur `mmv_v2_dev`, via `Invoke-MmvSqlScript.ps1`, avec empreinte imposée et `-LockTimeout 5s`.
2. Vérifier le COMMIT et la présence de 0007 dans l'historique EF Core.
3. Exécuter `0007_CatalogStockModule.verify.sql` : 27/27 attendus.
4. Vérifier les objets existants des Phases 02 à 04 et l'absence de modification des données antérieures.
5. Si tout est conforme, procéder de la même manière sur `mmv_v2_test`.
6. Rejouer l'inventaire exact du schéma et les contrôles de contraintes.

Aucune modification du SQL 0007 approuvé. Si elle devient indispensable, STOP pour une nouvelle revue d'empreinte.

Ne jamais arrêter ou redémarrer mes processus actifs sans mon autorisation.

## 3. Intégration réelle de la Phase 05

Sur `mmv_v2_test`, avec données fictives exclusivement :

- Tester les six familles, modèles et variantes.
- Créer deux magasins fictifs, Belgique EUR et Maroc MAD.
- Tester le catalogue des verres et les fournisseurs multiples.
- Tester prix HT × coefficient, arrondis, prix manuel et protection des coûts.
- Tester entrées, sorties, ajustements et réceptions partielles.
- Vérifier les interdictions de stock négatif et de sortie du stock réservé.
- Tester les inventaires complets, partiels et concurrents.
- Tester le parcours CSV : prévisualisation, contrôle à blanc, import, rejet atomique et absence de doublons.
- Tester les 16 permissions.
- Rejouer les suites PostgreSQL complètes des Phases 02 à 05.
- Effectuer les essais HTTPS réels et Tauri natifs.
- Vérifier les thèmes clair/sombre et le design Lot 6.
- Purger les données fictives de test et documenter le résidu.

## 4. Corriger le test frontend en échec

La suite Vitest présente encore un échec lié à `VITE_MMV_OPTICAL_MODULE` dans mon environnement local.

Corriger l'isolation du test afin que sa réussite ne dépende pas de ma configuration personnelle.

**Ne pas supprimer, modifier ou réinitialiser `apps/desktop/.env.development`.**

Rejouer l'intégralité des tests frontend et distinguer les échecs reproductibles des problèmes de délai.

Aucune clôture finale avec un test persistant en échec.

## 5. Réserves techniques et documentaires

Conserver comme points ouverts :

- Rattachement des emplacements aux magasins dans les contraintes PostgreSQL.
- Conservation des contenus CSV et politique de purge.
- Rôle applicatif SQL à privilèges limités.
- Restauration complète de sauvegarde.
- Fiscalité EUR/MAD, G05-LEGAL et validations réglementaires.
- Choix techniques non encore ratifiés du registre D-05.
- Exports CSV différés.
- Références Lot 6 qui ne sont pas entièrement versionnées.

Ne pas déclarer ces réserves résolues sans preuve. Ne pas modifier le schéma pour les résoudre sans une nouvelle revue SQL.

## 6. Livraison finale

Maintenir toute la documentation synchronisée.

Commits locaux autorisés.

Aucun push, aucune PR, aucun merge.

Ne pas commencer la Phase 06.

Produire une archive d'audit finale comprenant les preuves de migration, les tests réels, les correctifs frontend éventuels, les captures Tauri et les réserves restantes.

**Statut attendu : PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED.**

La clôture et l'autorisation de PR restent soumises à mon audit final.
