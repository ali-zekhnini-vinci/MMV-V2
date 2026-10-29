# 04 — Correspondance des 36 états visuels du Lot 3 (19/10/2026, 04-00)

**Source visuelle examinée :** `MMV-Lot3-Ordonnances.dc.html`, SHA-256 `c103df9e434245cdcba5b6246c5ec3032301e323c6ec1797826deaf8fd06c213` (**vérifié**, 93 625 octets, identique à `registres/DESIGN_SOURCES_SHA256.csv` ligne lot 3 et à l'entrée du dossier maître) ; copie de travail non versionnée `references/phase04/lot3/`. **Catalogue :** `MMV_V2_Registre_324_Etats.csv` (36 lignes `MMV-L03-*`) et `MMV_V2_Registre_134_Groupes.csv` (17 groupes `B-033` à `B-049`), copies `references/phase04/catalogue/`. Empreintes : [`SOURCE_MAP.md`](SOURCE_MAP.md).

La maquette est **DRAFT** : un état visuel n'est ni une règle métier ni une autorisation. Elle ne représente **qu'un seul type** (« ordonnance ») ; ses notes de conception décrivent l'ancien MMV (entité, validateur, plages, normalisation d'axe, suppression physique, rôles visuels) — **ignorées** comme source de règles (D-000). Seuls la composition, les libellés, les états et l'accessibilité sont exploités.

## Légende

- **Type** : `RX` ordonnance médicale ; `MES` mesure magasin ; `RX+MES` état commun aux deux types (D-04-01 APPROVED).
- **Traitement** : `ADOPT` (repris tel quel, sous réserve des jetons et composants V2) ; `ADAPT` (repris avec modifications imposées par D-04-01 ou proposées) ; `REPLACE` (conflit avec une règle proposée : l'état est remplacé) ; aucun état n'est `DEFERRED`.
- **Sous-lot** : `04-A` interface (double de test), `04-B` modèle/API, `04-C` intégration réelle.
- **Statut** : tous `PROPOSED` — aucun état `APPROVED` (approbation visuelle non prononcée).

## Les 36 états

| # | ID | Groupe | Planche | Type | Traitement V2 proposé | Traitement | Décision | Sous-lot |
|---|---|---|---|---|---|---|---|---|
| 1 | MMV-L03-listLight | B-033 | 01 | RX+MES | liste globale « Dossiers optiques » : date, client, prescripteur (RX) / collaborateur (MES), **colonne Type**, **statut** (brouillon / confirmé / invalidé), résumé OD/OG, saisi le, actions ; pagination serveur | ADAPT | D-04-01, D-04-08.b, D-04-05 | 04-A |
| 2 | MMV-L03-listDark | B-033 | 01 | RX+MES | jetons du thème sombre | ADOPT | — | 04-A |
| 3 | MMV-L03-listSearch | B-034 | 02 | RX+MES | recherche **serveur** par client et prescripteur, surlignage (composant Clients) | ADAPT | D-04-08.b | 04-A |
| 4 | MMV-L03-listFilters | B-034 | 03 | RX+MES | filtres type / statut / prescripteur, tri ; « invalidés » exclus par défaut, inclusion explicite | ADAPT | D-04-01, D-04-04.c | 04-A |
| 5 | MMV-L03-listByClient | B-035 | 04 | RX+MES | devient l'**onglet « Dossier optique »** de la fiche (deux sections), contexte client conservé, retour à la fiche | ADAPT | D-04-08.a/e | 04-A |
| 6 | MMV-L03-listNoResult | B-036 | 05 | RX+MES | « Aucun résultat » + effacer la recherche | ADOPT | — | 04-A |
| 7 | MMV-L03-listEmpty | B-036 | 06 | RX+MES | vide, **un état par section** (aucune ordonnance / aucune mesure) ; rejoint `MMV-L02-ficheNoRx` | ADOPT | D-04-01 | 04-A |
| 8 | MMV-L03-listLoading | B-036 | 07 | RX+MES | squelette + libellé | ADOPT | — | 04-A |
| 9 | MMV-L03-listError | B-036 | 08 | RX+MES | erreur technique bornée + Réessayer (aucune trace technique) | ADOPT | — | 04-A |
| 10 | MMV-L03-newClientLight | B-037 | 09 | RX (puis MES, GAP-04-02) | formulaire depuis la fiche, client prérempli ; **précédé du choix du type** (GAP-04-01) ; bloc provenance selon D-04-03 | ADAPT | D-04-01, D-04-03.a | 04-A |
| 11 | MMV-L03-newClientDark | B-037 | 10 | RX | thème sombre | ADOPT | — | 04-A |
| 12 | MMV-L03-newGlobal | B-038 | 11 | RX+MES | création depuis la liste globale ; exige `clients.read` pour choisir le client | ADAPT | D-04-05.b | 04-A |
| 13 | MMV-L03-clientPicker | B-038 | 12 | RX+MES | **réutilise `ClientPicker`** (Rendez-vous) ; clients actifs seulement | ADAPT | D-04-07 | 04-A |
| 14 | MMV-L03-saving | B-039 | 16 | RX+MES | bouton occupé, formulaire verrouillé | ADOPT | — | 04-A |
| 15 | MMV-L03-createSuccess | B-039 | 17 | RX+MES | **conflit** : la maquette fait de « Créer une vente » l'action principale → remplacée par « Voir le détail » / « Retour à la fiche » ; « Préparer une vente » **désactivée** « Phase ultérieure » | REPLACE | D-04-08.c/d | 04-A |
| 16 | MMV-L03-partialValid | B-040 | 13 | RX+MES | saisie partielle = **brouillon** ; états par œil (renseigné / non prescrit / non transcrit) | ADAPT | D-04-02.a/c | 04-A (après décision) |
| 17 | MMV-L03-formErrors | B-041 | 14 | RX+MES | erreurs sous le champ, données conservées ; **règles selon D-04-06** (aucune borne héritée) | ADAPT | D-04-06 | 04-A (après décision) |
| 18 | MMV-L03-archived | B-041 | 15 | RX+MES | refus métier clair « Réactivez le client… » pour les deux types | ADOPT | D-04-07 | 04-A |
| 19 | MMV-L03-detailLight | B-042 | 18 | RX (MES : GAP-04-03) | détail : badge de type, **statut**, **numéro de révision**, bloc provenance (prescripteur, dates de prescription / réception, saisie), pièces source, OD/OG, notes | ADAPT | D-04-01, D-04-03, D-04-04 | 04-A |
| 20 | MMV-L03-detailDark | B-042 | 19 | RX | thème sombre | ADOPT | — | 04-A |
| 21 | MMV-L03-detailPartial | B-043 | 21 | RX+MES | « — » distingue **non prescrit** et **non transcrit** (libellés accessibles, pas seulement le tiret) | ADAPT | D-04-02.c | 04-A (après décision) |
| 22 | MMV-L03-detailReadOnly | B-043 | 22 | RX+MES | lecture seule **déduite des permissions** de la session (et refusée par le serveur), jamais d'un rôle visuel « technicien » | ADAPT | D-04-05.e | 04-A |
| 23 | MMV-L03-editForm | B-044 | 23 | RX+MES | brouillon : modification en place ; confirmé : « **Rectifier** » → nouvelle révision, motif obligatoire | ADAPT | D-04-04.a/b | 04-A (après décision) |
| 24 | MMV-L03-editErrors | B-044 | 24 | RX+MES | erreurs sous champs, données conservées ; 409 de version géré (GAP-04-09) | ADAPT | D-04-06 | 04-A |
| 25 | MMV-L03-axisNormalized | B-045 | 25 | RX+MES | **conflit** : normalisation 0° → 180° non approuvée → axe **affiché tel que transcrit** (ou selon D-04-06.b) ; aucune notice de normalisation | REPLACE | D-04-06.b | 04-A (après décision) |
| 26 | MMV-L03-confirmClose | B-045 | 26 | RX+MES | garde de sortie existante (`useLeaveGuard`) + AlertDialog | ADOPT | — | 04-A |
| 27 | MMV-L03-editSuccess | B-045 | 27 | RX+MES | succès « Révision n enregistrée » (ou « Brouillon enregistré »), notice persistante | ADAPT | D-04-04 | 04-A |
| 28 | MMV-L03-deleteMenu | B-046 | 28 | RX+MES | **conflit** : « Supprimer » → « **Invalider** » au menu secondaire (`optical.invalidate`) | REPLACE | D-04-04.c | 04-A |
| 29 | MMV-L03-confirmDelete | B-046 | 29 | RX+MES | **conflit** : confirmation d'invalidation avec **motif** ; formulation non « irréversible / supprimée » | REPLACE | D-04-04.c | 04-A |
| 30 | MMV-L03-deleteSuccess | B-047 | 30 | RX+MES | **conflit** : « Supprimée » → « Invalidée » ; l'enregistrement reste visible avec bandeau (GAP-04-08) | REPLACE | D-04-04.c | 04-A |
| 31 | MMV-L03-deleteError | B-047 | 31 | RX+MES | erreur technique d'invalidation ; 404 introuvable, 409 déjà invalidé / version | ADAPT | D-04-04.c | 04-A |
| 32 | MMV-L03-reducedWindow | B-048 | 32 | RX+MES | OD empilé sur OG, aucun défilement horizontal, aucun champ masqué | ADOPT | — | 04-A |
| 33 | MMV-L03-tabOrder | B-048 | 33 | RX+MES | ordre de Tab logique (provenance → OD → OG → notes → actions) ; ordre exact selon les champs ratifiés | ADAPT | D-04-03, D-04-06 | 04-A |
| 34 | MMV-L03-focusField | B-049 | 34 | RX+MES | focus visible (jetons V2) | ADOPT | — | 04-A |
| 35 | MMV-L03-fieldError | B-049 | 35 | RX+MES | erreur sous le champ, `aria-describedby` | ADOPT | — | 04-A |
| 36 | MMV-L03-disabledFields | B-049 | 36 | RX+MES | axe désactivé si cylindre vide / nul, base désactivée sans prisme — **si D-04-06 le retient** | ADAPT | D-04-06 | 04-A (après décision) |

**Couverture : 36 / 36 états cartographiés** (17 / 17 groupes) — `ADOPT` 13, `ADAPT` 18, `REPLACE` 5 (createSuccess, axisNormalized, deleteMenu, confirmDelete, deleteSuccess). États dépendant d'une décision encore `PROPOSED` avant toute implémentation définitive : 16, 17, 21, 23, 25, 36 (et 33 pour l'ordre exact). Planche 20 : absente du catalogue (numérotation de la maquette, aucun état manquant). Le `data-mount="notes"` (notes de conception) est hors inventaire, comme pour les autres lots.

## États nécessaires absents du Lot 3 (lacunes de design)

La maquette ne connaît qu'un type et aucune des fonctions suivantes. Ces états sont **à concevoir** (complément Claude Design ou composition à partir des composants V2 existants, décision G04-DESIGN) ; aucun n'est déclaré conçu.

| Réf. | État manquant | Motif | Décision |
|---|---|---|---|
| GAP-04-01 | choix du type « Ordonnance médicale » / « Mesure magasin » avant saisie | D-04-01 | APPROVED (principe) |
| GAP-04-02 | formulaire « Mesure magasin » (clair / sombre, erreurs) | D-04-01 | D-04-03.b, D-04-06.d |
| GAP-04-03 | détail « Mesure magasin » | D-04-01 | D-04-03.b |
| GAP-04-04 | onglet « Dossier optique » de la fiche : deux sections, vide par section | D-04-01, D-04-08.a | — |
| GAP-04-05 | accès refusé : onglet absent sans `optical.read`, page 403 sur accès direct | D-04-05 | D-04-05 |
| GAP-04-06 | pièce source : ajout, liste, aperçu, format / taille refusés, accès refusé | D-04-03.c | D-04-03.c, D-04-05 |
| GAP-04-07 | historique des révisions et comparaison champ à champ | D-04-04.f | D-04-04 |
| GAP-04-08 | bandeau « Invalidé » (motif, date, auteur) sur détail et ligne | D-04-04.c | D-04-04 |
| GAP-04-09 | conflit de version (deux postes) : 409, rechargement sans perte de la saisie | J-04-06 | — |
| GAP-04-10 | confirmation de la transcription (brouillon → confirmé) | D-04-02.a | D-04-02 |
| GAP-04-11 | badge de statut brouillon / confirmé / invalidé | D-04-02.a | D-04-02 |
| GAP-04-12 | « Préparer une vente » désactivée « Phase ultérieure » | D-04-08.d | D-04-08 |

## États d'autres lots touchés par la Phase 04

| ID | Lot | État en Phase 03 | Traitement Phase 04 |
|---|---|---|---|
| MMV-L02-ficheRx | 2 | `DEFERRED` (« phase ultérieure », [`../03/STATE_MAP_47.md`](../03/STATE_MAP_47.md)) | remplacé par l'onglet « Dossier optique » (GAP-04-04), visible avec `optical.read` seulement |
| MMV-L02-ficheNoRx | 2 | `DEFERRED` | état vide par section (état 7) |
| MMV-L02-createSuccess | 2 | sans « Ajouter une ordonnance » (D-03-03) | **inchangé en 04** (pas d'ajout proposé) |
| MMV-L04-stepRxCorrective, stepRxNonCorrective, rxBlock, stepProductsLens | 4 | — | **hors 04** (module Ventes) ; contraintes anticipées : [`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 7 |
| MMV-L05-orderRxRequired, wsRx | 5 | — | **hors 04** (commandes / atelier) ; lecture seule d'une révision figée |

## Amendement daté — 19/10/2026 (suite de la revue, préparation 04-A)

- **G04-DESIGN — voie retenue par le propriétaire** : les 12 états complémentaires peuvent être **composés à partir des composants V2 existants**, sous réserve de **validation visuelle du propriétaire** (mandat [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md)). Composition proposée : [`PREPARATION_04A.md`](PREPARATION_04A.md) § 4. Aucun état n'est encore construit ; statut visé après captures : `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED`. Les tableaux ci-dessus restent `PROPOSED`.
- **Deux états complémentaires nouveaux**, issus des études ([`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md)), suivent la même voie :

| Réf. | État manquant | Motif | Décision |
|---|---|---|---|
| GAP-04-13 | « rectification en cours » : bandeau sur le détail (auteur, début, motif), comparaison brouillon ↔ révision de base, abandon | coexistence révision confirmée / brouillon | D-04-11 |
| GAP-04-14 | pièce invalidée citée par une révision confirmée (ligne libellée, avertissement avant transmission) | rattachement historisé | D-04-10 |

- **Traitement en 04-A des états dépendant de D-04-06** : état 25 (`axisNormalized`) — aucune normalisation, axe affiché tel que saisi ; état 36 (`disabledFields`) — **non implémenté** (aucune dépendance entre champs tant que D-04-06 n'est pas ratifiée) ; états 16, 17, 21, 23, 33 — mécanique construite, règles absentes ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 3).

## Amendement daté — 19/10/2026 (04-A réalisé sur banc isolé)

Les tableaux ci-dessus restent la cartographie `PROPOSED`. Réalisation 04-A (G04-A APPROVED — UI ONLY) : **50 états tracés** — 24 RÉALISÉ, 20 ADAPTÉ, 1 AUTRE COMPOSANT (état 13, `ClientPicker`), 1 ADAPTÉ partiel (33), 4 EN ATTENTE d'avis métier (16, 17, 21 : mécanique seule ; 36 : non implémenté) — détail, captures et tests : [`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md). Statut visuel de chaque état : `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED` ; aucun `APPROVED`.

## Amendement daté — 20/10/2026 (D-04-16 APPROVED)

Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). Cartographie ci-dessus conservée. Effets : état **21** (`detailPartial`) — la distinction « non prescrit » / « non transcrit » est abandonnée avec l'état par œil ; une absence reste écrite « — » (« Non renseigné »), jamais « 0,00 » ; **GAP-04-06** (pièce source) et **GAP-04-14** (pièce invalidée citée) sont **reportés** au backlog permanent BL-04-01. Couverture courante : [`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md), dernière section.
