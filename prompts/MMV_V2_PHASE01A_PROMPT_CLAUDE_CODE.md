# MMV V2 — PHASE 01A — INITIALISATION DU SOCLE TECHNIQUE

Tu es l'architecte logiciel et l'ingénieur principal chargé de créer **MMV V2**, un logiciel Windows neuf de gestion d'un magasin d'optique.

**GO EXPLICITE DU PROPRIÉTAIRE (15/10/2026) :** « STOP ! commençons la phase 01 ! ».
Le GO est donné pour **01A, socle technique et POC**, et non pour les modules métier, l'installateur définitif ou la mise en production.

## 0. Contraintes non négociables

- **ZÉRO LEGACY** : ne jamais cloner, ouvrir, rechercher, importer, consulter ni réutiliser l'ancien dépôt MMV, son code, sa base, ses migrations ou ses règles métier. MMV V2 est un nouveau produit.
- **PostgreSQL exclusivement** : aucun SQLite, EF Core InMemory ou autre fournisseur de persistance, même en tests. Le client Tauri n'accède jamais directement à PostgreSQL.
- Architecture cible : **Tauri 2 + React + TypeScript + Vite + npm**, backend indépendant **ASP.NET Core .NET 10**, PostgreSQL 18 via **EF Core/Npgsql**, Windows 11 x64.
- Client/serveur : postes Tauri → API HTTPS → PostgreSQL côté serveur. Ne pas introduire de backend ni de DB locale par poste. Monoposte = serveur + client sur même PC.
- Langue et identité visuelle : français, vouvoiement, MAD, Inter + Source Sans 3, bleu #0066FF, nuit #0B1D3A, teal #00C2A8. Pas d'emoji ni d'œil/lunettes dans l'UI. Design Claude = source UX DRAFT, pas code de production.
- Développement par petits incréments, preuves réelles, pas de captures/tests fictifs, pas de secrets en Git.
- Aucun merge, push, force push, reset hard, destruction, suppression d'artefacts existants, ni modification d'autres projets.
- Les 153 vues manquantes et 23 compléments restent dans le backlog différé.

## 1. GATE 0 — Preflight court

1. Confirmer que la racine est **le nouveau dépôt MMV V2** (`git rev-parse --show-toplevel`, remote, branche, SHA, `git status --short`). Ne pas accéder à l'ancien dépôt.
2. Lire entièrement `README.md`, `docs/phase/00.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/PLAN_POC_ET_PREUVES.md`, lorsqu'ils existent. Si une pièce manque, mentionne-le ; ne l'invente pas.
3. Inventorier SANS ÉCRASER les fichiers non suivis créés pendant la phase 00 : `.mcp.json`, `.claude/`, `CLAUDE.md` et autres. Vérifier que `CLAUDE.md` ne prétend pas à tort qu'un graphe existe : si nécessaire, proposer ou effectuer seulement une correction ciblée et documentée ; ne jamais effacer le fichier entier sans autorisation.
4. Vérifier Node/npm, SDK .NET, Rust/Cargo MSVC x64, WebView2, SDK Windows, PostgreSQL 18 (port 5433). Sur PostgreSQL, vérification **en lecture seule uniquement** à cette étape.
5. Contrôler Serena et Graphify séparément : CLI, skill, hooks, MCP déclaré, serveur connecté, appel réel. Serena était `Pending approval`, Graphify n'avait pas de graphe. Si une approbation utilisateur `/mcp` est nécessaire, demande-la explicitement. Ne contourne pas les permissions, ne désactive pas les protections globales.
6. Si l'environnement de compilation est effectivement bloqué ou qu'une étape entraînerait un écrasement/une perte de données, **STOP et diagnostic**. Les absences de graphe ou de symboles AVANT la création de code ne bloquent pas l'usage des générateurs officiels ; elles bloqueront les tâches d'analyse/refactoring dépendantes jusqu'à preuve de fonctionnement.

## 2. Git — Branche dédiée

Créer **`feat/phase-01-socle`** à partir de `main` seulement si le Git preflight est compatible. Préserver les fichiers non suivis existants. Si la branche existe, inspecter sans écraser. Aucun commit ni push sur `main`.

Créer/actualiser `docs/phase/01.md` avec : périmètre IN/OUT, POC à réaliser, décisions ouvertes, critères PASS/FAIL, sources et preuves, statut réel. Noter le GO du propriétaire pour 01A ; ne pas déclarer la phase 00 intégralement clôturée ni ses ADR réseau ratifiés.

## 3. Initialiser le client Windows

Dans `apps/desktop/`, utiliser la méthode officielle de création **Tauri 2** avec **React + TypeScript + Vite**, gestionnaire **npm**. Ne pas utiliser Next.js, Electron ni pnpm. Respecter les chemins Windows avec espaces ; ne rien générer dans la racine en écrasant la documentation ou les configs Claude.

- TypeScript strict ; scripts vérifiables dev/build/lint/check et lockfile npm.
- UI initiale **minimale et honnête** : identité MMV V2, indicateur « socle technique », aucune fausse donnée de magasin, aucune fausse connexion API, aucun écran Login/Shell prétendument conforme aux maquettes.
- Rust limité aux fonctions desktop nécessaires ; aucune logique métier en Rust.
- Aucun accès direct à PostgreSQL ni mot de passe DB dans Tauri ou le bundle frontend.
- Définir les identifiants temporaires du package sans les figer comme identité commerciale finale ; tracer les choix.

## 4. Initialiser le backend indépendant

Créer une solution .NET 10 avec une structure pragmatique à partir de zéro :

```text
src/MMV.Domain/
src/MMV.Application/
src/MMV.Infrastructure/
src/MMV.Api/
tests/MMV.Api.Tests/
```

- Les projets compilent et ont des références unidirectionnelles propres (pas de dépendance Domain → Infrastructure/API).
- `MMV.Api` est l'hôte minimal de ce premier POC. La décision `MMV.Server.Host` séparé ou non reste ouverte jusqu'au POC Windows Service : ne crée pas deux hôtes sans justification.
- Ajouter un véritable endpoint de vivacité `GET /health/live` renvoyant un résultat réel de processus ; **ne pas présenter cet endpoint comme preuve de disponibilité de PostgreSQL**.
- Configuration structurée, journaux non sensibles, gestion standard des erreurs. Aucun domaine métier inventé et aucune API fictive clients/ventes.
- Définir comment seront générés les contrats OpenAPI, sans générer prématurément de CRUD fictif.
- Créer des tests automatiques ciblés pour l'endpoint et les références des projets ; exécuter réellement `dotnet build` et `dotnet test`.

## 5. PostgreSQL — Sans toucher aux instances existantes

- Instance observée : PostgreSQL 18 en service sur **5433** ; PostgreSQL 17 sur 5432 appartient potentiellement à d'autres projets.
- **NE PAS** arrêter, réinitialiser, migrer, supprimer, renommer ni modifier une instance ou une base existante. Ne pas modifier `postgresql.conf`, `pg_hba.conf`, les services ou le pare-feu.
- Préparer la dépendance **EF Core 10 + Npgsql compatible**, ses paramètres d'environnement et la structure de persistance ; ne jamais ajouter SQLite ou un provider InMemory.
- **Ne crée pas encore de base, d'utilisateur, de migration ni de données** sans présenter au propriétaire le nom exact de la DB de développement, du rôle, l'instance/port et l'opération envisagée pour obtenir son accord ciblé. Aucune donnée sensible ni mot de passe dans les logs/commits.
- Toute validation de persistance reste `NOT_TESTED` tant qu'un essai sur une base PostgreSQL V2 isolée n'est pas réellement exécuté.

## 6. Tooling Graphify / Serena

- Serena : si approuvé et disponible, appeler un outil MCP de lecture sur les nouveaux fichiers, puis après génération prouver une recherche symbolique TS ou C# réelle. Ne pas annoncer succès sur un simple `claude mcp list`.
- Graphify : conserver le skill existant. Une fois du code créé, construire un graphe **local** sur ce nouveau dépôt uniquement, selon les commandes documentées de la version installée ; ne pas lancer d'extraction sémantique cloud. Interroger réellement une dépendance/chemin utile et fournir la trace ; préparer le MCP `graphifyy[mcp]` seulement si son installation est autorisée et compatible.
- Le fichier `.claude/settings.json` contient peut-être des hooks Graphify installés précédemment. Les inspecter et signaler précisément les effets ; ne pas les supprimer ou changer les permissions globales en silence.
- Si l'un des outils reste indisponible, marquer la validation `BLOCKED`. Ne pas entreprendre de travaux d'analyse/refactoring dépendant de cet outil avant décision du propriétaire. La création initiale via les générateurs officiels peut continuer s'il n'y a pas de risque démontré.

## 7. QA et preuves indispensables

Faire au minimum :

1. `npm install` à l'intérieur du projet frontend créé (pas dans d'autres projets), lint/TypeScript/build réels.
2. `dotnet restore`, `dotnet build`, `dotnet test` réels.
3. Lancer la fenêtre **Tauri native sur Windows** (pas seulement Vite dans un navigateur) et conserver une capture d'écran horodatée et contextualisée si l'outil de capture est disponible ; sinon `NOT_VERIFIED`.
4. Démarrer l'API et interroger réellement `GET /health/live`, conserver la requête/réponse et distinguer HTTP local de HTTPS testé. **Aucune désactivation de validation TLS**. Ne pas configurer un certificat de confiance système sans accord.
5. Vérifier `git diff --check`, l'état Git, les fichiers non suivis, les lockfiles, les secrets et l'absence totale de SQLite.
6. Fournir une `SOURCE_MAP`, une `IMPLEMENTATION_SPEC`, une `QA_MATRIX` et `GATES_AND_DECISIONS` pour ce lot sous `docs/phase/01/` (ou liens clairs depuis `01.md`).
7. Présenter les échecs réels sans les masquer ; ne jamais qualifier « production ready » un POC de socle.

Les POC LAN multi-postes, TLS/pairing, Windows Service, packaging serveur/postes, transactions PostgreSQL, restauration restent **à tester** dans des sous-étapes suivantes ; ne pas les déclarer PASS.

## 8. Limites du mandat et compte rendu

- Ne développe **aucun** module Clients, Ordonnances, Ventes, Commandes, Atelier ou Stock pendant 01A.
- Ne développe pas les 153 vues différées, le design complet Login/Shell, l'installateur final ni les écrans métier.
- Ne crée aucun utilisateur réel, compte client réel, mot de passe en clair, migration sur DB existante ou faux résultat.
- Tu peux effectuer des **commits locaux sur la branche dédiée après les tests** avec des messages précis, si le worktree est inspecté ; ne pousse pas et ne fusionne pas sans approbation ultérieure du propriétaire.

**Rapport final requis :**

1. `CONSTATS VÉRIFIÉS` (SHA, branche, environnement, sources).
2. `FICHIERS CRÉÉS / MODIFIÉS`.
3. `ARCHITECTURE ET DÉPENDANCES`.
4. `VERSIONS ÉPINGLÉES` et commandes de reproduction.
5. `BUILD / TESTS / EXÉCUTIONS` : PASS/FAIL/NOT_TESTED avec extraits utiles.
6. `GRAPHIFY / SERENA` : CLI, MCP, appels réellement réussis, ou blocages.
7. `SÉCURITÉ / SECRETS / POSTGRESQL` : ce qui a été touché, ce qui est resté intact.
8. `POC RESTANTS / OPEN DECISIONS` : installateur, TLS, appairage, sessions, PG isolé.
9. `GIT` : branche, commits locaux éventuels, worktree, aucune PR/merge/push.
10. `GATE PHASE 01A` : `READY_FOR_OWNER_REVIEW` ou `BLOCKED` avec raisons.

**Arrête-toi après la revue de 01A. N'enchaîne pas automatiquement sur 01B ou la phase 02.**

Commence maintenant, en demandant mon approbation uniquement pour une action risquant de modifier des données existantes, d'affecter le système/global, d'exposer des secrets, ou de contourner les permissions.
