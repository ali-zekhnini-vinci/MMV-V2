# MMV V2 — GO FINAL PHASE 04-A
## Simplification de l'enregistrement et clôture du lot frontend

### 1. CONTEXTE ET DÉCISION DU PROPRIÉTAIRE

Le propriétaire a examiné les formulaires optiques et approuvé les orientations suivantes :

- Le design officiel du Lot 3 reste la référence UX.
- Les boutons +/− sont conservés avec saisie clavier complémentaire.
- Les ordonnances médicales et les mesures magasin restent deux types distincts.
- Le champ « État de l'œil » est définitivement supprimé.
- La gestion des pièces sources est reportée au backlog.
- Les révisions et l'historique des modifications sont conservés.

**Nouvelle décision UX approuvée : remplacer les deux actions principales actuelles par une seule action « Enregistrer », accompagnée d'une option discrète « Enregistrer comme brouillon ».**

Je veux désormais finaliser et clôturer la Phase 04-A.

Il ne faut pas relancer une nouvelle étude générale, recréer les formulaires ou multiplier les demandes de validation.

### 2. COMPORTEMENT ATTENDU DU FORMULAIRE

#### Action principale : Enregistrer

Sur les formulaires « Nouvelle ordonnance médicale » et « Nouvelle mesure magasin », afficher un bouton principal bleu :

**Enregistrer**

Ce bouton constitue l'action naturelle de l'opticien.

Dans le prototype, il doit :

1. Contrôler les informations nécessaires à l'opération de confirmation.
2. Enregistrer les modifications.
3. Confirmer l'enregistrement si les conditions applicables sont satisfaites.
4. Afficher une confirmation claire.
5. Revenir au détail de l'enregistrement ou à la fiche client selon le parcours existant.

Le comportement doit également s'appliquer aux rectifications.

Lorsqu'un enregistrement confirmé est modifié, la dernière révision confirmée reste préservée jusqu'à la confirmation d'une nouvelle version.

**Ne jamais inventer de critères cliniques définitifs.**

D-04-02 et D-04-06 ne sont toujours pas approuvées.

Pour le banc uniquement, utiliser une règle minimale de démonstration, clairement identifiée comme telle : un enregistrement dont toutes les données optiques sont absentes ne peut pas être confirmé.

Ce contrôle ne représente pas une validation médicale. Les autres règles de complétude restent ouvertes pour 04-B.

Ne pas imposer une valeur aux deux yeux. Une valeur absente reste différente de zéro.

Si la confirmation est refusée, expliquer pourquoi et proposer l'enregistrement en brouillon. Ne pas transformer silencieusement l'action « Enregistrer » en « Enregistrer comme brouillon ».

#### Action secondaire : Enregistrer comme brouillon

Conserver cette possibilité, mais lui donner une importance visuelle inférieure.

Elle peut être présentée :

- Par un bouton secondaire discret.
- Ou dans un menu adjacent au bouton principal.

Choisir l'option la plus cohérente avec le Design System et la maquette Lot 3.

L'action doit rester visible ou immédiatement découvrable, accessible au clavier, et utilisable sans parcours compliqué.

Elle permet d'enregistrer des informations incomplètes sans confirmer l'ordonnance ou la mesure.

Un brouillon doit pouvoir être rouvert, modifié, confirmé ou abandonné selon le cycle existant.

### 3. SIMPLIFIER LES LIBELLÉS

L'interface doit être compréhensible par un opticien sans connaissance du système technique des révisions.

Privilégier :

- « Enregistrer »
- « Enregistrer comme brouillon »
- « Modifier »
- « Historique des modifications »
- « Dernière révision confirmée »
- « Annuler »

Conserver les notions techniques de révision dans le modèle interne.

Éviter de surcharger le formulaire avec des explications permanentes concernant les brouillons, versions ou révisions.

Ne jamais utiliser « révision en vigueur » pour suggérer une validité médicale ou réglementaire.

### 4. GÉRER CORRECTEMENT LES ÉCHECS PARTIELS

Corriger ou vérifier explicitement le problème précédemment identifié dans `OpticalRecordForm`.

Cas important :

1. Le brouillon est enregistré avec succès.
2. La confirmation échoue ensuite.

Dans ce cas :

- MMV doit conserver l'état réellement enregistré.
- Synchroniser l'état du formulaire avec le brouillon sauvegardé.
- Ne pas afficher de faux avertissement de perte de saisie.
- Ne pas annoncer que rien n'a été enregistré.
- Afficher un message clair : « Brouillon enregistré — confirmation refusée ».
- Préserver la dernière révision confirmée.
- Permettre de reprendre la modification sans doublon.

Tester également les refus 409 et 422, les erreurs de communication simulées et les doubles clics sur « Enregistrer ».

L'action `Ctrl+S` doit correspondre à l'action principale « Enregistrer ». Aucun comportement ambigu entre enregistrement et brouillon.

### 5. CONSERVER LA SIMPLIFICATION D-04-16

Ne pas réintroduire :

- Le champ « État de l'œil ».
- Les panneaux de pièces sources.
- Le téléchargement de PDF ou de photos.
- Les permissions `optical.documents.*`.
- Les tables ou API de gestion documentaire.

Conserver la distinction entre valeurs absentes et valeurs nulles.

Conserver les métadonnées de provenance prévues pour chaque type.

Les révisions restent obligatoires dans la conception, mais leur complexité doit rester aussi discrète que possible dans l'utilisation quotidienne.

### 6. FONCTIONNEMENT DES CONTRÔLES ±

Ne pas modifier le design approuvé.

Vérifier une dernière fois :

- Sphère.
- Cylindre.
- Addition.
- Prisme.
- Axe.
- Base.
- Acuité visuelle.
- Saisie manuelle.
- Navigation clavier.
- Boutons +/−.
- Valeurs négatives, positives, nulles et absentes.

Le pas de 0,25 reste un réglage de démonstration paramétrable, non une règle clinique approuvée.

Conserver le comportement sans arrondi automatique ni normalisation silencieuse.

### 7. CRITÈRES DE CLÔTURE DE 04-A

Je souhaite terminer cette phase, pas lancer un nouveau cycle de préparation.

Effectuer une vérification finale des parcours suivants :

1. Ouvrir la fiche d'un client fictif.
2. Créer une ordonnance médicale.
3. Utiliser les boutons +/− sur OD et OG.
4. Enregistrer et retrouver l'ordonnance.
5. Modifier les valeurs et retrouver l'ancienne révision.
6. Enregistrer une ordonnance incomplète comme brouillon.
7. Reprendre puis confirmer ce brouillon.
8. Vérifier le traitement des erreurs et conflits.
9. Créer une mesure magasin avec le même design.
10. Vérifier que la mesure conserve sa provenance distincte.
11. Vérifier l'absence des champs « État de l'œil » et « Pièces sources ».
12. Vérifier les listes, la recherche, les détails, les permissions simulées et l'invalidation.
13. Vérifier les retours vers la fiche client et les avertissements de sortie.
14. Vérifier que « Préparer une vente » reste désactivé.

Tester les thèmes clair et sombre, les zooms 100 %, 125 % et 150 %, ainsi que les fenêtres 1920×1080 et 1024×700.

Vérifier la navigation clavier, l'ordre de tabulation, les libellés accessibles et l'absence de défilement horizontal non prévu.

### 8. TESTS TECHNIQUES

Exécuter :

- TypeScript strict.
- ESLint.
- Vitest, suite frontend complète.
- Build frontend.
- Vérification de l'absence du banc dans le bundle de production.
- Tests de non-régression des composants communs.
- Contrôle du bon isolement du prototype.

Les tests .NET et PostgreSQL peuvent rester NOT_RERUN si aucun code serveur ou SQL n'a changé.

Ne pas déclarer les tests Tauri natifs réussis s'ils n'ont pas été exécutés.

Les éventuelles anomalies bloquantes doivent être corrigées, puis les tests concernés rejoués.

Ne pas masquer les défauts en désactivant des tests.

### 9. LIMITES ABSOLUES

La Phase 04-A reste un lot frontend exécuté dans un banc de démonstration isolé.

Il est interdit de :

- Commencer la Phase 04-B.
- Générer ou appliquer la migration PostgreSQL 0006.
- Ajouter les API optiques réelles.
- Introduire des données de santé réelles.
- Modifier les comptes du propriétaire ou leurs permissions.
- Activer le module optique dans l'application réelle.
- Reprendre du code de l'ancien MMV.
- Toucher aux fichiers non suivis du propriétaire.
- Interrompre les processus existants sans nécessité et autorisation.

Le futur module backend devra être développé seulement après validation du modèle métier et revue du SQL.

### 10. DOCUMENTATION ET DÉCISIONS

Enregistrer cette nouvelle décision propriétaire comme APPROVED sous le prochain identifiant D-04 libre :

**« Enregistrement optique simplifié : action principale Enregistrer, option secondaire discrète Enregistrer comme brouillon, sans suppression du cycle de révisions. »**

Vérifier les identifiants déjà présents avant toute création.

Conserver :

- D-04-01 APPROVED.
- D-04-15 APPROVED.
- D-04-16 APPROVED.
- Les décisions métier D-04-02 à D-04-14 encore applicables en PROPOSED.
- Les sujets relatifs aux pièces sources dans BL-04-01.

Ne pas créer de nouvelles décisions pour de simples détails réversibles d'interface.

Synchroniser le DOCUMENTATION GATE :

- README.md.
- docs/phase/04.md.
- LIVRAISON_04A.md.
- IMPLEMENTATION_SPEC.
- QA_MATRIX.
- SOURCE_MAP.
- STATE_COVERAGE_04A.
- GATES_AND_DECISIONS.
- ADR et backlog si nécessaire.

Préserver les entrées historiques.

### 11. CLÔTURE TECHNIQUE

Lorsque les corrections et tests sont terminés :

1. Vérifier que les critères de clôture de 04-A sont satisfaits.
2. Produire les captures finales des deux formulaires.
3. Confirmer que l'application réelle est inchangée.
4. Vérifier les changements Git et l'absence de secrets.
5. Effectuer le ou les commits locaux nécessaires.
6. Générer une archive ZIP légère et intègre pour mon audit final.
7. Fournir un rapport synthétique indiquant les tests effectués, leurs résultats, les correctifs et les éventuelles réserves.

**Si tous les critères techniques passent, déclarer :**

`PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`

Cette déclaration clôture techniquement le lot frontend 04-A, mais ne constitue pas un GO pour 04-B.

En cas de défaut réellement bloquant, conserver un statut explicite et corriger ce défaut avant de proposer la clôture.

Ne pas prolonger artificiellement 04-A pour des règles cliniques ou réglementaires qui relèvent du cadrage de 04-B.

### 12. GIT ET LIVRAISON

Rester sur la branche actuelle `feat/phase-04-00-cadrage`.

Commits locaux autorisés.

**Aucun push, aucune PR, aucun merge sans mon autorisation.**

Ne commence pas la Phase 04-B.

À la fin, donne-moi :
- Le SHA du dernier commit.
- L'archive ZIP et son SHA-256 complet.
- Le nombre de tests réellement réussis.
- Les captures finales.
- Les éventuels écarts bloquants.
- Les décisions métier restantes pour préparer 04-B.

## OBJECTIF FINAL

Je veux un module optique visuellement fidèle au Lot 3, rapide et simple à utiliser.

L'opticien doit pouvoir saisir ses valeurs, cliquer sur **Enregistrer**, et poursuivre son travail.

Le brouillon reste une possibilité secondaire, sans compliquer l'utilisation normale.

Les révisions restent fiables et traçables, mais ne doivent pas devenir une contrainte d'ergonomie.

**C'est le dernier mandat de développement de la Phase 04-A. Exécute-le, vérifie le résultat et prépare sa clôture technique.**
