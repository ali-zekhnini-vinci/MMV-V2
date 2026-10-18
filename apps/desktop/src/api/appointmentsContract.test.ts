// Cohérence des types et parseurs TypeScript Rendez-vous avec le contrat OpenAPI RÉELLEMENT généré par MMV.Api
// (03-B2). L'instantané src/api/contract/appointments.openapi.json est comparé au document du serveur par le test
// .NET tests/MMV.Api.Tests/Appointments/AppointmentsOpenApiContractTests.cs : une dérive côté serveur casse ce
// test-là, une dérive côté interface casse celui-ci (tables de nullabilité vérifiées à la compilation).
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, test } from "vitest";
import {
  type AppointmentDetail,
  type AppointmentInput,
  type AppointmentOptions,
  type AppointmentReason,
  type AppointmentResponsible,
  type AppointmentSummary,
  type ClientAppointmentCounts,
  type ClientAppointmentItem,
  type TimeZoneOffset,
  parseAppointmentDetail,
  parseAppointmentList,
  parseAppointmentOptions,
  parseAppointmentSummary,
  parseClientAppointmentPage,
} from "./appointments.ts";

interface Schema {
  type?: string | string[];
  format?: string;
  enum?: unknown[];
  default?: unknown;
  required?: string[];
  properties?: Record<string, Schema>;
  items?: Schema;
  $ref?: string;
}

interface Operation {
  parameters?: { name: string; in: string; required?: boolean; schema: Schema }[];
  requestBody?: { content: Record<string, { schema: Schema }> };
  responses: Record<string, unknown>;
}

const contract = JSON.parse(readFileSync(new URL("./contract/appointments.openapi.json", import.meta.url), "utf8")) as {
  paths: Record<string, Record<string, Operation>>;
  components: { schemas: Record<string, Schema> };
};
const schemas = contract.components.schemas;

/** Table vérifiée à la COMPILATION : chaque clé du type, `true` exactement si la valeur admet `null`. */
type Nullability<T> = { [K in keyof T]-?: null extends T[K] ? true : false };

const SUMMARY: Nullability<AppointmentSummary> = {
  id: false,
  clientId: false,
  clientReference: false,
  clientFirstName: false,
  clientLastName: false,
  clientArchived: false,
  responsibleUserId: true,
  responsibleDisplayName: true,
  startsAtUtc: false,
  endsAtUtc: false,
  reasonCode: false,
  status: false,
  version: false,
};
const DETAIL: Nullability<AppointmentDetail> = {
  ...SUMMARY,
  notes: true,
  cancelledAtUtc: true,
  cancelledByDisplayName: true,
  cancellationReason: true,
  createdAtUtc: false,
  updatedAtUtc: false,
  updatedByDisplayName: false,
};
const OFFSET: Nullability<TimeZoneOffset> = { fromUtc: false, toUtc: false, offsetMinutes: false };
const OPTIONS: Nullability<AppointmentOptions> = {
  timeZone: false,
  durationsMinutes: false,
  startStepMinutes: false,
  reasons: false,
  responsibles: false,
  receptionCapacity: true,
  displayStartTime: false,
  displayEndTime: false,
  hiddenWeekdays: false,
};
const REASON: Nullability<AppointmentReason> = { code: false, label: false, responsibleRequired: false };
const RESPONSIBLE: Nullability<AppointmentResponsible> = { id: false, displayName: false };
const INPUT: Nullability<AppointmentInput> = {
  clientId: false,
  responsibleUserId: true,
  startsAtUtc: false,
  startsAtLocal: false,
  durationMinutes: false,
  reasonCode: false,
  notes: true,
};

const sorted = (values: Iterable<string>) => [...values].sort();
const types = (schema: Schema) => (Array.isArray(schema.type) ? schema.type : schema.type ? [schema.type] : []);
const resolve = (schema: Schema): Schema => (schema.$ref ? schemas[schema.$ref.replace("#/components/schemas/", "")] : schema);
function props(name: string): Record<string, Schema> {
  const properties = schemas[name]?.properties;
  assert.ok(properties, `schéma ${name} sans propriétés`);
  return properties;
}

function assertResponseSchema(name: string, table: Record<string, boolean>) {
  const schema = schemas[name];
  assert.ok(schema?.properties, `schéma ${name} absent`);
  assert.deepEqual(sorted(Object.keys(schema.properties)), sorted(Object.keys(table)), `${name} : propriétés`);
  assert.deepEqual(sorted(schema.required ?? []), sorted(Object.keys(table)), `${name} : required`);
  for (const [key, nullable] of Object.entries(table)) {
    assert.equal(types(resolve(schema.properties[key])).includes("null"), nullable, `${name}.${key} : nullabilité`);
  }
}

/** Exemple conforme au schéma (valeurs neutres) ; `overrides` fixe les valeurs que le parseur contrôle sémantiquement. */
function sample(schema: Schema, overrides: Record<string, unknown> = {}): unknown {
  const resolved = resolve(schema);
  if (resolved.enum) return resolved.enum[0];
  if (resolved.properties) {
    return Object.fromEntries(Object.entries(resolved.properties).map(([key, value]) => [key, key in overrides ? overrides[key] : sample(value)]));
  }
  const kinds = types(resolved).filter((kind) => kind !== "null");
  if (kinds.includes("array")) return [sample(resolved.items ?? {})];
  if (kinds.includes("integer")) return 1;
  if (kinds.includes("boolean")) return true;
  if (resolved.format === "uuid") return "0199a000-0000-7000-8000-000000000001";
  if (resolved.format === "date-time") return "2026-10-17T09:00:00+00:00";
  return "x";
}

const INTERVAL = { startsAtUtc: "2026-10-17T09:00:00+00:00", endsAtUtc: "2026-10-17T09:30:00+00:00" };
const STORE = { timeZone: "Europe/Brussels", displayStartTime: "09:00", displayEndTime: "19:00" };

describe("contrat OpenAPI Rendez-vous ↔ types TypeScript", () => {
  test("réponses : mêmes propriétés et même nullabilité que les types de l'interface", () => {
    assertResponseSchema("AppointmentSummaryResponse", SUMMARY);
    assertResponseSchema("AppointmentDetailResponse", DETAIL);
    assertResponseSchema("AppointmentOptionsResponse", OPTIONS);
    assertResponseSchema("AppointmentReasonResponse", REASON);
    assertResponseSchema("AppointmentResponsibleResponse", RESPONSIBLE);
    assertResponseSchema("AppointmentListResponse", { items: false, timeZoneOffsets: false });
    assertResponseSchema("TimeZoneOffsetResponse", OFFSET);
    assert.ok(types(props("TimeZoneOffsetResponse").offsetMinutes).includes("integer"), "offsetMinutes : entier strict");
    assert.deepEqual(schemas.AppointmentStatusValue.enum, ["planned", "cancelled"]);
    for (const [owner, key] of [
      ["AppointmentSummaryResponse", "version"],
      ["AppointmentOptionsResponse", "startStepMinutes"],
      ["AppointmentOptionsResponse", "receptionCapacity"],
    ] as const) {
      assert.ok(types(props(owner)[key]).includes("integer"), `${owner}.${key} : entier strict`);
      assert.equal(types(props(owner)[key]).includes("string"), false, `${owner}.${key} : jamais une chaîne`);
    }
  });

  test("requêtes : AppointmentInput, + version (modification), { version, reason } (annulation), { version } (rétablissement)", () => {
    assert.deepEqual(sorted(Object.keys(props("AppointmentInputRequest"))), sorted(Object.keys(INPUT)));
    assert.deepEqual(sorted(Object.keys(props("AppointmentUpdateRequest"))), sorted([...Object.keys(INPUT), "version"]));
    assert.deepEqual(sorted(Object.keys(props("AppointmentCancelRequest"))), ["reason", "version"]);
    assert.deepEqual(Object.keys(props("AppointmentVersionRequest")), ["version"]);
    // Champs validés par le serveur (422 par champ) plutôt que par le schéma.
    assert.equal(schemas.AppointmentInputRequest.required, undefined);
  });

  test("routes consommées par l'interface : méthodes, statuts, corps ; 503 si magasin non configuré ; aucune suppression", () => {
    const route = (path: string, method: string) => {
      const operation = contract.paths[path]?.[method];
      assert.ok(operation, `${method.toUpperCase()} ${path} absent du contrat`);
      return operation;
    };
    assert.ok("200" in route("/api/appointments/options", "get").responses);
    assert.ok("503" in route("/api/appointments/options", "get").responses);
    assert.ok("200" in route("/api/appointments", "get").responses);
    assert.ok("201" in route("/api/appointments", "post").responses);
    assert.ok("409" in route("/api/appointments", "post").responses);
    assert.ok("409" in route("/api/appointments/{id}", "put").responses);
    assert.ok("409" in route("/api/appointments/{id}/cancel", "post").responses);
    assert.ok("409" in route("/api/appointments/{id}/restore", "post").responses);
    assert.equal(contract.paths["/api/appointments/{id}"].delete, undefined, "aucune suppression");
    const body = (path: string, method: string) => route(path, method).requestBody?.content["application/json"].schema.$ref;
    assert.equal(body("/api/appointments", "post"), "#/components/schemas/AppointmentInputRequest");
    assert.equal(body("/api/appointments/{id}", "put"), "#/components/schemas/AppointmentUpdateRequest");
    assert.equal(body("/api/appointments/{id}/cancel", "post"), "#/components/schemas/AppointmentCancelRequest");
    assert.equal(body("/api/appointments/{id}/restore", "post"), "#/components/schemas/AppointmentVersionRequest");
    const parameters = Object.fromEntries((route("/api/appointments", "get").parameters ?? []).map((parameter) => [parameter.name, parameter]));
    assert.deepEqual(sorted(Object.keys(parameters)), ["from", "status", "to"]);
    assert.deepEqual(parameters.status.schema.enum, ["planned", "all"]);
    assert.equal(parameters.from.required, true);
    assert.equal(parameters.to.required, true);
  });

  test("les parseurs stricts acceptent un exemple conforme au schéma et refusent un champ manquant", () => {
    const detail = sample(schemas.AppointmentDetailResponse, INTERVAL) as Record<string, unknown>;
    assert.notEqual(parseAppointmentDetail(detail), null);
    assert.notEqual(parseAppointmentSummary(sample(schemas.AppointmentSummaryResponse, INTERVAL)), null);
    const offsets = [
      { fromUtc: "2026-10-18T22:00:00+00:00", toUtc: "2026-10-25T01:00:00+00:00", offsetMinutes: 120 },
      { fromUtc: "2026-10-25T01:00:00+00:00", toUtc: "2026-10-25T23:00:00+00:00", offsetMinutes: 60 },
    ];
    assert.notEqual(parseAppointmentList({ items: [sample(schemas.AppointmentSummaryResponse, INTERVAL)], timeZoneOffsets: offsets }), null);
    // Décalages absents, vides, discontinus, inversés ou non entiers : réponse refusée (jamais d'heure devinée).
    assert.equal(parseAppointmentList({ items: [] }), null);
    assert.equal(parseAppointmentList({ items: [], timeZoneOffsets: [] }), null);
    assert.equal(parseAppointmentList({ items: [], timeZoneOffsets: [offsets[0], { ...offsets[1], fromUtc: "2026-10-25T02:00:00+00:00" }] }), null);
    assert.equal(parseAppointmentList({ items: [], timeZoneOffsets: [{ ...offsets[0], toUtc: offsets[0].fromUtc }] }), null);
    assert.equal(parseAppointmentList({ items: [], timeZoneOffsets: [{ ...offsets[0], offsetMinutes: 60.5 }] }), null);
    const options = sample(schemas.AppointmentOptionsResponse, STORE) as Record<string, unknown>;
    assert.notEqual(parseAppointmentOptions(options), null);
    for (const key of Object.keys(DETAIL)) {
      const incomplete = Object.fromEntries(Object.entries(detail).filter(([name]) => name !== key));
      assert.equal(parseAppointmentDetail(incomplete), null, `${key} manquant accepté`);
    }
    for (const key of Object.keys(OPTIONS)) {
      const incomplete = Object.fromEntries(Object.entries(options).filter(([name]) => name !== key));
      assert.equal(parseAppointmentOptions(incomplete), null, `${key} manquant accepté`);
    }
  });
});

// 03-D1 : historique des rendez-vous d'un client (GET /api/appointments/by-client/{clientId}).
const CLIENT_ITEM: Nullability<ClientAppointmentItem> = {
  id: false,
  startsAtUtc: false,
  endsAtUtc: false,
  startsAtLocal: false,
  endsAtLocal: false,
  reasonCode: false,
  status: false,
  responsibleUserId: true,
  responsibleDisplayName: true,
  cancelledAtUtc: true,
};
const COUNTS: Nullability<ClientAppointmentCounts> = { upcoming: false, past: false, cancelled: false };

describe("contrat OpenAPI historique des rendez-vous d'un client (03-D1)", () => {
  test("réponse typée, paramètres en liste blanche, lecture seule, 503 si magasin non configuré", () => {
    assertResponseSchema("ClientAppointmentItemResponse", CLIENT_ITEM);
    assertResponseSchema("ClientAppointmentCountsResponse", COUNTS);
    assertResponseSchema("ClientAppointmentPageResponse", { items: false, page: false, pageSize: false, totalCount: false, counts: false });
    const route = contract.paths["/api/appointments/by-client/{clientId}"];
    assert.deepEqual(Object.keys(route), ["get"]);
    assert.ok("503" in route.get.responses && "403" in route.get.responses);
    const parameters = Object.fromEntries((route.get.parameters ?? []).map((parameter) => [parameter.name, parameter]));
    assert.deepEqual(sorted(Object.keys(parameters)), ["clientId", "dir", "page", "pageSize", "scope"]);
    assert.deepEqual(parameters.scope.schema.enum, ["upcoming", "past", "cancelled", "all"]);
  });

  test("le parseur strict accepte un exemple conforme et refuse une heure murale invalide ou une annulation incohérente", () => {
    const item = { ...(sample(schemas.ClientAppointmentItemResponse, INTERVAL) as Record<string, unknown>), startsAtLocal: "2026-10-17T11:00", endsAtLocal: "2026-10-17T11:30", status: "planned", cancelledAtUtc: null };
    const page = { items: [item], page: 1, pageSize: 10, totalCount: 1, counts: { upcoming: 1, past: 0, cancelled: 0 } };
    assert.notEqual(parseClientAppointmentPage(page), null);
    assert.equal(parseClientAppointmentPage({ ...page, items: [{ ...item, startsAtLocal: "2026-10-17 11:00" }] }), null);
    assert.equal(parseClientAppointmentPage({ ...page, items: [{ ...item, status: "cancelled" }] }), null);
    assert.equal(parseClientAppointmentPage({ ...page, counts: { upcoming: 1, past: 0 } }), null);
  });
});
