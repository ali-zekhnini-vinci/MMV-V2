// Pilote minimal du protocole de débogage Chromium exposé par WebView2 (phase 02, preuve native).
// La fenêtre Tauri doit être lancée avec WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=<port>
// (développement uniquement, loopback). Les frappes passent par Input.* : ce sont de vrais événements
// clavier du moteur WebView2, pas des appels JavaScript synthétiques.
// Node 22 : WebSocket et fetch globaux, aucune dépendance.

const KEYS = {
  Tab: { key: "Tab", code: "Tab", windowsVirtualKeyCode: 9 },
  Enter: { key: "Enter", code: "Enter", windowsVirtualKeyCode: 13, text: "\r" },
  Escape: { key: "Escape", code: "Escape", windowsVirtualKeyCode: 27 },
  Space: { key: " ", code: "Space", windowsVirtualKeyCode: 32, text: " " },
};

export async function connect(port, urlPrefix = "http://localhost:1420") {
  const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
  const page = targets.find((t) => t.type === "page" && t.url.startsWith(urlPrefix));
  if (!page) throw new Error(`Aucune page ${urlPrefix} sur le port de débogage ${port}.`);

  const socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });

  let nextId = 0;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (message.id !== undefined && pending.has(message.id)) {
      const { resolve, reject } = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) reject(new Error(message.error.message));
      else resolve(message.result);
    }
  });

  const send = (method, params = {}) =>
    new Promise((resolve, reject) => {
      const id = ++nextId;
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });

  const evaluate = async (expression) => {
    const result = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
    if (result.exceptionDetails) throw new Error(`Évaluation échouée : ${result.exceptionDetails.text}`);
    return result.result.value;
  };

  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

  const api = {
    send,
    evaluate,
    sleep,
    async press(name, { shift = false } = {}) {
      const key = KEYS[name];
      const modifiers = shift ? 8 : 0;
      await send("Input.dispatchKeyEvent", { type: key.text ? "keyDown" : "rawKeyDown", modifiers, ...key });
      await send("Input.dispatchKeyEvent", { type: "keyUp", modifiers, ...key, text: undefined });
      await sleep(80);
    },
    /** Saisie dans l'élément focalisé (événements d'entrée réels ; le texte n'est jamais journalisé). */
    async type(text) {
      await send("Input.insertText", { text });
      await sleep(50);
    },
    async waitFor(predicate, { timeoutMs = 15000, label = predicate } = {}) {
      const deadline = Date.now() + timeoutMs;
      while (Date.now() < deadline) {
        if (await evaluate(`Boolean(${predicate})`)) return;
        await sleep(150);
      }
      throw new Error(`Délai dépassé en attendant : ${label}`);
    },
    /** Description non sensible de l'élément focalisé (étiquette, rôle, texte court). */
    focused: () =>
      evaluate(`(() => { const e = document.activeElement; if (!e) return null;
        const label = e.getAttribute('aria-label') || (e.labels && e.labels[0] && e.labels[0].textContent) || e.textContent;
        return (e.tagName + ' ' + (label || '').trim()).slice(0, 80); })()`),
    /** Facteur de zoom effectif de la vue (WebView2 : devicePixelRatio à DPI 100 %). */
    zoomFactor: () => evaluate("window.devicePixelRatio"),
    noHorizontalScroll: () =>
      evaluate(`document.documentElement.scrollWidth <= document.documentElement.clientWidth
        && document.body.scrollWidth <= document.documentElement.clientWidth`),
    reload: async () => {
      await send("Page.reload", { ignoreCache: true });
      await sleep(1500);
    },
    close: () => socket.close(),
  };
  await send("Runtime.enable");
  return api;
}
