# 01A — GATES_AND_DECISIONS

## Gates

| Gate | Statut | Motif |
|---|---|---|
| P01-01 Environnement Windows | PASS | voir QA Q-02 |
| P01-02 Fenêtre Tauri native | PASS | Q-07/Q-08/Q-09 |
| P01-03 API .NET 10 indépendante | PASS (HTTP local) | Q-11..Q-13 ; HTTPS `NOT_VERIFIED` (Q-14) |
| P01-04 PostgreSQL exclusif | PARTIAL | provider Npgsql seul (PASS) ; migration/CRUD réels `NOT_TESTED` |
| P01-15 Graphify | PASS | Q-21 |
| P01-16 Serena | BLOCKED | approbation `/mcp` requise ; tâches symboliques suspendues |
| P01-05..P01-14 | NOT_TESTED | sous-étapes suivantes, GO séparé |

## Décisions prises dans 01A (révisables)

| ID | Décision | Raison |
|---|---|---|
| T-01 | Identifiant Tauri provisoire `com.mmv.v2.dev`, produit `MMV V2 Dev`, crate `mmv-desktop`, package npm `mmv-v2-desktop` | Non commercial ; à remplacer avant tout packaging (le changement d'identifiant change le dossier de données WebView2) |
| T-02 | Un seul hôte `MMV.Api` | Pas de `MMV.Server.Host` sans POC Windows Service |
| T-03 | Gestion centrale NuGet + `packages.lock.json`, `package-lock.json`, `Cargo.lock` versionnés | Reproductibilité |
| T-04 | `graphify-out/` non versionné, `.graphifyignore` exclut lockfiles/artefacts | Graphe local régénérable ; les lockfiles produisaient 575/867 nœuds de bruit |
| T-05 | Solution au format `.slnx` | Format par défaut du SDK .NET 10 |
| T-06 | Normalisation LF via `.gitattributes` | Les générateurs Windows produisent du CRLF |
| T-07 | Icônes Tauri par défaut conservées provisoirement | Aucune identité visuelle validée |

## Décisions ouvertes (propriétaire)

| ID | Question | Bloque |
|---|---|---|
| O-01 | **Base PostgreSQL de développement V2** — proposition : instance PG18 `localhost:5433`, base `mmv_v2_dev`, rôle `mmv_v2_dev` (LOGIN, propriétaire de cette seule base, sans SUPERUSER/CREATEROLE/CREATEDB), mot de passe hors Git (`dotnet user-secrets`). Opération : `CREATE ROLE` + `CREATE DATABASE ... OWNER` par un administrateur, aucune modification de `postgresql.conf`/`pg_hba.conf`. **Non exécuté, accord ciblé requis.** | P01-04 |
| O-02 | Serena : approuver le serveur MCP projet via `/mcp` (ou refuser) | P01-16 |
| O-03 | HTTPS local : approuver le certificat de développement ASP.NET (`dotnet dev-certs https --trust`, modifie le magasin de certificats utilisateur) ou attendre la décision TLS ADR-006 | Q-14 |
| O-04 | Graphify MCP (`graphifyy[mcp]`) : installation non effectuée (non autorisée explicitement) ; le CLI suffit pour 01A | Usage MCP du graphe |
| O-05 | ADR-005 installateur, ADR-006 TLS, ADR-007 sessions, ADR-009 parc Windows | 01B+ |
| O-06 | Identité commerciale finale (identifiant, nom produit, icônes) | packaging |

## Observations outillage

- `.claude/settings.json` (phase 00, non modifié) : hooks `PreToolUse` `graphify hook-guard search` (Bash|Grep) et `graphify hook-guard read` (Read|Glob). Effet observé : sans graphe, silencieux (exit 0) ; une fois `graphify-out/graph.json` présent, injecte un rappel « MANDATORY: run graphify query before grepping ». Non bloquant.
- Un hook **global** de l'utilisateur (`~/.claude/claude-token-optimizer/hooks/context-guard.js`) bloque les sorties longues et la lecture d'images > ~50 Ko : la capture n'a pas pu être visualisée par l'agent, elle a été vérifiée par analyse de pixels. Non modifié.
