// Double de test de MMV.Api pour les tests du client : vrai serveur node:http sur 127.0.0.1, état en
// mémoire du processus de test (aucune persistance, aucun fetch simulé). Il reproduit les CONTRATS de
// src/MMV.Api/Auth/ (statuts, corps), pas leur sécurité : la preuve serveur est dans tests/MMV.*.Tests.
import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import type { AddressInfo } from "node:net";
import { type FakeAppointmentRecord, type FakeAppointmentsOptions, FAKE_APPOINTMENT_OPTIONS, handleAppointments, seedAppointments } from "./fakeAppointmentsApi.ts";
import { type FakeAuditOptions, handleAudit } from "./fakeAuditApi.ts";
import { type FakeClientRecord, type FakeClientsOptions, handleClients, seedClients } from "./fakeClientsApi.ts";

export interface FakeAccount {
  userName: string;
  password: string;
  displayName: string;
  permissions: string[];
  active?: boolean;
}

export interface FakeApiOptions extends FakeClientsOptions, FakeAppointmentsOptions, FakeAuditOptions {
  accounts?: FakeAccount[];
  /** Simule une base indisponible (/health/ready 503). */
  databaseDown?: boolean;
  /** Retarde la réponse de /api/auth/login (état « Connexion… »). */
  loginDelayMs?: number;
  /** Force un statut HTTP pour /api/auth/login (ex. 500, 429). */
  loginStatus?: number;
}

export interface FakeApi {
  url: URL;
  options: FakeApiOptions;
  /** Requêtes reçues (méthode, chemin, présence d'un Bearer) — jamais le jeton lui-même. */
  requests: { method: string; path: string; bearer: boolean }[];
  /** Révoque toutes les sessions (simule une révocation/expiration serveur). */
  revokeAll: () => void;
  /** Retire une permission à un compte (sessions comprises). */
  removePermission: (userName: string, permission: string) => void;
  activeSessions: () => number;
  /** Clients fictifs du double (phase 03-A), modifiables pour simuler un autre poste. */
  clients: FakeClientRecord[];
  /** Rendez-vous fictifs du double (phase 03-B, contrat PROPOSÉ), modifiables pour simuler un autre poste. */
  appointments: FakeAppointmentRecord[];
  stop: () => Promise<void>;
}

const LIVE = {
  status: "Healthy",
  scope: "process",
  processId: 4242,
  startedAtUtc: "2026-10-16T08:00:00Z",
  uptimeSeconds: 30,
  checkedAtUtc: "2026-10-16T08:00:30Z",
};

export async function startFakeApi(options: FakeApiOptions = {}): Promise<FakeApi> {
  const accounts = (options.accounts ?? []).map((account) => ({ ...account, active: account.active ?? true }));
  const sessions = new Map<string, string>(); // jeton -> userName
  const requests: FakeApi["requests"] = [];
  let counter = 0;
  const clients = seedClients(options.clients ?? []);
  const appointments = seedAppointments(options.appointments ?? [], clients, options.appointmentOptions ?? FAKE_APPOINTMENT_OPTIONS);

  const send = (response: ServerResponse, status: number, body?: unknown) => {
    response.writeHead(status, body === undefined ? {} : { "content-type": "application/json" });
    response.end(body === undefined ? undefined : JSON.stringify(body));
  };

  const readBody = (request: IncomingMessage) =>
    new Promise<unknown>((resolve) => {
      let raw = "";
      request.on("data", (chunk: Buffer) => (raw += chunk.toString("utf8")));
      request.on("end", () => {
        try {
          resolve(JSON.parse(raw));
        } catch {
          resolve(null);
        }
      });
    });

  const userView = (account: (typeof accounts)[number]) => ({
    id: `00000000-0000-7000-8000-${String(accounts.indexOf(account)).padStart(12, "0")}`,
    userName: account.userName,
    displayName: account.displayName,
    permissions: account.permissions,
  });

  const sessionBody = (account: (typeof accounts)[number]) => ({
    expiresAtUtc: new Date(Date.now() + 8 * 3600_000).toISOString(),
    idleTimeoutSeconds: 1800,
    user: userView(account),
  });

  const authenticate = (request: IncomingMessage) => {
    const header = request.headers.authorization ?? "";
    const token = header.startsWith("Bearer ") ? header.slice(7) : null;
    const userName = token === null ? undefined : sessions.get(token);
    const account = accounts.find((candidate) => candidate.userName === userName && candidate.active);
    return account ? { token: token as string, account } : null;
  };

  const server: Server = createServer((request, response) => {
    void (async () => {
      const path = request.url ?? "";
      const method = request.method ?? "GET";
      requests.push({ method, path, bearer: (request.headers.authorization ?? "").startsWith("Bearer ") });

      if (method === "GET" && path === "/health/live") return send(response, 200, LIVE);
      if (method === "GET" && path === "/health/ready") {
        const healthy = !options.databaseDown;
        return send(response, healthy ? 200 : 503, {
          status: healthy ? "Healthy" : "Unhealthy",
          scope: "database",
          checkedAtUtc: new Date().toISOString(),
          durationMs: 3,
        });
      }

      if (method === "POST" && path === "/api/auth/login") {
        const body = (await readBody(request)) as { userName?: string; password?: string } | null;
        if (options.loginDelayMs) await new Promise((resolve) => setTimeout(resolve, options.loginDelayMs));
        if (options.loginStatus) return send(response, options.loginStatus, { code: "forced" });
        const account = accounts.find(
          (candidate) => candidate.userName === body?.userName && candidate.password === body?.password && candidate.active,
        );
        if (!account) return send(response, 401, { code: "invalid_credentials" });
        counter += 1;
        const token = `fake-token-${counter}-${"x".repeat(40)}`;
        sessions.set(token, account.userName);
        return send(response, 200, { accessToken: token, tokenType: "Bearer", ...sessionBody(account) });
      }

      const auth = authenticate(request);
      if (path.startsWith("/api/") && auth === null) return send(response, 401, { code: "unauthenticated" });

      if (method === "POST" && path === "/api/auth/logout" && auth) {
        sessions.delete(auth.token);
        return send(response, 204);
      }
      if (method === "GET" && path === "/api/auth/session" && auth) return send(response, 200, sessionBody(auth.account));
      if (method === "GET" && path === "/api/admin/accounts" && auth) {
        if (!auth.account.permissions.includes("system.admin")) return send(response, 403, { code: "forbidden" });
        return send(
          response,
          200,
          accounts.map((account) => ({
            ...userView(account),
            isActive: account.active,
            createdAtUtc: "2026-10-16T08:00:00Z",
          })),
        );
      }
      if (auth && (path === "/api/clients" || path.startsWith("/api/clients/") || path.startsWith("/api/clients?"))) {
        return handleClients(request, response, {
          permissions: auth.account.permissions,
          clients,
          options,
          send,
          readBody: () => readBody(request),
        });
      }
      if (auth && (path === "/api/appointments" || path.startsWith("/api/appointments/") || path.startsWith("/api/appointments?"))) {
        return handleAppointments(request, response, {
          permissions: auth.account.permissions,
          appointments,
          clients,
          options,
          send,
          readBody: () => readBody(request),
          userDisplayName: auth.account.displayName,
        });
      }
      if (auth && path.startsWith("/api/audit/")) {
        return handleAudit(request, response, { permissions: auth.account.permissions, clients, options, send });
      }
      return send(response, 404, { code: "not_found" });
    })();
  });

  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  const { port } = server.address() as AddressInfo;

  return {
    url: new URL(`http://127.0.0.1:${port}`),
    options,
    requests,
    revokeAll: () => sessions.clear(),
    removePermission: (userName, permission) => {
      const account = accounts.find((candidate) => candidate.userName === userName);
      if (account) account.permissions = account.permissions.filter((item) => item !== permission);
    },
    activeSessions: () => sessions.size,
    clients,
    appointments,
    stop: () =>
      new Promise<void>((resolve) => {
        server.closeAllConnections();
        server.close(() => resolve());
      }),
  };
}
