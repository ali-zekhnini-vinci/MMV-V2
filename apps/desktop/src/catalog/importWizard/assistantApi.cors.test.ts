// GATE D (07/11/2026, recette native Tauri) : la politique CORS du poste (desktop-api) n'admet que GET, POST et PUT
// (jamais DELETE, décision de la phase 02). L'annulation d'une analyse en DELETE était refusée par WebView2 avant
// d'atteindre le serveur ; le client de l'assistant n'emploie donc que GET et POST (garde statique, complétée par
// tests/MMV.Api.Tests/DesktopCorsTests.cs côté serveur).
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "vitest";

const source = readFileSync(new URL("./assistantApi.ts", import.meta.url), "utf8");

test("assistant d'import : aucune requête DELETE (refusée par la politique CORS du poste)", () => {
  assert.doesNotMatch(source, /(call|write)\(\s*"DELETE"/);
  assert.doesNotMatch(source, /method: "GET" \| "POST" \| "DELETE"/);
});

test("annulation de l'analyse : POST /api/imports/{id}/assistant/analysis/cancel", () => {
  assert.match(source, /cancelAnalysis: \(batchId\) => call\("POST", `\/api\/imports\/\$\{batchId\}\/assistant\/analysis\/cancel`\)/);
});
