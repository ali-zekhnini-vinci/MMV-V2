// États de l'écran de socle : chaque état issu de checkTechnicalStatus a un libellé distinct et
// n'affiche jamais de contenu serveur brut. Le rendu natif est prouvé séparément (captures Tauri).
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { apiError } from "./api/client.ts";
import { describeLive, describeReady, type ScreenStatus, SUMMARY } from "./technicalStatusView.ts";

const live = {
  status: "Healthy",
  scope: "process" as const,
  processId: 4242,
  startedAtUtc: "2026-10-15T10:00:00Z",
  uptimeSeconds: 12.4,
  checkedAtUtc: "2026-10-15T10:00:12Z",
};
const ready = (status: "Healthy" | "Unhealthy") => ({
  status,
  scope: "database" as const,
  checkedAtUtc: "2026-10-15T10:00:12Z",
  durationMs: 7,
});

describe("écran socle : états", () => {
  test("cinq états, cinq résumés distincts", () => {
    const labels = Object.values(SUMMARY).map((entry) => entry.label);
    assert.equal(new Set(labels).size, 5);
  });

  test("chargement", () => {
    const status: ScreenStatus = { state: "loading" };
    assert.equal(SUMMARY[status.state].tone, "pending");
    assert.equal(describeLive(status), "Vérification…");
    assert.equal(describeReady(status), "Vérification…");
  });

  test("non configuré : raison affichée, base non vérifiée", () => {
    const status: ScreenStatus = { state: "not-configured", reason: "VITE_MMV_API_BASE_URL absente." };
    assert.equal(SUMMARY[status.state].tone, "error");
    assert.equal(describeLive(status), "VITE_MMV_API_BASE_URL absente.");
    assert.equal(describeReady(status), "Non vérifiée (serveur injoignable)");
  });

  test("API indisponible (réseau / TLS / CORS) : message borné", () => {
    const status: ScreenStatus = { state: "api-unreachable", error: apiError("network") };
    assert.equal(SUMMARY[status.state].tone, "error");
    assert.match(describeLive(status), /injoignable/);
    assert.equal(describeReady(status), "Non vérifiée (serveur injoignable)");
  });

  test("API en erreur HTTP : statut affiché, aucun corps serveur", () => {
    const status: ScreenStatus = { state: "api-unreachable", error: apiError("http", 500) };
    assert.equal(describeLive(status), "Réponse HTTP inattendue du serveur MMV. (HTTP 500)");
  });

  test("PostgreSQL indisponible (503 Unhealthy)", () => {
    const status: ScreenStatus = { state: "database-unavailable", live, ready: ready("Unhealthy"), error: null };
    assert.equal(SUMMARY[status.state].tone, "warning");
    assert.equal(describeLive(status), "Actif — PID 4242, depuis 12 s");
    assert.equal(describeReady(status), "Indisponible (vérifiée en 7 ms)");
  });

  test("PostgreSQL non déterminé (timeout de /health/ready)", () => {
    const status: ScreenStatus = { state: "database-unavailable", live, ready: null, error: apiError("timeout") };
    assert.equal(describeReady(status), "Non déterminée : Le serveur MMV n'a pas répondu à temps.");
  });

  test("API et PostgreSQL disponibles", () => {
    const status: ScreenStatus = { state: "ready", live, ready: ready("Healthy") };
    assert.equal(SUMMARY[status.state].tone, "ok");
    assert.equal(describeReady(status), "Disponible (connexion vérifiée en 7 ms)");
  });
});
