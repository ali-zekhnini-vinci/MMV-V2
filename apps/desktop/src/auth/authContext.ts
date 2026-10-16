// Contexte d'authentification partagé (types + hook), séparé du fournisseur pour Fast Refresh.
import { createContext, useContext } from "react";
import type { SessionInfo } from "../api/auth.ts";
import type { ApiError, ApiResult } from "../api/client.ts";
import type { ApiBaseUrlResult } from "../api/config.ts";

export type AuthNotice = "session-expired" | "signed-out" | null;

export type AuthState =
  | { status: "anonymous"; notice: AuthNotice }
  | { status: "authenticated"; session: SessionInfo };

export interface AuthContextValue {
  state: AuthState;
  baseUrl: ApiBaseUrlResult;
  /** `null` si la session est ouverte, sinon l'erreur bornée à présenter. */
  signIn: (userName: string, password: string) => Promise<ApiError | null>;
  /** Révoque la session côté serveur (au mieux) puis oublie le jeton localement dans tous les cas. */
  signOut: () => Promise<void>;
  /**
   * Exécute un appel protégé avec le jeton courant. Un 401 ferme la session locale
   * (« session expirée ») ; un 403 est renvoyé tel quel à l'écran appelant.
   */
  authorized: <T>(call: (baseUrl: URL, token: string) => Promise<ApiResult<T>>) => Promise<ApiResult<T>>;
  /** Revalide la session auprès du serveur (navigation, rechargement). */
  refreshSession: () => Promise<ApiResult<SessionInfo>>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (value === null) throw new Error("useAuth doit être utilisé sous <AuthProvider>.");
  return value;
}
