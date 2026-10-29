// Activation contrôlée du dossier optique RÉEL (phase 04-B, mandat GO du 27/10/2026). Deux conditions cumulatives :
// 1. l'interrupteur de build `VITE_MMV_OPTICAL_MODULE=enabled` (absent par défaut : aucune construction ordinaire ne
//    l'active) — parcours de test explicitement activé, jusqu'à la fin de 04-C et la validation du propriétaire ;
// 2. une session portant `clients.read` ET `optical.read`, droits attribués compte par compte par le propriétaire.
// Sinon, l'application garde l'onglet « Ordonnances — phase ultérieure » et l'entrée de menu reportée (O-04-16). Garde
// d'AFFICHAGE seulement : MMV.Api refuse toute route /api/optical sans les droits, quel que soit l'interrupteur.
import { createContext, useContext } from "react";
import { opticalAccess } from "./opticalAccess.ts";

/** Interrupteur lu au build par Vite (`env` absent hors Vite, ex. tests `node --test`) ; relu à chaque appel. */
export function isOpticalModuleActive(permissions: readonly string[], moduleSwitch: string | undefined = import.meta.env?.VITE_MMV_OPTICAL_MODULE): boolean {
  return moduleSwitch === "enabled" && opticalAccess(permissions).canRead;
}

/** Dossier optique monté par le Shell pour cette session (fiche client : onglet « Dossier optique »). */
export const OpticalActivationContext = createContext(false);

export const useOpticalActivation = (): boolean => useContext(OpticalActivationContext);
