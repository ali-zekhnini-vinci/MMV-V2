# 05 — QA_MATRIX

Statuts : `PASS` (exécuté, preuve citée) ; `NOT_TESTED` (prévu, rien d'exécuté) ; `NOT_RERUN` (non rejoué depuis le dernier changement). En 05-00 seule de la documentation a été écrite : **aucun test applicatif exécuté** ; .NET, PostgreSQL, frontend, HTTPS et natif sont `NOT_RERUN` depuis `dcc6f0d` (code inchangé).

## 05-00 — contrôles exécutés le 29/10/2026

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-05-001 | références distantes à jour | `git fetch origin --prune` | PASS — aucune nouvelle référence ; `origin/main` = `0a83a57a335369a5dfe54de66fd2fc376b6fb383` (merge PR #5) |
| Q-05-002 | `main` local = `origin/main` | `git rev-parse main origin/main` ; `git rev-list --count` dans les deux sens | PASS — identiques, 0 / 0 |
| Q-05-003 | commits Phase 04 inclus | `git merge-base --is-ancestor dcc6f0d main` (et `origin/main`) | PASS ; `97bfd5e`, `56dfb7e`, `3bb25a6` visibles dans `git log` |
| Q-05-004 | merge sans écart | `git diff main origin/feat/phase-04-00-cadrage --stat` | PASS — vide (arbre identique à `dcc6f0d`) |
| Q-05-005 | arbre de travail propre | `git status --short` | **FAIL (constat)** — modification suivie du propriétaire `apps/desktop/.env.development` (+1 ligne `VITE_MMV_OPTICAL_MODULE`) ; non suivis du propriétaire intacts. Non modifiés |
| Q-05-006 | branche dédiée | condition du mandat | **NOT_DONE** — `feat/phase-05-catalogue-stock` non créée (Q-05-005) ; O-05-02 |
| Q-05-007 | maquette Lot 6 authentique | SHA-256 de `Downloads/MMV initialisation projet V1/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` comparé à `registres/DESIGN_SOURCES_SHA256.csv` | PASS — `6ca2cec0…bc35e7`, 175 994 octets, 37 / 107, `DRAFT_NON_APPROVED` |
| Q-05-008 | catalogue Lot 6 | `MMV_V2_Registre_134_Groupes.csv` (B-098 à B-134), `MMV_V2_Registre_324_Etats.csv` (`MMV-L06-*`) ; `data-mount` de la maquette | PASS — 37 groupes, 107 états ; 108 `data-mount` = 107 + notes |
| Q-05-009 | couverture de la matrice | [`MATRICE_LOT6.md`](MATRICE_LOT6.md) | PASS — 37 / 37 groupes, 107 / 107 états affectés |
| Q-05-010 | codes d'audit proposés compatibles | formats `ck_events_*` de `docs/phase/03/sql/0005_AuditJournal.sql` | PASS — codes de [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) § 5 conformes |
| Q-05-011 | aucune base, aucun processus touchés | aucune commande `psql`, `dotnet`, `npm`, aucun arrêt de processus | PASS (déclaratif, aucune commande de ce type dans la session) |
| Q-05-012 | aucun fichier du propriétaire modifié | `git status --short` avant / après : seuls ajouts `docs/phase/05.md`, `docs/phase/05/`, `prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md` et modifications de `README.md`, `docs/phase/04.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md` | PASS (à la livraison) |
| Q-05-013 | liens internes des documents 05 | vérification de chaque chemin relatif cité | PASS (à la livraison, voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |

## 05-00 clôture — contrôles exécutés le 30/10/2026

Toujours **aucun test applicatif exécuté** : .NET, PostgreSQL, frontend, HTTPS et natif restent `NOT_RERUN` depuis `dcc6f0d` (aucun code modifié). Q-05-005 (FAIL, constat) et Q-05-006 (NOT_DONE) du 29/10 sont conservés ; leur cause est levée par Q-05-015.

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-05-014 | `main` à jour | `git fetch origin --prune` ; `git rev-parse` ; `git rev-list --left-right --count main...origin/main` | PASS — `0a83a57a335369a5dfe54de66fd2fc376b6fb383` partout, 0 / 0 |
| Q-05-015 | branche créée sans altérer les fichiers du propriétaire | `git switch -c feat/phase-05-catalogue-stock` ; SHA-256 de `apps/desktop/.env.development` avant / après | PASS — aucun conflit ; `58d1eb064bac4c3a57cf6eb1184a01fd500f39b816a6c1352d32c3fa56c3481d` identique ; fichier jamais indexé ni commité ; aucun `reset`, `clean`, `stash` |
| Q-05-016 | historique du cadrage initial | commit `6c207f9` = documents 05-00 du 29/10 sans modification | PASS |
| Q-05-017 | copies Lot 6 identiques aux originaux | SHA-256 de chaque fichier copié comparé à l'original du dossier du propriétaire | PASS — 15 / 15 identiques |
| Q-05-018 | manifeste | `cd references/phase05 && sha256sum -c SHA256SUMS.txt` | PASS — 15 OK |
| Q-05-019 | maquette authentique | SHA-256 = `registres/DESIGN_SOURCES_SHA256.csv` | PASS — `6ca2cec0…bc35e7`, 175 994 octets |
| Q-05-020 | index Lot 6 | script : registres (Lot 6) ↔ `data-mount` de la maquette | PASS — 37 groupes, 107 états, 108 montages (107 + `notes`), 0 état absent |
| Q-05-021 | références des Phases 02 à 04 intactes | `find references/phase02 phase03 phase04 -newermt 2026-10-29` | PASS — 0 fichier modifié sur 54 |
| Q-05-022 | décompte par module de la matrice | script sur [`MATRICE_LOT6.md`](MATRICE_LOT6.md) § 1 | PASS — 37 + 26 + 11 + 21 + 12 = 107 (une première estimation manuelle erronée a été corrigée avant livraison) |
| Q-05-023 | exemples d'arrondi de la spécification | `decimal` Python, demi vers le haut sur positifs | PASS — 103,125 → 103,13 ; 43,978 → 43,98 ; 24,00 |
| Q-05-024 | liens internes des documents mis à jour | script : liens Markdown relatifs + chemins `docs/`, `prompts/`, `references/`, `registres/`, `src/`, `apps/` cités entre accents graves (dossiers « à créer » exclus), sur 16 documents (README, ADR, périmètre, `04.md`, `05.md`, `05/*.md`, `references/phase05/*.md`) | PASS — 192 liens, 262 chemins, 0 cassé (seul signalement : `docs/phase/01..16`, notation de plage préexistante dans `docs/PERIMETRE_ET_SEQUENCE.md` de `main`) |
| Q-05-025 | aucune base, aucun processus, aucune migration | aucune commande `psql`, `dotnet`, `npm`, aucun arrêt de processus, aucun fichier `0007` | PASS (déclaratif) |

## Plan de tests ajouté le 30/10/2026 (NOT_TESTED)

| ID | Cas | Lot |
|---|---|---|
| Q-05-120 | prix HT = arrondi(achat × coefficient) : cas limites (demi-centimes, 4 décimales, grands montants), égalité C# ↔ `round(numeric, 2)` PostgreSQL | 05-A |
| Q-05-121 | aperçu ≠ enregistrement → `409 price_preview_stale` ; aucune écriture | 05-A |
| Q-05-122 | modification du prix d'achat seule : prix de vente inchangé, état `recalculation_pending` ; recalcul explicite unitaire et par lot | 05-A |
| Q-05-123 | prix manuel sans `pricing.manual` → 403 ; avec : mode `manual`, historique | 05-A |
| Q-05-124 | coefficient, prix d'achat, coefficient effectif, marge absents sans `costs.read` (toutes routes, exports) | 05-A / 05-C |
| Q-05-125 | aucun montant sérialisé en nombre JSON ; aucun type flottant dans le schéma (requête sur `information_schema`) | 05-A |
| Q-05-126 | prix en devise ≠ magasin → `422 currency_mismatch` et refus SQL (FK composite) ; aucun total multi-devises dans les listes / exports | 05-A |
| Q-05-127 | TTC absent (`tax_not_validated`) sans fiscalité validée ; présent avec un taux de test fictif validé dans `mmv_v2_test` | 05-A |
| Q-05-128 | historique des prix immuable ; vente (contrat `ICatalogQuery`) non affectée par un changement ultérieur | 05-A |
| Q-05-129 | SKU unique toutes familles et magasins ; code-barres unique si présent | 05-A |
| Q-05-130 | variante `order_only` sans principal : enregistrée, `incomplete` ; `ICatalogQuery` la signale (QR-01) | 05-A |
| Q-05-131 | deux magasins fictifs EUR / MAD : prix, stock, seuils et alertes isolés par magasin | 05-B / 05-C |
| Q-05-132 | sortie manuelle sous le réservé → `422 stock_reserved` (réservé injecté en test) | 05-B |
| Q-05-133 | import : contrôle à blanc sans écriture ; application tout ou rien ; devise du fichier ≠ magasin refusée | 05-D |

## Plan de tests — métier (NOT_TESTED)

| ID | Cas | Lot |
|---|---|---|
| Q-05-101 | référence unique insensible à la casse / espaces ; GTIN invalide refusé | 05-A |
| Q-05-102 | attributs refusés hors jeu de la catégorie ; politique hors `allowed_policies` refusée | 05-A |
| Q-05-103 | `order_only` sans fournisseur principal refusé ; prix vide accepté pour `order_only` seulement | 05-A |
| Q-05-104 | changement de politique refusé avec stock > 0 ou inventaire ouvert ; accepté à 0 ; audité | 05-A / 05-B |
| Q-05-105 | archivage refusé avec stock > 0 ; article archivé absent des nouveaux inventaires et non vendable (`ICatalogQuery`) ; réactivation | 05-A |
| Q-05-106 | un seul fournisseur principal ; fournisseur archivé non éligible comme principal ; refus d'archiver l'unique principal d'un `order_only` actif | 05-A |
| Q-05-107 | historique des prix : chaque changement tracé (ancien, nouveau, auteur) | 05-A |
| Q-05-108 | **coûts absents** (clé non émise) dans liste, fiche, rattachements, historique, mouvements, exports, sans `catalog.costs.read` | 05-A / 05-B / 05-C |
| Q-05-109 | écriture d'un coût sans `catalog.costs.write` → 403, rien d'écrit | 05-A |
| Q-05-110 | chaque route refuse sans sa permission ; `system.admin` seul refusé partout | tous |
| Q-05-111 | événements d'audit dans la même transaction ; aucune valeur de coût dans `changes` | tous |
| Q-05-112 | disponibilité : table § 3.5 du cadrage, seuil vide, seuil 0, archivé | 05-B |
| Q-05-113 | modification sans changement : aucune écriture (O-03-44) | 05-A |

## Plan de tests — stock (NOT_TESTED)

| ID | Cas | Lot |
|---|---|---|
| Q-05-201 | entrée / réception : avant, après, écart, motif, auteur ; solde = Σ mouvements | 05-B |
| Q-05-202 | sortie au-delà du stock → `422 stock_insufficient`, aucun mouvement | 05-B |
| Q-05-203 | ajustement : écart calculé ; `expectedOnHand` faux → `409 stock_changed` ; écart nul → aucun mouvement | 05-B |
| Q-05-204 | motif obligatoire ; `other` exige un commentaire ; quantité non entière / ≤ 0 refusée | 05-B |
| Q-05-205 | mouvement sur `order_only` / `service` / archivé refusé | 05-B |
| Q-05-206 | journal non modifiable (`UPDATE` / `DELETE` refusés par déclencheur) | 05-B |
| Q-05-207 | réémission d'un mouvement (même `requestId`) : un seul mouvement ; `requestId` réutilisé avec un autre contenu → 409 | 05-B |
| Q-05-208 | invariant `on_hand = Σ quantity_delta` sur tout le jeu de test (requête de contrôle) | 05-B / 05-C |
| Q-05-209 | `IStockLedger` : document multi-lignes tout ou rien ; lignes `order_only` / `service` ignorées ; contre-mouvements liés | 05-B |

## Plan de tests — inventaires (NOT_TESTED)

| ID | Cas | Lot |
|---|---|---|
| Q-05-301 | ouverture refusée si un article est déjà dans un inventaire ouvert | 05-C |
| Q-05-302 | comptage à l'aveugle : quantité théorique absente de la réponse tant que la session est ouverte | 05-C |
| Q-05-303 | validation : écarts appliqués en un seul tout ou rien, mouvements liés à la session ; motif manquant → 422 sans effet | 05-C |
| Q-05-304 | ligne dont le stock a changé depuis le comptage : 409 sauf acceptation explicite ou recomptage | 05-C |
| Q-05-305 | lignes non comptées : exclues ou mises à 0 selon le choix ; jamais d'interprétation implicite | 05-C |
| Q-05-306 | abandon : aucun mouvement, session figée ; session validée figée | 05-C |
| Q-05-307 | permission de comptage seule ne permet ni validation ni abandon | 05-C |

## Plan de tests — concurrence multi-postes (PostgreSQL réel, barrières déterministes, deux ordres par conflit)

| ID | Conflit | Attendu |
|---|---|---|
| Q-05-401 | deux sorties simultanées dont la somme dépasse le stock | une réussit, l'autre `422`, solde ≥ 0 |
| Q-05-402 | deux ajustements sur la même quantité attendue | un réussit, l'autre `409 stock_changed` |
| Q-05-403 | archivage ‖ mouvement | sérialisés : soit mouvement puis refus d'archivage (stock > 0), soit archivage puis refus du mouvement ; jamais d'article archivé avec stock |
| Q-05-404 | changement de politique ‖ mouvement | jamais de mouvement sur un article devenu non stocké |
| Q-05-405 | validation d'inventaire ‖ sortie sur un article du périmètre | la ligne est signalée (`409`) ou la sortie attend ; solde final = constaté + mouvements postérieurs |
| Q-05-406 | deux validations / validation ‖ abandon de la même session | une seule réussit |
| Q-05-407 | deux documents multi-articles en ordres inverses (`IStockLedger`) | aucun interblocage (ordre croissant des identifiants) |
| Q-05-408 | modification concurrente d'un article (deux postes) | `409 version_conflict`, aucune perte silencieuse |
| Q-05-409 | ouvertures d'inventaires concurrentes sur des périmètres qui se chevauchent | une seule réussit |
| Q-05-410 | retrait de permission ‖ écriture de stock | comportement reconduit de la Phase 04 (session révoquée / droit relu dans la transaction) — à décider avec O-05-06 |

## Plan de tests — interface, HTTPS, natif

Captures comparées aux planches Lot 6 (clair / sombre, 1920×1080, 1024×700), parcours clavier B-132 à B-134, états transverses ; HTTPS réel (HTTP en clair refusé) ; Tauri natif sur les parcours principaux de chaque sous-lot ; résidu de test 0.

## Implémentation — résultats du 30/10/2026

Détail : [`LIVRAISON_05.md`](LIVRAISON_05.md) § 2. Résumé : `MMV.Api.Tests` 499/499 PASS ; `MMV.Maintenance.Tests` 50/50 PASS ; `MMV.PostgreSql.Tests --filter Stage=Phase05` 8/8 PASS **sur cluster jetable** (pas sur `mmv_v2_test`) ; script 0007 idempotent rejoué, `verify.sql` 27/27 OK (jetable) ; `tsc` / `eslint` PASS ; Vitest 308/309 (1 FAIL préexistant dû à `.env.development` du propriétaire, O-05-11). NOT_TESTED : application sur les bases du propriétaire, suites PostgreSQL complètes sur `mmv_v2_test`, HTTPS en processus réel, Tauri natif, captures Lot 6. Les plans « NOT_TESTED » ci-dessus restent valables pour la cible officielle.

## Intégration finale — résultats du 30/10/2026 (0007 appliquée, `mmv_v2_test`)

Détail et preuves : [`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md) §§ 1 à 4. Le résumé précédent est conservé tel quel (historique).

| Contrôle | Statut |
|---|---|
| Précontrôles G05-SQL (empreintes 12 / 12, scripts régénérés identiques, PostgreSQL 18.6 / 5433, identités, 0001–0006 sans 0007, dérive nulle, sauvegardes) | PASS |
| Application 0007 `mmv_v2_dev` puis `mmv_v2_test`, `verify.sql` | PASS — 27 / 27 ×2 ; données antérieures inchangées (empreintes de contenu) |
| Inventaire exact du schéma sur la base réelle | PASS — inventaire appliqué ; écart de 8 empreintes de fonction avec l'inventaire revu expliqué et testé (O-05-13) |
| `MMV.PostgreSql.Tests` complet, banc Phase 05 servi par MMV.Api réel (HTTPS) | PASS — 82 / 82, résidu 0 ; rejoué après la dernière modification : 81 / 82 + 1 ignoré (processus réel arrêté) |
| HTTP en clair (Phase 05) | PASS — refus `403 https_required`, rien écrit (O-05-15 : `403 forbidden` sans le droit) |
| `MMV.Api.Tests`, `MMV.Maintenance.Tests`, `Invoke-MmvSqlScript.Tests.ps1` | PASS — 499 / 499, 50 / 50, 16 / 16 |
| Vitest | PASS — 311 / 311, deux passages, aucun échec de délai (O-05-11 corrigé) |
| Tauri natif `catalogue-05` | PASS — 79 contrôles, 16 captures (clair 1920×1080, sombre 1920×1080 et 1366×768) |
| Captures 1024×700, parcours clavier B-132 à B-134 | NOT_TESTED |
| Conformité visuelle aux planches Lot 6 | NOT_TESTED par l'agent — revue du propriétaire (G05-DESIGN) |

Concurrence (plan Q-05-401 à 410) sur PostgreSQL réel :

| ID | Statut |
|---|---|
| Q-05-401 | PASS — deux sorties simultanées, 5 tours : une seule aboutit, solde ≥ 0 |
| Q-05-402 | PASS en séquence (quantité attendue périmée → `stock_changed`) ; simultanéité stricte NOT_TESTED |
| Q-05-403, Q-05-404 | PASS en séquence (archivage et changement de politique refusés tant que du stock existe) ; course NOT_TESTED |
| Q-05-405 | PASS en séquence (sortie pendant le comptage → ligne signalée, acceptation explicite, écart appliqué au stock courant) |
| Q-05-406, Q-05-407, Q-05-410 | NOT_TESTED |
| Q-05-408 | PASS — version périmée `409` (prix, ligne d'inventaire comptée deux fois) |
| Q-05-409 | PASS en séquence (`inventory_open` sur périmètre recouvrant) ; ouvertures strictement simultanées NOT_TESTED |

## Contrôles G05-DESIGN (31/10/2026)

Rapport : [`LIVRAISON_05_DESIGN.md`](LIVRAISON_05_DESIGN.md) § 4 ; matrice visuelle : [`G05_DESIGN_MATRIX.md`](G05_DESIGN_MATRIX.md).

| ID | Contrôle | Statut |
|---|---|---|
| Q-05-D01 | `npm run check`, `eslint .` (0 erreur, 1 avertissement préexistant), `vite build` | PASS |
| Q-05-D02 | Vitest complet 315 / 315 (dont SKU généré, états du Lot 6, jetons de `Catalog.css`) | PASS |
| Q-05-D03 | Tauri natif `catalogue-05` sur l'interface refondue : 96 contrôles (SKU généré, 409, prix serveur EUR / MAD, prix manuel, refus de stock négatif, réception, inventaire, import tout ou rien, vendeur sans coûts) | PASS |
| Q-05-D04 | Tauri natif `design-05` : 284 contrôles, 146 captures (clair / sombre 1920×1080, 1366×768, 1024×700, zoom 125 %, états critiques) | PASS |
| Q-05-D05 | Aucun défilement horizontal de page à 1366×768 et 1024×700 | PASS |
| Q-05-D06 | Mouvement de stock au clavier seul, relu par l'API | PASS |
| Q-05-D07 | Comparaison cadre par cadre (107) avec les rendus du HTML original | 55 MATCH, 30 ADAPTED_APPROVED, 10 ADAPTED_PROPOSED, 3 DEFERRED_PROPOSED, 9 NOT_TESTED |
| Q-05-D08 | Acceptation visuelle | **OWNER_DESIGN_FINAL_REVIEW_REQUIRED** |
| Q-05-D09 | Tests backend (`MMV.Api.Tests`, `MMV.PostgreSql.Tests`) | NOT_RERUN (aucun fichier backend, SQL ou contrat modifié) |
