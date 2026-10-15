// Client HTTP minimal de MMV.Api : GET JSON uniquement, timeout borné, erreurs typées et bornées.
// Aucun accès à PostgreSQL côté client ; aucune réponse simulée.

export type ApiErrorKind =
  | "not-configured"
  | "network"
  | "timeout"
  | "aborted"
  | "http"
  | "invalid-response";

export interface ApiError {
  kind: ApiErrorKind;
  message: string;
  status?: number;
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
  http: "Réponse HTTP inattendue du serveur MMV.",
  "invalid-response": "Réponse du serveur MMV illisible ou incohérente.",
};

export function apiError(kind: ApiErrorKind, status?: number): ApiError {
  return status === undefined
    ? { kind, message: MESSAGES[kind] }
    : { kind, message: MESSAGES[kind], status };
}

export interface GetJsonOptions<T> {
  /** Valide le corps reçu ; `null` = réponse incohérente pour ce statut HTTP. */
  parse: (body: unknown, status: number) => T | null;
  /** Statuts dont le corps est un contrat attendu (ex. 503 de /health/ready). */
  acceptedStatuses?: readonly number[];
  timeoutMs?: number;
  signal?: AbortSignal;
}

export async function getJson<T>(
  baseUrl: URL,
  path: string,
  { parse, acceptedStatuses = [200], timeoutMs = DEFAULT_TIMEOUT_MS, signal }: GetJsonOptions<T>,
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

  try {
    let response: Response;
    try {
      response = await fetch(new URL(path, baseUrl), {
        method: "GET",
        headers: { Accept: "application/json" },
        cache: "no-store",
        credentials: "omit",
        // Pas de redirection silencieuse (ex. HTTPS -> HTTP).
        redirect: "error",
        signal: controller.signal,
      });
    } catch {
      return { ok: false, error: apiError(timedOut ? "timeout" : signal?.aborted ? "aborted" : "network") };
    }

    if (!acceptedStatuses.includes(response.status)) {
      return { ok: false, error: apiError("http", response.status) };
    }

    let body: unknown;
    try {
      body = await response.json();
    } catch {
      if (timedOut) return { ok: false, error: apiError("timeout") };
      if (signal?.aborted) return { ok: false, error: apiError("aborted") };
      return { ok: false, error: apiError("invalid-response", response.status) };
    }

    const data = parse(body, response.status);
    return data === null
      ? { ok: false, error: apiError("invalid-response", response.status) }
      : { ok: true, status: response.status, data };
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener("abort", forwardAbort);
  }
}
