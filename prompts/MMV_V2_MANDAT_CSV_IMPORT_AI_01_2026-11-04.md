# MMV V2 — MANDAT D'IMPLÉMENTATION
## LOT CSV-IMPORT-AI-01 — BENCHMARK OLLAMA ET NOUVELLE ARCHITECTURE D'IMPORT INTELLIGENT

> Copie de travail du mandat remis par le propriétaire le 04/11/2026 (archivée sans modification de fond, comme les mandats précédents dans `prompts/`).

**Projet :** Manage My Vision V2
**Périmètre :** Import catalogue CSV — Analyse intelligente par IA locale
**Type de travail :** Spike technique, benchmark, conception et implémentation contrôlée
**Priorité :** Haute

---

# 0. CONTEXTE ET OBJECTIF

Nous poursuivons le développement de MMV V2.

L'objectif de ce mandat est de transformer le fonctionnement actuel de l'import CSV.

Le système actuel doit être étudié avant toute modification. Nous souhaitons évoluer vers une architecture dans laquelle une IA locale intervient dès l'analyse du fichier CSV, afin de comprendre sa structure et les informations métier qu'il contient.

Cette évolution doit permettre d'importer des catalogues provenant de fournisseurs différents, même lorsque :

- Les colonnes ne sont pas identiques.
- Les en-têtes sont absents ou incomplets.
- Les colonnes changent d'ordre.
- Les lignes présentent des structures irrégulières.
- Les informations sont réparties sur plusieurs colonnes.
- Les variantes d'un produit sont représentées de différentes manières.
- Les familles de produits possèdent des structures différentes.
- Les fichiers contiennent plusieurs langues.
- Les prix, unités et références commerciales présentent des ambiguïtés.

L'objectif n'est pas de construire un simple convertisseur CSV.

Nous voulons construire un moteur d'interprétation de catalogues fournisseurs, capable de comprendre les données, de les rattacher au référentiel métier MMV et de présenter un résultat vérifiable avant importation.

**Règle fondamentale : aucune invention, aucune perte silencieuse de données et aucune affectation commerciale incorrecte.**

# 1. ARCHITECTURE ET CONTRAINTES NON NÉGOCIABLES

Conserver intégralement la stack MMV V2 :

| Composant | Technologie |
|---|---|
| Desktop | Tauri 2 |
| Frontend | React + TypeScript + Vite |
| Backend | ASP.NET Core .NET 10 LTS |
| Base de données | PostgreSQL 18 |
| IA locale | Ollama |
| Communication backend/frontend | Architecture existante MMV V2 |

Interdictions :

- Aucun SQLite.
- Aucun changement de stack.
- Aucune IA cloud ou API externe.
- Aucun transfert de catalogue vers un service externe.
- Aucune modification des bases persistantes sans autorisation distincte.
- Aucune modification de `.env.development`.
- Aucune modification des migrations existantes sans justification et validation.
- Aucun commit, push, PR ou merge automatique.

L'IA doit fonctionner localement avec Ollama.

Les règles métier restent sous le contrôle du backend MMV. L'IA ne devient jamais la source de vérité.

# 2. MÉTHODE DE TRAVAIL OBLIGATOIRE

Respecter la méthode utilisée lors des phases précédentes de MMV V2.

## GATE 0 — PRÉCONTRÔLE

Avant toute intervention :

1. Vérifier le dépôt et son emplacement.
2. Identifier la branche actuelle.
3. Identifier le SHA exact du HEAD.
4. Exécuter `git status --short`.
5. Examiner les modifications suivies et non suivies.
6. Examiner les derniers commits.
7. Identifier les éventuelles modifications non terminées.
8. Vérifier les outils disponibles.
9. Identifier les versions de .NET, Node, Rust, PostgreSQL et Ollama.
10. Vérifier les ressources matérielles disponibles : CPU, RAM, GPU, VRAM et espace disque.

Ne jamais supprimer, écraser ou mettre de côté des modifications préexistantes.

Si le worktree est sale ou si un travail antérieur risque d'être affecté :

**STOP — NO-GO.**

Ne jamais effectuer de reset, de clean ou de stash automatique.

Si les préconditions sont conformes, créer une branche locale dédiée :

`spike/csv-import-ollama-benchmark`

Si elle existe déjà, vérifier son état et ne pas écraser son contenu.

## GATE 0B — CARTOGRAPHIE DU SYSTÈME EXISTANT

Avant d'écrire du code, analyser :

- Le mécanisme actuel d'import CSV.
- Les services backend concernés.
- Les DTO et modèles utilisés.
- Les validations métier.
- Les relations entre produits, variantes, fournisseurs et familles.
- Les composants frontend de l'import.
- Les tests existants.
- Les éventuels traitements asynchrones.
- Les intégrations existantes avec Ollama.
- Les éventuels prototypes ou benchmarks précédents.

Identifier précisément les fichiers responsables de chaque opération.

Lire les documentations existantes et les décisions techniques de la Phase 05.

Produire une cartographie des dépendances et des points d'entrée.

Ne pas remplacer un mécanisme existant avant d'avoir compris son fonctionnement.

---

# 3. LOT A — BENCHMARK DES TROIS MODÈLES OLLAMA

## 3.1 Objectif

Comparer trois modèles pour identifier celui qui comprend le mieux les catalogues fournisseurs utilisés dans MMV.

Modèles candidats :

| ID | Modèle Ollama |
|---|---|
| M1 | `llama3.2` |
| M2 | `qwen3.5:2b` |
| M3 | `qwen3.5:4b` |

Vérifier les noms exacts et les versions réellement disponibles avec Ollama.

Ne jamais substituer silencieusement un modèle à un autre.

Si un modèle manque, identifier sa taille, son espace disque nécessaire et les conditions de téléchargement.

Ne pas télécharger de modèle sans avoir préalablement présenté les ressources nécessaires et obtenu l'autorisation.

## 3.2 Benchmark précédent

Un essai préliminaire Llama a déjà été réalisé sur quatre fichiers avec deux passages.

Retrouver les éventuels scripts, prompts, fichiers et résultats correspondants.

Cet essai constitue un historique technique, mais ne constitue pas le benchmark final.

Qwen doit être évalué dans les mêmes conditions.

Ne réutiliser d'anciens résultats que si les modèles, versions, prompts, fichiers et paramètres sont strictement comparables.

## 3.3 Corpus de test

Rechercher les fichiers CSV représentatifs déjà présents dans le projet.

Les familles à tester sont :

1. Montures.
2. Lentilles de contact.
3. Verres optiques.
4. Catalogue mixte.

Ajouter des cas difficiles :

- CSV sans en-têtes.
- Colonnes décalées.
- Séparateurs différents.
- Encodages différents.
- Guillemets et cellules contenant des séparateurs.
- Lignes irrégulières.
- Plusieurs langues.
- Variantes regroupées sur plusieurs lignes.
- Colonnes dupliquées.
- Prix ambigus.
- Valeurs manquantes.
- Unités différentes.
- Données incorrectes.
- Instructions malveillantes insérées dans les cellules.

Utiliser exactement les mêmes fichiers pour les trois modèles.

Si les fichiers existants contiennent des données personnelles ou commerciales sensibles, ne pas les exploiter sans validation. Préparer des fixtures synthétiques clairement identifiées si nécessaire.

Chaque fixture doit disposer d'un résultat attendu de référence, vérifié indépendamment des modèles.

## 3.4 Conditions expérimentales

Pour chaque modèle :

- Même corpus.
- Même consigne.
- Même schéma de sortie.
- Mêmes paramètres d'inférence lorsque compatibles.
- Même configuration de contexte.
- Même environnement matériel.
- Exécution locale uniquement.

Effectuer au minimum **5 passages mesurés par modèle sur la même matrice de fichiers**.

Prévoir un éventuel passage de chauffe, exclu des mesures.

Ne pas lancer les trois modèles simultanément si cela fausse les mesures de ressources.

Enregistrer les versions exactes, les paramètres et les empreintes des modèles disponibles.

## 3.5 Critères d'évaluation

Évaluer séparément les critères suivants :

| Critère | Vérification |
|---|---|
| Reconnaissance des familles | Identification correcte du type de produit |
| Reconnaissance des colonnes | Correspondance entre colonne CSV et champ métier |
| Alignement | Reconstruction correcte des lignes |
| Variantes | Regroupement sans association incorrecte |
| Conservation | Valeurs originales préservées |
| Ambiguïtés | Détection des informations incertaines |
| JSON | Respect du format structuré attendu |
| Hallucinations | Absence d'informations inventées |
| Robustesse | Comportement sur les fichiers irréguliers |
| Sécurité | Résistance aux instructions présentes dans les données |
| Performance | Temps de traitement |
| Ressources | RAM, VRAM et utilisation CPU/GPU |

Mesurer notamment :

- Taux de correspondance correcte des colonnes.
- Taux de reconnaissance correcte des familles.
- Exactitude des associations de variantes.
- Nombre de valeurs perdues.
- Nombre de valeurs inventées.
- Nombre d'affectations incorrectes.
- Nombre d'ambiguïtés correctement signalées.
- Taux de JSON valides.
- Temps médian et dispersion des temps.
- Pic de RAM et VRAM.

Ne pas créer arbitrairement une note globale pondérée sans expliciter les pondérations.

La conservation exacte des données et l'absence d'affectation commerciale incorrecte constituent des critères éliminatoires.

## 3.6 Livrables du benchmark

Créer un dossier de preuves isolé, en respectant les conventions existantes du dépôt.

Il doit contenir :

- Corpus de test.
- Résultats attendus.
- Prompts utilisés.
- Paramètres d'inférence.
- Sorties brutes des modèles.
- Journaux d'exécution.
- Mesures de ressources.
- Résultats des cinq passages.
- Tableau comparatif.
- Rapport de benchmark.

Le rapport doit permettre de reproduire le benchmark.

## 3.7 Décision sur le modèle

Présenter :

- Le meilleur modèle en exactitude.
- Le meilleur modèle en compréhension métier.
- Le meilleur modèle en vitesse.
- Le modèle le moins gourmand.
- Les éventuels échecs critiques.
- Les compromis observés.

Proposer un modèle principal pour MMV et justifier le choix.

Ne pas sélectionner un modèle uniquement parce qu'il est plus rapide ou plus volumineux.

**LOT A — GATE DE VALIDATION PROPRIÉTAIRE.**

À cette étape, remettre les résultats et attendre ma validation du modèle retenu avant l'intégration dans l'application.

---

# 4. LOT B — NOUVELLE ARCHITECTURE D'IMPORT CSV

Après validation du benchmark et du modèle retenu, procéder à la conception détaillée et à l'implémentation.

L'architecture cible est la suivante.

## ÉTAPE 1 — FICHIER CSV ORIGINAL

L'utilisateur sélectionne son fichier CSV.

Le système doit :

- Conserver le fichier original.
- Calculer son SHA-256.
- Identifier son nom, sa taille et son format.
- Préserver son contenu sans modification.
- Ne pas demander à l'utilisateur de reformater le fichier.

Le fichier original constitue la référence immuable de l'analyse.

## ÉTAPE 2 — LECTURE TOLÉRANTE

Construire un mécanisme de lecture capable de gérer :

- UTF-8 et BOM.
- Encodages historiques compatibles avec les fichiers fournisseurs.
- Virgules, points-virgules, tabulations et autres séparateurs détectables.
- Guillemets et champs contenant des séparateurs.
- Cellules vides.
- Colonnes variables.
- Lignes irrégulières.
- En-têtes absents.
- Lignes comportant un nombre différent de cellules.

Ne jamais supprimer silencieusement une ligne.

Ne jamais décaler arbitrairement des cellules pour rendre un CSV artificiellement valide.

Conserver les numéros de lignes, les cellules brutes et les anomalies détectées.

Produire une représentation structurée intermédiaire qui conserve la provenance de chaque valeur.

## ÉTAPE 3 — IA LOCALE

C'est le cœur de la nouvelle architecture.

Le modèle Ollama retenu intervient directement dans l'analyse sémantique du fichier.

Il doit comprendre :

- La famille de produits.
- La signification des colonnes.
- Les différents formats de références.
- Les variantes.
- Les relations entre lignes.
- Les informations commerciales.
- Les caractéristiques techniques.
- Les différences entre les familles de produits.

Il doit pouvoir reconnaître qu'un même champ métier peut être présenté différemment selon le fournisseur.

Il doit produire une proposition structurée en JSON.

Chaque valeur interprétée doit pouvoir être reliée à sa source :

- Numéro de ligne.
- Colonne d'origine.
- Valeur brute.
- Champ métier proposé.
- Niveau de confiance.
- Justification éventuelle.
- Statut de validation.

Si le modèle ne comprend pas une valeur, il doit la signaler comme inconnue ou ambiguë.

Il ne doit jamais inventer une référence, un prix, une puissance, une correction optique ou une caractéristique commerciale.

Les instructions présentes dans les cellules CSV doivent être considérées comme des données non fiables, jamais comme des consignes à exécuter.

L'IA ne doit avoir aucun accès direct à PostgreSQL.

## ÉTAPE 4 — VALIDATION MÉTIER MMV

Le backend MMV doit reprendre toutes les propositions de l'IA et les confronter aux règles métier.

La validation doit être déterministe.

Vérifier notamment :

- La famille du produit.
- Les champs obligatoires.
- Les formats de référence.
- Les prix et devises.
- Les unités.
- Les variantes.
- Les fournisseurs.
- Les caractéristiques techniques.
- Les règles spécifiques aux verres.
- Les incohérences entre les champs.
- Les doublons potentiels.

Une valeur reconnue par l'IA n'est pas automatiquement une valeur valide.

Prévoir des statuts distincts :

- VALIDE.
- AVERTISSEMENT.
- AMBIGU.
- INVALIDE.
- NON RECONNU.

Les valeurs originales doivent rester consultables.

Ne jamais transformer automatiquement une valeur ambiguë en valeur supposée correcte.

## ÉTAPE 5 — RÉCAPITULATIF ET IMPORT

Présenter un écran récapitulatif à l'utilisateur avant toute écriture.

Il doit afficher :

- Nombre de lignes analysées.
- Nombre de produits détectés.
- Nombre de variantes.
- Familles identifiées.
- Colonnes reconnues.
- Colonnes non reconnues.
- Erreurs.
- Avertissements.
- Ambiguïtés.
- Doublons potentiels.
- Valeurs nécessitant une correction manuelle.

L'utilisateur doit pouvoir consulter les valeurs originales et les interprétations proposées.

Il doit pouvoir corriger les correspondances avant l'import.

**Aucune donnée ne doit être importée avant la confirmation explicite de l'utilisateur.**

L'import définitif doit respecter les transactions, les règles de persistance et les mécanismes d'intégrité existants de MMV V2.

Prévoir également la traçabilité de l'import et la possibilité de retrouver les anomalies rencontrées.

---

# 5. EXIGENCES TECHNIQUES DE L'IMPLÉMENTATION

L'architecture doit respecter les frontières existantes de MMV V2.

Examiner notamment :

- La séparation Domain / Application / Infrastructure.
- Les services applicatifs existants.
- Les DTO.
- Les contrats API.
- Les mécanismes de validation.
- La gestion des erreurs.
- Les transactions PostgreSQL.
- Les permissions.
- Les conventions de journalisation.

Ne pas introduire de dépendance inutile.

Toute nouvelle bibliothèque doit être justifiée, vérifiée quant à sa licence et à sa compatibilité .NET 10.

Ne pas introduire de logique métier dans React.

Ne pas connecter directement le frontend à Ollama si cela contourne les frontières de sécurité existantes.

L'architecture de communication avec Ollama doit être définie côté backend, avec des délais d'expiration, une gestion des indisponibilités et des réponses invalides.

Ne pas imposer une migration PostgreSQL si le besoin peut être traité avec le schéma existant.

Toute migration réellement nécessaire devra faire l'objet d'un lot de revue distinct.

# 6. MATRICE QA OBLIGATOIRE

Préparer des tests couvrant les situations suivantes :

| ID | Scénario | Résultat attendu |
|---|---|---|
| CSV-01 | CSV classique | Lecture et interprétation correctes |
| CSV-02 | CSV sans en-têtes | Détection des colonnes |
| CSV-03 | Colonnes réordonnées | Correspondances correctes |
| CSV-04 | Lignes irrégulières | Aucune perte silencieuse |
| CSV-05 | Encodage différent | Lecture correcte ou erreur explicite |
| CSV-06 | Plusieurs familles | Classification correcte |
| CSV-07 | Variantes | Associations correctes |
| CSV-08 | Prix ambigus | Ambiguïté signalée |
| CSV-09 | Valeur inconnue | Aucune invention |
| CSV-10 | Colonnes inconnues | Conservation des données |
| CSV-11 | Prompt injection | Instructions ignorées |
| CSV-12 | Ollama indisponible | Erreur maîtrisée, aucun import partiel |
| CSV-13 | JSON invalide | Rejet et traçabilité |
| CSV-14 | Import interrompu | Intégrité des données préservée |
| CSV-15 | Réimport du même fichier | Comportement idempotent maîtrisé |
| CSV-16 | Modification manuelle | Correction conservée après validation |

Ajouter des tests spécifiques aux familles montures, lentilles et verres.

Les tests doivent exercer les vrais parcours applicatifs et pas uniquement des classes isolées.

# 7. DOCUMENTATION OBLIGATOIRE

Créer ou mettre à jour les documents selon les conventions du dépôt :

1. Rapport du benchmark Ollama.
2. ADR sur le choix du modèle.
3. Architecture détaillée de l'import CSV intelligent.
4. Diagramme des flux.
5. Contrat JSON de sortie de l'IA.
6. Spécification des validations métier.
7. Matrice QA.
8. Rapport d'implémentation.
9. Changelog et roadmap Phase 05.

Toute documentation nécessaire doit être synchronisée avant le statut `READY_FOR_OWNER_REVIEW`.

Ne pas modifier inutilement les autres documents du projet.

# 8. GESTION DES GATES

Respecter strictement les portes suivantes.

**GATE 0 — Préconditions**

Dépôt, environnement, branche et baseline conformes.

**GATE A — Benchmark**

Les trois modèles ont été testés avec le même corpus et au moins cinq passages mesurés par modèle.

Résultats reproductibles et rapport disponible.

Attendre validation du propriétaire.

**GATE B — Architecture**

Conception détaillée, ADR, diagrammes, contrats et matrice QA disponibles.

Attendre validation du propriétaire avant modification du code applicatif.

**GATE C — Implémentation**

Code, tests et intégration réalisés.

Aucune migration persistante non autorisée.

**GATE D — Validation finale**

Build, tests, intégrité, sécurité, non-régression et documentation vérifiés.

Produire le rapport complet.

Ne jamais déclarer un gate PASS sur la base d'une simple supposition.

Si un contrôle échoue, documenter le problème et appliquer STOP.

# 9. LIVRABLE FINAL

Le rapport de clôture devra contenir :

1. État Git initial.
2. Branche et SHA de référence.
3. Cartographie de l'existant.
4. Benchmark des trois modèles.
5. Résultats détaillés par modèle.
6. Modèle retenu et justification.
7. Architecture avant/après.
8. Fichiers créés.
9. Fichiers modifiés.
10. Modifications des contrats API.
11. Modifications éventuelles du schéma.
12. Tests exécutés.
13. Résultats PASS / FAIL / NOT TESTED.
14. Captures des écrans concernés.
15. Preuves d'intégrité.
16. Risques résiduels.
17. Décisions encore attendues.
18. Prochaine étape recommandée.

Fournir également un inventaire des outils et dépendances ajoutés.

# 10. INTERDICTIONS FINALES

- Aucun commit automatique.
- Aucun push.
- Aucune PR automatique.
- Aucun merge.
- Aucun reset Git.
- Aucun nettoyage destructif.
- Aucune modification des bases persistantes sans GO distinct.
- Aucune modification du `.env.development`.
- Aucune modification de la stack.
- Aucune suppression de tests existants pour faire passer la suite.
- Aucun PASS inventé.
- Aucune donnée commerciale inventée.
- Aucune refonte graphique hors périmètre.

---

# 11. INSTRUCTION DE DÉMARRAGE

Commence immédiatement par :

1. GATE 0.
2. Cartographie du système d'import existant.
3. Inventaire Ollama et des trois modèles.
4. Récupération des anciens essais Llama.
5. Identification du corpus de benchmark.
6. Définition du protocole expérimental.
7. Vérification de la faisabilité sur le matériel disponible.

Si les préconditions sont satisfaites et que les trois modèles sont disponibles, tu peux réaliser le LOT A dans son environnement isolé.

Si un téléchargement, une modification sensible ou une opération risquée est nécessaire, STOP et demande mon autorisation.

À la fin du LOT A, présente le rapport de benchmark et ta recommandation.

Prépare ensuite la conception du LOT B, sans modifier le code applicatif avant validation du propriétaire.

**Ne commence pas les corrections COR-05-01 à COR-05-08 dans ce mandat.**

**Ce mandat concerne exclusivement le moteur d'import CSV intelligent de MMV V2.**

L'objectif est une solution robuste, reproductible, sécurisée et adaptée aux catalogues optiques réels, pas une simple démonstration d'intelligence artificielle.
