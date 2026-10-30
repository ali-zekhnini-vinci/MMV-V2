// Passerelle HTTP de la Phase 05 (catalogue, tarifs, fournisseurs, stock, inventaires, import CSV) : routes réelles de
// MMV.Api. Le poste ne calcule AUCUN montant (aperçus serveur) et ne fait que formater les chaînes décimales reçues ;
// les coûts n'arrivent que si la session détient `costs.read` (le serveur ne les émet pas sinon). Écritures idempotentes :
// chaque écriture porte un `requestId` ; une réponse incertaine (réseau, délai) est rejouée UNE fois avec le même
// identifiant — le serveur reconnaît la réémission et renvoie l'état courant sans rien réécrire.
import { type ApiErrorKind, type ApiResult, requestJson } from "./client.ts";

export type Authorized = <T>(call: (baseUrl: URL, token: string) => Promise<ApiResult<T>>) => Promise<ApiResult<T>>;

const AMBIGUOUS: ReadonlySet<ApiErrorKind> = new Set(["network", "timeout", "http", "invalid-response"]);
const WRITE_TIMEOUT_MS = 20_000;

const anyObject = (body: unknown): unknown => (body !== null && typeof body === "object" ? body : null);

export function newRequestId(): string {
  return crypto.randomUUID();
}

export async function getCatalog<T>(authorized: Authorized, path: string): Promise<ApiResult<T>> {
  return authorized((baseUrl, token) => requestJson<T>(baseUrl, path, { token, parse: (body) => anyObject(body) as T | null }));
}

/**
 * Aperçu serveur (ex. POST /api/pricing/preview) : calcul sans écriture, donc SANS `requestId` — le serveur refuse tout
 * membre inconnu (400 invalid_body, O-05-14). 200 attendu ; rejouable sans risque.
 */
export async function previewCatalog<T>(authorized: Authorized, path: string, body: Record<string, unknown>): Promise<ApiResult<T>> {
  return authorized((baseUrl, token) =>
    requestJson<T>(baseUrl, path, { method: "POST", body, token, parse: (b) => anyObject(b) as T | null, acceptedStatuses: [200], timeoutMs: WRITE_TIMEOUT_MS }),
  );
}

/** Écriture : 200 ou 201 attendus ; rejouée une fois avec le même `requestId` si l'issue est incertaine. */
export async function writeCatalog<T>(authorized: Authorized, method: "POST" | "PUT", path: string, body: Record<string, unknown>): Promise<ApiResult<T>> {
  const payload = { requestId: newRequestId(), ...body };
  const send = () =>
    authorized((baseUrl, token) =>
      requestJson<T>(baseUrl, path, { method, body: payload, token, parse: (b) => anyObject(b) as T | null, acceptedStatuses: [200, 201], timeoutMs: WRITE_TIMEOUT_MS }),
    );
  const first = await send();
  return !first.ok && AMBIGUOUS.has(first.error.kind) ? send() : first;
}

export function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    search.set(key, String(value));
  }
  const text = search.toString();
  return text === "" ? "" : `?${text}`;
}

// ---------- contrat (extrait des réponses de MMV.Api) ----------

export type StockPolicy = "stocked" | "order_only" | "service";
export type AvailabilityCode = "archived" | "available" | "on_order" | "incomplete" | "in_stock" | "low_stock" | "out_of_stock";

export interface StoreLocation {
  id: string;
  code: string;
  name: string;
  isDefault: boolean;
  status: "active" | "archived";
}

export interface Store {
  id: string;
  code: string;
  name: string;
  countryCode: string;
  currency: "EUR" | "MAD";
  fiscalProfileStatus: string;
  locations: StoreLocation[];
}

export interface FamilyAttribute {
  key: string;
  label: string;
  kind: "text" | "integer" | "decimal";
  maxLength: number;
  min: number | null;
  max: number | null;
}

export interface Family {
  code: string;
  label: string;
  sortOrder: number;
  allowedPolicies: StockPolicy[];
  modelAttributes: FamilyAttribute[];
  variantAttributes: FamilyAttribute[];
}

export interface Price {
  storeId: string;
  currency: string;
  pricingMode: "coefficient" | "manual";
  priceStatus: "up_to_date" | "recalculation_pending" | "incomplete";
  salePriceHt: string | null;
  salePriceTtc?: string;
  ttcStatus: string;
  version: number;
  costsVisible: boolean;
  purchasePriceHt?: string;
  coefficient?: string;
  computedSalePriceHt?: string;
  effectiveCoefficient?: string;
  margin?: string;
}

export interface VariantRow {
  variantId: string;
  modelId: string;
  sku: string;
  label: string;
  modelName: string;
  brand: string | null;
  familyCode: string;
  stockPolicy: StockPolicy;
  barcode: string | null;
  manufacturerReference: string | null;
  status: "active" | "archived";
  isIncomplete: boolean;
  primarySupplier: { id: string; name: string; leadTimeDays: number | null } | null;
  price: Price;
  stock?: { onHand: number; reserved: number; available: number; lowStockThreshold: number | null };
  availability: AvailabilityCode;
  version: number;
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ModelVariant {
  row: VariantRow;
  attributes: Record<string, string>;
  lensIndexId: string | null;
  treatmentIds: string[];
  archiveReason: string | null;
}

export interface ModelDetail {
  id: string;
  familyCode: string;
  brand: string | null;
  name: string;
  description: string | null;
  lensRangeId: string | null;
  attributes: Record<string, string>;
  status: "active" | "archived";
  archiveReason: string | null;
  version: number;
  variants: ModelVariant[];
}

export interface SupplierLink {
  supplierId: string;
  code: string;
  name: string;
  supplierStatus: "active" | "archived";
  supplierReference: string | null;
  isPrimary: boolean;
  leadTimeDays: number | null;
  removed: boolean;
  version: number;
  supplierPriceHt?: string;
  supplierCurrency?: string;
}

export interface Balance {
  locationId: string;
  locationCode: string;
  locationName: string;
  isDefault: boolean;
  onHand: number;
  reserved: number;
  available: number;
  version: number;
}

export interface VariantDetail {
  row: VariantRow;
  model: { id: string; familyCode: string; brand: string | null; name: string; status: string; version: number };
  attributes: Record<string, string>;
  lensIndexId: string | null;
  treatmentIds: string[];
  archiveReason: string | null;
  taxCategoryCode: string | null;
  suppliers: SupplierLink[] | null;
  balances: Balance[] | null;
  costsVisible: boolean;
}

export interface LensReference {
  id: string;
  kind: "manufacturer" | "range" | "index" | "treatment";
  name: string;
  manufacturerId: string | null;
  indexValue: string | null;
  status: "active" | "archived";
  version: number;
}

export interface LensReferences {
  manufacturers: LensReference[];
  ranges: LensReference[];
  indices: LensReference[];
  treatments: LensReference[];
}

export interface PriceHistoryEntry {
  id: string;
  field: string;
  origin: string;
  currency: string;
  oldValue: string | null;
  newValue: string | null;
  oldMode: string | null;
  newMode: string | null;
  reason: string | null;
  changedAtUtc: string;
  changedBy: { id: string; displayName: string };
}

export interface SupplierRow {
  id: string;
  code: string;
  name: string;
  countryCode: string;
  city: string | null;
  contactName: string | null;
  status: "active" | "archived";
  variantCount: number;
  primaryCount: number;
  version: number;
}

export interface SupplierDetail {
  id: string;
  code: string;
  name: string;
  countryCode: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  addressLine: string | null;
  postalCode: string | null;
  city: string | null;
  legalIdentifiers: Record<string, string>;
  notes: string | null;
  status: "active" | "archived";
  archiveReason: string | null;
  version: number;
  variants: { variantId: string; modelId: string; sku: string; label: string; modelName: string; stockPolicy: StockPolicy; status: string; isPrimary: boolean; supplierReference: string | null }[] | null;
}

export interface BalanceRow {
  variantId: string;
  modelId: string;
  sku: string;
  label: string;
  modelName: string;
  brand: string | null;
  familyCode: string;
  locationId: string;
  locationCode: string;
  onHand: number;
  reserved: number;
  available: number;
  lowStockThreshold: number | null;
  availability: AvailabilityCode;
  version: number;
  thresholdVersion: number;
}

export interface Movement {
  id: string;
  seq: number;
  variantId: string;
  sku: string;
  label: string;
  locationId: string;
  locationCode: string;
  type: string;
  quantityDelta: number;
  quantityBefore: number;
  quantityAfter: number;
  reasonCode: string;
  comment: string | null;
  supplierName: string | null;
  sourceType: string | null;
  sourceNumber: string | null;
  performedAtUtc: string;
  performedBy: { id: string; displayName: string };
  unitCostHt?: string;
  currency?: string;
}

export interface ReceiptSummary {
  id: string;
  number: string;
  locationCode: string;
  supplierName: string;
  supplierDocumentReference: string | null;
  completesReceiptId: string | null;
  receivedAtUtc: string;
  receivedBy: { displayName: string };
  lineCount: number;
  receivedQuantity: number;
  outstandingQuantity: number | null;
}

export interface InventorySummary {
  id: string;
  number: string;
  storeId: string;
  locationIds: string[];
  scopeType: string;
  scopeFamilyCode: string | null;
  blind: boolean;
  status: "open" | "validated" | "abandoned";
  openedAtUtc: string;
  openedBy: { displayName: string };
  validatedAtUtc: string | null;
  abandonedAtUtc: string | null;
  abandonReason: string | null;
  lineCount: number;
  countedCount: number;
  differenceCount: number;
  version: number;
}

export interface InventoryLine {
  variantId: string;
  locationId: string;
  sku: string;
  label: string;
  modelName: string;
  locationCode: string;
  expectedAtOpen: number | null;
  expectedAtCount: number | null;
  countedQuantity: number | null;
  difference: number | null;
  currentOnHand: number | null;
  changedSinceCount: boolean;
  countedBy: string | null;
  countedAtUtc: string | null;
  reasonCode: string | null;
  comment: string | null;
  resolution: string | null;
  appliedDelta: number | null;
  version: number;
}

export interface ImportBatch {
  id: string;
  kind: "catalog" | "suppliers" | "prices";
  storeId: string | null;
  fileName: string;
  fileSha256: string;
  rowCount: number;
  status: "uploaded" | "checked" | "applied" | "cancelled";
  errorCount: number | null;
  containsCosts: boolean;
  containsManualPrices: boolean;
  uploadedAtUtc: string;
  uploadedBy: string;
  mapping: Record<string, string>;
  version: number;
}

export interface ImportPreview {
  batchId: string;
  headers: string[];
  sampleRows: string[][];
  suggestedMapping: Record<string, string>;
  rowCount: number;
  fields: { key: string; required: boolean }[];
}

export interface ImportSummary {
  batchId: string;
  status: string;
  total: number;
  create: number;
  update: number;
  unchanged: number;
  error: number;
}

export interface ImportReport {
  batch: ImportBatch;
  summary: ImportSummary;
  rows: { lineNo: number; outcome: string; errors: { field: string; code: string }[]; values: Record<string, string> | null }[];
  valuesVisible: boolean;
}
