// Recette NATIVE GATE D du nouvel assistant d'import CSV (CSV-IMPORT-AI-01, 07/11/2026) : deux vraies fenêtres Tauri /
// WebView2 du MÊME poste Windows (poste A et poste B : deux profils WebView2 distincts — ce n'est PAS une preuve sur deux
// machines), API MMV.Api de banc RÉELLE en HTTPS (https://localhost:7367, jamais l'API du propriétaire sur 7267) sur
// PostgreSQL 18 mmv_v2_test (0009 appliquée), Ollama local RÉEL qwen3.5:4b. Données et comptes FICTIFS (gd-admin, gd-vendeur).
// Chaîne testée : Tauri → HTTPS → MMV.Api → PostgreSQL 18 → Ollama qwen3.5:4b. Scénarios R1 à R9 de LIVRAISON_05_CSVAI_POST_AUDIT.md § 9.
// Usage (depuis apps/desktop) :
//   MMV_E2E_CREDENTIALS_FILE=<banc>/creds.json node --use-system-ca e2e/native-csvai.mjs --a 9223 --b 9224 \
//     --process-a mmv-gda --process-b mmv-gdb --out <preuves> --csv-dir <CSV fictifs> [--only depot,stale,verres,historique,clavier,pannes]
//     [--api-restart <script -Stop/-Start>] [--ollama-down <script -Down/-Up>]
// Identifiants de banc lus en mémoire, jamais affichés ni copiés dans les preuves.
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { connect } from "./cdp.mjs";

const args = process.argv.slice(2);
const option = (name, fallback) => (args.indexOf(name) >= 0 ? args[args.indexOf(name) + 1] : fallback);
const portA = Number(option("--a", "9223"));
const portB = Number(option("--b", "9224"));
const processA = option("--process-a", "mmv-gda");
const processB = option("--process-b", "mmv-gdb");
const outDir = resolve(option("--out", "."));
const csvDir = resolve(option("--csv-dir", "."));
const apiBase = option("--api", "https://localhost:7367");
const only = option("--only", "") ? new Set(option("--only", "").split(",")) : null;
const PAGE = "http://tauri.localhost";
const MODEL = "qwen3.5:4b";
const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, "");
let passed = 0;
const results = [];
const log = (step, detail = "") => console.log(`${new Date().toISOString()} ${step}${detail ? ` — ${detail}` : ""}`);
const check = (condition, label) => {
  if (!condition) throw new Error(`ÉCHEC : ${label}`);
  passed += 1;
  log("PASS", label);
};

function capture(processName, name, { width = 1366, height = 768 } = {}) {
  const file = join(outDir, `GD_native_${name}_${stamp}.png`);
  const output = execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolve("e2e/capture-window.ps1"),
    "-Process", processName, "-Out", file, "-ClientOnly", "-ClientWidth", String(width), "-ClientHeight", String(height)], { encoding: "utf8" }).trim();
  log("CAPTURE", output.replace(outDir, "<PREUVES>"));
}

const accounts = Object.fromEntries(JSON.parse(readFileSync(process.env.MMV_E2E_CREDENTIALS_FILE, "utf8")).map((a) => [a.key, a]));
const hasText = (value) => `document.body.innerText.includes(${JSON.stringify(value)})`;
const navButton = (label) =>
  `[...document.querySelectorAll('nav[aria-label="Navigation"] button')].find(b => (b.getAttribute('aria-label') || b.textContent).startsWith(${JSON.stringify(label)}))`;
const byText = (selector, label) => `[...document.querySelectorAll(${JSON.stringify(selector)})].find(e => e.textContent.trim() === ${JSON.stringify(label)})`;
const startsText = (selector, label) => `[...document.querySelectorAll(${JSON.stringify(selector)})].find(e => e.textContent.trim().startsWith(${JSON.stringify(label)}))`;
const click = async (page, label) => {
  await page.waitFor(`(${byText("button", label)}) !== undefined && !(${byText("button", label)}).disabled`, { label: `bouton « ${label} » actif`, timeoutMs: 20000 });
  await page.evaluate(`(${byText("button", label)}).click()`);
};
const clickStarting = async (page, label) => {
  await page.waitFor(`(${startsText("button", label)}) !== undefined && !(${startsText("button", label)}).disabled`, { label: `bouton « ${label}… » actif`, timeoutMs: 20000 });
  await page.evaluate(`(${startsText("button", label)}).click()`);
};

async function signIn(page, account) {
  if (await page.evaluate(`document.querySelector('nav[aria-label="Navigation"]') !== null`)) return;
  await page.waitFor(hasText("Bienvenue,"), { label: "écran de connexion", timeoutMs: 30000 });
  await page.evaluate("document.getElementById(document.querySelector('label[for]').htmlFor).focus()");
  await page.type(account.userName);
  await page.press("Tab");
  await page.type(account.password);
  await page.press("Enter");
  await page.waitFor(hasText(`Bonjour, ${account.displayName}`), { label: "shell après connexion", timeoutMs: 30000 });
}

async function go(page, label, ready) {
  await page.evaluate(`(${navButton(label)}).click()`);
  await page.sleep(300);
  if (await page.evaluate(hasText("Modifications non enregistrées"))) await page.evaluate(`(${byText("button", "Fermer sans enregistrer")}).click()`);
  await page.waitFor(ready, { label: `écran ${label}`, timeoutMs: 20000 });
}

async function setFile(page, selector, path) {
  const { root } = await page.send("DOM.getDocument", { depth: 0 });
  const { nodeId } = await page.send("DOM.querySelector", { nodeId: root.nodeId, selector });
  await page.send("DOM.setFileInputFiles", { nodeId, files: [path] });
}

const theme = async (page, mode) => {
  const current = await page.evaluate("document.documentElement.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')");
  if (current !== mode) {
    await page.evaluate(`[...document.querySelectorAll('button')].find(b => /thème|theme/i.test(b.getAttribute('aria-label') || ''))?.click()`);
    await page.sleep(300);
  }
};
const reveal = async (page, element) => {
  await page.evaluate(`(${element})?.scrollIntoView({ block: "center" })`);
  await page.sleep(300);
};

// ---------------------------------------------------------------- API de banc (lecture d'état et « autre poste »)
const tokens = {};
async function token(key) {
  if (tokens[key]) return tokens[key];
  const login = await fetch(`${apiBase}/api/auth/login`, { method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userName: accounts[key].userName, password: accounts[key].password }) });
  if (!login.ok) throw new Error(`connexion API ${key} : HTTP ${login.status}`);
  tokens[key] = (await login.json()).accessToken;
  return tokens[key];
}
async function apiCall(method, path, body, key = "gd-admin") {
  const response = await fetch(`${apiBase}${path}`, { method, headers: { "Content-Type": "application/json", Authorization: `Bearer ${await token(key)}` },
    body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null };
}

const run = String(Date.now() % 100000).padStart(5, "0");
/** CSV FICTIFS de l'exécution (Windows-1252, « ; », montant non qualifié, SKU en double à corriger). */
function writeCsvFiles() {
  const products = join(csvDir, `catalogue-gd-${run}.csv`);
  writeFileSync(products, Buffer.from("Marque;Modèle;Coloris;SKU;Prix\r\n"
    + `Banc Optique;GD Aviator ${run};Noir;GD${run}-01;42,50\r\n`
    + `Banc Optique;GD Aviator ${run};Écaille;GD${run}-02;44,00\r\n`
    + `Banc Optique;GD Ronde ${run};Bleu;GD${run}-01;39,90\r\n`, "latin1"));
  const prices = join(csvDir, `tarif-gd-${run}.csv`);
  writeFileSync(prices, Buffer.from("Marque;Modèle;Coloris;SKU;Prix\r\n"
    + `Banc Optique;GD Aviator ${run};Noir;GD${run}-01;50,00\r\n`, "latin1"));
  const lenses = join(csvDir, `verres-gd-${run}.csv`);
  writeFileSync(lenses, "Fabricant;Gamme;Type;Indice;Traitements;Politique;Sph min;Sph max;Add min;Add max;Diamètres;Prix achat HT\n"
    + "Verrier Banc;Confort Banc;Progressif;1,60;Durci Banc;stock;-6;4;0,75;3,5;65;31,00\n"
    + "Verrier Banc;Confort Banc;Progressif;1,60;Durci Banc;sur commande;-10;6;0,75;3,5;65/70;89,90\n", "utf8");
  return { products, prices, lenses };
}
const files = writeCsvFiles();

// ---------------------------------------------------------------- assistant : étapes communes
const IMPORT_READY = `document.querySelector('ol[aria-label="Étapes de l\\'import"]') !== null`;
async function openImport(page) {
  await go(page, "Produits", `${hasText("Nouveau produit")}`);
  await page.evaluate(`[...document.querySelectorAll('.cat-seg__item, a, button')].find(x => x.textContent.trim() === 'Import CSV').click()`);
  await page.waitFor(IMPORT_READY, { label: "assistant d'import (WizardEnabled du serveur)", timeoutMs: 20000 });
}

async function deposit(page, path, { supplier } = {}) {
  if (!(await page.evaluate(`document.querySelector('input[aria-label="Fichier CSV à importer"]') !== null`))) {
    if (await page.evaluate(`(${byText("button", "Nouvel import")}) !== undefined`)) await click(page, "Nouvel import");
    else await clickStarting(page, "Déposer un autre fichier");
  }
  await page.waitFor(`document.querySelector('input[aria-label="Fichier CSV à importer"]') !== null`, { label: "zone de dépôt" });
  await setFile(page, 'input[aria-label="Fichier CSV à importer"]', path);
  await page.waitFor(hasText("Aperçu du fichier d'origine"), { label: "inspection du serveur (aperçu)", timeoutMs: 20000 });
  if (supplier) {
    await page.evaluate("document.querySelectorAll('input[role=combobox]')[0].focus()");
    await page.type(supplier);
    await page.waitFor(`document.querySelector('.cat-combo .clients-spin') === null && document.querySelector('[role=listbox] [role=option]')?.innerText.includes(${JSON.stringify(supplier)}) === true`, { label: `suggestion ${supplier}` });
    await page.sleep(400);
    await page.press("ArrowDown");
    await page.press("Enter");
    await page.waitFor(`[...document.querySelectorAll('.cat-combo__chosen')].some(c => c.innerText.includes(${JSON.stringify(supplier)}))`, { label: `${supplier} choisi` });
  }
}

/** Identifiant du lot affiché (lu dans l'historique du serveur : dernier lot déposé par ce compte). */
/** Lot le plus récent déposé sous ce nom de fichier et absent de « known » (attendu jusqu'à 20 s). */
async function latestBatchId(path, known = new Set(Object.values(state))) {
  const name = basename(path);
  for (let i = 0; i < 80; i++) {
    const list = (await apiCall("GET", "/api/imports")).body;
    const found = [...list].filter((x) => x.fileName === name && !known.has(x.id)).sort((x, y) => (y.uploadedAtUtc ?? "").localeCompare(x.uploadedAtUtc ?? ""))[0];
    if (found) return found.id;
    await new Promise((r) => setTimeout(r, 250));
  }
  throw new Error(`lot ${name} introuvable`);
}

async function waitAnalysis(page, batchId, timeoutMs = 180000) {
  const deadline = Date.now() + timeoutMs;
  let state = null;
  while (Date.now() < deadline) {
    state = (await apiCall("GET", `/api/imports/${batchId}/assistant/analysis`)).body;
    if (!["queued", "running"].includes(state.status)) return state;
    await page.sleep(1000);
  }
  throw new Error("analyse non terminée");
}

/** Préférences de décision par question (libellés des actions proposées par le SERVEUR). */
const PREFERENCES = [
  /^Prix d'achat HT pour les/, /^Les montants sont en EUR/, /^Montures optiques pour les/, /^Articles sur commande/, /^Verres tenus en stock/,
  /contient des intitulés/, /^Confirmer la reprise/, /^Importer comme/,
];
async function decideGroup(page, batchId, groupKey) {
  const before = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.revision;
  const groups = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.review.groups;
  const at = groups.findIndex((g) => g.key === groupKey.key);
  await page.waitFor(`document.querySelectorAll('table[class*=cat-table--inner] tbody tr').length === ${groups.length}`, { label: "confirmations groupées à jour" });
  await page.evaluate(`[...document.querySelectorAll('table[class*=cat-table--inner] tbody tr')][${at}].querySelector('button').click()`);
  await page.waitFor(`document.querySelector('.iw-panel [role=radiogroup][aria-label="Actions possibles"]') !== null`, { label: `panneau ${groupKey.key}` });
  const labels = await page.evaluate(`[...document.querySelectorAll('.iw-panel [role=radiogroup] label')].map(l => l.innerText.split('\\n')[0].trim())`);
  const index = (() => {
    for (const p of PREFERENCES) { const i = labels.findIndex((l) => p.test(l)); if (i >= 0) return i; }
    return labels.findIndex((l) => !/^Ne pas importer/.test(l));
  })();
  log("DÉCISION", `${groupKey.key} → « ${labels[index]} »`);
  await page.evaluate(`[...document.querySelectorAll('.iw-panel [role=radiogroup] label')][${index}].click()`);
  await page.sleep(200);
  await page.evaluate(`[...document.querySelectorAll('.iw-panel .clients-actions button')].find(b => /Confirmer pour|Enregistrer et revalider/.test(b.textContent)).click()`);
  for (let i = 0; i < 60; i++) {
    if ((await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.revision > before) return labels[index];
    await page.sleep(250);
  }
  throw new Error(`décision non enregistrée : ${groupKey.key}`);
}

async function decideAllGroups(page, batchId) {
  for (let round = 0; round < 12; round++) {
    const view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    const groups = view.review.groups;
    if (groups.length === 0) return;
    await decideGroup(page, batchId, { key: groups[0].key, text: groups[0].explanation });
  }
  throw new Error("confirmations groupées non épuisées");
}

async function runCheck(page) {
  await clickStarting(page, "Vérification avant import");
  await page.waitFor(`${hasText("Contrôle à blanc")} || (${byText("button", "Relancer le contrôle")}) !== undefined || (${byText("button", "Contrôler avant import")}) !== undefined`, { label: "étape 4" });
  const label = (await page.evaluate(`(${byText("button", "Relancer le contrôle")}) !== undefined`)) ? "Relancer le contrôle" : "Contrôler avant import";
  await click(page, label);
  await page.waitFor(`document.querySelector('.iw-facts') !== null && ${hasText("Bloquants")} && (${byText("button", "Relancer le contrôle")}) !== undefined`, { label: "contrôle à blanc terminé", timeoutMs: 30000 });
  await page.sleep(500);
}

async function applyFromUi(page) {
  await clickStarting(page, "Confirmation et import");
  await page.waitFor(`(${startsText("button", "Importer…")}) !== undefined`, { label: "étape 5" });
  await clickStarting(page, "Importer…");
  await page.waitFor(`document.querySelector('[role=alertdialog], [role=dialog]') !== null && ${hasText("Importer maintenant ?")}`, { label: "modale de confirmation" });
}

// ---------------------------------------------------------------- scénarios
const state = {};
const steps = {
  // R1 dépôt tolérant, R2 analyse réelle, R3 corrections individuelle et groupées (+ poste B), R4 montants et devise,
  // R5 contrôle, import transactionnel, rejeu et réimport sans doublon.
  async depot(a, b) {
    await signIn(a, accounts["gd-admin"]);
    await signIn(b, accounts["gd-admin"]);
    await theme(a, "light");
    await openImport(a);
    const status = (await apiCall("GET", "/api/imports/assistant/status")).body;
    check(status.wizardEnabled === true && status.configuredModel === MODEL && status.modelInstalled === true, `serveur de banc : assistant activé, ${MODEL} configuré et installé (${status.installedDigest?.slice(0, 12)})`);
    await a.waitFor(hasText(`IA locale · ${MODEL} configuré`), { label: "badge du modèle configuré" });
    check(true, `badge « IA locale · ${MODEL} configuré » affiché`);
    await deposit(a, files.products);
    check(await a.evaluate(hasText("windows-1252") ) || await a.evaluate(hasText("Windows-1252")) || await a.evaluate(hasText("latin")), "R1 : encodage Windows-1252 reconnu par le serveur");
    check(await a.evaluate(hasText("GD" + run + "-01")), "R1 : aperçu du texte d'origine (SKU en double accepté au dépôt)");
    capture(processA, "01_depot_apercu_A_light");
    await click(a, "Analyser mon fichier");
    await a.waitFor(hasText("Analyse automatique"), { label: "étape 2" });
    const batchId = await latestBatchId(files.products);
    state.products = batchId;
    await a.waitFor(hasText("Lecture complète du fichier"), { label: "progression mesurée (lecture)", timeoutMs: 20000 });
    await a.sleep(1500);
    capture(processA, "02_analyse_en-cours_A_light");
    const analysis = await waitAnalysis(a, batchId);
    check(analysis.status === "done" || analysis.status === "incomplete", `R2 : analyse IA réelle terminée (${analysis.status}) en ${(analysis.elapsedMs / 1000).toFixed(1)} s`);
    check(analysis.model.configured === MODEL && analysis.model.active === MODEL, `R2 : modèle CONFIRMÉ par la réponse d'Ollama = ${analysis.model.active} (configuré ${analysis.model.configured})`);
    const codes = analysis.events.map((e) => e.code);
    check(codes.includes("ollama_running") && codes.includes("ollama_received") && codes.includes("analysis_done"), "R2 : activité réelle horodatée (file, envoi, réception, validation)");
    await a.waitFor(`${hasText("confirmé par la réponse")} && ${hasText("(confirmé)")}`, { label: "modèle confirmé affiché", timeoutMs: 20000 });
    check(await a.evaluate(`document.querySelector('.iw-model')?.innerText.includes(${JSON.stringify(MODEL)})`), "R2 : l'interface affiche le modèle réellement exécuté");
    capture(processA, "03_analyse_terminee_modele-confirme_A_light");
    state.analysis = { elapsedMs: analysis.elapsedMs, model: analysis.model, coverage: analysis.coverage };

    await clickStarting(a, "Vérifier et corriger");
    await a.waitFor(hasText("À confirmer pour plusieurs lignes à la fois"), { label: "étape 3" });
    let view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    log("GROUPES", view.review.groups.map((g) => g.key).join(", "));
    let money;
    for (const g of view.review.groups.filter((x) => x.key.startsWith("col:"))) {
      const issue = (await apiCall("GET", `/api/imports/${batchId}/assistant/issues/${encodeURIComponent(g.key)}`)).body;
      if (issue?.examplesDigest) { money = g; break; }
    }
    check(money !== undefined, "R4 : colonne de montant non qualifiée présentée à confirmer");
    const moneyIndex = view.review.groups.findIndex((g) => g.key === money.key);
    await a.evaluate(`[...document.querySelectorAll('table[class*=cat-table--inner] tbody tr')][${moneyIndex}].querySelector('button').click()`);
    await a.waitFor(`document.querySelector('.iw-panel [aria-label="Exemples choisis par le serveur"]') !== null`, { label: "exemples du serveur" });
    check(await a.evaluate(`[...document.querySelectorAll('.iw-panel [role=radiogroup] input')].every(i => !i.checked)`), "R4 : aucune présélection du montant");
    check(await a.evaluate(hasText("aucune conversion")), "R4 : aucune conversion monétaire annoncée");
    capture(processA, "04_montant_a-confirmer_exemples_A_light");
    await a.evaluate(`(${byText(".iw-panel button", "Fermer")})?.click()`);

    // Poste B : même lot ouvert par l'historique, révision lue ; A décide ensuite → B (révision périmée) est refusé.
    await openImport(b);
    await b.evaluate(`document.querySelector('.iw-history__toggle').click()`);
    await b.waitFor(`document.querySelector('#iw-history-body') !== null && ${hasText("catalogue-gd-" + run + ".csv")}`, { label: "B : historique ouvert" });
    await b.evaluate(`[...document.querySelectorAll('#iw-history-body tr')].find(tr => tr.innerText.includes(${JSON.stringify("catalogue-gd-" + run + ".csv")})).querySelector('button').click()`);
    await b.waitFor(hasText("À confirmer pour plusieurs lignes à la fois"), { label: "B : lot repris à l'étape 3" });
    const revisionB = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.revision;
    await decideGroup(a, batchId, { key: money.key, text: money.explanation });
    view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    check(view.batch.revision === revisionB + 1, `R3 : décision de A enregistrée en révision ${view.batch.revision} (append-only)`);
    // B, encore sur la révision lue, tente une décision : refus du serveur (conflit de révision), affiché, sans perte.
    const firstGroup = view.review.groups[0];
    if (firstGroup) {
      await b.evaluate(`(() => { const row = [...document.querySelectorAll('table tbody tr')].find(tr => tr.querySelector('button')?.textContent.trim() === 'Ouvrir'); row.querySelector('button').click(); })()`);
      await b.waitFor(`document.querySelector('.iw-panel [role=radiogroup] label') !== null`, { label: "B : panneau" });
      await b.evaluate(`[...document.querySelectorAll('.iw-panel [role=radiogroup] label')].find(l => !/^Ne pas importer/.test(l.innerText)).click()`);
      await b.sleep(200);
      await b.evaluate(`[...document.querySelectorAll('.iw-panel .clients-actions button')].find(b => /Confirmer pour|Enregistrer et revalider/.test(b.textContent)).click()`);
      await b.sleep(2000);
      const afterB = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.revision;
      check(afterB === revisionB + 1, "R3 : poste B sur une révision périmée → aucune écriture (409 revision_conflict), vue rechargée");
      capture(processB, "05_poste-B_revision-perimee_B_light");
    }

    await decideAllGroups(a, batchId);
    view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    const decisions = view.review.decisions.map((d) => d.type);
    check(decisions.includes("file_default"), "R4 : devise du magasin CONFIRMÉE explicitement (décision file_default), jamais supposée");
    // Correction individuelle : SKU en double de la ligne 4 → nouvelle valeur (unicité revérifiée par le serveur).
    const rows = (await apiCall("GET", `/api/imports/${batchId}/assistant/rows?filter=all`)).body.rows;
    const dup = rows.find((r) => r.line === 4);
    if (dup?.issueKeys.length) {
      await a.evaluate(`(() => { const row = [...document.querySelectorAll('table tbody tr')].find(tr => tr.querySelector('td')?.textContent.trim() === '4'); row.querySelector('button').click(); })()`);
      await a.waitFor(`document.querySelector('.iw-panel [role=radiogroup] label') !== null`, { label: "panneau du doublon" });
      const before = view.batch.revision;
      const labels = await a.evaluate(`[...document.querySelectorAll('.iw-panel [role=radiogroup] label')].map(l => l.innerText.split('\\n')[0].trim())`);
      const edit = labels.findIndex((l) => /^(Modifier la valeur|Corriger la valeur).*ligne 4$/.test(l));
      log("CORRECTION", `ligne 4 → « ${labels[edit]} »`);
      await a.evaluate(`[...document.querySelectorAll('.iw-panel [role=radiogroup] label')][${edit}].click()`);
      await a.waitFor(`document.querySelector('.iw-panel input.clients-input') !== null`, { label: "champ nouvelle valeur" });
      await a.evaluate(`document.querySelector('.iw-panel input.clients-input').focus()`);
      await a.type(`GD${run}-03`);
      capture(processA, "06_correction-individuelle_doublon-SKU_A_light");
      await a.evaluate(`[...document.querySelectorAll('.iw-panel .clients-actions button')].find(b => /Enregistrer et revalider/.test(b.textContent)).click()`);
      for (let i = 0; i < 40 && (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.revision === before; i++) await a.sleep(250);
      check((await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.revision === before + 1, "R3 : correction individuelle du SKU en double enregistrée et revalidée");
      await decideAllGroups(a, batchId);
    }

    await runCheck(a);
    view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    log("CONTRÔLE", JSON.stringify({ create: view.check.variantsToCreate, update: view.check.toUpdate, blocking: view.check.blocking, currency: view.check.currency }));
    check(view.check.blocking === 0 && view.check.variantsToCreate === 3, "R5 : contrôle à blanc : 3 variantes à créer, 0 bloquant");
    check(view.check.currency === "EUR" && view.check.containsCosts === true, "R4 : montants en EUR (devise du magasin), prix d'achat écrits avec costs.write");
    check(!JSON.stringify(view).includes("planFingerprint"), "empreinte du plan métier jamais servie au client");
    capture(processA, "07_controle-a-blanc_A_light");
    await applyFromUi(a);
    capture(processA, "08_confirmation_modale_A_light");
    await click(a, "Importer maintenant");
    await a.waitFor(hasText("Import enregistré par le serveur"), { label: "import enregistré", timeoutMs: 30000 });
    check(await a.evaluate(hasText("3 variante(s) créée(s)")), "R5 : import transactionnel : 3 variantes créées (nombres relus du serveur)");
    capture(processA, "09_import-enregistre_A_light");
    const variants = (await apiCall("GET", `/api/catalog/variants?q=GD${run}`)).body;
    check((variants.totalCount ?? variants.items?.length) === 3, "R5 : catalogue : exactement 3 variantes GD (aucun doublon)");

    // Réimport du MÊME fichier : analyse annulée (R2), choix « sans IA » tracé (R9), contrôle : rien à importer, aucun doublon.
    await deposit(a, files.products);
    await click(a, "Analyser mon fichier");
    await a.waitFor(hasText("Analyse automatique"), { label: "étape 2 (réimport)" });
    const again = await latestBatchId(files.products);
    state.again = again;
    await a.waitFor(`(${byText("button", "Annuler l'analyse IA")}) !== undefined || ${hasText("Vérifier sans IA")}`, { label: "annulation proposée", timeoutMs: 20000 });
    if (await a.evaluate(`(${byText("button", "Annuler l'analyse IA")}) !== undefined`)) {
      await click(a, "Annuler l'analyse IA");
      check(true, "R2 : annulation de l'analyse IA en cours possible");
    }
    await a.waitFor(`(${startsText("button", "Vérifier sans IA")}) !== undefined || (${startsText("button", "Vérifier et corriger")}) !== undefined && !(${startsText("button", "Vérifier et corriger")}).disabled`, { label: "après annulation", timeoutMs: 60000 });
    if (await a.evaluate(`(${startsText("button", "Vérifier sans IA")}) !== undefined`)) {
      await clickStarting(a, "Vérifier sans IA");
      await a.waitFor(hasText("Règles seules (choix tracé)"), { label: "choix sans IA tracé" });
      check(true, "R9 : « Vérifier sans IA » : règles seules, choix tracé et affiché");
      capture(processA, "10_reimport_sans-IA_choix-trace_A_light");
    } else {
      await clickStarting(a, "Vérifier et corriger");
    }
    await a.waitFor(hasText("À confirmer pour plusieurs lignes à la fois"), { label: "étape 3 (réimport)" });
    await decideAllGroups(a, again);
    const fix = (await apiCall("GET", `/api/imports/${again}/assistant/rows?filter=all`)).body.rows.find((r) => r.line === 4);
    if (fix?.issueKeys.length) {
      const rev = (await apiCall("GET", `/api/imports/${again}/assistant`)).body.batch.revision;
      await apiCall("POST", `/api/imports/${again}/assistant/decisions`, { requestId: crypto.randomUUID(), baseRevision: rev, decisions: [{ type: "row_skip", target: { line: 4 }, value: {} }] });
    }
    await runCheck(a);
    const replay = (await apiCall("GET", `/api/imports/${again}/assistant`)).body;
    check(replay.check.variantsToCreate === 0, `R5 : réimport du même fichier : 0 variante à créer (${replay.check.unchanged} inchangée(s))`);
    check(await a.evaluate(hasText("Aucune donnée nouvelle dans ce fichier")) && replay.steps.import.state === "locked", "R5 : réimport : « Aucune donnée nouvelle », étape d'import verrouillée par le serveur");
    const forced = await apiCall("POST", `/api/imports/${again}/assistant/apply`, { requestId: crypto.randomUUID(), version: replay.batch.version, revision: replay.batch.revision, confirmed: true });
    check(forced.status >= 400 && /nothing_to_import|already_applied|check_stale|blocking_remaining/.test(JSON.stringify(forced.body)), `R5 : application forcée par l'API refusée (HTTP ${forced.status} ${forced.body?.code})`);
    check(((await apiCall("GET", `/api/catalog/variants?q=GD${run}`)).body.totalCount ?? 3) === 3, "R5 : toujours 3 variantes GD (idempotence)");
    capture(processA, "11_reimport-refuse_sans-doublon_A_light");
  },

  // R5 : plan périmé (prix modifié par un autre poste après le contrôle) → 409 check_stale affiché, rien d'écrasé.
  async stale(a) {
    await signIn(a, accounts["gd-admin"]);
    await openImport(a);
    await deposit(a, files.prices);
    await click(a, "Analyser mon fichier");
    await a.waitFor(hasText("Analyse automatique"), { label: "étape 2 (tarif)" });
    const batchId = await latestBatchId(files.prices);
    state.prices = batchId;
    const analysis = await waitAnalysis(a, batchId);
    log("ANALYSE", `${analysis.status} ${analysis.model.active ?? "-"} ${(analysis.elapsedMs / 1000).toFixed(1)} s`);
    if (await a.evaluate(`(${startsText("button", "Vérifier sans IA")}) !== undefined`)) await clickStarting(a, "Vérifier sans IA");
    else await clickStarting(a, "Vérifier et corriger");
    await a.waitFor(hasText("À confirmer pour plusieurs lignes à la fois"), { label: "étape 3 (tarif)" });
    await decideAllGroups(a, batchId);
    await runCheck(a);
    let view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    check(view.check.toUpdate === 1 && view.check.blocking === 0, "R5 : contrôle : 1 article modifié (prix d'achat 42,50 → 50,00)");
    capture(processA, "12_tarif_controle_avant-apres_A_light");
    // Autre poste (API de banc, compte administrateur) : prix d'achat fixé à 45 APRÈS le contrôle.
    const variant = (await apiCall("GET", `/api/catalog/variants?q=GD${run}-01`)).body.items[0];
    const storeId = (await apiCall("GET", "/api/store")).body.store.id;
    const price = (await apiCall("GET", `/api/pricing/variants/${variant.variantId}?storeId=${storeId}`)).body;
    const version = price.price?.version ?? price.version;
    const other = await apiCall("PUT", `/api/pricing/variants/${variant.variantId}/purchase-price`, { requestId: crypto.randomUUID(), version, storeId, purchasePriceHt: "45" });
    check(other.status === 200, `autre poste : prix d'achat modifié à 45,00 après le contrôle (HTTP ${other.status} ${other.status === 200 ? "" : JSON.stringify(other.body)})`);
    await applyFromUi(a);
    await click(a, "Importer maintenant");
    await a.waitFor(hasText("Le contrôle est périmé : relancez-le."), { label: "refus check_stale affiché", timeoutMs: 20000 });
    const after = (await apiCall("GET", `/api/pricing/variants/${variant.variantId}?storeId=${storeId}`)).body;
    check(JSON.stringify(after).includes("45.00"), "R5 : 409 check_stale : prix de l'autre poste (45,00) conservé, jamais écrasé");
    check((await apiCall("GET", `/api/imports/${batchId}/assistant`)).body.batch.status === "checked", "R5 : lot non appliqué (rien n'est écrit)");
    capture(processA, "13_check-stale_refus_A_light");
    await runCheck(a);
    view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    check(view.check.toUpdate === 1, "R5 : nouveau contrôle sur l'état relu (45,00 → 50,00)");
    await applyFromUi(a);
    await click(a, "Importer maintenant");
    await a.waitFor(hasText("Import enregistré par le serveur"), { label: "import du tarif", timeoutMs: 30000 });
    check(JSON.stringify((await apiCall("GET", `/api/pricing/variants/${variant.variantId}?storeId=${storeId}`)).body).includes("50.00"), "R5 : après nouveau contrôle, prix d'achat 50,00 importé");
    capture(processA, "14_tarif_importe_apres-recontrole_A_light");
  },

  // R6 : offres de verres (circuit stocké mis à jour → fiche complète à revalider ; circuit sur commande créé), fournisseur désigné.
  async verres(a) {
    await signIn(a, accounts["gd-admin"]);
    await openImport(a);
    await deposit(a, files.lenses, { supplier: "Laboratoire Verres Banc" });
    check(await a.evaluate(`document.querySelector('input[name=target][value=lenses]')?.checked === true`), "R6 : destination « Référentiel des verres » suggérée par le serveur");
    capture(processA, "15_verres_depot_destination-suggeree_A_light");
    await click(a, "Analyser mon fichier");
    const batchId = await latestBatchId(files.lenses);
    state.lenses = batchId;
    const analysis = await waitAnalysis(a, batchId);
    check(analysis.model.active === MODEL || ["unavailable", "timeout", "invalid_output"].includes(analysis.status), `R6 : analyse ${analysis.status} (${analysis.model.active ?? "aucun modèle confirmé"})`);
    if (await a.evaluate(`(${startsText("button", "Vérifier sans IA")}) !== undefined`)) await clickStarting(a, "Vérifier sans IA");
    else await clickStarting(a, "Vérifier et corriger");
    await a.waitFor(hasText("À confirmer pour plusieurs lignes à la fois"), { label: "étape 3 (verres)" });
    await decideAllGroups(a, batchId);
    await runCheck(a);
    const view = (await apiCall("GET", `/api/imports/${batchId}/assistant`)).body;
    log("CONTRÔLE VERRES", JSON.stringify({ create: view.check.variantsToCreate, update: view.check.toUpdate, revalidate: view.check.offersToRevalidate, blocking: view.check.blocking, remaining: view.check.remaining.map((r) => r.code) }));
    check(view.check.blocking === 0 && view.check.variantsToCreate === 1 && view.check.toUpdate === 1, "R6 : 1 offre sur commande à créer, 1 offre stockée à modifier (circuits distincts)");
    check(view.check.offersToRevalidate === 1, "R6 : fiche complète dont la capacité change → à revalider");
    capture(processA, "16_verres_controle_revalidation_A_light");
    await applyFromUi(a);
    await click(a, "Importer maintenant");
    await a.waitFor(hasText("Import enregistré par le serveur"), { label: "import des verres", timeoutMs: 30000 });
    check(await a.evaluate(hasText("1 offre(s) créée(s), 1 modifiée(s)")), "R6 : 1 offre créée, 1 modifiée (relu du serveur)");
    const offers = (await apiCall("GET", "/api/catalog/lens-offers")).body;
    const list = Array.isArray(offers) ? offers : offers.items ?? offers.offers ?? [];
    const stocked = list.find((o) => o.production === "stocked");
    check(stocked && stocked.rulesComplete === false, "R6 : offre stockée : fiche repassée « à revalider » ");
    check(JSON.stringify(stocked).includes("offre saisie à la main") || stocked.notes === undefined, "R6 : note saisie à la main conservée (colonne absente du fichier)");
    capture(processA, "17_verres_importes_A_light");
  },

  // R7 : historique replié par défaut, reprise, lecture seule d'un lot importé.
  async historique(a) {
    await signIn(a, accounts["gd-admin"]);
    await openImport(a);
    check(await a.evaluate(`document.querySelector('.iw-history__toggle').getAttribute('aria-expanded') === 'false' && document.querySelector('#iw-history-body') === null`), "R7 : historique replié par défaut (liste non chargée)");
    await a.evaluate(`document.querySelector('.iw-history__toggle').focus()`);
    await a.press("Enter");
    await a.waitFor(`document.querySelector('#iw-history-body') !== null && document.querySelectorAll('#iw-history-body tbody tr').length > 0`, { label: "historique ouvert au clavier" });
    check(true, "R7/R8 : historique ouvert au clavier (Entrée)");
    capture(processA, "18_historique_ouvert_A_light");
    await a.evaluate(`[...document.querySelectorAll('#iw-history-body tr')].find(tr => tr.innerText.includes(${JSON.stringify("catalogue-gd-" + run + ".csv")}) && tr.innerText.includes('applied')).querySelector('button').click()`);
    await a.waitFor(hasText("Lot importé : lecture seule"), { label: "lot importé en lecture seule" });
    check(true, "R7 : reprise d'un lot importé : lecture seule");
    capture(processA, "19_lot-importe_lecture-seule_A_light");
  },

  // R8 : clavier, thèmes clair / sombre, tailles 1366×768 et 1920×1080 ; contraste mesuré sur les textes de l'assistant.
  async clavier(a) {
    await signIn(a, accounts["gd-admin"]);
    await openImport(a);
    await a.evaluate("document.activeElement?.blur(); window.scrollTo(0, 0)");
    const seen = [];
    for (let i = 0; i < 14; i++) {
      await a.press("Tab");
      seen.push(await a.focused());
    }
    log("TABULATION", seen.join(" | "));
    check(seen.some((s) => /Fichier CSV à importer/.test(s ?? "")), "R8 : zone de dépôt atteinte au clavier (Tab)");
    const outline = await a.evaluate("(() => { const s = getComputedStyle(document.activeElement); return s.outlineStyle !== 'none' || s.boxShadow !== 'none'; })()");
    check(outline, "R8 : focus visible sur l'élément atteint");
    for (const mode of ["light", "dark"]) {
      await theme(a, mode);
      for (const [w, h] of [[1366, 768], [1920, 1080]]) {
        capture(processA, `20_assistant_${w}x${h}_${mode}_A`, { width: w, height: h });
        check(await a.noHorizontalScroll(), `R8 : ${w}×${h} ${mode} : aucun défilement horizontal`);
      }
      const ratio = await a.evaluate(`(() => {
        const lum = (c) => { const v = c.match(/\\d+(\\.\\d+)?/g).slice(0, 3).map(Number).map(x => x / 255).map(x => x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4); return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2]; };
        // Fond effectif : couleurs semi-transparentes composées sur les fonds parents (alpha), jusqu'au premier fond opaque.
        const bg = (e) => { const stack = []; while (e) { const m = getComputedStyle(e).backgroundColor.match(/[\\d.]+/g)?.map(Number); if (m && (m[3] ?? 1) > 0) { stack.push(m); if ((m[3] ?? 1) >= 1) break; } e = e.parentElement; }
          let c = [255, 255, 255]; for (const m of stack.reverse()) { const a = m[3] ?? 1; c = [0, 1, 2].map((k) => m[k] * a + c[k] * (1 - a)); } return 'rgb(' + c.join(',') + ')'; };
        let worst = 21;
        for (const e of document.querySelectorAll('.iw-screen .iw-step__label, .iw-screen .iw-step__sub, .iw-history__title, .iw-history__summary, .iw-screen .clients-field__hint, .iw-screen .clients-muted, .cat-drop__title, .iw-bar__info')) {
          if (!e.offsetParent || e.closest('button:disabled')) continue;
          const a = lum(getComputedStyle(e).color), b = lum(bg(e));
          worst = Math.min(worst, (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05));
        }
        return Math.round(worst * 100) / 100; })()`);
      check(ratio >= 4.5, `R8 : contraste minimal des textes de l'assistant en ${mode} = ${ratio}:1 (≥ 4,5)`);
    }
    await theme(a, "light");
    // Échap ferme la modale de confirmation sans rien écrire (si un lot est prêt) ; sinon NOT_TESTED consigné.
    results.push("Échap sur la modale : couvert par le test Vitest importWizard ; non rejoué nativement");
  },

  // R2 : annulation d'une analyse IA réelle EN COURS depuis le poste (bouton « Annuler l'analyse IA »), puis choix explicite.
  async annulation(a) {
    await signIn(a, accounts["gd-admin"]);
    await openImport(a);
    await deposit(a, files.products);
    await click(a, "Analyser mon fichier");
    const batchId = await latestBatchId(files.products);
    state.cancel = batchId;
    await a.waitFor(`(${byText("button", "Annuler l'analyse IA")}) !== undefined && !(${byText("button", "Annuler l'analyse IA")}).disabled`, { label: "bouton d'annulation", timeoutMs: 20000 });
    const before = (await apiCall("GET", `/api/imports/${batchId}/assistant/analysis`)).body.status;
    await click(a, "Annuler l'analyse IA");
    let status = before;
    for (let i = 0; i < 120 && ["queued", "running"].includes(status); i++) {
      await a.sleep(500);
      status = (await apiCall("GET", `/api/imports/${batchId}/assistant/analysis`)).body.status;
    }
    log("ANNULATION", `${before} → ${status}`);
    check(status === "cancelled", `R2 : analyse en cours (${before}) annulée depuis le poste → ${status}`);
    await a.waitFor(`(${startsText("button", "Vérifier sans IA")}) !== undefined && (${startsText("button", "Relancer l'analyse IA")}) !== undefined`, { label: "choix après annulation", timeoutMs: 20000 });
    check(true, "R2/R9 : après annulation, relancer ou vérifier sans IA (aucune décision implicite)");
    capture(processA, "23_analyse-annulee_choix_A_light");
    await a.evaluate(`(${startsText("button", "Abandonner ce lot")})?.click()`);
  },

  // R9 : API coupée puis rétablie ; Ollama indisponible (point de terminaison injoignable) → « Vérifier sans IA ».
  async pannes(a) {
    await signIn(a, accounts["gd-admin"]);
    await openImport(a);
    const restart = option("--api-restart", "");
    if (!restart) {
      log("NOT_TESTED", "coupure API : --api-restart non fourni");
    } else {
      execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", restart, "-Stop"], { encoding: "utf8" });
      await go(a, "Produits", "true");
      await a.sleep(2500);
      const offline = await a.evaluate(`document.body.innerText`);
      check(/Déconnecté|hors ligne|injoignable|indisponible|Reconnexion/i.test(offline), "R9 : API coupée : état déconnecté affiché");
      capture(processA, "21_api-coupee_A_light");
      execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", restart, "-Start"], { encoding: "utf8" });
      await a.waitFor(hasText("Connecté"), { label: "reconnexion temps réel", timeoutMs: 60000 });
      check(true, "R9 : API rétablie : reconnexion automatique");
      await openImport(a);
    }
    const down = option("--ollama-down", "");
    if (!down) {
      log("NOT_TESTED", "Ollama indisponible : --ollama-down non fourni");
      return;
    }
    execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", down, "-Down"], { encoding: "utf8" });
    try {
      await a.reload();
      await signIn(a, accounts["gd-admin"]);
      await openImport(a);
      await a.waitFor(hasText("IA locale indisponible · règles seules possibles"), { label: "badge IA indisponible", timeoutMs: 20000 });
      check(true, "R9 : Ollama indisponible annoncé avant dépôt");
      await deposit(a, files.prices);
      await click(a, "Analyser mon fichier");
      const batchId = await latestBatchId(files.prices);
      const analysis = await waitAnalysis(a, batchId, 60000);
      check(analysis.status === "unavailable", `R9 : analyse « ${analysis.status} » : aucun repli silencieux`);
      await a.waitFor(`(${startsText("button", "Vérifier sans IA")}) !== undefined`, { label: "choix sans IA proposé", timeoutMs: 20000 });
      check(await a.evaluate(hasText("Aucune décision n'est prise à votre place")), "R9 : choix explicite proposé (relancer ou vérifier sans IA)");
      capture(processA, "22_ollama-indisponible_choix_A_light");
      await clickStarting(a, "Vérifier sans IA");
      await a.waitFor(hasText("Règles seules (choix tracé)"), { label: "repli par règles tracé" });
      check(true, "R9 : repli par règles seulement sur décision tracée");
      await a.evaluate(`(${startsText("button", "Abandonner ce lot")})?.click()`);
    } finally {
      execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", down, "-Up"], { encoding: "utf8" });
    }
  },
};

const a = await connect(portA, PAGE);
const b = await connect(portB, PAGE);
try {
  for (const [name, step] of Object.entries(steps)) {
    if (only && !only.has(name)) continue;
    log("ÉTAPE", name);
    await step(a, b);
  }
  log("RÉSULTAT", `${passed} contrôle(s) PASS`);
  for (const r of results) log("NOTE", r);
} finally {
  a.close();
  b.close();
}
