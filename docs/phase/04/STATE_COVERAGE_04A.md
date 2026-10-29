# 04-A — Couverture des états (36 du Lot 3 + 12 complémentaires 04-00 + 2 issus des études)

*Amendement 20/10/2026 : refonte UX des formulaires selon la maquette Lot 3 (D-04-15 APPROVED) — voir l'avant-dernière section ; les tableaux ci-dessous sont conservés tels qu'au 19/10/2026.*
*Amendement 20/10/2026 (D-04-16 APPROVED) : état de l'œil supprimé, pièces sources reportées (BL-04-01) — voir l'avant-dernière section.*
*Amendement 20/10/2026 (D-04-17 APPROVED, clôture 04-A) : enregistrement simplifié — voir la dernière section, qui donne la synthèse courante.*

**Date : 19/10/2026.** Mandat : [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md) (G04-A APPROVED — UI ONLY). Prototype exécuté **uniquement dans le banc isolé** `apps/desktop/optical-bench.html` (O-04-16 APPROVED), sur données fictives, droits simulés, sans serveur ni base. Correspondance d'origine : [`STATE_MAP_36.md`](STATE_MAP_36.md) ; composition des états complémentaires : [`PREPARATION_04A.md`](PREPARATION_04A.md) § 4 ; rapport : [`LIVRAISON_04A.md`](LIVRAISON_04A.md).

## Légende

- **RÉALISÉ** : construit comme cartographié (gabarit V2 du Lot 3 ou composition validable).
- **ADAPTÉ** : construit avec l'adaptation imposée par D-04-01 ou proposée (`ADAPT` / `REPLACE` de la carte).
- **AUTRE COMPOSANT** : représenté par un composant existant réutilisé, sans duplication.
- **EN ATTENTE** : avis métier requis ; seule la **mécanique** neutre est construite, ou rien (aucune règle inventée).
- Toute ligne repose, sauf mention contraire, sur des décisions **PROPOSED** (D-04-02 à D-04-14) : prototype réversible, non approuvé. Statut visuel de chaque état : `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED`.
- **Capture** : `docs/phase/04/evidence/P04A_navigateur_<nom>.jpg` (Edge sans interface, moteur Chromium de WebView2, **pas** Tauri natif). **Test** : `apps/desktop/src/bench/OpticalBench.test.tsx` (parcours), `inMemoryOpticalGateway.test.ts` (double), `src/optical/opticalUi.test.ts`, `src/optical/realAppIsolation.test.tsx`.

## Les 36 états du Lot 3

| # | État | Traitement 04-A | Réalisation | Capture | Test |
|---|---|---|---|---|---|
| 1 | listLight | ADAPTÉ | liste « Dossiers optiques » : date, **type**, client, prescripteur / collaborateur, **statut dérivé**, saisi le, action ; pagination ; résumé OD/OG **commutable** (D-04-13.b à trancher, désactivé par défaut) | `10-etat01-liste-globale` | J-04-02 liste |
| 2 | listDark | RÉALISÉ | jetons du thème sombre | `10-etat01-liste-globale-sombre`, `61-zoom*-liste-sombre` | — |
| 3 | listSearch | ADAPTÉ | recherche par client / prescripteur décidée par le service (double), surlignage `Highlight` des Clients | `12-etat03-recherche` | J-04-02 liste |
| 4 | listFilters | ADAPTÉ | filtres type / statut, tri, **invalidés exclus par défaut**, inclusion explicite | `11-etat04-filtres-invalides-inclus-resume` | J-04-02 liste |
| 5 | listByClient | ADAPTÉ | onglet « Dossier optique » de la fiche, **deux sections distinctes** | `02-etat05-fiche-dossier-optique` (+ sombre, 1920) | J-04-01 |
| 6 | listNoResult | RÉALISÉ | « Aucun résultat » + effacer / réinitialiser | `13-etat06-aucun-resultat` | J-04-02 liste |
| 7 | listEmpty | ADAPTÉ | un état vide **par section** | `03-etat07-fiche-vide-par-section` | J-04-01 |
| 8 | listLoading | RÉALISÉ | `LoadingBlock` (Clients) | `14-etat08-chargement` | — (capture seule) |
| 9 | listError | RÉALISÉ | erreur bornée + Réessayer, aucune trace technique | `15-etat09-erreur` | erreurs techniques |
| 10 | newClientLight | ADAPTÉ | formulaire depuis la fiche, client prérempli, **précédé du choix du type** ; qualité du prescripteur = « Non précisée » (liste BLOQUÉE) | `30-etat10-formulaire-ordonnance` | J-04-01 |
| 11 | newClientDark | RÉALISÉ | thème sombre | `30-etat10-formulaire-ordonnance-sombre` | — |
| 12 | newGlobal | ADAPTÉ | « Nouveau… » → type → client | `16-etats12-13-selection-client` | J-04-02 création |
| 13 | clientPicker | AUTRE COMPOSANT | `appointments/ClientPicker.tsx` réutilisé tel quel (clients actifs seulement) | `16-etats12-13-selection-client` | J-04-02 création |
| 14 | saving | RÉALISÉ | formulaire verrouillé, bouton « Enregistrement… » | `39-etat14-enregistrement-en-cours` | — (capture seule) |
| 15 | createSuccess | ADAPTÉ (`REPLACE`) | notice + détail ; **pas** de « Créer une vente » ; « Préparer une vente · Phase ultérieure » désactivé | `42-etat27-revision-enregistree`, `20-etat19-…` | J-04-01, J-04-04 |
| 16 | partialValid | EN ATTENTE (D-04-02.b/c) | brouillon sauvegardable incomplet, état par œil **proposé** ; critère de confirmation **non codé** côté poste, refus `422 incomplete` du double affiché tel quel | `25-etats16-21-brouillon-partiel`, `27-confirmation-refusee-422` | confirmation refusée |
| 17 | formErrors | EN ATTENTE (D-04-06) | mécanisme réalisé (bandeau, erreurs sous champ, saisie conservée, focus sur le premier champ) ; **seules** des erreurs de forme (nombre attendu, longueur) | `38-etats17-35-erreurs-sous-champ`, `38b-etat17-bandeau-erreurs` | ordre de Tab / erreurs |
| 18 | archived | ADAPTÉ | consultation seule, ajout désactivé ; saisie forcée refusée `409 client_archived` (deux types) | `04-etat18-client-archive` | client archivé |
| 19 | detailLight | ADAPTÉ | badges de type et de statut, n° de révision, provenance, pièces, OD/OG, notes, révisions, journal | `20-etat19-detail-rx-revision2`, `53-1920-detail-rx` | J-04-04 |
| 20 | detailDark | RÉALISÉ | thème sombre | `20-etat19-detail-rx-revision2-sombre` | — |
| 21 | detailPartial | EN ATTENTE (D-04-02.c) | absence écrite en toutes lettres (« Non prescrit sur le document », « Non transcrit »), jamais « 0,00 » ; libellés à ratifier | `25-etats16-21-brouillon-partiel` | grille neutre |
| 22 | detailReadOnly | ADAPTÉ | lecture seule **déduite des droits simulés**, jamais d'un rôle visuel | `35-etat22-lecture-seule-sans-pieces` | pièces / lecture seule |
| 23 | editForm | ADAPTÉ | brouillon modifié en place ; confirmé → « Rectifier » avec motif → brouillon de rectification (E-02) | `29-etat23-rectifier-motif` | J-04-04 |
| 24 | editErrors | ADAPTÉ | erreurs sous champ ; `409 version_conflict` → GAP-04-09 | `40-gap09-conflit-deux-postes` | J-04-06 |
| 25 | axisNormalized | ADAPTÉ (`REPLACE`) | **aucune normalisation** : l'axe reste tel que saisi (0 reste 0) ; aucune notice | `21-gap13-…` (axe 0) | double : valeurs ; grille neutre |
| 26 | confirmClose | RÉALISÉ | `useLeaveGuard` + confirmation (banc et formulaire) | `41-etat26-garde-de-sortie` | garde de sortie |
| 27 | editSuccess | ADAPTÉ | « Brouillon enregistré » / « Révision n enregistrée », notice | `42-etat27-revision-enregistree` | J-04-01, J-04-04 |
| 28 | deleteMenu | ADAPTÉ (`REPLACE`) | menu secondaire « Invalider… », **aucune** action « Supprimer » | `32-etats28-29-invalider` | J-04-07 |
| 29 | confirmDelete | ADAPTÉ (`REPLACE`) | confirmation avec **motif** ; pas de « irréversible / supprimée » | `32-etats28-29-invalider` | J-04-07 |
| 30 | deleteSuccess | ADAPTÉ (`REPLACE`) | « Invalidé » : enregistrement conservé, bandeau | `24-gap08-etat30-invalide` | J-04-07 |
| 31 | deleteError | ADAPTÉ | erreur technique bornée, rien n'est invalidé ; `409 already_invalidated` / `version_conflict` | `32b-etat31-erreur-invalidation` | erreurs techniques |
| 32 | reducedWindow | RÉALISÉ | OD au-dessus d'OG, aucun défilement horizontal de page (mesuré sur chaque capture) | `50-…`, `51-…-sombre`, `52-fenetre-reduite-detail` | — (mesure des captures) |
| 33 | tabOrder | ADAPTÉ partiel / EN ATTENTE | ordre DOM : provenance → OD (état, valeurs) → OG → notes → actions ; ordre **exact** après D-04-03 / D-04-06 | `43-etat34-focus-visible` | ordre de Tab |
| 34 | focusField | RÉALISÉ | focus visible (jetons V2) | `43-etat34-focus-visible` | — |
| 35 | fieldError | RÉALISÉ | erreur sous le champ, `aria-describedby`, `aria-invalid` | `38-etats17-35-erreurs-sous-champ` | ordre de Tab / erreurs |
| 36 | disabledFields | EN ATTENTE — **non implémenté** | aucune dépendance entre champs, aucun champ désactivé (D-04-06 ouverte) | — | grille neutre (aucun champ désactivé) |

## Les 12 états complémentaires (04-00) et les 2 issus des études

| Réf. | État | Traitement 04-A | Réalisation | Capture | Test |
|---|---|---|---|---|---|
| GAP-04-01 | choix du type | RÉALISÉ | boîte Radix à deux options larges, focus sur la première, Échap annule ; chaque option selon le droit d'écriture simulé du type | `05-gap01-choix-du-type` | J-04-01 |
| GAP-04-02 | formulaire mesure magasin | RÉALISÉ (PROTO) | même gabarit, provenance propre : date, collaborateur (comptes fictifs), catégorie « Non précisée » (liste BLOQUÉE) ; **aucun** prescripteur | `31-gap02-formulaire-mesure` (+ sombre) | J-04-02 création |
| GAP-04-03 | détail mesure magasin | RÉALISÉ | gabarit du détail, provenance propre, aucune zone pièces | `28-gap03-detail-mesure` | — |
| GAP-04-04 | onglet de la fiche | RÉALISÉ | `tabs.tsx`, deux sections, état vide par section | `02-…`, `03-…` | J-04-01 |
| GAP-04-05 | accès refusé | ADAPTÉ (O-04-16) | sans `optical.read` simulé : la fiche garde **l'onglet reporté actuel** (décision O-04-16, remplacement décidé en 04-C) ; accès direct au module → page 403 ; pièces « N pièce(s) — accès non autorisé » | `36-gap05-sans-droit-optique-fiche`, `37-gap05-acces-direct-refuse`, `35-…` | accès refusé ; pièces |
| GAP-04-06 | pièces source | RÉALISÉ (PROTO) | liste par révision + autres pièces du dossier ; « Joindre une pièce » = pièces **synthétiques** du banc ; refus 413 / 415 / doublon ; aperçu image synthétique (canvas, `data:`) ; PDF : aperçu reporté (O-04-01) | `33-gap06-apercu-piece-synthetique`, `34-gap06-format-refuse` | J-04-08 |
| GAP-04-07 | révisions et comparaison | RÉALISÉ (PROTO) | liste chronologique, abandonnées masquées par défaut, comparaison deux colonnes, écart écrit « Différent » | `23-gap07-revisions-comparaison` | J-04-04 |
| GAP-04-08 | invalidé | RÉALISÉ | bandeau (motif, date, auteur), statut « Invalidé », exclu par défaut des listes | `24-gap08-etat30-invalide` | J-04-07 |
| GAP-04-09 | conflit de version | RÉALISÉ | 409 : saisie conservée, bandeau, « Recharger » après confirmation | `40-gap09-conflit-deux-postes` | J-04-06 |
| GAP-04-10 | confirmation | RÉALISÉ (PROTO) | récapitulatif type / client / provenance / états par œil ; « Confirmé » ≠ « validé médicalement » | `26-gap10-confirmation` | J-04-04 |
| GAP-04-11 | badges de statut | RÉALISÉ | Brouillon / Confirmé — révision n / Confirmé · rectification en cours / Invalidé ; texte toujours présent | toutes | toutes |
| GAP-04-12 | vente inerte | RÉALISÉ | « Préparer une vente · Phase ultérieure » visible, désactivé, aucun contexte transmis | `20-…`, `42-…` | J-04-04 |
| GAP-04-13 | rectification en cours (E-02) | RÉALISÉ (PROTO) | bandeau (auteur, début, motif) ; **la dernière révision confirmée reste affichée** ; brouillon consultable séparément ; Poursuivre / Voir le brouillon / Abandonner | `21-gap13-rectification-en-cours` (+ sombre), `52-…` | J-04-04 |
| GAP-04-14 | pièce invalidée citée (E-01) | RÉALISÉ (PROTO) | ligne barrée **et** libellée « Invalidée le … — motif », avertissement avant transmission | `22-gap14-piece-invalidee` | J-04-08 |

## Synthèse

| Catégorie | Lot 3 (36) | Complémentaires (14) | Total (50) |
|---|---|---|---|
| RÉALISÉ | 11 — états 2, 6, 8, 9, 11, 14, 20, 26, 32, 34, 35 | 13 — GAP-04-01 à 04, 06 à 14 (dont 6 PROTO : 02, 06, 07, 10, 13, 14) | 24 |
| ADAPTÉ | 19 — états 1, 3, 4, 5, 7, 10, 12, 15, 18, 19, 22, 23, 24, 25, 27, 28, 29, 30, 31 | 1 — GAP-04-05 (O-04-16) | 20 |
| AUTRE COMPOSANT | 1 — état 13 (`ClientPicker` de Rendez-vous) | — | 1 |
| ADAPTÉ partiel, ordre exact en attente | 1 — état 33 | — | 1 |
| EN ATTENTE d'avis métier | 4 — états 16, 17, 21 (mécanique seule), 36 (non implémenté) | — | 4 |

**50 états tracés : 24 RÉALISÉ, 20 ADAPTÉ, 1 AUTRE COMPOSANT, 1 ADAPTÉ partiel, 4 EN ATTENTE.** Aucun état n'est `APPROVED` : validation visuelle du propriétaire requise ; les comportements de cycle de vie, de révision, de pièces et de droits reposent sur des décisions `PROPOSED`. **Rien de clinique n'est codé** : bornes, pas, dépendances OD / OG, normalisation d'axe, base du prisme, critère de complétude, listes de qualités et de catégories restent ouverts (D-04-06 [AVIS OPTICIEN]). La couverture n'est pas artificiellement portée à 100 % : les quatre états EN ATTENTE le restent jusqu'à l'examen métier.

## Amendement daté — 20/10/2026 (refonte UX Lot 3, D-04-15 APPROVED)

Mandat : [`prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md). Les formulaires « Ordonnance médicale » et « Mesure magasin » reprennent la mise en page de la maquette (en-tête avec actions, carte « Informations générales » avec le client concerné, cartes OD / OG côte à côte, boutons ±) ; détail et écarts : [`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 11. **Aucune catégorie ne change** : la refonte est visuelle et ergonomique ; les règles cliniques restent ouvertes (D-04-06). Captures : `docs/phase/04/evidence/P04A_UX_navigateur_apres-<nom>.jpg` ; comparaisons : `P04A_UX_compare_*.jpg`.

| # / Réf. | État | Traitement (inchangé) | Ce qui change le 20/10/2026 | Capture | Test |
|---|---|---|---|---|---|
| 10 | newClientLight | ADAPTÉ | gabarit `formPage` de la maquette ; client concerné dans la carte (`.clictx`), grille 3 colonnes, référence en lecture seule ; date **sans** astérisque | `apres-ordonnance-1920x1080-z100-clair`, `P04A_UX_compare_01_…` | J-04-01 |
| 11 | newClientDark | RÉALISÉ | idem, thème sombre | `apres-ordonnance-1920x1080-z100-sombre`, `P04A_UX_compare_02_…` | — |
| 14 | saving | RÉALISÉ | actions déplacées dans l'en-tête (verrouillées pendant l'enregistrement) | `P04A_navigateur_39-…` (plan d'origine, rejoué) | — |
| 17 | formErrors | EN ATTENTE (D-04-06) | bandeau inchangé ; erreur sous le champ ± (bordure d'erreur de la valeur) ; aucune règle croisée de la maquette | `apres-etat35-erreur-sous-champ`, `P04A_UX_compare_08_…` | Q-04-170 à Q-04-176 |
| 25 | axisNormalized | ADAPTÉ (`REPLACE`) | inchangé : axe sans pas ni normalisation, suffixe « degrés » de la maquette | `apres-ordonnance-saisie-rapide-*` | Q-04-171 |
| 32 | reducedWindow | RÉALISÉ | empilement OD / OG selon la largeur du formulaire (< 940 px) ; grille générale 3 → 2 → 1 colonnes | `apres-*-1024x700-*`, `P04A_UX_compare_06_…` | Q-04-178 |
| 33 | tabOrder | ADAPTÉ partiel / EN ATTENTE | ordre de la maquette dans chaque carte (Sphère → Acuité), OD puis OG ; boutons ± hors de l'ordre de Tab ; ↑ / ↓ et Alt+↓ | `apres-etat34-focus-stepper` | Q-04-174 |
| 34 | focusField | RÉALISÉ | anneau de focus de la maquette sur la valeur ± (`--shadow-focus`) | `apres-etat34-focus-stepper`, `P04A_UX_compare_07_…` | — |
| 35 | fieldError | RÉALISÉ | message sous le contrôle de la rangée, `aria-describedby` | `apres-etat35-erreur-sous-champ` | ordre de Tab / erreurs |
| 36 | disabledFields | EN ATTENTE — **non implémenté** | toujours aucun champ désactivé (Axe / Base de la maquette non repris) | — | Q-04-170 |
| GAP-04-02 | formulaire mesure magasin | RÉALISÉ (PROTO) | même gabarit et mêmes composants OD / OG que l'ordonnance, provenance propre, mention « pas une prescription délivrée par un médecin » | `apres-mesure-*`, `P04A_UX_compare_04_…`, `…_05_…` | Q-04-176 |

**Synthèse inchangée : 50 états tracés — 24 RÉALISÉ, 20 ADAPTÉ, 1 AUTRE COMPOSANT, 1 ADAPTÉ partiel, 4 EN ATTENTE.** Statut visuel : `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED` (refonte à valider par le propriétaire).

## Amendement daté — 20/10/2026 (simplification, D-04-16 APPROVED)

Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). Les tableaux précédents sont conservés comme historique ; seules les lignes ci-dessous changent.

| # / Réf. | État | Avant | Après le 20/10/2026 | Capture | Test |
|---|---|---|---|---|---|
| 10, 11 | newClientLight / Dark | ADAPTÉ / RÉALISÉ | inchangés, cartes OD / OG **sans** rangée « État de l'œil » | `P04A_SIMPL_navigateur_apres-ordonnance-1920x1080-z100-clair`, `…-sombre` | Q-04-180 |
| 16 | partialValid | EN ATTENTE (D-04-02.b/c) | **EN ATTENTE (D-04-02.b)** : brouillon incomplet sans état par œil ; un œil peut rester vide ; refus `422` du double sur la provenance seulement | `P04A_SIMPL_navigateur_25-etats16-21-brouillon-partiel`, `…_27-confirmation-refusee-422` | Q-04-183 |
| 19 | detailLight | ADAPTÉ | inchangé **sans** panneau de pièces ; révisions, comparaison, journal | `P04A_SIMPL_navigateur_20-etat19-detail-rx-revision2`, `…_23-gap07-revisions-comparaison` | Q-04-181 |
| 21 | detailPartial | EN ATTENTE (D-04-02.c) | **ADAPTÉ (D-04-16)** : absence écrite « — » avec le nom accessible « Non renseigné », jamais « 0,00 » ; plus de libellé d'état à ratifier | `P04A_SIMPL_navigateur_25-etats16-21-brouillon-partiel` | Q-04-180 |
| 22 | detailReadOnly | ADAPTÉ | inchangé (capture renommée `35-etat22-lecture-seule`) | `P04A_SIMPL_navigateur_35-etat22-lecture-seule` | Q-04-188 |
| 33 | tabOrder | ADAPTÉ partiel | ordre Sphère → Acuité, OD puis **directement** OG | — | Q-04-182 |
| GAP-04-05 | accès refusé | ADAPTÉ (O-04-16) | inchangé ; mention « N pièce(s) — accès non autorisé » sans objet | — | — |
| GAP-04-06 | pièces source | RÉALISÉ (PROTO) | **REPORTÉ (BL-04-01)** — retiré du prototype | — | Q-04-181 |
| GAP-04-07 | révisions et comparaison | RÉALISÉ (PROTO) | inchangé, sans lignes « état » ni « Pièces liées » | `P04A_SIMPL_navigateur_23-gap07-revisions-comparaison` | Q-04-185 |
| GAP-04-10 | confirmation | RÉALISÉ (PROTO) | récapitulatif « Correction » (résumé OD / OG) au lieu des états par œil | `P04A_SIMPL_navigateur_26-gap10-confirmation` | J-04-04 |
| GAP-04-14 | pièce invalidée citée | RÉALISÉ (PROTO) | **REPORTÉ (BL-04-01)** — retiré du prototype | — | — |

| Catégorie | Lot 3 (36) | Complémentaires (14) | Total (50) |
|---|---|---|---|
| RÉALISÉ | 11 — états 2, 6, 8, 9, 11, 14, 20, 26, 32, 34, 35 | 11 — GAP-04-01 à 04, 07 à 13 (dont 4 PROTO : 02, 07, 10, 13) | 22 |
| ADAPTÉ | 20 — états 1, 3, 4, 5, 7, 10, 12, 15, 18, 19, **21**, 22, 23, 24, 25, 27, 28, 29, 30, 31 | 1 — GAP-04-05 | 21 |
| AUTRE COMPOSANT | 1 — état 13 | — | 1 |
| ADAPTÉ partiel | 1 — état 33 | — | 1 |
| EN ATTENTE d'avis métier | 3 — états 16, 17 (mécanique seule), 36 (non implémenté) | — | 3 |
| REPORTÉ (BL-04-01) | — | 2 — GAP-04-06, GAP-04-14 | 2 |

**Synthèse courante : 50 états tracés — 22 RÉALISÉ, 21 ADAPTÉ, 1 AUTRE COMPOSANT, 1 ADAPTÉ partiel, 3 EN ATTENTE, 2 REPORTÉ.** Statut visuel : `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED`.

## Amendement daté — 20/10/2026 (enregistrement simplifié, D-04-17 APPROVED — clôture 04-A)

Mandat : [`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md). Les tableaux précédents sont conservés comme historique ; seules les lignes ci-dessous changent. Aucun état n'est ajouté ni retiré.

| # / Réf. | État | Avant | Après le 20/10/2026 (D-04-17) | Capture | Test |
|---|---|---|---|---|---|
| 10, 11 | newClientLight / Dark | RÉALISÉ / ADAPTÉ | inchangés ; en-tête : « Annuler », « Enregistrer comme brouillon » (tertiaire), « Enregistrer » (principal bleu) | `P04A_CLOT_navigateur_70-d0417-formulaire-ordonnance-actions`, `P04A_CLOT_navigateur_apres-ordonnance-1920x1080-z100-clair`, `…-sombre` | Q-04-190 |
| 14 | saving | RÉALISÉ | « Enregistrement… » sur le bouton principal, les deux actions désactivées, doubles déclenchements ignorés | `P04A_CLOT_navigateur_39-etat14-enregistrement-en-cours` | Q-04-195 |
| 16 | partialValid | EN ATTENTE (D-04-02.b) | **EN ATTENTE (D-04-02.b)** inchangé sur le fond ; refus du double désormais limité à la **règle minimale de démonstration** (aucune valeur optique) ; bandeau « Brouillon enregistré — confirmation refusée » | `P04A_CLOT_navigateur_27-confirmation-refusee-422` | Q-04-192 |
| 23 | editForm | ADAPTÉ | libellés « Modifier », « Modifier cet enregistrement ? », « Modifier — ordonnance médicale » ; « Dernière révision confirmée (révision N) reste affichée » | `P04A_CLOT_navigateur_75-d0417-modification-formulaire` | Q-04-198 |
| 26 | confirmClose | RÉALISÉ | inchangé pour une saisie non enregistrée ; **aucun** avertissement après un brouillon enregistré dont la confirmation a échoué | — | Q-04-194 |
| 27 | editSuccess | ADAPTÉ | « Ordonnance médicale enregistrée — RX-… : révision N confirmée » ; détail « Dernière révision confirmée — révision N » | `P04A_CLOT_navigateur_72-d0417-enregistree-revision1`, `…_42-etat27-revision-enregistree` | Q-04-190, Q-04-194 |
| GAP-04-07 | révisions et comparaison | RÉALISÉ (PROTO) | titre « Historique des modifications » | `P04A_CLOT_navigateur_23-gap07-revisions-comparaison` | Q-04-198 |
| GAP-04-09 | conflit de version | RÉALISÉ | 409 à l'enregistrement : inchangé ; 409 à la confirmation : brouillon annoncé enregistré, « Recharger » et « Laisser en brouillon » | `P04A_CLOT_navigateur_40-gap09-conflit-deux-postes`, `…_74-d0417-modification-conflit-confirmation` | Q-04-194, Q-04-199 |
| GAP-04-10 | confirmation | RÉALISÉ (PROTO) | récapitulatif retiré du parcours principal du formulaire (O-04-35) ; conservé sur le détail pour confirmer un brouillon existant | — | Q-04-190 |
| GAP-04-13 | rectification en cours (E-02) | RÉALISÉ (PROTO) | « Modification en cours » ; « en vigueur » retiré | `P04A_CLOT_navigateur_21-gap13-rectification-en-cours` | Q-04-198 |

**Synthèse courante : inchangée — 50 états tracés, 22 RÉALISÉ, 21 ADAPTÉ, 1 AUTRE COMPOSANT, 1 ADAPTÉ partiel, 3 EN ATTENTE (avis métier, G04-METIER), 2 REPORTÉ (BL-04-01).** Statut : `PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` (revue finale du propriétaire).

