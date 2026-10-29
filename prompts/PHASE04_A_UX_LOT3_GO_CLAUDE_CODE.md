# MMV V2 — PHASE 04-A — GO REFONTE UX SELON LA MAQUETTE OFFICIELLE DU LOT 3

## 1. CONTEXTE ET DÉCISION DU PROPRIÉTAIRE

J'ai personnellement testé le banc de démonstration de la Phase 04-A.

La structure fonctionnelle du prototype est satisfaisante, mais **je ne valide pas l'ergonomie actuelle des formulaires de saisie optique**.

Je souhaite conserver comme référence principale le design que j'avais précédemment conçu dans Claude Design.

**DÉCISION UX OFFICIELLE : la maquette HTML du Lot 3 — Ordonnances devient la référence visuelle et ergonomique des formulaires de saisie de la Phase 04.**

Le fichier HTML se trouve dans les références locales du projet, sous `reference/phase04/lot3/` ou `references/phase04/lot3/` selon le chemin réellement présent.

Localise le fichier `.html` du Lot 3, vérifie son identité avec le registre documentaire, puis analyse-le directement.

Tu possèdes déjà cette référence : ne me demande pas de la recréer ou de te la retransmettre.

**Important :** le HTML constitue une référence de design et d'interaction, pas une autorisation d'importer d'anciennes règles métier ou de réutiliser l'ancien code MMV.

---

## 2. OBJECTIF

Adapter les formulaires existants de 04-A pour retrouver une expérience aussi proche que possible de la maquette officielle, particulièrement l'écran « Nouvelle ordonnance ».

Je veux que la saisie optique soit :

- Rapide pour un opticien travaillant au comptoir.
- Intuitive et utilisable principalement à la souris, sans exclure le clavier.
- Visuellement claire, avec une comparaison immédiate entre OD et OG.
- Fidèle au Design System MMV.
- Cohérente en thèmes clair et sombre.
- Robuste lorsque la fenêtre Tauri est réduite.
- Accessible au clavier et compatible avec les principaux niveaux de zoom.

Ne réalise pas une nouvelle interprétation esthétique du formulaire.

**Reprends prioritairement les proportions, dispositions, composants, interactions et hiérarchies visuelles de mon HTML.**

Les adaptations ne sont autorisées que lorsqu'elles sont nécessaires à l'accessibilité, au responsive, à la cohérence avec les composants V2 ou à une décision métier déjà approuvée.

---

## 3. STRUCTURE VISUELLE OBLIGATOIRE

### A. En-tête du formulaire

Reproduire l'organisation de la maquette :

- Titre « Nouvelle ordonnance médicale » ou « Nouvelle mesure magasin ».
- Indication du client déjà sélectionné.
- Navigation pour revenir à la fiche client.
- Actions Annuler / Enregistrer clairement visibles.
- Respect du Shell et des dimensions du Design System existant.

### B. Informations générales

Conserver la présentation en carte structurée.

Pour l'ordonnance médicale :

- Client concerné.
- Date de prescription.
- Prescripteur.
- Référence de l'enregistrement.
- Autres informations déjà prévues dans le cadrage, sans inventer d'obligations.

Pour les mesures magasin :

- Même qualité de présentation.
- Client concerné.
- Date des mesures.
- Auteur ou professionnel ayant réalisé les mesures, selon le modèle métier futur.
- Provenance propre aux mesures magasin.

**Ne jamais présenter une mesure magasin comme une prescription délivrée par un médecin.**

Le design peut être partagé, mais les deux objets métier doivent rester distincts conformément à D-04-01.

### C. Correction optique

C'est la priorité principale.

Reprendre le bloc central de la maquette :

**Deux cartes OD et OG côte à côte**, avec leurs intitulés respectifs :

- Œil droit — OD.
- Œil gauche — OG.

Chaque carte doit conserver une grille de saisie claire avec les champs prévus dans le cadrage :

- Sphère.
- Cylindre.
- Axe.
- Addition.
- Prisme.
- Base.
- Acuité visuelle.

La disposition doit rester très proche de la maquette HTML, sans remplacer cette présentation par une accumulation de champs génériques.

---

## 4. BOUTONS + / − : EXIGENCE PRIORITAIRE

Je souhaite retrouver les contrôles rapides présents dans mon design.

Pour les champs compatibles :

**Sphère, cylindre, addition et prisme :**

- Bouton de diminution à gauche.
- Valeur au centre.
- Bouton d'augmentation à droite.
- Mise à jour immédiatement visible après le clic.
- Possibilité de maintenir une saisie manuelle au clavier.
- Possibilité d'effacer la valeur.
- Navigation clavier correcte.
- Libellés accessibles aux lecteurs d'écran.
- Fonctionnement indépendant pour OD et OG.

L'intérêt de ces boutons est de permettre à l'opticien de modifier rapidement une correction sans être obligé de sélectionner le champ puis de saisir chaque valeur.

### Très important : ne pas inventer de règles cliniques

Le HTML montre des boutons utilisant notamment un pas de `0,25`.

Ce pas n'est **pas encore validé comme règle métier générale** : D-04-06 reste ouverte.

Dans le prototype uniquement, tu peux reproduire ce pas à titre de démonstration visuelle et fonctionnelle, à condition de le rendre paramétrable et de ne pas l'intégrer comme contrainte métier définitive.

Respecter les distinctions suivantes :

- Une valeur absente n'est pas un zéro.
- Un clic sur + ou − depuis un champ vide constitue une nouvelle saisie explicite ; documenter le comportement retenu dans le prototype.
- Ne jamais modifier silencieusement une valeur saisie manuellement.
- Ne pas arrondir ni forcer une valeur au pas de démonstration.
- Ne pas transformer automatiquement un cylindre positif en cylindre négatif, ou inversement.
- Ne pas normaliser automatiquement l'axe.
- Aucune borne clinique définitive sans validation métier.

Les boutons doivent accélérer la saisie, pas la rendre moins fiable.

### Autres champs

**Axe :** contrôle numérique adapté à la maquette ; pas d'incrément imposé sans validation.

**Base :** sélecteur adapté à la nature de la donnée ; choix définitifs encore soumis au cadrage.

**Acuité visuelle :** conserver une présentation simple, sans imposer de format clinique non approuvé.

---

## 5. COMPOSANTS REACT RÉUTILISABLES

Ne copie pas le HTML comme une page autonome et ne l'intègre pas dans une iframe.

Reconstitue le design avec notre stack actuelle :

- React 19 et TypeScript strict.
- Tailwind CSS 4 et tokens MMV.
- Composants shadcn/ui / Radix déjà adaptés.
- Icônes Lucide.
- Composants existants du projet lorsque c'est pertinent.

Je veux notamment un composant réutilisable de saisie optique avec boutons +/−, dont le pas de démonstration est une propriété configurable.

Les deux formulaires, ordonnance médicale et mesure magasin, doivent pouvoir réutiliser les mêmes composants OD/OG sans fusionner leurs règles et métadonnées métier.

Évite les abstractions excessives et les bibliothèques supplémentaires si elles ne sont pas nécessaires.

---

## 6. CONSERVER LES ACQUIS DE LA PHASE 04-A

La refonte porte essentiellement sur l'ergonomie de saisie.

Ne supprime pas les fonctionnalités déjà implémentées dans le banc :

- Choix explicite entre ordonnance médicale et mesure magasin.
- Dossier optique depuis la fiche client.
- Liste et recherche.
- Détails et historique des révisions.
- Comparaison de deux révisions.
- Coexistence d'une dernière révision confirmée et d'un brouillon de rectification.
- Invalidation sans suppression physique.
- Simulation des permissions.
- Pièces synthétiques.
- Gestion des conflits entre postes.
- Navigation et protection contre la perte de saisie.
- Bouton « Préparer une vente » inactif.

Les décisions D-04-02 à D-04-14 restent proposées.

Aucune fonction encore fictive ne doit être présentée comme opérationnelle.

---

## 7. PÉRIMÈTRE ET ISOLATION

Nous sommes toujours dans la Phase 04-A.

**Ne commence pas la Phase 04-B.**

Contraintes absolues :

- Aucune migration PostgreSQL 0006.
- Aucun changement de schéma.
- Aucun nouveau service API optique.
- Aucune donnée médicale réelle.
- Aucun accès aux bases PostgreSQL pour ces démonstrations.
- Aucun nouveau droit attribué à un compte réel.
- Aucune activation des écrans optiques dans l'application de production.

Conserver la décision O-04-16 : le logiciel réel continue d'afficher « Ordonnances — phase ultérieure ».

Adapter les composants React du prototype, dans le banc isolé déjà créé.

Le banc reste clairement identifié comme un environnement de démonstration avec données fictives.

Ne consulte ni ne réutilise le code de l'ancien MMV.

---

## 8. TESTS OBLIGATOIRES

Tester notamment :

1. Modification de chaque champ OD et OG par boutons +/−.
2. Alternance entre saisie manuelle et clics.
3. Valeurs positives, négatives, nulles et absentes.
4. Préservation des valeurs saisies manuellement, même lorsqu'elles ne correspondent pas au pas de démonstration.
5. Indépendance des deux yeux.
6. Conservation des valeurs lors des navigations, selon les règles existantes du prototype.
7. Réinitialisation explicite d'un champ.
8. Navigation Tab, Shift+Tab et commandes clavier.
9. Mise à jour correcte des informations affichées.
10. Absence de régression dans les autres écrans optiques.

Exécuter :

- TypeScript strict.
- Lint.
- Vitest.
- Build frontend.
- Tests de non-intégration du banc dans l'application de production.
- Captures en thèmes clair et sombre, aux résolutions 1920×1080 et 1024×700 et aux zooms 100 %, 125 % et 150 %.

Ne pas présenter les tests Tauri natifs ou PostgreSQL comme réussis s'ils ne sont pas exécutés.

---

## 9. VALIDATION VISUELLE OBLIGATOIRE

Produire des captures comparatives entre :

**A. Maquette HTML officielle du Lot 3**

**B. Nouveau formulaire React dans le banc Phase 04-A**

Pour les deux types :

- Nouvelle ordonnance médicale.
- Nouvelle mesure magasin.

Les captures doivent permettre de comparer directement :

- Hiérarchie du formulaire.
- Dimensions et espacement des blocs.
- Présentation OD/OG.
- Boutons +/−.
- Typographie et couleurs.
- Lisibilité.
- Comportement en fenêtre réduite.

Si un élément du HTML n'est pas repris, documente précisément pourquoi.

Je veux une fidélité élevée à la référence, mais pas au prix d'un défaut fonctionnel ou d'accessibilité.

---

## 10. DOCUMENTATION GATE

Synchroniser les documents concernés :

- README.md.
- docs/phase/04.md.
- IMPLEMENTATION_SPEC.
- STATE_COVERAGE_04A.
- QA_MATRIX.
- SOURCE_MAP.
- GATES_AND_DECISIONS.
- LIVRAISON_04A.
- Registre des observations, si nécessaire.

Enregistrer une nouvelle décision :

**D-04-15 — Référence UX officielle des formulaires optiques : HTML Claude Design Lot 3, avec contrôles rapides +/− et saisie clavier complémentaire.**

Statut : **APPROVED — décision du propriétaire**.

Bien distinguer cette décision UX des règles cliniques encore proposées, notamment D-04-06.

Vérifier les identifiants de décisions déjà utilisés avant d'enregistrer D-04-15. En cas de conflit, utiliser le prochain identifiant libre et expliquer la correspondance.

Préserver l'historique documentaire.

---

## 11. LIVRAISON ATTENDUE

Travailler de manière autonome sur cette correction ciblée.

Ne pas multiplier les demandes d'autorisation concernant des détails visuels ou techniques réversibles.

À la fin, fournir :

1. Un résumé précis des composants modifiés ou créés.
2. Les écarts éventuels avec le HTML de référence.
3. Les résultats réels des tests.
4. Des captures de comparaison avant/après.
5. Le lien local et les instructions de lancement du banc.
6. Les commits locaux.
7. Une archive ZIP légère pour audit indépendant.
8. La liste des points encore dépendants d'une validation métier.

**Aucun push GitHub, aucune PR, aucun merge.**

Ne pas toucher aux fichiers non suivis du propriétaire.

Ne pas interrompre ses processus de développement existants.

### STATUT DE LIVRAISON ATTENDU

`PHASE04_A_UX_LOT3_READY — OWNER_VISUAL_REVIEW_REQUIRED`

L'objectif est simple : **retrouver mon design d'origine et améliorer la rapidité réelle de saisie optique, tout en conservant le socle technique MMV V2 et les protections déjà construites.**
