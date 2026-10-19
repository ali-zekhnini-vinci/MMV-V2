# 04-A — Couverture des états (36 du Lot 3 + 12 complémentaires 04-00 + 2 issus des études)

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
