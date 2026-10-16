// Session d'authentification du client (phase 02, variante DEV PROVISOIRE, ADR-007 PROPOSED).
// Le jeton opaque vit uniquement dans l'état React de ce fournisseur (mémoire du processus WebView2) :
// il disparaît à la fermeture ou au rechargement de la fenêtre. Aucune persistance, aucun journal.
// Ce fournisseur ne décide d'aucun droit : chaque appel protégé est autorisé ou refusé par MMV.Api.
import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { fetchSession, login, logout } from "../api/auth.ts";
import { type ApiBaseUrlResult, apiBaseUrl as configuredBaseUrl } from "../api/config.ts";
import { apiError, type ApiError, type ApiResult } from "../api/client.ts";
import { AuthContext, type AuthContextValue, type AuthNotice, type AuthState } from "./authContext.ts";

interface AuthProviderProps {
  children: ReactNode;
  /** Injectable pour les tests ; par défaut, l'origine configurée à la compilation. */
  baseUrl?: ApiBaseUrlResult;
}

export function AuthProvider({ children, baseUrl = configuredBaseUrl }: AuthProviderProps) {
  const [state, setState] = useState<AuthState>({ status: "anonymous", notice: null });
  // Le jeton n'est pas dans `state` (exposé aux composants) : seul ce fournisseur le lit.
  const tokenRef = useRef<string | null>(null);
  const expiryTimer = useRef<number | null>(null);

  const clearExpiryTimer = () => {
    if (expiryTimer.current !== null) window.clearTimeout(expiryTimer.current);
    expiryTimer.current = null;
  };

  const dropSession = useCallback((notice: AuthNotice) => {
    tokenRef.current = null;
    clearExpiryTimer();
    setState({ status: "anonymous", notice });
  }, []);

  const authorized = useCallback(
    async <T,>(call: (url: URL, token: string) => Promise<ApiResult<T>>): Promise<ApiResult<T>> => {
      const token = tokenRef.current;
      if (!baseUrl.ok) return { ok: false, error: apiError("not-configured") };
      if (token === null) return { ok: false, error: apiError("unauthorized") };
      const result = await call(baseUrl.url, token);
      // Un jeton remplacé entre-temps (déconnexion, nouvelle session) ne doit pas fermer la nouvelle session.
      if (!result.ok && result.error.kind === "unauthorized" && tokenRef.current === token) {
        dropSession("session-expired");
      }
      return result;
    },
    [baseUrl, dropSession],
  );

  const refreshSession = useCallback(async () => {
    const result = await authorized((url, token) => fetchSession(url, token));
    if (result.ok) {
      const session = result.data;
      setState((current) => (current.status === "authenticated" ? { status: "authenticated", session } : current));
    }
    return result;
  }, [authorized]);

  const signIn = useCallback(
    async (userName: string, password: string): Promise<ApiError | null> => {
      if (!baseUrl.ok) return apiError("not-configured");
      const result = await login(baseUrl.url, userName, password);
      if (!result.ok) return result.error;
      const { accessToken, ...session } = result.data;
      tokenRef.current = accessToken;
      setState({ status: "authenticated", session });
      return null;
    },
    [baseUrl],
  );

  const signOut = useCallback(async () => {
    const token = tokenRef.current;
    if (token !== null && baseUrl.ok) {
      // Révocation serveur au mieux : même si l'API est injoignable, le jeton est oublié localement.
      await logout(baseUrl.url, token);
    }
    dropSession("signed-out");
  }, [baseUrl, dropSession]);

  // Échéance absolue connue : la session locale est fermée sans attendre un appel refusé.
  const expiresAtUtc = state.status === "authenticated" ? state.session.expiresAtUtc : null;
  useEffect(() => {
    clearExpiryTimer();
    if (expiresAtUtc === null) return;
    const delay = Date.parse(expiresAtUtc) - Date.now();
    if (Number.isNaN(delay)) return;
    // setTimeout est borné à ~24,8 jours ; une session dev dure quelques heures.
    expiryTimer.current = window.setTimeout(() => dropSession("session-expired"), Math.max(0, Math.min(delay, 2 ** 31 - 1)));
    return clearExpiryTimer;
  }, [expiresAtUtc, dropSession]);

  const value = useMemo<AuthContextValue>(
    () => ({ state, baseUrl, signIn, signOut, authorized, refreshSession }),
    [state, baseUrl, signIn, signOut, authorized, refreshSession],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
