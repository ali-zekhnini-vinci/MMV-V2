# MMV V2 — PHASE 01B — INTÉGRATION POSTGRESQL / API HTTPS / TAURI

**Mandat propriétaire :** GO de la phase 01B après merge manuel déclaré `4ed20a8` de la phase 01A. Ce GO autorise le développement et les tests bornés de 01B, **pas** des changements administratifs PostgreSQL ou une nouvelle racine de confiance TLS sans confirmation explicite sur leur cible et leurs conséquences. Travaille sur le dépôt **MMV V2 neuf exclusivement**. Ne consulte jamais l'ancien logiciel, dépôt, historique, classes ou données MMV.

## OBJECTIF NON NÉGOCIABLE

Au terme de 01B, montrer, sur **une véritable fenêtre Tauri Windows**, le résultat réel d'une requête **HTTPS vérifiée** vers `MMV.Api`, et la disponibilité de la **nouvelle base PostgreSQL 18** issue de l'endpoint `GET /health/ready`. Fournir des tests sur une base de test PostgreSQL séparée. Aucun faux backend, aucune réponse mock présentée comme réelle, aucun provider SQLite/EF InMemory, aucune fonctionnalité métier, aucun serveur LAN/installation de production prétendus validés.

## GATE 0 — PRÉFLIGHT, STOP SI INCOHÉRENCE

1. Vérifie `git rev-parse --show-toplevel`, `git remote -v`, `git status --short`, `git log -5 --oneline`, `git branch -vv`, `git show -s --format=%H main`, `git merge-base --is-ancestor 79aa226 main` (ou équivalent robuste). Confirme le merge `4ed20a8` ou son équivalent réellement présent. Vérifie l'absence de divergences non expliquées. Ne remplace ni ne supprime `.claude/` ou `.serena/` non suivis. N'exécute pas `reset --hard`, `clean -fd`, `push --force`, `git add -A`.
2. Lis entièrement `README.md`, `CLAUDE.md`, `docs/phase/00.md`, `docs/phase/01.md`, tous les fichiers `docs/phase/01/*`, `docs/ADR_ET_DECISIONS.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/INSTALLATION_RESEAU_SECURITE.md`, `docs/PLAN_POC_ET_PREUVES.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `.mcp.json`, et les sources du socle (liste dans SOURCE_MAP).
3. Corrige **dès maintenant**, avec entrée datée et non réécriture du passé, les statuts devenus obsolètes : `README.md` et `docs/phase/01.md` mentionnent encore 01A non mergée. Note `main` au SHA effectivement observé, 01A MERGED, 01B IN_PROGRESS. Ne déclare pas les ADR réseau déjà approuvés.
4. Crée la branche `feat/phase-01b-integration` **depuis main** après vérification du worktree et des fichiers non suivis. Si la branche existe, vérifier sa provenance avant toute action. Aucune écriture sur main, aucun merge ni push sans nouveau GO.
5. Vérifie Windows, PG18 `localhost:5433`, PG17 `5432`, .NET SDK épinglé, Rust, Node/npm, compatibilité des packages et état Serena/Graphify. Vérifie `dotnet dev-certs https --check --trust` **sans installer de certificat** pendant le préflight.
6. Serena : fais au moins une vraie navigation symbolique C# avant de modifier le backend. Lorsque le nouveau client TS existe, teste une vraie navigation de symbole/référence TypeScript, ou signale exactement le problème de language server. Graphify : fais une requête/path utile sur le graphe local existant si disponible, régénère après changements avec CLI/AST locale uniquement (pas LLM/cloud). MCP Graphify = NOT_CONFIGURED tant qu'il ne l'est pas ; ne mens pas. Si un outil requis est inaccessible, STOP pour l'opération correspondante et expose la cause.
7. Établis avant codage `docs/phase/01b/` avec `SOURCE_MAP.md`, `IMPLEMENTATION_SPEC.md`, `QA_MATRIX.md`, `GATES_AND_DECISIONS.md`, plus un statut 01B dans `docs/phase/01.md`. Documente IN/OUT, provenance et critères exacts.

## ÉTAPE 1 — POSTGRESQL 18 : ISOLATION ET AUTORISATION CIBLÉE

- **Cibles proposées, non présumées existantes :** instance PostgreSQL 18 port **5433**, nouvelles bases `mmv_v2_dev` et `mmv_v2_test`, nouveaux rôles restreints `mmv_v2_dev` et `mmv_v2_test` (ou séparation application/migrations plus stricte motivée et approuvée). Ne jamais utiliser PG17/5432, une base de production, ni une base préexistante d'un autre projet.
- Avant toute écriture administrative, lecture seule : version réelle, `pg_isready`, service, processus, version/port via `SHOW`, noms existants via catalogues PostgreSQL en évitant toute extraction de données, `current_user`; contrôler l'identité de l'instance. Produire le plan SQL exact et le périmètre des changements. **DEMANDER UNE CONFIRMATION EXPLICITE DU PROPRIÉTAIRE** pour `CREATE ROLE`, `CREATE DATABASE`, `GRANT`, migrations et écriture de tests ciblant ces bases. S'il refuse ou ne répond pas, arrêter ce volet `BLOCKED_OWNER_APPROVAL`, poursuivre seulement les activités sans données et ne pas annoncer 01B terminée.
- Ne jamais modifier/supprimer PG17, `postgresql.conf`, `pg_hba.conf`, pare-feu ou rôle `postgres` ; pas de `DROP DATABASE`, `DROP ROLE`, `ALTER SYSTEM` ni changement de port. Si l'objet V2 ciblé existe déjà, inspecter sa propriété et STOP avant toute modification.
- Privilèges minimum : pas de SUPERUSER, CREATEDB, CREATEROLE, BYPASSRLS ni accès à des bases étrangères. Distingue rôles d'exécution/migration si pertinent pour un futur produit ; si simplicité POC retenue, limite explicitement le choix à la phase de développement. L'API utilisera localhost/5433, jamais un accès réseau direct depuis Tauri.
- Aucun mot de passe ou chaîne dans Git, `appsettings*`, fichiers de preuve, captures ou logs. Choisir user-secrets pour le serveur en développement et une configuration séparée pour les tests. Aucune valeur sensible dans `VITE_*`, ni dans le client distribué. Ne montre jamais de secrets dans la sortie.
- Avant migration, décrire quel schéma technique est justifié. `MmvDbContext` est actuellement vide : **ne pas fabriquer un modèle métier ou un produit factice** pour le POC. Un schéma/table de bootstrap purement technique et une migration EF Core initiale sont possibles UNIQUEMENT si leur finalité, caractère réutilisable et impact sur la base sont documentés et approuvés. Ne pas créer de table métier fictive. Un test CRUD éphémère dans `mmv_v2_test` peut être réalisé avec modèle technique isolé et rollback, sans laisser de données de démonstration persistantes.
- Générer/revoir SQL de migration avant de l'appliquer, puis migrer uniquement la nouvelle base approuvée. Ne jamais utiliser `EnsureCreated()` comme substitut des migrations ; ne pas déclencher de migrations automatiquement au démarrage de l'API. Réaliser preuves EF Core/Npgsql, migrations et vrai aller-retour lecture/écriture transactionnelle (rollback), distinguer les tests sur `dev` et `test`.

## ÉTAPE 2 — HEALTH REAL ET API

- Garder `GET /health/live` indépendant de PostgreSQL : il vérifie uniquement le processus et reste disponible même si PostgreSQL tombe.
- Créer `GET /health/ready` branché sur une **vraie** connexion PG18 via Npgsql/EF Core, avec timeout borné, annulation et résultat HTTP 200 si disponible, HTTP 503 si la DB n'est pas joignable. Seule la disponibilité de DB est prouvée, pas automatiquement l'état de migration si non vérifié. Aucun message d'erreur interne, nom de DB, mot de passe, stack trace, chaîne de connexion ou IP privée dans la réponse au client.
- Éviter des requêtes agressives à répétition ; utiliser un health check adapté (voir documentation Microsoft), pas d'ajout de dépendances non justifié. Conserver OpenAPI cohérent, avec contrat de réponse typé pour la UI ; ne pas exposer une API métier.
- Tests : base disponible et indisponible, liveness non couplé, aucun faux 200 sur erreur PostgreSQL, timeout, annulation. Un test de disponibilité local ne certifie pas le futur réseau LAN.

## ÉTAPE 3 — TLS HTTPS LOCAL (PAS TLS LAN)

- Utiliser uniquement l'API de développement sur `https://localhost:7267` (à vérifier contre le profil réel), bind sur loopback. Vérifier le certificat, son SAN localhost et le retour de `dotnet dev-certs https --check --trust`. Si non approuvé, **demander confirmation explicite** avant `dotnet dev-certs https --trust` (modification du magasin de certificats utilisateur). Ne pas nettoyer/remplacer globalement les autres certificats.
- Après approbation : requête HTTPS réussie avec validation normale de la chaîne et du nom ; tests négatifs d'untrust/mismatch si réalisables dans un environnement isolé, sans altérer le magasin global. Ne pas utiliser `curl -k`, `--insecure`, `DangerousAcceptAnyServerCertificateValidator`, `NODE_TLS_REJECT_UNAUTHORIZED=0`, `strict-ssl=false` ou équivalent. Aucun fallback silencieux en HTTP.
- `ADR-006` TLS LAN (CA privée vs domaine/certificat public) reste OPEN ; le certificat `dotnet dev-certs` n'est PAS un certificat déployable sur les postes en production.

## ÉTAPE 4 — RÉEL TAURI ↔ API

- Implémenter dans `apps/desktop/src/` un client API minimal, isolé et typé (format d'erreur borné, `AbortController`, timeout raisonnable, erreurs réseau explicites). Pas de fake API, pas de `mock`, pas d'auth simulée. L'écran « socle technique » reste temporaire et montre : chargement, API inaccessible, API joignable + PG indisponible, API + PG disponibles, et une action de rechargement. Distinction exacte entre `live` et `ready`.
- Paramétrer une origine d'API **dev uniquement** (ex. `VITE_MMV_API_BASE_URL=https://localhost:7267` hors secret), sans intégrer définitivement localhost à la version commerciale. Prévoir le futur choix DNS/IP sécurisé sans l'implémenter encore. Aucun accès direct à PostgreSQL dans le client.
- Configurer la **CSP Tauri** `connect-src` avec l'origine exacte HTTPS nécessaire pour le POC ; conserver les autres directives restrictives. Préférer un override de développement si l'origine localhost ne doit pas être embarquée en production. Aucun `connect-src *`, `https:`, ni désactivation CSP.
- CORS .NET : relever l'Origin **réel** envoyé par WebView2 (Vite dev et bundle Tauri peuvent être différents), autoriser explicitement uniquement les origines de test identifiées, méthodes et headers strictement nécessaires. Ne pas faire `AllowAnyOrigin`, `AllowCredentials` global, wildcard ni `SetIsOriginAllowed(_ => true)` ; vérifier préflight positif et origine interdite.
- Exécuter et capturer un **vrai** test depuis la fenêtre native Tauri Windows avec le backend HTTPS réel et PG18. Prouver la provenance : process, ports, log HTTP minimal non sensible, capture réelle (visuellement examinée), tests négatifs quand serveur ou DB indisponible. Si tests négatifs demandent arrêt PG18, prévoir fenêtre et autorisation ; privilégier tests isolés sans interrompre le service partagé.
- Ne pas commencer Login/Shell, clients, ordonnances, commandes, caisse, RBAC, installation ou Windows Service.

## ÉTAPE 5 — TESTS, OUTILLAGE, DOCUMENTATION GATE

- Tests backend existants non régressifs et nouveaux tests pertinents ; tests PostgreSQL **réels** sur `mmv_v2_test` uniquement. Tests PG non exécutés = `NOT_TESTED`, jamais PASS. Utiliser une variable/test fixture explicitement ciblée, vérifier dbname/version/port dans le setup et refuser une mauvaise cible ; ne jamais utiliser une base inconnue. Si Docker/Testcontainers envisagés, approbation ADR avant ajout : PostgreSQL seulement, jamais SQLite.
- Tests frontend de l'API/status si framework de test justifié et version verrouillée (Vitest recommandé) ; tests de validation d'affichage et d'erreur, pas de mock présenté comme preuve E2E. Build TypeScript, lint, npm audit ; `dotnet restore --locked-mode`, build, test. Build Tauri Windows et screenshot + inspection visuelle réels.
- Faire une revue minimale des vulnérabilités connues et des licences ajoutées. Pin exact ou lockfiles, justifier chaque nouveau package dans spec/ADR ; ne pas modifier la stack arbitrée.
- Mettre à jour Graphify local (`graphify update .`) et montrer une requête utile du nouveau flux ; Serena : recherche réelle sur C# et TypeScript si les langages sont disponibles. Enregistrer CLI/MCP distinctement. Ne pas modifier la config globale Claude pour contourner des refus. L'extra Graphify MCP doit faire l'objet d'un choix explicite si non essentiel à cette étape.

### DOCUMENTATION GATE — OBLIGATOIRE ET BLOQUANT

Mettre la documentation à jour **dans les mêmes changements/commits que le code**, pas en fin de projet :
- `README.md` : 01A effectivement MERGED (SHA vérifié), 01B IN_PROGRESS puis `READY_FOR_OWNER_REVIEW` seulement après tests ; commandes exactes ; limites clairement listées.
- `docs/phase/00.md`, `docs/phase/01.md` : ajouter des **amendements datés** sans effacer les GO/NO-GO historiques. Dans `01.md`, distinguer 01A/01B.
- `docs/phase/01b/SOURCE_MAP.md` : traçabilité des exigences/contrats jusqu'aux chemins réels et aux outils utilisés.
- `docs/phase/01b/IMPLEMENTATION_SPEC.md` : architecture testée, endpoints, configuration, modèle PG, migrations, CSP/CORS, versions, commandes reproductibles.
- `docs/phase/01b/QA_MATRIX.md` : chaque test avec commande, machine/conditions, sortie, PASS/FAIL/BLOCKED/NOT_TESTED/NOT_RERUN, captures et hash.
- `docs/phase/01b/GATES_AND_DECISIONS.md` : autorisations explicites SQL/certificats, ADR ouverts, tests P01-04/05/07/15/16 réellement couverts ou reportés ; ne pas valider POC LAN/Service/Packaging non exécutés.
- `docs/ADR_ET_DECISIONS.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/INSTALLATION_RESEAU_SECURITE.md`, `docs/PLAN_POC_ET_PREUVES.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `CLAUDE.md` et backlog : vérifier les écarts et modifier **seulement** ceux concernés, en expliquant pourquoi. Ne jamais transformer proposition en APPROVED sans décision du propriétaire.
- Ajouter le présent prompt dans `prompts/` et le référencer dans SOURCE_MAP.
- Vérifier les liens internes, les chemins, états Git, dates, le statut HTTPS vs TLS LAN, Graphify CLI vs MCP, Serena C# vs TypeScript, et test exécuté vs non exécuté. Si doc incohérente : `DOCUMENTATION_BLOCKED` et pas `READY_FOR_OWNER_REVIEW`.

## LIVRAISON ET GIT

- Commits locaux cohérents possibles sur la branche dédiée, sans commit de secrets, caches, logs, `target/`, `node_modules/`, `.serena/` runtime ni artefacts `graphify-out/` ; relire tous les fichiers indexés avant commit. Pas de PR, push ou merge sans nouveau GO du propriétaire. Ne fais aucun changement global de la machine non explicitement accepté.
- Rapporte un tableau `ID | action | preuve | statut`, SHA commits locaux, inventaire des fichiers, tests PASS/FAIL et limitations, screenshot réel, décision SQL/TLS accordée ou bloquée, reste à faire. Ne prétends jamais « production-ready ».
- Stop en fin de 01B, n'engage ni phase 02 ni fonctionnalités métier. Terminer par `PHASE01B_READY_FOR_OWNER_REVIEW` seulement si toutes les portes 01B approuvées et exécutées ; sinon `PHASE01B_PARTIAL — BLOCKED_OWNER_DECISION` avec raisons précises.

**COMMENCE MAINTENANT par le GATE 0. Avant toute écriture PostgreSQL ou modification du magasin de certificats, affiche le plan et demande mon approbation ciblée.**
