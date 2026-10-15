# MMV V2 — client Windows (Tauri 2 + React + TypeScript)

Socle technique de la phase 01A. Généré avec `npm create tauri-app@4.7.5` (template `react-ts`, npm), puis réduit :
aucune commande Rust métier, aucun plugin, aucun accès PostgreSQL, aucune donnée de magasin.

Identifiant provisoire : `com.mmv.v2.dev` / produit `MMV V2 Dev` (non définitif, voir `docs/phase/01/GATES_AND_DECISIONS.md`).

## Commandes

```sh
npm ci              # dépendances exactes depuis package-lock.json
npm run lint        # ESLint (typescript-eslint strict)
npm run check       # tsc strict (src + vite.config.ts)
npm run build       # check + build Vite (dist/)
npm run tauri dev   # fenêtre native avec rechargement
npx tauri build --debug --no-bundle   # binaire src-tauri/target/debug/mmv-desktop.exe
```

Prérequis Windows : Node 22, Rust stable `x86_64-pc-windows-msvc`, Visual Studio C++ Build Tools, WebView2.
