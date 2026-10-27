// PROTOTYPE 04-A — libellés du dossier optique. Vocabulaire D-04-01 (APPROVED) : « Ordonnance médicale » (document
// reçu d'un prescripteur, transcrit dans MMV) et « Mesure magasin » (relevé réalisé au magasin) ; le mot
// « prescription » n'est jamais appliqué à une mesure magasin. Les listes de motifs reprennent les propositions
// D-04-04 (PROPOSED). Les listes « qualité du prescripteur » et « catégorie de mesure » sont BLOQUÉES (avis opticien) :
// seule la valeur « Non précisée » existe.
import type {
  DisplayStatus,
  EyeField,
  EyeSide,
  InvalidationReason,
  OpticalKind,
  RectificationReason,
} from "./opticalContract.ts";

export const KIND_LABEL: Record<OpticalKind, string> = { prescription: "Ordonnance médicale", measurement: "Mesure magasin" };
export const KIND_PLURAL: Record<OpticalKind, string> = { prescription: "Ordonnances médicales", measurement: "Mesures magasin" };
export const KIND_EXPLANATION: Record<OpticalKind, string> = {
  prescription: "Document reçu d'un prescripteur, transcrit dans MMV. La personne qui transcrit n'en devient pas le prescripteur.",
  measurement: "Relevé réalisé au magasin par un collaborateur. Ce n'est pas une ordonnance médicale et il ne le deviendra jamais.",
};
/** Segment de route du banc (`/optique/ordonnance/…`, `/optique/mesure/…`). */
export const KIND_SEGMENT: Record<OpticalKind, string> = { prescription: "ordonnance", measurement: "mesure" };

export function kindFromSegment(segment: string | undefined): OpticalKind | null {
  if (segment === KIND_SEGMENT.prescription) return "prescription";
  if (segment === KIND_SEGMENT.measurement) return "measurement";
  return null;
}

export const STATUS_LABEL: Record<DisplayStatus, string> = {
  draft: "Brouillon",
  confirmed: "Confirmé",
  rectification: "Rectification en cours",
  invalidated: "Invalidé",
};

export const SIDE_LABEL: Record<EyeSide, string> = { od: "Œil droit (OD)", og: "Œil gauche (OG)" };
export const SIDE_SHORT: Record<EyeSide, string> = { od: "OD", og: "OG" };

export const FIELD_LABEL: Record<EyeField, string> = {
  sphere: "Sphère",
  cylinder: "Cylindre",
  axis: "Axe",
  addition: "Addition",
  prism: "Prisme",
  base: "Base",
  acuity: "Acuité visuelle",
};

/** Unités affichées à côté du champ (aucune conversion). */
export const FIELD_UNIT: Record<EyeField, string> = {
  sphere: "dioptries",
  cylinder: "dioptries",
  axis: "degrés",
  addition: "dioptries",
  prism: "dioptries prismatiques (Δ)",
  base: "notation telle que sur la source",
  acuity: "notation telle que sur la source",
};
export const FIELD_UNIT_SHORT: Record<EyeField, string> = { sphere: "D", cylinder: "D", axis: "°", addition: "D", prism: "Δ", base: "", acuity: "" };

/** Libellés de base proposés par la maquette Lot 3 (aide à la saisie seulement ; liste non approuvée, D-04-06). */
export const BASE_SUGGESTIONS: readonly string[] = ["Nasal", "Temporal", "Supérieur", "Inférieur"];

export const RECTIFICATION_REASON_LABEL: Record<RectificationReason, string> = {
  transcription_error: "Erreur de transcription",
  additional_information: "Complément d'information",
  other: "Autre (préciser)",
};

export const INVALIDATION_REASON_LABEL: Record<InvalidationReason, string> = {
  entry_error: "Saisie erronée",
  duplicate: "Doublon",
  unacceptable_document: "Document non recevable",
  other: "Autre (préciser)",
};


/** Libellés des événements de gestion (D-04-12 ; aucune valeur optique). */
const EVENT_LABEL: Record<string, string> = {
  created: "Saisie créée (brouillon)",
  draft_updated: "Brouillon modifié",
  confirmed: "Transcription confirmée",
  rectification_started: "Rectification ouverte",
  rectification_discarded: "Rectification abandonnée",
  invalidated: "Enregistrement invalidé",
};

export function eventLabel(code: string): string {
  const action = code.split(".").slice(1).join(".");
  return EVENT_LABEL[action] ?? code;
}

export function formatDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString("fr-FR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

/** Mention commune des écrans fondés sur des décisions encore PROPOSED. */
export const PROPOSED_NOTE = "Prototype 04-A : comportement fondé sur des propositions (D-04-02 à D-04-14) non ratifiées.";
