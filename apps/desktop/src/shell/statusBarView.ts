// Libellés de la barre d'état dérivés de l'état RÉEL de l'API et de la session. Module pur, testé par
// statusBarView.test.ts. Chaque indicateur = icône + texte (jamais la couleur seule).
import type { TechnicalStatus } from "../api/health.ts";

export type Tone = "ok" | "warning" | "error" | "pending";

export interface Indicator {
  text: string;
  tone: Tone;
}

export function serverIndicator(status: TechnicalStatus | null, checking: boolean): Indicator {
  if (status === null) return { text: "Serveur MMV — vérification…", tone: "pending" };
  const suffix = checking ? " (vérification…)" : "";
  switch (status.state) {
    case "not-configured":
      return { text: "Serveur MMV non configuré", tone: "error" };
    case "api-unreachable":
      return { text: `Serveur MMV injoignable${suffix}`, tone: "error" };
    case "database-unavailable":
    case "ready":
      return { text: `Serveur MMV joignable${suffix}`, tone: "ok" };
  }
}

export function databaseIndicator(status: TechnicalStatus | null): Indicator {
  if (status === null) return { text: "Base de données — vérification…", tone: "pending" };
  switch (status.state) {
    case "not-configured":
    case "api-unreachable":
      return { text: "Base de données — non vérifiée", tone: "pending" };
    case "database-unavailable":
      return { text: "Base de données indisponible", tone: "warning" };
    case "ready":
      return { text: "Base de données disponible", tone: "ok" };
  }
}

/**
 * « Session active — fin au plus tard à 18:42 » (heure locale). L'échéance absolue est connue ;
 * l'inactivité prolongée peut fermer la session plus tôt (contrôlée par le serveur).
 */
export function sessionIndicator(expiresAtUtc: string, locale = "fr-FR"): Indicator {
  const expires = new Date(expiresAtUtc);
  if (Number.isNaN(expires.getTime())) return { text: "Session active", tone: "ok" };
  const time = expires.toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit" });
  return { text: `Session active — fin au plus tard à ${time}`, tone: "ok" };
}
