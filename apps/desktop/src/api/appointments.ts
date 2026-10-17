// Contrat HTTP Rendez-vous — PROPOSÉ (sous-lot 03-B, 17/10/2026), NON implémenté par MMV.Api.
// Référence : docs/phase/03/API_APPOINTMENTS_PROPOSED.md. Les décisions D-03-13 à D-03-18 sont `PROPOSED` :
// tant que le propriétaire ne les a pas ratifiées, aucune route n'existe côté serveur et le service est
// désactivé côté client (appointments/appointmentsService.ts). Les règles qui dépendent de ces décisions
// (fuseau, durées, motifs, responsables, conflits, passé) sont fournies ou décidées par le SERVEUR :
// l'interface ne les déduit pas des maquettes. Parseurs stricts = garde à l'exécution.
import { type ApiResult, requestJson } from "./client.ts";

export type AppointmentStatus = "planned" | "cancelled";

/** Motif proposé par le serveur (code stable + libellé affiché), D-03-18. */
export interface AppointmentReason {
  code: string;
  label: string;
}

/** Compte MMV actif pouvant être responsable d'un rendez-vous (D-03-14) : identifiant et nom affiché seulement. */
export interface AppointmentResponsible {
  id: string;
  displayName: string;
}

/** Paramètres servis par MMV.Api : l'interface n'en code aucun en dur. */
export interface AppointmentOptions {
  /** Fuseau IANA du magasin (D-03-13) : affichage et saisie dans ce fuseau, stockage UTC. */
  timeZone: string;
  /** Durées autorisées en minutes (D-03-13), sans valeur par défaut. */
  durationsMinutes: number[];
  reasons: AppointmentReason[];
  responsibles: AppointmentResponsible[];
  /** D-03-14 : responsable obligatoire ou facultatif. */
  responsibleRequired: boolean;
  /** Plage horaire AFFICHÉE (`HH:mm`), non bloquante (D-03-13). */
  displayStartTime: string;
  displayEndTime: string;
  /** Jours masqués de la grille (0 = dimanche … 6 = samedi), affichage seulement. */
  hiddenWeekdays: number[];
}

/** Ligne d'agenda : aucune coordonnée du client (minimisation, comme D-03-10). */
export interface AppointmentSummary {
  id: string;
  clientId: string;
  clientReference: string;
  clientFirstName: string;
  clientLastName: string;
  clientArchived: boolean;
  responsibleUserId: string | null;
  responsibleDisplayName: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  reasonCode: string;
  status: AppointmentStatus;
  /** Jeton de concurrence optimiste (xmin PostgreSQL), renvoyé tel quel à chaque écriture. */
  version: number;
}

export interface AppointmentDetail extends AppointmentSummary {
  /** Notes internes (≤ 1000, consigne : aucune donnée de santé). */
  notes: string | null;
  cancelledAtUtc: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AppointmentInput {
  clientId: string;
  responsibleUserId: string | null;
  /** Instant de début ISO 8601 UTC (`…Z`), calculé dans le fuseau du magasin. */
  startsAtUtc: string;
  durationMinutes: number;
  reasonCode: string;
  notes: string | null;
}

export type AppointmentStatusFilter = "planned" | "all";

export interface AppointmentRange {
  /** Bornes ISO 8601 UTC, `to` exclu ; écart maximal {@link APPOINTMENT_MAX_RANGE_DAYS} jours. */
  fromUtc: string;
  toUtc: string;
  status: AppointmentStatusFilter;
}

export const APPOINTMENT_MAX_RANGE_DAYS = 31;
export const APPOINTMENT_NOTES_MAX_LENGTH = 1000;
export const APPOINTMENT_CANCELLATION_REASON_MAX_LENGTH = 200;

/** Codes 409 du contrat proposé (corps `{ code }`), chacun traduit par un message borné côté écran. */
export const APPOINTMENT_CONFLICT_CODES = [
  "version_conflict",
  "appointment_conflict",
  "client_archived",
  "appointment_cancelled",
  "appointment_not_cancelled",
  "appointment_past",
] as const;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const isNullableString = (value: unknown): value is string | null => value === null || typeof value === "string";
const isVersion = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

export function parseAppointmentSummary(value: unknown): AppointmentSummary | null {
  if (!isRecord(value)) return null;
  const {
    id,
    clientId,
    clientReference,
    clientFirstName,
    clientLastName,
    clientArchived,
    responsibleUserId,
    responsibleDisplayName,
    startsAtUtc,
    endsAtUtc,
    reasonCode,
    status,
    version,
  } = value;
  if (
    typeof id !== "string" ||
    typeof clientId !== "string" ||
    typeof clientReference !== "string" ||
    typeof clientFirstName !== "string" ||
    typeof clientLastName !== "string" ||
    typeof clientArchived !== "boolean" ||
    !isNullableString(responsibleUserId) ||
    !isNullableString(responsibleDisplayName) ||
    typeof startsAtUtc !== "string" ||
    typeof endsAtUtc !== "string" ||
    Number.isNaN(Date.parse(startsAtUtc)) ||
    Number.isNaN(Date.parse(endsAtUtc)) ||
    Date.parse(endsAtUtc) <= Date.parse(startsAtUtc) ||
    typeof reasonCode !== "string" ||
    (status !== "planned" && status !== "cancelled") ||
    !isVersion(version)
  ) {
    return null;
  }
  return {
    id,
    clientId,
    clientReference,
    clientFirstName,
    clientLastName,
    clientArchived,
    responsibleUserId,
    responsibleDisplayName,
    startsAtUtc,
    endsAtUtc,
    reasonCode,
    status,
    version,
  };
}

export function parseAppointmentDetail(value: unknown): AppointmentDetail | null {
  const summary = parseAppointmentSummary(value);
  if (summary === null || !isRecord(value)) return null;
  const { notes, cancelledAtUtc, cancellationReason, createdAtUtc, updatedAtUtc } = value;
  if (
    !isNullableString(notes) ||
    !isNullableString(cancelledAtUtc) ||
    !isNullableString(cancellationReason) ||
    typeof createdAtUtc !== "string" ||
    typeof updatedAtUtc !== "string"
  ) {
    return null;
  }
  return { ...summary, notes, cancelledAtUtc, cancellationReason, createdAtUtc, updatedAtUtc };
}

export function parseAppointmentList(body: unknown): AppointmentSummary[] | null {
  if (!isRecord(body) || !Array.isArray(body.items)) return null;
  const items: AppointmentSummary[] = [];
  for (const item of body.items) {
    const parsed = parseAppointmentSummary(item);
    if (parsed === null) return null;
    items.push(parsed);
  }
  return items;
}

export function parseAppointmentOptions(body: unknown): AppointmentOptions | null {
  if (!isRecord(body)) return null;
  const { timeZone, durationsMinutes, reasons, responsibles, responsibleRequired, displayStartTime, displayEndTime, hiddenWeekdays } = body;
  if (
    typeof timeZone !== "string" ||
    timeZone.length === 0 ||
    !Array.isArray(durationsMinutes) ||
    !durationsMinutes.every((minutes) => typeof minutes === "number" && Number.isSafeInteger(minutes) && minutes > 0 && minutes <= 24 * 60) ||
    !Array.isArray(reasons) ||
    !reasons.every((reason) => isRecord(reason) && typeof reason.code === "string" && typeof reason.label === "string") ||
    !Array.isArray(responsibles) ||
    !responsibles.every((person) => isRecord(person) && typeof person.id === "string" && typeof person.displayName === "string") ||
    typeof responsibleRequired !== "boolean" ||
    typeof displayStartTime !== "string" ||
    typeof displayEndTime !== "string" ||
    !TIME_PATTERN.test(displayStartTime) ||
    !TIME_PATTERN.test(displayEndTime) ||
    displayEndTime <= displayStartTime ||
    !Array.isArray(hiddenWeekdays) ||
    !hiddenWeekdays.every((day) => typeof day === "number" && Number.isInteger(day) && day >= 0 && day <= 6) ||
    hiddenWeekdays.length >= 7
  ) {
    return null;
  }
  // Fuseau inconnu de l'environnement : réponse incohérente plutôt qu'un affichage silencieusement faux.
  try {
    new Intl.DateTimeFormat("fr-FR", { timeZone });
  } catch {
    return null;
  }
  return {
    timeZone,
    durationsMinutes: [...(durationsMinutes as number[])],
    reasons: (reasons as AppointmentReason[]).map(({ code, label }) => ({ code, label })),
    responsibles: (responsibles as AppointmentResponsible[]).map(({ id, displayName }) => ({ id, displayName })),
    responsibleRequired,
    displayStartTime,
    displayEndTime,
    hiddenWeekdays: [...(hiddenWeekdays as number[])],
  };
}

export function appointmentListQuery(range: AppointmentRange): string {
  const query = new URLSearchParams();
  query.set("from", range.fromUtc);
  query.set("to", range.toUtc);
  query.set("status", range.status === "all" ? "all" : "planned");
  return query.toString();
}

const appointmentPath = (id: string) => `/api/appointments/${encodeURIComponent(id)}`;

export const getAppointmentOptions = (baseUrl: URL, token: string, signal?: AbortSignal): Promise<ApiResult<AppointmentOptions>> =>
  requestJson(baseUrl, "/api/appointments/options", { token, parse: parseAppointmentOptions, signal });

export const listAppointments = (
  baseUrl: URL,
  token: string,
  range: AppointmentRange,
  signal?: AbortSignal,
): Promise<ApiResult<AppointmentSummary[]>> =>
  requestJson(baseUrl, `/api/appointments?${appointmentListQuery(range)}`, { token, parse: parseAppointmentList, signal });

export const getAppointment = (baseUrl: URL, token: string, id: string, signal?: AbortSignal): Promise<ApiResult<AppointmentDetail>> =>
  requestJson(baseUrl, appointmentPath(id), { token, parse: parseAppointmentDetail, signal });

export const createAppointment = (baseUrl: URL, token: string, input: AppointmentInput): Promise<ApiResult<AppointmentDetail>> =>
  requestJson(baseUrl, "/api/appointments", { method: "POST", token, body: input, parse: parseAppointmentDetail, acceptedStatuses: [201] });

export const updateAppointment = (
  baseUrl: URL,
  token: string,
  id: string,
  input: AppointmentInput,
  version: number,
): Promise<ApiResult<AppointmentDetail>> =>
  requestJson(baseUrl, appointmentPath(id), { method: "PUT", token, body: { ...input, version }, parse: parseAppointmentDetail });

export const cancelAppointment = (
  baseUrl: URL,
  token: string,
  id: string,
  version: number,
  reason: string | null,
): Promise<ApiResult<AppointmentDetail>> =>
  requestJson(baseUrl, `${appointmentPath(id)}/cancel`, { method: "POST", token, body: { version, reason }, parse: parseAppointmentDetail });

export const restoreAppointment = (baseUrl: URL, token: string, id: string, version: number): Promise<ApiResult<AppointmentDetail>> =>
  requestJson(baseUrl, `${appointmentPath(id)}/restore`, { method: "POST", token, body: { version }, parse: parseAppointmentDetail });
