// Outils d'écran du prototype optique : contexte de retour validé, résumé OD/OG restitué tel que saisi, statut dérivé.
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { displayStatus, EMPTY_EYE_VALUES } from "./opticalContract.ts";
import { opticalAccess, OPTICAL_PERMISSIONS } from "./opticalAccess.ts";
import { describeOpticalFailure, eyeSummary, readClientTab, readOpticalLaunch } from "./opticalUi.ts";

describe("prototype optique — outils d'écran", () => {
  test("contexte de retour : seulement une fiche client ou la liste, client présélectionné validé", () => {
    assert.deepEqual(readOpticalLaunch({ returnTo: "/clients/c1", returnLabel: "X", client: { id: "c1", label: "X · CLI" } }), {
      returnTo: "/clients/c1",
      returnLabel: "X",
      client: { id: "c1", label: "X · CLI" },
    });
    assert.equal(readOpticalLaunch({ returnTo: "https://ailleurs.test", returnLabel: "X" }), null);
    assert.equal(readOpticalLaunch({ returnTo: "/clients/c1", returnLabel: "X", client: { id: "" } }), null);
    assert.equal(readOpticalLaunch("texte"), null);
    assert.equal(readClientTab({ clientTab: "optical" }), "optical");
    assert.equal(readClientTab(null), undefined);
  });

  test("résumé OD/OG : valeurs restituées EXACTEMENT, unité ° seulement affichée, absence écrite en toutes lettres", () => {
    assert.equal(
      eyeSummary("od", { state: "filled", values: { ...EMPTY_EYE_VALUES, sphere: "+1,25", cylinder: "-0.5", axis: "0" } }, "—"),
      "OD : Sph +1,25 · Cyl -0.5 · Axe 0°",
    );
    assert.equal(eyeSummary("og", { state: "not_prescribed", values: { ...EMPTY_EYE_VALUES } }, "Non prescrit sur le document"), "OG : Non prescrit sur le document");
  });

  test("statut dérivé (E-02) : invalidé > rectification en cours > confirmé > brouillon", () => {
    const revision = {} as never;
    assert.equal(displayStatus({ lifecycle: "active", lastConfirmed: null, openDraft: revision }), "draft");
    assert.equal(displayStatus({ lifecycle: "active", lastConfirmed: revision, openDraft: null }), "confirmed");
    assert.equal(displayStatus({ lifecycle: "active", lastConfirmed: revision, openDraft: revision }), "rectification");
    assert.equal(displayStatus({ lifecycle: "invalidated", lastConfirmed: revision, openDraft: revision }), "invalidated");
  });

  test("droits simulés : clients.read exigé ; écriture par type ; system.admin n'implique rien", () => {
    assert.equal(opticalAccess([OPTICAL_PERMISSIONS.read]).canRead, false);
    assert.equal(opticalAccess(["system.admin"]).canRead, false);
    const access = opticalAccess(["clients.read", OPTICAL_PERMISSIONS.read, OPTICAL_PERMISSIONS.measurementsWrite]);
    assert.equal(access.canWriteMeasurements, true);
    assert.equal(access.canWritePrescriptions, false);
  });

  test("messages d'échec bornés : conflit → rechargement proposé, jamais le texte du serveur", () => {
    const view = describeOpticalFailure({ kind: "conflict", message: "Texte serveur", status: 409, code: "version_conflict" });
    assert.equal(view.reload, true);
    assert.doesNotMatch(view.text, /Texte serveur/);
  });
});
