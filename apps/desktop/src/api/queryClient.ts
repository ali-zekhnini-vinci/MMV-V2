// TanStack Query v5 — politique commune des données serveur (Phase 03).
// - Un QueryClient par session authentifiée (créé par le Shell, vidé à sa fermeture) : aucune donnée
//   personnelle ne survit à la déconnexion, et rien n'est persisté (pas de persister, pas de stockage local).
// - Les erreurs sont les ApiError bornées du client HTTP (api/client.ts), jamais un texte serveur brut.
// - Nouvel essai unique et seulement pour les erreurs de transport ; jamais pour 4xx (401/403/404/409/422).
import { QueryClient } from "@tanstack/react-query";
import type { ApiError, ApiResult } from "./client.ts";

/** Erreur levée par les fonctions de requête/mutation : porte l'ApiError typée. */
export class ApiRequestError extends Error {
  readonly error: ApiError;

  constructor(error: ApiError) {
    super(error.message);
    this.name = "ApiRequestError";
    this.error = error;
  }
}

declare module "@tanstack/react-query" {
  interface Register {
    defaultError: ApiRequestError;
  }
}

/** Convertit un ApiResult en valeur ou en ApiRequestError (contrat attendu par TanStack Query). */
export function unwrap<T>(result: ApiResult<T>): T {
  if (!result.ok) throw new ApiRequestError(result.error);
  return result.data;
}

const RETRYABLE: ReadonlySet<ApiError["kind"]> = new Set(["network", "timeout"]);

export function shouldRetry(failureCount: number, error: unknown): boolean {
  return failureCount < 1 && error instanceof ApiRequestError && RETRYABLE.has(error.error.kind);
}

export function createMmvQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: shouldRetry,
        // Données partagées entre postes : fraîcheur courte, rafraîchies au retour sur la fenêtre.
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: true,
        refetchOnReconnect: true,
      },
      mutations: { retry: false },
    },
  });
}
