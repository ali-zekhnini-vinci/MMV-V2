// Saisie produit partagée par l'assistant de création (COR-05-06) et les écrans de fiche : brouillon de variante,
// corps d'API, familles, garde de sortie. Séparé des composants (Fast Refresh).
import { useState } from "react";
import type { Family } from "../api/catalog.ts";
import { useLeaveGuard } from "../shell/navigationGuard.ts";
import { useCatalogQuery } from "./catalogData.ts";

/** Préfixe de SKU par famille (même table que le générateur serveur `SkuGenerator`). */
export const SKU_PREFIX: Record<string, string> = {
  optical_frames: "MON",
  sunglasses: "SOL",
  ophthalmic_lenses: "VER",
  contact_lenses_solutions: "LEN",
  accessories: "ACC",
  services: "PRE",
};

export const skuPrefix = (familyCode: string) => SKU_PREFIX[familyCode] ?? "ART";

export interface VariantDraft {
  sku: string;
  skuCustom: boolean;
  label: string;
  manufacturerReference: string;
  barcode: string;
  stockPolicy: string;
  lensIndexId: string;
  treatmentIds: string[];
  attributes: Record<string, string>;
}

/** Nouvelle variante : SKU laissé vide, généré par le serveur à l'enregistrement. */
export const emptyVariant = (policy: string): VariantDraft => ({
  sku: "", skuCustom: false, label: "", manufacturerReference: "", barcode: "", stockPolicy: policy, lensIndexId: "", treatmentIds: [], attributes: {},
});

export const orNull = (value: string) => (value.trim() === "" ? null : value.trim());

export function variantBody(draft: VariantDraft, includePolicy: boolean) {
  return {
    // SKU vide : généré par le serveur (COR-05-06) ; personnalisé : transmis tel quel, unicité contrôlée par le serveur.
    sku: draft.skuCustom && draft.sku.trim() !== "" ? draft.sku.trim() : null,
    label: draft.label,
    manufacturerReference: orNull(draft.manufacturerReference),
    barcode: orNull(draft.barcode),
    attributes: draft.attributes,
    lensIndexId: orNull(draft.lensIndexId),
    treatmentIds: draft.treatmentIds,
    ...(includePolicy ? { stockPolicy: draft.stockPolicy } : {}),
  };
}

export function useFamilies() {
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  return Array.isArray(families.data) ? families.data : [];
}

/** Garde de sortie (menu du shell) + confirmation de fermeture du Lot 6 (prodClose). */
export function useUnsavedGuard(dirty: boolean) {
  const [pending, setPending] = useState<(() => void) | null>(null);
  useLeaveGuard((proceed) => {
    if (!dirty) return false;
    setPending(() => proceed);
    return true;
  });
  return {
    pending,
    ask: (proceed: () => void) => (dirty ? setPending(() => proceed) : proceed()),
    cancel: () => setPending(null),
    confirm: () => { const go = pending; setPending(null); go?.(); },
  };
}


export const WIZARD_STEPS = ["Informations générales", "Variantes", "Fournisseurs", "Tarification", "Stock initial et validation"] as const;

/** Étape d'un chemin d'erreur serveur (`variants[1].price.coefficient` → Tarification). */
export function stepOfField(path: string): number {
  if (/^variants\[\d+\]\.price/.test(path)) return 3;
  if (/^variants\[\d+\]\.stock/.test(path)) return 4;
  if (/^variants\[\d+\]\.suppliers/.test(path)) return 2;
  if (/^variants(\[|$)/.test(path)) return 1;
  return 0;
}
