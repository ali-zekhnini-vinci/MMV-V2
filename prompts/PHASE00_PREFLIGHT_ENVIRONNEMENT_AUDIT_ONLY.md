# PROMPT — Claude Code — MMV V2 / PHASE 00 : préflight environnement uniquement

Tu es auditeur technique senior et tu réalises uniquement le **préflight de la machine Windows** sur laquelle sera créé le **nouveau** projet MMV V2. Aucun développement ni initialisation ne sont autorisés.

## Sources de vérité

Lis le nouveau `README.md` et les documents `docs/phase/00.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/INSTALLATION_RESEAU_SECURITE.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md` du pack `MMV_V2_PHASE00_CADRAGE_2026-10-15` remis par le propriétaire.

Décisions définitives : **nouveau projet sans ancien dépôt**, PostgreSQL seul SGBD, architecture Serveur MMV / Poste de travail, Tauri + React pour le desktop, .NET côté serveur. 153 vues et 23 compléments de design sont reportés.

## Limites absolues

- **NE JAMAIS** rechercher, ouvrir, cloner, analyser ou consulter l'ancien dépôt MMV, aucun URL d'ancien GitHub, aucune branche ou ancien code, même par simple curiosité.
- Ne créer/modifier ni dépôt Git, branche, commit, code, base, fichier d'application, secret, service Windows, configuration réseau ou `CLAUDE.md`.
- Ne pas installer, désinstaller, mettre à jour de logiciel, plugin, langage, dépendance, Graphify ou Serena. Ne pas exécuter un programme/projet inconnu.
- Ne pas toucher à un PostgreSQL déjà installé, ne jamais effectuer `CREATE DATABASE`, migration, backup ou restauration.
- Ne pas lancer d'application existante ni de tests produisant des écritures, y compris des auto-installers et commandes pouvant télécharger des dépendances.
- Éviter toute commande récursive large qui pourrait examiner des dossiers anciens ou privés. Les chemins autorisés sont exclusivement les répertoires explicitement fournis **du nouveau projet** ou les informations système non sensibles.
- Ne pas exporter de PATH complet, secrets, variables d'environnement, noms d'utilisateurs Windows ou informations identifiantes.
- Toute action qui pourrait écrire ou contacter Internet = `NOT_EXECUTED_REQUIRES_APPROVAL`.

## Préflight permis si lecture seule (sans effets secondaires)

1. OS : édition/version/build et architecture Windows ; droits administrateur disponibles ou non, sans élévation.
2. Outillage déjà installé : versions `dotnet --info`, `node --version`, `npm --version`, `rustc --version`, `cargo --version`, `git --version`, `python --version`, `uv --version`, `claude --version` lorsque les commandes sont disponibles et n'initialisent rien.
3. WebView2 installé (via vue système en lecture seule) ; Visual Studio Build Tools/MSVC présence, sans installer.
4. PostgreSQL : vérifier la présence du produit/service **uniquement par métadonnées système**, sans connexion ni exploration des données ; ne pas considérer absence bloquante si rôle poste.
5. Graphify/Serena : vérifier la découverte CLI et les connexions MCP déclarées, **sans setup ni activation**. Une commande `--help`/`--version` est permis si sûre. Ne pas appeler un outil qui indexe/écrit.
6. Git du nouveau repo uniquement **si son chemin explicite existe**. S'il n'existe pas, rapporter `NOT_CREATED (EXPECTED IN PHASE 00)` ; aucun accès à d'autres projets.
7. Vérifier documentation officielle si l'utilisateur a autorisé un accès Web, sans télécharger ni installer.

## Tableau final obligatoire

| Composant | Disponible oui/non/inconnu | Version vérifiée | Preuve/commande | Action Phase 01 requise |
|---|---|---|---|---|

Ajoute une section `TESTS_NOT_EXECUTED`, une section `SECURITY_FINDINGS`, une section `BLOCKERS`, et une section `DECISIONS_OWNER_REQUIRED`.

Toutes preuves sont liées à des commandes **réellement exécutées** ; une commande proposée n'est pas une preuve. Ne jamais revendiquer Graphify/Serena « utilisés » sans appel réellement conclu ; `version-only` n'est pas une preuve de query graphe ou symbole.

Aucune modification et aucun test de faisabilité applicative autorisés. Conclus par `PHASE00_PREFLIGHT_ONLY — OWNER_REVIEW_REQUIRED`.
