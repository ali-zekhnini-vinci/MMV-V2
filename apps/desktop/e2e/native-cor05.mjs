// Preuves NATIVES des corrections Phase 05 (COR-05-01 à COR-05-08) : DEUX vraies fenêtres Tauri / WebView2 (postes A et
// B, profils WebView2 distincts) contre UNE API MMV.Api réelle de banc (jamais l'API du propriétaire sur 7267).
// Usage (depuis apps/desktop) :
//   MMV_E2E_CREDENTIALS_FILE=<banc>/creds.json node --use-system-ca e2e/native-cor05.mjs --a 9223 --b 9224 \
//     --process-a mmv-cor05a --process-b mmv-cor05b --out <preuves> [--only magasin,recherche,...] [--api-restart <commande>]
// Comptes éphémères du banc (cluster jetable) : identifiants lus en mémoire, jamais affichés ni copiés dans les preuves.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { connect } from "./cdp.mjs";

const args = process.argv.slice(2);
const option = (name, fallback) => (args.indexOf(name) >= 0 ? args[args.indexOf(name) + 1] : fallback);
const portA = Number(option("--a", "9223"));
const portB = Number(option("--b", "9224"));
const processA = option("--process-a", "mmv-cor05a");
const processB = option("--process-b", "mmv-cor05b");
const outDir = resolve(option("--out", "."));
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

function capture(processName, name, { width = 0, height = 0 } = {}) {
  const file = join(outDir, `COR05_native_${name}_${stamp}.png`);
  const extra = width ? ["-ClientWidth", String(width), "-ClientHeight", String(height)] : [];
  const output = execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolve("e2e/capture-window.ps1"),
    "-Process", processName, "-Out", file, "-ClientOnly", ...extra], { encoding: "utf8" }).trim();
  log("CAPTURE", output.replace(outDir, "<PREUVES>"));
}

const accounts = Object.fromEntries(JSON.parse(readFileSync(process.env.MMV_E2E_CREDENTIALS_FILE, "utf8")).map((a) => [a.key, a]));
const hasText = (value) => `document.body.innerText.includes(${JSON.stringify(value)})`;
const navButton = (label) =>
  `[...document.querySelectorAll('nav[aria-label="Navigation"] button')].find(b => (b.getAttribute('aria-label') || b.textContent).startsWith(${JSON.stringify(label)}))`;
const byText = (selector, label) => `[...document.querySelectorAll(${JSON.stringify(selector)})].find(e => e.textContent.trim() === ${JSON.stringify(label)})`;

async function signIn(page, account) {
  if (await page.evaluate(hasText(`Bonjour, ${account.displayName}`))) return;
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
  await page.waitFor(ready, { label: `écran ${label}` });
}

/** Tape dans un champ (sélecteur CSS) : vidage puis vraies frappes CDP. */
async function fill(page, selector, value) {
  await page.evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); el.focus(); el.select?.(); })()`);
  await page.press("Backspace");
  await page.sleep(50);
  if (value !== "") await page.type(value);
}

const rows = "document.querySelectorAll('table.cat-table tbody tr').length";
const theme = async (page, mode) => {
  const current = await page.evaluate("document.documentElement.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')");
  if (current !== mode) {
    await page.evaluate(`[...document.querySelectorAll('button')].find(b => /thème|theme/i.test(b.getAttribute('aria-label') || ''))?.click()`);
    await page.sleep(300);
  }
};

const steps = {
  // COR-05-01 : connexion → magasin de l'installation, aucun sélecteur.
  async magasin(a, b) {
    await signIn(a, accounts["natif-admin"]);
    await signIn(b, accounts["natif-vendeur"]);
    for (const page of [a, b]) {
      await go(page, "Produits", "document.querySelector('.cat-store__name') !== null");
      check(await page.evaluate("document.querySelector('.cat-store__name').textContent === 'Optique du Banc (fictif)'"), "magasin de l'installation affiché sans sélection");
      check(await page.evaluate("document.querySelector('.cat-store__currency').textContent === 'EUR'"), "devise du magasin EUR");
      check(await page.evaluate("document.querySelector('select[aria-label=\"Magasin actif\"]') === null && document.querySelectorAll('.cat-context select').length === 0"), "aucun sélecteur de magasin");
      await page.waitFor("document.querySelector('.cat-live--ok') !== null", { label: "temps réel connecté", timeoutMs: 20000 });
      check(true, "indicateur temps réel « Connecté »");
    }
    capture(processA, "01_catalogue_magasin_temps-reel_A_light", { width: 1366, height: 768 });
  },

  // COR-05-07 : recherche serveur, accents, mots combinés, au-delà de la première page.
  async recherche(a) {
    await go(a, "Produits", "document.querySelector('input[aria-label=\"Rechercher dans le catalogue\"]') !== null");
    const search = async (q, expected) => {
      await fill(a, ".clients-search input", q);
      await a.waitFor(`!document.querySelector('.cat-search-busy') && ${rows} === ${expected}`, { label: `recherche « ${q} » → ${expected}`, timeoutMs: 8000 });
      check(true, `recherche « ${q} » : ${expected} ligne(s)`);
    };
    await search("Ray Ban", 3);
    await search("RX5228", 2);
    await search("ray ban noir", 2);
    await search("rayban", 3);
    await search("ecaille", 1);
    await search("etui rigide modele 54", 1);
    check(await a.evaluate("document.querySelector('mark.clients-mark') !== null"), "mots surlignés comme dans Clients");
    capture(processA, "02_recherche_etui-54_page-2_A_light", { width: 1366, height: 768 });
    await search("ray ban noir", 2);
    capture(processA, "03_recherche_ray-ban-noir_A_light", { width: 1366, height: 768 });
    await fill(a, ".clients-search input", "");
    await go(a, "Stock & inventaires", "document.querySelector('input[aria-label=\"Rechercher dans le stock\"]') !== null");
    await fill(a, ".clients-search input", "holbrook");
    await a.waitFor(`${rows} === 2`, { label: "stock « holbrook »", timeoutMs: 8000 });
    check(true, "recherche Stock « holbrook » : 2 lignes (2 emplacements)");
    await fill(a, ".clients-search input", "");
  },

  // COR-05-02 : poste A modifie le stock → poste B mis à jour sans rechargement ; prix sur A → B selon ses droits.
  async tempsreel(a, b) {
    await go(b, "Stock & inventaires", "document.querySelector('input[aria-label=\"Rechercher dans le stock\"]') !== null");
    await fill(b, ".clients-search input", "holbrook");
    await b.waitFor(`${rows} === 2`, { label: "B : stock Holbrook" });
    const availableB = () => b.evaluate("[...document.querySelectorAll('table.cat-table tbody tr')].find(r => r.innerText.includes('MAGASIN'))?.innerText ?? ''");
    const before = await availableB();
    check(/\b4\b/.test(before), "B : Holbrook en main 4 au MAGASIN");
    await b.evaluate("window.__cor05_marker = 'non rechargé'");
    // Poste A : entrée de stock par l'écran (modale « Entrée »).
    await go(a, "Stock & inventaires", "document.querySelector('input[aria-label=\"Rechercher dans le stock\"]') !== null");
    await fill(a, ".clients-search input", "holbrook");
    await a.waitFor(`${rows} === 2`, { label: "A : stock Holbrook" });
    await a.evaluate(`[...document.querySelectorAll('table.cat-table tbody tr')].find(r => r.innerText.includes('MAGASIN')).querySelector('button[aria-label^="Entrée"], button[title^="Entrée"]')?.click()
      ?? [...[...document.querySelectorAll('table.cat-table tbody tr')].find(r => r.innerText.includes('MAGASIN')).querySelectorAll('button')].find(x => /entrée/i.test(x.textContent + (x.getAttribute('aria-label') ?? '')))?.click()`);
    await a.waitFor("document.querySelector('[role=alertdialog]') !== null", { label: "A : modale d'entrée" });
    const select = (id, label) => a.evaluate(`(() => { const s = document.getElementById(${JSON.stringify(id)});
      s.value = [...s.options].find(o => o.textContent.trim() === ${JSON.stringify(label)}).value; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
    await select("cat-movementLocation", "Magasin");
    await a.evaluate("document.getElementById('cat-quantity').focus()");
    await a.type("2");
    await select("cat-reasonCode", "Article retrouvé");
    await a.waitFor(hasText("Nouveau stock (estimé)"), { label: "A : estimation" });
    capture(processA, "04a_temps-reel_entree_stock_A_light", { width: 1366, height: 768 });
    await a.evaluate(`(${byText("[role=alertdialog] button", "Enregistrer le mouvement")}).click()`);
    await a.waitFor("document.querySelector('[role=alertdialog]') === null", { label: "A : entrée enregistrée", timeoutMs: 10000 });
    const t0 = Date.now();
    await b.waitFor("[...document.querySelectorAll('table.cat-table tbody tr')].find(r => r.innerText.includes('MAGASIN'))?.innerText.match(/\\b6\\b/) !== null", { label: "B mis à jour automatiquement", timeoutMs: 10000 });
    check(await b.evaluate("window.__cor05_marker === 'non rechargé'"), `B mis à jour sans rechargement en ${Date.now() - t0} ms (stock 4 → 6)`);
    capture(processB, "04_temps-reel_stock_B_apres_entree_A_light", { width: 1366, height: 768 });
  },

  // COR-05-02 : coupure de l'API puis reprise : états « reconnexion / déconnecté » puis « connecté » et resynchronisation.
  async reconnexion(_a, b) {
    const restart = option("--api-restart", "");
    if (!restart) {
      log("NOT_TESTED", "reconnexion : --api-restart non fourni");
      return;
    }
    execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", restart, "-Stop"], { encoding: "utf8" });
    await b.waitFor("document.querySelector('.cat-live--warn, .cat-live--err') !== null", { label: "B : reconnexion signalée", timeoutMs: 20000 });
    check(true, "B : « Reconnexion en cours » après coupure");
    await b.waitFor("document.querySelector('.cat-live--err') !== null", { label: "B : déconnecté", timeoutMs: 30000 });
    check(await b.evaluate(hasText("données potentiellement obsolètes")), "B : « Déconnecté — données potentiellement obsolètes »");
    capture(processB, "05_temps-reel_deconnecte_B_light", { width: 1366, height: 768 });
    execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", restart, "-Start"], { encoding: "utf8" });
    await b.waitFor("document.querySelector('.cat-live--ok') !== null", { label: "B : reconnecté", timeoutMs: 60000 });
    check(true, "B : reconnecté automatiquement, données relues");
  },

  // COR-05-06 + 08 : assistant cinq étapes, combobox fournisseur, PA HT × coefficient (aperçu serveur), stock initial.
  async assistant(a) {
    await go(a, "Produits", "document.querySelector('input[aria-label=\"Rechercher dans le catalogue\"]') !== null");
    await a.evaluate(`(${byText("button", "Nouveau produit")}).click()`);
    await a.waitFor("document.querySelector('.cat-steps--wizard') !== null", { label: "assistant" });
    check(await a.evaluate("document.querySelectorAll('.cat-steps--wizard .cat-step').length === 5"), "assistant : 5 étapes visibles");
    await a.evaluate(`(${byText("button", "Suivant")}).click()`);
    check(await a.evaluate(hasText("Le nom du modèle est obligatoire.")), "assistant : contrôle de l'étape 1");
    await a.evaluate(`(() => { const s = document.getElementById('cat-brand'); s.focus(); })()`);
    await a.type("Banc Natif");
    await a.evaluate("document.getElementById('cat-name').focus()");
    await a.type(`Monture assistant ${stamp}`);
    capture(processA, "06_assistant_etape1_A_light", { width: 1366, height: 768 });
    await a.evaluate(`(${byText("button", "Suivant")}).click()`);
    await a.waitFor(hasText("Variante 1"), { label: "étape 2" });
    await a.evaluate("document.getElementById('cat-variants-0--label').focus()");
    await a.type("Noir 52");
    await a.evaluate(`(${byText("button", "Suivant")}).click()`);
    await a.waitFor("document.querySelector('input[role=combobox]') !== null", { label: "étape 3 : combobox" });
    await a.evaluate("document.querySelector('input[role=combobox]').focus()");
    await a.type("lunet");
    await a.waitFor("document.querySelectorAll('[role=listbox] [role=option]').length >= 1", { label: "suggestions fournisseur" });
    check(await a.evaluate("document.querySelector('[role=listbox]').innerText.includes('Lunetterie Générale Distribution')"), "combobox : suggestion par nom partiel");
    capture(processA, "07_assistant_combobox_fournisseur_A_light", { width: 1366, height: 768 });
    await a.press("ArrowDown");
    await a.press("Enter");
    await a.waitFor("document.querySelector('.cat-combo__chosen') !== null", { label: "fournisseur choisi au clavier" });
    check(true, "combobox : sélection au clavier (flèche + Entrée)");
    await a.evaluate(`(${byText("button", "Suivant")}).click()`);
    await a.waitFor(hasText("Prix de vente HT = prix d'achat HT × coefficient"), { label: "étape 4" });
    await a.evaluate(`(${byText("button", "Coefficient")}).click()`);
    await a.evaluate("document.getElementById('cat-variants-0--price-purchasePriceHt').focus()");
    await a.type("40,125");
    await a.evaluate("document.getElementById('cat-variants-0--price-coefficient').focus()");
    await a.type("2,5");
    await a.evaluate(`(${byText("button", "Calculer")}).click()`);
    await a.waitFor(hasText("100,31"), { label: "aperçu serveur 40,125 × 2,5" });
    check(true, "aperçu serveur : 40,125 × 2,5 = 100,31 € (arrondi serveur)");
    capture(processA, "08_assistant_tarif_coefficient_A_light", { width: 1366, height: 768 });
    await a.evaluate(`(${byText("button", "Suivant")}).click()`);
    await a.waitFor(hasText("Résumé avant création"), { label: "étape 5" });
    await a.evaluate("document.getElementById('cat-variants-0--stock-quantity').focus()");
    await a.type("3");
    capture(processA, "09_assistant_resume_A_light", { width: 1366, height: 768 });
    await a.evaluate(`(${byText("button", "Créer le produit")}).click()`);
    await a.waitFor(hasText("Variantes (1)"), { label: "fiche du produit créé", timeoutMs: 15000 });
    check(await a.evaluate(hasText("100,31")), "produit créé tout ou rien : prix 100,31 € sur la fiche");
    check(await a.evaluate("/MON-[2-9A-HJKMNP-Z]{6}/.test(document.body.innerText)"), "SKU généré par le serveur (MON-xxxxxx)");
  },

  // COR-05-03 : tableaux de la fiche produit contenus dans leur carte, plusieurs résolutions, clair et sombre, zoom 125 %.
  async tableaux(a) {
    await go(a, "Produits", "document.querySelector('input[aria-label=\"Rechercher dans le catalogue\"]') !== null");
    await fill(a, ".clients-search input", "RX5228");
    await a.waitFor(`${rows} === 2`, { label: "RX5228" });
    await a.evaluate("document.querySelector('table.cat-table tbody tr').click()");
    await a.waitFor(hasText("Historique de stock"), { label: "fiche produit" });
    const contained = `(() => [...document.querySelectorAll('.cat-card')].every(card => {
      const c = card.getBoundingClientRect();
      return [...card.querySelectorAll('.cat-tscroll')].every(s => { const r = s.getBoundingClientRect(); return r.left >= c.left - 1 && r.right <= c.right + 1; });
    }) && document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)()`;
    for (const [w, h] of [[1920, 1080], [1366, 768], [1024, 700], [960, 640]]) {
      for (const mode of ["light", "dark"]) {
        await theme(a, mode);
        capture(processA, `10_fiche_tableaux_${w}x${h}_${mode}`, { width: w, height: h });
        await a.sleep(400);
        check(await a.evaluate(contained), `fiche produit ${w}×${h} ${mode} : tableaux dans leurs cartes, aucun défilement horizontal de page`);
      }
    }
    await theme(a, "light");
  },

  // COR-05-05 : référentiel verres (offres) — consultation et testeur.
  async verres(a) {
    await go(a, "Produits", "document.querySelector('.cat-seg') !== null");
    await a.evaluate(`[...document.querySelectorAll('.cat-seg__item')].find(x => x.textContent.includes('Référentiel verres')).click()`);
    await a.waitFor(hasText("Gammes et offres fournisseur"), { label: "référentiel verres" });
    check(true, "référentiel verres : vue « Gammes et offres fournisseur »");
    capture(processA, "11_verres_offres_A_light", { width: 1366, height: 768 });
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
