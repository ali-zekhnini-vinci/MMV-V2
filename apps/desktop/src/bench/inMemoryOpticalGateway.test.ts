// Double en mémoire du banc 04-A : cycle de vie PROPOSÉ (E-02), simplification D-04-16 (ni état d'œil ni pièce source),
// refus du contrat proposé, valeurs conservées à l'identique, droits simulés. Aucun réseau, aucune base. Ce n'est pas une preuve
// serveur : MMV.Api n'a aucune route optique (04-B non commencé).
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { OPTICAL_PERMISSIONS } from "../optical/opticalAccess.ts";
import { emptyContent, EMPTY_EYE_VALUES, EYE_FIELDS, type PrescriptionContent } from "../optical/opticalContract.ts";
import { compareRows } from "../optical/opticalRevisions.ts";
import { ALL_OPTICAL_PERMISSIONS, BENCH_MEASURERS, createBenchClients, createSeededGateway } from "./benchSeed.ts";
import type { BenchSession, InMemoryOpticalGateway } from "./inMemoryOpticalGateway.ts";

const actor = BENCH_MEASURERS[0];

async function setup(permissions: readonly string[] = ALL_OPTICAL_PERMISSIONS) {
  const session: { current: BenchSession } = { current: { actor, permissions } };
  const gateway = await createSeededGateway({ clients: createBenchClients(), session: () => session.current });
  return { gateway, session };
}

function refOf(gateway: InMemoryOpticalGateway, reference: string) {
  const found = gateway.findByReference(reference);
  if (!found) throw new Error(`référence fictive absente : ${reference}`);
  return found;
}

function rxContent(values: Partial<typeof EMPTY_EYE_VALUES> = {}): PrescriptionContent {
  const content = emptyContent("prescription") as PrescriptionContent;
  return { ...content, od: { ...EMPTY_EYE_VALUES, ...values } };
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
    assert.deepEqual(record.openDraft?.content.od, values);
    assert.equal(record.openDraft?.content.og.sphere, "");
    const again = must(await gateway.getRecord("prescription", record.id));
    assert.equal(again.openDraft?.content.od.axis, "0", "aucune normalisation 0° → 180°");
    assert.equal(again.openDraft?.content.od.prism, "0,00");
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
  test("brouillon sans aucune valeur optique → 422 incomplete (règle minimale de démonstration, D-04-17) → un seul œil suffit, OG laissé vide", async () => {
    const { gateway } = await setup();
    const draft = must(await gateway.createRecord("cli-fictif-3", rxContent()));
    const refused = await gateway.confirmDraft("prescription", draft.id, draft.version);
    assert.equal(refused.ok, false);
    if (!refused.ok) {
      assert.equal(refused.error.code, "incomplete");
      // Ni provenance exigée ni valeur imposée aux deux yeux (D-04-02.b et D-04-03.a/b ouvertes).
      assert.deepEqual(refused.error.fieldErrors, { correction: ["optical_values_missing"] });
    }
    // « 0 » est une valeur renseignée (absence ≠ 0) ; provenance laissée vide.
    const withZero: PrescriptionContent = { ...(draft.openDraft?.content as PrescriptionContent), od: { ...EMPTY_EYE_VALUES, sphere: "0" } };
    const saved = must(await gateway.saveDraft("prescription", draft.id, withZero, draft.version));
    const confirmed = must(await gateway.confirmDraft("prescription", saved.id, saved.version));
    assert.equal(confirmed.lastConfirmed?.revisionNo, 1);
    assert.equal(confirmed.openDraft, null);
    assert.equal(confirmed.lastConfirmed?.content.od.sphere, "0");
    assert.deepEqual(confirmed.lastConfirmed?.content.og, EMPTY_EYE_VALUES, "OG vide conservé vide, jamais assimilé à 0");
  });

  test("valeurs composées d'espaces : toujours absentes pour la règle de démonstration", async () => {
    const { gateway } = await setup();
    const draft = must(await gateway.createRecord("cli-fictif-3", rxContent({ base: "  ", acuity: " " })));
    const refused = await gateway.confirmDraft("prescription", draft.id, draft.version);
    assert.equal(refused.ok, false);
    if (!refused.ok) assert.equal(refused.error.code, "incomplete");
  });

  test("échec simulé de la confirmation seule : le brouillon enregistré reste intact, la tentative suivante confirme le même enregistrement", async () => {
    const faults = { latencyMs: 0, failedListReads: 0, failNextWrite: false, nextConfirmFault: null as "network" | "conflict" | null };
    const gateway = await createSeededGateway({ clients: createBenchClients(), session: () => ({ actor, permissions: ALL_OPTICAL_PERMISSIONS }), faults: () => faults });
    const draft = must(await gateway.createRecord("cli-fictif-3", rxContent({ sphere: "+1,00" })));
    for (const fault of ["network", "conflict"] as const) {
      faults.nextConfirmFault = fault;
      const refused = await gateway.confirmDraft("prescription", draft.id, draft.version);
      assert.equal(refused.ok, false);
      if (!refused.ok) assert.equal(refused.error.kind, fault);
      const unchanged = must(await gateway.getRecord("prescription", draft.id));
      assert.equal(unchanged.version, draft.version, "aucune écriture partielle");
      assert.equal(unchanged.openDraft?.content.od.sphere, "+1,00");
    }
    const confirmed = must(await gateway.confirmDraft("prescription", draft.id, draft.version));
    assert.equal(confirmed.id, draft.id);
    assert.equal(confirmed.lastConfirmed?.revisionNo, 1);
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

describe("double optique du banc — simplification D-04-16 (APPROVED)", () => {
  test("ni état d'œil ni pièce source : contenu = provenance + OD / OG + notes ; aucune opération documentaire", async () => {
    const { gateway } = await setup();
    const record = must(await gateway.getRecord("prescription", refOf(gateway, "RX-FICTIF-0001").id));
    const content = record.lastConfirmed?.content as PrescriptionContent;
    assert.deepEqual(Object.keys(content).sort(), ["kind", "notes", "od", "og", "provenance"]);
    assert.deepEqual(Object.keys(content.od), [...EYE_FIELDS], "un œil = ses seules valeurs, aucun champ « état »");
    assert.deepEqual(Object.keys(content.provenance).sort(), ["prescribedOn", "prescriberIdentifier", "prescriberName", "prescriberQualification", "receivedOn"]);
    assert.equal("documents" in record || "documentCount" in record, false, "aucune pièce servie");
    for (const method of ["attachDocument", "invalidateDocument", "fetchDocumentContent"]) assert.equal(method in gateway, false, `${method} absent`);
  });

  test("révisions conservées : comparaison des valeurs, motif de rectification, auteurs tracés", async () => {
    const { gateway } = await setup();
    const record = must(await gateway.getRecord("prescription", refOf(gateway, "RX-FICTIF-0001").id));
    const byNo = (no: number) => record.revisions.find((revision) => revision.revisionNo === no);
    const [one, two] = [byNo(1), byNo(2)];
    assert.ok(one && two);
    assert.equal(two.baseRevisionNo, 1);
    assert.equal(two.reasonCode, "additional_information");
    assert.ok(two.reasonComment.length > 0);
    for (const revision of [one, two]) {
      assert.equal(revision.startedBy.id, actor.id, "auteur de la saisie");
      assert.equal(revision.confirmedBy?.id, actor.id, "auteur de la confirmation");
      assert.ok(revision.confirmedAtUtc);
    }
    const different = compareRows(one.content, two.content, BENCH_MEASURERS).filter((row) => row.left !== row.right);
    assert.deepEqual(
      different.map((row) => [row.key, row.left, row.right]),
      [
        ["od.addition", "", "+2,00"],
        ["og.addition", "", "+2,00"],
      ],
    );
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

  test("droit d'écriture par TYPE ; aucun droit sur des pièces sources (D-04-16)", async () => {
    const { gateway, session } = await setup();
    session.current = { actor, permissions: ["clients.read", OPTICAL_PERMISSIONS.read, OPTICAL_PERMISSIONS.measurementsWrite] };
    const rx = await gateway.createRecord("cli-fictif-3", rxContent());
    assert.equal(!rx.ok && rx.error.status, 403);
    assert.equal((await gateway.createRecord("cli-fictif-3", emptyContent("measurement"))).ok, true);
    assert.deepEqual(Object.values(OPTICAL_PERMISSIONS).filter((permission) => permission.includes("document")), []);
  });
});
