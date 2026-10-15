// Contrats de GET /health/live (processus API) et GET /health/ready (base PostgreSQL),
// alignés sur src/MMV.Api/Health/LivenessEndpoint.cs et ReadinessEndpoint.cs.
import { type ApiError, type ApiResult, getJson } from "./client.ts";

export interface LiveStatus {
  status: string;
  scope: "process";
  processId: number;
  startedAtUtc: string;
  uptimeSeconds: number;
  checkedAtUtc: string;
}

export interface ReadyStatus {
  status: "Healthy" | "Unhealthy";
  scope: "database";
  checkedAtUtc: string;
  durationMs: number;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

export function parseLive(body: unknown): LiveStatus | null {
  if (!isRecord(body)) return null;
  const { status, scope, processId, startedAtUtc, uptimeSeconds, checkedAtUtc } = body;
  if (
    status !== "Healthy" ||
    scope !== "process" ||
    typeof processId !== "number" ||
    typeof startedAtUtc !== "string" ||
    typeof uptimeSeconds !== "number" ||
    typeof checkedAtUtc !== "string"
  ) {
    return null;
  }
  return { status, scope, processId, startedAtUtc, uptimeSeconds, checkedAtUtc };
}

/** 200 doit porter `Healthy` et 503 `Unhealthy` : toute autre combinaison est rejetée (pas de faux succès). */
export function parseReady(body: unknown, httpStatus: number): ReadyStatus | null {
  if (!isRecord(body)) return null;
  const { status, scope, checkedAtUtc, durationMs } = body;
  const expected = httpStatus === 200 ? "Healthy" : httpStatus === 503 ? "Unhealthy" : null;
  if (
    expected === null ||
    status !== expected ||
    scope !== "database" ||
    typeof checkedAtUtc !== "string" ||
    typeof durationMs !== "number"
  ) {
    return null;
  }
  return { status: expected, scope, checkedAtUtc, durationMs };
}

export const fetchLive = (baseUrl: URL, signal?: AbortSignal): Promise<ApiResult<LiveStatus>> =>
  getJson(baseUrl, "/health/live", { parse: parseLive, signal });

export const fetchReady = (baseUrl: URL, signal?: AbortSignal): Promise<ApiResult<ReadyStatus>> =>
  getJson(baseUrl, "/health/ready", { parse: parseReady, acceptedStatuses: [200, 503], signal });

export type TechnicalStatus =
  | { state: "not-configured"; reason: string }
  | { state: "api-unreachable"; error: ApiError }
  | { state: "database-unavailable"; live: LiveStatus; ready: ReadyStatus | null; error: ApiError | null }
  | { state: "ready"; live: LiveStatus; ready: ReadyStatus };

/** live d'abord (processus API), puis ready (base) : les deux états restent distincts à l'écran. */
export async function checkTechnicalStatus(baseUrl: URL, signal?: AbortSignal): Promise<TechnicalStatus> {
  const live = await fetchLive(baseUrl, signal);
  if (!live.ok) return { state: "api-unreachable", error: live.error };

  const ready = await fetchReady(baseUrl, signal);
  if (!ready.ok) return { state: "database-unavailable", live: live.data, ready: null, error: ready.error };
  if (ready.data.status !== "Healthy") {
    return { state: "database-unavailable", live: live.data, ready: ready.data, error: null };
  }
  return { state: "ready", live: live.data, ready: ready.data };
}
