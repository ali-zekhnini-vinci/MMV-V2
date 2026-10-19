// Plan des captures 04-A (banc isolé, Edge sans interface). Écrit plan-04a.json.
import { writeFileSync } from "node:fs";
const B = "http://127.0.0.1:1520/optical-bench.html";
const u = (q = "") => `${B}${q ? "?" + q : ""}`;
const W = { width: 1440, height: 900 };
const findLabel = (label, exact) =>
  `[...document.querySelectorAll('label')].find((x) => ${exact ? `x.textContent.replace(/\\s+/g,' ').trim() === ${JSON.stringify(label)}` : `x.textContent.trim().startsWith(${JSON.stringify(label)})`})`;
const setSelect = (label, value) => ({
  eval: `(() => { const l = ${findLabel(label, false)}; const el = l && document.getElementById(l.htmlFor); if (!el) return 'NOT FOUND select'; Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(el, ${JSON.stringify(value)}); el.dispatchEvent(new Event('change', { bubbles: true })); return 'ok'; })()`,
  wait: 300,
});
const typeIn = (label, text) => ({
  eval: `(() => { const l = ${findLabel(label, true)}; const el = l ? document.getElementById(l.htmlFor) : document.querySelector('[aria-label=${JSON.stringify(label)}]'); if (!el) return 'NOT FOUND input ' + ${JSON.stringify(label)}; const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, ${JSON.stringify(text)}); el.dispatchEvent(new Event('input', { bubbles: true })); return 'ok'; })()`,
  wait: 300,
});
const click = (text, wait = 700) => ({ click: text, wait });
const scrollTo = (text) => ({
  eval: `(() => { const el = [...document.querySelectorAll('h1,h2,h3,legend,.clients-banner__title')].find((h) => h.textContent.includes(${JSON.stringify(text)})); if (!el) return 'NOT FOUND scroll ' + ${JSON.stringify(text)}; el.scrollIntoView({ block: 'start' }); const b = document.querySelector('.bench'); if (b) b.scrollTop = 0; document.documentElement.scrollTop = 0; return 'ok'; })()`,
  wait: 300,
});
const pause = (ms) => ({ eval: "'ok'", wait: ms });
const card = (id, theme = "light") => u(`route=/clients/${id}&theme=${theme}`);
const openList = (ref) => [click(`Consulter Ordonnance médicale ${ref}`, 1000)];
const newRx = [click("Dossier optique"), click("Ajouter…"), click("Ordonnance médicale", 900)];
const plan = [];
const add = (name, url, actions = [], size = W, extra = {}) => plan.push({ name, url, actions, ...size, waitMs: 1800, ...extra });

for (const theme of ["light", "dark"]) {
  const t = theme === "dark" ? "-sombre" : "";
  add(`01-banc-accueil${t}`, u(`theme=${theme}`));
  add(`02-etat05-fiche-dossier-optique${t}`, card("cli-fictif-1", theme), [click("Dossier optique")]);
  add(`10-etat01-liste-globale${t}`, u(`route=/optique&theme=${theme}`));
  add(`20-etat19-detail-rx-revision2${t}`, u(`theme=${theme}`), [click("RX-FICTIF-0001", 1000)]);
  add(`21-gap13-rectification-en-cours${t}`, u(`theme=${theme}`), [click("RX-FICTIF-0002", 1000)]);
  add(`30-etat10-formulaire-ordonnance${t}`, card("cli-fictif-3", theme), newRx);
  add(`31-gap02-formulaire-mesure${t}`, card("cli-fictif-3", theme), [click("Dossier optique"), click("Ajouter…"), click("Mesure magasin", 900)]);
}
add("03-etat07-fiche-vide-par-section", card("cli-fictif-3"), [click("Dossier optique")]);
add("04-etat18-client-archive", card("cli-fictif-4"), [click("Dossier optique")]);
add("05-gap01-choix-du-type", card("cli-fictif-1"), [click("Dossier optique"), click("Ajouter…", 900)]);
add("11-etat04-filtres-invalides-inclus-resume", u("route=/optique"), [click("Inclure les invalidés"), click("Résumé OD/OG (D-04-13.b)"), click("Filtres", 600)]);
add("12-etat03-recherche", u("route=/optique"), [typeIn("Rechercher par client ou prescripteur", "Bravo"), pause(900)]);
add("13-etat06-aucun-resultat", u("route=/optique"), [typeIn("Rechercher par client ou prescripteur", "zzzz"), pause(900)]);
add("14-etat08-chargement", u("route=/optique&latency=3000"), [], W, { waitMs: 900 });
add("15-etat09-erreur", u(""), [click("Échec de la prochaine liste", 300), click("Dossiers optiques", 900)]);
add("16-etats12-13-selection-client", u("route=/optique"), [click("Nouveau…", 800), click("Ordonnance médicale", 800), typeIn("Rechercher un client actif", "Fictif"), pause(1000)]);
add("22-gap14-piece-invalidee", u(""), [click("RX-FICTIF-0002", 1000), scrollTo("Pièces source")]);
add("23-gap07-revisions-comparaison", u(""), [click("RX-FICTIF-0001", 1000), scrollTo("Révisions")]);
add("24-gap08-etat30-invalide", u(""), [click("RX-FICTIF-0003", 1000)]);
add("25-etats16-21-brouillon-partiel", u(""), [click("RX-FICTIF-0004", 1000)]);
add("26-gap10-confirmation", u(""), [click("RX-FICTIF-0004", 1000), click("Confirmer la transcription", 800)]);
add("27-confirmation-refusee-422", u(""), [click("RX-FICTIF-0004", 1000), click("Confirmer la transcription", 800), click("Confirmer", 1000)]);
add("28-gap03-detail-mesure", u(""), [click("MES-FICTIF-0001", 1000)]);
add("29-etat23-rectifier-motif", u("route=/optique"), [...openList("RX-FICTIF-0006"), click("Rectifier", 800)]);
add("32-etats28-29-invalider", u(""), [click("RX-FICTIF-0001", 1000), click("Plus d'actions", 600), click("Invalider…", 900)]);
add("32b-etat31-erreur-invalidation", u("route=/optique"), [...openList("RX-FICTIF-0006"), click("Échec de la prochaine écriture", 300), click("Plus d'actions", 600), click("Invalider…", 900), setSelect("Motif", "entry_error"), click("Invalider", 1000)]);
add("33-gap06-apercu-piece-synthetique", u(""), [click("RX-FICTIF-0001", 1000), scrollTo("Pièces source"), click("Afficher la pièce", 1000), scrollTo("Pièces source")]);
add("34-gap06-format-refuse", u(""), [click("RX-FICTIF-0001", 1000), scrollTo("Pièces source"), click("Joindre une pièce", 700), click("Image SVG renommée en .png (refusée)", 900), scrollTo("Pièces source")]);
add("35-etat22-lecture-seule-sans-pieces", u("profile=reader"), [click("RX-FICTIF-0002", 1000)]);
add("36-gap05-sans-droit-optique-fiche", card("cli-fictif-1") + "&profile=none", [click("Ordonnances", 700)]);
add("37-gap05-acces-direct-refuse", u("route=/optique&profile=none"));
add("38-etats17-35-erreurs-sous-champ", card("cli-fictif-3"), [...newRx, typeIn("Sphère OD", "abc"), typeIn("Prescripteur — nom (tel que sur le document)", "Dr Fictif-Test"), click("Enregistrer le brouillon", 1000)]);
add("38b-etat17-bandeau-erreurs", card("cli-fictif-3"), [...newRx, typeIn("Sphère OD", "abc"), click("Enregistrer le brouillon", 1000), scrollTo("Veuillez corriger")]);
add("39-etat14-enregistrement-en-cours", card("cli-fictif-3") + "&latency=3000", [click("Dossier optique", 3500), click("Ajouter…", 600), click("Ordonnance médicale", 900), click("Enregistrer le brouillon", 400)], W, { waitMs: 1500 });
add("40-gap09-conflit-deux-postes", u(""), [click("RX-FICTIF-0004", 1000), click("Poursuivre le brouillon", 900), click("Autre poste : modifier l'enregistrement ouvert", 300), typeIn("Remarques complémentaires (facultatif)", "Saisie du poste A (fictive)"), click("Enregistrer le brouillon", 1000)]);
add("41-etat26-garde-de-sortie", card("cli-fictif-3"), [...newRx, typeIn("Sphère OD", "+0,50"), click("Dossiers optiques", 800)]);
add("42-etat27-revision-enregistree", u("route=/optique"), [...openList("RX-FICTIF-0006"), click("Rectifier", 700), setSelect("Motif", "transcription_error"), click("Ouvrir la rectification", 1000), typeIn("Sphère OD", "-1,10"), click("Enregistrer et confirmer…", 800), click("Confirmer", 1200)]);
add("43-etat34-focus-visible", card("cli-fictif-3"), [
  ...newRx,
  { eval: "(() => { const l=[...document.querySelectorAll('label')].find((x)=>x.textContent.replace(/\\s+/g,' ').trim()==='Sphère OD'); const el=document.getElementById(l.htmlFor); el.scrollIntoView({block:'center'}); const all=[...document.querySelectorAll('input,select,textarea,button')]; all[all.indexOf(el)-1].focus(); return 'ok'; })()" },
  { key: "Tab", vk: 9, wait: 400 },
]);
const R = { width: 1024, height: 700 };
add("50-etat32-fenetre-reduite-formulaire", card("cli-fictif-3"), [...newRx, scrollTo("Correction optique")], R);
add("51-etat32-fenetre-reduite-formulaire-sombre", card("cli-fictif-3", "dark"), [...newRx, scrollTo("Correction optique")], R);
add("52-fenetre-reduite-detail", u(""), [click("RX-FICTIF-0002", 1000)], R);
add("53-1920-detail-rx", u(""), [click("RX-FICTIF-0001", 1000)], { width: 1920, height: 1080 });
add("54-1920-fiche-dossier-optique-sombre", card("cli-fictif-1", "dark"), [click("Dossier optique")], { width: 1920, height: 1080 });
for (const scale of [1.25, 1.5]) {
  const z = String(Math.round(scale * 100));
  add(`60-zoom${z}-formulaire`, card("cli-fictif-3"), [...newRx, scrollTo("Correction optique")], W, { scale });
  add(`61-zoom${z}-liste-sombre`, u("route=/optique&theme=dark"), [], W, { scale });
}
writeFileSync(process.argv[2], JSON.stringify(plan, null, 1));
console.log(plan.length, "captures");
