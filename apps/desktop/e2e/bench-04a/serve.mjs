// Serveur Vite ISOLÉ du banc 04-A (développement seulement) : port 1520, boucle locale, cache de dépendances dans le
// dossier temporaire du système — n'altère ni le cache ni le serveur 1420 éventuellement lancé par `tauri dev`.
// Usage : node e2e/bench-04a/serve.mjs   puis   http://127.0.0.1:1520/optical-bench.html
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const desktop = fileURLToPath(new URL("../..", import.meta.url));
const { createServer } = await import(pathToFileURL(join(desktop, "node_modules/vite/dist/node/index.js")).href);
const server = await createServer({
  configFile: join(desktop, "vite.config.ts"),
  root: desktop,
  cacheDir: process.argv[2] ?? join(tmpdir(), "mmv-v2-bench-04a-vite"),
  server: { port: 1520, strictPort: true, host: "127.0.0.1" },
  clearScreen: false,
});
await server.listen();
server.printUrls();
