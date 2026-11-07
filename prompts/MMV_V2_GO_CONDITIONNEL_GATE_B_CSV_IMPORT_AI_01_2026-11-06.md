# MMV V2 — GO CONDITIONNEL GATE B
## Mandat unique d'implémentation CSV-IMPORT-AI-01

> Archivé tel que reçu le 06/11/2026 (message du propriétaire), sans modification du fond.

Tu poursuis le développement du nouveau module d'import CSV intelligent de MMV V2.

Le dossier GATE B a été audité. Son architecture technique est recevable avec réserves.

**Tu es autorisé à réaliser dans un seul cycle C1, C2, C3, la préparation SQL 0009 et tous les tests possibles sans appliquer de migration aux bases persistantes.**

Ne t'arrête pas après chaque sous-lot pour demander une validation, sauf en cas de blocage de sécurité, décision métier nouvelle ou opération nécessitant une autorisation explicite.

## 1. Précontrôle

Vérifier la branche, le HEAD, l'état Git, les fichiers non suivis, la baseline applicative, PostgreSQL 18 et Ollama.

Préserver intégralement `apps/desktop/.env.development`, les archives du propriétaire et tout travail préexistant.

Lire les documents GATE B, B0, les ADR, les matrices QA et les décisions de Phase 05.

Utiliser exclusivement le nouveau dépôt MMV V2. Ne jamais consulter l'ancien projet.

## 2. C1 — Moteur d'import intelligent

Implémenter :

- `TolerantCsvReader` et `CsvDocument`.
- Échantillonnage déterministe représentatif.
- Interprétation locale avec `qwen3.5:4b`.
- Validation stricte du contrat `mmv.csvai.v2`.
- Validation métier déterministe.
- Reconstruction des produits et variantes côté serveur.
- Détection et diagnostic des doublons.
- Corrections en révisions append-only.
- Traçabilité des données originales.
- Gestion des analyses asynchrones.
- Annulation, délais, reprise et résultats tardifs.
- Repli explicite sur les règles seules.

L'IA ne doit jamais décider seule d'un prix, d'un SKU, d'une famille incertaine ou d'une fusion de produits.

Les anomalies métier ne doivent pas bloquer le dépôt du fichier, mais elles doivent bloquer toute écriture non conforme dans le catalogue.

## 3. C2 — API et interface Tauri

Implémenter les cinq étapes approuvées sur le principe :

1. Déposer le fichier.
2. Analyse automatique par l'IA.
3. Vérifier et corriger.
4. Vérification avant import.
5. Confirmation et import.

Reprendre les éléments du banc GATE B et le design system Lot 6.

Exigences :

- Barre de progression pour les opérations mesurables.
- Indicateur indéterminé pour l'inférence.
- Journal d'activité réel du serveur.
- Modèle Ollama réellement utilisé.
- Navigation avant/arrière sans perte de corrections.
- Corrections individuelles et groupées.
- Confirmation explicite des montants.
- Historique des imports replié par défaut.
- Interface responsive, clair/sombre et accessible au clavier.
- Aucune logique métier déportée dans React.

Préserver l'ancien parcours sous contrôle d'activation jusqu'à ce que le nouveau soit validé.

## 4. C3 — Catalogue des verres

Intégrer les fichiers de verres au référentiel spécialisé existant.

Respecter :

- Fabricants et gammes.
- Indices.
- Traitements.
- Plages SPH/CYL/ADD.
- Diamètres.
- Restrictions combinées.
- Offres de plusieurs fournisseurs.
- Prix et délais.
- Validation des compatibilités.
- Traçabilité des informations originales.

Ne pas assimiler un verre correcteur à une variante de monture.

Ne pas inventer de caractéristiques optiques ni de capacités fournisseurs.

Prévoir des tests spécifiques au référentiel des verres et à ses règles de compatibilité.

## 5. G05-SQL-0009 — Préparation

Préparer la migration additive 0009 et son dossier complet :

- Migration EF Core.
- Script SQL idempotent.
- Script de vérification en lecture seule.
- Inventaire des modifications.
- Compatibilité avec 0007 et 0008.
- Vérification des contraintes.
- SHA-256.
- Analyse des risques.
- Procédure de sauvegarde et de restauration.
- Tests sur un PostgreSQL 18 jetable.

**Interdiction absolue d'appliquer 0009 à `mmv_v2_dev` ou `mmv_v2_test` sans GO SQL distinct.**

Ne pas utiliser de migration automatique ni d'écriture directe pour contourner cette règle.

## 6. Intégration et recette

Exécuter, selon les dépendances et les possibilités de l'environnement :

- Build .NET 10.
- Tests API.
- Tests Maintenance.
- Tests Vitest.
- Vérification TypeScript et ESLint.
- Tests PostgreSQL 18 sur base jetable.
- Tests d'import transactionnel.
- Tests d'idempotence.
- Tests de concurrence.
- Tests de sécurité et d'injection.
- Tests de corrections et de révisions.
- Tests des variantes et catalogues de verres.
- Tests avec Ollama réel.
- Tests du parcours Tauri natif.
- Tests d'accessibilité et de non-régression visuelle.
- Vérification des autres modules de Phase 05.

Utiliser des données fictives pour les tests isolés.

Les tests dépendant de l'application de 0009 sur les bases persistantes restent `BLOCKED` ou `NOT_TESTED`, jamais `PASS` fictif.

Ne pas déclarer GATE D terminé si toutes ses preuves obligatoires ne sont pas réunies.

## 7. Décisions à respecter

- Rétention des originaux : aucune purge automatique avant politique validée.
- Texte décodé : ne pas modifier les données historiques sans autorisation.
- Timeout Ollama : 120 secondes provisoires, configurable, sans SLA.
- Doublon SKU sur un autre modèle : pas d'association automatique ; correction ou exclusion explicite.
- IA déclenchée par défaut pour le nouveau parcours catalogue, avec configuration permettant le mode règles seules.
- Priorité des décisions : utilisateur validé > règle métier > suggestion IA, sans possibilité de contourner une règle obligatoire.
- Aucun téléchargement automatique de modèles.
- Aucun changement de stack ou de SGBD.

## 8. Documentation

Mettre à jour les documents concernés en conservant l'historique.

Produire un rapport unique de livraison précisant :

- Ce qui a réellement été développé.
- Ce qui est testé.
- Ce qui reste bloqué.
- Les migrations proposées.
- Les résultats de tests.
- Les risques résiduels.
- Les décisions qui demandent encore mon intervention.

Ne pas multiplier les rapports redondants.

## 9. Livraison ZIP — NOUVELLE RÈGLE OBLIGATOIRE

Nous abandonnons la méthode précédente de constitution d'une grosse archive par copie manuelle de centaines de fichiers.

Désormais, utiliser exclusivement `git archive` pour exporter le contenu du lot.

Commande de référence :

`git archive --format=zip --output=<chemin_zip> <tree-ish>`

Attention : `git archive HEAD` seul n'inclut pas les modifications non commitées.

Si le travail n'est pas commité, créer un arbre Git temporaire avec un index isolé (`GIT_INDEX_FILE`, `git read-tree`, `git add` sur une liste explicite et `git write-tree`), puis archiver cet arbre avec `git archive`.

Ne jamais modifier l'index Git normal ni créer un commit sans autorisation.

Inclure uniquement les fichiers utiles :

- Code source créé ou modifié.
- Tests correspondants.
- Migrations SQL proposées.
- Documents de conception et de décisions actualisés.
- Rapport unique de livraison.
- Journaux synthétiques des tests.
- Captures représentatives nécessaires à la revue visuelle.

Ne pas réinclure toutes les anciennes preuves des Lots A, B0 et GATE B. Les référencer avec leurs empreintes et chemins.

Exclure obligatoirement les `.env`, secrets, bases, données privées, `node_modules` et fichiers temporaires.

Vérifier avant et après la création de l'archive que le worktree du propriétaire est inchangé par l'opération.

Nom de sortie :

`MMV-V2-CSVAI-IMPLEMENTATION.zip`

Fournir séparément son SHA-256 et le SHA de l'arbre Git exporté.

Vérifier l'intégrité du ZIP et l'inventaire de ses fichiers.

Ne pas inclure d'artefacts volumineux sans nécessité d'audit.

## 10. Fin de mandat

Une fois tous les travaux réalisables terminés :

1. Vérifier le code et les tests.
2. Préparer le dossier SQL exact.
3. Produire le ZIP avec `git archive`.
4. Donner le chemin de l'archive.
5. Présenter un résumé unique des résultats et des blocages.
6. STOP avant toute application de 0009 sur les bases persistantes.

Aucun push, aucune PR, aucun merge.

Ne pas commencer la Phase 06.

**Objectif : une seule livraison intégrée et auditable, sans validations intermédiaires inutiles, tout en préservant les portes de sécurité SQL et les décisions du propriétaire.**
