// Produits — Lot 6 groupes 05–13 (nouveau produit, erreurs, modification, non enregistré, fermeture, succès, conflit,
// désactivé, détail, blocs de la fiche, stock faible / rupture / lecture seule, menu d'actions, désactivation) traduits
// sur les règles approuvées : MODÈLE + VARIANTES (SKU, politique, prix et stock par variante), SKU interne GÉNÉRÉ
// automatiquement (correctif Q05), archivage au lieu de la suppression physique, plusieurs fournisseurs par variante.
// Bloc « Prix & disponibilité » : prix d'achat HT, coefficient, aperçu SERVEUR du prix de vente HT, prix validé, mode
// coefficient / manuel, journal — recalcul jamais silencieux, coûts et prix manuel sous permission, devise du magasin
// actif, aucun TTC inventé.
import {
  Archive, Bell, Boxes, Building2, Calculator, Coins, EllipsisVertical, History, Info, Layers, Lock as LockIcon, PackageOpen, Pencil, Plus,
  RotateCcw, Save, Sparkles, Star, TrendingUp, Truck, X,
} from "lucide-react";
import { useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router";
import type { Family, LensReferences, Movement, ModelDetail, Page, PriceHistoryEntry, VariantDetail } from "../api/catalog.ts";
import { query } from "../api/catalog.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { Button } from "../components/ui/button.tsx";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "../components/ui/dropdown-menu.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import {
  AVAILABILITY_LABELS, decimalInput, fieldError, formatDateTime, formatDecimal, formatMoney, MOVEMENT_LABELS, POLICY_LABELS, PRICE_STATUS_LABELS,
  reasonLabel,
} from "./catalogFormat.ts";
import { availabilityBadge, stockTone } from "./catalogStatus.ts";
import {
  Badge, Banner, Card, Diff, ErrorBanner, ErrorState, FamilyTag, Field, FormBar, FormSection, KeyValues, Loading, Lock, Modal, PageHeader, RefreshBar,
  ClipText, SelectField, StockCell, TableScroll,
} from "./CatalogUi.tsx";
import { errorMessage, useActiveStore, useCatalogPreview, useCatalogQuery, useCatalogWrite, useEditBase, useToast } from "./catalogData.ts";
import { type SupplierChoice, SupplierCombobox } from "./SupplierCombobox.tsx";
import { VariantThresholdModal } from "./StockViews.tsx";
import { emptyVariant, orNull, skuPrefix, useFamilies, useUnsavedGuard, type VariantDraft, variantBody } from "./productForm.ts";

export function UnsavedModal({ open, onLeave, onStay, onSave, saving }: { open: boolean; onLeave: () => void; onStay: () => void; onSave: () => void; saving: boolean }) {
  return (
    <Modal open={open} onClose={onStay} tone="warn" icon={Info} title="Modifications non enregistrées"
      description="Vous avez des modifications non enregistrées. Si vous fermez maintenant, elles seront perdues."
      actions={<>
        <Button variant="ghost" onClick={onLeave}>Fermer sans enregistrer</Button>
        <Button onClick={onStay}>Continuer l&apos;édition</Button>
        <Button variant="primary" disabled={saving} onClick={onSave}><Save size={16} aria-hidden="true" />Enregistrer</Button>
      </>} />
  );
}

export function VariantFields({ family, familyCode, lens, draft, onChange, errors, prefix = "", withPolicy, skuError, canAutoSku = true }: {
  /** Faux pour une variante existante : son SKU est déjà attribué (modifiable, jamais régénéré en silence). */
  canAutoSku?: boolean;
  family: Family | undefined;
  familyCode: string;
  lens: LensReferences | undefined;
  draft: VariantDraft;
  onChange: (draft: VariantDraft) => void;
  errors?: Record<string, string[]>;
  prefix?: string;
  withPolicy?: boolean;
  skuError?: string;
}) {
  const set = (patch: Partial<VariantDraft>) => onChange({ ...draft, ...patch });
  return (
    <div className="cat-grid">
      <Field label="Libellé de la variante" name={`${prefix}label`} value={draft.label} onChange={(label) => set({ label })} required errors={errors} placeholder="Ex. coloris, calibre, conditionnement" />
      <Field label="SKU interne" name={`${prefix}sku`} value={draft.sku} onChange={(sku) => set({ sku, skuCustom: true })} errors={errors} readOnly={!draft.skuCustom} error={skuError}
        placeholder={draft.skuCustom ? undefined : `${skuPrefix(familyCode)}-······ (généré à l'enregistrement)`}
        suffix={draft.skuCustom
          ? (canAutoSku && <button type="button" className="cat-input-action" onClick={() => set({ sku: "", skuCustom: false })}>Génération auto</button>)
          : <button type="button" className="cat-input-action" onClick={() => set({ skuCustom: true })}>Personnaliser</button>}
        hint={draft.skuCustom ? "Personnalisé : unique dans toute l'installation (contrôlé par le serveur)." : "Généré et garanti unique par le serveur MMV ; aucune saisie requise."} />
      {withPolicy && family ? (
        <SelectField label="Politique de stock" name={`${prefix}stockPolicy`} value={draft.stockPolicy} onChange={(v) => set({ stockPolicy: v })} required errors={errors}
          options={family.allowedPolicies.map((p) => ({ value: p, label: POLICY_LABELS[p] }))} />
      ) : null}
      <Field label="Code-barres (EAN / GTIN)" name={`${prefix}barcode`} value={draft.barcode} onChange={(v) => set({ barcode: v })} errors={errors} inputMode="numeric" hint="Facultatif ; clé de contrôle vérifiée." />
      <Field label="Référence fabricant" name={`${prefix}manufacturerReference`} value={draft.manufacturerReference} onChange={(v) => set({ manufacturerReference: v })} errors={errors} />
      {family?.code === "ophthalmic_lenses" && (
        <SelectField label="Indice" name={`${prefix}lensIndexId`} value={draft.lensIndexId} onChange={(v) => set({ lensIndexId: v })} required errors={errors}
          options={[{ value: "", label: "— Choisir —" }, ...(lens?.indices ?? []).filter((i) => i.status === "active").map((i) => ({ value: i.id, label: i.name }))]} />
      )}
      {family?.variantAttributes.map((attribute) => (
        <Field key={attribute.key} label={attribute.label} name={`${prefix}attributes.${attribute.key}`} value={draft.attributes[attribute.key] ?? ""}
          inputMode={attribute.kind === "text" ? "text" : "decimal"} errors={errors}
          onChange={(v) => set({ attributes: { ...draft.attributes, [attribute.key]: v } })} />
      ))}
      {family?.code === "ophthalmic_lenses" && (
        <fieldset className="clients-field cat-span2" style={{ border: 0, margin: 0, padding: 0 }}>
          <legend className="clients-field__label" style={{ marginBottom: 7 }}>Traitements inclus</legend>
          <div className="cat-checks">
            {(lens?.treatments ?? []).filter((t) => t.status === "active").map((t) => (
              <label key={t.id} className="cat-check">
                <input type="checkbox" checked={draft.treatmentIds.includes(t.id)}
                  onChange={(e) => set({ treatmentIds: e.target.checked ? [...draft.treatmentIds, t.id] : draft.treatmentIds.filter((id) => id !== t.id) })} />
                {t.name}
              </label>
            ))}
            {(lens?.treatments ?? []).filter((t) => t.status === "active").length === 0 && <span className="clients-muted">Aucun traitement configuré dans le référentiel.</span>}
          </div>
        </fieldset>
      )}
    </div>
  );
}

// ---------- modification du modèle (prodEdit, prodConflict, prodDeactivated, prodEditOk) ----------

export function ModelEditView() {
  const { id = "" } = useParams();
  const { store } = useActiveStore();
  const model = useCatalogQuery<ModelDetail>(store ? `/api/catalog/models/${id}${query({ storeId: store.id })}` : null);
  if (model.isPending) return <Loading label="Chargement du produit…" />;
  if (model.isError) return <ErrorState error={model.error} title="Impossible de charger le produit" onRetry={() => void model.refetch()} />;
  // Clé = identifiant seul : une actualisation (temps réel) ne remonte jamais le formulaire en cours de saisie.
  return <ModelEditForm key={model.data.id} model={model.data} onReload={() => void model.refetch()} />;
}

function ModelEditForm({ model, onReload }: { model: ModelDetail; onReload: () => void }) {
  const navigate = useNavigate();
  const toast = useToast();
  const access = useCatalogAccess();
  const families = useFamilies();
  const lens = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  const write = useCatalogWrite();
  const reactivate = useCatalogWrite();
  const family = families.find((f) => f.code === model.familyCode);
  const current = { brand: model.brand ?? "", name: model.name, description: model.description ?? "", lensRangeId: model.lensRangeId ?? "", attributes: model.attributes };
  const [draft, setDraft] = useState(current);
  const base = useEditBase(model.version, current, setDraft);
  const dirty = JSON.stringify(draft) !== JSON.stringify(base.initial);
  const guard = useUnsavedGuard(dirty && !write.isSuccess);
  const errors = write.error?.error.fieldErrors;
  const conflict = write.error?.error.code === "version_conflict" || base.remoteChanged;
  // Fiche modifiée ailleurs alors que rien n'a été saisi ici : rien à préserver, la dernière version est reprise.
  if (base.remoteChanged && !dirty) base.rebase();
  const reload = () => {
    if (base.remoteChanged) {
      base.rebase();
      write.reset();
    } else {
      onReload();
    }
  };
  const archived = model.status === "archived";
  const back = `/produits/modele/${model.id}`;

  const save = () => write.mutate(
    { method: "PUT", path: `/api/catalog/models/${model.id}`, body: {
      version: base.version, brand: orNull(draft.brand), name: draft.name, description: orNull(draft.description), lensRangeId: orNull(draft.lensRangeId), attributes: draft.attributes,
    } },
    { onSuccess: () => { toast.show("Modifications enregistrées", `${draft.name} a été mis à jour.`); navigate(back); } },
  );

  return (
    <section className="cat-screen cat-form" aria-labelledby="edit-model">
      <PageHeader title={`Modifier — ${model.name}`} subtitle={`${family?.label ?? model.familyCode} · ${model.variants.length} variante${model.variants.length > 1 ? "s" : ""}`}
        actions={<Button variant="ghost" onClick={() => guard.ask(() => navigate(back))}><X size={18} aria-hidden="true" />Fermer</Button>} />
      <div className="cat-form__body">
        {conflict && (
          <Banner tone="warn" icon={Info} title="Ce produit a été modifié sur un autre poste" role="alert"
            action={<Button size="sm" className="cat-btn-warn" onClick={reload}><RotateCcw size={16} aria-hidden="true" />{base.remoteChanged ? "Intégrer la dernière version" : "Recharger"}</Button>}>
            {base.remoteChanged
              ? "Une autre modification a été enregistrée pendant votre saisie. Intégrez-la : les champs que vous n'avez pas modifiés reprennent la valeur enregistrée, vos saisies sont conservées ; vérifiez puis enregistrez."
              : "Rechargez pour repartir de la version enregistrée sur le serveur ; vos saisies sont conservées dans le formulaire."}
          </Banner>
        )}
        {!conflict && write.error && <ErrorBanner error={write.error} />}
        {archived && (
          <Banner tone="neutral" icon={Archive} title="Produit archivé"
            action={access.catalogArchive && (
              <Button size="sm" disabled={reactivate.isPending} onClick={() => reactivate.mutate({ method: "POST", path: `/api/catalog/models/${model.id}/reactivate`, body: { version: model.version } },
                { onSuccess: () => toast.show("Produit réactivé") })}>
                <RotateCcw size={16} aria-hidden="true" />Réactiver
              </Button>
            )}>
            Il n&apos;apparaît plus au catalogue de vente ; son historique est conservé. Aucune modification n&apos;est possible tant qu&apos;il est archivé.
          </Banner>
        )}
        <FormSection title="Informations générales" icon={Info}>
          <Field label="Famille" name="familyReadonly" value={family?.label ?? model.familyCode} onChange={() => undefined} readOnly hint="La famille d'un modèle ne change pas." />
          <Field label="Nom du modèle" name="name" value={draft.name} onChange={(name) => setDraft({ ...draft, name })} required errors={errors} disabled={archived} />
          <Field label="Marque" name="brand" value={draft.brand} onChange={(brand) => setDraft({ ...draft, brand })} errors={errors} disabled={archived} />
          {model.familyCode === "ophthalmic_lenses" ? (
            <SelectField label="Gamme de verres" name="lensRangeId" value={draft.lensRangeId} required errors={errors} disabled={archived} onChange={(lensRangeId) => setDraft({ ...draft, lensRangeId })}
              options={[{ value: "", label: "— Choisir une gamme —" }, ...(lens.data?.ranges ?? []).map((r) => ({ value: r.id, label: `${lens.data?.manufacturers.find((m) => m.id === r.manufacturerId)?.name ?? "?"} — ${r.name}` }))]} />
          ) : <span aria-hidden="true" />}
          <Field label="Description" name="description" value={draft.description} onChange={(description) => setDraft({ ...draft, description })} errors={errors} multiline span2 disabled={archived} />
        </FormSection>
        {family && family.modelAttributes.length > 0 && (
          <FormSection title={`Détails — ${family.label}`} icon={Sparkles}>
            {family.modelAttributes.map((attribute) => (
              <Field key={attribute.key} label={attribute.label} name={`attributes.${attribute.key}`} value={draft.attributes[attribute.key] ?? ""} errors={errors} disabled={archived}
                onChange={(v) => setDraft({ ...draft, attributes: { ...draft.attributes, [attribute.key]: v } })} />
            ))}
          </FormSection>
        )}
        <Banner tone="info" icon={Layers} title="Variantes, prix et stock" compact>
          SKU, libellés, politique de stock, prix par magasin et fournisseurs se modifient variante par variante dans la fiche produit.
        </Banner>
      </div>
      <FormBar dirty={dirty} hint="Les champs marqués * sont obligatoires">
        <Button onClick={() => guard.ask(() => navigate(back))}>Annuler</Button>
        <Button variant="primary" disabled={archived || !dirty || write.isPending} onClick={save}>
          <Save size={16} aria-hidden="true" />{write.isPending ? "Enregistrement…" : "Enregistrer les modifications"}
        </Button>
      </FormBar>
      <UnsavedModal open={guard.pending !== null} onLeave={guard.confirm} onStay={guard.cancel} onSave={() => { guard.cancel(); save(); }} saving={write.isPending} />
    </section>
  );
}

// ---------- fiche produit (detLight, detGeneral, detHistory, detLow, detRupture, detDeactivated, detVendeur, delMenu) ----------

export function ModelDetailView() {
  const { id = "" } = useParams();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const model = useCatalogQuery<ModelDetail>(store ? `/api/catalog/models/${id}${query({ storeId: store.id })}` : null);
  const families = useFamilies();
  const lens = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  const write = useCatalogWrite();
  const [adding, setAdding] = useState<VariantDraft | null>(null);
  const [archiving, setArchiving] = useState(false);
  const [reason, setReason] = useState("");

  if (model.isPending) return <Loading label="Chargement du produit…" />;
  if (model.isError) return <ErrorState error={model.error} title="Impossible de charger le produit" onRetry={() => void model.refetch()} />;
  const data = model.data;
  const family = families.find((f) => f.code === data.familyCode);
  const selected = search.get("variante") ?? data.variants[0]?.row.variantId ?? null;
  const archived = data.status === "archived";
  const currency = store?.currency ?? "";

  return (
    <section className="cat-screen" aria-labelledby="model-title">
      <PageHeader
        back={{ label: "Produits", onClick: () => navigate("/produits") }}
        title={data.name}
        titleExtra={<>
          <FamilyTag code={data.familyCode} label={family?.label} />
          {archived ? <Badge tone="arch" icon={Archive}>Archivé</Badge> : <Badge tone="ok">Actif</Badge>}
        </>}
        subtitle={[data.brand, `${data.variants.length} variante${data.variants.length > 1 ? "s" : ""}`, store ? `prix en ${currency}` : null].filter(Boolean).join(" · ")}
        actions={access.catalogWrite || access.catalogArchive ? (
          <>
            {access.catalogWrite && !archived && (
              <Button onClick={() => navigate(`/produits/modele/${data.id}/modifier`)}><Pencil size={16} aria-hidden="true" />Modifier</Button>
            )}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button type="button" className="cat-ract cat-ract--boxed" aria-label="Autres actions"><EllipsisVertical size={18} aria-hidden="true" /></button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {access.catalogWrite && !archived && (
                  <DropdownMenuItem onSelect={() => setAdding(emptyVariant(family?.allowedPolicies[0] ?? "stocked"))}><Plus size={16} aria-hidden="true" />Ajouter une variante</DropdownMenuItem>
                )}
                {access.stockRead && <DropdownMenuItem onSelect={() => navigate("/inventaire")}><Boxes size={16} aria-hidden="true" />Voir le stock</DropdownMenuItem>}
                {access.catalogArchive && !archived && (
                  <DropdownMenuItem variant="destructive" className="cat-menu-danger" onSelect={() => { setReason(""); write.reset(); setArchiving(true); }}><Archive size={16} aria-hidden="true" />Archiver le modèle</DropdownMenuItem>
                )}
                {access.catalogArchive && archived && (
                  <DropdownMenuItem onSelect={() => write.mutate({ method: "POST", path: `/api/catalog/models/${data.id}/reactivate`, body: { version: data.version } }, { onSuccess: () => toast.show("Produit réactivé") })}>
                    <RotateCcw size={16} aria-hidden="true" />Réactiver le modèle
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </>
        ) : <Lock large>Lecture seule</Lock>}
      />
      {!archiving && <ErrorBanner error={write.error} onReload={() => void model.refetch()} />}
      {archived && (
        <Banner tone="neutral" icon={Archive} title="Produit archivé">
          Il n&apos;apparaît plus au catalogue de vente (aucune suppression physique) ; ses variantes, prix et mouvements restent consultables.{data.archiveReason ? ` Motif : ${data.archiveReason}.` : ""}
        </Banner>
      )}
      <RefreshBar asOf={new Date(model.dataUpdatedAt).toISOString()} onRefresh={() => void model.refetch()} refreshing={model.isFetching} />

      <div className="cat-detail">
        <div className="cat-stack">
          <Card title={`Variantes (${data.variants.length})`} icon={Layers} flush
            actions={access.catalogWrite && !archived && (
              <Button size="sm" onClick={() => setAdding(emptyVariant(family?.allowedPolicies[0] ?? "stocked"))}><Plus size={16} aria-hidden="true" />Variante</Button>
            )}>
            <TableScroll label="Variantes">
            <table className="clients-table cat-table cat-table--inner">
              <thead>
                <tr>
                  <th scope="col">Référence</th>
                  <th scope="col">Variante</th>
                  <th scope="col" className="cat-hide-sm">Politique</th>
                  <th scope="col" className="cat-num">Prix de vente HT</th>
                  {access.stockRead && <th scope="col" className="cat-num">Stock</th>}
                  <th scope="col">Statut</th>
                </tr>
              </thead>
              <tbody>
                {data.variants.map(({ row }) => {
                  const badge = availabilityBadge(row.availability);
                  const isSelected = row.variantId === selected;
                  return (
                    <tr key={row.variantId} className={isSelected ? "is-selected" : row.status === "archived" ? "is-archived" : undefined} data-href="variante" tabIndex={0} aria-selected={isSelected}
                      onClick={() => setSearch({ variante: row.variantId })} onKeyDown={(e) => e.key === "Enter" && setSearch({ variante: row.variantId })}>
                      <td><span className="cat-ref">{row.sku}</span></td>
                      <td className="cat-strong">{row.label}</td>
                      <td className="cat-secondary cat-hide-sm">{POLICY_LABELS[row.stockPolicy]}</td>
                      <td className="cat-num cat-strong">{formatMoney(row.price.salePriceHt, row.price.currency)}</td>
                      {access.stockRead && <td className="cat-num">{row.stock ? <StockCell value={row.stock.available} tone={stockTone(row.availability)} /> : <span className="clients-muted">—</span>}</td>}
                      <td><Badge tone={badge.tone} icon={badge.icon}>{AVAILABILITY_LABELS[row.availability]}</Badge></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            </TableScroll>
          </Card>
          {selected && <VariantMain key={`${selected}-${store?.id}`} variantId={selected} family={family} lens={lens.data} />}
          <Card title={`Détails — ${family?.label ?? data.familyCode}`} icon={Sparkles}>
            <KeyValues rows={[
              ["Marque", data.brand ?? "—"],
              ...(family?.modelAttributes ?? []).map((a): [string, string] => [a.label, data.attributes[a.key] ?? "—"]),
              ...(data.familyCode === "ophthalmic_lenses" ? [["Gamme", lens.data?.ranges.find((r) => r.id === data.lensRangeId)?.name ?? "—"] as [string, string]] : []),
              ["Description", <span key="d" className="cat-secondary" style={{ fontWeight: 500 }}>{data.description ?? "—"}</span>],
            ]} />
          </Card>
        </div>
        <div className="cat-stack">
          {selected && <VariantSide key={`${selected}-${store?.id}-side`} variantId={selected} />}
        </div>
      </div>

      <Modal open={adding !== null} onClose={() => setAdding(null)} tone="accent" icon={Plus} title="Nouvelle variante" size="wide"
        description="SKU généré automatiquement ; prix et stock se règlent ensuite par magasin."
        actions={<>
          <Button onClick={() => setAdding(null)}>Annuler</Button>
          <Button variant="primary" disabled={write.isPending || !adding}
            onClick={() => adding && write.mutate({ method: "POST", path: `/api/catalog/models/${data.id}/variants`, body: variantBody(adding, true) },
              { onSuccess: () => { toast.show("Variante ajoutée", adding.label); setAdding(null); } })}>
            <Save size={16} aria-hidden="true" />Créer la variante
          </Button>
        </>}>
        <ErrorBanner error={write.error} />
        {adding && <VariantFields family={family} familyCode={data.familyCode} lens={lens.data} draft={adding} onChange={setAdding} errors={write.error?.error.fieldErrors} withPolicy />}
      </Modal>
      <Modal open={archiving} onClose={() => setArchiving(false)} tone="err" icon={Archive} title="Archiver le modèle et ses variantes ?"
        description="Aucune suppression : le modèle, ses variantes, prix et mouvements restent consultables dans l'historique. Refusé si une variante a du stock, du réservé ou un inventaire ouvert."
        actions={<>
          <Button onClick={() => setArchiving(false)}>Annuler</Button>
          <Button className="cat-btn-danger-solid" disabled={write.isPending}
            onClick={() => write.mutate({ method: "POST", path: `/api/catalog/models/${data.id}/archive`, body: { version: data.version, reason: orNull(reason) } },
              { onSuccess: () => { setArchiving(false); toast.show("Produit archivé", "Il n'apparaît plus au catalogue de vente."); } })}>
            <Archive size={16} aria-hidden="true" />Archiver
          </Button>
        </>}>
        {write.error && (
          <Banner tone="err" icon={Info} title={write.error.error.code === "archive_refused" ? "Archivage refusé" : "Opération refusée"} role="alert" compact>
            {errorMessage(write.error.error)}
          </Banner>
        )}
        <Field label="Motif (facultatif)" name="archiveReason" value={reason} onChange={setReason} multiline />
      </Modal>
    </section>
  );
}

// ---------- colonne principale : informations de la variante, historiques ----------

function useVariant(variantId: string) {
  const { store } = useActiveStore();
  return useCatalogQuery<VariantDetail>(store ? `/api/catalog/variants/${variantId}${query({ storeId: store.id })}` : null);
}

function VariantMain({ variantId, family, lens }: { variantId: string; family: Family | undefined; lens: LensReferences | undefined }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const detail = useVariant(variantId);
  const write = useCatalogWrite();
  const [editing, setEditing] = useState<VariantDraft | null>(null);
  // Version figée à l'ouverture de la modale : une actualisation venue d'un autre poste ne la remplace pas (COR-05-02).
  const [editingVersion, setEditingVersion] = useState(0);
  const [archiving, setArchiving] = useState(false);

  if (detail.isPending) return <Loading label="Chargement de la variante…" />;
  if (detail.isError) return <ErrorState error={detail.error} title="Impossible de charger la variante" onRetry={() => void detail.refetch()} />;
  const { row } = detail.data;
  const archived = row.status === "archived";
  const policies = family?.allowedPolicies ?? [];

  return (
    <>
      <Card title={<>Informations — <span className="cat-ref">{row.sku}</span></>} icon={Info}
        actions={(access.catalogWrite || access.catalogArchive) && (
          <div className="clients-actions">
            {access.catalogWrite && !archived && (
              <Button size="sm" onClick={() => { setEditingVersion(row.version); setEditing({
                sku: row.sku, skuCustom: true, label: row.label, manufacturerReference: row.manufacturerReference ?? "", barcode: row.barcode ?? "", stockPolicy: row.stockPolicy,
                lensIndexId: detail.data.lensIndexId ?? "", treatmentIds: detail.data.treatmentIds, attributes: detail.data.attributes,
              }); }}><Pencil size={14} aria-hidden="true" />Modifier la variante</Button>
            )}
            {access.catalogArchive && (archived ? (
              <Button size="sm" disabled={write.isPending} onClick={() => write.mutate({ method: "POST", path: `/api/catalog/variants/${variantId}/reactivate`, body: { version: row.version } },
                { onSuccess: () => toast.show("Variante réactivée", row.sku) })}>
                <RotateCcw size={14} aria-hidden="true" />Réactiver
              </Button>
            ) : (
              <Button size="sm" variant="danger" onClick={() => { write.reset(); setArchiving(true); }}><Archive size={14} aria-hidden="true" />Archiver</Button>
            ))}
          </div>
        )}>
        {!archiving && <ErrorBanner error={write.error} onReload={() => void detail.refetch()} />}
        <KeyValues rows={[
          ["Référence (SKU)", <span key="s" className="cat-ref">{row.sku}</span>],
          ["Libellé", row.label],
          ["Politique de stock", access.catalogWrite && !archived && policies.length > 1 ? (
            <select key="p" className="cat-inline-select" aria-label="Changer la politique de stock" value={row.stockPolicy} disabled={write.isPending}
              onChange={(e) => write.mutate({ method: "POST", path: `/api/catalog/variants/${variantId}/policy`, body: { version: row.version, stockPolicy: e.target.value } },
                { onSuccess: () => toast.show("Politique de stock modifiée", POLICY_LABELS[e.target.value as keyof typeof POLICY_LABELS]) })}>
              {policies.map((p) => <option key={p} value={p}>{POLICY_LABELS[p]}</option>)}
            </select>
          ) : POLICY_LABELS[row.stockPolicy]],
          ["Code-barres", <span key="b" className="cat-mono">{row.barcode ?? "—"}</span>],
          ["Référence fabricant", row.manufacturerReference ?? "—"],
          ...Object.entries(detail.data.attributes).map(([key, value]): [string, string] => [family?.variantAttributes.find((a) => a.key === key)?.label ?? key, value]),
          ...(family?.code === "ophthalmic_lenses" ? [
            ["Indice", lens?.indices.find((i) => i.id === detail.data.lensIndexId)?.name ?? "—"] as [string, string],
            ["Traitements", detail.data.treatmentIds.map((t) => lens?.treatments.find((x) => x.id === t)?.name ?? "?").join(", ") || "aucun"] as [string, string],
          ] : []),
          ["Statut", archived ? <Badge key="st" tone="arch" icon={Archive}>Archivée</Badge> : <Badge key="st" tone="ok">Active</Badge>],
          ...(detail.data.archiveReason ? [["Motif d'archivage", detail.data.archiveReason] as [string, string]] : []),
        ]} />
        {access.catalogWrite && !archived && policies.length > 1 && (
          <p className="clients-field__hint" style={{ marginTop: 12 }}>Changement de politique refusé tant que du stock, du réservé ou un inventaire ouvert existent.</p>
        )}
      </Card>
      <PriceHistoryCard variantId={variantId} />

      <Modal open={editing !== null} onClose={() => setEditing(null)} tone="accent" icon={Pencil} title={`Modifier la variante ${row.sku}`} size="wide"
        actions={<>
          <Button onClick={() => setEditing(null)}>Annuler</Button>
          <Button variant="primary" disabled={write.isPending || !editing}
            onClick={() => editing && write.mutate({ method: "PUT", path: `/api/catalog/variants/${variantId}`, body: { version: editingVersion, ...variantBody(editing, false) } },
              { onSuccess: () => { toast.show("Modifications enregistrées", row.sku); setEditing(null); } })}>
            <Save size={16} aria-hidden="true" />Enregistrer les modifications
          </Button>
        </>}>
        <ErrorBanner error={write.error} onReload={() => { void detail.refetch(); setEditing(null); }} />
        {editing && row.version !== editingVersion && !write.error && (
          <Banner tone="warn" icon={Info} title="Variante modifiée sur un autre poste">
            Votre saisie est conservée. L&apos;enregistrement sera refusé tant que vous n&apos;aurez pas rouvert la variante sur sa dernière version.
          </Banner>
        )}
        {editing && <VariantFields family={family} familyCode={family?.code ?? ""} lens={lens} draft={editing} onChange={setEditing} canAutoSku={false} errors={write.error?.error.fieldErrors} />}
      </Modal>
      <Modal open={archiving} onClose={() => setArchiving(false)} tone="warn" icon={Archive} title={`Archiver la variante ${row.sku} ?`}
        description="Elle n'apparaîtra plus à la vente ; son historique (prix, mouvements) est conservé. Refusé tant qu'il reste du stock, du réservé ou un inventaire ouvert."
        actions={<>
          <Button onClick={() => setArchiving(false)}>Annuler</Button>
          <Button className="cat-btn-danger-solid" disabled={write.isPending}
            onClick={() => write.mutate({ method: "POST", path: `/api/catalog/variants/${variantId}/archive`, body: { version: row.version } },
              { onSuccess: () => { setArchiving(false); toast.show("Variante archivée", row.sku); } })}>
            <Archive size={16} aria-hidden="true" />Archiver la variante
          </Button>
        </>}>
        {write.error && <Banner tone="err" icon={Info} title="Archivage refusé" role="alert" compact>{errorMessage(write.error.error)}</Banner>}
      </Modal>
    </>
  );
}

const HISTORY_FIELDS: Record<string, string> = { purchase_price_ht: "Prix d'achat HT", coefficient: "Coefficient", pricing_mode: "Mode", sale_price_ht: "Prix de vente HT", supplier_price_ht: "Prix fournisseur HT" };
const HISTORY_ORIGINS: Record<string, string> = { edit: "Saisie", recalculation: "Recalcul", manual_price: "Prix manuel", import: "Import", receipt: "Réception" };
const MODE_LABELS: Record<string, string> = { coefficient: "Coefficient", manual: "Manuel" };

function historyValue(entry: PriceHistoryEntry, value: string | null, mode: string | null) {
  if (entry.field === "pricing_mode") return mode ? MODE_LABELS[mode] ?? mode : "—";
  if (entry.field === "coefficient") return value ? `× ${formatDecimal(value)}` : "—";
  return formatMoney(value, entry.currency);
}

function PriceHistoryCard({ variantId }: { variantId: string }) {
  const { store } = useActiveStore();
  const history = useCatalogQuery<PriceHistoryEntry[]>(store ? `/api/pricing/variants/${variantId}/history${query({ storeId: store.id })}` : null);
  const rows = Array.isArray(history.data) ? history.data : [];
  return (
    <Card title="Historique des prix" icon={History} flush actions={store && <span className="clients-muted">{store.name} · {store.currency}</span>}>
      {history.isPending ? <p className="cat-empty-mini clients-muted">Chargement…</p> : history.isError ? <p className="cat-empty-mini clients-muted">Historique indisponible.</p> : rows.length === 0 ? (
        <p className="cat-empty-mini clients-muted">Aucune modification de prix pour ce magasin.</p>
      ) : (
        <TableScroll label="Historique des prix">
        <table className="clients-table cat-table cat-table--inner cat-table--compact">
          <thead><tr><th scope="col">Date</th><th scope="col">Champ</th><th scope="col" className="cat-num">Ancien</th><th scope="col" className="cat-num">Nouveau</th><th scope="col">Origine</th><th scope="col" className="cat-hide-sm">Auteur</th></tr></thead>
          <tbody>{rows.slice(0, 12).map((h) => (
            <tr key={h.id}>
              <td className="clients-muted tnum" style={{ whiteSpace: "nowrap" }}>{formatDateTime(h.changedAtUtc)}</td>
              <td>{HISTORY_FIELDS[h.field] ?? h.field}</td>
              <td className="cat-num cat-secondary">{historyValue(h, h.oldValue, h.oldMode)}</td>
              <td className="cat-num cat-strong">{historyValue(h, h.newValue, h.newMode)}</td>
              <td className="cat-td-fill"><Badge tone={h.origin === "manual_price" ? "warn" : h.origin === "recalculation" ? "info" : "arch"}>{HISTORY_ORIGINS[h.origin] ?? h.origin}</Badge>{h.reason && <ClipText className="cat-sub" text={h.reason} />}</td>
              <td className="cat-secondary cat-hide-sm">{h.changedBy.displayName}</td>
            </tr>
          ))}</tbody>
        </table>
        </TableScroll>
      )}
    </Card>
  );
}

function StockHistoryCard({ variantId }: { variantId: string }) {
  const navigate = useNavigate();
  const { store } = useActiveStore();
  const movements = useCatalogQuery<Page<Movement>>(store ? `/api/stock/movements${query({ storeId: store.id, variantId, pageSize: 8 })}` : null);
  const rows = movements.data?.items ?? [];
  return (
    <Card title="Historique de stock" icon={History} flush foot={<button type="button" className="cat-card__link" onClick={() => navigate("/inventaire/mouvements")}>Tout voir</button>}>
      {movements.isPending ? <p className="cat-empty-mini clients-muted">Chargement…</p> : rows.length === 0 ? (
        <p className="cat-empty-mini clients-muted">Aucun mouvement enregistré pour cette variante dans ce magasin.</p>
      ) : (
        <TableScroll label="Historique de stock" side>
        <table className="clients-table cat-table cat-table--inner cat-table--compact cat-table--side">
          <thead><tr><th scope="col">Date</th><th scope="col">Type</th><th scope="col" className="cat-num">Qté</th><th scope="col">Motif</th></tr></thead>
          <tbody>{rows.map((m) => (
            <tr key={m.id}>
              <td className="clients-muted tnum cat-nowrap" style={{ fontSize: 12.5 }}>{formatDateTime(m.performedAtUtc)}</td>
              <td><Badge tone={m.quantityDelta > 0 ? "ok" : m.quantityDelta < 0 ? "err" : "info"}>{MOVEMENT_LABELS[m.type] ?? m.type}</Badge></td>
              <td className="cat-num"><Diff value={m.quantityDelta} /></td>
              <td className="cat-secondary cat-td-fill" style={{ fontSize: 12.5 }}>
                <ClipText text={m.comment ? `${reasonLabel(m.reasonCode)} — ${m.comment}` : reasonLabel(m.reasonCode)} />
                <div className="cat-sub">{m.locationCode} · stock {m.quantityAfter}</div>
              </td>
            </tr>
          ))}</tbody>
        </table>
        </TableScroll>
      )}
    </Card>
  );
}

// ---------- colonne latérale : prix & disponibilité, fournisseurs ----------

function VariantSide({ variantId }: { variantId: string }) {
  const access = useCatalogAccess();
  const detail = useVariant(variantId);
  if (detail.isPending || detail.isError) return null;
  return (
    <>
      <PriceCard detail={detail.data} />
      {access.suppliersRead && <SuppliersCard detail={detail.data} />}
      {access.stockRead && detail.data.row.stockPolicy === "stocked" && <StockHistoryCard variantId={variantId} />}
    </>
  );
}

function PriceCard({ detail }: { detail: VariantDetail }) {
  const access = useCatalogAccess();
  const { row } = detail;
  const price = row.price;
  const canEdit = row.status === "active" && ((access.pricingWrite && access.costsRead) || access.pricingManual || access.costsWrite);
  const [editing, setEditing] = useState(false);
  const [thresholdOpen, setThresholdOpen] = useState(false);
  const badge = availabilityBadge(row.availability);
  const balances = detail.balances ?? [];

  return (
    <Card title="Prix & disponibilité" icon={Coins}
      actions={<Badge tone="arch">{price.currency}</Badge>}
      foot={canEdit && (
        <Button size="sm" variant={editing ? "secondary" : "primary"} onClick={() => setEditing((v) => !v)}>
          {editing ? <><X size={14} aria-hidden="true" />Fermer l&apos;édition</> : <><Pencil size={14} aria-hidden="true" />Modifier le prix</>}
        </Button>
      )}>
      <div className="cat-price">
        <span className="cat-price__label">Prix de vente HT validé</span>
        <span className="cat-price__value" data-testid="sale-price">{formatMoney(price.salePriceHt, price.currency)}</span>
      </div>
      <div className="cat-line">
        <span>Mode</span>
        {price.pricingMode === "manual" ? <Badge tone="warn" icon={Pencil}>Prix manuel</Badge> : <Badge tone="accent" icon={Calculator}>Coefficient</Badge>}
      </div>
      <div className="cat-line">
        <span>État</span>
        <Badge tone={price.priceStatus === "up_to_date" ? "ok" : "warn"}>{PRICE_STATUS_LABELS[price.priceStatus]}</Badge>
      </div>
      <div className="cat-line">
        <span>Prix TTC</span>
        <span className={price.salePriceTtc ? "cat-line__value" : "clients-muted"}>{price.salePriceTtc ? formatMoney(price.salePriceTtc, price.currency) : "indisponible — fiscalité du magasin non validée"}</span>
      </div>
      <div className="cat-sep" />
      {price.costsVisible ? (
        <>
          <div className="cat-line"><span>Prix d&apos;achat HT</span><span className="cat-line__value">{formatMoney(price.purchasePriceHt ?? null, price.currency)}</span></div>
          <div className="cat-line"><span>Coefficient</span><span className="cat-line__value">{price.coefficient ? `× ${formatDecimal(price.coefficient)}` : "—"}</span></div>
          <div className="cat-line"><span>Coefficient effectif</span><span className="cat-line__value">{price.effectiveCoefficient ? `× ${formatDecimal(price.effectiveCoefficient)}` : "—"}</span></div>
          <div className="cat-line"><span>Marge HT</span>{price.margin ? <span className="cat-marg"><TrendingUp size={14} aria-hidden="true" />{formatMoney(price.margin, price.currency)}</span> : <span className="clients-muted">—</span>}</div>
          {price.priceStatus === "recalculation_pending" && price.computedSalePriceHt && (
            <div className="cat-line"><span>Prix recalculé proposé</span><span className="cat-line__value">{formatMoney(price.computedSalePriceHt, price.currency)}</span></div>
          )}
        </>
      ) : (
        <Lock>Prix d&apos;achat, coefficient et marge non visibles pour votre profil</Lock>
      )}
      {editing && <PriceEditor detail={detail} onDone={() => setEditing(false)} />}
      <div className="cat-sep" />
      {row.stockPolicy === "stocked" && row.stock ? (
        <>
          <div className="cat-line">
            <StockCell value={`${row.stock.available} disponible${row.stock.available > 1 ? "s" : ""}`} tone={stockTone(row.availability)} />
            <Badge tone={badge.tone} icon={badge.icon}>{AVAILABILITY_LABELS[row.availability]}</Badge>
          </div>
          <div className="cat-line"><span>En main · réservé</span><span className="cat-line__value">{row.stock.onHand} · {row.stock.reserved}</span></div>
          <div className="cat-line">
            <span>Seuil d&apos;alerte (magasin)</span>
            <span className="cat-line__value">
              {row.stock.lowStockThreshold ?? "non défini"}
              {access.stockAdjust && row.status === "active" && (
                <Button size="sm" variant="tertiary" onClick={() => setThresholdOpen(true)} aria-label="Modifier le seuil d'alerte"><Bell size={14} aria-hidden="true" />Modifier</Button>
              )}
            </span>
          </div>
          {thresholdOpen && <VariantThresholdModal variantId={row.variantId} sku={row.sku} onClose={() => setThresholdOpen(false)} />}
          {balances.length > 0 && (
            <TableScroll label="Stock par emplacement" side>
            <table className="clients-table cat-table cat-table--inner cat-table--compact cat-table--side" style={{ marginTop: 12 }}>
              <thead><tr><th scope="col">Emplacement</th><th scope="col" className="cat-num">En main</th><th scope="col" className="cat-num">Réservé</th><th scope="col" className="cat-num">Dispo.</th></tr></thead>
              <tbody>{balances.map((b) => (
                <tr key={b.locationId}><td className="cat-td-fill"><ClipText text={b.locationName} />{b.isDefault && <span className="cat-sub">défaut</span>}</td><td className="cat-num">{b.onHand}</td><td className="cat-num">{b.reserved}</td><td className="cat-num cat-strong">{b.available}</td></tr>
              ))}</tbody>
            </table>
            </TableScroll>
          )}
        </>
      ) : (
        <div className="cat-line">
          <span>{POLICY_LABELS[row.stockPolicy]}</span>
          <Badge tone={badge.tone} icon={badge.icon}>{AVAILABILITY_LABELS[row.availability]}</Badge>
        </div>
      )}
    </Card>
  );
}

/** Édition du prix dans le bloc Prix : aperçu SERVEUR obligatoire avant enregistrement, aucun calcul sur le poste. */
function PriceEditor({ detail, onDone }: { detail: VariantDetail; onDone: () => void }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const { store } = useActiveStore();
  const price = detail.row.price;
  const write = useCatalogWrite();
  const preview = useCatalogPreview<{ computedSalePriceHt: string; belowCost: boolean; currency: string }>();
  const [purchase, setPurchase] = useState(price.purchasePriceHt ?? "");
  const [coefficient, setCoefficient] = useState(price.coefficient ?? "");
  const [manual, setManual] = useState("");
  const [reason, setReason] = useState("");
  const [acceptBelowCost, setAcceptBelowCost] = useState(false);
  // Version du tarif figée à l'ouverture de l'éditeur (COR-05-02) : jamais d'écrasement d'un tarif changé ailleurs.
  const [baseVersion] = useState(price.version);
  // Aperçu valable seulement pour les valeurs aperçues : toute modification de saisie l'invalide (jamais d'écriture silencieuse).
  const [previewState, setPreviewState] = useState<{ key: string; value: string } | null>(null);
  const previewKey = `${purchase}|${coefficient}`;
  const previewed = previewState?.key === previewKey ? previewState.value : null;
  if (!store) return null;
  const variantPath = `/api/pricing/variants/${detail.row.variantId}`;
  const base = { storeId: store.id, version: baseVersion };
  const fieldErrors = write.error?.error.fieldErrors ?? preview.error?.error.fieldErrors;
  const done = (title: string) => ({ onSuccess: () => { toast.show(title, `${detail.row.sku} · magasin ${store.name}`); onDone(); } });

  return (
    <div className="cat-editor">
      <ErrorBanner error={(write.error ?? preview.error) as ApiRequestError | null} />
      {price.version !== baseVersion && !write.error && (
        <Banner tone="warn" icon={Info} title="Tarif modifié sur un autre poste">
          Vos saisies sont conservées mais l&apos;enregistrement sera refusé : fermez puis rouvrez l&apos;édition du tarif pour repartir du tarif enregistré.
        </Banner>
      )}
      {access.pricingWrite && access.costsRead && (
        <>
          <p className="cat-editor__title"><Calculator size={14} aria-hidden="true" />Prix par coefficient — calcul serveur, un seul arrondi au centime</p>
          <div className="cat-grid">
            <Field label="Prix d'achat HT" name="purchasePriceHt" value={purchase} onChange={setPurchase} inputMode="decimal" errors={fieldErrors} suffix={price.currency}
              disabled={!access.costsWrite} hint={access.costsWrite ? undefined : "Lecture seule (costs.write)."} />
            <Field label="Coefficient" name="coefficient" value={coefficient} onChange={setCoefficient} inputMode="decimal" errors={fieldErrors} suffix="×" />
          </div>
          <div className="cat-preview" aria-live="polite">
            <span>Aperçu du prix de vente HT</span>
            <span className="cat-preview__value">{previewed ? formatMoney(previewed, price.currency) : "—"}</span>
          </div>
          {previewed && <span className="sr-only">Prix calculé : {formatMoney(previewed, price.currency)}</span>}
          {previewed && preview.data?.belowCost && (
            <label className="cat-check cat-check--plain"><input type="checkbox" checked={acceptBelowCost} onChange={(e) => setAcceptBelowCost(e.target.checked)} />Je confirme une vente sous le prix d&apos;achat</label>
          )}
          <div className="cat-editor__actions">
            <Button size="sm" disabled={preview.isPending}
              onClick={() => preview.mutate({ path: "/api/pricing/preview", body: { storeId: store.id, purchasePriceHt: decimalInput(purchase), coefficient: decimalInput(coefficient) } },
                { onSuccess: (r) => { setPreviewState({ key: previewKey, value: r.computedSalePriceHt }); setAcceptBelowCost(false); } })}>
              <Calculator size={14} aria-hidden="true" />Aperçu
            </Button>
            <Button size="sm" variant="primary" disabled={!previewed || write.isPending}
              onClick={() => write.mutate({ method: "PUT", path: variantPath, body: {
                ...base, purchasePriceHt: access.costsWrite ? decimalInput(purchase) : undefined, coefficient: decimalInput(coefficient), expectedSalePriceHt: previewed, acceptBelowCost,
              } }, done("Prix enregistré"))}>
              <Save size={14} aria-hidden="true" />Enregistrer ce prix
            </Button>
            {price.priceStatus === "recalculation_pending" && price.computedSalePriceHt && (
              <Button size="sm" onClick={() => write.mutate({ method: "POST", path: `${variantPath}/recalculate`, body: { ...base, expectedSalePriceHt: price.computedSalePriceHt } }, done("Recalcul appliqué"))}>
                Appliquer le recalcul ({formatMoney(price.computedSalePriceHt, price.currency)})
              </Button>
            )}
          </div>
        </>
      )}
      {access.costsWrite && !access.pricingWrite && (
        <>
          <p className="cat-editor__title"><Coins size={14} aria-hidden="true" />Prix d&apos;achat HT seul — le prix de vente ne change pas</p>
          <Field label="Prix d'achat HT" name="purchaseOnly" value={purchase} onChange={setPurchase} inputMode="decimal" errors={fieldErrors} suffix={price.currency} />
          <div className="cat-editor__actions">
            <Button size="sm" variant="primary" onClick={() => write.mutate({ method: "PUT", path: `${variantPath}/purchase-price`, body: { ...base, purchasePriceHt: decimalInput(purchase) } }, done("Prix d'achat enregistré"))}>
              <Save size={14} aria-hidden="true" />Enregistrer le prix d&apos;achat
            </Button>
          </div>
        </>
      )}
      {access.pricingManual && (
        <>
          <p className="cat-editor__title"><LockIcon size={14} aria-hidden="true" />Prix manuel — le coefficient enregistré est conservé, non appliqué</p>
          <div className="cat-grid">
            <Field label="Prix de vente HT" name="salePriceHt" value={manual} onChange={setManual} inputMode="decimal" errors={fieldErrors} suffix={price.currency} />
            <Field label="Motif (facultatif)" name="reason" value={reason} onChange={setReason} />
          </div>
          <div className="cat-editor__actions">
            <Button size="sm" onClick={() => write.mutate({ method: "PUT", path: `${variantPath}/manual-price`, body: { ...base, salePriceHt: decimalInput(manual), reason: orNull(reason) } }, done("Prix manuel appliqué"))}>
              <Pencil size={14} aria-hidden="true" />Appliquer le prix manuel
            </Button>
          </div>
        </>
      )}
    </div>
  );
}

function SuppliersCard({ detail }: { detail: VariantDetail }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const navigate = useNavigate();
  const write = useCatalogWrite();
  const [linking, setLinking] = useState(false);
  const links = (detail.suppliers ?? []).filter((l) => !l.removed).sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary));
  const path = (id: string) => `/api/catalog/variants/${detail.row.variantId}/suppliers/${id}`;
  const canWrite = access.catalogWrite && detail.row.status === "active";

  return (
    <Card title="Fournisseurs" icon={Truck} flush
      foot={canWrite && <Button size="sm" onClick={() => { write.reset(); setLinking(true); }}><Plus size={14} aria-hidden="true" />Rattacher un fournisseur</Button>}>
      <div className="cat-card__body" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        {detail.row.isIncomplete && (
          <Banner tone="warn" icon={Info} title="Fiche incomplète" compact>Aucun fournisseur principal actif : la commande fournisseur effective l&apos;exigera.</Banner>
        )}
        {!linking && <ErrorBanner error={write.error} />}
        {links.length === 0 ? (
          <div className="cat-empty-mini">
            <span className="cat-thumb"><PackageOpen size={26} aria-hidden="true" /></span>
            <div className="cat-empty-mini__title">Aucun fournisseur rattaché</div>
            <p className="cat-empty-mini__text">Plusieurs fournisseurs sont possibles par variante, dont un principal.</p>
          </div>
        ) : links.map((l) => (
          <div key={l.supplierId} style={{ display: "flex", gap: 12, alignItems: "flex-start" }}>
            <span className="cat-thumb cat-thumb--round"><Building2 size={20} aria-hidden="true" /></span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6, alignItems: "center" }}>
                <button type="button" className="cat-card__link" style={{ color: "var(--text-primary)", fontWeight: 700, fontSize: 15 }} onClick={() => navigate(`/produits/fournisseurs/${l.supplierId}`)}>{l.name}</button>
                {l.isPrimary && <Badge tone="accent" icon={Star}>Principal</Badge>}
                {l.supplierStatus === "archived" && <Badge tone="arch">Archivé</Badge>}
              </div>
              <div className="cat-sub cat-mono">{l.code}</div>
              <KeyValues compact rows={[
                ["Réf. fournisseur", l.supplierReference ?? "—"],
                ["Délai indicatif", l.leadTimeDays !== null ? `${l.leadTimeDays} j` : "—"],
                ...(detail.costsVisible ? [["Prix fournisseur HT", l.supplierPriceHt && l.supplierCurrency ? formatMoney(l.supplierPriceHt, l.supplierCurrency) : "—"] as [string, string]] : []),
              ]} />
              {canWrite && (
                <div className="clients-actions" style={{ marginTop: 8 }}>
                  {!l.isPrimary && l.supplierStatus === "active" && (
                    <Button size="sm" variant="tertiary" onClick={() => write.mutate({ method: "PUT", path: path(l.supplierId), body: { version: l.version, isPrimary: true, supplierReference: l.supplierReference, leadTimeDays: l.leadTimeDays } },
                      { onSuccess: () => toast.show("Fournisseur principal modifié", l.name) })}>
                      <Star size={14} aria-hidden="true" />Rendre principal
                    </Button>
                  )}
                  <Button size="sm" variant="danger" onClick={() => write.mutate({ method: "POST", path: `${path(l.supplierId)}/remove`, body: { version: l.version } },
                    { onSuccess: () => toast.show("Fournisseur retiré", l.name) })}>
                    <X size={14} aria-hidden="true" />Retirer
                  </Button>
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
      {linking && <LinkSupplierModal detail={detail} onClose={() => setLinking(false)} />}
    </Card>
  );
}

function LinkSupplierModal({ detail, onClose }: { detail: VariantDetail; onClose: () => void }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const write = useCatalogWrite();
  const [supplier, setSupplier] = useState<SupplierChoice | null>(null);
  const supplierId = supplier?.id ?? "";
  const linked = (detail.suppliers ?? []).filter((l) => !l.removed).map((l) => l.supplierId);
  const [reference, setReference] = useState("");
  const [primary, setPrimary] = useState(false);
  const [priceHt, setPriceHt] = useState("");
  const [currency, setCurrency] = useState(detail.row.price.currency);
  const errors = write.error?.error.fieldErrors;
  return (
    <Modal open onClose={onClose} tone="accent" icon={Truck} title="Rattacher un fournisseur" size="wide"
      description={`Variante ${detail.row.sku}. Plusieurs fournisseurs possibles, au plus un principal ; le prix fournisseur ne modifie pas le prix d'achat de référence.`}
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={supplierId === "" || write.isPending}
          onClick={() => write.mutate({ method: "PUT", path: `/api/catalog/variants/${detail.row.variantId}/suppliers/${supplierId}`, body: {
            supplierReference: orNull(reference), isPrimary: primary,
            ...(access.costsWrite && priceHt.trim() !== "" ? { supplierPriceHt: decimalInput(priceHt), supplierCurrency: currency.trim().toUpperCase() } : {}),
          } }, { onSuccess: () => { toast.show("Fournisseur rattaché", detail.row.sku); onClose(); } })}>
          <Plus size={16} aria-hidden="true" />Rattacher
        </Button>
      </>}>
      <ErrorBanner error={write.error} />
      <div className="cat-grid">
        <div className="cat-span2">
          <SupplierCombobox label="Fournisseur" value={supplier} onChange={setSupplier} required exclude={linked} autoFocus
            error={fieldError(errors, "supplierId")} hint="Recherche par nom, code ou référence ; création rapide si le fournisseur n'existe pas." />
        </div>
        <Field label="Référence fournisseur" name="supplierReference" value={reference} onChange={setReference} errors={errors} />
        <div className="clients-field" style={{ justifyContent: "flex-end" }}>
          <label className="cat-check"><input type="checkbox" checked={primary} onChange={(e) => setPrimary(e.target.checked)} />Fournisseur principal</label>
        </div>
        {access.costsWrite && <Field label="Prix fournisseur HT" name="supplierPriceHt" value={priceHt} onChange={setPriceHt} inputMode="decimal" errors={errors} suffix={currency} hint="Facultatif ; sans conversion." />}
        {access.costsWrite && <Field label="Devise du prix fournisseur" name="supplierCurrency" value={currency} onChange={setCurrency} errors={errors} />}
      </div>
    </Modal>
  );
}
