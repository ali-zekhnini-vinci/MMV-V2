# MMV V2 — DÉMARRAGE OFFICIEL PHASE 05-00

La Phase 04 — Dossier optique a été fusionnée dans `main` par le propriétaire.

Nous commençons la Phase 05 : **Catalogue, Fournisseurs, Stock et Inventaires**.

## 1. Vérification du dépôt

- Vérifier que `main` contient réellement tous les commits de la Phase 04, notamment `dcc6f0d`.
- Vérifier l'état du dépôt et récupérer les références distantes sans modifier les fichiers du propriétaire.
- Ne pas toucher aux bases PostgreSQL ni aux processus actifs.
- Préparer une branche locale `feat/phase-05-catalogue-stock` depuis le `main` fusionné, si l'arbre de travail est propre.
- Aucun push, aucune PR, aucun merge.

## 2. Sources à examiner

Consulter exclusivement les sources de MMV V2 :

- La documentation actuelle du dépôt.
- Le registre des décisions approuvées.
- Le Lot 6 `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`.
- Son inventaire de 37 groupes UX et 107 états.
- Le backlog Phase 00 et les compléments catalogue, achats et stock.
- L'architecture existante des Phases 02, 03 et 04.

Ne consulter ni reprendre aucun code ou schéma de l'ancien logiciel MMV.

## 3. Cadrage fonctionnel demandé

Établir les spécifications des quatre domaines :

1. Catalogue : produits, références, catégories, variantes, prix, disponibilité, archivage.
2. Fournisseurs : fiches, références, association produit-fournisseur et historique.
3. Stock : politiques de stockage, quantités, entrées, sorties, ajustements, réservations futures.
4. Inventaires : sessions de comptage, écarts, validations et audit.

Étudier impérativement trois politiques distinctes : article stocké, article sur commande, service non stockable.

Ne pas considérer ces politiques comme approuvées tant que le propriétaire ne les a pas ratifiées.

## 4. Architecture et qualité

Préparer une proposition cohérente avec .NET 10, EF Core, PostgreSQL 18, React et Tauri 2.

Identifier les permissions, transactions atomiques, conflits multi-postes, mouvements de stock, protection des prix d'achat et règles de disponibilité.

Prévoir les contrats nécessaires à la future Phase 06 — Ventes — sans développer celle-ci.

Conserver les exigences Maroc/Belgique, données sensibles, audit et traçabilité.

## 5. Limites

Cette première mission est un cadrage ciblé : **aucune migration, aucun changement de schéma, aucune nouvelle table, aucune écriture dans les bases dev/test**.

Ne pas produire des centaines de questions ni repartir dans une analyse générale du produit.

Regrouper les choix véritablement structurants dans un registre de décisions court, avec recommandations, implications et options.

## 6. Livrables

- Synthèse de l'état Git après fusion de la Phase 04.
- Périmètre proposé de 05-A/05-B/05-C.
- Matrice des écrans Lot 6 → fonctionnalités → API.
- Modèle de données proposé, sans migration appliquée.
- Liste des décisions métier réellement indispensables.
- Plan de tests métier, concurrence et stock.
- Risques et dépendances avec les Phases 06 et Commandes.
- Documentation de cadrage à relire.

**Statut attendu : PHASE05_00 — OWNER_BUSINESS_REVIEW_REQUIRED.**

Ne pas commencer l'implémentation ni la Phase 06 avant validation du cadrage.
