// Garde de sortie d'écran (modifications non enregistrées). Le Shell utilise un routeur mémoire sans
// « data router » : `useBlocker` n'est pas disponible. Un écran enregistre une garde ; avant de changer
// d'entrée de menu, le Shell la consulte. La garde renvoie `true` si elle intercepte la navigation (elle
// affiche alors sa propre confirmation et appellera `proceed` si l'utilisateur confirme).
import { createContext, useContext, useEffect, useRef } from "react";

export type LeaveGuard = (proceed: () => void) => boolean;

export interface NavigationGuardRegistry {
  register: (guard: LeaveGuard) => () => void;
}

export const NavigationGuardContext = createContext<NavigationGuardRegistry>({ register: () => () => undefined });

/** Enregistre `guard` pendant la durée de vie du composant (toujours la version la plus récente). */
export function useLeaveGuard(guard: LeaveGuard): void {
  const { register } = useContext(NavigationGuardContext);
  const latest = useRef(guard);
  useEffect(() => {
    latest.current = guard;
  });
  useEffect(() => register((proceed) => latest.current(proceed)), [register]);
}
