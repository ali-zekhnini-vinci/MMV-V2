# Index Lot 6 — groupes, écrans et états

Généré le 30/10/2026 à partir de `references/phase04/catalogue/MMV_V2_Registre_134_Groupes.csv` et `MMV_V2_Registre_324_Etats.csv` (lignes Lot 6 seulement), contrôlé contre les attributs `data-mount` de `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`. Référence **visuelle et ergonomique seulement** (D-05-01) : aucune liste de champs, énumération ou règle n'en est tirée.

Contrôle : **37 groupes**, **107 états** `MMV-L06-*` ; 108 attributs `data-mount` dans la maquette (108 distincts) ; états du registre absents de la maquette : 0 ; montages hors registre : `notes`.

Module V2 : rattachement indicatif après la validation métier du 30/10/2026 ; disposition détaillée dans `docs/phase/05/MATRICE_LOT6.md`.

## 1. Groupes

| Groupe | Fonction | Planches | Comportement illustré | Module V2 |
|---|---|---|---|---|
| B-098 | Liste des produits | 01·02 | Modes clair et sombre · rôle Responsable / Opticien | Catalogue (modèles, variantes) + Tarifs |
| B-099 | Recherche & filtres | 03·04·05·06·07 | Filtre catégorie · fournisseur · disponibilité — panneau ouvert | Catalogue (modèles, variantes) + Tarifs |
| B-100 | Tri · Liste compacte | 08·09 | Tri par colonne · densité réduite | Catalogue (modèles, variantes) + Tarifs |
| B-101 | Liste vide · Aucun résultat · Chargement · Erreur · Export CSV | 10·11·12·13·14 | États transverses | Catalogue (modèles, variantes) + Tarifs |
| B-102 | Nouveau produit | 15·16 | Modes clair et sombre · catégorie Monture | Catalogue (modèles, variantes) + Tarifs |
| B-103 | Formulaire avec erreurs · Modification | 17·18 | Validation sous les champs · produit pré-rempli | Catalogue (modèles, variantes) + Tarifs |
| B-104 | Modifications non enregistrées · Confirmation de fermeture | 19·20 | Barre d'action active · avertissement | Catalogue (modèles, variantes) + Tarifs |
| B-105 | Création réussie · Modification réussie · Conflit · Produit désactivé | 21·22·23·24 | Succès · conflit multi-poste · IsActive = false | Catalogue (modèles, variantes) + Tarifs |
| B-106 | Détail Produit | 25·26 | Modes clair et sombre · rôle Responsable / Opticien | Catalogue (modèles, variantes) + Tarifs |
| B-107 | Informations · Prix & disponibilité · Fournisseur · Historique de stock | 27·28·29·30 | Blocs de la fiche (l'historique en vue étendue) | Catalogue (modèles, variantes) + Tarifs |
| B-108 | Stock faible · Rupture · Désactivé · Lecture seule (Vendeur) | 31·32·33·34 | Variantes d'état · visibilité par rôle | Catalogue (modèles, variantes) + Tarifs |
| B-109 | Menu d'actions · Suppression autorisée · Refus (historique) | 35·36·37 | Actions par produit · confirmation · refus expliqué | Catalogue (modèles, variantes) + Tarifs |
| B-110 | Désactivation · Confirmation · Réactivation | 38·39·40 | IsActive = false / true | Catalogue (modèles, variantes) + Tarifs |
| B-111 | Erreur métier · Erreur technique | 41·42 | Règle refusée · échec technique | Catalogue (modèles, variantes) + Tarifs |
| B-112 | Vue globale du stock | 43·44 | Modes clair et sombre | Stock (vue par magasin / emplacement) |
| B-113 | Stock faible · Rupture · Par catégorie · Recherche | 45·46·47·48 | Filtres et regroupements | Stock (vue par magasin / emplacement) |
| B-114 | Dernier rafraîchissement · Modifié sur un autre poste | 49·50 | Actualiser · conflit multi-poste (cible) | Stock (vue par magasin / emplacement) |
| B-115 | État vide · Chargement · Erreur · Export CSV | 51·52·53·54 | États transverses | Stock (vue par magasin / emplacement) |
| B-116 | Historique des mouvements | 55·56 | Modes clair et sombre | Stock (mouvements, réceptions simples) |
| B-117 | Filtres · Détail d'un mouvement | 57·58·59·60 | Type / produit / période · fiche mouvement | Stock (mouvements, réceptions simples) |
| B-118 | Entrée · Sortie · Ajustement | 61·62·63 | Saisie manuelle par type de mouvement | Stock (mouvements, réceptions simples) |
| B-119 | Confirmation d'ajustement · Motif obligatoire · Quantité invalide | 64·65·66 | Garde-fous de saisie | Stock (mouvements, réceptions simples) |
| B-120 | Mouvement réussi · Conflit · Erreur métier · Erreur technique | 67·68·69·70 | Succès · stock insuffisant · échecs | Stock (mouvements, réceptions simples) |
| B-121 | Liste des inventaires · Nouvel inventaire | 71·72 | Historique reconstitué (cible) · lancement d'un comptage | Inventaires |
| B-122 | Inventaire en cours · Saisie d'une quantité · Comparaison | 73·74·75 | Comptage physique · quantité constatée · stock théorique vs constaté | Inventaires |
| B-123 | Liste des écarts · Confirmation des ajustements · Inventaire terminé | 76·77·78 | Écarts seuls · motif obligatoire · clôture | Inventaires |
| B-124 | Abandon · Conflit multi-poste · Historique d'un inventaire | 79·80·81 | Abandon irréversible · stock modifié ailleurs · trace (cible) | Inventaires |
| B-125 | Liste des fournisseurs | 82·83 | Modes clair et sombre | Fournisseurs |
| B-126 | Recherche · Filtres | 84·85 | Recherche nom / référence / email · produits associés | Fournisseurs |
| B-127 | Nouveau · Erreurs · Modification · Modifications non enregistrées | 86·87·88·89 | Formulaire fournisseur (champs réels) · validation sous les champs | Fournisseurs |
| B-128 | Détail · Produits associés · Sans produit | 90·91·92·93 | Fiche fournisseur (clair / sombre) · rattachements | Fournisseurs |
| B-129 | Suppression autorisée · Refus (utilisé) · Désactivation indisponible | 94·95·96 | Sûre sans produit · refusée par clé étrangère · pas de champ actif | Fournisseurs |
| B-130 | États transverses · Export CSV | 97·98·99·100·101·102 | Vide · aucun résultat · chargement · erreur métier · erreur technique · CSV (cible) | Fournisseurs |
| B-131 | Fenêtres réduites | 103·104·105·106 | Catalogue · stock · détail produit · liste fournisseur — colonnes prioritaires | Transverse (accessibilité) |
| B-132 | Parcours clavier | 107·108 | Formulaire produit · formulaire fournisseur — au clavier | Transverse (accessibilité) |
| B-133 | Tabulation · Focus visible · Erreur sous champ · Colonnes prioritaires | 109·110·111·112 | Ordre de focus · anneau 3px · message sous le champ · table condensée | Transverse (accessibilité) |
| B-134 | Sans souris | 113·114 | Confirmation destructive accessible · mouvement de stock au clavier | Transverse (accessibilité) |

## 2. Écrans et états

| Section | Écran | Planches | État (variante) | `data-mount` | Identifiant |
|---|---|---|---|---|---|
| Catalogue Produits | Liste des produits | 01·02 | 01 · Mode clair | `catLight` | `MMV-L06-catLight` |
| Catalogue Produits | Liste des produits | 01·02 | 02 · Mode sombre | `catDark` | `MMV-L06-catDark` |
| Catalogue Produits | Recherche & filtres | 03·04·05·06·07 | 03 · Recherche | `catSearch` | `MMV-L06-catSearch` |
| Catalogue Produits | Recherche & filtres | 03·04·05·06·07 | 04·05·06·07 · Filtres (catégorie / fournisseur / disponibilité) | `catFilters` | `MMV-L06-catFilters` |
| Catalogue Produits | Tri · Liste compacte | 08·09 | 08 · Tri (prix de vente décroissant) | `catSort` | `MMV-L06-catSort` |
| Catalogue Produits | Tri · Liste compacte | 08·09 | 09 · Liste compacte | `catCompact` | `MMV-L06-catCompact` |
| Catalogue Produits | Liste vide · Aucun résultat · Chargement · Erreur · Export CSV | 10·11·12·13·14 | 10 · Liste vide | `catEmpty` | `MMV-L06-catEmpty` |
| Catalogue Produits | Liste vide · Aucun résultat · Chargement · Erreur · Export CSV | 10·11·12·13·14 | 11 · Aucun résultat | `catNoResult` | `MMV-L06-catNoResult` |
| Catalogue Produits | Liste vide · Aucun résultat · Chargement · Erreur · Export CSV | 10·11·12·13·14 | 12 · Chargement | `catLoading` | `MMV-L06-catLoading` |
| Catalogue Produits | Liste vide · Aucun résultat · Chargement · Erreur · Export CSV | 10·11·12·13·14 | 13 · Erreur technique | `catError` | `MMV-L06-catError` |
| Catalogue Produits | Liste vide · Aucun résultat · Chargement · Erreur · Export CSV | 10·11·12·13·14 | 14 · Export CSV (cible V1) | `catCsv` | `MMV-L06-catCsv` |
| Création et modification Produit | Nouveau produit | 15·16 | 15 · Mode clair | `prodNewLight` | `MMV-L06-prodNewLight` |
| Création et modification Produit | Nouveau produit | 15·16 | 16 · Mode sombre | `prodNewDark` | `MMV-L06-prodNewDark` |
| Création et modification Produit | Formulaire avec erreurs · Modification | 17·18 | 17 · Formulaire avec erreurs | `prodErrors` | `MMV-L06-prodErrors` |
| Création et modification Produit | Formulaire avec erreurs · Modification | 17·18 | 18 · Modification (VER-1610) | `prodEdit` | `MMV-L06-prodEdit` |
| Création et modification Produit | Modifications non enregistrées · Confirmation de fermeture | 19·20 | 19 · Modifications non enregistrées | `prodUnsaved` | `MMV-L06-prodUnsaved` |
| Création et modification Produit | Modifications non enregistrées · Confirmation de fermeture | 19·20 | 20 · Confirmation de fermeture | `prodClose` | `MMV-L06-prodClose` |
| Création et modification Produit | Création réussie · Modification réussie · Conflit · Produit désactivé | 21·22·23·24 | 21 · Création réussie | `prodCreateOk` | `MMV-L06-prodCreateOk` |
| Création et modification Produit | Création réussie · Modification réussie · Conflit · Produit désactivé | 21·22·23·24 | 22 · Modification réussie | `prodEditOk` | `MMV-L06-prodEditOk` |
| Création et modification Produit | Création réussie · Modification réussie · Conflit · Produit désactivé | 21·22·23·24 | 23 · Conflit de modification | `prodConflict` | `MMV-L06-prodConflict` |
| Création et modification Produit | Création réussie · Modification réussie · Conflit · Produit désactivé | 21·22·23·24 | 24 · Produit désactivé | `prodDeactivated` | `MMV-L06-prodDeactivated` |
| Détail Produit | Détail Produit | 25·26 | 25 · Mode clair | `detLight` | `MMV-L06-detLight` |
| Détail Produit | Détail Produit | 25·26 | 26 · Mode sombre | `detDark` | `MMV-L06-detDark` |
| Détail Produit | Informations · Prix & disponibilité · Fournisseur · Historique de stock | 27·28·29·30 | 27·28·29 · Informations, prix & fournisseur | `detGeneral` | `MMV-L06-detGeneral` |
| Détail Produit | Informations · Prix & disponibilité · Fournisseur · Historique de stock | 27·28·29·30 | 30 · Historique de stock (étendu) | `detHistory` | `MMV-L06-detHistory` |
| Détail Produit | Stock faible · Rupture · Désactivé · Lecture seule (Vendeur) | 31·32·33·34 | 31 · Stock faible | `detLow` | `MMV-L06-detLow` |
| Détail Produit | Stock faible · Rupture · Désactivé · Lecture seule (Vendeur) | 31·32·33·34 | 32 · Rupture | `detRupture` | `MMV-L06-detRupture` |
| Détail Produit | Stock faible · Rupture · Désactivé · Lecture seule (Vendeur) | 31·32·33·34 | 33 · Désactivé | `detDeactivated` | `MMV-L06-detDeactivated` |
| Détail Produit | Stock faible · Rupture · Désactivé · Lecture seule (Vendeur) | 31·32·33·34 | 34 · Lecture seule (Vendeur) | `detVendeur` | `MMV-L06-detVendeur` |
| Suppression et désactivation | Menu d'actions · Suppression autorisée · Refus (historique) | 35·36·37 | 35 · Menu d'actions | `delMenu` | `MMV-L06-delMenu` |
| Suppression et désactivation | Menu d'actions · Suppression autorisée · Refus (historique) | 35·36·37 | 36 · Confirmation de suppression | `delConfirm` | `MMV-L06-delConfirm` |
| Suppression et désactivation | Menu d'actions · Suppression autorisée · Refus (historique) | 35·36·37 | 37 · Refus de suppression (historique) | `delRefused` | `MMV-L06-delRefused` |
| Suppression et désactivation | Désactivation · Confirmation · Réactivation | 38·39·40 | 38 · Proposition de désactivation | `delDeactivate` | `MMV-L06-delDeactivate` |
| Suppression et désactivation | Désactivation · Confirmation · Réactivation | 38·39·40 | 39 · Confirmation de désactivation | `delDeactivateConfirm` | `MMV-L06-delDeactivateConfirm` |
| Suppression et désactivation | Désactivation · Confirmation · Réactivation | 38·39·40 | 40 · Réactivation | `delReactivate` | `MMV-L06-delReactivate` |
| Suppression et désactivation | Erreur métier · Erreur technique | 41·42 | 41 · Erreur métier | `delErrBiz` | `MMV-L06-delErrBiz` |
| Suppression et désactivation | Erreur métier · Erreur technique | 41·42 | 42 · Erreur technique | `delErrTech` | `MMV-L06-delErrTech` |
| Vue Stock | Vue globale du stock | 43·44 | 43 · Mode clair | `stockLight` | `MMV-L06-stockLight` |
| Vue Stock | Vue globale du stock | 43·44 | 44 · Mode sombre | `stockDark` | `MMV-L06-stockDark` |
| Vue Stock | Stock faible · Rupture · Par catégorie · Recherche | 45·46·47·48 | 45 · Produits en stock faible | `stockLow` | `MMV-L06-stockLow` |
| Vue Stock | Stock faible · Rupture · Par catégorie · Recherche | 45·46·47·48 | 46 · Produits en rupture | `stockRupture` | `MMV-L06-stockRupture` |
| Vue Stock | Stock faible · Rupture · Par catégorie · Recherche | 45·46·47·48 | 47 · Stock par catégorie | `stockByCat` | `MMV-L06-stockByCat` |
| Vue Stock | Stock faible · Rupture · Par catégorie · Recherche | 45·46·47·48 | 48 · Recherche dans le stock | `stockSearch` | `MMV-L06-stockSearch` |
| Vue Stock | Dernier rafraîchissement · Modifié sur un autre poste | 49·50 | 49 · Dernier rafraîchissement | `stockRefresh` | `MMV-L06-stockRefresh` |
| Vue Stock | Dernier rafraîchissement · Modifié sur un autre poste | 49·50 | 50 · Stock modifié sur un autre poste | `stockConflict` | `MMV-L06-stockConflict` |
| Vue Stock | État vide · Chargement · Erreur · Export CSV | 51·52·53·54 | 51 · État vide | `stockEmpty` | `MMV-L06-stockEmpty` |
| Vue Stock | État vide · Chargement · Erreur · Export CSV | 51·52·53·54 | 52 · Chargement | `stockLoading` | `MMV-L06-stockLoading` |
| Vue Stock | État vide · Chargement · Erreur · Export CSV | 51·52·53·54 | 53 · Erreur technique | `stockError` | `MMV-L06-stockError` |
| Vue Stock | État vide · Chargement · Erreur · Export CSV | 51·52·53·54 | 54 · Export CSV | `stockCsv` | `MMV-L06-stockCsv` |
| Mouvements de stock | Historique des mouvements | 55·56 | 55 · Mode clair | `movLight` | `MMV-L06-movLight` |
| Mouvements de stock | Historique des mouvements | 55·56 | 56 · Mode sombre | `movDark` | `MMV-L06-movDark` |
| Mouvements de stock | Filtres · Détail d'un mouvement | 57·58·59·60 | 57·58·59 · Filtres (type / produit / période) | `movFilters` | `MMV-L06-movFilters` |
| Mouvements de stock | Filtres · Détail d'un mouvement | 57·58·59·60 | 60 · Détail d'un mouvement | `movDetail` | `MMV-L06-movDetail` |
| Mouvements de stock | Entrée · Sortie · Ajustement | 61·62·63 | 61 · Entrée manuelle | `movEntry` | `MMV-L06-movEntry` |
| Mouvements de stock | Entrée · Sortie · Ajustement | 61·62·63 | 62 · Sortie manuelle | `movExit` | `MMV-L06-movExit` |
| Mouvements de stock | Entrée · Sortie · Ajustement | 61·62·63 | 63 · Ajustement | `movAdjust` | `MMV-L06-movAdjust` |
| Mouvements de stock | Confirmation d'ajustement · Motif obligatoire · Quantité invalide | 64·65·66 | 64 · Confirmation d'ajustement | `movAdjustConfirm` | `MMV-L06-movAdjustConfirm` |
| Mouvements de stock | Confirmation d'ajustement · Motif obligatoire · Quantité invalide | 64·65·66 | 65 · Motif obligatoire | `movReasonReq` | `MMV-L06-movReasonReq` |
| Mouvements de stock | Confirmation d'ajustement · Motif obligatoire · Quantité invalide | 64·65·66 | 66 · Quantité invalide | `movQtyInvalid` | `MMV-L06-movQtyInvalid` |
| Mouvements de stock | Mouvement réussi · Conflit · Erreur métier · Erreur technique | 67·68·69·70 | 67 · Mouvement réussi | `movSuccess` | `MMV-L06-movSuccess` |
| Mouvements de stock | Mouvement réussi · Conflit · Erreur métier · Erreur technique | 67·68·69·70 | 68 · Conflit multi-poste | `movConflict` | `MMV-L06-movConflict` |
| Mouvements de stock | Mouvement réussi · Conflit · Erreur métier · Erreur technique | 67·68·69·70 | 69 · Erreur métier (stock insuffisant) | `movErrBiz` | `MMV-L06-movErrBiz` |
| Mouvements de stock | Mouvement réussi · Conflit · Erreur métier · Erreur technique | 67·68·69·70 | 70 · Erreur technique | `movErrTech` | `MMV-L06-movErrTech` |
| Inventaire | Liste des inventaires · Nouvel inventaire | 71·72 | 71 · Liste des inventaires | `invList` | `MMV-L06-invList` |
| Inventaire | Liste des inventaires · Nouvel inventaire | 71·72 | 72 · Nouvel inventaire | `invNew` | `MMV-L06-invNew` |
| Inventaire | Inventaire en cours · Saisie d'une quantité · Comparaison | 73·74·75 | 73 · Inventaire en cours | `invInProgress` | `MMV-L06-invInProgress` |
| Inventaire | Inventaire en cours · Saisie d'une quantité · Comparaison | 73·74·75 | 74 · Saisie d'une quantité constatée | `invCount` | `MMV-L06-invCount` |
| Inventaire | Inventaire en cours · Saisie d'une quantité · Comparaison | 73·74·75 | 75 · Comparaison stock théorique / constaté | `invCompare` | `MMV-L06-invCompare` |
| Inventaire | Liste des écarts · Confirmation des ajustements · Inventaire terminé | 76·77·78 | 76 · Liste des écarts | `invDiffs` | `MMV-L06-invDiffs` |
| Inventaire | Liste des écarts · Confirmation des ajustements · Inventaire terminé | 76·77·78 | 77 · Confirmation des ajustements | `invConfirm` | `MMV-L06-invConfirm` |
| Inventaire | Liste des écarts · Confirmation des ajustements · Inventaire terminé | 76·77·78 | 78 · Inventaire terminé | `invDone` | `MMV-L06-invDone` |
| Inventaire | Abandon · Conflit multi-poste · Historique d'un inventaire | 79·80·81 | 79 · Abandon d'un inventaire | `invAbandon` | `MMV-L06-invAbandon` |
| Inventaire | Abandon · Conflit multi-poste · Historique d'un inventaire | 79·80·81 | 80 · Conflit / stock modifié sur un autre poste | `invConflict` | `MMV-L06-invConflict` |
| Inventaire | Abandon · Conflit multi-poste · Historique d'un inventaire | 79·80·81 | 81 · Historique d'un inventaire | `invHistory` | `MMV-L06-invHistory` |
| Fournisseurs | Liste des fournisseurs | 82·83 | 82 · Mode clair | `supListLight` | `MMV-L06-supListLight` |
| Fournisseurs | Liste des fournisseurs | 82·83 | 83 · Mode sombre | `supListDark` | `MMV-L06-supListDark` |
| Fournisseurs | Recherche · Filtres | 84·85 | 84 · Recherche | `supSearch` | `MMV-L06-supSearch` |
| Fournisseurs | Recherche · Filtres | 84·85 | 85 · Filtres (produits associés) | `supFilters` | `MMV-L06-supFilters` |
| Fournisseurs | Nouveau · Erreurs · Modification · Modifications non enregistrées | 86·87·88·89 | 86 · Nouveau fournisseur | `supNew` | `MMV-L06-supNew` |
| Fournisseurs | Nouveau · Erreurs · Modification · Modifications non enregistrées | 86·87·88·89 | 87 · Nouveau fournisseur avec erreurs | `supErrors` | `MMV-L06-supErrors` |
| Fournisseurs | Nouveau · Erreurs · Modification · Modifications non enregistrées | 86·87·88·89 | 88 · Modification d'un fournisseur | `supEdit` | `MMV-L06-supEdit` |
| Fournisseurs | Nouveau · Erreurs · Modification · Modifications non enregistrées | 86·87·88·89 | 89 · Modifications non enregistrées | `supUnsaved` | `MMV-L06-supUnsaved` |
| Fournisseurs | Détail · Produits associés · Sans produit | 90·91·92·93 | 90 · Détail — clair | `supDetailLight` | `MMV-L06-supDetailLight` |
| Fournisseurs | Détail · Produits associés · Sans produit | 90·91·92·93 | 91 · Détail — sombre | `supDetailDark` | `MMV-L06-supDetailDark` |
| Fournisseurs | Détail · Produits associés · Sans produit | 90·91·92·93 | 92 · Produits associés | `supProducts` | `MMV-L06-supProducts` |
| Fournisseurs | Détail · Produits associés · Sans produit | 90·91·92·93 | 93 · Fournisseur sans produit | `supNoProduct` | `MMV-L06-supNoProduct` |
| Fournisseurs | Suppression autorisée · Refus (utilisé) · Désactivation indisponible | 94·95·96 | 94 · Suppression autorisée (sans produit) | `supDelete` | `MMV-L06-supDelete` |
| Fournisseurs | Suppression autorisée · Refus (utilisé) · Désactivation indisponible | 94·95·96 | 95 · Refus de suppression (utilisé) | `supDeleteRefused` | `MMV-L06-supDeleteRefused` |
| Fournisseurs | Suppression autorisée · Refus (utilisé) · Désactivation indisponible | 94·95·96 | 96 · Désactivation / réactivation indisponible | `supNoDeactivate` | `MMV-L06-supNoDeactivate` |
| Fournisseurs | États transverses · Export CSV | 97·98·99·100·101·102 | 97 · État vide | `supEmpty` | `MMV-L06-supEmpty` |
| Fournisseurs | États transverses · Export CSV | 97·98·99·100·101·102 | 98 · Aucun résultat | `supNoResult` | `MMV-L06-supNoResult` |
| Fournisseurs | États transverses · Export CSV | 97·98·99·100·101·102 | 99 · Chargement | `supLoading` | `MMV-L06-supLoading` |
| Fournisseurs | États transverses · Export CSV | 97·98·99·100·101·102 | 100 · Erreur métier | `supErrBiz` | `MMV-L06-supErrBiz` |
| Fournisseurs | États transverses · Export CSV | 97·98·99·100·101·102 | 101 · Erreur technique | `supErrTech` | `MMV-L06-supErrTech` |
| Fournisseurs | États transverses · Export CSV | 97·98·99·100·101·102 | 102 · Exporter en CSV (cible V1) | `supCsv` | `MMV-L06-supCsv` |
| Adaptation et accessibilité | Fenêtres réduites | 103·104·105·106 | 103 · Catalogue en fenêtre réduite | `a11yCatalogue` | `MMV-L06-a11yCatalogue` |
| Adaptation et accessibilité | Fenêtres réduites | 103·104·105·106 | 104 · Vue stock en fenêtre réduite | `a11yStock` | `MMV-L06-a11yStock` |
| Adaptation et accessibilité | Fenêtres réduites | 103·104·105·106 | 105 · Détail produit en fenêtre réduite | `a11yDetail` | `MMV-L06-a11yDetail` |
| Adaptation et accessibilité | Fenêtres réduites | 103·104·105·106 | 106 · Liste fournisseur en fenêtre réduite | `a11ySuppliers` | `MMV-L06-a11ySuppliers` |
| Adaptation et accessibilité | Parcours clavier | 107·108 | 107 · Formulaire produit au clavier | `a11yProdKbd` | `MMV-L06-a11yProdKbd` |
| Adaptation et accessibilité | Parcours clavier | 107·108 | 108 · Formulaire fournisseur au clavier | `a11ySupKbd` | `MMV-L06-a11ySupKbd` |
| Adaptation et accessibilité | Tabulation · Focus visible · Erreur sous champ · Colonnes prioritaires | 109·110·111·112 | 109 · Ordre de tabulation | `a11yTabOrder` | `MMV-L06-a11yTabOrder` |
| Adaptation et accessibilité | Tabulation · Focus visible · Erreur sous champ · Colonnes prioritaires | 109·110·111·112 | 110 · Focus visible | `a11yFocus` | `MMV-L06-a11yFocus` |
| Adaptation et accessibilité | Tabulation · Focus visible · Erreur sous champ · Colonnes prioritaires | 109·110·111·112 | 111 · Erreur affichée sous un champ | `a11yFieldErr` | `MMV-L06-a11yFieldErr` |
| Adaptation et accessibilité | Tabulation · Focus visible · Erreur sous champ · Colonnes prioritaires | 109·110·111·112 | 112 · Tableau — colonnes prioritaires | `a11yPriority` | `MMV-L06-a11yPriority` |
| Adaptation et accessibilité | Sans souris | 113·114 | 113 · Confirmation destructive accessible | `a11yDestructive` | `MMV-L06-a11yDestructive` |
| Adaptation et accessibilité | Sans souris | 113·114 | 114 · Mouvement de stock réalisable sans souris | `a11yNoMouse` | `MMV-L06-a11yNoMouse` |

## 3. États V2 sans planche

Les états GAP-05-01 à GAP-05-09 (et suivants) **n'ont pas de planche** dans le Lot 6 et ne sont pas fabriqués ici : liste et sous-lot dans `docs/phase/05/MATRICE_LOT6.md` § 2.
