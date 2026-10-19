// Route /api/audit du double de test (src/test/fakeMmvApi.ts) — contrat IMPLÉMENTÉ par MMV.Api en 03-D2
// (instantané src/api/contract/audit.openapi.json). Événements FICTIFS fournis par chaque test, en mémoire du
// processus de test ; droit audit.read seul. Le double ne prouve aucune règle serveur (tests/MMV.Api.Tests).
import type { IncomingMessage, ServerResponse } from "node:http";
import type { AuditChange } from "../api/audit.ts";
import type { FakeClientRecord } from "./fakeClientsApi.ts";

export const AUDIT_PERMISSION = "audit.read";

export interface FakeAuditSeed {
  clientLastName: string;
  occurredAtUtc: string;
  actorDisplayName: string;
  module: "clients" | "appointments";
  action: string;
  entityType: "client" | "appointment";
  /** Identifiant de l'entité (rendez-vous) ; absent = le client lui-même. */
  entityId?: string;
  changes?: AuditChange[];
}

export interface FakeAuditOptions {
  auditEvents?: FakeAuditSeed[];
  /** Force un statut HTTP pour /api/audit (ex. 500). */
  auditStatus?: number;
}

type Send = (response: ServerResponse, status: number, body?: unknown) => void;

export function handleAudit(
  request: IncomingMessage,
  response: ServerResponse,
  context: { permissions: readonly string[]; clients: readonly FakeClientRecord[]; options: FakeAuditOptions; send: Send },
): void {
  const { send } = context;
  const url = new URL(request.url ?? "", "http://fake");
  const [, , scope, clientId] = url.pathname.split("/").filter(Boolean); // api / audit / clients / {id}
  if (request.method !== "GET" || scope !== "clients" || !clientId) return send(response, 404, { code: "not_found" });
  if (!context.permissions.includes(AUDIT_PERMISSION)) return send(response, 403, { code: "forbidden" });
  if (context.options.auditStatus) return send(response, context.options.auditStatus, { code: "forced" });
  const module = url.searchParams.get("module");
  const from = url.searchParams.get("from");
  const page = Number(url.searchParams.get("page") ?? "1");
  const pageSize = Number(url.searchParams.get("pageSize") ?? "20");
  if ((module !== null && module !== "clients" && module !== "appointments") || (from !== null && Number.isNaN(Date.parse(from))) || !(page >= 1) || !(pageSize >= 1)) {
    return send(response, 400, { code: "invalid_query" });
  }
  const client = context.clients.find((candidate) => candidate.id === clientId);
  const events = (context.options.auditEvents ?? [])
    .filter((seed) => client && seed.clientLastName === client.lastName && (module === null || seed.module === module) && (from === null || Date.parse(seed.occurredAtUtc) >= Date.parse(from)))
    .sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc))
    .map((seed, index) => ({
      id: `0199e000-0000-7000-8000-${String(index + 1).padStart(12, "0")}`,
      occurredAtUtc: seed.occurredAtUtc,
      actorUserId: "0199a000-0000-7000-8000-0000000000aa",
      actorDisplayName: seed.actorDisplayName,
      module: seed.module,
      action: seed.action,
      entityType: seed.entityType,
      entityId: seed.entityId ?? clientId,
      changes: seed.changes ?? [],
    }));
  send(response, 200, { items: events.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: events.length });
}
