// Routes /api/appointments du double de test (src/test/fakeMmvApi.ts) — contrat PROPOSÉ en 03-B
// (docs/phase/03/API_APPOINTMENTS_PROPOSED.md), NON implémenté par MMV.Api. Données FICTIVES fournies par
// chaque test, en mémoire du processus de test uniquement. Permissions PROPOSÉES appointments.read / write /
// cancel (D-03-17). Le double reproduit le contrat (statuts, codes, ordre des contrôles) et la règle de conflit
// PROPOSÉE (D-03-15 : chevauchement de deux rendez-vous non annulés du même responsable, intervalles
// semi-ouverts) ; il ne prouve aucune règle serveur.
import type { IncomingMessage, ServerResponse } from "node:http";
import type { FakeClientRecord } from "./fakeClientsApi.ts";

export const APPOINTMENT_PERMISSIONS = { read: "appointments.read", write: "appointments.write", cancel: "appointments.cancel" } as const;

export interface FakeAppointmentSeed {
  /** Nom du client fictif (doit exister dans les clients du double). */
  clientLastName: string;
  startsAtUtc: string;
  durationMinutes: number;
  reasonCode: string;
  /** Indice dans `responsibles` des options ; absent = non attribué. */
  responsible?: number;
  notes?: string | null;
  cancelled?: { reason: string | null };
}

export interface FakeAppointmentOptions {
  timeZone: string;
  durationsMinutes: number[];
  reasons: { code: string; label: string }[];
  responsibles: { id: string; displayName: string }[];
  responsibleRequired: boolean;
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
  cancellationReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

type Send = (response: ServerResponse, status: number, body?: unknown) => void;

/** Paramètres FICTIFS du double : ils n'anticipent aucune décision D-03-13 / D-03-14 / D-03-18. */
export const FAKE_APPOINTMENT_OPTIONS: FakeAppointmentOptions = {
  timeZone: "Europe/Paris",
  durationsMinutes: [15, 30, 45, 60],
  reasons: [
    { code: "motif-a", label: "Motif fictif A" },
    { code: "motif-b", label: "Motif fictif B" },
    { code: "motif-c", label: "Motif fictif C" },
  ],
  responsibles: [
    { id: "0199b000-0000-7000-8000-000000000001", displayName: "Responsable Un" },
    { id: "0199b000-0000-7000-8000-000000000002", displayName: "Responsable Deux" },
  ],
  responsibleRequired: false,
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
      cancellationReason: seed.cancelled?.reason ?? null,
      createdAtUtc: "2026-07-01T08:00:00Z",
      updatedAtUtc: "2026-07-01T08:00:00Z",
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

/** Ligne d'agenda : sans notes ni audit (minimisation, comme la liste Clients). */
const summary = (record: FakeAppointmentRecord) => Object.fromEntries(SUMMARY_FIELDS.map((field) => [field, record[field]]));

/** Règle PROPOSÉE D-03-15 : même responsable, rendez-vous non annulés, [début, fin[ qui se recouvrent. */
const overlaps = (records: readonly FakeAppointmentRecord[], candidate: Pick<FakeAppointmentRecord, "id" | "responsibleUserId" | "startsAtUtc" | "endsAtUtc">) =>
  candidate.responsibleUserId !== null &&
  records.some(
    (other) =>
      other.id !== candidate.id &&
      other.status === "planned" &&
      other.responsibleUserId === candidate.responsibleUserId &&
      Date.parse(other.startsAtUtc) < Date.parse(candidate.endsAtUtc) &&
      Date.parse(candidate.startsAtUtc) < Date.parse(other.endsAtUtc),
  );

function validate(body: Record<string, unknown>, clients: readonly FakeClientRecord[], options: FakeAppointmentOptions): Record<string, string[]> | null {
  const errors: Record<string, string[]> = {};
  const client = clients.find((candidate) => candidate.id === body.clientId);
  if (typeof body.clientId !== "string" || body.clientId === "") errors.clientId = ["required"];
  else if (!client) errors.clientId = ["not_found"];
  if (typeof body.startsAtUtc !== "string" || Number.isNaN(Date.parse(body.startsAtUtc))) errors.startsAtUtc = ["invalid_format"];
  if (typeof body.durationMinutes !== "number" || !options.durationsMinutes.includes(body.durationMinutes)) errors.durationMinutes = ["not_allowed"];
  if (typeof body.reasonCode !== "string" || !options.reasons.some((reason) => reason.code === body.reasonCode)) errors.reasonCode = ["not_allowed"];
  if (body.responsibleUserId === null) {
    if (options.responsibleRequired) errors.responsibleUserId = ["required"];
  } else if (!options.responsibles.some((person) => person.id === body.responsibleUserId)) errors.responsibleUserId = ["not_found"];
  if (body.notes !== null && (typeof body.notes !== "string" || body.notes.length > 1000)) errors.notes = ["too_long"];
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
  },
): Promise<void> {
  const { permissions, appointments, clients, send } = context;
  const settings = context.options.appointmentOptions ?? FAKE_APPOINTMENT_OPTIONS;
  const url = new URL(request.url ?? "", "http://fake");
  const method = request.method ?? "GET";
  const [, , id, action] = url.pathname.split("/").filter(Boolean); // api / appointments / {id|options} / {action}
  const can = (permission: string) => permissions.includes(permission);
  if (context.options.appointmentsDelayMs) await new Promise((resolve) => setTimeout(resolve, context.options.appointmentsDelayMs));

  if (method === "GET" && id === "options") {
    if (!can(APPOINTMENT_PERMISSIONS.read)) return send(response, 403, { code: "forbidden" });
    return send(response, 200, settings);
  }

  if (method === "GET" && id === undefined) {
    if (!can(APPOINTMENT_PERMISSIONS.read)) return send(response, 403, { code: "forbidden" });
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
    return send(response, 200, { items });
  }

  const raw = method === "GET" ? null : await context.readBody();
  const body = typeof raw === "object" && raw !== null && !Array.isArray(raw) ? (raw as Record<string, unknown>) : null;
  const now = new Date().toISOString();

  const fill = (record: FakeAppointmentRecord, input: Record<string, unknown>) => {
    const client = clients.find((candidate) => candidate.id === input.clientId) as FakeClientRecord;
    const responsible = settings.responsibles.find((person) => person.id === input.responsibleUserId) ?? null;
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
      notes: (input.notes as string | null) ?? null,
      updatedAtUtc: now,
    });
  };

  if (method === "POST" && id === undefined) {
    if (!can(APPOINTMENT_PERMISSIONS.write)) return send(response, 403, { code: "forbidden" });
    if (body === null) return send(response, 400, { code: "invalid_body" });
    const errors = validate(body, clients, settings);
    if (errors) return send(response, 422, { code: "validation_failed", errors });
    if (clients.find((candidate) => candidate.id === body.clientId)?.status === "archived") return send(response, 409, { code: "client_archived" });
    const record = { id: fakeId(appointments.length + 1), status: "planned", version: 1, cancelledAtUtc: null, cancellationReason: null, createdAtUtc: now } as FakeAppointmentRecord;
    fill(record, body);
    if (overlaps(appointments, record)) return send(response, 409, { code: "appointment_conflict" });
    appointments.push(record);
    return send(response, 201, record);
  }

  const record = appointments.find((candidate) => candidate.id === id);
  if (method === "GET" && action === undefined) {
    if (!can(APPOINTMENT_PERMISSIONS.read)) return send(response, 403, { code: "forbidden" });
    return record ? send(response, 200, record) : send(response, 404, { code: "appointment_not_found" });
  }

  if (!can(action === undefined ? APPOINTMENT_PERMISSIONS.write : APPOINTMENT_PERMISSIONS.cancel)) return send(response, 403, { code: "forbidden" });
  if (!record) return send(response, 404, { code: "appointment_not_found" });
  if (body === null) return send(response, 400, { code: "invalid_body" });
  if (typeof body.version !== "number") return send(response, 400, { code: "version_required" });
  if (body.version !== record.version) return send(response, 409, { code: "version_conflict" });

  if (method === "PUT" && action === undefined) {
    if (record.status === "cancelled") return send(response, 409, { code: "appointment_cancelled" });
    const errors = validate(body, clients, settings);
    if (errors) return send(response, 422, { code: "validation_failed", errors });
    const next = { ...record };
    fill(next, body);
    if (overlaps(appointments, next)) return send(response, 409, { code: "appointment_conflict" });
    Object.assign(record, next, { version: record.version + 1 });
    return send(response, 200, record);
  }
  if (method === "POST" && action === "cancel") {
    if (record.status === "cancelled") return send(response, 409, { code: "appointment_cancelled" });
    if (body.reason !== null && (typeof body.reason !== "string" || body.reason.length > 200)) {
      return send(response, 422, { code: "validation_failed", errors: { reason: ["too_long"] } });
    }
    Object.assign(record, { status: "cancelled", cancelledAtUtc: now, cancellationReason: body.reason || null, version: record.version + 1, updatedAtUtc: now });
    return send(response, 200, record);
  }
  if (method === "POST" && action === "restore") {
    if (record.status === "planned") return send(response, 409, { code: "appointment_not_cancelled" });
    if (record.clientArchived) return send(response, 409, { code: "client_archived" });
    if (overlaps(appointments, record)) return send(response, 409, { code: "appointment_conflict" });
    Object.assign(record, { status: "planned", cancelledAtUtc: null, cancellationReason: null, version: record.version + 1, updatedAtUtc: now });
    return send(response, 200, record);
  }
  return send(response, 404, { code: "not_found" });
}
