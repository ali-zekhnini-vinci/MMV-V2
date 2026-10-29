// Contrat HTTP de l'historique des actions (03-D2) — implémenté par MMV.Api (GET /api/audit/clients/{id}, droit
// DÉDIÉ audit.read). Instantané OpenAPI contract/audit.openapi.json, contrôlé des deux côtés. Lecture seule : le
// journal n'est alimenté que par le serveur, dans les transactions métier ; l'interface n'en produit jamais.
// Valeurs : seuls le créneau, la durée, le motif et le responsable sont servis, avec leur libellé calculé par le
// serveur ; tout autre champ arrive « masqué » (nom seulement). Parseurs stricts = garde à l'exécution.
import { type ApiResult, requestJson } from "./client.ts";

/** 04-B : `optical` servi seulement avec `audit.read` ET `optical.read` (sinon exclu par le serveur, filtre refusé 403). */
export type AuditModule = "clients" | "appointments" | "optical";

export interface AuditChange {
  field: string;
  /** Valeur non conservée (donnée personnelle ou texte libre) : nom du champ seulement. */
  masked: boolean;
  old: string | null;
  new: string | null;
  oldDisplay: string | null;
  newDisplay: string | null;
}

export interface AuditEvent {
  id: string;
  occurredAtUtc: string;
  /** Compte MMV qui a EXÉCUTÉ l'action (jamais le collaborateur responsable d'un rendez-vous). */
  actorUserId: string;
  actorDisplayName: string;
  module: string;
  action: string;
  entityType: string;
  entityId: string;
  changes: AuditChange[];
}

export interface AuditPage {
  items: AuditEvent[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuditQuery {
  module?: AuditModule;
  /** Bornes ISO 8601 UTC, `to` exclu. */
  fromUtc?: string;
  toUtc?: string;
  page: number;
  pageSize: number;
}

export const AUDIT_PAGE_SIZE = 20;

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);
const isNullableString = (value: unknown): value is string | null => value === null || typeof value === "string";
const isCount = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;

export function parseAuditChange(value: unknown): AuditChange | null {
  if (!isRecord(value)) return null;
  const { field, masked, old, new: next, oldDisplay, newDisplay } = value;
  if (
    typeof field !== "string" ||
    typeof masked !== "boolean" ||
    !isNullableString(old) ||
    !isNullableString(next) ||
    !isNullableString(oldDisplay) ||
    !isNullableString(newDisplay) ||
    // Un champ masqué ne transporte jamais de valeur.
    (masked && (old !== null || next !== null || oldDisplay !== null || newDisplay !== null))
  ) {
    return null;
  }
  return { field, masked, old, new: next, oldDisplay, newDisplay };
}

export function parseAuditEvent(value: unknown): AuditEvent | null {
  if (!isRecord(value) || !Array.isArray(value.changes)) return null;
  const { id, occurredAtUtc, actorUserId, actorDisplayName, module, action, entityType, entityId } = value;
  if (
    typeof id !== "string" ||
    typeof occurredAtUtc !== "string" ||
    Number.isNaN(Date.parse(occurredAtUtc)) ||
    typeof actorUserId !== "string" ||
    typeof actorDisplayName !== "string" ||
    typeof module !== "string" ||
    typeof action !== "string" ||
    typeof entityType !== "string" ||
    typeof entityId !== "string"
  ) {
    return null;
  }
  const changes: AuditChange[] = [];
  for (const change of value.changes) {
    const parsed = parseAuditChange(change);
    if (parsed === null) return null;
    changes.push(parsed);
  }
  return { id, occurredAtUtc, actorUserId, actorDisplayName, module, action, entityType, entityId, changes };
}

export function parseAuditPage(body: unknown): AuditPage | null {
  if (!isRecord(body) || !Array.isArray(body.items)) return null;
  const { page, pageSize, totalCount } = body;
  if (!isCount(page) || page < 1 || !isCount(pageSize) || pageSize < 1 || !isCount(totalCount)) return null;
  const items: AuditEvent[] = [];
  for (const item of body.items) {
    const parsed = parseAuditEvent(item);
    if (parsed === null) return null;
    items.push(parsed);
  }
  return { items, page, pageSize, totalCount };
}

export function auditQueryString(query: AuditQuery): string {
  const params = new URLSearchParams();
  if (query.module) params.set("module", query.module);
  if (query.fromUtc) params.set("from", query.fromUtc);
  if (query.toUtc) params.set("to", query.toUtc);
  params.set("page", String(query.page));
  params.set("pageSize", String(query.pageSize));
  return params.toString();
}

export const listClientAuditEvents = (baseUrl: URL, token: string, clientId: string, query: AuditQuery, signal?: AbortSignal): Promise<ApiResult<AuditPage>> =>
  requestJson(baseUrl, `/api/audit/clients/${encodeURIComponent(clientId)}?${auditQueryString(query)}`, { token, parse: parseAuditPage, signal });
