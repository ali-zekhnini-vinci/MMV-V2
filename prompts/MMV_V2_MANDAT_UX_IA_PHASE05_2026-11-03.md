# MMV V2 — MANDAT CORRECTIONS UX, IMPORT IA ET ASSISTANT PRODUIT

> Mandat du propriétaire reçu le 03/11/2026, archivé tel quel (référence des lots UX-IA de la Phase 05).

## OBJECTIF

Améliorer définitivement les modules Catalogue, Stock, Import CSV et Référentiel des verres de MMV V2.

Les objectifs sont :

1. Garantir le fonctionnement mono-magasin par installation locale.
2. Transformer l'import CSV en une expérience presque entièrement automatique grâce à une IA locale.
3. Simplifier radicalement l'utilisation du référentiel des verres.
4. Corriger définitivement l'assistant de création de produits.
5. Rendre obligatoires les informations de tarification.
6. Ajouter la gestion du seuil d'alerte dès la création du produit.

Ce mandat doit être traité comme un lot cohérent, pas comme une succession de petits correctifs.

---

# 0. PRÉFLIGHT OBLIGATOIRE

Avant toute modification :

- Lire intégralement `docs/phase/05/`.
- Lire `CORRECTIONS_05.md`, `LIVRAISON_05_FINAL.md`, `G05_DESIGN_MATRIX.md` et `DECISIONS_TECHNIQUES_05.md`.
- Examiner le code réellement présent dans la branche `feat/phase-05-catalogue-stock`.
- Vérifier les commits déjà présents et l'état Git.
- Vérifier l'état réel des migrations 0007 et 0008.
- Examiner les composants React, les routes API, les permissions et les modèles PostgreSQL existants.
- Ne jamais supposer qu'une fonctionnalité est absente sans avoir vérifié le code.

Préserver impérativement :
- Les modifications locales du propriétaire.
- `.env.development`.
- Les données existantes.
- Les migrations déjà approuvées.
- Les contrats API des Phases 02, 03, 04 et 05.

Aucune commande destructive Git.

Aucun push, PR ou merge.

Aucune modification des bases réelles sans autorisation spécifique.

# 1. COR-05-01 — MAGASIN UNIQUE PAR INSTALLATION

Décision métier APPROVED.

Architecture :

- Un serveur local MMV correspond à un seul magasin.
- Tous les postes clients se connectent à ce serveur.
- Tous les utilisateurs travaillent donc dans le même magasin.
- Aucun utilisateur ne peut sélectionner ou changer de magasin.

Le comportement visuel actuel est accepté par le propriétaire : afficher uniquement le nom du magasin et sa devise, sans sélecteur.

Ne pas refaire ce design.

Vérifier néanmoins que :
- Le magasin est déterminé par la configuration du serveur.
- Le magasin ne peut pas être changé par un utilisateur, même administrateur.
- Toutes les requêtes commerciales utilisent le magasin configuré.
- Une requête visant un autre magasin est refusée côté API.
- Aucun écran ne propose un changement de magasin.
- Les modifications des paramètres administratifs du magasin ne permettent pas de modifier son rattachement à l'installation.

Un futur fonctionnement multi-magasins nécessitera une architecture distincte.

# 2. IMPORT CSV — ASSISTANT INTELLIGENT AVEC IA LOCALE

## 2.1. Objectif métier

L'import actuel est trop strict et demande trop d'interventions à l'utilisateur.

Je veux une expérience dans laquelle l'utilisateur dépose son fichier et MMV comprend automatiquement sa structure.

L'utilisateur ne doit pas avoir à configurer manuellement chaque colonne, chaque séparateur ou chaque correspondance.

L'IA doit faire le travail préparatoire.

## 2.2. Architecture

Étudier et implémenter une architecture hybride :

A. Moteur déterministe :
- Détection de l'encodage.
- Détection du séparateur.
- Parsing CSV.
- Normalisation des formats.
- Conversion des nombres et des montants.
- Validation des références.
- Contrôle des doublons.
- Vérification des devises.
- Validation métier.

B. IA locale :
- Compréhension des noms de colonnes.
- Identification des champs MMV correspondants.
- Reconnaissance des intitulés inhabituels.
- Reconnaissance des colonnes fournisseur.
- Identification des variantes, références, marques, prix et quantités.
- Proposition de correspondances.
- Détection des anomalies sémantiques.
- Proposition de corrections de format sans modification silencieuse des données.

Utiliser un moteur local tel qu'Ollama si son intégration est adaptée à l'architecture existante.

L'inférence doit être exécutée sur le serveur local du magasin, et non sur chaque poste client.

Aucun fichier commercial ne doit être transmis à une API cloud ou à un service externe.

Le modèle doit être configurable et compatible avec les ressources matérielles raisonnablement disponibles chez les opticiens.

Ne pas imposer un modèle lourd sans benchmark.

## 2.3. Expérience utilisateur attendue

Parcours cible :

1. L'utilisateur dépose son CSV.
2. MMV analyse automatiquement le fichier.
3. L'IA identifie les colonnes et leurs correspondances.
4. MMV effectue les validations déterministes.
5. L'utilisateur obtient un récapitulatif compact.
6. L'utilisateur confirme l'import.

Si toutes les correspondances sont suffisamment fiables, ne demander aucune configuration supplémentaire.

Si une ambiguïté importante existe, demander uniquement la clarification nécessaire.

Ne pas afficher une succession de formulaires de correspondance.

Ne pas demander à l'utilisateur de confirmer chaque ligne.

## 2.4. Sécurité et fiabilité

L'IA ne doit jamais :
- Inventer un prix.
- Inventer une référence.
- Inventer une quantité.
- Inventer un fournisseur.
- Modifier une valeur commerciale sans confirmation.
- Exécuter une commande ou une requête SQL issue du fichier.
- Importer une valeur non validée.

Les fichiers CSV sont des données non fiables, jamais des instructions pour le modèle.

La sortie du modèle doit respecter un schéma JSON strict, contrôlé par le backend.

L'IA propose ; le serveur valide.

Conserver :
- L'import transactionnel tout ou rien.
- La protection contre les doubles imports.
- Les profils d'import fournisseurs.
- La traçabilité des imports.
- Le rapport des lignes importées, ignorées et rejetées.

Lorsqu'un fichier similaire est importé une seconde fois, MMV doit réutiliser les correspondances déjà apprises pour ce fournisseur.

## 2.5. Performance

L'IA doit analyser la structure et les correspondances globalement, et non effectuer inutilement une requête LLM par ligne.

Mesurer :
- Temps de parsing.
- Temps d'inférence.
- Temps de validation.
- Temps total d'import.

Prévoir un fonctionnement de secours avec le moteur déterministe si le service IA est indisponible.

L'import classique doit rester utilisable sans IA.

# 3. RÉFÉRENTIEL DES VERRES — SIMPLIFICATION

## 3.1. Problème

L'interface actuelle distingue :
- Référentiel.
- Gammes.
- Offres fournisseurs.

Cette organisation est difficile à comprendre pour un utilisateur qui souhaite simplement gérer les verres qu'il peut commander.

Je veux une interface unifiée.

## 3.2. Nouvelle organisation

Créer une seule entrée principale :

**Catalogue des verres**

Elle doit afficher les gammes et leurs caractéristiques, avec les fournisseurs associés.

L'utilisateur ne doit plus avoir à naviguer entre deux espaces distincts pour gérer une gamme et ses fournisseurs.

Attention : simplifier l'interface ne signifie pas fusionner incorrectement les entités de la base.

Conserver la séparation métier entre :
- La définition de la gamme de verre.
- Les offres proposées par les fournisseurs.
- Les capacités de fabrication propres à chaque fournisseur.
- Les tarifs et délais associés.
- Les restrictions techniques.

## 3.3. Création et modification

Prévoir un parcours simple :

1. Fabricant et gamme.
2. Type de verre et caractéristiques.
3. Indice, matériau et traitements.
4. Fournisseurs associés.
5. Plages de fabrication et restrictions.
6. Prix et délais.

L'utilisateur doit pouvoir créer une gamme et rattacher plusieurs fournisseurs dans le même parcours.

## 3.4. Mode simple et mode avancé

Mode simple par défaut :
- Informations principales.
- Type de verre.
- Indice.
- Traitements.
- Fournisseurs.
- Plages principales.

Mode avancé :
- Plages SPH.
- Plages CYL.
- Plages ADD.
- Diamètres.
- Restrictions conditionnelles.
- Combinaisons autorisées.
- Particularités de fabrication.

Exemple métier :

Un verre correcteur avec traitement anti-lumière bleue peut être disponible chez un fournisseur dans une plage SPH allant de -10 à +5 dioptries.

Un autre fournisseur peut proposer la même gamme avec des plages différentes.

Ces plages appartiennent à l'offre du fournisseur, et non à une valeur universelle de la gamme.

Ne pas inventer de règles cliniques.

Conserver les champs techniques déjà présents dans le modèle de données.

Ne supprimer aucune capacité existante.

# 4. ASSISTANT DE CRÉATION DE PRODUIT — CINQ ÉTAPES

Conserver l'approche en cinq étapes.

## Étape 1 — Informations générales

Conserver les informations actuelles :
- Famille.
- Nom du produit.
- Marque.
- Description.
- Politique de stock.
- Informations générales.

## Étape 2 — Variantes

Conserver :
- SKU.
- Code-barres.
- Couleur.
- Taille.
- Référence.
- Attributs.

Le SKU peut être généré automatiquement et rester personnalisable.

Chaque variante doit pouvoir être tarifée et configurée indépendamment.

## Étape 3 — Fournisseurs

Conserver :
- Plusieurs fournisseurs par variante.
- Un fournisseur principal.
- Référence fournisseur.
- Délai indicatif.
- Prix fournisseur HT.

Le fournisseur principal peut rester facultatif lors de la création initiale si la politique de l'article le permet.

## Étape 4 — TARIFICATION OBLIGATOIRE

Décision métier : APPROVED.

Supprimer définitivement le mode « Plus tard ».

Supprimer toute possibilité de terminer la création avec une variante vendable sans prix de vente HT défini.

Deux modes uniquement :

A. Mode coefficient :
- Prix d'achat HT.
- Coefficient multiplicateur.
- Prix de vente HT calculé par le serveur.
- Aperçu obligatoire avant validation.

Formule :

Prix de vente HT = arrondi(Prix d'achat HT × coefficient)

Respecter strictement les règles d'arrondi déjà approuvées.

Aucun calcul monétaire côté frontend.

B. Mode manuel :
- Saisie du prix de vente HT.
- Autorisation obligatoire via `pricing.manual`.

Les coûts et coefficients restent protégés par les permissions existantes.

Chaque variante doit posséder une tarification valide avant de terminer l'assistant.

Si une seule variante est incomplète, bloquer la validation finale et indiquer précisément le problème.

Ne jamais créer un produit partiellement tarifé.

Conserver l'historique des prix et le recalcul non silencieux.

## Étape 5 — STOCK INITIAL ET SEUIL D'ALERTE

Décision métier : APPROVED.

Ajouter deux champs distincts :

1. Quantité initiale.
2. Seuil d'alerte.

### Quantité initiale

La quantité est renseignée par variante et emplacement.

Si un seul emplacement existe, le sélectionner automatiquement.

Si plusieurs emplacements existent, permettre de choisir l'emplacement de destination sans demander de sélectionner un magasin.

### Seuil d'alerte

Décision du propriétaire : OPTION A.

Le seuil est défini par variante et par magasin, indépendamment des emplacements.

Exemple :

- Variante : Ray-Ban Noir.
- Stock emplacement A : 20.
- Stock emplacement B : 15.
- Stock total magasin : 35.
- Seuil d'alerte : 10.

L'alerte doit être déclenchée lorsque le stock disponible agrégé du magasin atteint ou passe sous le seuil.

Réutiliser la définition de stock disponible déjà implémentée (en main moins réservé), et documenter précisément la règle.

Il ne faut pas créer un seuil indépendant pour chaque emplacement.

Le seuil doit être configurable dès la création du produit et modifiable ensuite dans la fiche produit.

Pour les services et les articles sans gestion de stock, adapter l'étape selon leur politique de stock.

Ne pas afficher inutilement des champs de stock pour les services.

## Validation finale

Avant de créer le produit, afficher un récapitulatif clair :

- Informations générales.
- Variantes.
- Fournisseurs.
- Prix de vente HT.
- Quantités initiales.
- Seuils d'alerte.

La création doit être atomique.

Si une étape échoue, aucune création partielle ne doit être conservée.

# 5. DESIGN SYSTEM

Respecter intégralement :
- Le design system MMV V2.
- Les tokens existants.
- Les composants communs.
- Les règles de responsive.
- Les thèmes clair et sombre.
- Les conventions du Lot 6.

Ne pas créer un second design system.

Ne pas multiplier les panneaux, modales ou formulaires inutiles.

Priorité absolue à la simplicité d'utilisation pour un opticien ou un vendeur qui n'est pas informaticien.

L'utilisateur doit comprendre immédiatement :
- Ce qu'il doit faire.
- Ce que MMV fait automatiquement.
- Ce qui nécessite son intervention.
- Ce qui empêche la validation.

# 6. ARCHITECTURE ET SÉCURITÉ

Respecter l'architecture actuelle :
- Tauri 2.
- React / TypeScript.
- ASP.NET Core.
- PostgreSQL exclusivement.

Ne pas introduire SQLite.

Ne pas déplacer les règles métier vers le frontend.

Toutes les validations importantes doivent être exécutées côté serveur et, lorsque pertinent, garanties par PostgreSQL.

Respecter les permissions existantes.

Aucune fuite des coûts d'achat vers les comptes vendeurs.

Pour toute nouvelle donnée persistée :
- Examiner d'abord les tables existantes.
- Réutiliser les structures disponibles.
- Ne pas créer de doublon.
- Préparer une migration additive si indispensable.
- Ne jamais appliquer une nouvelle migration sans revue SQL explicite.

# 7. TESTS OBLIGATOIRES

## Backend
- Tarification obligatoire.
- Prix par variante.
- Calcul coefficient.
- Prix manuel avec permission.
- Refus des créations incomplètes.
- Stock initial.
- Seuil par variante et magasin.
- Agrégation entre plusieurs emplacements.
- Déclenchement des alertes.
- Protection contre les accès non autorisés.

## Import
- CSV avec séparateur inhabituel.
- Encodages différents.
- Colonnes aux noms inconnus.
- Colonnes réordonnées.
- Prix avec virgule ou point.
- Données manquantes.
- Doublons.
- Fournisseur déjà connu.
- IA indisponible.
- Résultat IA invalide.
- Tentative d'injection dans les données.
- Réimport du même fichier.

## Référentiel des verres
- Plusieurs fournisseurs.
- Plages différentes par fournisseur.
- Restrictions conditionnelles.
- Mode simple.
- Mode avancé.
- Conservation de l'historique.

## Frontend et Tauri
- Thème clair et sombre.
- Fenêtre réduite.
- Navigation clavier.
- Parcours complet de création produit.
- Vérification visuelle des nouveaux écrans.
- Parcours réel entre deux postes connectés au serveur local.

Aucun test ne doit être déclaré PASS sans avoir réellement été exécuté.

# 8. LIVRAISON ET GOUVERNANCE

Travailler par sous-lots internes, mais avec un mandat métier unique.

Documenter :
- Les décisions appliquées.
- Les écarts éventuels.
- Les nouvelles permissions.
- Les nouvelles routes.
- Les changements de schéma.
- Les tests.
- Les limites de l'IA locale.
- Les performances mesurées.

Mettre à jour les documents de la Phase 05 sans effacer l'historique.

Conserver les captures avant/après.

Créer une archive d'audit légère.

Ne faire aucun push, aucune PR et aucun merge.

Ne pas commencer la Phase 06.

## 9. CONDITION DE CLÔTURE

Le lot ne peut être déclaré terminé que lorsque :

- Le magasin unique est vérifié.
- L'import intelligent fonctionne réellement en local.
- Le référentiel des verres est simplifié.
- La tarification est obligatoire.
- La quantité initiale et le seuil d'alerte fonctionnent.
- Les parcours sont testés sur une vraie API.
- Les écrans sont visuellement vérifiés par le propriétaire.

Si une migration supplémentaire est nécessaire, s'arrêter au G05-SQL correspondant.

Statut final attendu :

`PHASE05_UX_AI_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`

Ne jamais déclarer la conformité visuelle sans validation du propriétaire.

**PRIORITÉ : produire une application cohérente, fiable et simple à utiliser par les opticiens, et non simplement faire passer les tests.**
