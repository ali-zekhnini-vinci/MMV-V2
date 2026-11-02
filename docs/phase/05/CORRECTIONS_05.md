# Phase 05 — Registre des corrections COR-05-01 à COR-05-08

Mandat : [`prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md`](../../../prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md) (GO du propriétaire du 02/11/2026 sur les huit corrections). Branche `feat/phase-05-catalogue-stock`, commits locaux uniquement (aucun push, aucune PR, aucun merge, aucune Phase 06).

Statuts : `IMPLEMENTED` (code + tests automatisés au vert), `PENDING` (en cours), `NOT_TESTED` (preuve non encore produite). Les preuves natives (Tauri) et visuelles sont rapportées séparément ; un statut `IMPLEMENTED` ne vaut pas conformité visuelle.

## Journal

| Date | Entrée |
| --- | --- |
| 2026-11-02 | Lot A (sans schéma) : COR-05-01, 02, 03, 06, 07, 08 implémentés côté serveur et poste. Tests : MMV.Api.Tests 499/499, MMV.Maintenance.Tests 50/50, PostgreSQL Phase 05 18/18 (+1 `NOT_TESTED` : processus HTTPS réel) sur cluster PostgreSQL 18 **jetable** (port 55433, base `mmv_v2_scratch`), Vitest 315/315, `npm run check` OK, ESLint 0 erreur (1 avertissement préexistant), `vite build` OK. Aucune écriture sur `mmv_v2_dev` / `mmv_v2_test`. |

## Registre

| ID | Objet | Statut | Preuves automatisées |
| --- | --- | --- | --- |
| COR-05-01 | Un serveur local = un magasin | IMPLEMENTED | `tests/MMV.PostgreSql.Tests/Phase05StoreContextTests.cs` (2) |
| COR-05-02 | Synchronisation temps réel SignalR | IMPLEMENTED (serveur + poste) ; natif deux postes `NOT_TESTED` | `Phase05RealtimeTests.cs` (vrais clients SignalR .NET, WebSocket et LongPolling) |
| COR-05-03 | Débordement des tableaux de la fiche produit | IMPLEMENTED ; captures `NOT_TESTED` | revue CSS, Vitest |
| COR-05-04 | Import CSV intelligent | PENDING | — |
| COR-05-05 | Référentiel verres (simple + avancé) | PENDING | — |
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

## Réserves et observations (lot A)

- `npm audit` : 1 avis modéré préexistant (`fast-uri` via `ajv` via `@hookform/resolvers`), sans lien avec `@microsoft/signalr`.
- `apps/desktop/.env.development` : modification du propriétaire (dernière écriture 01/11/2026 15:28, empreinte actuelle `a215e1f0…2e4e`), jamais modifiée par l'agent, jamais indexée.
