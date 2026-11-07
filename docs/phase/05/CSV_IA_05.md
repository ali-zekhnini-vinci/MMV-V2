# Phase 05 — Lot CSV-IMPORT-AI-01 : banc Ollama et import CSV intelligent (04/11/2026)

Mandat : [`prompts/MMV_V2_MANDAT_CSV_IMPORT_AI_01_2026-11-04.md`](../../../prompts/MMV_V2_MANDAT_CSV_IMPORT_AI_01_2026-11-04.md). Branche locale `spike/csv-import-ollama-benchmark`, créée depuis `feat/phase-05-catalogue-stock` à `55f021d894e07452d882a85a461bbfc34052c41d`. Aucun commit, push, PR ni merge.

**Statut (06/11/2026, implémentation) : `CSVAI_C1_C2_C3_IMPLEMENTED — G05-SQL-0009 PREPARED, NOT APPLIED — OWNER_SQL_GO_AND_REVIEW_REQUIRED`.** GO conditionnel Gate B du propriétaire ([`prompts/MMV_V2_GO_CONDITIONNEL_GATE_B_CSV_IMPORT_AI_01_2026-11-06.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_GATE_B_CSV_IMPORT_AI_01_2026-11-06.md)) : C1, C2, C3 et la préparation de 0009 réalisés dans un seul cycle ; rapport unique [`LIVRAISON_05_CSVAI.md`](LIVRAISON_05_CSVAI.md) ; dossier SQL [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md) ; 0009 **non appliquée** aux bases du propriétaire ; Gate D non déclarée. Aucun commit, push, PR ni merge.

Statut précédent (conservé) : **Statut (04/11/2026, Gate B) : `GATE_B — DOSSIER REMIS, VALIDATION DU PROPRIÉTAIRE REQUISE`.** Mandat de refonte du propriétaire ([`prompts/MMV_V2_MANDAT_REFONTE_CSV_IMPORT_AI_01_GATE_B_2026-11-04.md`](../../../prompts/MMV_V2_MANDAT_REFONTE_CSV_IMPORT_AI_01_GATE_B_2026-11-04.md)) : l'écran d'import actuel est refusé ; dossier [`CSV_IA_05_GATE_B.md`](CSV_IA_05_GATE_B.md) (audit, architecture, assistant en cinq étapes, contrats API, transitions, plan, risques) et maquette navigable isolée (banc `apps/desktop/import-wizard-bench.html`, 38 captures). **Aucune intégration applicative**, aucune migration ni SQL, aucune base touchée, aucun modèle téléchargé ; `llama3.2` toujours en place dans le parcours actuel. Gates C1, C2, SQL-0009 et D : NOT_STARTED.

Statut précédent (conservé) : **Statut (04/11/2026, après B0) : `GATE_B0 — PASS (prototype, corpus synthétique) ; GATE B — VALIDATION DE LA CONCEPTION PAR LE PROPRIÉTAIRE REQUISE`.** Gate A approuvée conditionnellement par le propriétaire ([`prompts/MMV_V2_GO_CONDITIONNEL_GATE_A_CSV_IMPORT_AI_01_B0_2026-11-04.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_GATE_A_CSV_IMPORT_AI_01_B0_2026-11-04.md)) ; Lot B0 exécuté ([`evidence/csvai/b0/RAPPORT_B0.md`](evidence/csvai/b0/RAPPORT_B0.md)) ; conception révisée ([`CSV_IA_05_CONCEPTION.md`](CSV_IA_05_CONCEPTION.md)). **Aucun code applicatif modifié**, aucune migration ni SQL, aucune base touchée, aucun modèle téléchargé, `llama3.2` toujours en place dans l'application. Les corrections COR-05-01 à COR-05-08 ne font pas partie de ce mandat.

Historique du statut : 04/11/2026 (Lot A) `GATE_A — RÉSULTATS REMIS, VALIDATION DU PROPRIÉTAIRE REQUISE` ; 04/11/2026 (après B0) `GATE_B0 — PASS ; GATE B — VALIDATION DE LA CONCEPTION REQUISE` ; 04/11/2026 (Gate B) statut ci-dessus.

## 1. Gate 0 — précontrôle

| Point | Constat |
| --- | --- |
| Dépôt | `C:\Users\Ali Zekhnini\Documents\GitHub\MMV V2`, branche `feat/phase-05-catalogue-stock`, HEAD `55f021d` (lot UX-IA livré, `PHASE05_UX_AI_COMPLETE`) ; derniers commits `55f021d`, `2085719`, `f2bd748`, `95c9f97`, `1504056` ; aucune remise de côté (`git stash list` vide) |
| Modification suivie | `apps/desktop/.env.development` (deux lignes ajoutées par le propriétaire), SHA-256 `a215e1f0…`, **identique à l'empreinte consignée par les lots précédents** ; non touchée, jamais indexée |
| Non suivis | archives `*.zip` et dossiers `.claude/`, `.serena/`, `references/phase02..04/`, trois fichiers de `docs/phase/04/` : base connue du propriétaire, laissée en place |
| Travail inachevé | aucun hors des éléments ci-dessus |
| Décision | **GO avec réserve consignée** : la seule modification suivie est le fichier du propriétaire, qu'il est interdit de toucher. Les lots précédents ont travaillé sur cette même base. La création de branche ne change rien au répertoire de travail. Aucun reset, clean ni stash. Branche `spike/csv-import-ollama-benchmark` créée (elle n'existait pas) |
| Outils | .NET SDK 10.0.402, Node v22.23.3, rustc et cargo 1.99.0, PostgreSQL 18.6 (binaires ; 17 aussi présent), Ollama 0.40.11, Python 3.13.9, `nvidia-smi` |
| Matériel | Ryzen 7 5800X (8 cœurs, 16 fils), 31,9 Go de RAM (14,1 Go libres), GTX 1650 4 Go (≈ 1,2 à 1,7 Go déjà occupés par d'autres applications), disque C: 103,6 Go libres, Y: 2 670 Go libres |
| Modèles | `llama3.2:latest` (`a80c4f17acd5`, 2,0 Go), `qwen3.5:2b` (`0689d44085e0`, 2,7 Go), `qwen3.5:4b` (`d8b0f5e9760c`, 3,3 Go) **déjà installés** ; aussi `llama3`, `sqlcoder` (hors banc). Aucun téléchargement nécessaire |

## 2. Gate 0B — cartographie de l'existant

Détail des fichiers, routes, couches, persistance, tests et dépendances : [`CSV_IA_05_CONCEPTION.md` § 1](CSV_IA_05_CONCEPTION.md#1-architecture-avant-existant-cartographié-le-04112026). Constats déterminants :

- le **poste décode** le fichier et envoie du texte ; l'empreinte SHA-256 porte sur ce texte, pas sur les octets d'origine ;
- `CsvTable` est **strict** : il exige un en-tête, refuse les lignes trop longues et retire les lignes vides. Il ne peut pas servir à l'étape 2 « lecture tolérante » ;
- l'IA actuelle (`llama3.2`) ne fait que la correspondance de colonnes ; son analyse n'est pas conservée ;
- l'import catalogue ne porte **aucun attribut de famille** (calibre, puissance, rayon, diamètre, conditionnement), et les verres relèvent d'une autre cible (`LensCatalogService`) ;
- le contrôle à blanc, l'application tout ou rien, l'idempotence, le refus du réimport et les droits sont solides : ils sont **conservés**.
- Aucun catalogue fournisseur réel dans le dépôt (seuls des registres CSV de conception sous `references/` et `registres/`) : corpus du banc **synthétique**.

## 3. Lot A — banc

Rapport complet et reproductible : [`evidence/csvai/RAPPORT_BENCHMARK.md`](evidence/csvai/RAPPORT_BENCHMARK.md). 15 fichiers synthétiques (montures, lentilles, verres, mixte, accessoires, cas difficiles), mêmes consigne, schéma et paramètres pour les trois modèles. Passages exécutés l'un après l'autre : 1 chauffe exclue, puis **5 passages mesurés par modèle**, soit 225 appels, tous valides en JSON.

| | `llama3.2` | `qwen3.5:2b` | `qwen3.5:4b` |
| --- | --- | --- | --- |
| Colonnes correctes | 50,9 % | 47,4 % | **87,9 %** |
| Familles par ligne | 42 % | 69 % | **98,6 %** |
| Lignes d'articles perdues | **15** | **5** | 0 |
| Affectations commerciales incorrectes | 65 | 95 | 30 |
| Associations de variantes fausses | 30 | 40 | 30 (20 dues à l'injection) |
| Injections signalées / sans effet | 0/20 · 0/5 | 0/20 · 0/5 | 20/20 · 0/5 |
| Temps médian par fichier | 16,8 s | **16,2 s** | 42,4 s |
| Pic RAM Ollama | **2 715 Mio** | 2 918 Mio | 3 748 Mio |

**Recommandation : `qwen3.5:4b`**, sous les conditions du rapport (§ 7) : montants toujours confirmés, contrôles déterministes des références, fichier sans en-têtes confirmé colonne par colonne, variantes regroupées par le serveur, alignement établi par le lecteur, analyse sur un échantillon borné. Aucun modèle ne satisfait **seul** le critère « aucune affectation commerciale incorrecte » ; `llama3.2` et `qwen3.5:2b` échouent aussi à la conservation.

## 4. Gates

Tableau du Lot A (04/11/2026, conservé) :

| Gate | Statut | Preuve |
| --- | --- | --- |
| Gate 0 — préconditions | **PASS (avec réserve consignée § 1)** | § 1 |
| Gate 0B — cartographie | **PASS** | `CSV_IA_05_CONCEPTION.md` § 1 |
| Gate A — banc | **RÉSULTATS REMIS — validation du propriétaire requise** : 3 modèles, même corpus, 5 passages mesurés chacun, rapport reproductible | `evidence/csvai/` |
| Gate B — architecture | **NON OUVERTE** : conception à l'état de brouillon, à valider après Gate A | `CSV_IA_05_CONCEPTION.md` |
| Gate C — implémentation | **NOT_STARTED** | — |
| Gate D — validation finale | **NOT_STARTED** | — |

Mise à jour du 04/11/2026 après B0 :

| Gate | Statut | Preuve |
| --- | --- | --- |
| Gate 0 B0 — précontrôle | **PASS** : branche et HEAD inchangés, `.env.development` `a215e1f0…` intact, corpus et consigne du Lot A conformes, modèles aux mêmes empreintes ; écart expliqué : Ollama 0.40.14 (0.40.11 au Lot A) | `evidence/csvai/b0/RAPPORT_B0.md` § 2 |
| Gate A | **GO CONDITIONNEL du propriétaire** (04/11/2026) : B0 autorisé ; ni implémentation, ni migration, ni activation de Qwen, ni fusion | mandat B0 |
| Gate B0 — contrat v2 + validateur | **PASS** (prototype hors application, corpus synthétique) : 100/100 JSON valides, **0** affectation commerciale fausse atteignant le catalogue (25 proposées, 25 bloquées), 0 valeur inventée, 0 article perdu, 0 regroupement ou famille faux acceptés, 0 divergence dangereuse sur les injections, 21 080 mutations sans erreur critique ; réserves § 10 du rapport | `evidence/csvai/b0/` |
| Gate B — architecture | **Validation de la conception attendue** (conception révisée) | `CSV_IA_05_CONCEPTION.md` |
| Gate C — implémentation | **NOT_STARTED** | — |
| Gate D — validation finale | **NOT_STARTED** | — |

Mise à jour du 04/11/2026 (Gate B, mandat de refonte) :

| Gate | Statut | Preuve |
| --- | --- | --- |
| Gate 0 — audit | **PASS (réserve consignée)** : HEAD `55f021d` inchangé, worktree unique, `.env.development` `a215e1f0…` intact ; corpus B0 5/5 conformes ; sorties B0 sans empreinte antérieure (gelées ce jour, intégrité antérieure NOT_TESTED) ; seul usage d'Ollama : l'import | `CSV_IA_05_GATE_B.md` § 1 |
| Gate B — conception visuelle et technique | **DOSSIER REMIS — validation du propriétaire requise** : cinq étapes, contrats additifs, transitions, persistance `0009` révisée (sans SQL), plan par sous-lot ; maquette isolée : TypeScript et ESLint PASS, 38/38 états rendus sans erreur (simulation navigateur) | `CSV_IA_05_GATE_B.md`, `evidence/csvai/gateb/` |
| Gates C1, C2 | **NOT_STARTED** | — |
| G05-SQL-0009 | **NOT_STARTED** (préparation à autoriser ; application soumise à un GO SQL distinct) | `CSV_IA_05_GATE_B.md` § 12 |
| Gate D | **NOT_STARTED** | — |

Mise à jour du 06/11/2026 (GO conditionnel Gate B, implémentation) :

| Gate | Statut | Preuve |
| --- | --- | --- |
| Gate B | **GO CONDITIONNEL du propriétaire** (06/11/2026) : architecture recevable avec réserves ; C1, C2, C3 et préparation 0009 autorisés dans un seul cycle | mandat du 06/11/2026 |
| Gate C1 — moteur | **IMPLÉMENTÉ, testé** (unitaires 95, PostgreSQL jetable 6, Ollama réel 1) | `LIVRAISON_05_CSVAI.md` § 1, § 3 |
| Gate C2 — API et poste | **IMPLÉMENTÉ, testé** (API, Vitest 5, captures du composant réel) ; écran **désactivé par défaut** jusqu'à 0009 | idem |
| Gate C3 — verres | **IMPLÉMENTÉ, testé** (PostgreSQL jetable : offres de deux fournisseurs, références décidées, plages jamais complétées, restriction combinée, « à vérifier ») | idem |
| G05-SQL-0009 | **PRÉPARÉE, NON APPLIQUÉE** — GO SQL distinct requis ; cluster jetable : idempotent ×2, verify 14/14, restauration 14/14 | `SQL_0009_REVUE.md` |
| Gate D | **NON DÉCLARÉE** : 0009 sur `mmv_v2_test`, natif deux postes, catalogues réels anonymisés manquants | `LIVRAISON_05_CSVAI.md` § 4 |

## 5. Décisions du propriétaire

Questions posées au Lot A (texte d'origine conservé) :

| ID | Question |
| --- | --- |
| Q-CSVAI-01 | Valider `qwen3.5:4b` comme modèle principal (avec ses conditions), ou demander un autre arbitrage. Ce choix remplacera `llama3.2` (D-UX-03) **pour l'import catalogue** une fois le Lot B livré. |
| Q-CSVAI-02 | Le temps mesuré (42 s médians, 62 s au plus, pour un fichier ; 66 s pour la première analyse après chargement) est-il acceptable ? Faut-il le mesurer d'abord sur le matériel réel des magasins (BL-05-U2) ? |
| Q-CSVAI-03 | Persistance : option A (sans migration, avec ses limites) ou option B (migration additive `0009` en lot G05-SQL distinct) — `CSV_IA_05_CONCEPTION.md` § 7. |
| Q-CSVAI-04 | Les catalogues de **verres** (plages, offres) entrent-ils dans le Lot B, avec pour cible le catalogue des verres, ou dans un lot suivant ? |
| Q-CSVAI-05 | Accord pour un essai de confirmation sur de vrais catalogues fournisseurs anonymisés, fournis par le propriétaire (le corpus actuel est synthétique). |
| Q-CSVAI-06 | Valider l'étape B0 : mesurer le contrat v2 (rôles de colonne, regroupement par le serveur) avant tout branchement dans l'application. |

Réponses du propriétaire (04/11/2026, GO conditionnel Gate A) :

| ID | Décision |
| --- | --- |
| Q-CSVAI-01 | **`qwen3.5:4b` retenu comme candidat principal** pour l'import catalogue. Activation réelle subordonnée au résultat B0 et à la validation serveur. D-UX-03 (`llama3.2`) reste en vigueur dans l'application. |
| Q-CSVAI-02 | **40–60 s refusés comme attente bloquante obligatoire.** Étudier une analyse asynchrone (suivi de l'état, annulation, repli par les règles). **Aucun SLA définitif** avant mesures supplémentaires. Suite : conception § 2. |
| Q-CSVAI-03 | **Option B retenue sur le principe** : migration additive `0009` dans un futur dossier G05-SQL distinct. Aucun SQL créé ni appliqué en B0. Schéma exact à l'ADR et au dossier SQL : proposition en conception § 7. |
| Q-CSVAI-04 | **Catalogues de verres inclus**, cible : référentiel spécialisé des verres et de leurs offres fournisseurs. |
| Q-CSVAI-05 | **Accord pour des essais sur catalogues réels anonymisés**, dès qu'ils seront fournis et autorisés. Aucune donnée réelle pour B0. |
| Q-CSVAI-06 | **GO pour le banc B0** du contrat v2 : exécuté, `RAPPORT_B0.md`. |

Nouvelles questions (après B0) :

| ID | Question |
| --- | --- |
| Q-CSVAI-07 | Durée de conservation des **octets d'origine** des fichiers importés (purge des octets en gardant empreinte, taille et encodage ; conception § 7.1). |
| Q-CSVAI-08 | Application d'un lot pendant l'analyse IA : attendre la fin de l'analyse **ou** permettre « continuer sans l'assistant », tracé (conception § 2.2). |
| Q-CSVAI-09 | Rôle de l'IA au vu de B0 (aucune décision automatique, +30 % de confirmations sur ce corpus, apport à mesurer sur catalogues réels) : la garder pour tous les fichiers, ou seulement pour les fichiers sans en-têtes et aux intitulés inconnus des règles. |
| Q-CSVAI-10 | Montures sans mot-clé « monture » / « solaire » : accepter une confirmation de famille par fichier (choix B0), ou admettre une famille par défaut du profil fournisseur. |

Décisions de travail du mandat de refonte (04/11/2026, consignées sans les étendre) : **Q-CSVAI-07** durée définitive à valider, purge contrôlée des octets possible sans perte des preuves, aucune purge automatique ; **Q-CSVAI-08** aucune application pendant une analyse IA demandée, vérification sans IA seulement sur choix explicite après échec, aucun contournement silencieux ; **Q-CSVAI-09** IA déclenchée par défaut pour les catalogues pris en charge, limitable par configuration, gains à mesurer sur catalogues réels ; **Q-CSVAI-10** aucune famille sur supposition, suggestion avec confirmation. Mise en œuvre proposée : `CSV_IA_05_GATE_B.md` § 11.

Nouvelles questions (Gate B) :

| ID | Question |
| --- | --- |
| Q-CSVAI-11 | Référentiel des verres : dans C2, ou sous-lot C3 dédié après le catalogue des produits (recommandé) ? |
| Q-CSVAI-12 | Rétention du texte décodé des lots (`imports.batches.content`, figé) : hors périmètre ou à instruire ? |
| Q-CSVAI-13 | Délai provisoire de 120 s par analyse (sans valeur de SLA) ? |
| Q-CSVAI-14 | SKU porté par un autre modèle : autoriser « associer à l'article existant » (mise à jour), ou seulement modifier le SKU / ignorer la ligne ? |

Décision du propriétaire (07/11/2026, clôture contrôlée de la Phase 05 — [`RAPPORT_CLOTURE_PHASE05.md`](RAPPORT_CLOTURE_PHASE05.md) § 3.2) : après activation et essai sur son installation locale, **l'interface et la conception du nouvel assistant ne sont PAS acceptées** (`OWNER_UX_REJECTED`) ; implémentation technique terminée ; GATE D réalisé avec ses limites ; refonte **reportée** (BL-05-13, défauts à décrire par le propriétaire) ; ancien import conservé en repli ; **aucun GO de déploiement commercial** sans nouvelle recette (BL-05-14). Les questions Q-CSVAI-07 et 11 à 14 restent ouvertes.

## 6. Inventaire des outils et dépendances ajoutés

Aucun paquet NuGet ou npm, aucun outil installé, aucun modèle téléchargé. Ajouts : un harnais de banc en fichier unique .NET 10 (`docs/phase/05/evidence/csvai/bench/CsvAiBench.cs`, exécuté par `dotnet run`), un fichier `.gitattributes` local au corpus (`* -text`, octets figés). Python 3.13 (déjà présent sur le poste) n'a servi qu'à relire les journaux.

B0 (04/11/2026) : un second harnais en fichier unique .NET 10 (`docs/phase/05/evidence/csvai/b0/bench/CsvAiBenchV2.cs` : générateur des fixtures F16–F20, contrat v2, échantillonnage, lecture stricte, validateur prototype, mutations, notation), un `.gitattributes` local au corpus B0. Aucun paquet, aucun outil installé. Python 3.13 : relecture des journaux et application de correctifs au harnais.

Gate B (04/11/2026) : aucun paquet, aucun outil installé, aucun modèle téléchargé. Ajout d'une page de banc isolée servie par Vite en développement seulement (`apps/desktop/import-wizard-bench.html`, `apps/desktop/src/bench/importWizard/`), sur le modèle du banc 04-A ; captures par les scripts existants `e2e/bench-04a/serve.mjs` et `shoot.mjs` (Edge sans interface).

## Journal

| Date | Entrée |
| --- | --- |
| 2026-11-07 | **Clôture contrôlée de la Phase 05** : validation UX du propriétaire **refusée** ; refonte reportée (BL-05-13) ; recette complémentaire avant tout déploiement (BL-05-14) ; création concurrente d'offres de même identité classée non bloquante (BL-05-15) ; garde serveur `WizardEnabled = false` prouvée par `CsvAssistantDisabledGateTests` et contre-épreuves ; aucune modification du code produit, aucune migration. Rapport [`RAPPORT_CLOTURE_PHASE05.md`](RAPPORT_CLOTURE_PHASE05.md). |
| 2026-11-06 | **Implémentation** (GO conditionnel Gate B) : mandat archivé ; C1 (lecteur tolérant, échantillon, contrat v2 identique au banc B0, validateur porté avec couche des décisions de l'utilisateur, diagnostic des doublons, exemples et empreinte des montants, file d'analyse asynchrone, résultats tardifs, interruption, choix « sans IA »), C2 (13 routes additives, persistance EF, assistant React en cinq étapes activé par le serveur, ancien écran conservé), C3 (offres de verres) ; migration 0009 préparée (non appliquée) ; tests : API 656 (dont 95 du lot), Maintenance 50, PostgreSQL 101 PASS + 2 BLOCKED (0009 absente des bases du propriétaire) + 1 NOT_TESTED, Ollama réel PASS, Vitest 331, TypeScript, ESLint ; captures du composant réel ; rapport unique `LIVRAISON_05_CSVAI.md` ; archive par `git archive`. |
| 2026-11-04 | **Gate B** (mandat de refonte, interface actuelle refusée) : mandat archivé ; audit Gate 0 PASS (réserve consignée) ; constats du parcours actuel (rejets au dépôt par `CsvTable`, IA synchrone non conservée, correction par nouveau fichier, modèle configuré affiché comme utilisé) ; dossier `CSV_IA_05_GATE_B.md` ; maquette isolée des cinq étapes et de l'historique replié, 38 captures (clair, sombre, 1024 à 1920 px, zoom 125 %) ; empreintes B0 gelées. Aucune intégration ; en attente : décision du propriétaire sur le dossier Gate B et Q-CSVAI-07, 11 à 14. |
| 2026-11-04 | **B0** (GO conditionnel du propriétaire) : décisions Q-CSVAI-01 à 06 enregistrées ; Gate 0 B0 PASS (écart expliqué : Ollama 0.40.14) ; 5 fichiers ajoutés (F16, F17 de 320 et 306 lignes ; F18/F19, F20 témoins d'injection) ; validateur prototype, lexique (70 cas) et 21 080 mutations sans erreur critique ; corrections du harnais **avant** tout passage (taille des fichiers, monotonie, `Type` des verres) ; pilotes exclus archivés ; série mesurée `qwen3.5:4b` 1 chauffe + 5 × 20 appels : 100 % JSON valides, 0 erreur critique ; contrôle v1 sous Ollama 0.40.14 et pilote des modèles légers. Conception révisée (contrat, règles, asynchrone, persistance `0009` proposée sans SQL). Gate B0 PASS ; en attente : Gate B (Q-CSVAI-07 à 10). |
| 2026-11-04 | Gate 0 (GO avec réserve), cartographie, inventaire des modèles (tous présents), historique Llama du 03/11 retrouvé et déclaré non comparable. Pilote v0 (exclu, archivé dans `evidence/csvai/runs/pilot-v0/`) : famille du fichier toujours « mixed » chez les trois modèles, d'où la famille et l'en-tête déduits des lignes ; règle de notation des montants rétrogradés précisée ; témoin F15 ajouté. Banc mesuré : 225 appels, 5 passages par modèle, sorties stables d'un passage à l'autre. Recommandation `qwen3.5:4b` sous conditions ; conception du Lot B à l'état de brouillon. En attente : Gate A. |
