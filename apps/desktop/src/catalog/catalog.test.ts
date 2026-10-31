import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { query } from "../api/catalog.ts";
import { findNavItemByPath, viewAccess, visibleSections } from "../shell/navigation.ts";
import { catalogAccess, isCatalogModuleActive } from "./catalogAccess.ts";
import { decimalInput, ERROR_MESSAGES, FAMILY_SHORT_LABELS, formatDecimal, formatMoney, NO_BREAK_SPACE, proposeSku, THIN_SPACE } from "./catalogFormat.ts";
import { availabilityBadge, stockTone } from "./catalogStatus.ts";

describe("SKU interne généré par MMV (correctif Q05, G05-DESIGN)", () => {
  it("préfixe de famille + 6 symboles sans caractère ambigu, tirés par le générateur cryptographique", () => {
    expect(proposeSku("optical_frames", () => [0, 1, 2, 3, 4, 5])).toBe("MON-234567");
    expect(proposeSku("accessories", () => [30, 31, 255, 100, 7, 8])).toBe("ACC-Z2999A");
    expect(proposeSku("inconnue", () => [0, 0, 0, 0, 0, 0])).toBe("ART-222222");
    for (const code of Object.keys(FAMILY_SHORT_LABELS)) expect(proposeSku(code)).toMatch(/^[A-Z]{3}-[2-9A-HJKMNP-Z]{6}$/);
  });

  it("deux propositions successives diffèrent (unicité garantie in fine par le serveur)", () => {
    const seen = new Set(Array.from({ length: 200 }, () => proposeSku("sunglasses")));
    expect(seen.size).toBe(200);
  });
});

describe("vocabulaire visuel du Lot 6 appliqué aux états V2 (disponibilité calculée par le serveur)", () => {
  it("En stock / Stock faible / Rupture reprennent b-ok / b-warn / b-err", () => {
    expect(availabilityBadge("in_stock").tone).toBe("ok");
    expect(availabilityBadge("low_stock").tone).toBe("warn");
    expect(availabilityBadge("out_of_stock").tone).toBe("err");
    expect(availabilityBadge("archived").tone).toBe("arch");
    expect(stockTone("out_of_stock")).toBe("err");
    expect(stockTone("on_order")).toBe("muted");
  });

  it("Catalog.css : couleurs issues des jetons MMV, sauf les trois teintes de famille du Lot 6 déclarées à la racine", () => {
    const css = readFileSync(new URL("./Catalog.css", import.meta.url), "utf8").replace(/\/\*[\s\S]*?\*\//g, "");
    const literals = css.match(/#[0-9a-f]{3,8}\b|rgba?\(|hsla?\(|oklch\(/gi) ?? [];
    expect(literals.sort()).toEqual(["#667085", "#7a5cff", "#98a2b3"]);
    const root = css.slice(css.indexOf(":root"), css.indexOf("}", css.indexOf(":root")));
    for (const literal of literals) expect(root).toContain(literal);
  });
});

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
