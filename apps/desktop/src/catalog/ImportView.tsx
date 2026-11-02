// Import CSV contrôlé (D-05-18, 05-D) — parcours V2 absent du Lot 6, dessiné avec ses composants (étapes, cartes
// .card, choix en carte du csvModal, table.dt, badges, bandeaux, compteurs) : dépôt → aperçu et correspondance des
// colonnes → contrôle à blanc (rapport par ligne, AUCUNE écriture) → application explicite, TOUT OU RIEN. Aucun import
// destructif : les lignes absentes du fichier ne changent rien. Coûts importés seulement avec costs.write ; un même
// fichier ne s'applique qu'une fois.
import { CheckCheck, CircleX, ClipboardCheck, Columns3, FileSpreadsheet, FileUp, History, Info, Play, X } from "lucide-react";
import { useState } from "react";
import type { ImportBatch, ImportPreview, ImportReport, ImportSummary } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { formatDateTime } from "./catalogFormat.ts";
import {
  Badge, type BadgeTone, Banner, Card, ErrorBanner, ErrorState, Loading, PageHeader, RadioCard, SelectField, TableFoot, TablePanel, TableScroll,
} from "./CatalogUi.tsx";
import { useActiveStore, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";

const KINDS = [
  { value: "catalog", label: "Catalogue (modèles et variantes)", sub: "Familles, modèles, variantes, SKU, politiques" },
  { value: "suppliers", label: "Fournisseurs", sub: "Code, nom, pays, contact" },
  { value: "prices", label: "Prix par magasin", sub: "Prix d'achat HT, coefficient (devise du magasin)" },
];

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

const STEPS = ["Dépôt du fichier", "Correspondance des colonnes", "Contrôle à blanc", "Application tout ou rien"];

export function ImportView() {
  const { store } = useActiveStore();
  const history = useCatalogQuery<ImportBatch[]>("/api/imports");
  const upload = useCatalogWrite<ImportPreview>();
  const [kind, setKind] = useState("catalog");
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);
  const report = useCatalogQuery<ImportReport>(batchId ? `/api/imports/${batchId}` : null);
  const batchStatus = report.data?.batch.status;
  const step = !batchId ? 0 : batchStatus === "applied" || batchStatus === "cancelled" ? 4 : batchStatus === "checked" ? 3 : preview ? 1 : 2;

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    const content = await file.text();
    upload.mutate(
      { method: "POST", path: "/api/imports", body: { kind, storeId: kind === "prices" ? store?.id : null, fileName: file.name, content } },
      { onSuccess: (result) => { setPreview(result); setBatchId(result.batchId); } },
    );
  };

  return (
    <section className="cat-screen" aria-labelledby="import-title">
      <PageHeader title="Import CSV" subtitle="Séparateur « ; », UTF-8 — contrôle à blanc obligatoire, application tout ou rien, aucune conversion de devise" />
      <ol className="cat-steps" aria-label="Étapes de l'import">
        {STEPS.map((label, index) => (
          <li key={label} className={cn("cat-step", index < step && "cat-step--done", index === Math.min(step, 3) && step < 4 && "cat-step--current")} aria-current={index === step ? "step" : undefined}>
            <span className="cat-step__n">{index < step ? <CheckCheck size={14} aria-hidden="true" /> : index + 1}</span>{label}
          </li>
        ))}
      </ol>
      <Card title="1. Dépôt du fichier" icon={FileUp}>
        <div className="cat-stack">
          <ErrorBanner error={upload.error} />
          <div className="cat-radios cat-radios--row" role="radiogroup" aria-label="Type">
            {KINDS.map((k) => <RadioCard key={k.value} name="importKind" value={k.value} checked={kind === k.value} onChange={setKind} label={k.label} sub={k.sub} />)}
          </div>
          {kind === "prices" && (
            <div className="cat-grid">
              <p className="clients-field__hint">
                Magasin cible : <strong>{store?.name}</strong> — {store?.currency}. Les prix sont importés dans la devise de ce magasin, sans conversion.
              </p>
            </div>
          )}
          <label className="cat-drop">
            <FileSpreadsheet size={28} aria-hidden="true" />
            <span className="cat-drop__title">{upload.isPending ? "Dépôt en cours…" : "Choisir un fichier CSV…"}</span>
            <span className="clients-field__hint">Le fichier n&apos;est lu qu&apos;une fois ; rien n&apos;est écrit avant l&apos;application explicite.</span>
            <input type="file" accept=".csv,text/csv" className="sr-only" onChange={(e) => void onFile(e.target.files?.[0])} />
          </label>
        </div>
      </Card>
      {batchId && <BatchSteps key={batchId} batchId={batchId} preview={preview} report={report} />}
      <Card title="Imports récents" icon={History} flush>
        {history.isPending ? <p className="cat-empty-mini clients-muted">Chargement…</p> : history.isError ? <div className="cat-card__body"><ErrorState error={history.error} /></div> : (
          (Array.isArray(history.data) ? history.data : []).length === 0 ? <p className="cat-empty-mini clients-muted">Aucun import.</p> : (
            <TableScroll label="Aperçu du fichier">
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
                  <td>{(b.status === "uploaded" || b.status === "checked") && <Button size="sm" variant="tertiary" onClick={() => { setPreview(null); setBatchId(b.id); }}>Reprendre</Button>}</td>
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

function BatchSteps({ batchId, preview, report }: { batchId: string; preview: ImportPreview | null; report: ReturnType<typeof useCatalogQuery<ImportReport>> }) {
  const toast = useToast();
  const check = useCatalogWrite<ImportSummary>();
  const apply = useCatalogWrite<ImportSummary>();
  const cancel = useCatalogWrite<ImportSummary>();
  const [mapping, setMapping] = useState<Record<string, string>>(preview?.suggestedMapping ?? {});
  if (report.isPending) return <Loading label="Chargement du lot…" />;
  if (report.isError) return <ErrorState error={report.error} title="Impossible de charger le lot" onRetry={() => void report.refetch()} />;
  const { batch, summary, rows, valuesVisible } = report.data;
  const closed = batch.status === "applied" || batch.status === "cancelled";
  const headers = preview?.headers ?? Object.values(batch.mapping);
  const fields = preview?.fields ?? Object.keys(batch.mapping).map((key) => ({ key, required: false }));
  const errorRows = rows.filter((r) => r.outcome === "error");

  return (
    <>
      {preview && (
        <Card title="2. Aperçu et correspondance des colonnes" icon={Columns3} actions={<span className="clients-muted">{preview.rowCount} ligne{preview.rowCount > 1 ? "s" : ""} dans le fichier</span>}>
          <div className="cat-stack">
            <div className="clients-panel">
              <div className="clients-panel__scroll">
                <TableScroll label="Rapport ligne par ligne">
                <table className="clients-table cat-table cat-table--inner cat-table--compact">
                  <thead><tr>{preview.headers.map((h) => <th key={h} scope="col">{h}</th>)}</tr></thead>
                  <tbody>{preview.sampleRows.map((row, i) => <tr key={i}>{row.map((cell, j) => <td key={j} className="cat-secondary">{cell}</td>)}</tr>)}</tbody>
                </table>
                </TableScroll>
              </div>
            </div>
            <div className="cat-grid cat-grid--3">
              {fields.map((field) => (
                <SelectField key={field.key} label={field.key} required={field.required} name={`map-${field.key}`} value={mapping[field.key] ?? ""}
                  onChange={(header) => setMapping((m) => (header === "" ? Object.fromEntries(Object.entries(m).filter(([key]) => key !== field.key)) : { ...m, [field.key]: header }))}
                  options={[{ value: "", label: "— Ignorer —" }, ...headers.map((h) => ({ value: h, label: h }))]} />
              ))}
            </div>
          </div>
        </Card>
      )}
      <Card title="3. Contrôle à blanc puis application" icon={ClipboardCheck} actions={<Badge tone={STATUS[batch.status].tone} small={false}>{STATUS[batch.status].label}</Badge>}>
        <div className="cat-stack">
          <ErrorBanner error={check.error ?? apply.error ?? cancel.error} />
          <div className="cat-counters">
            <Counter label="Créations" value={summary.create} />
            <Counter label="Modifications" value={summary.update} />
            <Counter label="Inchangées" value={summary.unchanged} />
            <Counter label="Erreurs" value={summary.error} tone={summary.error > 0 ? "err" : undefined} />
          </div>
          <p className="clients-muted" style={{ margin: 0 }}>
            {summary.total} ligne(s) : {summary.create} création(s), {summary.update} modification(s), {summary.unchanged} inchangée(s), <strong>{summary.error} erreur(s)</strong>.
            {batch.containsCosts && " Le fichier contient des coûts : application réservée au droit costs.write."}
          </p>
          {batch.status === "checked" && summary.error > 0 && (
            <Banner tone="err" icon={CircleX} title="Application impossible : tout ou rien" compact>Corrigez le fichier puis déposez-le de nouveau ; aucune ligne n&apos;a été écrite.</Banner>
          )}
          {batch.status === "applied" && <Banner tone="ok" icon={CheckCheck} title="Lot appliqué" compact>Toutes les lignes ont été écrites en une seule transaction ; ce fichier ne pourra plus être réappliqué.</Banner>}
          {!closed && (
            <div className="clients-actions clients-actions--wrap" style={{ justifyContent: "flex-start" }}>
              <Button disabled={check.isPending} onClick={() => check.mutate({ method: "POST", path: `/api/imports/${batchId}/check`, body: { version: batch.version, mapping } })}>
                <ClipboardCheck size={16} aria-hidden="true" />Contrôler (à blanc)
              </Button>
              <Button variant="primary" disabled={batch.status !== "checked" || summary.error > 0 || apply.isPending}
                onClick={() => apply.mutate({ method: "POST", path: `/api/imports/${batchId}/apply`, body: { version: batch.version } }, { onSuccess: (r) => toast.show("Import appliqué", `${r.create} création(s), ${r.update} modification(s).`) })}>
                <Play size={16} aria-hidden="true" />Appliquer (tout ou rien)
              </Button>
              <span className="cat-formbar__spacer" />
              <Button variant="danger" onClick={() => cancel.mutate({ method: "POST", path: `/api/imports/${batchId}/cancel`, body: { version: batch.version } })}><X size={16} aria-hidden="true" />Abandonner</Button>
            </div>
          )}
          {!valuesVisible && <Banner tone="neutral" icon={Info} title="Valeurs masquées" compact>Le fichier contient des coûts : leur lecture exige le droit costs.read.</Banner>}
        </div>
      </Card>
      {rows.length > 0 && (
        <TablePanel foot={<TableFoot icon={FileSpreadsheet}>{rows.length} ligne(s) contrôlée(s) · {errorRows.length} en erreur</TableFoot>}>
          <table className="clients-table cat-table cat-table--compact">
            <thead><tr><th scope="col" className="cat-num">Ligne</th><th scope="col">Résultat</th><th scope="col">Erreurs</th>{valuesVisible && <th scope="col">Valeurs</th>}</tr></thead>
            <tbody>{rows.map((row) => (
              <tr key={row.lineNo} className={row.outcome === "error" ? "is-warning" : undefined}>
                <td className="cat-num tnum">{row.lineNo}</td>
                <td><Badge tone={OUTCOMES[row.outcome]?.tone ?? "arch"}>{OUTCOMES[row.outcome]?.label ?? row.outcome}</Badge></td>
                <td className={row.errors.length > 0 ? "cat-tone-err" : "clients-muted"}>{row.errors.map((e) => `${e.field} : ${e.code}`).join(" · ") || "—"}</td>
                {valuesVisible && <td className="cat-secondary">{row.values ? Object.entries(row.values).map(([k, v]) => `${k}=${v}`).join(" ; ") : ""}</td>}
              </tr>
            ))}</tbody>
          </table>
        </TablePanel>
      )}
    </>
  );
}

function Counter({ label, value, tone }: { label: string; value: number; tone?: "err" }) {
  return (
    <div className="cat-counter">
      <div className="cat-counter__label">{label}</div>
      <div className={cn("cat-counter__value", tone === "err" && "cat-tone-err")}>{value}</div>
    </div>
  );
}
