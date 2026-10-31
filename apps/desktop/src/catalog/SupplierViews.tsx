// Fournisseurs — Lot 6 groupes 28–33 (liste clair/sombre, recherche, filtres, nouveau, erreurs, modification, non
// enregistré, détail, produits associés, sans produit, suppression / refus / désactivation, états transverses) sur les
// règles V2 : statut actif / ARCHIVÉ au lieu de la suppression physique (la maquette « supprimer » devient « archiver »),
// plusieurs fournisseurs possibles par variante, fiche incomplète autorisée (code, nom, pays seuls exigés), identifiants
// légaux facultatifs selon le pays (contrôle de format seulement), contact minimal.
import { Archive, Box, Building2, Flag, Info, PackageOpen, Pencil, Plus, RotateCcw, Save, SearchX, Star, Truck, X } from "lucide-react";
import { type ReactNode, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { type Page, query, type SupplierDetail, type SupplierRow } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { useLeaveGuard } from "../shell/navigationGuard.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { POLICY_LABELS } from "./catalogFormat.ts";
import {
  Badge, Banner, Card, EmptyState, ErrorBanner, ErrorState, Field, FilterChip, FormBar, FormSection, KeyValues, Loading, Modal, PageHeader, RowChevron, SearchBox,
  TableFoot, TablePanel, Toolbar, ToolbarSpacer,
} from "./CatalogUi.tsx";
import { errorMessage, rowLink, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";

const LEGAL_KEYS: Record<string, { key: string; label: string }[]> = {
  MA: [{ key: "ice", label: "ICE (15 chiffres)" }, { key: "if", label: "Identifiant fiscal" }, { key: "rc", label: "Registre de commerce" }],
  BE: [{ key: "enterpriseNumber", label: "Numéro d'entreprise (BCE)" }],
};

const COUNTRIES: Record<string, string> = { BE: "Belgique", MA: "Maroc" };

// ---------- liste (supListLight … supCsv) ----------

export function SuppliersListView() {
  const navigate = useNavigate();
  const access = useCatalogAccess();
  const [q, setQ] = useState("");
  const [status, setStatus] = useState("active");
  const [country, setCountry] = useState("");
  const [page, setPage] = useState(1);
  const list = useCatalogQuery<Page<SupplierRow>>(`/api/suppliers${query({ q, status, countryCode: country, page, pageSize: 50 })}`);
  const filtered = q !== "" || status !== "active" || country !== "";
  const create = access.suppliersWrite && <Button variant="primary" onClick={() => navigate("/produits/fournisseurs/nouveau")}><Plus size={18} aria-hidden="true" />Nouveau fournisseur</Button>;
  return (
    <section className="cat-screen" aria-labelledby="suppliers-title">
      <PageHeader title="Fournisseurs" subtitle="Répertoire des fournisseurs — plusieurs fournisseurs possibles par variante, archivage sans suppression"
        actions={<>
          {access.catalogImport && <Button onClick={() => navigate("/produits/import")}><Box size={16} aria-hidden="true" />Importer un CSV</Button>}
          {create}
        </>} />
      <Toolbar>
        <SearchBox value={q} onSearch={(value) => { setQ(value); setPage(1); }} placeholder="Rechercher par nom, code ou ville…" label="Rechercher un fournisseur" />
        <FilterChip icon={Flag} label="Pays" value={country} onChange={(value) => { setCountry(value); setPage(1); }}
          options={[{ value: "", label: "Tous les pays" }, ...Object.entries(COUNTRIES).map(([value, label]) => ({ value, label }))]} />
        <FilterChip icon={Archive} label="Statut" value={status} defaultValue="active" onChange={(value) => { setStatus(value); setPage(1); }}
          options={[{ value: "active", label: "Actifs" }, { value: "archived", label: "Archivés" }, { value: "all", label: "Tous" }]} />
        <ToolbarSpacer />
      </Toolbar>
      {list.isPending ? <Loading label="Chargement des fournisseurs…" /> : list.isError ? (
        <ErrorState error={list.error} title="Impossible de charger les fournisseurs" onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        filtered ? (
          <EmptyState tone="info" icon={SearchX} title="Aucun résultat"
            action={<Button onClick={() => { setQ(""); setStatus("active"); setCountry(""); setPage(1); }}><RotateCcw size={16} aria-hidden="true" />Réinitialiser la recherche</Button>}>
            {q ? `Aucun fournisseur ne correspond à « ${q} ». Vérifiez l'orthographe ou réinitialisez la recherche.` : "Aucun fournisseur pour ces filtres."}
          </EmptyState>
        ) : (
          <EmptyState icon={Truck} title="Aucun fournisseur" action={create}>Ajoutez votre premier fournisseur pour pouvoir le rattacher aux variantes.</EmptyState>
        )
      ) : (
        <TablePanel fill foot={
          <TableFoot icon={Truck} page={list.data.page} pageSize={list.data.pageSize} total={list.data.totalCount} onPage={setPage}>
            {list.data.totalCount} fournisseur{list.data.totalCount > 1 ? "s" : ""}
          </TableFoot>
        }>
          <table className="clients-table cat-table">
            <thead><tr><th scope="col">Nom</th><th scope="col">Code</th><th scope="col" className="cat-hide-sm">Pays</th><th scope="col" className="cat-hide-md">Contact</th><th scope="col" className="cat-num">Variantes</th><th scope="col" className="cat-num cat-hide-sm">Principal de</th><th scope="col">Statut</th><th scope="col"><span className="sr-only">Ouvrir</span></th></tr></thead>
            <tbody>{list.data.items.map((s) => (
              <tr key={s.id} className={s.status === "archived" ? "cat-row is-archived" : "cat-row"} {...rowLink(`/produits/fournisseurs/${s.id}`, (href) => navigate(href))}>
                <td>
                  <div className="clients-cell">
                    <span className="cat-thumb cat-thumb--round"><Building2 size={18} aria-hidden="true" /></span>
                    <div>
                      <div className="clients-cell__name">{s.name}</div>
                      <div className="cat-sub">{[s.city, COUNTRIES[s.countryCode] ?? s.countryCode].filter(Boolean).join(" · ")}</div>
                    </div>
                  </div>
                </td>
                <td><span className="cat-ref">{s.code}</span></td>
                <td className="cat-secondary cat-hide-sm">{s.countryCode}</td>
                <td className="cat-secondary cat-hide-md">{s.contactName ?? "—"}</td>
                <td className="cat-num">{s.variantCount > 0 ? <Badge tone="accent" icon={Box}>{s.variantCount}</Badge> : <span className="clients-muted">0</span>}</td>
                <td className="cat-num tnum cat-hide-sm">{s.primaryCount}</td>
                <td>{s.status === "active" ? <Badge tone="ok">Actif</Badge> : <Badge tone="arch" icon={Archive}>Archivé</Badge>}</td>
                <td><RowChevron /></td>
              </tr>
            ))}</tbody>
          </table>
        </TablePanel>
      )}
    </section>
  );
}

// ---------- formulaire (supNew, supErrors, supEdit, supUnsaved) ----------

interface SupplierDraft {
  code: string; name: string; countryCode: string; contactName: string; email: string; phone: string; addressLine: string; postalCode: string; city: string;
  legalIdentifiers: Record<string, string>; notes: string;
}

const EMPTY: SupplierDraft = { code: "", name: "", countryCode: "BE", contactName: "", email: "", phone: "", addressLine: "", postalCode: "", city: "", legalIdentifiers: {}, notes: "" };

const draftOf = (s: SupplierDetail): SupplierDraft => ({
  code: s.code, name: s.name, countryCode: s.countryCode, contactName: s.contactName ?? "", email: s.email ?? "", phone: s.phone ?? "", addressLine: s.addressLine ?? "",
  postalCode: s.postalCode ?? "", city: s.city ?? "", legalIdentifiers: s.legalIdentifiers, notes: s.notes ?? "",
});

const body = (d: SupplierDraft) => ({
  ...d,
  ...Object.fromEntries(Object.entries(d).filter(([, v]) => typeof v === "string").map(([k, v]) => [k, (v as string).trim() === "" ? null : v])),
  legalIdentifiers: d.legalIdentifiers,
});

function SupplierFormSections({ draft, onChange, errors }: { draft: SupplierDraft; onChange: (d: SupplierDraft) => void; errors?: Record<string, string[]> }) {
  const set = (patch: Partial<SupplierDraft>) => onChange({ ...draft, ...patch });
  const legal = LEGAL_KEYS[draft.countryCode] ?? [];
  return (
    <>
      <FormSection title="Informations du fournisseur" icon={Truck}>
        <Field label="Nom" name="name" value={draft.name} onChange={(name) => set({ name })} required errors={errors} span2 placeholder="Ex. Fournisseur fictif Belgique" />
        <Field label="Code interne" name="code" value={draft.code} onChange={(code) => set({ code })} required errors={errors} placeholder="Ex. FRN-BE-01" hint="Unique ; utilisé par l'import CSV." />
        <Field label="Pays (ISO)" name="countryCode" value={draft.countryCode} onChange={(c) => set({ countryCode: c.toUpperCase(), legalIdentifiers: {} })} required errors={errors} hint="BE ou MA pour les pilotes." />
        <Field label="Contact" name="contactName" value={draft.contactName} onChange={(contactName) => set({ contactName })} errors={errors} hint="Un seul contact, données minimales." />
        <Field label="Téléphone" name="phone" value={draft.phone} onChange={(phone) => set({ phone })} errors={errors} />
        <Field label="E-mail de contact" name="email" value={draft.email} onChange={(email) => set({ email })} type="email" errors={errors} span2 />
        <Field label="Adresse" name="addressLine" value={draft.addressLine} onChange={(addressLine) => set({ addressLine })} errors={errors} span2 placeholder="Adresse postale (facultative)" />
        <Field label="Code postal" name="postalCode" value={draft.postalCode} onChange={(postalCode) => set({ postalCode })} errors={errors} />
        <Field label="Ville" name="city" value={draft.city} onChange={(city) => set({ city })} errors={errors} />
      </FormSection>
      {legal.length > 0 && (
        <FormSection title={`Identifiants légaux — ${COUNTRIES[draft.countryCode] ?? draft.countryCode}`} icon={Flag}>
          {legal.map((k) => (
            <Field key={k.key} label={k.label} name={`legalIdentifiers.${k.key}`} value={draft.legalIdentifiers[k.key] ?? ""} errors={errors}
              onChange={(v) => set({ legalIdentifiers: { ...draft.legalIdentifiers, [k.key]: v } })} hint="Facultatif ; contrôle de format seulement." />
          ))}
        </FormSection>
      )}
      <FormSection title="Notes" icon={Info}>
        <Field label="Notes internes (aucune donnée sensible)" name="notes" value={draft.notes} onChange={(notes) => set({ notes })} errors={errors} multiline span2 />
      </FormSection>
    </>
  );
}

function SupplierFormScreen({ title, subtitle, initial, version, path, method, back, conflictReload }: {
  title: string; subtitle: string; initial: SupplierDraft; version?: number; path: string; method: "POST" | "PUT"; back: string; conflictReload?: () => void;
}) {
  const navigate = useNavigate();
  const toast = useToast();
  const write = useCatalogWrite<{ id: string }>();
  const [draft, setDraft] = useState(initial);
  const [leave, setLeave] = useState<(() => void) | null>(null);
  const dirty = JSON.stringify(draft) !== JSON.stringify(initial) && !write.isSuccess;
  const errors = write.error?.error.fieldErrors;
  useLeaveGuard((proceed) => { if (!dirty) return false; setLeave(() => proceed); return true; });
  const close = () => (dirty ? setLeave(() => () => navigate(back)) : navigate(back));
  const save = () => write.mutate({ method, path, body: { ...(version !== undefined ? { version } : {}), ...body(draft) } },
    { onSuccess: (r) => { toast.show(method === "POST" ? "Fournisseur créé" : "Modifications enregistrées", draft.name); navigate(method === "POST" ? `/produits/fournisseurs/${r.id}` : back); } });
  return (
    <section className="cat-screen cat-form" aria-labelledby="supplier-form">
      <PageHeader title={title} subtitle={subtitle} actions={<Button variant="ghost" onClick={close}><X size={18} aria-hidden="true" />Fermer</Button>} />
      <div className="cat-form__body cat-form__body--narrow">
        {dirty && !write.error && (
          <Banner tone="warn" icon={Info} title="Modifications non enregistrées">Enregistrez pour conserver vos changements avant de quitter la fiche.</Banner>
        )}
        {write.error && (write.error.error.code === "version_conflict" ? (
          <Banner tone="warn" icon={Info} title="Fournisseur modifié sur un autre poste" role="alert"
            action={conflictReload && <Button size="sm" className="cat-btn-warn" onClick={conflictReload}><RotateCcw size={16} aria-hidden="true" />Recharger</Button>}>
            Rechargez pour repartir de la version enregistrée ; vos saisies restent dans le formulaire.
          </Banner>
        ) : (
          <Banner tone="err" icon={Info} title="Le formulaire contient des erreurs" role="alert">
            {errors ? "Corrigez les champs signalés ci-dessous. Seuls le nom, le code et le pays sont obligatoires." : errorMessage(write.error.error)}
          </Banner>
        ))}
        <SupplierFormSections draft={draft} onChange={setDraft} errors={errors} />
      </div>
      <FormBar dirty={dirty} hint="Les champs marqués * sont obligatoires · fiche incomplète autorisée" narrow>
        <Button onClick={close}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending} onClick={save}>
          <Save size={16} aria-hidden="true" />{method === "POST" ? "Créer le fournisseur" : "Enregistrer les modifications"}
        </Button>
      </FormBar>
      <Modal open={leave !== null} onClose={() => setLeave(null)} tone="warn" icon={Info} title="Modifications non enregistrées"
        description="Vous avez modifié ce fournisseur. Si vous fermez maintenant, vos changements seront perdus."
        actions={<>
          <Button variant="ghost" onClick={() => { const go = leave; setLeave(null); go?.(); }}>Fermer sans enregistrer</Button>
          <Button onClick={() => setLeave(null)}>Continuer l&apos;édition</Button>
          <Button variant="primary" onClick={() => { setLeave(null); save(); }}><Save size={16} aria-hidden="true" />Enregistrer</Button>
        </>} />
    </section>
  );
}

export function SupplierCreateView() {
  return <SupplierFormScreen title="Nouveau fournisseur" subtitle="Ajouter un fournisseur au répertoire" initial={EMPTY} path="/api/suppliers" method="POST" back="/produits/fournisseurs" />;
}

export function SupplierEditView() {
  const { id = "" } = useParams();
  const detail = useCatalogQuery<SupplierDetail>(`/api/suppliers/${id}`);
  if (detail.isPending) return <Loading label="Chargement du fournisseur…" />;
  if (detail.isError) return <ErrorState error={detail.error} title="Impossible de charger le fournisseur" onRetry={() => void detail.refetch()} />;
  const s = detail.data;
  return <SupplierFormScreen key={s.version} title={`Modifier — ${s.name}`} subtitle={s.code} initial={draftOf(s)} version={s.version} path={`/api/suppliers/${s.id}`} method="PUT"
    back={`/produits/fournisseurs/${s.id}`} conflictReload={() => void detail.refetch()} />;
}

// ---------- détail (supDetailLight, supDetailDark, supProducts, supNoProduct, supDelete, supDeleteRefused, supNoDeactivate) ----------

export function SupplierDetailView() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const access = useCatalogAccess();
  const detail = useCatalogQuery<SupplierDetail>(`/api/suppliers/${id}`);
  const write = useCatalogWrite();
  const [archiving, setArchiving] = useState(false);
  const [accept, setAccept] = useState(false);
  const [reason, setReason] = useState("");
  if (detail.isPending) return <Loading label="Chargement du fournisseur…" />;
  if (detail.isError) return <ErrorState error={detail.error} title="Impossible de charger le fournisseur" onRetry={() => void detail.refetch()} />;
  const s = detail.data;
  const pending = write.error?.error.code === "incomplete_variants_unconfirmed";
  const archived = s.status === "archived";
  const variants = s.variants ?? [];
  return (
    <section className="cat-screen" aria-labelledby="supplier-title">
      <PageHeader
        back={{ label: "Fournisseurs", onClick: () => navigate("/produits/fournisseurs") }}
        title={s.name}
        titleExtra={archived ? <Badge tone="arch" icon={Archive}>Archivé</Badge> : <Badge tone="ok">Actif</Badge>}
        subtitle={`${s.code} · ${COUNTRIES[s.countryCode] ?? s.countryCode}`}
        actions={access.suppliersWrite && (archived ? (
          <Button disabled={write.isPending} onClick={() => write.mutate({ method: "POST", path: `/api/suppliers/${s.id}/reactivate`, body: { version: s.version } }, { onSuccess: () => toast.show("Fournisseur réactivé", s.name) })}>
            <RotateCcw size={16} aria-hidden="true" />Réactiver
          </Button>
        ) : (
          <>
            <Button onClick={() => navigate(`/produits/fournisseurs/${s.id}/modifier`)}><Pencil size={16} aria-hidden="true" />Modifier</Button>
            <Button variant="danger" onClick={() => { write.reset(); setAccept(false); setReason(""); setArchiving(true); }}><Archive size={16} aria-hidden="true" />Archiver</Button>
          </>
        ))}
      />
      {!archiving && <ErrorBanner error={write.error} onReload={() => void detail.refetch()} />}
      {archived && (
        <Banner tone="neutral" icon={Archive} title="Fournisseur archivé">
          Il ne peut plus devenir principal ; sa fiche et ses rattachements restent consultables (aucune suppression).{s.archiveReason ? ` Motif : ${s.archiveReason}.` : ""}
        </Banner>
      )}
      <div className="cat-detail cat-detail--side-left">
        <Card title="Coordonnées" icon={Truck}>
          <KeyValues rows={[
            ["Code", <span key="c" className="cat-ref">{s.code}</span>],
            ["Pays", COUNTRIES[s.countryCode] ?? s.countryCode],
            ["Contact", s.contactName ?? "—"],
            ["Téléphone", <span key="t" className="tnum">{s.phone ?? "—"}</span>],
            ["E-mail", s.email ?? "—"],
            ["Adresse", [s.addressLine, s.postalCode, s.city].filter(Boolean).join(", ") || "—"],
            ...Object.entries(s.legalIdentifiers).map(([k, v]): [string, ReactNode] => [LEGAL_KEYS[s.countryCode]?.find((x) => x.key === k)?.label ?? k.toUpperCase(), <span key={k} className="cat-mono">{v}</span>]),
            ["Notes", <span key="n" className="cat-secondary" style={{ fontWeight: 500 }}>{s.notes ?? "—"}</span>],
          ]} />
        </Card>
        {variants.length > 0 ? (
          <Card title={`Produits associés (${variants.length})`} icon={Box} flush
            foot={<span className="clients-muted">Plusieurs fournisseurs possibles par variante ; « principal » = fournisseur de commande par défaut.</span>}>
            <table className="clients-table cat-table cat-table--inner">
              <thead><tr><th scope="col">Référence</th><th scope="col">Produit</th><th scope="col" className="cat-hide-sm">Politique</th><th scope="col">Réf. fournisseur</th><th scope="col">Rôle</th></tr></thead>
              <tbody>{variants.map((v) => (
                <tr key={v.variantId} className={v.status === "archived" ? "is-archived" : undefined} {...rowLink(`/produits/modele/${v.modelId}?variante=${v.variantId}`, (href) => navigate(href))}>
                  <td><span className="cat-ref">{v.sku}</span></td>
                  <td><div className="clients-cell__name" style={{ fontSize: 13.5 }}>{v.modelName}</div><div className="cat-sub">{v.label}</div></td>
                  <td className="cat-secondary cat-hide-sm">{POLICY_LABELS[v.stockPolicy]}</td>
                  <td className="cat-secondary">{v.supplierReference ?? "—"}</td>
                  <td>{v.isPrimary ? <Badge tone="accent" icon={Star}>Principal</Badge> : <Badge tone="arch">Secondaire</Badge>}</td>
                </tr>
              ))}</tbody>
            </table>
          </Card>
        ) : (
          <Card title="Produits associés" icon={Box}>
            <div className="cat-empty-mini">
              <span className="cat-thumb"><PackageOpen size={28} aria-hidden="true" /></span>
              <div className="cat-empty-mini__title">Aucun produit associé</div>
              <p className="cat-empty-mini__text">Rattachez ce fournisseur depuis la fiche d&apos;une variante (bloc « Fournisseurs »). Il peut aussi être archivé sans rompre aucun historique.</p>
            </div>
          </Card>
        )}
      </div>
      <Modal open={archiving} onClose={() => setArchiving(false)} tone={pending ? "warn" : "err"} icon={Archive} title={`Archiver « ${s.name} » ?`}
        description="Aucune suppression physique : la fiche, ses rattachements et l'historique des réceptions restent consultables. Un fournisseur archivé ne peut plus devenir principal."
        actions={<>
          <Button onClick={() => setArchiving(false)}>Annuler</Button>
          <Button className="cat-btn-danger-solid" disabled={write.isPending || (pending && !accept)}
            onClick={() => write.mutate({ method: "POST", path: `/api/suppliers/${s.id}/archive`, body: { version: s.version, reason: reason.trim() || null, acceptIncompleteVariants: accept } },
              { onSuccess: () => { setArchiving(false); toast.show("Fournisseur archivé", s.name); } })}>
            <Archive size={16} aria-hidden="true" />Archiver
          </Button>
        </>}>
        {pending ? (
          <Banner tone="warn" icon={Info} title="Variantes concernées" role="alert" compact>
            Ce fournisseur est le principal de variantes commandées à la demande : elles deviendront « fiche incomplète ».
          </Banner>
        ) : write.error && <Banner tone="err" icon={Info} title="Archivage refusé" role="alert" compact>{errorMessage(write.error.error)}</Banner>}
        {pending && (
          <label className="cat-check cat-check--plain"><input type="checkbox" checked={accept} onChange={(e) => setAccept(e.target.checked)} />
            Je confirme : les variantes dont il est le principal deviendront « fiche incomplète ».</label>
        )}
        <Field label="Motif (facultatif)" name="supplierArchiveReason" value={reason} onChange={setReason} multiline />
      </Modal>
    </section>
  );
}
