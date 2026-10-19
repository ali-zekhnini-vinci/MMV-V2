// Client HTTP minimal de MMV.Api : JSON uniquement, timeout borné, erreurs typées et bornées.
// Aucun accès à PostgreSQL côté client ; aucune réponse simulée. Le jeton de session (phase 02)
// est passé explicitement par l'appelant et n'est jamais stocké ici ni journalisé.

export type ApiErrorKind =
  | "not-configured"
  | "network"
  | "timeout"
  | "aborted"
  | "unauthorized"
  | "forbidden"
  | "rate-limited"
  | "not-found"
  | "conflict"
  | "validation"
  | "http"
  | "invalid-response";

export interface ApiError {
  kind: ApiErrorKind;
  message: string;
  status?: number;
  /** Code métier borné renvoyé par le serveur (`{ code }`) pour 400/404/409/422 ; jamais un texte libre. */
  code?: string;
  /** Codes d'erreur par champ (`{ errors: { champ: [codes] } }`) pour 400/422 ; bornés, jamais affichés tels quels. */
  fieldErrors?: Record<string, string[]>;
}

export type ApiResult<T> =
  | { ok: true; status: number; data: T }
  | { ok: false; error: ApiError };

export const DEFAULT_TIMEOUT_MS = 8000;

const MESSAGES: Record<ApiErrorKind, string> = {
  "not-configured": "Adresse du serveur MMV non configurée.",
  network: "Serveur MMV injoignable (réseau ou certificat HTTPS refusé).",
  timeout: "Le serveur MMV n'a pas répondu à temps.",
  aborted: "Requête annulée.",
  unauthorized: "Session absente, expirée ou révoquée.",
  forbidden: "Accès non autorisé pour ce compte.",
  "rate-limited": "Trop de tentatives. Veuillez patienter avant de réessayer.",
  "not-found": "Élément introuvable sur le serveur MMV.",
  conflict: "Cet élément a été modifié depuis un autre poste.",
  validation: "Le serveur MMV a refusé les données saisies.",
  http: "Réponse HTTP inattendue du serveur MMV.",
  "invalid-response": "Réponse du serveur MMV illisible ou incohérente.",
};

const KIND_BY_STATUS: Partial<Record<number, ApiErrorKind>> = {
  401: "unauthorized",
  403: "forbidden",
  404: "not-found",
  409: "conflict",
  422: "validation",
  400: "validation",
  429: "rate-limited",
};

/** Statuts dont le corps d'erreur `{ code, errors }` est lu (borné) pour guider l'écran. */
// 503 : code borné éventuel (ex. `appointments_not_configured`, 03-B2) ; le statut reste qualifié « http ».
const STATUSES_WITH_ERROR_BODY: readonly number[] = [400, 404, 409, 422, 503];
const CODE_PATTERN = /^[a-z0-9_.-]{1,64}$/;
const FIELD_PATTERN = /^[A-Za-z0-9_.]{1,64}$/;

/** Extrait `{ code, errors }` d'un corps d'erreur, en ignorant tout ce qui n'est pas un code borné. */
export function parseErrorBody(body: unknown): Pick<ApiError, "code" | "fieldErrors"> {
  if (typeof body !== "object" || body === null || Array.isArray(body)) return {};
  const { code, errors } = body as Record<string, unknown>;
  const result: Pick<ApiError, "code" | "fieldErrors"> = {};
  if (typeof code === "string" && CODE_PATTERN.test(code)) result.code = code;
  if (typeof errors === "object" && errors !== null && !Array.isArray(errors)) {
    const fieldErrors: Record<string, string[]> = {};
    for (const [field, codes] of Object.entries(errors as Record<string, unknown>).slice(0, 50)) {
      if (!FIELD_PATTERN.test(field) || !Array.isArray(codes)) continue;
      const valid = codes.filter((item): item is string => typeof item === "string" && CODE_PATTERN.test(item)).slice(0, 10);
      if (valid.length > 0) fieldErrors[field] = valid;
    }
    if (Object.keys(fieldErrors).length > 0) result.fieldErrors = fieldErrors;
  }
  return result;
}

export function apiError(kind: ApiErrorKind, status?: number): ApiError {
  return status === undefined
    ? { kind, message: MESSAGES[kind] }
    : { kind, message: MESSAGES[kind], status };
}

export interface RequestOptions<T> {
  method?: "GET" | "POST" | "PUT";
  /** Corps JSON (POST). */
  body?: unknown;
  /** Jeton de session opaque, envoyé en `Authorization: Bearer`. */
  token?: string;
  /** Valide le corps reçu ; `null` = réponse incohérente pour ce statut HTTP. */
  parse: (body: unknown, status: number) => T | null;
  /** Statuts dont le corps est un contrat attendu (ex. 503 de /health/ready). */
  acceptedStatuses?: readonly number[];
  timeoutMs?: number;
  signal?: AbortSignal;
}

export type GetJsonOptions<T> = Omit<RequestOptions<T>, "method" | "body">;

/** Analyseur des réponses sans corps (204). */
export const noContent = (): true => true;

export async function requestJson<T>(
  baseUrl: URL,
  path: string,
  {
    method = "GET",
    body,
    token,
    parse,
    acceptedStatuses = [200],
    timeoutMs = DEFAULT_TIMEOUT_MS,
    signal,
  }: RequestOptions<T>,
): Promise<ApiResult<T>> {
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, timeoutMs);
  const forwardAbort = () => controller.abort();
  signal?.addEventListener("abort", forwardAbort, { once: true });
  if (signal?.aborted) controller.abort();

  const headers: Record<string, string> = { Accept: "application/json" };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (token !== undefined) headers.Authorization = `Bearer ${token}`;

  try {
    let response: Response;
    try {
      response = await fetch(new URL(path, baseUrl), {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        cache: "no-store",
        // Aucun cookie : la session est portée par l'en-tête Authorization (variante dev, ADR-007 PROPOSED).
        credentials: "omit",
        // Pas de redirection silencieuse (ex. HTTPS -> HTTP).
        redirect: "error",
        signal: controller.signal,
      });
    } catch {
      return { ok: false, error: apiError(timedOut ? "timeout" : signal?.aborted ? "aborted" : "network") };
    }

    if (!acceptedStatuses.includes(response.status)) {
      const error = apiError(KIND_BY_STATUS[response.status] ?? "http", response.status);
      if (STATUSES_WITH_ERROR_BODY.includes(response.status)) {
        try {
          Object.assign(error, parseErrorBody(await response.json()));
        } catch {
          // Corps absent ou illisible : l'erreur reste qualifiée par son seul statut.
        }
      }
      return { ok: false, error };
    }

    if (response.status === 204) {
      const data = parse(null, response.status);
      return data === null
        ? { ok: false, error: apiError("invalid-response", response.status) }
        : { ok: true, status: response.status, data };
    }

    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      if (timedOut) return { ok: false, error: apiError("timeout") };
      if (signal?.aborted) return { ok: false, error: apiError("aborted") };
      return { ok: false, error: apiError("invalid-response", response.status) };
    }

    const data = parse(payload, response.status);
    return data === null
      ? { ok: false, error: apiError("invalid-response", response.status) }
      : { ok: true, status: response.status, data };
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener("abort", forwardAbort);
  }
}

export function getJson<T>(baseUrl: URL, path: string, options: GetJsonOptions<T>): Promise<ApiResult<T>> {
  return requestJson(baseUrl, path, { ...options, method: "GET" });
}
