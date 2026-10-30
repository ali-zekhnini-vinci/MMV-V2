// Fournisseurs (Lot 6 B-125 à B-130, adaptés : statut actif / archivé au lieu de la suppression, identifiants légaux
// facultatifs selon le pays — contrôle de format seulement —, contact minimal).
import { Archive, ArrowLeft, Plus, RotateCcw, Save } from "lucide-react";
import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { type Page, query, type SupplierDetail, type SupplierRow } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import { POLICY_LABELS } from "./catalogFormat.ts";
import {
  Badge, Dialog, ErrorBanner, ErrorState, Field, Loading, PageHeader, Pager, Panel,
} from "./CatalogUi.tsx";
import { useCatalogQuery, useCatalogWrite } from "./catalogData.ts";

const LEGAL_KEYS: Record<string, { key: string; label: string }[]> = {
  MA: [{ key: "ice", label: "ICE (15 chiffres)" }, { key: "if", label: "Identifiant fiscal" }, { key: "rc", label: "Registre de commerce" }],
  BE: [{ key: "enterpriseNumber", label: "Numéro d'entreprise (BCE)" }],
};

export function SuppliersListView() {
  const navigate = useNavigate();
  const access = useCatalogAccess();
  const [q, setQ] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("active");
  const [page, setPage] = useState(1);
  const list = useCatalogQuery<Page<SupplierRow>>(`/api/suppliers${query({ q: search, status, page, pageSize: 50 })}`);
  return (
    <section>
      <PageHeader title="Fournisseurs" actions={access.suppliersWrite && (
        <Button variant="primary" onClick={() => navigate("/produits/fournisseurs/nouveau")}><Plus size={16} aria-hidden="true" />Nouveau fournisseur</Button>
      )} />
      <form className="cat-toolbar" role="search" onSubmit={(e) => { e.preventDefault(); setSearch(q.trim()); setPage(1); }}>
        <input className="cat-input cat-toolbar__search" placeholder="Nom, code, ville…" aria-label="Rechercher un fournisseur" value={q} onChange={(e) => setQ(e.target.value)} />
        <select className="cat-input" aria-label="Statut" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
          <option value="active">Actifs</option><option value="archived">Archivés</option><option value="all">Tous</option>
        </select>
        <Button type="submit">Rechercher</Button>
      </form>
      {list.isPending ? <Loading label="Chargement des fournisseurs…" /> : list.isError ? <ErrorState error={list.error} onRetry={() => void list.refetch()} /> : list.data.items.length === 0 ? (
        <p className="cat-muted">Aucun fournisseur pour ces critères.</p>
      ) : (
        <>
          <div className="cat-table-wrap">
            <table className="cat-table">
              <thead><tr><th scope="col">Code</th><th scope="col">Nom</th><th scope="col">Pays</th><th scope="col">Ville</th><th scope="col">Contact</th><th scope="col" className="cat-num">Variantes</th><th scope="col" className="cat-num">Principal de</th><th scope="col">Statut</th></tr></thead>
              <tbody>{list.data.items.map((s) => (
                <tr key={s.id} className="cat-row" tabIndex={0} onClick={() => navigate(`/produits/fournisseurs/${s.id}`)} onKeyDown={(e) => e.key === "Enter" && navigate(`/produits/fournisseurs/${s.id}`)}>
                  <td className="cat-mono">{s.code}</td><td className="cat-strong">{s.name}</td><td>{s.countryCode}</td><td>{s.city ?? "—"}</td><td>{s.contactName ?? "—"}</td>
                  <td className="cat-num">{s.variantCount}</td><td className="cat-num">{s.primaryCount}</td>
                  <td>{s.status === "active" ? <Badge tone="success">Actif</Badge> : <Badge tone="neutral">Archivé</Badge>}</td>
                </tr>
              ))}</tbody>
            </table>
          </div>
          <Pager page={list.data.page} pageSize={list.data.pageSize} total={list.data.totalCount} onPage={setPage} />
        </>
      )}
    </section>
  );
}

interface SupplierDraft {
  code: string; name: string; countryCode: string; contactName: string; email: string; phone: string; addressLine: string; postalCode: string; city: string;
  legalIdentifiers: Record<string, string>; notes: string;
}

const EMPTY: SupplierDraft = { code: "", name: "", countryCode: "BE", contactName: "", email: "", phone: "", addressLine: "", postalCode: "", city: "", legalIdentifiers: {}, notes: "" };

const draftOf = (s: SupplierDetail): SupplierDraft => ({
  code: s.code, name: s.name, countryCode: s.countryCode, contactName: s.contactName ?? "", email: s.email ?? "", phone: s.phone ?? "", addressLine: s.addressLine ?? "",
  postalCode: s.postalCode ?? "", city: s.city ?? "", legalIdentifiers: s.legalIdentifiers, notes: s.notes ?? "",
});

function SupplierForm({ draft, onChange, errors }: { draft: SupplierDraft; onChange: (d: SupplierDraft) => void; errors?: Record<string, string[]> }) {
  const set = (patch: Partial<SupplierDraft>) => onChange({ ...draft, ...patch });
  return (
    <>
      <div className="cat-grid">
        <Field label="Code interne" name="code" value={draft.code} onChange={(code) => set({ code })} required errors={errors} />
        <Field label="Nom" name="name" value={draft.name} onChange={(name) => set({ name })} required errors={errors} />
        <Field label="Pays (ISO, ex. BE, MA)" name="countryCode" value={draft.countryCode} onChange={(c) => set({ countryCode: c.toUpperCase(), legalIdentifiers: {} })} required errors={errors} />
        <Field label="Contact" name="contactName" value={draft.contactName} onChange={(contactName) => set({ contactName })} errors={errors} hint="Un seul contact, données minimales." />
        <Field label="E-mail" name="email" value={draft.email} onChange={(email) => set({ email })} type="email" errors={errors} />
        <Field label="Téléphone" name="phone" value={draft.phone} onChange={(phone) => set({ phone })} errors={errors} />
        <Field label="Adresse" name="addressLine" value={draft.addressLine} onChange={(addressLine) => set({ addressLine })} errors={errors} />
        <Field label="Code postal" name="postalCode" value={draft.postalCode} onChange={(postalCode) => set({ postalCode })} errors={errors} />
        <Field label="Ville" name="city" value={draft.city} onChange={(city) => set({ city })} errors={errors} />
        {(LEGAL_KEYS[draft.countryCode] ?? []).map((k) => (
          <Field key={k.key} label={k.label} name={`legalIdentifiers.${k.key}`} value={draft.legalIdentifiers[k.key] ?? ""} errors={errors}
            onChange={(v) => set({ legalIdentifiers: { ...draft.legalIdentifiers, [k.key]: v } })} hint="Contrôle de format seulement." />
        ))}
      </div>
      <Field label="Notes (aucune donnée sensible)" name="notes" value={draft.notes} onChange={(notes) => set({ notes })} errors={errors} multiline />
    </>
  );
}

const body = (d: SupplierDraft) => ({
  ...d,
  ...Object.fromEntries(Object.entries(d).filter(([, v]) => typeof v === "string").map(([k, v]) => [k, (v as string).trim() === "" ? null : v])),
  legalIdentifiers: d.legalIdentifiers,
});

export function SupplierCreateView() {
  const navigate = useNavigate();
  const write = useCatalogWrite<{ id: string }>();
  const [draft, setDraft] = useState<SupplierDraft>(EMPTY);
  return (
    <section>
      <PageHeader title="Nouveau fournisseur" actions={<Button variant="tertiary" onClick={() => navigate("/produits/fournisseurs")}><ArrowLeft size={16} aria-hidden="true" />Fournisseurs</Button>} />
      <ErrorBanner error={write.error} />
      <Panel><SupplierForm draft={draft} onChange={setDraft} errors={write.error?.error.fieldErrors} /></Panel>
      <div className="cat-actions">
        <Button variant="primary" disabled={write.isPending} onClick={() => write.mutate({ method: "POST", path: "/api/suppliers", body: body(draft) }, { onSuccess: (r) => navigate(`/produits/fournisseurs/${r.id}`) })}>
          <Save size={16} aria-hidden="true" />Enregistrer
        </Button>
      </div>
    </section>
  );
}

export function SupplierDetailView() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const access = useCatalogAccess();
  const detail = useCatalogQuery<SupplierDetail>(`/api/suppliers/${id}`);
  const write = useCatalogWrite();
  const [editing, setEditing] = useState<SupplierDraft | null>(null);
  const [archiving, setArchiving] = useState(false);
  const [accept, setAccept] = useState(false);
  if (detail.isPending) return <Loading label="Chargement du fournisseur…" />;
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
  const s = detail.data;
  const pending = write.error?.error.code === "incomplete_variants_unconfirmed";
  return (
    <section>
      <PageHeader title={s.name} subtitle={<>{s.code} · {s.countryCode} · {s.status === "active" ? <Badge tone="success">Actif</Badge> : <Badge tone="neutral">Archivé</Badge>}</>}
        actions={<>
          <Button variant="tertiary" onClick={() => navigate("/produits/fournisseurs")}><ArrowLeft size={16} aria-hidden="true" />Fournisseurs</Button>
          {access.suppliersWrite && s.status === "active" && <Button onClick={() => setEditing(draftOf(s))}>Modifier</Button>}
          {access.suppliersWrite && s.status === "active" && <Button variant="danger" onClick={() => { setAccept(false); setArchiving(true); }}><Archive size={16} aria-hidden="true" />Archiver</Button>}
          {access.suppliersWrite && s.status === "archived" && (
            <Button onClick={() => write.mutate({ method: "POST", path: `/api/suppliers/${s.id}/reactivate`, body: { version: s.version } })}><RotateCcw size={16} aria-hidden="true" />Réactiver</Button>
          )}
        </>} />
      <ErrorBanner error={write.error} />
      <Panel title="Fiche">
        <dl className="cat-facts">
          <div><dt>Contact</dt><dd>{s.contactName ?? "—"}</dd></div>
          <div><dt>E-mail</dt><dd>{s.email ?? "—"}</dd></div>
          <div><dt>Téléphone</dt><dd>{s.phone ?? "—"}</dd></div>
          <div><dt>Adresse</dt><dd>{[s.addressLine, s.postalCode, s.city].filter(Boolean).join(", ") || "—"}</dd></div>
          {Object.entries(s.legalIdentifiers).map(([k, v]) => <div key={k}><dt>{k.toUpperCase()}</dt><dd className="cat-mono">{v}</dd></div>)}
          {s.notes && <div><dt>Notes</dt><dd>{s.notes}</dd></div>}
          {s.archiveReason && <div><dt>Motif d&apos;archivage</dt><dd>{s.archiveReason}</dd></div>}
        </dl>
      </Panel>
      {s.variants && (
        <Panel title={`Produits associés (${s.variants.length})`}>
          {s.variants.length === 0 ? <p className="cat-muted">Aucun produit rattaché.</p> : (
            <table className="cat-table">
              <thead><tr><th scope="col">SKU</th><th scope="col">Produit</th><th scope="col">Politique</th><th scope="col">Référence fournisseur</th><th scope="col">Rôle</th></tr></thead>
              <tbody>{s.variants.map((v) => (
                <tr key={v.variantId} className="cat-row" tabIndex={0} onClick={() => navigate(`/produits/modele/${v.modelId}?variante=${v.variantId}`)}>
                  <td className="cat-mono">{v.sku}</td><td>{v.modelName} · {v.label}</td><td>{POLICY_LABELS[v.stockPolicy]}</td><td>{v.supplierReference ?? "—"}</td>
                  <td>{v.isPrimary ? <Badge tone="info">Principal</Badge> : "Secondaire"}</td>
                </tr>
              ))}</tbody>
            </table>
          )}
        </Panel>
      )}
      <Dialog title="Modifier le fournisseur" open={editing !== null} onClose={() => setEditing(null)}>
        <ErrorBanner error={write.error} />
        {editing && <SupplierForm draft={editing} onChange={setEditing} errors={write.error?.error.fieldErrors} />}
        <div className="cat-actions">
          <Button onClick={() => setEditing(null)}>Annuler</Button>
          <Button variant="primary" onClick={() => editing && write.mutate({ method: "PUT", path: `/api/suppliers/${s.id}`, body: { version: s.version, ...body(editing) } }, { onSuccess: () => setEditing(null) })}>Enregistrer</Button>
        </div>
      </Dialog>
      <Dialog title="Archiver le fournisseur" open={archiving} onClose={() => setArchiving(false)}>
        <p>Aucune suppression : la fiche et ses rattachements restent consultables. Un fournisseur archivé ne peut plus devenir principal.</p>
        {pending && (
          <label className="cat-check"><input type="checkbox" checked={accept} onChange={(e) => setAccept(e.target.checked)} />
            Je confirme : les variantes commandées à la demande dont il est le principal deviendront « fiche incomplète ».</label>
        )}
        <div className="cat-actions">
          <Button onClick={() => setArchiving(false)}>Annuler</Button>
          <Button variant="danger" onClick={() => write.mutate({ method: "POST", path: `/api/suppliers/${s.id}/archive`, body: { version: s.version, acceptIncompleteVariants: accept } }, { onSuccess: () => setArchiving(false) })}>Archiver</Button>
        </div>
      </Dialog>
    </section>
  );
}
