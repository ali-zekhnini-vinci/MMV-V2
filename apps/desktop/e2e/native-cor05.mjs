// Preuves NATIVES des corrections Phase 05 (COR-05-01 à COR-05-08) : DEUX vraies fenêtres Tauri / WebView2 (postes A et
// B, profils WebView2 distincts) contre UNE API MMV.Api réelle de banc (jamais l'API du propriétaire sur 7267).
// Usage (depuis apps/desktop) :
//   MMV_E2E_CREDENTIALS_FILE=<banc>/creds.json node --use-system-ca e2e/native-cor05.mjs --a 9223 --b 9224 \
//     --process-a mmv-cor05a --process-b mmv-cor05b --out <preuves> [--only magasin,recherche,...] [--api-restart <commande>] \
//     [--csv-dir <dossier des CSV fictifs de l'étape importcsv>]
// Étapes importcsv / offres (après G05-SQL 0008, 03/11/2026) : référentiel de base verres (fabricant, gamme, indice,
// traitements) amorcé par le banc ; fichiers CSV fictifs Windows-1252 (tabulation) déposés depuis le disque.
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
  // Session déjà ouverte (shell affiché) : la fenêtre garde son compte.
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
  await page.waitFor(ready, { label: `écran ${label}` });
}

/** Tape dans un champ (sélecteur CSS) : vidage puis vraies frappes CDP. */
async function fill(page, selector, value) {
  await page.evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); el.focus(); el.select?.(); })()`);
  await page.press("Backspace");
  await page.sleep(50);
  if (value !== "") await page.type(value);
}

/** Vrai fichier du disque dans un champ fichier (CDP DOM.setFileInputFiles : même chemin que la boîte « Ouvrir »). */
async function setFile(page, selector, path) {
  const { root } = await page.send("DOM.getDocument", { depth: 0 });
  const { nodeId } = await page.send("DOM.querySelector", { nodeId: root.nodeId, selector });
  await page.send("DOM.setFileInputFiles", { nodeId, files: [path] });
}

/** Liste déroulante native choisie par sa VALEUR (id du SelectField). */
const selectValue = (page, id, value) => page.evaluate(`(() => { const s = document.getElementById(${JSON.stringify(id)});
  s.value = ${JSON.stringify(value)}; s.dispatchEvent(new Event('change', { bubbles: true })); })()`);

/** Fait défiler l'élément (expression JS) au centre de la vue avant une capture. */
const reveal = async (page, element) => {
  await page.evaluate(`(${element})?.scrollIntoView({ block: "center" })`);
  await page.sleep(300);
};
const cardTitled = (title) => `[...document.querySelectorAll('.cat-card')].find(c => c.innerText.includes(${JSON.stringify(title)}))`;

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
    // Colonne « En main » de la ligne MAGASIN (4e cellule) : valeur de départ relevée, pas supposée.
    const onHandB = "Number([...document.querySelectorAll('table.cat-table tbody tr')].find(r => r.innerText.includes('MAGASIN')).cells[3].innerText.trim())";
    const before = await b.evaluate(onHandB);
    check(Number.isInteger(before) && (await availableB()) !== "", `B : Holbrook en main ${before} au MAGASIN`);
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
    await b.waitFor(`${onHandB} === ${before + 2}`, { label: "B mis à jour automatiquement", timeoutMs: 10000 });
    check(await b.evaluate("window.__cor05_marker === 'non rechargé'"), `B mis à jour sans rechargement en ${Date.now() - t0} ms (stock ${before} → ${before + 2})`);
    capture(processB, "04_temps-reel_stock_B_apres_entree_A_light", { width: 1366, height: 768 });
  },

  // COR-05-02 : prix modifié sur A (PA HT × coefficient, aperçu serveur) → B (vendeur, sans costs.read) voit le nouveau prix
  // de vente sans rechargement et ne reçoit jamais le prix d'achat ni le coefficient.
  async prix(a, b) {
    const CAT = 'input[aria-label="Rechercher dans le catalogue"]';
    for (const page of [a, b]) {
      await go(page, "Produits", `document.querySelector('${CAT}') !== null`);
      await fill(page, CAT, "holbrook");
      await page.waitFor(`${rows} === 1`, { label: "Holbrook" });
    }
    await b.evaluate("window.__cor05_prix = 'non rechargé'");
    check(await b.evaluate("!document.body.innerText.includes(\"Prix d'achat HT\") && !document.body.innerText.includes('Coef.')"), "B (vendeur) : aucune colonne de coût");
    await a.evaluate("document.querySelector('table.cat-table tbody tr').click()");
    await a.waitFor(`${byText("button", "Modifier le prix")} !== undefined`, { label: "A : fiche Holbrook" });
    await a.evaluate(`(${byText("button", "Modifier le prix")}).click()`);
    await a.waitFor("document.getElementById('cat-coefficient') !== null", { label: "A : éditeur de prix" });
    await fill(a, "#cat-coefficient", "2,6");
    await a.evaluate(`[...document.querySelectorAll('.cat-editor button')].find(x => x.textContent.trim() === 'Aperçu').click()`);
    await a.waitFor(hasText("159,25"), { label: "A : aperçu serveur 61,25 × 2,6" });
    check(true, "A : aperçu serveur 61,25 × 2,6 = 159,25 €");
    await a.evaluate(`[...document.querySelectorAll('.cat-editor button')].find(x => x.textContent.trim() === 'Enregistrer ce prix').click()`);
    const t0 = Date.now();
    await b.waitFor(hasText("159,25"), { label: "B : nouveau prix", timeoutMs: 10000 });
    check(await b.evaluate("window.__cor05_prix === 'non rechargé'"), `B : prix de vente 159,25 € reçu sans rechargement en ${Date.now() - t0} ms`);
    check(await b.evaluate("!document.body.innerText.includes('61,25')"), "B : prix d'achat jamais affiché au vendeur");
    capture(processB, "05c_temps-reel_prix_B_vendeur_light", { width: 1366, height: 768 });
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

  // COR-05-03 : zoom WebView2 réel 125 % (Ctrl++ du moteur), fiche produit 1366×768.
  async zoom(a) {
    const keys = (k) => execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolve("e2e/send-keys.ps1"), "-Process", processA, "-Keys", k, "-Repeat", "1"]);
    await go(a, "Produits", `document.querySelector('input[aria-label="Rechercher dans le catalogue"]') !== null`);
    await fill(a, 'input[aria-label="Rechercher dans le catalogue"]', "RX5228");
    await a.waitFor(`${rows} === 2`, { label: "RX5228" });
    await a.evaluate("document.querySelector('table.cat-table tbody tr').click()");
    await a.waitFor(hasText("Historique de stock"), { label: "fiche produit" });
    for (let i = 0; i < 8 && Math.abs((await a.zoomFactor()) - 1.25) > 0.01; i++) {
      keys((await a.zoomFactor()) < 1.25 ? "^{ADD}" : "^{SUBTRACT}");
      await a.sleep(400);
    }
    check(Math.abs((await a.zoomFactor()) - 1.25) < 0.01, "zoom WebView2 effectif 125 %");
    capture(processA, "10z_fiche_tableaux_1366x768_zoom125_light", { width: 1366, height: 768 });
    await a.sleep(400);
    check(await a.evaluate(`[...document.querySelectorAll('.cat-card')].every(card => { const c = card.getBoundingClientRect();
      return [...card.querySelectorAll('.cat-tscroll')].every(s => { const r = s.getBoundingClientRect(); return r.right <= c.right + 1; }); })
      && document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1`), "zoom 125 % : tableaux dans leurs cartes, aucun défilement horizontal de page");
    for (let i = 0; i < 8 && Math.abs((await a.zoomFactor()) - 1) > 0.01; i++) {
      keys("^{SUBTRACT}");
      await a.sleep(400);
    }
  },

  // COR-05-05 : référentiel verres (offres) — consultation et testeur.
  async verres(a) {
    await go(a, "Produits", "document.querySelector('.cat-seg') !== null");
    await a.evaluate(`[...document.querySelectorAll('.cat-seg__item')].find(x => x.textContent.includes('Référentiel verres')).click()`);
    await a.waitFor(hasText("Gammes et offres fournisseur"), { label: "référentiel verres" });
    check(true, "référentiel verres : vue « Gammes et offres fournisseur »");
    capture(processA, "11_verres_offres_A_light", { width: 1366, height: 768 });
  },

  // COR-05-04 : import CSV intelligent — vrai fichier Windows-1252 à tabulation, fournisseur du fichier, modèle d'import,
  // contrôle à blanc, application tout ou rien, tarifs du même fichier, réémission sans doublon, correction à l'écran.
  async importcsv(a) {
    const csvDir = option("--csv-dir", "");
    if (!csvDir) {
      log("NOT_TESTED", "importcsv : --csv-dir non fourni");
      return;
    }
    const card3 = "[...document.querySelectorAll('.cat-card')].find(c => c.innerText.includes('3. Contrôle à blanc'))";
    const status = (label) => `(${card3})?.innerText.includes(${JSON.stringify(label)}) === true`;
    const click = (label) => a.evaluate(`(${byText("button", label)}).click()`);
    const runCheck = async (label) => {
      await click("Contrôler (à blanc)");
      await a.waitFor(status("Contrôlé"), { label: `${label} : contrôle à blanc` });
    };
    const apply = async (label) => {
      await click("Appliquer (tout ou rien)");
      await a.waitFor(hasText("Lot appliqué"), { label: `${label} : application` });
    };
    const upload = async (file) => {
      await setFile(a, "input[type=file]", resolve(csvDir, file));
      await a.waitFor(`document.body.innerText.includes(${JSON.stringify(file)}) && ${status("Déposé")}`, { label: `dépôt ${file}` });
    };

    await go(a, "Produits", "document.querySelector('.cat-seg') !== null");
    await a.evaluate(`[...document.querySelectorAll('.cat-seg__item')].find(x => x.textContent.includes('Import CSV')).click()`);
    await a.waitFor(hasText("1. Dépôt du fichier"), { label: "écran Import CSV" });
    await a.evaluate("document.querySelector('input[role=combobox]').focus()");
    await a.type("optique d");
    await a.press("Enter"); // frappe rapide : suggestions encore celles de la saisie précédente
    check(await a.evaluate("document.querySelector('.cat-combo__chosen') === null"), "Entrée pendant la recherche : aucun fournisseur choisi sur des suggestions périmées (O-05-24)");
    await a.waitFor("[...document.querySelectorAll('[role=listbox] [role=option]')].map(o => o.innerText).join('|').includes('Optique Diffusion')"
      + " && document.querySelectorAll('[role=listbox] [role=option]').length === 1 && document.querySelector('.cat-combo .clients-spin') === null", { label: "suggestion « Optique Diffusion » seule" });
    await a.press("ArrowDown");
    await a.press("Enter");
    await a.waitFor("document.querySelector('.cat-combo__chosen')?.innerText.includes('Optique Diffusion') === true", { label: "fournisseur du fichier choisi" });
    check(true, "fournisseur du fichier « Optique Diffusion » choisi au clavier");

    await upload("catalogue-optidif.txt");
    check(await a.evaluate(hasText("encodage Windows-1252 (ANSI)")), "fichier réel Windows-1252 détecté (aucun caractère de remplacement)");
    check(await a.evaluate(hasText("séparateur tabulation")), "séparateur tabulation détecté");
    check(await a.evaluate("[...document.getElementById('cat-map-modelName').options].some(o => o.textContent === 'Modèle')"), "intitulé accentué « Modèle » lu intact");
    check(await a.evaluate("[...document.querySelectorAll('tr')].find(r => r.innerText.startsWith('Référence fournisseur'))?.innerText.includes('Probable — à vérifier') === true"),
      "« Ref » proposé comme référence du fournisseur (probable, à vérifier)");
    await selectValue(a, "cat-map-variantLabel", "Couleur");
    await selectValue(a, "cat-opt-family", "optical_frames");
    await selectValue(a, "cat-opt-policy", "order_only");
    await a.evaluate("[...document.querySelectorAll('label')].find(l => l.textContent.includes('Enregistrer cette correspondance')).querySelector('input').click()");
    await reveal(a, cardTitled("2. Correspondance"));
    capture(processA, "12_import_correspondance_A_light", { width: 1366, height: 768 });
    await runCheck("catalogue");
    check(await a.evaluate(hasText("3 création(s)")) && await a.evaluate(hasText("0 erreur(s)")), "contrôle à blanc : 3 créations, 0 erreur, rien écrit");
    await a.waitFor(hasText("Modèle d'import enregistré"), { label: "modèle d'import du fournisseur enregistré" });
    check(true, "modèle d'import enregistré pour Optique Diffusion");
    await reveal(a, card3);
    capture(processA, "13_import_controle-a-blanc_A_light", { width: 1366, height: 768 });
    await apply("catalogue");
    check(await a.evaluate(hasText("Ce fichier contient aussi des prix")), "lot appliqué tout ou rien ; tarifs du même fichier proposés");
    await reveal(a, card3);
    capture(processA, "14_import_applique_tarifs-proposes_A_light", { width: 1366, height: 768 });

    await click("Importer les tarifs de ce fichier");
    await a.waitFor(`${status("Déposé")} && document.body.innerText.includes("Les prix sont importés dans la devise du magasin")`, { label: "lot Prix du magasin" });
    await runCheck("tarifs");
    check(await a.evaluate(hasText("0 erreur(s)")), "tarifs : rapprochement par référence fournisseur, virgule décimale et milliers « 1 071,50 », 0 erreur");
    await apply("tarifs");
    check(true, "tarifs appliqués dans la devise du magasin (EUR), sans conversion");
    await reveal(a, card3);
    capture(processA, "15_import_tarifs_appliques_A_light", { width: 1366, height: 768 });

    // Après le passage « Prix du magasin », le type déposé reste « Prix » : retour explicite à « Catalogue ».
    await a.evaluate("document.querySelector('input[name=importKind][value=catalog]').click()");
    await upload("catalogue-optidif-v2.txt");
    check(await a.evaluate(hasText("modèle d'import du fournisseur repris")), "réémission : modèle d'import du fournisseur repris automatiquement");
    await runCheck("réémission");
    check(await a.evaluate(hasText("1 création(s)")) && await a.evaluate(hasText("1 modification(s)")) && await a.evaluate(hasText("2 inchangée(s)")),
      "réémission : 1 création, 1 modification, 2 inchangées — aucun doublon");
    await reveal(a, card3);
    capture(processA, "16_import_reemission_sans-doublon_A_light", { width: 1366, height: 768 });
    await apply("réémission");

    await upload("catalogue-optidif-erreur.txt");
    await runCheck("fichier en erreur");
    check(await a.evaluate(hasText("1 erreur(s)")) && await a.evaluate(hasText("Application impossible : tout ou rien")), "ligne sans modèle : 1 erreur, application refusée (tout ou rien)");
    check(await a.evaluate(`(${byText("button", "Appliquer (tout ou rien)")}).disabled`), "bouton « Appliquer » inactif tant qu'une erreur subsiste");
    await reveal(a, "document.querySelector('input[aria-label^=\"Correction ligne 3\"]')");
    capture(processA, "17_import_erreur_correction_A_light", { width: 1366, height: 768 });
    await fill(a, 'input[aria-label^="Correction ligne 3"]', "PO3092V Pilote");
    await click("Recontrôler avec 1 correction(s)");
    await a.waitFor(`${status("Déposé")} && !document.body.innerText.includes("Application impossible")`, { label: "version corrigée déposée" });
    await runCheck("version corrigée");
    check(await a.evaluate(hasText("2 création(s)")) && await a.evaluate(hasText("0 erreur(s)")), "correction à l'écran : nouvelle version déposée, 0 erreur (l'originale reste tracée)");
    await apply("version corrigée");
    await reveal(a, card3);
    capture(processA, "18_import_correction_appliquee_A_light", { width: 1366, height: 768 });
  },

  // COR-05-05 : offre fournisseur d'une gamme (niveau simple puis mode avancé), testeur, historique, coûts masqués au vendeur.
  async offres(a, b) {
    const dialog = "document.querySelector('[role=alertdialog]')";
    const click = (page, label) => page.evaluate(`(${byText("button", label)}).click()`);
    const tick = (scope, label) => a.evaluate(`[...document.querySelectorAll(${JSON.stringify(`${scope} label.cat-check`)})].find(l => l.textContent.includes(${JSON.stringify(label)})).querySelector('input').click()`);
    const evaluateCorrection = async (values, expected, label) => {
      for (const [name, value] of Object.entries(values)) await fill(a, `#cat-t-${name}`, value);
      await click(a, "Évaluer");
      await a.waitFor(hasText(expected), { label });
      check(true, label);
    };
    const toVerses = async (page) => {
      await go(page, "Produits", "document.querySelector('.cat-seg') !== null");
      await page.evaluate(`[...document.querySelectorAll('.cat-seg__item')].find(x => x.textContent.includes('Référentiel verres')).click()`);
      await page.waitFor(hasText("Verrier Fictif du Nord — Clarté Progressive (fictif)"), { label: "gamme fictive" });
    };

    await toVerses(a);
    await click(a, "Offre fournisseur");
    await a.waitFor(hasText("Nouvelle offre fournisseur"), { label: "formulaire d'offre" });
    await a.evaluate(`${dialog}.querySelector('input[role=combobox]').focus()`);
    await a.type("verres n");
    await a.waitFor("document.querySelector('.cat-combo .clients-spin') === null && document.querySelector('[role=listbox] [role=option]')?.innerText.includes('Laboratoire Verres Nord') === true", { label: "suggestion laboratoire en tête" });
    await a.press("ArrowDown");
    await a.press("Enter");
    await a.waitFor("document.querySelector('.cat-combo__chosen')?.innerText.includes('Laboratoire Verres Nord') === true", { label: "laboratoire choisi" });
    await selectValue(a, "cat-lensType", "progressive");
    await a.evaluate(`(() => { const s = document.getElementById('cat-indexId'); s.value = [...s.options].find(o => o.textContent === 'Indice 1,6').value;
      s.dispatchEvent(new Event('change', { bubbles: true })); })()`);
    await fill(a, "#cat-leadTimeDays", "7");
    await fill(a, "#cat-priceHt", "42,50");
    await fill(a, "#cat-priceCurrency", "EUR");
    await fill(a, "#cat-sphereMin", "-10");
    await fill(a, "#cat-sphereMax", "5");
    await fill(a, "#cat-additionMin", "0,75");
    await fill(a, "#cat-additionMax", "3,50");
    await fill(a, "#cat-diameters", "65, 70");
    await tick("[role=alertdialog]", "Anti-lumière bleue");
    await tick("[role=alertdialog]", "Antireflet");
    await fill(a, "#cat-sourceReference", "Fiche technique fictive 2026, p. 14");
    await fill(a, "#cat-reason", "Création");
    capture(processA, "19_verres_offre_saisie-simple_A_light", { width: 1366, height: 768 });
    await click(a, "Créer l'offre");
    const offerRow = "[...document.querySelectorAll('table tbody tr')].find(r => r.innerText.includes('Laboratoire Verres Nord'))";
    await a.waitFor(`${dialog} === null && (${offerRow})?.innerText.includes('Incomplète') === true`, { label: "offre créée, ligne du tableau" });
    check(true, "offre créée au niveau simple : fiche « Incomplète »");
    await a.evaluate(`document.querySelector('button[aria-label^="Afficher le détail de l\\'offre"]').click()`);
    await a.waitFor("document.querySelector('.cat-tester') !== null", { label: "détail de l'offre et testeur" });
    await tick(".cat-tester", "Anti-lumière bleue");
    await evaluateCorrection({ sphere: "-6,00", addition: "2,00" }, "Compatibilité à vérifier (données insuffisantes)",
      "testeur : SPH −6,00 dans la plage mais fiche incomplète → « à vérifier », jamais « compatible »");
    check(await a.evaluate(hasText("ni validation clinique")), "testeur : avertissement « ni validation clinique ni garantie du fabricant »");
    await reveal(a, "document.querySelector('.cat-tester [role=status]')");
    capture(processA, "20_verres_testeur_a-verifier_A_light", { width: 1366, height: 768 });
    await evaluateCorrection({ sphere: "-11,00" }, "Incompatible selon une règle explicite", "testeur : SPH −11,00 hors plage déclarée → « incompatible »");
    await reveal(a, "document.querySelector('.cat-tester [role=status]')");
    capture(processA, "21_verres_testeur_incompatible_A_light", { width: 1366, height: 768 });

    await click(a, "Modifier l'offre");
    await a.waitFor(hasText("Enregistrer la révision"), { label: "révision de l'offre" });
    await a.evaluate("[...document.querySelectorAll('[role=alertdialog] label')].find(l => l.textContent.includes('Mode avancé')).querySelector('input').click()");
    await a.waitFor("document.querySelector('[role=alertdialog]').innerText.includes('Fiche fournisseur complète')", { label: "mode avancé" });
    await a.evaluate("[...document.querySelectorAll('[role=alertdialog] label')].find(l => l.textContent.includes('Fiche fournisseur complète')).querySelector('input').click()");
    await fill(a, "#cat-cylinderMin", "-4");
    await fill(a, "#cat-cylinderMax", "0");
    await fill(a, "#cat-reason", "Fiche complète reçue");
    capture(processA, "22_verres_offre_mode-avance_A_light", { width: 1366, height: 768 });
    await click(a, "Enregistrer la révision");
    await a.waitFor(`${dialog} === null && /(^|[^n])Complète/.test((${offerRow})?.innerText ?? '')`, { label: "révision enregistrée" });
    check(true, "mode avancé : fiche déclarée complète, plage CYL ajoutée, révision enregistrée");
    await a.waitFor(hasText("Fiche complète reçue"), { label: "historique des révisions" });
    check(await a.evaluate("document.body.innerText.includes('n° 1') && document.body.innerText.includes('n° 2')"), "historique : révisions n° 1 et n° 2 avec motif et auteur");
    await evaluateCorrection({ sphere: "-6,00", cylinder: "-1,00", addition: "2,00" }, "Compatible avec les règles fournisseur complètes enregistrées",
      "testeur : fiche complète, SPH/CYL/ADD dans les plages → « compatible » (règles enregistrées)");
    await a.sleep(4500); // notification « Offre mise à jour » refermée
    await reveal(a, "document.querySelector('.cat-tester [role=status]')");
    capture(processA, "23_verres_testeur_compatible_A_light", { width: 1366, height: 768 });

    await toVerses(b);
    await b.waitFor(hasText("Laboratoire Verres Nord"), { label: "B : offre visible du vendeur" });
    check(await b.evaluate(`![...document.querySelectorAll('th')].some(th => th.textContent.includes('Prix HT')) && !document.body.innerText.includes('42,50')`),
      "B (vendeur, sans costs.read) : aucun prix fournisseur servi ni affiché");
    check(await b.evaluate(`!(${byText("button", "Offre fournisseur")})`), "B (sans catalog.configure) : aucune création d'offre proposée");
    capture(processB, "24_verres_offres_vendeur_B_light", { width: 1366, height: 768 });
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
