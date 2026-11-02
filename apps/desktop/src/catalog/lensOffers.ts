// Types, libellés et conversions des offres de verres (COR-05-05), séparés des composants (Fast Refresh).

export interface LensRange {
  min?: string | null;
  max?: string | null;
}

export interface LensRuleConditions {
  treatmentIds?: string[] | null;
  indexIds?: string[] | null;
  material?: string | null;
  sphere?: { min: number; max: number } | null;
  cylinder?: { min: number; max: number } | null;
  addition?: { min: number; max: number } | null;
}

/** Règle telle que servie par l'API (nombres JSON) ; envoyée en chaînes décimales. */
export interface LensRule {
  kind: string;
  effect: string;
  manufacturing: string;
  conditions?: LensRuleConditions | null;
  range?: { min: number; max: number } | null;
  diameters?: number[] | null;
  maxSphereCylinderSum?: number | null;
  surchargeHt?: number | null;
  extraLeadDays?: number | null;
  note?: string | null;
}

export interface LensOffer {
  id: string;
  rangeId: string;
  supplierId: string;
  supplierName: string;
  lensType: string;
  material: string | null;
  indexId: string | null;
  production: string;
  leadTimeDays: number | null;
  priceHt?: string;
  priceCurrency?: string;
  diameters: number[];
  sphereMin: string | null;
  sphereMax: string | null;
  cylinderMin: string | null;
  cylinderMax: string | null;
  additionMin: string | null;
  additionMax: string | null;
  treatmentIds: string[];
  rules: LensRule[];
  rulesComplete: boolean;
  sourceReference: string | null;
  validFrom: string | null;
  validUntil: string | null;
  notes: string | null;
  revision: number;
  status: "active" | "archived";
  updatedAtUtc: string;
  version: number;
  costsVisible: boolean;
}

export interface LensOfferDetail {
  offer: LensOffer;
  revisions: { revision: number; changedAtUtc: string; changedBy: { displayName: string }; reason: string | null }[];
}

export interface LensCheck {
  status: "compatible" | "incompatible" | "to_verify";
  manufacturing: string;
  reasons: { code: string; severity: string; values?: Record<string, string> | null }[];
  extraLeadDays: number;
  disclaimer: string;
  surchargeHt?: string;
}

export const LENS_TYPE_LABELS: Record<string, string> = {
  single_vision: "Unifocal", progressive: "Progressif", degressive: "Dégressif", bifocal: "Bifocal", other: "Autre",
};

export const PRODUCTION_LABELS: Record<string, string> = { stocked: "Stocké chez l'opticien", on_order: "Commandé à la demande" };

export const RULE_KIND_LABELS: Record<string, string> = {
  sphere_range: "Plage de sphère", cylinder_range: "Plage de cylindre", addition_range: "Plage d'addition", diameters: "Diamètres",
  combination_limit: "Limite |SPH| + |CYL|", restriction: "Restriction",
};

export const CHECK_LABELS: Record<LensCheck["status"], { title: string; tone: "ok" | "err" | "warn" }> = {
  compatible: { title: "Compatible avec les règles fournisseur complètes enregistrées", tone: "ok" },
  incompatible: { title: "Incompatible selon une règle explicite", tone: "err" },
  to_verify: { title: "Compatibilité à vérifier (données insuffisantes)", tone: "warn" },
};

export function reasonText(reason: LensCheck["reasons"][number], treatmentName: (id: string) => string): string {
  const v = reason.values ?? {};
  switch (reason.code) {
    case "treatment_not_offered": return `Traitement non proposé par ce fournisseur : ${treatmentName(v.treatmentId ?? "")}`;
    case "index_mismatch": return "Indice différent de celui de l'offre";
    case "restriction": return `Restriction fournisseur${v.note ? ` : ${v.note}` : ""}`;
    case "sphere_missing": return "Sphère non renseignée";
    case "addition_missing": return "Addition non renseignée (verre à addition)";
    case "addition_not_applicable": return "Addition sans objet pour un verre unifocal";
    case "rules_incomplete": return "Fiche fournisseur déclarée incomplète : la fabricabilité complète n'est pas garantie";
    case "data_expired": return `Fiche technique échue le ${v.validUntil}`;
    case "diameter_no_data": return "Aucun diamètre déclaré";
    case "diameter_not_offered": return `Diamètre ${v.value} mm non proposé`;
    case "diameter_denied": return `Diamètre ${v.value} mm exclu`;
    case "combination_exceeded": return `|SPH| + |CYL| dépasse ${v.max} D`;
    default: {
      const [dimension, kind] = [reason.code.split("_")[0], reason.code.slice(reason.code.indexOf("_") + 1)];
      const label = dimension === "sphere" ? "Sphère" : dimension === "cylinder" ? "Cylindre" : "Addition";
      if (kind === "out_of_range") return `${label} ${v.value} hors des plages déclarées (${v.min} à ${v.max})`;
      if (kind === "denied") return `${label} ${v.value} exclue par une règle (${v.min} à ${v.max})`;
      if (kind === "no_data") return `${label} ${v.value} : aucune plage déclarée`;
      if (kind === "special_manufacturing") return `${label} ${v.value} : fabrication spéciale`;
      return reason.code;
    }
  }
}

/** Plage lisible (« −10,00 à +5,00 D »). */
export function rangeText(min: string | number | null | undefined, max: string | number | null | undefined, unit = "D"): string {
  if (min === null || min === undefined || max === null || max === undefined) return "—";
  const f = (x: string | number) => {
    const n = Number(x);
    return `${n > 0 ? "+" : n < 0 ? "−" : ""}${Math.abs(n).toFixed(2).replace(".", ",")}`;
  };
  return `${f(min)} à ${f(max)} ${unit}`;
}

/** Chaîne décimale envoyée au serveur (virgule admise, signe conservé) ou null. */
export const decimalOrNull = (value: string) => (value.trim() === "" ? null : value.trim().replace(",", "."));
