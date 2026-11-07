// Assistant d'import CSV (CSV-IMPORT-AI-01, Gate C2) — passerelle vers les routes ADDITIVES `/api/imports/assistant…`.
// Le poste n'interprète rien : étapes, statuts, compteurs, anomalies, actions licites et exemples de montants viennent du
// serveur. Les corps d'erreur sont conservés en entier (code et détails bornés) pour afficher le refus exact du serveur.
// Écritures idempotentes : `requestId` par écriture, rejouée UNE fois avec le même identifiant si l'issue est incertaine.
import { requestJson } from "../../api/client.ts";
import { newRequestId } from "../../api/catalog.ts";
import { useAuthorized } from "../catalogData.ts";

export type StepKey = "deposit" | "analysis" | "review" | "check" | "import";
export type StepState = "todo" | "current" | "done" | "attention" | "blocked" | "stale" | "locked";

export interface AssistantStatus {
  enabled: boolean;
  mode: "always" | "unresolved_only" | "off";
  configuredModel: string;
  ollamaReachable: boolean;
  ollamaVersion: string | null;
  modelInstalled: boolean;
  installedDigest: string | null;
  wizardEnabled: boolean;
}

export interface InspectView {
  byteLength: number;
  sha256: string;
  encoding: string;
  delimiter: string;
  lineCount: number;
  columnCount: number;
  preview: string[][];
  suggestedTarget: "products" | "lenses" | null;
  evidence: string[];
  anomalies: string[];
}

export interface AnalysisEvent {
  seq: number;
  atUtc: string;
  code: string;
  data: Record<string, unknown> | null;
}

export interface AnalysisState {
  attempt: number;
  status: string;
  basedOnRevision: number | null;
  queuePosition: number | null;
  elapsedMs: number;
  model: { configured: string; active: string | null; digest: string | null; ollamaVersion: string | null };
  events: AnalysisEvent[];
  progress: { label: string; done: number; total: number }[];
  indeterminate: string | null;
  coverage: { sampled?: number; lines?: number; complete?: boolean } | null;
  families: { family: string; status: string; rows: number }[];
  structureAnomalies: string[];
  lateResultDiscarded: boolean;
  error: string | null;
  rulesOnly: boolean;
  rulesOnlyReason: string | null;
  batchEvents: AnalysisEvent[];
}

export interface IssueSummary {
  key: string;
  code: string;
  severity: string;
  lines: number;
  column: number | null;
  interpretation: string | null;
  explanation: string;
  suggestion: string | null;
  suggestionOrigin: string | null;
}

export interface ColumnView {
  column: number;
  header: string;
  field: string;
  status: string;
  origin: string;
  suggestion: string | null;
  aiField: string | null;
  userRevision: number | null;
  notWritten: string | null;
}

export interface DecisionView {
  revision: number;
  type: string;
  target: Record<string, unknown>;
  value: Record<string, unknown>;
  author: string;
  createdAtUtc: string;
}

export interface CheckSummary {
  target: string;
  basedOnRevision: number;
  checkedAtUtc: string;
  elapsedMs: number;
  productsToCreate: number;
  variantsToCreate: number;
  toUpdate: number;
  unchanged: number;
  skipped: number;
  duplicates: number;
  ambiguous: number;
  blocking: number;
  nothingToImport: boolean;
  containsCosts: boolean;
  containsManualPrices: boolean;
  /** Devise du magasin ; null si introuvable (aucun montant importable, jamais d'EUR par défaut). */
  currency: string | null;
  /** Verres : offres complètes dont une capacité change (fiche à revalider). */
  offersToRevalidate?: number;
  stale?: boolean;
  changes: { line: number; sku: string; field: string; before?: string | null; after?: string | null; money?: boolean }[];
  mappings: { column: number; header: string; field: string; origin: string; userRevision: number | null; notWritten: string | null }[];
  provenance: { revision: number; type: string; target: Record<string, unknown>; author: string; createdAtUtc: string; revoked: boolean }[];
  remaining: { key: string; code: string | null; lines: number[]; explanation: string }[];
  references: { kind: string; name: string; manufacturer: string | null; lines: number; decided: boolean }[];
  analysis: { attempt: number; status: string; model: string | null } | null;
  rulesOnly: boolean;
}

export interface AssistantView {
  batch: {
    id: string;
    status: string;
    version: number;
    revision: number;
    checkedRevision: number | null;
    target: "products" | "lenses";
    supplierId: string | null;
    fileName: string;
    uploadedAtUtc: string;
    rulesOnly: boolean;
    rulesOnlyReason: string | null;
    currency: string | null;
    original: { sha256: string; byteLength: number; encoding: string; delimiter: string };
    lines: number;
    columns: number;
  };
  steps: Record<StepKey, { state: StepState; sublabel: string | null }>;
  analysis: AnalysisState;
  review: {
    counters: Record<string, number>;
    confirmations: number;
    blockers: Record<string, number>;
    groups: IssueSummary[];
    issues: number;
    columns: ColumnView[];
    decisions: DecisionView[];
  };
  check: CheckSummary | null;
  permissions: string[];
}

export interface CellView {
  column: number;
  field: string;
  original: string;
  corrected: string | null;
  value: string | null;
  status: string;
  reason: string | null;
  sourceLine: number;
  money: boolean;
}

export interface RowView {
  line: number;
  kind: string;
  status: string;
  family: string | null;
  familyStatus: string;
  product: string | null;
  variant: string | null;
  reference: string | null;
  cells: CellView[];
  reasons: string[];
  issueKeys: string[];
  continuationOf: number | null;
  flags: string[];
}

export interface RowsView {
  total: number;
  counters: Record<string, number>;
  rows: RowView[];
}

export interface IssueAction {
  type: string;
  label: string;
  requires: string[];
  target: Record<string, unknown>;
  constraints: Record<string, unknown> | null;
}

export interface IssueDetail {
  key: string;
  code: string;
  severity: string;
  lines: number[];
  column: number | null;
  original: string[];
  interpretation: string | null;
  explanation: string;
  suggestion: string | null;
  suggestionOrigin: string | null;
  actions: IssueAction[];
  notOffered: { type: string; reason: string }[];
  diagnosis: { cause: string; retained: boolean; evidence: string }[] | null;
  examples: Record<string, unknown> | null;
  examplesDigest: string | null;
  grouped: boolean;
}

export interface DecisionInput {
  type: string;
  target: Record<string, unknown>;
  value: Record<string, unknown>;
  examplesDigest?: string | null;
}

export interface Revalidation {
  revision: number;
  counters: Record<string, number>;
  resolved: string[];
  raised: string[];
  checkStale: boolean;
}

export interface Applied {
  productsCreated: number;
  variantsCreated: number;
  updated: number;
  prices: number;
  offersCreated: number;
  offersUpdated: number;
  referencesCreated: number;
  appliedAtUtc: string;
  elapsedMs: number;
}

export interface ImportsSummary {
  total: number;
  lastUploadedAtUtc: string | null;
  resumable: number;
}

export interface HistoryBatch {
  id: string;
  kind: string;
  fileName: string;
  rowCount: number;
  status: string;
  uploadedAtUtc: string;
  uploadedBy: string;
  pipeline: string | null;
  analysis: { status: string; model: string | null; attempt: number } | null;
}

/** Réponse : succès (statut 2xx et corps) ou refus du serveur (statut, code, détails) ou échec réseau. */
export type Answer<T> =
  | { ok: true; status: number; data: T }
  | { ok: false; status: number; code: string; details: Record<string, unknown> | null };

export interface AssistantApi {
  status(): Promise<Answer<AssistantStatus>>;
  inspect(fileName: string, contentBase64: string): Promise<Answer<InspectView>>;
  create(body: { fileName: string; contentBase64: string; supplierId: string | null; target: string }): Promise<Answer<{ batchId: string }>>;
  view(batchId: string): Promise<Answer<AssistantView>>;
  analysis(batchId: string, after: number): Promise<Answer<AnalysisState>>;
  retry(batchId: string, version: number): Promise<Answer<AnalysisState>>;
  cancelAnalysis(batchId: string): Promise<Answer<AnalysisState>>;
  rows(batchId: string, filter: string, page: number, pageSize: number): Promise<Answer<RowsView>>;
  issue(batchId: string, key: string): Promise<Answer<IssueDetail>>;
  decide(batchId: string, baseRevision: number, decisions: DecisionInput[]): Promise<Answer<Revalidation>>;
  check(batchId: string, version: number, revision: number): Promise<Answer<CheckSummary>>;
  apply(batchId: string, version: number, revision: number): Promise<Answer<Applied>>;
  abandon(batchId: string, version: number): Promise<Answer<unknown>>;
  summary(): Promise<Answer<ImportsSummary>>;
  history(): Promise<Answer<HistoryBatch[]>>;
}

const ERROR_STATUSES = [400, 403, 404, 409, 422, 503];
const AMBIGUOUS = new Set(["network", "timeout", "http", "invalid-response"]);

/** Base64 des octets d'origine (le serveur décode ; le poste ne réécrit jamais le fichier). */
export async function fileToBase64(file: Blob): Promise<string> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(binary);
}

export function useAssistantApi(): AssistantApi {
  const authorized = useAuthorized();
  async function call<T>(method: "GET" | "POST", path: string, body?: Record<string, unknown>, timeoutMs = 20_000): Promise<Answer<T>> {
    const send = () =>
      authorized((baseUrl, token) =>
        requestJson<{ status: number; body: unknown }>(baseUrl, path, {
          method, body, token, timeoutMs, acceptedStatuses: [200, 201, 202, ...ERROR_STATUSES], parse: (payload, status) => ({ status, body: payload }),
        }),
      );
    let result = await send();
    if (!result.ok && method !== "GET" && AMBIGUOUS.has(result.error.kind)) result = await send();
    if (!result.ok) return { ok: false, status: result.error.status ?? 0, code: result.error.code ?? result.error.kind, details: null };
    const { status, body: payload } = result.data;
    if (status >= 400) {
      const error = (payload ?? {}) as { code?: string; details?: Record<string, unknown> | null; errors?: Record<string, string[]> };
      return { ok: false, status, code: error.code ?? `http_${status}`, details: error.details ?? (error.errors ? { errors: error.errors } : null) };
    }

    return { ok: true, status, data: payload as T };
  }

  const write = <T>(method: "POST", path: string, body: Record<string, unknown> = {}) => call<T>(method, path, { requestId: newRequestId(), ...body });
  return {
    status: () => call("GET", "/api/imports/assistant/status"),
    inspect: (fileName, contentBase64) => call("POST", "/api/imports/assistant/inspect", { fileName, contentBase64 }),
    create: (body) => write("POST", "/api/imports/assistant", { ...body, analysis: "auto" }),
    view: (batchId) => call("GET", `/api/imports/${batchId}/assistant`),
    analysis: (batchId, after) => call("GET", `/api/imports/${batchId}/assistant/analysis${after > 0 ? `?after=${after}` : ""}`),
    retry: (batchId, version) => write("POST", `/api/imports/${batchId}/assistant/analysis`, { version }),
    // GATE D : POST (la politique CORS du poste n'admet jamais DELETE ; un DELETE était refusé dans Tauri avant le serveur).
    cancelAnalysis: (batchId) => call("POST", `/api/imports/${batchId}/assistant/analysis/cancel`),
    rows: (batchId, filter, page, pageSize) => call("GET", `/api/imports/${batchId}/assistant/rows?filter=${encodeURIComponent(filter)}&page=${page}&pageSize=${pageSize}`),
    issue: (batchId, key) => call("GET", `/api/imports/${batchId}/assistant/issues/${encodeURIComponent(key)}`),
    decide: (batchId, baseRevision, decisions) => write("POST", `/api/imports/${batchId}/assistant/decisions`, { baseRevision, decisions }),
    check: (batchId, version, revision) => write("POST", `/api/imports/${batchId}/assistant/check`, { version, revision }),
    apply: (batchId, version, revision) => write("POST", `/api/imports/${batchId}/assistant/apply`, { version, revision, confirmed: true }),
    abandon: (batchId, version) => write("POST", `/api/imports/${batchId}/cancel`, { version }),
    summary: () => call("GET", "/api/imports/summary"),
    history: () => call("GET", "/api/imports"),
  };
}
