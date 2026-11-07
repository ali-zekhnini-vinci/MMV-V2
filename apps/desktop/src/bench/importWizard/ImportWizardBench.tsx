// BANC GATE B (CSV-IMPORT-AI-01) — maquette NAVIGABLE de l'assistant d'import CSV en cinq étapes, à valider par le
// propriétaire AVANT toute intégration (mandat de refonte du 04/11/2026). Composants et jetons du kit Lot 6 (CatalogUi,
// Catalog.css, Clients.css) ; aucune requête réseau ; aucune règle métier côté React : statuts, diagnostics, compteurs et
// actions licites figurent dans fixtures.ts comme le SERVEUR devra les fournir (contrats proposés, dossier Gate B § 7).
import {
  ArrowLeft, ArrowRight, Ban, Beaker, Bot, CheckCheck, ChevronDown, CircleCheck, CircleX, ClipboardCheck, Clock, Copy, FileSpreadsheet,
  FileText, FileUp, History, Info, Layers, ListChecks, Moon, Pencil, Play, RotateCcw, Server, ShieldCheck, Sparkles, Sun, Tags, TriangleAlert, Upload, X,
} from "lucide-react";
import { type ReactNode, useEffect, useState } from "react";
import { Button } from "../../components/ui/button.tsx";
import { cn } from "../../lib/utils.ts";
import "../../clients/Clients.css";
import "../../catalog/Catalog.css";
import "../bench.css";
import "./importWizard.css";
import { Badge, type BadgeTone, Banner, Card, Modal, PageHeader, RadioCard, SelectField, TableFoot, TablePanel, TableScroll } from "../../catalog/CatalogUi.tsx";
import {
  type AnalysisEvent, BENCH_STATES, EVENTS_DONE, EVENTS_DOWN, EVENTS_QUEUED, EVENTS_RUNNING, FAMILIES_FOUND, FILE, HISTORY, MODEL_DIGEST, MODEL_DONE,
  MODEL_DOWN, MODEL_LABEL, MODEL_OK, type ModelState, ORIGINAL_PREVIEW, PRICE_EXAMPLES, REVIEW_ROWS, type ReviewRow, type RowStatus, STEP_LABELS,
  STEP_ORDER, type StepKey, type StepState, STRUCTURE_ANOMALIES,
} from "./fixtures.ts";

const ROW_STATUS: Record<RowStatus, { label: string; tone: BadgeTone }> = {
  valid: { label: "Valide", tone: "ok" },
  warning: { label: "Avertissement", tone: "warn" },
  ambiguous: { label: "À confirmer", tone: "accent" },
  invalid: { label: "À corriger", tone: "err" },
  unrecognized: { label: "Non reconnue", tone: "arch" },
  ignored: { label: "Ignorée", tone: "arch" },
};

const time = (ms: number) => {
  const s = ms / 1000;
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${(s % 60).toLocaleString("fr-FR", { minimumIntegerDigits: 2, minimumFractionDigits: 1, maximumFractionDigits: 1 })}`;
};

/** États de la navigation horizontale pour chaque état du banc (le serveur en sera la source : statut du lot + analyse). */
function stepsOf(state: string): Record<StepKey, { state: StepState; sub: string }> {
  const locked = (sub: string) => ({ state: "locked" as const, sub });
  const done = (sub: string) => ({ state: "done" as const, sub });
  const analysisDone = done(`${MODEL_LABEL} · 24,4 s`);
  switch (state) {
    case "depot-vide":
      return { deposit: { state: "current", sub: "Aucun fichier" }, analysis: locked("Après le dépôt"), review: locked("—"), check: locked("—"), import: locked("—") };
    case "depot-fichier":
      return { deposit: { state: "current", sub: "1 fichier prêt" }, analysis: locked("Après le dépôt"), review: locked("—"), check: locked("—"), import: locked("—") };
    case "depot-refus":
      return { deposit: { state: "blocked", sub: "Fichier refusé (taille)" }, analysis: locked("Après le dépôt"), review: locked("—"), check: locked("—"), import: locked("—") };
    case "analyse-attente":
      return { deposit: done("Fichier reçu"), analysis: { state: "current", sub: "En attente d'Ollama" }, review: locked("Après l'analyse"), check: locked("—"), import: locked("—") };
    case "analyse-cours":
      return { deposit: done("Fichier reçu"), analysis: { state: "current", sub: "En cours · 00:12" }, review: locked("Après l'analyse"), check: locked("—"), import: locked("—") };
    case "analyse-terminee":
      return { deposit: done("Fichier reçu"), analysis: { state: "current", sub: "Terminée · 24,4 s" }, review: { state: "attention", sub: "5 à corriger · 3 à confirmer" }, check: locked("Après vérification"), import: locked("—") };
    case "analyse-indispo":
      return { deposit: done("Fichier reçu"), analysis: { state: "blocked", sub: "Ollama indisponible" }, review: locked("Votre choix requis"), check: locked("—"), import: locked("—") };
    case "verif-liste":
    case "verif-sku":
    case "verif-commercial":
      return { deposit: done("Fichier reçu"), analysis: analysisDone, review: { state: "current", sub: "5 à corriger · 3 à confirmer" }, check: { state: "todo", sub: "Non contrôlé" }, import: locked("Après contrôle") };
    case "controle-bloque":
      return { deposit: done("Fichier reçu"), analysis: analysisDone, review: { state: "attention", sub: "2 points restants" }, check: { state: "blocked", sub: "2 points bloquants" }, import: locked("Bloqué") };
    case "controle-pret":
      return { deposit: done("Fichier reçu"), analysis: analysisDone, review: done("Corrections revalidées"), check: { state: "current", sub: "Prêt · révision 6" }, import: { state: "todo", sub: "Confirmation requise" } };
    case "import-confirmation":
      return { deposit: done("Fichier reçu"), analysis: analysisDone, review: done("Corrections revalidées"), check: done("Prêt · révision 6"), import: { state: "current", sub: "Confirmation requise" } };
    case "import-cours":
      return { deposit: done("Fichier reçu"), analysis: analysisDone, review: done("Verrouillé"), check: done("Prêt · révision 6"), import: { state: "current", sub: "Écriture en cours" } };
    default:
      return { deposit: done("Fichier reçu"), analysis: analysisDone, review: done("Verrouillé"), check: done("Contrôlé"), import: done("Importé · 14:38") };
  }
}

const STEP_TARGET: Record<StepKey, string> = {
  deposit: "depot-fichier", analysis: "analyse-terminee", review: "verif-liste", check: "controle-pret", import: "import-confirmation",
};

export function ImportWizardBench({ initialState, theme: initialTheme, historyOpen }: { initialState: string; theme: "light" | "dark"; historyOpen: boolean }) {
  const [state, setState] = useState(initialState);
  const [theme, setTheme] = useState(initialTheme);
  useEffect(() => {
    const root = document.documentElement;
    root.dataset.theme = theme;
    return () => { delete root.dataset.theme; };
  }, [theme]);
  const current = BENCH_STATES.find((s) => s.id === state) ?? BENCH_STATES[0];
  const groups = [...new Set(BENCH_STATES.map((s) => s.group))];
  return (
    <div className="bench">
      <div className="bench__banner" role="note">
        <Beaker size={16} aria-hidden="true" />
        <strong>BANC GATE B — maquette non intégrée</strong> · assistant d&apos;import CSV · données fictives · aucune requête réseau · à valider par le propriétaire
      </div>
      <aside className="bench__side" aria-label="États de la maquette">
        {groups.map((group) => (
          <nav key={group} className="bench__nav" aria-label={group}>
            <p className="bench__hint"><strong>{group}</strong></p>
            {BENCH_STATES.filter((s) => s.group === group).map((s) => (
              <button key={s.id} type="button" className="bench__navitem" aria-current={s.id === state ? "page" : undefined} onClick={() => setState(s.id)}>
                {s.label}
              </button>
            ))}
          </nav>
        ))}
        <div className="bench__controls">
          <Button size="sm" onClick={() => setTheme((t) => (t === "dark" ? "light" : "dark"))}>
            {theme === "dark" ? <Sun size={14} aria-hidden="true" /> : <Moon size={14} aria-hidden="true" />}
            {theme === "dark" ? "Thème clair" : "Thème sombre"}
          </Button>
          <p className="bench__hint">URL : ?state={state}{theme === "dark" ? "&theme=dark" : ""}</p>
        </div>
      </aside>
      <main className="bench__content">
        <Wizard key={state} state={state} step={current.step} onGo={setState} historyOpen={historyOpen} />
      </main>
    </div>
  );
}

function modelBadge(state: string): ReactNode {
  if (state === "analyse-indispo") return <Badge tone="warn" icon={Bot}>IA locale indisponible · règles seules possibles</Badge>;
  if (state.startsWith("depot")) return <Badge tone="accent" icon={Bot}>IA locale · {MODEL_LABEL} configuré</Badge>;
  if (state === "analyse-attente" || state === "analyse-cours") return <Badge tone="accent" icon={Bot}>IA locale · {MODEL_LABEL} configuré · analyse en cours</Badge>;
  return <Badge tone="ok" icon={Bot}>Analyse IA terminée · {MODEL_LABEL} (confirmé)</Badge>;
}

function Wizard({ state, step, onGo, historyOpen }: { state: string; step: StepKey; onGo: (state: string) => void; historyOpen: boolean }) {
  const steps = stepsOf(state);
  return (
    <section className="cat-screen" aria-labelledby="import-title">
      <PageHeader title="Import CSV" subtitle="Assistant d'import : l'IA locale interprète, le serveur valide, vous arbitrez. Rien n'est écrit au catalogue avant votre confirmation finale."
        titleExtra={modelBadge(state)} />
      <ol className="cat-steps cat-steps--wizard iw-steps" aria-label="Étapes de l'import">
        {STEP_ORDER.map((key, index) => {
          const s = steps[key];
          const isCurrent = key === step;
          return (
            <li key={key} className={cn("cat-step", s.state === "done" && !isCurrent && "cat-step--done", isCurrent && "cat-step--current",
              s.state === "attention" && "iw-step--attention", s.state === "blocked" && "iw-step--blocked", s.state === "stale" && "iw-step--stale", s.state === "locked" && "iw-step--locked")}>
              <button type="button" className="cat-step__btn" disabled={s.state === "locked"} aria-current={isCurrent ? "step" : undefined}
                onClick={() => onGo(STEP_TARGET[key])}>
                <span className="cat-step__n">
                  {s.state === "done" && !isCurrent ? <CheckCheck size={14} aria-hidden="true" /> : s.state === "blocked" ? <Ban size={13} aria-hidden="true" /> : s.state === "attention" ? <TriangleAlert size={13} aria-hidden="true" /> : index + 1}
                </span>
                <span className="iw-step__text">
                  <span className="iw-step__label">{STEP_LABELS[key]}</span>
                  <span className="iw-step__sub">{s.sub}</span>
                </span>
              </button>
            </li>
          );
        })}
      </ol>
      {step === "deposit" && <DepositStep state={state} onGo={onGo} />}
      {step === "analysis" && <AnalysisStep state={state} onGo={onGo} />}
      {step === "review" && <ReviewStep state={state} onGo={onGo} />}
      {step === "check" && <CheckStep state={state} onGo={onGo} />}
      {step === "import" && <ImportStep state={state} onGo={onGo} />}
      <HistorySection initiallyOpen={historyOpen} />
    </section>
  );
}

// ---------------------------------------------------------------- étape 1
function DepositStep({ state, onGo }: { state: string; onGo: (state: string) => void }) {
  const [kind, setKind] = useState("catalog");
  const [target, setTarget] = useState("products");
  const [supplier, setSupplier] = useState(state === "depot-fichier" ? "lumen" : "");
  const hasFile = state === "depot-fichier";
  const refused = state === "depot-refus";
  return (
    <>
      <Card title="Déposer le fichier" icon={FileUp}>
        <div className="cat-stack">
          <div className="cat-stack" style={{ gap: 8 }}>
            <span className="iw-section-title">Que contient le fichier ?</span>
            <div className="cat-seg" role="radiogroup" aria-label="Type de fichier">
              {[["catalog", "Catalogue produits"], ["suppliers", "Fournisseurs"], ["prices", "Prix du magasin"]].map(([value, text]) => (
                <button key={value} type="button" role="radio" aria-checked={kind === value} aria-pressed={kind === value} className="cat-seg__item" onClick={() => setKind(value)}>{text}</button>
              ))}
            </div>
            {kind !== "catalog" && <p className="clients-field__hint" style={{ margin: 0 }}><Info size={12} aria-hidden="true" /> Fournisseurs et prix : parcours actuel par les règles, sans assistant IA (inchangé par ce lot).</p>}
          </div>
          {refused && (
            <Banner tone="err" icon={CircleX} title="Fichier refusé : limite technique dépassée" role="alert">
              « catalogue_complet_2026.csv » pèse <strong>2,6 Mio</strong> ; un lot d&apos;import accepte <strong>2 Mio et 5 000 lignes</strong> au plus.
              Rien n&apos;a été enregistré. Découpez le fichier (par exemple une partie par famille) puis déposez chaque partie.
            </Banner>
          )}
          {!hasFile ? (
            <label className="cat-drop">
              <Upload size={28} aria-hidden="true" />
              <span className="cat-drop__title">Glissez votre fichier ici, ou choisissez-le</span>
              <span className="clients-field__hint">CSV ou texte, séparateur et encodage reconnus automatiquement · 2 Mio et 5 000 lignes au plus</span>
              <span className="clients-actions"><span className="clients-chip"><FileSpreadsheet size={16} aria-hidden="true" />Choisir un fichier…</span></span>
              <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" />
            </label>
          ) : (
            <div className="iw-file">
              <FileSpreadsheet size={22} aria-hidden="true" />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div className="iw-file__name">{FILE.name}</div>
                <div className="iw-file__meta">{FILE.size} · {FILE.lines} lignes · {FILE.columns} colonnes · {FILE.encoding} · séparateur {FILE.delimiter}</div>
              </div>
              <Button size="sm" variant="tertiary"><RotateCcw size={14} aria-hidden="true" />Remplacer</Button>
              <Button size="sm" variant="tertiary"><X size={14} aria-hidden="true" />Retirer</Button>
            </div>
          )}
          {!refused && (
            <p className="clients-field__hint" style={{ margin: 0 }}>
              <ShieldCheck size={12} aria-hidden="true" /> Un fichier imparfait est accepté pour analyse (doublons, colonnes inconnues, en-têtes absents, prix ambigus, lignes irrégulières) :
              ces points seront présentés à l&apos;étape 3. Seules les limites techniques (taille, nombre de lignes, fichier illisible ou non textuel) empêchent le dépôt.
            </p>
          )}
          <div className="cat-grid">
            <SelectField label="Fournisseur du fichier (facultatif)" name="supplier" value={supplier} onChange={setSupplier}
              hint="Désigné : sa correspondance apprise est proposée et les lignes lui sont rattachées."
              options={[{ value: "", label: "— Aucun —" }, { value: "lumen", label: FILE.supplier }]} />
            <div className="cat-stack" style={{ gap: 8 }}>
              <span className="iw-section-title">Destination {hasFile && <Badge tone="info">suggestion du serveur</Badge>}</span>
              <div className="cat-radios" role="radiogroup" aria-label="Destination">
                <RadioCard name="target" value="products" checked={target === "products"} onChange={setTarget} label="Catalogue des produits"
                  sub={hasFile ? "Suggéré : mesures de monture dans 46 lignes, mot-clé « solaire » dans 12 lignes" : "Montures, solaires, lentilles, accessoires"} />
                <RadioCard name="target" value="lenses" checked={target === "lenses"} onChange={setTarget} label="Référentiel des verres" sub="Gammes, indices, traitements et offres fournisseurs" />
              </div>
            </div>
          </div>
        </div>
      </Card>
      {hasFile && (
        <TablePanel foot={<TableFoot icon={FileText}>8 premières lignes sur {FILE.lines} · texte d&apos;origine, sans aucune interprétation · ∅ = cellule vide</TableFoot>}>
          <div className="cat-card__head" style={{ padding: "12px 16px" }}><h2 className="cat-card__title"><FileText size={18} aria-hidden="true" />Aperçu du fichier d&apos;origine</h2></div>
          <TableScroll label="Aperçu du fichier d'origine">
            <table className="iw-raw">
              <tbody>
                {ORIGINAL_PREVIEW.map((cells, i) => (
                  <tr key={i}><th scope="row">L{i + 1}</th>{cells.map((c, j) => <td key={j}>{c}</td>)}</tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        </TablePanel>
      )}
      <div className="iw-bar">
        <span className="iw-bar__info">
          {hasFile ? "L'analyse démarre dès le dépôt : lecture complète par le serveur, puis interprétation par l'IA locale. Vous pourrez quitter l'écran pendant l'analyse." : "Choisissez un fichier pour continuer."}
        </span>
        <Button variant="primary" disabled={!hasFile} onClick={() => onGo("analyse-cours")}><Sparkles size={16} aria-hidden="true" />Analyser mon fichier</Button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- étape 2
function Progress({ label, done, total, ok }: { label: string; done: number; total: number; ok?: boolean }) {
  return (
    <div className="iw-progress">
      <div className="iw-progress__head"><span>{label}</span><span className="tnum">{done} / {total}</span></div>
      <div className="iw-progress__track" role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={total} aria-valuenow={done}>
        <div className={cn("iw-progress__fill", ok && "iw-progress__fill--ok")} style={{ width: `${(done / total) * 100}%` }} />
      </div>
    </div>
  );
}

function Indeterminate({ label, note }: { label: string; note: string }) {
  return (
    <div className="iw-progress iw-progress--indeterminate">
      <div className="iw-progress__head"><span><strong>{label}</strong></span><span>en cours</span></div>
      {/* Aucun pourcentage : la durée d'une réponse du modèle n'est pas mesurable à l'avance. */}
      <div className="iw-progress__track" role="progressbar" aria-label={label} aria-valuetext="en cours, durée non mesurable"><div className="iw-progress__fill" /></div>
      <span className="clients-field__hint">{note}</span>
    </div>
  );
}

const EVENT_ICON: Record<AnalysisEvent["tone"], typeof Info> = { ok: CircleCheck, info: Info, warn: TriangleAlert, err: CircleX, accent: Sparkles };

function AnalysisStep({ state, onGo }: { state: string; onGo: (state: string) => void }) {
  const variant = state === "analyse-attente" ? "queued" : state === "analyse-cours" ? "running" : state === "analyse-indispo" ? "down" : "done";
  const events = { queued: EVENTS_QUEUED, running: EVENTS_RUNNING, down: EVENTS_DOWN, done: EVENTS_DONE }[variant];
  const model: ModelState = { queued: MODEL_OK, running: MODEL_OK, down: MODEL_DOWN, done: MODEL_DONE }[variant];
  const elapsed = { queued: "00:41", running: "00:12", down: "00:03", done: "00:24" }[variant];
  const live = variant === "running" || variant === "queued";
  return (
    <>
      <div className="iw-layout">
        <div className="iw-main">
          <Card title="Analyse automatique" icon={Sparkles}
            actions={<span className="clients-muted tnum" role="timer" aria-label="Durée écoulée"><Clock size={14} aria-hidden="true" /> Durée écoulée {elapsed}</span>}>
            <div className="cat-stack">
              <p className="clients-muted" style={{ margin: 0 }}><FileSpreadsheet size={14} aria-hidden="true" /> {FILE.name} · {FILE.size} · reçu à 14:31:52</p>
              <div className="iw-facts">
                <div className="cat-counter"><div className="cat-counter__label">Lignes lues</div><div className="cat-counter__value">48</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Colonnes</div><div className="cat-counter__value">11</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Lignes irrégulières</div><div className="cat-counter__value cat-tone-warn">1</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Lignes vues par le modèle</div><div className="cat-counter__value">{variant === "down" ? "0" : "40"}<span className="clients-muted" style={{ fontSize: 13 }}> / 47</span></div></div>
              </div>
              <Progress label="Lecture complète du fichier (serveur)" done={48} total={48} ok />
              <Progress label="Première validation par les règles (serveur)" done={47} total={47} ok />
              {variant === "queued" && (
                <Indeterminate label="Analyse Ollama en attente" note="Une analyse d'un autre poste est en cours sur ce serveur (une seule à la fois). Position dans la file : 1. Durée non prévisible." />
              )}
              {variant === "running" && (
                <Indeterminate label={`Interprétation des colonnes par ${MODEL_LABEL}`} note="Le modèle répond en une seule fois : aucune progression intermédiaire n'est mesurable. Mesures du banc B0 sur le poste de développement : 18 à 55 s." />
              )}
              {variant === "done" && (
                <>
                  <Progress label="Revalidation avec la proposition du modèle (serveur)" done={47} total={47} ok />
                  <Banner tone="ok" icon={CircleCheck} title="Analyse terminée en 24,4 s" compact>
                    9 colonnes reconnues, 2 à confirmer, 1 non reconnue ; 5 lignes à corriger. Le modèle n&apos;a rien décidé seul : ses propositions ont été revalidées par le serveur.
                  </Banner>
                </>
              )}
              {variant === "down" && (
                <Banner tone="err" icon={CircleX} title="Assistant IA indisponible : Ollama ne répond pas" role="alert"
                  action={<div className="clients-actions clients-actions--wrap"><Button size="sm"><RotateCcw size={14} aria-hidden="true" />Relancer l&apos;analyse IA</Button></div>}>
                  La lecture et la première validation par les règles sont terminées. Vous pouvez relancer l&apos;analyse IA, ou poursuivre <strong>sans IA</strong> :
                  MMV n&apos;utilisera que ses règles et vos confirmations, sans second avis capable de signaler une erreur des règles. Ce choix est enregistré dans l&apos;historique du lot.
                </Banner>
              )}
            </div>
          </Card>
          <Card title="Activité du serveur" icon={ListChecks} actions={live ? <Badge tone="accent">suivi en direct</Badge> : undefined}>
            <ol className="iw-activity" aria-live="polite" aria-label="Événements de l'analyse">
              {events.map((e, i) => {
                const Icon = EVENT_ICON[e.tone];
                const isLive = live && i === events.length - 1;
                return (
                  <li key={e.seq} className={cn("iw-event", isLive && "iw-event--live")}>
                    <span className="iw-event__time">{time(e.atMs)}</span>
                    <span className={cn("iw-event__dot", `cat-ic--${e.tone}`)} aria-hidden="true"><Icon size={13} /></span>
                    <span><span className="iw-event__title">{e.title}</span><br /><span className="iw-event__detail">{e.detail}</span></span>
                  </li>
                );
              })}
            </ol>
          </Card>
        </div>
        <div className="iw-aside">
          <Card title="Moteur d'analyse" icon={Server}>
            <div className="iw-model">
              <div className="iw-model__row"><span>Ollama (serveur local)</span><span>{model.available === false ? <Badge tone="err">Injoignable</Badge> : <Badge tone="ok">Connecté · v{model.ollamaVersion}</Badge>}</span></div>
              <div className="iw-model__row"><span>Adresse</span><span className="iw-mono">{model.endpoint}</span></div>
              <div className="iw-model__row"><span>Modèle configuré</span><span className="iw-mono">{model.configured}</span></div>
              <div className="iw-model__row"><span>Modèle actif</span>
                <span>{model.active ? <>{model.active.name} <span className="clients-muted iw-mono">{MODEL_DIGEST}</span><br /><Badge tone="ok" icon={CircleCheck}>confirmé par la réponse</Badge></>
                  : model.available === false ? "—" : <span className="clients-muted">en attente de la réponse</span>}</span>
              </div>
              <div className="iw-model__row"><span>Lignes transmises au modèle</span><span>{variant === "down" ? "aucune" : "40 sur 47 (échantillon)"}</span></div>
              <div className="iw-model__row"><span>Lignes contrôlées par le serveur</span><span>47 sur 47</span></div>
              <p className="clients-field__hint" style={{ margin: 0 }}>
                <Info size={12} aria-hidden="true" /> Le modèle reçoit un échantillon représentatif ; le serveur lit et contrôle chaque ligne. Aucune donnée ne quitte le serveur du magasin.
              </p>
            </div>
          </Card>
          <Card title="Familles identifiées" icon={Layers}>
            <ul className="iw-list">
              {FAMILIES_FOUND.map((f) => (
                <li key={f.label}><span>{f.label} <span className="clients-muted">· {f.note}</span></span><Badge tone={ROW_STATUS[f.status].tone}>{f.count} lignes</Badge></li>
              ))}
            </ul>
          </Card>
          <Card title="Anomalies de structure" icon={TriangleAlert}>
            <ul className="iw-list">
              {STRUCTURE_ANOMALIES.map((a) => <li key={a.line}><span><strong>L{a.line}</strong> · {a.text}</span></li>)}
            </ul>
          </Card>
        </div>
      </div>
      <div className="iw-bar">
        <span className="iw-bar__info">
          {live && "Vous pouvez quitter cet écran : l'analyse continue sur le serveur et se retrouve dans « Imports récents ». L'import reste impossible tant qu'elle n'est pas terminée ou annulée."}
          {variant === "done" && "Étape suivante : vérifier les lignes et confirmer ce que MMV ne peut pas décider seul."}
          {variant === "down" && "Aucune décision n'est prise à votre place : choisissez de relancer l'IA ou de vérifier sans IA."}
        </span>
        {live && <Button variant="danger"><X size={16} aria-hidden="true" />Annuler l&apos;analyse IA</Button>}
        {variant === "down" && <Button onClick={() => onGo("verif-liste")}><ShieldCheck size={16} aria-hidden="true" />Vérifier sans IA</Button>}
        <Button variant="primary" disabled={variant !== "done"} onClick={() => onGo("verif-liste")}>Vérifier et corriger<ArrowRight size={16} aria-hidden="true" /></Button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- étape 3
const FILTERS: { key: string; label: string; count: number; match: (r: ReviewRow) => boolean }[] = [
  { key: "all", label: "Toutes", count: 48, match: () => true },
  { key: "valid", label: "Valides", count: 6, match: (r) => r.status === "valid" },
  { key: "fix", label: "Corrections nécessaires", count: 5, match: (r) => r.status === "invalid" },
  { key: "ambiguous", label: "Ambiguïtés", count: 36, match: (r) => r.status === "ambiguous" || r.status === "warning" },
  { key: "unrecognized", label: "Non reconnues", count: 1, match: (r) => r.status === "unrecognized" },
  { key: "duplicates", label: "Doublons potentiels", count: 3, match: (r) => r.duplicate === true },
];

function ReviewStep({ state, onGo }: { state: string; onGo: (state: string) => void }) {
  const [filter, setFilter] = useState(state === "verif-liste" ? "all" : state === "verif-sku" ? "duplicates" : "all");
  const active = FILTERS.find((f) => f.key === filter) ?? FILTERS[0];
  const rows = REVIEW_ROWS.filter(active.match);
  return (
    <>
      <Card title="À confirmer pour plusieurs lignes à la fois" icon={Layers} actions={<span className="clients-muted">3 confirmations · 1 information</span>}>
        <TableScroll label="Confirmations groupées">
          <table className="clients-table cat-table cat-table--inner cat-table--compact">
            <thead><tr><th scope="col">Question</th><th scope="col">Lignes</th><th scope="col">Proposition</th><th scope="col">État</th><th scope="col"><span className="sr-only">Action</span></th></tr></thead>
            <tbody>
              <tr className={state === "verif-commercial" ? "is-selected" : undefined}>
                <td><span className="cat-strong">Colonne « Prix net HT » : quel montant ?</span><br /><span className="clients-muted">montant non qualifié par l&apos;intitulé</span></td>
                <td className="tnum">45</td>
                <td>Prix d&apos;achat HT <Badge tone="accent" icon={Bot}>suggestion IA</Badge></td>
                <td><Badge tone="accent">À confirmer</Badge></td>
                <td><Button size="sm" onClick={() => onGo("verif-commercial")}>Vérifier les exemples</Button></td>
              </tr>
              <tr>
                <td><span className="cat-strong">Famille des lignes sans mot-clé</span><br /><span className="clients-muted">mesures de monture présentes, ni « monture » ni « solaire »</span></td>
                <td className="tnum">35</td>
                <td>Montures optiques <Badge tone="accent" icon={Bot}>suggestion IA</Badge></td>
                <td><Badge tone="accent">À confirmer</Badge></td>
                <td><Button size="sm">Choisir la famille</Button></td>
              </tr>
              <tr>
                <td><span className="cat-strong">Ligne 8 sans identité, juste après la ligne 7</span><br /><span className="clients-muted">reprendre « LU-2050 titane » (aucune autre valeur reprise)</span></td>
                <td className="tnum">1</td>
                <td>Variante de LU-2050 <Badge tone="info">règle</Badge></td>
                <td><Badge tone="accent">À confirmer</Badge></td>
                <td><Button size="sm">Confirmer ou refuser</Button></td>
              </tr>
              <tr>
                <td><span className="cat-strong">Colonne « PVC TTC »</span><br /><span className="clients-muted">MMV n&apos;importe pas de prix TTC (aucune conversion)</span></td>
                <td className="tnum">47</td>
                <td>Non importée, conservée dans le lot</td>
                <td><Badge tone="arch">Information</Badge></td>
                <td />
              </tr>
            </tbody>
          </table>
        </TableScroll>
      </Card>
      {state === "verif-sku" && <SkuPanel />}
      {state === "verif-commercial" && <CommercialPanel />}
      <div className="iw-filters" role="toolbar" aria-label="Filtrer les lignes">
        {FILTERS.map((f) => (
          <button key={f.key} type="button" className={cn("clients-chip", filter === f.key && "clients-chip--on")} aria-pressed={filter === f.key} onClick={() => setFilter(f.key)}>
            {f.label}<span className="iw-count">{f.count}</span>
          </button>
        ))}
      </div>
      <TablePanel foot={<TableFoot icon={FileSpreadsheet}>{rows.length} ligne(s) affichée(s) — extrait du banc ; le filtre « {active.label} » compte {active.count} ligne(s) sur 48</TableFoot>}>
        <TableScroll label="Lignes analysées">
          <table className="clients-table cat-table cat-table--compact">
            <thead>
              <tr>
                <th scope="col" className="cat-num">Ligne</th><th scope="col">Famille</th><th scope="col">Produit</th><th scope="col">Variante</th><th scope="col">Référence (SKU)</th>
                <th scope="col">Prix d&apos;achat HT</th><th scope="col">Résultat</th><th scope="col">Anomalies</th><th scope="col"><span className="sr-only">Action</span></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.line} className={cn(r.status === "invalid" && "is-warning", state === "verif-sku" && r.line === 10 && "is-selected")}>
                  <td className="cat-num tnum">{r.line}</td>
                  <td>{r.family.label}{r.family.status === "ambiguous" && <><br /><span className="clients-muted" style={{ fontSize: 12 }}>à confirmer</span></>}</td>
                  <td className="cat-strong">{r.product}</td>
                  <td className="cat-secondary">{r.variant}</td>
                  <td className="iw-mono">{r.reference}</td>
                  <td className="tnum">{r.amounts}</td>
                  <td><Badge tone={ROW_STATUS[r.status].tone}>{ROW_STATUS[r.status].label}</Badge></td>
                  <td><ul className="iw-issues">{r.issues.map((i) => <li key={i}>{i}</li>)}</ul></td>
                  <td>{r.status === "invalid" ? <Button size="sm" onClick={() => r.line === 10 && onGo("verif-sku")}><Pencil size={14} aria-hidden="true" />Corriger</Button>
                    : r.status !== "valid" ? <Button size="sm" variant="tertiary">Voir</Button> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableScroll>
      </TablePanel>
      <div className="iw-bar">
        <Button variant="tertiary" onClick={() => onGo("analyse-terminee")}><ArrowLeft size={16} aria-hidden="true" />Analyse</Button>
        <span className="iw-bar__info">Révision 1 du lot · vos décisions sont enregistrées au fil de l&apos;eau sur le serveur : revenir en arrière ou fermer l&apos;écran ne les perd pas.</span>
        <Button variant="primary" onClick={() => onGo("controle-bloque")}><ClipboardCheck size={16} aria-hidden="true" />Contrôler avant import</Button>
      </div>
    </>
  );
}

function SkuPanel() {
  const [action, setAction] = useState("");
  const [value, setValue] = useState("");
  const causes: [boolean, string, string][] = [
    [true, "Le fichier lui-même", "deux lignes du fichier portent le SKU MON-LU2041 (lignes 2 et 10)."],
    [false, "Une mauvaise correspondance de colonnes", "« Réf. interne » compte 46 valeurs distinctes sur 47 : seule cette valeur se répète."],
    [false, "Un article déjà présent dans le catalogue", "aucun article du catalogue ne porte MON-LU2041."],
    [false, "Une variante mal associée", "aucune ligne de continuation n'intervient (les deux lignes ont leur propre identité)."],
    [false, "Un véritable doublon", "coloris (Noir / Havane) et codes-barres différents : ce sont deux variantes distinctes."],
  ];
  return (
    <Card title="Ligne 10 — SKU interne en double" icon={Copy} className="iw-card--accent" actions={<Badge tone="err">À corriger</Badge>}>
      <div className="iw-panel">
        <div className="iw-compare">
          {[
            ["Ligne 2", "Monture LU-2041 acétate", "Noir", "3760000000017"],
            ["Ligne 10 (signalée)", "Monture LU-2041 acétate", "Havane", "3760000000093"],
          ].map(([title, name, color, ean]) => (
            <div key={String(title)} className="iw-compare__box">
              <div className="iw-compare__title">{title} — valeurs d&apos;origine</div>
              <div className="iw-list" style={{ gap: 4 }}>
                <div>Réf. interne : <span className={cn("iw-mono", "iw-hl")}>MON-LU2041</span></div>
                <div>Désignation : {name}</div>
                <div>Coloris : <strong>{color}</strong> · 52□18 145</div>
                <div>EAN : <span className="iw-mono">{ean}</span></div>
              </div>
            </div>
          ))}
        </div>
        <div>
          <p className="iw-section-title" style={{ marginBottom: 8 }}>Diagnostic du serveur — d&apos;où vient le conflit ?</p>
          <ul className="iw-diagnosis">
            {causes.map(([hit, title, text]) => (
              <li key={title} className={hit ? "is-cause" : undefined}>
                {hit ? <CircleCheck size={16} className="cat-tone-err" aria-label="cause retenue" /> : <X size={16} className="clients-muted" aria-label="écarté" />}
                <span>{title} — {text}</span>
              </li>
            ))}
          </ul>
        </div>
        <Banner tone="info" icon={Info} title="Deux variantes différentes portent le même SKU explicite" compact>
          MMV ne remplace jamais un SKU explicite et ne fusionne pas deux articles d&apos;après une intuition. Choisissez l&apos;une des actions possibles :
        </Banner>
        <div className="iw-options" role="radiogroup" aria-label="Action de correction">
          <RadioCard name="sku-action" value="rename" checked={action === "rename"} onChange={setAction} label="Modifier le SKU de la ligne 10"
            sub="Vous saisissez la nouvelle valeur ; le serveur vérifie son unicité dans le fichier et dans le catalogue." />
          {action === "rename" && (
            <div className="cat-grid" style={{ paddingLeft: 30 }}>
              <label className="cat-stack" style={{ gap: 6 }}>
                <span className="clients-field__label">Nouveau SKU (ligne 10)</span>
                <input className="cat-fix__input iw-mono" value={value} onChange={(e) => setValue(e.target.value)} aria-describedby="sku-note" placeholder="MMV ne propose aucune valeur" />
                <span id="sku-note" className="clients-field__hint">Valeur d&apos;origine « MON-LU2041 » conservée dans le fichier d&apos;origine (octets inchangés).</span>
              </label>
            </div>
          )}
          <RadioCard name="sku-action" value="skip10" checked={action === "skip10"} onChange={setAction} label="Ne pas importer la ligne 10" sub="Ligne ignorée volontairement, tracée dans le lot et l'historique." />
          <RadioCard name="sku-action" value="skip2" checked={action === "skip2"} onChange={setAction} label="Ne pas importer la ligne 2" sub="Ligne ignorée volontairement, tracée dans le lot et l'historique." />
        </div>
        <p className="clients-field__hint" style={{ margin: 0 }}>
          <Info size={12} aria-hidden="true" /> Actions non proposées ici : <strong>générer un SKU</strong> (le SKU est présent dans le fichier),
          <strong> associer à un article existant</strong> (aucun article du catalogue ne porte ce SKU), <strong>fusionner</strong> (déclinaisons différentes).
        </p>
        <div className="clients-actions" style={{ justifyContent: "flex-start" }}>
          <Button variant="primary" disabled={action === "" || (action === "rename" && value.trim() === "")}><ShieldCheck size={16} aria-hidden="true" />Enregistrer et revalider</Button>
          {action !== "" && <Badge tone="warn">Non revalidée</Badge>}
          <span className="clients-muted">La correction n&apos;est acquise qu&apos;après revalidation par le serveur (révision 2, à votre nom).</span>
        </div>
      </div>
    </Card>
  );
}

function CommercialPanel() {
  const [choice, setChoice] = useState("");
  return (
    <Card title="Colonne « Prix net HT » — que représente ce montant ?" icon={Tags} className="iw-card--accent" actions={<Badge tone="accent">45 lignes concernées</Badge>}>
      <div className="iw-panel">
        <div className="iw-compare">
          <div className="iw-compare__box">
            <div className="iw-compare__title">Interprétation</div>
            <div className="iw-list" style={{ gap: 4 }}>
              <div><Badge tone="info">Règles</Badge> montant <strong>non qualifié</strong> : « net » n&apos;indique ni achat ni vente → à confirmer</div>
              <div><Badge tone="accent" icon={Bot}>{MODEL_LABEL}</Badge> propose <strong>Prix d&apos;achat HT</strong> (confiance élevée)</div>
              <div><Badge tone="arch">Fournisseur</Badge> aucun import précédent de {FILE.supplier}</div>
            </div>
          </div>
          <div className="iw-compare__box">
            <div className="iw-compare__title">Valeurs de la colonne (toutes les lignes)</div>
            <div className="iw-list" style={{ gap: 4 }}>
              <div>45 lisibles · 1 illisible (L27 « 1.290 ») · 1 vide (L48)</div>
              <div>Minimum <strong className="tnum">12,90</strong> · médiane <strong className="tnum">39,90</strong> · maximum <strong className="tnum">118,00</strong></div>
              <div>Devise du magasin : EUR · <strong>aucune conversion</strong></div>
            </div>
          </div>
        </div>
        <TableScroll label="Exemples représentatifs">
          <table className="clients-table cat-table cat-table--inner cat-table--compact">
            <thead><tr><th scope="col" className="cat-num">Ligne</th><th scope="col">Valeur d&apos;origine</th><th scope="col">Lecture MMV</th><th scope="col">Pourquoi cet exemple</th></tr></thead>
            <tbody>{PRICE_EXAMPLES.map((e) => (
              <tr key={e.line} className={e.read === "—" ? "is-warning" : undefined}>
                <td className="cat-num tnum">{e.line}</td><td className="iw-mono">{e.raw}</td><td className="tnum cat-strong">{e.read}</td><td className="cat-secondary">{e.why}</td>
              </tr>
            ))}</tbody>
          </table>
        </TableScroll>
        <div className="iw-options" role="radiogroup" aria-label="Nature du montant">
          <span className="iw-section-title">Votre réponse (aucun choix par défaut pour un montant)</span>
          <RadioCard name="money" value="purchase" checked={choice === "purchase"} onChange={setChoice} label="Prix d'achat HT" sub="Coût d'achat chez ce fournisseur — droit costs.write requis (votre profil : accordé)" />
          <RadioCard name="money" value="sale" checked={choice === "sale"} onChange={setChoice} label="Prix de vente HT" sub="Prix de vente du magasin — droit pricing.manual requis" />
          <RadioCard name="money" value="ignore" checked={choice === "ignore"} onChange={setChoice} label="Ne pas importer cette colonne" sub="Conservée dans le lot d'origine, sans effet sur le catalogue" />
        </div>
        {choice === "purchase" && (
          <Banner tone="warn" icon={TriangleAlert} title="Conséquence si vous confirmez" compact>
            <strong>45 prix d&apos;achat HT</strong> (de 12,90 à 118,00 EUR) seront enregistrés pour <strong>{FILE.supplier}</strong> sur les variantes importées.
            Aucun prix de vente calculé, coefficients inchangés. La ligne 27 (« 1.290 ») reste à corriger séparément.
          </Banner>
        )}
        <div className="clients-actions" style={{ justifyContent: "flex-start" }}>
          <Button variant="primary" disabled={choice === ""}><CheckCheck size={16} aria-hidden="true" />Confirmer pour les 45 lignes</Button>
          <Button variant="tertiary">Annuler</Button>
          <span className="clients-muted">Pas de « tout confirmer » pour les montants : une confirmation par colonne commerciale, exemples sous les yeux.</span>
        </div>
      </div>
    </Card>
  );
}

// ---------------------------------------------------------------- étape 4
function Counter({ label, value, tone }: { label: string; value: number | string; tone?: "err" | "warn" | "ok" }) {
  return (
    <div className="cat-counter">
      <div className="cat-counter__label">{label}</div>
      <div className={cn("cat-counter__value", tone && `cat-tone-${tone}`)}>{value}</div>
    </div>
  );
}

function CheckStep({ state, onGo }: { state: string; onGo: (state: string) => void }) {
  const blocked = state === "controle-bloque";
  return (
    <>
      <Card title="Contrôle à blanc par le serveur" icon={ClipboardCheck}
        actions={<span className="clients-muted">Calculé à 14:36:12 · révision {blocked ? 4 : 6} · aucune écriture au catalogue</span>}>
        <div className="cat-stack">
          <div className="cat-counters">
            <Counter label="Produits à créer" value={14} />
            <Counter label="Variantes à créer" value={blocked ? 38 : 39} />
            <Counter label="Articles à modifier" value={3} />
            <Counter label="Articles inchangés" value={2} />
          </div>
          <div className="cat-counters">
            <Counter label="Lignes ignorées volontairement" value={blocked ? 2 : 3} />
            <Counter label="Doublons" value={blocked ? "1 résolu" : "2 résolus"} />
            <Counter label="Valeurs ambiguës" value={blocked ? 1 : 0} tone={blocked ? "warn" : undefined} />
            <Counter label="Erreurs bloquantes" value={blocked ? 1 : 0} tone={blocked ? "err" : "ok"} />
          </div>
          {blocked ? (
            <Banner tone="err" icon={CircleX} title="Import impossible : 2 points restent à régler (tout ou rien)" role="alert">
              <ul className="iw-list" style={{ marginTop: 6 }}>
                <li><span><strong>Ligne 27</strong> · Prix net HT « 1.290 » : milliers ou décimales ? MMV ne devine pas.</span><Button size="sm" onClick={() => onGo("verif-commercial")}>Corriger à l&apos;étape 3</Button></li>
                <li><span><strong>Ligne 31</strong> · 12 cellules au lieu de 11 : ligne mal alignée, à corriger ou à ignorer.</span><Button size="sm" onClick={() => onGo("verif-liste")}>Corriger à l&apos;étape 3</Button></li>
              </ul>
            </Banner>
          ) : (
            <Banner tone="ok" icon={CircleCheck} title="Contrôle réussi : 42 articles prêts à être importés" compact>
              14 produits et 39 variantes à créer, 3 articles à modifier ; 3 lignes ignorées à votre demande (L31, L36, L48). Rien n&apos;a encore été écrit.
            </Banner>
          )}
        </div>
      </Card>
      <div className="iw-layout">
        <div className="iw-main">
          <Card title="Articles existants modifiés — avant / après" icon={Pencil} flush>
            <TableScroll label="Avant et après">
              <table className="clients-table cat-table cat-table--inner cat-table--compact">
                <thead><tr><th scope="col">Article (SKU)</th><th scope="col">Champ</th><th scope="col">Avant (catalogue)</th><th scope="col">Après (fichier)</th><th scope="col" className="cat-num">Ligne</th></tr></thead>
                <tbody>
                  <tr><td className="iw-mono">SOL-SX310</td><td>Prix d&apos;achat HT ({FILE.supplier})</td><td className="tnum">49,80</td><td className="tnum cat-strong">51,20</td><td className="cat-num tnum">5</td></tr>
                  <tr><td className="iw-mono">SOL-SX310-GR</td><td>Code-barres (EAN)</td><td className="clients-muted">—</td><td className="iw-mono cat-strong">3760000000055</td><td className="cat-num tnum">6</td></tr>
                  <tr><td className="iw-mono">MON-LU2042</td><td>Libellé de variante</td><td>LU 2042 Or</td><td className="cat-strong">Or</td><td className="cat-num tnum">4</td></tr>
                </tbody>
              </table>
            </TableScroll>
          </Card>
          <Card title="Provenance des corrections" icon={History} flush>
            <TableScroll label="Provenance des corrections">
              <table className="clients-table cat-table cat-table--inner cat-table--compact">
                <thead><tr><th scope="col" className="cat-num">Rév.</th><th scope="col">Décision</th><th scope="col" className="cat-num">Lignes</th><th scope="col">Auteur</th><th scope="col">Revalidée</th></tr></thead>
                <tbody>
                  <tr><td className="cat-num tnum">2</td><td>L10 · SKU « MON-LU2041 » → « MON-LU2041-HA »</td><td className="cat-num tnum">1</td><td>A. Martin</td><td><Badge tone="ok">Oui · 14:33</Badge></td></tr>
                  <tr><td className="cat-num tnum">3</td><td>« Prix net HT » = prix d&apos;achat HT (suggestion IA confirmée)</td><td className="cat-num tnum">45</td><td>A. Martin</td><td><Badge tone="ok">Oui · 14:34</Badge></td></tr>
                  <tr><td className="cat-num tnum">4</td><td>Famille « Montures optiques » pour les lignes sans mot-clé</td><td className="cat-num tnum">35</td><td>A. Martin</td><td><Badge tone="ok">Oui · 14:35</Badge></td></tr>
                  {!blocked && <tr><td className="cat-num tnum">5</td><td>L27 · « 1.290 » → « 12,90 » (saisie)</td><td className="cat-num tnum">1</td><td>A. Martin</td><td><Badge tone="ok">Oui · 14:35</Badge></td></tr>}
                  {!blocked && <tr><td className="cat-num tnum">6</td><td>L31, L36, L48 ignorées pour cet import</td><td className="cat-num tnum">3</td><td>A. Martin</td><td><Badge tone="ok">Oui · 14:36</Badge></td></tr>}
                </tbody>
              </table>
            </TableScroll>
          </Card>
        </div>
        <div className="iw-aside">
          <Card title="Correspondances validées" icon={ListChecks}>
            <ul className="iw-list">
              {[
                ["Désignation", "Modèle / désignation", "règle"], ["Coloris", "Coloris", "règle"], ["Cal. · Pont · Branche", "Mesures de monture", "règle"],
                ["Réf. interne", "SKU interne", "règle"], ["EAN", "Code-barres (EAN)", "règle"], ["Code", "Référence fournisseur", "votre choix"],
                ["Prix net HT", "Prix d'achat HT", "IA confirmée par vous"], ["PVC TTC", "non importée", "règle"], ["Remarque", "non importée", "règle"],
              ].map(([col, field, origin]) => (
                <li key={col}><span><strong>{col}</strong> → {field}</span><span className="clients-muted">{origin}</span></li>
              ))}
            </ul>
          </Card>
        </div>
      </div>
      <div className="iw-bar">
        <Button variant="tertiary" onClick={() => onGo("verif-liste")}><ArrowLeft size={16} aria-hidden="true" />Retour à la vérification</Button>
        <span className="iw-bar__info">{blocked ? "Corrigez les points restants à l'étape 3 : le contrôle sera recalculé par le serveur." : "Toute nouvelle correction invalide ce contrôle : il faudra le relancer."}</span>
        <Button variant="primary" disabled={blocked} onClick={() => onGo("import-confirmation")}>Continuer vers l&apos;import<ArrowRight size={16} aria-hidden="true" /></Button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- étape 5
function ImportStep({ state, onGo }: { state: string; onGo: (state: string) => void }) {
  const [open, setOpen] = useState(state === "import-confirmation");
  const running = state === "import-cours";
  const success = state === "import-reussi";
  return (
    <>
      <Card title={success ? "Import enregistré" : "Synthèse définitive"} icon={success ? CircleCheck : Play}
        actions={success ? <Badge tone="ok" small={false}>Appliqué</Badge> : <Badge tone="accent" small={false}>Contrôlé · révision 6</Badge>}>
        <div className="cat-stack">
          {success ? (
            <Banner tone="ok" icon={CircleCheck} title="Import confirmé par le serveur à 14:38:05 (1,9 s)">
              Les nombres ci-dessous sont ceux <strong>réellement enregistrés</strong>, relus après la transaction. Ce fichier ne pourra pas être importé une seconde fois ;
              la correspondance est mémorisée pour {FILE.supplier}.
            </Banner>
          ) : running ? (
            <div className="cat-stack">
              <Indeterminate label="Écriture en une seule transaction" note="Tout ou rien : aucun article n'est visible avant la fin. Si la connexion est perdue, l'état réel du lot sera relu à la reconnexion." />
            </div>
          ) : (
            <p className="clients-muted" style={{ margin: 0 }}>Ce qui sera écrit, en une seule opération (tout ou rien) — rien d&apos;autre :</p>
          )}
          <div className="cat-counters">
            <Counter label={success ? "Produits créés" : "Produits à créer"} value={14} tone={success ? "ok" : undefined} />
            <Counter label={success ? "Variantes créées" : "Variantes à créer"} value={39} tone={success ? "ok" : undefined} />
            <Counter label={success ? "Articles modifiés" : "Articles à modifier"} value={3} />
            <Counter label={success ? "Prix d'achat enregistrés" : "Prix d'achat HT à enregistrer"} value={40} />
          </div>
          <p className="clients-muted" style={{ margin: 0 }}>
            Fichier d&apos;origine {FILE.name} · SHA-256 {FILE.sha256} · 6 révisions de corrections (A. Martin) · analyse IA {MODEL_LABEL} ({MODEL_DIGEST}) ·
            3 lignes ignorées volontairement · 2 articles inchangés.
          </p>
          {success && (
            <div className="clients-actions clients-actions--wrap" style={{ justifyContent: "flex-start" }}>
              <Button variant="primary"><Layers size={16} aria-hidden="true" />Voir les articles importés</Button>
              <Button onClick={() => onGo("depot-vide")}><FileUp size={16} aria-hidden="true" />Importer un autre fichier</Button>
            </div>
          )}
        </div>
      </Card>
      {!success && (
        <div className="iw-bar">
          <Button variant="tertiary" disabled={running} onClick={() => onGo("controle-pret")}><ArrowLeft size={16} aria-hidden="true" />Vérification</Button>
          <span className="iw-bar__info">{running ? "Import en cours sur le serveur…" : "L'import exige votre confirmation explicite."}</span>
          <Button variant="primary" disabled={running} onClick={() => setOpen(true)}><Play size={16} aria-hidden="true" />{running ? "Import en cours…" : "Importer 42 articles…"}</Button>
        </div>
      )}
      <Modal open={open && !running && !success} onClose={() => setOpen(false)} tone="accent" icon={Play} title="Importer 42 articles dans le catalogue ?"
        description={<>14 produits et 39 variantes créés, 3 articles modifiés, 40 prix d&apos;achat HT pour {FILE.supplier}. L&apos;opération est enregistrée dans le journal d&apos;audit ; le même fichier ne pourra plus être importé.</>}
        actions={<><Button onClick={() => setOpen(false)}>Annuler</Button><Button variant="primary" onClick={() => onGo("import-cours")}>Importer maintenant</Button></>} />
    </>
  );
}

// ---------------------------------------------------------------- historique repliable
function HistorySection({ initiallyOpen }: { initiallyOpen: boolean }) {
  const [open, setOpen] = useState(initiallyOpen);
  const { summary } = HISTORY;
  return (
    <section className="iw-history" data-open={open} aria-labelledby="import-history-title">
      <button type="button" className="iw-history__toggle" aria-expanded={open} aria-controls="import-history-body" onClick={() => setOpen((o) => !o)}>
        <History size={18} aria-hidden="true" />
        <span className="iw-history__title" id="import-history-title">Imports récents</span>
        <span className="iw-history__summary">{summary.total} imports · dernier le {summary.last} · {summary.resumable} à reprendre</span>
        <ChevronDown size={18} className="iw-history__chevron" aria-hidden="true" />
      </button>
      {/* Fermé : le tableau n'est ni rendu ni chargé (seul le résumé est demandé au serveur). */}
      {open && (
        <div className="iw-history__body" id="import-history-body">
          <TableScroll label="Imports récents">
            <table className="clients-table cat-table cat-table--inner">
              <thead><tr><th scope="col">Date</th><th scope="col">Fichier</th><th scope="col">Type</th><th scope="col" className="cat-num">Lignes</th><th scope="col">Statut</th><th scope="col" className="cat-hide-sm">Analyse</th><th scope="col" className="cat-hide-sm">Auteur</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead>
              <tbody>{HISTORY.rows.map((h) => (
                <tr key={h.date + h.file}>
                  <td className="clients-muted tnum">{h.date}</td>
                  <td className="cat-strong">{h.file}</td>
                  <td className="cat-secondary">{h.kind}</td>
                  <td className="cat-num tnum">{h.lines}</td>
                  <td><Badge tone={h.tone}>{h.status}</Badge></td>
                  <td className="cat-secondary cat-hide-sm">{h.model}</td>
                  <td className="cat-secondary cat-hide-sm">{h.who}</td>
                  <td>{h.resume && <Button size="sm" variant="tertiary">Reprendre</Button>}</td>
                </tr>
              ))}</tbody>
            </table>
          </TableScroll>
          <TableFoot icon={History}>5 derniers imports affichés sur {summary.total} — aucun historique supprimé</TableFoot>
        </div>
      )}
    </section>
  );
}
