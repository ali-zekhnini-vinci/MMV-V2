// Plan des captures de la refonte UX Lot 3 (D-04-15) sur le banc isolé : matrice 1920×1080 / 1024×700 × zoom 100 /
// 125 / 150 % × clair / sombre × ordonnance / mesure, puis états des contrôles ± (mode « apres » seulement).
// Usage : node e2e/bench-04a/plan-lot3.mjs <plan.json> [avant|apres]   puis shoot.mjs (même format que plan.mjs).
import { writeFileSync } from "node:fs";
const mode = process.argv[3] ?? "apres";
const B = "http://127.0.0.1:1520/optical-bench.html";
const card = (id, theme) => `${B}?route=/clients/${id}&theme=${theme}`;
const click = (text, wait = 700) => ({ click: text, wait });
const ev = (expression, wait = 350) => ({ eval: expression, wait });
const top = ev("(() => { const c = document.querySelector('.bench__content'); if (c) c.scrollTop = 0; return 'ok'; })()");
const scrollTo = (text) =>
  ev(`(() => { const el = [...document.querySelectorAll('h1,h2,h3')].find((h) => h.textContent.includes(${JSON.stringify(text)})); if (!el) return 'NOT FOUND ' + ${JSON.stringify(text)}; el.scrollIntoView({ block: 'start' }); const b = document.querySelector('.bench'); if (b) b.scrollTop = 0; document.documentElement.scrollTop = 0; return 'ok'; })()`);
const field = (label) => `[...document.querySelectorAll('label')].map((l) => document.getElementById(l.htmlFor)).find((el, i, all) => el && [...document.querySelectorAll('label')][i].textContent.replace(/\\s+/g, ' ').trim() === ${JSON.stringify(label)})`;
const type = (label, text) =>
  ev(`(() => { const el = ${field(label)}; if (!el) return 'NOT FOUND ' + ${JSON.stringify(label)}; Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, ${JSON.stringify(text)}); el.dispatchEvent(new Event('input', { bubbles: true })); return 'ok'; })()`);
const focus = (label) => ev(`(() => { const el = ${field(label)}; if (!el) return 'NOT FOUND ' + ${JSON.stringify(label)}; el.scrollIntoView({ block: 'center' }); const b = document.querySelector('.bench'); if (b) b.scrollTop = 0; document.documentElement.scrollTop = 0; el.focus({ preventScroll: true }); return 'ok'; })()`);
const press = (name, n = 1) => Array.from({ length: n }, () => click(name, 120));
const kinds = {
  ordonnance: { client: "cli-fictif-3", chooser: "Transcrire une ordonnance médicale" },
  mesure: { client: "cli-fictif-3", chooser: "Saisir une mesure magasin" },
};
const open = (kind) => [click("Dossier optique"), click(kinds[kind].chooser, 1000), top];
const plan = [];
const add = (name, url, actions, width, height, scale = 1) => plan.push({ name, url, actions, width, height, scale, waitMs: 1800 });

for (const kind of Object.keys(kinds)) {
  for (const theme of ["light", "dark"]) {
    for (const [w, h] of [[1920, 1080], [1024, 700]]) {
      for (const scale of mode === "avant" ? [1] : [1, 1.25, 1.5]) {
        add(`${mode}-${kind}-${w}x${h}-z${Math.round(scale * 100)}-${theme === "dark" ? "sombre" : "clair"}`, card(kinds[kind].client, theme), open(kind), w, h, scale);
      }
    }
  }
}
if (mode === "avant") {
  add("avant-ordonnance-1920-correction", card("cli-fictif-3", "light"), [...open("ordonnance"), scrollTo("Correction optique")], 1920, 1080);
} else {
  // Saisie rapide : clics ± sur chaque champ OD / OG (valeurs indépendantes), puis états d'un champ.
  const quick = [
    ...open("ordonnance"),
    ...press("Diminuer Sphère OD de 0,25", 9),
    ...press("Diminuer Cylindre OD de 0,25", 2),
    type("Axe OD", "175"),
    ...press("Augmenter Addition OD de 0,25", 7),
    ...press("Augmenter Sphère OG de 0,25", 6),
    ...press("Diminuer Cylindre OG de 0,25", 3),
    type("Axe OG", "10"),
    ...press("Augmenter Addition OG de 0,25", 7),
    type("Acuité visuelle OD", "10/10"),
    type("Acuité visuelle OG", "9/10"),
  ];
  for (const theme of ["light", "dark"]) {
    const t = theme === "dark" ? "sombre" : "clair";
    add(`apres-ordonnance-saisie-rapide-1920-${t}`, card("cli-fictif-3", theme), [...quick, scrollTo("Correction optique")], 1920, 1080);
    add(`apres-mesure-saisie-rapide-1920-${t}`, card("cli-fictif-3", theme), [...open("mesure"), ...press("Augmenter Sphère OD de 0,25", 5), ...press("Augmenter Sphère OG de 0,25", 4), scrollTo("Correction optique")], 1920, 1080);
  }
  add("apres-ordonnance-saisie-rapide-1024", card("cli-fictif-3", "light"), [...quick, scrollTo("Correction optique")], 1024, 700);
  add("apres-etat34-focus-stepper", card("cli-fictif-3", "light"), [...open("ordonnance"), type("Sphère OD", "-2,25"), focus("Cylindre OD")], 1920, 1080);
  add("apres-valeur-manuelle-hors-pas", card("cli-fictif-3", "light"), [...open("ordonnance"), type("Sphère OD", "-2,13"), ...press("Augmenter Sphère OD de 0,25", 1), type("Cylindre OD", "abc"), scrollTo("Correction optique")], 1920, 1080);
  add("apres-etat35-erreur-sous-champ", card("cli-fictif-3", "light"), [...open("ordonnance"), type("Cylindre OD", "abc"), click("Enregistrer le brouillon", 1200)], 1920, 1080);
  add("apres-pas-configurable-0125", `${card("cli-fictif-3", "light")}&step=0.125`, [...open("ordonnance"), ...press("Augmenter Sphère OD de 0,125", 3), scrollTo("Correction optique")], 1920, 1080);
  add("apres-base-liste-proposee", card("cli-fictif-3", "light"), [...open("ordonnance"), ...press("Augmenter Prisme OD de 0,25", 4), click("Choisir une base proposée pour OD", 600)], 1920, 1080);
}
// Premier chargement : optimisation des dépendances par Vite.
plan[0].waitMs = 6000;
writeFileSync(process.argv[2], JSON.stringify(plan, null, 1));
console.log(plan.length, "captures");
