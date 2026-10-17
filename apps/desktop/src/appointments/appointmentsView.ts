// Présentation des rendez-vous (03-B) : libellés, tonalités d'affichage, messages d'échec bornés. Module pur.
// Les messages traduisent des CODES du contrat proposé, jamais un texte serveur.
import type { AppointmentOptions, AppointmentSummary } from "../api/appointments.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { formatNumericDate, formatTimeRange, utcToWall } from "./time.ts";

/** Cinq teintes d'agenda (Lot 2, planches 28–31) attribuées par POSITION du motif dans la liste serveur :
 * repère visuel seulement, aucun sens métier attaché à une couleur. */
export const EVENT_TONES = 5;

export function reasonTone(options: AppointmentOptions, reasonCode: string): number {
  const index = options.reasons.findIndex((reason) => reason.code === reasonCode);
  return index < 0 ? -1 : index % EVENT_TONES;
}

export function reasonLabel(options: AppointmentOptions, reasonCode: string): string {
  return options.reasons.find((reason) => reason.code === reasonCode)?.label ?? "Motif non répertorié";
}

export const STATUS_LABEL = { planned: "Planifié", cancelled: "Annulé" } as const;

/** « Y. El Amrani » (vue semaine, colonnes étroites). */
export const shortClientName = (appointment: Pick<AppointmentSummary, "clientFirstName" | "clientLastName">) =>
  `${appointment.clientFirstName.charAt(0).toLocaleUpperCase("fr")}. ${appointment.clientLastName}`;

export const clientInitials = (appointment: Pick<AppointmentSummary, "clientFirstName" | "clientLastName">) =>
  `${appointment.clientFirstName.charAt(0)}${appointment.clientLastName.charAt(0)}`.toLocaleUpperCase("fr");

/** « 16/07/2026 à 10:30 » dans le fuseau du magasin. */
export function formatWhen(startsAtUtc: string, timeZone: string): string {
  const wall = utcToWall(startsAtUtc, timeZone);
  return `${formatNumericDate(wall.date)} à ${wall.time}`;
}

export interface FailureView {
  tone: "error" | "info" | "warning";
  title: string;
  text: string;
  /** Conflit horaire (planches 34, 42) : rendu « Conflit horaire ». */
  slotConflict?: boolean;
}

/** Échec d'une écriture → message borné. `slot` = créneau demandé, pour le conflit horaire. */
export function describeAppointmentFailure(error: ApiRequestError, slot?: { startsAtUtc: string; endsAtUtc: string; timeZone: string }): FailureView {
  const { kind, code, message } = error.error;
  if (kind === "conflict") {
    switch (code) {
      case "appointment_conflict":
        return {
          tone: "error",
          title: "Conflit horaire",
          text: slot
            ? `Le créneau ${formatTimeRange(slot.startsAtUtc, slot.endsAtUtc, slot.timeZone)} chevauche un autre rendez-vous de ce responsable.`
            : "Ce créneau chevauche un autre rendez-vous de ce responsable.",
          slotConflict: true,
        };
      case "client_archived":
        return { tone: "warning", title: "Client archivé", text: "Ce client est archivé : le serveur MMV refuse cette opération." };
      case "appointment_past":
        return { tone: "warning", title: "Rendez-vous passé", text: "Ce rendez-vous est terminé : le serveur MMV refuse sa modification." };
      case "appointment_cancelled":
        return { tone: "info", title: "Rendez-vous annulé", text: "Ce rendez-vous a été annulé entre-temps. Les données ont été actualisées." };
      case "appointment_not_cancelled":
        return { tone: "info", title: "Rendez-vous déjà actif", text: "Ce rendez-vous a déjà été rétabli. Les données ont été actualisées." };
      default:
        return {
          tone: "error",
          title: "Action impossible",
          text: "Ce rendez-vous a été modifié depuis un autre poste. Les données ont été actualisées. Veuillez réessayer.",
        };
    }
  }
  if (kind === "forbidden") return { tone: "info", title: "Action non autorisée", text: "Le serveur MMV a refusé cette action pour votre compte." };
  if (kind === "not-found") return { tone: "error", title: "Rendez-vous introuvable", text: "Ce rendez-vous n'existe plus sur le serveur MMV. Les données ont été actualisées." };
  if (kind === "validation") return { tone: "error", title: "Données refusées", text: "Le serveur MMV a refusé les données saisies. Vérifiez les champs signalés." };
  return { tone: "error", title: "Erreur technique", text: `${message} Aucune modification n'a été enregistrée.` };
}
