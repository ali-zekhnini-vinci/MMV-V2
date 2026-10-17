// Formulaire Rendez-vous (03-B) — React Hook Form + Zod. Validation d'ergonomie seulement : MMV.Api
// revalidera tout (contrat PROPOSÉ, docs/phase/03/API_APPOINTMENTS_PROPOSED.md). Aucune règle n'est déduite
// des maquettes : durées, motifs, responsables et caractère obligatoire du responsable viennent des
// paramètres SERVEUR (AppointmentOptions) ; conflits, passé et client archivé sont décidés par le serveur.
// Seules exigences locales, structurelles : un client, une date, une heure, une durée (sans quoi aucun
// créneau n'existe) et un motif (proposition D-03-18).
import { z } from "zod";
import type { AppointmentDetail, AppointmentInput, AppointmentOptions } from "../api/appointments.ts";
import { APPOINTMENT_CANCELLATION_REASON_MAX_LENGTH, APPOINTMENT_NOTES_MAX_LENGTH } from "../api/appointments.ts";
import { formatIsoDate, parseFrenchDate } from "../clients/clientForm.ts";
import { minutesBetween, utcToWall, wallTimeToUtc } from "./time.ts";

export interface AppointmentFormValues {
  clientId: string;
  /** Libellé affiché du client choisi (« Yasmine El Amrani · CLI-000123 »), jamais envoyé. */
  clientLabel: string;
  /** `jj/mm/aaaa`, dans le fuseau du magasin. */
  date: string;
  /** `hh:mm`, dans le fuseau du magasin. */
  time: string;
  /** Minutes, valeur d'une liste serveur ; `""` = non choisie. */
  durationMinutes: string;
  /** `""` = non attribué. */
  responsibleUserId: string;
  reasonCode: string;
  notes: string;
}

export const EMPTY_APPOINTMENT_FORM: AppointmentFormValues = {
  clientId: "",
  clientLabel: "",
  date: "",
  time: "",
  durationMinutes: "",
  responsibleUserId: "",
  reasonCode: "",
  notes: "",
};

export const APPOINTMENT_MESSAGES = {
  clientRequired: "Sélectionnez un client",
  dateRequired: "Date requise",
  dateFormat: "Date attendue au format jj/mm/aaaa",
  timeRequired: "Heure requise",
  timeFormat: "Heure attendue au format hh:mm",
  timeNonexistent: "Cette heure n'existe pas ce jour-là (changement d'heure)",
  durationRequired: "Choisissez une durée",
  durationNotAllowed: "Durée non proposée par le serveur",
  responsibleRequired: "Choisissez un responsable",
  responsibleUnknown: "Responsable inconnu ou inactif",
  reasonRequired: "Le motif est requis",
  reasonUnknown: "Motif non proposé par le serveur",
  notesTooLong: `${APPOINTMENT_NOTES_MAX_LENGTH} caractères au maximum`,
  cancellationTooLong: `${APPOINTMENT_CANCELLATION_REASON_MAX_LENGTH} caractères au maximum`,
  clientArchived: "Ce client est archivé : aucun nouveau rendez-vous n'est possible",
  clientUnknown: "Client introuvable",
  past: "Ce créneau est déjà passé",
};

const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

/** Schéma construit à partir des paramètres serveur (listes blanches, responsable obligatoire ou non). */
export function appointmentFormSchema(options: AppointmentOptions) {
  const durations = new Set(options.durationsMinutes.map(String));
  const reasons = new Set(options.reasons.map((reason) => reason.code));
  const responsibles = new Set(options.responsibles.map((person) => person.id));
  return z
    .object({
      clientId: z.string().min(1, APPOINTMENT_MESSAGES.clientRequired),
      clientLabel: z.string(),
      date: z
        .string()
        .trim()
        .min(1, APPOINTMENT_MESSAGES.dateRequired)
        .refine((value) => parseFrenchDate(value) !== null, APPOINTMENT_MESSAGES.dateFormat),
      time: z.string().trim().min(1, APPOINTMENT_MESSAGES.timeRequired).regex(TIME_PATTERN, APPOINTMENT_MESSAGES.timeFormat),
      durationMinutes: z
        .string()
        .min(1, APPOINTMENT_MESSAGES.durationRequired)
        .refine((value) => durations.has(value), APPOINTMENT_MESSAGES.durationNotAllowed),
      responsibleUserId: z
        .string()
        .refine((value) => value !== "" || !options.responsibleRequired, APPOINTMENT_MESSAGES.responsibleRequired)
        .refine((value) => value === "" || responsibles.has(value), APPOINTMENT_MESSAGES.responsibleUnknown),
      reasonCode: z
        .string()
        .min(1, APPOINTMENT_MESSAGES.reasonRequired)
        .refine((value) => reasons.has(value), APPOINTMENT_MESSAGES.reasonUnknown),
      notes: z.string().max(APPOINTMENT_NOTES_MAX_LENGTH, APPOINTMENT_MESSAGES.notesTooLong),
    })
    .superRefine((values, context) => {
      const date = parseFrenchDate(values.date.trim());
      if (date === null || !TIME_PATTERN.test(values.time.trim())) return;
      const zoned = wallTimeToUtc(date, values.time.trim(), options.timeZone);
      if (!zoned.ok) {
        context.addIssue({ code: "custom", path: ["time"], message: APPOINTMENT_MESSAGES.timeNonexistent });
      }
    });
}

/** Valeurs validées → corps du contrat (instant UTC calculé dans le fuseau du magasin). */
export function toAppointmentInput(values: AppointmentFormValues, options: AppointmentOptions): AppointmentInput {
  const date = parseFrenchDate(values.date.trim());
  const zoned = date === null ? null : wallTimeToUtc(date, values.time.trim(), options.timeZone);
  if (zoned === null || !zoned.ok) throw new Error("Valeurs de rendez-vous non validées.");
  const notes = values.notes.trim();
  return {
    clientId: values.clientId,
    responsibleUserId: values.responsibleUserId === "" ? null : values.responsibleUserId,
    startsAtUtc: zoned.utc,
    durationMinutes: Number(values.durationMinutes),
    reasonCode: values.reasonCode,
    notes: notes === "" ? null : notes,
  };
}

/** Rendez-vous existant → valeurs du formulaire (modification, déplacement). */
export function toAppointmentFormValues(appointment: AppointmentDetail, timeZone: string): AppointmentFormValues {
  const wall = utcToWall(appointment.startsAtUtc, timeZone);
  return {
    clientId: appointment.clientId,
    clientLabel: appointmentClientLabel(appointment),
    date: formatIsoDate(wall.date),
    time: wall.time,
    durationMinutes: String(minutesBetween(appointment.startsAtUtc, appointment.endsAtUtc)),
    responsibleUserId: appointment.responsibleUserId ?? "",
    reasonCode: appointment.reasonCode,
    notes: appointment.notes ?? "",
  };
}

/** Rendez-vous existant → corps complet (déplacement par glisser : seuls début conservé/changé). */
export function toAppointmentInputFrom(appointment: AppointmentDetail, startsAtUtc: string): AppointmentInput {
  return {
    clientId: appointment.clientId,
    responsibleUserId: appointment.responsibleUserId,
    startsAtUtc,
    durationMinutes: minutesBetween(appointment.startsAtUtc, appointment.endsAtUtc),
    reasonCode: appointment.reasonCode,
    notes: appointment.notes,
  };
}

export const appointmentClientName = (appointment: { clientFirstName: string; clientLastName: string }) =>
  `${appointment.clientFirstName} ${appointment.clientLastName}`;

export const appointmentClientLabel = (appointment: { clientFirstName: string; clientLastName: string; clientReference: string }) =>
  `${appointmentClientName(appointment)} · ${appointment.clientReference}`;

type FormField = keyof AppointmentFormValues;

const SERVER_FIELDS: Record<string, FormField> = {
  clientId: "clientId",
  startsAtUtc: "time",
  durationMinutes: "durationMinutes",
  responsibleUserId: "responsibleUserId",
  reasonCode: "reasonCode",
  notes: "notes",
};

const SERVER_MESSAGES: Partial<Record<FormField, Record<string, string>>> = {
  clientId: { required: APPOINTMENT_MESSAGES.clientRequired, not_found: APPOINTMENT_MESSAGES.clientUnknown, archived: APPOINTMENT_MESSAGES.clientArchived },
  time: { required: APPOINTMENT_MESSAGES.timeRequired, invalid_format: APPOINTMENT_MESSAGES.timeFormat, in_past: APPOINTMENT_MESSAGES.past },
  durationMinutes: { required: APPOINTMENT_MESSAGES.durationRequired, not_allowed: APPOINTMENT_MESSAGES.durationNotAllowed },
  responsibleUserId: { required: APPOINTMENT_MESSAGES.responsibleRequired, not_found: APPOINTMENT_MESSAGES.responsibleUnknown },
  reasonCode: { required: APPOINTMENT_MESSAGES.reasonRequired, not_allowed: APPOINTMENT_MESSAGES.reasonUnknown },
  notes: { too_long: APPOINTMENT_MESSAGES.notesTooLong },
};

/** Codes serveur par champ → messages bornés (jamais le texte du serveur). Champs inconnus ignorés. */
export function appointmentServerFieldErrors(fieldErrors: Record<string, string[]> | undefined): [FormField, string][] {
  if (!fieldErrors) return [];
  const result: [FormField, string][] = [];
  for (const [serverField, codes] of Object.entries(fieldErrors)) {
    const field = SERVER_FIELDS[serverField];
    if (!field) continue;
    const messages = SERVER_MESSAGES[field] ?? {};
    result.push([field, messages[codes[0]] ?? "Valeur refusée par le serveur"]);
  }
  return result;
}

/** Motif d'annulation facultatif (proposition D-03-16). */
export const cancellationSchema = z.object({
  reason: z.string().max(APPOINTMENT_CANCELLATION_REASON_MAX_LENGTH, APPOINTMENT_MESSAGES.cancellationTooLong),
});
