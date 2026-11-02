# MMV V2 — MANDAT MAÎTRE DE CORRECTION PHASE 05 (2026-11-02)

> Copie fidèle du mandat transmis par le propriétaire le 02/11/2026 (session Claude Code). Archivé selon la convention `prompts/`.

## DÉCISION DU PROPRIÉTAIRE

Je valide officiellement les huit corrections métier et ergonomiques COR-05-01 à COR-05-08.

Tu es autorisé à effectuer leur implémentation complète sur la branche `feat/phase-05-catalogue-stock`.

**Objectif : finaliser la Phase 05 avant sa Pull Request et son merge dans main.**

Je ne veux pas de nouveaux questionnaires métier ni de demandes de GO pour chaque composant, écran, service ou correction technique ordinaire. Tu dois travailler de manière autonome, en respectant strictement les décisions ci-dessous. Ne commence pas la Phase 06.

## 1. RÈGLES ABSOLUES

1. La référence visuelle reste le Lot 6 de Claude Design.
2. Les règles métier approuvées de MMV V2 restent prioritaires sur les anciennes maquettes.
3. Ne jamais reprendre le code, les DTO ou le modèle de données de l'ancien MMV.
4. Conserver la stack : Tauri 2, React/TypeScript, ASP.NET Core .NET 10, EF Core et PostgreSQL 18 exclusivement.
5. Ne pas supprimer ou dégrader les fonctionnalités déjà opérationnelles.
6. Ne pas modifier la migration 0007 déjà appliquée.
7. Ne pas modifier `apps/desktop/.env.development`.
8. Ne pas arrêter les processus actifs du propriétaire.
9. Aucun push, aucune PR, aucun merge.
10. Commits locaux autorisés.
11. Respecter toutes les permissions, l'audit et les contrôles transactionnels existants.

Tu peux modifier React, CSS, les services .NET, les routes API et les tests lorsque c'est nécessaire. Toute évolution de schéma devra être préparée sous forme d'une nouvelle migration, jamais par modification rétroactive de 0007.

## 2. COR-05-01 — UN SERVEUR LOCAL = UN MAGASIN

Contexte : MMV sera initialement installé localement dans chaque magasin d'optique. Chaque installation disposera d'un serveur MMV, de sa base PostgreSQL et de plusieurs postes Tauri connectés sur le réseau local. Il n'y a pas de serveur central multi-magasins à développer pour cette version.

Comportement attendu : le magasin actif est configuré lors de l'installation ou de l'initialisation du serveur. Un collaborateur qui se connecte utilise automatiquement ce magasin. Il ne doit pas pouvoir sélectionner librement un autre magasin. Les pages Catalogue, Stock, Prix, Fournisseurs, Inventaires et futures Ventes doivent utiliser le magasin défini par le serveur. L'API doit vérifier le contexte magasin et ne pas faire confiance à un `storeId` choisi arbitrairement par le frontend. Les permissions existantes restent obligatoires.

Contraintes : conserver les identifiants magasin dans les données ; conserver la compatibilité avec une future évolution multi-magasins ; ne pas supprimer `stores create`, `stores list` et `locations create` ; conserver la devise propre au magasin (EUR ou MAD) ; ne jamais convertir automatiquement les devises ; ne pas permettre de changer la devise d'un magasin existant ; les environnements fictifs Belgique et Maroc doivent rester testables via des configurations serveur isolées ; un serveur de production ne doit pas choisir silencieusement un magasin si plusieurs magasins existent dans sa base : configuration explicite et échec sûr en cas d'ambiguïté. Étudier les mécanismes existants de configuration et éviter toute migration inutile.

Critères : un utilisateur ouvre MMV et arrive directement dans son magasin ; aucun sélecteur de magasin dans le parcours standard ; les données d'un autre magasin ne peuvent pas être consultées par simple modification d'une requête ; une installation EUR ne vend jamais en MAD ; les tests de non-régression des deux magasins fictifs restent possibles.

## 3. COR-05-02 — SYNCHRONISATION TEMPS RÉEL

Implémenter ASP.NET Core SignalR et son client React/TypeScript ; pas de polling présenté comme équivalent. Notifier après modification confirmée : produits, variantes, fournisseurs, tarifs, entrées/sorties de stock, ajustements, réceptions, inventaires, disponibilité, alertes de stock. Architecture de notification réutilisable pour les futurs modules, sans refaire les Phases 02 à 04.

Fonctionnement : 1. un poste envoie une modification ; 2. l'API exécute validations et transaction PostgreSQL ; 3. après COMMIT réussi, notification ; 4. les clients actualisent depuis l'API ; 5. nouvel état sans rechargement manuel. Jamais de notification pour une transaction annulée.

Sécurité et robustesse : connexion SignalR authentifiée ; filtrage serveur par installation/magasin et autorisations ; aucune fuite de prix d'achat ou de coefficient sans `costs.read` ; reconnexion automatique ; resynchronisation après reconnexion ; événements manqués, désordre, requêtes simultanées ; préservation des formulaires non enregistrés ; détection des conflits ; actualisation manuelle de secours. Remplacer « Pas de temps réel » par un indicateur discret : Connecté / Reconnexion en cours / Déconnecté — données potentiellement obsolètes.

Critères : deux véritables clients Tauri sur la même API locale ; stock A → B automatique ; prix A → B selon permissions ; coupure puis reconnexion → état cohérent ; deux modifications concurrentes ne s'écrasent jamais silencieusement.

## 4. COR-05-03 — DÉBORDEMENT DES TABLEAUX PRODUIT

Dans la fiche produit, les tableaux « Prix et disponibilité » et « Historique de stock » débordent des cartes. Corriger layout/grilles/conteneurs ; jamais de dépassement ; défilement horizontal interne si nécessaire ; en-têtes, actions et colonnes essentielles accessibles ; motifs longs/commentaires avec détail consultable ; pas de double barre inutile ; composition Lot 6 conservée. Recette : 1920×1080, 1366×768, 1024×700, fenêtre étroite, zoom 125 %, clair et sombre ; captures avant/après.

## 5. COR-05-04 — IMPORT CSV INTELLIGENT ET SIMPLIFIÉ

Détection automatique (séparateurs , ; tab ; encodages usuels ; intitulés différents ; décimales point/virgule ; espaces/casse ; zéros initiaux, références jamais converties en nombres). Correspondance automatique proposée, vérifiable et modifiable (`PA HT`, `Prix achat`, `Purchase Price` → `prix_achat_ht` ; `Ref`, `Référence`, `Product Code` → référence produit/fabricant selon contexte). Modèles fournisseur : profil d'import enregistré par fournisseur et reproposé. Correction avant import : prévisualisation, doublons, colonnes obligatoires, erreurs par ligne, correction des valeurs avant confirmation, facultatifs vides, SKU généré si nécessaire, compte rendu des créations réelles. Import atomique et protections conservés. Ne jamais inventer valeur manquante, devise, taux fiscal ou référence fournisseur ; valeurs ambiguës signalées et confirmées. Pas d'IA ni service payant : règles, normalisation, synonymes, regex, profils ; extension IA locale future éventuelle sans dépendance. Tests : plusieurs structures, erreurs, doublons, réémission, montants ambigus, permissions.

## 6. COR-05-05 — REFONTE FONCTIONNELLE DU RÉFÉRENTIEL VERRES

Interface simple au quotidien + mode avancé pour plages et restrictions réelles de fabrication fournisseur. Niveau 1 : fournisseur, fabricant/marque, gamme et type, matériau/indice, traitements, politique stocké/commande, plage SPH min/max, plage CYL, addition, diamètres, délais et prix connus ; design system Lot 6, libellés clairs, aide contextuelle. Niveau 2 : plusieurs plages SPH/CYL, plages d'addition, diamètres conditionnels, restrictions de combinaison SPH/CYL, conditions indice/matériau, conditions traitements et combinaisons, fabrication standard vs spéciale, restrictions fournisseur, suppléments de prix et délais, date de validité et provenance de la fiche technique. Règles structurées, validables, historisées ; aucune expression arbitraire non sécurisée.

Exemple de test fictif : gamme + anti-lumière bleue, SPH -10,00 à +5,00 D ; -6,00 dans la plage ; -11,00 ou +6,00 hors plage ; une SPH dans la plage ne garantit pas la fabricabilité complète. États : compatible avec les règles complètes enregistrées / incompatible selon une règle explicite / compatibilité à vérifier faute de données. Jamais présenté comme validation clinique ou garantie fabricant.

Architecture séparée : référentiel verres ; capacités et restrictions fournisseur ; variantes commercialisables ; politique de stock ; correction client issue du dossier optique. Contrats Phase 06 préparés sans vente/commande de verres. Référentiels existants préservés ; migration additive si nécessaire (GATE SQL). UX : plus de formulaire technique opaque ; règles complexes seulement en mode avancé.

## 7. COR-05-06 — ASSISTANT DE CRÉATION PRODUIT EN CINQ ÉTAPES

1 Informations générales (famille, marque, modèle, désignation, caractéristiques) ; 2 Variantes (coloris, dimensions, SKU automatique, code-barres facultatif, politique de stock) ; 3 Fournisseurs (autocomplétion, principal, référence, prix d'achat si applicable) ; 4 Tarification (PA HT, coefficient, aperçu PV HT serveur, manuel si autorisé, devise du magasin) ; 5 Stock initial et validation (emplacement, quantité si stocké, résumé, confirmation). Règles : Précédent/Suivant, progression, validation par étape, conservation des saisies, brouillon récupérable si nécessaire, SKU auto unique, fournisseur facultatif, pas de stock initial pour service/sur commande, contrôle serveur prix et permissions, aucune écriture partielle, enregistrement final transactionnel ou orchestration serveur atomique, idempotence. Réutiliser composants/APIs ; pas de produits orphelins ni mouvements sans produit valide ; adapter aux verres correcteurs ; conserver modification et historique.

## 8. COR-05-07 — HARMONISATION DE LA RECHERCHE

Référence : comportement validé Clients et Dossier optique ; pas de troisième composant incohérent. Recherche pendant la frappe, debounce, côté serveur sur toutes les données autorisées, accents/casse, espaces superflus, noms/marques/modèles/SKU/références/variantes, plusieurs mots combinés, pagination cohérente, retour page 1 au changement de filtre, neutralisation des réponses tardives, états chargement/aucun résultat/erreur, filtré sur le magasin actif. Modules : Catalogue, Stock, autres listes Phase 05 pertinentes. Cas : `Ray Ban`, `RX5228`, `noir`, `ray ban noir` — résultats cohérents au-delà de la première page.

## 9. COR-05-08 — FOURNISSEURS AVEC AUTOCOMPLÉTION

Combobox accessible : recherche par nom, par référence fournisseur si pertinent, suggestions, clavier, Entrée, résultats limités/paginés, chargement/absence, permissions, création rapide si `suppliers.write` puis sélection automatique. Réutilisé dans l'assistant, la modification de variante, les réceptions et autres écrans Phase 05. Pas de doublons fournisseurs.

## 10. FIDÉLITÉ VISUELLE ET TRAVAUX ANTÉRIEURS

Lot 6 = référence principale. Sujets ouverts de l'audit G05-DESIGN à vérifier : trois exports CSV (Catalogue, Stock, Fournisseurs) ; états visuels non capturés ; logo du shell sur fond blanc en thème sombre ; prix d'achat sans perte de précision. Si déjà corrigés, apporter les preuves. Ne rien supprimer pour simplifier. Prix à quatre décimales : précision conservée, seules les décimales finales nulles peuvent être masquées.

## 11. POSTGRESQL — GATE OBLIGATOIRE

0007 appliquée sur `mmv_v2_dev` et `mmv_v2_test` : interdiction de la modifier. Si nouveau schéma : nouvelle migration EF Core, script SQL idempotent, diff exact, documentation tables/index/clés/contraintes, compatibilité données, plan de sauvegarde, SHA-256 et manifeste, tests uniquement sur PostgreSQL 18 jetable isolé, revue SQL consolidée unique. **STOP OBLIGATOIRE AVANT APPLICATION À `mmv_v2_dev` OU `mmv_v2_test`.** Statut intermédiaire `PHASE05_CORRECTIONS — OWNER_SQL_REVIEW_REQUIRED`. Le GATE n'empêche pas les écrans, services et tests indépendants du schéma.

## 12. TESTS OBLIGATOIRES

Backend : compilation, unitaires, intégration, PostgreSQL réel autorisé, permissions, concurrence, transactions, historique et audit. Frontend : TypeScript, lint, Vitest, build, recherche, assistant, CSV, états de connexion, responsive. Tauri natif : connexion sans sélection, deux postes temps réel, reconnexion, création complète produit, PA × coefficient, recherche et suggestions fournisseurs, recherche Catalogue et Stock, référentiel verres, saisie/validation de plages, tables fiche produit sans débordement. Design : captures clair/sombre multi-résolutions, comparaison Lot 6 ; PASS / FAIL / NOT_TESTED distincts.

## 13. LIVRAISON ET ORGANISATION

Un seul chantier cohérent ; sous-lots internes et commits locaux possibles ; hypothèses documentées ; contradiction métier réelle isolée avec arbitrage concis. Livrables : registre COR-05-01..08 ; code ; tests et preuves ; captures Tauri ; comparaison Lot 6 ; doc mono-magasin ; doc SignalR/resynchronisation ; spécification référentiel verres ; doc import CSV ; rapport régressions et réserves ; archive d'audit légère ; SHA des commits. Aucun push/PR/merge, aucune Phase 06.

Statut final : sans migration `PHASE05_CORRECTIONS_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` ; avec migration `PHASE05_CORRECTIONS — OWNER_SQL_REVIEW_REQUIRED`, puis après validation SQL, fin des tests sur les bases autorisées et livraison finale.
