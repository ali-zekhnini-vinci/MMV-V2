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

const scenarios = {
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
