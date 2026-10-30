// Modèles et variantes (Lot 6 B-102 à B-111, adaptés aux règles approuvées : modèle + variantes, SKU par variante,
// politique par variante, archivage au lieu de suppression). Fiche variante : prix du magasin actif (aperçu SERVEUR,
// recalcul jamais silencieux, prix manuel sous permission), fournisseurs (principal, prix fournisseur sous costs.*),
// stock par emplacement, historique des prix.
import { Archive, ArrowLeft, Plus, RotateCcw, Save, Trash2 } from "lucide-react";
import { useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router";
import type { Family, LensReferences, ModelDetail, PriceHistoryEntry, SupplierRow, Page, VariantDetail } from "../api/catalog.ts";
import { query } from "../api/catalog.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import {
  AVAILABILITY_LABELS, AVAILABILITY_TONES, decimalInput, formatDateTime, formatMoney, POLICY_LABELS, PRICE_STATUS_LABELS,
} from "./catalogFormat.ts";
import {
  Badge, Dialog, ErrorBanner, ErrorState, Field, Loading, PageHeader, Panel, SelectField, StoreSelect,
} from "./CatalogUi.tsx";
import { useActiveStore, useCatalogQuery, useCatalogWrite } from "./catalogData.ts";

interface VariantDraft {
  sku: string;
  label: string;
  manufacturerReference: string;
  barcode: string;
  stockPolicy: string;
  lensIndexId: string;
  treatmentIds: string[];
  attributes: Record<string, string>;
}

const emptyVariant = (policy: string): VariantDraft => ({
  sku: "", label: "", manufacturerReference: "", barcode: "", stockPolicy: policy, lensIndexId: "", treatmentIds: [], attributes: {},
});

const orNull = (value: string) => (value.trim() === "" ? null : value.trim());

function variantBody(draft: VariantDraft, includePolicy: boolean) {
  return {
    sku: draft.sku,
    label: draft.label,
    manufacturerReference: orNull(draft.manufacturerReference),
    barcode: orNull(draft.barcode),
    attributes: draft.attributes,
    lensIndexId: orNull(draft.lensIndexId),
    treatmentIds: draft.treatmentIds,
    ...(includePolicy ? { stockPolicy: draft.stockPolicy } : {}),
  };
}

function useFamilies() {
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  return Array.isArray(families.data) ? families.data : [];
}

// ---------- création ----------

export function ModelCreateView() {
  const navigate = useNavigate();
  const families = useFamilies();
  const lens = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  const write = useCatalogWrite<{ id: string }>();
  const [familyCode, setFamilyCode] = useState("optical_frames");
  const [brand, setBrand] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [lensRangeId, setLensRangeId] = useState("");
  const [variants, setVariants] = useState<VariantDraft[]>([emptyVariant("stocked")]);
  const family = families.find((f) => f.code === familyCode);
  const errors = write.error?.error.fieldErrors;

  const submit = () =>
    write.mutate(
      {
        method: "POST",
        path: "/api/catalog/models",
        body: {
          familyCode, brand: orNull(brand), name, description: orNull(description), lensRangeId: orNull(lensRangeId), attributes: {},
          variants: variants.map((v) => variantBody(v, true)),
        },
      },
      { onSuccess: (result) => navigate(`/produits/modele/${result.id}`) },
    );

  return (
    <section aria-labelledby="new-model">
      <PageHeader
        title="Nouveau produit"
        subtitle="Un modèle (famille, marque, nom) et au moins une variante (SKU, libellé, politique de stock). Les prix se fixent ensuite par magasin."
        actions={<Button variant="tertiary" onClick={() => navigate("/produits")}><ArrowLeft size={16} aria-hidden="true" />Catalogue</Button>}
      />
      <ErrorBanner error={write.error} />
      <Panel title="Modèle">
        <div className="cat-grid">
          <SelectField label="Famille" name="familyCode" value={familyCode} required errors={errors}
            onChange={(value) => { setFamilyCode(value); const f = families.find((x) => x.code === value); setVariants((current) => current.map((v) => ({ ...v, stockPolicy: f?.allowedPolicies[0] ?? "stocked", attributes: {} }))); }}
            options={families.map((f) => ({ value: f.code, label: f.label }))} />
          <Field label="Marque" name="brand" value={brand} onChange={setBrand} errors={errors} />
          <Field label="Nom du modèle" name="name" value={name} onChange={setName} required errors={errors} />
          {familyCode === "ophthalmic_lenses" && (
            <SelectField label="Gamme de verres" name="lensRangeId" value={lensRangeId} required errors={errors} onChange={setLensRangeId}
              options={[{ value: "", label: "— Choisir —" }, ...(lens.data?.ranges ?? []).filter((r) => r.status === "active").map((r) => ({
                value: r.id, label: `${lens.data?.manufacturers.find((m) => m.id === r.manufacturerId)?.name ?? "?"} — ${r.name}`,
              }))]} />
          )}
        </div>
        <Field label="Description" name="description" value={description} onChange={setDescription} errors={errors} multiline />
      </Panel>
      {variants.map((variant, index) => (
        <Panel key={index} title={`Variante ${index + 1}`} actions={variants.length > 1 && (
          <Button size="sm" variant="danger" onClick={() => setVariants((current) => current.filter((_, i) => i !== index))}>
            <Trash2 size={14} aria-hidden="true" />Retirer
          </Button>
        )}>
          <VariantFields family={family} lens={lens.data} draft={variant} prefix={`variants[${index}].`} errors={errors} withPolicy
            onChange={(next) => setVariants((current) => current.map((v, i) => (i === index ? next : v)))} />
        </Panel>
      ))}
      <div className="cat-actions">
        <Button onClick={() => setVariants((current) => [...current, emptyVariant(family?.allowedPolicies[0] ?? "stocked")])}>
          <Plus size={16} aria-hidden="true" />Ajouter une variante
        </Button>
        <Button variant="primary" disabled={write.isPending} onClick={submit}>
          <Save size={16} aria-hidden="true" />{write.isPending ? "Enregistrement…" : "Enregistrer"}
        </Button>
      </div>
    </section>
  );
}

function VariantFields({ family, lens, draft, onChange, errors, prefix = "", withPolicy }: {
  family: Family | undefined;
  lens: LensReferences | undefined;
  draft: VariantDraft;
  onChange: (draft: VariantDraft) => void;
  errors?: Record<string, string[]>;
  prefix?: string;
  withPolicy?: boolean;
}) {
  const set = (patch: Partial<VariantDraft>) => onChange({ ...draft, ...patch });
  return (
    <>
      <div className="cat-grid">
        <Field label="SKU interne" name={`${prefix}sku`} value={draft.sku} onChange={(sku) => set({ sku })} required errors={errors} hint="Unique dans toute l'installation." />
        <Field label="Libellé de la variante" name={`${prefix}label`} value={draft.label} onChange={(label) => set({ label })} required errors={errors} hint="Ex. coloris, calibre." />
        <Field label="Référence fabricant" name={`${prefix}manufacturerReference`} value={draft.manufacturerReference} onChange={(v) => set({ manufacturerReference: v })} errors={errors} />
        <Field label="Code-barres (EAN / GTIN)" name={`${prefix}barcode`} value={draft.barcode} onChange={(v) => set({ barcode: v })} errors={errors} inputMode="numeric" hint="Facultatif ; clé de contrôle vérifiée." />
        {withPolicy && family && (
          <SelectField label="Politique de stock" name={`${prefix}stockPolicy`} value={draft.stockPolicy} onChange={(v) => set({ stockPolicy: v })} required errors={errors}
            options={family.allowedPolicies.map((p) => ({ value: p, label: POLICY_LABELS[p] }))} />
        )}
        {family?.code === "ophthalmic_lenses" && (
          <SelectField label="Indice" name={`${prefix}lensIndexId`} value={draft.lensIndexId} onChange={(v) => set({ lensIndexId: v })} required errors={errors}
            options={[{ value: "", label: "— Choisir —" }, ...(lens?.indices ?? []).filter((i) => i.status === "active").map((i) => ({ value: i.id, label: i.name }))]} />
        )}
        {family?.variantAttributes.map((attribute) => (
          <Field key={attribute.key} label={attribute.label} name={`${prefix}attributes.${attribute.key}`} value={draft.attributes[attribute.key] ?? ""}
            inputMode={attribute.kind === "text" ? "text" : "decimal"} errors={errors}
            onChange={(v) => set({ attributes: { ...draft.attributes, [attribute.key]: v } })} />
        ))}
      </div>
      {family?.code === "ophthalmic_lenses" && (
        <fieldset className="cat-fieldset">
          <legend className="cat-label">Traitements inclus</legend>
          {(lens?.treatments ?? []).filter((t) => t.status === "active").map((t) => (
            <label key={t.id} className="cat-check">
              <input type="checkbox" checked={draft.treatmentIds.includes(t.id)}
                onChange={(e) => set({ treatmentIds: e.target.checked ? [...draft.treatmentIds, t.id] : draft.treatmentIds.filter((id) => id !== t.id) })} />
              {t.name}
            </label>
          ))}
        </fieldset>
      )}
    </>
  );
}

// ---------- fiche modèle ----------

export function ModelDetailView() {
  const { id = "" } = useParams();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const model = useCatalogQuery<ModelDetail>(store ? `/api/catalog/models/${id}${query({ storeId: store.id })}` : null);
  const families = useFamilies();
  const lens = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  const write = useCatalogWrite();
  const [adding, setAdding] = useState(false);
  const [newVariant, setNewVariant] = useState<VariantDraft>(emptyVariant("stocked"));
  const [archiving, setArchiving] = useState(false);
  const [reason, setReason] = useState("");

  if (model.isPending) return <Loading label="Chargement du produit…" />;
  if (model.isError) return <ErrorState error={model.error} onRetry={() => void model.refetch()} />;
  const data = model.data;
  const family = families.find((f) => f.code === data.familyCode);
  const selected = search.get("variante") ?? data.variants[0]?.row.variantId ?? null;

  return (
    <section aria-labelledby="model-title">
      <PageHeader
        title={`${data.brand ? `${data.brand} — ` : ""}${data.name}`}
        subtitle={<>{family?.label ?? data.familyCode} · {data.status === "archived" ? <Badge tone="neutral">Archivé</Badge> : <Badge tone="success">Actif</Badge>}</>}
        actions={
          <>
            <StoreSelect />
            <Button variant="tertiary" onClick={() => navigate("/produits")}><ArrowLeft size={16} aria-hidden="true" />Catalogue</Button>
            {access.catalogArchive && data.status === "active" && (
              <Button variant="danger" onClick={() => setArchiving(true)}><Archive size={16} aria-hidden="true" />Archiver le modèle</Button>
            )}
            {access.catalogArchive && data.status === "archived" && (
              <Button onClick={() => write.mutate({ method: "POST", path: `/api/catalog/models/${data.id}/reactivate`, body: { version: data.version } })}>
                <RotateCcw size={16} aria-hidden="true" />Réactiver
              </Button>
            )}
          </>
        }
      />
      <ErrorBanner error={write.error} />
      {data.description && <p className="cat-muted">{data.description}</p>}
      <div className="cat-split">
        <Panel title={`Variantes (${data.variants.length})`} actions={access.catalogWrite && data.status === "active" && (
          <Button size="sm" onClick={() => { setNewVariant(emptyVariant(family?.allowedPolicies[0] ?? "stocked")); setAdding(true); }}>
            <Plus size={14} aria-hidden="true" />Variante
          </Button>
        )}>
          <ul className="cat-list">
            {data.variants.map(({ row }) => (
              <li key={row.variantId}>
                <button type="button" className={`cat-list__item${row.variantId === selected ? " is-selected" : ""}`} onClick={() => setSearch({ variante: row.variantId })}>
                  <span className="cat-mono">{row.sku}</span>
                  <span>{row.label}</span>
                  <span className="cat-num">{formatMoney(row.price.salePriceHt, row.price.currency)}</span>
                  <Badge tone={AVAILABILITY_TONES[row.availability]}>{AVAILABILITY_LABELS[row.availability]}</Badge>
                </button>
              </li>
            ))}
          </ul>
        </Panel>
        {selected && <VariantPanel key={`${selected}-${store?.id}`} variantId={selected} family={family} lens={lens.data} />}
      </div>

      <Dialog title="Nouvelle variante" open={adding} onClose={() => setAdding(false)}>
        <ErrorBanner error={write.error} />
        <VariantFields family={family} lens={lens.data} draft={newVariant} onChange={setNewVariant} errors={write.error?.error.fieldErrors} withPolicy />
        <div className="cat-actions">
          <Button onClick={() => setAdding(false)}>Annuler</Button>
          <Button variant="primary" disabled={write.isPending}
            onClick={() => write.mutate({ method: "POST", path: `/api/catalog/models/${data.id}/variants`, body: variantBody(newVariant, true) }, { onSuccess: () => setAdding(false) })}>
            Enregistrer
          </Button>
        </div>
      </Dialog>
      <Dialog title="Archiver le modèle et ses variantes" open={archiving} onClose={() => setArchiving(false)}>
        <p>Aucune suppression : le modèle et ses variantes restent consultables dans l&apos;historique. Refusé si une variante a du stock, du réservé ou un inventaire ouvert.</p>
        <Field label="Motif (facultatif)" name="archiveReason" value={reason} onChange={setReason} multiline />
        <div className="cat-actions">
          <Button onClick={() => setArchiving(false)}>Annuler</Button>
          <Button variant="danger" disabled={write.isPending}
            onClick={() => write.mutate({ method: "POST", path: `/api/catalog/models/${data.id}/archive`, body: { version: data.version, reason: orNull(reason) } }, { onSuccess: () => setArchiving(false) })}>
            Archiver
          </Button>
        </div>
      </Dialog>
    </section>
  );
}

// ---------- fiche variante ----------

function VariantPanel({ variantId, family, lens }: { variantId: string; family: Family | undefined; lens: LensReferences | undefined }) {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const detail = useCatalogQuery<VariantDetail>(store ? `/api/catalog/variants/${variantId}${query({ storeId: store.id })}` : null);
  const write = useCatalogWrite();
  const [editing, setEditing] = useState<VariantDraft | null>(null);
  const [tab, setTab] = useState<"prix" | "fournisseurs" | "stock" | "historique">("prix");

  if (detail.isPending) return <Loading label="Chargement de la variante…" />;
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
  const { row } = detail.data;
  const archived = row.status === "archived";

  return (
    <Panel title={`${row.sku} — ${row.label}`} className="cat-detail" actions={
      <div className="cat-actions">
        {access.catalogWrite && !archived && (
          <Button size="sm" onClick={() => setEditing({
            sku: row.sku, label: row.label, manufacturerReference: row.manufacturerReference ?? "", barcode: row.barcode ?? "", stockPolicy: row.stockPolicy,
            lensIndexId: detail.data.lensIndexId ?? "", treatmentIds: detail.data.treatmentIds, attributes: detail.data.attributes,
          })}>Modifier</Button>
        )}
        {access.catalogArchive && (
          <Button size="sm" variant={archived ? "secondary" : "danger"} disabled={write.isPending}
            onClick={() => write.mutate({ method: "POST", path: `/api/catalog/variants/${variantId}/${archived ? "reactivate" : "archive"}`, body: { version: row.version } })}>
            {archived ? "Réactiver" : "Archiver"}
          </Button>
        )}
      </div>
    }>
      <ErrorBanner error={write.error} />
      <dl className="cat-facts">
        <div><dt>Politique</dt><dd>{POLICY_LABELS[row.stockPolicy]}</dd></div>
        <div><dt>Disponibilité</dt><dd><Badge tone={AVAILABILITY_TONES[row.availability]}>{AVAILABILITY_LABELS[row.availability]}</Badge></dd></div>
        <div><dt>Code-barres</dt><dd className="cat-mono">{row.barcode ?? "—"}</dd></div>
        <div><dt>Réf. fabricant</dt><dd>{row.manufacturerReference ?? "—"}</dd></div>
        <div><dt>Fournisseur principal</dt><dd>{row.primarySupplier ? `${row.primarySupplier.name}${row.primarySupplier.leadTimeDays !== null ? ` (${row.primarySupplier.leadTimeDays} j)` : ""}` : "—"}</dd></div>
        {Object.entries(detail.data.attributes).map(([key, value]) => (
          <div key={key}><dt>{family?.variantAttributes.find((a) => a.key === key)?.label ?? key}</dt><dd>{value}</dd></div>
        ))}
        {family?.code === "ophthalmic_lenses" && (
          <div><dt>Indice · traitements</dt><dd>{lens?.indices.find((i) => i.id === detail.data.lensIndexId)?.name ?? "—"} · {detail.data.treatmentIds.map((t) => lens?.treatments.find((x) => x.id === t)?.name ?? "?").join(", ") || "aucun"}</dd></div>
        )}
      </dl>
      {access.catalogWrite && !archived && <PolicyChange variantId={variantId} family={family} current={row.stockPolicy} version={row.version} />}

      <div className="cat-tabs" role="tablist">
        {(["prix", "fournisseurs", "stock", "historique"] as const).filter((t) => (t === "fournisseurs" ? access.suppliersRead : t === "stock" ? access.stockRead && row.stockPolicy === "stocked" : true)).map((t) => (
          <button key={t} type="button" role="tab" aria-selected={tab === t} className={`cat-tab${tab === t ? " is-active" : ""}`} onClick={() => setTab(t)}>
            {{ prix: "Prix", fournisseurs: "Fournisseurs", stock: "Stock", historique: "Historique des prix" }[t]}
          </button>
        ))}
      </div>
      {tab === "prix" && <PricingPanel detail={detail.data} />}
      {tab === "fournisseurs" && <SupplierLinksPanel detail={detail.data} />}
      {tab === "stock" && detail.data.balances && (
        <table className="cat-table">
          <thead><tr><th scope="col">Emplacement</th><th scope="col" className="cat-num">En main</th><th scope="col" className="cat-num">Réservé</th><th scope="col" className="cat-num">Disponible</th></tr></thead>
          <tbody>{detail.data.balances.map((b) => (
            <tr key={b.locationId}><td>{b.locationName}{b.isDefault ? " (défaut)" : ""}</td><td className="cat-num">{b.onHand}</td><td className="cat-num">{b.reserved}</td><td className="cat-num">{b.available}</td></tr>
          ))}</tbody>
        </table>
      )}
      {tab === "historique" && <PriceHistory variantId={variantId} />}

      <Dialog title="Modifier la variante" open={editing !== null} onClose={() => setEditing(null)}>
        <ErrorBanner error={write.error} />
        {editing && <VariantFields family={family} lens={lens} draft={editing} onChange={setEditing} errors={write.error?.error.fieldErrors} />}
        <div className="cat-actions">
          <Button onClick={() => setEditing(null)}>Annuler</Button>
          <Button variant="primary" disabled={write.isPending}
            onClick={() => editing && write.mutate({ method: "PUT", path: `/api/catalog/variants/${variantId}`, body: { version: row.version, ...variantBody(editing, false) } }, { onSuccess: () => setEditing(null) })}>
            Enregistrer
          </Button>
        </div>
      </Dialog>
    </Panel>
  );
}

function PolicyChange({ variantId, family, current, version }: { variantId: string; family: Family | undefined; current: string; version: number }) {
  const write = useCatalogWrite();
  const options = family?.allowedPolicies ?? [];
  if (options.length < 2) return null;
  return (
    <div className="cat-inline">
      <ErrorBanner error={write.error} />
      <label className="cat-label" htmlFor={`policy-${variantId}`}>Changer la politique</label>
      <select id={`policy-${variantId}`} className="cat-input w-auto" value={current} disabled={write.isPending}
        onChange={(e) => write.mutate({ method: "POST", path: `/api/catalog/variants/${variantId}/policy`, body: { version, stockPolicy: e.target.value } })}>
        {options.map((p) => <option key={p} value={p}>{POLICY_LABELS[p]}</option>)}
      </select>
      <span className="cat-muted">Refusé tant que du stock, du réservé ou un inventaire ouvert existent.</span>
    </div>
  );
}

// ---------- tarif ----------

function PricingPanel({ detail }: { detail: VariantDetail }) {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const price = detail.row.price;
  const write = useCatalogWrite();
  const preview = useCatalogWrite<{ computedSalePriceHt: string; belowCost: boolean; currency: string }>();
  const [purchase, setPurchase] = useState(price.purchasePriceHt ?? "");
  const [coefficient, setCoefficient] = useState(price.coefficient ?? "");
  const [manual, setManual] = useState("");
  const [reason, setReason] = useState("");
  const [acceptBelowCost, setAcceptBelowCost] = useState(false);
  // Aperçu valable seulement pour les valeurs aperçues : toute modification de saisie l'invalide (jamais d'écriture silencieuse).
  const [previewState, setPreviewState] = useState<{ key: string; value: string } | null>(null);
  const previewKey = `${purchase}|${coefficient}`;
  const previewed = previewState?.key === previewKey ? previewState.value : null;
  const setPreviewed = (value: string) => setPreviewState({ key: previewKey, value });
  if (!store) return null;
  const variantPath = `/api/pricing/variants/${detail.row.variantId}`;
  const base = { storeId: store.id, version: price.version };
  const fieldErrors = write.error?.error.fieldErrors ?? preview.error?.error.fieldErrors;

  return (
    <div className="cat-pricing">
      <dl className="cat-facts">
        <div><dt>Prix de vente HT</dt><dd className="cat-price">{formatMoney(price.salePriceHt, price.currency)}</dd></div>
        <div><dt>Mode</dt><dd>{price.pricingMode === "manual" ? "Prix manuel" : "Coefficient"}</dd></div>
        <div><dt>État</dt><dd><Badge tone={price.priceStatus === "up_to_date" ? "success" : "warning"}>{PRICE_STATUS_LABELS[price.priceStatus]}</Badge></dd></div>
        <div><dt>TTC</dt><dd>{price.salePriceTtc ? formatMoney(price.salePriceTtc, price.currency) : "TTC indisponible — fiscalité du magasin non validée"}</dd></div>
        {price.costsVisible && (
          <>
            <div><dt>Prix d&apos;achat HT</dt><dd>{formatMoney(price.purchasePriceHt ?? null, price.currency)}</dd></div>
            <div><dt>Coefficient</dt><dd>{price.coefficient ?? "—"}</dd></div>
            <div><dt>Coefficient effectif</dt><dd>{price.effectiveCoefficient ?? "—"}</dd></div>
            <div><dt>Marge HT</dt><dd>{formatMoney(price.margin ?? null, price.currency)}</dd></div>
            {price.priceStatus === "recalculation_pending" && <div><dt>Prix recalculé proposé</dt><dd>{formatMoney(price.computedSalePriceHt ?? null, price.currency)}</dd></div>}
          </>
        )}
      </dl>
      <ErrorBanner error={(write.error ?? preview.error) as ApiRequestError | null} />
      {access.pricingWrite && access.costsRead && detail.row.status === "active" && (
        <fieldset className="cat-fieldset">
          <legend className="cat-label">Prix par coefficient (calcul serveur, un seul arrondi au centime)</legend>
          <div className="cat-grid">
            <Field label={`Prix d'achat HT (${price.currency})`} name="purchasePriceHt" value={purchase} onChange={setPurchase} inputMode="decimal" errors={fieldErrors}
              disabled={!access.costsWrite} hint={access.costsWrite ? undefined : "Lecture seule : droit costs.write requis."} />
            <Field label="Coefficient" name="coefficient" value={coefficient} onChange={setCoefficient} inputMode="decimal" errors={fieldErrors} />
          </div>
          <div className="cat-actions">
            <Button disabled={preview.isPending}
              onClick={() => preview.mutate({ method: "POST", path: "/api/pricing/preview", body: { storeId: store.id, purchasePriceHt: decimalInput(purchase), coefficient: decimalInput(coefficient) } },
                { onSuccess: (r) => { setPreviewed(r.computedSalePriceHt); setAcceptBelowCost(false); } })}>
              Aperçu
            </Button>
            {previewed && <span>Prix calculé : <strong>{formatMoney(previewed, price.currency)}</strong></span>}
            {previewed && preview.data?.belowCost && (
              <label className="cat-check"><input type="checkbox" checked={acceptBelowCost} onChange={(e) => setAcceptBelowCost(e.target.checked)} />Je confirme une vente sous le prix d&apos;achat</label>
            )}
            <Button variant="primary" disabled={!previewed || write.isPending}
              onClick={() => write.mutate({ method: "PUT", path: variantPath, body: {
                ...base, purchasePriceHt: access.costsWrite ? decimalInput(purchase) : undefined, coefficient: decimalInput(coefficient), expectedSalePriceHt: previewed, acceptBelowCost,
              } })}>
              Enregistrer ce prix
            </Button>
            {price.priceStatus === "recalculation_pending" && price.computedSalePriceHt && (
              <Button onClick={() => write.mutate({ method: "POST", path: `${variantPath}/recalculate`, body: { ...base, expectedSalePriceHt: price.computedSalePriceHt } })}>
                Appliquer le recalcul ({formatMoney(price.computedSalePriceHt, price.currency)})
              </Button>
            )}
          </div>
        </fieldset>
      )}
      {access.costsWrite && !access.pricingWrite && detail.row.status === "active" && (
        <fieldset className="cat-fieldset">
          <legend className="cat-label">Prix d&apos;achat HT seul (le prix de vente ne change pas)</legend>
          <Field label={`Prix d'achat HT (${price.currency})`} name="purchaseOnly" value={purchase} onChange={setPurchase} inputMode="decimal" errors={fieldErrors} />
          <Button onClick={() => write.mutate({ method: "PUT", path: `${variantPath}/purchase-price`, body: { ...base, purchasePriceHt: decimalInput(purchase) } })}>Enregistrer le prix d&apos;achat</Button>
        </fieldset>
      )}
      {access.pricingManual && detail.row.status === "active" && (
        <fieldset className="cat-fieldset">
          <legend className="cat-label">Prix manuel (le coefficient enregistré est conservé, non appliqué)</legend>
          <div className="cat-grid">
            <Field label={`Prix de vente HT (${price.currency})`} name="salePriceHt" value={manual} onChange={setManual} inputMode="decimal" errors={fieldErrors} />
            <Field label="Motif (facultatif)" name="reason" value={reason} onChange={setReason} />
          </div>
          <Button onClick={() => write.mutate({ method: "PUT", path: `${variantPath}/manual-price`, body: { ...base, salePriceHt: decimalInput(manual), reason: orNull(reason) } })}>
            Appliquer le prix manuel
          </Button>
        </fieldset>
      )}
    </div>
  );
}

function PriceHistory({ variantId }: { variantId: string }) {
  const { store } = useActiveStore();
  const history = useCatalogQuery<PriceHistoryEntry[]>(store ? `/api/pricing/variants/${variantId}/history${query({ storeId: store.id })}` : null);
  if (history.isPending) return <Loading label="Chargement de l'historique…" />;
  if (history.isError) return <ErrorState error={history.error} />;
  const rows = Array.isArray(history.data) ? history.data : [];
  if (rows.length === 0) return <p className="cat-muted">Aucune modification de prix.</p>;
  const labels: Record<string, string> = { purchase_price_ht: "Prix d'achat HT", coefficient: "Coefficient", pricing_mode: "Mode", sale_price_ht: "Prix de vente HT", supplier_price_ht: "Prix fournisseur HT" };
  const origins: Record<string, string> = { edit: "Saisie", recalculation: "Recalcul", manual_price: "Prix manuel", import: "Import", receipt: "Réception" };
  return (
    <table className="cat-table">
      <thead><tr><th scope="col">Date</th><th scope="col">Champ</th><th scope="col">Ancien</th><th scope="col">Nouveau</th><th scope="col">Origine</th><th scope="col">Auteur</th></tr></thead>
      <tbody>{rows.map((h) => (
        <tr key={h.id}>
          <td>{formatDateTime(h.changedAtUtc)}</td>
          <td>{labels[h.field] ?? h.field}</td>
          <td>{h.field === "pricing_mode" ? h.oldMode ?? "—" : h.field === "coefficient" ? h.oldValue ?? "—" : formatMoney(h.oldValue, h.currency)}</td>
          <td>{h.field === "pricing_mode" ? h.newMode ?? "—" : h.field === "coefficient" ? h.newValue ?? "—" : formatMoney(h.newValue, h.currency)}</td>
          <td>{origins[h.origin] ?? h.origin}{h.reason ? ` — ${h.reason}` : ""}</td>
          <td>{h.changedBy.displayName}</td>
        </tr>
      ))}</tbody>
    </table>
  );
}

// ---------- fournisseurs d'une variante ----------

function SupplierLinksPanel({ detail }: { detail: VariantDetail }) {
  const access = useCatalogAccess();
  const suppliers = useCatalogQuery<Page<SupplierRow>>("/api/suppliers?pageSize=200");
  const write = useCatalogWrite();
  const [supplierId, setSupplierId] = useState("");
  const [reference, setReference] = useState("");
  const [primary, setPrimary] = useState(false);
  const [priceHt, setPriceHt] = useState("");
  const [currency, setCurrency] = useState(detail.row.price.currency);
  const links = (detail.suppliers ?? []).filter((l) => !l.removed);
  const path = (id: string) => `/api/catalog/variants/${detail.row.variantId}/suppliers/${id}`;

  return (
    <div>
      {detail.row.isIncomplete && <p className="cat-alert" role="status">Fiche incomplète : aucun fournisseur principal actif. La commande fournisseur effective l&apos;exigera.</p>}
      <ErrorBanner error={write.error} />
      <table className="cat-table">
        <thead><tr><th scope="col">Fournisseur</th><th scope="col">Référence</th><th scope="col">Délai</th>{detail.costsVisible && <th scope="col" className="cat-num">Prix fournisseur HT</th>}<th scope="col">Rôle</th><th scope="col" /></tr></thead>
        <tbody>{links.length === 0 ? <tr><td colSpan={6} className="cat-muted">Aucun fournisseur rattaché.</td></tr> : links.map((l) => (
          <tr key={l.supplierId}>
            <td>{l.name} <span className="cat-muted">({l.code}){l.supplierStatus === "archived" ? " · archivé" : ""}</span></td>
            <td>{l.supplierReference ?? "—"}</td>
            <td>{l.leadTimeDays !== null ? `${l.leadTimeDays} j` : "—"}</td>
            {detail.costsVisible && <td className="cat-num">{l.supplierPriceHt && l.supplierCurrency ? formatMoney(l.supplierPriceHt, l.supplierCurrency) : "—"}</td>}
            <td>{l.isPrimary ? <Badge tone="info">Principal</Badge> : access.catalogWrite && l.supplierStatus === "active" ? (
              <Button size="sm" variant="tertiary" onClick={() => write.mutate({ method: "PUT", path: path(l.supplierId), body: { version: l.version, isPrimary: true, supplierReference: l.supplierReference, leadTimeDays: l.leadTimeDays } })}>Rendre principal</Button>
            ) : null}</td>
            <td>{access.catalogWrite && <Button size="sm" variant="danger" onClick={() => write.mutate({ method: "POST", path: `${path(l.supplierId)}/remove`, body: { version: l.version } })}>Retirer</Button>}</td>
          </tr>
        ))}</tbody>
      </table>
      {access.catalogWrite && detail.row.status === "active" && (
        <fieldset className="cat-fieldset">
          <legend className="cat-label">Rattacher un fournisseur</legend>
          <div className="cat-grid">
            <SelectField label="Fournisseur" name="supplierId" value={supplierId} onChange={setSupplierId}
              options={[{ value: "", label: "— Choisir —" }, ...(suppliers.data?.items ?? []).filter((s) => s.status === "active").map((s) => ({ value: s.id, label: `${s.name} (${s.code})` }))]} />
            <Field label="Référence fournisseur" name="supplierReference" value={reference} onChange={setReference} errors={write.error?.error.fieldErrors} />
            {access.costsWrite && <Field label="Prix fournisseur HT" name="supplierPriceHt" value={priceHt} onChange={setPriceHt} inputMode="decimal" errors={write.error?.error.fieldErrors} hint="Sans conversion ; ne modifie pas le prix d'achat de référence." />}
            {access.costsWrite && <Field label="Devise du prix fournisseur" name="supplierCurrency" value={currency} onChange={setCurrency} errors={write.error?.error.fieldErrors} />}
          </div>
          <label className="cat-check"><input type="checkbox" checked={primary} onChange={(e) => setPrimary(e.target.checked)} />Fournisseur principal</label>
          <Button disabled={supplierId === "" || write.isPending}
            onClick={() => write.mutate({ method: "PUT", path: path(supplierId), body: {
              supplierReference: orNull(reference), isPrimary: primary,
              ...(access.costsWrite && priceHt.trim() !== "" ? { supplierPriceHt: decimalInput(priceHt), supplierCurrency: currency.trim().toUpperCase() } : {}),
            } })}>
            Rattacher
          </Button>
        </fieldset>
      )}
    </div>
  );
}
