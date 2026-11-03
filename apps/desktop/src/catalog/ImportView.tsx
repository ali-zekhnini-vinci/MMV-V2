// Import CSV intelligent (D-05-18, 05-D, COR-05-04, UX-IA 03/11/2026) — parcours V2 absent du Lot 6, dessiné avec ses
// composants (étapes, cartes .card, choix en carte, table.dt, badges, bandeaux, compteurs) :
// 1 dépôt → 2 analyse AUTOMATIQUE par le serveur (encodage et séparateur détectés, règles, modèle appris du fournisseur,
// assistant IA LOCAL du serveur pour les colonnes restantes ; aucune donnée ne quitte le serveur du magasin) → contrôle
// à blanc lancé d'office quand tout est reconnu ; sinon UNE carte « Quelques précisions » limitée aux seules questions
// nécessaires (colonne ambiguë, montant proposé par l'assistant, champ requis absent, valeur par défaut, séparateur
// décimal) → 3 récapitulatif compact (compteurs, colonnes reconnues, durées) → 4 import explicite, TOUT OU RIEN.
// Jamais de valeur inventée, de montant converti ou de correspondance de montant appliquée sur la seule foi de
// l'assistant ; la correspondance détaillée reste modifiable à la demande ; un même fichier ne s'applique qu'une fois ;
// le modèle d'import du fournisseur est appris à chaque import réussi.
import { Bot, CheckCheck, CircleX, ClipboardCheck, Columns3, FileSpreadsheet, FileUp, History, Info, Pencil, Play, Sparkles, Tags, Timer, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type { AssistantServiceStatus, ImportBatch, ImportPreview, ImportReport, ImportSummary, MappingSuggestion } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { useActiveStore, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";
import { formatDateTime, POLICY_LABELS } from "./catalogFormat.ts";
import {
  Badge, type BadgeTone, Banner, Card, ErrorBanner, ErrorState, Loading, PageHeader, RadioCard, SelectField, TableFoot, TablePanel, TableScroll,
} from "./CatalogUi.tsx";
import { applyCorrections, type CsvEncoding, decodeCsv, ENCODING_LABELS } from "./csvFile.ts";
import { useFamilies } from "./productForm.ts";
import { type SupplierChoice, SupplierCombobox } from "./SupplierCombobox.tsx";

const KINDS = [
  { value: "catalog", label: "Catalogue (modèles et variantes)", sub: "Fichier fournisseur ou catalogue maison ; SKU facultatif (généré)" },
  { value: "suppliers", label: "Fournisseurs", sub: "Code, nom, pays, contact" },
  { value: "prices", label: "Prix du magasin", sub: "Prix d'achat HT, coefficient — rapprochement par SKU ou référence fournisseur" },
];

/** Libellés métier des champs MMV (jamais les clés techniques à l'écran). */
const FIELD_LABELS: Record<string, string> = {
  familyCode: "Famille", brand: "Marque", modelName: "Modèle / désignation", modelDescription: "Description", variantLabel: "Libellé de variante",
  color: "Coloris", sku: "SKU interne", manufacturerReference: "Référence fabricant", barcode: "Code-barres (EAN)", stockPolicy: "Politique de stock",
  lensManufacturer: "Fabricant (verres)", lensRange: "Gamme (verres)", lensIndex: "Indice (verres)", lensTreatments: "Traitements (verres)",
  supplierCode: "Code fournisseur", supplierReference: "Référence fournisseur", primarySupplier: "Fournisseur principal",
  code: "Code", name: "Nom", countryCode: "Pays", contactName: "Contact", email: "E-mail", phone: "Téléphone", addressLine: "Adresse", postalCode: "Code postal",
  city: "Ville", notes: "Notes", purchasePriceHt: "Prix d'achat HT", coefficient: "Coefficient", salePriceHt: "Prix de vente HT", currency: "Devise",
};

const label = (field: string) => FIELD_LABELS[field] ?? field;

const CONFIDENCE: Record<MappingSuggestion["confidence"], { label: string; tone: BadgeTone }> = {
  profile: { label: "Modèle du fournisseur", tone: "accent" },
  exact: { label: "Reconnu", tone: "ok" },
  assistant: { label: "Assistant IA", tone: "accent" },
  probable: { label: "Probable", tone: "warn" },
  ambiguous: { label: "À confirmer", tone: "err" },
};

/** Anomalies signalées par l'assistant (codes bornés) — simples indications, aucune valeur n'est modifiée. */
const WARNINGS: Record<string, string> = {
  decimal_comma: "virgule décimale", currency_in_values: "symbole de devise dans les valeurs", unit_in_values: "unité dans les valeurs",
  mixed_content: "contenu hétérogène", values_mismatch_header: "valeurs peu cohérentes avec l'intitulé", possible_duplicate_column: "colonne probablement en double",
};

/** Codes d'erreur de ligne → français (jamais la valeur saisie dans le message). */
const ROW_ERRORS: Record<string, string> = {
  required: "obligatoire", unknown: "inconnu(e)", duplicate: "en double", archived: "archivé(e)", invalid_format: "format invalide", out_of_range: "hors limites",
  too_long: "trop long", not_allowed: "non admis pour cette famille", formula_not_allowed: "formule de tableur refusée", ambiguous_number: "montant ambigu (milliers ou décimales ?)",
  currency_mismatch: "devise différente du magasin (aucune conversion)", supplier_required: "fournisseur du fichier à désigner au dépôt",
  sku_belongs_to_other_model: "SKU d'un autre modèle", policy_change_not_supported: "changement de politique non géré par l'import",
  coefficient_below_one: "coefficient < 1 (à saisir à l'écran)", sale_price_conflicts_coefficient: "prix de vente incohérent avec le coefficient",
};

const OUTCOMES: Record<string, { label: string; tone: BadgeTone }> = {
  create: { label: "Création", tone: "ok" },
  update: { label: "Modification", tone: "info" },
  unchanged: { label: "Inchangée", tone: "arch" },
  error: { label: "Erreur", tone: "err" },
};

const STATUS: Record<ImportBatch["status"], { label: string; tone: BadgeTone }> = {
  uploaded: { label: "Déposé", tone: "info" },
  checked: { label: "Contrôlé", tone: "accent" },
  applied: { label: "Appliqué", tone: "ok" },
  cancelled: { label: "Abandonné", tone: "arch" },
};

const STEPS = ["Déposer le fichier", "Analyse automatique", "Vérifier le récapitulatif", "Importer"];

const seconds = (ms: number) => (ms < 1000 ? `${ms} ms` : `${(ms / 1000).toLocaleString("fr-FR", { maximumFractionDigits: 1 })} s`);

interface Source {
  fileName: string;
  text: string;
  encoding: CsvEncoding;
}

/** Question posée à l'utilisateur : seulement ce que MMV ne peut pas décider seul. */
interface Question {
  key: string;
  title: string;
  hint: string;
  options: { value: string; label: string }[];
  proposed: string;
  /** Applique la réponse à la correspondance. */
  apply: (mapping: Record<string, string>, answer: string) => Record<string, string>;
}

export function ImportView() {
  const { store } = useActiveStore();
  const access = useCatalogAccess();
  const history = useCatalogQuery<ImportBatch[]>("/api/imports");
  const assistant = useCatalogQuery<AssistantServiceStatus>("/api/imports/assistant");
  const upload = useCatalogWrite<ImportPreview>();
  const [kind, setKind] = useState("catalog");
  const [supplier, setSupplier] = useState<SupplierChoice | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [source, setSource] = useState<Source | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);
  const report = useCatalogQuery<ImportReport>(batchId ? `/api/imports/${batchId}` : null);
  const batchStatus = report.data?.batch.status;
  const step = upload.isPending ? 1 : !batchId ? 0 : batchStatus === "applied" || batchStatus === "cancelled" ? 4 : batchStatus === "checked" ? 2 : 1;
  const supplierAllowed = kind !== "suppliers" && access.suppliersRead;

  const send = (file: Source, uploadKind: string, after?: (preview: ImportPreview) => void) =>
    upload.mutate(
      { method: "POST", path: "/api/imports", body: {
        kind: uploadKind, storeId: uploadKind === "prices" ? store?.id : null, fileName: file.fileName, content: file.text,
        supplierId: uploadKind !== "suppliers" && supplier ? supplier.id : null,
      } },
      { onSuccess: (result) => { setKind(uploadKind); setPreview(result); setBatchId(result.batchId); after?.(result); } },
    );

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    const decoded = decodeCsv(new Uint8Array(await file.arrayBuffer()));
    const next = { fileName: file.name, text: decoded.text, encoding: decoded.encoding };
    setSource(next);
    setPreview(null);
    setBatchId(null);
    send(next, kind);
  };

  const ai = assistant.data;
  return (
    <section className="cat-screen" aria-labelledby="import-title">
      <PageHeader title="Import CSV" subtitle="Déposez votre fichier : MMV reconnaît seul l'encodage, le séparateur et les colonnes, puis vous montre un récapitulatif avant d'importer."
        titleExtra={ai && (
          <Badge tone={ai.enabled && ai.available ? "accent" : "arch"} icon={Bot}>
            {ai.enabled ? (ai.available ? `Assistant IA local · ${ai.model}` : "Assistant IA local indisponible — analyse par règles") : "Analyse par règles"}
          </Badge>
        )} />
      <ol className="cat-steps" aria-label="Étapes de l'import">
        {STEPS.map((stepLabel, index) => (
          <li key={stepLabel} className={cn("cat-step", index < step && "cat-step--done", index === Math.min(step, 3) && step < 4 && "cat-step--current")} aria-current={index === step ? "step" : undefined}>
            <span className="cat-step__n">{index < step ? <CheckCheck size={14} aria-hidden="true" /> : index + 1}</span>{stepLabel}
          </li>
        ))}
      </ol>
      <Card title="1. Déposer le fichier" icon={FileUp}>
        <div className="cat-stack">
          <ErrorBanner error={upload.error} />
          <div className="cat-radios cat-radios--row" role="radiogroup" aria-label="Type">
            {KINDS.map((k) => <RadioCard key={k.value} name="importKind" value={k.value} checked={kind === k.value} onChange={setKind} label={k.label} sub={k.sub} />)}
          </div>
          <div className="cat-grid">
            {supplierAllowed && (
              <SupplierCombobox label="Fournisseur du fichier (facultatif)" value={supplier} onChange={setSupplier} allowCreate={false}
                hint="Désigné : la correspondance apprise lors de son dernier import est reprise et les lignes lui sont rattachées (aucun doublon)." />
            )}
            {kind === "prices" && (
              <p className="clients-field__hint" style={{ alignSelf: "end" }}>
                Magasin : <strong>{store?.name}</strong> — {store?.currency}. Les prix sont importés dans la devise du magasin, sans aucune conversion.
              </p>
            )}
          </div>
          <label className={cn("cat-drop", upload.isPending && "is-busy")}>
            {upload.isPending ? <Sparkles size={28} aria-hidden="true" /> : <FileSpreadsheet size={28} aria-hidden="true" />}
            <span className="cat-drop__title" role={upload.isPending ? "status" : undefined}>
              {upload.isPending ? "Analyse du fichier en cours… (colonnes, formats, assistant IA local)" : "Choisir un fichier CSV ou texte…"}
            </span>
            <span className="clients-field__hint">Séparateur « ; », « , », « | » ou tabulation ; UTF-8 ou Windows-1252. Rien n&apos;est écrit avant votre confirmation finale.</span>
            <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" disabled={upload.isPending} onChange={(e) => { void onFile(e.target.files?.[0]); e.target.value = ""; }} />
          </label>
        </div>
      </Card>
      {batchId && (
        <BatchSteps key={batchId} batchId={batchId} preview={preview} report={report} source={source} supplier={supplier}
          onResend={(text, resendKind) => source && send({ ...source, text, fileName: source.fileName.replace(/(\.\w+)?$/, (ext) => `-corrige${ext}`) }, resendKind)} />
      )}
      <Card title="Imports récents" icon={History} flush>
        {history.isPending ? <p className="cat-empty-mini clients-muted">Chargement…</p> : history.isError ? <div className="cat-card__body"><ErrorState error={history.error} /></div> : (
          (Array.isArray(history.data) ? history.data : []).length === 0 ? <p className="cat-empty-mini clients-muted">Aucun import.</p> : (
            <TableScroll label="Imports récents">
              <table className="clients-table cat-table cat-table--inner">
                <thead><tr><th scope="col">Date</th><th scope="col">Fichier</th><th scope="col">Type</th><th scope="col" className="cat-num">Lignes</th><th scope="col">Statut</th><th scope="col" className="cat-hide-sm">Auteur</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead>
                <tbody>{(Array.isArray(history.data) ? history.data : []).map((b) => (
                  <tr key={b.id} className={b.id === batchId ? "is-selected" : undefined}>
                    <td className="clients-muted tnum">{formatDateTime(b.uploadedAtUtc)}</td>
                    <td className="cat-strong">{b.fileName}</td>
                    <td className="cat-secondary">{KINDS.find((k) => k.value === b.kind)?.label}</td>
                    <td className="cat-num tnum">{b.rowCount}</td>
                    <td><Badge tone={STATUS[b.status].tone}>{STATUS[b.status].label}</Badge></td>
                    <td className="cat-secondary cat-hide-sm">{b.uploadedBy}</td>
                    <td>{(b.status === "uploaded" || b.status === "checked") && <Button size="sm" variant="tertiary" onClick={() => { setPreview(null); setSource(null); setBatchId(b.id); }}>Reprendre</Button>}</td>
                  </tr>
                ))}</tbody>
              </table>
            </TableScroll>
          )
        )}
      </Card>
    </section>
  );
}

/** Questions strictement nécessaires, déduites de l'aperçu serveur. */
function questionsOf(preview: ImportPreview, kind: string, supplierId: string | null, families: { code: string; label: string }[]): Question[] {
  const mapping = preview.suggestedMapping;
  const questions: Question[] = [];
  const asked = new Set<string>();
  const headerSamples = (header: string) => {
    const index = preview.headers.indexOf(header);
    const values = index >= 0 ? preview.sampleRows.slice(0, 3).map((r) => r[index] ?? "").filter((v) => v !== "") : [];
    return values.length > 0 ? `Exemples : ${values.join(" · ")}` : "Colonne vide dans les premières lignes.";
  };
  for (const s of preview.suggestions.filter((x) => x.confidence === "ambiguous" && mapping[x.field] !== x.header)) {
    s.alternatives.forEach((a) => asked.add(a));
    const fromAssistant = s.source === "assistant";
    questions.push({
      key: `col:${s.header}`,
      title: `Que contient la colonne « ${s.header} » ?`,
      hint: `${headerSamples(s.header)}${fromAssistant ? ` — proposition de l'assistant : ${label(s.field)} (à confirmer${s.alternatives.some((a) => /Price|coefficient/.test(a)) ? ", c'est un montant" : ""}).` : ""}`,
      options: [{ value: "", label: "— Choisir —" }, ...s.alternatives.map((a) => ({ value: a, label: label(a) })), { value: "@ignore", label: "Aucun — ignorer la colonne" }],
      proposed: fromAssistant ? s.field : "",
      apply: (m, answer) => (answer === "@ignore" || answer === "" ? m : { ...m, [answer]: s.header }),
    });
  }
  const used = new Set(Object.entries(mapping).filter(([k]) => !k.startsWith("@")).map(([, v]) => v));
  const free = preview.headers.filter((h) => !used.has(h));
  const requiredMissing = preview.fields.filter((f) => f.required && !mapping[f.key] && !asked.has(f.key) && !(kind === "catalog" && (f.key === "familyCode" || f.key === "stockPolicy")));
  if (kind === "prices" && !mapping.sku && !mapping.supplierReference && !asked.has("sku") && !asked.has("supplierReference")) {
    requiredMissing.push({ key: supplierId ? "supplierReference" : "sku", required: true });
  }
  for (const f of requiredMissing) {
    questions.push({
      key: `req:${f.key}`,
      title: `Quelle colonne contient « ${label(f.key)} » ?`,
      hint: "Champ obligatoire que MMV n'a pas reconnu avec certitude.",
      options: [{ value: "", label: "— Choisir une colonne —" }, ...free.map((h) => ({ value: h, label: h }))],
      proposed: "",
      apply: (m, answer) => (answer === "" ? m : { ...m, [f.key]: answer }),
    });
  }
  if (kind === "catalog" && !mapping.familyCode && !mapping["@familyCode"]) {
    questions.push({
      key: "opt:family", title: "Quelle famille pour tout le fichier ?", hint: "Le fichier n'a pas de colonne « Famille » : MMV ne la devine jamais.",
      options: [{ value: "", label: "— Choisir —" }, ...families.map((f) => ({ value: f.code, label: f.label }))], proposed: "",
      apply: (m, answer) => (answer === "" ? m : { ...m, "@familyCode": answer }),
    });
  }
  if (kind === "catalog" && !mapping.stockPolicy && !mapping["@stockPolicy"]) {
    questions.push({
      key: "opt:policy", title: "Quelle politique de stock pour tout le fichier ?", hint: "Stocké (en magasin), sur commande, ou prestation.",
      options: [{ value: "", label: "— Choisir —" }, ...Object.entries(POLICY_LABELS).map(([value, text]) => ({ value, label: text }))], proposed: "",
      apply: (m, answer) => (answer === "" ? m : { ...m, "@stockPolicy": answer }),
    });
  }
  if (preview.decimalAmbiguous && !mapping["@decimal"]) {
    questions.push({
      key: "opt:decimal", title: "Comment lire les montants comme « 1,234 » ?", hint: "Certaines valeurs peuvent se lire comme des milliers ou des décimales.",
      options: [{ value: "", label: "— Choisir —" }, { value: ",", label: "Virgule décimale (1,234 = 1 euro 23)" }, { value: ".", label: "Point décimal (1,234 = mille deux cent trente-quatre)" }],
      proposed: "", apply: (m, answer) => (answer === "" ? m : { ...m, "@decimal": answer }),
    });
  }
  return questions;
}

function BatchSteps({ batchId, preview, report, source, supplier, onResend }: {
  batchId: string;
  preview: ImportPreview | null;
  report: ReturnType<typeof useCatalogQuery<ImportReport>>;
  source: Source | null;
  supplier: SupplierChoice | null;
  onResend: (content: string, kind: string) => void;
}) {
  const toast = useToast();
  const families = useFamilies();
  const check = useCatalogWrite<ImportSummary>();
  const apply = useCatalogWrite<ImportSummary>();
  const cancel = useCatalogWrite<ImportSummary>();
  const [mapping, setMapping] = useState<Record<string, string>>(preview?.suggestedMapping ?? {});
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [answered, setAnswered] = useState(false);
  const [details, setDetails] = useState(false);
  const [corrections, setCorrections] = useState<Record<number, Record<string, string>>>({});
  const autoChecked = useRef(false);
  const kind = report.data?.batch.kind ?? "catalog";
  const ready = kind !== "catalog" || families.length > 0;
  const questions = preview && ready ? questionsOf(preview, kind, report.data?.batch.supplierId ?? null, families) : [];
  const pendingQuestions = answered ? [] : questions;

  const runCheck = (next: Record<string, string>) => {
    if (!report.data) return;
    setMapping(next);
    check.mutate({ method: "POST", path: `/api/imports/${batchId}/check`, body: { version: report.data.batch.version, mapping: next } });
  };

  // Tout est reconnu : contrôle à blanc lancé d'office (aucune configuration demandée).
  useEffect(() => {
    if (autoChecked.current || !preview || !report.data || report.data.batch.status !== "uploaded" || !ready) return;
    if (questions.length === 0) {
      autoChecked.current = true;
      // `mapping` vaut déjà la correspondance proposée : seul le contrôle à blanc est lancé.
      check.mutate({ method: "POST", path: `/api/imports/${batchId}/check`, body: { version: report.data.batch.version, mapping: preview.suggestedMapping } });
    }
  });

  // L'utilisateur est conduit à ce qu'il doit faire : l'analyse à l'arrivée du fichier, puis le récapitulatif contrôlé.
  const status = report.data?.batch.status;
  const loaded = report.data !== undefined;
  useEffect(() => {
    if (preview && loaded) document.getElementById("import-analysis")?.scrollIntoView({ block: "start", behavior: "smooth" });
  }, [preview, loaded]);
  useEffect(() => {
    if (status === "checked") document.getElementById("import-recap")?.scrollIntoView({ block: "start", behavior: "smooth" });
  }, [status]);

  if (report.isPending) return <Loading label="Chargement du lot…" />;
  if (report.isError) return <ErrorState error={report.error} title="Impossible de charger le lot" onRetry={() => void report.refetch()} />;
  const { batch, summary, rows, valuesVisible } = report.data;
  const closed = batch.status === "applied" || batch.status === "cancelled";
  const fields = preview?.fields ?? Object.keys(batch.mapping).filter((k) => !k.startsWith("@")).map((key) => ({ key, required: false }));
  const effective = Object.keys(mapping).length > 0 ? mapping : batch.mapping;
  const suggestion = (field: string) => preview?.suggestions.find((s) => s.field === field && s.header === effective[field]);
  const errorRows = rows.filter((r) => r.outcome === "error");
  const setField = (key: string, value: string) => setMapping((m) => (value === "" ? Object.fromEntries(Object.entries(m).filter(([k]) => k !== key)) : { ...m, [key]: value }));
  const columnOf = (field: string) => effective[field];
  const correctable = source !== null && !closed && errorRows.length > 0;
  const pending = Object.keys(corrections).length;
  const fileHasPrices = batch.kind === "catalog" && (preview?.suggestions ?? []).some((s) => s.field === "supplierReference")
    && (preview?.headers ?? []).some((h) => /prix|price|pa\s*ht|tarif|cost/i.test(h));
  const mappedColumns = new Set(Object.entries(effective).filter(([k]) => !k.startsWith("@")).map(([, v]) => v));
  const byAssistant = (preview?.suggestions ?? []).filter((s) => s.source === "assistant" && effective[s.field] === s.header).length;
  const ignored = (preview?.headers ?? []).filter((h) => !mappedColumns.has(h));
  const analysis = preview?.analysis;
  const missingAnswers = pendingQuestions.filter((q) => (answers[q.key] ?? q.proposed) === "");
  const elapsed = apply.data?.elapsedMs ?? check.data?.elapsedMs;

  const confirmAnswers = () => {
    const next = pendingQuestions.reduce((m, q) => q.apply(m, answers[q.key] ?? q.proposed), { ...(preview?.suggestedMapping ?? {}) });
    setAnswered(true);
    runCheck(next);
  };
  // Corrections : le lot courant est abandonné (tracé) et la version corrigée du fichier déposée comme nouveau lot.
  const resendCorrected = () => {
    if (!source) return;
    cancel.mutate({ method: "POST", path: `/api/imports/${batchId}/cancel`, body: { version: batch.version } });
    onResend(applyCorrections(source.text, corrections), batch.kind);
  };

  return (
    <>
      {preview && (
        <Card id="import-analysis" title="2. Analyse automatique" icon={Sparkles} actions={<span className="clients-muted">{preview.rowCount} ligne{preview.rowCount > 1 ? "s" : ""} dans le fichier</span>}>
          <div className="cat-stack">
            <p className="clients-muted" style={{ margin: 0 }}>
              <Info size={14} aria-hidden="true" /> {source ? `${source.fileName} · encodage ${ENCODING_LABELS[source.encoding]} · ` : ""}
              séparateur {preview.delimiter === "tab" ? "tabulation" : `« ${preview.delimiter} »`} · {mappedColumns.size} colonne{mappedColumns.size > 1 ? "s" : ""} reconnue{mappedColumns.size > 1 ? "s" : ""}
              {byAssistant > 0 && ` (dont ${byAssistant} par l'assistant IA)`}{ignored.length > 0 && ` · ${ignored.length} ignorée${ignored.length > 1 ? "s" : ""}`}
              {preview.profileApplied && " · correspondance apprise du fournisseur reprise"}
            </p>
            {analysis && <AnalysisLine analysis={analysis} />}
            {analysis && analysis.warnings.length > 0 && (
              <p className="clients-field__hint" style={{ margin: 0 }}>
                À vérifier : {analysis.warnings.map((w) => `« ${w.header} » — ${WARNINGS[w.code] ?? w.code}`).join(" ; ")}. Aucune valeur n&apos;a été modifiée.
              </p>
            )}
            {preview.alreadyApplied && (
              <Banner tone="warn" icon={Info} title="Ce fichier a déjà été importé" compact>
                Le même fichier (contenu identique) a déjà été appliqué : il ne peut pas l&apos;être une seconde fois. Déposez la version à jour du fournisseur.
              </Banner>
            )}
            {pendingQuestions.length > 0 && !closed && (
              <div className="cat-variant" role="group" aria-label="Quelques précisions">
                <div className="cat-variant__head"><Pencil size={14} aria-hidden="true" />Quelques précisions avant le contrôle ({pendingQuestions.length})</div>
                <div className="cat-grid">
                  {pendingQuestions.map((q) => (
                    <SelectField key={q.key} label={q.title} name={q.key} value={answers[q.key] ?? q.proposed} required hint={q.hint}
                      onChange={(value) => setAnswers((a) => ({ ...a, [q.key]: value }))} options={q.options} />
                  ))}
                </div>
                <div className="clients-actions" style={{ justifyContent: "flex-start" }}>
                  <Button variant="primary" disabled={missingAnswers.length > 0 || check.isPending} onClick={confirmAnswers}>
                    <ClipboardCheck size={16} aria-hidden="true" />Valider et contrôler les lignes
                  </Button>
                  {missingAnswers.length > 0 && <span className="clients-muted">{missingAnswers.length} réponse{missingAnswers.length > 1 ? "s" : ""} manquante{missingAnswers.length > 1 ? "s" : ""}</span>}
                </div>
              </div>
            )}
            <div>
              <Button size="sm" variant="tertiary" aria-expanded={details} onClick={() => setDetails((d) => !d)}>
                <Columns3 size={14} aria-hidden="true" />{details ? "Masquer la correspondance détaillée" : "Voir ou modifier la correspondance des colonnes"}
              </Button>
            </div>
            {details && (
              <div className="cat-stack">
                <TableScroll label="Correspondance des colonnes">
                  <table className="clients-table cat-table cat-table--inner cat-table--compact">
                    <thead><tr><th scope="col">Champ MMV</th><th scope="col">Colonne du fichier</th><th scope="col">Origine</th><th scope="col">Exemples</th></tr></thead>
                    <tbody>{fields.map((field) => {
                      const column = effective[field.key];
                      const index = column ? preview.headers.indexOf(column) : -1;
                      const s = suggestion(field.key);
                      return (
                        <tr key={field.key}>
                          <td><span className="cat-strong">{label(field.key)}</span>{field.required && <Badge tone="arch">requis</Badge>}</td>
                          <td style={{ minWidth: 200 }}>
                            <SelectField label={`Colonne pour ${label(field.key)}`} name={`map-${field.key}`} value={column ?? ""} disabled={closed}
                              onChange={(header) => setField(field.key, header)}
                              options={[{ value: "", label: "— Ignorer —" }, ...preview.headers.map((h) => ({ value: h, label: h }))]} />
                          </td>
                          <td>{s && s.confidence !== "ambiguous" ? <Badge tone={CONFIDENCE[s.confidence].tone}>{CONFIDENCE[s.confidence].label}</Badge> : column ? <Badge tone="info">Votre choix</Badge> : <span className="clients-muted">—</span>}</td>
                          <td className="cat-secondary cat-td-fill">{index >= 0 ? preview.sampleRows.slice(0, 3).map((r) => r[index] ?? "").filter((v) => v !== "").join(" · ") || "—" : "—"}</td>
                        </tr>
                      );
                    })}</tbody>
                  </table>
                </TableScroll>
                {!closed && <div><Button size="sm" disabled={check.isPending} onClick={() => { setAnswered(true); runCheck(effective); }}><ClipboardCheck size={14} aria-hidden="true" />Recontrôler avec cette correspondance</Button></div>}
              </div>
            )}
          </div>
        </Card>
      )}
      <Card id="import-recap" title={closed ? "4. Import terminé" : "3. Récapitulatif avant import"} icon={ClipboardCheck} actions={<Badge tone={STATUS[batch.status].tone} small={false}>{STATUS[batch.status].label}</Badge>}>
        <div className="cat-stack">
          <ErrorBanner error={check.error ?? apply.error ?? cancel.error} />
          {check.isPending && <Loading label="Contrôle des lignes (aucune écriture)…" />}
          {batch.status === "uploaded" && !check.isPending && (
            <p className="clients-muted" style={{ margin: 0 }}>{pendingQuestions.length > 0 ? "Répondez aux précisions ci-dessus : MMV contrôlera ensuite chaque ligne, sans rien écrire." : "Contrôle des lignes en préparation…"}</p>
          )}
          {batch.status !== "uploaded" && (
            <>
              <div className="cat-counters">
                <Counter label="Créations" value={summary.create} />
                <Counter label="Modifications" value={summary.update} />
                <Counter label="Inchangées" value={summary.unchanged} />
                <Counter label="Erreurs" value={summary.error} tone={summary.error > 0 ? "err" : undefined} />
              </div>
              <p className="clients-muted" style={{ margin: 0 }}>
                {summary.total} ligne(s) lue(s) : {summary.create} {closed ? "créée(s)" : "à créer"}{batch.kind === "catalog" && " (SKU générés s'ils sont absents)"},
                {" "}{summary.update} {closed ? "modifiée(s)" : "à modifier"}, {summary.unchanged} sans changement, <strong>{summary.error} en erreur</strong>. Les articles absents du fichier ne changent pas.
                {batch.containsCosts && " Le fichier contient des coûts : import réservé au droit costs.write."}
                {elapsed !== undefined && elapsed !== null && ` ${apply.data ? "Import" : "Contrôle"} en ${seconds(elapsed)} (serveur).`}
              </p>
            </>
          )}
          {batch.status === "checked" && summary.error > 0 && (
            <Banner tone="err" icon={CircleX} title="Import impossible tant qu'une ligne est en erreur (tout ou rien)" compact>
              {correctable ? "Corrigez les valeurs signalées ci-dessous puis recontrôlez : une version corrigée du fichier est déposée, l'originale reste tracée." : "Corrigez le fichier puis déposez-le de nouveau ; aucune ligne n'a été écrite."}
            </Banner>
          )}
          {batch.status === "applied" && (
            <Banner tone="ok" icon={CheckCheck} title="Import terminé" compact>
              Toutes les lignes ont été écrites en une seule opération ; ce fichier ne pourra plus être réimporté.
              {batch.supplierId && ` Correspondance mémorisée${supplier ? ` pour ${supplier.name}` : " pour ce fournisseur"} : elle sera reprise à son prochain fichier.`}
            </Banner>
          )}
          {batch.status === "applied" && fileHasPrices && source && (
            <Banner tone="info" icon={Tags} title="Ce fichier contient aussi des prix" compact
              action={<Button size="sm" onClick={() => onResend(source.text, "prices")}>Importer les tarifs de ce fichier</Button>}>
              Second passage « Prix du magasin » : rapprochement par la référence du fournisseur, montants contrôlés avant import.
            </Banner>
          )}
          {!closed && batch.status !== "uploaded" && (
            <div className="clients-actions clients-actions--wrap" style={{ justifyContent: "flex-start" }}>
              <Button variant="primary" disabled={batch.status !== "checked" || summary.error > 0 || apply.isPending || summary.create + summary.update === 0}
                onClick={() => apply.mutate({ method: "POST", path: `/api/imports/${batchId}/apply`, body: { version: batch.version } }, { onSuccess: (r) => toast.show("Import terminé", `${r.create} création(s), ${r.update} modification(s).`) })}>
                <Play size={16} aria-hidden="true" />{apply.isPending ? "Import en cours…" : `Importer ${summary.create + summary.update} ligne${summary.create + summary.update > 1 ? "s" : ""}`}
              </Button>
              {correctable && (
                <Button disabled={pending === 0} onClick={resendCorrected}>
                  <Pencil size={16} aria-hidden="true" />Recontrôler avec {pending} correction(s)
                </Button>
              )}
              <span className="cat-formbar__spacer" />
              <Button variant="danger" onClick={() => cancel.mutate({ method: "POST", path: `/api/imports/${batchId}/cancel`, body: { version: batch.version } })}><X size={16} aria-hidden="true" />Abandonner</Button>
            </div>
          )}
          {!valuesVisible && <Banner tone="neutral" icon={Info} title="Valeurs masquées" compact>Le fichier contient des coûts : leur lecture exige le droit costs.read.</Banner>}
        </div>
      </Card>
      {errorRows.length > 0 && (
        <TablePanel foot={<TableFoot icon={FileSpreadsheet}>{errorRows.length} ligne(s) en erreur sur {rows.length}</TableFoot>}>
          <table className="clients-table cat-table cat-table--compact">
            <thead><tr><th scope="col" className="cat-num">Ligne</th><th scope="col">Résultat</th><th scope="col">Erreurs et corrections</th>{valuesVisible && <th scope="col">Valeurs lues</th>}</tr></thead>
            <tbody>{errorRows.map((row) => (
              <tr key={row.lineNo} className="is-warning">
                <td className="cat-num tnum">{row.lineNo}</td>
                <td><Badge tone={OUTCOMES[row.outcome]?.tone ?? "arch"}>{OUTCOMES[row.outcome]?.label ?? row.outcome}</Badge></td>
                <td className="cat-td-fill">
                  {row.errors.map((e) => {
                    const column = columnOf(e.field);
                    return (
                      <div key={`${e.field}-${e.code}`} className="cat-fix">
                        <span className="cat-tone-err">{label(e.field)} : {ROW_ERRORS[e.code] ?? e.code}</span>
                        {correctable && column && (
                          <input className="cat-fix__input" aria-label={`Correction ligne ${row.lineNo}, ${label(e.field)}`} placeholder="Nouvelle valeur"
                            value={corrections[row.lineNo]?.[column] ?? ""}
                            onChange={(event) => setCorrections((c) => ({ ...c, [row.lineNo]: { ...(c[row.lineNo] ?? {}), [column]: event.target.value } }))} />
                        )}
                      </div>
                    );
                  })}
                </td>
                {valuesVisible && <td className="cat-secondary">{row.values ? Object.entries(row.values).map(([k, v]) => `${label(k)} = ${v}`).join(" ; ") : ""}</td>}
              </tr>
            ))}</tbody>
          </table>
        </TablePanel>
      )}
    </>
  );
}

/** Ce que MMV a fait automatiquement : moteur utilisé et durées mesurées par le serveur. */
function AnalysisLine({ analysis }: { analysis: NonNullable<ImportPreview["analysis"]> }) {
  const down = analysis.assistantStatus === "unavailable" || analysis.assistantStatus === "timeout" || analysis.assistantStatus === "invalid_output";
  const text = {
    used: `Assistant IA local (${analysis.assistantModel ?? "modèle local"}) consulté pour les colonnes non reconnues par les règles — ses propositions ont été vérifiées par le serveur.`,
    not_needed: "Toutes les colonnes ont été reconnues par les règles ou la correspondance apprise : assistant IA non sollicité.",
    disabled: "Analyse par règles (assistant IA local non activé sur ce serveur).",
    unavailable: "Assistant IA local indisponible : analyse par règles seulement, l'import reste possible.",
    timeout: "Assistant IA local trop lent : analyse par règles seulement, l'import reste possible.",
    invalid_output: "Réponse de l'assistant IA écartée (format non conforme) : analyse par règles seulement.",
  }[analysis.assistantStatus];
  return (
    <p className={cn("clients-field__hint", down && "cat-tone-warn")} style={{ margin: 0 }}>
      <Bot size={12} aria-hidden="true" /> {text} <span className="tnum"><Timer size={12} aria-hidden="true" /> lecture {seconds(analysis.parseMs)}
        {analysis.assistantStatus !== "not_needed" && analysis.assistantStatus !== "disabled" && ` · assistant ${seconds(analysis.assistantMs)}`}</span>
    </p>
  );
}

function Counter({ label: text, value, tone }: { label: string; value: number; tone?: "err" }) {
  return (
    <div className="cat-counter">
      <div className="cat-counter__label">{text}</div>
      <div className={cn("cat-counter__value", tone === "err" && "cat-tone-err")}>{value}</div>
    </div>
  );
}
