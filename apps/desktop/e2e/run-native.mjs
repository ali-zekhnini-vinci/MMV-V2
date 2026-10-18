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
// Préfixe des captures : P02_native (phase 02, défaut) ou P03_native (scénarios clients-*, phase 03-A3).
const prefix = option("--prefix", "P02_native");
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
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-Process", "mmv-desktop", "-Out", file, "-ClientOnly", ...extra],
    { encoding: "utf8" },
  ).trim();
  log("CAPTURE", output.replace(outDir, "<EVIDENCE>"));
  return file;
}

/** Vraies frappes système (SendKeys) vers la fenêtre Tauri. */
function osKeys(keys, repeat = 1) {
  execFileSync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", resolve("e2e/send-keys.ps1"),
    "-Process", "mmv-desktop", "-Keys", keys, "-Repeat", String(repeat)]);
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
  const base = "https://localhost:7267";
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
const API = "https://localhost:7267";

/** Lecture de l'état réel par l'API (compte éphémère ; jeton jamais affiché). */
async function apiGet(account, path) {
  const json = { "Content-Type": "application/json" };
  const login = await fetch(`${API}/api/auth/login`, { method: "POST", headers: json, body: J({ userName: account.userName, password: account.password }) });
  const auth = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  const response = await fetch(`${API}${path}`, { headers: auth });
  const body = await response.json();
  await fetch(`${API}/api/auth/logout`, { method: "POST", headers: auth });
  return body;
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
  if (date) await fillField(page, "Date", date);
  if (time) await fillField(page, "Heure", time);
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

const scenarios = {
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
    await fillAppointment(page, { client: "Natif", last, date: "15/01/2026", time: "10:00", duration: "30 minutes", reason: "Conseil / Devis", responsible: "B3N Responsable Deux" });
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
};

if (!scenarios[scenario]) {
  console.error(`Scénarios : ${Object.keys(scenarios).join(", ")}`);
  process.exit(1);
}

const page = await connect(port);
try {
  log("SCÉNARIO", scenario);
  await scenarios[scenario](page);
  log("TERMINÉ", scenario);
} catch (error) {
  log("ERREUR", error.message);
  process.exitCode = 1;
} finally {
  page.close();
}
