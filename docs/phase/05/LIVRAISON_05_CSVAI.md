# Livraison CSV-IMPORT-AI-01 — implémentation C1 + C2 + C3 et préparation G05-SQL-0009 (06/11/2026)

Mandat : [`prompts/MMV_V2_GO_CONDITIONNEL_GATE_B_CSV_IMPORT_AI_01_2026-11-06.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_GATE_B_CSV_IMPORT_AI_01_2026-11-06.md) (archivé tel que reçu). Conception : [`CSV_IA_05_GATE_B.md`](CSV_IA_05_GATE_B.md), [`CSV_IA_05_CONCEPTION.md`](CSV_IA_05_CONCEPTION.md). Registre : [`CSV_IA_05.md`](CSV_IA_05.md). Dossier SQL : [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md). Preuves : [`evidence/csvai/implementation/`](evidence/csvai/implementation/).

**Statut : `CSVAI_C1_C2_C3_IMPLEMENTED — G05-SQL-0009 PREPARED, NOT APPLIED — OWNER_SQL_GO_AND_REVIEW_REQUIRED`.** Gate D **non déclarée** (preuves obligatoires manquantes : 0009 sur `mmv_v2_test`, parcours Tauri natif deux postes). Branche `spike/csv-import-ollama-benchmark` (HEAD `55f021d`, inchangé) ; **aucun commit, push, PR ni merge** ; `apps/desktop/.env.development` intact (`a215e1f0…`) ; aucune base du propriétaire touchée ; aucun modèle téléchargé ; Phase 06 non commencée.

> Note datée 06/11/2026 (après audit externe) : corrections COR-CSVAI-01 à 05 livrées dans [`LIVRAISON_05_CSVAI_POST_AUDIT.md`](LIVRAISON_05_CSVAI_POST_AUDIT.md) (verdict `READY_FOR_G05_SQL_0009`, pas une autorisation). Le présent rapport n'est pas réécrit ; ses résultats de tests sont ceux du 06/11 avant corrections (`NOT_RERUN` pour cette version), et la procédure de retour arrière à laquelle il renvoie (`SQL_0009_REVUE.md` § 6) est révisée : `pg_restore --clean` sur la base active retiré (§ 6.1), restauration sur base neuve puis bascule (§ 6.2).

## 1. Ce qui a été développé

| Sous-lot | Contenu | Fichiers principaux |
| --- | --- | --- |
| **C1.1** lecteur | `TolerantCsvReader` / `CsvDocument` : décodage identique au poste (BOM UTF-16, BOM UTF-8, UTF-8 strict, sinon Windows-1252), séparateur, RFC 4180, aucune ligne retirée, anomalies par ligne ; refus **techniques** seulement (taille, lignes, vide, binaire, classeur, indécodable) | `src/MMV.Application/Catalog/Assisted/TolerantCsvReader.cs` |
| **C1.2** échantillon | échantillon déterministe ≤ 40 lignes et couverture (début, positions, valeurs de famille, structures, anomalies, mots-clés) ; analyse **incomplète** déclarée si une catégorie n'est pas couverte | `CatalogInterpretationContract.cs` |
| **C1.3** validation | lexique et validateur **portés du prototype B0** (monotonie P1–P4), + couche des **décisions de l'utilisateur** (priorité utilisateur > règle > IA) ; familles par classes stables tirées des seules preuves des règles ; reconstruction serveur des produits et variantes ; continuation ; doublons et **diagnostic** (5 causes testées, retenues ou écartées avec preuve) ; exemples représentatifs des montants et empreinte `examplesDigest` ; projection vers les champs d'écriture (attributs de famille, montants confirmés) | `CatalogLexicon.cs`, `CatalogInterpretationValidator.cs`, `AssistantUserLayer.cs`, `CatalogMaterializer.cs` |
| **C1.4** interpréteur | contrat `mmv.csvai.v2` (consigne **identique** au banc B0, contrôlée par test), schéma à clés fixes, lecture stricte, `think: false`, modèle **confirmé par la réponse d'Ollama** (sinon `invalid_output`), délai 120 s configurable, boucle locale seulement, aucun téléchargement | `src/MMV.Api/Installation/CatalogInterpreterService.cs` |
| **C1.5** asynchrone | file d'analyse (une à la fois, canal borné, position réelle, file pleine → indisponible explicite), événements réels persistés, comparaison-échange (`xmin`), annulation, **résultat tardif conservé sans effet**, interruption au redémarrage, repli « sans IA » sur **choix explicite** tracé | `AssistedAnalysis.cs`, `CatalogInterpreterService.cs` (`AnalysisScheduler`) |
| **C1.6** sécurité | injections jamais interprétées (consigne dans une identité = `INVALIDE`), formules refusées, nombres signés distincts des formules, aucune trace de raisonnement conservée, original immuable | (C1.1–C1.5) + gardes 0009 |
| **C2.1** API | 13 routes **additives** `/api/imports/…assistant…` (état, inspection sans écriture, dépôt, état du lot, suivi, relance, annulation, lignes, anomalie, décisions, contrôle, application, résumé) ; routes actuelles inchangées et `409 pipeline_mismatch` sur un lot du nouveau parcours ; colonne additive `pipeline` / `analysis` dans l'historique | `src/MMV.Api/Catalog/AssistedImportEndpoints.cs`, `AssistedImportService.cs`, `ImportService.Assisted.cs` |
| **C2.2** persistance | EF Core des 5 tables de 0009, magasin d'assistant, cache borné des revalidations | `src/MMV.Infrastructure/Catalog/AssistantModel.cs`, `AssistantStores.cs` |
| **C2.3** poste | assistant en **cinq étapes** (repris du banc Gate B et du kit Lot 6) : barres réelles, indicateur indéterminé pour l'inférence, journal d'activité du serveur, modèle actif confirmé, navigation sans perte, corrections individuelles et groupées, confirmation des montants avec empreinte, modale de confirmation, historique **replié** ; activé seulement si le serveur l'active (`CatalogInterpreter:WizardEnabled`, faux par défaut) — l'ancien écran `ImportView` est conservé | `apps/desktop/src/catalog/importWizard/` |
| **C3** verres | une ligne = une **offre fournisseur** (jamais une variante de monture) : fabricant et gamme (existants ou création sur **décision** explicite), indice et traitements **existants seulement**, géométrie, matériau, plages SPH / CYL / ADD (une borne seule = omise et affichée, jamais complétée), diamètres, restriction combinée \|SPH\| + \|CYL\| → règle structurée `combination_limit`, prix fournisseur (confirmé) et délai, offres de **plusieurs fournisseurs**, création ou mise à jour (révision, instantané, audit) par `LensOfferService` ; fiche **jamais** déclarée complète par un import (compatibilité « à vérifier » hors données fournies) | `AssistedLensPlanner.cs`, `LensOfferService.cs` (validation à blanc exposée) |
| **SQL-0009** | migration EF additive, script, script idempotent, contrôle en lecture seule, inventaire, manifeste SHA-256, dossier | `SQL_0009_REVUE.md`, `sql/0009_*` |

Changements de code existant (limités, rétrocompatibles) : `ImportService` rendu `partial` ; `PlanCatalogAsync` accepte en plus des clés préfixées (`attr:`, `modelattr:`, `price:`) produites seulement par le nouveau parcours (parcours actuel inchangé, 656 tests API verts) ; garde `pipeline_mismatch` ; `AuditFieldPolicy` complétée (champs non confidentiels de l'assistant) ; 4 actions d'audit ajoutées ; `client.ts` accepte `DELETE` ; route « Import » → `ImportEntry` (nouvel écran seulement si activé par le serveur).

## 2. Décisions du propriétaire appliquées (mandat du 06/11/2026, § 7)

| Décision | Mise en œuvre |
| --- | --- |
| Aucune purge automatique des originaux | aucune purge programmée ; seule la purge **tracée** est permise par la garde en base (procédure à écrire après politique validée) |
| Texte décodé : pas de modification des données historiques | `imports.batches` et ses lignes existantes inchangés ; aucun lot antérieur réécrit |
| Délai Ollama 120 s provisoire, configurable, sans SLA | `CatalogInterpreter:TimeoutSeconds = 120` ; dépassement → `timeout` tracé, choix explicite |
| Doublon de SKU sur un autre modèle : pas d'association automatique | action « associer » **non proposée** (raison affichée) ; correction ou exclusion seulement ; refus `sku_belongs_to_other_model` au contrôle |
| IA par défaut, mode règles seules configurable | `Mode = always` par défaut ; `off` → règles seules tracées « désactivée par configuration » ; `unresolved_only` disponible |
| Priorité utilisateur > règle > IA, sans contourner une règle obligatoire | couche utilisateur appliquée en dernier ; une décision contredite par les valeurs reste `AMBIGU` ; montants : empreinte + droit ; aucune route « tout confirmer » |
| Aucun téléchargement de modèle | seules `/api/version`, `/api/tags`, `/api/chat` sont appelées (test : aucun `/api/pull`) |
| Aucun changement de pile ni de SGBD | aucune dépendance NuGet ou npm ajoutée |

## 3. Résultats des tests (06/11/2026)

| Suite | Résultat | Détail |
| --- | --- | --- |
| Build .NET 10 (`MMV.slnx`) | **PASS** (0 avertissement, 0 erreur) | analyseurs en erreur ; synthèse [`TESTS_SYNTHESE.md`](evidence/csvai/implementation/TESTS_SYNTHESE.md) |
| `MMV.Api.Tests` | **656 / 656** | dont **95** tests C1/C2 du lot (`Catalog/Assisted/*`) : lecteur, contrat, échantillon, comparaison B0 sur F01–F20, **mutations de monotonie** (20 fichiers, 0 violation), interpréteur (requête, modèle confirmé, délai, boucle locale, annulation), matérialisation |
| `MMV.Maintenance.Tests` | **50 / 50** | — |
| `MMV.PostgreSql.Tests` | **101 PASS · 2 BLOCKED · 1 NOT_TESTED** (104) | BLOCKED = `*_AllMigrationsApplied` sur les bases du propriétaire (0009 non appliquée, attendu) ; NOT_TESTED = processus HTTPS réel ; dont **6** parcours bout en bout de l'assistant (cluster jetable avec 0009) et **1** parcours avec **Ollama réel** |
| Vitest | **331 / 331** | dont 5 tests de l'assistant (historique replié sans requête, étapes du serveur, montant sans présélection + empreinte, refus affiché, modale) |
| TypeScript (3 configurations) | **PASS** | `npm run check` |
| ESLint | **0 erreur** | 1 avertissement préexistant (`src/clients/ClientsTable.tsx`, hors lot) |
| SQL 0009 sur cluster jetable | **PASS** | idempotent ×2, verify 14/14, inventaire, sauvegarde → restauration → verify 14/14 ([essais](evidence/csvai/implementation/SQL_0009_ESSAIS.md)) |
| Ollama réel (`qwen3.5:4b`, 0.40.14) | **PASS** | F01 30,0 s · F07 28,3 s · F17 52,8 s (40 lignes vues sur 306, couverture complète) ; modèle confirmé ; aucune écriture avant confirmation ([résultats](evidence/csvai/implementation/OLLAMA_REEL_qwen3.5-4b_20261106.txt)) — mesures du poste de développement, sans valeur de SLA |
| Captures (composant réel, réponses scriptées, Edge sans interface) | **12 / 12**, 0 erreur console, 0 débordement | [`captures/`](evidence/csvai/implementation/captures/) — simulation navigateur, **pas** une preuve Tauri |

Critères prioritaires du dossier Gate B (§ 14) : 1 SKU en double accepté **PASS** · 2 doublon expliqué **PASS** · 3 retour arrière sans perte **PASS (API ; natif NOT_TESTED)** · 4 correction groupée revalidée **PASS** · 5 Qwen analyse automatiquement **PASS (Ollama réel)** · 6 événements réels **PASS** · 7 interface non figée **PASS (API asynchrone ; natif NOT_TESTED)** · 8 original traçable **PASS** · 9 anomalie ≠ refus **PASS** · 10 historique replié **PASS (Vitest)** · 11 modèle affiché = modèle utilisé **PASS** · 12 aucun prix inventé **PASS** · 13 aucun article perdu **PASS** · 14 aucune fusion sans justification **PASS** · 15 aucun import sans confirmation **PASS**.

## 4. Ce qui reste bloqué ou non testé

| Élément | Statut | Raison |
| --- | --- | --- |
| Application de 0009 à `mmv_v2_dev`, `mmv_v2_test` | **BLOCKED** | GO SQL distinct requis (interdiction absolue du mandat) |
| `SchemaStateTests.*_AllMigrationsApplied` (bases du propriétaire) | **BLOCKED** (échec attendu) | la base n'a pas 0009 ; se règle par l'application après GO |
| Parcours Tauri natif deux postes, Ollama réel | **NOT_TESTED** | exige 0009 sur une base servie par l'API des postes, et l'écran activé |
| Tests Phase 05 de l'assistant sur `mmv_v2_test` | **NOT_TESTED** | `Phase05AssistantFact` : 0009 absente de la cible officielle |
| Aller-retour des **données** de l'assistant par sauvegarde / restauration | **NOT_TESTED** | tables vides lors de la sauvegarde du cluster jetable |
| Navigation clavier complète et contrastes mesurés du nouvel écran | **NOT_TESTED** | boutons et radios natifs (`aria-*`), aucune mesure outillée |
| Catalogues réels anonymisés, matériel des magasins (BL-05-U2) | **NOT_TESTED** | données et matériel non fournis |
| Processus HTTPS réel (`Phase05RealHttpsTests`) | **NOT_TESTED** | processus non désigné (inchangé depuis UX-IA) |

## 5. Migrations proposées

Une seule : **`20261106154105_CsvImportAssistant` (0009)**, additive (5 tables, 13 index, 4 fonctions et 11 déclencheurs de garde), dossier [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md), manifeste [`sql/0009_MANIFEST.sha256`](sql/0009_MANIFEST.sha256). **STOP** : aucune application sans GO SQL.

## 6. Risques résiduels

| Risque | Mesure / état |
| --- | --- |
| Règles écrites et éprouvées sur un corpus synthétique | essai sur catalogues réels anonymisés avant Gate D (Q-CSVAI-05) |
| Durée d'analyse (28–53 s sur le poste de développement) | asynchrone, annulable, délai configurable ; mesure sur le matériel des magasins (BL-05-U2) |
| Surface d'écriture nouvelle (corrections, montants) | révisions append-only, revalidation complète, droits par champ, empreinte des exemples, gardes en base |
| Double maintenance ancien / nouveau parcours | drapeau serveur ; retrait de l'ancien parcours sur décision seulement |
| Volume des originaux et des événements | bornes en base ; aucune purge (politique à décider, Q-CSVAI-07) |
| Revalidation complète à chaque lecture (5 000 lignes au plus) | cache borné par révision ; à mesurer sur fichiers volumineux réels |
| Prix des offres de verres sans colonne devise | devise du magasin (aucune conversion), affichée au contrôle ; à confirmer |

## 7. Décisions qui demandent encore l'intervention du propriétaire

1. **GO SQL G05-SQL-0009** (dossier [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md)) : sauvegardes, application dev puis test, contrôle 14/14.
2. **Activation de l'écran** (`CatalogInterpreter:WizardEnabled=true`) après 0009, puis Gate D (natif deux postes, Ollama réel, catalogues réels anonymisés).
3. **Revue visuelle** du nouvel écran ([captures](evidence/csvai/implementation/captures/)) et de l'écart de libellé « lignes lisibles » des montants.
4. **Q-CSVAI-07** (durée de rétention des originaux) et procédure de purge tracée.
5. **Devise des prix des offres de verres** en l'absence de colonne devise (actuellement : devise du magasin, sans conversion).
6. **Retrait de l'ancien parcours** (`ImportView`) après validation du nouveau.
7. Commit de ce lot (non autorisé par le mandat ; arbre exporté par `git archive` pour l'audit).

## 8. Livraison

Archive `MMV-V2-CSVAI-IMPLEMENTATION.zip` produite **exclusivement par `git archive`** d'un arbre Git temporaire (index isolé `GIT_INDEX_FILE`, liste explicite) ; index normal et worktree du propriétaire inchangés (contrôlés avant et après). Méthode : [`evidence/csvai/implementation/LIVRAISON_ZIP.md`](evidence/csvai/implementation/LIVRAISON_ZIP.md) ; les empreintes de l'archive et de l'arbre exporté sont fournies séparément (fichier annexe `.sha256`, inventaire, message de livraison), une archive ne pouvant contenir sa propre empreinte. Preuves antérieures (Lot A, B0, Gate B) **référencées** par leurs manifestes, non réincluses : `evidence/csvai/corpus/MANIFEST.json`, `evidence/csvai/b0/corpus/MANIFEST_B0.json`, `evidence/csvai/gateb/B0_FREEZE_SHA256_20261104.txt`, archive Gate B `MMV-V2-CSVAI-GATE-B-20261106.zip` (SHA-256 `cf4de23452dc8df48c96a2cb9e055683c4cc3fd3f743f63efc6bb8588043c074`).
