import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { apiError } from "../api/client.ts";
import type { TechnicalStatus } from "../api/health.ts";
import { compactIndicatorText, databaseIndicator, serverIndicator, sessionIndicator } from "./statusBarView.ts";

const live = {
  status: "Healthy",
  scope: "process" as const,
  processId: 1,
  startedAtUtc: "2026-10-16T08:00:00Z",
  uptimeSeconds: 1,
  checkedAtUtc: "2026-10-16T08:00:01Z",
};
const ready = { status: "Healthy" as const, scope: "database" as const, checkedAtUtc: "2026-10-16T08:00:01Z", durationMs: 2 };

describe("barre d'état : valeurs réelles uniquement", () => {
  test("avant toute réponse : vérification, jamais « disponible »", () => {
    assert.equal(serverIndicator(null, true).tone, "pending");
    assert.equal(databaseIndicator(null).tone, "pending");
  });

  test("API injoignable : serveur en erreur, base non vérifiée", () => {
    const status: TechnicalStatus = { state: "api-unreachable", error: apiError("network") };
    assert.deepEqual(serverIndicator(status, false), { text: "Serveur MMV injoignable", tone: "error" });
    assert.deepEqual(databaseIndicator(status), { text: "Base de données — non vérifiée", tone: "pending" });
  });

  test("base indisponible", () => {
    const status: TechnicalStatus = { state: "database-unavailable", live, ready: null, error: apiError("timeout") };
    assert.equal(serverIndicator(status, false).tone, "ok");
    assert.deepEqual(databaseIndicator(status), { text: "Base de données indisponible", tone: "warning" });
  });

  test("prêt + rechargement : état conservé, rechargement signalé", () => {
    const status: TechnicalStatus = { state: "ready", live, ready };
    assert.equal(serverIndicator(status, true).text, "Serveur MMV joignable (vérification…)");
    assert.equal(databaseIndicator(status).text, "Base de données disponible");
  });

  test("O-03-34 : libellés compacts des fenêtres étroites, même information", () => {
    assert.equal(compactIndicatorText("Base de données disponible"), "Base disponible");
    assert.equal(compactIndicatorText("Base de données — non vérifiée"), "Base — non vérifiée");
    assert.equal(compactIndicatorText("Serveur MMV joignable (vérification…)"), "Serveur joignable…");
    assert.equal(compactIndicatorText("Serveur MMV injoignable"), "Serveur injoignable");
    assert.equal(compactIndicatorText("Session active — fin au plus tard à 18:42"), "Session jusqu'à 18:42");
  });

  test("session : heure locale de fin au plus tard", () => {
    const text = sessionIndicator("2026-10-16T16:42:00Z", "fr-FR").text;
    assert.match(text, /^Session active — fin au plus tard à \d{2}:\d{2}$/);
    assert.equal(sessionIndicator("pas une date").text, "Session active");
  });
});
