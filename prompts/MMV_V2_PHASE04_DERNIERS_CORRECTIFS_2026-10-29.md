# MMV V2 — PHASE 04 — DERNIERS CORRECTIFS AVANT CLÔTURE

La livraison `PHASE04_C_COMPLETE` au commit `97bfd5e` a été auditée.

**Décision : 04-C acceptée techniquement. G04-FINAL reste ouvert pour trois corrections ciblées.**

Lire le rapport d'audit joint `MMV_V2_AUDIT_FINAL_PHASE04_C_2026-10-28.md`.

## 1. O-04-53 — Archivage client concurrent

Corriger la possibilité qu'une écriture optique soit validée après le commit d'archivage du client, alors que son éligibilité avait été contrôlée avant.

Exigences :

- Synchronisation transactionnelle PostgreSQL entre écriture optique et archivage du client.
- Vérification et verrouillage appropriés dans la même transaction.
- Ordre cohérent des verrous afin d'éviter les interblocages.
- Si l'archivage gagne : aucune nouvelle création, modification de brouillon, confirmation ou rectification optique ne doit être validée.
- Si l'écriture optique gagne : elle peut être validée, puis le client peut être archivé.
- La consultation et l'invalidation motivée d'un dossier déjà existant restent permises selon les permissions.

Ne pas considérer une simple seconde lecture du statut comme une protection suffisante.

## 2. O-04-54 — Désactivation concurrente du collaborateur

Garantir la cohérence transactionnelle entre la désactivation ou la perte d'habilitation d'un collaborateur et une nouvelle confirmation de mesure magasin.

Préserver impérativement :

- Le nom du collaborateur historique (`citedUsers`).
- Les anciennes mesures confirmées.
- Les rectifications qui conservent un collaborateur historique, selon la règle déjà approuvée.
- Le refus de nouvelles mesures lorsque le collaborateur n'est plus éligible.

Ajouter des tests PostgreSQL réels pour les deux ordres de concurrence et les changements de permissions.

## 3. O-04-52 — Corriger le texte du formulaire

Le pas ± de 0,25 dioptrie est officiellement approuvé pour Sphère, Cylindre et Addition.

Remplacer les libellés obsolètes parlant de « pas de démonstration », « D-04-06 ouverte » ou de « Prototype » dans le formulaire réel.

Ne pas laisser entendre que les règles cliniques encore ouvertes sont approuvées. Le Prisme conserve sa saisie libre sans pas ± imposé.

Conserver exactement le design validé du Lot 3. Aucune refonte.

## 4. Tests et contrôle

Après les correctifs :

- Rejouer les tests de concurrence sur PostgreSQL 18.6, port 5433, base `mmv_v2_test` uniquement.
- Démontrer les deux ordres de validation pour chaque conflit, avec barrières de synchronisation déterministes.
- Vérifier absence de doublon, de perte de données, d'événement d'audit incohérent et d'erreur HTTP 500.
- Rejouer les tests .NET, PostgreSQL et frontend affectés.
- Vérifier visuellement le nouveau libellé sous Tauri.
- Vérifier que les données de test sont purgées.
- Ne pas modifier ni arrêter les processus du propriétaire.

Le SQL 0006 déjà approuvé et appliqué doit rester inchangé.

Si une nouvelle migration ou une modification de schéma est indispensable, **STOP et demande de revue SQL**, sans l'appliquer.

## 5. Limites

Ne pas rouvrir les décisions métier déjà approuvées.

Ne pas changer arbitrairement les règles cliniques, les conventions prismatiques ni le cas des deux sphères à zéro.

Ne pas activer le module pour des données médicales réelles.

Ne pas toucher PostgreSQL 17 (5432), aux comptes réels ni aux données existantes de `mmv_v2_dev`.

Commits locaux autorisés. Aucun push, aucune PR, aucun merge.

## 6. Livraison finale

Synchroniser les documents, décisions, tests et réserves.

Fournir :

1. Le nouveau commit et le diff exact.
2. Les résultats des tests de concurrence avant/après correction.
3. Les preuves PostgreSQL et les contrôles de régression.
4. Une capture Tauri avec le libellé corrigé.
5. Une archive ZIP légère d'audit final.
6. Les réserves non bloquantes qui subsistent.

Statut attendu :

**PHASE04_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED**

Ne commencer ni la Phase 05 ni aucun autre module avant le GO du propriétaire.
