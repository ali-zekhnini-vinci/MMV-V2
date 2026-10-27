// Route /optique du Shell (04-B), chargée à la demande : écrans du dossier optique sous l'hôte réel (OpticalHost.tsx).
import { useCallback, useMemo, useState } from "react";
import { useLocation } from "react-router";
import { type ClientNotice, ClientsUiContext, INITIAL_LIST_STATE, type ScopedClientNotice } from "../clients/clientsUi.ts";
import { OpticalModule } from "./OpticalModule.tsx";

/** Route /optique du Shell : messages d'écran limités à l'écran visé (même règle que ClientsModule, O-03-15 / O-03-43). */
export function OpticalRoute() {
  const { pathname } = useLocation();
  const [scoped, setScoped] = useState<ScopedClientNotice | null>(null);
  const [shownPath, setShownPath] = useState(pathname);
  if (shownPath !== pathname) {
    setShownPath(pathname);
    if (scoped !== null && scoped.path !== pathname) setScoped(null);
  }
  const notice = scoped !== null && scoped.path === pathname ? scoped : null;
  const setNotice = useCallback((next: ClientNotice | null, path?: string) => setScoped(next === null ? null : { ...next, path: path ?? pathname }), [pathname]);
  const clientsUi = useMemo(() => ({ listState: INITIAL_LIST_STATE, setListState: () => undefined, notice, setNotice }), [notice, setNotice]);
  return (
    <ClientsUiContext.Provider value={clientsUi}>
      <OpticalModule />
    </ClientsUiContext.Provider>
  );
}
