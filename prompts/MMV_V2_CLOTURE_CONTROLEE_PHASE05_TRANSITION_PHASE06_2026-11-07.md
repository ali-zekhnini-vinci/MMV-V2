# MMV V2 — CLÔTURE CONTRÔLÉE PHASE 05 ET TRANSITION PHASE 06

## Décision du propriétaire

Je souhaite terminer la Phase 05 et passer à la Phase 06 sans continuer les multiples cycles de développement consacrés à l'assistant CSV.

J'ai activé et essayé le nouvel assistant CSV dans mon installation locale.

**Mon verdict utilisateur : l'interface et sa conception ne sont PAS acceptées.** J'ai identifié de nombreux problèmes que je souhaite traiter ultérieurement.

En revanche, le socle technique de la Phase 05 est suffisamment avancé pour préparer sa clôture, sous réserve de l'absence de défaut critique.

Je demande un mandat unique regroupant les opérations finales.

## A — Précontrôle et intégrité

Vérifier l'état réel du dépôt MMV V2 :

- Branche, HEAD, index et modifications non commitées.
- Relation entre `spike/csv-import-ollama-benchmark`, `feat/phase-05-catalogue-stock` et `main`.
- Présence de tous les développements de Phase 05.
- Migrations 0007, 0008 et 0009.
- Suites de tests et preuves GATE D.
- Configuration locale de l'assistant.
- Absence de secrets dans les modifications destinées à Git.

Préserver impérativement `apps/desktop/.env.development` et les fichiers personnels.

Ne pas exécuter `reset --hard`, `clean`, stash automatique ni une opération susceptible de supprimer du travail.

## B — Traitement des réserves

Enregistrer formellement les décisions suivantes.

**1. Assistant CSV**

- Implémentation technique terminée.
- GATE D réalisé avec les limites documentées.
- Validation UX du propriétaire : REFUSÉE.
- Nouvelle refonte : REPORTÉE.
- Ancien import conservé comme solution de repli.
- Aucun GO pour un déploiement commercial du nouvel assistant sans nouvelle recette.

Créer une entrée de backlog dédiée aux défauts de conception, avec un identifiant unique conforme au registre existant.

Ne pas inventer le détail des défauts que le propriétaire n'a pas encore décrits.

**2. Contrôles non réalisés**

Reporter explicitement, sans les déclarer PASS :

- Deux machines Windows distinctes.
- Catalogues réels anonymisés.
- Touche Échap en Tauri natif.
- Non-régression native complète des modules antérieurs.
- Performances de Qwen sur le matériel réel des magasins.

**3. Risques techniques**

Examiner les risques encore ouverts, notamment la création concurrente d'offres de verres de même identité.

Si un défaut peut corrompre les données ou contourner une règle commerciale, le corriger de manière ciblée ou désactiver son chemin d'exécution pour les déploiements non autorisés.

Ne pas introduire de migration supplémentaire sans validation distincte.

Les défauts purement ergonomiques sont documentés puis reportés.

**4. Autres réserves**

Conserver les dossiers ouverts :

- G05-FISCAL / ADR-010.
- G05-LEGAL.
- Conservation des originaux CSV.
- Choix définitif de rétention des sauvegardes.
- Contrastes globaux.
- Autres réserves existantes.

Ne pas les marquer comme résolues sans preuve.

## C — Clôture documentaire

Actualiser les documents existants de la Phase 05.

Créer un rapport consolidé :

`docs/phase/05/RAPPORT_CLOTURE_PHASE05.md`

Il doit contenir :

- Périmètre réalisé.
- Décisions approuvées.
- Décisions reportées.
- Résultats des tests.
- État des migrations.
- Risques résiduels.
- Défauts bloquants ou non bloquants.
- Statut UX de l'assistant.
- Limites d'exploitation.
- État Git.
- Conditions de validation finale.

Distinguer :

`TECHNICALLY_IMPLEMENTED`

`OWNER_UX_REJECTED`

`DEFERRED`

`BLOCKED`

`READY_FOR_MANUAL_MERGE`

Ne pas déclarer la Phase 05 entièrement approuvée si sa validation UX reste refusée.

## D — Consolidation Git

La Phase 05 ne doit pas être fusionnée en perdant les développements du lot CSVAI.

Auditer d'abord la relation entre les branches.

Consolider proprement les travaux sur la branche de livraison Phase 05, sans écraser les fichiers personnels.

J'autorise la préparation et la création de commits LOCAUX limités aux fichiers du projet et nécessaires à cette clôture.

Avant chaque commit, vérifier les fichiers indexés et l'absence de secrets.

Ne pas inclure `.env.development`.

Ne pas modifier `main` directement.

Ne pas effectuer de push, créer une PR ou faire un merge sans ma validation finale.

Exécuter les tests de non-régression nécessaires après consolidation.

En cas de conflit Git ou de divergence non résolue, STOP sans forcer.

## E — Archive de clôture

Utiliser exclusivement la méthode `git archive`.

Créer :

`MMV-V2-PHASE05-CLOTURE.zip`

Inclure les fichiers complets nécessaires à la revue finale, le rapport consolidé et les preuves essentielles.

Ne pas reproduire les centaines de preuves historiques déjà livrées.

Fournir :

- Archive ZIP.
- SHA-256.
- Arbre Git exporté.
- Inventaire.
- Résultats des tests.
- Branche et commit de livraison.

## F — Préparation de la Phase 06

Dans ce même mandat, préparer le dossier de cadrage de la Phase 06 en lecture seule et documentation uniquement.

Ne pas commencer son développement applicatif avant que la Phase 05 soit fusionnée.

La Phase 06 correspond au module **Ventes et paiements**.

Reprendre la feuille de route officielle et les références du Lot 4.

Le Lot 4 comporte 15 groupes de design et 31 états visuels, actuellement référencés comme DRAFT.

Analyser les dépendances déjà disponibles :

- Catalogue.
- Tarification.
- Stock.
- Clients.
- Dossiers optiques.
- Permissions.
- Audit.
- PostgreSQL et transactions.

Préparer la liste des décisions métier restant à valider avant l'implémentation :

- Cycle de vie d'une vente.
- Réservation et décrémentation du stock.
- Paiement et encaissement.
- Numérotation.
- Annulation et corrections.
- Fiscalité.
- Idempotence.
- Cohérence avec les commandes et l'atelier futurs.

Ne pas présumer que ces règles sont déjà approuvées.

Créer un document de cadrage Phase 06, séparé du rapport de clôture Phase 05, contenant les questions réellement ouvertes et un plan d'implémentation consolidé.

## G — Rapport final

À la fin du mandat, présenter :

1. Statut de clôture technique Phase 05.
2. Liste des risques bloquants éventuels.
3. Branche et commits locaux.
4. Résultats finaux des tests.
5. Archive `git archive` et empreintes.
6. Étapes nécessaires pour ma validation et le merge.
7. Dossier de préparation Phase 06.
8. Décisions nécessaires avant de développer les ventes.

STOP avant push, PR ou merge.

Ne pas engager de modification Phase 06 sur une branche Phase 05 non fusionnée.

**Objectif : terminer proprement la Phase 05, sans prétendre que l'UX de l'assistant CSV est approuvée, puis préparer immédiatement la Phase 06 sans multiplier les allers-retours.**
