// PROTOTYPE 04-A — critères de la liste globale conservés pendant la session (retour du détail sans nouvelle
// recherche), comme clientsUi.ts. Rien n'est persisté. Fournis par l'hôte du module (en 04-A : le banc isolé).
import { createContext, useContext } from "react";
import { OPTICAL_PAGE_SIZE, type OpticalListParams } from "./opticalContract.ts";

export interface OpticalListState extends Omit<OpticalListParams, "pageSize"> {
  /** D-04-13.b à trancher : résumé OD/OG dans la liste (maquette) ou détail seulement (minimisation). */
  showSummary: boolean;
}

export const INITIAL_OPTICAL_LIST: OpticalListState = {
  search: "",
  kind: "all",
  status: "all",
  includeInvalidated: false,
  sort: "date:desc",
  page: 1,
  showSummary: false,
};

export const toOpticalParams = (state: OpticalListState): OpticalListParams => ({
  search: state.search,
  kind: state.kind,
  status: state.status,
  includeInvalidated: state.includeInvalidated,
  sort: state.sort,
  page: state.page,
  pageSize: OPTICAL_PAGE_SIZE,
});

export interface OpticalUi {
  list: OpticalListState;
  setList: (update: (current: OpticalListState) => OpticalListState) => void;
}

export const OpticalUiContext = createContext<OpticalUi | null>(null);

export function useOpticalUi(): OpticalUi {
  const value = useContext(OpticalUiContext);
  if (value === null) throw new Error("useOpticalUi doit être utilisé sous un fournisseur OpticalUiContext.");
  return value;
}
