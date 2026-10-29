# Architecture MMV V2 et stack cible — propositions phase 00

**Statut des propositions :** `PROPOSED`, à ratifier. Les décisions existantes `NO LEGACY`, `POSTGRESQL ONLY` et serveur/postes sont déjà approuvées.

## 1. Vue d'ensemble

```text
  [Poste A : Tauri 2 / React]    [Poste B : Tauri 2 / React]
              │                               │
       TLS HTTPS / API REST / SignalR (LAN ; aucune DB client)
              └──────────────┬────────────────┘
                             ▼
                    [SERVEUR MMV Windows]
              Service ASP.NET Core / .NET 10 LTS
              Auth · Policies · Modules métier · OpenAPI
                  Services d'application / EF Core
                             │
                  Npgsql / transactions SQL
                             ▼
                    [PostgreSQL 18.x]
                  données et pièces jointes serveur
                  sauvegardes protégées + restauration

  Monoposte = PostgreSQL + service API + client Tauri sur le même PC.
  Multiposte = une seule API/DB serveur pour tous les clients.
```

Le poste Tauri n'héberge ni backend .NET complet ni DB ; Rust Tauri est une couche mince d'intégration OS. Aucun accès direct du client à PostgreSQL, ni secret DB ni droit admin côté poste.

## 2. Structure proposée du dépôt neuf (ne pas créer maintenant)

```text
MMV-V2/
  apps/desktop/                    # Tauri 2 + React + Vite + TS strict
  src/MMV.Domain/                 # Invariants et objets métier V2 inédits
  src/MMV.Application/            # Cas d'usage et contrats V2
  src/MMV.Infrastructure/         # EF Core Npgsql, adaptateurs externes
  src/MMV.Api/                    # REST, OpenAPI, SignalR, RBAC
  src/MMV.Server.Host/            # Hébergement service Windows / diagnostics
  packages/api-client/            # Client et types TS générés depuis OpenAPI
  docs/adr/                       # ADR vérifiés
  docs/phase/                     # Preuves / gates
  installer/                      # Orchestrateur à prototyper
  tests/                          # PG intégration + tests UI et contractuels
  design/                         # Références visuelles homologuées
```

Aucun composant supplémentaire par principe : éviter de multiplier les projets backend artificiellement. On peut fusionner `Api` et `Server.Host` si le POC démontre que c'est plus simple. Les modules métier sont `Customers`, `Appointments`, `Prescriptions`, `Catalog`, `Inventory`, `Sales`, `Payments`, `Orders`, `Workshop`, puis extensions.

## 3. Matrice des composants

| Couche | Choix de référence | Statut | Usage/limite |
|---|---|---|---|
| OS cible | Windows 11 x64 ; autres versions selon parc | Proposition | Support et prérequis WebView2 à vérifier |
| Desktop | Tauri 2 + WebView2 | Cible propriétaire | Aucune logique métier critique Rust |
| Front | React + TypeScript strict + Vite + npm | Proposition | UI sans Next.js |
| Style | Tailwind CSS 4 + variables MMV ; CSS ciblé | **Intégré phase 02** (4.3.3) | Fidélité des tokens, pas thème shadcn par défaut |
| Composants | Radix primitives + shadcn/ui adaptés | **Intégré phase 02** (Button, Label, DropdownMenu) | Accessibilité, réécriture des styles |
| Icônes | lucide-react | **Intégré phase 02** (1.49.0) | Pas d'emoji dans le produit |
| Routes | React Router | **Intégré phase 02** (8.4.0, routeur mémoire) | Le routing/guard client n'est pas un contrôle d'accès |
| Réseau/data | TanStack Query v5 ; openapi-typescript/openapi-fetch | Proposition — **non installé** (avec le premier cas d'usage réel) | API typed, retry limité pour mutations critiques |
| Listes | TanStack Table v8 | Proposition | Tri et pagination serveur selon taille |
| Formulaires | React Hook Form + Zod | **Intégré phase 02** (7.90.0 + 4.6.7, Login) | UI seulement, toujours revalider sur le serveur |
| Agenda | FullCalendar Standard | Proposition | Pas de plugin Premium sans licence payante |
| Kanban | dnd-kit | À ajouter au lot atelier | Fallback clavier obligatoire |
| Dialogues/notifications | Radix + Sonner | Proposition | Pas de toasts comme seule preuve d'opération |
| Etat UI | React hooks/context ; Zustand si besoin mesuré | Option | Éviter 2 sources de vérité |
| Graphiques | Recharts | Différé | Rapports ou dashboard concernés seulement |
| Langue/format | Français, Intl, MAD, Inter + Source Sans 3 | Cible visuelle ; polices **embarquées localement** (OFL) en phase 02 | Définir règles métier/arrondis distinctement |
| API/backend | ASP.NET Core .NET 10 LTS ; C# | Proposition forte | Service Windows et monolithe modulaire |
| Persistance | EF Core 10 + Npgsql 10 + PostgreSQL 18.x | PostgreSQL approuvé, versions proposées | Pas de SQLite même tests/unitaires |
| Temps réel | ASP.NET Core SignalR | Proposition | Notifications/invalidation cache, pas CRUD primaire |
| Auth | ASP.NET Core Identity + policies | Proposition | Séparer authentification, autorisation et pairing |
| OpenAPI | ASP.NET Core OpenAPI | Proposition | Types TS générés ; contrat API source de vérité |
| Tests backend | xUnit, tests intégration PostgreSQL (Testcontainers possible CI/dev) | Proposition | Ne jamais remplacer PG par SQLite/InMemory pour les tests de persistance |
| Tests UI | Vitest, Testing Library, Playwright | Proposition | Browser Playwright ≠ preuve exacte du runtime Tauri Windows |
| Logs | Microsoft logging ; Serilog si besoin démontré | Proposition | Pas de données de santé/personnelles dans les logs |
| Jobs | BackgroundService seulement si besoin | Proposition | Maintenance et sauvegarde contrôlées |
| Graphify | graphifyy, repo-local, local en préférence | Obligatoire pour phases code | Preuve de graphe construit et requêtes réellement lancées |
| Serena | MCP local, navigation symbolique TS/C# | Obligatoire pour phases code | Projet neuf et LSP fonctionnel ; vérifier licence des outils de dev |

> Note datée 15/10/2026 (phase 01B) : la proposition « Tests UI » ci-dessus reste une proposition. En 01B, les tests du client API utilisent le lanceur natif `node --test` (Node 22, aucune dépendance) : l'ajout de Vitest a échoué sur npm 10.9.9 (plantage du résolveur, détail `docs/phase/01b/QA_MATRIX.md` Q-B14 ; décision O-B08). À réévaluer avant les tests de composants UI.

**Versions :** ne pas déduire un numéro de patch du document ; pinner les versions exactes en phase 01, après `npm`, `dotnet`, `cargo`, NuGet, LSP et docs officiels vérifiés sur Windows. PostgreSQL 18 actuellement maintenu (support jusqu'en 2030).

> Note datée 16/10/2026 (phase 02, corrections finales) : les lignes Style, Composants, Icônes, Routes, Formulaires et polices sont **installées et utilisées** dans `apps/desktop` (versions exactes et chemins : `docs/phase/02/IMPLEMENTATION_SPEC.md` § Pile frontend, `apps/desktop/README.md`). Les autres lignes frontend (TanStack Query/Table, FullCalendar, dnd-kit, Sonner, Zustand, Recharts) restent des propositions **non installées** : elles seront ajoutées avec leur premier cas d'usage réel. Statuts « Proposition » remplacés pour ces seules lignes ; aucun ADR n'est ratifié par cette note.

> Note datée 17/10/2026 (phase 03-A) : **TanStack Query** 5.104.1 et **TanStack Table** 8.21.3 sont installés et utilisés (`apps/desktop/src/api/queryClient.ts`, `apps/desktop/src/clients/`) : un cache par session, vidé à la déconnexion, aucun persister ; tri et pagination **serveur** (mode manuel). `openapi-typescript` / `openapi-fetch` **non installés** : le document OpenAPI actuel ne déclare que des réponses 200 sans schéma (`docs/phase/03/API_CLIENTS_PROPOSED.md`, O-03-01). FullCalendar Standard : cadrage 03-B (`docs/phase/03/RDV_03B_PREPARATION.md`), v7 MIT, aucun plugin Premium. Radix `react-alert-dialog` et `react-tabs` ajoutés (composants shadcn/ui). Aucun ADR ratifié par cette note.

> Note datée 17/10/2026 (phase 03-B) : **FullCalendar Standard** 7.1.0 (MIT) et `temporal-polyfill` 1.0.5 sont installés et utilisés par le module Rendez-vous (`apps/desktop/src/appointments/`, chunk chargé à la demande) : vues `timeGridDay` / `timeGridWeek`, fuseau du magasin fourni par le serveur, rendu par les jetons MMV (FullCalendar 7 sans thème). Aucun plugin Premium. Service désactivé tant que les décisions D-03-13 à D-03-18 ne sont pas ratifiées.

## 4. Choix d'architecture comparés

| Scénario | Atouts | Coûts / risques | Décision |
|---|---|---|---|
| **Tauri + API .NET + PG serveur** | UI web fidèle, frontière backend unique, vrai LAN | Deux runtimes, installation et TLS plus complexes | **Cible recommandée** |
| Electron + API .NET + PG | Chromium homogène, large écosystème | Runtime/package plus lourd, changement non demandé | Rejeté sauf échec POC |
| Web SPA seule + API .NET | Déploiement postes simplifié | Moins d'intégration matériel/Windows, contredit orientation desktop | Hors V1 |
| Tauri + sidecar .NET et PostgreSQL par poste | Confort d'un PC isolé | Bases divergentes, sync à concevoir, redondance | **Interdit** |
| Microservices + broker | Découplage potentiel à très grande échelle | Surdimensionné pour magasin LAN | Différé |

## 5. Invariants de départ (à valider en scénarios)

- Une vente/paiement/mouvement de stock ne peut être « réussi » en UI sans confirmation API persistée.
- Toute écriture critique doit traiter le double-clic/retry (clé idempotente) et la concurrence entre postes (transaction / version / HTTP 409).
- Stock et argent : mutations autorisées côté serveur uniquement ; C# `decimal`, PostgreSQL `numeric` et règles d'arrondi fiscal fixées avant le module Vente.
- SignalR ne transporte pas de secrets ni de dossiers optiques complets ; il indique un changement, puis le poste relit via API sous RBAC.
- Le cache TanStack Query n'est pas une base offline ; aucun sync automatique supposé.
- L'absence d'accès serveur doit être clairement visible (lecture via cache, si autorisée plus tard, jamais présentée comme données fraîches).

## 6. Identité visuelle

Design DRAFT : interface FR avec vouvoiement ; monnaies MAD ; `#0066FF`, `#0B1D3A`, `#00C2A8`, polices Inter et Source Sans 3 ; dark/light ; sidebar 260/72, topbar 64, statusbar 32 ; claviers/focus, 1440×900 et 1920×1080. Les dimensions exactes et variantes seront auditées visuellement avant implémentation et validées par le propriétaire.

> Note datée 15/10/2026 (soir, terminaison 01B) : la note précédente est remplacée sur demande du propriétaire. Vitest **5.0.3** est adopté pour les tests unitaires du client (`npm test` = `vitest run`, environnement node) avec un `overrides` npm qui contourne le plantage du résolveur npm 10.9.9 (détail : `docs/phase/01b/QA_MATRIX.md` Q-B24). Testing Library et Playwright restent des propositions ; la preuve d'exécution réelle reste la fenêtre Tauri native.

> Note datée 17/10/2026 (phase 03-A2) : premier module métier dans les quatre couches — `MMV.Domain/Clients` (entité, repliement de recherche), `MMV.Application/Clients` (validation, cas d'usage, port `IClientStore`), `MMV.Infrastructure/Clients` (EF Core 10 / Npgsql, schéma `clients`, concurrence `xmin`), `MMV.Api/Clients` (minimal API à `TypedResults`, policies par permission). Sens des références inchangé (`ProjectReferenceRulesTests`). OpenAPI : schémas réels pour les routes Clients, instantané `apps/desktop/src/api/contract/clients.openapi.json` contrôlé contre le serveur et contre les types TypeScript ; `openapi-typescript` / `openapi-fetch` toujours **non installés**. Aucun ADR ratifié par cette note.

> Note datée 17/10/2026 (phase 03-A3) : schéma `clients` désormais présent dans `mmv_v2_dev` et `mmv_v2_test` (3 migrations EF Core par base, appliquées par script relu, jamais au démarrage). Côté client, une garde **d'affichage** (`apps/desktop/src/clients/clientPermissions.ts`) masque ou désactive les actions selon les permissions de la session ; l'autorisation reste exclusivement serveur (policies `clients.*`). Aucune dépendance ajoutée.

> Note datée 17/10/2026 (phase 03-B2) : module **Rendez-vous** côté serveur, même découpage que Clients (`MMV.Domain/Appointments`, `MMV.Application/Appointments`, `MMV.Infrastructure/Appointments`, `MMV.Api/Appointments`) ; schéma PostgreSQL `appointments` (migration 0004 **non appliquée**). Paramètres du magasin lus dans la section serveur `Appointments` (fuseau IANA, plage affichée, capacité d'accueil), sans valeur par défaut : absents → 503 `appointments_not_configured`, l'API démarre quand même. Concurrence : transaction `READ COMMITTED` par écriture, verrous consultatifs transactionnels par ressource (agenda du collaborateur, pool d'accueil) pris par clés croissantes, verrou partagé sur le client, `xmin` pour la concurrence optimiste ; aucun microservice, aucune extension, aucun FullCalendar Premium. Fuseaux : `TimeZoneInfo` (Windows) côté serveur, Temporal / ICU côté interface — concordance à surveiller (O-03-22). Contrat OpenAPI des routes Rendez-vous contrôlé par instantané des deux côtés (`apps/desktop/src/api/contract/appointments.openapi.json`). Aucune dépendance ajoutée.

> Note datée 18/10/2026 (phase 03-B3) : **cohérence des fuseaux serveur ↔ poste.** MMV.Api convertit avec les données de fuseaux de Windows, le poste avec l'ICU de WebView2 ; elles divergent réellement sur ce poste (Africa/Casablanca, 01/01–15/02/2026). Stratégie retenue, vérifiable et sans nouvelle dépendance : la liste des rendez-vous porte les décalages UTC du serveur sur la période (`timeZoneOffsets`), comparés par le poste (période bloquée en cas d'écart) ; chaque écriture annonce l'heure murale saisie (`startsAtLocal`), recalculée par le serveur (422 `time_zone_mismatch`). Le serveur reste l'autorité ; les postes et le serveur doivent rester à jour (Windows Update, WebView2) ; une base de fuseaux embarquée reste une alternative non introduite. Le contrat élargi impose de mettre à jour MMV.Api et les postes ensemble. Détail : `docs/phase/03/API_APPOINTMENTS.md` § Cohérence des fuseaux ; tests PostgreSQL exécutés séquentiellement (limite de 10 connexions par rôle de test).

> Note datée 18/10/2026 (phase 03-D) : **journal d'audit transversal** — `MMV.Domain/Audit` (événement, catalogue d'actions, politique de champs), `MMV.Application/Audit` (`IAuditRecorder` appelé par les services métier dans leur unité de travail, lecture `AuditService`), `MMV.Infrastructure/Audit` (même `MmvDbContext`, schéma `audit`), `MMV.Api/Audit` (`GET /api/audit/clients/{id}`, policy `AuditRead`) ; interface `apps/desktop/src/audit/`. Sélecteurs de date et d'heure (shadcn/ui Popover + React DayPicker) dans `apps/desktop/src/components/ui/`. Migration 0005 préparée, non appliquée.

> Note datée 19/10/2026 (phase 03-D3) : migration `0005_AuditJournal` **appliquée** (`mmv_v2_dev`, `mmv_v2_test`) ; architecture du journal retenue (D-03-19 à D-03-21). Les déclencheurs d'ajout seul ne protègent pas contre un superutilisateur ni contre le rôle propriétaire : un **rôle SQL applicatif distinct** du rôle de migration (droits `INSERT` / `SELECT` sur `audit.events`, aucune propriété) et une **rétention contrôlée** sont à prévoir (O-B04, D-03-22 ouverte).

> Note datée 19/10/2026 (phase 04-00) : **aucun changement d'architecture.** Le dossier optique est **proposé** dans le monolithe existant (schéma PostgreSQL `optical`, couches `Optical/` de MMV.Domain / Application / Infrastructure / Api, `apps/desktop/src/optical/`), écriture métier et audit dans la même transaction, pièces source **proposées** en table PostgreSQL (aucun stockage externe, aucun lien public) ; aucune dépendance nouvelle. Tout reste `PROPOSED` : [`phase/04/IMPLEMENTATION_SPEC.md`](phase/04/IMPLEMENTATION_SPEC.md), [`phase/04/DATA_MODEL_PROPOSED.md`](phase/04/DATA_MODEL_PROPOSED.md).

> Note datée 20/10/2026 (Phase 04-A, D-04-16 APPROVED) : **aucun changement d'architecture.** Les pièces source **ne sont plus proposées** pour la Phase 04 : gestion documentaire reportée au backlog permanent BL-04-01 ([`PERIMETRE_ET_SEQUENCE.md`](PERIMETRE_ET_SEQUENCE.md)) ; aucune table ni API de pièces. Le dossier optique proposé se limite aux en-têtes et révisions des deux types (schéma `optical`), toujours `PROPOSED`.
