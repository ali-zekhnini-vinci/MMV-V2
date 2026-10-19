// PROTOTYPE 04-A — contrat PROVISOIRE du dossier optique (docs/phase/04/API_OPTICAL_PROPOSED.md et amendements
// E-01 / E-02, tous PROPOSED). Aucun appel HTTP : MMV.Api n'expose aucune route /api/optical (04-B non commencé).
// Ces types ne servent qu'au banc de démonstration isolé (src/bench/) et aux tests ; ils seront remplacés par le
// contrat OpenAPI généré par le serveur en 04-B. Seule D-04-01 (deux types distincts) est une décision acquise.
// Valeurs optiques : chaînes conservées EXACTEMENT telles que saisies (aucune normalisation, aucun arrondi, aucune
// conversion) ; chaîne vide = non renseigné, jamais confondu avec la valeur « 0 ».
import type { ApiResult } from "../api/client.ts";

/** D-04-01 (APPROVED) : ordonnance médicale reçue ≠ mesure réalisée au magasin. Aucune conversion. */
export type OpticalKind = "prescription" | "measurement";
export const OPTICAL_KINDS: readonly OpticalKind[] = ["prescription", "measurement"];

export type EyeSide = "od" | "og";
export const EYE_SIDES: readonly EyeSide[] = ["od", "og"];

/** D-04-02.c (PROPOSED) : état explicite par œil ; `not_transcribed` n'existe qu'au brouillon. */
export type EyeState = "filled" | "not_prescribed" | "not_transcribed";
export const EYE_STATES: readonly EyeState[] = ["filled", "not_prescribed", "not_transcribed"];

/** Colonnes du Lot 3 (Sphère, Cylindre, Axe, Addition, Prisme, Base) + acuité (D-04-06, PROTO). */
export const EYE_FIELDS = ["sphere", "cylinder", "axis", "addition", "prism", "base", "acuity"] as const;
export type EyeField = (typeof EYE_FIELDS)[number];
export type EyeValues = Record<EyeField, string>;

export interface EyeEntry {
  state: EyeState;
  values: EyeValues;
}

/** D-04-03.a (PROPOSED) : provenance d'une ordonnance ; la liste des qualités est à fournir (BLOQUÉ). */
export interface PrescriptionProvenance {
  prescriberName: string;
  prescriberQualification: "unspecified";
  prescriberIdentifier: string;
  /** Date portée sur le document (`AAAA-MM-JJ` ou `""`) ; aucune validité calculée. */
  prescribedOn: string;
  /** Date de réception au magasin, distincte. */
  receivedOn: string;
}

/** D-04-03.b (PROPOSED) : provenance d'une mesure magasin ; la liste des catégories est à fournir (BLOQUÉ). */
export interface MeasurementProvenance {
  measuredOn: string;
  /** Collaborateur ayant mesuré (compte fictif du banc), distinct de l'auteur de la saisie. */
  measuredByUserId: string;
  category: "unspecified";
}

interface ContentBase {
  od: EyeEntry;
  og: EyeEntry;
  notes: string;
}

export interface PrescriptionContent extends ContentBase {
  kind: "prescription";
  provenance: PrescriptionProvenance;
  /** E-01 / D-04-10 (PROPOSED) : pièces de l'enregistrement liées à CETTE révision. */
  linkedDocumentIds: string[];
}

export interface MeasurementContent extends ContentBase {
  kind: "measurement";
  provenance: MeasurementProvenance;
}

export type RecordContent = PrescriptionContent | MeasurementContent;

export interface OpticalActor {
  id: string;
  displayName: string;
}

/** E-02 / D-04-11 (PROPOSED) : statut d'une révision ; le numéro n'est attribué qu'à la confirmation. */
export type RevisionStatus = "draft" | "confirmed" | "discarded";

/** D-04-04.b (PROPOSED) : motifs de rectification. */
export type RectificationReason = "transcription_error" | "additional_information" | "other";
export const RECTIFICATION_REASONS: readonly RectificationReason[] = ["transcription_error", "additional_information", "other"];

/** D-04-04.c (PROPOSED) : motifs d'invalidation (aucune suppression physique). */
export type InvalidationReason = "entry_error" | "duplicate" | "unacceptable_document" | "other";
export const INVALIDATION_REASONS: readonly InvalidationReason[] = ["entry_error", "duplicate", "unacceptable_document", "other"];

export type DocumentInvalidationReason = "attached_by_mistake" | "illegible" | "other";
export const DOCUMENT_INVALIDATION_REASONS: readonly DocumentInvalidationReason[] = ["attached_by_mistake", "illegible", "other"];

export interface OpticalRevision {
  id: string;
  status: RevisionStatus;
  /** Attribué à la confirmation (1, 2, 3… sans trou) ; `null` pour un brouillon ou une révision abandonnée. */
  revisionNo: number | null;
  /** Révision confirmée de départ d'une rectification ; `null` pour une première saisie. */
  baseRevisionNo: number | null;
  reasonCode: RectificationReason | null;
  reasonComment: string;
  content: RecordContent;
  startedAtUtc: string;
  startedBy: OpticalActor;
  updatedAtUtc: string;
  updatedBy: OpticalActor;
  confirmedAtUtc: string | null;
  confirmedBy: OpticalActor | null;
  discardedAtUtc: string | null;
  discardedBy: OpticalActor | null;
}

export interface OpticalInvalidation {
  atUtc: string;
  by: OpticalActor;
  reasonCode: InvalidationReason;
  comment: string;
  /** Un brouillon ouvert a été abandonné dans la même opération (D-04-11.e). */
  discardedDraft: boolean;
}

/** Types acceptés (D-04-03.c / D-04-14.a, PROPOSED). */
export type DocumentMediaType = "application/pdf" | "image/jpeg" | "image/png";

export interface SourceDocument {
  id: string;
  mediaType: DocumentMediaType;
  sizeBytes: number;
  uploadedAtUtc: string;
  uploadedBy: OpticalActor;
  /** Nom d'origine : servi seulement avec `optical.documents.read` (D-04-05). */
  originalName: string;
  invalidation: { atUtc: string; by: OpticalActor; reasonCode: DocumentInvalidationReason } | null;
}

export interface OpticalClientRef {
  id: string;
  reference: string;
  displayName: string;
  status: "active" | "archived";
}

/** Événement de gestion (libellés seulement, aucune valeur optique — D-04-12, prototype). */
export interface OpticalEvent {
  id: string;
  atUtc: string;
  code: string;
  actor: OpticalActor;
  revisionNo?: number;
  reasonCode?: string;
}

/** Statut affiché, DÉRIVÉ du cycle et des révisions (E-02). */
export type DisplayStatus = "draft" | "confirmed" | "rectification" | "invalidated";

export interface OpticalRecord {
  kind: OpticalKind;
  id: string;
  reference: string;
  client: OpticalClientRef;
  lifecycle: "active" | "invalidated";
  invalidation: OpticalInvalidation | null;
  lastConfirmed: OpticalRevision | null;
  openDraft: OpticalRevision | null;
  /** Toutes les révisions (confirmées, brouillon, abandonnées), dans l'ordre chronologique. */
  revisions: OpticalRevision[];
  /** `null` sans `optical.documents.read` : seul le nombre est connu (`documentCount`). */
  documents: SourceDocument[] | null;
  documentCount: number;
  events: OpticalEvent[];
  createdAtUtc: string;
  createdBy: OpticalActor;
  /** Jeton de concurrence de l'enregistrement (équivalent `xmin`). */
  version: number;
}

export interface OpticalRecordSummary {
  kind: OpticalKind;
  id: string;
  reference: string;
  client: OpticalClientRef;
  status: DisplayStatus;
  revisionNo: number | null;
  /** Date de prescription (RX) ou de mesure (MES) de la révision affichée. */
  date: string;
  /** Prescripteur (RX) ou collaborateur ayant mesuré (MES), tel qu'enregistré. */
  source: string;
  od: EyeEntry;
  og: EyeEntry;
  recordedAtUtc: string;
}

export type OpticalStatusFilter = "all" | "draft" | "confirmed" | "rectification";
export type OpticalSort = "date:desc" | "date:asc" | "client:asc" | "client:desc" | "recorded:desc";

export interface OpticalListParams {
  search: string;
  kind: "all" | OpticalKind;
  status: OpticalStatusFilter;
  /** Invalidés exclus par défaut, inclusion explicite (état 4). */
  includeInvalidated: boolean;
  sort: OpticalSort;
  page: number;
  pageSize: number;
}

export interface OpticalPage {
  items: OpticalRecordSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export const OPTICAL_PAGE_SIZE = 8;

/** Pièce synthétique du banc (aucun fichier du poste n'est lu en 04-A). */
export interface SyntheticDocumentChoice {
  id: string;
  label: string;
  /** Type annoncé par le « fichier » ; le double vérifie la « signature » simulée. */
  declaredType: string;
  detectedType: DocumentMediaType | "image/svg+xml" | "text/html";
  sizeBytes: number;
  name: string;
  /** Empreinte simulée (détection des doublons, D-04-10.d). */
  digest: string;
}

export interface DocumentContent {
  mediaType: DocumentMediaType;
  /** Image synthétique générée localement ; `null` pour un PDF (aperçu PDF reporté en 04-C, O-04-01). */
  imageDataUrl: string | null;
}

export interface RectificationRequest {
  expectedRevisionNo: number;
  reasonCode: RectificationReason;
  comment: string;
  version: number;
}

export interface InvalidationRequest {
  reasonCode: InvalidationReason;
  comment: string;
  version: number;
}

/**
 * Accès aux données du dossier optique. En 04-A, seule une implémentation EN MÉMOIRE existe (banc isolé, tests) ;
 * le serveur réel (04-B) reprendra les mêmes refus : 403, 404, 409 (`version_conflict`, `draft_exists`,
 * `revision_conflict`, `client_archived`, `invalidated`, `duplicate_document`, `too_many_documents`), 413, 415, 422.
 */
export interface OpticalGateway {
  listRecords(params: OpticalListParams): Promise<ApiResult<OpticalPage>>;
  listClientRecords(clientId: string, includeInvalidated: boolean): Promise<ApiResult<OpticalRecordSummary[]>>;
  getRecord(kind: OpticalKind, id: string): Promise<ApiResult<OpticalRecord>>;
  listMeasurers(): Promise<ApiResult<OpticalActor[]>>;
  createRecord(clientId: string, content: RecordContent): Promise<ApiResult<OpticalRecord>>;
  saveDraft(kind: OpticalKind, id: string, content: RecordContent, version: number): Promise<ApiResult<OpticalRecord>>;
  confirmDraft(kind: OpticalKind, id: string, version: number): Promise<ApiResult<OpticalRecord>>;
  startRectification(kind: OpticalKind, id: string, request: RectificationRequest): Promise<ApiResult<OpticalRecord>>;
  discardRectification(kind: OpticalKind, id: string, version: number): Promise<ApiResult<OpticalRecord>>;
  invalidateRecord(kind: OpticalKind, id: string, request: InvalidationRequest): Promise<ApiResult<OpticalRecord>>;
  attachDocument(id: string, choice: SyntheticDocumentChoice): Promise<ApiResult<OpticalRecord>>;
  invalidateDocument(id: string, documentId: string, reasonCode: DocumentInvalidationReason): Promise<ApiResult<OpticalRecord>>;
  fetchDocumentContent(documentId: string): Promise<ApiResult<DocumentContent>>;
}

export const EMPTY_EYE_VALUES: EyeValues = { sphere: "", cylinder: "", axis: "", addition: "", prism: "", base: "", acuity: "" };

export const emptyEye = (): EyeEntry => ({ state: "not_transcribed", values: { ...EMPTY_EYE_VALUES } });

export function emptyContent(kind: OpticalKind, measuredByUserId = ""): RecordContent {
  return kind === "prescription"
    ? {
        kind,
        provenance: { prescriberName: "", prescriberQualification: "unspecified", prescriberIdentifier: "", prescribedOn: "", receivedOn: "" },
        od: emptyEye(),
        og: emptyEye(),
        notes: "",
        linkedDocumentIds: [],
      }
    : { kind, provenance: { measuredOn: "", measuredByUserId, category: "unspecified" }, od: emptyEye(), og: emptyEye(), notes: "" };
}

/** Statut affiché dérivé (E-02) : invalidé > rectification en cours > confirmé > brouillon. */
export function displayStatus(record: Pick<OpticalRecord, "lifecycle" | "lastConfirmed" | "openDraft">): DisplayStatus {
  if (record.lifecycle === "invalidated") return "invalidated";
  if (record.lastConfirmed === null) return "draft";
  return record.openDraft !== null ? "rectification" : "confirmed";
}

/** Révision affichée par défaut : la dernière confirmée, sinon le brouillon de première saisie. */
export function shownRevision(record: Pick<OpticalRecord, "lastConfirmed" | "openDraft" | "revisions">): OpticalRevision {
  return record.lastConfirmed ?? record.openDraft ?? record.revisions[record.revisions.length - 1];
}
