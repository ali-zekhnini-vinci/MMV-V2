// Point d'entrée du BANC 04-A (optical-bench.html), servi par `vite` en développement seulement : `vite build` ne
// construit que index.html, donc ni ce fichier ni les prototypes optiques n'entrent dans l'application Tauri.
// La garde réseau est installée AVANT le premier rendu : aucune requête ne peut sortir de la page.
// Paramètres d'URL (captures) : route, profile, theme, latency, step (pas de démonstration des boutons ±).
import React from "react";
import ReactDOM from "react-dom/client";
import "../styles/index.css";
import "../App.css";
import { installBenchNetwork } from "./benchNetwork.ts";
import { createBenchClients } from "./benchSeed.ts";
import { OpticalBench } from "./OpticalBench.tsx";

const clients = createBenchClients();
const network = installBenchNetwork(() => clients);
const params = new URLSearchParams(window.location.search);
const route = params.get("route");

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <OpticalBench
      clients={clients}
      network={network}
      initialRoute={route && route.startsWith("/") ? route : "/"}
      profileId={params.get("profile") ?? "all"}
      theme={params.get("theme") === "dark" ? "dark" : "light"}
      latencyMs={Number(params.get("latency")) || 0}
      step={Number(params.get("step")) > 0 ? Number(params.get("step")) : undefined}
    />
  </React.StrictMode>,
);
