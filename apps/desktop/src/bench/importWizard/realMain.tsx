// Page de CAPTURES du VRAI composant de l'assistant (CSV-IMPORT-AI-01, Gate C2) : `ImportWizardView` intégré, servi par
// `vite` en développement seulement (`vite build` ne construit que index.html). Les réponses du serveur sont SCRIPTÉES
// (données fictives, forme exacte des contrats de MMV.Api) : simulation navigateur pour la revue visuelle, pas une preuve
// Tauri native ni une preuve du serveur (éprouvé par tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests).
// Paramètres : state=<depot|analyse-cours|analyse-indispo|verif|controle|importe>, theme=light|dark.
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import ReactDOM from "react-dom/client";
import "../../styles/index.css";
import "../../App.css";
import { AuthContext, type AuthContextValue } from "../../auth/authContext.ts";
import type { AnalysisState, Answer, AssistantApi, AssistantView, IssueDetail } from "../../catalog/importWizard/assistantApi.ts";
import { ImportWizardView } from "../../catalog/importWizard/ImportWizard.tsx";

const params = new URLSearchParams(window.location.search);
const state = params.get("state") ?? "depot";
if (params.get("theme") === "dark") document.documentElement.dataset.theme = "dark";

const ok = <T,>(data: T): Answer<T> => ({ ok: true, status: 200, data });
const at = (s: number) => new Date(Date.UTC(2026, 10, 6, 13, 31, 52 + s)).toISOString();
const ev = (seq: number, s: number, code: string, data: Record<string, unknown>) => ({ seq, atUtc: at(s), code, data });
const BATCH_EVENTS = [
  ev(1, 0, "file_received", { bytes: 6650 }), ev(2, 0, "encoding_detected", { encoding: "windows-1252", delimiter: ";" }),
  ev(3, 0, "structure_read", { lines: 48, columns: 11, irregular: 1 }), ev(4, 0, "rules_validated", { rows: 47, confirmations: 7 }),
];
const RUNNING = [ev(5, 1, "ollama_queued", { attempt: 1, model: "qwen3.5:4b" }), ev(6, 1, "sample_prepared", { sampled: 40, total: 48, complete: true }), ev(7, 1, "ollama_running", { model: "qwen3.5:4b", ollamaVersion: "0.40.14" })];
const DONE = [...RUNNING, ev(8, 47, "ollama_received", { model: "qwen3.5:4b", elapsedMs: 46644, evalCount: 412 }), ev(9, 47, "validation_running", { rows: 48 }), ev(10, 47, "analysis_done", { status: "done", confirmations: 7 })];

function analysis(status: string, events = DONE): AnalysisState {
  const done = status === "done";
  return {
    attempt: 1, status, basedOnRevision: 1, queuePosition: null, elapsedMs: done ? 46644 : status === "running" ? 12400 : 3100,
    model: { configured: "qwen3.5:4b", active: done ? "qwen3.5:4b" : null, digest: done ? "d8b0f5e9760c" : null, ollamaVersion: status === "unavailable" ? null : "0.40.14" },
    events, progress: [{ label: "Lecture complète du fichier", done: 48, total: 48 }, { label: "Validation par les règles", done: 47, total: 47 }, ...(done ? [{ label: "Revalidation avec la proposition de l'IA", done: 47, total: 47 }] : [])],
    indeterminate: status === "running" ? "Le modèle analyse l'échantillon (durée non mesurable à l'avance)" : null, coverage: { sampled: 40, lines: 48, complete: true },
    families: [{ family: "sunglasses", status: "VALIDE", rows: 12 }, { family: "?", status: "AMBIGU", rows: 35 }],
    structureAnomalies: ["L8 : fewer_cells"], lateResultDiscarded: false, error: status === "unavailable" ? "ollama_unreachable" : null, rulesOnly: false, rulesOnlyReason: null, batchEvents: BATCH_EVENTS,
  };
}

const COLUMNS = ["Marque", "Modèle", "Coloris", "Calibre", "Pont", "Branche", "Réf. fournisseur", "EAN", "Prix net HT", "PVC TTC", "Remarque"];
const FIELDS = ["brand", "modelName", "color", "eyeSize", "bridgeSize", "templeLength", "supplierReference", "barcode", "priceUnqualified", "salePriceTtc", "notes"];

function view(kind: string): AssistantView {
  const analysisStatus = kind === "analyse-cours" ? "running" : kind === "analyse-indispo" ? "unavailable" : "done";
  const checked = kind === "controle" || kind === "importe";
  const base: AssistantView = {
    batch: {
      id: "0193a7c1-0000-7000-8000-000000000001", status: kind === "importe" ? "applied" : checked ? "checked" : "uploaded", version: 12, revision: checked ? 6 : 1, checkedRevision: checked ? 6 : null,
      target: "products", supplierId: null, fileName: "lumen_optique_catalogue_2026.csv", uploadedAtUtc: at(0), rulesOnly: false, rulesOnlyReason: null, currency: "EUR",
      original: { sha256: "4c1f8e0b2a9d3c7e5f60718293a4b5c6d7e8f90112233445566778899aabbccd", byteLength: 6650, encoding: "windows-1252", delimiter: ";" }, lines: 48, columns: 11,
    },
    steps: {
      deposit: { state: "done", sublabel: "lumen_optique_catalogue_2026.csv" },
      analysis: analysisStatus === "running" ? { state: "current", sublabel: "analyse en cours" } : analysisStatus === "unavailable" ? { state: "blocked", sublabel: "choix requis : relancer ou vérifier sans IA" } : { state: "done", sublabel: "analyse terminée" },
      review: analysisStatus !== "done" ? { state: "locked", sublabel: null } : checked ? { state: "done", sublabel: "rien à corriger" } : { state: "attention", sublabel: "7 point(s) à traiter" },
      check: kind === "importe" ? { state: "done", sublabel: "contrôle à jour" } : kind === "controle" ? { state: "done", sublabel: "contrôle à jour" } : analysisStatus === "done" ? { state: "todo", sublabel: null } : { state: "locked", sublabel: null },
      import: kind === "importe" ? { state: "done", sublabel: "importé" } : kind === "controle" ? { state: "todo", sublabel: "prêt à importer" } : { state: "locked", sublabel: null },
    },
    analysis: analysis(analysisStatus, analysisStatus === "running" ? RUNNING : analysisStatus === "unavailable" ? [ev(5, 1, "ollama_queued", { attempt: 1, model: "qwen3.5:4b" }), ev(6, 4, "ollama_unavailable", { error: "ollama_unreachable" })] : DONE),
    review: {
      counters: { all: 48, valid: 6, corrections: 5, ambiguous: 36, unrecognized: 1, duplicates: 3 }, confirmations: checked ? 0 : 7, blockers: {},
      groups: checked ? [] : [
        { key: "col:8", code: "money_column", severity: "AMBIGU", lines: 0, column: 8, interpretation: "priceUnqualified", explanation: "montant non qualifié", suggestion: null, suggestionOrigin: null },
        { key: "family:none", code: "family_unconfirmed", severity: "AMBIGU", lines: 35, column: null, interpretation: null, explanation: "famille sans preuve déterministe : à confirmer", suggestion: "optical_frames", suggestionOrigin: "ai-suggestion" },
        { key: "continuation", code: "continuation_rows", severity: "AMBIGU", lines: 1, column: null, interpretation: "identité reprise de la ligne précédente", explanation: "Ces lignes n'ont ni marque ni modèle : MMV propose de reprendre l'identité de l'article qui les précède (aucune autre valeur reprise).", suggestion: null, suggestionOrigin: null },
        { key: "default:stockPolicy", code: "default_required", severity: "AMBIGU", lines: 0, column: null, interpretation: null, explanation: "Le fichier n'indique pas la politique de stock des articles.", suggestion: null, suggestionOrigin: null },
      ],
      issues: 12,
      columns: COLUMNS.map((header, column) => ({ column, header, field: FIELDS[column], status: column === 8 ? "AMBIGU" : "VALIDE", origin: column === 8 ? "rule" : "rule+ai", suggestion: null, aiField: FIELDS[column], userRevision: null, notWritten: column === 9 ? "MMV enregistre des prix HT : aucune conversion TTC → HT" : null })),
      decisions: checked ? [2, 3, 4, 5, 6].map((revision, i) => ({ revision, type: ["column_field", "family_class", "continuation", "file_default", "cell_value"][i], target: {}, value: {}, author: "u", createdAtUtc: at(120 + i * 30) })) : [],
    },
    check: checked ? {
      target: "products", basedOnRevision: 6, checkedAtUtc: at(320), elapsedMs: 184, productsToCreate: 19, variantsToCreate: 41, toUpdate: 3, unchanged: 2, skipped: 1, duplicates: 0, ambiguous: 0,
      blocking: 0, nothingToImport: false, containsCosts: true, containsManualPrices: false, currency: "EUR", stale: false,
      changes: [{ line: 12, sku: "LUM-2041-NR-52", field: "barcode", before: "3700000000017", after: "3700000000024" }, { line: 13, sku: "LUM-2041-EC-52", field: "variantLabel", before: "Ecaille", after: "Écaille" }, { line: 14, sku: "LUM-2050-TI-54", field: "purchasePriceHt", before: "•••", after: "•••", money: true }],
      mappings: COLUMNS.map((header, column) => ({ column, header, field: column === 8 ? "purchasePriceHt" : FIELDS[column], origin: column === 8 ? "user" : "rule+ai", userRevision: column === 8 ? 2 : null, notWritten: column === 9 ? "MMV enregistre des prix HT : aucune conversion TTC → HT" : null })),
      provenance: [2, 3, 4, 5, 6].map((revision, i) => ({ revision, type: ["column_field", "family_class", "continuation", "file_default", "cell_value"][i], target: {}, author: "u", createdAtUtc: at(120 + i * 30), revoked: false })),
      remaining: [], references: [], analysis: { attempt: 1, status: "done", model: "qwen3.5:4b" }, rulesOnly: false,
    } : null,
    permissions: ["catalog.import", "costs.read", "costs.write", "pricing.manual"],
  };
  return base;
}

const MONEY: IssueDetail = {
  key: "col:8", code: "money_column", severity: "AMBIGU", lines: [], column: 8, original: ["Prix net HT", "42,50", "44,00", "39,90", "61,00"], interpretation: "priceUnqualified",
  explanation: "montant non qualifié ; confirmation commerciale requise", suggestion: null, suggestionOrigin: null,
  actions: [
    { type: "column_field", label: "Prix d'achat HT pour les 45 lignes lisibles", requires: ["costs.write"], target: { column: 8 }, constraints: { field: "purchasePriceHt", examplesDigest: "e".repeat(64) } },
    { type: "column_field", label: "Prix de vente HT pour les 45 lignes lisibles", requires: ["pricing.manual"], target: { column: 8 }, constraints: { field: "salePriceHt", examplesDigest: "e".repeat(64) } },
    { type: "column_field", label: "Ne pas importer cette colonne", requires: [], target: { column: 8 }, constraints: { field: "ignore" } },
  ],
  notOffered: [{ type: "column_field:salePriceTtc", reason: "MMV enregistre des prix HT ; aucune conversion TTC → HT n'est faite" }, { type: "confirm_all", reason: "les montants se confirment colonne par colonne, après lecture des exemples" }],
  diagnosis: null,
  examples: {
    readable: 45, unreadable: 1, empty: 1, min: "18.90", median: "44.00", max: "129.00", currency: "EUR",
    examples: [{ role: "first", line: 2, original: "42,50", value: "42.50" }, { role: "min", line: 31, original: "18,90", value: "18.90" }, { role: "median", line: 17, original: "44,00", value: "44.00" }, { role: "max", line: 40, original: "129,00", value: "129.00" }, { role: "unreadable", line: 22, original: "sur devis", value: null }],
  },
  examplesDigest: "e".repeat(64), grouped: true,
};

const api: AssistantApi = {
  status: async () => ok({ enabled: true, mode: "always", configuredModel: "qwen3.5:4b", ollamaReachable: state !== "analyse-indispo", ollamaVersion: "0.40.14", modelInstalled: state !== "analyse-indispo", installedDigest: "d8b0f5e9760c", wizardEnabled: true }),
  inspect: async () => ({ ok: false, status: 400, code: "too_large", details: null }),
  create: async () => ({ ok: false, status: 400, code: "too_large", details: null }),
  view: async () => ok(view(state)),
  analysis: async (_batch, after) => { const a = view(state).analysis; return ok({ ...a, events: a.events.filter((e) => e.seq > after) }); },
  retry: async () => ok(view(state).analysis),
  cancelAnalysis: async () => ok(view(state).analysis),
  rows: async () => ok({
    total: 48, counters: view(state).review.counters,
    rows: [
      { line: 2, kind: "data", status: "AMBIGU", family: "optical_frames", familyStatus: "AMBIGU", product: "LU-2041", variant: "Noir", reference: "LUM-2041-NR", cells: [], reasons: ["famille sans preuve déterministe : à confirmer"], issueKeys: ["family:none"], continuationOf: null, flags: [] },
      { line: 8, kind: "data", status: "AMBIGU", family: "optical_frames", familyStatus: "AMBIGU", product: "LU-2050", variant: "Titane", reference: "LUM-2050-TI", cells: [], reasons: ["reprise de L7 (à confirmer)"], issueKeys: ["continuation"], continuationOf: 7, flags: ["continuation"] },
      { line: 10, kind: "data", status: "INVALIDE", family: "sunglasses", familyStatus: "VALIDE", product: "SOL-88 solaire", variant: "Écaille", reference: "LUM-88-EC", cells: [], reasons: ["c6 supplierReference : référence en double dans le fichier"], issueKeys: ["dup:supplierReference:lum-88-ec"], continuationOf: null, flags: ["duplicate"] },
      { line: 11, kind: "data", status: "INVALIDE", family: "sunglasses", familyStatus: "VALIDE", product: "SOL-88 solaire", variant: "Écaille", reference: "LUM-88-EC", cells: [], reasons: ["c6 supplierReference : référence en double dans le fichier"], issueKeys: ["dup:supplierReference:lum-88-ec"], continuationOf: null, flags: ["duplicate"] },
      { line: 22, kind: "data", status: "INVALIDE", family: "optical_frames", familyStatus: "AMBIGU", product: "LU-3001", variant: "Bleu", reference: "LUM-3001-BL", cells: [], reasons: ["c8 : montant illisible"], issueKeys: ["cell:22:8"], continuationOf: null, flags: [] },
      { line: 31, kind: "data", status: "VALIDE", family: "sunglasses", familyStatus: "VALIDE", product: "SOL-12 polarisée", variant: "Gris", reference: "LUM-12-GR", cells: [], reasons: [], issueKeys: [], continuationOf: null, flags: [] },
    ],
  }),
  issue: async (_batch, key) => ok(key === "col:8" ? MONEY : {
    ...MONEY, key, code: "reference_duplicate", severity: "INVALIDE", lines: [10, 11], column: 6, original: ["LUM-88-EC", "LUM-88-EC"], interpretation: "lum-88-ec", examples: null, examplesDigest: null,
    explanation: "La valeur « LUM-88-EC » apparaît sur 2 lignes : une référence fournisseur identifie un seul article.",
    diagnosis: [
      { cause: "in_file_conflict", retained: false, evidence: "les déclinaisons et EAN des lignes concernées sont identiques" },
      { cause: "column_mapping", retained: false, evidence: "colonne diversifiée : 46 valeurs distinctes sur 47 lignes" },
      { cause: "continuation", retained: false, evidence: "aucune ligne concernée n'est une continuation" },
      { cause: "true_duplicate", retained: true, evidence: "toutes les valeurs reconnues des lignes sont identiques" },
    ],
    actions: [
      { type: "cell_value", label: "Modifier la valeur de la ligne 10", requires: [], target: { line: 10, column: 6 }, constraints: { value: null, unique: true } },
      { type: "row_skip", label: "Ignorer la ligne 10", requires: [], target: { line: 10 }, constraints: null },
      { type: "cell_value", label: "Modifier la valeur de la ligne 11", requires: [], target: { line: 11, column: 6 }, constraints: { value: null, unique: true } },
      { type: "row_skip", label: "Ignorer la ligne 11", requires: [], target: { line: 11 }, constraints: null },
    ],
    notOffered: [{ type: "merge", reason: "aucune fusion automatique de lignes ou de produits" }, { type: "link_existing", reason: "association automatique à un article existant refusée (décision du propriétaire du 06/11/2026)" }],
  }),
  decide: async () => ok({ revision: 2, counters: {}, resolved: [], raised: [], checkStale: false }),
  check: async () => ok(view("controle").check as never),
  apply: async () => ok({ productsCreated: 19, variantsCreated: 41, updated: 3, prices: 44, offersCreated: 0, offersUpdated: 0, referencesCreated: 0, appliedAtUtc: at(400), elapsedMs: 912 }),
  abandon: async () => ok({}),
  summary: async () => ok({ total: 12, lastUploadedAtUtc: at(0), resumable: 2 }),
  history: async () => ok([
    { id: "0193a7c1-0000-7000-8000-000000000001", kind: "catalog", fileName: "lumen_optique_catalogue_2026.csv", rowCount: 48, status: state === "importe" ? "applied" : "uploaded", uploadedAtUtc: at(0), uploadedBy: "Agent MMV", pipeline: "assistant_v2", analysis: { status: state === "analyse-indispo" ? "unavailable" : "done", model: state === "analyse-indispo" ? null : "qwen3.5:4b", attempt: 1 } },
    { id: "0193a7c1-0000-7000-8000-000000000002", kind: "prices", fileName: "tarifs_magasin.csv", rowCount: 120, status: "applied", uploadedAtUtc: at(-86400), uploadedBy: "Agent MMV", pipeline: null, analysis: null },
  ]),
};

const auth = { authorized: async () => ({ ok: false, error: { kind: "network", message: "hors ligne" } }) } as unknown as AuthContextValue;
ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthContext.Provider value={auth}>
        <div style={{ padding: 24, maxWidth: 1440, margin: "0 auto" }}>
          <ImportWizardView api={api} />
        </div>
      </AuthContext.Provider>
    </QueryClientProvider>
  </React.StrictMode>,
);
