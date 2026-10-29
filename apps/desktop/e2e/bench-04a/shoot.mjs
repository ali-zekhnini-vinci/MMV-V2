// Captures du banc 04-A dans Edge sans interface (moteur Chromium, celui de WebView2) par le protocole CDP.
// Simulation NAVIGATEUR : ce n'est PAS une preuve Tauri native. Zoom 125 / 150 % = facteur d'échelle de l'appareil
// (largeur CSS réduite d'autant). Usage (serveur serve.mjs lancé) :
//   node e2e/bench-04a/plan.mjs plan.json && node e2e/bench-04a/shoot.mjs plan.json <dossier-captures> <profil-edge-jetable>
import { spawn } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
const [, , planPath, outDir, profileDir] = process.argv;
const plan = JSON.parse(readFileSync(planPath, "utf8"));
mkdirSync(outDir, { recursive: true });
const port = 9333;
const edge = spawn("C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", [
  "--headless=new", `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, "--no-first-run", "--disable-extensions", "--hide-scrollbars", "about:blank",
], { stdio: "ignore" });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let targets;
for (let i = 0; i < 100; i++) { try { targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); if (targets.some((t) => t.type === "page")) break; } catch { /* démarrage */ } await sleep(300); }
const page = targets.find((t) => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener("open", r, { once: true }));
let id = 0; const pending = new Map(); const errors = [];
ws.addEventListener("message", (e) => {
  const m = JSON.parse(e.data);
  if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
  if (m.method === "Runtime.exceptionThrown") errors.push(m.params.exceptionDetails.text + " " + (m.params.exceptionDetails.exception?.description ?? ""));
  if (m.method === "Runtime.consoleAPICalled" && m.params.type === "error") errors.push(m.params.args.map((a) => a.value ?? a.description).join(" "));
  if (m.method === "Log.entryAdded" && m.params.entry.level === "error") errors.push(m.params.entry.text);
});
const send = (method, params = {}) => new Promise((resolve, reject) => { const i = ++id; pending.set(i, { resolve, reject }); ws.send(JSON.stringify({ id: i, method, params })); });
await send("Page.enable"); await send("Runtime.enable"); await send("Log.enable");
const evaluate = async (expression) => (await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true })).result.value;
const clickText = async (text, selector = "button, [role=tab], [role=menuitem], [role=menuitemradio], [role=option], a, label") => {
  const box = await evaluate(`(() => {
    const all = [...document.querySelectorAll(${JSON.stringify(selector)})].filter((el) => el.getClientRects().length && !el.closest('[aria-hidden=true]'));
    const wanted = ${JSON.stringify(text)};
    const el = all.find((el) => (el.getAttribute('aria-label') || el.textContent || '').trim() === wanted) || all.find((el) => (el.getAttribute('aria-label') || el.textContent || '').trim().includes(wanted));
    if (!el) return null;
    el.scrollIntoView({ block: 'center' }); const b = document.querySelector('.bench'); if (b) b.scrollTop = 0; document.documentElement.scrollTop = 0;
    const r = el.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  })()`);
  if (!box) return "NOT FOUND: " + text;
  // Vrais événements souris du moteur (Radix active onglets et menus sur pointerdown).
  for (const type of ["mouseMoved", "mousePressed", "mouseReleased"]) await send("Input.dispatchMouseEvent", { type, x: box.x, y: box.y, button: "left", clickCount: 1 });
  return "ok";
};
const report = [];
for (const shot of plan) {
  const scale = shot.scale ?? 1;
  await send("Emulation.setDeviceMetricsOverride", { width: Math.round(shot.width / scale), height: Math.round(shot.height / scale), deviceScaleFactor: scale, mobile: false });
  const before = errors.length;
  await send("Page.navigate", { url: shot.url });
  await sleep(shot.waitMs ?? 1500);
  const notes = [];
  for (const action of shot.actions ?? []) {
    if (action.click) notes.push(await clickText(action.click, action.selector));
    if (action.eval) notes.push(await evaluate(action.eval));
    if (action.type) notes.push(await evaluate(`(() => { const el = document.querySelector(${JSON.stringify(action.selector)}); if (!el) return 'NOT FOUND input'; const set = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(el), 'value').set; set.call(el, ${JSON.stringify(action.type)}); el.dispatchEvent(new Event('input', { bubbles: true })); return 'typed'; })()`));
    if (action.key) { await send("Input.dispatchKeyEvent", { type: "keyDown", key: action.key, code: action.key, windowsVirtualKeyCode: action.vk ?? 0 }); await send("Input.dispatchKeyEvent", { type: "keyUp", key: action.key, code: action.key, windowsVirtualKeyCode: action.vk ?? 0 }); }
    await sleep(action.wait ?? 500);
  }
  if (shot.scrollTo) await evaluate(`(() => { const el = [...document.querySelectorAll('h1,h2,h3')].find((h) => h.textContent.includes(${JSON.stringify(shot.scrollTo)})); if (el) el.scrollIntoView({ block: 'start' }); })()`);
  await sleep(300);
  const metrics = await evaluate(`({ hScroll: document.documentElement.scrollWidth > document.documentElement.clientWidth, contentH: (() => { const c = document.querySelector('.bench__content'); return c ? c.scrollWidth > c.clientWidth + 1 : null; })(), title: document.title, h1: document.querySelector('h1')?.textContent })`);
  const { data } = await send("Page.captureScreenshot", { format: "jpeg", quality: 70 });
  writeFileSync(`${outDir}/${shot.name}.jpg`, Buffer.from(data, "base64"));
  const view = await evaluate(`({ w: window.innerWidth, h: window.innerHeight })`);
  const small = await send("Page.captureScreenshot", { format: "jpeg", quality: 45, clip: { x: 0, y: 0, width: view.w, height: view.h, scale: 620 / view.w / (shot.scale ?? 1) } });
  writeFileSync(`${outDir}/${shot.name}.small.jpg`, Buffer.from(small.data, "base64"));
  report.push({ name: shot.name, notes, metrics, errors: errors.slice(before) });
}
writeFileSync(`${outDir}/report.json`, JSON.stringify(report, null, 2));
ws.close(); edge.kill();
console.log(JSON.stringify(report.map((r) => ({ n: r.name, notes: r.notes.filter((x) => x !== 'ok' && x !== 'typed'), h: r.metrics.hScroll || r.metrics.contentH, h1: r.metrics.h1, err: r.errors.length })), null, 0));
process.exit(0);
