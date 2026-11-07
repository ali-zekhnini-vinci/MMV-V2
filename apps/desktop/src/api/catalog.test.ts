// O-05-14 (intégration finale Phase 05, Tauri natif sur MMV.Api réel) : l'aperçu de prix échouait (400 invalid_body)
// parce que le poste y ajoutait un `requestId` que PreviewRequest ne déclare pas (membres inconnus refusés par le
// serveur). Double node:http local reproduisant cette règle : l'aperçu part SANS requestId, les écritures AVEC.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";
import { afterEach, describe, expect, it } from "vitest";
import { type Authorized, previewCatalog, writeCatalog } from "./catalog.ts";

const PREVIEW_MEMBERS = new Set(["storeId", "purchasePriceHt", "coefficient"]);
let close: (() => Promise<void>) | null = null;

async function strictServer() {
  const bodies: Record<string, Record<string, unknown>> = {};
  const server = createServer((request, response) => {
    let text = "";
    request.on("data", (chunk) => (text += chunk));
    request.on("end", () => {
      const body = JSON.parse(text) as Record<string, unknown>;
      bodies[request.url ?? ""] = body;
      const unknown = request.url === "/api/pricing/preview" && Object.keys(body).some((key) => !PREVIEW_MEMBERS.has(key));
      response.writeHead(unknown ? 400 : 200, { "Content-Type": "application/json" });
      response.end(JSON.stringify(unknown ? { code: "invalid_body" } : { computedSalePriceHt: "103.13", id: "x" }));
    });
  });
  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  close = () => new Promise((resolve) => server.close(() => resolve()));
  const url = new URL(`http://127.0.0.1:${(server.address() as AddressInfo).port}`);
  const authorized: Authorized = (call) => call(url, "jeton-fictif");
  return { bodies, authorized };
}

afterEach(async () => {
  await close?.();
  close = null;
});

describe("passerelle Catalogue : aperçu sans requestId, écritures idempotentes", () => {
  it("l'aperçu de prix n'envoie que ses trois membres et aboutit", async () => {
    const { bodies, authorized } = await strictServer();
    const result = await previewCatalog<{ computedSalePriceHt: string }>(authorized, "/api/pricing/preview", { storeId: "s", purchasePriceHt: "41.25", coefficient: "2.5" });
    expect(result.ok).toBe(true);
    expect(Object.keys(bodies["/api/pricing/preview"]).sort()).toEqual(["coefficient", "purchasePriceHt", "storeId"]);
  });

  it("une écriture porte toujours un requestId (et l'aperçu par writeCatalog serait refusé)", async () => {
    const { bodies, authorized } = await strictServer();
    expect((await writeCatalog(authorized, "PUT", "/api/pricing/variants/v", { storeId: "s", coefficient: "2.5" })).ok).toBe(true);
    expect(typeof bodies["/api/pricing/variants/v"].requestId).toBe("string");
    expect((await writeCatalog(authorized, "POST", "/api/pricing/preview", { storeId: "s", purchasePriceHt: "1", coefficient: "2" })).ok).toBe(false);
  });
});
