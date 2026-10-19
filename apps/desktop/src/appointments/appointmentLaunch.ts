// Ouverture de l'agenda depuis la fiche client (routeur mémoire du Shell), état validé à la réception (jamais de
// confiance aveugle dans `location.state`) ; l'écran d'origine est mémorisé pour y revenir après l'opération. Garde
// d'affichage uniquement : le serveur reste seul juge (droits, client archivé). Module pur.
// - 03-B2 : prise de rendez-vous, client présélectionné, aucune seconde recherche.
// - 03-D1 : détail d'un rendez-vous choisi dans l'onglet « Rendez-vous » de la fiche, agenda positionné sur sa date.
import { isCivilDate } from "./time.ts";

interface LaunchOrigin {
  /** Chemin de retour (fiche client) et nom affiché du client pour le bouton de retour. */
  returnTo: string;
  returnLabel: string;
}

export type AppointmentLaunchState = LaunchOrigin &
  (
    | {
        /** Client présélectionné : identifiant et libellé affiché (« NOM Prénom · CLI-000123 »). */
        newAppointmentFor: { clientId: string; clientLabel: string };
        openAppointment?: undefined;
      }
    | {
        /** Rendez-vous à ouvrir et date civile du magasin où positionner l'agenda. */
        openAppointment: { id: string; date: string };
        newAppointmentFor?: undefined;
      }
  );

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);

export function readAppointmentLaunch(state: unknown): AppointmentLaunchState | null {
  if (!isRecord(state)) return null;
  const { returnTo, returnLabel, newAppointmentFor, openAppointment } = state;
  if (typeof returnTo !== "string" || !returnTo.startsWith("/clients/") || typeof returnLabel !== "string") return null;
  if (isRecord(newAppointmentFor) && openAppointment === undefined) {
    const { clientId, clientLabel } = newAppointmentFor;
    if (typeof clientId !== "string" || clientId === "" || typeof clientLabel !== "string") return null;
    return { newAppointmentFor: { clientId, clientLabel }, returnTo, returnLabel };
  }
  if (isRecord(openAppointment) && newAppointmentFor === undefined) {
    const { id, date } = openAppointment;
    if (typeof id !== "string" || id === "" || typeof date !== "string" || !isCivilDate(date)) return null;
    return { openAppointment: { id, date }, returnTo, returnLabel };
  }
  return null;
}

export function appointmentLaunchFor(client: { id: string; reference: string }, name: string): AppointmentLaunchState {
  return {
    newAppointmentFor: { clientId: client.id, clientLabel: `${name} · ${client.reference}` },
    returnTo: `/clients/${client.id}`,
    returnLabel: name,
  };
}

/** Détail d'un rendez-vous de l'historique du client (03-D1). */
export function appointmentDetailLaunchFor(client: { id: string }, name: string, appointment: { id: string; date: string }): AppointmentLaunchState {
  return { openAppointment: appointment, returnTo: `/clients/${client.id}`, returnLabel: name };
}
