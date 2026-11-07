# MMV V2 — GO CONDITIONNEL G05-SQL-0009
## Migration locale, dernier correctif et GATE D en un seul mandat

> Copie du mandat transmis par le propriétaire le 07/11/2026 (texte reçu en conversation), enregistrée pour référence par les documents de la Phase 05.

Je suis le propriétaire de MMV V2.

J'autorise CONDITIONNELLEMENT l'application de la migration 0009 sur `mmv_v2_dev` puis `mmv_v2_test`, uniquement après réussite intégrale des précontrôles et des sauvegardes définitives décrits ci-dessous.

Ce mandat couvre également la correction du contrôle de plan périmé, l'activation contrôlée de l'assistant CSV, la recette GATE D et la livraison ZIP finale.

Aucune autorisation de déploiement sur une base de production ou contenant des données clients réelles n'est accordée.

## 1. GATE 0 — Précontrôles obligatoires

Avant toute écriture :

- Vérifier branche, HEAD, index et worktree.
- Préserver `apps/desktop/.env.development` et les fichiers non suivis du propriétaire.
- Vérifier PostgreSQL 18.6, port 5433 et identité du cluster.
- Vérifier séparément `mmv_v2_dev` et `mmv_v2_test`.
- Confirmer migrations 0001–0008 présentes et 0009 absente.
- Vérifier l'absence de dérive du schéma.
- Vérifier l'absence d'opération SQL concurrente et de session applicative résiduelle.
- Vérifier que le propriétaire a arrêté l'API, les postes Tauri et tout processus susceptible d'écrire dans les bases.
- Vérifier que la migration cible ne contient pas d'opération destructive.
- Confirmer les empreintes des manifestes 7/7 et 2/2.

Le SHA-256 imposé du script idempotent est :

`30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0`

Si le script, la structure, les migrations ou l'identité de la base diffèrent, STOP.

Ne pas arrêter de force les processus du propriétaire. Demander son intervention lorsqu'un arrêt est nécessaire.

## 2. Sauvegardes définitives

Après arrêt des applications et vérification de l'absence d'écriture concurrente :

1. Sauvegarder séparément les deux bases.
2. Stocker les sauvegardes hors du dépôt.
3. Vérifier les codes de sortie de `pg_dump`.
4. Calculer les SHA-256.
5. Vérifier les fichiers avec `pg_restore --list`.
6. Produire les empreintes de cohérence par le script en lecture seule prévu.
7. Vérifier effectivement la possibilité de restaurer une sauvegarde dans une base distincte, ou STOP si ce contrôle ne peut être obtenu.
8. Conserver les sauvegardes de prévalidation déjà existantes, sans suppression.

Aucun secret dans les journaux.

## 3. Application contrôlée de 0009

Utiliser exclusivement le script idempotent approuvé, avec l'outil officiel `Invoke-MmvSqlScript.ps1` et un `lock_timeout` de 5 secondes.

Ordre obligatoire :

**A. `mmv_v2_dev`**

- Appliquer 0009.
- Vérifier COMMIT et code de sortie.
- Exécuter `0009_CsvImportAssistant.verify.sql`.
- Exiger 14/14 OK.
- Vérifier les neuf migrations enregistrées.
- Vérifier la conservation des anciennes données et structures.
- Documenter les empreintes après migration.

Si un contrôle échoue, STOP avant de toucher à `mmv_v2_test`.

**B. `mmv_v2_test`**

Uniquement si DEV est entièrement conforme :

- Appliquer le même script et vérifier à nouveau son SHA-256.
- Exiger COMMIT.
- Exiger verify 14/14 OK.
- Vérifier les migrations, contraintes et données préexistantes.
- Rejouer la suite PostgreSQL complète.

Aucun retour arrière automatique ou destructif. Toute restauration réelle nécessite un choix distinct du propriétaire, surtout si des écritures postérieures à la sauvegarde risquent d'être perdues.

## 4. Correction supplémentaire — Protection contre les plans périmés

L'audit indépendant a identifié un défaut potentiel dans :

`src/MMV.Application/Catalog/Assisted/AssistedImportService.cs`

La méthode `SameAsCheckAsync` compare actuellement les numéros de lignes et leurs résultats, sans vérifier toutes les valeurs et versions métier ayant servi au contrôle.

Cela peut laisser passer un changement de prix ou de caractéristiques entre le contrôle à blanc et l'import lorsque la ligne reste classée `Update`.

Corriger cette protection avant de déclarer GATE D PASS.

Exigences :

- Vérifier que les données appliquées correspondent aux données réellement contrôlées et confirmées.
- Tenir compte des versions et des modifications pertinentes des articles existants.
- Préserver la cohérence transactionnelle et les protections de concurrence existantes.
- Refuser l'application avec `409 check_stale` si le plan métier n'est plus identique.
- Ne pas confondre une simple modification sans conséquence avec un changement qui nécessite une nouvelle validation.
- Ne jamais écraser silencieusement un prix modifié entre-temps.

Ajouter au minimum deux tests de concurrence :

1. Contrôle effectué, prix modifié par un autre acteur, puis tentative d'application : refus.
2. Contrôle effectué, caractéristiques d'une offre de verres modifiées, puis tentative d'application : refus ou protection de version démontrée.

Vérifier également le cas où la modification intervient entre la relecture et l'écriture transactionnelle.

Ne pas créer de nouvelle migration si les mécanismes existants suffisent.

## 5. Décisions métier pour la recette

Conserver provisoirement les décisions suivantes :

- Identité d'une offre : fournisseur, gamme, type, indice, matériau, traitements et approvisionnement.
- Géométrie : caractéristique de l'offre, pas identité distincte.
- Deux géométries incompatibles de même identité : conflit, aucune fusion automatique.
- Une modification de capacité d'une fiche complète impose une revalidation.
- Une devise absente du fichier nécessite une confirmation explicite de la devise magasin proposée.
- Aucune conversion monétaire automatique.
- Aucune devise EUR supposée.
- Aucun effacement d'une valeur existante lorsqu'une colonne est absente.
- Aucune purge automatique des originaux.
- Ancien assistant CSV conservé comme solution de repli.

Documenter les décisions encore provisoires sans les présenter comme des validations de production.

## 6. Activation contrôlée du nouvel assistant

Après succès de 0009 et correction du défaut précédent :

- Configurer `CatalogInterpreter:WizardEnabled=true` uniquement dans l'environnement local de recette autorisé.
- Utiliser `qwen3.5:4b` pour le nouveau parcours.
- Vérifier que le backend confirme le modèle réellement exécuté.
- Ne pas modifier `.env.development` du propriétaire.
- Ne pas activer globalement le nouveau parcours en production.
- Préserver l'ancien import CSV jusqu'à validation finale.

## 7. GATE D — Recette complète

Tester la chaîne :

Tauri → HTTPS → MMV.Api → PostgreSQL 18 → Ollama `qwen3.5:4b`.

Effectuer dans le même mandat, lorsque l'environnement le permet :

- Les scénarios R1 à R9 de `LIVRAISON_05_CSVAI_POST_AUDIT.md`.
- Dépôt CSV tolérant.
- Analyse IA réelle et progression.
- Corrections individuelles et groupées.
- Contrôle à blanc.
- Confirmation des montants et devises.
- Import transactionnel.
- Idempotence et rejeu.
- Concurrence et `check_stale`.
- Doublons de SKU.
- Import des offres de verres.
- Modes stocké et sur commande.
- Revalidation des fiches complètes.
- Historique repliable.
- Clavier et contraste dans Tauri natif.
- Thèmes clair et sombre.
- Coupure et reprise API.
- Indisponibilité Ollama et repli par règles.
- Tests de non-régression des modules précédents.

Utiliser uniquement des comptes et données fictifs pour les tests.

Si deux postes Windows distincts ne sont pas disponibles, tester ce qui est réalisable et marquer honnêtement les scénarios multi-machines `NOT_TESTED`. Deux fenêtres sur un seul poste ne constituent pas une preuve de fonctionnement sur deux machines.

Si des catalogues fournisseurs réels anonymisés ne sont pas disponibles, le signaler. Ne pas en inventer.

Après les tests, vérifier l'absence de données ou comptes de test résiduels non autorisés.

## 8. Critères de clôture

Ne pas déclarer GATE D PASS si :

- La migration n'est pas conforme.
- Les tests critiques échouent.
- Le correctif `check_stale` n'est pas démontré.
- L'interface utilise un modèle différent de celui affiché.
- Une écriture commerciale incorrecte peut être acceptée.
- Des données de test indésirables subsistent.
- Le parcours Tauri réel reste non testé tout en étant présenté comme validé.

Classer chaque contrôle : PASS, FAIL, BLOCKED ou NOT_TESTED.

## 9. Livraison unique par git archive

Produire une seule archive finale compacte avec `git archive` :

`MMV-V2-CSVAI-GATE-D.zip`

Utiliser un index Git temporaire isolé pour inclure les fichiers non commités, sans modifier l'index normal.

L'archive doit contenir les fichiers complets utiles, les tests, les preuves SQL, les journaux pertinents, les captures représentatives et un rapport unique.

Ne pas recopier les anciens dossiers de preuves déjà livrés.

Fournir séparément :

- Le SHA-256 du ZIP.
- L'inventaire Git.
- Le SHA de l'arbre exporté.
- La liste des fichiers modifiés.
- Le rapport final.

Aucun commit, push, PR ou merge.

Ne pas commencer la Phase 06.

## 10. Arrêt

Si tous les contrôles obligatoires sont satisfaits, remettre le verdict :

`GATE_D — READY_FOR_OWNER_FINAL_REVIEW`

Sinon :

`GATE_D — BLOCKED`

avec les raisons exactes.

Effectuer en une seule intervention toutes les actions autorisées et réalisables.

Ne pas multiplier les validations intermédiaires, mais respecter strictement les conditions de STOP.

L'objectif est de terminer techniquement la Phase 05 et de préparer sa validation manuelle, sans compromettre les données existantes.
