# Phase 05 — Lot UX-IA : assistant produit, import CSV avec IA locale, catalogue des verres unifié (03/11/2026)

Mandat : [`prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md`](../../../prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md) (décisions métier APPROVED : magasin unique, tarification obligatoire, seuil d'alerte option A). Branche `feat/phase-05-catalogue-stock` (départ `f2bd748`), commits locaux uniquement : aucun push, aucune PR, aucun merge, Phase 06 non commencée. `apps/desktop/.env.development` du propriétaire intact, jamais indexé. Rapport de livraison : [`LIVRAISON_05_UX_IA.md`](LIVRAISON_05_UX_IA.md).

**Statut (03/11/2026) : `PHASE05_UX_AI_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** Aucune migration : le lot n'a demandé aucun G05-SQL. La conformité visuelle n'est **pas** déclarée : elle appartient au propriétaire.

## 1. Préflight

| Point vérifié | Constat |
| --- | --- |
| Documents Phase 05 | Registre des corrections, livraisons finale et corrections, matrice G05-DESIGN, choix techniques relus. |
| Git | Branche `feat/phase-05-catalogue-stock`, tête `f2bd748` ; seule modification locale suivie : `apps/desktop/.env.development` (propriétaire, non touché) ; archives et dossiers non suivis du propriétaire laissés en place. |
| Migrations 0007 / 0008 | Appliquées à `mmv_v2_dev` et `mmv_v2_test` selon les preuves du 03/11 ([`evidence/sql0008/`](evidence/sql0008/)) ; **non relu en direct** dans ce lot (`NOT_RERUN` : aucune connexion aux bases réelles, l'accès aux secrets a été refusé par la politique de l'environnement). Le code déclare toujours exactement les 8 migrations revues (`SchemaStateTests.Assembly_DeclaresExactlyTheReviewedMigrations`, PASS). |
| Magasin unique (COR-05-01) | Déjà en place : magasin résolu par `Installation:StoreCode` (ou unique magasin actif), filtre `RequireActiveStore` sur tous les groupes commerciaux, `storeId` étranger refusé (`404 store_not_found`), aucun écran de choix, création de magasin réservée à la console de maintenance. |
| Seuil d'alerte | Déjà modélisé par (variante, magasin) : `stock.variant_store_settings.low_stock_threshold`, règle `disponible ≤ seuil` sur le disponible AGRÉGÉ des emplacements actifs (`Availabilities.Compute`) — l'option A ne demande aucune migration. |
| Assistant produit | Mode « Plus tard » présent (tarif facultatif), un seul fournisseur, pas de seuil. |
| Import CSV | Moteur déterministe complet (encodage, séparateur, synonymes, montants, modèles fournisseur) ; aucune IA. |
| Verres | Deux espaces séparés (« Gammes et offres fournisseur » / « Référentiel de base ») ; création gamme et offres en plusieurs écrans. |
| IA locale | Ollama 0.40.11 sur `127.0.0.1:11434`, modèles `llama3.2` (3 B) et `llama3` (8 B) déjà installés ; poste de développement : Ryzen 7 5800X, 32 Go, GTX 1650 4 Go. |

## 2. Décisions appliquées

| ID | Décision |
| --- | --- |
| D-UX-01 | **Tarification obligatoire** dans l'assistant : `POST /api/catalog/products` refuse toute variante sans tarif (`422`, `variants[i].price: required`) ; deux modes seulement (coefficient avec aperçu serveur confirmé, prix manuel avec `pricing.manual`) ; « Plus tard » supprimé ; rien n'est créé si une seule variante est incomplète (transaction unique, inchangée). |
| D-UX-02 | **Seuil d'alerte option A** : champ `lowStockThreshold` par variante stockée dans l'assistant, enregistré pour le magasin de l'installation dans la même transaction (`StockService.SetThresholdAsync`, droit `stock.adjust` comme `PUT /api/stock/thresholds`, sinon `403 threshold_forbidden` avant écriture) ; refusé pour une prestation ou un article commandé (`not_allowed`). Modifiable ensuite depuis la fiche produit. Règle documentée : alerte « stock faible » quand `en main − réservé`, sommé sur les emplacements actifs du magasin, **atteint ou passe sous** le seuil ; un seul seuil par (variante, magasin), jamais par emplacement. |
| D-UX-03 | **IA locale** : Ollama sur le serveur du magasin uniquement (point d'accès limité à la boucle locale, sinon service désactivé), modèle configurable `ImportAssistant:Model`, **`llama3.2` retenu après banc** (§ 5). Une seule inférence par fichier, jamais par ligne, lancée seulement s'il reste une colonne non reconnue, une ambiguïté ou un champ requis absent. Sortie contrainte par un schéma JSON à clés fixes (une entrée par en-tête, champs et confiances énumérés), température 0, génération bornée. |
| D-UX-04 | **L'IA propose, le serveur valide** : règles sûres (modèle fournisseur, synonymes exacts) jamais remplacées ; colonne inexistante, champ hors type, confiance « low », doublons : écartés ; **aucun montant appliqué sur la seule foi de l'IA** (prix d'achat, coefficient, prix de vente : « à confirmer », préchoisi) ; valeurs jamais modifiées. |
| D-UX-05 | **Contrôle des valeurs** (ajouté après l'essai natif réel, où l'IA avait rattaché une colonne de prix à la politique de stock) : une proposition n'est retenue que si les exemples de la colonne sont compatibles avec le champ (codes de famille et de politique connus, booléen, EAN de 8 à 14 chiffres, montant lisible, devise et pays bien formés, e-mail, indice numérique). |
| D-UX-06 | **Apprentissage** : la correspondance d'un import appliqué devient le modèle d'import du fournisseur (même transaction, tracé `import_profile.saved`) ; le séparateur décimal n'est **jamais** appris (un montant ambigu « 1,234 » du fichier suivant reste signalé) ; une colonne déjà examinée par le modèle appris ne relance pas l'IA. |
| D-UX-07 | Moteur déterministe enrichi : séparateur « \| » (serveur et poste) ; fichier de montures sans colonne « variante » : le coloris sert de libellé de variante (proposé « probable »). |
| D-UX-08 | **Catalogue des verres unifié** côté écran seulement : une entrée « Catalogue des verres » (gammes, caractéristiques, fournisseurs), parcours « Nouvelle gamme » en six étapes créant fabricant (au besoin), gamme et offres de plusieurs fournisseurs en **une** transaction (`LensCatalogService`). Les entités restent séparées : références (`catalog.lens_references`) et offres par fournisseur (`catalog.lens_offers`, plages, restrictions, prix, délai, révision). Les caractéristiques communes (type, indice, matériau, traitements) sont recopiées dans chaque offre, puisque le modèle les porte par offre. Listes de référence (fabricants, gammes, indices, traitements) conservées, repliées en bas de page. |
| D-UX-09 | Mode simple par défaut : plage de sphère (et d'addition pour un verre à addition) ; mode avancé : cylindre, diamètres, restrictions conditionnelles, limites de combinaison, fabrication spéciale, provenance, validité, fiche complète. Aucune règle clinique n'est déduite ; un champ vide donne « à vérifier ». |
| D-UX-10 | Assistant produit : plusieurs fournisseurs (un principal, référence et prix fournisseur HT par variante), prix d'achat du mode coefficient repris du fournisseur principal quand la devise est celle du magasin ; brouillon de session repris **à son étape** avec « Recommencer » ; récapitulatif complet avant création. |

## 3. Écarts et questions au propriétaire

| ID | Écart / question |
| --- | --- |
| Q-UX-01 | La tarification est obligatoire dans **l'assistant**. Trois chemins hors assistant créent encore une variante sans prix (affichée « prix incomplet ») : ajout d'une variante depuis la fiche produit (`POST /api/catalog/models/{id}/variants`), route historique `POST /api/catalog/models`, import CSV catalogue (les tarifs suivent par le second passage « Prix du magasin »). Contrats des lots précédents conservés ; **décision du propriétaire requise** pour les rendre aussi bloquants. |
| Q-UX-02 | Le seuil d'alerte exige `stock.adjust` (même droit que l'écran Stock). Un compte avec `catalog.write` sans `stock.adjust` voit l'explication et pourra fixer le seuil plus tard. |
| Q-UX-03 | Performances mesurées sur le poste de développement (§ 5) ; à confirmer sur le matériel réel des magasins (serveur sans carte graphique : inférence plus lente, l'import reste possible par les règles). |
| O-UX-01 | Capture native 15 antérieure à la correction du libellé de fin d'import (« Contrôle en 19 ms » affichait la durée de l'**import** ; corrigé en « Import en … (serveur) », et « à créer » devient « créée(s) » après import). |
| O-UX-02 | Logo du thème sombre (O-05-17, BL-05-C2) inchangé : décision visuelle du propriétaire toujours attendue. |
| O-UX-03 | Le banc garde ses données fictives (le serveur refuse de supprimer un modèle d'import : historique) ; le scénario natif crée donc un fournisseur fictif par exécution. |

## 4. Contrats et configuration

- **Nouvelles routes** : `POST /api/catalog/lens-catalog` (gamme + offres, `catalog.configure` et `suppliers.read`, coûts `costs.write`) ; `GET /api/imports/assistant` (état de l'assistant : activé, joignable, modèle — `catalog.import`). Toutes deux dans des groupes filtrés par le magasin de l'installation.
- **Contrats étendus (additifs)** : `ProductVariantRequest.lowStockThreshold` ; aperçu d'import `analysis` (statut de l'assistant, modèle, `parseMs`, `assistantMs`, anomalies à codes bornés) et `alreadyApplied` ; `ImportSummary.elapsedMs` ; `MappingSuggestion.source` (`rules` / `assistant`) et confiance `assistant`.
- **Changement de comportement approuvé** : `POST /api/catalog/products` refuse une variante sans tarif (D-UX-01).
- **Permissions** : aucune nouvelle.
- **Schéma** : **aucun changement, aucune migration**. Audit : deux champs ajoutés à la liste blanche des valeurs conservées (`assistantStatus`, `assistantModel`, codes non personnels), sans effet sur le schéma.
- **Configuration** (`appsettings.json`, section `ImportAssistant`) : `Enabled` (vrai sur le serveur ; repli automatique si Ollama est absent), `Endpoint` (`http://127.0.0.1:11434`, boucle locale obligatoire), `Model` (`llama3.2`), `TimeoutSeconds` (60), `KeepAlive` (`15m`). Les bancs de test le désactivent explicitement.

## 5. Banc de l'IA locale et performances

Banc réel (`ImportAssistantTests.Benchmark_LocalOllama_IfRequested`, variable `MMV_OLLAMA_BENCH`), quatre fichiers (monture français, monture anglais, tarif, en-tête piégé), deux passages chacun ; journal : [`evidence/uxai/UXAI_ollama_benchmark_20261103.log`](evidence/uxai/UXAI_ollama_benchmark_20261103.log).

| Modèle | Durée par fichier | Correspondances attendues trouvées | Montant appliqué sans confirmation | Remarque |
| --- | --- | --- | --- | --- |
| `llama3.2` (3 B, 2,0 Go) | 4,5 à 20,1 s (8 mesures, médiane ≈ 10 s) | monture FR 5 / 5, EN 4 / 5, tarif 3 / 3, piège 1 / 1 | 0 | **retenu** |
| `llama3` (8 B, 4,7 Go) | 11,7 à 32,9 s | monture FR 5 / 5, EN 3 / 5, tarif 2 / 3, piège 1 / 1 | 0 | plus lent, confond référence fournisseur et SKU interne |

Premier essai (schéma « tableau ») : `llama3.2` répétait les colonnes jusqu'au délai (100 s) ; corrigé par le schéma à clés fixes et la génération bornée. Mesures natives réelles (API de banc, poste Tauri) : lecture du fichier 0 à 6 ms, analyse par l'assistant 8,3 à 8,7 s (modèle chargé ; 36 s au premier appel après déchargement du modèle), contrôle à blanc 17 ms et import 19 ms pour 3 lignes ; réimport du même fichier : aucune inférence.

**Limites de l'IA locale** : propositions non déterministes d'un modèle à l'autre (d'où la validation serveur et la confirmation des montants) ; un intitulé générique (« Item Description ») peut rester mal classé par les règles et demander une précision ; temps dépendant du matériel ; aucune donnée ne quitte le serveur ; indisponible, trop lente ou hors schéma : analyse par les règles seule, sans blocage.

## 6. Tests

| Suite | Résultat (arbre final) |
| --- | --- |
| `MMV.Api.Tests` | **561 / 561** (dont `ImportAssistantTests` 32 : validation, valeurs, sortie invalide, indisponibilité, délai, injection, boucle locale, banc facultatif ; `SingleStoreRoutesTests` 2) |
| `MMV.Maintenance.Tests` | **50 / 50** |
| `MMV.PostgreSql.Tests` (cluster jetable 55433) | **96 / 97** + 1 `NOT_TESTED` (processus réel non désigné) — dont `Phase05ProductWizardTests` ×3, `Phase05ImportAssistantTests` ×4 (faux service Ollama), `Phase05LensCatalogTests`, `Phase05StoreContextTests` ×3 |
| Vitest ; `npm run check` ; ESLint ; `vite build` | **326 / 326** ; OK ; 0 erreur (1 avertissement préexistant) ; OK |
| Natif deux postes Tauri, API de banc réelle, IA réelle | **33 / 33 PASS** — [`evidence/uxai/UXAI_native_run_20261103.log`](evidence/uxai/UXAI_native_run_20261103.log), 26 captures JPEG réduites |

## Journal

| Date | Entrée |
| --- | --- |
| 2026-11-03 | Contrôle ajouté au moteur déterministe : une même référence fournisseur répétée dans un fichier est refusée par ligne (`supplierReference: duplicate`), comme le SKU et le code-barres (trouvé par le test « données manquantes / doublons »). Exécution native antérieure à ce seul contrôle (fichiers sans doublon) ; suites relancées après. |
| 2026-11-03 | Lot UX-IA livré sans migration : assistant produit (tarif obligatoire, fournisseurs multiples, quantité et seuil), import avec IA locale (Ollama, `llama3.2`, validation serveur, apprentissage), catalogue des verres unifié (création gamme + plusieurs fournisseurs en une transaction), magasin unique revérifié. Défauts trouvés en natif et corrigés : schéma IA « tableau » en boucle, colonne de prix proposée comme politique de stock (contrôle des valeurs), bandeau de reprise du brouillon invisible hors étape 1, récapitulatif d'import hors écran, libellés de cellules trop longs, pictogrammes d'aide décalés. |
