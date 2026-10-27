// Hôte RÉEL du dossier optique (04-B) : passerelle HTTP vers MMV.Api (session courante), droits d'affichage dérivés des
// permissions servies par le serveur, critères de liste conservés pendant la session. Monté par le Shell uniquement si
// le dossier optique est activé pour la session (opticalActivation.ts) ; le banc isolé garde son propre hôte en mémoire.
// Fournisseurs seulement (module léger) : les écrans sont chargés à la demande (OpticalRoute.tsx, fiche client).
import { type ReactNode, useMemo, useState } from "react";
import { createHttpOpticalGateway } from "../api/optical.ts";
import { useAuth } from "../auth/authContext.ts";
import { OpticalAccessContext, opticalAccess } from "./opticalAccess.ts";
import { OpticalGatewayContext } from "./opticalQueries.ts";
import { INITIAL_OPTICAL_LIST, type OpticalListState, OpticalUiContext } from "./opticalUiState.ts";

export function OpticalHost({ permissions, children }: { permissions: readonly string[]; children: ReactNode }) {
  const { authorized } = useAuth();
  // Une passerelle par session : elle garde les identifiants des écritures à l'issue incertaine (aucun doublon).
  const gateway = useMemo(() => createHttpOpticalGateway(authorized), [authorized]);
  const key = permissions.join("|");
  // eslint-disable-next-line react-hooks/exhaustive-deps -- recalcul sur le CONTENU des permissions, pas leur identité
  const access = useMemo(() => opticalAccess(permissions), [key]);
  const [list, setListState] = useState<OpticalListState>(INITIAL_OPTICAL_LIST);
  const ui = useMemo(() => ({ list, setList: (update: (current: OpticalListState) => OpticalListState) => setListState(update) }), [list]);
  return (
    <OpticalGatewayContext.Provider value={gateway}>
      <OpticalAccessContext.Provider value={access}>
        <OpticalUiContext.Provider value={ui}>{children}</OpticalUiContext.Provider>
      </OpticalAccessContext.Provider>
    </OpticalGatewayContext.Provider>
  );
}
