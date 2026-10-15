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
| `_ds/…/tokens/fonts.css` | `6d834a2b…` | **non repris** (`@import` Google Fonts distant) |

## Assets embarqués dans le client (copie à l'identique, provenance)

| Fichier versionné | Source | SHA-256 |
|---|---|---|
| `apps/desktop/src/assets/brand/logo-login.png` (300×132) | `MMV-Login-Handoff/logo-login.png` (= `MMV-Shell-Handoff/assets/logo-dark.png`) | `3df57decabf76455374f0051b6dec006dffe8b55ad7aeabd6b6e3cce8e3856cb` |
| `apps/desktop/src/assets/brand/logo-horizontal.png` (630×140) | `MMV-Shell-Handoff/assets/logo-horizontal.png` | `3570c6f9f0a08f60428e8fe1be6d426c0d9e4f9c5ce65eb7b128a311f1913a71` |
| `apps/desktop/src/assets/brand/logo-horizontal-dark-sidebar.png` (630×140) | `MMV-Shell-Handoff/assets/logo-horizontal-dark-sidebar.png` | `3ec0e0708dc800288eee0de7a339c5e9997aed83808c8076f0c7b30e42f1d03e` |
| `apps/desktop/src/assets/brand/logo-monogram.png` (232×128) | `MMV-Shell-Handoff/assets/logo-monogram.png` | `28cfa5299a2aa4c2326900b1219fcb0e85b1759749afe08b645cb4f54194402e` |

Logos de la marque MMV fournis par le propriétaire ; droits de redistribution à confirmer (O-02-06). Aucune police, aucun JavaScript ni HTML de référence importé dans le runtime.

## Dépendances ajoutées (npm, versions exactes)

| Paquet | Version | Licence | Portée | Justification |
|---|---|---|---|---|
| `lucide-react` | 1.49.0 | ISC | runtime | icônes Lucide prescrites par le design system, locales |
| `jsdom` | 30.1.1 | MIT | dev | environnement DOM des tests d'écran |
| `@testing-library/react` | 16.3.3 | MIT | dev | rendu React en test |
| `@testing-library/dom` | 10.4.2 | MIT | dev | pair requis |
| `@testing-library/user-event` | 14.6.7 | MIT | dev | clavier/clic au niveau DOM |

Versions publiées depuis ≥ 2 semaines au 16/10/2026.

## Traçabilité exigence → chemin

| Exigence | Chemin(s) | Statut |
|---|---|---|
| Design tokens | `apps/desktop/src/styles/tokens.css` | implémenté |
| Login (états, clavier, ARIA) | `apps/desktop/src/login/LoginScreen.tsx`, `loginForm.ts`, `LoginScreen.css` | implémenté |
| Session volatile côté client | `apps/desktop/src/auth/AuthProvider.tsx`, `authContext.ts` | implémenté |
| Client API (POST, Bearer, 401/403/429) | `apps/desktop/src/api/client.ts`, `apps/desktop/src/api/auth.ts` | implémenté |
| Shell 260/72, 64, 32, thème, navigation, profil, déconnexion | `apps/desktop/src/shell/*` | implémenté |
| Barre d'état réelle | `apps/desktop/src/shell/StatusBar.tsx`, `statusBarView.ts`, `useServerHealth.ts` | implémenté |
| Diagnostic 01B préservé | `apps/desktop/src/views/DiagnosticView.tsx`, `apps/desktop/src/technicalStatusView.ts` | implémenté |
| Modules futurs non simulés | `apps/desktop/src/shell/navigation.ts`, `apps/desktop/src/views/UnavailableView.tsx` | implémenté |
| Identité/sessions/permissions serveur | `src/MMV.Domain/Identity/`, `src/MMV.Application/Identity/`, `src/MMV.Infrastructure/Identity/`, `src/MMV.Api/Auth/` | à venir (02-B) |
| Bootstrap administrateur local | `src/MMV.Maintenance/` | à venir (02-B) |
