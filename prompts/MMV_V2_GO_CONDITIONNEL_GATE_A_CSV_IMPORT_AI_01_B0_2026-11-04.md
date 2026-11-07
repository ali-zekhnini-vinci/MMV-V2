# MMV V2 — GO CONDITIONNEL GATE A
## LOT CSV-IMPORT-AI-01 — EXÉCUTION B0 ET RÉVISION DE LA CONCEPTION

> Copie de travail de la décision et du mandat remis par le propriétaire le 04/11/2026 (archivée sans modification de fond, comme les mandats précédents dans `prompts/`).

Tu poursuis le travail sur MMV V2, dans le dépôt existant et sur la branche `spike/csv-import-ollama-benchmark`.

Le propriétaire a examiné le rapport du Lot A et le document de conception du Lot B.

**Décision : GATE A APPROUVÉ CONDITIONNELLEMENT.**

Tu es autorisé à réaliser le Lot B0, c'est-à-dire la validation expérimentale du nouveau contrat `mmv.csvai.v2`.

Cette autorisation ne constitue PAS un GO pour l'implémentation applicative, les migrations, l'activation de Qwen dans MMV ni la fusion de la Phase 05.

## 1. Décisions à enregistrer

- Q-CSVAI-01 : `qwen3.5:4b` retenu comme candidat principal pour l'import catalogue. L'activation réelle dépend du résultat B0 et de la validation serveur.
- Q-CSVAI-02 : les délais de 40–60 secondes ne sont pas acceptés comme attente bloquante obligatoire dans l'interface. Étudier une analyse asynchrone, avec suivi de l'état, possibilité d'annulation et repli par règles. Ne pas fixer de SLA définitif avant mesures supplémentaires.
- Q-CSVAI-03 : option B retenue sur le principe. Prévoir une migration additive `0009` dans un futur dossier G05-SQL distinct. Aucune création ou application SQL autorisée dans B0.
- Q-CSVAI-04 : les catalogues de verres sont inclus. Leur cible métier reste le référentiel spécialisé des verres et de leurs offres fournisseurs.
- Q-CSVAI-05 : accord pour des essais sur catalogues réels anonymisés lorsque ceux-ci seront fournis et autorisés. Aucune donnée réelle n'est disponible pour ce lot.
- Q-CSVAI-06 : GO pour le benchmark B0 du contrat v2.

Enregistrer ces décisions en préservant l'historique des décisions précédentes.

## 2. Précontrôle GATE B0

Avant tout travail :

1. Vérifier la branche actuelle et le HEAD.
2. Exécuter `git status --short`.
3. Vérifier l'intégrité du travail du Lot A.
4. Contrôler les empreintes des fixtures et des prompts.
5. Vérifier les modèles Ollama installés et leurs empreintes.
6. Préserver impérativement `apps/desktop/.env.development`, dont l'empreinte de référence commence par `a215e1f0`.
7. Préserver les archives et les fichiers non suivis.
8. Vérifier que les preuves du Lot A ne seront pas écrasées.

Ne pas modifier les configurations locales du propriétaire.

En cas d'écart inexpliqué, STOP et rapport.

## 3. Objectif du B0

Le contrat v1 a révélé les faiblesses suivantes :

- Affectations commerciales incorrectes.
- Regroupements de variantes erronés.
- Sensibilité aux injections de prompt.
- Confusion entre références, noms de fabricant et montants.
- Difficultés sur les fichiers sans en-têtes.
- Temps d'analyse élevé.

Le contrat v2 doit réduire la responsabilité du modèle.

**L'IA propose une interprétation des colonnes. Le serveur détermine les valeurs, les rattachements, les regroupements et la validité métier.**

Le benchmark doit vérifier ce principe et mesurer son efficacité réelle.

## 4. Expérimentation

Réutiliser les 15 fichiers synthétiques du Lot A.

Ajouter deux fichiers d'au moins 300 lignes présentant plusieurs familles, variations de formats et anomalies réparties dans tout le fichier.

Exécuter au minimum :

- Une chauffe exclue des mesures.
- Cinq passages mesurés avec `qwen3.5:4b`.
- Le nouveau contrat `mmv.csvai.v2`.
- Une sortie JSON strictement validée.
- Un échantillon borné et déterministe.
- Des mesures de durée et de ressources.
- Un contrôle des données de sortie avec un résultat attendu indépendant.

Ne jamais remplacer ou modifier les résultats du Lot A.

Si nécessaire, faire un pilote comparatif limité des modèles plus légers avec le contrat v2, sans substitution automatique. Le contrat v2 étant plus simple, les classements du benchmark v1 ne sont pas nécessairement transposables.

## 5. Validation déterministe expérimentale

Construire un validateur prototype indépendant de la production.

Il doit vérifier les propositions de l'IA et empêcher toute affectation non justifiée.

Tester notamment :

- Prix d'achat versus prix de vente.
- HT versus TTC.
- Montant versus pourcentage.
- Fabricant versus référence fabricant.
- Référence fournisseur versus SKU.
- SKU versus code-barres.
- Famille et attributs.
- Colonnes sans en-têtes.
- Colonnes dupliquées.
- Valeurs inconnues.
- Regroupement des variantes.
- Lignes de continuation.
- Cellules et en-têtes contenant des injections.

Une proposition incorrecte doit être refusée, mise en ambiguïté ou soumise explicitement à l'utilisateur.

Le validateur ne doit pas simplement reproduire les décisions du modèle.

Les résultats attendus doivent être établis indépendamment des sorties de l'IA.

## 6. Corrections à intégrer à la conception

### C1 — Montants

Clarifier la différence entre correspondance fournisseur déjà apprise, validation technique et confirmation des montants.

Aucune décision du modèle ne peut lever une confirmation commerciale exigée.

### C4 — Références

Ne pas rejeter systématiquement les références alphanumériques ou entièrement alphabétiques.

Un nom de fabricant et une référence fournisseur ne sont pas équivalents.

En cas de doute, utiliser `AMBIGU`.

### CSV — Valeurs numériques

Préserver les nombres décimaux signés, notamment les puissances optiques négatives.

Ne pas assimiler un nombre négatif valide à une formule Excel dangereuse.

### Identité des fichiers

Étudier la transition entre les empreintes du texte décodé et celles des octets originaux.

Prévoir la compatibilité avec les lots existants.

Ne pas modifier le mécanisme actuel d'idempotence dans B0.

### Échantillonnage

Ne pas utiliser exclusivement les premières lignes du fichier.

Proposer un échantillonnage déterministe qui couvre :

- Le début du fichier.
- Des positions réparties dans le fichier.
- Les anomalies détectées.
- Les familles distinctes détectables par les règles.
- Les changements de structure.

Si certaines sections ne sont pas suffisamment représentées, déclarer l'analyse incomplète plutôt que généraliser abusivement.

### Stockage et conservation

Avant de proposer définitivement `original_bytes bytea`, étudier les limites de taille, la croissance de PostgreSQL, la rétention, les sauvegardes et l'accès aux fichiers.

L'option B reste la décision de principe, mais son schéma exact doit faire l'objet de l'ADR et du dossier SQL.

## 7. Tests de sécurité

Rejouer F13 et son témoin F15.

Le validateur final doit rendre les mêmes décisions métier pour les données équivalentes, indépendamment des instructions malveillantes insérées.

Le modèle peut produire une réponse différente, mais cette réponse ne doit provoquer :

- Aucun regroupement incorrect accepté.
- Aucune famille imposée sans preuve.
- Aucune modification de prix.
- Aucune disparition d'article.
- Aucune modification de l'identité d'un produit.

Ajouter des injections dans les en-têtes, les notes, les références et les cellules de continuation.

Les cellules contenant des instructions restent des données non fiables.

## 8. Critères d'acceptation du B0

Le Lot B0 sera acceptable seulement si :

1. Le corpus est conservé sans perte.
2. Le contrat v2 respecte son schéma.
3. Les valeurs originales ne sont jamais inventées.
4. Le validateur ne laisse passer aucune affectation commerciale incorrecte comme valide.
5. Les ambiguïtés non résolues bloquent l'application.
6. Les variantes sont regroupées uniquement par une logique déterministe validée.
7. Les tentatives d'injection n'altèrent pas les décisions métier finales.
8. Le temps et la consommation mémoire sont mesurés.
9. Les résultats sont reproductibles.
10. Les limites sont clairement documentées.

Une erreur reconnue et bloquée par le validateur est différente d'une erreur qui atteint le catalogue.

Présenter ces deux métriques séparément.

Si un critère critique échoue, ne pas déclarer le B0 PASS.

## 9. Livrables

Produire :

- Rapport B0 avec résultats détaillés.
- Comparaison v1 versus v2 sur les métriques comparables.
- Tableau des erreurs avant et après validation.
- Analyse des injections.
- Mesures de performance.
- Rapport des risques résiduels.
- Contrat JSON v2 révisé.
- Règles du validateur révisées.
- Proposition d'architecture actualisée.
- Proposition de persistance et de migration 0009, sans SQL exécuté.
- Matrice QA actualisée.
- Décisions et gates mis à jour.

Utiliser les chemins et conventions documentaires déjà présents dans le dépôt.

Conserver les preuves brutes utiles à une revue indépendante, sans secret.

## 10. Restrictions

Dans ce mandat :

- Aucun changement dans le code applicatif.
- Aucune modification du frontend de production.
- Aucune migration créée ou appliquée.
- Aucun accès en écriture aux bases persistantes.
- Aucun remplacement de `llama3.2` dans l'application.
- Aucun nouveau téléchargement de modèle.
- Aucun commit, push, PR ni merge.
- Aucun démarrage de Phase 06.
- Aucun effacement des preuves du Lot A.

## 11. Clôture

À la fin, présenter un tableau des gates :

| Gate | Résultat |
|---|---|
| Gate 0 | Précontrôle |
| Gate A | GO conditionnel du propriétaire |
| Gate B0 | PASS / FAIL / BLOCKED |
| Gate B | Validation de conception attendue |
| Gate C | NOT_STARTED |
| Gate D | NOT_STARTED |

Terminer par une recommandation argumentée :

- GO pour préparer l'implémentation ;
- GO avec réserves ;
- ou NO-GO, avec corrections nécessaires.

**STOP APRÈS LE LOT B0.**

Ne pas commencer B1, B2, B3 ni les étapes suivantes sans un nouveau mandat.

L'objectif est de prouver que l'architecture v2 sécurise réellement l'interprétation des catalogues avant d'introduire cette nouvelle logique dans MMV V2.
