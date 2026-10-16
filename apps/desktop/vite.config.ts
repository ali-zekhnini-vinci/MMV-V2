import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import process from "node:process";
import { readFileSync } from "node:fs";
const host = process.env.TAURI_DEV_HOST;
const { version } = JSON.parse(readFileSync(new URL("./package.json", import.meta.url), "utf8")) as { version: string };

// https://vite.dev/config/
export default defineConfig(() => ({
  // Tailwind CSS 4 (plugin Vite officiel) : thème = jetons MMV (src/styles/index.css), aucun thème shadcn par défaut.
  plugins: [react(), tailwindcss()],

  // Version réelle du paquet client, affichée dans la barre d'état (aucun numéro figé à la main).
  define: { __APP_VERSION__: JSON.stringify(version) },

  // Tests (npm test) : environnement node par défaut ; les tests d'écran (*.test.tsx) déclarent
  // `@vitest-environment jsdom`. Aucun service externe : doubles node:http locaux (src/test/).
  test: {
    include: ["src/**/*.test.{ts,tsx}"],
    environment: "node",
    // Compléments jsdom minimaux pour les primitives Radix (ResizeObserver) ; sans effet en environnement node.
    setupFiles: ["src/test/setup.ts"],
  },

  // Vite options tailored for Tauri development and only applied in `tauri dev` or `tauri build`
  //
  // 1. prevent Vite from obscuring rust errors
  clearScreen: false,
  // 2. tauri expects a fixed port, fail if that port is not available
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host
      ? {
          protocol: "ws",
          host,
          port: 1421,
        }
      : undefined,
    watch: {
      // 3. tell Vite to ignore watching `src-tauri`
      ignored: ["**/src-tauri/**"],
    },
  },
}));
