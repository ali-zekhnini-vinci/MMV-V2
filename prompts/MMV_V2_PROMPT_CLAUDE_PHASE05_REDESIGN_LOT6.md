# MMV V2 — G05-DESIGN NO-GO — REDESIGN FIDÈLE AU LOT 6

## Mandat du propriétaire

Le code fonctionnel de Phase 05 et la migration 0007 sont livrés et testés. Cependant, **le design de Phase 05 n'est PAS approuvé** : la réalisation ne respecte pas suffisamment les maquettes originales de Claude Design **Lot 6 — Catalogue, Stock, Inventaires et Fournisseurs**.

**OBJECTIF : corriger et harmoniser toutes les interfaces Phase 05 en prenant le Lot 6 comme autorité de référence visuelle, tout en respectant IMPÉRATIVEMENT toutes les décisions métier approuvées de MMV V2.** C'est une intervention UX/UI, pas une nouvelle phase de conception fonctionnelle ou une réécriture backend.

Lire le fichier joint `MMV_V2_PHASE05_AUDIT_FIDELITE_LOT6.md`, ses images de comparaison, puis ouvrir dans le ZIP Claude Design fourni par le propriétaire :

`MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`

Charger aussi les dépendances `_ds/` et `support.js` du ZIP. Vérifier les 37 groupes et 107 états. La source est un design statique ; ne pas copier les DTO, entités, données ou règles métier de l'ancien MMV cités dans le texte.

### 1. Priorité des sources, sans ambiguïté

1. **Décisions approuvées MMV V2 Q01–Q20, D-05 et correctifs déjà demandés** = autorité fonctionnelle.
2. **Design system et shell transversal de MMV V2** = préserver les composants communs et la cohérence des Phases 02–04.
3. **Claude Design Lot 6** = autorité pour le placement, la composition, les grilles, l'espacement, les tableaux, les cartes, les dialogues, les badges, les états clair/sombre et les comportements visuels du Lot 6.
4. Les fonctions supplémentaires de MMV V2 absentes de la maquette doivent être intégrées en **prolongeant les mêmes composants et tokens**, pas via une nouvelle UI disparate.

**Il est interdit de supprimer ou simplifier une fonctionnalité métier approuvée pour faciliter la reproduction du design.** Il est également interdit de considérer les anciennes suppressions physiques, catégories figées, prix TTC ou fournisseur unique de la maquette comme des décisions V2.

### 2. Corrections UX attendues

- **Catalogue** : remettre les filtres sur une rangée horizontale compacte aux résolutions desktop suffisantes ; structurer le tableau dans une carte avec en-tête, lignes, bordures, badges et footer cohérents. Supprimer la succession verticale de listes pleine largeur et le vide inutile. Conserver les six familles, les variantes, politiques de stock, magasins et les droits de consultation des coûts.
- **Création / modification produit** : retrouver la mise en page en cartes du Lot 6 et les grilles lisibles à deux colonnes ; sections Modèle, Variantes, prix/stock selon les règles V2 ; barre d'actions contextualisée en pied, gestion des erreurs sous champ, états dirty/fermeture/conflit. Ne pas aligner six champs minuscules de variante sur une seule ligne. **SKU interne généré automatiquement**, jamais exigé manuellement pour une création normale (correctif Q05 à conserver).
- **Détail produit / variante** : retrouver la hiérarchie du Lot 6 (informations, prix et disponibilité, fournisseur, historique) avec une distinction claire entre modèle, variante et magasin. Préserver historique, archivage, multi-fournisseurs et politiques de stock.
- **Coefficient multiplicateur** : dans le bloc **Prix**, ajouter naturellement « Prix d'achat HT », « Coefficient », « Aperçu du prix de vente HT », « Prix de vente HT validé », « Mode : coefficient / manuel » et le journal d'historique. Adapter la grille, pas créer un écran étranger au DS. L'aperçu et l'arrondi proviennent **exclusivement de l'API**. Recalcul jamais silencieux. Protéger coût/coefficient/marge et mode manuel par permissions. EUR ou MAD par magasin, pas de TTC fictif.
- **Stock** : KPI comme le Lot 6 (références, unités, stock faible, ruptures), zone fraîcheur/actualisation, filtres compacts et tableau visuellement cadré. Garder les stocks par emplacement et les quantités réservées (absents de l'ancienne maquette) via détail, colonne ou vue secondaire cohérente.
- **Mouvements / réceptions** : tableaux et modales dans le vocabulaire du Lot 6, champs groupés, boutons de confirmation placés selon les conventions, succès/erreurs/conflits ; maintenir interdiction de stock négatif et motifs.
- **Inventaires** : barre de progression, comptage par ligne, statut Compté/À compter, comparatif et justification des écarts, validation distincte du comptage. Maintenir les transactions qui ne détruisent pas les mouvements concurrents.
- **Fournisseurs** : fiche, filtres, tableau, états, détails et liens aux variantes dans la présentation Lot 6 ; pas de suppression physique, plusieurs fournisseurs possibles, fiche incomplète autorisée.
- **Référentiel verres et Import CSV** : états supplémentaires dessinés dans le DS Lot 6, avec les mêmes alignements, tables, badges, feedback et dialogues ; ne pas omettre les étapes d'import de contrôle à blanc.
- **Dark mode et responsive** : régler les surfaces, contrastes, logo sur fond blanc préexistant si pertinent dans ce lot, largeurs, densité, zoom et scroll. Ne pas modifier le shell commun sans tests de non-régression des phases déjà fusionnées.

### 3. Procédure obligatoire de comparaison

1. Inventorier **tous les 37 groupes et 107 cadres** du HTML et les rapprocher des 05-A/05-B/05-C/05-D.
2. Produire `docs/phase/05/G05_DESIGN_MATRIX.md` : pour chaque cadre, écran natif correspondant ou adaptation, statut `MATCH` / `ADAPTED_APPROVED` / `DEFERRED_APPROVED` / `NOT_TESTED`, capture source, capture après correction, justification précise. Ne pas déclarer une adaptation approuvée si le propriétaire ne l'a pas validée.
3. Créer les composants de composition réutilisables : header d'écran, toolbar compacte, filtres, tables, cartes, formulaires, barres d'action, états et dialogues, **en réutilisant les composants V2 existants** plutôt qu'en dupliquant le design system.
4. Travailler par famille et corriger les écarts visualisés dans le rapport. Vérifier les dispositions sur **vrais composants branchés à l'API**, pas seulement dans un banc fictif.
5. Produire des captures appariées de la maquette source et du client Tauri : catalogue, création, détail/prix, fournisseurs, stock, mouvements, inventaire, import et référentiel verres. Clair et sombre ; 1920×1080 et 1366×768 ; vue réduite 1024×700 ; zoom 100 % et 125 % sur les flux critiques.
6. Les informations métier peuvent diverger (produits fictifs EUR/MAD), mais la structure visuelle, la densité et le placement doivent être manifestement fidèles. Expliquer tout écart résiduel avant de le déclarer accepté.

### 4. Bornes strictes de sécurité et d'architecture

- Aucun changement au SQL 0007 déjà appliqué, à ses empreintes, aux migrations 0001–0006, aux règles métier et aux données existantes.
- Privilégier exclusivement React, CSS et composants UX de Phase 05 ; adapter les contrats client si nécessaire **sans changer la sémantique API**. Toute modification métier/SQL nécessite un arrêt et une revue explicite.
- Ne pas toucher à `apps/desktop/.env.development` ni aux processus actifs du propriétaire.
- Aucun accès/modification aux vraies données médicales, à PostgreSQL 17 ni aux bases de production.
- Conserver idempotence, concurrence, audit, contrôles de permissions, prix HT, garanties de stock et import atomique. Rien ne doit être mocké dans les parcours de recette finale.
- Ne pas reprendre ni importer le code et les données de l'ancien MMV. Les coordonnées de tiers du HTML d'origine ne sont pas des données d'essai sûres : utiliser des données fictives clairement identifiées.
- Corriger les défauts visuels signalés sans casser les phases antérieures.

### 5. Tests et livraison

- Relancer TypeScript, lint, build, suite Vitest complète et non-régressions frontend ; tests backend ciblés seulement si le contrat client bouge.
- Tester sous Tauri natif les flux réels avec comptes et données fictifs : création/modification, prix/coefficient, fournisseur, stock, inventaire et import CSV. Tester les permissions, erreurs 409, refus stock négatif et l'absence de fuite des coûts.
- Produire l'archive d'audit : diff Git, captures source/native côte à côte, matrice G05-DESIGN, inventaire des adaptations métier et rapport de tests.
- Commit local autorisé. **Aucun push, aucune PR, aucun merge, aucune Phase 06.**
- Ne pas rouvrir les 20 décisions métier ni exiger de nouveau questionnaire. En cas d'écart purement visuel non bloquant, proposer une adaptation documentée, sans altérer les fonctionnalités. Regrouper les seuls arbitrages bloquants, s'il y en a.

**Statut attendu : `PHASE05_UX_LOT6_ALIGNED — OWNER_DESIGN_FINAL_REVIEW_REQUIRED`.**

**Critère final : nous devons reconnaître le Lot 6 dans chaque écran MMV V2, y compris là où de nouvelles règles (coefficient, variantes, magasins, inventaires) ont été intégrées proprement.**
