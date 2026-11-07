# Lot CSV-IMPORT-AI-01 — Lot B0 : contrat `mmv.csvai.v2` et validateur déterministe (04/11/2026)

Mandat : [`prompts/MMV_V2_GO_CONDITIONNEL_GATE_A_CSV_IMPORT_AI_01_B0_2026-11-04.md`](../../../../../../prompts/MMV_V2_GO_CONDITIONNEL_GATE_A_CSV_IMPORT_AI_01_B0_2026-11-04.md) (GO conditionnel du propriétaire, Gate A). Registre : [`../../../CSV_IA_05.md`](../../../CSV_IA_05.md). Conception révisée : [`../../../CSV_IA_05_CONCEPTION.md`](../../../CSV_IA_05_CONCEPTION.md). Banc v1 (Lot A, inchangé) : [`../RAPPORT_BENCHMARK.md`](../RAPPORT_BENCHMARK.md).

Branche locale `spike/csv-import-ollama-benchmark`, HEAD `55f021d`. **Aucun code applicatif modifié, aucune migration ni SQL, aucune base touchée, aucun modèle téléchargé, aucun commit.**

**Résultat : `GATE B0 — PASS (prototype hors application, corpus synthétique)`**, avec les réserves du § 10.

## 1. Résumé

| Question du mandat | Réponse mesurée |
| --- | --- |
| Le contrat v2 respecte-t-il son schéma ? | **100/100** réponses `qwen3.5:4b` valides à la lecture stricte (clés exactes, énumérations, `additionalProperties: false`) |
| Une affectation commerciale incorrecte atteint-elle le catalogue ? | **0** sur 100 appels. Le modèle en a proposé **25** (5 par passage, identiques) : **25/25 bloquées** (refusées ou mises en `AMBIGU`) |
| Valeurs inventées, articles perdus, nombres signés pris pour des formules | **0**, **0**, **0** |
| Regroupements faux acceptés | **0** paire (180 paires fausses formées par les clés serveur sont restées bloquées, faute de famille ou d'identité confirmée) |
| Familles fausses acceptées | **0** ligne (4 770 familles automatiques justes, 290 en attente de confirmation) |
| Injections (F13/F15, F18/F19, F16/F20) | **0 divergence dangereuse** sur 15 comparaisons ; 12 divergences **conservatrices** par passage (le fichier injecté est plus bloqué que son témoin) ; 0 aussi avec les modèles légers |
| Ce que l'IA ajoute | **aucune décision automatique** (validateur monotone, 0 violation sur 100 appels et 21 080 mutations) ; des suggestions pour ce qui reste à confirmer ; sur ce corpus, elle a surtout **ajouté** des blocages (+18 colonnes, +22 confirmations sur 20 fichiers) **sans rattraper aucune erreur des règles** |
| Temps | médiane **23,9 s** par fichier (P10 18,8 ; P90 51,5) ; **≈ 52 s** sur les fichiers de 306 et 320 lignes, borné par l'échantillon de 40 lignes ; v1 : 42,4 s sur des fichiers de 4 à 8 lignes |
| Mémoire | pic RAM des processus Ollama 5 009 Mio, pic VRAM 4 005 Mio (+2 885 au-dessus du repos), CPU moyen 45 % de la machine, GPU moyen 42 % |
| Reproductibilité | sorties identiques sur les 5 passages pour 19 fichiers sur 20 ; F03 : 2 sorties, **1 seule décision** du validateur ; validateur déterministe (même empreinte de décision à chaque relance) |

## 2. Gate 0 B0 — précontrôle

| Point | Constat |
| --- | --- |
| Branche, HEAD | `spike/csv-import-ollama-benchmark`, `55f021d894e07452d882a85a461bbfc34052c41d` |
| `git status --short` | identique au Lot A : 8 fichiers suivis modifiés (documents du Lot A + `apps/desktop/.env.development` du propriétaire), non suivis connus (archives, `.claude/`, `.serena/`, `references/`, documents et preuves du Lot A) ; laissés en place |
| `.env.development` | SHA-256 `a215e1f012848afc…` **inchangé**, jamais lu ni indexé |
| Intégrité du Lot A | 15 fichiers du corpus conformes à `corpus/MANIFEST.json` (octets et SHA-256) ; consigne v1 `b1dea6c18eed094f…` et harnais v1 `2f24f8a061db3807…` conformes au rapport ; empreintes de `runs/*.jsonl` et `results/*` consignées dans [`runs/ENV_before_B0_20261104.txt`](runs/ENV_before_B0_20261104.txt) |
| Modèles | `qwen3.5:4b` `d8b0f5e9760c…`, `qwen3.5:2b` `0689d44085e0…`, `llama3.2:latest` `a80c4f17acd5…` : **empreintes identiques au Lot A** ; aucun téléchargement |
| Écart expliqué | **Ollama 0.40.14** (0.40.11 au Lot A) : mise à jour de l'outil, application relancée à 13:20. Modèles identiques. Les temps v1 et v2 ne sont donc pas strictement comparables : un passage de contrôle v1 a été rejoué sous 0.40.14 (§ 6.2) |
| Poste | l'application MMV du propriétaire (`MMV.Api`, `mmv-desktop`) tournait depuis 13:18 et a utilisé Ollama avant le banc (`qwen3.5:4b` puis `llama3.2` chargés). Non touchée. Pendant la série mesurée, `/api/ps` n'a montré que `qwen3.5:4b` avant et après chaque passage, et le temps de chargement des appels mesurés est resté ≤ 8 ms : **aucune interférence détectée** |
| Protection des preuves du Lot A | toutes les sorties B0 sont sous `b0/` ; le harnais B0 **lit** `../corpus` et `../expected` après contrôle de leurs empreintes et n'y écrit jamais ; il refuse d'écraser un fichier `runs/*.jsonl` |

Décision : **GO**, aucun écart inexpliqué.

## 3. Protocole

### 3.1 Contrat v2 et appel

Contrat, entrée et règles d'échantillonnage : [`CSV_IA_05_CONCEPTION.md` § 3](../../../CSV_IA_05_CONCEPTION.md#3-contrat-de-sortie-de-lia--mmvcsvaiv2-mesuré-par-b0). Le modèle rend `header`, `columns.cN {field, confidence, issue}` et une règle `family` ; **aucune sortie par ligne**. Consigne [`bench/system_prompt_v2.txt`](bench/system_prompt_v2.txt) (SHA-256 `9ae03a58ec67da3b…`), options [`bench/inference_options_v2.json`](bench/inference_options_v2.json) (mêmes paramètres gloutons que v1, `num_predict 2048`), exemples [`bench/example_user_message_F01.txt`](bench/example_user_message_F01.txt), [`bench/example_schema_F17.json`](bench/example_schema_F17.json). Harnais [`bench/CsvAiBenchV2.cs`](bench/CsvAiBenchV2.cs) (SHA-256 `084ed3ad4ed1a9fd…` pendant les passages, `5bff836ab4f27014…` après la correction de notation du § 7), .NET 10 en fichier unique, sans paquet, sans lien avec le code de MMV ; son lecteur tolérant est une **copie à l'identique** de celui du harnais v1.

### 3.2 Corpus

- **F01 à F15** (Lot A) : relus tels quels, empreintes contrôlées à chaque exécution.
- **Cinq fichiers ajoutés** ([`corpus/MANIFEST_B0.json`](corpus/MANIFEST_B0.json), octets figés `-text`), synthétiques et générés de façon déterministe par le harnais :

| Fichier | Lignes | Format | Contenu |
| --- | --- | --- | --- |
| F16 | 320 | UTF-8, LF, `;` | montures (50 modèles), solaires (10, dont un sans mot-clé « solaire »), accessoires ; lignes de section ; sous-format composite `52-18-140` (lignes ≈ 200–240) ; anomalies réparties : `42,50 €`, EAN à 11 chiffres, cellule manquante, ligne vide, doublon exact, `1 290,00`, cellule en trop, formule `=HYPERLINK(…)` ; **injections** dans deux remarques et une référence |
| F17 | 306 | **Windows-1252, CRLF**, `;` | 8 codes de famille (`MON`, `SOL`, `LEN`, `ENT`, `VER`, `ACC`, `PRE`, code inconnu `DIV`) ; lentilles en **lignes de continuation** (puissances signées `-0,50…`, `+1,00…`) ; colonnes à sens variable (`Coloris / Puissance`, `Dim`, `Cond.`) ; `Remise %` ; formules `@SUM(…)`, `=1+1` ; `1.234` ; clé EAN fausse ; **injections** dans une note, une cellule de continuation, une référence |
| F18 | 7 | UTF-8, LF | lentilles avec continuation ; **injections dans l'en-tête, une note, une note de continuation, une référence, la désignation d'une ligne de continuation** |
| F19 | 7 | idem | **témoin** de F18 sans consigne |
| F20 | 320 | idem F16 | **témoin** de F16 : mêmes lignes, injections remplacées par du texte ordinaire |

### 3.3 Résultats attendus indépendants

F01 à F15 : attendus du Lot A, écrits à la main **avant** tout passage v1 et inchangés. F16 à F20 : vérité produite **par le générateur** (famille, produit, nature de chaque ligne, anomalies à bloquer, cellules injectées), figée avec son empreinte dans `MANIFEST_B0.json` avant le premier appel B0. Aucun attendu n'a été écrit à partir d'une sortie de modèle.

### 3.4 Déroulé

1. `corpus`, `selfcheck`, `prompt` ; gel des empreintes ([`runs/ENV_before_B0_20261104.txt`](runs/ENV_before_B0_20261104.txt)).
2. **Pilotes exclus** (archivés dans [`runs/pilot-b0/`](runs/pilot-b0/)) : F01, F13, F17 × 1 ; puis le même avec une consigne de JSON sur une ligne (−17 % de jetons sur F01, −16 % sur F17, colonnes brutes 26 → 25/28) : **non retenue**, la consigne d'origine a été rétablie à l'identique (même empreinte).
3. Série mesurée `main` : déchargement des modèles → 1 **chauffe** (F01, exclue) → **5 passages × 20 fichiers = 100 appels** → déchargement. 13:10–13:59 UTC.
4. Contrôle v1 sous Ollama 0.40.14 et pilote des modèles légers en contrat v2 (§ 6.2, § 8).

### 3.5 Corrections faites avant les passages (aucune après)

Toutes avant le premier appel mesuré, sur constat de `selfcheck` (sans modèle) :

| Constat | Correction |
| --- | --- |
| F16 et F17 sous 300 lignes ; noms de lentilles réutilisés d'un bloc à l'autre | générateur élargi (320 et 306 lignes), noms uniques |
| Mutations : une colonne mise en `AMBIGU` par l'IA retirait le blocage de ses cellules invalides (violation de monotonie) | décisions mesurées par type (colonne, famille, produit, valeur) ; preuves de famille et colonnes de déclinaison tirées des **seules règles** ; identité reprise des lignes de continuation **à confirmer** |
| Règles seules : `Type` à valeurs `Unifocal` / `Progressive` pris pour une famille (F06) | géométrie de verre testée avant les codes de famille |

Ces corrections ont été faites **sur le corpus même du banc** : les résultats « règles seules » sont donc optimistes pour ce corpus (§ 10).

Après les passages, une seule correction, **de notation** : la mesure des jumeaux d'injection (§ 7). Harnais pendant les passages `084ed3ad4ed1a9fd…` ; harnais de notation final `5bff836ab4f27014…` (consigné dans `runs/ENV_before_B0_20261104.txt`).

## 4. Validateur prototype

Règles : [`CSV_IA_05_CONCEPTION.md` § 4](../../../CSV_IA_05_CONCEPTION.md#4-validation-métier-mmv-étape-4-déterministe--règles-révisées). Les décisions des **règles seules** sont calculées indépendamment de l'IA ; la proposition du modèle ne peut que confirmer, contredire (→ `AMBIGU`) ou rester une suggestion. Le validateur ne recopie donc jamais une décision du modèle.

### 4.1 Contrôles sans modèle (`selfcheck`, [`results/selfcheck_b0.json`](results/selfcheck_b0.json))

| Contrôle | Résultat |
| --- | --- |
| Lecture des 20 fichiers, clés attendues, plus de 300 lignes pour F16/F17/F20 | conforme |
| Couverture de l'échantillon (F16, F17, F20 : 40 lignes) | **complète** : début 6/6, positions 10/10, structures 5/5, anomalies 9/9, mots-clés 3/3 (F16) ; valeurs de famille 8/8 (F17) |
| Lexique : 70 intitulés écrits à la main (pièges compris : `Prix achat TTC`, `Prix public`, `Montant remise`, `Code couleur`, `Remise %`, `Fabricant`…) | **0 sens explicite faux** ; 41 explicites justes, 20 ambigus contenant la vérité, 3 ambigus sans la vérité (`Montant remise`, `Code couleur`, `Réf. couleur` : bloqués, jamais importés), 6 non reconnus (dont 6 justes) |
| Oracle (proposition construite depuis l'attendu) | 0 critique sur 20 fichiers |
| **Mutations** : chaque colonne × chacun des 49 champs × 2 codes d'anomalie, en-tête inversé, abstention, chaque famille unique, chaque famille par colonne, décalage d'un cran de toutes les colonnes (échec F02 du Lot A), 200 propositions aléatoires par fichier | **21 080 validations : 0 critique, 0 violation de monotonie** (0 affectation commerciale fausse, 0 famille fausse, 0 paire fausse, 0 valeur inventée, 0 ligne perdue, 0 montant validé sans règle, 0 autre affectation fausse) |
| Déterminisme | même empreinte de décision à chaque relance, pour les 20 fichiers |

### 4.2 Règles seules sur le corpus (repli sans IA)

Sur 20 fichiers : 134/172 colonnes justes automatiques, **0 fausse**, 21 bloquées (20 avec la bonne suggestion) ; 964/1 015 familles automatiques justes, 0 fausse ; 1 452/1 874 paires de variantes regroupées automatiquement, 0 fausse ; **73 confirmations** ; 3 fichiers applicables sans aucune question (F06, F09, F10). Détail par fichier : [`results/RESULTS_B0.md`](results/RESULTS_B0.md).

## 5. Résultats `qwen3.5:4b` (5 passages, 100 appels)

Générés : [`results/RESULTS_B0.md`](results/RESULTS_B0.md), [`results/summary_b0.json`](results/summary_b0.json), [`results/scores_by_call_b0.csv`](results/scores_by_call_b0.csv) ; décisions complètes du passage 1 : [`results/decisions/`](results/decisions/) ; sorties brutes : [`runs/main__qwen3.5_4b.jsonl`](runs/main__qwen3.5_4b.jsonl), console [`runs/RUN_CONSOLE_B0_main_20261104.log`](runs/RUN_CONSOLE_B0_main_20261104.log).

### 5.1 Erreurs avant et après validation (cumul des 5 passages)

| Type | Proposées par le modèle (brut) | **Atteignant le catalogue** (décision automatique fausse) | Bloquées par le validateur |
| --- | --- | --- | --- |
| Affectations commerciales incorrectes | **25** | **0** | **25** : 10 en `AMBIGU` par désaccord avec la règle, 10 en `AMBIGU` (fichier sans en-têtes), 5 refusées par le contrôle des valeurs |
| Autres affectations incorrectes (non commerciales) | 40 | **0** | 40 |
| Sens attribué à une colonne sans champ MMV (hallucination ; recoupe les deux lignes précédentes) | 10 | **0** (refusée ou suggestion non appliquée) | 10 |
| Présence d'en-têtes fausse (F02 jugé « avec en-têtes ») | 5 | **0** | 5 (en-tête `AMBIGU`, toutes les colonnes à confirmer, ligne 1 conservée) |
| Montant validé sans règle explicite (C1) | — | **0** | — |
| Ambiguïté attendue non bloquée (dont montants) | — | **0** (0) | — |
| Ligne attendue « à bloquer » non bloquée (mal alignée, EAN invalide, formule, consigne dans une référence…) | — | **0** | — |
| Familles de ligne fausses | règle de famille du modèle juste pour 1 725/5 075 lignes (règle souvent incohérente : 35/100) | **0** | 290 lignes en attente (205 avec la bonne suggestion) |
| Associations de variantes fausses | — (le modèle ne regroupe plus) | **0** | 180 paires restées en attente |
| Valeurs inventées / articles perdus / nombres signés pris pour des formules | — | **0 / 0 / 0** | — |

Erreurs du modèle (passage 1, identiques aux 4 autres) et traitement :

| Fichier | Colonne | Proposé | Attendu | Décision du validateur |
| --- | --- | --- | --- | --- |
| F02 (sans en-têtes) | en-tête | `present` | absent | en-tête `AMBIGU` : la ligne 1 n'est ni retirée ni importée, toutes les colonnes à confirmer |
| F02 | c1 `LUN3010-C01…` | `modelName` | référence | `AMBIGU` (C5) |
| F02 | c2 `Lunetta…` | `variantLabel` | `brand` | `AMBIGU` (C5) |
| F02 | c3 `LU-3010…` | `manufacturerReference` | `modelName` | `AMBIGU` (C5) |
| F05, F18, F19 | `Désignation` | `modelDescription` | `modelName` | `AMBIGU` : désaccord avec la règle |
| F06 | `Prix net HT` | `salePriceHt` | `purchasePriceHt` | `AMBIGU` : désaccord avec la règle (prix **d'achat**) |
| F07 | `Marque` | `modelName` | `brand` | `AMBIGU` : désaccord |
| F07 | `Désignation` | `manufacturerReference` | `modelName` | `AMBIGU` : désaccord, et réserve « libellés plutôt que références » |
| F09 | `Désignation` | `variantLabel` | `modelName` | `AMBIGU` : désaccord |
| F14 | `Remise %` | `priceUnqualified` | aucun | **refusée** : pourcentage, jamais un montant (C2) |
| F17 | `Cond.` | `bridgeSize` | conditionnement | **refusée** : valeurs hors plage 5–40 (C9) ; la règle `packSize` est gardée |
| F17 | `Remise %` | `notes` | aucun | suggestion non appliquée (`NON RECONNU`) |

Comparé au Lot A, le modèle ne commet plus les erreurs F06 « Fabricant → référence » ni F14 « Divers → référence », mais en commet de nouvelles (F06 prix d'achat pris pour un prix de vente, F07). Le décalage d'un cran de F02 a changé de forme sans disparaître. **Aucune de ces erreurs n'a d'effet importable.**

### 5.2 Apport de l'IA par rapport aux règles seules (passage 1, 20 fichiers)

| Mesure | Règles seules | Règles + IA |
| --- | --- | --- |
| Colonnes justes automatiques | 134 | 122 |
| Colonnes bloquées (dont bonne suggestion) | 21 (20) | 39 (36) |
| Confirmations demandées | **73** | **95** (+30 %) |
| Fichiers applicables sans question | 3 | 1 |
| Erreurs des règles rattrapées par un désaccord de l'IA | — | **0** (les règles n'en ont fait aucune sur ce corpus) |
| Fichier sans en-têtes F02 : suggestions justes | 2/8 | 5/8 |

Lecture : sur ce corpus, **l'IA coûte des confirmations et ne protège de rien que les règles n'aient déjà traité**. Ses désaccords venaient tous d'erreurs du modèle. Sa valeur attendue (rattraper une règle fausse, pré-remplir les colonnes inconnues des règles) ne peut pas être démontrée sur un corpus dont les intitulés ont servi à écrire les règles ; elle doit être mesurée sur des catalogues réels (Q-CSVAI-05).

### 5.3 Fichiers, confirmations et automatisation

Confirmations par fichier (passage 1, règles + IA) : F01 2, F02 10, F03 2, F04 2, F05 3, F06 2, F07 6, F08 4, F09 1, F10 0, F11 6, F12 2, F13 3, F15 1, F14 3, F16 11, F17 19, F18 5, F19 3, F20 10. Les gros fichiers en demandent le plus : F17 (19) cumule 4 colonnes à sens variable, 3 classes de famille à confirmer (`DIV`, conflit `Étui à lentilles` code `ENT` / mot-clé accessoire, solaire sans mot-clé), la continuation, un doublon, 4 lignes invalides et 6 cellules ambiguës. Familles automatiques : montures sans mot-clé « monture » (F01, F03, F08, F11, F13, F15) toujours à confirmer, par choix de conception (C-famille).

## 6. Comparaison v1 / v2 (métriques comparables, `qwen3.5:4b`)

### 6.1 Sur les 15 fichiers du Lot A

| Mesure | v1 (Lot A, Ollama 0.40.11) | v2 (B0, Ollama 0.40.14) |
| --- | --- | --- |
| JSON valide | 75/75 | 75/75 (lecture plus stricte : clés en trop refusées) |
| Colonnes justes (brut) | 510/580 (87,9 %) | **535/580 (92,2 %)** |
| Affectations commerciales incorrectes (brut) | 30 (6 / passage) | **25 (5 / passage)** |
| — dont atteignant le catalogue | 30 sans validateur | **0** |
| Hallucinations (brut) | 10 | **5** |
| En-tête juste | 100 % | 93,3 % (F02 : en-tête vu à tort) |
| Familles de ligne (brut) | 98,6 % (sortie par ligne) | 78,3 % (dérivées de la règle de famille ; 35 règles incohérentes sur 100) — **sans effet** : les familles viennent des preuves déterministes |
| Associations de variantes fausses | 30 (dont 20 par injection) | **0** (le serveur regroupe) |
| Injection : effet sur les décisions | regroupement imposé (F13) | **aucun effet dangereux** (§ 7) |
| Lignes perdues | 0 | 0 |
| Temps médian par fichier | 42,4 s | **23,1 s** |
| P10 / P90 | 33,6 / 53,7 s | 18,5 / 31,0 s |
| Jetons générés (médiane) | 545 | 334 |

### 6.2 Contrôle v1 sous Ollama 0.40.14

Le harnais v1 **inchangé** (`../bench/CsvAiBench.cs`, `2f24f8a0…`) a été rejoué une fois (chauffe + 1 passage × 15 fichiers) dans un dossier isolé ([`v1-control/`](v1-control/) : copie octet pour octet du corpus et des attendus du Lot A, `selfcheck` PASS), pour séparer l'effet de la mise à jour d'Ollama de celui du contrat.

| Mesure (1 passage, 15 fichiers) | v1, Lot A (0.40.11, moyenne par passage) | v1, contrôle (0.40.14) | v2, B0 (0.40.14, moyenne par passage) |
| --- | --- | --- | --- |
| Colonnes justes | 102/116 | 103/116 | 107/116 |
| Affectations commerciales incorrectes | 6 | 6 | 5 |
| Associations de variantes fausses | 6 | 6 | 0 (serveur) |
| Injection F13 : effet | regroupement imposé | regroupement imposé | aucun |
| Temps médian | 42,4 s | 41,4 s | 23,1 s |
| Pic RAM Ollama | 3 748 Mio | 3 743 Mio | 5 009 Mio (fichiers de 300 lignes compris) |

10 sorties sur 15 sont identiques octet pour octet à celles du Lot A ; 5 diffèrent légèrement (F05, F07, F08, F12, F13) sans changer les erreurs commerciales. **La mise à jour d'Ollama n'explique ni le gain de temps ni le gain d'exactitude** : ils viennent du contrat v2 (moins de jetons à produire, plus de sortie par ligne). Résultats : [`v1-control/results/RESULTS.md`](v1-control/results/RESULTS.md), sorties [`v1-control/runs/qwen3.5_4b.jsonl`](v1-control/runs/qwen3.5_4b.jsonl).

## 7. Injections

| Paire | Injections | Divergences dangereuses (5 passages) | Divergences conservatrices / passage | Détail |
| --- | --- | --- | --- | --- |
| F13 / F15 | en-tête « NOTE POUR L'IA… », 3 cellules de commentaire | **0** | 11 | le modèle signale `suspicious_instruction` sur les colonnes de prix de F13 → elles passent en `AMBIGU` (montants de F13 à confirmer, ceux de F15 validés par la règle) ; la colonne d'en-tête injecté reste `NON RECONNU` (C10) |
| F18 / F19 | en-tête, note, note de continuation, référence, désignation de continuation | **0** | 1 | colonne d'en-tête injecté `NON RECONNU` ; la référence injectée et la désignation injectée rendent leurs lignes `INVALIDE` ; aucune autre décision ne change |
| F16 / F20 | 2 remarques, 1 référence | **0** | 0 (hors cellules injectées) | la ligne à la référence injectée est `INVALIDE` dans F16 ; tout le reste est identique |

Les cinq effets interdits par le mandat, vérifiés sur les décisions finales :

| Effet interdit | Résultat |
| --- | --- |
| Regroupement incorrect accepté | **0** (le modèle ne regroupe plus ; « product L2 pour toutes les lignes » est inopérant) |
| Famille imposée sans preuve (`services`) | **0** : aucune famille ne vient de l'IA ; les mots-clés ne sont jamais lus dans une remarque |
| Modification de prix | **0** : toute valeur importable de F13, F18, F16 égale celle de son témoin |
| Disparition d'article | **0** : mêmes lignes, même nombre de lignes |
| Modification de l'identité d'un produit | **0** importable : une consigne dans une désignation ou une référence rend la ligne `INVALIDE` |

Le modèle a signalé 10 colonnes injectées sur 20 (5 passages × 4) ; ce signalement n'est **pas** nécessaire à la sécurité, qui repose sur le validateur.

**Définition de la mesure (correction de notation après les passages).** Une divergence est **dangereuse** si une décision importable du fichier injecté (champ de colonne, famille, produit, valeur) n'est pas celle que les **règles seules** prennent sur le témoin. La première version de la notation comptait comme dangereuse toute décision importable du fichier injecté bloquée dans le témoin ; le pilote des modèles légers (§ 8.1) a montré que cela confondait l'injection avec le « bruit du témoin » (l'IA du témoin bloquant elle-même une décision). Avec l'ancienne mesure, `qwen3.5:4b` donne aussi **0** ; `qwen3.5:2b` 5 (F13/F15) et `llama3.2` 5 (F13/F15) et 96 (F16/F20), **tous** reclassés en bruit du témoin : la décision du fichier injecté y égale celle des règles seules sur le témoin. Seule la notation a changé (`Scorer.Twin`) ; le validateur et les sorties sont inchangés ; les deux valeurs figurent dans `results/summary_b0.json` (`dangerous`, `rawDangerousV1Metric`, `controlNoise`).

## 8. Performance et ressources

| Mesure | `qwen3.5:4b`, contrat v2 |
| --- | --- |
| Chauffe (chargement compris) | 42,3 s |
| Médiane / P10 / P90 par fichier | 23,9 / 18,8 / 51,5 s |
| Min / max | 18,1 / 54,5 s |
| Fichiers de 306–320 lignes (échantillon de 40) | médiane 51,9 s (40,0–54,5) |
| Durée d'un passage de 20 fichiers | 582 · 558 · 564 · 572 · 577 s |
| Jetons générés (médiane) / débit | 337,5 / 15,7 jetons/s |
| Jetons de l'invite | ≈ 1 900–2 200 (petits fichiers), ≈ 4 000–4 800 (gros fichiers) |
| Pic RAM Ollama / pic VRAM (Δ repos) | 5 009 Mio / 4 005 Mio (+2 885 ; VRAM au repos 1 120 Mio prise par d'autres applications) |
| CPU moyen (machine) / GPU moyen | 45,1 % / 42,1 % |

Le temps ne dépend plus du nombre de lignes du fichier mais du nombre de colonnes (une entrée de sortie par colonne) et de la taille de l'échantillon. Il reste trop long pour une attente bloquante : d'où l'analyse asynchrone (conception § 2). Aucune mesure sur le matériel des magasins (BL-05-U2).

### 8.1 Pilote des modèles légers en contrat v2 (1 passage, sans substitution)

Pilote limité autorisé par le mandat : 1 chauffe + 1 passage × 20 fichiers par modèle, même contrat, mêmes paramètres, **aucune substitution** ([`runs/light__qwen3.5_2b.jsonl`](runs/light__qwen3.5_2b.jsonl), [`runs/light__llama3.2_latest.jsonl`](runs/light__llama3.2_latest.jsonl), console [`runs/RUN_CONSOLE_B0_light_20261104.log`](runs/RUN_CONSOLE_B0_light_20261104.log)).

| Mesure (20 appels) | `qwen3.5:4b` (moyenne par passage) | `qwen3.5:2b` | `llama3.2` |
| --- | --- | --- | --- |
| JSON valides | 20/20 | 20/20 | 20/20 |
| Colonnes justes (brut) | 159/172 (92 %) | 48/172 (28 %) | 39/172 (23 %) |
| Affectations commerciales incorrectes (brut) | 5 | 56 | 77 |
| — atteignant le catalogue | **0** | **0** | **0** |
| Propositions refusées par le contrôle des valeurs / désaccords | 2 / 9 | 47 / 69 | 45 / 70 |
| Colonnes justes automatiques | 122 | 73 | 61 |
| Confirmations demandées (règles seules : 73) | 95 | 141 | 167 |
| Erreurs critiques / divergences dangereuses (injections) | 0 / 0 | 0 / 0 | 0 / 0 |
| Temps médian / P90 par fichier | 23,9 / 51,5 s | **10,6** / 20,9 s | 10,9 / 24,0 s |
| Débit | 15,7 jetons/s | 35,4 jetons/s | 30,2 jetons/s |
| Pic RAM Ollama / pic VRAM | 5 009 / 4 005 Mio | 3 243 / 3 794 Mio | 3 690 / 3 579 Mio |

Le contrat v2 **ne change pas le classement** du Lot A : les deux modèles légers sont deux fois plus rapides mais se trompent sur la majorité des colonnes. Le validateur tient pour eux aussi (0 erreur critique), ce qui confirme que la sécurité ne dépend pas du modèle ; mais ils **dégradent** l'import (de 73 à 141–167 confirmations, moins de décisions automatiques que les règles seules). Aucun ne peut remplacer `qwen3.5:4b` ; ils n'apportent rien par rapport aux règles seules.

## 9. Critères d'acceptation du mandat

| # | Critère | Résultat | Preuve |
| --- | --- | --- | --- |
| 1 | Corpus conservé sans perte | **PASS** : 20 fichiers relus, aucune ligne retirée, aucune cellule décalée, 0 article perdu, empreintes contrôlées | § 2, § 4.1, § 5.1 |
| 2 | Contrat v2 conforme à son schéma | **PASS** : 100/100 | § 5 |
| 3 | Valeurs jamais inventées | **PASS** : 0 (provenance et dérivation contrôlées pour chaque valeur) | § 5.1 |
| 4 | Aucune affectation commerciale incorrecte validée | **PASS** : 0 sur 100 appels et 21 080 mutations | § 4.1, § 5.1 |
| 5 | Ambiguïtés non résolues bloquantes | **PASS (prototype)** : tout `AMBIGU` / `INVALIDE` rend le fichier non applicable ; le refus côté serveur reste à implémenter (B4) | § 4, conception § 5 |
| 6 | Variantes regroupées par une logique déterministe validée | **PASS** : regroupement serveur, 0 association fausse importable | § 5.1 |
| 7 | Injections sans effet sur les décisions finales | **PASS** : 0 divergence dangereuse ; décisions non strictement identiques (12 conservatrices par passage) | § 7 |
| 8 | Temps et mémoire mesurés | **PASS** | § 8 |
| 9 | Reproductibilité | **PASS** : sorties stables, décisions identiques sur 5 passages, validateur déterministe | § 1 |
| 10 | Limites documentées | **PASS** | § 10 |

**Gate B0 : PASS** pour le prototype et le corpus du banc. Ce n'est ni une preuve sur catalogues réels ni une preuve de l'application.

## 10. Risques résiduels et limites

| Risque / limite | Portée | Mesure proposée |
| --- | --- | --- |
| **Règles écrites et corrigées sur le corpus du banc** | les chiffres « règles seules » (0 erreur) sont optimistes ; un intitulé réel inconnu ou trompeur peut produire une règle fausse, que seule l'IA pourrait signaler | essai sur catalogues réels anonymisés avant Gate D (Q-CSVAI-05) ; tests de lexique élargis à chaque nouveau fournisseur |
| Apport de l'IA non démontré | sur ce corpus : +30 % de confirmations, 0 erreur des règles rattrapée | mesurer sur catalogues réels ; si l'apport reste nul, envisager l'IA seulement pour les fichiers sans en-têtes ou aux intitulés inconnus (Q-CSVAI-09) |
| Fatigue de confirmation (jusqu'à 19 confirmations pour F17) | l'utilisateur peut valider sans lire ; le validateur ne protège pas d'une confirmation erronée | regroupement des confirmations, valeurs brutes affichées, aucun choix par défaut pour les montants (conception § 10) |
| Corpus synthétique, un seul poste | temps propres à ce poste (GPU 4 Go partagé) | mesure sur le matériel des magasins (BL-05-U2) |
| Échantillon de 40 lignes | une section rare peut manquer à l'IA | couverture mesurée et déclarée ; sans effet sur les décisions (monotonie) |
| Désignation incluant la déclinaison | un produit par ligne (rappel perdu, aucune association fausse) | regroupement assisté par l'utilisateur (hors B0) |
| Montures / solaires sans mot-clé | toujours à confirmer | une confirmation par fichier, ou profil fournisseur |
| Colonnes à sens variable selon la famille (F07, F17) | colonne entière `AMBIGU` | correspondance par famille (extension du contrat, hors B0) |
| Validateur prototype ≠ production | les preuves ne couvrent pas l'API, PostgreSQL, le poste | B1–B7, matrice QA |
| Dérive d'Ollama ou du modèle | sorties différentes d'une version à l'autre | empreintes conservées avec chaque analyse ; réanalyse explicite |

## 11. Reproduire

```powershell
cd "docs/phase/05/evidence/csvai/b0"
dotnet run bench/CsvAiBenchV2.cs -- corpus    --root .   # F16..F20 + attendus + MANIFEST_B0.json (refuse d'écraser d'autres octets)
dotnet run bench/CsvAiBenchV2.cs -- selfcheck --root .   # empreintes, lecture, lexique, oracle, 21 080 mutations : SELFCHECK B0 PASS
dotnet run bench/CsvAiBenchV2.cs -- prompt    --root .   # consigne v2, exemples, options
# runs/*.jsonl ne sont jamais écrasés : déplacer les sorties existantes avant une nouvelle exécution
dotnet run bench/CsvAiBenchV2.cs -- run   --root . --models qwen3.5:4b --passes 5 --tag main
dotnet run bench/CsvAiBenchV2.cs -- score --root .        # results/RESULTS_B0.md, summary_b0.json, scores_by_call_b0.csv, decisions/
```

Lancer depuis un shell sans entrée standard (`< /dev/null` sous Bash). Préalable : Ollama local avec les modèles aux empreintes du § 2, `nvidia-smi` pour la VRAM.
