// Passerelle HTTP du dossier optique (04-B) : cohérence avec le contrat OpenAPI RÉELLEMENT généré par MMV.Api
// (instantané src/api/contract/optical.openapi.json, comparé au serveur par OpticalOpenApiContractTests.cs),
// conversion `null` ↔ `""` sans aucune réécriture des valeurs saisies, et IDEMPOTENCE des écritures (réponse incertaine
// rejouée avec le même identifiant ; création incertaine jamais dupliquée). Serveur simulé par `fetch` (aucun réseau).
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { afterEach, describe, test, vi } from "vitest";
import { emptyContent, knownActors, type PrescriptionContent } from "../optical/opticalContract.ts";
import type { ApiResult } from "./client.ts";
import { contentBody, createHttpOpticalGateway, opticalListQuery, parseOpticalRecord } from "./optical.ts";

interface Schema {
  properties?: Record<string, Schema>;
  required?: string[];
}

const contract = JSON.parse(readFileSync(new URL("./contract/optical.openapi.json", import.meta.url), "utf8")) as {
  paths: Record<string, Record<string, unknown>>;
  components: { schemas: Record<string, Schema> };
};
const schemas = contract.components.schemas;
const keys = (name: string) => Object.keys(schemas[name]?.properties ?? {}).sort();

const BASE = new URL("https://localhost:7267");
const authorized = <T,>(call: (baseUrl: URL, token: string) => Promise<ApiResult<T>>) => call(BASE, "jeton-fictif");

const actor = { id: "u-1", displayName: "Fictif Auteur" };
const eye = (sphere: string | null) => ({ sphere, cylinder: null, axis: null, addition: null, prism: null, base: null, acuity: null });
const revision = (status: "draft" | "confirmed", sphere: string | null, revisionNo: number | null) => ({
  id: `rev-${status}`,
  status,
  revisionNo,
  baseRevisionNo: null,
  reasonCode: null,
  reasonComment: null,
  content: {
    provenance: { prescriberName: "Dr Fictif", prescriberQualification: null, prescriberIdentifier: null, prescribedOn: "2026-10-20", receivedOn: null },
    od: eye(sphere),
    og: eye(null),
    notes: null,
  },
  startedAtUtc: "2026-10-27T09:00:00Z",
  startedBy: actor,
  updatedAtUtc: "2026-10-27T09:00:00Z",
  updatedBy: actor,
  confirmedAtUtc: status === "confirmed" ? "2026-10-27T09:00:00Z" : null,
  confirmedBy: status === "confirmed" ? actor : null,
  discardedAtUtc: null,
  discardedBy: null,
});
const record = (status: "draft" | "confirmed", sphere: string | null, version = 7) => ({
  kind: "prescription",
  id: "rx-1",
  reference: "RX-000001",
  client: { id: "c-1", reference: "CLI-000001", displayName: "FICTIF Patient", status: "active" },
  lifecycle: "active",
  invalidation: null,
  lastConfirmed: status === "confirmed" ? revision("confirmed", sphere, 1) : null,
  openDraft: status === "draft" ? revision("draft", sphere, null) : null,
  revisions: [revision(status, sphere, status === "confirmed" ? 1 : null)],
  historyAvailable: false,
  events: [],
  citedUsers: [],
  createdAtUtc: "2026-10-27T09:00:00Z",
  createdBy: actor,
  version,
});

function content(sphere: string): PrescriptionContent {
  const value = emptyContent("prescription") as PrescriptionContent;
  return { ...value, provenance: { ...value.provenance, prescriberName: "Dr Fictif", prescribedOn: "2026-10-20" }, od: { ...value.od, sphere } };
}

type Reply = { status: number; body?: unknown } | "network";
function fakeServer(replies: Reply[]) {
  const calls: { method: string; path: string; body: Record<string, unknown> | null }[] = [];
  vi.stubGlobal("fetch", async (url: URL, init: RequestInit) => {
    calls.push({ method: init.method ?? "GET", path: `${url.pathname}${url.search}`, body: init.body ? (JSON.parse(String(init.body)) as Record<string, unknown>) : null });
    const reply = replies.shift() ?? { status: 500 };
    if (reply === "network") throw new TypeError("réseau coupé");
    return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), { status: reply.status, headers: { "Content-Type": "application/json" } });
  });
  return calls;
}

afterEach(() => vi.unstubAllGlobals());

describe("contrat OpenAPI du dossier optique ↔ passerelle", () => {
  test("deux familles d'écriture distinctes, aucune route DELETE ni documentaire", () => {
    const paths = Object.keys(contract.paths);
    for (const family of ["prescriptions", "measurements"]) {
      for (const suffix of ["", "/{id}", "/{id}/draft", "/{id}/draft/confirm", "/{id}/draft/discard", "/{id}/rectifications", "/{id}/invalidate"]) {
        assert.ok(paths.includes(`/api/optical/${family}${suffix}`), `${family}${suffix}`);
      }
    }
    assert.ok(paths.every((path) => !/document|convert/.test(path)));
    assert.ok(Object.values(contract.paths).every((methods) => !("delete" in methods)));
  });

  test("corps envoyés = schémas du serveur (aucun champ « type », aucun état d'œil)", () => {
    const body = contentBody(content("+1,25"));
    assert.deepEqual(Object.keys(body).sort(), keys("PrescriptionContentRequest"));
    assert.deepEqual(Object.keys(body.provenance as object).sort(), keys("PrescriptionProvenanceRequest"));
    assert.deepEqual(Object.keys(body.od as object).sort(), keys("EyeValuesRequest"));
    const measurement = contentBody(emptyContent("measurement", "u-2"));
    assert.deepEqual(Object.keys(measurement.provenance as object).sort(), keys("MeasurementProvenanceRequest"));
    assert.deepEqual(keys("OpticalCreateRequestOfPrescriptionContentRequest"), ["clientId", "confirm", "content", "requestId"]);
    assert.deepEqual(keys("OpticalDraftRequestOfPrescriptionContentRequest"), ["content", "requestId", "version"]);
  });

  test("réponses lues = schémas du serveur", () => {
    assert.deepEqual(keys("OpticalRecordResponseOfPrescriptionProvenanceResponse"), Object.keys(record("draft", null)).sort());
    assert.deepEqual(keys("OpticalRevisionResponseOfPrescriptionProvenanceResponse"), Object.keys(revision("draft", null, null)).sort());
    assert.deepEqual(keys("EyeValuesResponse"), Object.keys(eye(null)).sort());
  });

  test("valeurs : vide → null à l'envoi, null → vide à la lecture, saisie jamais réécrite (signe, virgule, zéros)", () => {
    const body = contentBody({ ...content("−2,125"), notes: "  " });
    assert.equal((body.od as Record<string, unknown>).sphere, "−2,125");
    assert.equal((body.od as Record<string, unknown>).cylinder, null);
    assert.equal((body.og as Record<string, unknown>).sphere, null);
    assert.equal(body.notes, null);
    const parsed = parseOpticalRecord(record("confirmed", "0,00"));
    assert.ok(parsed);
    assert.equal(parsed.lastConfirmed?.content.od.sphere, "0,00", "0,00 reste une valeur explicite");
    assert.equal(parsed.lastConfirmed?.content.od.cylinder, "", "absent ≠ 0");
    assert.equal(parsed.lastConfirmed?.content.kind, "prescription");
    assert.equal(parseOpticalRecord({ ...record("draft", null), historyAvailable: "oui" }), null, "réponse incohérente refusée");
  });

  test("04-C (O-04-48) : collaborateur cité servi avec l'enregistrement, nommé même hors de la liste des habilités", () => {
    const cited = { id: "u-ancien", displayName: "Collaborateur désactivé" };
    const parsed = parseOpticalRecord({ ...record("confirmed", "+0,75"), citedUsers: [cited] });
    assert.deepEqual(parsed?.citedUsers, [cited]);
    assert.equal(parseOpticalRecord({ ...record("confirmed", "+0,75"), citedUsers: undefined }), null, "champ exigé par le contrat");
    assert.equal(parseOpticalRecord({ ...record("confirmed", "+0,75"), citedUsers: [{ id: 1 }] }), null, "acteur mal formé refusé");
    const active = [{ id: "u-actif", displayName: "Collaborateur actif" }];
    assert.deepEqual(knownActors(active, [cited, active[0]]), [...active, cited], "liste active complétée, sans doublon");
  });

  test("liste : paramètres de liste blanche uniquement", () => {
    assert.equal(
      opticalListQuery({ search: " Fictif ", kind: "measurement", status: "rectification", includeInvalidated: true, sort: "client:asc", page: 2, pageSize: 8 }),
      "q=Fictif&kind=measurement&status=rectification&includeInvalidated=true&sort=client&dir=asc&page=2&pageSize=8",
    );
  });
});

describe("idempotence des écritures", () => {
  test("« Enregistrer » : réponse incertaine rejouée UNE fois avec le même identifiant", async () => {
    const calls = fakeServer(["network", { status: 200, body: record("confirmed", "+1,25") }]);
    const gateway = createHttpOpticalGateway(authorized);

    const result = await gateway.createAndConfirm("c-1", content("+1,25"));

    assert.ok(result.ok);
    assert.equal(calls.length, 2);
    assert.equal(calls[0].path, "/api/optical/prescriptions");
    assert.equal(calls[0].body?.confirm, true);
    assert.equal(calls[0].body?.requestId, calls[1].body?.requestId, "même identifiant de requête");
  });

  test("issue restée incertaine : la tentative suivante reprend le même identifiant (aucun doublon) ; une issue nette le libère", async () => {
    const calls = fakeServer(["network", "network", { status: 201, body: record("confirmed", "+1,25") }, { status: 201, body: record("confirmed", "+1,25") }]);
    const gateway = createHttpOpticalGateway(authorized);

    const first = await gateway.createAndConfirm("c-1", content("+1,25"));
    assert.equal(first.ok, false);
    const retry = await gateway.createAndConfirm("c-1", content("+1,25"));
    assert.ok(retry.ok);
    await gateway.createAndConfirm("c-1", content("+1,25"));

    assert.equal(new Set(calls.slice(0, 3).map((call) => call.body?.requestId)).size, 1);
    assert.notEqual(calls[3].body?.requestId, calls[0].body?.requestId, "nouvelle création après une issue nette");
  });

  test("création incertaine puis « Enregistrer » : le brouillon déjà créé est retrouvé et confirmé, jamais dupliqué", async () => {
    const calls = fakeServer(["network", "network", { status: 200, body: record("draft", "+1,00") }, { status: 200, body: record("confirmed", "+1,50") }]);
    const gateway = createHttpOpticalGateway(authorized);

    assert.equal((await gateway.createRecord("c-1", content("+1,00"))).ok, false);
    const saved = await gateway.createAndConfirm("c-1", content("+1,50"));

    assert.ok(saved.ok);
    assert.equal(calls[2].path, "/api/optical/prescriptions");
    assert.equal(calls[2].body?.requestId, calls[0].body?.requestId, "réémission de la création incertaine");
    assert.equal(calls[3].path, "/api/optical/prescriptions/rx-1/draft/confirm", "saisie courante appliquée et confirmée sur le même enregistrement");
    assert.equal(calls[3].body?.version, 7);
  });

  test("brouillon : saisie modifiée après une issue incertaine = nouvel identifiant (409 si la première avait abouti)", async () => {
    const calls = fakeServer(["network", "network", { status: 409, body: { code: "version_conflict" } }]);
    const gateway = createHttpOpticalGateway(authorized);

    await gateway.saveDraft("prescription", "rx-1", content("+1,00"), 7);
    const second = await gateway.saveDraft("prescription", "rx-1", content("+2,00"), 7);

    assert.equal(second.ok, false);
    assert.equal(second.ok ? null : second.error.code, "version_conflict");
    assert.notEqual(calls[2].body?.requestId, calls[0].body?.requestId);
  });

  test("refus net (422) : aucun rejeu", async () => {
    const calls = fakeServer([{ status: 422, body: { code: "incomplete", errors: { correction: ["sphere_zero_only"] } } }]);
    const gateway = createHttpOpticalGateway(authorized);

    const refused = await gateway.saveAndConfirm("prescription", "rx-1", content("0,00"), 7);

    assert.equal(refused.ok, false);
    assert.deepEqual(refused.ok ? null : refused.error.fieldErrors, { correction: ["sphere_zero_only"] });
    assert.equal(calls.length, 1);
  });
});
