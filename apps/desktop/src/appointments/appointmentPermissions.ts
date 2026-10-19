// Actions Rendez-vous proposées selon les permissions de la session (03-B ; droits D-03-17 approuvés en 03-B2). Garde
// d'AFFICHAGE uniquement, comme clients/clientPermissions.ts : le serveur reste seul juge, `system.admin`
// n'accorde rien. Le sélecteur de client exige en plus `clients.read` (recherche GET /api/clients).
import { PERMISSION_APPOINTMENTS_CANCEL, PERMISSION_APPOINTMENTS_READ, PERMISSION_APPOINTMENTS_WRITE, PERMISSION_CLIENTS_READ } from "../api/auth.ts";
import { useAuth } from "../auth/authContext.ts";

export interface AppointmentPermissions {
  /** Consulter l'agenda et l'historique des rendez-vous d'un client (`appointments.read`, 03-D1). */
  canRead: boolean;
  /** Créer, modifier, déplacer (`appointments.write`). */
  canWrite: boolean;
  /** Annuler et rétablir (`appointments.cancel`). */
  canCancel: boolean;
  /** Rechercher un client pour un nouveau rendez-vous (`clients.read`). */
  canPickClient: boolean;
}

export function appointmentPermissions(permissions: readonly string[]): AppointmentPermissions {
  return {
    canRead: permissions.includes(PERMISSION_APPOINTMENTS_READ),
    canWrite: permissions.includes(PERMISSION_APPOINTMENTS_WRITE),
    canCancel: permissions.includes(PERMISSION_APPOINTMENTS_CANCEL),
    canPickClient: permissions.includes(PERMISSION_CLIENTS_READ),
  };
}

export function useAppointmentPermissions(): AppointmentPermissions {
  const { state } = useAuth();
  return appointmentPermissions(state.status === "authenticated" ? state.session.user.permissions : []);
}

export const APPOINTMENTS_READ_ONLY_HINT = "Consultation seule : votre compte ne permet pas de créer ni de modifier des rendez-vous.";
export const NO_APPOINTMENT_WRITE_TITLE = "Votre compte ne permet pas de modifier les rendez-vous (appointments.write)";
export const NO_APPOINTMENT_CANCEL_TITLE = "Votre compte ne permet pas d'annuler ni de rétablir les rendez-vous (appointments.cancel)";
