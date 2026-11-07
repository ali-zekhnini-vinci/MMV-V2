# MMV V2 — GO OFFICIEL IMPLÉMENTATION COMPLÈTE PHASE 05

*Mandat du propriétaire reçu le 30/10/2026, consigné tel quel.*

## Décision du propriétaire

J'autorise immédiatement le développement réel de la **Phase 05 — Catalogue, Tarification, Fournisseurs, Stock, Inventaires et Import CSV**.

Le cadrage 05-00 est terminé. Les décisions métier Q01 à Q20 sont approuvées.

**Je ne veux plus de succession de questionnaires, de phases documentaires intermédiaires ou de demandes de GO après chaque sous-lot.**

Tu dois avancer de manière autonome, implémenter, tester et corriger les problèmes rencontrés.

Tu ne dois t'arrêter que devant une décision réellement bloquante, une opération risquée sur mes données ou le GATE SQL obligatoire.

## 1. Vérification initiale

- Te placer sur `feat/phase-05-catalogue-stock`.
- Confirmer que la branche part du `main` contenant la Phase 04 fusionnée, PR #5.
- Lire les documents existants de `docs/phase/05/`.
- Examiner les décisions D-05, les questions QR et les références du Lot 6.
- Préserver intégralement `apps/desktop/.env.development` et toutes mes modifications locales.
- Ne pas consulter ou réutiliser le code de l'ancien MMV.

### Questions ouvertes

Pour D-05-07, D-05-09, D-05-13 et QR-01 à QR-13 :

1. Vérifier si Q01–Q20 y répondent déjà.
2. Appliquer directement les décisions correspondantes lorsqu'elles existent.
3. Utiliser les règles techniques conservatrices proposées ci-dessous lorsqu'aucune décision antérieure ne les contredit.
4. Documenter les choix techniques et les hypothèses non approuvées.
5. Si une décision réellement structurante reste impossible à déduire, me soumettre une seule synthèse consolidée des arbitrages bloquants, pas plusieurs questionnaires.

Ne pas bloquer le développement des fonctions indépendantes de ces points.

## 2. Règles d'implémentation retenues

Respecter intégralement Q01–Q20.

Appliquer les conventions suivantes si elles ne contredisent aucune décision officielle :

- Fournisseur principal facultatif pendant la préparation d'une fiche incomplète ; obligatoire pour une commande fournisseur effective.
- Stock négatif interdit.
- Prix de vente HT = prix d'achat HT × coefficient.
- Coefficient strictement positif.
- Calculs monétaires décimaux exclusivement côté serveur.
- Arrondi du prix HT à deux décimales, à mi-chemin en s'éloignant de zéro, une seule fois.
- Prix manuel possible avec permission dédiée.
- Coûts, coefficients et marges protégés par une permission spécifique, dans l'API comme dans l'interface.
- Recalcul de prix jamais silencieux.
- Historisation des changements de prix.
- Comptage et validation d'inventaire protégés par des permissions distinctes.
- Aucune conversion automatique entre EUR et MAD.
- Aucun taux de TVA inventé.
- Aucune vente réelle autorisée tant que la fiscalité et les autres exigences applicables restent ouvertes.

## 3. Références Phase 05

Le propriétaire a demandé que les références de Phase 05 figurent dans `references/`.

Examiner les règles Git existantes.

Versionner les fichiers nécessaires à la consultation du Lot 6, à condition qu'ils soient sûrs à partager et qu'ils ne contiennent ni code confidentiel hérité ni données sensibles.

Inclure le HTML, ses dépendances réelles, le README, l'index et les empreintes.

Ne pas recopier les anciennes entités, DTO ou règles métier du logiciel MMV précédent.

Vérifier que les références fonctionnent après un nouveau clone Git, ou documenter explicitement le mécanisme de récupération lorsque leur versionnement direct n'est pas approprié.

## 4. Développement 05-A — Catalogue, Tarification et Fournisseurs

Implémenter réellement :

- Les six familles de produits approuvées.
- Produits, modèles et variantes.
- SKU internes uniques, références fabricant, références fournisseur et codes-barres facultatifs.
- Politiques stocké, commandé à la demande et non stockable.
- Magasins et emplacements.
- Devise par magasin.
- Catalogue configurable des verres.
- Fournisseurs multiples et fournisseur principal.
- Prix d'achat HT par variante.
- Coefficients multiplicateurs.
- Aperçu des prix de vente HT.
- Prix manuel autorisé.
- Historique des prix.
- Contrôles des permissions et de la concurrence.
- API .NET et écrans React intégrés au vrai serveur.

Reprendre l'expérience visuelle du Lot 6 en tenant compte des nouvelles règles métier approuvées.

## 5. Développement 05-B — Stock et réceptions

Implémenter :

- Quantités physiques par emplacement et variante.
- Disponibilités et quantités réservées, avec contrat prêt pour la Phase 06.
- Entrées et sorties de stock.
- Ajustements motivés.
- Historique immuable des mouvements enregistrés.
- Réceptions simples de fournisseurs.
- Réceptions partielles lorsqu'elles sont pertinentes.
- Alertes de stock faible.
- Seuils configurables.
- Contrôles empêchant les sorties concurrentes excessives.
- Contrôle du stock négatif au niveau transactionnel.

Ne pas inventer un stock physique pour les services ou les produits exclusivement commandés à la demande.

## 6. Développement 05-C — Inventaires

Implémenter :

- Inventaires complets et partiels.
- Sessions de comptage.
- Comptage par emplacement.
- Enregistrement des quantités constatées.
- Comparaison avec les quantités théoriques.
- Justification des écarts.
- Validation avec permission spécifique.
- Ajustements atomiques.
- Historique et audit.
- Gestion des conflits de comptage et des mouvements concurrents.

Un inventaire ne doit jamais écraser silencieusement des mouvements intervenus pendant son comptage.

## 7. Développement 05-D — Import CSV

Implémenter un import contrôlé permettant notamment :

- Prévisualisation des données.
- Correspondance des colonnes.
- Vérification des références et doublons.
- Validation des formats.
- Détection des erreurs.
- Simulation avant import.
- Rapport des lignes acceptées et refusées.
- Gestion des permissions.
- Prévention des doublons en cas de réémission.

Aucun import destructif automatique.

## 8. Architecture et données

Stack obligatoire :

- Tauri 2.
- React et TypeScript.
- ASP.NET Core .NET 10.
- EF Core / Npgsql.
- PostgreSQL 18 exclusivement.

Respecter les conventions des Phases 02, 03 et 04.

Prévoir les futurs contrats nécessaires aux ventes, réservations, commandes et réceptions, sans développer prématurément la Phase 06.

Garantir :

- Transactions atomiques.
- Idempotence des opérations critiques.
- Permissions serveur.
- Audit sans fuite d'informations confidentielles.
- Concurrence multi-postes.
- Conservation des historiques.
- Aucune suppression physique non justifiée.
- Tests automatisés des erreurs, conflits et permissions.

## 9. Migrations PostgreSQL — UNIQUE ARRÊT OBLIGATOIRE

Tu es autorisé à générer les migrations EF Core et les scripts SQL correspondant à la Phase 05.

Tu peux utiliser une base ou un cluster jetable isolé pour tes essais techniques, sans toucher à mes bases existantes.

Ne crée pas artificiellement quatre migrations si une autre décomposition techniquement cohérente est préférable.

Avant toute application sur `mmv_v2_dev` ou `mmv_v2_test`, tu dois :

1. Finaliser les migrations nécessaires aux fonctions développées.
2. Fournir le SQL exact.
3. Fournir les empreintes SHA-256.
4. Fournir le diff de schéma.
5. Vérifier les migrations déjà présentes.
6. Préparer les sauvegardes et les contrôles d'application.
7. Présenter une seule revue consolidée `G05-SQL`.

**STOP OBLIGATOIRE À G05-SQL.**

Ne rien appliquer aux bases existantes sans mon GO explicite sur les scripts exacts.

Après mon GO, tu pourras appliquer les scripts approuvés selon la procédure de sauvegarde et de contrôle, puis exécuter les tests d'intégration réels.

Toute modification ultérieure d'un script approuvé exige une nouvelle revue de son empreinte.

## 10. Organisation du travail

Les sous-lots 05-A, 05-B, 05-C et 05-D sont des étapes internes de développement.

Tu peux effectuer des commits locaux réguliers, documenter et corriger en continu.

Ne me sollicite pas pour autoriser :

- Chaque nouvel écran.
- Chaque classe C#.
- Chaque route API.
- Chaque composant React.
- Chaque test.
- Chaque correctif technique ordinaire.
- Chaque commit local.

En revanche, ne prends pas seul de décision qui contredirait les règles métier approuvées, exposerait des données sensibles ou entraînerait une perte de données.

## 11. Tests et validation

Exécuter les tests pertinents :

- .NET unitaires et intégration.
- PostgreSQL réel sur environnement autorisé.
- API HTTPS.
- React / TypeScript / Vitest.
- Tauri natif.
- Permissions.
- Concurrence entre deux postes.
- Calculs des prix et précision monétaire.
- Stock négatif.
- Réceptions.
- Inventaires.
- Imports.
- EUR et MAD.
- Journal d'audit.

Présenter séparément les tests PASS, FAIL et NOT_TESTED.

## 12. Git et livraison

Commits locaux autorisés.

Aucun push, aucune PR, aucun merge : ces opérations restent sous mon contrôle, sauf nouvelle autorisation explicite.

Ne pas toucher aux processus actifs du propriétaire.

Maintenir la documentation du projet synchronisée.

Livrer un ZIP léger comprenant le diff, les rapports de tests, le dossier SQL et les preuves nécessaires à l'audit.

Ne pas commencer la Phase 06.

### Statuts attendus

Premier arrêt :

`PHASE05_IMPLEMENTED — OWNER_G05_SQL_REVIEW_REQUIRED`

Après approbation SQL et tests d'intégration :

`PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`

**Objectif : livrer une Phase 05 réellement implémentée, pas une nouvelle étude documentaire.**
