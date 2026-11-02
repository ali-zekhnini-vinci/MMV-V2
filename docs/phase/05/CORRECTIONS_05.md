# Phase 05 — Registre des corrections COR-05-01 à COR-05-08

Mandat : [`prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md`](../../../prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md) (GO du propriétaire du 02/11/2026 sur les huit corrections). Branche `feat/phase-05-catalogue-stock`, commits locaux uniquement (aucun push, aucune PR, aucun merge, aucune Phase 06).

**Statut : `PHASE05_CORRECTIONS — OWNER_SQL_REVIEW_REQUIRED`** — la migration additive `0008_CatalogCorrections` (COR-05-04, COR-05-05) est préparée et éprouvée sur cluster jetable, **non appliquée** : revue [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md).

Statuts : `IMPLEMENTED` (code + tests automatisés au vert), `PENDING` (en cours), `NOT_TESTED` (preuve non encore produite). Les preuves natives (Tauri) et visuelles sont rapportées séparément ; un statut `IMPLEMENTED` ne vaut pas conformité visuelle.

## Journal

| Date | Entrée |
| --- | --- |
| 2026-11-02 | Lot B (schéma 0008, cluster jetable seulement) : COR-05-04 et COR-05-05 implémentés. `MMV.Api.Tests` 527 / 527 (dont 28 règles pures des corrections), `MMV.Maintenance.Tests` 50 / 50, PostgreSQL Phase 05 **20 / 20** (+1 `NOT_TESTED`) sur cluster jetable 0001→0008, `verify.sql` 0008 14 / 14, Vitest 325 / 325, ESLint 0 erreur, `vite build` OK. 0008 **non appliquée** à `mmv_v2_dev` / `mmv_v2_test`. |
| 2026-11-02 | Lot A (sans schéma) : COR-05-01, 02, 03, 06, 07, 08 implémentés côté serveur et poste. Tests : MMV.Api.Tests 499/499, MMV.Maintenance.Tests 50/50, PostgreSQL Phase 05 18/18 (+1 `NOT_TESTED` : processus HTTPS réel) sur cluster PostgreSQL 18 **jetable** (port 55433, base `mmv_v2_scratch`), Vitest 315/315, `npm run check` OK, ESLint 0 erreur (1 avertissement préexistant), `vite build` OK. Aucune écriture sur `mmv_v2_dev` / `mmv_v2_test`. |

## Registre

| ID | Objet | Statut | Preuves automatisées |
| --- | --- | --- | --- |
| COR-05-01 | Un serveur local = un magasin | IMPLEMENTED | `tests/MMV.PostgreSql.Tests/Phase05StoreContextTests.cs` (2) |
| COR-05-02 | Synchronisation temps réel SignalR | IMPLEMENTED (serveur + poste) ; natif deux postes `NOT_TESTED` | `Phase05RealtimeTests.cs` (vrais clients SignalR .NET, WebSocket et LongPolling) |
| COR-05-03 | Débordement des tableaux de la fiche produit | IMPLEMENTED ; captures `NOT_TESTED` | revue CSS, Vitest |
| COR-05-04 | Import CSV intelligent | IMPLEMENTED (code) ; PostgreSQL sur cluster jetable seulement ; bases du propriétaire `NOT_TESTED` (0008 non appliquée) | `Phase05CorrectionsSchemaTests.CsvImport_*`, `CorrectionsRulesTests` (nombres, synonymes, séparateur), `corrections.test.ts` (encodage, corrections) |
| COR-05-05 | Référentiel verres (simple + avancé) | IMPLEMENTED (code) ; idem | `Phase05CorrectionsSchemaTests.LensOffers_*`, `CorrectionsRulesTests` (évaluateur, exemple −10 / +5) |
| COR-05-06 | Assistant de création en cinq étapes | IMPLEMENTED ; natif `NOT_TESTED` | `Phase05ProductWizardTests.cs` |
| COR-05-07 | Recherche harmonisée | IMPLEMENTED | `Phase05SearchTests.cs` |
| COR-05-08 | Fournisseurs avec autocomplétion | IMPLEMENTED ; natif `NOT_TESTED` | `Phase05SearchTests.cs` (recherche fournisseur), doublon de nom refusé (service) |

## COR-05-01 — Un serveur local = un magasin

- Configuration : section `Installation`, clé `StoreCode` (code `org.stores`, créé par `MMV.Maintenance stores create`). Variable d'environnement équivalente : `Installation__StoreCode`.
- Résolution (`src/MMV.Api/Installation/ActiveStore.cs`) : code configuré → ce magasin (refus s'il est inconnu ou archivé) ; sans code → le magasin **unique** actif de la base ; zéro ou plusieurs magasins → **échec sûr** `503 store_not_configured` avec motif borné (`no_store`, `ambiguous`, `unknown_code`, `archived`). Jamais de choix silencieux. Aucune migration.
- Toutes les routes magasin (`/api/catalog`, `/api/pricing`, `/api/suppliers`, `/api/stock`, `/api/inventories`, `/api/imports`, `/api/stores`, `/api/store`) passent par `ActiveStoreFilter`. Le `storeId` envoyé par le poste devient **facultatif** et n'est qu'une assertion : différent du magasin de l'installation ⇒ `404 store_not_found` (même réponse qu'un magasin inexistant). Écritures désignant un emplacement, une session d'inventaire, une réception, un mouvement ou un lot d'import d'un autre magasin ⇒ `404`.
- `GET /api/stores` ne renvoie plus que le magasin de l'installation ; nouveau `GET /api/store` (`{ store, source }`, `source` = `configuration` ou `single_store`).
- Poste : le sélecteur de magasin est remplacé par un **badge** (nom + devise) ; message explicite si le serveur n'a pas de magasin. L'import des tarifs vise le magasin de l'installation.
- Devise : propre au magasin (EUR ou MAD), jamais convertie ; le serveur MA refuse l'euro (`currency_mismatch`, testé).
- Magasins fictifs BE / MA : testables par **deux serveurs isolés par configuration** sur la même base (banc PostgreSQL : `Client` = BE, `ClientMa` = MA).
- Base de développement du propriétaire : un seul magasin actif (`BE-PILOTE`, EUR, lecture seule le 02/11/2026) ⇒ résolution automatique, aucune configuration à ajouter. Une base contenant plusieurs magasins exige `Installation:StoreCode`.
- Maintenance inchangée : `stores create`, `stores list`, `locations create`.

## COR-05-02 — Temps réel (SignalR)

- Serveur : hub `/hubs/changes` (`src/MMV.Api/Realtime/Realtime.cs`), ASP.NET Core SignalR (cadre partagé, aucun paquet serveur ajouté). Authentification par la session MMV (en-tête `Authorization`, ou paramètre `access_token` **uniquement sur le chemin du hub** pour le WebSocket du navigateur), HTTPS obligatoire, CORS dédié `desktop-realtime` (mêmes origines que l'API).
- Notification **après écriture confirmée** : filtre `PublishesChanges` sur les groupes d'écriture ; seulement si la réponse est un succès 2xx (le service a validé la transaction avant de répondre) ; refus, conflit, annulation ⇒ aucune notification (testé).
- Contenu : numéro de séquence, instance du serveur, sujets, route, identifiants de route. **Aucune valeur métier** (ni prix, ni coût, ni coefficient, ni quantité — testé) : le poste relit l'API, qui applique `costs.read`.
- Filtrage serveur : une connexion ne reçoit que les sujets que sa session peut lire (`catalog`/`pricing` ← `catalog.read`, `stock`/`inventory` ← `stock.read`, `suppliers` ← `suppliers.read`, `imports` ← `catalog.import`) et seulement pour le magasin de l'installation ; un envoi par connexion (jamais de doublon).
- Révocation : contrôle périodique **sans glissement ni écriture** (`AuthService.IsSessionValidAsync`, `Realtime:SessionCheckSeconds`, 30 s par défaut) ; session révoquée/expirée/inactive ⇒ connexion fermée (testé).
- Poste : `src/realtime/` (connexion unique par session, montée dans le Shell si le module Catalogue & stock est actif ; `@microsoft/signalr` 10.0.11 chargé à la demande). Reconnexion automatique sans fin (0, 1, 2, 5, 10 s puis 15 s) ; au-delà de 20 s : « Déconnecté — données potentiellement obsolètes ». Resynchronisation complète après reconnexion ou redémarrage du serveur (événements manqués jamais rejoués : l'état est relu). Événements dupliqués ignorés ; l'ordre importe peu (chaque événement ne déclenche qu'une relecture TanStack).
- Indicateur discret (barre de contexte du module et bandeau « Dernier état chargé ») : Connecté / Reconnexion en cours / Déconnecté ; « Actualiser » reste disponible (secours manuel). La mention « pas de temps réel » est retirée.
- Saisies préservées et conflits : formulaires à version **figée à l'ouverture** (`useEditBase`) ; une actualisation venue d'un autre poste ne remplace jamais la saisie ; bandeau « modifié sur un autre poste » avec intégration à trois voies (les champs non modifiés reprennent la valeur enregistrée). Modales de variante, éditeur de tarif et comptage d'inventaire figent aussi leur version : le serveur refuse `409` plutôt que d'écraser (deux écritures concurrentes : une réussit, l'autre `409`, testé). Correctif induit : le formulaire fournisseur n'est plus remonté (clé = identifiant, plus la version).

## COR-05-03 — Tableaux de la fiche produit

Cause : dans la colonne latérale de 380 px, les en-têtes en capitales `nowrap` et les cellules à 18 px de marge latérale de `clients-table` dépassaient la carte. Correction (`TableScroll`, `ClipText`, CSS `cat-tscroll`, `cat-table--side`, `cat-td-fill`) : tout tableau de carte est contenu dans un conteneur à défilement horizontal **interne** (une seule barre, région focalisable au clavier) ; cellules resserrées dans la colonne latérale ; motif et commentaire de mouvement coupés « … » avec texte complet consultable (clic / Entrée / info-bulle). Appliqué à « Prix & disponibilité » (stock par emplacement), « Historique de stock », « Historique des prix », variantes, produits du fournisseur, référentiel, import, réception, assistant.

## COR-05-06 — Assistant de création produit

- Poste : `src/catalog/ProductWizard.tsx` remplace l'ancien formulaire (route `/produits/nouveau`). Cinq étapes (Informations générales, Variantes, Fournisseurs, Tarification, Stock initial et validation), progression cliquable (étapes `.cat-steps` du Lot 6), contrôle par étape, brouillon de session en mémoire (jamais sur disque), aperçu du prix par le serveur, résumé final.
- Serveur : `POST /api/catalog/products` (`ProductCreationService`) — modèle, variantes, rattachements fournisseur, tarifs et stock initial dans **une seule transaction** (les services existants s'y exécutent avec leurs règles, droits et audit) ; le moindre refus annule tout (testé : échec au tarif après création du modèle et du lien ⇒ aucune ligne). Droits par section vérifiés avant toute écriture. Idempotence : même `requestId` ⇒ même produit (`200`, `replayed`), identifiants de sous-requêtes dérivés (SHA-256, déterministes).
- SKU : générés par le **serveur** quand la saisie est vide (`SkuGenerator`, préfixe de famille + 6 symboles), uniques (vérification avant écriture + contrainte) — clôt O-05-20. Création simple (`POST /models`, `/variants`) : même règle.
- Stock initial : mouvement `manual_in` motif `initial_stock` (nouveau code borné, aucune migration : la contrainte ne vérifie que le format) ; refusé pour une prestation ou un article commandé (testé).

## COR-05-07 — Recherche

Serveur (`src/MMV.Infrastructure/Catalog/CatalogSearch.cs`) : même principe que Clients — casse et accents ignorés (`translate` + `lower` natifs, aucune extension, aucune colonne), espaces superflus ignorés, **chaque mot** doit apparaître, forme compacte (« rayban » ↔ « Ray-Ban », « RX 5228 » ↔ « RX5228 »), document = SKU, libellé, modèle, marque, code-barres, référence fabricant, caractéristiques (coloris…), références fournisseur ; paramètres liés, jokers LIKE neutralisés. Appliqué au catalogue, au stock et aux fournisseurs (testé : `Ray Ban`, `RX5228`, `noir`, `ray ban noir`, accents, au-delà de la première page). Poste : délai de frappe commun 300 ms (`SEARCH_DEBOUNCE_MS`, partagé avec Clients), Échap vide le champ, page 1 à chaque changement, page précédente conservée pendant le chargement, indicateur « recherche en cours », surlignage des mots (composant `Highlight` de Clients).

## COR-05-08 — Fournisseurs avec autocomplétion

`src/catalog/SupplierCombobox.tsx` : combobox ARIA (saisie, suggestions serveur limitées à 8, flèches, Entrée, Échap, chargement, aucun résultat, surlignage), création rapide si `suppliers.write` (nom repris, code proposé, pays du magasin) puis sélection automatique. Réutilisé : assistant (étape 3), rattachement d'une variante, réceptions, filtre « Fournisseur » du catalogue. Doublons : le serveur refuse désormais un nom déjà porté par un fournisseur **actif** (casse, accents, espaces ignorés) sauf confirmation explicite `allowDuplicateName` (jamais envoyée par la création rapide).

## COR-05-04 — Import CSV intelligent

- **Détection** : encodage sur le poste (`csvFile.ts` : BOM UTF-8 / UTF-16, UTF-8 strict, sinon Windows-1252) ; séparateur par **cohérence** du nombre de colonnes sur 20 lignes (`;`, `,`, tabulation ; guillemets respectés), même règle serveur (`CsvTable.DetectDelimiter`) et poste ; intitulés normalisés (casse, accents, espaces, ponctuation) ; valeurs toujours en **chaînes** (« 000123 » reste « 000123 »).
- **Correspondance** (`ImportMapper`) : dictionnaire de synonymes français / anglais (ex. `PA HT`, `Prix achat`, `Purchase Price` → prix d'achat HT ; `Couleur`, `Coloris`, `Color` → coloris), confiance affichée (**Modèle fournisseur**, **Reconnu**, **Probable — à vérifier**, **Ambigu — à confirmer**). Contexte : fichier d'un fournisseur désigné ⇒ `Ref` / `Référence` / `Product Code` proposés comme **référence fournisseur** (probable) ; sans fournisseur ⇒ ambigu (jamais appliqué sans choix). Une même colonne peut servir de libellé de variante et de coloris.
- **Modèles fournisseur** (`imports.supplier_profiles`, 0008) : correspondance et options validées enregistrables au contrôle (« Enregistrer comme modèle ») ; reprises automatiquement au dépôt d'un nouveau fichier de ce fournisseur (colonnes encore présentes).
- **Valeurs par défaut choisies** (jamais inventées) : famille et politique de stock pour tout le fichier quand la colonne manque (options `@familyCode`, `@stockPolicy`).
- **Montants** (`NumberText`) : virgule ou point décimal, milliers (espace, insécable, point, virgule, apostrophe), symbole ou code de devise ; séparateur détecté par colonne ; valeur **ambiguë** (« 1,234 ») signalée `ambiguous_number` tant que le séparateur n'est pas confirmé (`@decimal`) ; devise différente du magasin refusée (aucune conversion).
- **Rapprochement sans SKU** : référence fournisseur (fournisseur du fichier), puis code-barres ; sinon création avec **SKU généré par le serveur** à l'application. Réémission d'un fichier corrigé du même fournisseur : mises à jour, pas de doublon (testé). Rattachement inchangé = ligne « inchangée » ; fournisseur du fichier principal par défaut seulement si la variante n'en a aucun.
- **Prix dans un fichier catalogue** : second lot « Prix du magasin » proposé après application (rapprochement par référence fournisseur) — décision qui évite d'élargir la contrainte `ck_batches_store` ; deux applications distinctes, chacune tout ou rien.
- **Corrections avant import** : dans le rapport, chaque erreur de ligne offre une saisie ; « Recontrôler avec N correction(s) » abandonne le lot (tracé) et dépose la version corrigée comme **nouveau lot** (empreinte propre, l'original reste en historique).
- **Compte rendu** : créations (dont SKU générés), modifications, inchangées, erreurs par ligne en français (champ métier + motif borné, jamais la valeur dans le message).
- **IA** : aucune ; point d'extension documenté (assistant local futur soumis à la même confirmation), aucune dépendance.

## COR-05-05 — Référentiel verres

- Séparation : référentiel de base (fabricants, gammes, indices, traitements — inchangé) ; **offres fournisseur** (`catalog.lens_offers`, 0008) = capacités et restrictions d'UN fournisseur pour UNE gamme ; variantes commercialisables (catalogue, inchangé) ; politique de stock de l'offre ; correction du client (dossier optique, non modifié — contrat `LensPrescription` prêt pour la Phase 06).
- Niveau 1 (écran « Gammes et offres fournisseur ») : par gamme, tableau des offres (fournisseur, type, indice / matériau, traitements, stocké / sur commande, SPH, CYL, ADD, diamètres, délai, prix connu si `costs.read`, fiche complète ou non) ; détail dépliable ; **testeur de correction**.
- Niveau 2 (« Mode avancé ») : règles STRUCTURÉES — plages SPH / CYL / ADD multiples (autoriser / interdire), diamètres conditionnels, limite |SPH| + |CYL|, restriction explicite, conditions (traitements tous choisis, indices, matériau, plages de la correction), fabrication standard / spéciale, supplément HT (coût), délai supplémentaire, note ; validité et provenance de la fiche ; déclaration « fiche complète » ; motif de révision. Aucune expression libre : membre inconnu ⇒ `400`, type inconnu ⇒ `422` (testé).
- Évaluation (`LensCompatibilityEvaluator`) : **compatible** seulement si toutes les dimensions sont couvertes ET la fiche déclarée complète et en cours de validité ; **incompatible** sur règle explicite (hors plage, combinaison, diamètre, traitement non proposé, restriction) ; **à vérifier** sinon (donnée manquante, fiche incomplète ou échue). Règle de spécificité : des règles « autoriser » à conditions qui s'appliquent remplacent la plage de base (ex. anti-lumière bleue : −10,00 à +5,00) ; « interdire » l'emporte toujours. Mention systématique : ni validation clinique ni garantie du fabricant. Exemple du mandat testé : −6,00 dans la plage (à vérifier tant que la fiche est incomplète), −11,00 et +6,00 hors plage.
- Historique : révision incrémentée et instantané en **ajout seul** (`catalog.lens_offer_revisions`) à chaque modification ; audit `lens_offer.*`.
- Assistant (COR-05-06) : pour un verre, la gamme est requise à l'étape 1, indice et traitements à l'étape 2, et l'étape 3 propose les fournisseurs ayant une offre pour cette gamme.

## Réserves et observations (lot A)

- `npm audit` : 1 avis modéré préexistant (`fast-uri` via `ajv` via `@hookform/resolvers`), sans lien avec `@microsoft/signalr`.
- `apps/desktop/.env.development` : modification du propriétaire (dernière écriture 01/11/2026 15:28, empreinte actuelle `a215e1f0…2e4e`), jamais modifiée par l'agent, jamais indexée.

## Réserves et observations (lot B)

- **0008 non appliquée** : le code des corrections suppose 0008 ; sur une base 0007 seule, `/api/imports` et `/api/catalog/lens-offers` échouent (colonne et tables absentes). Les tests 0008 sont ignorés (`NOT_TESTED`) sur une telle cible.
- Offres de verres : un compte sans `costs.write` ne peut pas enregistrer une offre dont il modifierait (ou masquerait, faute de `costs.read`) le prix ou un supplément — refus `403 costs_forbidden`, jamais d'effacement silencieux ; à affiner si besoin (champ « inchangé » explicite).
- Import : la colonne fournisseur d'un fichier catalogue est rapprochée par **code** fournisseur (pas par nom) ; un fichier fournisseur se rattache par le fournisseur désigné au dépôt.
- Les offres de verres ne pilotent encore aucune vente ni commande (Phase 06 hors mandat) ; le contrat d'évaluation est prêt.
