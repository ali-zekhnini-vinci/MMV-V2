// Activation du service Rendez-vous côté client (03-B). **Désactivé** : le contrat
// (docs/phase/03/API_APPOINTMENTS_PROPOSED.md) et les décisions D-03-13 à D-03-18 sont PROPOSÉS, aucune route
// /api/appointments n'existe dans MMV.Api et aucune migration n'est appliquée. L'écran affiche alors un état
// explicite « service non activé », sans donnée ni rendez-vous fictif. Les tests fournissent `{ enabled: true }`
// avec le double de test (src/test/fakeAppointmentsApi.ts), comme le module Clients en 03-A (O-03-04).
import { createContext, useContext } from "react";

export interface AppointmentsService {
  enabled: boolean;
  /** Horloge injectable (tests) : instant courant, pour « Aujourd'hui » et le repère d'heure courante. */
  now?: () => Date;
}

/** Bascule unique : passera à `true` après GO du propriétaire (décisions, API, migration). */
export const APPOINTMENTS_API_ENABLED = false;

export const AppointmentsServiceContext = createContext<AppointmentsService>({ enabled: APPOINTMENTS_API_ENABLED });

export const useAppointmentsService = (): AppointmentsService => useContext(AppointmentsServiceContext);
