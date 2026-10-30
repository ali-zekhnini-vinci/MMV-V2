import { describe, expect, it } from "vitest";
import { query } from "../api/catalog.ts";
import { findNavItemByPath, viewAccess, visibleSections } from "../shell/navigation.ts";
import { catalogAccess, isCatalogModuleActive } from "./catalogAccess.ts";
import { decimalInput, ERROR_MESSAGES, formatDecimal, formatMoney, NO_BREAK_SPACE, THIN_SPACE } from "./catalogFormat.ts";

describe("formatage des montants (aucun calcul côté poste)", () => {
  it("groupe les milliers et remplace le point décimal, sans arrondi ni flottant", () => {
    expect(formatDecimal("1234.50")).toBe(`1${THIN_SPACE}234,50`);
    expect(formatDecimal("9999999999.99")).toBe(`9${THIN_SPACE}999${THIN_SPACE}999${THIN_SPACE}999,99`);
    expect(formatDecimal("0.0050")).toBe("0,0050");
    expect(formatDecimal("-12.30")).toBe("-12,30");
  });

  it("affiche la devise du magasin, EUR ou MAD, sans conversion", () => {
    expect(formatMoney("103.13", "EUR")).toBe(`103,13${NO_BREAK_SPACE}€`);
    expect(formatMoney("43.98", "MAD")).toBe(`43,98${NO_BREAK_SPACE}MAD`);
    expect(formatMoney(null, "EUR")).toBe("—");
  });

  it("transmet la saisie telle quelle au serveur (virgule → point, aucun arrondi)", () => {
    expect(decimalInput(" 41,25 ")).toBe("41.25");
    expect(decimalInput("2.12345")).toBe("2.12345");
    expect(decimalInput("")).toBeNull();
  });

  it("traduit les codes d'erreur bornés du serveur", () => {
    for (const code of ["stock_insufficient", "stock_changed", "price_preview_stale", "costs_forbidden", "currency_mismatch", "import_has_errors"]) {
      expect(ERROR_MESSAGES[code]).toBeTruthy();
    }
  });
});

describe("activation du module Catalogue & stock", () => {
  it("exige l'interrupteur ET un droit de lecture", () => {
    expect(isCatalogModuleActive(["catalog.read"], "enabled")).toBe(true);
    expect(isCatalogModuleActive(["stock.read"], "enabled")).toBe(true);
    expect(isCatalogModuleActive(["catalog.read"], undefined)).toBe(false);
    expect(isCatalogModuleActive(["system.admin"], "enabled")).toBe(false);
  });

  it("chaque droit est distinct, aucun n'en implique un autre", () => {
    const access = catalogAccess(["costs.read", "inventory.count"]);
    expect(access.costsRead).toBe(true);
    expect(access.costsWrite).toBe(false);
    expect(access.inventoryCount).toBe(true);
    expect(access.inventoryValidate).toBe(false);
  });

  it("remplace les entrées reportées « Produits » et « Inventaire » quand il est actif", () => {
    expect(viewAccess("products", [], false, false)).toBe("later");
    expect(viewAccess("products", [], false, true)).toBe("allowed");
    expect(findNavItemByPath("/produits/modele/x", false, true)?.id).toBe("products");
    expect(findNavItemByPath("/inventaire/inventaires/x", false, true)?.id).toBe("inventory");
    const labels = visibleSections([], false, true).flatMap((s) => s.items.map((i) => i.label));
    expect(labels).toContain("Stock & inventaires");
  });

  it("construit des paramètres de liste sans valeurs vides", () => {
    expect(query({ storeId: "s", q: "", page: 2, incomplete: null })).toBe("?storeId=s&page=2");
  });
});
