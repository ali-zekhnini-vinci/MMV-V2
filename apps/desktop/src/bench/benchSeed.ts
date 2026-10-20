// BANC 04-A — jeu de démonstration EXPLICITEMENT FICTIF, construit en mémoire à l'ouverture du banc (aucune base,
// aucun fichier de données). Identités préfixées « Fictif- », domaines `exemple.test`, valeurs optiques de
// démonstration sans lien avec une personne réelle. Construit par les opérations publiques du double, pour que chaque
// état (brouillon, confirmé, rectification en cours, invalidé) soit cohérent. Aucune pièce source (D-04-16).
import type { ClientDetail } from "../api/clients.ts";
import {
  emptyContent,
  type EyeValues,
  EMPTY_EYE_VALUES,
  type MeasurementContent,
  type OpticalActor,
  type OpticalClientRef,
  type PrescriptionContent,
} from "../optical/opticalContract.ts";
import { OPTICAL_PERMISSIONS } from "../optical/opticalAccess.ts";
import { type BenchFaults, type BenchSession, InMemoryOpticalGateway } from "./inMemoryOpticalGateway.ts";

export const BENCH_MEASURERS: readonly OpticalActor[] = [
  { id: "usr-fictif-1", displayName: "Fictif-Opticien Un" },
  { id: "usr-fictif-2", displayName: "Fictif-Opticienne Deux" },
];

export const BENCH_OTHER_WORKSTATION: OpticalActor = { id: "usr-fictif-9", displayName: "Fictif-Poste B" };

const client = (
  index: number,
  firstName: string,
  lastName: string,
  options: { status?: "active" | "archived"; city?: string; email?: string | null } = {},
): ClientDetail => ({
  id: `cli-fictif-${index}`,
  reference: `CLI-FICTIF-${index.toString().padStart(3, "0")}`,
  firstName,
  lastName,
  phone: null,
  email: options.email === undefined ? `${firstName.toLowerCase()}@exemple.test` : options.email,
  city: options.city ?? "Ville-Démo",
  status: options.status ?? "active",
  createdAtUtc: "2026-09-01T08:00:00Z",
  version: 1,
  birthDate: null,
  addressLine: null,
  postalCode: null,
  notes: "Client FICTIF du banc de démonstration 04-A. Aucune donnée réelle.",
  updatedAtUtc: "2026-09-01T08:00:00Z",
  archivedAtUtc: options.status === "archived" ? "2026-10-01T08:00:00Z" : null,
});

export function createBenchClients(): ClientDetail[] {
  return [
    client(1, "Camille", "Fictif-Alpha"),
    client(2, "Sacha", "Fictif-Bravo"),
    client(3, "Noa", "Fictif-Charlie", { email: null }),
    client(4, "Alix", "Fictif-Delta", { status: "archived" }),
    client(5, "Lou", "Fictif-Echo"),
    client(6, "Eden", "Fictif-Foxtrot"),
  ];
}

export const toClientRef = (detail: ClientDetail): OpticalClientRef => ({
  id: detail.id,
  reference: detail.reference,
  displayName: `${detail.lastName.toLocaleUpperCase("fr")} ${detail.firstName}`,
  status: detail.status,
});

/** Valeurs d'un œil ; `eye({})` = œil laissé entièrement vide (non renseigné, jamais 0). */
const eye = (values: Partial<EyeValues>): EyeValues => ({ ...EMPTY_EYE_VALUES, ...values });

const rx = (
  prescriberName: string,
  prescribedOn: string,
  od: EyeValues,
  og: EyeValues,
  extra: Partial<PrescriptionContent["provenance"]> = {},
  notes = "Valeurs FICTIVES de démonstration.",
): PrescriptionContent => {
  const base = emptyContent("prescription") as PrescriptionContent;
  return { ...base, provenance: { ...base.provenance, prescriberName, prescribedOn, ...extra }, od, og, notes };
};

const mes = (measuredOn: string, measuredByUserId: string, od: EyeValues, og: EyeValues, notes = "Relevé FICTIF de démonstration."): MeasurementContent => {
  const base = emptyContent("measurement") as MeasurementContent;
  return { ...base, provenance: { ...base.provenance, measuredOn, measuredByUserId }, od, og, notes };
};

export const ALL_OPTICAL_PERMISSIONS = ["clients.read", ...Object.values(OPTICAL_PERMISSIONS)];

/**
 * Remplit le double par ses opérations publiques, sous une session de préparation disposant de tous les droits
 * simulés, avec une horloge de démonstration (octobre 2026). Toute anomalie lève une erreur : le banc ne démarre pas
 * sur un jeu incohérent.
 */
export async function seedBench(gateway: InMemoryOpticalGateway, setSession: (session: BenchSession) => void, setClock: (iso: string) => void) {
  const author = BENCH_MEASURERS[0];
  setSession({ actor: author, permissions: ALL_OPTICAL_PERMISSIONS });
  let minute = 0;
  const tick = (day: number) => {
    minute += 7;
    setClock(new Date(Date.UTC(2026, 9, day, 8, minute)).toISOString());
  };
  const must = async <T>(result: Promise<{ ok: true; data: T } | { ok: false; error: { code?: string } }>) => {
    const value = await result;
    if (!value.ok) throw new Error(`Jeu de démonstration incohérent : ${value.error.code ?? "erreur"}`);
    return value.data;
  };

  // 1. Alpha — ordonnance confirmée (révision 1), addition omise à la transcription → révision 2 (complément).
  tick(2);
  let record = await must(
    gateway.createRecord(
      "cli-fictif-1",
      rx("Dr Fictif-Prescripteur A", "2026-09-28", eye({ sphere: "+1,25", cylinder: "-0,50", axis: "90" }), eye({ sphere: "+1,00", cylinder: "-0,25", axis: "85" }), { receivedOn: "2026-10-02" }),
    ),
  );
  tick(2);
  record = await must(gateway.confirmDraft("prescription", record.id, record.version));
  tick(5);
  record = await must(gateway.startRectification("prescription", record.id, { expectedRevisionNo: 1, reasonCode: "additional_information", comment: "Addition figurant au verso de l'ordonnance (fictif).", version: record.version }));
  const draft = record.openDraft?.content as PrescriptionContent;
  tick(5);
  record = await must(gateway.saveDraft("prescription", record.id, { ...draft, od: eye({ sphere: "+1,25", cylinder: "-0,50", axis: "90", addition: "+2,00" }), og: eye({ sphere: "+1,00", cylinder: "-0,25", axis: "85", addition: "+2,00" }) }, record.version));
  tick(5);
  await must(gateway.confirmDraft("prescription", record.id, record.version));

  // 2. Alpha — mesure magasin confirmée (type distinct, aucun lien avec l'ordonnance, D-04-03.d option A).
  tick(6);
  record = await must(gateway.createRecord("cli-fictif-1", mes("2026-10-06", "usr-fictif-2", eye({ sphere: "+1,50", cylinder: "-0,50", axis: "95" }), eye({ sphere: "+1,25" }))));
  tick(6);
  await must(gateway.confirmDraft("measurement", record.id, record.version));

  // 3. Bravo — ordonnance confirmée, rectification en cours (GAP-04-13 : la révision 1 reste en vigueur).
  tick(7);
  record = await must(gateway.createRecord("cli-fictif-2", rx("Dr Fictif-Prescripteur B", "2026-10-01", eye({ sphere: "-2,25", cylinder: "-0,75", axis: "0" }), eye({ sphere: "-2,00" }))));
  tick(7);
  record = await must(gateway.confirmDraft("prescription", record.id, record.version));
  tick(9);
  record = await must(gateway.startRectification("prescription", record.id, { expectedRevisionNo: 1, reasonCode: "transcription_error", comment: "", version: record.version }));
  const rectification = record.openDraft?.content as PrescriptionContent;
  tick(9);
  await must(
    gateway.saveDraft(
      "prescription",
      record.id,
      { ...rectification, od: eye({ sphere: "-2,25", cylinder: "-0,50", axis: "0" }) },
      record.version,
    ),
  );

  // 4. Bravo — ordonnance saisie en double, invalidée (motif « Doublon »).
  tick(10);
  record = await must(gateway.createRecord("cli-fictif-2", rx("Dr Fictif-Prescripteur B", "2026-10-01", eye({ sphere: "-2,25" }), eye({ sphere: "-2,00" }))));
  tick(10);
  await must(gateway.invalidateRecord("prescription", record.id, { reasonCode: "duplicate", comment: "", version: record.version }));

  // 5. Bravo — mesure magasin en brouillon, OG laissé vide (non renseigné).
  tick(11);
  await must(gateway.createRecord("cli-fictif-2", mes("2026-10-11", "usr-fictif-1", eye({ sphere: "-2,00", cylinder: "-0,50", axis: "180" }), eye({}))));

  // 6. Echo — brouillon partiel d'ordonnance (OG vide, provenance incomplète) : refus 422 à la confirmation.
  tick(12);
  await must(gateway.createRecord("cli-fictif-5", rx("", "", eye({ sphere: "0,00", acuity: "8/10" }), eye({}))));

  // 7. Delta (client archivé) — ordonnance confirmée, consultable ; création et rectification refusées.
  tick(13);
  record = await must(gateway.createRecord("cli-fictif-4", rx("Dr Fictif-Prescripteur C", "2026-09-15", eye({ sphere: "+0,75", prism: "1", base: "BN" }), eye({ sphere: "+0,75" }))));
  tick(13);
  await must(gateway.confirmDraft("prescription", record.id, record.version));

  // 8–11. Foxtrot et Echo — enregistrements supplémentaires (pagination de la liste globale).
  const extra: [string, PrescriptionContent | MeasurementContent][] = [
    ["cli-fictif-6", rx("Dr Fictif-Prescripteur A", "2026-09-20", eye({ sphere: "-0,75" }), eye({ sphere: "-1,00" }))],
    ["cli-fictif-6", mes("2026-10-14", "usr-fictif-1", eye({ sphere: "-0,50" }), eye({ sphere: "-0,75" }))],
    ["cli-fictif-5", rx("Dr Fictif-Prescripteur D", "2026-08-30", eye({ sphere: "+2,50", addition: "+2,50" }), eye({ sphere: "+2,25", addition: "+2,50" }))],
    ["cli-fictif-6", rx("Dr Fictif-Prescripteur C", "2026-07-12", eye({ sphere: "-3,00", cylinder: "-1,25", axis: "15" }), eye({ sphere: "-3,25", cylinder: "-1,00", axis: "170" }))],
  ];
  for (const [clientId, content] of extra) {
    tick(14);
    const created = await must(gateway.createRecord(clientId, content));
    tick(14);
    await must(gateway.confirmDraft(content.kind, created.id, created.version));
  }
}

const SEED_FAULTS: BenchFaults = { latencyMs: 0, failedListReads: 0, failNextWrite: false };

/**
 * Double neuf rempli du jeu fictif. Pendant la préparation : session de préparation, horloge de démonstration, aucune
 * panne simulée, et le client archivé est traité comme encore actif (ses enregistrements datent d'avant l'archivage).
 */
export async function createSeededGateway(options: {
  clients: readonly ClientDetail[];
  session: () => BenchSession;
  faults?: () => BenchFaults;
}): Promise<InMemoryOpticalGateway> {
  let seeding = true;
  let seedSession: BenchSession | null = null;
  let clock: string | null = null;
  const gateway = new InMemoryOpticalGateway({
    clients: () => options.clients.map(toClientRef).map((ref) => (seeding ? { ...ref, status: "active" as const } : ref)),
    measurers: BENCH_MEASURERS,
    session: () => seedSession ?? options.session(),
    faults: () => (seeding ? SEED_FAULTS : (options.faults?.() ?? SEED_FAULTS)),
    now: () => clock ?? new Date().toISOString(),
  });
  await seedBench(
    gateway,
    (next) => {
      seedSession = next;
    },
    (iso) => {
      clock = iso;
    },
  );
  seeding = false;
  seedSession = null;
  clock = null;
  return gateway;
}
