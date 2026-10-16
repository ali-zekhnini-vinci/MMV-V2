# MMV V2 — PHASE 02 — GO DE DÉVELOPPEMENT : LOGIN, SHELL, IDENTITÉ MINIMALE

Tu es le responsable d'implémentation senior de MMV V2. Le propriétaire a confirmé le merge manuel de la Phase 01B. **La Phase 02 peut commencer**, dans le périmètre de ce mandat seulement. Il souhaite avancer vers un vrai logiciel, avec une seule revue consolidée par lot, sans multiplication de micro-autorisations.

## 0 — Préflight court, obligatoire

1. Travaille dans le dépôt **neuf** MMV-V2 ; vérifie `git fetch origin`, le SHA réel de `origin/main`, et que les commits de 01B y sont ancêtres. N'invente pas le SHA de merge 01B : le propriétaire ne l'a pas fourni dans ce mandat. Ne rebase/force-push/reset aucun travail non relu ; conserve `.claude/` et `.serena/` locaux. Si `main` local est ancien, synchronise-le proprement uniquement après contrôle de l'absence de modifications locales, puis crée `feat/phase-02-auth-shell` **depuis `origin/main` fusionné**. Si divergence ou changement inattendu : STOP factuel.
2. Consulte les documents **actuels du dépôt**, notamment `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/ADR_ET_DECISIONS.md`, `docs/INSTALLATION_RESEAU_SECURITE.md`, `docs/PLAN_POC_ET_PREUVES.md`, `CLAUDE.md`, README, phase 01/01B, ainsi que les références visuelles de Phase 02 fournies dans `references/phase02/` (ou leur emplacement constaté). Ne consulte ou n'importe **JAMAIS** l'ancien dépôt/ancien code MMV.
3. Crée `docs/phase/02.md` et `docs/phase/02/{IMPLEMENTATION_SPEC,SOURCE_MAP,QA_MATRIX,GATES_AND_DECISIONS}.md` (en conservant la nomenclature locale existante). Note le merge 01B comme **fait rapporté puis vérifié par Git**. Le statut commence à `IN_PROGRESS`, non `APPROVED`/`MERGED`.
4. Les références Login/Shell sont **visuelles et DRAFT** : exploite les screenshots, assets et tokens, mais **ignore intégralement les recommandations historiques Avalonia/.NET 8/AXAML ou règles métier** présentes dans certains handoffs. Les HTML `.dc.html` ne sont pas du code applicatif à reprendre. Si les références sont absentes, bloque seulement la reproduction visuelle exacte et précise quels fichiers manquent ; poursuis les travaux techniques indépendants.
5. PostgreSQL **uniquement** (Npgsql/EF Core) ; jamais SQLite, EF InMemory comme substitut de persistance, ni connexion directe du client à PG. Ne touche pas à PG17/5432, aux autres bases, aux rôles `postgres` ni aux certificats système.

## 1 — Portée officielle de Phase 02

**IN — objectif utilisateur visible :** une personne autorisée lance le client Tauri Windows, atteint le véritable écran de connexion, s'authentifie auprès de MMV.Api sur HTTPS local, puis accède à un **Shell** fonctionnel avec session et menu cohérents, thème clair/sombre, état du serveur réel et déconnexion. Un premier administrateur peut être créé de manière contrôlée, côté serveur. L'API refuse systématiquement les utilisateurs non authentifiés ou insuffisamment autorisés.

**OUT :** modules Clients/Rendez-vous (Phase 03), ordonnances, ventes, catalogue, stock, données clients réelles, dashboards alimentés par fausses statistiques, gestion des postes réels, pairing LAN, TLS de production, Windows Service, installer Serveur/Poste, backup d'exploitation, facturation et migrations étrangères. Leur absence ne bloque pas la Phase 02 **locale** ; elle empêche toute affirmation de prêt pour déploiement/multiposte.

## 2 — UI : références visuelles, pas de design inventé

Source : `references/phase02/sources/claude-design/MMV-Login-Handoff/`, `MMV-Shell-Handoff/`, `_ds/**/tokens/`, `MMV-Lot1.dc.html`, avec README du pack. Le pack est fourni exclusivement comme référence du propriétaire, à mettre hors code runtime. Si l'emplacement diffère, le découvrir plutôt que le supposer.

- Implémente en **React + TypeScript**, composants cohérents avec la stack existante, styles via design tokens. Réutilise les vrais logos/assets fournis ; ne les redessine pas. Aucune police distante ou ressource CDN sans validation ; n'embarque pas de font non autorisée.
- **Login** conforme à la capture de référence : deux panneaux, formulaire identifiant/mot de passe, visibilité facultative du mot de passe, états vide/validation/chargement/identifiants invalides/API inaccessible/session expirée, navigation clavier, vrais labels/ARIA, absence d'inscription publique et de lien de réinitialisation inventé.
- **Shell** : sidebar **260 px ouverte / 72 px réduite**, topbar **64 px**, barre d'état **32 px**, thème clair/sombre, navigation active, focus visible, profil/déconnexion, états « serveur inaccessible », « rechargement », « non autorisé ». Préserve les séquences clavier et la lisibilité à 1440×900 et 1920×1080, zoom 125 % / 150 %, sans scrolling horizontal global.
- La **barre d'état** doit lire la vraie santé/API, pas des voyants simulés. Pour les entrées de navigation vers modules absents, indique clairement « Disponible dans une phase suivante » ou désactive-les : **ne crée pas de faux CRUD ni de données métiers**.
- Les 5 familles de dashboards du Lot 1 sont des **références de navigation DRAFT**, non des permissions serveur approuvées. À ce stade, un contenu d'accueil sobre sans chiffre inventé suffit ; noter les cinq variantes riches en backlog si elles dépassent le périmètre validé.
- Préserver le diagnostic technique de 01B dans une section identifiable, ne pas casser `/health/live` ou `/health/ready`.
- Une capture réelle Tauri et comparaison annotée aux captures sources sont obligatoires. Pas de capture navigateur présentée comme native.

## 3 — Authentification/API : socle vrai mais borné

1. Adopter les briques éprouvées d'**ASP.NET Core Identity / PasswordHasher** et des **policies côté serveur**, architecture `Domain / Application / Infrastructure / API` respectée. Éviter toute crypto maison. Migrations EF Core **explicites**, révisables et versionnées ; **jamais** `EnsureCreated()` ni migration au démarrage.
2. Créer uniquement les entités **techniques d'identité/session/permissions indispensables**. Ne pas modéliser clients, prescriptions, ventes, magasins multiples ou permissions métier spéculatives.
3. Prévoir un **bootstrap administrateur initial, une seule fois, via une procédure locale côté serveur** (CLI/commande de maintenance dédiée avec contrôle de préconditions), **jamais** un endpoint public anonyme « create first admin », jamais un compte administrateur ou mot de passe codé en dur, jamais de données d'identité réelles dans Git. Prévoir une manière reproductible de vérifier qu'un second bootstrap est refusé. L'exécution réelle avec identité et mot de passe du propriétaire nécessite une action/validation ciblée.
4. Utilisateur non connecté : `401`. Authentifié mais non autorisé : `403`. Comptes invalides/désactivés et erreurs login : réponses neutres sans révéler l'existence d'un compte ; protections contre tentatives répétées ; mot de passe haché via Identity. Les routes métier futures ne doivent pas être exposées.
5. Définir une **matrice minimale de permissions techniques** : `SystemAdmin` (administration technique seulement) et accès à l'espace authentifié. Les cinq profils visuels ne donnent **aucun droit métier par défaut**. Ne pas inventer une matrice de droits client, caisse, ordonnance, stock. Cas de test au minimum : anonyme/refus, admin/autorisé, utilisateur non-admin/refus, désactivation, déconnexion et révocation.
6. **ADR-007 (sessions) reste PROPOSED.** Pour ce premier POC local, tu peux proposer une session révocable côté serveur **avec secret opaque et de durée bornée, conservé en mémoire volatile du client uniquement**, sans persistance entre redémarrages et sans refresh longue durée. Compare brièvement avec cookies HttpOnly + anti-CSRF et coffre OS ; explicite clairement avantages/risques et marque ce choix **provisoire dev**, sans prétendre avoir approuvé une session distribuable. Pas de JWT ou refresh stocké dans `localStorage`, `sessionStorage`, logs, fichiers ou front bundle. Si une variante techniquement plus sûre/sobre s'impose, la justifier avant de la figer dans une migration.
7. Les requêtes authentifiées doivent utiliser **HTTPS avec validation normale du certificat**. CSP/CORS restreints, origines exactes, aucun `allow-any-origin` avec credentials. Test réel depuis WebView2. Ne pas modifier le magasin de certificats Windows ni la sécurité LAN.
8. Vérifier les identités des bases/port **5433** et la version PG18 avant les opérations d'écriture. Tests automatisés d'intégration **uniquement sur `mmv_v2_test`**, sans comptes personnels et avec isolation/nettoyage contrôlé. Aucune écriture de test sur `mmv_v2_dev`.

### Borne d'autorisation sur les migrations

**GO pour développer**, générer les migrations d'identité et tester le code sur des doubles/fixtures non persistantes si nécessaires. **Ne pas appliquer une nouvelle migration d'identité à `mmv_v2_dev` ou `mmv_v2_test`, et ne pas exécuter le bootstrap admin réel, sans présenter UNE seule fois un dossier de décision** : scripts SQL complets, impacts/tables, vérification de cible, approche de test, reprise en cas d'échec. L'utilisateur autorisera alors en une réponse l'application aux deux bases et le bootstrap local. Après ce GO, exécute le reste sans nouvelles micro-demandes sauf anomalie sérieuse. Conserve toute migration préexistante sans l'altérer.

## 4 — Implémentation, tests et qualité

Découpe **interne** pour des commits lisibles :
- **02-A** : Design tokens, Login/Shell visuels, états, routing/garde UI, tests visuels et clavier ; déconnectés des droits backend tant que pas d'API d'identité.
- **02-B** : identité API/EF Core, bootstrap local contrôlé, authentification et autorisation minimales ; migrations soumises au gate ciblé ci-dessus.
- **02-C** : intégration réelle Tauri/API/PG, sessions, tests 401/403/révocation, quatre scénarios de résilience, captures natives, audit final.

Ne considère jamais qu'une garde React remplace les permissions côté API. Préserver les fonctionnalités et tests de 01B. Exécuter `dotnet restore/build/test`, tests PostgreSQL autorisés après GO, `npm ci`, TypeScript, lint, Vitest, build frontend, vrai runtime Tauri, test native login/logout et captures. Les tests API/auth doivent démontrer login invalide, session valide, expiration/révocation, et accès interdit. Les tests doivent couvrir le bon comportement à un redémarrage du client (aucune session persistée si variante volatile).

Utiliser réellement **Serena C# et TypeScript**, et **Graphify CLI** pour analyser au moins une dépendance du nouveau flux ; enregistrer l'usage concret. Si MCP Serena a besoin d'être reconnecté, ne pas bloquer les travaux qui ne dépendent pas de lui. Graphify MCP reste optionnel.

## 5 — DOCUMENTATION GATE permanent

Dans **les mêmes commits que le travail**, mettre à jour sans falsifier l'historique : `README.md`, `CLAUDE.md` si concerné, `docs/phase/01.md` et `docs/phase/01b/*` uniquement si le statut de merge ou une référence l'exige, `docs/phase/02.md`, `docs/phase/02/{SOURCE_MAP,IMPLEMENTATION_SPEC,QA_MATRIX,GATES_AND_DECISIONS}.md`, architecture/ADR/backlog si concerné. Vérifier références et preuves. Les décisions de sécurité restent `PROPOSED`/`OPEN` tant que le propriétaire n'a pas ratifié. Distinguer `PASS`, `FAIL`, `BLOCKED`, `NOT_TESTED`, `NOT_RERUN`. La fusion de 01B est un fait à tracer avec le **vrai SHA contrôlé**, pas un SHA supposé.

## 6 — Politique Git & livraison

- Uniquement branche `feat/phase-02-auth-shell` ; petits commits **locaux** nommés, historique clair.
- **Aucun push, aucune PR, aucun merge ; l'utilisateur s'en charge manuellement après ma revue externe.** Aucun travail Phase 03.
- Ne pas versionner secrets, certificats, caches, builds, `.claude/`, `.serena/` personnels, ni fichiers de design externes sans décision de gestion des sources ; documenter la provenance de tout asset utilisé.
- Réagir seulement à un blocage réel ou au **seul gate migration/bootstrap**.

## 7 — Rapport consolidé

Fournir : SHA de `origin/main` et preuve du merge 01B ; liste des états/login/shell implémentés ; stratégie de session et statut ADR ; fichiers/migrations SQL proposés ; état des deux bases (sans secrets) ; tests et preuves Tauri **réels** ; visual diff aux références ; contrôles 401/403/révocation ; Serena/Graphify ; documentation ; commits locaux et limites. Ne pas annoncer Phase 02 COMPLETE si les migrations, le bootstrap, les tests d'auth ou la preuve native sont absents.

À la fin de la livraison : **`PHASE02_IMPLEMENTED — OWNER_REVIEW_REQUIRED`** uniquement si tous les gates convenus passent ; sinon décrire l'état exact et les seules actions requises.
