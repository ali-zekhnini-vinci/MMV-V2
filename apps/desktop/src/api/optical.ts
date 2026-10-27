// Passerelle HTTP du dossier optique (phase 04-B) : routes réelles /api/optical de MMV.Api (contrat
// docs/phase/04/API_OPTICAL.md, extrait OpenAPI src/api/contract/optical.openapi.json). Le serveur reste seul juge des
// droits et des règles ; cette passerelle ne fait que convertir le contrat (valeurs absentes `null` ↔ `""` à l'écran) et
// garantir l'IDEMPOTENCE des écritures :
// - chaque écriture porte un `requestId` ; une réponse incertaine (réseau, délai, erreur serveur) est rejouée UNE fois
//   automatiquement avec le même identifiant — le serveur reconnaît la réémission et renvoie l'état sans rien réécrire ;
// - tant que l'issue reste incertaine, une nouvelle tentative IDENTIQUE reprend le même identifiant (aucun doublon) ;
//   une saisie modifiée part avec un nouvel identifiant et la version lue (409 si la première avait abouti) ;
// - une création incertaine reste rattachée au client et au type : la tentative suivante retrouve l'enregistrement déjà
//   créé au lieu d'en créer un second, puis y applique la saisie courante si elle a changé.
// Aucune valeur saisie n'est journalisée ; le jeton de session est transmis par `authorized`, jamais conservé ici.
import type {
  EyeField,
  EyeValues,
  InvalidationRequest,
  OpticalActor,
  OpticalEvent,
  OpticalGateway,
  OpticalKind,
  OpticalListParams,
  OpticalPage,
  OpticalRecord,
  OpticalRecordSummary,
  OpticalRevision,
  RecordContent,
  RectificationRequest,
} from "../optical/opticalContract.ts";
import { EYE_FIELDS, INVALIDATION_REASONS, RECTIFICATION_REASONS } from "../optical/opticalContract.ts";
import { type ApiErrorKind, type ApiResult, requestJson } from "./client.ts";

export type Authorized = <T>(call: (baseUrl: URL, token: string) => Promise<ApiResult<T>>) => Promise<ApiResult<T>>;

const SEGMENT: Record<OpticalKind, string> = { prescription: "prescriptions", measurement: "measurements" };

/** Issues incertaines : l'écriture a pu aboutir côté serveur. */
const AMBIGUOUS: ReadonlySet<ApiErrorKind> = new Set(["network", "timeout", "http", "invalid-response"]);

/** Délai des écritures : une confirmation reste courte, mais laisse au serveur le temps d'attendre un verrou. */
const WRITE_TIMEOUT_MS = 15_000;

// ---------- corps des requêtes ----------

const orNull = (value: string): string | null => (value.trim() === "" ? null : value);

function eyeBody(values: EyeValues): Record<EyeField, string | null> {
  return Object.fromEntries(EYE_FIELDS.map((field) => [field, orNull(values[field])])) as Record<EyeField, string | null>;
}

/** Contenu sans champ « type » (la route le porte) ; champs vides → `null` ; valeurs envoyées telles que saisies. */
export function contentBody(content: RecordContent): Record<string, unknown> {
  const provenance =
    content.kind === "prescription"
      ? {
          prescriberName: orNull(content.provenance.prescriberName),
          prescriberQualification: orNull(content.provenance.prescriberQualification),
          prescriberIdentifier: orNull(content.provenance.prescriberIdentifier),
          prescribedOn: orNull(content.provenance.prescribedOn),
          receivedOn: orNull(content.provenance.receivedOn),
        }
      : {
          measuredOn: orNull(content.provenance.measuredOn),
          measuredByUserId: orNull(content.provenance.measuredByUserId),
          category: orNull(content.provenance.category),
        };
  return { provenance, od: eyeBody(content.od), og: eyeBody(content.og), notes: orNull(content.notes) };
}

/** Chaîne de requête de la liste globale (valeurs de liste blanche seulement). */
export function opticalListQuery(params: OpticalListParams): string {
  const query = new URLSearchParams();
  const search = params.search.trim().slice(0, 100);
  if (search !== "") query.set("q", search);
  query.set("kind", params.kind);
  query.set("status", params.status);
  query.set("includeInvalidated", params.includeInvalidated ? "true" : "false");
  const [sort, dir] = params.sort.split(":");
  query.set("sort", sort === "client" || sort === "recorded" ? sort : "date");
  query.set("dir", dir === "asc" ? "asc" : "desc");
  query.set("page", String(Math.max(1, Math.trunc(params.page) || 1)));
  query.set("pageSize", String(Math.min(100, Math.max(1, Math.trunc(params.pageSize) || 20))));
  return query.toString();
}

// ---------- lecture des réponses (contrat strict, `null` → `""`) ----------

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);
const text = (value: unknown): string | undefined => (value === null ? "" : typeof value === "string" ? value : undefined);
const isVersion = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;
const isNumberOrNull = (value: unknown): value is number | null => value === null || (typeof value === "number" && Number.isSafeInteger(value));

function parseActor(value: unknown): OpticalActor | null {
  return isRecord(value) && typeof value.id === "string" && typeof value.displayName === "string" ? { id: value.id, displayName: value.displayName } : null;
}

function parseEye(value: unknown): EyeValues | null {
  if (!isRecord(value)) return null;
  const eye: Partial<EyeValues> = {};
  for (const field of EYE_FIELDS) {
    const parsed = text(value[field]);
    if (parsed === undefined) return null;
    eye[field] = parsed;
  }
  return eye as EyeValues;
}

function parseClient(value: unknown): OpticalRecord["client"] | null {
  if (!isRecord(value)) return null;
  const { id, reference, displayName, status } = value;
  return typeof id === "string" && typeof reference === "string" && typeof displayName === "string" && (status === "active" || status === "archived")
    ? { id, reference, displayName, status }
    : null;
}

function parseContent(kind: OpticalKind, value: unknown): RecordContent | null {
  if (!isRecord(value) || !isRecord(value.provenance)) return null;
  const od = parseEye(value.od);
  const og = parseEye(value.og);
  const notes = text(value.notes);
  if (od === null || og === null || notes === undefined) return null;
  const p = value.provenance;
  if (kind === "prescription") {
    const fields = [p.prescriberName, p.prescriberQualification, p.prescriberIdentifier, p.prescribedOn, p.receivedOn].map(text);
    if (fields.some((field) => field === undefined)) return null;
    const [prescriberName, prescriberQualification, prescriberIdentifier, prescribedOn, receivedOn] = fields as string[];
    return { kind, provenance: { prescriberName, prescriberQualification, prescriberIdentifier, prescribedOn, receivedOn }, od, og, notes };
  }
  const fields = [p.measuredOn, p.measuredByUserId, p.category].map(text);
  if (fields.some((field) => field === undefined)) return null;
  const [measuredOn, measuredByUserId, category] = fields as string[];
  return { kind, provenance: { measuredOn, measuredByUserId, category }, od, og, notes };
}

function parseRevision(kind: OpticalKind, value: unknown): OpticalRevision | null {
  if (!isRecord(value)) return null;
  const { id, status, revisionNo, baseRevisionNo, reasonCode, reasonComment, startedAtUtc, updatedAtUtc, confirmedAtUtc, discardedAtUtc } = value;
  const content = parseContent(kind, value.content);
  const startedBy = parseActor(value.startedBy);
  const updatedBy = parseActor(value.updatedBy);
  const confirmedBy = value.confirmedBy === null ? null : parseActor(value.confirmedBy);
  const discardedBy = value.discardedBy === null ? null : parseActor(value.discardedBy);
  const comment = text(reasonComment);
  if (
    typeof id !== "string" ||
    (status !== "draft" && status !== "confirmed" && status !== "discarded") ||
    !isNumberOrNull(revisionNo) ||
    !isNumberOrNull(baseRevisionNo) ||
    !(reasonCode === null || (typeof reasonCode === "string" && (RECTIFICATION_REASONS as readonly string[]).includes(reasonCode))) ||
    comment === undefined ||
    content === null ||
    typeof startedAtUtc !== "string" ||
    startedBy === null ||
    typeof updatedAtUtc !== "string" ||
    updatedBy === null ||
    !(confirmedAtUtc === null || typeof confirmedAtUtc === "string") ||
    (value.confirmedBy !== null && confirmedBy === null) ||
    !(discardedAtUtc === null || typeof discardedAtUtc === "string") ||
    (value.discardedBy !== null && discardedBy === null)
  ) {
    return null;
  }
  return {
    id,
    status,
    revisionNo,
    baseRevisionNo,
    reasonCode: reasonCode as OpticalRevision["reasonCode"],
    reasonComment: comment,
    content,
    startedAtUtc,
    startedBy,
    updatedAtUtc,
    updatedBy,
    confirmedAtUtc,
    confirmedBy,
    discardedAtUtc,
    discardedBy,
  };
}

function parseEvent(value: unknown): OpticalEvent | null {
  if (!isRecord(value)) return null;
  const actor = parseActor(value.actor);
  const { id, atUtc, code, revisionNo, reasonCode } = value;
  if (typeof id !== "string" || typeof atUtc !== "string" || typeof code !== "string" || actor === null || !isNumberOrNull(revisionNo) || !(reasonCode === null || typeof reasonCode === "string")) {
    return null;
  }
  return { id, atUtc, code, actor, ...(revisionNo === null ? {} : { revisionNo }), ...(reasonCode === null ? {} : { reasonCode }) };
}

function parseList<T>(value: unknown, parse: (item: unknown) => T | null): T[] | null {
  if (!Array.isArray(value)) return null;
  const items = value.map(parse);
  return items.every((item) => item !== null) ? (items as T[]) : null;
}

export function parseOpticalRecord(value: unknown): OpticalRecord | null {
  if (!isRecord(value) || (value.kind !== "prescription" && value.kind !== "measurement")) return null;
  const kind = value.kind;
  const { id, reference, lifecycle, createdAtUtc, version, historyAvailable } = value;
  const client = parseClient(value.client);
  const createdBy = parseActor(value.createdBy);
  const lastConfirmed = value.lastConfirmed === null ? null : parseRevision(kind, value.lastConfirmed);
  const openDraft = value.openDraft === null ? null : parseRevision(kind, value.openDraft);
  const revisions = parseList(value.revisions, (item) => parseRevision(kind, item));
  const events = parseList(value.events, parseEvent);
  let invalidation: OpticalRecord["invalidation"] = null;
  if (value.invalidation !== null) {
    const raw = value.invalidation;
    const by = isRecord(raw) ? parseActor(raw.by) : null;
    const comment = isRecord(raw) ? text(raw.comment) : undefined;
    if (
      !isRecord(raw) ||
      typeof raw.atUtc !== "string" ||
      by === null ||
      typeof raw.reasonCode !== "string" ||
      !(INVALIDATION_REASONS as readonly string[]).includes(raw.reasonCode) ||
      comment === undefined ||
      typeof raw.discardedDraft !== "boolean"
    ) {
      return null;
    }
    invalidation = { atUtc: raw.atUtc, by, reasonCode: raw.reasonCode as InvalidationRequest["reasonCode"], comment, discardedDraft: raw.discardedDraft };
  }
  if (
    typeof id !== "string" ||
    typeof reference !== "string" ||
    client === null ||
    (lifecycle !== "active" && lifecycle !== "invalidated") ||
    (value.lastConfirmed !== null && lastConfirmed === null) ||
    (value.openDraft !== null && openDraft === null) ||
    revisions === null ||
    events === null ||
    typeof historyAvailable !== "boolean" ||
    typeof createdAtUtc !== "string" ||
    createdBy === null ||
    !isVersion(version)
  ) {
    return null;
  }
  return { kind, id, reference, client, lifecycle, invalidation, lastConfirmed, openDraft, revisions, historyAvailable, events, createdAtUtc, createdBy, version };
}

export function parseOpticalSummary(value: unknown): OpticalRecordSummary | null {
  if (!isRecord(value) || (value.kind !== "prescription" && value.kind !== "measurement")) return null;
  const { kind, id, reference, status, revisionNo, recordedAtUtc } = value;
  const client = parseClient(value.client);
  const date = text(value.date);
  const source = text(value.source);
  const od = parseEye(value.od);
  const og = parseEye(value.og);
  if (
    typeof id !== "string" ||
    typeof reference !== "string" ||
    client === null ||
    (status !== "draft" && status !== "confirmed" && status !== "rectification" && status !== "invalidated") ||
    !isNumberOrNull(revisionNo) ||
    date === undefined ||
    source === undefined ||
    od === null ||
    og === null ||
    typeof recordedAtUtc !== "string"
  ) {
    return null;
  }
  return { kind, id, reference, client, status, revisionNo, date, source, od, og, recordedAtUtc };
}

function parsePage(value: unknown): OpticalPage | null {
  if (!isRecord(value)) return null;
  const items = parseList(value.items, parseOpticalSummary);
  const { page, pageSize, totalCount } = value;
  return items !== null && isVersion(page) && isVersion(pageSize) && isVersion(totalCount) ? { items, page, pageSize, totalCount } : null;
}

// ---------- passerelle ----------

const sameContent = (left: RecordContent, right: RecordContent) => JSON.stringify(contentBody(left)) === JSON.stringify(contentBody(right));

export interface HttpOpticalGatewayOptions {
  /** Générateur d'identifiants de requête (tests) ; par défaut `crypto.randomUUID()`. */
  newRequestId?: () => string;
}

export function createHttpOpticalGateway(authorized: Authorized, options: HttpOpticalGatewayOptions = {}): OpticalGateway {
  const newRequestId = options.newRequestId ?? (() => crypto.randomUUID());
  // Écritures dont l'issue est incertaine : clé de l'opération → identifiant de requête à réutiliser.
  const pending = new Map<string, string>();

  const read = <T>(path: string, parse: (body: unknown) => T | null) => authorized((baseUrl, token) => requestJson(baseUrl, path, { token, parse }));

  const send = (method: "POST" | "PUT", path: string, body: Record<string, unknown>) =>
    authorized((baseUrl, token) =>
      requestJson(baseUrl, path, { method, token, body, parse: parseOpticalRecord, acceptedStatuses: [200, 201], timeoutMs: WRITE_TIMEOUT_MS }),
    );

  /** Écriture idempotente : un rejeu automatique en cas d'issue incertaine, identifiant conservé tant qu'elle le reste. */
  async function write(key: string, call: (requestId: string) => Promise<ApiResult<OpticalRecord>>): Promise<ApiResult<OpticalRecord>> {
    const requestId = pending.get(key) ?? newRequestId();
    pending.set(key, requestId);
    let result = await call(requestId);
    if (!result.ok && AMBIGUOUS.has(result.error.kind)) result = await call(requestId);
    if (result.ok || !AMBIGUOUS.has(result.error.kind)) pending.delete(key);
    return result;
  }

  const recordKey = (operation: string, kind: OpticalKind, id: string, version: number, body: unknown) =>
    `${operation}:${kind}:${id}:${version}:${JSON.stringify(body)}`;

  const saveDraft = (kind: OpticalKind, id: string, content: RecordContent, version: number) => {
    const body = contentBody(content);
    return write(recordKey("draft", kind, id, version, body), (requestId) => send("PUT", `/api/optical/${SEGMENT[kind]}/${id}/draft`, { requestId, version, content: body }));
  };

  const saveAndConfirm = (kind: OpticalKind, id: string, content: RecordContent, version: number) => {
    const body = contentBody(content);
    return write(recordKey("save-confirm", kind, id, version, body), (requestId) =>
      send("POST", `/api/optical/${SEGMENT[kind]}/${id}/draft/confirm`, { requestId, version, content: body }),
    );
  };

  /**
   * Création (brouillon ou confirmée). Une création incertaine reste rattachée au client et au type : la tentative
   * suivante reçoit l'enregistrement déjà créé ; si la saisie ou l'intention a changé entre-temps, elle y est appliquée.
   */
  async function create(clientId: string, content: RecordContent, confirm: boolean): Promise<ApiResult<OpticalRecord>> {
    const created = await write(`create:${content.kind}:${clientId}`, (requestId) =>
      send("POST", `/api/optical/${SEGMENT[content.kind]}`, { requestId, clientId, confirm, content: contentBody(content) }),
    );
    if (!created.ok) return created;
    const record = created.data;
    const shown = record.openDraft ?? record.lastConfirmed;
    if (confirm && record.openDraft !== null) return saveAndConfirm(record.kind, record.id, content, record.version);
    if (!confirm && record.openDraft !== null && shown !== null && !sameContent(shown.content, content)) return saveDraft(record.kind, record.id, content, record.version);
    return created;
  }

  return {
    listRecords: (params) => read(`/api/optical/records?${opticalListQuery(params)}`, parsePage),
    listClientRecords: (clientId, includeInvalidated) =>
      read(`/api/optical/clients/${encodeURIComponent(clientId)}/records?includeInvalidated=${includeInvalidated ? "true" : "false"}`, (body) =>
        parseList(body, parseOpticalSummary),
      ),
    getRecord: (kind, id) => read(`/api/optical/${SEGMENT[kind]}/${encodeURIComponent(id)}`, parseOpticalRecord),
    listMeasurers: () => read("/api/optical/measurers", (body) => parseList(body, parseActor)),
    createRecord: (clientId, content) => create(clientId, content, false),
    createAndConfirm: (clientId, content) => create(clientId, content, true),
    saveDraft,
    saveAndConfirm,
    confirmDraft: (kind, id, version) =>
      write(recordKey("confirm", kind, id, version, null), (requestId) => send("POST", `/api/optical/${SEGMENT[kind]}/${id}/draft/confirm`, { requestId, version })),
    startRectification: (kind, id, request: RectificationRequest) =>
      write(recordKey("rectify", kind, id, request.version, request), (requestId) =>
        send("POST", `/api/optical/${SEGMENT[kind]}/${id}/rectifications`, {
          requestId,
          version: request.version,
          expectedRevisionNo: request.expectedRevisionNo,
          reasonCode: request.reasonCode,
          comment: orNull(request.comment),
        }),
      ),
    discardRectification: (kind, id, version) =>
      write(recordKey("discard", kind, id, version, null), (requestId) => send("POST", `/api/optical/${SEGMENT[kind]}/${id}/draft/discard`, { requestId, version })),
    invalidateRecord: (kind, id, request: InvalidationRequest) =>
      write(recordKey("invalidate", kind, id, request.version, request), (requestId) =>
        send("POST", `/api/optical/${SEGMENT[kind]}/${id}/invalidate`, { requestId, version: request.version, reasonCode: request.reasonCode, comment: orNull(request.comment) }),
      ),
  };
}
