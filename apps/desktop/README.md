# MMV V2 — client Windows (Tauri 2 + React + TypeScript)

Socle technique de la phase 01A. Généré avec `npm create tauri-app@4.7.5` (template `react-ts`, npm), puis réduit :
aucune commande Rust métier, aucun plugin, aucun accès PostgreSQL, aucune donnée de magasin.

Identifiant provisoire : `com.mmv.v2.dev` / produit `MMV V2 Dev` (non définitif, voir `docs/phase/01/GATES_AND_DECISIONS.md`).

## Commandes

```sh
npm ci              # dépendances exactes depuis package-lock.json
npm run lint        # ESLint (typescript-eslint strict)
npm test            # Vitest (node + jsdom), doubles node:http locaux
npm run check       # tsc strict (src + vite.config.ts)
npm run build       # check + build Vite (dist/)
npm run tauri dev   # fenêtre native avec rechargement
npx tauri build --debug --no-bundle   # binaire src-tauri/target/debug/mmv-desktop.exe
```

Prérequis Windows : Node 22, Rust stable `x86_64-pc-windows-msvc`, Visual Studio C++ Build Tools, WebView2.

Prérequis Node : **22.22 ou plus** (exigence de `react-router` 8).

## Pile frontend (phase 02, installée ET utilisée — 16/10/2026)

| Technologie | Version | Utilisation réelle |
|---|---|---|
| Tailwind CSS 4 + `@tailwindcss/vite` | 4.3.3 | `vite.config.ts` ; entrée `src/styles/index.css` (`@import "tailwindcss"`) ; thème `@theme inline reference` = jetons MMV de `tokens.css` (palettes, tailles, rayons, ombres et polices par défaut retirés) |
| shadcn/ui (copie locale adaptée, `components.json`) | style new-york, sans thème par défaut | `src/components/ui/{button,label,dropdown-menu}.tsx`, `src/lib/utils.ts` (`cn`) |
| Radix (`react-slot` 1.5.0, `react-label` 2.1.17, `react-dropdown-menu` 2.2.1) | — | `Button asChild`, étiquettes du login, menu profil (`src/shell/ProfileMenu.tsx`) |
| `class-variance-authority` 0.7.1, `clsx` 2.1.1, `tailwind-merge` 3.7.0 | — | variantes et fusion de classes des composants shadcn/ui |
| React Hook Form 7.90.0 + Zod 4.6.7 + `@hookform/resolvers` 5.9.1 | — | `src/login/LoginScreen.tsx`, schéma `loginSchema` de `src/login/loginForm.ts` |
| React Router 8.4.0 | — | `src/shell/Shell.tsx` : routeur mémoire, une route par entrée (`navigation.ts`, `path`), route inconnue → Accueil |
| Lucide React 1.49.0 | — | icônes (inchangé) |
| Inter / Source Sans 3 (`@fontsource-variable/*` 5.3.0 / 5.3.1, SIL OFL 1.1) | — | `src/styles/fonts.css` : woff2 intégrés au bundle, aucun Google Fonts ; licences copiées dans `public/licenses/` |

Le CSS spécifique au design (login, shell : grille 260/72/64/32, dégradé, infobulles du rail) reste dans `src/login/LoginScreen.css` et `src/shell/Shell.css`, **hors couche** : il prime sur les utilitaires Tailwind et sur le preflight. Ajout d'un composant shadcn : `npx shadcn add <composant>` puis remplacement des classes du thème par défaut (`bg-primary`, `text-muted-foreground`…) par les jetons MMV (`bg-accent`, `text-text-secondary`…) — un test (`src/styles/styles.test.ts`) refuse les variables du thème shadcn par défaut.

Non installés volontairement (aucun cas d'usage réel en phase 02) : TanStack Query/Table, Sonner, Zustand, FullCalendar, Recharts.
