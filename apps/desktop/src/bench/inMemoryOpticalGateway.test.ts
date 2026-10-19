// Double en mémoire du banc 04-A : cycle de vie PROPOSÉ (E-02), pièces liées par révision (E-01), refus du contrat
// proposé, valeurs conservées à l'identique, droits simulés. Aucun réseau, aucune base. Ce n'est pas une preuve
// serveur : MMV.Api n'a aucune route optique (04-B non commencé).
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { OPTICAL_PERMISSIONS } from "../optical/opticalAccess.ts";
import { emptyContent, EMPTY_EYE_VALUES, type PrescriptionContent } from "../optical/opticalContract.ts";
import { ALL_OPTICAL_PERMISSIONS, BENCH_MEASURERS, createBenchClients, createSeededGateway, SYNTHETIC_DOCUMENTS } from "./benchSeed.ts";
import type { BenchSession, InMemoryOpticalGateway } from "./inMemoryOpticalGateway.ts";

const actor = BENCH_MEASURERS[0];

async function setup(permissions: readonly string[] = ALL_OPTICAL_PERMISSIONS) {
  const session: { current: BenchSession } = { current: { actor, permissions } };
  const gateway = await createSeededGateway({ clients: createBenchClients(), session: () => session.current });
  return { gateway, session };
}

function syn(id: string) {
  const choice = SYNTHETIC_DOCUMENTS.find((candidate) => candidate.id === id);
  if (!choice) throw new Error(`pièce synthétique inconnue : ${id}`);
  return choice;
}

function refOf(gateway: InMemoryOpticalGateway, reference: string) {
  const found = gateway.findByReference(reference);
  if (!found) throw new Error(`référence fictive absente : ${reference}`);
  return found;
}

function rxContent(values: Partial<typeof EMPTY_EYE_VALUES> = {}): PrescriptionContent {
  const content = emptyContent("prescription") as PrescriptionContent;
  return { ...content, od: { state: "filled", values: { ...EMPTY_EYE_VALUES, ...values } } };
}

const must = <T>(result: { ok: true; data: T } | { ok: false; error: unknown }): T => {
  if (!result.ok) throw new Error(`échec inattendu : ${JSON.stringify(result.error)}`);
  return result.data;
};

describe("double optique du banc — valeurs et forme", () => {
  test("valeurs conservées EXACTEMENT : signe, décimales, virgule, 0 et 180, vide ≠ 0", async () => {
    const { gateway } = await setup();
    const values = { sphere: "+1,25", cylinder: "-0.5", axis: "0", addition: "2", prism: "0,00", base: "BN", acuity: "8/10" };
    const record = must(await gateway.createRecord("cli-fictif-3", rxContent(values)));
    assert.deepEqual(record.openDraft?.content.od.values, values);
    assert.equal(record.openDraft?.content.og.values.sphere, "");
    const again = must(await gateway.getRecord("prescription", record.id));
    assert.equal(again.openDraft?.content.od.values.axis, "0", "aucune normalisation 0° → 180°");
    assert.equal(again.openDraft?.content.od.values.prism, "0,00");
  });

  test("seules des erreurs de FORME : nombre attendu, longueur ; aucune borne", async () => {
    const { gateway } = await setup();
    const refused = await gateway.createRecord("cli-fictif-3", rxContent({ sphere: "abc", axis: "999", cylinder: "-45,75", acuity: "x".repeat(17) }));
    assert.equal(refused.ok, false);
    if (refused.ok) return;
    assert.equal(refused.error.status, 422);
    assert.deepEqual(refused.error.fieldErrors, { "od.sphere": ["number_expected"], "od.acuity": ["too_long"] });
    // 999 et -45,75 : aucune borne clinique (D-04-06 ouverte).
    assert.equal((await gateway.createRecord("cli-fictif-3", rxContent({ axis: "999", cylinder: "-45,75" }))).ok, true);
  });
});

describe("double optique du banc — cycle de vie proposé (E-02)", () => {
  test("brouillon → 422 incomplete (règle de démonstration) → confirmé révision 1", async () => {
    const { gateway } = await setup();
    const draft = must(await gateway.createRecord("cli-fictif-3", rxContent({ sphere: "+1,00" })));
    const refused = await gateway.confirmDraft("prescription", draft.id, draft.version);
    assert.equal(refused.ok, false);
    if (!refused.ok) {
      assert.equal(refused.error.code, "incomplete");
      assert.deepEqual(Object.keys(refused.error.fieldErrors ?? {}).sort(), ["og.state", "provenance.prescribedOn", "provenance.prescriberName"]);
    }
    const complete: PrescriptionContent = {
      ...(draft.openDraft?.content as PrescriptionContent),
      provenance: { ...(draft.openDraft?.content as PrescriptionContent).provenance, prescriberName: "Dr Fictif-Test", prescribedOn: "2026-10-01" },
      og: { state: "not_prescribed", values: { ...EMPTY_EYE_VALUES } },
    };
    const saved = must(await gateway.saveDraft("prescription", draft.id, complete, draft.version));
    const confirmed = must(await gateway.confirmDraft("prescription", saved.id, saved.version));
    assert.equal(confirmed.lastConfirmed?.revisionNo, 1);
    assert.equal(confirmed.openDraft, null);
  });

  test("rectification : révision 1 intacte, un seul brouillon (409 draft_exists), numéro attribué à la confirmation", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0006");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const revisionOne = JSON.stringify(record.lastConfirmed);
    const opened = must(await gateway.startRectification(ref.kind, ref.id, { expectedRevisionNo: 1, reasonCode: "transcription_error", comment: "", version: record.version }));
    assert.equal(opened.lastConfirmed?.revisionNo, 1);
    assert.equal(opened.openDraft?.revisionNo, null);
    assert.equal(opened.openDraft?.baseRevisionNo, 1);
    const second = await gateway.startRectification(ref.kind, ref.id, { expectedRevisionNo: 1, reasonCode: "other", comment: "x", version: opened.version });
    assert.equal(!second.ok && second.error.code, "draft_exists");
    const content = { ...(opened.openDraft?.content as PrescriptionContent), notes: "rectifié (fictif)" };
    const saved = must(await gateway.saveDraft(ref.kind, ref.id, content, opened.version));
    const confirmed = must(await gateway.confirmDraft(ref.kind, ref.id, saved.version));
    assert.equal(confirmed.lastConfirmed?.revisionNo, 2);
    assert.equal(JSON.stringify(confirmed.revisions.find((revision) => revision.revisionNo === 1)), revisionOne, "révision 1 inchangée");
  });

  test("abandon d'une rectification : conservée « discarded », numéro non consommé", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0002");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const discarded = must(await gateway.discardRectification(ref.kind, ref.id, record.version));
    assert.equal(discarded.openDraft, null);
    assert.equal(discarded.revisions.filter((revision) => revision.status === "discarded").length, 1);
    const reopened = must(await gateway.startRectification(ref.kind, ref.id, { expectedRevisionNo: 1, reasonCode: "other", comment: "fictif", version: discarded.version }));
    const confirmed = must(await gateway.confirmDraft(ref.kind, ref.id, reopened.version));
    assert.equal(confirmed.lastConfirmed?.revisionNo, 2);
  });

  test("conflit de version (autre poste) : 409 sans écriture", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0004");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    gateway.simulateOtherWorkstation(ref.kind, ref.id, { id: "b", displayName: "Fictif-Poste B" });
    const stale = await gateway.saveDraft(ref.kind, ref.id, { ...(record.openDraft?.content as PrescriptionContent), notes: "poste A" }, record.version);
    assert.equal(!stale.ok && stale.error.code, "version_conflict");
    assert.equal(must(await gateway.getRecord(ref.kind, ref.id)).openDraft?.content.notes, record.openDraft?.content.notes);
  });

  test("invalidation : conservée, rectification abandonnée dans la même opération, exclue des listes par défaut", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0002");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const invalidated = must(await gateway.invalidateRecord(ref.kind, ref.id, { reasonCode: "entry_error", comment: "", version: record.version }));
    assert.equal(invalidated.lifecycle, "invalidated");
    assert.equal(invalidated.invalidation?.discardedDraft, true);
    assert.equal(invalidated.openDraft, null);
    assert.equal(invalidated.lastConfirmed?.revisionNo, 1, "révision confirmée conservée");
    const again = await gateway.invalidateRecord(ref.kind, ref.id, { reasonCode: "entry_error", comment: "", version: invalidated.version });
    assert.equal(!again.ok && again.error.code, "already_invalidated");
    const list = must(await gateway.listClientRecords("cli-fictif-2", false));
    assert.equal(list.some((row) => row.id === ref.id), false);
    assert.equal(must(await gateway.listClientRecords("cli-fictif-2", true)).some((row) => row.id === ref.id), true);
  });

  test("client archivé : création et rectification refusées (409), consultation permise", async () => {
    const { gateway } = await setup();
    const created = await gateway.createRecord("cli-fictif-4", rxContent());
    assert.equal(!created.ok && created.error.code, "client_archived");
    const ref = refOf(gateway, "RX-FICTIF-0005");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const rectify = await gateway.startRectification(ref.kind, ref.id, { expectedRevisionNo: 1, reasonCode: "other", comment: "x", version: record.version });
    assert.equal(!rectify.ok && rectify.error.code, "client_archived");
  });

  test("D-04-01 : aucune conversion — un brouillon de mesure n'accepte pas un contenu d'ordonnance", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "MES-FICTIF-0002");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const mixed = await gateway.saveDraft("measurement", ref.id, rxContent(), record.version);
    assert.equal(!mixed.ok && mixed.error.code, "kind_mismatch");
    assert.equal((await gateway.getRecord("prescription", ref.id)).ok, false, "une mesure n'est jamais lue comme une ordonnance");
  });
});

describe("double optique du banc — pièces source (E-01, E-05)", () => {
  test("liens par révision : la révision 1 cite le recto seul, la révision 2 recto + verso, contenu stocké une fois", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0001");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const [recto, verso] = record.documents ?? [];
    const byNo = (no: number) => record.revisions.find((revision) => revision.revisionNo === no)?.content as PrescriptionContent;
    assert.deepEqual(byNo(1).linkedDocumentIds, [recto.id]);
    assert.deepEqual(byNo(2).linkedDocumentIds, [recto.id, verso.id]);
    assert.equal(record.documents?.length, 2);
  });

  test("refus : 413 trop volumineux, 415 signature ≠ type annoncé (SVG, HTML), 409 doublon d'empreinte", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0001");
    const code = async (id: string) => {
      const result = await gateway.attachDocument(ref.id, syn(id));
      return result.ok ? "ok" : `${result.error.status} ${result.error.code}`;
    };
    assert.equal(await code("syn-big"), "413 too_large");
    assert.equal(await code("syn-svg"), "415 unsupported_media_type");
    assert.equal(await code("syn-html"), "415 unsupported_media_type");
    assert.equal(await code("syn-recto"), "409 duplicate_document");
    assert.equal(await code("syn-scan"), "ok");
  });

  test("pièce invalidée : liens historiques conservés, plus liable à un brouillon, contenu servi avec événement « transmis »", async () => {
    const { gateway } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0002");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    const photo = record.documents?.find((document) => document.invalidation !== null);
    assert.ok(photo);
    assert.deepEqual((record.lastConfirmed?.content as PrescriptionContent).linkedDocumentIds, [photo.id]);
    const link = await gateway.saveDraft(ref.kind, ref.id, { ...(record.openDraft?.content as PrescriptionContent), linkedDocumentIds: [photo.id] }, record.version);
    assert.deepEqual(!link.ok && link.error.fieldErrors, { linkedDocumentIds: ["invalid_document"] });
    const before = record.events.length;
    assert.equal((await gateway.fetchDocumentContent(photo.id)).ok, true);
    const after = must(await gateway.getRecord(ref.kind, ref.id));
    assert.equal(after.events.length, before + 1);
    assert.equal(after.events[after.events.length - 1].code, "document.content_served");
  });
});

describe("double optique du banc — droits simulés (D-04-05, PROPOSED)", () => {
  test("sans optical.read : 403 partout, identifiant connu ou inconnu indiscernables", async () => {
    const { gateway, session } = await setup();
    const ref = refOf(gateway, "RX-FICTIF-0001");
    session.current = { actor, permissions: ["clients.read"] };
    const known = await gateway.getRecord(ref.kind, ref.id);
    const unknown = await gateway.getRecord("prescription", "rx-inconnu");
    assert.deepEqual(known, unknown);
    assert.equal(!known.ok && known.error.status, 403);
    assert.equal(!(await gateway.listRecords({ search: "", kind: "all", status: "all", includeInvalidated: true, sort: "date:desc", page: 1, pageSize: 8 })).ok, true);
  });

  test("droit d'écriture par TYPE ; pièces : nombre seulement sans optical.documents.read, contenu refusé sans événement", async () => {
    const { gateway, session } = await setup();
    session.current = { actor, permissions: ["clients.read", OPTICAL_PERMISSIONS.read, OPTICAL_PERMISSIONS.measurementsWrite] };
    const rx = await gateway.createRecord("cli-fictif-3", rxContent());
    assert.equal(!rx.ok && rx.error.status, 403);
    assert.equal((await gateway.createRecord("cli-fictif-3", emptyContent("measurement"))).ok, true);
    const ref = refOf(gateway, "RX-FICTIF-0001");
    const record = must(await gateway.getRecord(ref.kind, ref.id));
    assert.equal(record.documents, null);
    assert.equal(record.documentCount, 2);
    session.current = { actor, permissions: ALL_OPTICAL_PERMISSIONS };
    const docId = must(await gateway.getRecord(ref.kind, ref.id)).documents?.[0]?.id ?? "";
    session.current = { actor, permissions: ["clients.read", OPTICAL_PERMISSIONS.read] };
    const events = record.events.length;
    assert.equal(!(await gateway.fetchDocumentContent(docId)).ok, true);
    session.current = { actor, permissions: ALL_OPTICAL_PERMISSIONS };
    assert.equal(must(await gateway.getRecord(ref.kind, ref.id)).events.length, events, "refus non journalisé (D-04-12.d)");
  });
});
