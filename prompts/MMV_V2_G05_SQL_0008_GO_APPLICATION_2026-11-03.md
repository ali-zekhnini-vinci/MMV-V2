# MMV V2 — G05-SQL 0008 APPROVED — GO APPLICATION LOCALE

J'ai fait auditer la migration 0008_CatalogCorrections.

**Verdict du propriétaire : GO CONDITIONNEL pour application sur `mmv_v2_dev`, puis `mmv_v2_test`.**

Script exclusivement approuvé :

`docs/phase/05/sql/0008_CatalogCorrections.idempotent.sql`

SHA-256 obligatoire :

`9a75acea0173a5019b520362cf04a5ca10e1de814753603f21d46c1c287e715c`

Je te transmets également le rapport `MMV_V2_AUDIT_G05_SQL_0008_2026-11-03.md`.

## 1. Précontrôles obligatoires

Avant toute écriture :

- Vérifier la branche `feat/phase-05-catalogue-stock`.
- Vérifier les 8/8 empreintes du manifeste directement dans le dépôt.
- Vérifier que les migrations EF Core et le SQL approuvé correspondent.
- Vérifier PostgreSQL 18 sur le port 5433.
- Ne jamais toucher à PostgreSQL 17 sur le port 5432.
- Confirmer les bases exactes `mmv_v2_dev` et `mmv_v2_test`.
- Confirmer la présence de 0001–0007 et l'absence de 0008.
- Vérifier l'absence de dérive du schéma.
- Vérifier qu'aucune opération concurrente ne risque d'écrire pendant la migration.

Ne pas arrêter mes processus actifs sans autorisation.

Si un précontrôle échoue, STOP avant toute écriture.

## 2. Sauvegardes

Sauvegarder les deux bases séparément, hors du dépôt.

Pour chaque sauvegarde :

- Vérifier que `pg_dump` a réussi.
- Vérifier que le fichier est non vide.
- Calculer son SHA-256.
- Vérifier son catalogue avec `pg_restore --list`.
- Conserver l'emplacement exact dans le journal de migration.

Ne pas prétendre qu'une restauration complète a été éprouvée si seul le catalogue de sauvegarde a été vérifié.

## 3. Application sur mmv_v2_dev

Appliquer exclusivement le script 0008 approuvé via `Invoke-MmvSqlScript.ps1`, avec empreinte imposée et `LockTimeout 5s`.

Après application :

- Confirmer le COMMIT.
- Confirmer la présence de la migration 0008 dans `__EFMigrationsHistory`.
- Exécuter `0008_CatalogCorrections.verify.sql` : 14/14 OK attendus.
- Vérifier le schéma et les contraintes.
- Confirmer que les données préexistantes sont préservées.
- Conserver les journaux.

En cas d'échec : STOP, ne pas appliquer sur test.

## 4. Application sur mmv_v2_test

Si dev est entièrement conforme, exécuter les mêmes contrôles sur test après sa propre sauvegarde.

Rejouer ensuite les tests réels :

- PostgreSQL Phases 02–05.
- COR-05-04 : import CSV intelligent, profils fournisseur, corrections, réémissions et transactions.
- COR-05-05 : offres de verres, plages, restrictions, historique, compatibilité et permissions.
- COR-05-01 à COR-05-08 : non-régression.
- API HTTPS.
- SignalR entre deux clients.
- Frontend et Tauri natif sur les parcours pertinents.

Utiliser exclusivement des données fictives.

Purger les données d'essai et documenter le résidu.

## 5. Contraintes

- Ne modifier aucune migration déjà approuvée, notamment 0007 et 0008.
- Toute nouvelle modification SQL nécessite une nouvelle revue.
- Préserver `.env.development`.
- Ne pas arrêter mes processus sans autorisation.
- Commits locaux autorisés.
- Aucun push, aucune PR, aucun merge.
- Ne pas commencer la Phase 06.

## 6. Livraison attendue

Fournir :

1. Preuves de sauvegarde.
2. Journal d'application dev/test.
3. Contrôles SQL 14/14 sur chaque base.
4. Vérification de l'intégrité des données existantes.
5. Résultats des suites complètes.
6. Captures natives complémentaires.
7. Liste des problèmes réellement restants.
8. Archive d'audit finale légère.

**Statut attendu : `PHASE05_CORRECTIONS_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.**

Ne pas déclarer la Phase 05 définitivement fusionnable avant mon audit final.
