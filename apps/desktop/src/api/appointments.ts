// Contrat HTTP Rendez-vous — IMPLÉMENTÉ par MMV.Api (sous-lot 03-B2, 17/10/2026 ; décisions D-03-13 à D-03-18
// approuvées). Référence : docs/phase/03/API_APPOINTMENTS.md ; instantané OpenAPI contract/appointments.openapi.json
// (contrôlé des deux côtés). Fuseau, durées, pas, motifs et leur mode d'attribution, collaborateurs éligibles,
// capacité d'accueil et plage affichée sont SERVIS par le serveur ; conflits, capacité, passé, changements d'heure
// et client archivé sont DÉCIDÉS par le serveur. Parseurs stricts = garde à l'exécution.
import { type ApiResult, requestJson } from "./client.ts";

export type AppointmentStatus = "planned" | "cancelled";

/** Motif servi par le serveur (code stable + libellé affiché), D-03-18. */
export interface AppointmentReason {
  code: string;
  label: string;
  /** D-03-14 : `true` = rendez-vous personnalisé (responsable obligatoire) ; `false` = rendez-vous d'accueil. */
  responsibleRequired: boolean;
}

/** Collaborateur actif ET explicitement éligible (D-03-14) : identifiant et nom affiché seulement. */
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
  /** Pas du début en minutes (heure du magasin). */
  startStepMinutes: number;
  reasons: AppointmentReason[];
  responsibles: AppointmentResponsible[];
  /** Capacité d'accueil (D-03-15) ; `null` = aucun rendez-vous sans responsable. */
  receptionCapacity: number | null;
  /** Plage horaire AFFICHÉE (`HH:mm`, fin `24:00` admise), non bloquante (D-03-13). */
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
  /** Auteur de l'annulation (nom affiché), D-03-16. */
  cancelledByDisplayName: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  /** Auteur de la dernière modification (O-03-23, 03-B3) ; aucun historique détaillé (dette produit). */
  updatedByDisplayName: string;
}

/** Décalage UTC du magasin selon le SERVEUR, constant sur [fromUtc, toUtc[ (B2-REV-03). */
export interface TimeZoneOffset {
  fromUtc: string;
  toUtc: string;
  offsetMinutes: number;
}

/** Rendez-vous de la période et décalages du serveur sur CETTE période (comparés à ceux du poste). */
export interface AppointmentList {
  items: AppointmentSummary[];
  timeZoneOffsets: TimeZoneOffset[];
}

export interface AppointmentInput {
  clientId: string;
  responsibleUserId: string | null;
  /** Instant de début ISO 8601 UTC (`…Z`), calculé dans le fuseau du magasin. */
  startsAtUtc: string;
  /**
   * Heure murale du magasin `AAAA-MM-JJTHH:mm` d'où ce poste a calculé `startsAtUtc` ; le serveur la recalcule et
   * refuse tout écart (422 `startsAtLocal: time_zone_mismatch`, B2-REV-03).
   */
  startsAtLocal: string;
  durationMinutes: number;
  reasonCode: string;
  notes: string | null;
}

/** Historique d'un client (03-D1) : catégorie demandée. */
export type ClientAppointmentScope = "upcoming" | "past" | "cancelled" | "all";

export interface ClientAppointmentQuery {
  scope: ClientAppointmentScope;
  /** Absent : ordre par défaut du serveur (à venir croissant, autres décroissant). */
  direction?: "asc" | "desc";
  page: number;
  pageSize: number;
}

/**
 * Ligne de l'historique d'un client : aucune donnée du client (déjà sur la fiche), ni notes ni motif d'annulation.
 * `startsAtLocal` / `endsAtLocal` : heure murale du magasin calculée par le SERVEUR (`AAAA-MM-JJTHH:mm`).
 */
export interface ClientAppointmentItem {
  id: string;
  startsAtUtc: string;
  endsAtUtc: string;
  startsAtLocal: string;
  endsAtLocal: string;
  reasonCode: string;
  status: AppointmentStatus;
  responsibleUserId: string | null;
  responsibleDisplayName: string | null;
  cancelledAtUtc: string | null;
}

export interface ClientAppointmentCounts {
  upcoming: number;
  past: number;
  cancelled: number;
}

export interface ClientAppointmentPage {
  items: ClientAppointmentItem[];
  page: number;
  pageSize: number;
  /** Effectif de la catégorie demandée. */
  totalCount: number;
  counts: ClientAppointmentCounts;
}

export const CLIENT_APPOINTMENTS_PAGE_SIZE = 10;

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

/** Codes 409 du contrat (corps `{ code }`), chacun traduit par un message borné côté écran. */
export const APPOINTMENT_CONFLICT_CODES = [
  "version_conflict",
  "appointment_conflict",
  "reception_capacity_full",
  "reception_not_configured",
  "responsible_unavailable",
  "client_archived",
  "appointment_cancelled",
  "appointment_not_cancelled",
  "appointment_past",
] as const;

/** 503 : paramètres du magasin non configurés côté serveur (fuseau, plage affichée). */
export const APPOINTMENTS_NOT_CONFIGURED_CODE = "appointments_not_configured";

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const isNullableString = (value: unknown): value is string | null => value === null || typeof value === "string";
const isVersion = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;
const END_TIME_PATTERN = /^(([01]\d|2[0-3]):[0-5]\d|24:00)$/;
const isPositiveInteger = (value: unknown, max: number): value is number =>
  typeof value === "number" && Number.isSafeInteger(value) && value > 0 && value <= max;

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
  const { notes, cancelledAtUtc, cancelledByDisplayName, cancellationReason, createdAtUtc, updatedAtUtc, updatedByDisplayName } = value;
  if (
    !isNullableString(notes) ||
    !isNullableString(cancelledAtUtc) ||
    !isNullableString(cancelledByDisplayName) ||
    !isNullableString(cancellationReason) ||
    typeof createdAtUtc !== "string" ||
    typeof updatedAtUtc !== "string" ||
    typeof updatedByDisplayName !== "string"
  ) {
    return null;
  }
  return { ...summary, notes, cancelledAtUtc, cancelledByDisplayName, cancellationReason, createdAtUtc, updatedAtUtc, updatedByDisplayName };
}

/** Segments stricts : instants valides, non vides, contigus et triés ; décalage entier borné à ±18 h. */
export function parseTimeZoneOffsets(value: unknown): TimeZoneOffset[] | null {
  if (!Array.isArray(value) || value.length === 0) return null;
  const segments: TimeZoneOffset[] = [];
  for (const segment of value) {
    if (!isRecord(segment)) return null;
    const { fromUtc, toUtc, offsetMinutes } = segment;
    if (
      typeof fromUtc !== "string" ||
      typeof toUtc !== "string" ||
      Number.isNaN(Date.parse(fromUtc)) ||
      Number.isNaN(Date.parse(toUtc)) ||
      Date.parse(toUtc) <= Date.parse(fromUtc) ||
      typeof offsetMinutes !== "number" ||
      !Number.isInteger(offsetMinutes) ||
      Math.abs(offsetMinutes) > 18 * 60
    ) {
      return null;
    }
    const previous = segments[segments.length - 1];
    if (previous && Date.parse(previous.toUtc) !== Date.parse(fromUtc)) return null;
    segments.push({ fromUtc, toUtc, offsetMinutes });
  }
  return segments;
}

export function parseAppointmentList(body: unknown): AppointmentList | null {
  if (!isRecord(body) || !Array.isArray(body.items)) return null;
  const items: AppointmentSummary[] = [];
  for (const item of body.items) {
    const parsed = parseAppointmentSummary(item);
    if (parsed === null) return null;
    items.push(parsed);
  }
  const timeZoneOffsets = parseTimeZoneOffsets(body.timeZoneOffsets);
  if (timeZoneOffsets === null) return null;
  return { items, timeZoneOffsets };
}

export function parseAppointmentOptions(body: unknown): AppointmentOptions | null {
  if (!isRecord(body)) return null;
  const {
    timeZone,
    durationsMinutes,
    startStepMinutes,
    reasons,
    responsibles,
    receptionCapacity,
    displayStartTime,
    displayEndTime,
    hiddenWeekdays,
  } = body;
  if (
    typeof timeZone !== "string" ||
    timeZone.length === 0 ||
    !Array.isArray(durationsMinutes) ||
    durationsMinutes.length === 0 ||
    !durationsMinutes.every((minutes) => isPositiveInteger(minutes, 24 * 60)) ||
    !isPositiveInteger(startStepMinutes, 60) ||
    !Array.isArray(reasons) ||
    reasons.length === 0 ||
    !reasons.every(
      (reason) => isRecord(reason) && typeof reason.code === "string" && typeof reason.label === "string" && typeof reason.responsibleRequired === "boolean",
    ) ||
    !Array.isArray(responsibles) ||
    !responsibles.every((person) => isRecord(person) && typeof person.id === "string" && typeof person.displayName === "string") ||
    !(receptionCapacity === null || isPositiveInteger(receptionCapacity, 10_000)) ||
    typeof displayStartTime !== "string" ||
    typeof displayEndTime !== "string" ||
    !TIME_PATTERN.test(displayStartTime) ||
    !END_TIME_PATTERN.test(displayEndTime) ||
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
    startStepMinutes,
    reasons: (reasons as AppointmentReason[]).map(({ code, label, responsibleRequired }) => ({ code, label, responsibleRequired })),
    responsibles: (responsibles as AppointmentResponsible[]).map(({ id, displayName }) => ({ id, displayName })),
    receptionCapacity,
    displayStartTime,
    displayEndTime,
    hiddenWeekdays: [...(hiddenWeekdays as number[])],
  };
}

const LOCAL_WALL_PATTERN = /^\d{4}-\d{2}-\d{2}T([01]\d|2[0-3]):[0-5]\d$/;
const isCount = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;

export function parseClientAppointmentItem(value: unknown): ClientAppointmentItem | null {
  if (!isRecord(value)) return null;
  const { id, startsAtUtc, endsAtUtc, startsAtLocal, endsAtLocal, reasonCode, status, responsibleUserId, responsibleDisplayName, cancelledAtUtc } = value;
  if (
    typeof id !== "string" ||
    typeof startsAtUtc !== "string" ||
    typeof endsAtUtc !== "string" ||
    Number.isNaN(Date.parse(startsAtUtc)) ||
    Number.isNaN(Date.parse(endsAtUtc)) ||
    Date.parse(endsAtUtc) <= Date.parse(startsAtUtc) ||
    typeof startsAtLocal !== "string" ||
    typeof endsAtLocal !== "string" ||
    !LOCAL_WALL_PATTERN.test(startsAtLocal) ||
    !LOCAL_WALL_PATTERN.test(endsAtLocal) ||
    typeof reasonCode !== "string" ||
    (status !== "planned" && status !== "cancelled") ||
    !isNullableString(responsibleUserId) ||
    !isNullableString(responsibleDisplayName) ||
    !isNullableString(cancelledAtUtc) ||
    (status === "cancelled") !== (cancelledAtUtc !== null)
  ) {
    return null;
  }
  return { id, startsAtUtc, endsAtUtc, startsAtLocal, endsAtLocal, reasonCode, status, responsibleUserId, responsibleDisplayName, cancelledAtUtc };
}

export function parseClientAppointmentPage(body: unknown): ClientAppointmentPage | null {
  if (!isRecord(body) || !Array.isArray(body.items) || !isRecord(body.counts)) return null;
  const { page, pageSize, totalCount } = body;
  const { upcoming, past, cancelled } = body.counts;
  if (!isPositiveInteger(page, 100_000) || !isPositiveInteger(pageSize, 100) || !isCount(totalCount) || !isCount(upcoming) || !isCount(past) || !isCount(cancelled)) {
    return null;
  }
  const items: ClientAppointmentItem[] = [];
  for (const item of body.items) {
    const parsed = parseClientAppointmentItem(item);
    if (parsed === null) return null;
    items.push(parsed);
  }
  return { items, page, pageSize, totalCount, counts: { upcoming, past, cancelled } };
}

export function clientAppointmentsQuery(query: ClientAppointmentQuery): string {
  const params = new URLSearchParams();
  params.set("scope", query.scope);
  if (query.direction) params.set("dir", query.direction);
  params.set("page", String(query.page));
  params.set("pageSize", String(query.pageSize));
  return params.toString();
}

export const listClientAppointments = (
  baseUrl: URL,
  token: string,
  clientId: string,
  query: ClientAppointmentQuery,
  signal?: AbortSignal,
): Promise<ApiResult<ClientAppointmentPage>> =>
  requestJson(baseUrl, `/api/appointments/by-client/${encodeURIComponent(clientId)}?${clientAppointmentsQuery(query)}`, {
    token,
    parse: parseClientAppointmentPage,
    signal,
  });

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
): Promise<ApiResult<AppointmentList>> =>
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
