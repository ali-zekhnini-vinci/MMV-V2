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
| Style | Tailwind CSS 4 + variables MMV ; CSS ciblé | Proposition | Fidélité des tokens, pas thème shadcn par défaut |
| Composants | Radix primitives + shadcn/ui adaptés | Proposition | Accessibilité, réécriture des styles |
| Icônes | lucide-react | Proposition | Pas d'emoji dans le produit |
| Routes | React Router | Proposition | Le routing/guard client n'est pas un contrôle d'accès |
| Réseau/data | TanStack Query v5 ; openapi-typescript/openapi-fetch | Proposition | API typed, retry limité pour mutations critiques |
| Listes | TanStack Table v8 | Proposition | Tri et pagination serveur selon taille |
| Formulaires | React Hook Form + Zod | Proposition | UI seulement, toujours revalider sur le serveur |
| Agenda | FullCalendar Standard | Proposition | Pas de plugin Premium sans licence payante |
| Kanban | dnd-kit | À ajouter au lot atelier | Fallback clavier obligatoire |
| Dialogues/notifications | Radix + Sonner | Proposition | Pas de toasts comme seule preuve d'opération |
| Etat UI | React hooks/context ; Zustand si besoin mesuré | Option | Éviter 2 sources de vérité |
| Graphiques | Recharts | Différé | Rapports ou dashboard concernés seulement |
| Langue/format | Français, Intl, MAD, Inter + Source Sans 3 | Cible visuelle | Définir règles métier/arrondis distinctement |
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

**Versions :** ne pas déduire un numéro de patch du document ; pinner les versions exactes en phase 01, après `npm`, `dotnet`, `cargo`, NuGet, LSP et docs officiels vérifiés sur Windows. PostgreSQL 18 actuellement maintenu (support jusqu'en 2030).

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
