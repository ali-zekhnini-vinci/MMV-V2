# 05 — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 et maquette Lot 6 uniquement)

| Source | Usage |
|---|---|
| `prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md` | mandat 05-00 (copie versionnée du message du propriétaire) |
| `prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md` | réponses Q01–Q20 et mandat de clôture 05-00 (copie versionnée du message du propriétaire, 30/10/2026) |
| `CLAUDE.md`, `README.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/phase/00.md` | gouvernance, DOCUMENTATION GATE, registre des décisions, backlog et séquence (Phase 05 « avant le premier encaissement réel ») |
| `docs/phase/04.md`, `docs/phase/04/GATES_AND_DECISIONS.md`, `SOURCE_MAP.md`, `QA_MATRIX.md`, `API_OPTICAL.md`, `IMPLEMENTATION_SPEC.md` (impacts Ventes) | conventions de documentation, d'API, d'idempotence, de concurrence et de verrous (O-04-53 / O-04-54) |
| `docs/phase/03/sql/0005_AuditJournal.sql` | formats des codes d'audit |
| `src/MMV.Domain/Identity/Permissions.cs` | permissions existantes (format `domaine.action`, jusqu'à trois segments) |
| `src/MMV.Infrastructure/*/…Model.cs`, `…Store.cs` (recherche ciblée) | schémas existants (`auth`, `clients`, `appointments`, `audit`, `optical`), `xmin`, `archived_at_utc`, verrous |
| `registres/DESIGN_SOURCES_SHA256.csv` | empreinte attendue du Lot 6 |
| `references/phase04/catalogue/MMV_V2_Registre_134_Groupes.csv`, `MMV_V2_Registre_324_Etats.csv` | 37 groupes B-098 à B-134, 107 états `MMV-L06-*` (non dupliqués ; lignes Lot 6 extraites dans `references/phase05/INDEX_LOT6.md`) |
| `Downloads/MMV initialisation projet V1/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` (dossier du propriétaire, lecture seule) | sections, légendes de planches, notes de conception ; **référence visuelle et ergonomique seulement** (D-05-01 `APPROVED`) |
| `Downloads/MMV initialisation projet V1/` : `support.js`, `_ds/…`, `scraps/lot6-*.png`, liste des entrées de `MMV.zip` | ressources de consultation du Lot 6 et recherche d'autres références Lot 6 (30/10/2026) |

Aucune lecture de l'ancien dépôt, de son code, de ses DTO, de son modèle de données ni de ses migrations (les mentions de l'ancien code figurent dans le texte même de la maquette, O-05-01). Aucune connexion à `mmv_v2_dev` ni `mmv_v2_test`.

## Maquette Lot 6

| Fichier | SHA-256 | Taille | Registre |
|---|---|---|---|
| `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` | `6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7` | 175 994 octets | identique à `registres/DESIGN_SOURCES_SHA256.csv` (37 groupes, 107 états, `DRAFT_NON_APPROVED`) |

*29/10/2026 :* la maquette n'avait pas été copiée dans le dépôt ; un rendu de contrôle sera fait avec G05-DESIGN.

## Références Phase 05 — `references/phase05/` (30/10/2026)

**Copie de travail non versionnée**, comme `references/phase03/` et `references/phase04/` (O-05-08) ; les empreintes ci-dessous, versionnées, en sont la preuve. Copies **octet pour octet** depuis `Downloads/MMV initialisation projet V1/`, chaque empreinte comparée à l'original (Q-05-017) ; manifeste `references/phase05/SHA256SUMS.txt` (`sha256sum -c` : 15 OK, Q-05-018).

| Fichier (sous `references/phase05/`) | Octets | SHA-256 |
|---|---:|---|
| `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` | 175 994 | `6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7` |
| `lot6/support.js` | 64 222 | `ae4f0ac8449655e17cca1e3b179effcb6817a3b0d8dc47f112a9c39c25c39fd7` |
| `lot6/_ds/mmv-managemyvision-design-system-90800da0-d730-42af-b51e-c94dbc7d7135/styles.css` | 512 | `99d81fe93bb0fbf3019f816d52cb7fe1c894c3c1ae6b7f307d41097e86fbc859` |
| `lot6/_ds/…/_ds_bundle.js` | 126 785 | `88cf0aa328630a56fc5c47b763df7820615e967f27aab8c5b7bb2500f67e2eda` |
| `lot6/_ds/…/assets/logo-dark.png` | 66 344 | `3df57decabf76455374f0051b6dec006dffe8b55ad7aeabd6b6e3cce8e3856cb` |
| `lot6/_ds/…/assets/logo-horizontal.png` | 104 999 | `3570c6f9f0a08f60428e8fe1be6d426c0d9e4f9c5ce65eb7b128a311f1913a71` |
| `lot6/_ds/…/tokens/base.css` | 1 015 | `d5ca8e35701776c514e19d03f19f27c97fbe2594c6afd4b86a3c5d70ac40c320` |
| `lot6/_ds/…/tokens/colors.css` | 4 201 | `1a9bf7858d61e17b4aca196ec3e2469254dc4724dba378c78de04d48157ff92c` |
| `lot6/_ds/…/tokens/fonts.css` | 359 | `6d834a2bfbbcbe9fe38ffa7828775309bd8a1e5155c94b6a43d83b0b60c3918b` |
| `lot6/_ds/…/tokens/radii.css` | 647 | `0861f3679dcead974a2baa82b06b8eda07f3ef74ffd27e9f3397afc0419b4a66` |
| `lot6/_ds/…/tokens/shadows.css` | 903 | `aabd33b441a5937b34ef162729103d11466b750da4848a02707d007b961b8bdb` |
| `lot6/_ds/…/tokens/spacing.css` | 658 | `589f06b2d380ccbfe4ace5d0ee3a82edf2170ee3140604b0d30dc07787f5a70f` |
| `lot6/_ds/…/tokens/typography.css` | 1 465 | `da0dea77a4462e93ea66937c23f2ef822c54db4a60840166f91c28fae5132d7f` |
| `lot6/scraps/lot6-check.png` | 31 177 | `eb6cac0de244e7eea2d636303984d08624e954ace8242efd77b2521b44c420a2` |
| `lot6/scraps/lot6-s1.png` | 26 898 | `2a2d2ee09170beb7f4d19c13cb80705d6fff6924a48f91cf5628e186914b9a20` |

Fichiers générés (non sources) : `README.md` (contenu, statut, limites, consultation), `INDEX_LOT6.md` (37 groupes, 107 états, `data-mount`, module V2 indicatif), `SHA256SUMS.txt`.

Écartés : `MMV.zip` (3,6 Mo, archive des Lots 1 à 6 ; ses entrées Lot 6 ont les mêmes empreintes que les copies), `scraps/check-list.png` (non rattaché au Lot 6), `_ds/…/readme.md`, `_ds_manifest.json`, `_adherence.oxlintrc.json`, `assets/logo-monogram.png` (non chargés par la maquette). `support.js` et les fichiers `_ds` copiés sont identiques à ceux de `references/phase03/lot2/` (doublon de ~ 370 Ko accepté pour que la maquette reste consultable par chemins relatifs). Aucune autre référence Lot 6 trouvée dans `Downloads/`. Aucun état sans planche fabriqué.

## Mise à jour 30/10/2026 (implémentation) — versionnement partiel de `references/phase05/`

Versionnés : `README.md`, `INDEX_LOT6.md`, `SHA256SUMS.txt`, `.gitignore`, `lot6/support.js`, `lot6/_ds/…`. **Non versionnés** (identifiants de l'ancien logiciel et coordonnées de fournisseurs dont le caractère fictif n'est pas établi) : `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`, `lot6/scraps/*.png` — récupération et contrôle d'empreinte : `references/phase05/README.md` § « Versionnement ». Empreintes inchangées. Aucune source supplémentaire lue pour l'implémentation ; ancien dépôt non consulté.

## Mise à jour 30/10/2026 (G05-SQL accordée, intégration finale)

| Source | Usage |
|---|---|
| `prompts/MMV_V2_G05_SQL_GO_INTEGRATION_FINALE_2026-10-30.md` | mandat (copie versionnée du message du propriétaire) |
| `MMV_V2_AUDIT_G05_SQL_0007_2026-10-30.md` | **non disponible** dans l'espace de travail, non lu (O-05-18) |
| `docs/phase/05/sql/0007_MANIFEST.sha256` et les 12 fichiers qu'il couvre | précontrôle 12 / 12 OK ; aucun de ces fichiers modifié |
| `mmv_v2_dev`, `mmv_v2_test` (PostgreSQL 18.6, 5433) | lectures de contrôle en session lecture seule ; écritures : script approuvé (dev, test), données fictives de banc (test seulement), purgées |
| `docs/phase/04/LIVRAISON_04C.md`, `evidence/P04C_harness_c04http.cs.txt`, `apps/desktop/e2e/run-native.mjs` | procédés de banc reconduits (API copiée hors dépôt, Tauri renommé, CDP) |

Produits (nouveaux, versionnés) : [`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md) ; `sql/0007_CatalogStockModule.schema-inventory.applied.txt` (relu sur `mmv_v2_test` après application, SHA-256 `5bf96490ef341638ae2072e3a4ffaeff788b9b9970134f6ac4972dde153c3d63`, **hors manifeste 0007**, O-05-13) ; `evidence/P05F_*` (précontrôles, application, `verify.sql`, empreintes de contenu, PostgreSQL, .NET, frontend, natif, journal de l'API de banc, outil de banc, 16 captures).

## Sources de la refonte G05-DESIGN (31/10/2026)

| Source | Usage | SHA-256 |
|---|---|---|
| `prompts/MMV_V2_PROMPT_CLAUDE_PHASE05_REDESIGN_LOT6.md` | mandat (copie versionnée, identique à celle du ZIP de comparaison) | `c5b7cd6310a057d5b2550426bd292b60fd16d56d56bdffa7eab030f83c431360` |
| `MMV_V2_LOT6_COMPARAISON_VISUELLE.zip` (racine, **non suivi**) | audit `MMV_V2_PHASE05_AUDIT_FIDELITE_LOT6.md` (`359e5219e84deed7a1dd5bedc2265b8e41e94483a45f29917e0ef0db5bbe1dfe`), rendus de référence et comparaisons du propriétaire | `5b36c347dd31922e9242396584778907eba91f13b8a2b0198546e5d872e852f8` |
| `MMV initialisation projet V1 (7).zip` (racine, **non suivi**) | `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` (`6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7`, **identique** au fichier déjà enregistré), `_ds/`, `support.js` | `d214daa703cd8f8c4f2cdf6c44750c5a746f93387da61a4f292e624a271b9c52` |

Rendus des 107 cadres (Edge sans interface, [`evidence/design/G05D_render-lot6.mjs.txt`](evidence/design/G05D_render-lot6.mjs.txt)) et planches côte à côte : **non versionnés** (O-05-10 : identifiants de l'ancien logiciel et coordonnées de fournisseurs de la maquette) ; archive d'audit `MMV-V2-phase05-design-lot6.zip`. Versionnés : captures natives, journaux et correspondances dans [`evidence/design/`](evidence/design/).

## Mise à jour 02–03/11/2026 (corrections COR-05-01 à COR-05-08)

- Mandat : [`prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md`](../../../prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md) ; registre : [`CORRECTIONS_05.md`](CORRECTIONS_05.md) ; revue SQL : [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md) ; manifeste [`sql/0008_MANIFEST.sha256`](sql/0008_MANIFEST.sha256).
- Code serveur nouveau : `src/MMV.Api/Installation/ActiveStore.cs`, `src/MMV.Api/Realtime/Realtime.cs`, `src/MMV.Application/Catalog/ProductCreationService.cs`, `ImportAssistant.cs`, `LensOfferService.cs`, `src/MMV.Domain/Catalog/SkuGenerator.cs`, `LensOffer.cs`, `LensOffers.cs`, `src/MMV.Domain/CsvImport/SupplierImportProfile.cs`, `src/MMV.Infrastructure/Catalog/CatalogSearch.cs`, `CorrectionStores.cs`, migration `src/MMV.Infrastructure/Persistence/Migrations/20261102183203_CatalogCorrections.cs`.
- Poste nouveau : `apps/desktop/src/realtime/` (client SignalR, fournisseur, contexte), `apps/desktop/src/catalog/ProductWizard.tsx`, `productForm.ts`, `SupplierCombobox.tsx`, `catalogRealtime.ts`, `csvFile.ts`, `lensOffers.ts`, `LensOffersView.tsx`, `corrections.test.ts`.
- Outils de preuve : `apps/desktop/e2e/native-cor05.mjs` (scénario natif deux postes) ; preuves légères `docs/phase/05/evidence/corrections/` (42 captures JPEG réduites, journal de l'exécution complète, banc d'amorçage `COR05_harness_seed.cs.txt` sans secret).
- Référence visuelle consultée : `references/phase05/lot6/_ds/…/assets/logo-dark.png` (copie de travail non versionnée, empreinte `3df57dec…`, identique à l'actif du Shell-Handoff) pour O-05-17 ; aucune copie versionnée.
- Dépendances ajoutées : `@microsoft/signalr` 10.0.11 (poste, chargé à la demande) ; `Microsoft.AspNetCore.SignalR.Client` 10.0.13 (tests PostgreSQL seulement). Serveur : SignalR du cadre partagé ASP.NET Core (aucun paquet).
