// Fournisseur temps réel de la session (COR-05-02), monté une fois dans le Shell : une seule connexion pour tous
// les modules, ouverte tant que la session et le module concerné sont actifs. Chaque notification invalide les
// requêtes TanStack des chemins associés à ses sujets (`topicQueries`), qui se relisent depuis l'API ; les saisies en
// cours ne sont jamais remplacées (formulaires à version figée, `useEditBase`). Reconnexion ou redémarrage du serveur :
// relecture de toutes les données du module. Architecture réutilisable : un module futur ajoute ses sujets à la table.
import { useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useEffect, useMemo, useState } from "react";
import { useAuth } from "../auth/authContext.ts";
import { type RealtimeStatus, startRealtime } from "./realtimeClient.ts";
import { matchesTopics, RealtimeContext, type RealtimeRouting } from "./realtimeContext.ts";

export function RealtimeProvider({ enabled, routing, children }: { enabled: boolean; routing: RealtimeRouting; children: ReactNode }) {
  const { baseUrl, authorized } = useAuth();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<RealtimeStatus>("disconnected");
  const [lastEventAt, setLastEventAt] = useState<string | null>(null);
  const hubUrl = baseUrl.ok ? new URL("/hubs/changes", baseUrl.url).toString() : null;

  useEffect(() => {
    if (!enabled || hubUrl === null) return undefined;
    return startRealtime({
      hubUrl,
      token: async () => {
        // Jeton courant lu sans requête : l'appel « autorisé » renvoie immédiatement ce que le fournisseur détient.
        const result = await authorized(async (_url, token) => ({ ok: true as const, status: 200, data: token }));
        return result.ok ? result.data : "";
      },
      onStatus: setStatus,
      onChange: (change) => {
        setLastEventAt(change.atUtc);
        void queryClient.invalidateQueries({ predicate: (query) => matchesTopics(query.queryKey, routing, change.topics) });
      },
      onResync: () => void queryClient.invalidateQueries({ queryKey: [routing.keyRoot] }),
    });
  }, [enabled, hubUrl, authorized, queryClient, routing]);

  const value = useMemo(() => ({ status: enabled ? status : ("off" as const), lastEventAt }), [enabled, status, lastEventAt]);
  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
