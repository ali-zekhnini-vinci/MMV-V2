// Import CSV contrôlé (D-05-18, 05-D) : dépôt → aperçu et correspondance des colonnes → contrôle à blanc (rapport par
// ligne, aucune écriture) → application explicite, TOUT OU RIEN. Aucun import destructif : les lignes absentes du
// fichier ne changent rien. Coûts importés seulement avec costs.write ; un même fichier ne s'applique qu'une fois.
import { FileUp } from "lucide-react";
import { useState } from "react";
import type { ImportBatch, ImportPreview, ImportReport, ImportSummary } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { formatDateTime } from "./catalogFormat.ts";
import {
  Badge, ErrorBanner, ErrorState, Loading, PageHeader, Panel, SelectField,
} from "./CatalogUi.tsx";
import { useActiveStore, useCatalogQuery, useCatalogWrite } from "./catalogData.ts";

const KINDS = [
  { value: "catalog", label: "Catalogue (modèles et variantes)" },
  { value: "suppliers", label: "Fournisseurs" },
  { value: "prices", label: "Prix par magasin" },
];

const OUTCOMES: Record<string, { label: string; tone: "success" | "info" | "neutral" | "error" }> = {
  create: { label: "Création", tone: "success" },
  update: { label: "Modification", tone: "info" },
  unchanged: { label: "Inchangée", tone: "neutral" },
  error: { label: "Erreur", tone: "error" },
};

export function ImportView() {
  const { stores, store } = useActiveStore();
  const history = useCatalogQuery<ImportBatch[]>("/api/imports");
  const upload = useCatalogWrite<ImportPreview>();
  const [kind, setKind] = useState("catalog");
  const [storeId, setStoreId] = useState(store?.id ?? "");
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    const content = await file.text();
    upload.mutate(
      { method: "POST", path: "/api/imports", body: { kind, storeId: kind === "prices" ? storeId || store?.id : null, fileName: file.name, content } },
      { onSuccess: (result) => { setPreview(result); setBatchId(result.batchId); } },
    );
  };

  return (
    <section>
      <PageHeader title="Import CSV" subtitle="Séparateur « ; », UTF-8. Contrôle à blanc obligatoire avant toute application ; application tout ou rien ; aucune conversion de devise." />
      <Panel title="1. Dépôt du fichier">
        <ErrorBanner error={upload.error} />
        <div className="cat-inline">
          <SelectField label="Type" name="importKind" value={kind} onChange={setKind} options={KINDS} />
          {kind === "prices" && <SelectField label="Magasin cible" name="importStore" value={storeId || store?.id || ""} onChange={setStoreId} options={stores.map((s) => ({ value: s.id, label: `${s.name} — ${s.currency}` }))} />}
          <label className="cat-file">
            <FileUp size={16} aria-hidden="true" />
            Choisir un fichier CSV…
            <input type="file" accept=".csv,text/csv" className="sr-only" onChange={(e) => void onFile(e.target.files?.[0])} />
          </label>
        </div>
        {upload.isPending && <Loading label="Dépôt…" />}
      </Panel>
      {preview && batchId && <BatchSteps key={batchId} batchId={batchId} preview={preview} />}
      <Panel title="Imports récents">
        {history.isPending ? <Loading label="Chargement…" /> : history.isError ? <ErrorState error={history.error} /> : (
          <table className="cat-table">
            <thead><tr><th scope="col">Date</th><th scope="col">Fichier</th><th scope="col">Type</th><th scope="col" className="cat-num">Lignes</th><th scope="col">Statut</th><th scope="col">Auteur</th><th scope="col" /></tr></thead>
            <tbody>{(Array.isArray(history.data) ? history.data : []).map((b) => (
              <tr key={b.id}>
                <td>{formatDateTime(b.uploadedAtUtc)}</td><td>{b.fileName}</td><td>{KINDS.find((k) => k.value === b.kind)?.label}</td><td className="cat-num">{b.rowCount}</td>
                <td><Badge tone={b.status === "applied" ? "success" : b.status === "cancelled" ? "neutral" : "info"}>{{ uploaded: "Déposé", checked: "Contrôlé", applied: "Appliqué", cancelled: "Abandonné" }[b.status]}</Badge></td>
                <td>{b.uploadedBy}</td>
                <td>{(b.status === "uploaded" || b.status === "checked") && <Button size="sm" variant="tertiary" onClick={() => { setPreview(null); setBatchId(b.id); }}>Reprendre</Button>}</td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </Panel>
    </section>
  );
}

function BatchSteps({ batchId, preview }: { batchId: string; preview: ImportPreview | null }) {
  const report = useCatalogQuery<ImportReport>(`/api/imports/${batchId}`);
  const check = useCatalogWrite<ImportSummary>();
  const apply = useCatalogWrite<ImportSummary>();
  const cancel = useCatalogWrite<ImportSummary>();
  const [mapping, setMapping] = useState<Record<string, string>>(preview?.suggestedMapping ?? {});
  if (report.isPending) return <Loading label="Chargement du lot…" />;
  if (report.isError) return <ErrorState error={report.error} />;
  const { batch, summary, rows, valuesVisible } = report.data;
  const closed = batch.status === "applied" || batch.status === "cancelled";
  const headers = preview?.headers ?? Object.values(batch.mapping);
  const fields = preview?.fields ?? Object.keys(batch.mapping).map((key) => ({ key, required: false }));

  return (
    <>
      {preview && (
        <Panel title="2. Aperçu et correspondance des colonnes">
          <div className="cat-table-wrap">
            <table className="cat-table">
              <thead><tr>{preview.headers.map((h) => <th key={h} scope="col">{h}</th>)}</tr></thead>
              <tbody>{preview.sampleRows.map((row, i) => <tr key={i}>{row.map((cell, j) => <td key={j}>{cell}</td>)}</tr>)}</tbody>
            </table>
          </div>
          <div className="cat-grid">
            {fields.map((field) => (
              <SelectField key={field.key} label={`${field.key}${field.required ? " *" : ""}`} name={`map-${field.key}`} value={mapping[field.key] ?? ""}
                onChange={(header) => setMapping((m) => (header === "" ? Object.fromEntries(Object.entries(m).filter(([key]) => key !== field.key)) : { ...m, [field.key]: header }))}
                options={[{ value: "", label: "— Ignorer —" }, ...headers.map((h) => ({ value: h, label: h }))]} />
            ))}
          </div>
        </Panel>
      )}
      <Panel title="3. Contrôle à blanc puis application" actions={<Badge tone={batch.status === "applied" ? "success" : "info"}>{{ uploaded: "Déposé", checked: "Contrôlé", applied: "Appliqué", cancelled: "Abandonné" }[batch.status]}</Badge>}>
        <ErrorBanner error={check.error ?? apply.error ?? cancel.error} />
        <p>{summary.total} ligne(s) : {summary.create} création(s), {summary.update} modification(s), {summary.unchanged} inchangée(s), <strong>{summary.error} erreur(s)</strong>.
          {batch.containsCosts && " Le fichier contient des coûts : application réservée au droit costs.write."}</p>
        {!closed && (
          <div className="cat-actions">
            <Button onClick={() => check.mutate({ method: "POST", path: `/api/imports/${batchId}/check`, body: { version: batch.version, mapping } })}>Contrôler (à blanc)</Button>
            <Button variant="primary" disabled={batch.status !== "checked" || summary.error > 0 || apply.isPending}
              onClick={() => apply.mutate({ method: "POST", path: `/api/imports/${batchId}/apply`, body: { version: batch.version } })}>Appliquer (tout ou rien)</Button>
            <Button variant="danger" onClick={() => cancel.mutate({ method: "POST", path: `/api/imports/${batchId}/cancel`, body: { version: batch.version } })}>Abandonner</Button>
          </div>
        )}
        {rows.length > 0 && (
          <div className="cat-table-wrap">
            <table className="cat-table">
              <thead><tr><th scope="col">Ligne</th><th scope="col">Résultat</th><th scope="col">Erreurs</th>{valuesVisible && <th scope="col">Valeurs</th>}</tr></thead>
              <tbody>{rows.map((row) => (
                <tr key={row.lineNo}>
                  <td className="cat-num">{row.lineNo}</td>
                  <td><Badge tone={OUTCOMES[row.outcome]?.tone ?? "neutral"}>{OUTCOMES[row.outcome]?.label ?? row.outcome}</Badge></td>
                  <td>{row.errors.map((e) => `${e.field} : ${e.code}`).join(" · ") || "—"}</td>
                  {valuesVisible && <td className="cat-muted">{row.values ? Object.entries(row.values).map(([k, v]) => `${k}=${v}`).join(" ; ") : ""}</td>}
                </tr>
              ))}</tbody>
            </table>
          </div>
        )}
        {!valuesVisible && <p className="cat-muted">Valeurs masquées : le fichier contient des coûts (droit costs.read requis).</p>}
      </Panel>
    </>
  );
}
