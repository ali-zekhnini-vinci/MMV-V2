// Contexte du temps réel (séparé du fournisseur pour Fast Refresh). `off` : module sans temps réel pour la session.
import type { QueryKey } from "@tanstack/react-query";
import { createContext, useContext } from "react";
import type { RealtimeStatus } from "./realtimeClient.ts";

export interface RealtimeValue {
  status: RealtimeStatus | "off";
  lastEventAt: string | null;
}

export const RealtimeContext = createContext<RealtimeValue>({ status: "off", lastEventAt: null });

export const useRealtime = () => useContext(RealtimeContext);

export const REALTIME_LABELS: Record<RealtimeValue["status"], string> = {
  connecting: "Connexion en cours…",
  connected: "Connecté — mises à jour en temps réel",
  reconnecting: "Reconnexion en cours…",
  disconnected: "Déconnecté — données potentiellement obsolètes",
  off: "Temps réel indisponible",
};

/** Racine de clé TanStack et préfixes de chemins d'API relus pour chaque sujet. */
export interface RealtimeRouting {
  keyRoot: string;
  topicQueries: Record<string, readonly string[]>;
}

export function matchesTopics(key: QueryKey, routing: RealtimeRouting, topics: readonly string[]): boolean {
  if (key[0] !== routing.keyRoot || typeof key[1] !== "string") return false;
  const path = key[1];
  return topics.some((topic) => (routing.topicQueries[topic] ?? []).some((prefix) => path.startsWith(prefix)));
}
