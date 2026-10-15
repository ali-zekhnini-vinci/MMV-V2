import { useCallback, useEffect, useRef, useState } from "react";
import { apiBaseUrl } from "./api/config.ts";
import { checkTechnicalStatus } from "./api/health.ts";
import { describeLive, describeReady, type ScreenStatus, SUMMARY } from "./technicalStatusView.ts";
import "./App.css";

// Écran de socle technique (phase 01B, temporaire) : état réel de MMV.Api (/health/live) et de la
// base PostgreSQL (/health/ready). Aucune donnée de magasin, aucun écran Login/Shell.
function useTechnicalStatus() {
  const [status, setStatus] = useState<ScreenStatus>(() =>
    apiBaseUrl.ok ? { state: "loading" } : { state: "not-configured", reason: apiBaseUrl.reason },
  );
  const inFlight = useRef<AbortController | null>(null);

  const reload = useCallback(async () => {
    if (!apiBaseUrl.ok) return;
    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;
    setStatus({ state: "loading" });
    const next = await checkTechnicalStatus(apiBaseUrl.url, controller.signal);
    if (!controller.signal.aborted) setStatus(next);
  }, []);

  useEffect(() => {
    // Vérification unique au montage ; ensuite uniquement sur action explicite (pas de sondage).
    const initial = window.setTimeout(() => void reload(), 0);
    return () => {
      window.clearTimeout(initial);
      inFlight.current?.abort();
    };
  }, [reload]);

  return { status, reload };
}

function App() {
  const { status, reload } = useTechnicalStatus();
  const summary = SUMMARY[status.state];

  return (
    <main className="socle">
      <header className="socle__header">
        <h1 className="socle__title">MMV V2</h1>
        <span className="socle__badge">Socle technique</span>
      </header>

      <p className="socle__lead">
        Environnement de développement de la phase 01B. Cet écran temporaire affiche l'état réel du
        serveur MMV et de sa base de données.
      </p>

      <p className={`socle__status socle__status--${summary.tone}`} role="status" aria-live="polite">
        {summary.label}
      </p>

      <dl className="socle__facts">
        <div>
          <dt>Serveur MMV (processus, /health/live)</dt>
          <dd>{describeLive(status)}</dd>
        </div>
        <div>
          <dt>Base de données (/health/ready)</dt>
          <dd>{describeReady(status)}</dd>
        </div>
        {apiBaseUrl.ok && (
          <div>
            <dt>Adresse du serveur (développement)</dt>
            <dd>{apiBaseUrl.url.origin}</dd>
          </div>
        )}
      </dl>

      <button
        type="button"
        className="socle__reload"
        onClick={() => void reload()}
        disabled={status.state === "loading" || status.state === "not-configured"}
      >
        Revérifier
      </button>
    </main>
  );
}

export default App;
