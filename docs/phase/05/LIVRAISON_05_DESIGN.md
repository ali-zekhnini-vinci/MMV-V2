# 05 — LIVRAISON_05_DESIGN — refonte fidèle au Lot 6 (G05-DESIGN)

**Statut (31/10/2026) : `PHASE05_UX_LOT6_ALIGNED — OWNER_DESIGN_FINAL_REVIEW_REQUIRED`.** Branche locale `feat/phase-05-catalogue-stock` ; commit local seulement — **aucun push, aucune PR, aucun merge, Phase 06 non commencée**. L'acceptation visuelle appartient au propriétaire ; rien n'est déclaré « conforme » sur la seule base des tests.

Matrice cadre par cadre : [`G05_DESIGN_MATRIX.md`](G05_DESIGN_MATRIX.md). Captures et journaux : [`evidence/design/`](evidence/design/). Archive d'audit (hors Git) : `MMV-V2-phase05-design-lot6.zip` (racine du dépôt, non suivie).

## 1. Mandat et sources

| Élément | Référence | SHA-256 |
|---|---|---|
| Mandat G05-DESIGN NO-GO | [`prompts/MMV_V2_PROMPT_CLAUDE_PHASE05_REDESIGN_LOT6.md`](../../../prompts/MMV_V2_PROMPT_CLAUDE_PHASE05_REDESIGN_LOT6.md) (identique à la copie du ZIP de comparaison) | `c5b7cd63…c431360` |
| Audit de fidélité du propriétaire | `MMV_V2_PHASE05_AUDIT_FIDELITE_LOT6.md` dans `MMV_V2_LOT6_COMPARAISON_VISUELLE.zip` (non versionné : images de comparaison) | `359e5219…bbe1dfe` (fichier) ; ZIP `5b36c347…72e852f8` |
| Design original | `MMV initialisation projet V1 (7).zip` → `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`, `_ds/`, `support.js` | ZIP `d214daa7…a271b9c52` ; HTML `6ca2cec0…bbc35e7`, voir [`SOURCE_MAP.md`](SOURCE_MAP.md) (empreinte **identique** à celle déjà enregistrée) |

Priorité appliquée (mandat § 1) : décisions V2 (Q01–Q20, D-05) > design system et shell V2 > Lot 6 pour la composition. Aucune fonctionnalité retirée ; aucune règle, aucun DTO, aucune donnée de l'ancien MMV repris (les noms de fournisseurs et coordonnées de la maquette ne servent qu'à la comparaison, hors Git).

## 2. Ce qui a changé (interface seulement)

Fichiers : `apps/desktop/src/catalog/*` (écrans, composants, CSS), `apps/desktop/e2e/run-native.mjs` et `e2e/cdp.mjs` (banc natif), tests `catalog.test.ts`. **Aucun fichier backend, SQL, migration ou contrat d'API modifié** (`git diff 83036c3 -- src tests` vide ; manifeste `sql/0007_MANIFEST.sha256` 12 / 12 OK).

- **Vocabulaire du Lot 6 réutilisé, pas dupliqué** : les écrans emploient les classes V2 déjà portées des Lots 2–3 (`Clients.css` : en-tête `.phead`, barre d'outils, recherche, puces `.chipbtn`, panneau de table `.tbl` / `table.dt` / `.tbl-foot`, badges, états, champs) et les composants V2 (`Button`, `DropdownMenu`, `Popover`, Radix `AlertDialog`). [`Catalog.css`](../../../apps/desktop/src/catalog/Catalog.css) ne contient que les compléments propres au Lot 6 (cartes `.card`, sections de formulaire, `.refbar`, cartes KPI `.statmini`, `.kv2`, pastilles de famille `.cat`, `.stockcell`, `.marg`, `.lock`, `.seg`, `.qtybox`, `.diff`, modales `.modal` / `.modal.wide`, bandeaux, toast) — jetons MMV clair / sombre ; seules trois teintes de famille sans jeton reprennent les valeurs du Lot 6 (test dédié).
- **Kit de composition** ([`CatalogUi.tsx`](../../../apps/desktop/src/catalog/CatalogUi.tsx)) : en-tête d'écran (fil « ‹ Retour », titre + badges, actions), barre d'outils (recherche sans bouton, puces de filtre à menu, puce bascule, tri), table en carte à hauteur d'écran (défilement interne, pied visible, pagination), cartes, sections de formulaire deux colonnes, pied d'actions collant (état « modifications non enregistrées »), bandeaux, modale du Lot 6 (piège de focus Radix), KPI, bandeau de fraîcheur, toast.
- **Module** : sections en commande segmentée + **un seul** sélecteur du magasin actif (devise EUR / MAD) dans la barre de contexte — le shell commun des Phases 02–04 n'est pas modifié (le Lot 6 range Inventaire et Fournisseurs dans la barre latérale).
- **Catalogue** : filtres sur une rangée (Famille, Fournisseur, Disponibilité, Politique, Statut, Tri), liste compacte, fournisseur principal ou « Fiche incomplète », coefficient et prix d'achat HT seulement si servis (costs.read).
- **Produit** : formulaire en cartes (Informations générales, Variantes en sous-cartes deux colonnes, Prix, Stock, Détails de la famille) ; **SKU interne généré automatiquement** (correctif Q05 du mandat : préfixe de famille + 6 symboles tirés par `crypto.getRandomValues`, lecture seule, « Personnaliser » facultatif, unicité garantie par le serveur — un doublon improbable est refusé et un nouveau SKU proposé) ; fournisseur principal facultatif rattaché à chaque variante après la création (même route que la fiche ; échec signalé, fiche incomplète autorisée) ; écran de **modification du modèle** (route PUT existante) avec 409, non enregistré et fermeture confirmée (garde du shell).
- **Fiche** : hiérarchie du Lot 6 (fil, titre + famille + statut, Modifier + menu ⋮, fraîcheur ; colonne principale + colonne 380 px). Bloc **Prix & disponibilité** : prix de vente HT validé, mode coefficient / manuel, état, TTC « indisponible » sans taux inventé, prix d'achat HT, coefficient, coefficient effectif, marge (costs.read) ; « Modifier le prix » ouvre dans le bloc : prix d'achat + coefficient → **aperçu calculé par le serveur** (seule une valeur aperçue est enregistrable, toute saisie l'invalide), prix manuel (pricing.manual) + motif, recalcul explicite ; stock par emplacement (en main / réservé / disponible). Fournisseurs multiples (principal, références, délai, prix fournisseur sous costs.read), historique des prix et de stock.
- **Stock** : quatre KPI (références suivies, unités en main, stock faible et rupture **comptés par le serveur**), fraîcheur + Actualiser, puces, table par emplacement avec réservé ; modale de mouvement du Lot 6 (Entrée / Sortie / Ajustement, motifs codés, aperçu « stock actuel → nouveau stock » jamais négatif — « Refusé » si la sortie dépasse), confirmation d'ajustement, 409 « le stock a changé », stock insuffisant, erreur technique. Mouvements (filtres Type / Produit / Période / Emplacement, détail), réceptions (modale large).
- **Inventaires** : liste, nouvel inventaire (choix en cartes, emplacements, aveugle), session avec **barre de progression**, comptage −/+, statuts Compté / À compter, vues Saisie / Comparaison / Écarts, motif par ligne, confirmation (lignes modifiées à accepter explicitement), terminé, abandon ; bandeau de fraîcheur ajouté (le comptage n'est jamais présenté comme temps réel).
- **Fournisseurs** : liste, filtres Pays / Statut, formulaire en cartes (fiche incomplète autorisée), modification dédiée, détail (coordonnées + produits associés), archivage (jamais de suppression). **Référentiel des verres** et **Import CSV** (étapes, contrôle à blanc, tout ou rien) dessinés avec les mêmes composants.

## 3. Comparaison visuelle

- Source : les **107 cadres** (+ notes) du HTML original rendus dans Edge sans interface, icônes Lucide chargées ([`G05D_render-lot6.mjs.txt`](evidence/design/G05D_render-lot6.mjs.txt), [`G05D_lot6_frames.tsv`](evidence/design/G05D_lot6_frames.tsv)).
- Réalisation : **146 captures natives** Tauri / WebView2, écrans réels branchés à MMV.Api (HTTPS 7367, `mmv_v2_test`, données fictives) — clair et sombre en 1920×1080 ; 1366×768 et 1024×700 (clair) ; zoom 125 % (catalogue, nouveau produit, fiche, stock, inventaire) ; états critiques (chargement, erreur technique par refus réseau côté WebView2, 409, quantité invalide, succès, refus d'archivage, archivage / réactivation, inventaire modifié pendant le comptage, clôture). **146 planches côte à côte** dans l'archive ([`G05D_pairs.tsv`](evidence/design/G05D_pairs.tsv)).
- Mesure (catalogue, 1920×1080, positions DOM) : barre d'outils à y = 218, en-tête de table à y = 275 (Lot 6 : 132–166 et 181 ; livraison du 30/10 : ≈ 460). Écart résiduel ≈ +94 px : barre des sections du module (58 px) et marges du shell commun.
- Matrice : 55 `MATCH`, 30 `ADAPTED_APPROVED`, 10 `ADAPTED_PROPOSED` (décision requise), 3 `DEFERRED_PROPOSED` (export CSV), 9 `NOT_TESTED` ([`G05_DESIGN_MATRIX.md`](G05_DESIGN_MATRIX.md)).

## 4. Tests

| Contrôle | Résultat | Preuve |
|---|---|---|
| `npm run check` (tsc ×3) | **PASS** | [`G05D_frontend_20261031.log`](evidence/design/G05D_frontend_20261031.log) |
| `eslint .` | **PASS** — 0 erreur, 1 avertissement préexistant (`ClientsTable.tsx`, TanStack Table) | idem |
| Vitest complet | **PASS 315 / 315** (28 fichiers ; +4 tests : SKU généré, alphabet, états du Lot 6, jetons de `Catalog.css`) | idem |
| `vite build` | **PASS** | idem |
| Tauri natif `catalogue-05` (fonctionnel, exécutable final) | **PASS — 96 contrôles** : six familles, création avec **SKU généré** et fournisseur principal relus par l'API, **409** (modification concurrente non écrasée), prix coefficient EUR puis MAD sans conversion (aperçu serveur), prix manuel (coefficient conservé), entrée motivée, **sortie excessive refusée par le serveur** (stock inchangé, jamais négatif), réception partielle, comptage d'inventaire, import CSV (contrôle à blanc sans écriture, application, réimport sans doublon, rejet tout ou rien), vendeur : aucun coût affiché ni servi, aucune action d'écriture | [`G05F_native_catalogue-05_20261031.log`](evidence/design/G05F_native_catalogue-05_20261031.log) |
| Tauri natif `design-05` (captures + états) | **PASS — 284 contrôles, 146 captures** ; aucun défilement horizontal de page à 1366×768 et 1024×700 ; mouvement de stock **au clavier seul** relu par l'API (+1) | [`G05D_native_design-05_20261031.log`](evidence/design/G05D_native_design-05_20261031.log) |
| Données fictives du banc | purgées après chaque passe ; résidu `mmv_v2_test` = **0** à la fin | journaux du banc (archive) |
| Tests backend (`MMV.Api.Tests`, `MMV.PostgreSql.Tests`) | **NOT_RERUN** — aucun fichier backend, SQL ou contrat modifié (derniers résultats : [`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md)) | — |

Banc : copie du build `bin/g05` (inchangé depuis le 30/10) lancée sur `https://localhost:7367` (`mmv_v2_test`) ; exécutable `com.mmv.v2.p05test` construit avec `VITE_MMV_API_BASE_URL` et `VITE_MMV_CATALOG_MODULE` **au build de banc seulement** ; outil de semis jetable étendu (produits, fournisseurs, seuils, inventaire complet — [`G05D_harness_p05native.cs.txt`](evidence/design/G05D_harness_p05native.cs.txt)). Processus du propriétaire jamais touchés (MMV.Api 7267, `mmv-desktop`) ; `apps/desktop/.env.development` intact (`58d1eb06…c3481d`).

## 5. Points ouverts et décisions attendues

1. **Adaptations proposées** (`ADAPTED_PROPOSED`) : filtres en menus (04–07), historique de stock étendu (30), menu ⋮ sur la fiche (35), conflit de stock détecté à l'écriture (50), inventaire modifié pendant le comptage en bandeau (80), filtres fournisseurs Pays / Statut (85), fenêtres réduites (103–106).
2. **Export CSV** (14, 54, 102) : non implémenté, report BL-05-09 à ratifier.
3. **O-05-17** : logo du shell sur fond blanc en thème sombre — actif approuvé du Shell-Handoff (Phase 02) ; le Lot 6 utilise `logo-dark.png`. Shell commun non modifié sans votre accord.
4. **SKU généré** (O-05-20) : génération sur le poste, unicité par le serveur ; le registre QR-08 « saisi » est complété par une entrée datée, sans réécriture.
5. Prix d'achat affichés avec la précision servie par l'API (4 décimales, « 41,2500 € ») : aucun arrondi sur le poste (règle du mandat) — le Lot 6 montre 2 décimales.
6. États `NOT_TESTED` (9) : vides (10, 51, 97), chargements (52, 99), désactivé en formulaire (24), erreur technique d'archivage (42), refus fournisseur utilisé (95), ordre de tabulation annoté (109).

**Statut : `PHASE05_UX_LOT6_ALIGNED — OWNER_DESIGN_FINAL_REVIEW_REQUIRED`** — acceptation visuelle du propriétaire requise avant toute PR.
