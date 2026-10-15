# 01A — SOURCE_MAP

## Sources lues (dépôt neuf uniquement)

| Source | Usage |
|---|---|
| `prompts/MMV_V2_PHASE01A_PROMPT_CLAUDE_CODE.md` | Mandat 01A (versionné avec les corrections de finalisation, pour la reproductibilité) |
| `prompts/MMV_V2_PROMPT_FINALISATION_01A_DOCUMENTATION_ET_OUTILLAGE.md` | Mandat de finalisation documentaire et outillage (15/10/2026) ; versionné avec le commit final 01A |
| `README.md`, `docs/phase/00.md` | Décisions D-000..D-006, gates phase 00 |
| `docs/ARCHITECTURE_ET_STACK.md` | Structure cible du dépôt, stack |
| `docs/ADR_ET_DECISIONS.md` | Statuts ADR (aucun ratifié par 01A) |
| `docs/PERIMETRE_ET_SEQUENCE.md` | Séquence phases, backlog différé |
| `docs/PLAN_POC_ET_PREUVES.md` | IDs POC P01-xx |

Aucune lecture de l'ancien dépôt MMV, de son code, de sa base ou de ses règles. Aucune maquette Claude Design n'a été utilisée : seuls les jetons de couleur/polices du cadrage sont repris (DRAFT).

## Provenance des fichiers créés

| Chemin | Origine |
|---|---|
| `apps/desktop/**` | `npm create tauri-app@4.7.5 -- desktop --template react-ts --manager npm --identifier com.mmv.v2.dev`, puis réduction (suppression démo `greet`, plugin opener, assets Vite/Tauri) et ESLint ajouté |
| `apps/desktop/src-tauri/icons/**` | Icônes par défaut du générateur Tauri, **provisoires** |
| `global.json` | `dotnet new globaljson --sdk-version 10.0.402 --roll-forward latestFeature` |
| `MMV.slnx`, `src/*`, `tests/*` | `dotnet new sln/classlib/webapi/xunit` (SDK 10.0.402), puis code écrit à la main (santé, persistance, tests) |
| `Directory.Build.props`, `Directory.Packages.props` | Écrits à la main : options communes, gestion centrale NuGet, lockfiles |
| `.gitignore`, `.gitattributes`, `.graphifyignore`, `tests/.editorconfig` | Écrits à la main |
| `CLAUDE.md` | Phase 00 (local jusqu'ici) ; 01A : une ligne ajoutée (graphe local non versionné) ; finalisation 15/10/2026 : section Graphify corrigée (renvoi au wiki absent et obligation absolue retirés) et section **DOCUMENTATION GATE** ajoutée ; versionné avec le commit final 01A |
| `.mcp.json` | Phase 00, non modifié (déclaration du serveur Serena, commande `serena` via PATH, sans secret) ; versionné avec le commit final 01A |
| `.claude/settings.json` | Phase 00, local, non versionné : les deux hooks Graphify non approuvés ont été retirés manuellement par le propriétaire (contenu `{}`) après refus de l'agent par la protection d'auto-modification |
| `.claude/CLAUDE.md`, `.claude/skills/graphify/**`, `.serena/project.yml` | Phase 00 / configuration Serena, locaux, **non modifiés**, non versionnés (licence du skill et décision TypeScript en attente, voir GATES_AND_DECISIONS O-02/O-04) |

## Documentation officielle de référence (à revérifier par le propriétaire)

- Tauri 2 : https://v2.tauri.app/start/create-project/
- ASP.NET Core health checks : https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
- Npgsql EF Core 10 : https://www.npgsql.org/efcore/release-notes/10.0.html
