// PROTOTYPE 04-A — contrat PROVISOIRE du dossier optique (docs/phase/04/API_OPTICAL_PROPOSED.md et amendements
// E-01 / E-02, tous PROPOSED). Aucun appel HTTP : MMV.Api n'expose aucune route /api/optical (04-B non commencé).
// Ces types ne servent qu'au banc de démonstration isolé (src/bench/) et aux tests ; ils seront remplacés par le
// contrat OpenAPI généré par le serveur en 04-B. Décisions acquises : D-04-01 (deux types distincts), D-04-15 (UX
// Lot 3) et D-04-16 (simplification : aucun « état de l'œil », pièces sources reportées au backlog permanent).
// Valeurs optiques : chaînes conservées EXACTEMENT telles que saisies (aucune normalisation, aucun arrondi, aucune
// conversion) ; chaque champ peut rester vide, chaîne vide = non renseigné, jamais confondu avec la valeur « 0 ».
import type { ApiResult } from "../api/client.ts";

/** D-04-01 (APPROVED) : ordonnance médicale reçue ≠ mesure réalisée au magasin. Aucune conversion. */
export type OpticalKind = "prescription" | "measurement";
export const OPTICAL_KINDS: readonly OpticalKind[] = ["prescription", "measurement"];

export type EyeSide = "od" | "og";
export const EYE_SIDES: readonly EyeSide[] = ["od", "og"];

/** Colonnes du Lot 3 (Sphère, Cylindre, Axe, Addition, Prisme, Base) + acuité (D-04-06, PROTO). */
export const EYE_FIELDS = ["sphere", "cylinder", "axis", "addition", "prism", "base", "acuity"] as const;
export type EyeField = (typeof EYE_FIELDS)[number];
/** Valeurs d'un œil (D-04-16 : aucun état d'œil séparé ; un œil sans valeur est simplement non renseigné). */
export type EyeValues = Record<EyeField, string>;

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
  od: EyeValues;
  og: EyeValues;
  notes: string;
}

export interface PrescriptionContent extends ContentBase {
  kind: "prescription";
  provenance: PrescriptionProvenance;
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
  od: EyeValues;
  og: EyeValues;
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
 * `revision_conflict`, `client_archived`, `invalidated`), 422. Aucune opération sur des pièces sources (D-04-16 :
 * gestion documentaire reportée au backlog permanent).
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
}

export const EMPTY_EYE_VALUES: EyeValues = { sphere: "", cylinder: "", axis: "", addition: "", prism: "", base: "", acuity: "" };

export const emptyEye = (): EyeValues => ({ ...EMPTY_EYE_VALUES });

export function emptyContent(kind: OpticalKind, measuredByUserId = ""): RecordContent {
  return kind === "prescription"
    ? {
        kind,
        provenance: { prescriberName: "", prescriberQualification: "unspecified", prescriberIdentifier: "", prescribedOn: "", receivedOn: "" },
        od: emptyEye(),
        og: emptyEye(),
        notes: "",
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
