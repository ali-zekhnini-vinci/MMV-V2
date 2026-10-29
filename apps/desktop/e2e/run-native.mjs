// Scénarios de preuve NATIVE (phase 02) contre la vraie fenêtre Tauri/WebView2 et le vrai MMV.Api.
// Usage (depuis apps/desktop) :
//   node e2e/run-native.mjs <scénario> [--port 9222] [--out <dossier de preuves>]
// Identifiants des comptes éphémères (base de TEST) : fichier JSON désigné par MMV_E2E_CREDENTIALS_FILE,
// lu en mémoire, jamais affiché ni copié dans les preuves. Voir docs/phase/02/IMPLEMENTATION_SPEC.md.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { connect } from "./cdp.mjs";

const args = process.argv.slice(2);
const scenario = args[0];
const option = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : fallback;
};
const port = Number(option("--port", "9222"));
const outDir = resolve(option("--out", "."));
// Préfixe des captures : P02_native (phase 02, défaut) ou P03_native (scénarios clients-*, phase 03-A3) ;
// P03B3_native (rdv-b3*), P03C_native (parcours-03c, matrice-03c), P03D_native (parcours-03d1).
// P04C_native (optique-04c : dossier optique réel, migration 0006 appliquée, banc mmv_v2_test).
const prefix = option("--prefix", "P02_native");
// 03-D3 : banc isolé à côté des processus ouverts par le propriétaire (son MMV.Api garde 7267, son mmv-desktop reste
// ouvert) : origine de l'API de test et nom du processus de la fenêtre testée (exécutable renommé).
const API = option("--api", "https://localhost:7267");
const windowProcess = option("--process", "mmv-desktop");
const stamp = new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d+Z$/, "Z");

const log = (step, detail = "") => console.log(`${new Date().toISOString()} ${step}${detail ? ` — ${detail}` : ""}`);
const check = (condition, label) => {
  if (!condition) throw new Error(`ÉCHEC : ${label}`);
  log("PASS", label);
};

function capture(name, { width = 0, height = 0 } = {}) {
  const file = join(outDir, `${prefix}_${name}_${stamp}.png`);
  const scriptPath = resolve("e2e/capture-window.ps1");
  const extra = width ? ["-ClientWidth", String(width), "-ClientHeight", String(height)] : [];
  const output = execFileSync(
    "powershell.exe",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-Process", windowProcess, "-Out", file, "-ClientOnly", ...extra],
    { encoding: "utf8" },
  ).trim();
  log("CAPTURE", output.replace(outDir, "<EVIDENCE>"));
  return file;
}

/** Vraies frappes système (SendKeys) vers la fenêtre Tauri. */
function osKeys(keys, repeat = 1) {
  execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolve("e2e/send-keys.ps1"),
    "-Process", windowProcess, "-Keys", keys, "-Repeat", String(repeat)]);
}

/** Zoom WebView2 réel (Ctrl++ / Ctrl+- du moteur : paliers 100, 110, 125, 150 %…), vérifié par devicePixelRatio. */
async function setZoom(page, factor) {
  for (let step = 0; step < 12; step++) {
    const current = await page.zoomFactor();
    if (Math.abs(current - factor) < 0.01) break;
    osKeys(current < factor ? "^{ADD}" : "^{SUBTRACT}");
    await page.sleep(300);
  }
  const actual = await page.zoomFactor();
  check(Math.abs(actual - factor) < 0.01, `zoom WebView2 effectif ${Math.round(actual * 100)} %`);
}

function credentials() {
  const path = process.env.MMV_E2E_CREDENTIALS_FILE;
  if (!path) throw new Error("MMV_E2E_CREDENTIALS_FILE absent.");
  return JSON.parse(readFileSync(path, "utf8"));
}

const text = (page) => page.evaluate("document.body.innerText");
const hasText = (value) => `document.body.innerText.includes(${JSON.stringify(value)})`;

async function freshLogin(page) {
  await page.reload();
  await page.waitFor(hasText("Bienvenue,"), { label: "écran de connexion" });
}

async function signIn(page, account) {
  await freshLogin(page);
  await page.evaluate("document.getElementById(document.querySelector('label[for]').htmlFor).focus()");
  await page.type(account.userName);
  await page.press("Tab");
  await page.type(account.password);
  await page.press("Enter");
  await page.waitFor(hasText(`Bonjour, ${account.displayName}`), { label: "shell après connexion" });
}

const navButton = (label) =>
  `[...document.querySelectorAll('nav[aria-label="Navigation"] button')].find(b => (b.getAttribute('aria-label') || b.textContent).startsWith(${JSON.stringify(label)}))`;

const buttonByText = (label) => `[...document.querySelectorAll('button')].find(b => b.textContent.trim() === ${JSON.stringify(label)})`;
const buttonByName = (label) =>
  `[...document.querySelectorAll('button')].find(b => (b.getAttribute('aria-label') || b.textContent.trim()) === ${JSON.stringify(label)})`;
const isDisabled = (label) => `(${buttonByName(label)})?.disabled === true`;

/** Focalise le champ de formulaire désigné par son étiquette (« Prénom », « Nom »…). */
const focusField = (page, label) =>
  page.evaluate(`document.getElementById([...document.querySelectorAll('label[for]')]
    .find(l => l.textContent.replace('*', '').trim() === ${JSON.stringify(label)}).htmlFor).focus()`);

async function fillField(page, label, value) {
  await focusField(page, label);
  await page.evaluate("document.activeElement.select && document.activeElement.select()");
  await page.type(value);
}

/** Thème du shell imposé (la préférence « mmv.theme » de la WebView persiste entre les sessions). */
async function setTheme(page, theme) {
  if ((await page.evaluate("document.documentElement.dataset.theme")) !== theme) {
    await page.evaluate(`document.querySelector('[aria-label^="Thème"]').click()`);
    await page.sleep(300);
  }
  check((await page.evaluate("document.documentElement.dataset.theme")) === theme, `thème ${theme}`);
}

async function openClients(page) {
  await page.evaluate(`${navButton("Clients")}.click()`);
  await page.waitFor("document.querySelector('h1')?.textContent === 'Clients'", { label: "écran Clients" });
}

/** Autre poste : modification directe sur l'API réelle avec un autre compte éphémère (jeton jamais affiché). */
async function otherPostUpdate(account, search, city) {
  const base = API;
  const json = { "Content-Type": "application/json" };
  const login = await fetch(`${base}/api/auth/login`, { method: "POST", headers: json, body: JSON.stringify({ userName: account.userName, password: account.password }) });
  const auth = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  const page = await (await fetch(`${base}/api/clients?q=${encodeURIComponent(search)}`, { headers: auth })).json();
  const detail = await (await fetch(`${base}/api/clients/${page.items[0].id}`, { headers: auth })).json();
  const { firstName, lastName, birthDate, phone, email, addressLine, postalCode, notes, version } = detail;
  const put = await fetch(`${base}/api/clients/${detail.id}`, {
    method: "PUT",
    headers: { ...auth, ...json },
    body: JSON.stringify({ firstName, lastName, birthDate, phone, email, addressLine, city, postalCode, notes, version }),
  });
  await fetch(`${base}/api/auth/logout`, { method: "POST", headers: auth });
  return put.status;
}

// ——— Rendez-vous (03-B3) : magasin configuré par le propriétaire (Europe/Brussels, grille 09:00–19:00, aucun jour
// masqué, capacité d'accueil 1), données FICTIVES validées sur mmv_v2_test par l'outil jetable désigné par
// MMV_E2E_TOOL (comptes b3n-*, clients « Fictif-B3N-* »), nettoyées après la passe.
const J = JSON.stringify;

// 03-C : une seule session API par compte pendant le scénario (la connexion est limitée à 10 tentatives par minute
// et par adresse) ; toutes fermées à la fin du scénario (apiLogoutAll).
const apiSessions = new Map();

/** Lecture de l'état réel par l'API (compte éphémère ; jeton jamais affiché). */
async function apiGet(account, path) {
  if (!apiSessions.has(account.userName)) {
    const login = await fetch(`${API}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: J({ userName: account.userName, password: account.password }),
    });
    if (!login.ok) throw new Error(`Connexion API refusée (${login.status}).`);
    apiSessions.set(account.userName, { Authorization: `Bearer ${(await login.json()).accessToken}` });
  }
  return (await fetch(`${API}${path}`, { headers: apiSessions.get(account.userName) })).json();
}

async function apiLogoutAll() {
  for (const auth of apiSessions.values()) await fetch(`${API}/api/auth/logout`, { method: "POST", headers: auth });
  apiSessions.clear();
}

function tool(...toolArgs) {
  const path = process.env.MMV_E2E_TOOL;
  if (!path) throw new Error("MMV_E2E_TOOL absent.");
  const output = execFileSync("dotnet", ["run", path, "--", ...toolArgs], { encoding: "utf8" }).trim();
  log("OUTIL", output.split("\n").pop());
}

async function setSelect(page, label, optionText) {
  const ok = await page.evaluate(`(() => {
    const l = [...document.querySelectorAll('label[for]')].find(l => l.textContent.replace('*', '').trim() === ${J(label)});
    const s = l && document.getElementById(l.htmlFor);
    if (!s || s.tagName !== 'SELECT') return false;
    const o = [...s.options].find(o => o.textContent.trim() === ${J(optionText)});
    if (!o) return false;
    Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(s, o.value);
    s.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
  })()`);
  check(ok, `${label} = ${optionText}`);
}

const click = (page, expression, label) => page.evaluate(`(() => { const e = ${expression}; if (!e) return false; e.click(); return true; })()`).then((ok) => check(ok, label));
const inAside = (label) =>
  `[...document.querySelectorAll('aside button')].find(b => (b.getAttribute('aria-label') || b.textContent.trim()) === ${J(label)})`;
const inDialog = (label) => `[...document.querySelectorAll('[role=alertdialog] button')].find(b => b.textContent.trim() === ${J(label)})`;
const eventFor = (text) =>
  `[...document.querySelectorAll('section[aria-label="Agenda"] [data-appointment-id]')].map(e => e.closest('[role=button]')).find(b => b && b.textContent.includes(${J(text)}))`;

async function openRdv(page) {
  await page.evaluate(`${navButton("Rendez-vous")}.click()`);
  await page.waitFor("document.querySelector('h1')?.textContent === 'Rendez-vous' && !!document.querySelector('section[aria-label=\"Agenda\"]')", { label: "agenda" });
}

async function pickClient(page, firstName, lastName) {
  await click(page, "document.querySelector('aside button[aria-haspopup=\"listbox\"]')", "ouverture du sélecteur de client");
  await page.waitFor("!!document.querySelector('[aria-label=\"Rechercher un client actif\"]')", { label: "sélecteur de client" });
  await page.evaluate("document.querySelector('[aria-label=\"Rechercher un client actif\"]').focus()");
  await page.type(lastName);
  // Libellé « NOM Prénom » (clientDisplayName) : nom comparé sans casse.
  const option = `[...document.querySelectorAll('[role=option]')].find(o => o.textContent.toUpperCase().includes(${J(lastName.toUpperCase())}) && o.textContent.includes(${J(` ${firstName}`)}))`;
  await page.waitFor(`!!${option}`, { label: `client ${firstName}` });
  await click(page, option, `client ${firstName} choisi`);
  await page.waitFor(`document.querySelector('aside button[aria-haspopup="listbox"]')?.textContent.includes(${J(firstName)})`, { label: "client affiché" });
}

async function fillAppointment(page, { client, last, date, time, duration, responsible, reason, notes }) {
  if (client) await pickClient(page, client, last);
  // 03-D1 : date et heure choisies dans les sélecteurs (date attendue jj/mm/aaaa, comme avant).
  if (date) await pickDateNative(page, date.split("/").reverse().join("-"));
  if (time) await pickTimeNative(page, time);
  if (duration) await setSelect(page, "Durée", duration);
  if (reason) await setSelect(page, "Motif", reason);
  if (responsible) await setSelect(page, "Responsable", responsible);
  if (notes !== undefined) await fillField(page, "Notes", notes);
}

async function abandonForm(page) {
  await click(page, inAside("Annuler"), "Annuler la saisie");
  if (await page.evaluate(`!!${inDialog("Abandonner")}`)) await click(page, inDialog("Abandonner"), "abandon confirmé");
  await page.waitFor("!document.querySelector('aside form')", { label: "formulaire fermé" });
}

async function openEvent(page, text) {
  await page.waitFor(`!!${eventFor(text)}`, { label: `rendez-vous « ${text} » affiché` });
  await click(page, eventFor(text), `ouverture de « ${text} »`);
  await page.waitFor("document.querySelector('aside[aria-label^=\"Détail du rendez-vous de\"]')?.textContent.includes('Responsable')", { label: "détail" });
}

const dayList = async (account, day) => (await apiGet(account, `/api/appointments?from=${day}T00:00:00Z&to=${day}T23:00:00Z&status=all`)).items;

// ——— Validation fonctionnelle 03-C : parcours complet et matrice d'affichage (thèmes × résolutions × zooms). Même
// banc que 03-B3 (magasin Europe/Brussels, 09:00–19:00, aucun jour masqué, capacité 1 passés au seul processus de
// test ; données FICTIVES c3n-* / « Fictif-C3N-* » sur mmv_v2_test, nettoyées après la passe).
const STORE_TZ = "Europe/Brussels";

/** Heure murale du magasin d'un instant UTC (ICU de Node, indépendant du poste testé). */
const storeWall = (utc) => new Intl.DateTimeFormat("fr-FR", { timeZone: STORE_TZ, hour: "2-digit", minute: "2-digit" }).format(new Date(utc));

/** Premier jour ouvré à au moins `offset` jours d'aujourd'hui (fuseau du magasin) : ISO, saisie et titre de la vue Jour. */
function targetDay(offset) {
  const format = (date, options) => new Intl.DateTimeFormat("fr-FR", { timeZone: STORE_TZ, ...options }).format(date);
  let date = new Date(Date.now() + offset * 86_400_000);
  while (["samedi", "dimanche"].includes(format(date, { weekday: "long" }))) date = new Date(date.getTime() + 86_400_000);
  const [dd, mm, yyyy] = format(date, { day: "2-digit", month: "2-digit", year: "numeric" }).split("/");
  const long = format(date, { weekday: "long", day: "numeric", month: "long", year: "numeric" });
  return { iso: `${yyyy}-${mm}-${dd}`, fr: `${dd}/${mm}/${yyyy}`, label: long[0].toUpperCase() + long.slice(1) };
}

/** Zone cliente de la fenêtre Tauri (résolution d'essai), sans capture. */
function resize(width, height) {
  const output = execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolve("e2e/capture-window.ps1"),
    "-Process", windowProcess, "-Out", "unused.png", "-ResizeOnly", "-ClientWidth", String(width), "-ClientHeight", String(height)], { encoding: "utf8" }).trim();
  check(output === `client ${width}x${height}`, `fenêtre ${width}×${height} (${output})`);
}

async function gotoDay(page, day) {
  for (let i = 0; i < 20 && !(await text(page)).includes(day.label); i++) {
    await click(page, buttonByName("Jour suivant"), "Jour suivant");
    await page.sleep(300);
  }
  await page.waitFor(hasText(day.label), { label: day.label });
}

/** Tabulations réelles jusqu'au bouton désigné (étiquette accessible exacte). */
async function tabTo(page, label, max = 40) {
  for (let i = 0; i < max; i++) {
    if ((await page.focused()) === `BUTTON ${label}`) return check(true, `clavier : focus sur « ${label} » (${i} tabulations)`);
    await page.press("Tab");
  }
  throw new Error(`ÉCHEC : « ${label} » non atteint au clavier`);
}

/**
 * Rapport coordonnées CDP / coordonnées CSS sous zoom : un mouvement de souris réel en (200, 200) est relu par la page
 * (clientX). Le glisser-déposer est ensuite exprimé dans l'unité attendue par le moteur.
 */
async function mouseScale(page) {
  await page.evaluate("window.__c3Mouse = null; addEventListener('mousemove', e => { window.__c3Mouse = [e.clientX, e.clientY]; }, { once: true })");
  await page.send("Input.dispatchMouseEvent", { type: "mouseMoved", x: 200, y: 200 });
  await page.waitFor("window.__c3Mouse !== null", { label: "étalonnage souris" });
  const [cx] = await page.evaluate("window.__c3Mouse");
  return 200 / cx;
}

/** Glisse le rendez-vous désigné de `slots` hauteurs (créneaux de sa durée), après l'avoir amené à l'écran. */
async function dragEvent(page, eventText, slots) {
  await page.evaluate(`(${eventFor(eventText)}).scrollIntoView({ block: 'center' })`);
  await page.sleep(300);
  const [x, y, h] = await page.evaluate(`(() => { const r = (${eventFor(eventText)}).getBoundingClientRect(); return [r.left + r.width / 2, r.top + 6, r.height]; })()`);
  const scale = await mouseScale(page);
  log("GLISSER", `départ CSS (${Math.round(x)}, ${Math.round(y)}), hauteur ${Math.round(h)} px, échelle CDP ${scale.toFixed(2)}`);
  await page.drag(x * scale, y * scale, x * scale, (y + slots * h) * scale);
}

/** Thème réellement appliqué : luminance du fond de la page. */
const backgroundLuminance = (page) =>
  page.evaluate(`(() => { const [r, g, b] = getComputedStyle(document.body).backgroundColor.match(/[\\d.]+/g).map(Number);
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255; })()`);

/** L'élément est visible dans la fenêtre après défilement de son conteneur (accessible sans défilement horizontal). */
const reachable = (page, expression) =>
  page.evaluate(`(() => { const e = ${expression}; if (!e) return false; e.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    const r = e.getBoundingClientRect(); return r.width > 0 && r.left >= 0 && r.top >= 0 && r.right <= innerWidth + 0.5 && r.bottom <= innerHeight + 0.5; })()`);

// ——— 03-D1 : sélecteurs de date (calendrier) et d'heure (liste), historique des rendez-vous sur la fiche ———
/** Déclencheur d'un sélecteur du formulaire (bouton nommé par l'étiquette du champ puis la valeur). */
const pickerButton = (label) =>
  `[...document.querySelectorAll('aside button[aria-haspopup]')].find(b => (b.getAttribute('aria-labelledby') || '').split(' ')
    .some(id => document.getElementById(id)?.textContent.replace('*', '').trim() === ${J(label)}))`;
const calendarOpen = "!!document.querySelector('[role=dialog][aria-label=\"Calendrier\"]')";
const timeListOpen = "!!document.querySelector('[role=listbox][aria-labelledby]') && !document.querySelector('[aria-label=\"Rechercher un client actif\"]')";

/** Libellé DayPicker d'un jour (« jeudi 16 juillet 2026 »), sans le jour de la semaine. */
const dayLabel = (iso) => {
  const [y, m, d] = iso.split("-").map(Number);
  return new Intl.DateTimeFormat("fr-FR", { timeZone: "UTC", day: "numeric", month: "long", year: "numeric" }).format(new Date(Date.UTC(y, m - 1, d, 12)));
};

async function chooseDropdown(page, label, value) {
  const ok = await page.evaluate(`(() => {
    const s = document.querySelector(${J(`[role=dialog][aria-label="Calendrier"] select[aria-label="${label}"]`)});
    if (!s) return false;
    Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(s, ${J(String(value))});
    s.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
  })()`);
  check(ok, `calendrier : ${label} = ${value}`);
  await page.sleep(200);
}

/** Date choisie dans le calendrier (années / mois par listes, jour par clic). */
async function pickDateNative(page, iso, { shot } = {}) {
  await click(page, pickerButton("Date"), "ouverture du calendrier");
  await page.waitFor(calendarOpen, { label: "calendrier" });
  const [y, m] = iso.split("-").map(Number);
  await chooseDropdown(page, "Choisir l'année", y);
  await chooseDropdown(page, "Choisir le mois", m - 1);
  if (shot) await shot();
  const day = `[...document.querySelectorAll('[role=dialog][aria-label="Calendrier"] button')].find(b => (b.getAttribute('aria-label') || '').includes(${J(dayLabel(iso))}))`;
  await click(page, day, `jour ${iso}`);
  await page.waitFor(`!${calendarOpen}`, { label: "calendrier fermé" });
}

/** Heure choisie dans la liste déroulante. */
async function pickTimeNative(page, time, { shot } = {}) {
  await click(page, pickerButton("Heure"), "ouverture de la liste des heures");
  await page.waitFor(timeListOpen, { label: "liste des heures" });
  if (shot) await shot();
  const option = `[...document.querySelectorAll('[role=listbox] [role=option]')].find(o => o.firstElementChild?.textContent === ${J(time)})`;
  await page.evaluate(`(${option}).scrollIntoView({ block: 'center' })`);
  await click(page, option, `heure ${time}`);
  await page.waitFor(`!document.querySelector('[role=listbox][aria-labelledby]')`, { label: "liste fermée" });
}

const pickerText = (label) => `(${pickerButton(label)})?.textContent.trim()`;
const tabByName = (label) => `[...document.querySelectorAll('[role=tab]')].find(t => t.textContent.trim() === ${J(label)})`;
const historyRows = "[...document.querySelectorAll('section.client-rdv tbody tr')]";

// ——— 03-D3 : historique des actions (journal d'audit 0005 appliqué) ———
/** Écriture ou lecture par l'API réelle avec la session du compte (jeton jamais affiché) : statut et corps. */
async function apiSend(account, method, path, body) {
  if (!apiSessions.has(account.userName)) await apiGet(account, "/api/clients?pageSize=1");
  const headers = { ...apiSessions.get(account.userName), ...(body ? { "Content-Type": "application/json" } : {}) };
  const response = await fetch(`${API}${path}`, { method, headers, body: body ? J(body) : undefined });
  const textBody = await response.text();
  return { status: response.status, json: textBody ? JSON.parse(textBody) : null };
}

const auditItems = "[...document.querySelectorAll('ol.client-history__list > li')]";
/** Entrées affichées de l'onglet Historique : action, auteur, champs. */
const readHistory = (page) =>
  page.evaluate(`${auditItems}.map(li => ({
    action: li.querySelector('.client-history__action')?.textContent.trim(),
    actor: li.querySelector('.client-history__actor')?.textContent.trim(),
    changes: [...li.querySelectorAll('.client-history__change')].map(c => c.textContent.trim()),
    open: !!li.querySelector('.client-history__open'),
  }))`);

async function openTab(page, label) {
  await page.waitFor(`!!${tabByName(label)}`, { label: `onglet ${label}` });
  // Onglets Radix : activation à l'appui du bouton de la souris (mousedown), pas au clic synthétique.
  await click(page, `(() => { const t = ${tabByName(label)}; t?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, button: 0 })); return t; })()`, `onglet ${label}`);
}

async function openFiche(page, first, upper) {
  await openClients(page);
  await page.evaluate("document.querySelector('.clients-search input').focus(); document.querySelector('.clients-search input').select()");
  await page.type(`${first} ${upper}`);
  await click(page, `(() => { const r = [...document.querySelectorAll('tbody tr')].find(r => r.textContent.includes(${J(first)})); return r?.querySelector('button[aria-label^="Consulter"]'); })()`, "Consulter la fiche");
  await page.waitFor(`document.querySelector('h1')?.textContent.includes(${J(first)})`, { label: "fiche client" });
}

const scenarios = {
  /**
   * 03-D3 — historique des actions sur PostgreSQL 18 réel (0005 appliquée) : Tauri → HTTPS → MMV.Api → PostgreSQL →
   * audit.events, données fictives d3n-* / « Fictif-D3N-* » sur mmv_v2_test (outil jetable), nettoyées après. Création,
   * modification, enregistrement sans changement (aucun événement), modification par un autre poste (auteur réel),
   * conflit 409 (aucun événement), archivage, réactivation, rendez-vous créé, conflit horaire refusé (aucun événement),
   * glisser-déposer, changement de responsable, annulation, rétablissement ; chronologie, auteurs et champs relus à
   * l'écran ET par l'API ; filtres ; refus sans audit.read. Options : --theme, --size, --zoom, --day-offset,
   * --first-name, --time, --captures all|key, --api, --process.
   */
  async "historique-03d3"(page) {
    const { agent, other, clientLastName: last } = credentials();
    const theme = option("--theme", "light");
    const [width, height] = option("--size", "1920x1080").split("x").map(Number);
    const zoom = Number(option("--zoom", "1"));
    const day = targetDay(Number(option("--day-offset", "3")));
    const first = option("--first-name", "Journal");
    const time = option("--time", "10:00");
    const all = option("--captures", "all") === "all";
    const tag = `${theme}_${width}x${height}_z${Math.round(zoom * 100)}`;
    const shot = (name, key = false) => (all || key) && capture(`d3_${tag}_${name}`);
    const upper = last.toUpperCase();
    const who = `${upper} ${first}`;
    const [hh, mm] = time.split(":").map(Number);
    const later = (minutes) => `${String(hh + Math.floor((mm + minutes) / 60)).padStart(2, "0")}:${String((mm + minutes) % 60).padStart(2, "0")}`;
    log("PARCOURS 03-D3", `${tag}, API ${API}, jour ${day.iso} ${time}, client ${who}`);

    resize(width, height);
    await signIn(page, agent);
    await setZoom(page, zoom);
    await setTheme(page, theme);

    // 1. Création du client (Ctrl+S).
    await openClients(page);
    await click(page, buttonByText("Nouveau client"), "Nouveau client");
    await page.waitFor("!!document.querySelector('form')", { label: "formulaire client" });
    await fillField(page, "Prénom", first);
    await fillField(page, "Nom", last);
    await fillField(page, "Téléphone", "0470 00 00 00");
    await fillField(page, "Ville", "Namur");
    osKeys("^s");
    await page.waitFor(hasText("Client créé"), { label: "client créé (Ctrl+S)" });
    await click(page, buttonByText("Voir la fiche"), "Voir la fiche");
    await page.waitFor(`document.querySelector('h1')?.textContent.includes(${J(first)})`, { label: "fiche" });
    const client = (await apiGet(agent, `/api/clients?q=${encodeURIComponent(last)}&status=all&pageSize=100`)).items.find((c) => c.firstName === first);
    check(!!client, `API : client enregistré (${client?.reference})`);
    const events = async () => (await apiGet(agent, `/api/audit/clients/${client.id}?pageSize=100`));
    check((await events()).totalCount === 1, "API : 1 événement (création)");

    // 2. Modification réelle (téléphone et ville).
    await click(page, buttonByText("Modifier"), "Modifier");
    await page.waitFor("!!document.querySelector('form')", { label: "formulaire de modification" });
    await fillField(page, "Téléphone", "0470 11 22 33");
    await fillField(page, "Ville", "Liège");
    await click(page, buttonByText("Enregistrer"), "Enregistrer");
    await page.waitFor(hasText("Modifications enregistrées"), { label: "modification enregistrée" });
    const afterEdit = (await apiSend(agent, "GET", `/api/clients/${client.id}`)).json;
    check((await events()).totalCount === 2, "API : 2 événements");

    // 3. Enregistrement SANS changement : succès affiché, aucune écriture (version, date) ni événement.
    await click(page, buttonByText("Modifier"), "Modifier (sans changement)");
    await page.waitFor("!!document.querySelector('form')", { label: "formulaire de modification" });
    await click(page, buttonByText("Enregistrer"), "Enregistrer sans changement");
    await page.waitFor(hasText("Modifications enregistrées"), { label: "succès cohérent" });
    const unchanged = (await apiSend(agent, "GET", `/api/clients/${client.id}`)).json;
    check(unchanged.version === afterEdit.version && unchanged.updatedAtUtc === afterEdit.updatedAtUtc,
      `API : version ${unchanged.version} et date de modification inchangées`);
    check((await events()).totalCount === 2, "API : aucun événement pour l'enregistrement sans changement");

    // 4. Autre poste (compte sans audit.read) : modification réelle ; puis l'écran, périmé, est refusé (409) sans événement.
    await click(page, buttonByText("Modifier"), "Modifier (formulaire ouvert avant l'autre poste)");
    await page.waitFor("!!document.querySelector('form')", { label: "formulaire de modification" });
    const otherPut = await apiSend(other, "PUT", `/api/clients/${client.id}`, {
      firstName: unchanged.firstName, lastName: unchanged.lastName, birthDate: unchanged.birthDate, phone: unchanged.phone, email: unchanged.email,
      addressLine: unchanged.addressLine, city: "Mons", postalCode: unchanged.postalCode, notes: unchanged.notes, version: unchanged.version,
    });
    check(otherPut.status === 200, "autre poste : ville modifiée (200)");
    await fillField(page, "Code postal", "4000");
    await click(page, buttonByText("Enregistrer"), "Enregistrer (version périmée)");
    await page.waitFor(hasText("modifié depuis un autre poste"), { label: "conflit 409 affiché" });
    shot("01_conflit_client");
    check((await events()).totalCount === 3, "API : 3 événements (le refus 409 n'en ajoute aucun)");
    await click(page, inDialog("Recharger la fiche"), "Recharger la fiche");
    await page.waitFor(`document.querySelector('h1')?.textContent.includes(${J(first)}) && !!${buttonByText("Archiver")}`, { label: "fiche rechargée" });

    // 5. Archivage puis réactivation.
    await click(page, buttonByText("Archiver"), "Archiver");
    await click(page, inDialog("Archiver le client"), "Archiver le client");
    await page.waitFor(hasText("Client archivé"), { label: "client archivé" });
    await click(page, buttonByText("Réactiver"), "Réactiver");
    await click(page, inDialog("Réactiver le client"), "Réactiver le client");
    await page.waitFor(hasText("Client réactivé"), { label: "client réactivé" });
    await page.waitFor(`!!${buttonByText("Archiver")}`, { label: "client actif" });

    // 6. Rendez-vous depuis la fiche ; tentative en conflit horaire refusée (aucun événement).
    await click(page, buttonByText("Prendre un rendez-vous"), "Prendre un rendez-vous");
    await page.waitFor(`document.querySelector('aside button[aria-haspopup="listbox"]')?.textContent.includes(${J(first)})`, { label: "client présélectionné" });
    await fillAppointment(page, { date: day.fr, time, duration: "30 minutes", reason: "Conseil / Devis", responsible: "D3N Responsable Un", notes: "Note fictive D3" });
    await click(page, inAside("Créer"), "Créer");
    await page.waitFor(hasText("Rendez-vous créé"), { label: "rendez-vous créé" });
    let mine = (await dayList(agent, day.iso)).find((i) => i.clientId === client.id);
    check(mine && storeWall(mine.startsAtUtc) === time, `API : rendez-vous ${time} ${STORE_TZ}`);
    const before = (await events()).totalCount;
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous (même responsable, créneau chevauchant)");
    await page.waitFor("!!document.querySelector('aside form')", { label: "formulaire" });
    await fillAppointment(page, { client: first, last, date: day.fr, time: later(15), duration: "30 minutes", reason: "Conseil / Devis", responsible: "D3N Responsable Un" });
    await click(page, inAside("Créer"), "Créer (conflit)");
    await page.waitFor(hasText("Conflit horaire"), { label: "conflit horaire refusé" });
    shot("02_conflit_horaire");
    check((await events()).totalCount === before, "API : le conflit horaire n'ajoute aucun événement");
    await abandonForm(page);

    // 7. Déplacement par glisser-déposer (+1 h), puis changement de responsable.
    await page.waitFor(`!!${eventFor(who)}`, { label: "rendez-vous visible" });
    await dragEvent(page, who, 2);
    await page.waitFor(hasText("Rendez-vous déplacé"), { label: "glisser-déposer accepté", timeoutMs: 15000 });
    await page.sleep(800);
    mine = (await dayList(agent, day.iso)).find((i) => i.id === mine.id);
    check(storeWall(mine.startsAtUtc) === later(60), `API : déplacé à ${later(60)}`);
    await openEvent(page, who);
    await click(page, inAside("Modifier"), "Modifier le rendez-vous");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Modifier le rendez-vous\"]')", { label: "formulaire de modification" });
    await setSelect(page, "Responsable", "D3N Responsable Deux");
    await click(page, inAside("Enregistrer"), "Enregistrer");
    await page.waitFor(hasText("Rendez-vous modifié"), { label: "responsable changé" });
    mine = (await dayList(agent, day.iso)).find((i) => i.id === mine.id);
    check(mine.responsibleDisplayName === "D3N Responsable Deux", "API : responsable Deux");

    // 8. Annulation (motif saisi) puis rétablissement.
    await click(page, inAside("Annuler le rendez-vous"), "Annuler le rendez-vous");
    await page.waitFor("!!document.querySelector('[role=alertdialog] textarea')", { label: "confirmation d'annulation" });
    await page.evaluate("document.querySelector('[role=alertdialog] textarea').focus()");
    await page.type("Motif fictif D3");
    await click(page, inDialog("Annuler le rendez-vous"), "confirmation de l'annulation");
    await page.waitFor(hasText("Rendez-vous annulé"), { label: "annulation" });
    await click(page, inAside("Rétablir le rendez-vous"), "Rétablir le rendez-vous");
    await page.waitFor(hasText("Rendez-vous rétabli"), { label: "rétablissement" });

    // 9. Fiche → Historique : chronologie, auteurs réels, champs ; vérité serveur.
    await click(page, `[...document.querySelectorAll('aside button')].find(b => b.textContent.trim() === 'Voir la fiche client')`, "Voir la fiche client");
    await page.waitFor(`document.querySelector('h1')?.textContent.includes(${J(first)}) && !!${buttonByText("Prendre un rendez-vous")}`, { label: "retour sur la fiche" });
    await openTab(page, "Historique");
    await page.waitFor(`${auditItems}.length === 10`, { label: "10 actions affichées" });
    const rows = await readHistory(page);
    rows.forEach((r, i) => log("HISTORIQUE", `${i + 1}. ${r.action} — ${r.actor} — [${r.changes.join(" | ")}]${r.open ? " — Voir le rendez-vous" : ""}`));
    const expected = ["Rendez-vous rétabli", "Rendez-vous annulé", "Changement de responsable", "Rendez-vous déplacé", "Rendez-vous créé",
      "Réactivation du dossier", "Archivage du dossier", "Modification des informations", "Modification des informations", "Création du dossier client"];
    check(J(rows.map((r) => r.action)) === J(expected), "chronologie : du plus récent au plus ancien, 10 actions réussies");
    check(rows.every((r, i) => r.actor === (i === 7 ? other.displayName : agent.displayName)), `auteurs réels (dont « ${other.displayName} » pour la modification de l'autre poste)`);
    check(J(rows[8].changes) === J(["Téléphone", "Ville"]) && J(rows[7].changes) === J(["Ville"]), "champs modifiés cités, valeurs personnelles masquées");
    check(rows[2].changes.some((c) => c === "Responsable : D3N Responsable Un → D3N Responsable Deux"), "changement de responsable : Un → Deux");
    check(rows[3].changes.some((c) => c.startsWith("Date et heure :") && c.includes("→")), `déplacement : ${rows[3].changes.join(" ; ")}`);
    check(J(rows[1].changes) === J(["Motif d'annulation"]), "motif d'annulation masqué");
    check(rows[4].changes.includes("Responsable : D3N Responsable Un") && rows[4].changes.includes("Notes"), "création : responsable nommé, notes masquées");
    check(rows.slice(0, 5).every((r) => r.open) && rows.slice(5).every((r) => !r.open), "« Voir le rendez-vous » sur les seules actions de rendez-vous");
    const served = await events();
    check(served.totalCount === 10 && J(served.items.map((e) => e.actorDisplayName)) === J(rows.map((r) => r.actor)), "API : 10 événements, mêmes auteurs dans le même ordre");
    const instants = served.items.map((e) => Date.parse(e.occurredAtUtc));
    check(instants.every((t, i) => i === 0 || instants[i - 1] >= t), "API : horodatages décroissants");
    const listed = await page.evaluate("document.querySelector('ol.client-history__list').innerText");
    check(["0470", "Namur", "Liège", "Mons", "4000", "Note fictive", "Motif fictif"].every((v) => !listed.includes(v)), "aucune valeur personnelle dans l'historique");
    check(await page.noHorizontalScroll(), "historique : aucun défilement horizontal");
    shot("03_historique", true);
    await setSelect(page, "Type d'action", "Rendez-vous");
    await page.waitFor(`${auditItems}.length === 5`, { label: "filtre Rendez-vous : 5 actions" });
    await setSelect(page, "Type d'action", "Dossier client");
    await page.waitFor(`${auditItems}.length === 5 && ${auditItems}.every(li => !li.querySelector('.client-history__open'))`, { label: "filtre Dossier client : 5 actions" });
    shot("04_historique_filtre_dossier");
    await setSelect(page, "Période", "7 derniers jours");
    await page.waitFor(`${auditItems}.length === 5`, { label: "période 7 jours : 5 actions" });

    // 10. Compte sans audit.read : onglet absent, lecture refusée par le serveur (403).
    const refused = await apiSend(other, "GET", `/api/audit/clients/${client.id}`);
    check(refused.status === 403, `API : lecture de l'historique refusée sans audit.read (${refused.status})`);
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
    await signIn(page, other);
    await setZoom(page, zoom);
    await openFiche(page, first, upper);
    await page.waitFor(`!!${tabByName("Informations")}`, { label: "onglets de la fiche" });
    check(!(await page.evaluate(`!!${tabByName("Historique")}`)), "onglet Historique absent sans audit.read");
    shot("05_sans_audit_read", true);
    check(Math.abs((await page.zoomFactor()) - zoom) < 0.01, `zoom ${Math.round(zoom * 100)} % effectif`);
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
    await apiLogoutAll();
    log("FIN", tag);
  },


  /**
   * 03-D1 — sélecteurs de date et d'heure, préremplissage depuis la grille, historique des rendez-vous sur la fiche,
   * corrections O-03-33 (focus après Échap), O-03-34 (barre d'état étroite), O-03-35 (panneau superposé), O-03-36
   * (« NOM Prénom »). Données fictives d1n-* / « Fictif-D1N-* » sur mmv_v2_test (outil jetable), nettoyées après.
   */
  async "parcours-03d1"(page) {
    const { agent, clientLastName: last } = credentials();
    const theme = option("--theme", "light");
    const [width, height] = option("--size", "1920x1080").split("x").map(Number);
    const zoom = Number(option("--zoom", "1"));
    const day = targetDay(Number(option("--day-offset", "3")));
    const time = option("--time", "10:30");
    const tag = `${theme}_${width}x${height}_z${Math.round(zoom * 100)}`;
    const shot = (name) => capture(`d1_${tag}_${name}`);
    const upper = last.toUpperCase();
    log("PARCOURS 03-D1", `${tag}, jour ${day.iso}, heure ${time}`);

    resize(width, height);
    await signIn(page, agent);
    await setZoom(page, zoom);
    await setTheme(page, theme);

    // O-03-34 : barre d'état sans libellé tronqué.
    const truncated = await page.evaluate(`[...document.querySelectorAll('.statusbar__item')].filter(i => i.scrollWidth > i.clientWidth + 1).length`);
    check(truncated === 0, "barre d'état : aucun libellé tronqué");
    const shortShown = await page.evaluate("getComputedStyle(document.querySelector('.statusbar__short')).display !== 'none'");
    log("BARRE", shortShown ? "libellés compacts (fenêtre étroite)" : "libellés complets");

    // 1. Nouveau rendez-vous : calendrier et liste des heures.
    await openRdv(page);
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous");
    await page.waitFor("!!document.querySelector('aside form')", { label: "formulaire" });
    check(await page.noHorizontalScroll(), "formulaire : aucun défilement horizontal");
    // O-03-35 : à 960 px CSS le panneau se superpose (agenda toujours visible à gauche, boutons à l'écran).
    const layout = await page.evaluate(`(() => { const a = document.querySelector('aside.rdv-side').getBoundingClientRect();
      const c = document.querySelector('section.rdv-cal').getBoundingClientRect(); return { asideTop: a.top, calBottom: c.bottom, calVisible: a.left - c.left }; })()`);
    check(layout.asideTop < layout.calBottom && layout.calVisible > 150, `panneau à côté de l'agenda (agenda visible sur ${Math.round(layout.calVisible)} px)`);
    check(await reachable(page, inAside("Créer")), "« Créer » accessible à l'écran");
    shot("01_formulaire");
    await pickDateNative(page, day.iso, { shot: () => shot("02_calendrier") });
    check((await page.evaluate(pickerText("Date"))) === day.fr, `date affichée ${day.fr}`);
    await pickTimeNative(page, time, {
      shot: async () => {
        const info = await page.evaluate(`(() => { const l = document.querySelector('[role=listbox][aria-labelledby]');
          const a = document.getElementById(l.getAttribute('aria-activedescendant')); const lr = l.getBoundingClientRect(); const ar = a.getBoundingClientRect();
          return { count: l.querySelectorAll('[role=option]').length, height: lr.height, first: l.querySelector('[role=option]').textContent,
            active: a.firstElementChild.textContent, visible: ar.top >= lr.top - 1 && ar.bottom <= lr.bottom + 1 }; })()`);
        check(info.visible, `heure proposée visible à l'ouverture (${info.active})`);
        check(info.count === 288 && info.first === "00:00", `liste : journée entière au pas de 5 minutes (${info.count} heures)`);
        check(info.height <= 300, `liste défilante bornée (${Math.round(info.height)} px)`);
        shot("03_heures");
      },
    });
    check((await page.evaluate(pickerText("Heure"))) === time, `heure affichée ${time}`);
    await pickClient(page, "Existant", last);
    await setSelect(page, "Durée", "30 minutes");
    await setSelect(page, "Motif", "Conseil / Devis");
    await setSelect(page, "Responsable", "D1N Responsable Un");
    shot("04_saisie");
    await click(page, inAside("Créer"), "Créer");
    await page.waitFor(hasText("Rendez-vous créé"), { label: "Rendez-vous créé" });
    check((await text(page)).includes(`${upper} Existant ·`), "notice « NOM Prénom » (O-03-36)");
    const created = (await dayList(agent, day.iso)).find((item) => item.clientFirstName === "Existant" && item.status === "planned" && storeWall(item.startsAtUtc) === time);
    check(created && storeWall(created.startsAtUtc) === time, `API : rendez-vous à ${time} heure du magasin (${created?.startsAtUtc})`);
    shot("05_cree");

    // 2. Créneau sélectionné sur la grille → date et heure préremplies.
    await gotoDay(page, day);
    await page.evaluate("document.querySelector('section[aria-label=\"Agenda\"] .rdv-fc__slot[data-time=\"15:00:00\"]').scrollIntoView({ block: 'center' })");
    await page.sleep(300);
    const lane = await page.evaluate(`(() => { const r = document.querySelector('section[aria-label=\"Agenda\"] .rdv-fc__slot[data-time="15:00:00"]').getBoundingClientRect();
      return [r.left + r.width * 0.6, r.top + 3, r.bottom - 3]; })()`);
    const scale = await mouseScale(page);
    await page.drag(lane[0] * scale, lane[1] * scale, lane[0] * scale, lane[2] * scale);
    await page.waitFor("!!document.querySelector('aside form')", { label: "formulaire prérempli" });
    check((await page.evaluate(pickerText("Date"))) === day.fr && (await page.evaluate(pickerText("Heure"))) === "15:00", `préremplissage depuis la grille : ${day.fr} 15:00`);
    shot("06_preremplissage");
    await abandonForm(page);

    // 3. O-03-33 : Échap dans la confirmation d'annulation rend le focus au bouton.
    await openEvent(page, `${upper} Existant`);
    await tabTo(page, "Annuler le rendez-vous");
    osKeys("{ENTER}");
    await page.waitFor("!!document.querySelector('[role=alertdialog]')", { label: "confirmation d'annulation" });
    osKeys("{ESC}");
    await page.waitFor("!document.querySelector('[role=alertdialog]')", { label: "confirmation fermée" });
    await page.sleep(300);
    check((await page.focused()) === "BUTTON Annuler le rendez-vous", `focus rendu au bouton (${await page.focused()})`);
    check((await dayList(agent, day.iso)).some((item) => item.id === created.id && item.status === "planned"), "rien n'a été annulé");
    await page.press("Escape");

    // 4. Fiche client : onglet Rendez-vous (historique servi par l'API).
    await openClients(page);
    await page.evaluate("document.querySelector('.clients-search input').focus(); document.querySelector('.clients-search input').select()");
    await page.type(`Historique ${last}`);
    await click(page, buttonByName(`Consulter ${upper} Historique`), "Consulter la fiche");
    await page.waitFor(`!!${tabByName("Rendez-vous")}`, { label: "onglet Rendez-vous" });
    // Onglets Radix : activation à l'appui du bouton de la souris (mousedown), pas au clic synthétique.
    await click(page, `(() => { const t = ${tabByName("Rendez-vous")}; t?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, button: 0 })); return t; })()`, "onglet Rendez-vous");
    await page.waitFor(`${historyRows}.length === 2`, { label: "rendez-vous à venir" });
    const chips = await page.evaluate("[...document.querySelectorAll('.client-rdv__scopes button')].map(b => b.textContent.trim()).join(' | ')");
    check(chips === "À venir (2) | Passés (3) | Annulés (1) | Tous (6)", `catégories : ${chips}`);
    check(await page.noHorizontalScroll(), "fiche : aucun défilement horizontal");
    shot("07_fiche_a_venir");
    await click(page, buttonByText("Passés (3)"), "Passés");
    await page.waitFor(`${historyRows}.length === 3 && ${historyRows}.every(r => r.textContent.includes('Passé'))`, { label: "rendez-vous passés" });
    shot("08_fiche_passes");
    await click(page, buttonByText("Annulés (1)"), "Annulés");
    await page.waitFor(`${historyRows}.length === 1 && ${historyRows}[0].textContent.includes('Annulé')`, { label: "rendez-vous annulé" });
    await click(page, buttonByText("À venir (2)"), "À venir");
    await page.waitFor(`${historyRows}.length === 2`, { label: "rendez-vous à venir" });
    await click(page, `${historyRows}[0].querySelector('button')`, "Voir le détail");
    await page.waitFor("document.querySelector('aside[aria-label^=\"Détail du rendez-vous de\"]')?.textContent.includes('Responsable')", { label: "détail dans l'agenda" });
    check(!!(await page.evaluate(`!!${buttonByText(`Retour à la fiche de ${upper} Historique`)}`)), "retour à la fiche proposé");
    shot("09_detail_depuis_fiche");
    await page.press("Escape");
    await page.waitFor(`document.querySelector('h1')?.textContent === ${J(`${upper} Historique`)}`, { label: "retour sur la fiche" });
    log("FIN", tag);
  },

  /**
   * 03-C — parcours fonctionnel complet : accueil → recherche (absent) → création du client (Ctrl+S) → fiche →
   * recherche (présent) → prise de rendez-vous depuis la fiche → modification → déplacement au clavier → glisser-déposer
   * → annulation au clavier (Échap puis confirmation) → rétablissement au clavier → retour à la fiche ; vérité serveur
   * relue après chaque écriture. Options : --theme light|dark, --size LxH, --zoom 1|1.25|1.5, --day-offset N,
   * --first-name, --captures all|key.
   */
  async "parcours-03c"(page) {
    const { agent, other, clientLastName: last } = credentials();
    const theme = option("--theme", "light");
    const [width, height] = option("--size", "1920x1080").split("x").map(Number);
    const zoom = Number(option("--zoom", "1"));
    const day = targetDay(Number(option("--day-offset", "2")));
    const first = option("--first-name", "Parcours");
    const all = option("--captures", "all") === "all";
    const tag = `${theme}_${width}x${height}_z${Math.round(zoom * 100)}`;
    const shot = (name, key = false) => (all || key) && capture(`c3_${tag}_${name}`);
    const who = `${first} ${last}`;
    log("PARCOURS", `${tag}, jour ${day.iso}`);

    resize(width, height);
    // La connexion recharge la page, ce qui ramène WebView2 à 100 % : le zoom est réglé APRÈS la connexion.
    await signIn(page, agent);
    await setZoom(page, zoom);
    await setTheme(page, theme);

    // 1. Accueil.
    await page.evaluate(`${navButton("Accueil")}.click()`);
    await page.waitFor("!!document.getElementById('home-title')", { label: "accueil" });
    check(await page.noHorizontalScroll(), "accueil : aucun défilement horizontal");
    shot("01_accueil");

    // 2. Recherche : client absent.
    await openClients(page);
    await page.waitFor("!!document.querySelector('table')", { label: "liste réelle" });
    await page.evaluate("document.querySelector('.clients-search input').focus(); document.querySelector('.clients-search input').select()");
    await page.type(who);
    await page.waitFor(hasText("Aucun résultat pour"), { label: "client absent" });
    shot("02_recherche_absent");

    // 3. Création du client, enregistrement au clavier (Ctrl+S, frappe système).
    await click(page, buttonByText("Nouveau client"), "Nouveau client");
    await page.waitFor("!!document.querySelector('form')", { label: "formulaire client" });
    await fillField(page, "Prénom", first);
    await fillField(page, "Nom", last);
    await fillField(page, "Téléphone", "0470 00 00 00");
    shot("03_client_formulaire");
    osKeys("^s");
    await page.waitFor(hasText("Client créé"), { label: "client créé (Ctrl+S)" });
    check(/CLI-\d{6,18}/.test(await text(page)), "référence attribuée par le serveur");
    await click(page, buttonByText("Voir la fiche"), "Voir la fiche");
    await page.waitFor(`document.querySelector('h1')?.textContent.includes(${J(first)})`, { label: "fiche" });
    shot("04_fiche", true);
    const found = (await apiGet(agent, `/api/clients?q=${encodeURIComponent(last)}`)).items;
    check(found.some((c) => c.firstName === first), "API : client enregistré");

    // 4. Recherche : client retrouvé (sans casse), fiche rouverte depuis la liste.
    await openClients(page);
    await page.evaluate("document.querySelector('.clients-search input').focus(); document.querySelector('.clients-search input').select()");
    await page.type(who.toLowerCase());
    const row = `[...document.querySelectorAll('tbody tr')].find(r => r.textContent.includes(${J(first)}))`;
    await page.waitFor(`!!${row} && document.querySelectorAll('tbody mark').length >= 1`, { label: "client retrouvé, surligné" });
    shot("05_recherche_trouve");
    await click(page, `(${row})?.querySelector('button[aria-label^="Consulter"]')`, "Consulter");
    await page.waitFor(`!!${buttonByText("Prendre un rendez-vous")}`, { label: "fiche client" });

    // 5. Prise de rendez-vous depuis la fiche : client présélectionné, aucune seconde recherche.
    await click(page, buttonByText("Prendre un rendez-vous"), "Prendre un rendez-vous");
    await page.waitFor(`document.querySelector('aside button[aria-haspopup="listbox"]')?.textContent.includes(${J(first)})`, { label: "client présélectionné" });
    check(!(await page.evaluate("!!document.querySelector('[aria-label=\"Rechercher un client actif\"]')")), "aucune seconde recherche");
    check(!(await text(page)).includes("Heure incohérente"), "décalages serveur = poste");
    await fillAppointment(page, { date: day.fr, time: "10:00", duration: "30 minutes", reason: "Conseil / Devis", responsible: "C3N Responsable Un", notes: "Note de parcours fictive" });
    check(await reachable(page, inAside("Créer")), "« Créer » accessible à l'écran");
    shot("06_rdv_formulaire", true);
    await click(page, inAside("Créer"), "Créer");
    await page.waitFor(hasText("Rendez-vous créé"), { label: "rendez-vous créé" });
    let mine = (await dayList(agent, day.iso)).find((i) => i.clientFirstName === first);
    check(mine && storeWall(mine.startsAtUtc) === "10:00" && mine.responsibleDisplayName === "C3N Responsable Un", `API : 10:00 ${STORE_TZ} (${mine?.startsAtUtc}), responsable Un`);
    const firstReason = mine.reasonCode;
    shot("07_rdv_cree");

    // 6. Modification : motif et notes ; auteur de la dernière modification.
    await openEvent(page, who);
    await click(page, inAside("Modifier"), "Modifier");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Modifier le rendez-vous\"]')", { label: "formulaire de modification" });
    await setSelect(page, "Motif", "SAV / Réparation");
    await fillField(page, "Notes", "Note modifiée (parcours)");
    await click(page, inAside("Enregistrer"), "Enregistrer");
    await page.waitFor(hasText("Rendez-vous modifié"), { label: "modification" });
    await page.waitFor(hasText("par C3N Accueil"), { label: "auteur de la dernière modification" });
    mine = (await dayList(agent, day.iso)).find((i) => i.clientFirstName === first);
    check(mine.reasonCode !== firstReason && storeWall(mine.startsAtUtc) === "10:00", "API : motif modifié, créneau inchangé");
    shot("08_rdv_modifie");

    // 7. Déplacement au clavier (commande « Déplacer », validation par Entrée) : 10:00 → 12:00.
    await click(page, inAside("Déplacer"), "Déplacer");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Déplacer le rendez-vous\"]')", { label: "formulaire de déplacement" });
    await fillField(page, "Heure", "12:00");
    await page.press("Enter");
    await page.waitFor(hasText("Rendez-vous déplacé"), { label: "déplacement au clavier" });
    mine = (await dayList(agent, day.iso)).find((i) => i.clientFirstName === first);
    check(storeWall(mine.startsAtUtc) === "12:00", `API : déplacé à 12:00 (${mine.startsAtUtc})`);

    // 8. Glisser-déposer réel (souris) : 12:00 → 13:00.
    await click(page, inAside("Fermer le détail"), "fermeture du détail");
    await page.waitFor(`!!${eventFor(who)}`, { label: "rendez-vous visible" });
    await dragEvent(page, who, 2);
    await page.waitFor(hasText("Rendez-vous déplacé"), { label: "glisser-déposer accepté", timeoutMs: 15000 });
    await page.sleep(800);
    mine = (await dayList(agent, day.iso)).find((i) => i.clientFirstName === first);
    check(storeWall(mine.startsAtUtc) === "13:00", `API : déposé à 13:00 (${mine.startsAtUtc})`);
    shot("09_glisser_depose", true);

    // 9. Annulation au clavier : Échap referme la confirmation sans effet, puis motif saisi et confirmation par Entrée.
    await openEvent(page, who);
    await page.evaluate(`(${inAside("Modifier")}).focus()`);
    await tabTo(page, "Annuler le rendez-vous");
    await page.press("Enter");
    await page.waitFor("!!document.querySelector('[role=alertdialog]')", { label: "confirmation d'annulation" });
    check(await page.evaluate("!!document.activeElement?.closest('[role=alertdialog]')"), "clavier : focus piégé dans la confirmation");
    await page.press("Escape");
    await page.waitFor("!document.querySelector('[role=alertdialog]')", { label: "Échap : confirmation refermée" });
    log("FOCUS après Échap", (await page.focused()) ?? "aucun");
    check((await dayList(agent, day.iso)).find((i) => i.clientFirstName === first).status === "planned", "API : Échap sans effet");
    await tabTo(page, "Annuler le rendez-vous", 5).catch(async () => { await page.evaluate(`(${inAside("Annuler le rendez-vous")}).focus()`); });
    await page.press("Enter");
    await page.waitFor("!!document.querySelector('[role=alertdialog] textarea')", { label: "confirmation rouverte" });
    for (let i = 0; i < 6 && (await page.evaluate("document.activeElement?.tagName")) !== "TEXTAREA"; i++) await page.press("Tab");
    await page.type("Client indisponible (fictif)");
    await tabTo(page, "Annuler le rendez-vous", 8);
    check(await page.evaluate("!!document.activeElement?.closest('[role=alertdialog]')"), "clavier : bouton de confirmation du dialogue");
    await page.press("Enter");
    await page.waitFor(hasText("Rendez-vous annulé"), { label: "annulation" });
    await page.waitFor(hasText("Annulé le"), { label: "auteur et date de l'annulation" });
    check((await dayList(agent, day.iso)).find((i) => i.clientFirstName === first).status === "cancelled", "API : annulé");
    shot("10_rdv_annule", true);

    // 10. Rétablissement au clavier.
    await page.evaluate(`(${inAside("Rétablir le rendez-vous")}).focus()`);
    check((await page.focused()) === "BUTTON Rétablir le rendez-vous", "clavier : focus sur « Rétablir le rendez-vous »");
    await page.press("Enter");
    await page.waitFor(hasText("Rendez-vous rétabli"), { label: "rétablissement" });
    mine = (await dayList(agent, day.iso)).find((i) => i.clientFirstName === first);
    check(mine.status === "planned" && storeWall(mine.startsAtUtc) === "13:00", "API : replanifié à 13:00");

    // 11. Retour à la fiche depuis le détail.
    await click(page, `[...document.querySelectorAll('aside button')].find(b => b.textContent.trim() === 'Voir la fiche client')`, "Voir la fiche client");
    await page.waitFor(`document.querySelector('h1')?.textContent.includes(${J(first)}) && !!${buttonByText("Prendre un rendez-vous")}`, { label: "retour sur la fiche" });
    check(await page.noHorizontalScroll(), "fiche : aucun défilement horizontal");
    shot("11_retour_fiche", true);

    // 12. Autre poste : la vérité serveur est la même.
    const seen = (await dayList(other, day.iso)).find((i) => i.clientFirstName === first);
    check(seen?.status === "planned" && storeWall(seen.startsAtUtc) === "13:00", "API (autre poste) : rendez-vous planifié à 13:00");
    check(Math.abs((await page.zoomFactor()) - zoom) < 0.01, `zoom ${Math.round(zoom * 100)} % conservé pendant tout le parcours`);
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /**
   * 03-C — matrice d'affichage : 2 thèmes × 1440×900 / 1920×1080 × zooms 100 / 125 / 150 %. Pour chaque combinaison :
   * accueil, liste Clients, fiche, agenda Jour et Semaine, formulaire de rendez-vous ; aucun défilement horizontal,
   * actions principales accessibles, thème effectivement appliqué ; une capture (agenda + formulaire) par combinaison.
   * Prérequis : un passage de « parcours-03c » (client « Parcours » et son rendez-vous du jour cible).
   */
  async "matrice-03c"(page) {
    const { agent, clientLastName: last } = credentials();
    const day = targetDay(Number(option("--day-offset", "2")));
    const first = option("--first-name", "Parcours");
    const who = `${first} ${last}`;
    await setZoom(page, 1);
    await signIn(page, agent);
    for (const theme of ["light", "dark"]) {
      await setTheme(page, theme);
      for (const [width, height] of [[1440, 900], [1920, 1080]]) {
        resize(width, height);
        for (const zoom of [1, 1.25, 1.5]) {
          await setZoom(page, zoom);
          const tag = `${theme}_${width}x${height}_z${Math.round(zoom * 100)}`;
          const css = await page.evaluate("[innerWidth, innerHeight]");
          log("COMBINAISON", `${tag} — vue CSS ${css[0]}×${css[1]}`);
          const luminance = await backgroundLuminance(page);
          check(theme === "dark" ? luminance < 0.3 : luminance > 0.7, `${tag} : thème appliqué (luminance du fond ${luminance.toFixed(2)})`);

          await page.evaluate(`${navButton("Accueil")}.click()`);
          await page.waitFor("!!document.getElementById('home-title')", { label: "accueil" });
          check(await page.noHorizontalScroll(), `${tag} accueil : aucun défilement horizontal`);

          await openClients(page);
          await page.waitFor("!!document.querySelector('table')", { label: "liste" });
          check(await page.noHorizontalScroll(), `${tag} clients : aucun défilement horizontal`);
          check(await reachable(page, "document.querySelector('.clients-search input')") && await reachable(page, buttonByText("Nouveau client")), `${tag} clients : recherche et « Nouveau client » accessibles`);
          await page.evaluate("document.querySelector('.clients-search input').focus(); document.querySelector('.clients-search input').select()");
          await page.type(who);
          const row = `[...document.querySelectorAll('tbody tr')].find(r => r.textContent.includes(${J(first)}))`;
          await page.waitFor(`!!${row}`, { label: "client du parcours" });
          await click(page, `(${row})?.querySelector('button[aria-label^="Consulter"]')`, `${tag} fiche`);
          await page.waitFor(`!!${buttonByText("Prendre un rendez-vous")}`, { label: "fiche" });
          check(await page.noHorizontalScroll(), `${tag} fiche : aucun défilement horizontal`);
          check(await reachable(page, buttonByText("Prendre un rendez-vous")), `${tag} fiche : « Prendre un rendez-vous » accessible`);

          await openRdv(page);
          await gotoDay(page, day);
          await page.waitFor(`!!${eventFor(who)}`, { label: "rendez-vous du parcours" });
          check(!(await text(page)).includes("Heure incohérente"), `${tag} agenda : décalages serveur = poste`);
          check(await page.noHorizontalScroll(), `${tag} agenda Jour : aucun défilement horizontal`);
          check(await reachable(page, eventFor(who)), `${tag} agenda Jour : rendez-vous accessible`);
          await click(page, buttonByName("Semaine"), `${tag} vue Semaine`);
          await page.waitFor(hasText("Semaine du"), { label: "vue Semaine" });
          check(await page.noHorizontalScroll(), `${tag} agenda Semaine : aucun défilement horizontal`);
          await click(page, buttonByName("Jour"), `${tag} vue Jour`);
          await click(page, buttonByText("Nouveau rendez-vous"), `${tag} Nouveau rendez-vous`);
          await page.waitFor("!!document.querySelector('aside[aria-label=\"Nouveau rendez-vous\"]')", { label: "formulaire" });
          check(await page.noHorizontalScroll(), `${tag} formulaire : aucun défilement horizontal`);
          check(await reachable(page, inAside("Créer")) && await reachable(page, inAside("Annuler")), `${tag} formulaire : « Créer » et « Annuler » accessibles`);
          await page.evaluate("document.querySelector('aside[aria-label=\"Nouveau rendez-vous\"]').scrollTop = 0");
          capture(`c3_matrice_${tag}`);
          await abandonForm(page);
        }
      }
    }
    await setZoom(page, 1);
    resize(1920, 1080);
    await setTheme(page, "light");
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /**
   * Garde-fou B2-REV-03 sur la divergence RÉELLE de ce poste (O-03-22) : MMV.Api de TEST configuré en
   * Africa/Casablanca (essai du garde-fou, pas la configuration du magasin). Windows place UTC+0 dès le 01/01/2026,
   * WebView2 le 15/02/2026 : la semaine du 05/01/2026 doit être bloquée et une saisie au 15/01/2026 refusée par le
   * serveur ; la semaine courante (données concordantes) reste utilisable.
   */
  async "rdv-b3-guard"(page) {
    const { agent, clientLastName: last } = credentials();
    await setZoom(page, 1);
    await signIn(page, agent);
    await setTheme(page, "light");
    await openRdv(page);
    await click(page, buttonByName("Semaine"), "vue Semaine");
    await page.waitFor(hasText("Semaine du"), { label: "vue Semaine" });
    check(!(await text(page)).includes("Heure incohérente"), "semaine courante (Casablanca) : décalages Windows = WebView2");
    for (let i = 0; i < 60 && !(await text(page)).includes("Semaine du 5 au 11 janvier 2026"); i++) {
      await click(page, buttonByName("Semaine précédente"), `Semaine précédente (${i + 1})`);
      await page.sleep(120);
    }
    await page.waitFor(hasText("Semaine du 5 au 11 janvier 2026"), { label: "semaine du 05/01/2026" });
    await page.waitFor(hasText("Heure incohérente entre ce poste et le serveur MMV"), { label: "agenda de la période bloqué" });
    check((await text(page)).includes("UTC selon le serveur, UTC+1 selon ce poste"), "écart affiché : serveur UTC, poste UTC+1");
    check(!(await page.evaluate(`!!${buttonByText("Nouveau rendez-vous")}`)), "aucune création proposée sur la période bloquée");
    capture("rdv_guard_week_blocked_1920x1080", { width: 1920, height: 1080 });

    await click(page, buttonByText("Aujourd'hui"), "Aujourd'hui");
    await page.waitFor("!document.body.innerText.includes('Heure incohérente')", { label: "semaine courante débloquée" });
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous");
    // 03-C : client et responsable paramétrables (jeu de données c3n-*) ; valeurs par défaut = jeu 03-B3.
    await fillAppointment(page, { client: option("--client-first", "Natif"), last, date: "15/01/2026", time: "10:00", duration: "30 minutes", reason: "Conseil / Devis",
      responsible: option("--responsible", "B3N Responsable Deux") });
    await click(page, inAside("Créer"), "Créer (15/01/2026)");
    await page.waitFor("document.querySelector('aside form')?.textContent.includes(\"L'heure de ce poste diffère de celle du serveur MMV\")", { label: "refus serveur time_zone_mismatch" });
    capture("rdv_guard_write_refused_1920x1080", { width: 1920, height: 1080 });
    await abandonForm(page);
    const january = await apiGet(agent, "/api/appointments?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z&status=all");
    check(january.items.length === 0, "API : aucun rendez-vous enregistré en janvier 2026");
    log("DÉCALAGES SERVEUR (semaine du 05/01/2026)", J(january.timeZoneOffsets.slice(0, 3)));
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /**
   * Parcours Rendez-vous complet (03-B3) : création, conflit, capacité d'accueil, modification, déplacement (clavier
   * et glisser-déposer), responsable retiré de l'éligibilité, annulation / rétablissement, création depuis la fiche
   * client sans seconde recherche, semaine du changement d'heure (25/10/2026) sans écart de fuseau.
   */
  async "rdv-b3"(page) {
    const { agent, other, suffix, clientLastName: last } = credentials();
    await setZoom(page, 1);
    await signIn(page, agent);
    await setTheme(page, "light");
    await openRdv(page);
    check(!(await text(page)).includes("Agenda non configuré"), "magasin configuré (pas de 503)");
    check(!(await text(page)).includes("Heure incohérente"), "aujourd'hui : décalages serveur = poste");
    for (let i = 0; i < 3 && !(await text(page)).includes("Lundi 19 octobre 2026"); i++) {
      await click(page, buttonByName("Jour suivant"), "Jour suivant");
      await page.sleep(400);
    }
    await page.waitFor(hasText("Lundi 19 octobre 2026"), { label: "lundi 19/10/2026" });
    capture("rdv_day_empty_1920x1080", { width: 1920, height: 1080 });

    // 1. Création d'un rendez-vous personnalisé.
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Nouveau rendez-vous\"]')", { label: "formulaire" });
    check((await text(page)).includes("Heures du magasin (Europe/Brussels)"), "fuseau du magasin affiché");
    await fillAppointment(page, { client: "Natif", last, date: "19/10/2026", time: "10:00", duration: "30 minutes", reason: "Conseil / Devis", responsible: "B3N Responsable Un", notes: "Note native fictive" });
    capture("rdv_form_filled_1920x1080", { width: 1920, height: 1080 });
    await click(page, inAside("Créer"), "Créer");
    await page.waitFor(hasText("Rendez-vous créé"), { label: "création" });
    let items = await dayList(agent, "2026-10-19");
    check(items.length === 1 && items[0].startsAtUtc.startsWith("2026-10-19T08:00:00") && items[0].responsibleDisplayName === "B3N Responsable Un",
      "API : 10:00 Bruxelles = 08:00Z, responsable Un");
    capture("rdv_created_1920x1080", { width: 1920, height: 1080 });

    // 2. Conflit horaire (même responsable).
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous");
    await fillAppointment(page, { client: "Accueil", last, date: "19/10/2026", time: "10:15", duration: "30 minutes", reason: "Conseil / Devis", responsible: "B3N Responsable Un" });
    await click(page, inAside("Créer"), "Créer (conflit)");
    await page.waitFor("document.querySelector('aside [role=alert]')?.textContent.includes('Conflit horaire')", { label: "conflit" });
    capture("rdv_conflict_1920x1080", { width: 1920, height: 1080 });
    await abandonForm(page);

    // 3. Capacité d'accueil (1) : une place prise, la seconde refusée.
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous");
    await fillAppointment(page, { client: "Accueil", last, date: "19/10/2026", time: "11:00", duration: "30 minutes", reason: "Retrait de commande", responsible: "Non attribué (accueil)" });
    await click(page, inAside("Créer"), "Créer (accueil)");
    await page.waitFor(hasText("Rendez-vous créé"), { label: "accueil 11:00" });
    await click(page, buttonByText("Nouveau rendez-vous"), "Nouveau rendez-vous");
    await fillAppointment(page, { client: "Fiche", last, date: "19/10/2026", time: "11:15", duration: "30 minutes", reason: "SAV / Réparation", responsible: "Non attribué (accueil)" });
    await click(page, inAside("Créer"), "Créer (accueil complet)");
    await page.waitFor("document.querySelector('aside [role=alert]')?.textContent.includes(\"Capacité d'accueil atteinte\")", { label: "capacité atteinte" });
    capture("rdv_capacity_full_1920x1080", { width: 1920, height: 1080 });
    await abandonForm(page);

    // 4. Modification (notes) : auteur et date de la dernière modification affichés.
    await openEvent(page, `Natif ${last}`);
    await click(page, inAside("Modifier"), "Modifier");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Modifier le rendez-vous\"]')", { label: "formulaire de modification" });
    await fillField(page, "Notes", "Note modifiée");
    await click(page, inAside("Enregistrer"), "Enregistrer");
    await page.waitFor(hasText("Rendez-vous modifié"), { label: "modification" });
    await page.waitFor(hasText("par B3N Agent"), { label: "auteur de la dernière modification" });
    capture("rdv_detail_modified_1920x1080", { width: 1920, height: 1080 });

    // 5. Déplacement au clavier (commande « Déplacer ») : 10:00 → 12:00.
    await click(page, inAside("Déplacer"), "Déplacer");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Déplacer le rendez-vous\"]')", { label: "formulaire de déplacement" });
    await fillField(page, "Heure", "12:00");
    await page.press("Enter");
    await page.waitFor(hasText("Rendez-vous déplacé"), { label: "déplacement" });
    items = await dayList(agent, "2026-10-19");
    check(items.some((i) => i.clientFirstName === "Natif" && i.startsAtUtc.startsWith("2026-10-19T10:00:00")), "API : déplacé à 12:00 Bruxelles (10:00Z)");

    // 6. Glisser-déposer réel (souris) : 12:00 → 13:00 (deux hauteurs d'un rendez-vous de 30 min).
    await click(page, inAside("Fermer le détail"), "fermeture du détail");
    await page.waitFor(`!!${eventFor(`Natif ${last}`)}`, { label: "rendez-vous visible" });
    const [x, y, h] = await page.evaluate(`(() => { const r = (${eventFor(`Natif ${last}`)}).getBoundingClientRect(); return [r.left + r.width / 2, r.top + 6, r.height]; })()`);
    log("GLISSER", `départ (${Math.round(x)}, ${Math.round(y)}), hauteur ${Math.round(h)} px`);
    await page.drag(x, y, x, y + 2 * h);
    await page.waitFor(hasText("Rendez-vous déplacé"), { label: "glisser-déposer accepté", timeoutMs: 15000 });
    await page.sleep(800);
    items = await dayList(agent, "2026-10-19");
    const dropped = items.find((i) => i.clientFirstName === "Natif");
    check(dropped?.startsAtUtc.startsWith("2026-10-19T11:00:00"), `API : déposé à 13:00 Bruxelles (11:00Z) — obtenu ${dropped?.startsAtUtc}`);
    capture("rdv_drag_drop_1920x1080", { width: 1920, height: 1080 });

    // 7. Responsable retiré de l'éligibilité : déplacement refusé (B2-REV-01), notes toujours corrigeables.
    tool("eligibility", suffix, "off");
    await openEvent(page, `Natif ${last}`);
    await click(page, inAside("Déplacer"), "Déplacer (responsable retiré)");
    await page.waitFor("!!document.querySelector('aside[aria-label=\"Déplacer le rendez-vous\"]')", { label: "formulaire de déplacement" });
    await fillField(page, "Heure", "15:00");
    await page.press("Enter");
    await page.waitFor("document.querySelector('aside [role=alert]')?.textContent.includes('non habilité')", { label: "refus : responsable non habilité" });
    capture("rdv_responsible_ineligible_1920x1080", { width: 1920, height: 1080 });
    await abandonForm(page);
    items = await dayList(agent, "2026-10-19");
    check(items.find((i) => i.clientFirstName === "Natif")?.startsAtUtc.startsWith("2026-10-19T11:00:00"), "API : créneau inchangé");
    await openEvent(page, `Natif ${last}`);
    await click(page, inAside("Modifier"), "Modifier (responsable retiré)");
    await page.waitFor("[...document.querySelectorAll('aside select option')].some(o => o.selected && o.textContent.includes(\"n'est plus habilité\"))", { label: "responsable actuel conservé" });
    await fillField(page, "Notes", "Note après retrait");
    await click(page, inAside("Enregistrer"), "Enregistrer (notes)");
    await page.waitFor(hasText("Rendez-vous modifié"), { label: "notes corrigées malgré le retrait" });
    tool("eligibility", suffix, "on");

    // 8. Annulation avec motif, puis rétablissement.
    await click(page, inAside("Annuler le rendez-vous"), "Annuler le rendez-vous");
    await page.waitFor("!!document.querySelector('[role=alertdialog]')", { label: "confirmation d'annulation" });
    await page.evaluate("document.querySelector('[role=alertdialog] textarea').focus()");
    await page.type("Client indisponible (fictif)");
    await click(page, inDialog("Annuler le rendez-vous"), "annulation confirmée");
    await page.waitFor(hasText("Rendez-vous annulé"), { label: "annulation" });
    await page.waitFor(hasText("Annulé le"), { label: "auteur et date de l'annulation" });
    capture("rdv_cancelled_1920x1080", { width: 1920, height: 1080 });
    await click(page, inAside("Rétablir le rendez-vous"), "Rétablir");
    await page.waitFor(hasText("Rendez-vous rétabli"), { label: "rétablissement" });
    items = await dayList(agent, "2026-10-19");
    check(items.find((i) => i.clientFirstName === "Natif")?.status === "planned", "API : rendez-vous replanifié");

    // 9. Création depuis la fiche client : client conservé, aucune seconde recherche, retour à la fiche.
    await openClients(page);
    const consult = `[...document.querySelectorAll('tbody tr')].find(r => r.textContent.includes('Fiche'))?.querySelector('button[aria-label^="Consulter"]')`;
    await page.waitFor(`!!${consult}`, { label: "ligne du client Fiche" });
    await click(page, consult, "fiche du client Fiche");
    await page.waitFor(`!!${buttonByText("Prendre un rendez-vous")}`, { label: "fiche client" });
    await click(page, buttonByText("Prendre un rendez-vous"), "Prendre un rendez-vous");
    await page.waitFor("document.querySelector('aside button[aria-haspopup=\"listbox\"]')?.textContent.includes('Fiche')", { label: "client présélectionné" });
    check(!(await page.evaluate("!!document.querySelector('[aria-label=\"Rechercher un client actif\"]')")), "aucune seconde recherche");
    capture("rdv_from_client_form_1920x1080", { width: 1920, height: 1080 });
    await fillAppointment(page, { date: "19/10/2026", time: "16:00", duration: "30 minutes", reason: "Prise de mesures", responsible: "B3N Responsable Deux" });
    await click(page, inAside("Créer"), "Créer depuis la fiche");
    await page.waitFor(hasText("Rendez-vous créé"), { label: "création depuis la fiche" });
    const back = `[...document.querySelectorAll('button')].find(b => b.textContent.trim().startsWith('Retour à la fiche de'))`;
    check(await page.evaluate(`!!${back}`), "retour à la fiche proposé");
    capture("rdv_from_client_created_1920x1080", { width: 1920, height: 1080 });
    await click(page, back, "Retour à la fiche");
    await page.waitFor(`!!${buttonByText("Prendre un rendez-vous")}`, { label: "retour sur la fiche" });

    // 10. Semaine du 19 au 25/10/2026 (recul d'heure le 25) : décalages serveur = poste, rendez-vous affichés.
    await openRdv(page);
    await click(page, buttonByName("Semaine"), "vue Semaine");
    await click(page, buttonByName("Semaine suivante"), "Semaine suivante");
    await page.waitFor(hasText("Semaine du 19 au 25 octobre 2026"), { label: "semaine du changement d'heure" });
    // Vue Semaine : nom abrégé (« N. Nom »).
    await page.waitFor(`!!${eventFor(`N. ${last}`)}`, { label: "rendez-vous de la semaine affichés" });
    check(!(await text(page)).includes("Heure incohérente"), "semaine du 25/10 : décalages Windows = WebView2");
    capture("rdv_week_dst_1920x1080", { width: 1920, height: 1080 });
    capture("rdv_week_dst_1440x900", { width: 1440, height: 900 });
    check(await page.noHorizontalScroll(), "1440×900 : aucun défilement horizontal");

    // Constat : données de fuseaux de CETTE WebView2 (Africa/Casablanca, Europe/Brussels), comparées à O-03-22.
    const tz = await page.evaluate(`(() => {
      const off = (z, t) => new Intl.DateTimeFormat('en-US', { timeZone: z, timeZoneName: 'longOffset' }).formatToParts(new Date(t)).find(p => p.type === 'timeZoneName').value;
      const scan = (z, from, to) => { const out = []; let p = off(z, from); for (let t = Date.parse(from); t < Date.parse(to); t += 15 * 60000) { const o = off(z, t); if (o !== p) { out.push(new Date(t).toISOString() + ' ' + p + '→' + o); p = o; } } return out; };
      return { userAgent: navigator.userAgent.match(/Edg\\/[0-9.]+/)?.[0], casablanca: scan('Africa/Casablanca', '2026-01-01T00:00:00Z', '2028-01-01T00:00:00Z'), brussels: scan('Europe/Brussels', '2026-01-01T00:00:00Z', '2028-01-01T00:00:00Z') };
    })()`);
    log("FUSEAUX WebView2", J(tz));
    await click(page, buttonByName("Jour"), "vue Jour");

    // 11. Autre poste : la vérité serveur est relue (nombre de rendez-vous planifiés du jour).
    items = await dayList(other, "2026-10-19");
    check(items.filter((i) => i.status === "planned").length === 3, "API (autre poste) : 3 rendez-vous planifiés le 19/10");
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /** Écran de connexion : focus initial, tabulation, validation, zoom — indépendant de la base. */
  async "login-states"(page) {
    await setZoom(page, 1);
    await freshLogin(page);
    check((await page.focused())?.includes("Identifiant"), "focus initial sur Identifiant");
    capture("login_initial_1920x1080", { width: 1920, height: 1080 });

    await page.press("Tab");
    check((await page.focused())?.includes("Mot de passe"), "Tab -> Mot de passe");
    await page.press("Tab");
    check((await page.focused())?.includes("Afficher le mot de passe"), "Tab -> Afficher le mot de passe");
    await page.press("Tab");
    check((await page.focused())?.includes("Se connecter"), "Tab -> Se connecter");
    await page.press("Tab", { shift: true });
    await page.press("Tab", { shift: true });
    await page.press("Tab", { shift: true });
    check((await page.focused())?.includes("Identifiant"), "Maj+Tab ×3 -> Identifiant");

    await page.press("Enter");
    await page.waitFor(hasText("Veuillez saisir votre identifiant."), { label: "validation" });
    check((await text(page)).includes("Veuillez saisir votre mot de passe."), "Entrée champs vides : deux messages");
    capture("login_validation_1920x1080", { width: 1920, height: 1080 });

    await freshLogin(page);
    capture("login_initial_1440x900", { width: 1440, height: 900 });
    for (const factor of [1.25, 1.5]) {
      await setZoom(page, factor);
      check(await page.noHorizontalScroll(), `1440×900 zoom ${factor * 100} % : aucun défilement horizontal`);
      capture(`login_1440x900_zoom${factor * 100}`);
    }
    await setZoom(page, 1);
  },

  /** API arrêtée : bandeau technique, champs non marqués. */
  async "login-api-down"(page) {
    await freshLogin(page);
    await page.evaluate("document.getElementById(document.querySelector('label[for]').htmlFor).focus()");
    await page.type("compte.inexistant");
    await page.press("Tab");
    await page.type("valeur-quelconque");
    await page.press("Enter");
    await page.waitFor(hasText("Connexion impossible pour le moment."), { label: "bandeau technique" });
    capture("login_api-down_1920x1080", { width: 1920, height: 1080 });
  },

  /** Identifiants incorrects sur l'API réelle (compte éphémère de TEST). */
  async "login-invalid"(page) {
    const { admin } = credentials();
    await freshLogin(page);
    await page.evaluate("document.getElementById(document.querySelector('label[for]').htmlFor).focus()");
    await page.type(admin.userName);
    await page.press("Tab");
    await page.type("mot-de-passe-errone");
    await page.press("Enter");
    await page.waitFor(hasText("Identifiant ou mot de passe incorrect."), { label: "refus neutre" });
    check(await page.evaluate("document.querySelector('input[type=password]').value === ''"), "mot de passe vidé");
    capture("login_invalid_1920x1080", { width: 1920, height: 1080 });
  },

  /** Shell réel : clair/sombre, ouvert/réduit, 1920 et 1440, zoom, clavier, comptes, déconnexion. */
  async shell(page) {
    const { admin } = credentials();
    await setZoom(page, 1);
    await signIn(page, admin);
    await page.waitFor(hasText("Base de données disponible"), { label: "barre d'état réelle" });
    check((await text(page)).includes("Serveur MMV joignable"), "barre d'état : serveur joignable (/health/live)");
    capture("shell_open_light_1920x1080", { width: 1920, height: 1080 });

    await page.evaluate(`document.querySelector('[aria-label="Réduire le menu"]').click()`);
    await page.sleep(300);
    capture("shell_collapsed_light_1920x1080");

    await page.evaluate(`document.querySelector('[aria-label^="Thème"]').click()`);
    await page.sleep(300);
    capture("shell_collapsed_dark_1920x1080");
    await page.evaluate(`document.querySelector('[aria-label="Ouvrir le menu"]').click()`);
    await page.sleep(300);
    capture("shell_open_dark_1920x1080");
    await page.evaluate(`document.querySelector('[aria-label^="Thème"]').click()`);
    await page.sleep(300);

    capture("shell_open_light_1440x900", { width: 1440, height: 900 });
    await page.evaluate(`document.querySelector('[aria-label="Réduire le menu"]').click()`);
    await page.sleep(300);
    capture("shell_collapsed_light_1440x900");
    await page.evaluate(`document.querySelector('[aria-label="Ouvrir le menu"]').click()`);
    for (const factor of [1.25, 1.5]) {
      await setZoom(page, factor);
      check(await page.noHorizontalScroll(), `shell 1440×900 zoom ${factor * 100} % : aucun défilement horizontal`);
      capture(`shell_1440x900_zoom${factor * 100}`);
    }
    await setZoom(page, 1);

    // Clavier : ordre de tabulation recommandé (états §7) depuis le début du document.
    await page.evaluate("document.activeElement && document.activeElement.blur()");
    const order = [];
    for (let i = 0; i < 5; i++) {
      await page.press("Tab");
      order.push(await page.focused());
    }
    log("ORDRE", order.join(" | "));
    check(order[0].includes("Thème") && order[1].includes("Menu utilisateur") && order[2].includes("Réduire le menu"),
      "Tab : Thème -> Menu utilisateur -> Réduire le menu");
    check(order[3].includes("Accueil"), "Tab : puis première entrée de navigation");
    await page.press("Enter");

    // Module futur et Comptes (API protégée réelle).
    await page.evaluate(`${navButton("Clients")}.click()`);
    await page.waitFor(hasText("Disponible dans une phase suivante"), { label: "module futur" });
    capture("shell_module-later_1920x1080", { width: 1920, height: 1080 });
    await page.evaluate(`${navButton("Comptes")}.click()`);
    await page.waitFor("document.querySelector('table')", { label: "liste des comptes" });
    capture("shell_accounts_1920x1080");
    await page.evaluate(`${navButton("Diagnostic technique")}.click()`);
    await page.waitFor(hasText("Serveur MMV et base de données disponibles"), { label: "diagnostic 01B" });
    capture("shell_diagnostic_1920x1080");

    // Déconnexion depuis la sidebar.
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "retour au login" });
    check(!(await text(page)).includes("expiré"), "déconnexion volontaire : pas de bandeau d'expiration");
    capture("logout_back-to-login_1920x1080");
  },

  /** Connexion puis attente d'une action serveur (révocation/403/désactivation) déclenchée hors fenêtre. */
  async "sign-in"(page) {
    const role = option("--account", "admin");
    await signIn(page, credentials()[role]);
    log("PRÊT", `session ouverte (compte éphémère « ${role} »)`);
  },

  /** Compte éphémère sans system.admin : entrée « Comptes » absente, accueil accessible, déconnexion. */
  async "user-no-admin"(page) {
    const { user } = credentials();
    await signIn(page, user);
    check(!(await page.evaluate(`!!${navButton("Comptes")}`)), "navigation : « Comptes » absente sans system.admin");
    capture("user-no-admin_home_1920x1080", { width: 1920, height: 1080 });
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "retour au login" });
  },

  /** Compte désactivé côté serveur : refus neutre, identique à un mot de passe erroné. */
  async "login-disabled"(page) {
    const { off } = credentials();
    await freshLogin(page);
    await page.evaluate("document.getElementById(document.querySelector('label[for]').htmlFor).focus()");
    await page.type(off.userName);
    await page.press("Tab");
    await page.type(off.password);
    await page.press("Enter");
    await page.waitFor(hasText("Identifiant ou mot de passe incorrect."), { label: "refus neutre (compte désactivé)" });
    check(!(await text(page)).includes("Bonjour,"), "aucune session ouverte");
    capture("login_disabled-account_1920x1080", { width: 1920, height: 1080 });
  },

  /** Après révocation serveur : la navigation ramène au login « session expirée ». */
  async "after-revocation"(page) {
    await page.evaluate(`${navButton("Diagnostic technique")}.click()`);
    await page.waitFor(hasText("Votre session a expiré."), { label: "session expirée" });
    capture("revoked_session-expired_1920x1080", { width: 1920, height: 1080 });
  },

  /** Après retrait de system.admin côté serveur : 403 -> écran « Accès non autorisé ». */
  async "after-permission-removed"(page) {
    await page.evaluate(`${navButton("Comptes")}.click()`);
    await page.waitFor(hasText("Accès non autorisé"), { label: "403 serveur" });
    capture("forbidden_403_1920x1080", { width: 1920, height: 1080 });
  },

  /** API arrêtée pendant la session : bandeau et barre d'état. */
  async "shell-api-down"(page) {
    await page.evaluate(`${navButton("Diagnostic technique")}.click()`);
    await page.waitFor(buttonByText("Revérifier"), { label: "diagnostic affiché" });
    await page.evaluate(`${buttonByText("Revérifier")}.click()`);
    await page.waitFor(hasText("Serveur MMV inaccessible."), { label: "bandeau serveur inaccessible" });
    capture("shell_api-down_1920x1080", { width: 1920, height: 1080 });
  },

  /** API redémarrée (session persistée côté serveur) : la session continue sans reconnexion. */
  async "shell-api-back"(page) {
    await page.waitFor(buttonByText("Réessayer"), { label: "bandeau serveur inaccessible" });
    await page.evaluate(`${buttonByText("Réessayer")}.click()`);
    await page.waitFor(`!${hasText("Serveur MMV inaccessible.")}`, { label: "bandeau levé" });
    await page.evaluate(`${navButton("Accueil")}.click()`);
    await page.sleep(800);
    check((await text(page)).includes("Bonjour,"), "session conservée après redémarrage de l'API");
    capture("shell_api-restarted_session-kept_1920x1080", { width: 1920, height: 1080 });
  },

  /** API sans base : barre d'état « base indisponible », session non validable sans faux succès. */
  async "shell-db-down"(page) {
    await page.evaluate(`${navButton("Diagnostic technique")}.click()`);
    await page.waitFor(buttonByText("Revérifier"), { label: "diagnostic affiché" });
    await page.evaluate(`${buttonByText("Revérifier")}.click()`);
    await page.waitFor(hasText("Base de données indisponible"), { label: "base indisponible" });
    capture("shell_db-down_1920x1080", { width: 1920, height: 1080 });
  },

  // ── Phase 03-A3 : module Clients sur la vraie chaîne Tauri → HTTPS → MMV.Api → PostgreSQL 18 (mmv_v2_test). ──
  // Comptes éphémères { admin (system.admin seul), read, write, full } ; clients exclusivement fictifs.

  /** system.admin seul : l'écran Clients affiche le refus serveur (403), aucune donnée. */
  async "clients-admin-only"(page) {
    await setZoom(page, 1);
    await signIn(page, credentials().admin);
    await setTheme(page, "light");
    await openClients(page);
    await page.waitFor(hasText("Accès non autorisé"), { label: "403 serveur" });
    check(!(await page.evaluate("!!document.querySelector('table')")), "aucune table affichée");
    capture("clients_forbidden_admin-only_1920x1080", { width: 1920, height: 1080 });
  },

  /** Compte complet : création, validation, fiche, recherche, archivage, réactivation, conflit, thèmes. */
  async "clients-full"(page) {
    const accounts = credentials();
    const suffix = accounts.suffix;
    await setZoom(page, 1);
    await signIn(page, accounts.full);
    await setTheme(page, "light");
    await openClients(page);
    await page.waitFor("document.querySelector('table')", { label: "liste réelle" });
    capture("clients_list_light_1920x1080", { width: 1920, height: 1080 });

    // Validation locale : aucune requête, messages sous les champs.
    await page.evaluate(`${buttonByText("Nouveau client")}.click()`);
    await page.waitFor(hasText("Nouveau client") + " && !!document.querySelector('form')", { label: "formulaire" });
    capture("clients_form_empty_1920x1080");
    await page.evaluate(`${buttonByText("Enregistrer")}.click()`);
    await page.waitFor(hasText("Veuillez corriger les erreurs du formulaire"), { label: "erreurs de validation" });
    check((await page.focused())?.includes("Prénom"), "focus sur le premier champ en erreur");
    capture("clients_form_errors_1920x1080");

    // Création réelle.
    await fillField(page, "Prénom", "Noémie");
    await fillField(page, "Nom", `Ducastel-${suffix}`);
    await fillField(page, "Date de naissance", "03/02/1990");
    await fillField(page, "Téléphone", "0471 22 33 44");
    await fillField(page, "Email", "noemie@exemple.test");
    await fillField(page, "Ville", "Namur");
    await page.waitFor(hasText("Modifications non enregistrées"), { label: "bandeau non enregistré" });
    capture("clients_form_filled_unsaved_1920x1080");
    await page.evaluate(`${buttonByText("Enregistrer")}.click()`);
    await page.waitFor(hasText("Client créé"), { label: "client créé" });
    check(/CLI-\d{6,18}/.test(await text(page)), "référence attribuée par le serveur affichée");
    capture("clients_create_success_1920x1080");
    await page.evaluate(`${buttonByText("Voir la fiche")}.click()`);
    await page.waitFor(`document.querySelector('h1')?.textContent.includes('Noémie')`, { label: "fiche" });
    capture("clients_detail_light_1920x1080");

    // Recherche sans accents, puis aucun résultat.
    await openClients(page);
    await page.waitFor("document.querySelector('table')", { label: "liste" });
    await page.evaluate("document.querySelector('.clients-search input').focus()");
    await page.type(`noemie ducastel-${suffix}`);
    await page.waitFor(`document.querySelectorAll('tbody tr').length === 1 && document.querySelectorAll('tbody mark').length >= 2`, { label: "résultat surligné (2 mots, sans accents)" });
    capture("clients_search_hit_1920x1080");
    await page.evaluate("document.querySelector('.clients-search input').select()");
    await page.type(`introuvable-${suffix}`);
    await page.waitFor(hasText("Aucun résultat pour"), { label: "aucun résultat" });
    capture("clients_search_empty_1920x1080");
    await page.evaluate(`${buttonByText("Effacer la recherche")}.click()`);
    await page.waitFor("document.querySelectorAll('tbody tr').length === 3", { label: "3 clients actifs (2 doublons + 1 créé)" });

    // Archivage depuis la liste puis inclusion des archivés.
    await page.evaluate(`${buttonByName(`Archiver DUCASTEL-${suffix.toUpperCase()} Noémie`)}.click()`);
    await page.waitFor("!!document.querySelector('[role=alertdialog]')", { label: "confirmation d'archivage" });
    capture("clients_archive_confirm_1920x1080");
    await page.evaluate(`${buttonByText("Archiver le client")}.click()`);
    await page.waitFor(hasText("a été retiré(e) des listes actives"), { label: "client archivé" });
    await page.waitFor("document.querySelectorAll('tbody tr').length === 2", { label: "liste active : 2" });
    capture("clients_archived_notice_1920x1080");
    await page.evaluate(`${buttonByText("Inclure les clients archivés")}.click()`);
    await page.waitFor("document.querySelectorAll('tbody tr').length === 3", { label: "actifs + archivés : 3" });
    capture("clients_list_with-archived_1920x1080");

    // Fiche archivée : lecture seule, réactivation.
    await page.evaluate(`${buttonByName(`Consulter DUCASTEL-${suffix.toUpperCase()} Noémie`)}.click()`);
    await page.waitFor(hasText("Consultation en lecture seule"), { label: "fiche archivée" });
    check(await page.evaluate(isDisabled("Modifier")), "fiche archivée : Modifier désactivé");
    capture("clients_detail_archived_1920x1080");
    await page.evaluate(`${buttonByText("Réactiver")}.click()`);
    await page.waitFor("!!document.querySelector('[role=alertdialog]')", { label: "confirmation de réactivation" });
    await page.evaluate(`${buttonByText("Réactiver le client")}.click()`);
    await page.waitFor(hasText("est de nouveau actif(ve)"), { label: "client réactivé" });
    await page.waitFor(`!(${isDisabled("Modifier")})`, { label: "Modifier réactivé" });

    // Conflit : formulaire ouvert, un autre poste enregistre, puis enregistrement ici -> 409 et aucune écriture.
    await page.evaluate(`${buttonByText("Modifier")}.click()`);
    await page.waitFor("!!document.querySelector('form')", { label: "formulaire de modification" });
    capture("clients_form_edit_1920x1080");
    const otherStatus = await otherPostUpdate(accounts.write, `ducastel-${suffix}`, "Arlon");
    check(otherStatus === 200, `autre poste : modification enregistrée (${otherStatus})`);
    await fillField(page, "Ville", "Mons");
    await page.evaluate(`${buttonByText("Enregistrer")}.click()`);
    await page.waitFor("!!document.querySelector('[role=alertdialog]')", { label: "conflit de version" });
    log("CONFLIT", (await page.evaluate("document.querySelector('[role=alertdialog]').innerText")).replace(/\s+/g, " ").slice(0, 200));
    capture("clients_conflict_409_1920x1080");
    await page.evaluate(`${buttonByText("Recharger la fiche")}.click()`);
    await page.waitFor(hasText("Arlon"), { label: "fiche rechargée avec la version de l'autre poste" });
    check(!(await text(page)).includes("Mons"), "aucune écriture de la version périmée");
    capture("clients_detail_after-conflict_1920x1080");

    // Thème sombre et 1440×900.
    await setTheme(page, "dark");
    await page.sleep(300);
    capture("clients_detail_dark_1920x1080");
    await openClients(page);
    await page.waitFor("document.querySelector('table')", { label: "liste" });
    capture("clients_list_dark_1920x1080");
    await setTheme(page, "light");
    await page.sleep(300);
    capture("clients_list_light_1440x900", { width: 1440, height: 900 });
    check(await page.noHorizontalScroll(), "liste 1440×900 : aucun défilement horizontal");
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /** Lecture seule : aucune action d'écriture proposée, motif affiché, fiche aux actions désactivées. */
  async "clients-read"(page) {
    await setZoom(page, 1);
    await signIn(page, credentials().read);
    await setTheme(page, "light");
    await openClients(page);
    await page.waitFor("document.querySelector('table')", { label: "liste en lecture" });
    check((await text(page)).includes("Consultation seule"), "motif « Consultation seule » affiché");
    check(!(await page.evaluate(`!!${buttonByText("Nouveau client")}`)), "« Nouveau client » absent");
    check(await page.evaluate("![...document.querySelectorAll('tbody button')].some(b => /^(Modifier|Archiver|Réactiver) /.test(b.getAttribute('aria-label') || ''))"),
      "aucune action Modifier/Archiver/Réactiver dans les lignes");
    capture("clients_list_read-only_1920x1080", { width: 1920, height: 1080 });
    await page.evaluate("document.querySelector('tbody button[aria-label^=\"Consulter\"]').click()");
    await page.waitFor("!!document.querySelector('.client-head')", { label: "fiche" });
    check(await page.evaluate(isDisabled("Modifier")) && await page.evaluate(isDisabled("Archiver")), "fiche : Modifier et Archiver désactivés");
    capture("clients_detail_read-only_1920x1080");
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /** Écriture sans archivage : création/modification proposées, archivage désactivé avec motif. */
  async "clients-write"(page) {
    await setZoom(page, 1);
    await signIn(page, credentials().write);
    await setTheme(page, "light");
    await openClients(page);
    await page.waitFor("document.querySelector('table')", { label: "liste" });
    check(await page.evaluate(`!!${buttonByText("Nouveau client")}`), "« Nouveau client » présent");
    check(await page.evaluate("![...document.querySelectorAll('tbody button')].some(b => /^Archiver /.test(b.getAttribute('aria-label') || ''))"),
      "aucune action Archiver dans les lignes");
    await page.evaluate("document.querySelector('tbody button[aria-label^=\"Consulter\"]').click()");
    await page.waitFor("!!document.querySelector('.client-head')", { label: "fiche" });
    check(!(await page.evaluate(isDisabled("Modifier"))) && await page.evaluate(isDisabled("Archiver")), "fiche : Modifier actif, Archiver désactivé");
    capture("clients_detail_write-no-archive_1920x1080", { width: 1920, height: 1080 });
    await page.evaluate(`document.querySelector('[aria-label="Se déconnecter"]').click()`);
    await page.waitFor(hasText("Bienvenue,"), { label: "déconnexion" });
  },

  /** Après redémarrage du processus client : écran de connexion, aucune session restaurée. */
  async "after-client-restart"(page) {
    await page.waitFor(hasText("Bienvenue,"), { label: "login après redémarrage" });
    check(!(await text(page)).includes("Bonjour,"), "aucune session restaurée");
    const storage = await page.evaluate("JSON.stringify(Object.keys(localStorage)) + JSON.stringify(Object.keys(sessionStorage)) + document.cookie");
    log("STOCKAGE", storage);
    check(!/token|session|auth/i.test(storage), "aucune clé de session dans le stockage WebView2");
    capture("client-restart_login_1920x1080", { width: 1920, height: 1080 });
  },

  /**
   * 04-C : dossier optique RÉEL (migration 0006 appliquée, G04-SQL) dans la fenêtre Tauri native, contre MMV.Api sur
   * mmv_v2_test, comptes et clients FICTIFS (outil de banc seed-ui) ; chaque étape de l'interface est vérifiée par
   * l'API réelle. Boutons ± cliqués à la VRAIE souris (Input.dispatchMouseEvent), saisies au clavier (insertText).
   * Fichier MMV_E2E_CREDENTIALS_FILE : { password, agent, reader, noOptical, agentId, clientId, client2Id, clientLastName }.
   */
  async "optique-04c"(page) {
    const c = credentials();
    const account = (userName, displayName) => ({ userName, password: c.password, displayName });
    const agent = account(c.agent, "C04 Opticien fictif");
    const reader = account(c.reader, "C04 Lecture fictive");
    const noOptical = account(c.noOptical, "C04 Sans optique");
    const upper = c.clientLastName.toUpperCase();
    const shot = (name, size) => capture(`c04_${name}`, size);
    const inputOf = (label) =>
      `document.getElementById([...document.querySelectorAll('label[for]')].find(l => l.textContent.replace(/\\s+/g, ' ').replace('*', '').trim() === ${J(label)})?.htmlFor)`;
    const valueOf = (label) => page.evaluate(`(${inputOf(label)})?.value ?? null`);
    const typeInto = async (label, value) => {
      check(await page.evaluate(`(() => { const e = ${inputOf(label)}; if (!e) return false; e.scrollIntoView({ block: 'center' }); e.focus(); e.select?.(); return true; })()`), `champ ${label}`);
      await page.type(value);
    };
    /**
     * Vrai clic souris au centre de l'élément (événements pointeur du moteur WebView2), en coordonnées CSS de la vue :
     * pas d'étalonnage (mouseScale), qu'un curseur réel posé sur la fenêtre fausse.
     */
    const mouseClick = async (expression, label) => {
      const rect = await page.evaluate(`(() => { const e = ${expression}; if (!e) return null; e.scrollIntoView({ block: 'center' });
        const r = e.getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2]; })()`);
      check(rect !== null, label);
      const [x, y] = rect;
      await page.send("Input.dispatchMouseEvent", { type: "mouseMoved", x, y });
      await page.send("Input.dispatchMouseEvent", { type: "mousePressed", x, y, button: "left", clickCount: 1 });
      await page.send("Input.dispatchMouseEvent", { type: "mouseReleased", x, y, button: "left", clickCount: 1 });
      await page.sleep(120);
    };
    const stepper = async (label, times) => {
      for (let i = 0; i < times; i++) await mouseClick(buttonByName(label), i === 0 ? `± souris : ${label}` : `${label} (${i + 1})`);
    };
    const datePick = async (label, iso) => {
      await click(page, `[...document.querySelectorAll('button[aria-haspopup]')].find(b => (b.getAttribute('aria-labelledby') || '').split(' ')
        .some(id => document.getElementById(id)?.textContent.trim() === ${J(label)}))`, `calendrier : ${label}`);
      await page.waitFor(calendarOpen, { label: "calendrier ouvert" });
      const [y, m] = iso.split("-").map(Number);
      await chooseDropdown(page, "Choisir l'année", y);
      await chooseDropdown(page, "Choisir le mois", m - 1);
      await click(page, `[...document.querySelectorAll('[role=dialog][aria-label="Calendrier"] button')].find(b => (b.getAttribute('aria-label') || '').includes(${J(dayLabel(iso))}))`, `jour ${iso}`);
      await page.waitFor(`!${calendarOpen}`, { label: "calendrier fermé" });
    };
    const selectIn = async (scope, value, label) =>
      check(await page.evaluate(`(() => { const s = document.querySelector(${J(scope)}); if (!s) return false;
        Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(s, ${J(value)});
        s.dispatchEvent(new Event('change', { bubbles: true })); return s.value === ${J(value)}; })()`), label);
    const records = async () => (await apiGet(agent, `/api/optical/clients/${c.clientId}/records?includeInvalidated=true`));
    const detail = async (kind, id) => (await apiSend(agent, "GET", `/api/optical/${kind === "prescription" ? "prescriptions" : "measurements"}/${id}`)).json;
    const num = (v) => (v === null || v === "" ? null : Number(v.replace("−", "-").replace(",", ".").replace(" ", "")));
    /** Ligne du dossier optique désignée par sa référence : attend son chargement (liste servie par l'API), puis la consulte. */
    const consult = async (reference, label) => {
      const row = `[...document.querySelectorAll('button[aria-label^="Consulter"]')].find(b => b.closest('tr')?.textContent.includes(${J(reference)}))`;
      await page.waitFor(`!!${row}`, { label: `ligne ${reference}` });
      await click(page, row, label);
    };
    const openOptical = async (account, first = "Patient") => {
      await openFiche(page, first, upper);
      await openTab(page, "Dossier optique");
      await page.waitFor(hasText("Ordonnances médicales"), { label: "onglet Dossier optique" });
    };
    log("PARCOURS 04-C", `API ${API}, client fictif ${upper}`);

    resize(1920, 1080);
    await signIn(page, agent);
    await setZoom(page, 1);
    await setTheme(page, "light");

    // 1. Ordonnance : saisie OD / OG aux boutons ± (souris) et au clavier, provenance, « Enregistrer » atomique.
    await openOptical(agent);
    check((await records()).length === 0, "API : dossier optique vide");
    shot("01_dossier-vide_light_1920x1080");
    await click(page, buttonByText("Transcrire une ordonnance médicale"), "Transcrire une ordonnance médicale");
    await page.waitFor(`!!(${inputOf("Sphère OD")})`, { label: "formulaire ordonnance" });
    // O-04-52 : libellé du pas approuvé (0,25 : Sphère, Cylindre, Addition ; Prisme libre), aucune mention obsolète.
    const formText = await page.evaluate("document.querySelector('form')?.textContent.replace(/\\s+/g, ' ') ?? ''");
    check(formText.includes("Boutons ± : pas de 0,25 (Sphère, Cylindre, Addition) · ↑ / ↓ au clavier · saisie libre conservée telle quelle · Prisme en saisie libre"), "libellé du pas 0,25 approuvé (O-04-52)");
    check(!/démonstration|D-04-06 ouverte|Prototype|PROPOSED/.test(formText), "formulaire réel : ni « démonstration », ni « D-04-06 ouverte », ni « Prototype » (O-04-52)");
    shot("02_ordonnance-vide_light_1920x1080");
    await stepper("Diminuer Sphère OD de 0,25", 9);
    await stepper("Diminuer Cylindre OD de 0,25", 2);
    await typeInto("Axe OD", "175");
    await stepper("Augmenter Addition OD de 0,25", 8);
    await stepper("Augmenter Sphère OG de 0,25", 6);
    await typeInto("Cylindre OG", "−0,75");
    await typeInto("Axe OG", "10");
    const ui = {
      odSphere: await valueOf("Sphère OD"), odCyl: await valueOf("Cylindre OD"), odAdd: await valueOf("Addition OD"),
      ogSphere: await valueOf("Sphère OG"), ogCyl: await valueOf("Cylindre OG"), ogAdd: await valueOf("Addition OG"),
    };
    log("VALEURS ÉCRAN", J(ui));
    check(num(ui.odSphere) === -2.25 && num(ui.odCyl) === -0.5 && num(ui.odAdd) === 2 && num(ui.ogSphere) === 1.5, "± : −2,25 / −0,50 / +2,00 / +1,50 (pas 0,25 depuis le vide)");
    check(ui.ogCyl === "−0,75", "saisie clavier conservée telle quelle (signe moins typographique, virgule)");
    check(ui.ogAdd === "", "Addition OG non touchée : vide, jamais 0");
    await datePick("Date de prescription", "2026-10-20");
    await typeInto("Prescripteur — nom (tel que sur le document)", "Dr Fictif-C04");
    shot("03_ordonnance-remplie_light_1920x1080");
    await click(page, buttonByText("Enregistrer"), "Enregistrer (ordonnance)");
    await page.waitFor(hasText("Dernière révision confirmée"), { label: "ordonnance enregistrée" });
    const rxRow = (await records()).find((r) => r.kind === "prescription");
    check(!!rxRow, `API : ordonnance créée (${rxRow?.reference})`);
    let rx = await detail("prescription", rxRow.id);
    const od = rx.lastConfirmed.content.od;
    const og = rx.lastConfirmed.content.og;
    check(rx.lastConfirmed.revisionNo === 1 && rx.openDraft === null, "API : révision 1 confirmée, aucun brouillon (création + confirmation atomiques)");
    check(od.sphere === ui.odSphere && od.cylinder === ui.odCyl && od.axis === "175" && od.addition === ui.odAdd && og.sphere === ui.ogSphere && og.cylinder === "−0,75" && og.axis === "10",
      "API : valeurs enregistrées = valeurs affichées, exactes (aucune réécriture)");
    check(og.addition === null && og.prism === null, "API : champs vides enregistrés null (jamais 0)");
    const shown = await text(page);
    check(shown.includes(rx.reference) && shown.includes(ui.odSphere) && shown.includes("−0,75"), "écran : référence et valeurs de l'API affichées");
    shot("04_ordonnance-enregistree_light_1920x1080");

    // 2. Modification : rectification motivée, puis conflit avec un autre poste (même compte, autre session API).
    await click(page, buttonByText("Modifier"), "Modifier");
    await page.waitFor(hasText("Modifier cet enregistrement ?"), { label: "dialogue de modification" });
    await selectIn("[role=alertdialog] select", "transcription_error", "motif : erreur de transcription");
    await click(page, inDialog("Modifier"), "ouvrir la modification");
    await page.waitFor(`(${inputOf("Sphère OD")})?.value === ${J(ui.odSphere)}`, { label: "formulaire de modification prérempli" });
    await stepper("Augmenter Sphère OD de 0,25", 1);
    rx = await detail("prescription", rxRow.id);
    check(rx.lastConfirmed.revisionNo === 1 && rx.openDraft !== null, "API : rectification ouverte, révision 1 toujours de référence");
    const other = await apiSend(agent, "PUT", `/api/optical/prescriptions/${rx.id}/draft`, {
      requestId: crypto.randomUUID(), version: rx.version,
      content: { ...rx.openDraft.content, od: { ...rx.openDraft.content.od, sphere: "-2,50" } },
    });
    check(other.status === 200, "autre poste : brouillon modifié (200)");
    await click(page, buttonByText("Enregistrer"), "Enregistrer (formulaire périmé)");
    await page.waitFor(hasText("Modifié depuis un autre poste"), { label: "conflit affiché" });
    rx = await detail("prescription", rxRow.id);
    check(rx.lastConfirmed.revisionNo === 1 && rx.openDraft.content.od.sphere === "-2,50", "API : rien écrit par le poste périmé (révision 1, brouillon de l'autre poste)");
    check(await page.evaluate(`(() => { const b = [...document.querySelectorAll('.clients-banner[role=alert]')].find(e => e.textContent.includes("Modifié depuis un autre poste"));
      b?.scrollIntoView({ block: 'center' }); return !!b && b.textContent.includes("rien n'a été enregistré"); })()`), "écran : bandeau d'alerte du conflit (saisie conservée, rien enregistré)");
    await page.sleep(300);
    shot("05_conflit-deux-postes_light_1920x1080");

    // 3. Reprise : « Recharger » depuis le conflit reprend le brouillon réel, « Enregistrer » confirme la révision 2.
    await click(page, buttonByText("Recharger"), "Recharger (bandeau de conflit)");
    await page.sleep(300);
    if (await page.evaluate(`!!${inDialog("Recharger")}`)) await click(page, inDialog("Recharger"), "rechargement confirmé (saisie locale abandonnée)");
    await page.waitFor(`(${inputOf("Sphère OD")})?.value === "-2,50"`, { label: "brouillon réel repris (−2,50)" });
    // Deux clics : révision 2 (−2,00) distincte de la révision 1 (−2,25), pour vérifier ce qu'un compte sans audit.read voit.
    await stepper("Augmenter Sphère OD de 0,25", 2);
    const resumed = await valueOf("Sphère OD");
    check(num(resumed) === -2 && num(resumed) !== num(ui.odSphere), `révision 2 distincte de la révision 1 (${resumed})`);
    await click(page, buttonByText("Enregistrer"), "Enregistrer (révision 2)");
    await page.waitFor(hasText("Dernière révision confirmée"), { label: "révision 2 enregistrée" });
    rx = await detail("prescription", rxRow.id);
    check(rx.lastConfirmed.revisionNo === 2 && rx.lastConfirmed.content.od.sphere === resumed && rx.openDraft === null, `API : révision 2 confirmée (${resumed})`);
    const rev1 = rx.revisions.find((r) => r.revisionNo === 1);
    check(rev1?.content.od.sphere === ui.odSphere, "API (audit.read) : révision 1 conservée intacte");
    check((await text(page)).includes("Historique des modifications"), "écran : historique des modifications (audit.read)");
    shot("06_revision2-historique_light_1920x1080");

    // 4. Mesure magasin : brouillon, reprise, confirmation ; collaborateur ayant mesuré.
    await openOptical(agent);
    await click(page, buttonByText("Saisir une mesure magasin"), "Saisir une mesure magasin");
    await page.waitFor(`!!(${inputOf("Sphère OD")})`, { label: "formulaire mesure" });
    await page.waitFor(`[...((${inputOf("Collaborateur ayant mesuré")})?.options ?? [])].some(o => o.textContent.trim() === ${J(agent.displayName)})`,
      { label: "liste des collaborateurs habilités chargée (/measurers)" });
    await setSelect(page, "Collaborateur ayant mesuré", agent.displayName);
    await datePick("Date de la mesure", "2026-10-28");
    await stepper("Augmenter Sphère OD de 0,25", 3);
    await click(page, buttonByText("Enregistrer comme brouillon"), "Enregistrer comme brouillon");
    await page.sleep(1500);
    let mesRow = (await records()).find((r) => r.kind === "measurement");
    check(!!mesRow, `API : mesure créée (${mesRow?.reference})`);
    let mes = await detail("measurement", mesRow.id);
    check(mes.lastConfirmed === null && mes.openDraft?.content.od.sphere === "+0,75", "API : brouillon seulement (+0,75), aucune révision confirmée");
    shot("07_mesure-brouillon_light_1920x1080");
    await openOptical(agent);
    await consult(mes.reference, "Consulter la mesure (brouillon)");
    await page.waitFor(hasText("Confirmer la mesure"), { label: "détail du brouillon" });
    await click(page, buttonByText("Modifier"), "Modifier (reprise du brouillon de mesure)");
    await page.waitFor(`(${inputOf("Sphère OD")})?.value === "+0,75"`, { label: "brouillon de mesure repris" });
    await stepper("Augmenter Sphère OG de 0,25", 2);
    await click(page, buttonByText("Enregistrer"), "Enregistrer (mesure)");
    await page.waitFor(hasText("Dernière révision confirmée"), { label: "mesure enregistrée" });
    mes = await detail("measurement", mesRow.id);
    check(mes.lastConfirmed?.revisionNo === 1 && mes.lastConfirmed.content.og.sphere === "+0,50" && mes.lastConfirmed.content.provenance.measuredByUserId === c.agentId,
      "API : mesure confirmée (OD +0,75, OG +0,50, collaborateur)");
    check((await text(page)).includes(agent.displayName), "écran : collaborateur ayant mesuré nommé");
    shot("08_mesure-confirmee_light_1920x1080");

    // 5. Invalidation motivée (menu « Plus d'actions », vraie souris : déclencheur Radix au pointeur).
    await mouseClick(buttonByName("Plus d'actions"), "Plus d'actions");
    await page.waitFor("!!document.querySelector('[role=menuitem]')", { label: "menu" });
    await mouseClick("[...document.querySelectorAll('[role=menuitem]')].find(m => m.textContent.includes('Invalider'))", "Invalider…");
    await page.waitFor("!!document.querySelector('[role=alertdialog] select')", { label: "dialogue d'invalidation" });
    await click(page, inDialog("Invalider"), "Invalider sans motif");
    await page.waitFor(hasText("Choisissez un motif d'invalidation."), { label: "motif exigé" });
    await selectIn("[role=alertdialog] select", "entry_error", "motif : erreur de saisie");
    await click(page, inDialog("Invalider"), "Invalider");
    await page.waitFor("!document.querySelector('[role=alertdialog]')", { label: "dialogue fermé" });
    await page.sleep(800);
    mes = await detail("measurement", mesRow.id);
    check(mes.lifecycle === "invalidated" && mes.invalidation.reasonCode === "entry_error", "API : mesure invalidée, motif enregistré, aucune suppression");
    shot("09_mesure-invalidee_light_1920x1080");

    // 6. Lecture seule (optical.read sans audit.read) : aucune action d'écriture, historique masqué, valeurs antérieures absentes.
    await signIn(page, reader);
    await openOptical(reader);
    check(!(await page.evaluate(`!!${buttonByText("Ajouter…")}`)), "lecture : aucun bouton Ajouter…");
    await consult(rx.reference, "Consulter l'ordonnance (lecture)");
    await page.waitFor(hasText("Dernière révision confirmée"), { label: "détail en lecture" });
    const readText = await text(page);
    check(!(await page.evaluate(`!!${buttonByText("Modifier")}`)) && !(await page.evaluate(`!!${buttonByName("Plus d'actions")}`)), "lecture : ni Modifier ni Invalider");
    check(readText.includes("exige le droit de consultation de l'historique"), "lecture : historique masqué (audit.read absent)");
    check(!readText.includes(ui.odSphere), `lecture : valeur de la révision 1 (${ui.odSphere}) absente`);
    shot("10_lecture-seule_light_1920x1080");

    // 7. Sans droit optique : onglet reporté, aucune requête /api/optical servie.
    await signIn(page, noOptical);
    await openFiche(page, "Patient", upper);
    check(!(await page.evaluate(`!!${tabByName("Dossier optique")}`)), "sans optical.read : aucun onglet Dossier optique");
    await openTab(page, "Ordonnances");
    await page.waitFor(hasText("Ordonnances — phase ultérieure"), { label: "sans optical.read : onglet reporté « Ordonnances — phase ultérieure »" });
    shot("11_sans-droit-optique_light_1920x1080");

    // 8. Thème sombre, 1024×700, zoom 125 % : saisie ± puis abandon (rien n'est écrit).
    resize(1024, 700);
    await signIn(page, agent);
    await setZoom(page, 1.25);
    await setTheme(page, "dark");
    const before = (await apiGet(agent, `/api/optical/clients/${c.client2Id}/records?includeInvalidated=true`)).length;
    await openOptical(agent, "Second");
    await click(page, buttonByText("Transcrire une ordonnance médicale"), "Transcrire (sombre)");
    await page.waitFor(`!!(${inputOf("Sphère OD")})`, { label: "formulaire (sombre)" });
    await stepper("Augmenter Sphère OD de 0,25", 4);
    await stepper("Diminuer Sphère OG de 0,25", 3);
    shot("12_ordonnance_dark_1024x700_z125");
    await click(page, buttonByText("Annuler"), "Annuler");
    if (await page.evaluate(`!!${inDialog("Abandonner")}`)) await click(page, inDialog("Abandonner"), "abandon confirmé");
    await page.sleep(800);
    check((await apiGet(agent, `/api/optical/clients/${c.client2Id}/records?includeInvalidated=true`)).length === before, "abandon : aucun enregistrement créé");
    await setZoom(page, 1);
    await setTheme(page, "light");
  },
};

if (!scenarios[scenario]) {
  console.error(`Scénarios : ${Object.keys(scenarios).join(", ")}`);
  process.exit(1);
}

// 03-D3 : exécutable construit (ressources embarquées) servi par http://tauri.localhost ; défaut : serveur Vite de `tauri dev`.
const page = await connect(port, option("--page-url", "http://localhost:1420"));
try {
  log("SCÉNARIO", scenario);
  await scenarios[scenario](page);
  log("TERMINÉ", scenario);
} catch (error) {
  log("ERREUR", error.message);
  process.exitCode = 1;
} finally {
  await apiLogoutAll().catch((error) => log("AVERTISSEMENT", `déconnexion API : ${error.message}`));
  page.close();
}
