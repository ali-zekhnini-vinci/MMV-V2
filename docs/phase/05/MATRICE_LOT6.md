# 05 — Matrice Lot 6 → fonctionnalités → API

Source : `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` (DRAFT, empreinte `6ca2cec0…bc35e7`, 175 994 octets), catalogue `MMV_V2_Registre_134_Groupes.csv` (B-098 à B-134, **37 groupes**) et `MMV_V2_Registre_324_Etats.csv` (`MMV-L06-*`, **107 états** ; la maquette compte 108 `data-mount`, le 108ᵉ étant les notes de conception, hors inventaire).

> **Mise à jour 30/10/2026.** D-05-01 est `APPROVED` : le Lot 6 est une référence visuelle et ergonomique seulement ; copie de travail et index dans `references/phase05/` (`INDEX_LOT6.md`, `SHA256SUMS.txt`). Les §§ 1 à 3 sont l'état du 29/10/2026, conservé ; les routes et permissions qu'ils citent sont remplacées par [`API_PROPOSED.md`](API_PROPOSED.md) révisé. Correspondance avec les futurs modules et dispositions révisées : **§ 4** ; nouveaux états sans planche : **§ 5**.

Dispositions : **ADOPT** (repris tel quel côté interface) · **ADAPT** (repris avec une règle V2 différente) · **REPLACE** (la planche illustre une limite de l'ancien logiciel ; remplacée par un état V2) · **DEFER** (hors Phase 05). Routes : [`API_PROPOSED.md`](API_PROPOSED.md). Permissions : D-05-11. Toutes les dispositions sont `PROPOSED` (D-05-01).

## 1. Groupes

| Groupe | États (`data-mount`) | Fonctionnalité V2 | API | Permission | Lot | Disposition |
|---|---|---|---|---|---|---|
| B-098 Liste des produits | catLight, catDark | liste paginée ; colonnes coût / marge présentes seulement si le serveur les renvoie | `GET /api/catalog/products` | `catalog.read` (+ `catalog.costs.read`) | 05-A | ADAPT — colonnes redéfinies (politique, disponibilité), pas de DTO hérité |
| B-099 Recherche & filtres | catSearch, catFilters | recherche référence / désignation / marque / EAN ; filtres catégorie, fournisseur, politique, disponibilité, statut | idem, paramètres `q`, `categoryId`, `supplierId`, `policy`, `availability`, `status` | `catalog.read` | 05-A | ADAPT — filtre « politique » ajouté |
| B-100 Tri · Liste compacte | catSort, catCompact | tri serveur par colonne, densité réduite (préférence locale) | `sort`, `dir` | `catalog.read` | 05-A | ADOPT |
| B-101 États transverses | catEmpty, catNoResult, catLoading, catError, catCsv | vide / aucun résultat / chargement / erreur ; export CSV | `GET /api/catalog/products/export.csv` | `catalog.read` (+ coûts) | 05-A (états), 05-C (CSV) | ADOPT ; CSV ADAPT (coûts filtrés) |
| B-102 Nouveau produit | prodNewLight, prodNewDark | formulaire par sections : identité, catégorie + attributs, politique, prix, stock (seuil), fournisseurs | `POST /api/catalog/products` | `catalog.write` (+ `catalog.costs.write` pour les coûts) | 05-A | ADAPT — catégories en liste fermée (D-05-05), politique obligatoire (D-05-02) |
| B-103 Erreurs · Modification | prodErrors, prodEdit | validation sous les champs (codes serveur `422` par champ) ; modification pré-remplie | `PUT /api/catalog/products/{id}` | `catalog.write` | 05-A | ADOPT |
| B-104 Non enregistré · Fermeture | prodUnsaved, prodClose | garde de navigation (composant existant Clients) | — | — | 05-A | ADOPT |
| B-105 Succès · Conflit · Désactivé | prodCreateOk, prodEditOk, prodConflict, prodDeactivated | succès ; conflit `409` (version `xmin`) avec rechargement ; « désactivé » → **archivé** | idem | `catalog.write` | 05-A | ADAPT — « désactivé » remplacé par « archivé » (D-05-07) |
| B-106 Détail produit | detLight, detDark | fiche article | `GET /api/catalog/products/{id}` | `catalog.read` | 05-A | ADOPT |
| B-107 Blocs de la fiche | detGeneral, detHistory | informations, prix & disponibilité, **fournisseurs** (plusieurs), historique de stock | `GET …/{id}`, `GET …/{id}/suppliers`, `GET /api/stock/movements?productId=` | `catalog.read`, `suppliers.read`, `stock.read` | 05-A (27-29), 05-B (30) | ADAPT — plusieurs fournisseurs (D-05-06) ; historique avec avant / après et auteur (D-05-09) |
| B-108 Variantes d'état | detLow, detRupture, detDeactivated, detVendeur | stock faible, rupture, archivé, lecture seule (droits, pas rôle) | idem | selon droits effectifs | 05-B (31-32), 05-A (33-34) | ADAPT — « lecture seule » déduite des permissions, jamais du profil |
| B-109 Menu · Suppression · Refus | delMenu, delConfirm, delRefused | menu d'actions ; **pas de suppression** : « Archiver » ; refus d'archivage expliqué (stock > 0, inventaire ouvert) | `POST …/{id}/archive` | `catalog.archive` | 05-A | **REPLACE** — suppression physique remplacée par l'archivage (D-05-07) |
| B-110 Désactivation · Réactivation | delDeactivate, delDeactivateConfirm, delReactivate | confirmation d'archivage, réactivation | `POST …/{id}/archive`, `…/reactivate` | `catalog.archive` | 05-A | ADAPT — vocabulaire « archiver » |
| B-111 Erreur métier · technique | delErrBiz, delErrTech | `422` avec code métier ; erreur technique générique | — | — | 05-A | ADOPT |
| B-112 Vue globale du stock | stockLight, stockDark | articles `stocked` : quantité, seuil, disponibilité | `GET /api/stock/balances` | `stock.read` | 05-B | ADAPT — `order_only` / `service` exclus, accessibles par filtre de politique au catalogue |
| B-113 Filtres et regroupements | stockLow, stockRupture, stockByCat, stockSearch | filtres faible / rupture, regroupement par catégorie, recherche | idem, `availability`, `categoryId`, `q`, `groupBy=category` | `stock.read` | 05-B | ADOPT |
| B-114 Rafraîchissement · Autre poste | stockRefresh, stockConflict | « dernier état chargé à HH:MM », Actualiser ; conflit **réel** à l'action (`409`) | `asOf` dans la réponse | `stock.read` | 05-B | ADAPT — conflit détecté par le serveur, plus une « cible UI » |
| B-115 États transverses · CSV | stockEmpty, stockLoading, stockError, stockCsv | états ; export CSV | `GET /api/stock/balances/export.csv` | `stock.read` | 05-B (états), 05-C (CSV) | ADOPT |
| B-116 Historique des mouvements | movLight, movDark | journal paginé | `GET /api/stock/movements` | `stock.read` | 05-B | ADAPT — colonnes avant / après et auteur (D-05-09) |
| B-117 Filtres · Détail | movFilters, movDetail | filtres type / article / période / auteur ; fiche mouvement (source, motif, coût si autorisé) | `GET /api/stock/movements/{id}` | `stock.read` (+ coûts) | 05-B | ADAPT — coût de réception protégé |
| B-118 Entrée · Sortie · Ajustement | movEntry, movExit, movAdjust | réception / entrée manuelle, sortie manuelle, ajustement en quantité constatée | `POST /api/stock/movements` (`receipt`, `manual_in`, `manual_out`), `POST /api/stock/adjustments` | `stock.movements.write`, `stock.adjust` | 05-B | ADAPT — ajustement : quantité constatée + quantité attendue, écart calculé (D-05-09) |
| B-119 Garde-fous | movAdjustConfirm, movReasonReq, movQtyInvalid | confirmation, motif codé obligatoire, quantité entière > 0 / ≥ 0 | validations `422` | idem | 05-B | ADOPT |
| B-120 Succès · Conflit · Erreurs | movSuccess, movConflict, movErrBiz, movErrTech | succès ; `409 stock_changed` ; `422 stock_insufficient` ; technique | idem | idem | 05-B | ADOPT |
| B-121 Liste · Nouvel inventaire | invList, invNew | sessions **persistées** ; ouverture avec périmètre et option aveugle | `GET/POST /api/stock/inventories` | `stock.read`, `stock.inventory.count` | 05-C | **REPLACE** — « historique reconstitué (cible) » remplacé par des sessions réelles (D-05-10) |
| B-122 Comptage · Comparaison | invInProgress, invCount, invCompare | saisie par ligne (version), théorique mémorisé au comptage | `PUT /api/stock/inventories/{id}/lines/{productId}` | `stock.inventory.count` | 05-C | ADAPT |
| B-123 Écarts · Confirmation · Terminé | invDiffs, invConfirm, invDone | écarts seuls, motifs, validation atomique, clôture | `POST …/{id}/validate` | `stock.inventory.validate` | 05-C | ADAPT — permission distincte du comptage |
| B-124 Abandon · Conflit · Historique | invAbandon, invConflict, invHistory | abandon irréversible ; lignes modifiées depuis le comptage ; historique réel | `POST …/{id}/abandon`, `GET …/{id}` | `stock.inventory.validate`, `stock.read` | 05-C | ADAPT — conflit détecté par ligne ; historique stocké |
| B-125 Liste des fournisseurs | supListLight, supListDark | liste paginée avec statut | `GET /api/suppliers` | `suppliers.read` | 05-A | ADAPT — colonne statut ajoutée |
| B-126 Recherche · Filtres | supSearch, supFilters | recherche nom / code / e-mail ; filtres produits associés, pays, statut | idem | `suppliers.read` | 05-A | ADAPT |
| B-127 Formulaire fournisseur | supNew, supErrors, supEdit, supUnsaved | formulaire (champs D-05-06), erreurs sous champs, garde de navigation | `POST/PUT /api/suppliers…` | `suppliers.write` | 05-A | ADAPT — identifiants légaux selon le pays, notes |
| B-128 Détail · Produits associés | supDetailLight, supDetailDark, supProducts, supNoProduct | fiche ; articles rattachés (référence fournisseur, principal, coût si autorisé) | `GET /api/suppliers/{id}`, `…/{id}/products` | `suppliers.read` (+ coûts) | 05-A | ADAPT |
| B-129 Suppression · Refus · Désactivation indisponible | supDelete, supDeleteRefused, supNoDeactivate | **archivage / réactivation** ; refus expliqué (unique principal d'un article sur commande) | `POST /api/suppliers/{id}/archive`, `…/reactivate` | `suppliers.write` | 05-A | **REPLACE** — fournisseur doté d'un statut (D-05-06, D-05-07) |
| B-130 États transverses · CSV | supEmpty, supNoResult, supLoading, supErrBiz, supErrTech, supCsv | états ; export CSV | `GET /api/suppliers/export.csv` | `suppliers.read` | 05-A (états), 05-C (CSV) | ADOPT |
| B-131 Fenêtres réduites | a11yCatalogue, a11yStock, a11yDetail, a11ySuppliers | colonnes prioritaires, aucun défilement horizontal global | — | — | 05-A / 05-B | ADOPT (contrôle à 1024×700 comme en Phase 04) |
| B-132 Parcours clavier | a11yProdKbd, a11ySupKbd | formulaires au clavier | — | — | 05-A | ADOPT |
| B-133 Focus · Erreur · Colonnes | a11yTabOrder, a11yFocus, a11yFieldErr, a11yPriority | ordre de tabulation, anneau visible, erreur sous champ, table condensée | — | — | 05-A / 05-B | ADOPT |
| B-134 Sans souris | a11yDestructive, a11yNoMouse | confirmation d'archivage et mouvement de stock au clavier | — | — | 05-A / 05-B | ADAPT — « destructive » = archivage |

Couverture : **37 / 37 groupes, 107 / 107 états** affectés ; 3 REPLACE (B-109, B-121, B-129), 20 ADAPT, 14 ADOPT (B-101, B-115 et B-130 comptés ADOPT pour leurs états ; leur export CSV est ADAPT). Aucune planche n'est DEFER : les exports CSV, dessinés comme « cible », sont proposés en 05-C.

## 2. États V2 sans planche (à composer à partir des composants existants, validation visuelle requise)

| ID | État | Lot |
|---|---|---|
| GAP-05-01 | choix / changement de politique d'article, refus de changement (stock > 0) | 05-A |
| GAP-05-02 | fournisseurs multiples d'un article, choix du principal | 05-A |
| GAP-05-03 | historique des prix (avec / sans droit coûts) | 05-A |
| GAP-05-04 | article archivé avec réactivation, refus d'archivage (stock > 0) | 05-A |
| GAP-05-05 | réception fournisseur (fournisseur, quantité, coût) | 05-B |
| GAP-05-06 | fiche article `order_only` / `service` (pas de bloc stock) | 05-A |
| GAP-05-07 | ligne d'inventaire modifiée depuis le comptage : recompter / accepter | 05-C |
| GAP-05-08 | lignes non comptées à la validation : exclure / mettre à 0 | 05-C |
| GAP-05-09 | comptage à l'aveugle | 05-C |

## 3. Écarts volontaires avec la maquette (D-05-01)

| Maquette | V2 proposé | Décision |
|---|---|---|
| catégories figées issues d'une énumération héritée | liste fermée fournie par le propriétaire | D-05-05 |
| un fournisseur obligatoire par article | 0..n fournisseurs, principal obligatoire pour `order_only` | D-05-06 |
| suppression physique, refus par clé étrangère | archivage seulement | D-05-07 |
| fournisseur sans statut, sans identifiants légaux | statut actif / archivé, identifiants selon le pays | D-05-06 |
| ajustement « valeur absolue » sans quantité avant / après | quantité constatée + attendue, avant / après conservés | D-05-09 |
| historique d'inventaire « reconstitué » | sessions persistées | D-05-10 |
| conflit multi-poste « cible UI, aucun jeton » | versions et verrous serveur, `409` | IMPLEMENTATION_SPEC § 4 |
| seuil d'alerte par défaut | aucun défaut imposé | § 3.1 du cadrage |
| visibilité des prix par rôle (masquage d'interface) | permissions serveur, champs retirés des réponses | D-05-11 |
| politique de stock absente | trois politiques | D-05-02 |

## 4. Correspondance Lot 6 ↔ futurs modules (révision 30/10/2026, après G05-METIER)

| Module V2 | Groupes Lot 6 | États | Sous-lot | Révision de disposition (décision) |
|---|---|---|---|---|
| **Catalogue** (modèles, variantes, familles, référentiel verres) | B-098 à B-111 | 37 | 05-A | ADAPT renforcé : liste au niveau **modèle** avec variantes dépliables, colonne SKU (D-05-04, D-05-14) ; filtre « famille » sur six familles (D-05-05) ; formulaire B-102 / B-103 en deux niveaux modèle → variantes ; politique par variante (D-05-02) ; B-109 / B-110 restent REPLACE / ADAPT vers l'archivage (D-05-07, QR-02) |
| **Tarifs** | blocs prix de B-102, B-106, B-107 | (inclus dans les 37) | 05-A | **ADAPT** : prix de vente HT par magasin, prix d'achat HT et coefficient (si `costs.read`), aperçu serveur, état « recalcul en attente », prix manuel ; TTC affiché seulement si fiscalité validée (D-05-16). Le Lot 6 ne dessine ni coefficient ni magasin : GAP-05-10 à 13 |
| **Fournisseurs** | B-125 à B-130 | 21 | 05-A | ADAPT : rattachements **par variante**, prix fournisseur avec devise, principal unique ; B-129 : l'archivage du dernier principal n'est plus refusé mais rend les variantes `order_only` « incomplètes » (QR-01) |
| **Stock** (soldes, mouvements, réceptions simples, alertes) | B-112 à B-120 (+ bloc historique de B-107 et états stock de B-108, comptés au Catalogue) | 26 | 05-B | ADAPT : vue par **magasin** (emplacement par défaut), colonnes en main / réservé / disponible (D-05-17), filtre « alertes » (D-05-19) ; B-118 « entrée » = réception simple avec fournisseur et coût HT protégé (Q12) |
| **Inventaires** | B-121 à B-124 | 11 | 05-C | ADAPT : session par magasin, complète ou partielle (Q10), écarts justifiés ; permissions `inventory.count` / `inventory.validate` (QR-05) |
| **Transverse** (accessibilité, fenêtres réduites) | B-131 à B-134 | 12 | 05-A à 05-C | ADOPT inchangé |
| **Exports CSV** | états CSV de B-101, B-115, B-130 | (inclus) | 05-C | ADAPT : montants avec devise par ligne, aucun total multi-devises |
| **Magasins / fiscalité** | — (aucune planche) | 0 | 05-A | administration par la commande de maintenance ; sélecteur de magasin actif = GAP-05-12 |
| **Import CSV** | — (aucune planche) | 0 | 05-D | GAP-05-15 |

Contrôle (décompte scripté des `data-mount` du § 1, 30/10/2026) : Catalogue B-098 à B-111 (14 groupes, 37 états) + Stock B-112 à B-120 (9 groupes, 26 états) + Inventaires B-121 à B-124 (4 groupes, 11 états) + Fournisseurs B-125 à B-130 (6 groupes, 21 états) + Transverse B-131 à B-134 (4 groupes, 12 états) = **37 groupes, 107 états**, chaque état compté une seule fois. Index détaillé par état : `references/phase05/INDEX_LOT6.md`.

## 5. Nouveaux états V2 sans planche (30/10/2026)

Comme GAP-05-01 à 09 (§ 2), **aucune planche n'est fabriquée** : composition à partir des composants V2 existants et validation visuelle (G05-DESIGN).

| ID | État | Lot | Décision |
|---|---|---|---|
| GAP-05-10 | grille des variantes d'un modèle (ajout, SKU, politique, archivage d'une variante) | 05-A | D-05-04 |
| GAP-05-11 | tarif d'une variante : prix d'achat HT, coefficient, aperçu, enregistrement ; avec / sans `costs.read` | 05-A | D-05-16 |
| GAP-05-12 | sélecteur du magasin actif ; affichage de la devise ; « TTC indisponible — fiscalité non validée » | 05-A | D-05-15 |
| GAP-05-13 | « recalcul en attente », recalcul unitaire et par lot (aperçu + confirmation) ; prix manuel | 05-A | D-05-16 |
| GAP-05-14 | référentiel verres : fabricants, gammes, indices, traitements | 05-A | D-05-21 |
| GAP-05-15 | import CSV : dépôt, rapport de contrôle à blanc, application | 05-D | D-05-18 |
| GAP-05-16 | fiche `order_only` « incomplète — fournisseur principal manquant » (liste, filtre, fiche) | 05-A | QR-01 |
| GAP-05-17 | liste des alertes de stock faible et réglage du seuil par magasin | 05-B | D-05-19 |
