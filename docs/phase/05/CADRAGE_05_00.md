# 05-00 — Cadrage Catalogue, Fournisseurs, Stock et Inventaires

Date : 29/10/2026. Mandat : [`prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md`](../../../prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md). Statut au 29/10/2026 : **`PHASE05_00 — OWNER_BUSINESS_REVIEW_REQUIRED`** (historique ; statut courant ci-dessous).

> **Clôture 30/10/2026 — `PHASE05_00_COMPLETE — OWNER_IMPLEMENTATION_REVIEW_REQUIRED`.** Le propriétaire a répondu à Q01–Q20 ([`prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md`](../../../prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md)) ; registre mis à jour (APPROVED / PROPOSED / SUPERSEDED) dans [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md). Branche `feat/phase-05-catalogue-stock` **créée** depuis `main` = `0a83a57` (O-05-02 levé, modification locale du propriétaire conservée). Les §§ 1 à 7 ci-dessous sont l'état du 29/10/2026, conservé ; **le § 8 prévaut** là où il diffère (variantes, magasins, prix HT × coefficient, devise par magasin, import CSV, séquence). Toujours aucun code, aucune migration, aucune table, aucune donnée pilote.

Documentation seulement : aucun code, aucune migration, aucun schéma, aucune table, aucune connexion à `mmv_v2_dev` / `mmv_v2_test`, aucun processus du propriétaire touché. Toutes les règles ci-dessous sont **proposées** ; les arbitrages sont regroupés dans [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md) (D-05-01 à D-05-13).

## 1. État Git après la fusion de la Phase 04

| Contrôle | Résultat |
|---|---|
| `git fetch origin --prune` | aucune référence nouvelle ; `main` = `origin/main` = `0a83a57` (« Merge pull request #5 from …/feat/phase-04-00-cadrage ») |
| `dcc6f0d` dans `main` et `origin/main` | oui (`git merge-base --is-ancestor`) ; `97bfd5e`, `56dfb7e`, `3bb25a6` également présents |
| Écart avec la branche Phase 04 | `git diff main origin/feat/phase-04-00-cadrage` vide : l'arbre fusionné est identique à `dcc6f0d` |
| Avance / retard `main` ↔ `origin/main` | 0 / 0 |
| Arbre de travail | **non propre** : une modification suivie appartenant au propriétaire, `apps/desktop/.env.development` (+1 ligne `VITE_MMV_OPTICAL_MODULE`, interrupteur local du module optique) ; fichiers non suivis du propriétaire (archives de phase, `.claude*`, `.serena/`, `references/`, pack Phase 04, `apps/desktop/desktop*.zip`) |
| Branche `feat/phase-05-catalogue-stock` | **non créée** : la condition du mandat (« si l'arbre de travail est propre ») n'est pas remplie. Rien n'a été modifié, remisé ni restauré. Création proposée après décision du propriétaire (O-05-02) |
| Migrations | 0001 à 0006 appliquées selon la documentation Phase 04 ; **non revérifié** (aucune connexion aux bases, conformément au mandat) |

Les documents de cadrage sont donc écrits comme **fichiers non suivis** sur `main` (aucun commit) ; `git switch -c feat/phase-05-catalogue-stock` les emportera tels quels.

## 2. Sources et réserve majeure sur le Lot 6

Sources : documentation du dépôt, registre des décisions, maquette Lot 6 (empreinte conforme au registre `registres/DESIGN_SOURCES_SHA256.csv`), catalogue de 37 groupes (B-098 à B-134) et 107 états (`MMV-L06-*`), backlog Phase 00, architecture des Phases 02 à 04. Détail : [`SOURCE_MAP.md`](SOURCE_MAP.md).

**O-05-01 — le Lot 6 dérive de l'ancien logiciel.** Sa propre en-tête annonce des « champs limités au code réel » d'un dépôt de l'ancien MMV et ses notes nomment entités, énumérations, cas d'usage et DTO. Conséquence (D-05-01) : la maquette est utilisée comme **référence d'interface uniquement** ; toute liste de champs, énumération ou règle est redéfinie ici à partir de besoins neufs. Les écarts volontaires avec la maquette sont listés en [`MATRICE_LOT6.md`](MATRICE_LOT6.md) § 3. Aucun code, schéma ou dépôt de l'ancien logiciel n'a été consulté.

**Compléments catalogue, achats et stock du backlog Phase 00.** Le dépôt ne contient pas le détail des « 153 vues + 23 compléments » (seuls leur existence et leur statut `DEFERRED` sont tracés dans `docs/PERIMETRE_ET_SEQUENCE.md` et `docs/phase/00.md`). Les éléments liés (commandes fournisseurs, réceptions, étiquettes, réapprovisionnement) sont traités ici comme **hors périmètre** (D-05-12) ; la liste exacte reste à fournir par le propriétaire (O-05-03).

## 3. Spécifications fonctionnelles proposées

### 3.1 Catalogue

| Élément | Règle proposée |
|---|---|
| Article | unité de vente et, s'il est stocké, unité de stock (D-05-04) |
| Référence | référence interne **unique** (comparaison insensible à la casse et aux espaces de bord), obligatoire, modifiable avec audit ; EAN / GTIN facultatif, unique s'il est renseigné, clé de contrôle vérifiée |
| Désignation, marque, description | désignation obligatoire ; marque facultative ; description libre bornée |
| Catégorie | liste fermée (D-05-05), obligatoire ; jeu d'attributs par catégorie validé par le serveur (ex. monture : couleur, calibre, pont, branche, matière) |
| Variantes | regroupement facultatif par « modèle » ; chaque coloris / taille est un article (D-05-04) |
| Politique | `stocked` / `order_only` / `service`, obligatoire (D-05-02) |
| Prix | prix de vente public TTC (devise de l'installation) ; facultatif pour `order_only` ; prix d'achat de référence protégé ; historique (D-05-08) |
| Seuil d'alerte | `stocked` uniquement ; vide = « seuil non défini » (aucune valeur par défaut imposée) |
| Disponibilité | calculée par le serveur, jamais saisie (§ 3.5) |
| Archivage | archivage / réactivation, jamais de suppression (D-05-07) |
| Recherche, filtres, tri | référence, désignation, marque, EAN ; filtres catégorie, fournisseur, politique, disponibilité, statut ; tri par colonne ; pagination serveur |

### 3.2 Fournisseurs

| Élément | Règle proposée |
|---|---|
| Fiche | nom, code interne unique, pays, contact, adresse, notes, identifiants légaux facultatifs selon le pays, statut actif / archivé (D-05-06) |
| Rattachement article ↔ fournisseur | plusieurs fournisseurs par article ; référence fournisseur, prix d'achat fournisseur (protégé), délai indicatif ; au plus un principal ; principal obligatoire pour `order_only` |
| Historique | journal d'audit (création, modification, archivage, rattachements) ; historique des prix d'achat fournisseur (lecture soumise à `catalog.costs.read`) ; réceptions du fournisseur visibles dans les mouvements |
| Archivage | interdit de choisir un fournisseur archivé comme principal ; archivage refusé s'il est l'unique fournisseur principal d'un article `order_only` actif |
| Données personnelles | le nom et les coordonnées d'un contact sont des données personnelles (RGPD / APD, loi 09-08 / CNDP) : un seul contact, minimal, sans donnée sensible |

### 3.3 Stock

| Élément | Règle proposée |
|---|---|
| Quantité en main | entier ≥ 0 (D-05-03), uniquement pour `stocked` ; unité = pièce (une boîte de lentilles = une pièce) |
| Mouvements | journal en ajout seul ; types de 05-B : réception, entrée manuelle, sortie manuelle, ajustement, correction d'inventaire ; types réservés à la Phase 06 et aux Commandes : vente, annulation de vente, retour client, retour fournisseur (D-05-09) |
| Atomicité | mouvement et solde écrits dans la même transaction, solde verrouillé ; quantité avant / après enregistrée |
| Ajustement | saisie de la quantité constatée + quantité attendue ; conflit si le stock a changé ; motif + confirmation |
| Seuils | état « stock faible » si quantité ≤ seuil ; « rupture » si 0 ; « seuil non défini » si vide ; aucune notification active en Phase 05 (filtre « stock faible » seulement) |
| Vue stock | « dernier état chargé à HH:MM » + Actualiser ; jamais présentée comme temps réel ; avertissement si une ligne a changé lors d'une action (version) |
| Réservations futures | **contrat seulement** (§ 6) : `disponible = en main − réservé`, réservé = 0 en Phase 05 ; table et écritures créées par la Phase 06 |
| Valorisation | aucune (D-05-08) ; coût unitaire de réception conservé, protégé |

### 3.4 Inventaires

| Étape | Règle proposée (D-05-10) |
|---|---|
| Ouverture | périmètre (tout, catégorie, sélection) d'articles `stocked` actifs ; refus si un article est déjà dans un inventaire ouvert ; numéro lisible ; option comptage à l'aveugle |
| Comptage | saisie de la quantité constatée par ligne (plusieurs postes possibles, une ligne à la fois, version par ligne) ; quantité théorique mémorisée au moment de la saisie ; recomptage possible avant validation |
| Comparaison / écarts | écart = constaté − théorique ; vue « écarts seuls » ; lignes non comptées signalées (non comptée ≠ 0) |
| Validation | permission distincte ; motif obligatoire pour chaque écart ; lignes dont le stock a changé depuis le comptage → recompter ou accepter explicitement ; application **atomique** de tous les écarts en mouvements « correction d'inventaire » liés à la session ; lignes non comptées : exclues ou mises à 0 sur choix explicite |
| Abandon | irréversible, sans effet sur le stock, conservé dans l'historique |
| Audit | ouverture, validation, abandon audités ; lignes et mouvements constituent la preuve détaillée |

### 3.5 Règles de disponibilité (calculées par le serveur)

| Politique / statut | Disponibilité | Vendable (contrat Phase 06) |
|---|---|---|
| archivé (toute politique) | `archived` | non |
| `service` actif | `available` | oui |
| `order_only` actif | `on_order` (+ délai indicatif) | oui, en commande |
| `stocked`, disponible > seuil (ou seuil vide et disponible > 0) | `in_stock` | oui, dans la limite du disponible |
| `stocked`, 0 < disponible ≤ seuil | `low_stock` | oui, dans la limite du disponible |
| `stocked`, disponible = 0 | `out_of_stock` | non en stock (D-05-03) ; vente en commande = décision Phase 06 |

Le libellé « seuil non défini » est un indicateur secondaire, pas un état de disponibilité.

## 4. Périmètre proposé des sous-lots (D-05-13)

| Sous-lot | IN | Migration | Groupes Lot 6 |
|---|---|---|---|
| **05-A Catalogue et fournisseurs** | catégories (liste fermée), modèles, articles et trois politiques, prix et historique, protection des coûts, fournisseurs, rattachements, archivage / réactivation, listes / recherche / filtres / tri, fiches, formulaires, conflits, permissions `catalog.*` / `suppliers.*`, audit | `0007` (G05-SQL-A) | B-098 à B-111 (sauf bloc historique de stock), B-125 à B-130, B-131 à B-133 pour ces écrans |
| **05-B Stock et mouvements** | soldes, journal de mouvements, entrées / sorties / ajustements, motifs, vue stock, historique des mouvements, bloc « historique de stock » de la fiche, service de disponibilité, contrats internes pour la Phase 06 (testés, non appelés par une vente), permissions `stock.read` / `stock.movements.write` / `stock.adjust` | `0008` (G05-SQL-B) | B-107 (bloc 30), B-108, B-112 à B-120, B-134 |
| **05-C Inventaires et clôture** | sessions, comptage, comparaison, écarts, validation atomique, abandon, historique ; exports CSV (catalogue, stock, fournisseurs, mouvements, inventaire) avec filtrage des coûts ; intégration PostgreSQL réelle, HTTPS, Tauri natif ; clôture | `0009` (G05-SQL-C) | B-121 à B-124, états CSV de B-101 / B-115 / B-130, B-131 à B-134 restants |

Chaque sous-lot : GO explicite, interrupteur de build `VITE_MMV_CATALOG_MODULE` (sur le modèle du module optique) jusqu'à la clôture, tests .NET / PostgreSQL / frontend / HTTPS / natif, revue du propriétaire. **Exigence du backlog Phase 00 :** la Phase 05 est à livrer **avant le premier encaissement réel**.

## 5. Risques et dépendances

### 5.1 Avec la Phase 06 — Ventes

| ID | Risque / dépendance | Traitement proposé |
|---|---|---|
| R-05-01 | La vente doit décrémenter le stock dans **sa** transaction | contrat `IStockLedger` appelé dans la transaction de la vente, verrous ordonnés (§ 6, [`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 4) ; testé en 05-B sans vente |
| R-05-02 | Prix et fiscalité : vente réelle impossible sans ADR-010 | catégorie fiscale prévue, renseignée après ADR-010 ; la Phase 06 refuse une vente sans catégorie fiscale |
| R-05-03 | Stock négatif interdit (D-05-03) ⇒ ventes bloquées si la réception n'est pas saisie | discipline de réception en 05-B ; option C de D-05-03 réservée |
| R-05-04 | Articles `order_only` sans prix | la Phase 06 exige un prix saisi (permission de la vente) |
| R-05-05 | Réservations (acomptes, commandes clients) | contrat défini, table créée par la Phase 06 ; `disponible = en main − réservé` dès 05-B |
| R-05-06 | Instantané de ligne de vente | la vente copie référence, libellé, politique, prix, catégorie fiscale ; archivage et changements de prix sans effet rétroactif |
| R-05-07 | Mesure ≠ ordonnance (D-04-01) pour les verres | inchangé : la Phase 06 lie la ligne « verre » à une révision confirmée ([`docs/phase/04/IMPLEMENTATION_SPEC.md`](../04/IMPLEMENTATION_SPEC.md) § 7) |

### 5.2 Avec la phase Commandes (clients, fournisseurs, atelier)

| ID | Risque / dépendance | Traitement proposé |
|---|---|---|
| R-05-08 | Article `order_only` vendu ⇒ besoin d'approvisionnement | fournisseur principal obligatoire (D-05-06) ; la phase Commandes créera la commande fournisseur |
| R-05-09 | Réception rapprochée d'une commande fournisseur | en 05-B, réception simple (fournisseur, quantité, coût) ; le rapprochement commande ↔ réception est OUT (D-05-12) ; colonnes de référence de source prévues |
| R-05-10 | Article reçu pour un client précis | ne passe pas par le stock disponible (`order_only`) ; si un article `stocked` est commandé pour un client, réservation (Phase 06 / Commandes) |
| R-05-11 | Retour fournisseur | type de mouvement réservé, motif prévu ; écran OUT |

### 5.3 Risques propres à la Phase 05

| ID | Risque | Traitement |
|---|---|---|
| R-05-12 | Dérive vers les règles héritées du Lot 6 | D-05-01 ; revue de la matrice par le propriétaire |
| R-05-13 | Fuite des prix d'achat (réponse API, CSV, journal, audit) | filtrage serveur, tests de non-présence des clés (Q-05-1xx) |
| R-05-14 | Interblocages multi-articles (inventaire, futures ventes) | verrous dans l'ordre des identifiants d'article, tests à barrières |
| R-05-15 | Volume du journal de mouvements | index (article, date) ; aucun calcul de solde par somme en ligne (solde maintenu, invariant vérifié par test) |
| R-05-16 | Traçabilité réglementaire des dispositifs médicaux (lot / identifiant unique) au Maroc et en Belgique | **G05-LEGAL** avant toute vente réelle ; si obligatoire, BL-05-03 remonte avant la Phase 06 |
| R-05-17 | Rôle SQL applicatif = propriétaire des tables (O-B04, BL-04-04) : les déclencheurs « ajout seul » ne protègent pas contre lui | inchangé, prérequis d'un pilote |
| R-05-18 | Durées de conservation (pièces comptables, inventaires) | à confirmer avec le comptable ; aucune purge prévue |

## 6. Contrats prévus pour la Phase 06 (définis, non développés)

Détail : [`API_PROPOSED.md`](API_PROPOSED.md) § 6. Résumé : `ICatalogQuery.GetSellableItems(ids)` (politique, prix, catégorie fiscale, disponibilité, version) ; `IStockLedger.ApplyDocumentMovements(source, lignes, requestId)` dans la transaction de l'appelant, tout ou rien ; `IStockLedger.ReverseDocumentMovements(source, motif, requestId)` ; `IStockReservations` (réserver, libérer, consommer) défini comme interface sans implémentation.

## 7. Exigences transverses conservées

- **Maroc / Belgique** : devise par installation (MAD / EUR), identifiants légaux fournisseurs selon le pays, fiscalité suspendue à ADR-010, traçabilité des dispositifs médicaux à vérifier (G05-LEGAL), conservation comptable à confirmer.
- **Données sensibles** : prix d'achat, marges et coûts = données commerciales protégées par permission ; contacts fournisseurs = données personnelles minimales ; aucune donnée client ni de santé dans les modules 05 (la liaison client ↔ article naîtra des ventes).
- **Audit et traçabilité** : écritures auditées dans la même transaction (schéma `audit`, D-03-19), sans valeur de coût dans les événements ; mouvements et inventaires immuables ; historique des prix.
- **Multi-postes** : concurrence optimiste `xmin` sur les fiches, verrous pessimistes sur les soldes, idempotence par identifiant de requête, conflits explicites (`409`) ; aucun temps réel.

## 8. Révision 30/10/2026 — spécifications après validation métier (prévaut sur les §§ 3, 4 et 7)

Décisions citées : [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md). Modèle de données : [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) ; API : [`API_PROPOSED.md`](API_PROPOSED.md).

### 8.1 Structure du catalogue (D-05-04, D-05-05, D-05-14, D-05-21 — APPROVED)

| Niveau | Contenu | Porte |
|---|---|---|
| Famille | liste fermée de six : montures optiques, lunettes de soleil, verres correcteurs, lentilles et solutions, accessoires, prestations | jeu d'attributs (défini dans le code, PROPOSED) |
| Modèle produit | famille, marque / fabricant, nom, description ; pour les verres : gamme d'un fabricant (D-05-21) | aucune quantité, aucun prix |
| Variante | **SKU unique**, référence fabricant, code-barres facultatif, attributs (couleur, calibre…, ou indice + traitements pour les verres), **politique de stock** (`stocked` / `order_only` / `service`) | prix par magasin (§ 8.2), stock par emplacement (§ 8.3), seuil par magasin, fournisseurs (0..n, un principal) |
| Référentiel verres | fabricants, gammes, indices, traitements — configurables, **aucune valeur pré-remplie** | — |

Fiche `order_only` sans fournisseur principal : enregistrable, signalée « incomplète » ; fournisseur principal exigé à la commande effective (QR-01, PROPOSED). Aucune grille de puissances : les puissances viennent de la prescription au moment de la vente ou de la commande (R-05-07 inchangé).

### 8.2 Spécification du coefficient multiplicateur (D-05-16 — APPROVED ; arrondi PROPOSED)

**Données par (variante, magasin)**, montants dans la devise du magasin :

| Donnée | Type proposé | Règle |
|---|---|---|
| prix d'achat HT | `numeric(14,4)`, ≥ 0 | enregistré ; protégé (`costs.read` / `costs.write`) |
| coefficient | `numeric(8,4)`, > 0 et ≤ 100 | configurable par variante ; protégé (`costs.read`) car prix HT ÷ coefficient = prix d'achat ; < 1 (vente sous le coût) accepté seulement avec confirmation explicite (QR-12) |
| mode de prix | `coefficient` / `manual` | `manual` exige `pricing.manual` |
| prix de vente HT | `numeric(12,2)`, ≥ 0 | **stocké** (jamais recalculé à la lecture) |
| état de prix | calculé | `up_to_date` ; `recalculation_pending` (mode coefficient et prix stocké ≠ prix calculé à partir des valeurs courantes) ; `incomplete` (prix d'achat ou coefficient absent) |

**Calcul.** `prix de vente HT = arrondi(prix d'achat HT × coefficient)`. Le produit est calculé **exactement** en décimal (C# `decimal`, PostgreSQL `numeric`), **un seul arrondi**, à la fin. Proposition d'arrondi (QR-09) : au **centime** (2 décimales : unité mineure ISO 4217 de l'EUR comme du MAD), **demi-unité arrondie en s'éloignant de zéro** (`MidpointRounding.AwayFromZero` ; `round(numeric, 2)` de PostgreSQL a le même comportement sur `numeric`, ce qui permet un contrôle croisé). Exemples : 41,2500 × 2,5000 = 103,125 → **103,13** ; 19,9900 × 2,2000 = 43,978 → **43,98** ; 12,0000 × 2,0000 = 24,00. Un arrondi commercial supplémentaire (0,05, 0,10, terminaisons « ,90 ») n'est **pas** proposé ; s'il est souhaité, il devient une étape explicite et documentée après l'arrondi au centime.

**Recalcul explicite, jamais silencieux.**
1. Modification du prix d'achat ou du coefficient dans l'écran de tarif : le poste demande un **aperçu au serveur** (aucune écriture) ; l'enregistrement renvoie le prix aperçu (`expectedSalePriceHt`) ; le serveur recalcule et refuse en cas d'écart (`409 price_preview_stale`). Prix, coefficient, prix d'achat et historique sont écrits dans la même transaction.
2. Changement du prix d'achat **ailleurs** (modification par un détenteur de `costs.write` sans `pricing.write`, réception à un coût différent, nouveau prix fournisseur, import) : le **prix de vente ne change pas** ; la variante passe en `recalculation_pending` (ancien prix, prix recalculé proposé). Une réception ou un prix fournisseur ne modifie jamais d'eux-mêmes le prix d'achat HT de référence : la mise à jour est une action explicite.
3. Recalcul : action explicite (`pricing.write`), unitaire ou par lot avec **liste d'aperçu et confirmation** ; jamais de tâche de fond.
4. Prix manuel : saisi avec `pricing.manual` ; le coefficient enregistré est conservé mais non appliqué ; **coefficient effectif** (prix HT ÷ prix d'achat, 4 décimales) et marge affichés avec `costs.read` seulement. Retour au mode coefficient = recalcul explicite avec aperçu.

**Historisation.** Journal en ajout seul : chaque champ modifié (prix d'achat, coefficient, mode, prix de vente HT) → ancienne et nouvelle valeur, magasin, devise, origine (`edit`, `recalculation`, `manual_price`, `import`), auteur, date, identifiant de requête. Lignes de prix d'achat et de coefficient lisibles avec `costs.read` seulement. Audit sans valeur de coût (inchangé).

**Aucun flottant binaire.** Serveur en `decimal` / `numeric` ; montants et coefficients transmis en JSON comme **chaînes décimales à échelle fixe** (`"103.13"`, `"2.5000"`) ; le poste **ne calcule aucun montant** (aperçu serveur), il formate seulement ; import CSV lu en chaînes décimales ; tests de propriétés sur le calcul et l'arrondi.

**TTC calculé séparément.** Le TTC n'est jamais saisi ni stocké au catalogue. Il est calculé à la lecture **seulement si** le profil fiscal du magasin est validé **et** la variante a une catégorie fiscale dotée d'un taux validé : `TTC = arrondi_fiscal(HT × (1 + taux))`, règle d'arrondi fiscale à décider avec ADR-010. Sinon : TTC absent, `ttcStatus = tax_not_validated`, affichage « TTC indisponible — fiscalité du magasin non validée ». **Aucun taux ni catégorie fiscale n'est inventé.**

**Non-rétroactivité.** La Phase 06 copie sur la ligne de vente : SKU, libellé, politique, prix HT, mode, taux, TTC, devise, magasin. Aucune modification de prix, de coefficient ou de fiscalité ne touche une vente passée.

### 8.3 Magasins, emplacements, EUR / MAD et fiscalité non ratifiée (D-05-15, D-05-20 — APPROVED ; fiscalité non ratifiée)

| Élément | Spécification |
|---|---|
| Magasin | code, nom, pays (ISO 3166-1), **devise** (ISO 4217), statut du profil fiscal (`not_validated` / `validated`) ; créé par la commande de maintenance (pas d'écran en Phase 05) ; devise **non modifiable** dès qu'un prix ou un mouvement existe |
| Emplacement | rattaché à un magasin ; exactement un emplacement par défaut par magasin ; plusieurs emplacements possibles (structure), écrans de la Phase 05 limités à l'emplacement par défaut ; transferts OUT (BL-05-04) |
| Devise | portée par le magasin ; chaque prix enregistre sa devise ; contrainte SQL composite (magasin, devise) → impossible d'enregistrer un prix EUR pour un magasin MAD ; formats `1 234,50 €` / `1 234,50 MAD` |
| Conversion | **aucune** : pas de table de taux, pas de calcul croisé, aucun total multi-devises ; exports et listes groupés par magasin / devise ; prix fournisseur dans une autre devise que celle du magasin : non copiable, saisie manuelle du prix d'achat |
| Pilotes fictifs | Belgique — EUR ; Maroc — MAD. **Rien n'est créé maintenant** (aucun compte, aucune donnée dans PostgreSQL). Proposé : les tests réels créent les deux magasins fictifs dans `mmv_v2_test` après chaque G05-SQL, résidu 0 ; forme des pilotes réels ouverte (QR-11) |
| Contexte utilisateur | magasin actif choisi par le poste ; en Phase 05 tous les magasins sont accessibles aux détenteurs des permissions ; restriction par magasin = BL-05-07 |

**Configuration fiscale — structure seulement, non ratifiée.** Par magasin : statut du profil fiscal et catégories fiscales (code, libellé, taux **vide**, référence légale, validé le / par). Aucune ligne de taux n'est créée par la migration ; un taux ne peut être enregistré que sur la base d'une décision du propriétaire appuyée par un avis fiscal (ADR-010). Sont **ouverts** : régimes, taux et catégories applicables en Belgique et au Maroc, rattachement famille → catégorie fiscale, arrondi de la taxe (par ligne ou par document), mentions obligatoires. Tant que le profil n'est pas validé : prix HT seulement, TTC indisponible, toute vente réelle bloquée (Phase 06).

### 8.4 Disponibilité et réservations futures (D-05-17, D-05-19)

`disponible = en main − réservé`, par (variante, emplacement), agrégé par magasin ; réservé = 0 jusqu'à la Phase 06. La réservation naît à la **validation** d'une vente ou d'une commande client, jamais au devis. États : archivé → `archived` ; `service` → `available` ; `order_only` → `on_order` (ou `incomplete` sans fournisseur principal, QR-01) ; `stocked` → `in_stock` / `low_stock` (0 < disponible ≤ seuil du magasin) / `out_of_stock` (disponible = 0). Une sortie manuelle ne peut ni rendre la quantité négative (D-05-03) ni entamer le réservé (`422 stock_reserved`).

### 8.5 Séquence d'implémentation proposée (D-05-13 révisée — PROPOSED)

| Sous-lot | Contenu | Migration | Prérequis |
|---|---|---|---|
| **05-A** Référentiels, catalogue, tarifs, fournisseurs | magasins et emplacements (maintenance), structure fiscale vide, familles, référentiel verres, modèles / variantes, SKU / références / code-barres, politiques, prix par magasin (coefficient, manuel, aperçu, recalcul, historique), fournisseurs et rattachements, archivage, permissions `catalog.*` / `pricing.*` / `costs.*` / `suppliers.*`, audit ; écrans Lot 6 catalogue et fournisseurs + états GAP | `0007` (G05-SQL-A) | GO 05-A ; réponses QR-01, 02, 05, 07, 08, 09, 12 |
| **05-B** Stock par emplacement | soldes (en main, réservé = 0), journal de mouvements, **réceptions simples** (fournisseur, quantité, coût HT dans la devise du magasin), entrées / sorties / ajustements motivés, seuils et alertes de stock faible, disponibilité, contrats `IStockLedger` / `IStockReservations` pour la Phase 06 ; permissions `stock.*` | `0008` (G05-SQL-B) | 05-A accepté ; QR-03, QR-10 |
| **05-C** Inventaires et clôture | sessions par magasin (complètes / partielles), comptage, écarts justifiés, validation atomique, abandon ; exports CSV ; intégration réelle PostgreSQL / HTTPS / Tauri avec deux magasins fictifs EUR / MAD dans `mmv_v2_test` ; clôture | `0009` (G05-SQL-C) | 05-B accepté ; QR-04 |
| **05-D** Import CSV contrôlé | § 8.6 ; **avant le premier déploiement** | `0010` (G05-SQL-D) | 05-A au minimum ; autorisation pour toute donnée réelle |

Chaque sous-lot : GO explicite, interrupteur `VITE_MMV_CATALOG_MODULE`, tests .NET / PostgreSQL / frontend / HTTPS / natif, revue du propriétaire.

### 8.6 Import CSV contrôlé (D-05-18 — APPROVED ; détail PROPOSED)

1. **Dépôt** d'un fichier UTF-8 (séparateur `;`, en-têtes fixes par type : modèles / variantes, prix par magasin, fournisseurs, rattachements) ; taille et nombre de lignes bornés ; fichier conservé dans une zone de préparation, jamais exécuté.
2. **Contrôle à blanc** : analyse en chaînes, montants en décimal, devise = devise du magasin cible (sinon refus, aucune conversion), SKU / code-barres uniques, familles et référentiel verres existants, politiques admises ; rapport par ligne (créée / modifiée / inchangée / erreur) ; **aucune écriture** au catalogue.
3. **Application explicite** par `catalog.import` (+ `costs.write` si le fichier contient des prix d'achat ou coefficients, + `pricing.manual` pour des prix manuels), tout ou rien, idempotente (identifiant du lot), auditée ; prix existants jamais recalculés silencieusement (les écarts deviennent `recalculation_pending`).
4. Exports et rapports : neutralisation des cellules commençant par `=`, `+`, `-`, `@`.
5. Aucune donnée réelle importée sans autorisation écrite du propriétaire.

### 8.7 Divergences vérifiées

| Sujet | Avant (29/10) | Après validation (30/10) | Statut |
|---|---|---|---|
| Fournisseur principal des variantes commandées | obligatoire dès l'enregistrement | fiche enregistrable « incomplète » ; principal exigé à la commande effective (et à la validation d'une vente / commande client) | PROPOSED (QR-01) |
| Prix et fiscalité | prix TTC saisi, devise unique de l'installation, catégorie fiscale vide | prix HT = achat HT × coefficient, par magasin ; TTC calculé séparément, seulement avec fiscalité validée | APPROVED ; arrondi PROPOSED ; fiscalité non ratifiée |
| Disponibilité et réservations | contrat seulement, réservé = 0 | réservation à la validation d'une vente / commande, jamais au devis ; formule fixée, réservé = 0 en Phase 05 | APPROVED (règle) ; QR-10 |
| Comptage / validation d'inventaire | `stock.inventory.count` ≠ `stock.inventory.validate` | famille de permissions « inventaires » distincte (Q16) ; séparation `inventory.count` / `inventory.validate` recommandée | APPROVED (principe) ; séparation PROPOSED (QR-05) |
| Deux magasins EUR / MAD | devise unique par installation, multi-magasin OUT | magasin porteur de la devise ; architecture multi-magasins / emplacements préparée ; pilotes fictifs BE-EUR et MA-MAD ; aucune conversion | APPROVED |
