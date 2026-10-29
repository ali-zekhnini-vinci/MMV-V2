// PROTOTYPE 04-A — libellés de révision et lignes de comparaison champ à champ (GAP-04-07). Module pur : valeurs
// restituées telles qu'enregistrées, aucune interprétation clinique. Ni « état de l'œil » ni pièces liées (D-04-16).
import { formatIsoDate } from "../clients/clientForm.ts";
import { EYE_FIELDS, EYE_SIDES, type OpticalActor, type OpticalRevision, type RecordContent } from "./opticalContract.ts";
import { FIELD_LABEL, SIDE_SHORT } from "./opticalLabels.ts";

export function revisionLabel(revision: OpticalRevision): string {
  if (revision.status === "confirmed") return `Révision ${revision.revisionNo}`;
  if (revision.status === "discarded") return `Rectification abandonnée (base : révision ${revision.baseRevisionNo})`;
  return revision.baseRevisionNo === null ? "Brouillon de première saisie (non confirmé)" : `Brouillon de rectification (non confirmé, base : révision ${revision.baseRevisionNo})`;
}

interface CompareRow {
  key: string;
  label: string;
  left: string;
  right: string;
}

/** Lignes comparables d'un contenu, valeurs restituées telles qu'enregistrées. */
function rowsOf(content: RecordContent, measurers: readonly OpticalActor[]): [string, string, string][] {
  const rows: [string, string, string][] = [];
  if (content.kind === "prescription") {
    const p = content.provenance;
    rows.push(
      ["provenance.prescriberName", "Prescripteur — nom", p.prescriberName],
      ["provenance.prescriberQualification", "Prescripteur — qualité", p.prescriberQualification],
      ["provenance.prescriberIdentifier", "Prescripteur — identifiant", p.prescriberIdentifier],
      ["provenance.prescribedOn", "Date de prescription", formatIsoDate(p.prescribedOn)],
      ["provenance.receivedOn", "Date de réception", formatIsoDate(p.receivedOn)],
    );
  } else {
    const p = content.provenance;
    rows.push(
      ["provenance.measuredOn", "Date de la mesure", formatIsoDate(p.measuredOn)],
      ["provenance.measuredByUserId", "Collaborateur ayant mesuré", measurers.find((actor) => actor.id === p.measuredByUserId)?.displayName ?? ""],
      ["provenance.category", "Catégorie de mesure", p.category],
    );
  }
  for (const side of EYE_SIDES) {
    for (const field of EYE_FIELDS) rows.push([`${side}.${field}`, `${SIDE_SHORT[side]} — ${FIELD_LABEL[field]}`, content[side][field]]);
  }
  rows.push(["notes", "Notes", content.notes]);
  return rows;
}

export function compareRows(left: RecordContent, right: RecordContent, measurers: readonly OpticalActor[]): CompareRow[] {
  const a = rowsOf(left, measurers);
  const b = new Map(rowsOf(right, measurers).map(([key, , value]) => [key, value]));
  return a.map(([key, label, value]) => ({ key, label, left: value, right: b.get(key) ?? "" }));
}
