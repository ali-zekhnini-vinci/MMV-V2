// Contrat HTTP Clients — implémenté par MMV.Api au sous-lot 03-A2 (G03-METIER A/B acceptés, C accepté sur le
// principe le 17/10/2026). Référence : docs/phase/03/API_CLIENTS.md. Les types ci-dessous correspondent aux
// schémas OpenAPI générés par le serveur (instantané src/api/contract/clients.openapi.json, vérifié par
// src/api/clientsContract.test.ts) : ClientSummary ↔ ClientSummaryResponse, ClientDetail ↔ ClientDetailResponse,
// ClientPage ↔ ClientPageResponse, ClientInput ↔ ClientInputRequest (+ version : ClientUpdateRequest).
// Les parseurs stricts restent la garde à l'exécution (aucun type généré ne valide une réponse).
// Le serveur reste seul juge des droits (clients.read / clients.write / clients.archive) et des règles de validation.
import { type ApiResult, requestJson } from "./client.ts";

export type ClientStatus = "active" | "archived";

/** Ligne de liste : seulement les colonnes affichées (minimisation des données personnelles). */
export interface ClientSummary {
  id: string;
  /** Référence lisible attribuée par PostgreSQL (`CLI-000123`, 6 chiffres au minimum), jamais réutilisée. */
  reference: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  email: string | null;
  city: string | null;
  status: ClientStatus;
  createdAtUtc: string;
  /** Jeton de concurrence optimiste (xmin PostgreSQL), renvoyé tel quel à chaque écriture. */
  version: number;
}

export interface ClientDetail extends ClientSummary {
  /** Date civile ISO `AAAA-MM-JJ` (aucun fuseau). */
  birthDate: string | null;
  addressLine: string | null;
  postalCode: string | null;
  notes: string | null;
  updatedAtUtc: string;
  archivedAtUtc: string | null;
}

export interface ClientPage {
  items: ClientSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Champs saisissables (D-03-02). Les champs sensibles de la maquette (sécurité sociale, mutuelle) sont exclus (D-03-03). */
export interface ClientInput {
  firstName: string;
  lastName: string;
  birthDate: string | null;
  phone: string | null;
  email: string | null;
  addressLine: string | null;
  city: string | null;
  postalCode: string | null;
  notes: string | null;
}

export type ClientStatusFilter = "active" | "archived" | "all";
export type ClientSortField = "name" | "createdAt" | "city";
export type SortDirection = "asc" | "desc";

export interface ClientListParams {
  search: string;
  status: ClientStatusFilter;
  sort: ClientSortField;
  direction: SortDirection;
  /** Page 1-indexée. */
  page: number;
  pageSize: number;
}

/** Bornes du listing (liste blanche côté serveur, rejet 400 `invalid_query` au-delà). */
export const CLIENT_PAGE_SIZE = 20;
export const CLIENT_MAX_PAGE_SIZE = 100;
export const CLIENT_SEARCH_MIN_LENGTH = 2;
export const CLIENT_SEARCH_MAX_LENGTH = 100;
export const CLIENT_SORT_FIELDS: readonly ClientSortField[] = ["name", "createdAt", "city"];
export const CLIENT_STATUS_FILTERS: readonly ClientStatusFilter[] = ["active", "archived", "all"];

/** Chaîne de requête du listing : uniquement des valeurs de liste blanche, recherche bornée. */
export function clientListQuery(params: ClientListParams): string {
  const query = new URLSearchParams();
  const search = params.search.trim().slice(0, CLIENT_SEARCH_MAX_LENGTH);
  if (search.length >= CLIENT_SEARCH_MIN_LENGTH) query.set("q", search);
  query.set("status", CLIENT_STATUS_FILTERS.includes(params.status) ? params.status : "active");
  query.set("sort", CLIENT_SORT_FIELDS.includes(params.sort) ? params.sort : "name");
  query.set("dir", params.direction === "desc" ? "desc" : "asc");
  query.set("page", String(Math.max(1, Math.trunc(params.page) || 1)));
  query.set("pageSize", String(Math.min(CLIENT_MAX_PAGE_SIZE, Math.max(1, Math.trunc(params.pageSize) || CLIENT_PAGE_SIZE))));
  return query.toString();
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const isNullableString = (value: unknown): value is string | null => value === null || typeof value === "string";

export function parseClientSummary(value: unknown): ClientSummary | null {
  if (!isRecord(value)) return null;
  const { id, reference, firstName, lastName, phone, email, city, status, createdAtUtc, version } = value;
  if (
    typeof id !== "string" ||
    typeof reference !== "string" ||
    typeof firstName !== "string" ||
    typeof lastName !== "string" ||
    !isNullableString(phone) ||
    !isNullableString(email) ||
    !isNullableString(city) ||
    (status !== "active" && status !== "archived") ||
    typeof createdAtUtc !== "string" ||
    typeof version !== "number" ||
    !Number.isSafeInteger(version) ||
    version < 0
  ) {
    return null;
  }
  return { id, reference, firstName, lastName, phone, email, city, status, createdAtUtc, version };
}

export function parseClientDetail(value: unknown): ClientDetail | null {
  const summary = parseClientSummary(value);
  if (summary === null || !isRecord(value)) return null;
  const { birthDate, addressLine, postalCode, notes, updatedAtUtc, archivedAtUtc } = value;
  if (
    !isNullableString(birthDate) ||
    !isNullableString(addressLine) ||
    !isNullableString(postalCode) ||
    !isNullableString(notes) ||
    typeof updatedAtUtc !== "string" ||
    !isNullableString(archivedAtUtc)
  ) {
    return null;
  }
  return { ...summary, birthDate, addressLine, postalCode, notes, updatedAtUtc, archivedAtUtc };
}

export function parseClientPage(body: unknown): ClientPage | null {
  if (!isRecord(body) || !Array.isArray(body.items)) return null;
  const { page, pageSize, totalCount } = body;
  if (![page, pageSize, totalCount].every((n) => typeof n === "number" && Number.isSafeInteger(n) && n >= 0)) return null;
  const items: ClientSummary[] = [];
  for (const item of body.items) {
    const parsed = parseClientSummary(item);
    if (parsed === null) return null;
    items.push(parsed);
  }
  return { items, page: page as number, pageSize: pageSize as number, totalCount: totalCount as number };
}

const clientPath = (id: string) => `/api/clients/${encodeURIComponent(id)}`;

export const listClients = (baseUrl: URL, token: string, params: ClientListParams, signal?: AbortSignal): Promise<ApiResult<ClientPage>> =>
  requestJson(baseUrl, `/api/clients?${clientListQuery(params)}`, { token, parse: parseClientPage, signal });

export const getClient = (baseUrl: URL, token: string, id: string, signal?: AbortSignal): Promise<ApiResult<ClientDetail>> =>
  requestJson(baseUrl, clientPath(id), { token, parse: parseClientDetail, signal });

export const createClient = (baseUrl: URL, token: string, input: ClientInput, signal?: AbortSignal): Promise<ApiResult<ClientDetail>> =>
  requestJson(baseUrl, "/api/clients", { method: "POST", token, body: input, parse: parseClientDetail, acceptedStatuses: [201], signal });

export const updateClient = (
  baseUrl: URL,
  token: string,
  id: string,
  input: ClientInput,
  version: number,
  signal?: AbortSignal,
): Promise<ApiResult<ClientDetail>> =>
  requestJson(baseUrl, clientPath(id), { method: "PUT", token, body: { ...input, version }, parse: parseClientDetail, signal });

export const archiveClient = (baseUrl: URL, token: string, id: string, version: number, signal?: AbortSignal): Promise<ApiResult<ClientDetail>> =>
  requestJson(baseUrl, `${clientPath(id)}/archive`, { method: "POST", token, body: { version }, parse: parseClientDetail, signal });

export const reactivateClient = (baseUrl: URL, token: string, id: string, version: number, signal?: AbortSignal): Promise<ApiResult<ClientDetail>> =>
  requestJson(baseUrl, `${clientPath(id)}/reactivate`, { method: "POST", token, body: { version }, parse: parseClientDetail, signal });
