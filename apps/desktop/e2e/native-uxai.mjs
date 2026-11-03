// Preuves NATIVES du lot UX-IA (03/11/2026) : DEUX vraies fenêtres Tauri / WebView2 (poste A administrateur, poste B
// vendeur) contre UNE API MMV.Api réelle de banc (cluster jetable, jamais l'API du propriétaire sur 7267), assistant IA
// local réel (Ollama du serveur de banc). Étapes : produit (assistant 5 étapes, tarif obligatoire, quantité + seuil,
// seuil modifié depuis la fiche, clavier), import (analyse automatique, précisions, récapitulatif, réimport refusé,
// tarifs à confirmer), verres (catalogue unifié, nouvelle gamme à deux fournisseurs, mode avancé, restriction).
// Usage (depuis apps/desktop) :
//   MMV_E2E_CREDENTIALS_FILE=<banc>/creds.json node --use-system-ca e2e/native-uxai.mjs --a 9223 --b 9224 \
//     --process-a mmv-uxaia --process-b mmv-uxaib --out <preuves> --csv-dir <CSV fictifs> [--only produit,import,verres]
// Comptes éphémères du banc : identifiants lus en mémoire, jamais affichés ni copiés dans les preuves.
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { connect } from "./cdp.mjs";

const args = process.argv.slice(2);
const option = (name, fallback) => (args.indexOf(name) >= 0 ? args[args.indexOf(name) + 1] : fallback);
const portA = Number(option("--a", "9223"));
const portB = Number(option("--b", "9224"));
const processA = option("--process-a", "mmv-uxaia");
const processB = option("--process-b", "mmv-uxaib");
const outDir = resolve(option("--out", "."));
const csvDir = resolve(option("--csv-dir", "."));
const apiBase = option("--api", "https://localhost:7367");
const only = option("--only", "") ? new Set(option("--only", "").split(",")) : null;
const PAGE = "http://tauri.localhost";
const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, "");
let passed = 0;
const log = (step, detail = "") => console.log(`${new Date().toISOString()} ${step}${detail ? ` — ${detail}` : ""}`);
const check = (condition, label) => {
  if (!condition) throw new Error(`ÉCHEC : ${label}`);
  passed += 1;
  log("PASS", label);
};

function capture(processName, name, { width = 1366, height = 768 } = {}) {
  const file = join(outDir, `UXAI_native_${name}_${stamp}.png`);
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
const click = (page, label) => page.evaluate(`(${byText("button", label)}).click()`);
const clickStarting = (page, label) => page.evaluate(`(${startsText("button", label)}).click()`);
const cardTitled = (title) => `[...document.querySelectorAll('.cat-card')].find(c => c.innerText.includes(${JSON.stringify(title)}))`;

async function signIn(page, account) {
  if (await page.evaluate(`document.querySelector('nav[aria-label="Navigation"]') !== null`)) return;
  await page.waitFor(hasText("Bienvenue,"), { label: "écran de connexion" });
  await page.evaluate("document.getElementById(document.querySelector('label[for]').htmlFor).focus()");
  await page.type(account.userName);
  await page.press("Tab");
  await page.type(account.password);
  await page.press("Enter");
  await page.waitFor(hasText(`Bonjour, ${account.displayName}`), { label: "shell après connexion" });
}

async function go(page, label, ready) {
  await page.evaluate(`(${navButton(label)}).click()`);
  await page.sleep(300);
  // Saisie en cours (reprise d'une exécution précédente) : la garde de sortie demande confirmation.
  if (await page.evaluate(hasText("Modifications non enregistrées"))) await page.evaluate(`(${byText("button", "Fermer sans enregistrer")}).click()`);
  await page.waitFor(ready, { label: `écran ${label}` });
}

/** Tape dans un champ (sélecteur CSS) : vidage puis vraies frappes CDP. */
async function fill(page, selector, value) {
  await page.evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); el.scrollIntoView({ block: "center" }); el.focus(); el.select?.(); })()`);
  await page.press("Backspace");
  await page.sleep(40);
  if (value !== "") await page.type(value);
}

async function setFile(page, selector, path) {
  const { root } = await page.send("DOM.getDocument", { depth: 0 });
  const { nodeId } = await page.send("DOM.querySelector", { nodeId: root.nodeId, selector });
  await page.send("DOM.setFileInputFiles", { nodeId, files: [path] });
}

/** Liste déroulante native choisie par sa VALEUR ou par le texte d'une option. */
const selectValue = (page, id, value) => page.evaluate(`(() => { const s = document.getElementById(${JSON.stringify(id)});
  s.value = ${JSON.stringify(value)}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
const selectText = (page, id, text) => page.evaluate(`(() => { const s = document.getElementById(${JSON.stringify(id)});
  s.value = [...s.options].find(o => o.textContent.includes(${JSON.stringify(text)})).value; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);

/** Combobox fournisseur n° index : frappe réelle, suggestion choisie au clavier. */
async function pickSupplier(page, index, typed, expected) {
  await page.evaluate(`document.querySelectorAll('input[role=combobox]')[${index}].focus()`);
  await page.type(typed);
  // Suggestions À JOUR (aucune recherche en cours) : la combobox refuse Entrée sur des suggestions périmées (O-05-24).
  await page.waitFor(`document.querySelector('.cat-combo .clients-spin') === null && document.querySelector('[role=listbox] [role=option]')?.innerText.includes(${JSON.stringify(expected)}) === true`, { label: `suggestion ${expected}` });
  await page.sleep(400);
  await page.press("ArrowDown");
  await page.press("Enter");
  await page.waitFor(`[...document.querySelectorAll('.cat-combo__chosen')].some(c => c.innerText.includes(${JSON.stringify(expected)}))`, { label: `${expected} choisi` });
}

const reveal = async (page, element) => {
  await page.evaluate(`(${element})?.scrollIntoView({ block: "center" })`);
  await page.sleep(300);
};
const theme = async (page, mode) => {
  const current = await page.evaluate("document.documentElement.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')");
  if (current !== mode) {
    await page.evaluate(`[...document.querySelectorAll('button')].find(b => /thème|theme/i.test(b.getAttribute('aria-label') || ''))?.click()`);
    await page.sleep(300);
  }
};
const toast = async (page) => page.sleep(4500); // notification refermée avant une capture
// Suffixe unique par exécution (le banc garde les données des exécutions précédentes).
const run = String(Date.now() % 100000).padStart(5, "0");
const productName = `UX Aviator Banc ${run}`;

/** Fournisseur FICTIF propre à l'exécution (aucun modèle d'import appris), créé par l'API de banc avec le compte admin. */
async function benchSupplier() {
  const login = await fetch(`${apiBase}/api/auth/login`, { method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userName: accounts["uxai-admin"].userName, password: accounts["uxai-admin"].password }) });
  const { accessToken } = await login.json();
  const name = `Fournisseur import ${run}`;
  const created = await fetch(`${apiBase}/api/suppliers`, { method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${accessToken}` },
    body: JSON.stringify({ requestId: crypto.randomUUID(), code: `UXI${run}`, name, countryCode: "BE" }) });
  if (!created.ok) throw new Error(`fournisseur de banc : HTTP ${created.status}`);
  return name;
}

/** CSV FICTIFS de l'exécution : fichier fournisseur UTF-8 « | » aux en-têtes inhabituels ; tarifs Windows-1252 « ; ». */
function writeCsvFiles() {
  const supplierFile = join(csvDir, `fournisseur-ux-${run}.csv`);
  writeFileSync(supplierFile, "Désignation article|Réf. fourn.|Coloris|EAN13|Fabricant monture|Tarif net HT\n"
    + `Monture Banc UX ${run} A|OD${run}-01|Noir||Banc Optique|42,50\n`
    + `Monture Banc UX ${run} A|OD${run}-02|Havane||Banc Optique|42,50\n`
    + `Monture Banc UX ${run} B|OD${run}-03|Bleu||Banc Optique|38,00\n`, "utf8");
  const pricesFile = join(csvDir, `tarifs-ux-${run}.csv`);
  writeFileSync(pricesFile, Buffer.from("Code produit;Libellé;Tarif;Prix public conseillé\r\n"
    + `OD${run}-01;Monture ${run} A Noir;42,50;119,00\r\nOD${run}-02;Monture ${run} A Havane;42,50;119,00\r\nOD${run}-03;Monture ${run} B Bleu;38,00;109,00\r\n`, "latin1"));
  return { supplierFile, pricesFile, supplierName: `fournisseur-ux-${run}.csv`, pricesName: `tarifs-ux-${run}.csv` };
}

const steps = {
  // Assistant de création produit : 5 étapes, fournisseurs multiples, tarif OBLIGATOIRE, quantité + seuil par magasin.
  async produit(a, b) {
    await signIn(a, accounts["uxai-admin"]);
    await signIn(b, accounts["uxai-vendeur"]);
    await theme(a, "light");
    await go(a, "Produits", `document.querySelector(".cat-steps") === null && ${hasText("Nouveau produit")}`);
    await click(a, "Nouveau produit");
    await a.waitFor("document.querySelector('.cat-steps--wizard') !== null", { label: "assistant ouvert" });
    // Brouillon de session d'une exécution précédente : repris à son étape, « Recommencer » l'efface.
    if (await a.evaluate(hasText("Brouillon repris"))) {
      check(true, "brouillon de session repris à son étape, « Recommencer » proposé");
      await click(a, "Recommencer");
    }
    await a.waitFor(hasText("Étape 1 sur 5"), { label: "assistant étape 1" });

    // Clavier : Tab avance de champ en champ, Entrée sur « Suivant » passe à l'étape suivante (contrôle d'étape compris).
    await fill(a, "#cat-name", productName);
    await a.press("Tab");
    check(await a.evaluate("document.activeElement?.id !== 'cat-name' && document.activeElement?.tagName !== 'BODY'"), "clavier : Tab quitte le champ « Nom du modèle » vers le champ suivant");
    await fill(a, "#cat-brand", "Ray-Ban");
    capture(processA, "01_produit_etape1_A_light");
    await a.evaluate(`(${startsText("button", "Suivant")}).focus()`);
    await a.press("Enter");
    await a.waitFor(hasText("Étape 2 sur 5"), { label: "étape 2 au clavier" });
    check(true, "clavier : Entrée sur « Suivant » ouvre l'étape 2 (Variantes)");

    await fill(a, "#cat-variants-0--label", "Noir 58");
    await click(a, "Ajouter une variante");
    await a.waitFor("document.querySelector('#cat-variants-1--label') !== null", { label: "variante 2" });
    await fill(a, "#cat-variants-1--label", "Or 58");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 3 sur 5"), { label: "étape 3" });

    await click(a, "Ajouter un fournisseur");
    await a.waitFor("document.querySelectorAll('input[role=combobox]').length === 1", { label: "ligne fournisseur 1" });
    await pickSupplier(a, 0, "Lunetterie Générale Banc", "Lunetterie Générale Banc UX");
    // Fournisseur choisi : sa combobox laisse place à une étiquette — la nouvelle ligne porte la seule combobox restante.
    await click(a, "Ajouter un fournisseur");
    await a.waitFor("document.querySelectorAll('input[role=combobox]').length === 1", { label: "ligne fournisseur 2" });
    await pickSupplier(a, 0, "Optique Diffusion Banc", "Optique Diffusion Banc UX");
    await fill(a, "#cat-variants-0--suppliers-0--supplierReference", "UXAV-N58");
    await fill(a, "#cat-variants-1--suppliers-0--supplierReference", "UXAV-O58");
    await fill(a, "#cat-variants-0--suppliers-0--supplierPriceHt", "40,00");
    await fill(a, "#cat-variants-1--suppliers-0--supplierPriceHt", "44,00");
    await fill(a, "#cat-variants-0--suppliers-1--supplierReference", "OD-2201");
    check(await a.evaluate(hasText("Principal")), "deux fournisseurs, le premier principal par défaut, prix fournisseur HT par variante");
    capture(processA, "02_produit_fournisseurs_A_light");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 4 sur 5"), { label: "étape 4" });

    check(!(await a.evaluate(hasText("Plus tard"))), "tarification : le mode « Plus tard » n'existe plus");
    check(await a.evaluate("document.querySelector('#cat-variants-0--price-purchasePriceHt').value === '40,00'"), "prix d'achat repris du fournisseur principal (même devise)");
    await fill(a, "#cat-variants-0--price-coefficient", "2,5");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Complétez cette étape pour continuer"), { label: "blocage tarif incomplet" });
    check(await a.evaluate(hasText("Calculez l'aperçu du prix de vente (Noir 58)")), "blocage précis : aperçu serveur requis pour « Noir 58 »");
    capture(processA, "03_produit_tarif-incomplet-bloque_A_light");
    await clickStarting(a, "Calculer");
    await a.waitFor(hasText("100,00"), { label: "aperçu serveur 100,00 €" });
    check(true, "aperçu calculé par le serveur : 40,00 × 2,5 = 100,00 € HT");
    await a.evaluate(`[...document.querySelectorAll('.cat-variant')][1].querySelectorAll('[role=radio]')[1].click()`);
    await a.waitFor("document.querySelector('#cat-variants-1--price-salePriceHt') !== null", { label: "prix manuel variante 2" });
    await fill(a, "#cat-variants-1--price-salePriceHt", "129,00");
    await fill(a, "#cat-variants-1--price-reason", "Finition or");
    capture(processA, "04_produit_tarifs-coefficient-et-manuel_A_light");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 5 sur 5"), { label: "étape 5" });

    await fill(a, "#cat-variants-0--stock-quantity", "20");
    await fill(a, "#cat-variants-1--stock-quantity", "15");
    await selectText(a, "cat-variants-1--stock-locationId", "Réserve");
    await fill(a, "#cat-variants-0--lowStockThreshold", "10");
    await fill(a, "#cat-variants-1--lowStockThreshold", "5");
    check(await a.evaluate(hasText("tous emplacements actifs confondus")), "seuil expliqué : stock disponible agrégé du magasin, tous emplacements");
    capture(processA, "05_produit_stock-seuil_A_light");
    await reveal(a, cardTitled("Récapitulatif avant création"));
    check(await a.evaluate(`(${cardTitled("Récapitulatif avant création")}).innerText.includes('100,00') && (${cardTitled("Récapitulatif avant création")}).innerText.includes('129,00')`),
      "récapitulatif : prix de vente HT des deux variantes");
    capture(processA, "06_produit_recapitulatif_A_light");
    await theme(a, "dark");
    capture(processA, "07_produit_recapitulatif_A_dark");
    capture(processA, "08_produit_recapitulatif_fenetre-reduite_A_dark", { width: 1024, height: 640 });
    await theme(a, "light");

    await click(a, "Créer le produit");
    await a.waitFor(`${hasText("Seuil d'alerte (magasin)")} && ${hasText(productName)}`, { label: "fiche produit créée" });
    check(true, "création atomique : fiche produit ouverte");
    check(await a.evaluate(`[...document.querySelectorAll('.cat-line')].some(l => l.innerText.includes("Seuil d'alerte") && l.innerText.includes('10'))`), "fiche : seuil 10 pour le magasin");
    await toast(a);
    await a.evaluate(`document.querySelector('button[aria-label="Modifier le seuil d\\'alerte"]').click()`);
    await a.waitFor(hasText("Seuil de stock faible"), { label: "dialogue seuil depuis la fiche" });
    await fill(a, "#cat-lowStockThreshold", "12");
    capture(processA, "09_produit_fiche_modifier-seuil_A_light");
    await click(a, "Enregistrer le seuil");
    await a.waitFor(`[...document.querySelectorAll('.cat-line')].some(l => l.innerText.includes("Seuil d'alerte") && l.innerText.includes('12'))`, { label: "seuil 12" });
    check(true, "seuil modifié depuis la fiche produit (12)");

    // Poste B (vendeur) : le produit est visible avec son prix de vente, jamais le prix d'achat.
    await go(b, "Produits", "document.querySelector('form[role=search] input') !== null");
    await fill(b, "form[role=search] input", productName);
    await b.waitFor(hasText(productName) + " && document.querySelectorAll('table.cat-table tbody tr').length >= 1", { label: "B : produit trouvé", timeoutMs: 20000 });
    check(!(await b.evaluate(hasText("40,00"))), "B (vendeur) : prix d'achat jamais affiché");
    check(!(await b.evaluate(byText("button", "Nouveau produit"))), "B (vendeur, sans catalog.write) : aucune création proposée");
    capture(processB, "10_produit_vu-du-vendeur_B_light");
  },

  // Import CSV : analyse automatique (règles + assistant IA local réel), précisions limitées, récapitulatif, import, réimport.
  async import(a) {
    const files = writeCsvFiles();
    const supplier = await benchSupplier();
    await signIn(a, accounts["uxai-admin"]);
    await theme(a, "light");
    await go(a, "Produits", `document.querySelector(".cat-steps") === null && ${hasText("Nouveau produit")}`);
    await a.evaluate(`[...document.querySelectorAll('.cat-seg__item, a, button')].find(x => x.textContent.trim() === 'Import CSV').click()`);
    await a.waitFor(hasText("Déposez votre fichier"), { label: "écran import" });
    await a.waitFor(hasText("Assistant IA local · llama3.2"), { label: "état de l'assistant IA local" });
    check(true, "assistant IA local du serveur actif (llama3.2)");
    await pickSupplier(a, 0, supplier, supplier);
    capture(processA, "11_import_depot_A_light");
    const started = Date.now();
    await setFile(a, "input[type=file]", files.supplierFile);
    await a.waitFor(hasText(`${files.supplierName} · encodage`), { label: "analyse du fichier", timeoutMs: 120000 });
    log("MESURE", `dépôt + analyse affichés en ${Date.now() - started} ms`);
    check(await a.evaluate(hasText("séparateur « | »")), "séparateur inhabituel « | » détecté");
    const analysis = await a.evaluate("[...document.querySelectorAll('.clients-field__hint')].map(p => p.innerText).find(t => t.includes('Assistant IA') || t.includes('assistant IA')) ?? ''");
    log("ANALYSE", analysis.replace(/\s+/g, " "));
    check(analysis.includes("consulté"), "assistant IA local consulté pour les colonnes non reconnues (durées affichées)");
    const questions = await a.evaluate("[...document.querySelectorAll('[role=group][aria-label=\"Quelques précisions\"] label')].map(l => l.innerText)");
    log("QUESTIONS", JSON.stringify(questions));
    check(questions.length > 0 && questions.length <= 4, `seules les précisions nécessaires sont demandées (${questions.length})`);
    capture(processA, "12_import_analyse_precisions_A_light");
    // Valeurs choisies par l'utilisateur (jamais déduites) : famille et politique quand le fichier ne les porte pas.
    if (await a.evaluate("document.getElementById('cat-opt-family') !== null")) await selectValue(a, "cat-opt-family", "optical_frames");
    if (await a.evaluate("document.getElementById('cat-opt-policy') !== null")) await selectValue(a, "cat-opt-policy", "stocked");
    // Toute autre question restée vide : colonne ignorée (choix prudent, jamais une valeur inventée).
    await a.evaluate(`[...document.querySelectorAll('[role=group][aria-label="Quelques précisions"] select')].filter(s => s.value === '').forEach(s => {
      const ignore = [...s.options].find(o => o.value === '@ignore'); if (ignore) { s.value = '@ignore'; s.dispatchEvent(new Event('change', { bubbles: true })); } })`);
    await click(a, "Valider et contrôler les lignes");
    await a.waitFor(`${hasText("3. Récapitulatif avant import")} && ${hasText("à créer")}`, { label: "récapitulatif", timeoutMs: 30000 });
    check(await a.evaluate(hasText("0 en erreur")), "contrôle à blanc : 0 ligne en erreur");
    check(await a.evaluate(`!!(${startsText("button", "Importer 3 ligne")})`), "bouton unique « Importer 3 lignes »");
    capture(processA, "13_import_recapitulatif_A_light");
    capture(processA, "14_import_recapitulatif_fenetre-reduite_A_light", { width: 1024, height: 640 });
    await clickStarting(a, "Importer 3 ligne");
    await a.waitFor(hasText("Import terminé"), { label: "import terminé", timeoutMs: 30000 });
    check(await a.evaluate(hasText(`Correspondance mémorisée pour ${supplier}`)), "correspondance apprise pour le fournisseur");
    await toast(a);
    capture(processA, "15_import_termine_correspondance-apprise_A_light");

    await setFile(a, "input[type=file]", files.supplierFile);
    await a.waitFor(hasText("Ce fichier a déjà été importé"), { label: "réimport annoncé", timeoutMs: 60000 });
    check(await a.evaluate(hasText("correspondance apprise du fournisseur reprise")), "réimport : correspondance apprise reprise");
    check(await a.evaluate(hasText("assistant IA non sollicité")), "réimport : aucune inférence (règles + correspondance apprise)");
    await a.waitFor(hasText("3. Récapitulatif avant import") + " && document.querySelector('.cat-counters') !== null", { label: "contrôle automatique", timeoutMs: 30000 });
    check(await a.evaluate(hasText("3 sans changement")), "réimport : contrôle automatique, 3 lignes sans changement");
    check(await a.evaluate(`(${startsText("button", "Importer")}).disabled`), "réimport : rien à importer, bouton inactif (aucun doublon possible)");
    capture(processA, "16_import_reimport-refuse_A_light");
    await click(a, "Abandonner");

    // Tarifs du même fournisseur (Windows-1252, « ; ») : les montants proposés par l'assistant restent À CONFIRMER.
    await a.evaluate("document.querySelector('input[name=importKind][value=prices]').click()");
    await setFile(a, "input[type=file]", files.pricesFile);
    await a.waitFor(hasText(`${files.pricesName} · encodage`), { label: "analyse des tarifs", timeoutMs: 120000 });
    check(await a.evaluate(hasText("encodage Windows-1252")), "tarifs : encodage Windows-1252 détecté sur le poste");
    const priceQuestions = await a.evaluate("[...document.querySelectorAll('[role=group][aria-label=\"Quelques précisions\"] label')].map(l => l.innerText)");
    log("QUESTIONS", JSON.stringify(priceQuestions));
    check(priceQuestions.some((q) => q.includes("Que contient la colonne")), "montant proposé par l'assistant : confirmation demandée, jamais appliqué seul");
    await reveal(a, "document.querySelector('[role=group][aria-label=\"Quelques précisions\"]')");
    capture(processA, "17_import_tarifs_montants-a-confirmer_A_light");
  },

  // Catalogue des verres unifié : nouvelle gamme + deux fournisseurs aux plages différentes, mode avancé, restriction.
  async verres(a, b) {
    await signIn(a, accounts["uxai-admin"]);
    await signIn(b, accounts["uxai-vendeur"]);
    await theme(a, "light");
    const toLenses = async (page) => {
      await go(page, "Produits", hasText("Catalogue"));
      await page.evaluate(`[...document.querySelectorAll('.cat-seg__item, a, button')].find(x => x.textContent.trim() === 'Catalogue des verres').click()`);
      await page.waitFor(hasText("Vos gammes de verres et les fournisseurs qui les fabriquent"), { label: "catalogue des verres" });
    };
    await toLenses(a);
    check(!(await a.evaluate(hasText("Référentiel de base"))), "une seule entrée « Catalogue des verres » (plus d'espace « Référentiel de base » séparé)");
    await click(a, "Nouvelle gamme");
    await a.waitFor(hasText("Étape 1 sur 6"), { label: "parcours nouvelle gamme" });
    await selectText(a, "cat-manufacturerId", "Verrier Fictif UX");
    await fill(a, "#cat-range-name", `UX Bleu Confort ${run}`);
    capture(processA, "18_verres_etape1_gamme_A_light");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 2 sur 6"), { label: "étape type" });
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 3 sur 6"), { label: "étape indice" });
    await selectText(a, "cat-indexId", "1,6");
    await fill(a, "#cat-material", "Polycarbonate");
    await a.evaluate(`[...document.querySelectorAll('label.cat-check')].find(l => l.textContent.includes('Anti-lumière bleue UX')).querySelector('input').click()`);
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 4 sur 6"), { label: "étape fournisseurs" });
    await pickSupplier(a, 0, "Verres Nord Banc", "Laboratoire Verres Nord Banc UX");
    await click(a, "Ajouter un fournisseur");
    await a.waitFor("document.querySelectorAll('input[role=combobox]').length === 1", { label: "second fournisseur" });
    await pickSupplier(a, 0, "Verres Sud Banc", "Laboratoire Verres Sud Banc UX");
    capture(processA, "19_verres_fournisseurs_A_light");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 5 sur 6"), { label: "étape plages" });
    check(await a.evaluate(hasText("Mode simple")), "mode simple par défaut");
    await fill(a, "#cat-offers-0--sphereMin", "-10");
    await fill(a, "#cat-offers-0--sphereMax", "5");
    await fill(a, "#cat-offers-1--sphereMin", "-6");
    await fill(a, "#cat-offers-1--sphereMax", "4");
    capture(processA, "20_verres_plages_mode-simple_A_light");
    await a.evaluate(`[...document.querySelectorAll('label')].find(l => l.textContent.trim().startsWith('Mode avancé')).querySelector('input').click()`);
    await a.waitFor("document.querySelector('#cat-offers-1--cylinderMin') !== null", { label: "mode avancé" });
    await fill(a, "#cat-offers-1--cylinderMin", "-4");
    await fill(a, "#cat-offers-1--cylinderMax", "0");
    await a.evaluate(`[...[...document.querySelectorAll('.cat-variant')][1].querySelectorAll('label')].find(l => l.textContent.includes('Fiche fournisseur complète'))?.querySelector('input').click()`);
    await a.evaluate(`[...[...document.querySelectorAll('.cat-variant')][1].querySelectorAll('button')].find(x => x.textContent.includes('Ajouter une règle')).click()`);
    await a.waitFor("document.getElementById('cat-offers-1--rules-0--kind') !== null", { label: "règle avancée" });
    await selectValue(a, "cat-offers-1--rules-0--kind", "restriction");
    await selectValue(a, "cat-offers-1--rules-0--effect", "deny");
    await fill(a, "#cat-offers-1--rules-0--conditions-cylinderMin", "-4");
    await fill(a, "#cat-offers-1--rules-0--conditions-cylinderMax", "-2,25");
    await a.evaluate(`[...[...document.querySelectorAll('.cat-variant')][1].querySelectorAll('label.cat-check')].find(l => l.textContent.includes('Anti-lumière bleue UX')).querySelector('input').click()`);
    await fill(a, "#cat-offers-1--rules-0--note", "Pas d'anti-lumière bleue au-delà de −2,00 de cylindre");
    await reveal(a, "document.getElementById('cat-offers-1--rules-0--kind')");
    capture(processA, "21_verres_plages_mode-avance_restriction_A_light");
    await clickStarting(a, "Suivant");
    await a.waitFor(hasText("Étape 6 sur 6"), { label: "étape prix" });
    await fill(a, "#cat-offers-0--priceHt", "42,50");
    await fill(a, "#cat-offers-1--priceHt", "39,00");
    await fill(a, "#cat-offers-0--leadTimeDays", "7");
    await fill(a, "#cat-offers-1--leadTimeDays", "10");
    await reveal(a, cardTitled("Récapitulatif avant création"));
    capture(processA, "22_verres_recapitulatif_A_light");
    await theme(a, "dark");
    capture(processA, "23_verres_recapitulatif_A_dark");
    await theme(a, "light");
    await click(a, "Créer la gamme");
    const rangeCard = cardTitled(`Verrier Fictif UX — UX Bleu Confort ${run}`);
    await a.waitFor(`(${rangeCard})?.innerText.includes('2 fournisseurs') === true`, { label: "gamme créée avec deux fournisseurs" });
    check(true, "gamme et deux offres fournisseur créées en une opération");
    await toast(a);
    await reveal(a, rangeCard);
    capture(processA, "24_verres_catalogue-unifie_A_light");

    // Restriction conditionnelle du fournisseur Sud : anti-lumière bleue refusée au-delà de −2,00 de cylindre.
    await a.evaluate(`(${rangeCard}).querySelectorAll('button[aria-label^="Afficher le détail"]')[1].click()`);
    await a.waitFor("document.querySelector('.cat-tester') !== null", { label: "testeur" });
    await a.evaluate(`[...document.querySelectorAll('.cat-tester label.cat-check')].find(l => l.textContent.includes('Anti-lumière bleue UX')).querySelector('input').click()`);
    await fill(a, "#cat-t-sphere", "-2,00");
    await fill(a, "#cat-t-cylinder", "-3,00");
    await click(a, "Évaluer");
    await a.waitFor(hasText("Incompatible selon une règle explicite"), { label: "restriction appliquée" });
    check(await a.evaluate(hasText("Pas d'anti-lumière bleue")), "restriction conditionnelle du fournisseur appliquée par le testeur");
    await reveal(a, "document.querySelector('.cat-tester [role=status]')");
    capture(processA, "25_verres_restriction-testee_A_light");

    await toLenses(b);
    await b.waitFor(hasText(`UX Bleu Confort ${run}`), { label: "B : gamme visible", timeoutMs: 20000 });
    check(!(await b.evaluate(byText("button", "Nouvelle gamme"))), "B (vendeur) : aucune création de gamme proposée");
    check(!(await b.evaluate("[...document.querySelectorAll('th')].some(th => th.textContent.includes('Prix HT'))")), "B (vendeur) : aucun prix fournisseur affiché");
    capture(processB, "26_verres_vu-du-vendeur_B_light");
  },
};

const a = await connect(portA, PAGE);
const b = await connect(portB, PAGE);
try {
  for (const [name, run] of Object.entries(steps)) {
    if (only && !only.has(name)) continue;
    log("ÉTAPE", name);
    await run(a, b);
  }
  log("RÉSULTAT", `${passed} contrôle(s) PASS`);
} finally {
  a.close();
  b.close();
}
