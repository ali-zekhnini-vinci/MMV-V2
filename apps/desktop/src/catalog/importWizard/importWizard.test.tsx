// @vitest-environment jsdom
// Assistant d'import CSV (CSV-IMPORT-AI-01, Gate C2) contre une API INJECTÉE (données fictives) : le poste affiche ce
// que le serveur décide (étapes, statuts, actions licites, exemples et leur empreinte) et n'envoie que des décisions.
// Le serveur réel (routes, revalidation, transaction) est éprouvé par tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests.
import assert from "node:assert/strict";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test } from "vitest";
import { AuthContext, type AuthContextValue } from "../../auth/authContext.ts";
import type { AnalysisState, Answer, AssistantApi, AssistantView, DecisionInput, IssueDetail } from "./assistantApi.ts";
import { ImportWizardView } from "./ImportWizard.tsx";

afterEach(() => cleanup());

const ok = <T,>(data: T): Answer<T> => ({ ok: true, status: 200, data });

const analysis = (status: string, active: string | null, events: AnalysisState["events"] = []): AnalysisState => ({
  attempt: 1, status, basedOnRevision: 1, queuePosition: null, elapsedMs: 2400, model: { configured: "qwen3.5:4b", active, digest: active ? "d8b0f5e9760c" : null, ollamaVersion: "0.40.14" },
  events, progress: [{ label: "Lecture complète du fichier", done: 3, total: 3 }], indeterminate: status === "running" ? "Le modèle analyse l'échantillon (durée non mesurable à l'avance)" : null,
  coverage: { sampled: 3, lines: 3, complete: true }, families: [{ family: "?", status: "AMBIGU", rows: 2 }], structureAnomalies: [], lateResultDiscarded: false, error: null,
  rulesOnly: false, rulesOnlyReason: null, batchEvents: [{ seq: 1, atUtc: "2026-11-06T10:00:00Z", code: "file_received", data: { bytes: 120 } }],
});

function view(overrides: Partial<AssistantView> = {}): AssistantView {
  return {
    batch: {
      id: "b1", status: "uploaded", version: 7, revision: 1, checkedRevision: null, target: "products", supplierId: null, fileName: "catalogue.csv", uploadedAtUtc: "2026-11-06T10:00:00Z",
      rulesOnly: false, rulesOnlyReason: null, currency: "EUR", original: { sha256: "a".repeat(64), byteLength: 120, encoding: "utf-8", delimiter: ";" }, lines: 3, columns: 5,
    },
    steps: {
      deposit: { state: "done", sublabel: "catalogue.csv" }, analysis: { state: "done", sublabel: "analyse terminée" }, review: { state: "attention", sublabel: "3 point(s) à traiter" },
      check: { state: "todo", sublabel: null }, import: { state: "locked", sublabel: null },
    },
    analysis: analysis("done", "qwen3.5:4b", [{ seq: 2, atUtc: "2026-11-06T10:00:03Z", code: "ollama_received", data: { model: "qwen3.5:4b", elapsedMs: 2400, evalCount: 321 } }]),
    review: {
      counters: { all: 2, valid: 0, corrections: 1, ambiguous: 1, unrecognized: 0, duplicates: 2 }, confirmations: 3, blockers: {},
      groups: [{ key: "col:4", code: "money_column", severity: "AMBIGU", lines: 0, column: 4, interpretation: "priceUnqualified", explanation: "montant non qualifié", suggestion: null, suggestionOrigin: null }],
      issues: 3, columns: [0, 1, 2, 3, 4].map((c) => ({ column: c, header: ["Marque", "Modèle", "Coloris", "SKU", "Prix"][c], field: "brand", status: "VALIDE", origin: "rule", suggestion: null, aiField: null, userRevision: null, notWritten: null })),
      decisions: [],
    },
    check: null,
    permissions: ["catalog.import", "costs.read", "costs.write"],
    ...overrides,
  };
}

const MONEY: IssueDetail = {
  key: "col:4", code: "money_column", severity: "AMBIGU", lines: [], column: 4, original: ["Prix", "42,50", "44,00"], interpretation: "priceUnqualified", explanation: "montant non qualifié",
  suggestion: null, suggestionOrigin: null,
  actions: [
    { type: "column_field", label: "Prix d'achat HT pour les 2 lignes lisibles", requires: ["costs.write"], target: { column: 4 }, constraints: { field: "purchasePriceHt", examplesDigest: "f".repeat(64) } },
    { type: "column_field", label: "Prix de vente HT pour les 2 lignes lisibles", requires: ["pricing.manual"], target: { column: 4 }, constraints: { field: "salePriceHt", examplesDigest: "f".repeat(64) } },
    { type: "column_field", label: "Ne pas importer cette colonne", requires: [], target: { column: 4 }, constraints: { field: "ignore" } },
  ],
  notOffered: [{ type: "confirm_all", reason: "les montants se confirment colonne par colonne" }], diagnosis: null,
  examples: { readable: 2, unreadable: 0, empty: 0, min: "42.50", median: "44.00", max: "44.00", currency: "EUR", examples: [{ role: "first", line: 2, original: "42,50", value: "42.50" }] },
  examplesDigest: "f".repeat(64), grouped: true,
};

function fakeApi(initial: AssistantView, extra: Partial<AssistantApi> = {}) {
  const calls: { name: string; args: unknown[] }[] = [];
  let current = initial;
  const record = <T,>(name: string, result: () => Answer<T>) => async (...args: unknown[]) => { calls.push({ name, args }); return result(); };
  const api: AssistantApi = {
    status: record("status", () => ok({ enabled: true, mode: "always", configuredModel: "qwen3.5:4b", ollamaReachable: true, ollamaVersion: "0.40.14", modelInstalled: true, installedDigest: "d8b0", wizardEnabled: true })),
    inspect: record("inspect", () => ok({ byteLength: 120, sha256: "a".repeat(64), encoding: "utf-8", delimiter: ";", lineCount: 3, columnCount: 5, preview: [["Marque", "Prix"], ["Ray", ""]], suggestedTarget: "products", evidence: [], anomalies: [] })),
    create: record("create", () => ({ ok: true, status: 201, data: { batchId: "b1" } })),
    view: record("view", () => ok(current)),
    analysis: record("analysis", () => ok(current.analysis)),
    retry: record("retry", () => ok(current.analysis)),
    cancelAnalysis: record("cancelAnalysis", () => ok(current.analysis)),
    rows: record("rows", () => ok({ total: 1, counters: current.review.counters, rows: [{ line: 2, kind: "data", status: "AMBIGU", family: null, familyStatus: "AMBIGU", product: "RB1", variant: "Noir", reference: "S-1", cells: [], reasons: ["référence en double dans le fichier"], issueKeys: ["dup:sku:s-1"], continuationOf: null, flags: ["duplicate"] }] })),
    issue: record("issue", () => ok(MONEY)),
    decide: record("decide", () => ok({ revision: 2, counters: {}, resolved: ["col:4"], raised: [], checkStale: false })),
    check: record("check", () => ok(current.check ?? ({} as never))),
    apply: record("apply", () => ok({ productsCreated: 1, variantsCreated: 2, updated: 0, prices: 2, offersCreated: 0, offersUpdated: 0, referencesCreated: 0, appliedAtUtc: "2026-11-06T10:05:00Z", elapsedMs: 80 })),
    abandon: record("abandon", () => ok({})),
    summary: record("summary", () => ok({ total: 12, lastUploadedAtUtc: "2026-11-05T09:00:00Z", resumable: 2 })),
    history: record("history", () => ok([{ id: "b1", kind: "catalog", fileName: "catalogue.csv", rowCount: 3, status: "uploaded", uploadedAtUtc: "2026-11-06T10:00:00Z", uploadedBy: "Agent", pipeline: "assistant_v2", analysis: { status: "done", model: "qwen3.5:4b", attempt: 1 } }])),
    ...extra,
  };
  return { api, calls, setView: (next: AssistantView) => { current = next; } };
}

function renderWizard(api: AssistantApi) {
  const auth = { authorized: async () => ({ ok: false, error: { kind: "network", message: "hors ligne" } }) } as unknown as AuthContextValue;
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthContext.Provider value={auth}>
        <ImportWizardView api={api} />
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

async function resume(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: /Imports récents/ }));
  await user.click(await screen.findByRole("button", { name: "Reprendre" }));
}

describe("assistant d'import en cinq étapes", () => {
  test("historique replié par défaut : aucune liste chargée avant l'ouverture, résumé seul", async () => {
    const { api, calls } = fakeApi(view());
    const user = renderWizard(api);
    const toggle = await screen.findByRole("button", { name: /Imports récents/ });
    assert.equal(toggle.getAttribute("aria-expanded"), "false");
    await waitFor(() => assert.match(toggle.textContent ?? "", /12 import\(s\).*2 à reprendre/));
    assert.equal(calls.filter((c) => c.name === "history").length, 0);
    await user.click(toggle);
    assert.equal(toggle.getAttribute("aria-expanded"), "true");
    await screen.findByRole("region", { name: "Imports récents" });
    assert.equal(calls.filter((c) => c.name === "history").length, 1);
    screen.getByText("Terminée · qwen3.5:4b");
  });

  test("dépôt : inspection sans écriture puis création ; les étapes viennent du serveur", async () => {
    const running = view({ steps: { ...view().steps, analysis: { state: "current", sublabel: "analyse en cours" }, review: { state: "locked", sublabel: null } }, analysis: analysis("running", null) });
    const { api, calls } = fakeApi(running);
    const user = renderWizard(api);
    const input = screen.getByLabelText("Fichier CSV à importer");
    await user.upload(input, new File(["Marque;Prix\nRay;\n"], "catalogue.csv", { type: "text/csv" }));
    await screen.findByText(/120 o · 3 lignes · 5 colonnes/);
    assert.equal(calls.filter((c) => c.name === "create").length, 0);
    await user.click(screen.getByRole("button", { name: /Analyser mon fichier/ }));
    await waitFor(() => assert.equal(calls.filter((c) => c.name === "create").length, 1));
    const steps = screen.getByRole("list", { name: "Étapes de l'import" });
    assert.equal(within(steps).getByRole("button", { name: /Vérifier et corriger/ }).hasAttribute("disabled"), true);
    await screen.findByText("Fichier reçu par le serveur");
    // Modèle actif : jamais affiché avant la réponse d'Ollama ; aucun pourcentage pour l'inférence.
    screen.getByText("en attente de la réponse");
    const bar = screen.getByRole("progressbar", { name: /Le modèle analyse/ });
    assert.equal(bar.getAttribute("aria-valuenow"), null);
    screen.getByRole("button", { name: /Annuler l'analyse IA/ });
  });

  test("montant : aucune présélection, exemples du serveur, empreinte transmise avec la décision", async () => {
    const { api, calls } = fakeApi(view());
    const user = renderWizard(api);
    await resume(user);
    await user.click(await screen.findByRole("button", { name: "Ouvrir" }));
    const save = await screen.findByRole("button", { name: /Enregistrer et revalider|Confirmer pour/ });
    assert.equal(save.hasAttribute("disabled"), true);
    for (const radio of screen.getAllByRole("radio")) assert.equal((radio as HTMLInputElement).checked, false);
    screen.getByText(/2 lisibles · 0 illisibles · 0 vides/);
    screen.getByText(/Non proposé : les montants se confirment colonne par colonne/);
    await user.click(screen.getByRole("radio", { name: /Prix d'achat HT pour les 2 lignes/ }));
    await user.click(screen.getByRole("button", { name: /Confirmer pour les 2 lignes/ }));
    await waitFor(() => assert.equal(calls.filter((c) => c.name === "decide").length, 1));
    const [batchId, baseRevision, decisions] = (calls.find((c) => c.name === "decide")?.args ?? []) as [string, number, DecisionInput[]];
    assert.deepEqual([batchId, baseRevision], ["b1", 1]);
    assert.deepEqual(decisions, [{ type: "column_field", target: { column: 4 }, value: { field: "purchasePriceHt" }, examplesDigest: "f".repeat(64) }]);
  });

  test("clavier seul (jsdom, pas un test natif) : choix d'une action et confirmation sans souris", async () => {
    const { api, calls } = fakeApi(view());
    const user = renderWizard(api);
    await resume(user);
    await user.click(await screen.findByRole("button", { name: "Ouvrir" }));
    const radios = await screen.findAllByRole("radio");
    radios[0].focus();
    await user.keyboard(" ");
    assert.equal((radios[0] as HTMLInputElement).checked, true);
    const confirm = screen.getByRole("button", { name: /Confirmer pour les 2 lignes/ });
    while (document.activeElement !== confirm) await user.tab();
    await user.keyboard("{Enter}");
    await waitFor(() => assert.equal(calls.filter((c) => c.name === "decide").length, 1));
  });

  test("refus du serveur affiché tel quel (exemples relus exigés), droit manquant signalé", async () => {
    const { api } = fakeApi(view({ permissions: ["catalog.import", "costs.read"] }), {
      decide: async () => ({ ok: false, status: 422, code: "decision_invalid", details: { code: "examples_not_reviewed" } }),
    });
    const user = renderWizard(api);
    await resume(user);
    await user.click(await screen.findByRole("button", { name: "Ouvrir" }));
    await user.click(await screen.findByRole("radio", { name: /Prix d'achat HT/ }));
    await screen.findByText("Droit costs.write requis pour cette action");
    assert.equal(screen.getByRole("button", { name: /Confirmer pour/ }).hasAttribute("disabled"), true);
    await user.click(screen.getByRole("radio", { name: /Ne pas importer/ }));
    await user.click(screen.getByRole("button", { name: /Enregistrer et revalider/ }));
    await screen.findByText("Les exemples ont changé : relisez-les avant de confirmer.");
  });

  test("import : confirmation explicite dans une boîte de dialogue, nombres relus du serveur", async () => {
    const checked = view({
      batch: { ...view().batch, status: "checked", checkedRevision: 1 },
      steps: { ...view().steps, review: { state: "done", sublabel: null }, check: { state: "done", sublabel: "contrôle à jour" }, import: { state: "todo", sublabel: "prêt à importer" } },
      check: {
        target: "products", basedOnRevision: 1, checkedAtUtc: "2026-11-06T10:04:00Z", elapsedMs: 40, productsToCreate: 1, variantsToCreate: 2, toUpdate: 0, unchanged: 0, skipped: 0, duplicates: 0,
        ambiguous: 0, blocking: 0, nothingToImport: false, containsCosts: true, containsManualPrices: false, currency: "EUR", stale: false, changes: [], mappings: [], provenance: [], remaining: [],
        references: [], analysis: { attempt: 1, status: "done", model: "qwen3.5:4b" }, rulesOnly: false,
      },
    });
    const { api, calls } = fakeApi(checked);
    const user = renderWizard(api);
    await resume(user);
    await screen.findByText(/Prix d'achat HT écrits \(EUR, aucune conversion\)/);
    await user.click(screen.getByRole("button", { name: /Importer…/ }));
    const dialog = await screen.findByRole("alertdialog");
    assert.equal(calls.filter((c) => c.name === "apply").length, 0);
    await user.click(within(dialog).getByRole("button", { name: "Importer maintenant" }));
    await waitFor(() => assert.equal(calls.filter((c) => c.name === "apply").length, 1));
    await screen.findByText(/2 variante\(s\) créée\(s\), 0 modifiée\(s\), 2 tarif\(s\)/);
  });

  test("verres : fiches complètes à revalider signalées ; devise du magasin introuvable jamais affichée comme EUR", async () => {
    const checked = view({
      batch: { ...view().batch, status: "checked", checkedRevision: 1, target: "lenses", currency: null },
      steps: { ...view().steps, review: { state: "done", sublabel: null }, check: { state: "done", sublabel: "contrôle à jour" }, import: { state: "todo", sublabel: "prêt à importer" } },
      check: {
        target: "lenses", basedOnRevision: 1, checkedAtUtc: "2026-11-06T10:04:00Z", elapsedMs: 40, productsToCreate: 0, variantsToCreate: 0, toUpdate: 2, unchanged: 0, skipped: 0, duplicates: 0,
        ambiguous: 0, blocking: 0, nothingToImport: false, containsCosts: true, containsManualPrices: false, currency: null, offersToRevalidate: 1, stale: false,
        changes: [{ line: 2, sku: "Gamme · progressive", field: "rulesComplete", before: "fiche complète", after: "fiche à revalider" }], mappings: [], provenance: [], remaining: [],
        references: [], analysis: { attempt: 1, status: "done", model: "qwen3.5:4b" }, rulesOnly: false,
      },
    });
    const { api } = fakeApi(checked);
    const user = renderWizard(api);
    await resume(user);
    await screen.findByText(/Fiches complètes à revalider/);
    await screen.findByText(/Prix d'achat HT écrits \(devise introuvable, aucune conversion\)/);
    assert.equal(screen.queryByText(/\(EUR, aucune conversion\)/), null);
  });
});
