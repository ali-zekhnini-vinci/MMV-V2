// Routes /api/clients du double de test (src/test/fakeMmvApi.ts) — contrat implémenté par MMV.Api en 03-A2
// (docs/phase/03/API_CLIENTS.md ; schémas : src/api/contract/clients.openapi.json). Données FICTIVES fournies par
// chaque test, en mémoire du processus de test uniquement ; jamais persistées ni montrées hors des tests.
// Permissions clients.read / clients.write / clients.archive (G03-METIER B acceptée le 17/10/2026).
// Le double reproduit le contrat (statuts, corps, liste blanche, ordre des contrôles), pas la sécurité ni les
// règles complètes : celles-ci sont prouvées côté serveur (tests/MMV.Api.Tests, tests/MMV.PostgreSql.Tests).
import type { IncomingMessage, ServerResponse } from "node:http";

export const CLIENT_PERMISSIONS = { read: "clients.read", write: "clients.write", archive: "clients.archive" } as const;

export interface FakeClientSeed {
  firstName: string;
  lastName: string;
  phone?: string | null;
  email?: string | null;
  city?: string | null;
  archived?: boolean;
  notes?: string | null;
}

export interface FakeClientRecord {
  id: string;
  reference: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  email: string | null;
  city: string | null;
  status: "active" | "archived";
  createdAtUtc: string;
  version: number;
  birthDate: string | null;
  addressLine: string | null;
  postalCode: string | null;
  notes: string | null;
  updatedAtUtc: string;
  archivedAtUtc: string | null;
}

export interface FakeClientsOptions {
  clients?: FakeClientSeed[];
  /** Retarde les réponses /api/clients (états de chargement). */
  clientsDelayMs?: number;
  /** Force un statut HTTP pour GET /api/clients (ex. 500). */
  clientsListStatus?: number;
}

type Send = (response: ServerResponse, status: number, body?: unknown) => void;

const INPUT_FIELDS = ["firstName", "lastName", "birthDate", "phone", "email", "addressLine", "city", "postalCode", "notes"] as const;

const fold = (value: string) => value.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase("fr");

const fakeId = (index: number) => `0199a000-0000-7000-8000-${String(index).padStart(12, "0")}`;
const fakeReference = (index: number) => `CLI-${String(index).padStart(6, "0")}`;

export function seedClients(seeds: readonly FakeClientSeed[]): FakeClientRecord[] {
  return seeds.map((seed, index) => {
    const created = new Date(Date.UTC(2026, 0, 1 + index, 9)).toISOString();
    return {
      id: fakeId(index + 1),
      reference: fakeReference(index + 1),
      firstName: seed.firstName,
      lastName: seed.lastName,
      phone: seed.phone ?? null,
      email: seed.email ?? null,
      city: seed.city ?? null,
      status: seed.archived ? "archived" : "active",
      createdAtUtc: created,
      version: 1,
      birthDate: null,
      addressLine: null,
      postalCode: null,
      notes: seed.notes ?? null,
      updatedAtUtc: created,
      archivedAtUtc: seed.archived ? created : null,
    };
  });
}

const summary = ({ id, reference, firstName, lastName, phone, email, city, status, createdAtUtc, version }: FakeClientRecord) => ({
  id,
  reference,
  firstName,
  lastName,
  phone,
  email,
  city,
  status,
  createdAtUtc,
  version,
});

/** Validation minimale du double (règles complètes : MMV.Application ClientInputValidator, testées côté serveur). */
function validate(body: Record<string, unknown> | null): Record<string, string[]> | null {
  const errors: Record<string, string[]> = {};
  for (const field of ["firstName", "lastName"] as const) {
    const value = body?.[field];
    if (typeof value !== "string" || value.trim() === "") errors[field] = ["required"];
  }
  // Règle propre au double, pour éprouver le report des erreurs serveur sous les champs.
  if (typeof body?.email === "string" && body.email.endsWith(".invalid")) errors.email = ["invalid_format"];
  return Object.keys(errors).length > 0 ? errors : null;
}

function pickInput(body: Record<string, unknown>) {
  const input: Record<string, string | null> = {};
  for (const field of INPUT_FIELDS) input[field] = typeof body[field] === "string" ? (body[field] as string) : null;
  return input as Pick<FakeClientRecord, (typeof INPUT_FIELDS)[number]>;
}

export async function handleClients(
  request: IncomingMessage,
  response: ServerResponse,
  context: { permissions: readonly string[]; clients: FakeClientRecord[]; options: FakeClientsOptions; send: Send; readBody: () => Promise<unknown> },
): Promise<void> {
  const { permissions, clients, options, send } = context;
  const url = new URL(request.url ?? "", "http://fake");
  const method = request.method ?? "GET";
  const [, , id, action] = url.pathname.split("/").filter(Boolean); // api / clients / {id} / {action}
  const can = (permission: string) => permissions.includes(permission);
  if (options.clientsDelayMs) await new Promise((resolve) => setTimeout(resolve, options.clientsDelayMs));

  if (method === "GET" && id === undefined) {
    if (!can(CLIENT_PERMISSIONS.read)) return send(response, 403, { code: "forbidden" });
    if (options.clientsListStatus) return send(response, options.clientsListStatus, { code: "forced" });
    const status = url.searchParams.get("status") ?? "active";
    const sort = url.searchParams.get("sort") ?? "name";
    const dir = url.searchParams.get("dir") ?? "asc";
    const page = Number(url.searchParams.get("page") ?? "1");
    const pageSize = Number(url.searchParams.get("pageSize") ?? "20");
    const q = url.searchParams.get("q");
    const valid =
      ["active", "archived", "all"].includes(status) &&
      ["name", "createdAt", "city"].includes(sort) &&
      ["asc", "desc"].includes(dir) &&
      Number.isInteger(page) &&
      page >= 1 &&
      Number.isInteger(pageSize) &&
      pageSize >= 1 &&
      pageSize <= 100 &&
      (q === null || (q.length >= 2 && q.length <= 100));
    if (!valid) return send(response, 400, { code: "invalid_query" });

    const needle = q === null ? null : fold(q);
    const digits = q === null ? "" : q.replace(/\D/g, "");
    const matches = (client: FakeClientRecord) =>
      needle === null ||
      fold(`${client.lastName} ${client.firstName}`).includes(needle) ||
      fold(`${client.firstName} ${client.lastName}`).includes(needle) ||
      (client.email !== null && fold(client.email).includes(needle)) ||
      (digits.length >= 2 && client.phone !== null && client.phone.replace(/\D/g, "").includes(digits));
    const filtered = clients.filter((client) => (status === "all" || client.status === status) && matches(client));
    const key = (client: FakeClientRecord) =>
      sort === "name" ? fold(`${client.lastName} ${client.firstName}`) : sort === "city" ? fold(client.city ?? "") : client.createdAtUtc;
    filtered.sort((a, b) => key(a).localeCompare(key(b), "fr") * (dir === "desc" ? -1 : 1) || a.id.localeCompare(b.id));
    const items = filtered.slice((page - 1) * pageSize, page * pageSize).map(summary);
    return send(response, 200, { items, page, pageSize, totalCount: filtered.length });
  }

  const raw = method === "GET" ? null : await context.readBody();
  const body = typeof raw === "object" && raw !== null && !Array.isArray(raw) ? (raw as Record<string, unknown>) : null;

  if (method === "POST" && id === undefined) {
    if (!can(CLIENT_PERMISSIONS.write)) return send(response, 403, { code: "forbidden" });
    if (body === null) return send(response, 400, { code: "invalid_body" });
    const errors = validate(body);
    if (errors) return send(response, 422, { code: "validation_failed", errors });
    const now = new Date().toISOString();
    const index = clients.length + 1;
    const record: FakeClientRecord = {
      ...pickInput(body),
      id: fakeId(index),
      reference: fakeReference(index),
      status: "active",
      createdAtUtc: now,
      updatedAtUtc: now,
      archivedAtUtc: null,
      version: 1,
    } as FakeClientRecord;
    clients.push(record);
    return send(response, 201, record);
  }

  const client = clients.find((candidate) => candidate.id === id);
  if (method === "GET" && action === undefined) {
    if (!can(CLIENT_PERMISSIONS.read)) return send(response, 403, { code: "forbidden" });
    return client ? send(response, 200, client) : send(response, 404, { code: "client_not_found" });
  }

  if (!can(action === undefined ? CLIENT_PERMISSIONS.write : CLIENT_PERMISSIONS.archive)) return send(response, 403, { code: "forbidden" });
  if (!client) return send(response, 404, { code: "client_not_found" });
  if (body === null) return send(response, 400, { code: "invalid_body" });
  if (typeof body.version !== "number") return send(response, 400, { code: "version_required" });
  // Même ordre que MMV.Api : un client archivé est refusé en modification avant le contrôle de version.
  if (method === "PUT" && action === undefined && client.status === "archived") return send(response, 409, { code: "client_archived" });
  if (body.version !== client.version) return send(response, 409, { code: "version_conflict" });

  if (method === "PUT" && action === undefined) {
    const errors = validate(body);
    if (errors) return send(response, 422, { code: "validation_failed", errors });
    Object.assign(client, pickInput(body), { version: client.version + 1, updatedAtUtc: new Date().toISOString() });
    return send(response, 200, client);
  }
  if (method === "POST" && (action === "archive" || action === "reactivate")) {
    const archive = action === "archive";
    if ((client.status === "archived") === archive) return send(response, 409, { code: archive ? "already_archived" : "already_active" });
    const now = new Date().toISOString();
    Object.assign(client, { status: archive ? "archived" : "active", archivedAtUtc: archive ? now : null, version: client.version + 1, updatedAtUtc: now });
    return send(response, 200, client);
  }
  return send(response, 404, { code: "not_found" });
}
