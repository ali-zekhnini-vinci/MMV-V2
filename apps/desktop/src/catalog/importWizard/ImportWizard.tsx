// Assistant d'import CSV en cinq étapes (CSV-IMPORT-AI-01, Gate C2) : déposer → analyse automatique par l'IA locale →
// vérifier et corriger → vérification avant import → confirmation et import. Repris du banc Gate B et du kit Lot 6.
// AUCUNE règle métier côté React : l'état de chaque étape, les statuts, compteurs, anomalies, diagnostics, actions licites,
// exemples de montants et leur empreinte viennent du serveur ; le poste n'envoie que des DÉCISIONS que le serveur revalide.
// Reprise exacte (rechargement, autre poste, « Reprendre ») : l'état est relu du serveur, aucune correction n'est perdue.
import {
  ArrowLeft, ArrowRight, Ban, Bot, CheckCheck, ChevronDown, CircleCheck, CircleX, ClipboardCheck, Clock, FileSpreadsheet, FileText, FileUp, History, Info, Layers,
  ListChecks, Play, RotateCcw, Server, ShieldCheck, Sparkles, TriangleAlert, Upload, X,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button } from "../../components/ui/button.tsx";
import { cn } from "../../lib/utils.ts";
import { Badge, type BadgeTone, Banner, Card, Modal, PageHeader, RadioCard, TableFoot, TablePanel, TableScroll } from "../CatalogUi.tsx";
import { formatDateTime } from "../catalogFormat.ts";
import { type SupplierChoice, SupplierCombobox } from "../SupplierCombobox.tsx";
import {
  type AnalysisEvent, type AnalysisState, type Answer, type Applied, type AssistantApi, type AssistantStatus, type AssistantView, type DecisionInput,
  fileToBase64, type HistoryBatch, type ImportsSummary, type InspectView, type IssueAction, type IssueDetail, type RowsView, type StepKey, type StepState, useAssistantApi,
} from "./assistantApi.ts";
import "../Catalog.css";
import "../../clients/Clients.css";
import "./importWizard.css";

const STEP_ORDER: StepKey[] = ["deposit", "analysis", "review", "check", "import"];
const STEP_LABELS: Record<StepKey, string> = {
  deposit: "Déposer le fichier", analysis: "Analyse automatique", review: "Vérifier et corriger", check: "Vérification avant import", import: "Confirmation et import",
};

// Libellés de PRÉSENTATION des codes bornés du serveur (aucune décision n'en dépend).
const EVENT_LABELS: Record<string, string> = {
  file_received: "Fichier reçu par le serveur", encoding_detected: "Encodage et séparateur détectés", structure_read: "Structure lue (toutes les lignes)",
  rules_validated: "Validation par les règles terminée", sample_prepared: "Échantillon préparé pour le modèle", ollama_queued: "Analyse mise en file",
  ollama_running: "Interprétation par le modèle en cours", ollama_received: "Réponse du modèle reçue", validation_running: "Revalidation par le serveur",
  analysis_done: "Analyse terminée", ollama_unavailable: "Ollama indisponible", ollama_timeout: "Délai d'analyse dépassé", ollama_invalid_output: "Réponse hors contrat écartée",
  analysis_cancelled: "Analyse annulée", analysis_interrupted: "Analyse interrompue (redémarrage du serveur)", late_result_discarded: "Résultat tardif conservé sans effet",
  rules_only_chosen: "Vérification sans IA choisie", analysis_disabled: "Analyse IA désactivée",
};
const EVENT_TONE: Record<string, BadgeTone> = {
  analysis_done: "ok", rules_validated: "ok", ollama_received: "ok", ollama_unavailable: "err", ollama_timeout: "err", ollama_invalid_output: "err",
  analysis_cancelled: "warn", analysis_interrupted: "warn", late_result_discarded: "warn", rules_only_chosen: "warn", analysis_disabled: "warn",
};
const ANALYSIS_LABELS: Record<string, string> = {
  none: "Aucune analyse", queued: "En file", running: "En cours", done: "Terminée", incomplete: "Terminée (incomplète)", unavailable: "Indisponible",
  timeout: "Délai dépassé", invalid_output: "Réponse hors contrat", cancelled: "Annulée", interrupted: "Interrompue",
};
const ROW_STATUS: Record<string, { label: string; tone: BadgeTone }> = {
  VALIDE: { label: "Valide", tone: "ok" }, AVERTISSEMENT: { label: "Avertissement", tone: "warn" }, AMBIGU: { label: "À confirmer", tone: "accent" },
  INVALIDE: { label: "À corriger", tone: "err" }, NON_RECONNU: { label: "Non reconnue", tone: "arch" }, IGNOREE: { label: "Ignorée", tone: "arch" },
};
const FAMILY_LABELS: Record<string, string> = {
  optical_frames: "Montures optiques", sunglasses: "Lunettes de soleil", ophthalmic_lenses: "Verres correcteurs", contact_lenses_solutions: "Lentilles et solutions",
  accessories: "Accessoires", services: "Prestations",
};
const FILTERS: { key: string; label: string }[] = [
  { key: "all", label: "Toutes" }, { key: "valid", label: "Valides" }, { key: "corrections", label: "Corrections nécessaires" }, { key: "ambiguous", label: "Ambiguïtés" },
  { key: "unrecognized", label: "Non reconnues" }, { key: "duplicates", label: "Doublons potentiels" },
];
const REFUSALS: Record<string, string> = {
  too_large: "Le fichier dépasse 2 Mio : découpez-le (par exemple une partie par famille).", too_many_rows: "Le fichier dépasse 5 000 lignes : découpez-le.",
  empty_file: "Le fichier est vide.", binary_content: "Le fichier n'est pas un texte CSV (contenu binaire).", spreadsheet_not_csv: "Classeur Excel ou OpenDocument : exportez-le en CSV.",
  undecodable: "Encodage illisible : enregistrez le fichier en UTF-8.", invalid_base64: "Fichier illisible par le poste.",
};
const DECISION_ERRORS: Record<string, string> = {
  examples_not_reviewed: "Les exemples ont changé : relisez-les avant de confirmer.", one_money_decision_per_request: "Un seul montant se confirme à la fois.",
  sku_exists: "Cette valeur est déjà utilisée par une autre ligne du fichier.", value_invalid: "Valeur refusée par le serveur.", field_already_used: "Ce champ est déjà porté par une autre colonne.",
  action_not_allowed: "Action non permise ici.", analysis_required: "Terminez l'analyse ou choisissez « Vérifier sans IA ».", revision_conflict: "Une autre décision a été enregistrée entre-temps : l'écran est rechargé.",
  costs_forbidden: "Droit costs.write requis pour les prix d'achat.", manual_price_forbidden: "Droit pricing.manual requis pour les prix de vente.",
  catalog_configure_required: "Droit catalog.configure requis.", batch_closed: "Lot clos : lecture seule.",
};
const APPLY_ERRORS: Record<string, string> = {
  check_stale: "Le contrôle est périmé : relancez-le.", analysis_running: "Une analyse IA est en cours.", already_applied: "Ce fichier a déjà été importé.",
  blocking_remaining: "Des points bloquants restent à traiter.", nothing_to_import: "Aucune donnée nouvelle dans ce fichier : rien à importer.",
  confirmation_required: "Confirmation explicite requise.", costs_forbidden: "Droit costs.write requis.", manual_price_forbidden: "Droit pricing.manual requis.",
  catalog_configure_required: "Droit catalog.configure requis.", apply_refused: "Le serveur a refusé une écriture : rien n'a été importé.",
};

const ACTIVE = new Set(["queued", "running"]);
const lastOf = <T,>(items: T[]): T | undefined => items[items.length - 1];
const clock = (iso: string) => new Date(iso).toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
const seconds = (ms: number) => `${(ms / 1000).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} s`;
const size = (bytes: number) => (bytes < 1024 ? `${bytes} o` : bytes < 1024 * 1024 ? `${(bytes / 1024).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} Kio` : `${(bytes / 1048576).toLocaleString("fr-FR", { maximumFractionDigits: 2 })} Mio`);
const failure = (answer: Answer<unknown>, labels: Record<string, string>) =>
  answer.ok ? null : labels[(answer.details?.code as string | undefined) ?? answer.code] ?? labels[answer.code] ?? `Refus du serveur (${(answer.details?.code as string | undefined) ?? answer.code}).`;

export function ImportWizard() {
  return <ImportWizardView api={useAssistantApi()} />;
}

/** Vue testable : l'API est injectée (aucune règle locale, seulement des appels au serveur). */
export function ImportWizardView({ api }: { api: AssistantApi }) {
  const [batchId, setBatchId] = useState<string | null>(null);
  const [view, setView] = useState<AssistantView | null>(null);
  const [status, setStatus] = useState<AssistantStatus | null>(null);
  const [step, setStep] = useState<StepKey>("deposit");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [focusIssue, setFocusIssue] = useState<string | null>(null);
  const [analysis, setAnalysis] = useState<AnalysisState | null>(null);
  const lastSeq = useRef(0);

  useEffect(() => {
    void api.status().then((answer) => { if (answer.ok) setStatus(answer.data); });
  }, [api]);

  const reload = useCallback(async (id: string, moveTo?: StepKey) => {
    const answer = await api.view(id);
    if (!answer.ok) {
      setLoadError(answer.code === "assistant_disabled" ? "L'assistant d'import n'est pas activé sur ce serveur." : "Lot introuvable ou inaccessible.");
      return null;
    }

    setLoadError(null);
    setView(answer.data);
    setAnalysis(answer.data.analysis);
    lastSeq.current = lastOf(answer.data.analysis.events)?.seq ?? 0;
    if (moveTo) setStep(moveTo);
    return answer.data;
  }, [api]);

  // Ouverture d'un lot (dépôt ou « Reprendre ») : étape proposée par le SERVEUR (première étape à traiter).
  const open = useCallback(async (id: string) => {
    setBatchId(id);
    setFocusIssue(null);
    const data = await reload(id);
    if (!data) return;
    const s = data.steps;
    setStep(data.batch.status === "applied" ? "import"
      : s.analysis.state === "current" || s.analysis.state === "blocked" ? "analysis"
      : s.check.state === "done" ? "import"
      : s.check.state === "blocked" || s.check.state === "stale" ? "check"
      : s.review.state !== "locked" ? "review" : "analysis");
  }, [reload]);

  // Suivi de l'analyse : interrogation chaque seconde, jamais une requête qui attend le modèle.
  useEffect(() => {
    if (batchId === null || analysis === null || !ACTIVE.has(analysis.status)) return;
    const timer = window.setInterval(async () => {
      const answer = await api.analysis(batchId, lastSeq.current);
      if (!answer.ok) return;
      const next = answer.data;
      setAnalysis((previous) => {
        // Fusion par numéro de séquence : un événement déjà reçu n'est jamais affiché deux fois.
        const seen = new Set((previous?.events ?? []).map((e) => e.seq));
        return { ...next, events: [...(previous?.events ?? []), ...next.events.filter((e) => !seen.has(e.seq))], batchEvents: previous?.batchEvents ?? next.batchEvents };
      });
      lastSeq.current = lastOf(next.events)?.seq ?? lastSeq.current;
      if (!ACTIVE.has(next.status)) void reload(batchId);
    }, 1000);
    return () => window.clearInterval(timer);
  }, [api, batchId, analysis, reload]);

  const steps: Record<StepKey, { state: StepState; sublabel: string | null }> = view?.steps ?? {
    deposit: { state: "current", sublabel: null }, analysis: { state: "locked", sublabel: null }, review: { state: "locked", sublabel: null },
    check: { state: "locked", sublabel: null }, import: { state: "locked", sublabel: null },
  };
  const readOnly = view !== null && (view.batch.status === "applied" || view.batch.status === "cancelled");
  const reset = () => { setBatchId(null); setView(null); setAnalysis(null); setStep("deposit"); setFocusIssue(null); };

  return (
    <section className="cat-screen iw-screen" aria-labelledby="import-title">
      <PageHeader title="Import CSV" subtitle="Assistant d'import : l'IA locale interprète, le serveur valide, vous arbitrez. Rien n'est écrit au catalogue avant votre confirmation finale."
        titleExtra={<ModelBadge status={status} analysis={analysis} rulesOnly={view?.batch.rulesOnly ?? false} reason={view?.batch.rulesOnlyReason ?? null} />} />
      <ol className="cat-steps cat-steps--wizard iw-steps" aria-label="Étapes de l'import">
        {STEP_ORDER.map((key, index) => {
          const s = steps[key];
          const isCurrent = key === step;
          const locked = s.state === "locked" || (batchId === null && key !== "deposit");
          return (
            <li key={key} className={cn("cat-step", s.state === "done" && !isCurrent && "cat-step--done", isCurrent && "cat-step--current",
              s.state === "attention" && "iw-step--attention", s.state === "blocked" && "iw-step--blocked", s.state === "stale" && "iw-step--stale", locked && "iw-step--locked")}>
              <button type="button" className="cat-step__btn" disabled={locked} aria-current={isCurrent ? "step" : undefined} onClick={() => setStep(key)}>
                <span className="cat-step__n">
                  {s.state === "done" && !isCurrent ? <CheckCheck size={14} aria-hidden="true" /> : s.state === "blocked" ? <Ban size={13} aria-hidden="true" />
                    : s.state === "attention" || s.state === "stale" ? <TriangleAlert size={13} aria-hidden="true" /> : index + 1}
                </span>
                <span className="iw-step__text">
                  <span className="iw-step__label">{STEP_LABELS[key]}</span>
                  <span className="iw-step__sub">{s.sublabel ?? (locked ? "—" : "")}</span>
                </span>
              </button>
            </li>
          );
        })}
      </ol>
      {loadError && <Banner tone="err" icon={CircleX} title={loadError} role="alert" />}
      {readOnly && <Banner tone="info" icon={Info} title={view?.batch.status === "applied" ? "Lot importé : lecture seule" : "Lot abandonné : lecture seule"} compact
        action={<Button size="sm" onClick={reset}><FileUp size={14} aria-hidden="true" />Nouvel import</Button>} />}
      {step === "deposit" && <DepositStep api={api} view={view} onCreated={(id) => void open(id)} onReset={reset} />}
      {step === "analysis" && view && analysis && <AnalysisStep api={api} view={view} analysis={analysis} onReload={(to) => void reload(view.batch.id, to)} />}
      {step === "review" && view && <ReviewStep api={api} view={view} focusIssue={focusIssue} readOnly={readOnly} onReload={(to) => void reload(view.batch.id, to)} />}
      {step === "check" && view && <CheckStep api={api} view={view} readOnly={readOnly} onReload={(to) => void reload(view.batch.id, to)} onFix={(key) => { setFocusIssue(key); setStep("review"); }} />}
      {step === "import" && view && <ImportStep api={api} view={view} onReload={(to) => void reload(view.batch.id, to)} />}
      {view && !readOnly && (
        <p className="clients-field__hint" style={{ margin: 0 }}>
          <Button size="sm" variant="tertiary" onClick={async () => { if ((await api.abandon(view.batch.id, view.batch.version)).ok) await reload(view.batch.id); }}>
            <X size={14} aria-hidden="true" />Abandonner ce lot
          </Button>{" "}Rien n&apos;est écrit au catalogue ; le lot reste consultable dans l&apos;historique.
        </p>
      )}
      <HistorySection api={api} onResume={(id) => void open(id)} />
    </section>
  );
}

function ModelBadge({ status, analysis, rulesOnly, reason }: { status: AssistantStatus | null; analysis: AnalysisState | null; rulesOnly: boolean; reason: string | null }) {
  if (rulesOnly) return <Badge tone="warn" icon={ShieldCheck}>{reason === "configuration" ? "IA désactivée par configuration" : "Règles seules (choix tracé)"}</Badge>;
  if (analysis && (analysis.status === "done" || analysis.status === "incomplete") && analysis.model.active) {
    return <Badge tone="ok" icon={Bot}>Analyse IA terminée · {analysis.model.active} (confirmé)</Badge>;
  }

  if (status === null) return null;
  if (!status.enabled || status.mode === "off") return <Badge tone="warn" icon={Bot}>IA désactivée par configuration · règles seules</Badge>;
  if (!status.ollamaReachable || !status.modelInstalled) return <Badge tone="warn" icon={Bot}>IA locale indisponible · règles seules possibles</Badge>;
  return <Badge tone="accent" icon={Bot}>IA locale · {status.configuredModel} configuré{analysis && ACTIVE.has(analysis.status) ? " · analyse en cours" : ""}</Badge>;
}

// ---------------------------------------------------------------- étape 1 : déposer
function DepositStep({ api, view, onCreated, onReset }: { api: AssistantApi; view: AssistantView | null; onCreated: (id: string) => void; onReset: () => void }) {
  const [file, setFile] = useState<{ name: string; base64: string; size: number } | null>(null);
  const [inspect, setInspect] = useState<InspectView | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [target, setTarget] = useState<"products" | "lenses">("products");
  const [supplier, setSupplier] = useState<SupplierChoice | null>(null);
  const [busy, setBusy] = useState(false);
  const [dragging, setDragging] = useState(false);
  if (view !== null) {
    return (
      <Card title="Fichier déposé" icon={FileUp}>
        <div className="iw-file">
          <FileSpreadsheet size={22} aria-hidden="true" />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="iw-file__name">{view.batch.fileName}</div>
            <div className="iw-file__meta">{size(view.batch.original.byteLength)} · {view.batch.lines} lignes · {view.batch.columns} colonnes · {view.batch.original.encoding} · séparateur {view.batch.original.delimiter} · reçu le {formatDateTime(view.batch.uploadedAtUtc)}</div>
            <div className="iw-file__meta iw-mono">SHA-256 {view.batch.original.sha256}</div>
          </div>
          <Badge tone="info">{view.batch.target === "lenses" ? "Référentiel des verres" : "Catalogue des produits"}</Badge>
        </div>
        <p className="clients-field__hint">Le fichier d&apos;origine est conservé tel quel (octets et empreinte) ; les corrections sont enregistrées à part. <Button size="sm" variant="tertiary" onClick={onReset}>Déposer un autre fichier</Button></p>
      </Card>
    );
  }

  async function choose(selected: File | undefined) {
    if (!selected) return;
    setRefusal(null);
    setInspect(null);
    setBusy(true);
    const base64 = await fileToBase64(selected);
    setFile({ name: selected.name, base64, size: selected.size });
    const answer = await api.inspect(selected.name, base64);
    setBusy(false);
    if (!answer.ok) {
      setRefusal(REFUSALS[answer.code] ?? `Fichier refusé (${answer.code}).`);
      return;
    }

    setInspect(answer.data);
    if (answer.data.suggestedTarget) setTarget(answer.data.suggestedTarget);
  }

  async function analyse() {
    if (!file) return;
    setBusy(true);
    const answer = await api.create({ fileName: file.name, contentBase64: file.base64, supplierId: supplier?.id ?? null, target });
    setBusy(false);
    if (!answer.ok) {
      setRefusal(REFUSALS[answer.code] ?? (answer.code === "supplier_unknown" ? "Fournisseur inconnu ou archivé." : `Refus du serveur (${answer.code}).`));
      return;
    }

    onCreated(answer.data.batchId);
  }

  return (
    <>
      <Card title="Déposer le fichier" icon={FileUp}>
        <div className="cat-stack">
          {refusal && <Banner tone="err" icon={CircleX} title="Fichier refusé : limite technique" role="alert">{refusal} Rien n&apos;a été enregistré.</Banner>}
          {inspect === null ? (
            <label className={cn("cat-drop", dragging && "cat-drop--over")}
              onDragOver={(e) => { e.preventDefault(); setDragging(true); }} onDragLeave={() => setDragging(false)}
              onDrop={(e) => { e.preventDefault(); setDragging(false); void choose(e.dataTransfer.files[0]); }}>
              <Upload size={28} aria-hidden="true" />
              <span className="cat-drop__title">Glissez votre fichier ici, ou choisissez-le</span>
              <span className="clients-field__hint">CSV ou texte, séparateur et encodage reconnus par le serveur · 2 Mio et 5 000 lignes au plus</span>
              <span className="clients-actions"><span className="clients-chip"><FileSpreadsheet size={16} aria-hidden="true" />Choisir un fichier…</span></span>
              <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" aria-label="Fichier CSV à importer" onChange={(e) => void choose(e.target.files?.[0])} />
            </label>
          ) : (
            <div className="iw-file">
              <FileSpreadsheet size={22} aria-hidden="true" />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div className="iw-file__name">{file?.name}</div>
                <div className="iw-file__meta">{size(inspect.byteLength)} · {inspect.lineCount} lignes · {inspect.columnCount} colonnes · {inspect.encoding} · séparateur {inspect.delimiter}</div>
              </div>
              <Button size="sm" variant="tertiary" onClick={() => { setInspect(null); setFile(null); }}><X size={14} aria-hidden="true" />Retirer</Button>
            </div>
          )}
          <p className="clients-field__hint" style={{ margin: 0 }}>
            <ShieldCheck size={12} aria-hidden="true" /> Un fichier imparfait est accepté pour analyse (doublons, colonnes inconnues, en-têtes absents, prix ambigus, lignes irrégulières) :
            ces points seront présentés à l&apos;étape 3. Seules les limites techniques empêchent le dépôt.
          </p>
          <div className="cat-grid">
            <SupplierCombobox label="Fournisseur du fichier (facultatif)" value={supplier} onChange={setSupplier} allowCreate={false}
              hint="Désigné : les lignes (ou les offres de verres) lui sont rattachées." />
            <div className="cat-stack" style={{ gap: 8 }}>
              <span className="iw-section-title">Destination {inspect?.suggestedTarget && <Badge tone="info">suggestion du serveur</Badge>}</span>
              <div className="cat-radios" role="radiogroup" aria-label="Destination">
                <RadioCard name="target" value="products" checked={target === "products"} onChange={(v) => setTarget(v as "products")} label="Catalogue des produits"
                  sub="Montures, solaires, lentilles, accessoires, prestations" />
                <RadioCard name="target" value="lenses" checked={target === "lenses"} onChange={(v) => setTarget(v as "lenses")} label="Référentiel des verres"
                  sub={inspect?.suggestedTarget === "lenses" ? `Suggéré : ${inspect.evidence.slice(0, 3).join(", ")}` : "Gammes, indices, traitements et offres fournisseurs"} />
              </div>
            </div>
          </div>
        </div>
      </Card>
      {inspect && (
        <TablePanel foot={<TableFoot icon={FileText}>{Math.min(8, inspect.lineCount)} premières lignes sur {inspect.lineCount} · texte d&apos;origine, sans aucune interprétation · ∅ = cellule vide</TableFoot>}>
          <div className="cat-card__head" style={{ padding: "12px 16px" }}><h2 className="cat-card__title"><FileText size={18} aria-hidden="true" />Aperçu du fichier d&apos;origine</h2></div>
          <TableScroll label="Aperçu du fichier d'origine">
            <table className="iw-raw">
              <tbody>
                {inspect.preview.map((cells, i) => (
                  <tr key={i}><th scope="row">L{i + 1}</th>{cells.map((c, j) => <td key={j}>{c === "" ? "∅" : c}</td>)}</tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        </TablePanel>
      )}
      <div className="iw-bar">
        <span className="iw-bar__info">{inspect ? "L'analyse démarre dès le dépôt : lecture complète par le serveur, puis interprétation par l'IA locale. Vous pourrez quitter l'écran pendant l'analyse." : "Choisissez un fichier pour continuer."}</span>
        <Button variant="primary" disabled={inspect === null || busy} onClick={() => void analyse()}><Sparkles size={16} aria-hidden="true" />Analyser mon fichier</Button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- étape 2 : analyse
function Progress({ label, done, total }: { label: string; done: number; total: number }) {
  const ratio = total === 0 ? 1 : done / total;
  return (
    <div className="iw-progress">
      <div className="iw-progress__head"><span>{label}</span><span className="tnum">{done} / {total}</span></div>
      <div className="iw-progress__track" role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={total} aria-valuenow={done}>
        <div className={cn("iw-progress__fill", ratio >= 1 && "iw-progress__fill--ok")} style={{ width: `${ratio * 100}%` }} />
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

function eventDetail(e: AnalysisEvent): string {
  const d = e.data ?? {};
  switch (e.code) {
    case "file_received": return `${size(Number(d.bytes ?? 0))}`;
    case "encoding_detected": return `${d.encoding} · séparateur ${d.delimiter}`;
    case "structure_read": return `${d.lines} lignes · ${d.columns} colonnes · ${d.irregular} irrégulière(s)`;
    case "rules_validated": return `${d.rows} lignes contrôlées · ${d.confirmations} point(s) à confirmer`;
    case "sample_prepared": return `${d.sampled} lignes vues par le modèle sur ${d.total}${d.complete === false ? " (couverture incomplète)" : ""}`;
    case "ollama_queued": return `tentative ${d.attempt} · ${d.model}`;
    case "ollama_running": return `${d.model} · Ollama ${d.ollamaVersion ?? "?"}`;
    case "ollama_received": return `${d.model} · ${seconds(Number(d.elapsedMs ?? 0))} · ${d.evalCount ?? "?"} jetons`;
    case "analysis_done": return `${ANALYSIS_LABELS[String(d.status)] ?? d.status} · ${d.confirmations} point(s) à confirmer`;
    case "ollama_unavailable": case "ollama_timeout": case "ollama_invalid_output": return String(d.error ?? "");
    default: return "";
  }
}

function AnalysisStep({ api, view, analysis, onReload }: { api: AssistantApi; view: AssistantView; analysis: AnalysisState; onReload: (to?: StepKey) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const live = ACTIVE.has(analysis.status);
  const needsChoice = view.steps.analysis.state === "blocked";
  const reviewable = view.steps.review.state !== "locked";
  const events = [...analysis.batchEvents, ...analysis.events];
  const run = async (action: () => Promise<Answer<unknown>>, to?: StepKey) => {
    setBusy(true);
    const answer = await action();
    setBusy(false);
    setError(failure(answer, DECISION_ERRORS));
    onReload(to);
  };
  return (
    <>
      <div className="iw-layout">
        <div className="iw-main">
          <Card title="Analyse automatique" icon={Sparkles}
            actions={<span className="clients-muted tnum" role="timer" aria-label="Durée mesurée par le serveur"><Clock size={14} aria-hidden="true" /> {seconds(analysis.elapsedMs)}</span>}>
            <div className="cat-stack">
              <p className="clients-muted" style={{ margin: 0 }}><FileSpreadsheet size={14} aria-hidden="true" /> {view.batch.fileName} · tentative {analysis.attempt || "—"} · {ANALYSIS_LABELS[analysis.status] ?? analysis.status}</p>
              <div className="iw-facts">
                <div className="cat-counter"><div className="cat-counter__label">Lignes lues</div><div className="cat-counter__value">{view.batch.lines}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Colonnes</div><div className="cat-counter__value">{view.batch.columns}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Anomalies de structure</div><div className="cat-counter__value">{analysis.structureAnomalies.length}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Lignes vues par le modèle</div><div className="cat-counter__value">{analysis.coverage?.sampled ?? "—"}<span className="clients-muted" style={{ fontSize: 13 }}> / {analysis.coverage?.lines ?? view.batch.lines}</span></div></div>
              </div>
              {analysis.progress.map((p) => <Progress key={p.label} label={p.label} done={p.done} total={p.total} />)}
              {analysis.indeterminate && <Indeterminate label={analysis.indeterminate} note={analysis.queuePosition ? `Position dans la file : ${analysis.queuePosition}. Une seule analyse à la fois sur ce serveur.` : "Le modèle répond en une seule fois : aucune progression intermédiaire n'est mesurable."} />}
              {analysis.coverage?.complete === false && <Banner tone="warn" icon={TriangleAlert} title="Analyse incomplète" compact>Le modèle n&apos;a pas vu toutes les catégories de lignes ; ses suggestions restent des suggestions. Le serveur a contrôlé chaque ligne.</Banner>}
              {analysis.lateResultDiscarded && <Banner tone="warn" icon={Info} title="Résultat tardif conservé sans effet" compact>La réponse du modèle est arrivée après l&apos;annulation : elle est tracée mais n&apos;a rien modifié.</Banner>}
              {needsChoice && (
                <Banner tone="err" icon={CircleX} title={`Analyse IA ${(ANALYSIS_LABELS[analysis.status] ?? analysis.status).toLowerCase()}`} role="alert">
                  Aucune décision n&apos;est prise à votre place : relancez l&apos;analyse IA, ou poursuivez <strong>sans IA</strong> (règles et vos confirmations seulement ; choix enregistré dans l&apos;historique du lot).
                </Banner>
              )}
              {error && <Banner tone="err" icon={CircleX} title={error} role="alert" compact />}
            </div>
          </Card>
          <Card title="Activité du serveur" icon={ListChecks} actions={live ? <Badge tone="accent">suivi en direct</Badge> : undefined}>
            <ol className="iw-activity" aria-live="polite" aria-label="Événements de l'analyse">
              {events.map((e, i) => (
                <li key={`${e.seq}-${e.code}`} className={cn("iw-event", live && i === events.length - 1 && "iw-event--live")}>
                  <span className="iw-event__time">{clock(e.atUtc)}</span>
                  <span className={cn("iw-event__dot", `cat-ic--${EVENT_TONE[e.code] ?? "info"}`)} aria-hidden="true"><Info size={13} /></span>
                  <span><span className="iw-event__title">{EVENT_LABELS[e.code] ?? e.code}</span><br /><span className="iw-event__detail">{eventDetail(e)}</span></span>
                </li>
              ))}
            </ol>
          </Card>
        </div>
        <div className="iw-aside">
          <Card title="Moteur d'analyse" icon={Server}>
            <div className="iw-model">
              <div className="iw-model__row"><span>Ollama (serveur local)</span><span>{analysis.model.ollamaVersion ? <Badge tone="ok">v{analysis.model.ollamaVersion}</Badge> : <span className="clients-muted">—</span>}</span></div>
              <div className="iw-model__row"><span>Modèle configuré</span><span className="iw-mono">{analysis.model.configured}</span></div>
              <div className="iw-model__row"><span>Modèle actif</span>
                <span>{analysis.model.active ? <>{analysis.model.active} <span className="clients-muted iw-mono">{analysis.model.digest}</span><br /><Badge tone="ok" icon={CircleCheck}>confirmé par la réponse</Badge></>
                  : live ? <span className="clients-muted">en attente de la réponse</span> : "—"}</span>
              </div>
              <p className="clients-field__hint" style={{ margin: 0 }}><Info size={12} aria-hidden="true" /> Le modèle reçoit un échantillon ; le serveur lit et contrôle chaque ligne. Aucune donnée ne quitte le serveur du magasin.</p>
            </div>
          </Card>
          <Card title="Familles identifiées" icon={Layers}>
            <ul className="iw-list">
              {analysis.families.map((f) => (
                <li key={`${f.family}-${f.status}`}><span>{FAMILY_LABELS[f.family] ?? "Famille à confirmer"}</span><Badge tone={(ROW_STATUS[f.status] ?? ROW_STATUS.AMBIGU).tone}>{f.rows} lignes</Badge></li>
              ))}
            </ul>
          </Card>
          {analysis.structureAnomalies.length > 0 && (
            <Card title="Anomalies de structure" icon={TriangleAlert}>
              <ul className="iw-list">{analysis.structureAnomalies.slice(0, 12).map((a) => <li key={a}><span>{a}</span></li>)}</ul>
            </Card>
          )}
        </div>
      </div>
      <div className="iw-bar">
        <span className="iw-bar__info">{live ? "Vous pouvez quitter cet écran : l'analyse continue sur le serveur et se retrouve dans « Imports récents »." : reviewable ? "Étape suivante : vérifier les lignes et confirmer ce que MMV ne peut pas décider seul." : ""}</span>
        {live && <Button variant="danger" disabled={busy} onClick={() => void run(() => api.cancelAnalysis(view.batch.id))}><X size={16} aria-hidden="true" />Annuler l&apos;analyse IA</Button>}
        {needsChoice && <Button disabled={busy} onClick={() => void run(() => api.retry(view.batch.id, view.batch.version))}><RotateCcw size={16} aria-hidden="true" />Relancer l&apos;analyse IA</Button>}
        {needsChoice && !view.batch.rulesOnly && <Button disabled={busy} onClick={() => void run(() => api.decide(view.batch.id, view.batch.revision, [{ type: "rules_only", target: {}, value: {} }]), "review")}><ShieldCheck size={16} aria-hidden="true" />Vérifier sans IA</Button>}
        <Button variant="primary" disabled={!reviewable} onClick={() => onReload("review")}>Vérifier et corriger<ArrowRight size={16} aria-hidden="true" /></Button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- étape 3 : vérifier et corriger
function ReviewStep({ api, view, focusIssue, readOnly, onReload }: { api: AssistantApi; view: AssistantView; focusIssue: string | null; readOnly: boolean; onReload: (to?: StepKey) => void }) {
  const [filter, setFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [rows, setRows] = useState<RowsView | null>(null);
  const [issueKey, setIssueKey] = useState<string | null>(focusIssue);
  const pageSize = 50;
  useEffect(() => {
    let alive = true;
    void api.rows(view.batch.id, filter, page, pageSize).then((answer) => { if (alive && answer.ok) setRows(answer.data); });
    return () => { alive = false; };
  }, [api, view.batch.id, view.batch.revision, filter, page]);
  const decisions = view.review.decisions;
  return (
    <>
      <Card title="À confirmer pour plusieurs lignes à la fois" icon={Layers} actions={<span className="clients-muted">{view.review.groups.length} confirmation(s) groupée(s)</span>}>
        {view.review.groups.length === 0 ? <p className="clients-muted">Aucune confirmation groupée en attente.</p> : (
          <TableScroll label="Confirmations groupées">
            <table className="clients-table cat-table cat-table--inner cat-table--compact">
              <thead><tr><th scope="col">Question</th><th scope="col">Lignes</th><th scope="col">Proposition</th><th scope="col">État</th><th scope="col"><span className="sr-only">Action</span></th></tr></thead>
              <tbody>
                {view.review.groups.map((g) => (
                  <tr key={g.key} className={issueKey === g.key ? "is-selected" : undefined}>
                    <td><span className="cat-strong">{g.explanation}</span>{g.column !== null && <><br /><span className="clients-muted">colonne {view.review.columns[g.column]?.header || `c${g.column}`}</span></>}</td>
                    <td className="tnum">{g.lines || "—"}</td>
                    <td>{g.suggestion ? <>{FAMILY_LABELS[g.suggestion] ?? g.suggestion} <Badge tone="accent" icon={Bot}>suggestion IA</Badge></> : g.interpretation ?? <span className="clients-muted">aucune présélection</span>}</td>
                    <td><Badge tone={(ROW_STATUS[g.severity] ?? ROW_STATUS.AMBIGU).tone}>{(ROW_STATUS[g.severity] ?? ROW_STATUS.AMBIGU).label}</Badge></td>
                    <td><Button size="sm" onClick={() => setIssueKey(g.key)} disabled={readOnly}>Ouvrir</Button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Card>
      {issueKey && <IssuePanel key={`${issueKey}-${view.batch.revision}`} api={api} view={view} issueKey={issueKey} onClose={() => setIssueKey(null)} onDecided={() => { setIssueKey(null); onReload(); }} />}
      <div className="iw-filters" role="toolbar" aria-label="Filtrer les lignes">
        {FILTERS.map((f) => (
          <button key={f.key} type="button" className={cn("clients-chip", filter === f.key && "clients-chip--on")} aria-pressed={filter === f.key} onClick={() => { setFilter(f.key); setPage(1); }}>
            {f.label}<span className="iw-count">{(rows?.counters ?? view.review.counters)[f.key] ?? 0}</span>
          </button>
        ))}
      </div>
      <TablePanel foot={<TableFoot icon={FileSpreadsheet}>{rows ? `${rows.rows.length} ligne(s) affichée(s) sur ${rows.total} · page ${page}` : "Chargement…"}</TableFoot>}>
        <TableScroll label="Lignes analysées">
          <table className="clients-table cat-table cat-table--compact">
            <thead><tr><th scope="col" className="cat-num">Ligne</th><th scope="col">Famille</th><th scope="col">Produit</th><th scope="col">Variante</th><th scope="col">Référence</th><th scope="col">Résultat</th><th scope="col">Anomalies</th><th scope="col"><span className="sr-only">Action</span></th></tr></thead>
            <tbody>
              {(rows?.rows ?? []).map((r) => {
                const s = ROW_STATUS[r.status] ?? ROW_STATUS.AMBIGU;
                return (
                  <tr key={r.line} className={cn(r.status === "INVALIDE" && "is-warning")}>
                    <td className="cat-num tnum">{r.line}</td>
                    <td>{r.family ? FAMILY_LABELS[r.family] ?? r.family : "—"}{r.familyStatus === "AMBIGU" && <><br /><span className="clients-muted" style={{ fontSize: 12 }}>à confirmer</span></>}</td>
                    <td className="cat-strong">{r.product ?? "—"}</td>
                    <td className="cat-secondary">{r.variant ?? "—"}</td>
                    <td className="iw-mono">{r.reference ?? "—"}{r.flags.includes("corrected") && <> <Badge tone="info">corrigée</Badge></>}</td>
                    <td><Badge tone={s.tone}>{s.label}</Badge>{r.continuationOf !== null && <><br /><span className="clients-muted" style={{ fontSize: 12 }}>reprise de L{r.continuationOf}</span></>}</td>
                    <td><ul className="iw-issues">{r.reasons.filter((x) => !x.startsWith("règle")).slice(0, 3).map((x) => <li key={x}>{x}</li>)}</ul></td>
                    <td>{r.issueKeys.length > 0 && !readOnly && <Button size="sm" onClick={() => setIssueKey(r.issueKeys[0])}>Corriger</Button>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </TableScroll>
      </TablePanel>
      {rows && rows.total > pageSize && (
        <div className="clients-actions">
          <Button size="sm" disabled={page === 1} onClick={() => setPage((p) => p - 1)}><ArrowLeft size={14} aria-hidden="true" />Page précédente</Button>
          <Button size="sm" disabled={page * pageSize >= rows.total} onClick={() => setPage((p) => p + 1)}>Page suivante<ArrowRight size={14} aria-hidden="true" /></Button>
        </div>
      )}
      {decisions.length > 0 && (
        <Card title="Décisions enregistrées (révisions)" icon={History}>
          <ul className="iw-list">
            {decisions.map((d) => (
              <li key={d.revision}>
                <span>Révision {d.revision} · {d.type} · {formatDateTime(d.createdAtUtc)}</span>
                {!readOnly && d.type !== "revoke" && d.type !== "rules_only" && (
                  <Button size="sm" variant="tertiary" onClick={async () => { await api.decide(view.batch.id, view.batch.revision, [{ type: "revoke", target: { revision: d.revision }, value: {} }]); onReload(); }}>
                    <RotateCcw size={14} aria-hidden="true" />Annuler cette décision
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </Card>
      )}
      <div className="iw-bar">
        <span className="iw-bar__info">{view.review.confirmations > 0 ? `${view.review.confirmations} point(s) à traiter avant l'import.` : "Aucun point bloquant : contrôlez avant import."}</span>
        <Button variant="primary" onClick={() => onReload("check")}>Vérification avant import<ArrowRight size={16} aria-hidden="true" /></Button>
      </div>
    </>
  );
}

function IssuePanel({ api, view, issueKey, onClose, onDecided }: { api: AssistantApi; view: AssistantView; issueKey: string; onClose: () => void; onDecided: () => void }) {
  const [issue, setIssue] = useState<IssueDetail | null>(null);
  const [choice, setChoice] = useState<number | null>(null);
  const [text, setText] = useState("");
  const [state, setState] = useState<"idle" | "pending" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    void api.issue(view.batch.id, issueKey).then((answer) => setIssue(answer.ok ? answer.data : null));
  }, [api, view.batch.id, issueKey]);
  const selected: IssueAction | null = issue && choice !== null ? issue.actions[choice] : null;
  const needsText = selected !== null && selected.type === "cell_value";
  const missingRight = selected?.requires.find((p) => !view.permissions.includes(p));
  async function save() {
    if (!issue || !selected) return;
    const decision: DecisionInput = {
      type: selected.type, target: selected.target,
      value: selected.type === "cell_value" ? { value: text } : Object.fromEntries(Object.entries(selected.constraints ?? {}).filter(([k]) => k !== "examplesDigest" && k !== "unique")),
      examplesDigest: (selected.constraints?.examplesDigest as string | undefined) ?? null,
    };
    setState("pending");
    const answer = await api.decide(view.batch.id, view.batch.revision, [decision]);
    if (!answer.ok) {
      setState("error");
      setError(failure(answer, DECISION_ERRORS));
      if (answer.code === "revision_conflict") onDecided();
      return;
    }

    onDecided();
  }

  if (issue === null) return <Card title="Anomalie" icon={TriangleAlert}><p className="clients-muted">Chargement…</p></Card>;
  const examples = issue.examples as { readable?: number; unreadable?: number; empty?: number; min?: string; median?: string; max?: string; currency?: string; examples?: { role: string; line: number; original: string; value: string | null }[]; hidden?: boolean } | null;
  return (
    <Card title={issue.explanation} icon={TriangleAlert} className="iw-panel" actions={<Button size="sm" variant="tertiary" onClick={onClose}><X size={14} aria-hidden="true" />Fermer</Button>}>
      <div className="cat-stack">
        <div className="iw-compare">
          <div className="iw-compare__box"><div className="iw-compare__title">Valeur(s) d&apos;origine</div><ul className="iw-list">{issue.original.slice(0, 6).map((o, i) => <li key={i}><span className="iw-mono">{o === "" ? "∅" : o}</span></li>)}</ul></div>
          <div className="iw-compare__box"><div className="iw-compare__title">Interprétation proposée</div><p>{issue.interpretation ?? "aucune"}{issue.suggestion && <> · <Badge tone="accent" icon={Bot}>suggestion IA : {FAMILY_LABELS[issue.suggestion] ?? issue.suggestion}</Badge></>}</p>
            {issue.lines.length > 0 && <p className="clients-muted">Lignes {issue.lines.slice(0, 12).join(", ")}{issue.lines.length > 12 ? "…" : ""}</p>}</div>
        </div>
        {issue.diagnosis && (
          <div className="iw-diagnosis" aria-label="Diagnostic du serveur">
            <span className="iw-section-title">Diagnostic du serveur</span>
            <ul className="iw-list">{issue.diagnosis.map((d) => <li key={d.cause}><span><strong>{d.retained ? "Retenue" : "Écartée"}</strong> · {d.evidence}</span><Badge tone={d.retained ? "warn" : "arch"}>{d.cause}</Badge></li>)}</ul>
          </div>
        )}
        {examples && !examples.hidden && (
          <div className="cat-stack" style={{ gap: 8 }}>
            <span className="iw-section-title">Toutes les lignes : {examples.readable} lisibles · {examples.unreadable} illisibles · {examples.empty} vides · minimum {examples.min} · médiane {examples.median} · maximum {examples.max} {examples.currency ?? "(devise du magasin introuvable)"} (aucune conversion)</span>
            <TableScroll label="Exemples choisis par le serveur">
              <table className="clients-table cat-table cat-table--compact">
                <thead><tr><th scope="col">Exemple</th><th scope="col" className="cat-num">Ligne</th><th scope="col">Valeur d&apos;origine</th><th scope="col">Lecture</th></tr></thead>
                <tbody>{(examples.examples ?? []).map((e) => <tr key={`${e.role}-${e.line}`}><td>{e.role}</td><td className="cat-num tnum">{e.line}</td><td className="iw-mono">{e.original}</td><td className="tnum">{e.value ?? "illisible"}</td></tr>)}</tbody>
              </table>
            </TableScroll>
          </div>
        )}
        {examples?.hidden && <Banner tone="info" icon={Info} title="Montants masqués (droit costs.read requis)" compact />}
        <div className="iw-options" role="radiogroup" aria-label="Actions possibles">
          {issue.actions.map((a, i) => (
            <RadioCard key={i} name={`action-${issue.key}`} value={String(i)} checked={choice === i} onChange={() => setChoice(i)} label={a.label}
              sub={a.requires.length > 0 ? `Droit requis : ${a.requires.join(", ")}` : undefined} />
          ))}
        </div>
        {needsText && <label className="clients-field"><span className="clients-field__label">Nouvelle valeur</span><input className="clients-input" value={text} onChange={(e) => setText(e.target.value)} /></label>}
        {issue.notOffered.length > 0 && <ul className="iw-list">{issue.notOffered.map((n) => <li key={n.type}><span className="clients-muted">Non proposé : {n.reason}</span></li>)}</ul>}
        {missingRight && <Banner tone="warn" icon={ShieldCheck} title={`Droit ${missingRight} requis pour cette action`} compact />}
        {state === "error" && error && <Banner tone="err" icon={CircleX} title={error} role="alert" compact />}
        <div className="clients-actions">
          <Button variant="primary" disabled={selected === null || state === "pending" || (needsText && text.trim() === "") || missingRight !== undefined} onClick={() => void save()}>
            <CheckCheck size={16} aria-hidden="true" />{issue.examplesDigest && selected?.type === "column_field" && selected.constraints?.field !== "ignore" ? `Confirmer pour les ${examples?.readable ?? ""} lignes` : "Enregistrer et revalider"}
          </Button>
          {state === "pending" && <Badge tone="info">Non revalidée</Badge>}
        </div>
      </div>
    </Card>
  );
}

// ---------------------------------------------------------------- étape 4 : vérification avant import
function CheckStep({ api, view, readOnly, onReload, onFix }: { api: AssistantApi; view: AssistantView; readOnly: boolean; onReload: (to?: StepKey) => void; onFix: (key: string) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const check = view.check;
  const stale = check?.stale === true;
  const run = async () => {
    setBusy(true);
    const answer = await api.check(view.batch.id, view.batch.version, view.batch.revision);
    setBusy(false);
    setError(failure(answer, { ...DECISION_ERRORS, ...APPLY_ERRORS }));
    onReload("check");
  };
  return (
    <>
      <Card title="Vérification avant import" icon={ClipboardCheck} actions={check && <span className="clients-muted">révision {check.basedOnRevision} · {formatDateTime(check.checkedAtUtc)} · {seconds(check.elapsedMs)}</span>}>
        <div className="cat-stack">
          {(check === null || stale) && <Banner tone={stale ? "warn" : "info"} icon={Info} title={stale ? "Contrôle périmé : une correction a suivi le contrôle" : "Contrôle à blanc sur la révision courante"} compact>Aucune écriture au catalogue ; le serveur rejoue tout le plan d&apos;import.</Banner>}
          {error && <Banner tone="err" icon={CircleX} title={error} role="alert" compact />}
          {check && !stale && (
            <>
              <div className="iw-facts">
                <div className="cat-counter"><div className="cat-counter__label">{check.target === "lenses" ? "Références à créer" : "Produits à créer"}</div><div className="cat-counter__value">{check.productsToCreate}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">{check.target === "lenses" ? "Offres à créer" : "Variantes à créer"}</div><div className="cat-counter__value">{check.variantsToCreate}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">À modifier</div><div className="cat-counter__value">{check.toUpdate}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Inchangés</div><div className="cat-counter__value">{check.unchanged}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Ignorées</div><div className="cat-counter__value">{check.skipped}</div></div>
                <div className="cat-counter"><div className="cat-counter__label">Bloquants</div><div className={cn("cat-counter__value", check.blocking > 0 && "cat-tone-err")}>{check.blocking}</div></div>
              </div>
              {check.nothingToImport && <Banner tone="warn" icon={TriangleAlert} title="Aucune donnée nouvelle dans ce fichier" compact>Rien ne serait créé ni modifié : l&apos;import sera refusé.</Banner>}
              {check.remaining.length > 0 && (
                <div className="cat-stack" style={{ gap: 8 }}>
                  <span className="iw-section-title">Erreurs restantes</span>
                  <ul className="iw-list">{check.remaining.slice(0, 30).map((r) => (
                    <li key={r.key}><span>{r.explanation}{r.lines.length > 0 && <span className="clients-muted"> · L{r.lines.slice(0, 6).join(", L")}</span>}</span>
                      {!readOnly && !r.key.startsWith("plan:") && <Button size="sm" onClick={() => onFix(r.key)}>Corriger à l&apos;étape 3</Button>}</li>
                  ))}</ul>
                </div>
              )}
              {check.changes.length > 0 && (
                <TableScroll label="Avant / après des articles existants">
                  <table className="clients-table cat-table cat-table--compact">
                    <thead><tr><th scope="col" className="cat-num">Ligne</th><th scope="col">Article</th><th scope="col">Champ</th><th scope="col">Catalogue</th><th scope="col">Fichier</th></tr></thead>
                    <tbody>{check.changes.slice(0, 100).map((c, i) => <tr key={i}><td className="cat-num tnum">{c.line}</td><td className="iw-mono">{c.sku}</td><td>{c.field}</td><td>{c.before ?? "—"}</td><td>{c.after ?? "—"}</td></tr>)}</tbody>
                  </table>
                </TableScroll>
              )}
              <div className="iw-compare">
                <div className="iw-compare__box"><div className="iw-compare__title">Correspondances validées</div>
                  <ul className="iw-list">{check.mappings.map((m) => <li key={m.column}><span>{m.header || `c${m.column}`} → <strong>{m.field}</strong>{m.notWritten && <span className="clients-muted"> · non écrit : {m.notWritten}</span>}</span><Badge tone={m.origin === "user" ? "info" : "ok"}>{m.origin}</Badge></li>)}</ul></div>
                <div className="iw-compare__box"><div className="iw-compare__title">Provenance des corrections</div>
                  <ul className="iw-list">{check.provenance.length === 0 ? <li><span className="clients-muted">Aucune correction</span></li> : check.provenance.map((p) => <li key={p.revision}><span>Révision {p.revision} · {p.type}{p.revoked ? " (annulée)" : ""}</span><span className="clients-muted">{formatDateTime(p.createdAtUtc)}</span></li>)}</ul></div>
              </div>
              {check.references.length > 0 && <ul className="iw-list">{check.references.map((r) => <li key={`${r.kind}-${r.name}`}><span>{r.kind === "range" ? "Gamme" : "Fabricant"} « {r.name} » absent du référentiel</span><Badge tone={r.decided ? "ok" : "warn"}>{r.decided ? "création décidée" : "à décider"}</Badge></li>)}</ul>}
            </>
          )}
        </div>
      </Card>
      <div className="iw-bar">
        <span className="iw-bar__info">{check && !stale && check.blocking === 0 && !check.nothingToImport ? "Contrôle à jour : vous pouvez passer à la confirmation." : "Relancez le contrôle après chaque correction."}</span>
        <Button onClick={() => onReload("review")}><ArrowLeft size={16} aria-hidden="true" />Vérifier et corriger</Button>
        {!readOnly && <Button disabled={busy} onClick={() => void run()}><Play size={16} aria-hidden="true" />{check ? "Relancer le contrôle" : "Contrôler avant import"}</Button>}
        <Button variant="primary" disabled={view.steps.import.state === "locked"} onClick={() => onReload("import")}>Confirmation et import<ArrowRight size={16} aria-hidden="true" /></Button>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- étape 5 : confirmation et import
function ImportStep({ api, view, onReload }: { api: AssistantApi; view: AssistantView; onReload: (to?: StepKey) => void }) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Applied | null>(null);
  const [error, setError] = useState<string | null>(null);
  const check = view.check;
  const applied = view.batch.status === "applied";
  async function confirm() {
    setBusy(true);
    const answer = await api.apply(view.batch.id, view.batch.version, view.batch.revision);
    setBusy(false);
    setOpen(false);
    if (answer.ok) setResult(answer.data);
    else setError(failure(answer, APPLY_ERRORS));
    onReload("import");
  }

  return (
    <>
      <Card title="Confirmation et import" icon={ShieldCheck}>
        <div className="cat-stack">
          {busy && <Indeterminate label="Écriture en une seule transaction" note="Tout ou rien : en cas d'échec, rien n'est écrit. Ne fermez pas MMV." />}
          {error && <Banner tone="err" icon={CircleX} title={error} role="alert" />}
          {result && <Banner tone="ok" icon={CircleCheck} title="Import enregistré par le serveur" role="status">
            {view.batch.target === "lenses" ? `${result.offersCreated} offre(s) créée(s), ${result.offersUpdated} modifiée(s), ${result.referencesCreated} référence(s) créée(s)` : `${result.variantsCreated} variante(s) créée(s), ${result.updated} modifiée(s), ${result.prices} tarif(s)`} · {formatDateTime(result.appliedAtUtc)} · {seconds(result.elapsedMs)}
          </Banner>}
          {applied && !result && <Banner tone="ok" icon={CircleCheck} title="Lot importé" compact>Relisez le rapport dans l&apos;historique.</Banner>}
          {check && !applied && (
            <ul className="iw-list">
              <li><span>{view.batch.target === "lenses" ? "Offres de verres à créer" : "Variantes à créer"}</span><strong className="tnum">{check.variantsToCreate}</strong></li>
              <li><span>{view.batch.target === "lenses" ? "Références à créer (décidées)" : "Produits à créer"}</span><strong className="tnum">{check.productsToCreate}</strong></li>
              <li><span>Articles modifiés</span><strong className="tnum">{check.toUpdate}</strong></li>
              {check.containsCosts && <li><span>Prix d&apos;achat HT écrits ({check.currency ?? "devise introuvable"}, aucune conversion)</span><Badge tone="warn">costs.write</Badge></li>}
              {check.containsManualPrices && <li><span>Prix de vente HT écrits ({check.currency ?? "devise introuvable"})</span><Badge tone="warn">pricing.manual</Badge></li>}
              {(check.offersToRevalidate ?? 0) > 0 && <li><span>Fiches complètes à revalider (capacités modifiées)</span><Badge tone="warn">{check.offersToRevalidate}</Badge></li>}
              <li><span>Contrôle</span><span>révision {check.basedOnRevision} · {check.rulesOnly ? "règles seules" : check.analysis?.model ?? "—"}</span></li>
            </ul>
          )}
        </div>
      </Card>
      <div className="iw-bar">
        <span className="iw-bar__info">Rien n&apos;est écrit avant votre confirmation ; seules les lignes contrôlées sont importées.</span>
        <Button onClick={() => onReload("check")}><ArrowLeft size={16} aria-hidden="true" />Vérification avant import</Button>
        <Button variant="primary" disabled={applied || view.steps.import.state === "locked" || busy} onClick={() => setOpen(true)}><Play size={16} aria-hidden="true" />Importer…</Button>
      </div>
      <Modal open={open} onClose={() => setOpen(false)} tone="warn" icon={TriangleAlert} title="Importer maintenant ?" busy={busy}
        description={check ? `${check.variantsToCreate} création(s) et ${check.toUpdate} modification(s) seront écrites en une seule transaction.` : undefined}
        actions={<><Button onClick={() => setOpen(false)}>Annuler</Button><Button variant="primary" disabled={busy} onClick={() => void confirm()}>Importer maintenant</Button></>} />
    </>
  );
}

// ---------------------------------------------------------------- historique (replié par défaut)
function HistorySection({ api, onResume }: { api: AssistantApi; onResume: (id: string) => void }) {
  const [open, setOpen] = useState(false);
  const [summary, setSummary] = useState<ImportsSummary | null>(null);
  const [batches, setBatches] = useState<HistoryBatch[] | null>(null);
  useEffect(() => {
    void api.summary().then((answer) => { if (answer.ok) setSummary(answer.data); });
  }, [api]);
  useEffect(() => {
    // Liste chargée SEULEMENT à l'ouverture.
    if (open && batches === null) void api.history().then((answer) => { if (answer.ok) setBatches(answer.data); });
  }, [api, open, batches]);
  const text = useMemo(() => summary === null ? "" : `${summary.total} import(s)${summary.lastUploadedAtUtc ? ` · dernier le ${formatDateTime(summary.lastUploadedAtUtc)}` : ""} · ${summary.resumable} à reprendre`, [summary]);
  return (
    <section className="iw-history" data-open={open}>
      <button type="button" className="iw-history__toggle" aria-expanded={open} aria-controls="iw-history-body" onClick={() => setOpen((o) => !o)}>
        <History size={18} aria-hidden="true" /><span className="iw-history__title">Imports récents</span><span className="iw-history__summary">{text}</span>
        <ChevronDown size={16} className="iw-history__chevron" aria-hidden="true" />
      </button>
      {open && (
        <div className="iw-history__body" id="iw-history-body">
          <TableScroll label="Imports récents">
            <table className="clients-table cat-table cat-table--compact">
              <thead><tr><th scope="col">Fichier</th><th scope="col">Déposé</th><th scope="col">Lignes</th><th scope="col">Statut</th><th scope="col">Analyse</th><th scope="col"><span className="sr-only">Action</span></th></tr></thead>
              <tbody>
                {(batches ?? []).map((b) => (
                  <tr key={b.id}>
                    <td className="cat-strong">{b.fileName}</td>
                    <td>{formatDateTime(b.uploadedAtUtc)} · {b.uploadedBy}</td>
                    <td className="tnum">{b.rowCount}</td>
                    <td>{b.status}</td>
                    <td>{b.pipeline === "assistant_v2" ? (b.analysis ? `${ANALYSIS_LABELS[b.analysis.status] ?? b.analysis.status}${b.analysis.model ? ` · ${b.analysis.model}` : ""}` : "règles seules") : <span className="clients-muted">parcours actuel</span>}</td>
                    <td>{b.pipeline === "assistant_v2" && <Button size="sm" onClick={() => onResume(b.id)}>Reprendre</Button>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        </div>
      )}
    </section>
  );
}

