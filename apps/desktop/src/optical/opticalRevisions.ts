// PROTOTYPE 04-A — libellés de révision et lignes de comparaison champ à champ (GAP-04-07). Module pur : valeurs
// restituées telles qu'enregistrées, aucune interprétation clinique.
import { formatIsoDate } from "../clients/clientForm.ts";
import { EYE_FIELDS, EYE_SIDES, type OpticalActor, type OpticalRevision, type RecordContent } from "./opticalContract.ts";
import { eyeStateLabel, FIELD_LABEL, SIDE_SHORT, UNSPECIFIED_LABEL } from "./opticalLabels.ts";

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
function rowsOf(content: RecordContent, measurers: readonly OpticalActor[], documentName: (id: string) => string): [string, string, string][] {
  const rows: [string, string, string][] = [];
  if (content.kind === "prescription") {
    const p = content.provenance;
    rows.push(
      ["provenance.prescriberName", "Prescripteur — nom", p.prescriberName],
      ["provenance.prescriberQualification", "Prescripteur — qualité", UNSPECIFIED_LABEL],
      ["provenance.prescriberIdentifier", "Prescripteur — identifiant", p.prescriberIdentifier],
      ["provenance.prescribedOn", "Date de prescription", formatIsoDate(p.prescribedOn)],
      ["provenance.receivedOn", "Date de réception", formatIsoDate(p.receivedOn)],
    );
  } else {
    const p = content.provenance;
    rows.push(
      ["provenance.measuredOn", "Date de la mesure", formatIsoDate(p.measuredOn)],
      ["provenance.measuredByUserId", "Collaborateur ayant mesuré", measurers.find((actor) => actor.id === p.measuredByUserId)?.displayName ?? ""],
      ["provenance.category", "Catégorie de mesure", UNSPECIFIED_LABEL],
    );
  }
  for (const side of EYE_SIDES) {
    rows.push([`${side}.state`, `${SIDE_SHORT[side]} — état`, eyeStateLabel(content.kind, content[side].state)]);
    for (const field of EYE_FIELDS) rows.push([`${side}.${field}`, `${SIDE_SHORT[side]} — ${FIELD_LABEL[field]}`, content[side].values[field]]);
  }
  rows.push(["notes", "Notes", content.notes]);
  if (content.kind === "prescription") rows.push(["linkedDocumentIds", "Pièces liées", content.linkedDocumentIds.map(documentName).join(", ")]);
  return rows;
}

export function compareRows(left: RecordContent, right: RecordContent, measurers: readonly OpticalActor[], documentName: (id: string) => string): CompareRow[] {
  const a = rowsOf(left, measurers, documentName);
  const b = new Map(rowsOf(right, measurers, documentName).map(([key, , value]) => [key, value]));
  return a.map(([key, label, value]) => ({ key, label, left: value, right: b.get(key) ?? "" }));
}
