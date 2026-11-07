// Activation du module Catalogue & stock (Phase 05) : interrupteur de build `VITE_MMV_CATALOG_MODULE=enabled` ET droit de
// lecture (catalog.read ou stock.read). Garde d'AFFICHAGE seulement : MMV.Api refuse chaque route sans le droit exact.
import { createContext, useContext } from "react";

export const CATALOG_PERMISSIONS = {
  catalogRead: "catalog.read",
  catalogWrite: "catalog.write",
  catalogArchive: "catalog.archive",
  catalogConfigure: "catalog.configure",
  catalogImport: "catalog.import",
  pricingWrite: "pricing.write",
  pricingManual: "pricing.manual",
  costsRead: "costs.read",
  costsWrite: "costs.write",
  suppliersRead: "suppliers.read",
  suppliersWrite: "suppliers.write",
  stockRead: "stock.read",
  stockMovementsWrite: "stock.movements.write",
  stockAdjust: "stock.adjust",
  inventoryCount: "inventory.count",
  inventoryValidate: "inventory.validate",
  auditRead: "audit.read",
} as const;

export type CatalogAccess = Record<keyof typeof CATALOG_PERMISSIONS, boolean>;

export function catalogAccess(permissions: readonly string[]): CatalogAccess {
  return Object.fromEntries(Object.entries(CATALOG_PERMISSIONS).map(([key, permission]) => [key, permissions.includes(permission)])) as CatalogAccess;
}

export function isCatalogModuleActive(permissions: readonly string[], moduleSwitch: string | undefined = import.meta.env?.VITE_MMV_CATALOG_MODULE): boolean {
  const access = catalogAccess(permissions);
  return moduleSwitch === "enabled" && (access.catalogRead || access.stockRead);
}

export const CatalogAccessContext = createContext<CatalogAccess>(catalogAccess([]));

export const useCatalogAccess = (): CatalogAccess => useContext(CatalogAccessContext);
