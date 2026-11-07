// Point d'entrée du BANC GATE B (import-wizard-bench.html), servi par `vite` en développement seulement : `vite build` ne
// construit que index.html, donc ni ce fichier ni la maquette de l'assistant n'entrent dans l'application Tauri.
// Maquette PUREMENT présentationnelle : aucune requête réseau, aucune API appelée, données fictives figées (fixtures.ts).
// Paramètres d'URL (captures) : state=<identifiant d'état>, theme=light|dark, history=open.
import React from "react";
import ReactDOM from "react-dom/client";
import "../../styles/index.css";
import "../../App.css";
import { ImportWizardBench } from "./ImportWizardBench.tsx";
import { BENCH_STATES } from "./fixtures.ts";

const params = new URLSearchParams(window.location.search);
const wanted = params.get("state");

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <ImportWizardBench
      initialState={BENCH_STATES.some((s) => s.id === wanted) ? (wanted as string) : BENCH_STATES[0].id}
      theme={params.get("theme") === "dark" ? "dark" : "light"}
      historyOpen={params.get("history") === "open"}
    />
  </React.StrictMode>,
);
