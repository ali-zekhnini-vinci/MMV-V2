// Diagnostic technique : écran de socle de la phase 01B conservé (mêmes libellés et mêmes
// endpoints anonymes /health/live et /health/ready), désormais monté dans le shell.
import type { ApiBaseUrlResult } from "../api/config.ts";
import type { ServerHealth } from "../shell/useServerHealth.ts";
import { describeLive, describeReady, type ScreenStatus, SUMMARY } from "../technicalStatusView.ts";

interface DiagnosticViewProps {
  baseUrl: ApiBaseUrlResult;
  health: ServerHealth;
}

export function DiagnosticView({ baseUrl, health }: DiagnosticViewProps) {
  const status: ScreenStatus = health.status === null || health.checking ? { state: "loading" } : health.status;
  const summary = SUMMARY[status.state];

  return (
    <section className="view" aria-labelledby="diagnostic-title">
      <header className="view__header">
        <h1 id="diagnostic-title" className="view__title">
          Diagnostic technique
        </h1>
        <p className="view__lead">État réel du serveur MMV et de sa base de données (environnement de développement).</p>
      </header>

      <div className="card">
        <p className={`diagnostic__status diagnostic__status--${summary.tone}`} role="status" aria-live="polite">
          {summary.label}
        </p>

        <dl className="diagnostic__facts">
          <div>
            <dt>Serveur MMV (processus, /health/live)</dt>
            <dd>{describeLive(status)}</dd>
          </div>
          <div>
            <dt>Base de données (/health/ready)</dt>
            <dd>{describeReady(status)}</dd>
          </div>
          {baseUrl.ok && (
            <div>
              <dt>Adresse du serveur (développement)</dt>
              <dd>{baseUrl.url.origin}</dd>
            </div>
          )}
        </dl>

        <button
          type="button"
          className="button button--secondary"
          onClick={() => void health.reload()}
          disabled={health.checking || !baseUrl.ok}
        >
          Revérifier
        </button>
      </div>
    </section>
  );
}
