// Contrats d'authentification du client contre un double node:http réel (aucun mock de fetch).
import assert from "node:assert/strict";
import { afterAll, beforeAll, describe, test } from "vitest";
import { type FakeApi, startFakeApi } from "../test/fakeMmvApi.ts";
import { fetchAccounts, fetchSession, login, logout, parseAccounts, parseLoginSuccess, parseSessionInfo } from "./auth.ts";

const user = { id: "u1", userName: "admin", displayName: "Admin", permissions: ["system.admin"] };

describe("parseurs (aucun faux succès)", () => {
  test("login : jeton Bearer suffisamment long exigé", () => {
    const base = { tokenType: "Bearer", expiresAtUtc: "x", idleTimeoutSeconds: 1, user };
    assert.equal(parseLoginSuccess({ ...base, accessToken: "court" }), null);
    assert.equal(parseLoginSuccess({ ...base, tokenType: "Cookie", accessToken: "a".repeat(43) }), null);
    assert.equal(parseLoginSuccess({ ...base, accessToken: "a".repeat(43) })?.user.userName, "admin");
  });

  test("session : utilisateur et permissions typés", () => {
    assert.equal(parseSessionInfo({ expiresAtUtc: "x", idleTimeoutSeconds: 1, user: { ...user, permissions: [1] } }), null);
    assert.equal(parseSessionInfo({ expiresAtUtc: "x", idleTimeoutSeconds: 1, user })?.user.displayName, "Admin");
  });

  test("comptes : tableau strict", () => {
    assert.equal(parseAccounts({}), null);
    assert.equal(parseAccounts([{ ...user, isActive: "oui", createdAtUtc: "x" }]), null);
    assert.equal(parseAccounts([{ ...user, isActive: true, createdAtUtc: "x" }])?.length, 1);
  });
});

describe("parcours HTTP", () => {
  let api: FakeApi;

  beforeAll(async () => {
    api = await startFakeApi({
      accounts: [
        { userName: "admin", password: "Mot-de-passe-1", displayName: "Admin", permissions: ["system.admin"] },
        { userName: "simple", password: "Mot-de-passe-2", displayName: "Simple", permissions: [] },
      ],
    });
  });
  afterAll(() => api.stop());

  test("identifiants invalides -> unauthorized (401), message neutre", async () => {
    const result = await login(api.url, "admin", "faux");
    assert.equal(!result.ok && result.error.kind, "unauthorized");
    assert.equal(!result.ok && result.error.status, 401);
  });

  test("login, session, comptes, logout puis jeton refusé", async () => {
    const result = await login(api.url, "admin", "Mot-de-passe-1");
    assert.ok(result.ok);
    const token = result.data.accessToken;

    const session = await fetchSession(api.url, token);
    assert.equal(session.ok && session.data.user.userName, "admin");
    assert.equal(api.requests.at(-1)?.bearer, true);

    const accounts = await fetchAccounts(api.url, token);
    assert.equal(accounts.ok && accounts.data.length, 2);

    const out = await logout(api.url, token);
    assert.equal(out.ok && out.status, 204);

    const after = await fetchSession(api.url, token);
    assert.equal(!after.ok && after.error.kind, "unauthorized");
  });

  test("compte sans system.admin -> forbidden (403) sur /api/admin/accounts", async () => {
    const result = await login(api.url, "simple", "Mot-de-passe-2");
    assert.ok(result.ok);
    const accounts = await fetchAccounts(api.url, result.data.accessToken);
    assert.equal(!accounts.ok && accounts.error.kind, "forbidden");
  });

  test("429 -> rate-limited", async () => {
    api.options.loginStatus = 429;
    try {
      const result = await login(api.url, "admin", "Mot-de-passe-1");
      assert.equal(!result.ok && result.error.kind, "rate-limited");
    } finally {
      api.options.loginStatus = undefined;
    }
  });
});
