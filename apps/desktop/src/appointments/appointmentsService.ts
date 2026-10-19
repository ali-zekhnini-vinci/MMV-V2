// Activation du service Rendez-vous côté client. 03-B : désactivé (contrat et décisions proposés). 03-B2
// (17/10/2026, D-03-13 à D-03-18 approuvées, routes /api/appointments implémentées) : **activé**. Tant que la
// migration 0004 n'est pas appliquée et que les paramètres du magasin (section serveur « Appointments ») ne sont pas
// configurés, le serveur répond 503 `appointments_not_configured` et l'écran l'indique, sans donnée fictive.
// L'état « service non activé » reste un interrupteur de repli testé (O-03-04). Les tests fournissent le double
// src/test/fakeAppointmentsApi.ts et une horloge fixe.
import { createContext, useContext } from "react";

export interface AppointmentsService {
  enabled: boolean;
  /** Horloge injectable (tests) : instant courant, pour « Aujourd'hui » et le repère d'heure courante. */
  now?: () => Date;
}

/** Bascule unique (O-03-04) : `true` depuis 03-B2. */
export const APPOINTMENTS_API_ENABLED = true;

export const AppointmentsServiceContext = createContext<AppointmentsService>({ enabled: APPOINTMENTS_API_ENABLED });

export const useAppointmentsService = (): AppointmentsService => useContext(AppointmentsServiceContext);
