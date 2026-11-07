# Phase 05 — Livraison du lot UX-IA (03/11/2026)

**Statut : `PHASE05_UX_AI_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** Revue visuelle et audit final du propriétaire requis ; la conformité visuelle n'est pas déclarée. Branche locale `feat/phase-05-catalogue-stock`, commits locaux uniquement ; aucun push, aucune PR, aucun merge ; Phase 06 non commencée ; `apps/desktop/.env.development` du propriétaire intact (SHA-256 `a215e1f0…`), jamais indexé.

Mandat : [`prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md`](../../../prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md). Registre (préflight, décisions, écarts, contrats, banc IA) : [`UX_IA_05.md`](UX_IA_05.md). Preuves : [`evidence/uxai/`](evidence/uxai/).

## 1. Ce qui change pour l'opticien

| Domaine | Avant | Après |
| --- | --- | --- |
| Création de produit | Tarif facultatif (« Plus tard »), un seul fournisseur, pas de seuil | Cinq étapes conservées ; **tarif obligatoire** pour chaque variante (coefficient avec aperçu serveur, ou prix manuel autorisé) ; plusieurs fournisseurs dont un principal, référence et prix fournisseur par variante ; prix d'achat repris du fournisseur principal ; **quantité initiale** par emplacement (emplacement unique choisi d'office) et **seuil d'alerte** par magasin ; récapitulatif complet ; seuil modifiable depuis la fiche produit. |
| Import CSV | Correspondance colonne par colonne à vérifier | Déposer → MMV analyse (encodage, séparateur dont « \| », règles, correspondance apprise du fournisseur, **assistant IA local** pour le reste) → seules les précisions nécessaires (famille, politique, montant à confirmer…) → récapitulatif compact → « Importer N lignes ». Correspondance du fournisseur apprise à chaque import ; réimport du même fichier annoncé et sans effet. |
| Verres | Deux espaces (« Gammes et offres » / « Référentiel de base »), création en plusieurs écrans | **Catalogue des verres** unique : chaque gamme avec ses caractéristiques et ses fournisseurs ; « Nouvelle gamme » en six étapes, plusieurs fournisseurs aux plages, prix et délais propres, mode simple par défaut et mode avancé (restrictions conditionnelles, combinaisons, fabrication spéciale). |
| Magasin | Magasin de l'installation (COR-05-01) | Inchangé, revérifié : aucune route d'écriture de magasin, nouvelles routes filtrées par le magasin de l'installation. |

## 2. Données, schéma, contrats

Aucune migration, aucune nouvelle table, aucune nouvelle permission ; aucune base réelle touchée (tous les essais sur le cluster jetable 55433). Nouvelles routes : `POST /api/catalog/lens-catalog`, `GET /api/imports/assistant`. Contrats étendus de façon additive ; seul changement de comportement : `POST /api/catalog/products` refuse une variante sans tarif (décision APPROVED). Détail : [`UX_IA_05.md`](UX_IA_05.md) § 4.

## 3. Tests (arbre livré)

| Suite | Résultat |
| --- | --- |
| `MMV.Api.Tests` | **561 / 561** |
| `MMV.Maintenance.Tests` | **50 / 50** |
| `MMV.PostgreSql.Tests` (cluster jetable 55433) | **96 / 97** + 1 `NOT_TESTED` (processus HTTPS réel non désigné dans ce mode) |
| Vitest ; `npm run check` ; ESLint ; `vite build` | **326 / 326** ; OK ; 0 erreur (1 avertissement préexistant) ; OK |
| Natif : deux postes Tauri (administrateur / vendeur), API de banc réelle, IA locale réelle | **33 / 33 PASS** — 26 captures |
| Banc IA réel (`llama3.2`, `llama3`) | consigné — [`UXAI_ollama_benchmark_20261103.log`](evidence/uxai/UXAI_ollama_benchmark_20261103.log) |

Synthèse : [`UXAI_suites_20261103.log`](evidence/uxai/UXAI_suites_20261103.log) ; matrice des exigences : [`QA_MATRIX.md`](QA_MATRIX.md) § « Lot UX-IA ». Le magasin et les migrations des bases réelles n'ont pas été relus en direct (`NOT_RERUN`, voir `UX_IA_05.md` § 1).

## 4. Performances mesurées

Lecture du fichier 0 à 6 ms ; assistant IA (`llama3.2`, modèle chargé) 8,3 à 8,7 s en natif réel, 4,5 à 20 s au banc ; premier appel après déchargement du modèle ≈ 36 s ; contrôle à blanc 17 ms et import 19 ms pour 3 lignes ; réimport : aucune inférence. Sans assistant (absent, lent, réponse invalide) : analyse par règles, aucun blocage.

## 5. Défauts trouvés et corrigés pendant le lot

1. Schéma de sortie « tableau » : `llama3.2` répétait les colonnes jusqu'au délai → schéma à clés fixes et génération bornée.
2. En réel, l'IA a rattaché une colonne de prix à « politique de stock » → contrôle déterministe des valeurs (D-UX-05).
3. Apprentissage automatique du séparateur décimal : aurait masqué l'ambiguïté « 1,234 » → jamais appris (D-UX-06).
4. Référence fournisseur répétée dans un même fichier non signalée → refus par ligne.
5. Bandeau « Brouillon repris / Recommencer » invisible hors étape 1 → affiché à l'étape de reprise.
6. Récapitulatif d'import hors écran à 1366 × 768 → l'écran conduit à l'analyse puis au récapitulatif.
7. Libellés de cellules trop longs, « indice Indice 1,6 », pictogrammes d'aide décalés, libellé de durée trompeur après import → corrigés.

## 6. Points ouverts pour le propriétaire

- **Revue visuelle** des nouveaux écrans (captures `evidence/uxai/`, clair, sombre, fenêtre réduite).
- **Q-UX-01** : imposer aussi le tarif hors assistant (ajout de variante depuis la fiche, route historique `POST /api/catalog/models`, import catalogue avant le passage « Prix du magasin ») ?
- **Q-UX-02** : seuil d'alerte réservé à `stock.adjust` (comme l'écran Stock) — à confirmer.
- **Q-UX-03** : confirmer les temps de l'IA sur le matériel réel des magasins ; modèle configurable.
- Toujours ouverts : logo sombre (O-05-17), G05-FISCAL / G05-LEGAL, restauration complète des sauvegardes non éprouvée.

## 7. Archive d'audit

`MMV-V2-phase05-uxai.zip` (racine du dépôt, non versionnée) : mandat, documents de la Phase 05 modifiés, preuves `evidence/uxai/` et correctif `git format-patch` des commits du lot. Empreinte communiquée dans le compte rendu de livraison.
