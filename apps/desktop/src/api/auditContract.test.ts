// Cohérence des types et parseurs TypeScript de l'historique (03-D2) avec le contrat OpenAPI RÉELLEMENT généré par
// MMV.Api : l'instantané src/api/contract/audit.openapi.json est comparé au document du serveur par
// tests/MMV.Api.Tests/Audit/AuditOpenApiContractTests.cs ; une dérive côté interface casse ce test-ci.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, test } from "vitest";
import { type AuditChange, type AuditEvent, auditQueryString, parseAuditPage } from "./audit.ts";

interface Schema {
  type?: string | string[];
  enum?: unknown[];
  required?: string[];
  properties?: Record<string, Schema>;
  $ref?: string;
}

const contract = JSON.parse(readFileSync(new URL("./contract/audit.openapi.json", import.meta.url), "utf8")) as {
  paths: Record<string, Record<string, { parameters?: { name: string; schema: Schema }[]; responses: Record<string, unknown> }>>;
  components: { schemas: Record<string, Schema> };
};
const schemas = contract.components.schemas;

type Nullability<T> = { [K in keyof T]-?: null extends T[K] ? true : false };
const EVENT: Nullability<AuditEvent> = {
  id: false,
  occurredAtUtc: false,
  actorUserId: false,
  actorDisplayName: false,
  module: false,
  action: false,
  entityType: false,
  entityId: false,
  changes: false,
};
const CHANGE: Nullability<AuditChange> = { field: false, masked: false, old: true, new: true, oldDisplay: true, newDisplay: true };

const sorted = (values: Iterable<string>) => [...values].sort();
const types = (schema: Schema) => (Array.isArray(schema.type) ? schema.type : schema.type ? [schema.type] : []);

function assertResponseSchema(name: string, table: Record<string, boolean>) {
  const schema = schemas[name];
  assert.ok(schema?.properties, `schéma ${name} absent`);
  assert.deepEqual(sorted(Object.keys(schema.properties)), sorted(Object.keys(table)), `${name} : propriétés`);
  assert.deepEqual(sorted(schema.required ?? []), sorted(Object.keys(table)), `${name} : required`);
  for (const [key, nullable] of Object.entries(table)) {
    assert.equal(types(schema.properties[key]).includes("null"), nullable, `${name}.${key} : nullabilité`);
  }
}

const event = {
  id: "0199e000-0000-7000-8000-000000000001",
  occurredAtUtc: "2026-07-16T08:00:00+00:00",
  actorUserId: "0199a000-0000-7000-8000-000000000001",
  actorDisplayName: "Agent",
  module: "appointments",
  action: "appointment.moved",
  entityType: "appointment",
  entityId: "0199c000-0000-7000-8000-000000000001",
  changes: [
    { field: "startsAtUtc", masked: false, old: "2026-07-16T08:00:00Z", new: "2026-07-16T09:00:00Z", oldDisplay: "16/07/2026 10:00", newDisplay: "16/07/2026 11:00" },
    { field: "notes", masked: true, old: null, new: null, oldDisplay: null, newDisplay: null },
  ],
};

describe("contrat OpenAPI historique des actions (03-D2) ↔ types TypeScript", () => {
  test("réponses typées, route unique en lecture, paramètres en liste blanche", () => {
    assertResponseSchema("AuditEventResponse", EVENT);
    assertResponseSchema("AuditChangeResponse", CHANGE);
    assertResponseSchema("AuditEventPageResponse", { items: false, page: false, pageSize: false, totalCount: false });
    assert.deepEqual(Object.keys(contract.paths), ["/api/audit/clients/{clientId}"]);
    const route = contract.paths["/api/audit/clients/{clientId}"];
    assert.deepEqual(Object.keys(route), ["get"]);
    assert.deepEqual(sorted(Object.keys(route.get.responses)), ["200", "400", "401", "403"]);
    const parameters = Object.fromEntries((route.get.parameters ?? []).map((parameter) => [parameter.name, parameter]));
    assert.deepEqual(sorted(Object.keys(parameters)), ["clientId", "from", "module", "page", "pageSize", "to"]);
    assert.deepEqual(parameters.module.schema.enum, ["clients", "appointments"]);
  });

  test("parseur strict : exemple conforme accepté ; champ masqué porteur d'une valeur, champ manquant refusés", () => {
    const page = { items: [event], page: 1, pageSize: 20, totalCount: 1 };
    assert.notEqual(parseAuditPage(page), null);
    const leaking = { ...event, changes: [{ field: "phone", masked: true, old: "06", new: null, oldDisplay: null, newDisplay: null }] };
    assert.equal(parseAuditPage({ ...page, items: [leaking] }), null);
    for (const key of Object.keys(EVENT)) {
      const incomplete = Object.fromEntries(Object.entries(event).filter(([name]) => name !== key));
      assert.equal(parseAuditPage({ ...page, items: [incomplete] }), null, `${key} manquant accepté`);
    }
    assert.equal(auditQueryString({ module: "clients", fromUtc: "2026-07-09T07:00:00.000Z", page: 2, pageSize: 20 }), "module=clients&from=2026-07-09T07%3A00%3A00.000Z&page=2&pageSize=20");
  });
});
