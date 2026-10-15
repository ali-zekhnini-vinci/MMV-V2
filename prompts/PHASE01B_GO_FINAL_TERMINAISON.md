# MMV V2 — GO FINAL PHASE 01B — TERMINAISON COMPLÈTE AVANT PHASE 02

Tu es l'architecte logiciel senior responsable de la livraison technique de MMV V2.

Le propriétaire confirme que les migrations PostgreSQL ont été exécutées manuellement et t'autorise à terminer la Phase 01B dans son périmètre déjà défini.

**OBJECTIF : terminer intégralement la Phase 01B, produire les preuves, synchroniser la documentation et préparer les commits locaux pour la revue finale.**

Ne multiplie plus les demandes d'autorisation pour des opérations de développement et de test déjà approuvées. Arrête-toi uniquement en cas de risque réel, d'opération destructrice ou de décision architecturale importante non validée.

---

## 1. ÉTAT OFFICIEL DU PROJET

Dépôt : MMV-V2.

Branche de travail : `feat/phase-01b-integration`.

Phase 01A : fusionnée manuellement dans `main`, commit `4ed20a8`.

Phase 01B : en cours de finalisation.

Phase 02 : NON COMMENCÉE.

Ne consulte jamais l'ancien projet MMV. La V2 est une reconstruction intégrale.

Stack retenue :
- Tauri 2
- React / TypeScript / Vite
- ASP.NET Core .NET 10
- EF Core / Npgsql
- PostgreSQL 18 exclusivement
- Architecture serveur/postes Windows à terme

SQLite est interdit, dans tous les environnements.

Préserve les décisions architecturales et les limites de phase déjà documentées.

## 2. MIGRATIONS POSTGRESQL — EXÉCUTÉES

Le propriétaire a exécuté :

`docs/phase/01b/sql/0001_InitialBaseline.idempotent.sql`

SHA-256 approuvé :

`9C8077A5F016002E3E2E69373CBBAF8AC86DA1B14D1EE16E90678564C657C784`

Résultats :

### Développement

- Base : `mmv_v2_dev`
- Rôle : `mmv_v2_dev`
- PostgreSQL 18, port 5433
- CREATE TABLE
- START TRANSACTION
- DO
- COMMIT
- **DEV exit=0**

### Tests

- Base : `mmv_v2_test`
- Rôle : `mmv_v2_test`
- PostgreSQL 18, port 5433
- NOTICE : `__EFMigrationsHistory` existe déjà
- CREATE TABLE
- START TRANSACTION
- DO
- COMMIT
- **TEST exit=0**

### Explication officielle du NOTICE

Le propriétaire avait exécuté les commandes une première fois.

Lors de cette première tentative, il s'était trompé dans le mot de passe de la base DEV, mais pas dans celui de la base TEST.

La migration TEST avait donc déjà été exécutée. Lors de la seconde exécution, PostgreSQL a signalé que `__EFMigrationsHistory` existait déjà.

Ce comportement est attendu avec le script idempotent.

**Il n'est pas nécessaire d'ouvrir une nouvelle enquête sur ce NOTICE.**

Il faut néanmoins vérifier l'état effectif des deux bases et confirmer qu'aucune ligne d'historique n'est dupliquée.

Ne réapplique pas les migrations et n'effectue aucun nettoyage.

## 3. CONTRÔLES POST-MIGRATION

Effectue les vérifications en lecture seule sur les deux bases.

Vérifie :

1. Base réellement connectée.
2. Rôle utilisé.
3. Version PostgreSQL 18.
4. Port 5433.
5. Propriétaire et schéma.
6. Présence de `__EFMigrationsHistory`.
7. Structure correcte et clé primaire.
8. Présence d'une seule ligne pour `20261015125000_InitialBaseline`.
9. Version EF Core enregistrée.
10. Absence de tables métier, données fictives ou objets inattendus.
11. Cohérence avec les migrations EF Core déclarées dans le projet.

Distingue précisément :
- Migration SQL exécutée avec succès.
- État de la base vérifié.
- État EF Core vérifié.

Si les résultats sont conformes, poursuis immédiatement.

Si une anomalie véritable est détectée, arrête les opérations d'écriture et présente uniquement les éléments nécessaires à sa résolution.

## 4. TESTS D'INTÉGRATION POSTGRESQL

L'autorisation O-B02 a déjà été donnée pour les tests d'écriture éphémères sur `mmv_v2_test`.

Exécute les tests prévus :

- Connexion Npgsql réelle.
- Contrôle strict du nom de base, du rôle, du port et de la version.
- Lecture du schéma.
- Vérification de l'historique EF Core.
- Création d'une table temporaire dans la base TEST uniquement.
- Écriture de test.
- Transaction et ROLLBACK.
- Vérification de l'absence de résidus.

Aucune écriture de test sur `mmv_v2_dev`.

Aucune connexion à PostgreSQL 17.

Aucun `EnsureCreated()`, migration automatique au démarrage, DROP ou opération destructive.

Les contrôles de cible doivent précéder chaque test d'écriture.

## 5. FINALISATION API ASP.NET CORE

Termine et vérifie le backend de Phase 01B.

### /health/live

- Confirme que l'API répond.
- Reste indépendant de PostgreSQL.
- Ne divulgue pas d'information sensible inutile.

### /health/ready

- Vérifie une connexion réelle à PostgreSQL.
- Retourne HTTP 200 lorsque PostgreSQL est disponible.
- Retourne HTTP 503 lorsque PostgreSQL est indisponible.
- Gère correctement le timeout.
- Ne révèle aucun secret, chaîne de connexion ou détail d'infrastructure sensible.

Effectue de vrais tests, et non uniquement des tests simulés.

Vérifie également que les migrations ne sont jamais déclenchées automatiquement au démarrage.

## 6. FINALISATION TAURI + REACT + HTTPS

L'objectif est de démontrer la chaîne complète :

**Fenêtre Tauri → HTTPS → API ASP.NET Core → PostgreSQL 18**

Le certificat HTTPS local a déjà été approuvé par Windows.

Termine le client API TypeScript et l'écran de diagnostic technique prévu pour 01B.

L'interface doit présenter des états réellement issus de l'API :

- API disponible.
- PostgreSQL disponible.
- API indisponible.
- PostgreSQL indisponible.
- Chargement et erreurs réseau.

Vérifie :

- Une véritable fenêtre Tauri Windows.
- Les appels réels depuis WebView2.
- La validation TLS normale, sans exception de certificat.
- L'origine réellement utilisée par WebView2.
- Les règles CORS restrictives.
- La CSP de développement.
- L'absence de connexion directe du client à PostgreSQL.
- L'absence de secret côté React/Tauri.
- L'absence de sondage permanent non prévu.

Ne modifie pas inutilement l'architecture ni le design validés.

Ne développe aucun écran métier, login, dashboard ou module fonctionnel.

Fournis une capture de la fenêtre native et les preuves techniques des scénarios testés.

Une capture de navigateur ne remplace pas une preuve Tauri native.

## 7. TESTS FRONTEND ET NON-RÉGRESSION

Exécute les vérifications pertinentes :

- TypeScript check.
- ESLint.
- Build frontend.
- Tests unitaires.
- Tests du client HTTP.
- Tests des états de l'interface.
- Build Tauri Windows.
- Vérification de l'exécutable natif.
- Tests backend .NET.
- Tests PostgreSQL autorisés.
- Contrôles de sécurité du périmètre 01B.

### Vitest

L'installation de Vitest a été confirmée comme fonctionnelle dans un environnement npm propre.

Je souhaite revenir à la stack frontend prévue si cela peut être réalisé proprement.

Tu peux intégrer Vitest à la place de la solution provisoire `node --test`, sous réserve de :

- Vérifier la compatibilité des versions.
- Mettre à jour correctement `package.json` et le lockfile.
- Exécuter les tests.
- Ne pas casser les tests existants.
- Documenter ce changement.

Si un blocage sérieux subsiste, conserve temporairement la solution fonctionnelle et documente-le sans arrêter toute la Phase 01B.

Aucun résultat non exécuté ne doit être présenté comme PASS.

## 8. SERENA ET GRAPHIFY

### Serena

La navigation symbolique C# fonctionne déjà.

La configuration TypeScript a été ajoutée au projet, mais le serveur MCP doit être reconnecté.

Tente la reconnexion par les mécanismes disponibles et vérifie un véritable symbole TypeScript.

Si une intervention de session Claude Code est indispensable, signale la procédure de reconnexion sans bloquer le reste de 01B.

Un simple redémarrage ne prouve pas que le langage TypeScript fonctionne.

### Graphify

Actualise le graphe avec le code final.

Effectue au moins une analyse utile du chemin :

API → Infrastructure → DbContext → PostgreSQL

Et, si représentable dans le graphe :

React → Client API → Backend.

Graphify CLI suffit pour 01B. Ne prétends pas que Graphify MCP fonctionne.

Fournis les résultats réellement obtenus.

## 9. DOCUMENTATION GATE — OBLIGATOIRE

La synchronisation documentaire est une obligation permanente du projet MMV V2.

Relis et actualise tous les documents concernés :

- `README.md`
- `CLAUDE.md`
- `docs/phase/00.md` si réellement concerné
- `docs/phase/01.md`
- `docs/phase/01b/`
- `GATES_AND_DECISIONS.md`
- `QA_MATRIX.md`
- `SOURCE_MAP.md`
- `IMPLEMENTATION_SPEC.md`
- Documentation d'architecture
- ADR impactés
- Backlog et plan de phases si nécessaire

Conserve les décisions historiques et ajoute des amendements datés.

La documentation doit refléter :

- Phase 01A fusionnée.
- Phase 01B implémentée ou encore partiellement bloquée selon les preuves.
- PostgreSQL 18 et les deux bases effectivement créées.
- Migrations baseline effectivement exécutées.
- Explication du NOTICE de la base TEST.
- Tests PostgreSQL réellement réalisés.
- Résultats HTTPS.
- Résultats Tauri natifs.
- Configuration Serena/Graphify.
- Limitations connues.
- Décisions reportées aux futures phases.

Ne confonds jamais l'environnement local de développement avec une installation multiposte opérationnelle.

Ne déclare pas ADR-006 ou d'autres ADR approuvés sans validation du propriétaire.

Vérifie la cohérence entre documentation, code, tests et état Git.

## 10. GIT ET LIVRAISON

Travaille exclusivement sur :

`feat/phase-01b-integration`

Préserve les modifications et commits déjà présents.

Avant la livraison :

1. Examine le diff complet.
2. Vérifie l'absence de secrets.
3. Vérifie l'absence de fichiers générés parasites.
4. Vérifie les dépendances.
5. Vérifie les tests.
6. Vérifie le DOCUMENTATION GATE.
7. Prépare des commits locaux cohérents.

Les commits locaux sont autorisés.

**Interdictions :**
- Aucun push.
- Aucune Pull Request automatique.
- Aucun merge.
- Aucun force push.
- Aucune modification de `main`.
- Aucun développement de Phase 02.
- Aucun accès à l'ancien MMV.

## 11. RAPPORT FINAL UNIQUE

Une fois les tâches terminées, présente un rapport structuré comprenant :

### A. État de la Phase 01B
Fonctionnalités techniques implémentées et limites.

### B. PostgreSQL
Résultats des contrôles post-migration et des tests transactionnels.

### C. API
Résultats de `/health/live` et `/health/ready`.

### D. Tauri
Preuves de communication HTTPS réelle et capture native.

### E. Qualité
Tests .NET, frontend, Tauri, sécurité et non-régression.

### F. Outillage
Serena et Graphify, avec statuts exacts.

### G. Documentation
Fichiers synchronisés et conformité au DOCUMENTATION GATE.

### H. Git
Commits locaux, SHA, fichiers modifiés et état de la branche.

### I. Écarts éventuels
Uniquement les vrais problèmes restants, avec leur gravité et leur impact.

### J. Préparation Phase 02
Indique si le socle est prêt pour commencer la Phase 02.

Ne commence pas à implémenter la Phase 02 et ne considère pas son périmètre comme approuvé.

## 12. RÈGLE DE TRAVAIL

Le propriétaire souhaite maintenant avancer vers le développement réel de MMV V2.

Évite les démarches administratives superflues et les cycles de validation inutiles.

Réalise toutes les opérations techniques autorisées en autonomie.

N'interromps le travail que pour une véritable décision bloquante, une opération destructive, un problème de sécurité important ou une action qui exige nécessairement le propriétaire.

Ne sacrifie pas la qualité, mais reste pragmatique.

**Livraison attendue : Phase 01B finalisée, testée, documentée et prête pour audit global et décision de merge.**

Termine ton rapport par :

`PHASE01B_COMPLETE — OWNER_REVIEW_REQUIRED`
