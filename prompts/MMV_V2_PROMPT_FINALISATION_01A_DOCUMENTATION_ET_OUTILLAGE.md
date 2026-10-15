# MMV V2 — Phase 01A — Finalisation documentaire et outillage (avant commit)

Tu es responsable technique et auditeur de livraison du **nouveau** projet MMV V2.

## Contexte et décisions du propriétaire

- La Phase 01A a été implémentée sur `feat/phase-01-socle`, quatre commits publiés jusqu'à `f111dc09fc28061e5cbf6000e9ae1a5995ab0943`.
- Le rapport de correction le plus récent indique que `README.md`, `docs/phase/00.md`, `docs/phase/01.md` ont des modifications **non commitées**. Préserve-les : ne les écrase pas.
- **Serena est fonctionnel pour l'analyse symbolique C#**, sur la base des vrais appels réussis `mcp__serena__find_symbol("MmvDbContext")` et `mcp__serena__get_symbols_overview("src/MMV.Infrastructure/DependencyInjection.cs")`. Les outils `get_current_config` et `list_dir` ne sont pas exposés dans le contexte `claude-code` : **n'essaie pas de modifier Serena uniquement pour obtenir ces deux outils**. Les capacités TypeScript restent à vérifier ultérieurement.
- **Graphify CLI/skill et graphe AST local sont utilisables** : un chemin de dépendances concret a été obtenu. **Graphify MCP n'est pas configuré** et ne doit pas être déclaré PASS. Pas d'extraction LLM/cloud.
- L'ancien dépôt MMV, son code et ses règles restent totalement interdits.
- PostgreSQL 18 est seul SGBD V2 ; ne touche ni PG17 ni PG18 dans cette mission.
- Les écrans complémentaires restent dans le backlog ; pas de conception supplémentaire ni de code métier.

## Préflight

1. Vérifie dépôt, branche, HEAD, remote, `git status --short`, fichiers suivis/non suivis et diff avant modification.
2. Lis l'intégralité des documents de cadrage applicables, les documents 01A, les fichiers locaux `CLAUDE.md`, `.claude/`, `.mcp.json`, `.serena/` **sans modifier de réglage global**, et les références exactes citées.
3. Lis et préserve les trois diffs documentaires existants. N'entreprends aucune opération Git destructive, aucun reset, merge, push ou checkout.

## Corrections obligatoires — documentation

1. Termine l'actualisation de `README.md` : Phase 01A implémentée, audit et merge toujours en attente ; 01B non démarrée ; aucun fonctionnement PostgreSQL réel ou multi-postes revendiqué.
2. Dans `docs/phase/00.md`, garde l'historique initial NO-GO et ajoute une note datée expliquant le GO limité à 01A, sans ratifier les ADR non approuvés.
3. Dans `docs/phase/01.md`, conserve l'IN/OUT originel ; explique séparément l'autorisation ultérieure de push et l'état réel. Actualise le statut Serena **PASS pour recherche symbolique C# seulement**, sous réserve des preuves exactes fournies dans le rapport. La revue du propriétaire et le merge restent PENDING.
4. Dans `docs/phase/01/QA_MATRIX.md`, conserve Q-22 comme résultat historique si utile, puis ajoute une nouvelle entrée datée prouvant les appels Serena réellement effectués (noms, cible, résultat et limites). Ne prétends pas les avoir réexécutés si ce n'est pas le cas. Mentionne Graphify CLI PASS, Graphify MCP NOT_CONFIGURED. Les builds/test Windows antérieurs restent « PASS selon exécution rapportée », et les builds non rejoués `NOT_RERUN`.
5. Dans `docs/phase/01/GATES_AND_DECISIONS.md`, réévalue P01-16 en `PASS (navigation symbolique C#)` en citant les vrais appels, et remplace O-02 « approval en attente » par le constat de connexion. Conserve les points réseau, TLS, base dédiée et MCP Graphify comme ouverts. Note la validation TypeScript comme future.
6. Dans `docs/phase/01/SOURCE_MAP.md`, corrige les références et inclus le prompt `prompts/MMV_V2_PHASE01A_PROMPT_CLAUDE_CODE.md` s'il est revu puis ajouté au suivi. Vérifie que tous les chemins documentés existent réellement.
7. Dans `docs/phase/01/IMPLEMENTATION_SPEC.md`, change uniquement les éléments devenus factuellement faux ; ne fabrique aucun état de conformité.
8. Vérifie `Q-13` et `launchSettings.json` avant de modifier quoi que ce soit : le profil `https` peut exposer **simultanément** `https://localhost:7267` et `http://localhost:5022`. Si c'est le cas, `dotnet run --launch-profile https` suivi d'un appel HTTP sur 5022 est cohérent, **pas un bug**. Corrige uniquement un libellé réellement trompeur.
9. Introduis dans **`CLAUDE.md` à la racine** une section **DOCUMENTATION GATE** permanente : avant chaque livraison, lire docs concernés, synchroniser README, phase NN, SOURCE_MAP, IMPLEMENTATION_SPEC, QA_MATRIX, GATES_AND_DECISIONS, ADR/backlog selon impact ; ne jamais déclarer READY_FOR_OWNER_REVIEW avec des statuts obsolètes ; ne pas falsifier les historiques, les PASS ou les GO. Retire les renvois à un wiki Graphify absent et toute obligation absolue de `graphify query` non approuvée. Ne prétends pas qu'un graphe cloud ou un MCP Graphify existe.

## Configuration agent et fichiers non suivis

- `.mcp.json` : examiner la commande Serena, la portabilité, l'absence de secrets et de chemins privés. Proposer son versionnement uniquement si vérifié ; ne pas ajouter de permission automatique.
- `.claude/settings.json` : il ne contient, selon le rapport, que deux hooks Graphify activés sans approbation. Confirme-le en lisant le contenu exact. Si c'est exact, **désactive ces hooks en retirant seulement cette configuration projet**, sans toucher à la configuration Claude globale ou à d'autres hooks. Si le fichier contient autre chose, STOP et demande de décision.
- `.claude/CLAUDE.md` : doublon des règles globales, ne pas le versionner tel quel. Ne le supprimer qu'après vérification exacte et explicite de son contenu ; sinon le laisser local et l'indiquer.
- `.claude/skills/graphify/**` : préserver localement ; ne pas versionner le code tiers sans examen de sa licence et d'une stratégie de maintenance.
- `.serena/project.yml` : configuration locale C# à examiner et à documenter ; ne pas la versionner avant contrôle des chemins locaux et décision sur TypeScript.
- `prompts/MMV_V2_PHASE01A_PROMPT_CLAUDE_CODE.md` : vérifier son contenu et l'absence de secrets ; **autorisation de préparer son suivi Git**, car il est cité par SOURCE_MAP.
- Mettre à jour `.gitignore` **uniquement si nécessaire** pour exclure les caches/logs/paramètres locaux ; ne pas ignorer des configurations partagées déjà destinées à être versionnées.
- Ne pas créer de fichiers de secrets, de migration, de base, de serveur Windows, d'installateur ou de nouvelle fonctionnalité.

## DOCUMENTATION GATE — obligatoire

Avant de déclarer la tâche terminée :

1. Produis une matrice `document -> fait actuel -> mise à jour -> preuve` couvrant README, phase 00, phase 01, QA, gates, SOURCE_MAP, IMPLEMENTATION_SPEC et CLAUDE.md.
2. Contrôle **toutes** les références de chemins internes citées et les statuts incohérents (Serena BLOCKED vs PASS, 01A/01B, HTTP/HTTPS, Graphify CLI/MCP, POSTGRESQL NOT_TESTED).
3. `git diff --check`, `git status --short`, `git diff --stat`, inventaire des nouveaux fichiers à suivre, contrôle des secrets. Fournis le diff complet des documents modifiés ou un patch à partager avec le propriétaire (hors Git si non approuvé).
4. Si aucun code applicatif ni configuration de build n'a changé, ne relance pas inutilement le build natif Tauri. Rejoue seulement les vérifications raisonnables et précise `NOT_RERUN` pour les autres.
5. Aucun fichier ne doit être présenté comme « approuvé » ou « mergé » sans décision correspondante.

## Arrêt et livraison

- Aucun commit, push, PR, merge ou début de 01B.
- Fournis le tableau des décisions closes/ouvertes, les tests effectivement exécutés, tous les fichiers modifiés, et un verdict précis sur la possibilité de commiter ces corrections.
- Si une vérification nécessaire est impossible, déclare `BLOCKED` avec cause précise, plutôt que d'inventer une preuve.

Termine par : `PHASE01A_DOC_GATE_READY — OWNER_REVIEW_REQUIRED`.
