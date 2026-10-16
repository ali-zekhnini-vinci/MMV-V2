# MMV V2 — GO PHASE 03 / DÉMARRAGE 03-A — CLIENTS, PRÉPARATION RENDEZ-VOUS

Tu es un ingénieur logiciel principal et un auditeur de cohérence intervenant sur **MMV V2 entièrement neuf**.

Le propriétaire confirme que la Phase 02 est fusionnée et autorise à démarrer la Phase 03, en commençant par le module **Clients**. L'autorisation couvre le développement frontend, les propositions de contrats métier, le code et les tests non destructifs, et les **commits locaux** sur une nouvelle branche. Elle **n'autorise pas encore une nouvelle migration métier ou la création de permissions persistantes en base**, qui nécessitent un GO ciblé.

## 0. Sources à lire d'abord (ordre d'autorité)

1. `CLAUDE.md`, `README.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/ADR_ET_DECISIONS.md` du dépôt actuel ; `docs/phase/02/` pour l'architecture implémentée.
2. `references/phase03/PHASE03_CADRAGE.md` et `references/phase03/DECISIONS_METIER_A_RATIFIER.md`.
3. `references/phase03/LOT2_ETATS_VISUELS.md` et la maquette `references/phase03/lot2/MMV-Lot2-Clients-RendezVous.dc.html`, en tant que **références visuelles DRAFT uniquement**. Le fichier inclut des légendes historiques faisant allusion à l'ancien dépôt : **ignorer ces références et n'accéder sous aucun prétexte à l'ancien dépôt MMV**. Ne jamais copier le code de la maquette dans la production. Conserver les tokens approuvés en phase 02.
4. Les dernières décisions explicitement approuvées par le propriétaire dans la documentation courante. Les textes DRAFT ne sont pas des lois métier.

## 1. Git et préflight (G03-0)

- `git fetch origin` puis vérifier `origin/main`, la fusion de Phase 02, les migrations d'identité et la stack réellement présente.
- Partir de **`origin/main` actuel**, sur la nouvelle branche `feat/phase-03-clients-rdv`. Ne pas continuer sur `feat/phase-02-auth-shell` ni sur un `main` local obsolète.
- Ne pas écraser les modifications utilisateur. Si un état Git inattendu apparaît, STOP et expliquer précisément.
- Vérifier la présence/les versions de Tailwind CSS 4, shadcn/ui/Radix, React Router, RHF/Zod, Vitest, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 18 (seulement la base MMV).
- Aucun accès à l'ancien dépôt MMV, aucun SQLite, aucune migration lancée au démarrage.
- Déclarer dans `docs/phase/03.md` et `docs/phase/03/` les gates initiaux, les preuves et les décisions ouvertes.

## 2. Module Clients — démarrer concrètement le frontend

**Installer et utiliser réellement** TanStack Query v5 et TanStack Table v8 (versions compatibles recherchées, figées dans `package-lock.json`). Conserver les briques UI existantes (Tailwind CSS 4, shadcn/ui adapté, Radix, Lucide, React Hook Form, Zod, React Router) : ne pas ajouter une seconde librairie de design, ni recréer TanStack Table manuellement.

Brancher la navigation « Clients » préexistante `/clients` (actuellement `availability: later`). Préparer, à partir des maquettes, des composants réutilisables testés pour :
- Liste Clients avec colonnes réellement homologables, pagination/tri/recherche (pilotables côté serveur quand l'API sera ratifiée), état vide/chargement/erreur, filtres archivé/actif.
- Formulaire unique de création/modification, validations UX, état dirty et confirmation de fermeture, accessibilité clavier.
- Fiche Client avec onglets Informations, Ordonnances, Ventes/commandes, les deux derniers en **état indisponible/vides explicitement liés aux phases ultérieures**, sans simuler de données métier.
- Confirmation d'archivage et réactivation (UI prête, action persistante désactivée jusqu'aux règles validées).
- Préparer le pattern des erreurs API/401/403, conflits optimistes, cache invalidation, annulation de requêtes et rafraîchissement. Côté serveur, jamais de décision d'autorisation dans le seul React.

**Aucune donnée client fictive persistée.** Les scénarios artificiels sont admis dans les tests/Story fixtures explicitement séparées ; pas de listes factices montrées comme réelles en fonctionnement normal. Avant ratification d'une API, afficher honnêtement l'état indisponible plutôt qu'un faux PASS.

## 3. Conception des contrats et modèle Clients (sans DDL appliqué)

Examiner ce que les 47 états DRAFT suggèrent et produire un dossier `docs/phase/03/` comprenant :
- Table de correspondance état visuel → fonctionnalité → décision métier nécessaire → état APPROVED/PROPOSED/DEFERRED.
- Proposition de modèle Clients strictement minimal, champs et contraintes argumentés, index/recherche, politique de cycle de vie, unicité, date/temps, concurrence, confidentialité ; distinguer champs utiles au MVP et optionnels/sensibles.
- API REST proposée (routes, requêtes/réponses, pagination/tri whitelist, validation, erreurs 400/401/403/404/409/422, version/ETag si choisi) ; décider de la politique d'archivage après arbitrage.
- Stratégie **permissions métier** explicites : ne pas présumer que `system.admin` représente tous les droits Clients, ni créer de comptes/rôles à partir de profils visuels.
- Plan EF Core / Npgsql et migration proposée ; scripts SQL **générés mais non appliqués** ; pas de tables métier créées avant GO.
- Vérification du contrat OpenAPI ; préférence `openapi-typescript`/`openapi-fetch` si génération fonctionnelle sans casser le client actuel, sinon documenter le blocage sans multiplier les dépendances arbitraires.

Ne pas figer des règles V2 en lisant les noms `Customer`/services ou les exemples de valeurs de l'ancien dépôt mentionnés dans le design. Ne pas introduire de données médicales/ordonnances Phase 04 dans les tables Clients 03-A.

## 4. Préparer le module Rendez-vous (03-B), sans implémenter la persistance maintenant

Recenser les maquettes agenda jour/semaine + sélecteur client + formulaire + détails + édition + conflit + annulation et la navigation `/rendez-vous` à ajouter. Étudier la faisabilité FullCalendar **Standard** React, vues `timeGridDay`, `timeGridWeek` (et `dayGridMonth` seulement si justifié), `interaction` pour sélection/déplacement avec **alternative clavier explicite**.

Établir les questions métier de date/heure, fuseau, durée, personne/ressource, politique de chevauchement, statuts et accès aux notes. Aucun calendrier opérationnel prétendu avant décisions et API. Ne pas utiliser FullCalendar Premium non licencié ; ne pas installer encore si 03-B n'est pas démarré.

## 5. Tests et outillage

- Ajouter tests Vitest + Testing Library sur table, liste, formulaires, contrôles dirty, filtres/tri/pagination, clavier, light/dark. Comparer les captures aux planches et énumérer les écarts ; vraie fenêtre Tauri si l'environnement le permet.
- Vérifier `npm ci`, typage, lint **sans exclusion**, Vitest, build ; tests .NET existants non régressifs ; secrets absents du diff.
- Avant ajout d'une API persistante, écrire tests d'autorisation serveur, tests Npgsql ciblés `mmv_v2_test`, absence de migration automatique, aucune fixture réelle.
- Serena MCP/CLI symboles TypeScript et C# et Graphify CLI : consigner les appels réels, sans prétendre qu'un outil non opérationnel est PASS.
- Ne pas toucher à PG17/5432 ni aux bases non MMV, ni aux mots de passe, certificats ou configuration réseau.

## 6. DOCUMENTATION GATE — en continu

Vérifier et synchroniser au minimum : `README.md`, `CLAUDE.md` si pertinent, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/phase/03.md`, `docs/phase/03/{IMPLEMENTATION_SPEC,QA_MATRIX,GATES_AND_DECISIONS,SOURCE_MAP}.md`, ADR/backlog si nécessaires. Conserver l'historique daté ; ne pas altérer les phases terminées. Faire correspondre preuves, statut, code, migrations et questions ouvertes. Ne pas marquer Phase 03 achevée avant 03-B et 03-C.

## 7. Autonomie et arrêt unique

Avancer sans demandes répétitives dans tout le périmètre **sans écriture métier persistante**. Commits locaux structurés **autorisés** sur `feat/phase-03-clients-rdv` ; pas de push, PR, merge ni Phase 04. N'attendre une approbation que pour le dossier de décisions métier/permissions/migrations de Phase 03, ou un incident réel de sécurité ou destructif.

### Livrables du premier passage

1. Rapport préflight et SHA de `origin/main` depuis lequel la branche a été créée.
2. Architecture UI de 03-A et composants réellement intégrés + premiers écrans Clientes utilisables hors données fictives ; tests et captures disponibles.
3. Dossier d'arbitrage métier condensé, avec recommandations clairement distinguées des décisions validées ; proposition de schéma, droits et API.
4. Proposition de séquence 03-B Rendez-vous fondée sur 47 états visuels, sans migrations appliquées.
5. Résultats tests, état Git/commits locaux et DOCUMENTATION GATE.

Finir par : **`PHASE03_A_UI_READY — OWNER_BUSINESS_RULES_AND_SQL_REVIEW_REQUIRED`**.
