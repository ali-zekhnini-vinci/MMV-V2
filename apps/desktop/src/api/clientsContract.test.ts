// Cohérence des types et parseurs TypeScript avec le contrat OpenAPI RÉELLEMENT généré par MMV.Api (O-03-01).
// L'instantané src/api/contract/clients.openapi.json est comparé au document du serveur par le test .NET
// tests/MMV.Api.Tests/Clients/ClientsOpenApiContractTests.cs : une dérive côté serveur casse ce test-là, une
// dérive côté interface casse celui-ci (compilation des tables de nullabilité, puis comparaison au schéma).
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, test } from "vitest";
import {
  CLIENT_MAX_PAGE_SIZE,
  CLIENT_PAGE_SIZE,
  CLIENT_SEARCH_MAX_LENGTH,
  CLIENT_SEARCH_MIN_LENGTH,
  CLIENT_SORT_FIELDS,
  CLIENT_STATUS_FILTERS,
  type ClientDetail,
  type ClientInput,
  type ClientPage,
  type ClientSummary,
  parseClientDetail,
  parseClientPage,
  parseClientSummary,
} from "./clients.ts";

interface Schema {
  type?: string | string[];
  format?: string;
  enum?: unknown[];
  default?: unknown;
  minimum?: number;
  maximum?: number;
  minLength?: number;
  maxLength?: number;
  required?: string[];
  properties?: Record<string, Schema>;
  items?: Schema;
  $ref?: string;
}

interface Operation {
  parameters?: { name: string; in: string; schema: Schema }[];
  requestBody?: { content: Record<string, { schema: Schema }> };
  responses: Record<string, unknown>;
}

const contract = JSON.parse(readFileSync(new URL("./contract/clients.openapi.json", import.meta.url), "utf8")) as {
  paths: Record<string, Record<string, Operation>>;
  components: { schemas: Record<string, Schema> };
};
const schemas = contract.components.schemas;

/** Table vérifiée à la COMPILATION : chaque clé du type, `true` exactement si la valeur admet `null`. */
type Nullability<T> = { [K in keyof T]-?: null extends T[K] ? true : false };

const SUMMARY: Nullability<ClientSummary> = {
  id: false,
  reference: false,
  firstName: false,
  lastName: false,
  phone: true,
  email: true,
  city: true,
  status: false,
  createdAtUtc: false,
  version: false,
};
const DETAIL: Nullability<ClientDetail> = {
  ...SUMMARY,
  birthDate: true,
  addressLine: true,
  postalCode: true,
  notes: true,
  updatedAtUtc: false,
  archivedAtUtc: true,
};
const PAGE: Nullability<Omit<ClientPage, "items">> = { page: false, pageSize: false, totalCount: false };
const INPUT: Nullability<ClientInput> = {
  firstName: false,
  lastName: false,
  birthDate: true,
  phone: true,
  email: true,
  addressLine: true,
  city: true,
  postalCode: true,
  notes: true,
};

const sorted = (values: Iterable<string>) => [...values].sort();
const types = (schema: Schema) => (Array.isArray(schema.type) ? schema.type : schema.type ? [schema.type] : []);
/** Propriétés d'un schéma nommé (échec explicite si absentes). */
function props(name: string): Record<string, Schema> {
  const properties = schemas[name]?.properties;
  assert.ok(properties, `schéma ${name} sans propriétés`);
  return properties;
}
const resolve = (schema: Schema): Schema => (schema.$ref ? schemas[schema.$ref.replace("#/components/schemas/", "")] : schema);

function assertResponseSchema(name: string, table: Record<string, boolean>) {
  const schema = schemas[name];
  assert.ok(schema?.properties, `schéma ${name} absent`);
  assert.deepEqual(sorted(Object.keys(schema.properties)), sorted(Object.keys(table)), `${name} : propriétés`);
  // Réponses : toutes les propriétés sont toujours présentes (éventuellement null).
  assert.deepEqual(sorted(schema.required ?? []), sorted(Object.keys(table)), `${name} : required`);
  for (const [key, nullable] of Object.entries(table)) {
    assert.equal(types(resolve(schema.properties[key])).includes("null"), nullable, `${name}.${key} : nullabilité`);
  }
}

/** Exemple conforme au schéma (valeurs neutres, aucune donnée personnelle). */
function sample(schema: Schema): unknown {
  const resolved = resolve(schema);
  if (resolved.enum) return resolved.enum[0];
  if (resolved.properties) return Object.fromEntries(Object.entries(resolved.properties).map(([key, value]) => [key, sample(value)]));
  const kinds = types(resolved).filter((kind) => kind !== "null");
  if (kinds.includes("array")) return [sample(resolved.items ?? {})];
  if (kinds.includes("integer")) return 1;
  if (resolved.format === "uuid") return "0199a000-0000-7000-8000-000000000001";
  if (resolved.format === "date-time") return "2026-10-17T09:00:00+00:00";
  return "x";
}

describe("contrat OpenAPI Clients ↔ types TypeScript", () => {
  test("réponses : mêmes propriétés et même nullabilité que ClientSummary / ClientDetail / ClientPage", () => {
    assertResponseSchema("ClientSummaryResponse", SUMMARY);
    assertResponseSchema("ClientDetailResponse", DETAIL);
    assertResponseSchema("ClientPageResponse", { ...PAGE, items: false });
    assert.equal(resolve(props("ClientPageResponse").items.items ?? {}), schemas.ClientSummaryResponse);
    assert.deepEqual(schemas.ClientStatusValue.enum, ["active", "archived"]);
    for (const key of ["version", "page", "pageSize", "totalCount"]) {
      const owner = props(key === "version" ? "ClientSummaryResponse" : "ClientPageResponse");
      assert.deepEqual(types(owner[key]), ["integer"], `${key} : entier strict`);
    }
  });

  test("requêtes : ClientInput, + version pour la modification, { version } pour l'archivage", () => {
    assert.deepEqual(sorted(Object.keys(props("ClientInputRequest"))), sorted(Object.keys(INPUT)));
    assert.deepEqual(sorted(Object.keys(props("ClientUpdateRequest"))), sorted([...Object.keys(INPUT), "version"]));
    assert.deepEqual(Object.keys(props("ClientVersionRequest")), ["version"]);
    // Facultatifs côté schéma : prénom et nom sont contrôlés par le serveur (422 required par champ).
    assert.equal(schemas.ClientInputRequest.required, undefined);
  });

  test("routes consommées par l'interface : méthodes, statuts de succès, corps attendus", () => {
    const route = (path: string, method: string) => {
      const operation = contract.paths[path]?.[method];
      assert.ok(operation, `${method.toUpperCase()} ${path} absent du contrat`);
      return operation;
    };
    assert.ok("200" in route("/api/clients", "get").responses);
    assert.ok("201" in route("/api/clients", "post").responses);
    assert.ok("200" in route("/api/clients/{id}", "get").responses);
    assert.ok("409" in route("/api/clients/{id}", "put").responses);
    assert.ok("409" in route("/api/clients/{id}/archive", "post").responses);
    assert.ok("409" in route("/api/clients/{id}/reactivate", "post").responses);
    assert.equal(contract.paths["/api/clients/{id}"].delete, undefined, "aucune suppression");
    const body = (path: string, method: string) => route(path, method).requestBody?.content["application/json"].schema.$ref;
    assert.equal(body("/api/clients", "post"), "#/components/schemas/ClientInputRequest");
    assert.equal(body("/api/clients/{id}", "put"), "#/components/schemas/ClientUpdateRequest");
    assert.equal(body("/api/clients/{id}/archive", "post"), "#/components/schemas/ClientVersionRequest");
  });

  test("paramètres de liste : listes blanches et bornes identiques à clientListQuery", () => {
    const list = contract.paths["/api/clients"].get;
    const parameters = Object.fromEntries((list.parameters ?? []).map((parameter) => [parameter.name, parameter.schema]));
    assert.deepEqual(sorted(Object.keys(parameters)), sorted(["q", "status", "sort", "dir", "page", "pageSize"]));
    assert.deepEqual(parameters.status.enum, [...CLIENT_STATUS_FILTERS]);
    assert.deepEqual(parameters.sort.enum, [...CLIENT_SORT_FIELDS]);
    assert.deepEqual(parameters.dir.enum, ["asc", "desc"]);
    assert.equal(parameters.pageSize.maximum, CLIENT_MAX_PAGE_SIZE);
    assert.equal(parameters.pageSize.default, CLIENT_PAGE_SIZE);
    assert.equal(parameters.q.minLength, CLIENT_SEARCH_MIN_LENGTH);
    assert.equal(parameters.q.maxLength, CLIENT_SEARCH_MAX_LENGTH);
  });

  test("les parseurs stricts acceptent un exemple conforme au schéma et refusent un champ manquant", () => {
    const detail = sample(schemas.ClientDetailResponse) as Record<string, unknown>;
    assert.notEqual(parseClientDetail(detail), null);
    assert.notEqual(parseClientSummary(sample(schemas.ClientSummaryResponse)), null);
    assert.notEqual(parseClientPage(sample(schemas.ClientPageResponse)), null);
    for (const key of Object.keys(DETAIL)) {
      const incomplete = Object.fromEntries(Object.entries(detail).filter(([name]) => name !== key));
      assert.equal(parseClientDetail(incomplete), null, `${key} manquant accepté`);
    }
  });
});
