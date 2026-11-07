# MMV V2 — PHASE 05-00 — VALIDATION MÉTIER ET CLÔTURE DU CADRAGE

Le propriétaire a répondu aux 20 questions du cadrage métier de la Phase 05.

**Décision : GO pour la finalisation du cadrage 05-00.**

Objectif : mettre à jour le dépôt avec les décisions approuvées, intégrer les références de la Phase 05 dans `references/`, puis préparer le lancement du développement.

## 1. Décisions métier à intégrer

Q01 — Six familles : montures optiques, lunettes de soleil, verres correcteurs, lentilles et solutions, accessoires, prestations.

Q02 — Modèle produit + variantes. SKU, prix et stock par variante.

Q03 — Catalogue des verres configurable par fabricant, gamme, indice et traitements.

Q04 — Politique de stock configurable par variante : stocké, commandé à la demande ou service non stockable.

Q05 — SKU interne unique, références fabricant et fournisseur, code-barres facultatif.

Q06 — Architecture multi-emplacements et multi-magasins préparée.

Q07 — Stock physique géré par quantité et variante ; traçabilité individuelle possible ultérieurement.

Q08 — Stock négatif interdit.

Q09 — Réservation lors de la validation d'une vente ou commande, pas automatiquement au devis.

Q10 — Inventaires complets ou partiels, comptages, écarts justifiés et validation.

Q11 — Plusieurs fournisseurs possibles par article, dont un fournisseur principal.

Q12 — Phase 05 limitée aux fournisseurs, prix d'achat et réceptions simples. Commandes fournisseurs complètes reportées.

Q13 — Prix par variante, avec calcul par coefficient multiplicateur et possibilité de prix manuel autorisé.

Q14 — Devises et fiscalité configurables par magasin.

Q15 — Historique des prix ; aucune modification rétroactive des ventes.

Q16 — Permissions distinctes pour catalogue, tarifs, coûts, fournisseurs, stock et inventaires.

Q17 — Architecture d'import CSV contrôlé prévue, réalisation avant le premier déploiement.

Q18 — Alertes de stock faible avec seuil configurable.

**Q19 — NOUVELLE DÉCISION APPROUVÉE**

Prix de vente HT = prix d'achat HT × coefficient multiplicateur.

- Prix d'achat HT enregistré.
- Coefficient configurable par variante.
- Prix de vente HT calculé.
- Prix manuel possible avec permission spécifique.
- Recalcul explicite, jamais silencieux.
- Historisation des modifications.
- Précision décimale adaptée ; aucun calcul monétaire en flottants binaires.
- Arrondi monétaire explicite à spécifier.
- Prix TTC calculé séparément à partir de la fiscalité validée du magasin.
- Aucune taxe ni taux fiscal inventé.
- Ne pas recalculer rétroactivement les ventes anciennes.

**Q20 — NOUVELLE DÉCISION APPROUVÉE**

Prévoir deux environnements pilotes fictifs :

- Belgique : EUR.
- Maroc : MAD.

La devise appartient au magasin. Aucune conversion automatique entre EUR et MAD.

Ne pas créer de vrais comptes ni écrire de données pilotes dans PostgreSQL à cette étape.

## 2. Références Phase 05 — EXIGENCE NOUVELLE

Créer et organiser les références de la Phase 05 dans le dossier existant `references/`.

Avant toute opération :

1. Examiner la structure réelle de `references/` et sa convention de nommage.
2. Rechercher les références déjà disponibles de la Phase 05, notamment le Lot 6 Catalogue / Stock / Inventaires / Fournisseurs.
3. Réutiliser et copier sans modification les fichiers sources de design réellement disponibles.
4. Ne pas fabriquer les neuf états sans planche signalés dans la matrice.
5. Ne pas inventer de fichiers ou de références introuvables.
6. Ne pas modifier les références des Phases 00 à 04.
7. Éviter les doublons de fichiers volumineux et les déplacements qui casseraient des liens.

Organisation indicative :

`references/phase05/lot6/`

Adapter le chemin aux conventions effectives du dépôt.

Ajouter :

- Le fichier HTML source du Lot 6, s'il existe et peut être copié.
- Les ressources nécessaires à sa consultation, si elles existent.
- Un `README.md` décrivant le contenu et les limites.
- Un index permettant de retrouver les groupes, écrans et états.
- Un manifeste SHA-256 des fichiers de référence.

**Important :** le Lot 6 a été conçu en s'appuyant sur des éléments de l'ancien logiciel. Il sert uniquement de référence visuelle et ergonomique.

Ne jamais consulter ni réutiliser l'ancien dépôt MMV, son code, ses DTO, son modèle de données ou ses migrations.

Les nouveaux modèles métier doivent dériver exclusivement des décisions approuvées et de l'architecture MMV V2.

## 3. Synchronisation documentaire

Mettre à jour :

- `docs/phase/05.md`
- `docs/phase/05/CADRAGE_05_00.md`
- `docs/phase/05/DECISIONS_05_METIER_PROPOSED.md` : convertir les décisions effectivement ratifiées en APPROVED et conserver les questions réellement ouvertes.
- `docs/phase/05/DATA_MODEL_PROPOSED.md`
- `docs/phase/05/API_PROPOSED.md`
- `docs/phase/05/IMPLEMENTATION_SPEC.md`
- `docs/phase/05/QA_MATRIX.md`
- `docs/phase/05/MATRICE_LOT6.md`
- `docs/phase/05/GATES_AND_DECISIONS.md`
- `docs/phase/05/SOURCE_MAP.md`
- `docs/ADR_ET_DECISIONS.md`
- `README.md`

Conserver les décisions historiques. Ne pas renuméroter arbitrairement les identifiants D-05.

Vérifier notamment les divergences concernant :

- Fournisseur principal sur les articles commandés : distinguer création d'une fiche incomplète et commande effective.
- Calcul HT × coefficient et fiscalité.
- Disponibilité et réservations futures.
- Permissions de comptage et de validation d'inventaire.
- Support de deux magasins fictifs EUR/MAD.

Ne pas déclarer une règle clinique ou juridique approuvée sans validation.

## 4. Git et environnement

La Phase 04 a été fusionnée dans `main`, PR #5.

Le cadrage 05-00 est actuellement constitué de documents non suivis, et `apps/desktop/.env.development` comporte une modification locale du propriétaire.

J'autorise la création de :

`feat/phase-05-catalogue-stock`

Depuis le `main` fusionné, à condition de préserver intégralement les modifications locales.

Ne pas modifier, supprimer ou réinitialiser `.env.development`.

Ne pas utiliser `git reset --hard`, `git clean` ni `git stash` sur des fichiers du propriétaire.

Si un conflit apparaît, STOP et expliquer précisément.

## 5. Limites de la mission

Cette mission clôture le cadrage documentaire 05-00.

Aucun backend 05-A ou 05-B à développer maintenant.

Aucune migration 0007 à générer ou appliquer.

Aucune création de table et aucune écriture dans `mmv_v2_dev` ou `mmv_v2_test`.

Ne pas arrêter les processus du propriétaire.

Commits locaux documentaires autorisés sur la branche Phase 05.

Aucun push, aucune Pull Request, aucun merge.

## 6. Livraison attendue

Fournir :

1. Nouvel état Git et SHA du commit.
2. Registre D-05 avec décisions approuvées et questions restantes.
3. Liste exacte des références ajoutées dans `references/phase05/`.
4. Empreintes SHA-256 et preuve d'intégrité.
5. Matrice de correspondance Lot 6 ↔ futurs modules.
6. Spécification finale du coefficient multiplicateur.
7. Spécification EUR/MAD et configuration fiscale non encore ratifiée.
8. Résultat de la vérification des liens documentaires.
9. Proposition concise de séquence d'implémentation 05-A / 05-B / 05-C.

**Statut attendu : PHASE05_00_COMPLETE — OWNER_IMPLEMENTATION_REVIEW_REQUIRED.**

Ne pas commencer le développement sans le GO du propriétaire.
