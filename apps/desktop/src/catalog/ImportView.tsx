// Import CSV intelligent (D-05-18, 05-D, COR-05-04) — parcours V2 absent du Lot 6, dessiné avec ses composants (étapes,
// cartes .card, choix en carte, table.dt, badges, bandeaux, compteurs) : dépôt (encodage et séparateur détectés,
// fournisseur facultatif) → correspondance PROPOSÉE (modèle du fournisseur, synonymes, confiance affichée, ambiguïtés à
// confirmer, valeurs par défaut choisies, séparateur décimal) → contrôle à blanc (rapport par ligne, AUCUNE écriture,
// corrections possibles : nouvelle version du fichier = nouveau lot) → application explicite, TOUT OU RIEN. Aucun import
// destructif, aucun montant converti, aucune valeur inventée ; coûts importés seulement avec costs.write ; un même
// fichier ne s'applique qu'une fois. Fichier fournisseur avec prix : second passage « tarifs » rapproché par référence.
import { CheckCheck, CircleX, ClipboardCheck, Columns3, FileSpreadsheet, FileUp, History, Info, Pencil, Play, Tags, X } from "lucide-react";
import { useState } from "react";
import type { ImportBatch, ImportPreview, ImportReport, ImportSummary, MappingSuggestion } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { errorMessage, useActiveStore, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";
import { formatDateTime, POLICY_LABELS } from "./catalogFormat.ts";
import {
  Badge, type BadgeTone, Banner, Card, ErrorBanner, ErrorState, Loading, PageHeader, RadioCard, SelectField, Switch, TableFoot, TablePanel, TableScroll,
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

const CONFIDENCE: Record<MappingSuggestion["confidence"], { label: string; tone: BadgeTone }> = {
  profile: { label: "Modèle fournisseur", tone: "accent" },
  exact: { label: "Reconnu", tone: "ok" },
  probable: { label: "Probable — à vérifier", tone: "warn" },
  ambiguous: { label: "Ambigu — à confirmer", tone: "err" },
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

const STEPS = ["Dépôt du fichier", "Correspondance des colonnes", "Contrôle et corrections", "Application tout ou rien"];

interface Source {
  fileName: string;
  text: string;
  encoding: CsvEncoding;
}

export function ImportView() {
  const { store } = useActiveStore();
  const access = useCatalogAccess();
  const history = useCatalogQuery<ImportBatch[]>("/api/imports");
  const upload = useCatalogWrite<ImportPreview>();
  const [kind, setKind] = useState("catalog");
  const [supplier, setSupplier] = useState<SupplierChoice | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [source, setSource] = useState<Source | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);
  const report = useCatalogQuery<ImportReport>(batchId ? `/api/imports/${batchId}` : null);
  const batchStatus = report.data?.batch.status;
  const step = !batchId ? 0 : batchStatus === "applied" || batchStatus === "cancelled" ? 4 : batchStatus === "checked" ? 3 : preview ? 1 : 2;
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
    send(next, kind);
  };

  return (
    <section className="cat-screen" aria-labelledby="import-title">
      <PageHeader title="Import CSV" subtitle="Séparateur, encodage et colonnes détectés automatiquement — contrôle à blanc obligatoire, application tout ou rien, aucune conversion de devise" />
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
          <div className="cat-grid">
            {supplierAllowed && (
              <SupplierCombobox label="Fournisseur du fichier (facultatif)" value={supplier} onChange={setSupplier} allowCreate={false}
                hint="Désigné : son modèle d'import est repris, « Ref » devient sa référence et les lignes lui sont rattachées (aucun doublon à la réémission)." />
            )}
            {kind === "prices" && (
              <p className="clients-field__hint" style={{ alignSelf: "end" }}>
                Magasin : <strong>{store?.name}</strong> — {store?.currency}. Les prix sont importés dans la devise du magasin, sans aucune conversion.
              </p>
            )}
          </div>
          <label className="cat-drop">
            <FileSpreadsheet size={28} aria-hidden="true" />
            <span className="cat-drop__title">{upload.isPending ? "Dépôt en cours…" : "Choisir un fichier CSV ou texte…"}</span>
            <span className="clients-field__hint">Séparateur « ; », « , » ou tabulation ; UTF-8 ou Windows-1252. Rien n&apos;est écrit avant l&apos;application explicite.</span>
            <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" onChange={(e) => void onFile(e.target.files?.[0])} />
          </label>
          {source && preview && (
            <p className="clients-muted" style={{ margin: 0 }}>
              <Info size={14} aria-hidden="true" /> {source.fileName} · encodage {ENCODING_LABELS[source.encoding]} · séparateur {preview.delimiter === "tab" ? "tabulation" : `« ${preview.delimiter} »`} · {preview.rowCount} ligne(s)
              {preview.profileApplied && " · modèle d'import du fournisseur repris"}
            </p>
          )}
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
  const saveProfile = useCatalogWrite();
  const [mapping, setMapping] = useState<Record<string, string>>(preview?.suggestedMapping ?? {});
  const [remember, setRemember] = useState(false);
  const [corrections, setCorrections] = useState<Record<number, Record<string, string>>>({});
  if (report.isPending) return <Loading label="Chargement du lot…" />;
  if (report.isError) return <ErrorState error={report.error} title="Impossible de charger le lot" onRetry={() => void report.refetch()} />;
  const { batch, summary, rows, valuesVisible } = report.data;
  const closed = batch.status === "applied" || batch.status === "cancelled";
  const fields = preview?.fields ?? Object.keys(batch.mapping).filter((k) => !k.startsWith("@")).map((key) => ({ key, required: false }));
  const suggestion = (field: string) => preview?.suggestions.find((s) => s.field === field && s.header === mapping[field]);
  const errorRows = rows.filter((r) => r.outcome === "error");
  const option = (key: string) => mapping[key] ?? "";
  const setOption = (key: string, value: string) => setMapping((m) => (value === "" ? Object.fromEntries(Object.entries(m).filter(([k]) => k !== key)) : { ...m, [key]: value }));
  const unconfirmed = (preview?.suggestions ?? []).filter((s) => s.confidence === "ambiguous" && mapping[s.field] === undefined);
  const columnOf = (field: string) => mapping[field] ?? batch.mapping[field];
  const correctable = source !== null && !closed && errorRows.length > 0;
  const pending = Object.keys(corrections).length;
  const fileHasPrices = batch.kind === "catalog" && (preview?.suggestions ?? []).some((s) => s.field === "supplierReference")
    && (preview?.headers ?? []).some((h) => /prix|price|pa\s*ht|tarif|cost/i.test(h));

  // Corrections : le lot courant est abandonné (tracé) et la version corrigée du fichier déposée comme nouveau lot.
  const resendCorrected = () => {
    if (!source) return;
    cancel.mutate({ method: "POST", path: `/api/imports/${batchId}/cancel`, body: { version: batch.version } });
    onResend(applyCorrections(source.text, corrections), batch.kind);
  };
  const runCheck = () => check.mutate({ method: "POST", path: `/api/imports/${batchId}/check`, body: { version: batch.version, mapping } }, {
    onSuccess: () => {
      if (remember && batch.supplierId && (batch.kind === "catalog" || batch.kind === "prices") && preview) {
        saveProfile.mutate({ method: "PUT", path: `/api/imports/profiles/${batch.supplierId}/${batch.kind}`, body: { mapping, headers: preview.headers } },
          { onSuccess: () => toast.show("Modèle d'import enregistré", supplier ? `Proposé pour les prochains fichiers de ${supplier.name}.` : undefined) });
      }
    },
  });

  return (
    <>
      {preview && (
        <Card title="2. Correspondance des colonnes" icon={Columns3} actions={<span className="clients-muted">{preview.rowCount} ligne{preview.rowCount > 1 ? "s" : ""} dans le fichier</span>}>
          <div className="cat-stack">
            {unconfirmed.length > 0 && (
              <Banner tone="warn" icon={Info} title="Colonnes ambiguës à confirmer" compact>
                {unconfirmed.map((s) => `« ${s.header} » (${s.alternatives.map((a) => FIELD_LABELS[a] ?? a).join(" ou ")})`).join(", ")} : choisissez le champ correspondant ou laissez-les ignorées.
              </Banner>
            )}
            {preview.decimalAmbiguous && !mapping["@decimal"] && (
              <Banner tone="warn" icon={Info} title="Séparateur décimal à confirmer" compact>
                Certaines valeurs (ex. « 1,234 ») peuvent se lire comme des milliers ou des décimales : précisez-le ci-dessous, sinon ces lignes seront signalées en erreur.
              </Banner>
            )}
            <TableScroll label="Correspondance des colonnes">
              <table className="clients-table cat-table cat-table--inner cat-table--compact">
                <thead><tr><th scope="col">Champ MMV</th><th scope="col">Colonne du fichier</th><th scope="col">Confiance</th><th scope="col">Exemples</th></tr></thead>
                <tbody>{fields.map((field) => {
                  const column = mapping[field.key];
                  const index = column ? preview.headers.indexOf(column) : -1;
                  const s = suggestion(field.key);
                  const ambiguousHere = preview.suggestions.find((x) => x.confidence === "ambiguous" && x.alternatives.includes(field.key) && mapping[field.key] === undefined);
                  return (
                    <tr key={field.key}>
                      <td><span className="cat-strong">{FIELD_LABELS[field.key] ?? field.key}</span>{field.required && <Badge tone="arch">requis</Badge>}</td>
                      <td style={{ minWidth: 200 }}>
                        <SelectField label={`Colonne pour ${FIELD_LABELS[field.key] ?? field.key}`} name={`map-${field.key}`} value={column ?? ""}
                          onChange={(header) => setOption(field.key, header)}
                          options={[{ value: "", label: "— Ignorer —" }, ...preview.headers.map((h) => ({ value: h, label: h }))]} />
                      </td>
                      <td>{s ? <Badge tone={CONFIDENCE[s.confidence].tone}>{CONFIDENCE[s.confidence].label}</Badge> : column ? <Badge tone="info">Choix manuel</Badge>
                        : ambiguousHere ? <Badge tone="err">« {ambiguousHere.header} » ?</Badge> : <span className="clients-muted">—</span>}</td>
                      <td className="cat-secondary cat-td-fill">{index >= 0 ? preview.sampleRows.slice(0, 3).map((r) => r[index] ?? "").filter((v) => v !== "").join(" · ") || "—" : "—"}</td>
                    </tr>
                  );
                })}</tbody>
              </table>
            </TableScroll>
            <div className="cat-grid cat-grid--3">
              {batch.kind === "catalog" && !mapping.familyCode && (
                <SelectField label="Famille pour tout le fichier" name="opt-family" value={option("@familyCode")} onChange={(v) => setOption("@familyCode", v)} required
                  options={[{ value: "", label: "— Choisir —" }, ...families.map((f) => ({ value: f.code, label: f.label }))]} hint="Colonne « Famille » absente : valeur choisie par vous, jamais déduite." />
              )}
              {batch.kind === "catalog" && !mapping.stockPolicy && (
                <SelectField label="Politique de stock pour tout le fichier" name="opt-policy" value={option("@stockPolicy")} onChange={(v) => setOption("@stockPolicy", v)} required
                  options={[{ value: "", label: "— Choisir —" }, ...Object.entries(POLICY_LABELS).map(([value, label]) => ({ value, label }))]} />
              )}
              {batch.kind === "prices" && (
                <SelectField label="Séparateur décimal des montants" name="opt-decimal" value={option("@decimal")} onChange={(v) => setOption("@decimal", v)}
                  options={[{ value: "", label: "Détection automatique" }, { value: ",", label: "Virgule (12,50)" }, { value: ".", label: "Point (12.50)" }]}
                  hint={preview.decimalSeparator ? `Détecté : « ${preview.decimalSeparator} »` : "Aucune décimale détectée"} />
              )}
            </div>
            {batch.supplierId && (batch.kind === "catalog" || batch.kind === "prices") && (
              <Switch label={`Enregistrer cette correspondance comme modèle d'import${supplier ? ` de ${supplier.name}` : " du fournisseur"}`} checked={remember} onChange={setRemember}
                hint="Reproposée automatiquement pour ses prochains fichiers (modifiable à chaque fois)." />
            )}
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
            Compte rendu : {summary.total} ligne(s) — {summary.create} création(s){batch.kind === "catalog" && " (SKU générés à l'application s'ils sont absents)"}, {summary.update} modification(s),
            {" "}{summary.unchanged} inchangée(s), <strong>{summary.error} erreur(s)</strong>. Les lignes absentes du fichier ne changent rien.
            {batch.containsCosts && " Le fichier contient des coûts : application réservée au droit costs.write."}
          </p>
          {batch.status === "checked" && summary.error > 0 && (
            <Banner tone="err" icon={CircleX} title="Application impossible : tout ou rien" compact>
              {correctable ? "Corrigez les valeurs signalées dans le rapport ci-dessous puis recontrôlez : une nouvelle version du fichier est déposée, l'originale reste tracée." : "Corrigez le fichier puis déposez-le de nouveau ; aucune ligne n'a été écrite."}
            </Banner>
          )}
          {batch.status === "applied" && <Banner tone="ok" icon={CheckCheck} title="Lot appliqué" compact>Toutes les lignes ont été écrites en une seule transaction ; ce fichier ne pourra plus être réappliqué.</Banner>}
          {batch.status === "applied" && fileHasPrices && source && (
            <Banner tone="info" icon={Tags} title="Ce fichier contient aussi des prix" compact
              action={<Button size="sm" onClick={() => onResend(source.text, "prices")}>Importer les tarifs de ce fichier</Button>}>
              Second passage « Prix du magasin » : rapprochement par la référence du fournisseur, montants contrôlés à blanc avant application.
            </Banner>
          )}
          {!closed && (
            <div className="clients-actions clients-actions--wrap" style={{ justifyContent: "flex-start" }}>
              <Button disabled={check.isPending} onClick={runCheck}>
                <ClipboardCheck size={16} aria-hidden="true" />Contrôler (à blanc)
              </Button>
              {correctable && (
                <Button disabled={pending === 0} onClick={resendCorrected}>
                  <Pencil size={16} aria-hidden="true" />Recontrôler avec {pending} correction(s)
                </Button>
              )}
              <Button variant="primary" disabled={batch.status !== "checked" || summary.error > 0 || apply.isPending}
                onClick={() => apply.mutate({ method: "POST", path: `/api/imports/${batchId}/apply`, body: { version: batch.version } }, { onSuccess: (r) => toast.show("Import appliqué", `${r.create} création(s), ${r.update} modification(s).`) })}>
                <Play size={16} aria-hidden="true" />Appliquer (tout ou rien)
              </Button>
              <span className="cat-formbar__spacer" />
              <Button variant="danger" onClick={() => cancel.mutate({ method: "POST", path: `/api/imports/${batchId}/cancel`, body: { version: batch.version } })}><X size={16} aria-hidden="true" />Abandonner</Button>
            </div>
          )}
          {saveProfile.error && <p className="clients-field__error">Modèle d&apos;import non enregistré : {errorMessage(saveProfile.error.error)}</p>}
          {!valuesVisible && <Banner tone="neutral" icon={Info} title="Valeurs masquées" compact>Le fichier contient des coûts : leur lecture exige le droit costs.read.</Banner>}
        </div>
      </Card>
      {rows.length > 0 && (
        <TablePanel foot={<TableFoot icon={FileSpreadsheet}>{rows.length} ligne(s) contrôlée(s) · {errorRows.length} en erreur</TableFoot>}>
          <table className="clients-table cat-table cat-table--compact">
            <thead><tr><th scope="col" className="cat-num">Ligne</th><th scope="col">Résultat</th><th scope="col">Erreurs et corrections</th>{valuesVisible && <th scope="col">Valeurs lues</th>}</tr></thead>
            <tbody>{rows.map((row) => (
              <tr key={row.lineNo} className={row.outcome === "error" ? "is-warning" : undefined}>
                <td className="cat-num tnum">{row.lineNo}</td>
                <td><Badge tone={OUTCOMES[row.outcome]?.tone ?? "arch"}>{OUTCOMES[row.outcome]?.label ?? row.outcome}</Badge></td>
                <td className={row.errors.length > 0 ? "cat-td-fill" : "clients-muted cat-td-fill"}>
                  {row.errors.length === 0 ? "—" : row.errors.map((e) => {
                    const column = columnOf(e.field);
                    return (
                      <div key={`${e.field}-${e.code}`} className="cat-fix">
                        <span className="cat-tone-err">{FIELD_LABELS[e.field] ?? e.field} : {ROW_ERRORS[e.code] ?? e.code}</span>
                        {correctable && column && (
                          <input className="cat-fix__input" aria-label={`Correction ligne ${row.lineNo}, ${FIELD_LABELS[e.field] ?? e.field}`} placeholder="Nouvelle valeur"
                            value={corrections[row.lineNo]?.[column] ?? ""}
                            onChange={(event) => setCorrections((c) => ({ ...c, [row.lineNo]: { ...(c[row.lineNo] ?? {}), [column]: event.target.value } }))} />
                        )}
                      </div>
                    );
                  })}
                </td>
                {valuesVisible && <td className="cat-secondary">{row.values ? Object.entries(row.values).map(([k, v]) => `${FIELD_LABELS[k] ?? k} = ${v}`).join(" ; ") : ""}</td>}
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
