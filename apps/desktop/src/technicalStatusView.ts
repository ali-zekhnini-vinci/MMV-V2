// Libellés de l'écran de socle technique (phase 01B) dérivés de l'état réel renvoyé par MMV.Api.
// Module pur, sans React ni réseau : testé par technicalStatusView.test.ts.
import type { TechnicalStatus } from "./api/health.ts";

export type ScreenStatus = { state: "loading" } | TechnicalStatus;

export const SUMMARY: Record<ScreenStatus["state"], { label: string; tone: string }> = {
  loading: { label: "Vérification en cours…", tone: "pending" },
  "not-configured": { label: "Serveur MMV non configuré", tone: "error" },
  "api-unreachable": { label: "Serveur MMV injoignable", tone: "error" },
  "database-unavailable": { label: "Serveur MMV joignable — base de données indisponible", tone: "warning" },
  ready: { label: "Serveur MMV et base de données disponibles", tone: "ok" },
};

export function describeLive(status: ScreenStatus): string {
  switch (status.state) {
    case "loading":
      return "Vérification…";
    case "not-configured":
      return status.reason;
    case "api-unreachable":
      return status.error.status === undefined
        ? status.error.message
        : `${status.error.message} (HTTP ${status.error.status})`;
    case "database-unavailable":
    case "ready":
      return `Actif — PID ${status.live.processId}, depuis ${Math.round(status.live.uptimeSeconds)} s`;
  }
}

export function describeReady(status: ScreenStatus): string {
  switch (status.state) {
    case "loading":
      return "Vérification…";
    case "not-configured":
    case "api-unreachable":
      return "Non vérifiée (serveur injoignable)";
    case "database-unavailable":
      return status.ready
        ? `Indisponible (vérifiée en ${status.ready.durationMs} ms)`
        : `Non déterminée : ${status.error?.message ?? "erreur inconnue"}`;
    case "ready":
      return `Disponible (connexion vérifiée en ${status.ready.durationMs} ms)`;
  }
}
