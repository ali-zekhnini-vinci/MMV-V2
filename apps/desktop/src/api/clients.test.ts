// Contrat Clients PROPOSED (phase 03-A) : liste blanche des paramètres, parseurs stricts, erreurs typées.
// Exécuté contre le double node:http (src/test/fakeClientsApi.ts), avec des clients FICTIFS de test.
import assert from "node:assert/strict";
import { afterAll, beforeAll, describe, test } from "vitest";
import { CLIENT_PERMISSIONS } from "../test/fakeClientsApi.ts";
import { type FakeApi, startFakeApi } from "../test/fakeMmvApi.ts";
import { login } from "./auth.ts";
import { parseErrorBody } from "./client.ts";
import {
  archiveClient,
  type ClientListParams,
  clientListQuery,
  createClient,
  getClient,
  listClients,
  parseClientDetail,
  parseClientPage,
  reactivateClient,
  updateClient,
} from "./clients.ts";

const PARAMS: ClientListParams = { search: "", status: "active", sort: "name", direction: "asc", page: 1, pageSize: 20 };
const ALL = Object.values(CLIENT_PERMISSIONS);

describe("paramètres de liste (liste blanche)", () => {
  test("valeurs par défaut explicites, recherche trop courte ignorée", () => {
    assert.equal(clientListQuery({ ...PARAMS, search: " a " }), "status=active&sort=name&dir=asc&page=1&pageSize=20");
  });

  test("recherche rognée et bornée, valeurs hors liste remplacées", () => {
    const query = new URLSearchParams(
      clientListQuery({
        ...PARAMS,
        search: `  ${"x".repeat(150)}  `,
        status: "deleted" as never,
        sort: "email" as never,
        direction: "sideways" as never,
        page: -3,
        pageSize: 5000,
      }),
    );
    assert.equal(query.get("q")?.length, 100);
    assert.equal(query.get("status"), "active");
    assert.equal(query.get("sort"), "name");
    assert.equal(query.get("dir"), "asc");
    assert.equal(query.get("page"), "1");
    assert.equal(query.get("pageSize"), "100");
  });
});

describe("parseurs (aucun faux succès)", () => {
  const detail = {
    id: "1",
    reference: "CLI-000001",
    firstName: "Test",
    lastName: "Fictif",
    phone: null,
    email: null,
    city: null,
    status: "active",
    createdAtUtc: "2026-01-01T00:00:00Z",
    version: 3,
    birthDate: null,
    addressLine: null,
    postalCode: null,
    notes: null,
    updatedAtUtc: "2026-01-01T00:00:00Z",
    archivedAtUtc: null,
  };

  test("fiche : statut, version entière et champs nullables exigés", () => {
    assert.equal(parseClientDetail(detail)?.version, 3);
    assert.equal(parseClientDetail({ ...detail, status: "deleted" }), null);
    assert.equal(parseClientDetail({ ...detail, version: 1.5 }), null);
    assert.equal(parseClientDetail({ ...detail, phone: 612 }), null);
    assert.equal(parseClientDetail({ ...detail, notes: undefined }), null);
  });

  test("page : compteurs entiers et chaque ligne valide", () => {
    assert.equal(parseClientPage({ items: [detail], page: 1, pageSize: 20, totalCount: 1 })?.items.length, 1);
    assert.equal(parseClientPage({ items: [{ ...detail, id: 7 }], page: 1, pageSize: 20, totalCount: 1 }), null);
    assert.equal(parseClientPage({ items: [], page: 1, pageSize: 20, totalCount: -1 }), null);
    assert.equal(parseClientPage([]), null);
  });

  test("corps d'erreur : seuls des codes bornés sont retenus", () => {
    assert.deepEqual(parseErrorBody({ code: "version_conflict" }), { code: "version_conflict" });
    assert.deepEqual(parseErrorBody({ code: "Texte libre <script>" }), {});
    assert.deepEqual(parseErrorBody({ errors: { lastName: ["required", 4], "bad field": ["x"], email: "invalid" } }), {
      fieldErrors: { lastName: ["required"] },
    });
    assert.deepEqual(parseErrorBody("erreur"), {});
  });
});

describe("parcours HTTP (double)", () => {
  let api: FakeApi;
  let full: string;
  let readOnly: string;
  let none: string;

  beforeAll(async () => {
    api = await startFakeApi({
      accounts: [
        { userName: "gestion", password: "Mot-de-passe-1", displayName: "Gestion", permissions: [...ALL] },
        { userName: "lecture", password: "Mot-de-passe-2", displayName: "Lecture", permissions: [CLIENT_PERMISSIONS.read] },
        { userName: "admin", password: "Mot-de-passe-3", displayName: "Admin", permissions: ["system.admin"] },
      ],
      clients: Array.from({ length: 25 }, (_, index) => ({ firstName: `Prénom${index}`, lastName: `Fictif${String(index).padStart(2, "0")}` })),
    });
    const token = async (userName: string, password: string) => {
      const result = await login(api.url, userName, password);
      assert.ok(result.ok);
      return result.data.accessToken;
    };
    full = await token("gestion", "Mot-de-passe-1");
    readOnly = await token("lecture", "Mot-de-passe-2");
    none = await token("admin", "Mot-de-passe-3");
  });
  afterAll(() => api.stop());

  test("liste paginée côté serveur", async () => {
    const second = await listClients(api.url, full, { ...PARAMS, page: 2 });
    assert.ok(second.ok);
    assert.equal(second.data.totalCount, 25);
    assert.equal(second.data.items.length, 5);
    assert.equal(second.data.items[0].lastName, "Fictif20");
  });

  test("system.admin n'emporte aucun droit Clients (403)", async () => {
    const result = await listClients(api.url, none, PARAMS);
    assert.equal(!result.ok && result.error.kind, "forbidden");
  });

  test("lecture seule : création refusée (403), fiche lisible, 404 typé", async () => {
    const created = await createClient(api.url, readOnly, {
      firstName: "A",
      lastName: "B",
      birthDate: null,
      phone: null,
      email: null,
      addressLine: null,
      city: null,
      postalCode: null,
      notes: null,
    });
    assert.equal(!created.ok && created.error.kind, "forbidden");
    const missing = await getClient(api.url, readOnly, "inconnu");
    assert.equal(!missing.ok && missing.error.kind, "not-found");
    assert.equal(!missing.ok && missing.error.code, "client_not_found");
  });

  test("création 201, validation 422 avec codes par champ", async () => {
    const empty = { firstName: "", lastName: " ", birthDate: null, phone: null, email: null, addressLine: null, city: null, postalCode: null, notes: null };
    const refused = await createClient(api.url, full, empty);
    assert.equal(!refused.ok && refused.error.kind, "validation");
    assert.deepEqual(!refused.ok && refused.error.fieldErrors, { firstName: ["required"], lastName: ["required"] });
    const created = await createClient(api.url, full, { ...empty, firstName: "Nouveau", lastName: "Fictif" });
    assert.ok(created.ok);
    assert.equal(created.status, 201);
    assert.equal(created.data.version, 1);
  });

  test("concurrence optimiste : version périmée -> 409, archivage puis réactivation", async () => {
    const page = await listClients(api.url, full, PARAMS);
    assert.ok(page.ok);
    const target = page.data.items[0];
    const input = { firstName: target.firstName, lastName: target.lastName, birthDate: null, phone: null, email: null, addressLine: null, city: "Ville", postalCode: null, notes: null };
    const updated = await updateClient(api.url, full, target.id, input, target.version);
    assert.ok(updated.ok);
    const stale = await updateClient(api.url, full, target.id, input, target.version);
    assert.equal(!stale.ok && stale.error.kind, "conflict");
    assert.equal(!stale.ok && stale.error.code, "version_conflict");

    const forbidden = await archiveClient(api.url, readOnly, target.id, updated.data.version);
    assert.equal(!forbidden.ok && forbidden.error.kind, "forbidden");
    const archived = await archiveClient(api.url, full, target.id, updated.data.version);
    assert.ok(archived.ok);
    assert.equal(archived.data.status, "archived");
    const active = await listClients(api.url, full, PARAMS);
    assert.equal(active.ok && active.data.items.some((item) => item.id === target.id), false);
    const restored = await reactivateClient(api.url, full, target.id, archived.data.version);
    assert.equal(restored.ok && restored.data.status, "active");
  });

  test("le jeton n'est jamais dans l'URL et chaque appel porte un Bearer", () => {
    const clientCalls = api.requests.filter((request) => request.path.startsWith("/api/clients"));
    assert.ok(clientCalls.length > 0);
    for (const call of clientCalls) {
      assert.equal(call.bearer, true);
      assert.doesNotMatch(call.path, /token|fake-token/i);
    }
  });
});
