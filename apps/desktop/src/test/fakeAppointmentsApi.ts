// Routes /api/appointments du double de test (src/test/fakeMmvApi.ts) — contrat IMPLÉMENTÉ par MMV.Api en 03-B2
// (docs/phase/03/API_APPOINTMENTS.md, instantané src/api/contract/appointments.openapi.json). Données FICTIVES
// fournies par chaque test, en mémoire du processus de test uniquement. Permissions appointments.read / write /
// cancel (D-03-17). Le double reproduit le contrat (statuts, codes, ordre des contrôles), les deux modes
// d'attribution (D-03-14), le chevauchement par collaborateur et la capacité d'accueil instant par instant
// (D-03-15) ; il ne prouve aucune règle serveur (prouvées par tests/MMV.Api.Tests et tests/MMV.PostgreSql.Tests).
import type { IncomingMessage, ServerResponse } from "node:http";
import { localOffsetSegments, localWallDateTime, type OffsetSegment } from "../appointments/time.ts";
import type { FakeClientRecord } from "./fakeClientsApi.ts";

export const APPOINTMENT_PERMISSIONS = { read: "appointments.read", write: "appointments.write", cancel: "appointments.cancel" } as const;

export interface FakeAppointmentSeed {
  /** Nom du client fictif (doit exister dans les clients du double). */
  clientLastName: string;
  startsAtUtc: string;
  durationMinutes: number;
  reasonCode: string;
  /** Indice dans `responsibles` des options ; absent = non attribué (rendez-vous d'accueil). */
  responsible?: number;
  notes?: string | null;
  cancelled?: { reason: string | null };
}

export interface FakeAppointmentOptions {
  timeZone: string;
  durationsMinutes: number[];
  startStepMinutes: number;
  reasons: { code: string; label: string; responsibleRequired: boolean }[];
  responsibles: { id: string; displayName: string }[];
  receptionCapacity: number | null;
  displayStartTime: string;
  displayEndTime: string;
  hiddenWeekdays: number[];
}

export interface FakeAppointmentsOptions {
  appointments?: FakeAppointmentSeed[];
  appointmentOptions?: FakeAppointmentOptions;
  /** Force un statut HTTP pour GET /api/appointments (ex. 500). */
  appointmentsListStatus?: number;
  /** Retarde les réponses /api/appointments (états de chargement). */
  appointmentsDelayMs?: number;
  /** Magasin non configuré côté serveur : 503 `appointments_not_configured` après contrôle des droits. */
  appointmentsNotConfigured?: boolean;
  /**
   * Décalages « serveur » servis avec la liste (B2-REV-03). Par défaut : ceux de l'ICU du processus de test (serveur
   * et poste concordants) ; un test peut simuler des données de fuseaux divergentes.
   */
  serverOffsets?: (fromUtc: string, toUtc: string, timeZone: string) => OffsetSegment[];
  /** Instant « serveur » : active le refus 409 `appointment_past` d'un changement de réservation d'un rendez-vous commencé. */
  appointmentsNow?: string;
}

export interface FakeAppointmentRecord {
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
  status: "planned" | "cancelled";
  version: number;
  notes: string | null;
  cancelledAtUtc: string | null;
  cancelledByDisplayName: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  updatedByDisplayName: string;
}

type Send = (response: ServerResponse, status: number, body?: unknown) => void;

/**
 * Paramètres du double : motifs et durées APPROUVÉS (D-03-13, D-03-18) ; fuseau, plage affichée, capacité et
 * collaborateurs sont des valeurs FICTIVES de magasin (aucune localisation réelle n'est présumée).
 */
export const FAKE_APPOINTMENT_OPTIONS: FakeAppointmentOptions = {
  timeZone: "Europe/Brussels",
  durationsMinutes: [15, 30, 45, 60, 90, 120],
  startStepMinutes: 5,
  reasons: [
    { code: "conseil_devis", label: "Conseil / Devis", responsibleRequired: true },
    { code: "choix_equipement", label: "Choix de monture et verres", responsibleRequired: true },
    { code: "prise_mesures", label: "Prise de mesures", responsibleRequired: true },
    { code: "essayage_ajustement", label: "Essayage / Ajustement", responsibleRequired: true },
    { code: "retrait_commande", label: "Retrait de commande", responsibleRequired: false },
    { code: "sav_reparation", label: "SAV / Réparation", responsibleRequired: false },
    { code: "autre", label: "Autre motif", responsibleRequired: true },
  ],
  responsibles: [
    { id: "0199b000-0000-7000-8000-000000000001", displayName: "Responsable Un" },
    { id: "0199b000-0000-7000-8000-000000000002", displayName: "Responsable Deux" },
  ],
  receptionCapacity: 1,
  displayStartTime: "09:00",
  displayEndTime: "19:00",
  hiddenWeekdays: [0],
};

const fakeId = (index: number) => `0199c000-0000-7000-8000-${String(index).padStart(12, "0")}`;
const addMinutes = (utc: string, minutes: number) => new Date(Date.parse(utc) + minutes * 60_000).toISOString().replace(".000Z", "Z");

export function seedAppointments(
  seeds: readonly FakeAppointmentSeed[],
  clients: readonly FakeClientRecord[],
  options: FakeAppointmentOptions,
): FakeAppointmentRecord[] {
  return seeds.map((seed, index) => {
    const client = clients.find((candidate) => candidate.lastName === seed.clientLastName);
    if (!client) throw new Error(`Client fictif absent du double : ${seed.clientLastName}`);
    const responsible = seed.responsible === undefined ? null : options.responsibles[seed.responsible];
    return {
      id: fakeId(index + 1),
      clientId: client.id,
      clientReference: client.reference,
      clientFirstName: client.firstName,
      clientLastName: client.lastName,
      clientArchived: client.status === "archived",
      responsibleUserId: responsible?.id ?? null,
      responsibleDisplayName: responsible?.displayName ?? null,
      startsAtUtc: seed.startsAtUtc,
      endsAtUtc: addMinutes(seed.startsAtUtc, seed.durationMinutes),
      reasonCode: seed.reasonCode,
      status: seed.cancelled ? "cancelled" : "planned",
      version: 1,
      notes: seed.notes ?? null,
      cancelledAtUtc: seed.cancelled ? "2026-07-01T08:00:00Z" : null,
      cancelledByDisplayName: seed.cancelled ? "Agent Fictif" : null,
      cancellationReason: seed.cancelled?.reason ?? null,
      createdAtUtc: "2026-07-01T08:00:00Z",
      updatedAtUtc: "2026-07-01T08:00:00Z",
      updatedByDisplayName: "Agent Fictif",
    };
  });
}

const SUMMARY_FIELDS = [
  "id",
  "clientId",
  "clientReference",
  "clientFirstName",
  "clientLastName",
  "clientArchived",
  "responsibleUserId",
  "responsibleDisplayName",
  "startsAtUtc",
  "endsAtUtc",
  "reasonCode",
  "status",
  "version",
] as const;

/** Ligne d'agenda : sans notes ni audit (minimisation, D-03-17). */
const summary = (record: FakeAppointmentRecord) => Object.fromEntries(SUMMARY_FIELDS.map((field) => [field, record[field]]));

type Slot = Pick<FakeAppointmentRecord, "id" | "responsibleUserId" | "startsAtUtc" | "endsAtUtc">;
const overlapping = (records: readonly FakeAppointmentRecord[], candidate: Slot) =>
  records.filter(
    (other) =>
      other.id !== candidate.id &&
      other.status === "planned" &&
      other.responsibleUserId === candidate.responsibleUserId &&
      Date.parse(other.startsAtUtc) < Date.parse(candidate.endsAtUtc) &&
      Date.parse(candidate.startsAtUtc) < Date.parse(other.endsAtUtc),
  );

/** Occupation maximale simultanée des rendez-vous d'accueil sur le créneau (balayage, fin avant début). */
function maxConcurrent(others: readonly FakeAppointmentRecord[], candidate: Slot): number {
  const from = Date.parse(candidate.startsAtUtc);
  const to = Date.parse(candidate.endsAtUtc);
  const events = others.flatMap((other) => [
    [Math.max(from, Date.parse(other.startsAtUtc)), 1],
    [Math.min(to, Date.parse(other.endsAtUtc)), -1],
  ]);
  events.sort((a, b) => a[0] - b[0] || a[1] - b[1]);
  let current = 0;
  let max = 0;
  for (const [, delta] of events) {
    current += delta;
    max = Math.max(max, current);
  }
  return max;
}

/** `null` si le créneau est disponible, sinon le code 409 du contrat. */
function availability(records: readonly FakeAppointmentRecord[], candidate: Slot, options: FakeAppointmentOptions): string | null {
  const others = overlapping(records, candidate);
  if (candidate.responsibleUserId !== null) return others.length > 0 ? "appointment_conflict" : null;
  return maxConcurrent(others, candidate) + 1 > (options.receptionCapacity ?? 0) ? "reception_capacity_full" : null;
}

const UTC_INSTANT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|\+00:00)$/;

/**
 * `keptResponsibleId` : responsable actuel d'un rendez-vous modifié SANS changement de réservation (même créneau, même
 * durée) — comme le serveur, son éligibilité n'est alors pas revérifiée (motif et notes corrigeables, B2-REV-01).
 */
function validate(
  body: Record<string, unknown>,
  clients: readonly FakeClientRecord[],
  options: FakeAppointmentOptions,
  keptResponsibleId: string | null = null,
): Record<string, string[]> | null {
  const errors: Record<string, string[]> = {};
  const client = clients.find((candidate) => candidate.id === body.clientId);
  if (typeof body.clientId !== "string" || body.clientId === "") errors.clientId = ["required"];
  else if (!client) errors.clientId = ["not_found"];
  if (typeof body.startsAtUtc !== "string" || body.startsAtUtc === "") errors.startsAtUtc = ["required"];
  else if (!UTC_INSTANT.test(body.startsAtUtc)) errors.startsAtUtc = ["invalid_format"];
  else if (new Date(body.startsAtUtc).getUTCMinutes() % options.startStepMinutes !== 0) errors.startsAtUtc = ["invalid_step"];
  if (typeof body.startsAtLocal !== "string" || body.startsAtLocal === "") errors.startsAtLocal = ["required"];
  else if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(body.startsAtLocal)) errors.startsAtLocal = ["invalid_format"];
  else if (typeof body.startsAtUtc === "string" && UTC_INSTANT.test(body.startsAtUtc) && localWallDateTime(body.startsAtUtc, options.timeZone) !== body.startsAtLocal) {
    errors.startsAtLocal = ["time_zone_mismatch"];
  }
  if (typeof body.durationMinutes !== "number") errors.durationMinutes = ["required"];
  else if (!options.durationsMinutes.includes(body.durationMinutes)) errors.durationMinutes = ["not_allowed"];
  const reason = options.reasons.find((candidate) => candidate.code === body.reasonCode);
  if (typeof body.reasonCode !== "string" || body.reasonCode === "") errors.reasonCode = ["required"];
  else if (!reason) errors.reasonCode = ["not_allowed"];
  if (body.responsibleUserId === null || body.responsibleUserId === undefined) {
    if (reason?.responsibleRequired) errors.responsibleUserId = ["required"];
    else if (reason && options.receptionCapacity === null) errors.responsibleUserId = ["reception_not_configured"];
  } else if (body.responsibleUserId !== keptResponsibleId && !options.responsibles.some((person) => person.id === body.responsibleUserId)) {
    errors.responsibleUserId = ["not_eligible"];
  }
  if (body.notes !== null && body.notes !== undefined && (typeof body.notes !== "string" || body.notes.length > 1000)) errors.notes = ["too_long"];
  return Object.keys(errors).length > 0 ? errors : null;
}

export async function handleAppointments(
  request: IncomingMessage,
  response: ServerResponse,
  context: {
    permissions: readonly string[];
    appointments: FakeAppointmentRecord[];
    clients: readonly FakeClientRecord[];
    options: FakeAppointmentsOptions;
    send: Send;
    readBody: () => Promise<unknown>;
    userDisplayName?: string;
  },
): Promise<void> {
  const { permissions, appointments, clients, send } = context;
  const settings = context.options.appointmentOptions ?? FAKE_APPOINTMENT_OPTIONS;
  const url = new URL(request.url ?? "", "http://fake");
  const method = request.method ?? "GET";
  const [, , id, action] = url.pathname.split("/").filter(Boolean); // api / appointments / {id|options} / {action}
  const can = (permission: string) => permissions.includes(permission);
  if (context.options.appointmentsDelayMs) await new Promise((resolve) => setTimeout(resolve, context.options.appointmentsDelayMs));

  const required =
    method === "GET" ? APPOINTMENT_PERMISSIONS.read : action === undefined ? APPOINTMENT_PERMISSIONS.write : APPOINTMENT_PERMISSIONS.cancel;
  if (!can(required)) return send(response, 403, { code: "forbidden" });
  if (context.options.appointmentsNotConfigured) return send(response, 503, { code: "appointments_not_configured" });

  if (method === "GET" && id === "options") return send(response, 200, settings);

  if (method === "GET" && id === undefined) {
    if (context.options.appointmentsListStatus) return send(response, context.options.appointmentsListStatus, { code: "forced" });
    const from = Date.parse(url.searchParams.get("from") ?? "");
    const to = Date.parse(url.searchParams.get("to") ?? "");
    const status = url.searchParams.get("status") ?? "planned";
    if (Number.isNaN(from) || Number.isNaN(to) || to <= from || to - from > 31 * 86_400_000 || !["planned", "all"].includes(status)) {
      return send(response, 400, { code: "invalid_query" });
    }
    const items = appointments
      .filter((item) => (status === "all" || item.status === "planned") && Date.parse(item.startsAtUtc) < to && Date.parse(item.endsAtUtc) > from)
      .sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc) || a.id.localeCompare(b.id))
      .map(summary);
    const fromUtc = new Date(from).toISOString();
    const toUtc = new Date(to).toISOString();
    const timeZoneOffsets = (context.options.serverOffsets ?? localOffsetSegments)(fromUtc, toUtc, settings.timeZone);
    return send(response, 200, { items, timeZoneOffsets });
  }

  const raw = method === "GET" ? null : await context.readBody();
  const body = typeof raw === "object" && raw !== null && !Array.isArray(raw) ? (raw as Record<string, unknown>) : null;
  const now = new Date().toISOString();

  const fill = (record: FakeAppointmentRecord, input: Record<string, unknown>) => {
    const client = clients.find((candidate) => candidate.id === input.clientId) as FakeClientRecord;
    const kept =
      record.responsibleUserId !== undefined && record.responsibleUserId !== null && input.responsibleUserId === record.responsibleUserId
        ? { id: record.responsibleUserId, displayName: record.responsibleDisplayName }
        : null;
    const responsible = settings.responsibles.find((person) => person.id === input.responsibleUserId) ?? kept;
    Object.assign(record, {
      clientId: client.id,
      clientReference: client.reference,
      clientFirstName: client.firstName,
      clientLastName: client.lastName,
      clientArchived: client.status === "archived",
      responsibleUserId: responsible?.id ?? null,
      responsibleDisplayName: responsible?.displayName ?? null,
      startsAtUtc: new Date(input.startsAtUtc as string).toISOString().replace(".000Z", "Z"),
      endsAtUtc: addMinutes(input.startsAtUtc as string, input.durationMinutes as number),
      reasonCode: input.reasonCode,
      notes: typeof input.notes === "string" && input.notes.trim() !== "" ? input.notes.trim() : null,
      updatedAtUtc: now,
      updatedByDisplayName: context.userDisplayName ?? "Agent Fictif",
    });
  };

  if (method === "POST" && id === undefined) {
    if (body === null || "version" in body) return send(response, 400, { code: "invalid_body" });
    const errors = validate(body, clients, settings);
    if (errors) return send(response, 422, { code: "validation_failed", errors });
    if (clients.find((candidate) => candidate.id === body.clientId)?.status === "archived") return send(response, 409, { code: "client_archived" });
    const record = {
      id: fakeId(appointments.length + 1),
      status: "planned",
      version: 1,
      cancelledAtUtc: null,
      cancelledByDisplayName: null,
      cancellationReason: null,
      createdAtUtc: now,
    } as FakeAppointmentRecord;
    fill(record, body);
    const refusal = availability(appointments, record, settings);
    if (refusal) return send(response, 409, { code: refusal });
    appointments.push(record);
    return send(response, 201, record);
  }

  const record = appointments.find((candidate) => candidate.id === id);
  if (method === "GET" && action === undefined) {
    return record ? send(response, 200, record) : send(response, 404, { code: "appointment_not_found" });
  }

  if (!record) return send(response, 404, { code: "appointment_not_found" });
  if (body === null) return send(response, 400, { code: "invalid_body" });
  if (typeof body.version !== "number") return send(response, 400, { code: "version_required" });
  if (body.version !== record.version) return send(response, 409, { code: "version_conflict" });

  if (method === "PUT" && action === undefined) {
    if (record.status === "cancelled") return send(response, 409, { code: "appointment_cancelled" });
    const sameBooking =
      body.responsibleUserId === record.responsibleUserId &&
      typeof body.startsAtUtc === "string" &&
      Date.parse(body.startsAtUtc) === Date.parse(record.startsAtUtc) &&
      body.durationMinutes === (Date.parse(record.endsAtUtc) - Date.parse(record.startsAtUtc)) / 60_000;
    const errors = validate(body, clients, settings, sameBooking ? record.responsibleUserId : null);
    if (errors) return send(response, 422, { code: "validation_failed", errors });
    if (record.clientArchived || clients.find((candidate) => candidate.id === body.clientId)?.status === "archived") {
      return send(response, 409, { code: "client_archived" });
    }
    const next = { ...record };
    fill(next, body);
    const rebooks =
      next.startsAtUtc !== record.startsAtUtc || next.endsAtUtc !== record.endsAtUtc || next.responsibleUserId !== record.responsibleUserId || next.clientId !== record.clientId;
    const serverNow = context.options.appointmentsNow;
    if (serverNow && rebooks && Date.parse(record.startsAtUtc) < Date.parse(serverNow)) return send(response, 409, { code: "appointment_past" });
    const refusal = availability(appointments, next, settings);
    if (refusal) return send(response, 409, { code: refusal });
    Object.assign(record, next, { version: record.version + 1 });
    return send(response, 200, record);
  }
  if (method === "POST" && action === "cancel") {
    if (record.status === "cancelled") return send(response, 409, { code: "appointment_cancelled" });
    if (body.reason !== null && body.reason !== undefined && (typeof body.reason !== "string" || body.reason.length > 200)) {
      return send(response, 422, { code: "validation_failed", errors: { reason: ["too_long"] } });
    }
    Object.assign(record, {
      status: "cancelled",
      cancelledAtUtc: now,
      cancelledByDisplayName: context.userDisplayName ?? null,
      cancellationReason: typeof body.reason === "string" && body.reason.trim() !== "" ? body.reason.trim() : null,
      version: record.version + 1,
      updatedAtUtc: now,
      updatedByDisplayName: context.userDisplayName ?? "Agent Fictif",
    });
    return send(response, 200, record);
  }
  if (method === "POST" && action === "restore") {
    if (record.status === "planned") return send(response, 409, { code: "appointment_not_cancelled" });
    if (context.options.appointmentsNow && Date.parse(record.startsAtUtc) < Date.parse(context.options.appointmentsNow)) {
      return send(response, 409, { code: "appointment_past" });
    }
    if (record.clientArchived) return send(response, 409, { code: "client_archived" });
    const refusal = availability(appointments, record, settings);
    if (refusal) return send(response, 409, { code: refusal });
    Object.assign(record, {
      status: "planned",
      cancelledAtUtc: null,
      cancelledByDisplayName: null,
      cancellationReason: null,
      version: record.version + 1,
      updatedAtUtc: now,
      updatedByDisplayName: context.userDisplayName ?? "Agent Fictif",
    });
    return send(response, 200, record);
  }
  return send(response, 404, { code: "not_found" });
}
