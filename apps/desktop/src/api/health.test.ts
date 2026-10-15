// Tests du client API contre un vrai serveur HTTP local (node:http, 127.0.0.1) : aucun mock de fetch.
// Ils valident contrats, erreurs et timeout du client ; ils ne prouvent PAS le parcours
// Tauri -> HTTPS -> MMV.Api -> PostgreSQL (preuve E2E séparée, fenêtre native).
// Exécution : npm test  (Vitest, environnement node).
import assert from "node:assert/strict";
import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import type { AddressInfo } from "node:net";
import { afterAll, beforeAll, describe, test } from "vitest";
import { getJson } from "./client.ts";
import { resolveApiBaseUrl } from "./config.ts";
import { checkTechnicalStatus, fetchReady, parseLive, parseReady } from "./health.ts";

type Route = (request: IncomingMessage, response: ServerResponse) => void;

const live = {
  status: "Healthy",
  scope: "process",
  processId: 4242,
  startedAtUtc: "2026-10-15T10:00:00Z",
  uptimeSeconds: 12.5,
  checkedAtUtc: "2026-10-15T10:00:12Z",
};
const ready = (status: string) => ({ status, scope: "database", checkedAtUtc: "2026-10-15T10:00:12Z", durationMs: 7 });

const json = (status: number, body: unknown): Route => (_request, response) => {
  response.writeHead(status, { "content-type": "application/json" });
  response.end(JSON.stringify(body));
};

async function startServer(routes: Record<string, Route>): Promise<{ server: Server; url: URL }> {
  const server = createServer((request, response) => {
    const route = routes[request.url ?? ""];
    if (route) route(request, response);
    else json(404, {})(request, response);
  });
  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  const { port } = server.address() as AddressInfo;
  return { server, url: new URL(`http://127.0.0.1:${port}`) };
}

const stop = (server: Server) =>
  new Promise<void>((resolve) => {
    server.closeAllConnections();
    server.close(() => resolve());
  });

describe("resolveApiBaseUrl", () => {
  test("accepte une origine HTTPS et la normalise", () => {
    const result = resolveApiBaseUrl("https://localhost:7267/");
    assert.equal(result.ok && result.url.origin, "https://localhost:7267");
  });

  for (const raw of [undefined, "", "http://localhost:7267", "https://localhost:7267/api", "https://u:p@localhost:7267", "localhost:7267"]) {
    test(`refuse ${JSON.stringify(raw)}`, () => {
      assert.equal(resolveApiBaseUrl(raw).ok, false);
    });
  }
});

describe("contrats de santé", () => {
  test("parseLive exige scope process et Healthy", () => {
    assert.deepEqual(parseLive(live), live);
    assert.equal(parseLive({ ...live, scope: "database" }), null);
    assert.equal(parseLive({ ...live, processId: "4242" }), null);
  });

  test("parseReady refuse un 200 non Healthy et un 503 Healthy (pas de faux succès)", () => {
    assert.equal(parseReady(ready("Healthy"), 200)?.status, "Healthy");
    assert.equal(parseReady(ready("Unhealthy"), 503)?.status, "Unhealthy");
    assert.equal(parseReady(ready("Unhealthy"), 200), null);
    assert.equal(parseReady(ready("Healthy"), 503), null);
    assert.equal(parseReady(ready("Degraded"), 200), null);
  });
});

describe("client HTTP (serveur local réel)", () => {
  let server: Server;
  let url: URL;

  beforeAll(async () => {
    ({ server, url } = await startServer({
      "/health/live": json(200, live),
      "/health/ready": json(503, ready("Unhealthy")),
      "/broken": (_request, response) => {
        response.writeHead(200, { "content-type": "application/json" });
        response.end("{pas du json");
      },
      "/error": json(500, { detail: "stack trace interne" }),
      "/slow": (_request, response) => {
        setTimeout(() => json(200, live)(_request, response), 1000);
      },
    }));
  });

  afterAll(() => stop(server));

  test("503 de /health/ready est un contrat attendu, pas une erreur réseau", async () => {
    const result = await fetchReady(url);
    assert.equal(result.ok && result.status, 503);
    assert.equal(result.ok && result.data.status, "Unhealthy");
  });

  test("statut HTTP non attendu -> erreur http bornée, sans contenu serveur", async () => {
    const result = await getJson(url, "/error", { parse: parseLive });
    assert.equal(result.ok, false);
    assert.equal(!result.ok && result.error.kind, "http");
    assert.equal(!result.ok && result.error.status, 500);
    assert.doesNotMatch(!result.ok ? result.error.message : "", /stack/);
  });

  test("JSON invalide -> invalid-response", async () => {
    const result = await getJson(url, "/broken", { parse: parseLive });
    assert.equal(!result.ok && result.error.kind, "invalid-response");
  });

  test("timeout borné -> timeout", async () => {
    const started = Date.now();
    const result = await getJson(url, "/slow", { parse: parseLive, timeoutMs: 150 });
    assert.equal(!result.ok && result.error.kind, "timeout");
    assert.ok(Date.now() - started < 900);
  });

  test("annulation par l'appelant -> aborted", async () => {
    const controller = new AbortController();
    const pending = getJson(url, "/slow", { parse: parseLive, signal: controller.signal });
    controller.abort();
    const result = await pending;
    assert.equal(!result.ok && result.error.kind, "aborted");
  });

  test("API joignable + base indisponible -> database-unavailable", async () => {
    const status = await checkTechnicalStatus(url);
    assert.equal(status.state, "database-unavailable");
    assert.equal(status.state === "database-unavailable" && status.ready?.status, "Unhealthy");
  });
});

describe("états de l'écran socle", () => {
  test("API et base disponibles -> ready", async () => {
    const { server, url } = await startServer({
      "/health/live": json(200, live),
      "/health/ready": json(200, ready("Healthy")),
    });
    try {
      assert.equal((await checkTechnicalStatus(url)).state, "ready");
    } finally {
      await stop(server);
    }
  });

  test("API arrêtée (port fermé) -> api-unreachable / network", async () => {
    const { server, url } = await startServer({});
    await stop(server);
    const status = await checkTechnicalStatus(url);
    assert.equal(status.state, "api-unreachable");
    assert.equal(status.state === "api-unreachable" && status.error.kind, "network");
  });
});
