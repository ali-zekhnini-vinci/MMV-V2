// Corrections Phase 05 (COR-05-02, 04, 05, 06, 08) : fonctions pures du poste.
import { describe, expect, it } from "vitest";
import { matchesTopics } from "../realtime/realtimeContext.ts";
import { DISCONNECTED_AFTER_MS, retryDelay, STEADY_RETRY_MS } from "../realtime/realtimeClient.ts";
import { mergeDraft } from "./catalogData.ts";
import { suggestSupplierCode } from "./catalogFormat.ts";
import { CATALOG_REALTIME } from "./catalogRealtime.ts";
import { applyCorrections, decodeCsv, detectDelimiter, parseCsv, serializeCsv } from "./csvFile.ts";
import { lensStepOfField, rangeText, reasonText } from "./lensOffers.ts";
import { stepOfField } from "./productForm.ts";

describe("CSV fournisseur (COR-05-04)", () => {
  it("décode UTF-8 strict, sinon Windows-1252, et respecte les BOM", () => {
    expect(decodeCsv(new TextEncoder().encode("Modèle;Réf\n"))).toEqual({ text: "Modèle;Réf\n", encoding: "utf-8" });
    expect(decodeCsv(new Uint8Array([0xef, 0xbb, 0xbf, 0x41]))).toEqual({ text: "A", encoding: "utf-8" });
    expect(decodeCsv(new Uint8Array([0x4d, 0x6f, 0x64, 0xe8, 0x6c, 0x65]))).toEqual({ text: "Modèle", encoding: "windows-1252" });
    expect(decodeCsv(new Uint8Array([0xff, 0xfe, 0x41, 0x00])).encoding).toBe("utf-16le");
  });

  it("détecte le séparateur par cohérence, comme le serveur", () => {
    expect(detectDelimiter("a;b;c\n1;2;3\n")).toBe(";");
    expect(detectDelimiter("a\tb\n1\t2\n")).toBe("\t");
    expect(detectDelimiter('nom,prix\n"Dupont; Fils","12,50"\n')).toBe(",");
    expect(detectDelimiter("Ref;Désignation\n000123;Monture, acétate\n")).toBe(";");
    // UX-IA : séparateur inhabituel « | » (exports de caisse / ERP), virgules décimales conservées dans les valeurs.
    expect(detectDelimiter("Désignation|Réf. fourn.|Tarif net\nAlpha|IA-1|42,50\n")).toBe("|");
  });

  it("garde les zéros initiaux et réécrit sans perte", () => {
    const rows = parseCsv('Ref;Nom\n000123;"Étui; cuir"\n', ";");
    expect(rows).toEqual([["Ref", "Nom"], ["000123", "Étui; cuir"]]);
    expect(serializeCsv(rows, ";")).toBe('Ref;Nom\n000123;"Étui; cuir"\n');
  });

  it("applique des corrections par numéro de ligne du fichier (lignes vides comprises)", () => {
    const text = "Ref;PA HT\n000123;1,234\n\n000124;abc\n";
    expect(applyCorrections(text, { 4: { "PA HT": "12,40" } })).toBe("Ref;PA HT\n000123;1,234\n000124;12,40\n");
  });
});

describe("Assistant de création (COR-05-06)", () => {
  it("renvoie à la bonne étape une erreur serveur", () => {
    expect(stepOfField("name")).toBe(0);
    expect(stepOfField("variants[0].label")).toBe(1);
    expect(stepOfField("variants[1].suppliers[0].supplierReference")).toBe(2);
    expect(stepOfField("variants[0].price.coefficient")).toBe(3);
    expect(stepOfField("variants[2].stock.quantity")).toBe(4);
    // UX-IA : tarif obligatoire et seuil d'alerte par variante.
    expect(stepOfField("variants[1].price")).toBe(3);
    expect(stepOfField("variants[0].lowStockThreshold")).toBe(4);
  });
});

describe("Temps réel (COR-05-02)", () => {
  it("relit seulement les requêtes des sujets notifiés", () => {
    expect(matchesTopics(["catalog", "/api/stock/balances?q=x"], CATALOG_REALTIME, ["stock"])).toBe(true);
    expect(matchesTopics(["catalog", "/api/catalog/variants"], CATALOG_REALTIME, ["pricing"])).toBe(true);
    expect(matchesTopics(["catalog", "/api/store"], CATALOG_REALTIME, ["stock", "catalog"])).toBe(false);
    expect(matchesTopics(["clients", "/api/clients"], CATALOG_REALTIME, ["catalog"])).toBe(false);
    expect(matchesTopics(["catalog", "/api/imports"], CATALOG_REALTIME, ["stock"])).toBe(false);
  });

  it("reconnecte sans fin avec un délai borné", () => {
    expect([0, 1, 2, 3, 4].map(retryDelay)).toEqual([0, 1000, 2000, 5000, 10000]);
    expect(retryDelay(50)).toBe(STEADY_RETRY_MS);
    expect(DISCONNECTED_AFTER_MS).toBeGreaterThan(10000);
  });

  it("intègre une version distante sans écraser la saisie (fusion à trois voies)", () => {
    const before = { name: "Alpha", brand: "Banc", description: "" };
    const after = { name: "Alpha", brand: "Banc", description: "Nouvelle description" };
    const draft = { name: "Alpha 2", brand: "Banc", description: "" };
    expect(mergeDraft(draft, before, after)).toEqual({ name: "Alpha 2", brand: "Banc", description: "Nouvelle description" });
  });
});

describe("Fournisseurs (COR-05-08) et verres (COR-05-05)", () => {
  it("propose un code fournisseur lisible", () => {
    expect(suggestSupplierCode("Lunetterie Générale & Fils")).toBe("LUNETTERIEGE");
    expect(suggestSupplierCode("Optic 2000")).toBe("OPTIC2000");
  });

  it("affiche plages et motifs sans jargon", () => {
    expect(rangeText("-10.00", "5.00")).toBe("−10,00 à +5,00 D");
    expect(rangeText(null, "5")).toBe("—");
    expect(reasonText({ code: "sphere_out_of_range", severity: "error", values: { value: "-11.00", min: "-10.00", max: "+5.00" } }, () => "?"))
      .toBe("Sphère -11.00 hors des plages déclarées (-10.00 à +5.00)");
    expect(reasonText({ code: "rules_incomplete", severity: "warning" }, () => "?")).toContain("incomplète");
  });
});

describe("Catalogue des verres unifié (UX-IA)", () => {
  it("renvoie chaque erreur serveur à l'étape du parcours « Nouvelle gamme »", () => {
    expect(lensStepOfField("manufacturer.name")).toBe(0);
    expect(lensStepOfField("range.name")).toBe(0);
    expect(lensStepOfField("offers[0].lensType")).toBe(1);
    expect(lensStepOfField("offers[1].treatmentIds")).toBe(2);
    expect(lensStepOfField("offers")).toBe(3);
    expect(lensStepOfField("offers[1].sphereMin")).toBe(4);
    expect(lensStepOfField("offers[0].rules[2].range")).toBe(4);
    expect(lensStepOfField("offers[0].priceHt")).toBe(5);
  });
});
