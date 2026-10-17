// Prise de rendez-vous depuis la fiche client (03-B2, GO § 2) : état de navigation transmis à `/rendez-vous`
// (routeur mémoire du Shell). Le client choisi est conservé — aucune seconde recherche — et l'écran d'origine est
// mémorisé pour y revenir après l'opération. Garde d'affichage uniquement : le serveur reste seul juge (droits,
// client archivé). Module pur, état validé à la réception (jamais de confiance aveugle dans `location.state`).

export interface AppointmentLaunchState {
  /** Client présélectionné : identifiant et libellé affiché (« Prénom Nom · CLI-000123 »). */
  newAppointmentFor: { clientId: string; clientLabel: string };
  /** Chemin de retour (fiche client) et nom affiché du client pour le bouton de retour. */
  returnTo: string;
  returnLabel: string;
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);

export function readAppointmentLaunch(state: unknown): AppointmentLaunchState | null {
  if (!isRecord(state) || !isRecord(state.newAppointmentFor)) return null;
  const { clientId, clientLabel } = state.newAppointmentFor;
  const { returnTo, returnLabel } = state;
  if (
    typeof clientId !== "string" ||
    clientId === "" ||
    typeof clientLabel !== "string" ||
    typeof returnTo !== "string" ||
    !returnTo.startsWith("/clients/") ||
    typeof returnLabel !== "string"
  ) {
    return null;
  }
  return { newAppointmentFor: { clientId, clientLabel }, returnTo, returnLabel };
}

export function appointmentLaunchFor(client: { id: string; reference: string }, name: string): AppointmentLaunchState {
  return {
    newAppointmentFor: { clientId: client.id, clientLabel: `${name} · ${client.reference}` },
    returnTo: `/clients/${client.id}`,
    returnLabel: name,
  };
}
