// BANC 04-A — double EN MÉMOIRE du futur service optique (aucun réseau, aucune base, aucune persistance). Il
// reproduit les refus du contrat PROPOSÉ (API_OPTICAL_PROPOSED.md + amendements E-01/E-02/E-05) pour que les écrans
// puissent les afficher ; ce n'est PAS une implémentation de référence et il ne tranche aucune décision.
// - Erreurs de champ : seulement des erreurs de FORME (nombre attendu, longueur). Aucune borne, aucun pas, aucune
//   dépendance entre champs (D-04-06 ouverte, avis opticien requis).
// - Critère de confirmation (`422 incomplete`) : RÈGLE DE DÉMONSTRATION tirée des recommandations PROPOSED
//   D-04-02.b (option B : aucun œil « non transcrit ») et D-04-03.a/b (provenance minimale) ; le poste ne la code pas.
// - Valeurs conservées EXACTEMENT telles que reçues (aucune normalisation, aucun arrondi).
import { type ApiError, apiError, type ApiResult } from "../api/client.ts";
import { findFolded } from "../clients/searchText.ts";
import {
  displayStatus,
  type DocumentContent,
  type DocumentInvalidationReason,
  EYE_FIELDS,
  EYE_SIDES,
  type InvalidationRequest,
  type OpticalActor,
  type OpticalClientRef,
  type OpticalEvent,
  type OpticalGateway,
  type OpticalKind,
  type OpticalListParams,
  type OpticalPage,
  type OpticalRecord,
  type OpticalRecordSummary,
  type OpticalRevision,
  type RecordContent,
  type RectificationRequest,
  shownRevision,
  type SourceDocument,
  type SyntheticDocumentChoice,
} from "../optical/opticalContract.ts";
import { OPTICAL_PERMISSIONS } from "../optical/opticalAccess.ts";

/** Limites proposées (D-04-14.b, PROPOSED) appliquées par le double pour illustrer les refus. */
export const DOCUMENT_MAX_BYTES = 10 * 1024 * 1024;
export const DOCUMENT_MAX_ACTIVE = 10;
const ACCEPTED_TYPES = ["application/pdf", "image/jpeg", "image/png"] as const;
const NUMERIC_FIELDS = new Set(["sphere", "cylinder", "axis", "addition", "prism"]);
// Forme d'un nombre décimal signé (point ou virgule) ; la valeur reçue n'est jamais réécrite.
const NUMBER_SHAPE = /^[+\-−]?\s?\d+(?:[.,]\d+)?$/u;
const EYE_VALUE_MAX = 16;
const TEXT_MAX = 120;
const NOTES_MAX = 2000;

export interface BenchSession {
  actor: OpticalActor;
  permissions: readonly string[];
}

export interface BenchFaults {
  /** Délai simulé de chaque réponse (états de chargement, enregistrement en cours). */
  latencyMs: number;
  /** Lectures de liste à faire échouer (état 9) : 2 = la lecture et la nouvelle tentative automatique (queryClient.ts). */
  failedListReads: number;
  /** Échec technique simulé de la prochaine écriture (états 24, 31). */
  failNextWrite: boolean;
}

interface StoredDocument extends SourceDocument {
  digest: string;
  label: string;
}

interface StoredRecord {
  kind: OpticalKind;
  id: string;
  reference: string;
  clientId: string;
  lifecycle: "active" | "invalidated";
  invalidation: OpticalRecord["invalidation"];
  revisions: OpticalRevision[];
  documents: StoredDocument[];
  events: OpticalEvent[];
  createdAtUtc: string;
  createdBy: OpticalActor;
  version: number;
}

export interface InMemoryOpticalGatewayOptions {
  clients: () => readonly OpticalClientRef[];
  measurers: readonly OpticalActor[];
  session: () => BenchSession;
  faults?: () => BenchFaults;
  now?: () => string;
  /** Génère l'aperçu SYNTHÉTIQUE d'une pièce image (canvas du banc) ; `null` si indisponible. */
  renderImage?: (document: { label: string; mediaType: string }) => string | null;
}

/** Métadonnées servies : ni empreinte ni libellé interne du banc. */
const publicDocument = (document: StoredDocument): SourceDocument => ({
  id: document.id,
  mediaType: document.mediaType,
  sizeBytes: document.sizeBytes,
  uploadedAtUtc: document.uploadedAtUtc,
  uploadedBy: document.uploadedBy,
  originalName: document.originalName,
  invalidation: document.invalidation,
});

const NO_FAULTS: BenchFaults = { latencyMs: 0, failedListReads: 0, failNextWrite: false };

const clone = <T>(value: T): T => structuredClone(value);

const fail = <T>(kind: ApiError["kind"], status: number, code?: string, fieldErrors?: Record<string, string[]>): ApiResult<T> => ({
  ok: false,
  error: { ...apiError(kind, status), ...(code ? { code } : {}), ...(fieldErrors ? { fieldErrors } : {}) },
});

const ok = <T>(data: T, status = 200): ApiResult<T> => ({ ok: true, status, data: clone(data) });

/** Erreurs de forme du contenu (jamais une règle clinique) — codes bornés `champ: [code]`. */
export function shapeErrors(content: RecordContent): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  for (const side of EYE_SIDES) {
    for (const field of EYE_FIELDS) {
      const value = content[side].values[field];
      if (value.length > EYE_VALUE_MAX) errors[`${side}.${field}`] = ["too_long"];
      else if (value.trim() !== "" && NUMERIC_FIELDS.has(field) && !NUMBER_SHAPE.test(value.trim())) errors[`${side}.${field}`] = ["number_expected"];
    }
  }
  if (content.notes.length > NOTES_MAX) errors.notes = ["too_long"];
  if (content.kind === "prescription") {
    const { prescriberName, prescriberIdentifier } = content.provenance;
    if (prescriberName.length > TEXT_MAX) errors["provenance.prescriberName"] = ["too_long"];
    if (prescriberIdentifier.length > TEXT_MAX) errors["provenance.prescriberIdentifier"] = ["too_long"];
  }
  return errors;
}

/** Règle de DÉMONSTRATION du double (recommandations PROPOSED), jamais reprise par le poste. */
export function incompleteErrors(content: RecordContent): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  for (const side of EYE_SIDES) if (content[side].state === "not_transcribed") errors[`${side}.state`] = ["not_transcribed"];
  if (content.kind === "prescription") {
    if (content.provenance.prescriberName.trim() === "") errors["provenance.prescriberName"] = ["required_to_confirm"];
    if (content.provenance.prescribedOn === "") errors["provenance.prescribedOn"] = ["required_to_confirm"];
  } else {
    if (content.provenance.measuredOn === "") errors["provenance.measuredOn"] = ["required_to_confirm"];
    if (content.provenance.measuredByUserId === "") errors["provenance.measuredByUserId"] = ["required_to_confirm"];
  }
  return errors;
}

export class InMemoryOpticalGateway implements OpticalGateway {
  private readonly records: StoredRecord[] = [];
  private sequence = 0;
  private readonly options: InMemoryOpticalGatewayOptions;
  /** Journal des opérations reçues (méthode, enregistrement) — preuve « aucun appel réseau » dans les tests. */
  readonly calls: string[] = [];

  constructor(options: InMemoryOpticalGatewayOptions) {
    this.options = options;
  }

  // ---------- outils ----------

  private now(): string {
    return this.options.now?.() ?? new Date().toISOString();
  }

  private nextId(prefix: string): string {
    this.sequence += 1;
    return `${prefix}-${this.sequence.toString().padStart(4, "0")}`;
  }

  private async respond<T>(operation: string, run: () => ApiResult<T>, write = false): Promise<ApiResult<T>> {
    this.calls.push(operation);
    const faults = this.options.faults?.() ?? NO_FAULTS;
    if (faults.latencyMs > 0) await new Promise((resolve) => setTimeout(resolve, faults.latencyMs));
    if (write && faults.failNextWrite) {
      faults.failNextWrite = false;
      return fail("network", 0);
    }
    return run();
  }

  private has(permission: string): boolean {
    return this.options.session().permissions.includes(permission);
  }

  /** Lecture : `clients.read` + `optical.read`, sinon 403 AVANT toute recherche (O-04-15, refus indiscernables). */
  private canRead(): boolean {
    return this.has("clients.read") && this.has(OPTICAL_PERMISSIONS.read);
  }

  private canWrite(kind: OpticalKind): boolean {
    return this.canRead() && this.has(kind === "prescription" ? OPTICAL_PERMISSIONS.prescriptionsWrite : OPTICAL_PERMISSIONS.measurementsWrite);
  }

  private client(id: string): OpticalClientRef | undefined {
    return this.options.clients().find((candidate) => candidate.id === id);
  }

  private find(kind: OpticalKind, id: string): StoredRecord | undefined {
    return this.records.find((record) => record.kind === kind && record.id === id);
  }

  private event(record: StoredRecord, action: string, extra: Partial<OpticalEvent> = {}) {
    const code = action.startsWith("document.") ? action : `${record.kind}.${action}`;
    record.events.push({ id: this.nextId("EVT"), atUtc: this.now(), code, actor: this.options.session().actor, ...extra });
  }

  private draftOf(record: StoredRecord): OpticalRevision | undefined {
    return record.revisions.find((revision) => revision.status === "draft");
  }

  private lastConfirmedOf(record: StoredRecord): OpticalRevision | undefined {
    return [...record.revisions].reverse().find((revision) => revision.status === "confirmed");
  }

  private view(record: StoredRecord): OpticalRecord {
    const client = this.client(record.clientId) ?? { id: record.clientId, reference: "—", displayName: "Client inconnu", status: "active" as const };
    const readDocs = this.has(OPTICAL_PERMISSIONS.documentsRead);
    const lastConfirmed = this.lastConfirmedOf(record) ?? null;
    const openDraft = this.draftOf(record) ?? null;
    return clone({
      kind: record.kind,
      id: record.id,
      reference: record.reference,
      client,
      lifecycle: record.lifecycle,
      invalidation: record.invalidation,
      lastConfirmed,
      openDraft,
      revisions: record.revisions,
      // Sans `optical.documents.read` : aucune métadonnée nominative, seulement le nombre (D-04-05).
      documents: readDocs ? record.documents.map(publicDocument) : null,
      documentCount: record.documents.length,
      events: record.events,
      createdAtUtc: record.createdAtUtc,
      createdBy: record.createdBy,
      version: record.version,
    });
  }

  private summary(record: StoredRecord): OpticalRecordSummary {
    const view = this.view(record);
    const revision = shownRevision(view);
    const content = revision.content;
    return {
      kind: record.kind,
      id: record.id,
      reference: record.reference,
      client: view.client,
      status: displayStatus(view),
      revisionNo: view.lastConfirmed?.revisionNo ?? null,
      date: content.kind === "prescription" ? content.provenance.prescribedOn : content.provenance.measuredOn,
      source: this.sourceOf(content),
      od: content.od,
      og: content.og,
      recordedAtUtc: revision.startedAtUtc,
    };
  }

  private sourceOf(content: RecordContent): string {
    if (content.kind === "prescription") return content.provenance.prescriberName;
    return this.options.measurers.find((actor) => actor.id === content.provenance.measuredByUserId)?.displayName ?? "";
  }

  /** Vérifie les pièces liées à un brouillon : même enregistrement, non invalidées (E-01). */
  private linkErrors(record: StoredRecord, content: RecordContent): Record<string, string[]> {
    if (content.kind !== "prescription") return {};
    const invalid = content.linkedDocumentIds.some((id) => {
      const document = record.documents.find((candidate) => candidate.id === id);
      return document === undefined || document.invalidation !== null;
    });
    return invalid ? { linkedDocumentIds: ["invalid_document"] } : {};
  }

  private checkKind(record: StoredRecord, content: RecordContent): boolean {
    return content.kind === record.kind;
  }

  // ---------- lecture ----------

  listRecords(params: OpticalListParams): Promise<ApiResult<OpticalPage>> {
    return this.respond("listRecords", () => {
      const faults = this.options.faults?.();
      if (faults && faults.failedListReads > 0) {
        faults.failedListReads -= 1;
        return fail("network", 0);
      }
      if (!this.canRead()) return fail("forbidden", 403);
      const words = params.search.trim().split(/\s+/).filter((word) => [...word].length >= 2);
      let rows = this.records.map((record) => this.summary(record));
      rows = rows.filter((row) => {
        if (params.kind !== "all" && row.kind !== params.kind) return false;
        if (row.status === "invalidated") return params.includeInvalidated;
        return params.status === "all" || row.status === params.status;
      });
      if (words.length > 0) {
        rows = rows.filter((row) => {
          const haystack = `${row.client.displayName} ${row.client.reference} ${row.source} ${row.reference}`;
          return words.every((word) => findFolded(haystack, word) !== null);
        });
      }
      const [field, direction] = params.sort.split(":");
      const factor = direction === "asc" ? 1 : -1;
      rows.sort((a, b) => {
        if (field === "client") return factor * a.client.displayName.localeCompare(b.client.displayName, "fr");
        if (field === "recorded") return factor * a.recordedAtUtc.localeCompare(b.recordedAtUtc);
        // Dates absentes (brouillon) toujours en fin de liste, quel que soit le sens.
        if (a.date === "" || b.date === "") return a.date === b.date ? 0 : a.date === "" ? 1 : -1;
        return factor * a.date.localeCompare(b.date);
      });
      const start = (params.page - 1) * params.pageSize;
      return ok({ items: rows.slice(start, start + params.pageSize), page: params.page, pageSize: params.pageSize, totalCount: rows.length });
    });
  }

  listClientRecords(clientId: string, includeInvalidated: boolean): Promise<ApiResult<OpticalRecordSummary[]>> {
    return this.respond("listClientRecords", () => {
      if (!this.canRead()) return fail("forbidden", 403);
      if (!this.client(clientId)) return fail("not-found", 404, "not_found");
      const rows = this.records
        .filter((record) => record.clientId === clientId)
        .map((record) => this.summary(record))
        .filter((row) => includeInvalidated || row.status !== "invalidated")
        .sort((a, b) => b.recordedAtUtc.localeCompare(a.recordedAtUtc));
      return ok(rows);
    });
  }

  getRecord(kind: OpticalKind, id: string): Promise<ApiResult<OpticalRecord>> {
    return this.respond(`getRecord ${id}`, () => {
      if (!this.canRead()) return fail("forbidden", 403);
      const record = this.find(kind, id);
      return record ? ok(this.view(record)) : fail("not-found", 404, "not_found");
    });
  }

  listMeasurers(): Promise<ApiResult<OpticalActor[]>> {
    return this.respond("listMeasurers", () => (this.canRead() ? ok([...this.options.measurers]) : fail("forbidden", 403)));
  }

  // ---------- écriture ----------

  createRecord(clientId: string, content: RecordContent): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      "createRecord",
      () => {
        if (!this.canWrite(content.kind)) return fail("forbidden", 403);
        const client = this.client(clientId);
        if (!client) return fail("not-found", 404, "not_found");
        if (client.status === "archived") return fail("conflict", 409, "client_archived");
        const errors = shapeErrors(content);
        if (Object.keys(errors).length > 0) return fail("validation", 422, "validation_failed", errors);
        const at = this.now();
        const actor = this.options.session().actor;
        const prefix = content.kind === "prescription" ? "RX-FICTIF" : "MES-FICTIF";
        const rank = this.records.filter((candidate) => candidate.kind === content.kind).length + 1;
        // Une première saisie ne lie aucune pièce : la pièce se joint ensuite à l'enregistrement (E-01).
        const initial: RecordContent = content.kind === "prescription" ? { ...content, linkedDocumentIds: [] } : content;
        const record: StoredRecord = {
          kind: content.kind,
          id: this.nextId(content.kind === "prescription" ? "rx" : "mes"),
          reference: `${prefix}-${rank.toString().padStart(4, "0")}`,
          clientId,
          lifecycle: "active",
          invalidation: null,
          revisions: [this.newRevision(initial, null, null, "", at, actor)],
          documents: [],
          events: [],
          createdAtUtc: at,
          createdBy: actor,
          version: 1,
        };
        this.records.push(record);
        this.event(record, "created");
        return ok(this.view(record), 201);
      },
      true,
    );
  }

  private newRevision(content: RecordContent, baseRevisionNo: number | null, reasonCode: OpticalRevision["reasonCode"], reasonComment: string, at: string, actor: OpticalActor): OpticalRevision {
    return {
      id: this.nextId("rev"),
      status: "draft",
      revisionNo: null,
      baseRevisionNo,
      reasonCode,
      reasonComment,
      content: clone(content),
      startedAtUtc: at,
      startedBy: actor,
      updatedAtUtc: at,
      updatedBy: actor,
      confirmedAtUtc: null,
      confirmedBy: null,
      discardedAtUtc: null,
      discardedBy: null,
    };
  }

  /** Préalables communs : droit d'écriture du type, enregistrement existant et actif, version attendue. */
  private writable(kind: OpticalKind, id: string, version: number): StoredRecord | ApiResult<never> {
    if (!this.canWrite(kind)) return fail("forbidden", 403);
    const record = this.find(kind, id);
    if (!record) return fail("not-found", 404, "not_found");
    if (record.lifecycle === "invalidated") return fail("conflict", 409, "invalidated");
    if (record.version !== version) return fail("conflict", 409, "version_conflict");
    return record;
  }

  saveDraft(kind: OpticalKind, id: string, content: RecordContent, version: number): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `saveDraft ${id}`,
      () => {
        const record = this.writable(kind, id, version);
        if ("ok" in record) return record;
        const draft = this.draftOf(record);
        if (!draft) return fail("conflict", 409, "not_draft");
        if (!this.checkKind(record, content)) return fail("validation", 422, "kind_mismatch");
        if (this.client(record.clientId)?.status === "archived") return fail("conflict", 409, "client_archived");
        const errors = { ...shapeErrors(content), ...this.linkErrors(record, content) };
        if (Object.keys(errors).length > 0) return fail("validation", 422, "validation_failed", errors);
        // Corps identique : aucune écriture, aucun événement (comme O-03-44).
        if (JSON.stringify(draft.content) === JSON.stringify(content)) return ok(this.view(record));
        draft.content = clone(content);
        draft.updatedAtUtc = this.now();
        draft.updatedBy = this.options.session().actor;
        record.version += 1;
        this.event(record, "draft_updated");
        return ok(this.view(record));
      },
      true,
    );
  }

  confirmDraft(kind: OpticalKind, id: string, version: number): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `confirmDraft ${id}`,
      () => {
        const record = this.writable(kind, id, version);
        if ("ok" in record) return record;
        const draft = this.draftOf(record);
        if (!draft) return fail("conflict", 409, "not_draft");
        if (this.client(record.clientId)?.status === "archived") return fail("conflict", 409, "client_archived");
        const last = this.lastConfirmedOf(record);
        if ((last?.revisionNo ?? null) !== draft.baseRevisionNo) return fail("conflict", 409, "revision_conflict");
        const errors = incompleteErrors(draft.content);
        if (Object.keys(errors).length > 0) return fail("validation", 422, "incomplete", errors);
        const at = this.now();
        draft.status = "confirmed";
        draft.revisionNo = (last?.revisionNo ?? 0) + 1;
        draft.confirmedAtUtc = at;
        draft.confirmedBy = this.options.session().actor;
        record.version += 1;
        this.event(record, "confirmed", { revisionNo: draft.revisionNo });
        return ok(this.view(record));
      },
      true,
    );
  }

  startRectification(kind: OpticalKind, id: string, request: RectificationRequest): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `startRectification ${id}`,
      () => {
        const record = this.writable(kind, id, request.version);
        if ("ok" in record) return record;
        if (this.draftOf(record)) return fail("conflict", 409, "draft_exists");
        if (this.client(record.clientId)?.status === "archived") return fail("conflict", 409, "client_archived");
        const last = this.lastConfirmedOf(record);
        if (!last || last.revisionNo !== request.expectedRevisionNo) return fail("conflict", 409, "revision_conflict");
        if (request.reasonCode === "other" && request.comment.trim() === "") return fail("validation", 422, "validation_failed", { comment: ["required"] });
        // Le brouillon reprend le contenu et les LIENS de la révision de base, pas les octets des pièces (E-01).
        const content = clone(last.content);
        if (content.kind === "prescription") {
          content.linkedDocumentIds = content.linkedDocumentIds.filter((docId) => record.documents.find((doc) => doc.id === docId)?.invalidation === null);
        }
        record.revisions.push(this.newRevision(content, last.revisionNo, request.reasonCode, request.comment, this.now(), this.options.session().actor));
        record.version += 1;
        this.event(record, "rectification_started", { reasonCode: request.reasonCode });
        return ok(this.view(record), 201);
      },
      true,
    );
  }

  discardRectification(kind: OpticalKind, id: string, version: number): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `discardRectification ${id}`,
      () => {
        const record = this.writable(kind, id, version);
        if ("ok" in record) return record;
        const draft = this.draftOf(record);
        // Une première saisie ne s'abandonne pas : elle s'invalide (D-04-04.d).
        if (!draft || draft.baseRevisionNo === null) return fail("conflict", 409, "not_rectification");
        draft.status = "discarded";
        draft.discardedAtUtc = this.now();
        draft.discardedBy = this.options.session().actor;
        record.version += 1;
        this.event(record, "rectification_discarded");
        return ok(this.view(record));
      },
      true,
    );
  }

  invalidateRecord(kind: OpticalKind, id: string, request: InvalidationRequest): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `invalidateRecord ${id}`,
      () => {
        if (!this.canRead() || !this.has(OPTICAL_PERMISSIONS.invalidate)) return fail("forbidden", 403);
        const record = this.find(kind, id);
        if (!record) return fail("not-found", 404, "not_found");
        if (record.lifecycle === "invalidated") return fail("conflict", 409, "already_invalidated");
        if (record.version !== request.version) return fail("conflict", 409, "version_conflict");
        if (request.reasonCode === "other" && request.comment.trim() === "") return fail("validation", 422, "validation_failed", { comment: ["required"] });
        const at = this.now();
        const actor = this.options.session().actor;
        // Brouillon ouvert abandonné dans la MÊME opération (D-04-11.e, recommandation).
        const draft = this.draftOf(record);
        if (draft && draft.baseRevisionNo !== null) {
          draft.status = "discarded";
          draft.discardedAtUtc = at;
          draft.discardedBy = actor;
          this.event(record, "rectification_discarded");
        }
        record.lifecycle = "invalidated";
        record.invalidation = { atUtc: at, by: actor, reasonCode: request.reasonCode, comment: request.comment, discardedDraft: draft !== undefined && draft.baseRevisionNo !== null };
        record.version += 1;
        this.event(record, "invalidated", { reasonCode: request.reasonCode });
        return ok(this.view(record));
      },
      true,
    );
  }

  // ---------- pièces source (ordonnances seulement, D-04-03.c) ----------

  attachDocument(id: string, choice: SyntheticDocumentChoice): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `attachDocument ${id}`,
      () => {
        if (!this.canRead() || !this.has(OPTICAL_PERMISSIONS.documentsWrite)) return fail("forbidden", 403);
        const record = this.find("prescription", id);
        if (!record) return fail("not-found", 404, "not_found");
        if (record.lifecycle === "invalidated") return fail("conflict", 409, "invalidated");
        if (choice.sizeBytes > DOCUMENT_MAX_BYTES) return fail("validation", 413, "too_large");
        // Type reconnu par la « signature » (simulée) et concordant avec le type annoncé, sinon 415 (E-05).
        const accepted = (ACCEPTED_TYPES as readonly string[]).includes(choice.detectedType);
        if (!accepted || choice.detectedType !== choice.declaredType) return fail("validation", 415, "unsupported_media_type");
        if (record.documents.some((document) => document.digest === choice.digest)) return fail("conflict", 409, "duplicate_document");
        if (record.documents.filter((document) => document.invalidation === null).length >= DOCUMENT_MAX_ACTIVE) return fail("conflict", 409, "too_many_documents");
        const document: StoredDocument = {
          id: this.nextId("doc"),
          mediaType: choice.detectedType as StoredDocument["mediaType"],
          sizeBytes: choice.sizeBytes,
          uploadedAtUtc: this.now(),
          uploadedBy: this.options.session().actor,
          originalName: choice.name,
          invalidation: null,
          digest: choice.digest,
          label: choice.label,
        };
        record.documents.push(document);
        // Brouillon ouvert : la pièce lui est liée (lien modifiable jusqu'à la confirmation, E-01).
        const draft = this.draftOf(record);
        if (draft && draft.content.kind === "prescription") draft.content.linkedDocumentIds.push(document.id);
        record.version += 1;
        this.event(record, "document.attached");
        return ok(this.view(record), 201);
      },
      true,
    );
  }

  invalidateDocument(id: string, documentId: string, reasonCode: DocumentInvalidationReason): Promise<ApiResult<OpticalRecord>> {
    return this.respond(
      `invalidateDocument ${documentId}`,
      () => {
        if (!this.canRead() || !this.has(OPTICAL_PERMISSIONS.documentsWrite)) return fail("forbidden", 403);
        const record = this.find("prescription", id);
        const document = record?.documents.find((candidate) => candidate.id === documentId);
        if (!record || !document) return fail("not-found", 404, "not_found");
        if (document.invalidation !== null) return fail("conflict", 409, "already_invalidated");
        document.invalidation = { atUtc: this.now(), by: this.options.session().actor, reasonCode };
        // Retirée du brouillon ouvert ; les liens des révisions CONFIRMÉES sont conservés (historique, E-01).
        const draft = this.draftOf(record);
        if (draft && draft.content.kind === "prescription") draft.content.linkedDocumentIds = draft.content.linkedDocumentIds.filter((linked) => linked !== documentId);
        record.version += 1;
        this.event(record, "document.invalidated");
        return ok(this.view(record));
      },
      true,
    );
  }

  fetchDocumentContent(documentId: string): Promise<ApiResult<DocumentContent>> {
    return this.respond(`fetchDocumentContent ${documentId}`, () => {
      // Refus sans événement (D-04-12.d) ; même réponse pour une pièce inconnue sans droit.
      if (!this.canRead() || !this.has(OPTICAL_PERMISSIONS.documentsRead)) return fail("forbidden", 403);
      const record = this.records.find((candidate) => candidate.documents.some((document) => document.id === documentId));
      const document = record?.documents.find((candidate) => candidate.id === documentId);
      if (!record || !document) return fail("not-found", 404, "not_found");
      // Événement écrit AVANT la transmission : il atteste une transmission autorisée et commencée, pas une lecture.
      this.event(record, "document.content_served");
      const imageDataUrl = document.mediaType === "application/pdf" ? null : (this.options.renderImage?.({ label: document.label, mediaType: document.mediaType }) ?? null);
      return ok({ mediaType: document.mediaType, imageDataUrl });
    });
  }

  // ---------- commandes du banc (jamais exposées par le futur serveur) ----------

  /** Simule une écriture depuis un autre poste : la version change, la saisie en cours devient périmée (GAP-04-09). */
  simulateOtherWorkstation(kind: OpticalKind, id: string, actor: OpticalActor): boolean {
    const record = this.find(kind, id);
    if (!record) return false;
    record.version += 1;
    const draft = this.draftOf(record);
    if (draft) {
      draft.updatedAtUtc = this.now();
      draft.updatedBy = actor;
    }
    record.events.push({ id: this.nextId("EVT"), atUtc: this.now(), code: `${kind}.draft_updated`, actor });
    return true;
  }

  /** Raccourcis du banc : enregistrement d'une référence fictive (`RX-FICTIF-0002`…). */
  findByReference(reference: string): { kind: OpticalKind; id: string } | null {
    const record = this.records.find((candidate) => candidate.reference === reference);
    return record ? { kind: record.kind, id: record.id } : null;
  }

  get size(): number {
    return this.records.length;
  }
}
