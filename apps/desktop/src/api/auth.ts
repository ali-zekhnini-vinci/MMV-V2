// Contrats de l'authentification MMV.Api (phase 02), alignés sur src/MMV.Api/Auth/AuthEndpoints.cs.
// Le jeton opaque reçu n'est conservé qu'en mémoire volatile par l'appelant (auth/AuthProvider.tsx) :
// jamais dans localStorage, sessionStorage, IndexedDB, cookies, fichiers ni journaux.
import { type ApiResult, noContent, requestJson } from "./client.ts";

/** Permissions techniques connues du client ; le serveur reste seul juge (policies ASP.NET Core). */
export const PERMISSION_SYSTEM_ADMIN = "system.admin";
export const PERMISSION_CLIENTS_WRITE = "clients.write";
export const PERMISSION_CLIENTS_ARCHIVE = "clients.archive";
export const PERMISSION_CLIENTS_READ = "clients.read";
/** Rendez-vous (03-B) : droits PROPOSÉS (D-03-17), inconnus du serveur tant que le propriétaire ne les a pas ratifiés. */
export const PERMISSION_APPOINTMENTS_WRITE = "appointments.write";
export const PERMISSION_APPOINTMENTS_CANCEL = "appointments.cancel";

export interface SessionUser {
  id: string;
  userName: string;
  displayName: string;
  permissions: string[];
}

export interface SessionInfo {
  expiresAtUtc: string;
  idleTimeoutSeconds: number;
  user: SessionUser;
}

export interface LoginSuccess extends SessionInfo {
  accessToken: string;
}

export interface AccountSummary {
  id: string;
  userName: string;
  displayName: string;
  isActive: boolean;
  permissions: string[];
  createdAtUtc: string;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const isStringArray = (value: unknown): value is string[] =>
  Array.isArray(value) && value.every((item) => typeof item === "string");

export function parseSessionUser(value: unknown): SessionUser | null {
  if (!isRecord(value)) return null;
  const { id, userName, displayName, permissions } = value;
  if (typeof id !== "string" || typeof userName !== "string" || typeof displayName !== "string") return null;
  if (!isStringArray(permissions)) return null;
  return { id, userName, displayName, permissions: [...permissions] };
}

export function parseSessionInfo(body: unknown): SessionInfo | null {
  if (!isRecord(body)) return null;
  const { expiresAtUtc, idleTimeoutSeconds, user } = body;
  const parsedUser = parseSessionUser(user);
  if (typeof expiresAtUtc !== "string" || typeof idleTimeoutSeconds !== "number" || parsedUser === null) return null;
  return { expiresAtUtc, idleTimeoutSeconds, user: parsedUser };
}

export function parseLoginSuccess(body: unknown): LoginSuccess | null {
  if (!isRecord(body)) return null;
  const { accessToken, tokenType } = body;
  const info = parseSessionInfo(body);
  if (typeof accessToken !== "string" || accessToken.length < 32 || tokenType !== "Bearer" || info === null) {
    return null;
  }
  return { ...info, accessToken };
}

export function parseAccounts(body: unknown): AccountSummary[] | null {
  if (!Array.isArray(body)) return null;
  const accounts: AccountSummary[] = [];
  for (const item of body) {
    if (!isRecord(item)) return null;
    const { id, userName, displayName, isActive, permissions, createdAtUtc } = item;
    if (
      typeof id !== "string" ||
      typeof userName !== "string" ||
      typeof displayName !== "string" ||
      typeof isActive !== "boolean" ||
      !isStringArray(permissions) ||
      typeof createdAtUtc !== "string"
    ) {
      return null;
    }
    accounts.push({ id, userName, displayName, isActive, permissions: [...permissions], createdAtUtc });
  }
  return accounts;
}

export const login = (
  baseUrl: URL,
  userName: string,
  password: string,
  signal?: AbortSignal,
): Promise<ApiResult<LoginSuccess>> =>
  requestJson(baseUrl, "/api/auth/login", {
    method: "POST",
    body: { userName, password },
    parse: parseLoginSuccess,
    signal,
  });

export const fetchSession = (baseUrl: URL, token: string, signal?: AbortSignal): Promise<ApiResult<SessionInfo>> =>
  requestJson(baseUrl, "/api/auth/session", { token, parse: parseSessionInfo, signal });

export const logout = (baseUrl: URL, token: string, signal?: AbortSignal): Promise<ApiResult<true>> =>
  requestJson(baseUrl, "/api/auth/logout", {
    method: "POST",
    token,
    parse: noContent,
    acceptedStatuses: [204],
    signal,
  });

export const fetchAccounts = (
  baseUrl: URL,
  token: string,
  signal?: AbortSignal,
): Promise<ApiResult<AccountSummary[]>> =>
  requestJson(baseUrl, "/api/admin/accounts", { token, parse: parseAccounts, signal });
