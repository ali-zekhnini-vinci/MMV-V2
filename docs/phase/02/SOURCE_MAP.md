# 02 — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 uniquement)

| Source | Usage |
|---|---|
| `prompts/PHASE02_PROMPT_CLAUDE_CODE.md` | Mandat Phase 02 (GO du 16/10/2026), versionné avec la phase |
| `README.md`, `CLAUDE.md` | État 01B, DOCUMENTATION GATE |
| `docs/PERIMETRE_ET_SEQUENCE.md` | Phase 02 = Shell/Login/tokens, statut serveur, bootstrap admin, auth et permissions minimales |
| `docs/ADR_ET_DECISIONS.md` | ADR-007 sessions `PROPOSED`, ADR-006 `OPEN DECISION`, D-003 PostgreSQL seul |
| `docs/phase/01b/IMPLEMENTATION_SPEC.md`, `docs/phase/01b/GATES_AND_DECISIONS.md` | procédure de secrets, application des scripts SQL par le propriétaire (`psql`), privilèges des rôles (propriétaires de leur base) |
| `references/phase02/` (copie de travail, **non versionné**) | références visuelles DRAFT (ci-dessous) |

Aucune lecture de l'ancien dépôt, code, base ou règles MMV. Les handoffs citent un ancien dépôt et une cible Avalonia/.NET 8/AXAML : **ignorés** (mandat §0.4) ; seules les cotes, couleurs, textes et états visuels sont exploités.

## Références visuelles (`references/phase02/sources/claude-design/`)

Empreintes SHA-256 relevées le 16/10/2026 (16 premiers caractères). Statut : **DRAFT**, aucune approbation présumée.

| Fichier | SHA-256 (préfixe) | Usage |
|---|---|---|
| `MMV-Lot1.dc.html` | `04e0f3df91b1dd50` | identique à l'entrée lot 1 de `registres/DESIGN_SOURCES_SHA256.csv` ; prototype non repris dans le code |
| `MMV-Login-Handoff/MMV-Login-handoff.md` | `7a5c02e9ad495ddd` | cotes du login |
| `MMV-Login-Handoff/MMV-Login-states.md` | `d6c1320815a27927` | états du login |
| `MMV-Login-Handoff/MMV-Login-1920x1080.png` | `acaeaf824bd4164e` | capture de référence |
| `MMV-Shell-Handoff/MMV-Shell-handoff.md` | `1340ef2c8ad56e68` | cotes du shell |
| `MMV-Shell-Handoff/MMV-Shell-states.md` | `3ab6d01351a8d94e` | états, tabulation, accessibilité |
| `MMV-Shell-Handoff/MMV-Shell-{Open,Collapsed}-{Light,Dark}-1920x1080.png`, `…-Light-1440x900.png` | `41538b45…`, `b12976ac…`, `d0a95413…`, `9e74a371…`, `51b78646…`, `bc1fa737…` | captures de référence |
| `_ds/mmv-managemyvision-design-system-90800da0-…/tokens/{colors,radii,shadows,spacing,typography}.css` | `1a9bf785…`, `0861f367…`, `aabd33b4…`, `589f06b2…`, `da0dea77…` | valeurs reprises dans `apps/desktop/src/styles/tokens.css` |
| `_ds/…/tokens/fonts.css` | `6d834a2b…` | **non repris** (`@import` Google Fonts distant) ; familles Inter et Source Sans 3 embarquées localement depuis le 16/10/2026 (paquets npm OFL, ci-dessous) |

## Assets embarqués dans le client (copie à l'identique, provenance)

| Fichier versionné | Source | SHA-256 |
|---|---|---|
| `apps/desktop/src/assets/brand/logo-login.png` (300×132) | `MMV-Login-Handoff/logo-login.png` (= `MMV-Shell-Handoff/assets/logo-dark.png`) | `3df57decabf76455374f0051b6dec006dffe8b55ad7aeabd6b6e3cce8e3856cb` |
| `apps/desktop/src/assets/brand/logo-horizontal.png` (630×140) | `MMV-Shell-Handoff/assets/logo-horizontal.png` | `3570c6f9f0a08f60428e8fe1be6d426c0d9e4f9c5ce65eb7b128a311f1913a71` |
| `apps/desktop/src/assets/brand/logo-horizontal-dark-sidebar.png` (630×140) | `MMV-Shell-Handoff/assets/logo-horizontal-dark-sidebar.png` | `3ec0e0708dc800288eee0de7a339c5e9997aed83808c8076f0c7b30e42f1d03e` |
| `apps/desktop/src/assets/brand/logo-monogram.png` (232×128) | `MMV-Shell-Handoff/assets/logo-monogram.png` | `28cfa5299a2aa4c2326900b1219fcb0e85b1759749afe08b645cb4f54194402e` |

Logos de la marque MMV fournis par le propriétaire ; droits de redistribution à confirmer (O-02-06). Aucun JavaScript ni HTML de référence importé dans le runtime.

**Polices (16/10/2026)** : Inter et Source Sans 3 proviennent des paquets npm `@fontsource-variable/inter` 5.3.0 et `@fontsource-variable/source-sans-3` 5.3.1 (fichiers woff2 de Google Fonts redistribués par Fontsource, **SIL Open Font License 1.1** : usage, embarquement et redistribution autorisés avec la licence, vente seule interdite). Chargées par `apps/desktop/src/styles/fonts.css`, intégrées au bundle ; textes de licence copiés depuis les paquets dans `apps/desktop/public/licenses/Inter-OFL-1.1.txt` et `SourceSans3-OFL-1.1.txt`. *Historique : aucune police embarquée jusqu'au 16/10/2026.*

## Dépendances ajoutées (npm, versions exactes)

| Paquet | Version | Licence | Portée | Justification |
|---|---|---|---|---|
| `lucide-react` | 1.49.0 | ISC | runtime | icônes Lucide prescrites par le design system, locales |
| `jsdom` | 30.1.1 | MIT | dev | environnement DOM des tests d'écran |
| `@testing-library/react` | 16.3.3 | MIT | dev | rendu React en test |
| `@testing-library/dom` | 10.4.2 | MIT | dev | pair requis |
| `@testing-library/user-event` | 14.6.7 | MIT | dev | clavier/clic au niveau DOM |

Versions publiées depuis ≥ 2 semaines au 16/10/2026.

**Ajouts du 16/10/2026 (corrections finales, pile frontend prévue par `docs/ARCHITECTURE_ET_STACK.md`)** — versions exactes (`--save-exact`), `npm audit` : 0 vulnérabilité :

| Paquet | Version | Licence | Portée | Justification / usage |
|---|---|---|---|---|
| `tailwindcss` | 4.3.3 | MIT | dev | Tailwind CSS 4 (`src/styles/index.css`) |
| `@tailwindcss/vite` | 4.3.3 | MIT | dev | plugin Vite officiel |
| `@radix-ui/react-slot` | 1.5.0 | MIT | runtime | `Button asChild` (shadcn/ui) |
| `@radix-ui/react-label` | 2.1.17 | MIT | runtime | étiquettes du login |
| `@radix-ui/react-dropdown-menu` | 2.2.1 | MIT | runtime | menu profil |
| `class-variance-authority` | 0.7.1 | Apache-2.0 | runtime | variantes shadcn/ui |
| `clsx` | 2.1.1 | MIT | runtime | `cn()` |
| `tailwind-merge` | 3.7.0 | MIT | runtime | `cn()` (conflits d'utilitaires) |
| `react-hook-form` | 7.90.0 | MIT | runtime | formulaire de connexion |
| `zod` | 4.6.7 | MIT | runtime | schéma `loginSchema` |
| `@hookform/resolvers` | 5.9.1 | MIT | runtime | `zodResolver` |
| `react-router` | 8.4.0 | MIT | runtime | navigation du shell ; exige React ≥ 19.2.7 (plage `react`/`react-dom` relevée de `^19.1.0` à `^19.2.7`, version résolue inchangée 19.3.1) et Node ≥ 22.22 |
| `@fontsource-variable/inter` | 5.3.0 | OFL-1.1 | runtime (asset) | police Inter locale |
| `@fontsource-variable/source-sans-3` | 5.3.1 | OFL-1.1 | runtime (asset) | police Source Sans 3 locale |

shadcn/ui n'est pas une dépendance npm : composants copiés et adaptés (`apps/desktop/src/components/ui/`, `apps/desktop/components.json`), sans le thème par défaut.

## Dépendances ajoutées (NuGet, gestion centrale `Directory.Packages.props`)

| Paquet | Version | Licence | Projet | Justification |
|---|---|---|---|---|
| `Microsoft.Extensions.Identity.Core` | 10.0.13 | MIT | `MMV.Infrastructure` | `PasswordHasher<T>` d'ASP.NET Core Identity (aucune crypto maison) |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.13 (existant) | MIT | `MMV.PostgreSql.Tests` | pipeline HTTP réel contre `mmv_v2_test` |
| `Microsoft.Extensions.Configuration.EnvironmentVariables`, `…UserSecrets` | 10.0.4 → **10.0.13** | MIT | tests PostgreSQL, `MMV.Maintenance` | alignement exigé par Mvc.Testing 10.0.13 (NU1605) |

## Traçabilité exigence → chemin

| Exigence | Chemin(s) | Statut |
|---|---|---|
| Design tokens | `apps/desktop/src/styles/tokens.css` | implémenté |
| Tailwind CSS 4 + thème = jetons MMV | `apps/desktop/vite.config.ts`, `apps/desktop/src/styles/index.css` | implémenté (16/10/2026) |
| shadcn/ui adaptés + primitives Radix | `apps/desktop/src/components/ui/`, `apps/desktop/src/lib/utils.ts`, `apps/desktop/components.json` | implémenté (16/10/2026) |
| Polices Inter / Source Sans 3 locales | `apps/desktop/src/styles/fonts.css`, `apps/desktop/public/licenses/` | implémenté (16/10/2026) |
| Conformité de la pile de style | `apps/desktop/src/styles/styles.test.ts` | implémenté (16/10/2026) |
| Login (états, clavier, ARIA ; React Hook Form + Zod) | `apps/desktop/src/login/LoginScreen.tsx`, `loginForm.ts`, `LoginScreen.css` | implémenté |
| Session volatile côté client | `apps/desktop/src/auth/AuthProvider.tsx`, `authContext.ts` | implémenté |
| Client API (POST, Bearer, 401/403/429) | `apps/desktop/src/api/client.ts`, `apps/desktop/src/api/auth.ts` | implémenté |
| Shell 260/72, 64, 32, thème, navigation (React Router), profil (Radix), déconnexion | `apps/desktop/src/shell/*` | implémenté |
| Barre d'état réelle | `apps/desktop/src/shell/StatusBar.tsx`, `statusBarView.ts`, `useServerHealth.ts` | implémenté |
| Diagnostic 01B préservé | `apps/desktop/src/views/DiagnosticView.tsx`, `apps/desktop/src/technicalStatusView.ts` | implémenté |
| Modules futurs non simulés | `apps/desktop/src/shell/navigation.ts`, `apps/desktop/src/views/UnavailableView.tsx` | implémenté |
| Identité/sessions/permissions serveur | `src/MMV.Domain/Identity/`, `src/MMV.Application/Identity/`, `src/MMV.Infrastructure/Identity/`, `src/MMV.Api/Auth/` | implémenté (tests sur doubles) |
| CORS `/api` distinct de la santé | `src/MMV.Api/Cors/DesktopCors.cs` | implémenté |
| Bootstrap administrateur local | `src/MMV.Maintenance/Program.cs` | implémenté ; exécution réelle O-02-01 |
| Cible stricte du bootstrap (mmv_v2_dev / 5433 / PG 18 / locale) | `src/MMV.Maintenance/BootstrapTargetPolicy.cs`, `tests/MMV.Maintenance.Tests/`, `tests/MMV.PostgreSql.Tests/BootstrapTargetPostgreSqlTests.cs` | implémenté, PASS (16/10/2026) |
| Dossier O-02-01 (manifeste SHA-256) | `docs/phase/02/O-02-01_MANIFEST.sha256` | prêt ; non exécuté |
| Migration d'identité + scripts relus | `src/MMV.Infrastructure/Persistence/Migrations/20261015223849_AuthIdentity*.cs`, `docs/phase/02/sql/` | générée ; **non appliquée** (O-02-01) |
| Application contrôlée des scripts | `scripts/Invoke-MmvSqlScript.ps1` | prêt ; non exécuté |
| Tests serveur | `tests/MMV.Api.Tests/Identity/`, `tests/MMV.PostgreSql.Tests/{IdentityPostgreSqlTests,AuthHttpPostgreSqlTests,SchemaInspection,SchemaStateTests}.cs` | doubles PASS ; PostgreSQL après GO |
| Banc natif | `apps/desktop/e2e/` | implémenté |
