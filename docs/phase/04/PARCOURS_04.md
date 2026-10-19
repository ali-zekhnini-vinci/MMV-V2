# 04 — Parcours d'acceptation (PROPOSED)

Parcours J-04-01 à J-04-06 du cadrage (`references/phase04/CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md` § 4), précisés pour MMV V2 tel qu'il existe au 19/10/2026 (fiche client à onglets, `ClientPicker`, garde de sortie, notices persistantes, journal d'audit). Les états visuels renvoient à [`STATE_MAP_36.md`](STATE_MAP_36.md), les droits à [`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md), les routes à [`API_OPTICAL_PROPOSED.md`](API_OPTICAL_PROPOSED.md). Aucun parcours n'est implémenté.

## J-04-01 — Depuis la fiche client (prioritaire)

| Étape | Écran / état | Droits | Appel |
|---|---|---|---|
| 1 | fiche d'un client **actif**, onglet « Dossier optique » (GAP-04-04) | `clients.read`, `optical.read` | `GET /api/optical/clients/{id}/records` |
| 2 | deux sections « Ordonnances médicales » / « Mesures magasin », vides ou listées (états 5, 7) | idem | — |
| 3 | « Ajouter… » → **choix explicite du type** (GAP-04-01) ; chaque bouton visible selon le droit d'écriture du type | `optical.prescriptions.write` ou `optical.measurements.write` | — |
| 4a | **Ordonnance médicale** : formulaire, client prérempli (états 10, 11), provenance (D-04-03.a), OD/OG | — | — |
| 4b | **Mesure magasin** : formulaire distinct (GAP-04-02), date de mesure, collaborateur, catégorie (D-04-03.b), OD/OG | — | — |
| 5 | enregistrer en **brouillon** (états 14, 16) ou enregistrer et **confirmer** (GAP-04-10) | idem | `POST …` puis `POST …/confirm` |
| 6 | détail de l'enregistrement, badge de type et de statut (état 19 / GAP-04-03), notice persistante | `optical.read` | `GET …/{id}` |
| 7 | « Retour à la fiche » : onglet « Dossier optique » rouvert, **aucune nouvelle recherche** | — | — |

Refus : client archivé → état 18 (D-04-07) ; quitter avec saisie → état 26 ; erreur technique → message borné, saisie conservée.

## J-04-02 — Depuis la liste globale

Menu « Dossiers optiques » (D-04-08.b) → liste paginée (états 1–4, 6, 8, 9) → « Nouveau… » → choix du type → sélection du client actif (états 12, 13 ; `ClientPicker`) → formulaire, client conservé pendant la saisie → enregistrement → détail. Sans `optical.read` : entrée de menu désactivée « accès non autorisé » ; accès direct → 403 (GAP-04-05).

## J-04-03 — Consultation en équipe

Un compte avec `optical.read` voit le dossier, les révisions et la comparaison ; sans droit d'écriture, détail en lecture seule (état 22). Sans `optical.read` : onglet absent, aucune donnée, ni aperçu ni téléchargement ; l'appel direct de l'API répond 403 ; l'historique ne contient pas les événements optiques. Les pièces exigent `optical.documents.read` (GAP-04-06). Les rôles visuels n'ouvrent aucun droit.

## J-04-04 — Rectification et historique

Détail d'un enregistrement confirmé → « Rectifier » (état 23) → formulaire prérempli de la révision courante → motif obligatoire (D-04-04.b) → enregistrement → révision n+1 (état 27) → « Révisions » : liste et comparaison champ à champ (GAP-04-07) → « Historique » de la fiche : événement `prescription.revised` avec auteur, date, numéro et motif codifié — **aucune valeur** optique. La révision n reste consultable et inchangée.

## J-04-05 — Prêt pour la vente (inerte)

Depuis le détail ou le dossier : « Préparer une vente » **visible et désactivée**, « Phase ultérieure » (GAP-04-12) ; proposée seulement sur un enregistrement confirmé non invalidé, et jamais pour une mesure magasin là où une ordonnance médicale serait exigée. Aucun contexte transmis, aucun brouillon de vente, aucun montant.

## J-04-06 — Deux postes

Postes A et B ouvrent la même ordonnance (même `version`). A rectifie et enregistre ; B enregistre ensuite → 409 `version_conflict` / `revision_conflict` (GAP-04-09) : message clair, saisie de B conservée à l'écran, rechargement proposé ; **rien n'est écrit** pour B, aucun événement d'audit. Les pièces restent rattachées à l'ordonnance quelle que soit la révision.

## Parcours complémentaires

| Réf. | Parcours | Points clés |
|---|---|---|
| J-04-07 | Invalidation | menu secondaire « Invalider » (état 28) → motif (état 29) → bandeau « Invalidé » (état 30, GAP-04-08) ; exclu des listes par défaut ; consultable ; erreur → état 31 |
| J-04-08 | Pièce source | « Joindre une pièce » (PDF / JPEG / PNG, taille bornée) → liste des pièces → aperçu dans le poste (événement `document.viewed`) ; refus de format / taille, accès refusé (GAP-04-06) |
| J-04-09 | Fenêtre réduite et clavier | états 32 à 36 ; formulaire utilisable sans souris ; zoom 100 / 125 / 150 %, thèmes clair / sombre |

## Amendement daté — 19/10/2026 (études préalables)

Parcours ci-dessus conservés. Précisions proposées (D-04-10 à D-04-12, `PROPOSED`) :

- **J-04-04** : « Rectifier » ouvre un **brouillon de rectification** ; pendant ce temps la fiche et le détail continuent d'afficher la **dernière révision confirmée**, avec le bandeau « rectification en cours » (GAP-04-13). La révision n+1 n'existe qu'à la confirmation ; un abandon laisse la révision n courante. Un second poste qui tente d'ouvrir une autre rectification reçoit `409 draft_exists`.
- **J-04-06** : les pièces restent rattachées à l'enregistrement ; chaque révision confirmée conserve **ses** liens (E-01).
- **J-04-08** : « Afficher la pièce » produit l'événement `document.content_served` — l'historique indique « contenu transmis au poste de … », jamais « consulté » ; aucun aperçu n'est chargé sans action explicite (E-03).

## Amendement daté — 19/10/2026 (04-A, démonstration sur le banc isolé)

Parcours **démontrables** sur le banc `apps/desktop/optical-bench.html` (données fictives, droits simulés, aucun serveur) et couverts par `apps/desktop/src/bench/OpticalBench.test.tsx` : J-04-01 (fiche → onglet → type → brouillon → détail → retour sur l'onglet), J-04-02 (liste → « Nouveau… » → type → client actif → formulaire), J-04-04 (rectification, révision 1 en vigueur, comparaison, révision 2), J-04-05 (« Préparer une vente · Phase ultérieure » inerte), J-04-06 (autre poste simulé → 409, saisie conservée), J-04-07 (invalidation motivée), J-04-08 (pièces synthétiques, « transmis »), J-04-09 (clavier, fenêtre réduite, zooms — captures navigateur). J-04-03 : profils simulés (lecture seule, sans pièces, sans droit optique). **Aucun parcours n'est réalisé dans l'application réelle** (O-04-16) ; les appels décrits ci-dessus restent `PROPOSED`. Mode d'emploi : [`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 4.
