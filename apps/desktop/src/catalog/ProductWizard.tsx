// Assistant de création produit en cinq étapes (COR-05-06), dans la composition du Lot 6 (cartes, grilles à deux
// colonnes, barre d'actions en pied, étapes .cat-steps du Lot 6 déjà portées pour l'import) :
// 1 Informations générales · 2 Variantes · 3 Fournisseurs · 4 Tarification · 5 Stock initial et validation.
// Règles : Précédent / Suivant, progression visible, contrôle propre à chaque étape, saisies conservées entre étapes et
// en BROUILLON DE SESSION (mémoire du poste, jamais sur disque — les coûts n'y sont pas persistés) repris si l'on revient ;
// SKU généré et garanti unique par le serveur ; fournisseur et tarif facultatifs (fiche incomplète admise) ; aucun stock
// initial pour une prestation ou un article commandé ; aperçu du prix calculé PAR LE SERVEUR. Enregistrement final :
// UNE requête `POST /api/catalog/products`, tout ou rien, `requestId` conservé pour la réémission (aucun doublon).
import { Boxes, Building2, Calculator, Check, ChevronLeft, ChevronRight, ClipboardCheck, Coins, Info, Layers, Plus, Save, Sparkles, Trash2, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router";
import { type LensReferences, newRequestId } from "../api/catalog.ts";
import { useAuth } from "../auth/authContext.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { errorMessage, useActiveStore, useCatalogPreview, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";
import { decimalInput, fieldError, formatMoney, POLICY_LABELS } from "./catalogFormat.ts";
import { Badge, Banner, Card, Field, FormBar, KeyValues, Lock, PageHeader, SelectField, TableScroll } from "./CatalogUi.tsx";
import { UnsavedModal, VariantFields } from "./ModelViews.tsx";
import { emptyVariant, orNull, skuPrefix, stepOfField, useFamilies, useUnsavedGuard, type VariantDraft, variantBody, WIZARD_STEPS } from "./productForm.ts";
import type { LensOffer } from "./lensOffers.ts";
import { type SupplierChoice, SupplierCombobox } from "./SupplierCombobox.tsx";

type PriceMode = "none" | "coefficient" | "manual";

interface PriceDraft {
  mode: PriceMode;
  purchase: string;
  coefficient: string;
  manual: string;
  reason: string;
  acceptBelowCost: boolean;
  /** Aperçu serveur valable pour la saisie `key` seulement (toute modification l'invalide). */
  preview: { key: string; value: string; belowCost: boolean } | null;
}

interface StockDraft {
  locationId: string;
  quantity: string;
}

interface WizardDraft {
  requestId: string;
  step: number;
  familyCode: string;
  brand: string;
  name: string;
  description: string;
  lensRangeId: string;
  modelAttributes: Record<string, string>;
  variants: VariantDraft[];
  supplier: SupplierChoice | null;
  references: string[];
  leadTimeDays: string;
  prices: PriceDraft[];
  stocks: StockDraft[];
}

const emptyPrice = (): PriceDraft => ({ mode: "none", purchase: "", coefficient: "", manual: "", reason: "", acceptBelowCost: false, preview: null });

const priceKey = (price: PriceDraft) => `${decimalInput(price.purchase) ?? ""}|${decimalInput(price.coefficient) ?? ""}`;

function freshDraft(defaultLocation: string): WizardDraft {
  return {
    requestId: newRequestId(), step: 0, familyCode: "optical_frames", brand: "", name: "", description: "", lensRangeId: "", modelAttributes: {},
    variants: [emptyVariant("stocked")], supplier: null, references: [""], leadTimeDays: "", prices: [emptyPrice()], stocks: [{ locationId: defaultLocation, quantity: "" }],
  };
}

// Brouillons de la session, en mémoire du poste seulement (aucune persistance disque), par compte.
const drafts = new Map<string, WizardDraft>();

export function ProductWizardView() {
  const navigate = useNavigate();
  const toast = useToast();
  const access = useCatalogAccess();
  const { state } = useAuth();
  const userId = state.status === "authenticated" ? state.session.user.id : "";
  const { store } = useActiveStore();
  const families = useFamilies();
  const lens = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  const write = useCatalogWrite<{ modelId: string; variants: { variantId: string; sku: string }[]; replayed: boolean }>();
  const preview = useCatalogPreview<{ computedSalePriceHt: string; belowCost: boolean; currency: string }>();
  const defaultLocation = store?.locations.find((l) => l.isDefault && l.status === "active")?.id ?? store?.locations.find((l) => l.status === "active")?.id ?? "";
  const [resumed] = useState(() => drafts.has(userId));
  const [draft, setDraft] = useState<WizardDraft>(() => drafts.get(userId) ?? freshDraft(defaultLocation));
  const [tried, setTried] = useState<number | null>(null);
  // Verres : fournisseurs ayant déclaré une offre pour la gamme choisie (référentiel COR-05-05), proposés à l'étape 3.
  const lensOffers = useCatalogQuery<LensOffer[]>(draft.familyCode === "ophthalmic_lenses" && draft.lensRangeId !== "" && access.suppliersRead
    ? `/api/catalog/lens-offers?rangeId=${draft.lensRangeId}` : null);
  const family = families.find((f) => f.code === draft.familyCode);
  const errors = write.error?.error.fieldErrors;
  const dirty = draft.name !== "" || draft.brand !== "" || draft.variants.some((v) => v.label !== "") || draft.supplier !== null;
  const guard = useUnsavedGuard(dirty && !write.isSuccess);
  const currency = store?.currency ?? "";
  const stockedIndexes = draft.variants.map((v, i) => (v.stockPolicy === "stocked" ? i : -1)).filter((i) => i >= 0);
  const coefficientAllowed = access.pricingWrite && access.costsRead;

  // Brouillon de session : chaque changement est mémorisé (jusqu'à la création ou à « Recommencer »).
  useEffect(() => {
    if (userId !== "" && !write.isSuccess) drafts.set(userId, draft);
  }, [draft, userId, write.isSuccess]);

  const patch = (next: Partial<WizardDraft>) => setDraft((current) => ({ ...current, ...next }));
  const setVariant = (index: number, next: VariantDraft) => patch({ variants: draft.variants.map((v, i) => (i === index ? next : v)) });
  const setPrice = (index: number, next: Partial<PriceDraft>) => patch({ prices: draft.prices.map((p, i) => (i === index ? { ...p, ...next } : p)) });
  const setStock = (index: number, next: Partial<StockDraft>) => patch({ stocks: draft.stocks.map((s, i) => (i === index ? { ...s, ...next } : s)) });

  const changeFamily = (code: string) => {
    const next = families.find((x) => x.code === code);
    const policy = next?.allowedPolicies[0] ?? "stocked";
    patch({
      familyCode: code, modelAttributes: {}, lensRangeId: "",
      variants: draft.variants.map((v) => ({ ...v, stockPolicy: next?.allowedPolicies.some((allowed) => allowed === v.stockPolicy) ? v.stockPolicy : policy, attributes: {}, lensIndexId: "", treatmentIds: [] })),
    });
  };
  const addVariant = () => patch({
    variants: [...draft.variants, emptyVariant(family?.allowedPolicies[0] ?? "stocked")],
    references: [...draft.references, ""], prices: [...draft.prices, emptyPrice()], stocks: [...draft.stocks, { locationId: defaultLocation, quantity: "" }],
  });
  const removeVariant = (index: number) => patch({
    variants: draft.variants.filter((_, i) => i !== index), references: draft.references.filter((_, i) => i !== index),
    prices: draft.prices.filter((_, i) => i !== index), stocks: draft.stocks.filter((_, i) => i !== index),
  });

  // ---------- contrôle propre à chaque étape (le serveur reste seul juge) ----------
  const stepProblems = (step: number): Record<string, string> => {
    const problems: Record<string, string> = {};
    if (step === 0) {
      if (draft.name.trim() === "") problems.name = "Le nom du modèle est obligatoire.";
      if (draft.familyCode === "ophthalmic_lenses" && draft.lensRangeId === "") problems.lensRangeId = "Choisissez la gamme de verres.";
    }
    if (step === 1) {
      draft.variants.forEach((v, i) => {
        if (v.label.trim() === "") problems[`variants[${i}].label`] = "Libellé obligatoire.";
        if (draft.familyCode === "ophthalmic_lenses" && v.lensIndexId === "") problems[`variants[${i}].lensIndexId`] = "Indice obligatoire.";
        if (v.skuCustom && v.sku.trim() === "") problems[`variants[${i}].sku`] = "Saisissez le SKU ou revenez à la génération automatique.";
      });
    }
    if (step === 2 && draft.leadTimeDays.trim() !== "" && !/^\d{1,3}$/.test(draft.leadTimeDays.trim())) problems.leadTimeDays = "Nombre de jours entier (0 à 999).";
    if (step === 3) {
      draft.prices.forEach((p, i) => {
        if (p.mode === "coefficient") {
          if (decimalInput(p.purchase) === null) problems[`variants[${i}].price.purchasePriceHt`] = "Prix d'achat HT requis.";
          if (decimalInput(p.coefficient) === null) problems[`variants[${i}].price.coefficient`] = "Coefficient requis.";
          else if (p.preview?.key !== priceKey(p)) problems[`variants[${i}].price.expectedSalePriceHt`] = "Calculez l'aperçu du prix de vente (serveur) avant de continuer.";
          else if (p.preview.belowCost && !p.acceptBelowCost) problems[`variants[${i}].price.acceptBelowCost`] = "Prix sous le prix d'achat : confirmation requise.";
        }
        if (p.mode === "manual" && decimalInput(p.manual) === null) problems[`variants[${i}].price.salePriceHt`] = "Prix de vente HT requis.";
      });
    }
    if (step === 4) {
      stockedIndexes.forEach((i) => {
        const s = draft.stocks[i];
        if (s.quantity.trim() !== "" && !/^\d{1,6}$/.test(s.quantity.trim())) problems[`variants[${i}].stock.quantity`] = "Quantité entière positive.";
        if (s.quantity.trim() !== "" && Number(s.quantity) > 0 && s.locationId === "") problems[`variants[${i}].stock.locationId`] = "Choisissez l'emplacement.";
      });
    }
    return problems;
  };
  const problems = tried === draft.step ? stepProblems(draft.step) : {};
  const message = (name: string) => problems[name] ?? fieldError(errors, name) ?? undefined;
  const go = (step: number) => {
    if (step > draft.step) {
      for (let s = draft.step; s < step; s += 1) {
        if (Object.keys(stepProblems(s)).length > 0) {
          setTried(s);
          patch({ step: s });
          return;
        }
      }
    }
    setTried(null);
    patch({ step });
  };

  const runPreview = (index: number) => {
    const price = draft.prices[index];
    const purchasePriceHt = decimalInput(price.purchase);
    const coefficient = decimalInput(price.coefficient);
    if (purchasePriceHt === null || coefficient === null) {
      setTried(3);
      return;
    }
    const key = priceKey(price);
    preview.mutate({ path: "/api/pricing/preview", body: { purchasePriceHt, coefficient } }, {
      onSuccess: (r) => setPrice(index, { preview: { key, value: r.computedSalePriceHt, belowCost: r.belowCost } }),
    });
  };
  const copyFirstPrice = () => patch({ prices: draft.prices.map((p, i) => (i === 0 ? p : { ...draft.prices[0] })) });

  const submit = () => {
    for (let s = 0; s < WIZARD_STEPS.length; s += 1) {
      if (Object.keys(stepProblems(s)).length > 0) {
        setTried(s);
        patch({ step: s });
        return;
      }
    }
    const leadTimeDays = draft.leadTimeDays.trim() === "" ? null : Number(draft.leadTimeDays);
    write.mutate({
      method: "POST", path: "/api/catalog/products",
      body: {
        requestId: draft.requestId, familyCode: draft.familyCode, brand: orNull(draft.brand), name: draft.name.trim(), description: orNull(draft.description),
        lensRangeId: orNull(draft.lensRangeId), attributes: draft.modelAttributes,
        variants: draft.variants.map((v, i) => {
          const p = draft.prices[i];
          const s = draft.stocks[i];
          const quantity = s.quantity.trim() === "" ? 0 : Number(s.quantity);
          return {
            variant: variantBody(v, true),
            suppliers: draft.supplier ? [{ supplierId: draft.supplier.id, supplierReference: orNull(draft.references[i] ?? ""), isPrimary: true, leadTimeDays }] : [],
            ...(p.mode === "coefficient" && coefficientAllowed ? {
              price: { mode: "coefficient", purchasePriceHt: decimalInput(p.purchase), coefficient: decimalInput(p.coefficient), expectedSalePriceHt: p.preview?.value, acceptBelowCost: p.acceptBelowCost },
            } : {}),
            ...(p.mode === "manual" && access.pricingManual ? {
              price: { mode: "manual", salePriceHt: decimalInput(p.manual), reason: orNull(p.reason), ...(access.costsWrite && decimalInput(p.purchase) ? { purchasePriceHt: decimalInput(p.purchase) } : {}) },
            } : {}),
            ...(v.stockPolicy === "stocked" && access.stockMovementsWrite && quantity > 0 ? { stock: { locationId: s.locationId, quantity } } : {}),
          };
        }),
      },
    }, {
      onSuccess: (result) => {
        drafts.delete(userId);
        toast.show(result.replayed ? "Produit déjà créé" : "Produit créé",
          `${draft.name} · ${result.variants.length} variante${result.variants.length > 1 ? "s" : ""} (${result.variants.map((v) => v.sku).join(", ")})`);
        navigate(`/produits/modele/${result.modelId}`);
      },
      onError: (error) => {
        // Erreur serveur : retour à la première étape concernée, saisies intactes, même requestId (réémission sûre).
        const fields = Object.keys(error.error.fieldErrors ?? {});
        if (fields.length > 0) patch({ step: Math.min(...fields.map(stepOfField)) });
      },
    });
  };

  const restart = () => {
    drafts.delete(userId);
    write.reset();
    setTried(null);
    setDraft(freshDraft(defaultLocation));
  };
  const close = () => guard.ask(() => { drafts.delete(userId); navigate("/produits"); });
  const last = draft.step === WIZARD_STEPS.length - 1;
  const activeLocations = useMemo(() => store?.locations.filter((l) => l.status === "active") ?? [], [store]);

  return (
    <section className="cat-screen cat-form" aria-labelledby="new-model">
      <PageHeader title="Nouveau produit" subtitle={`Étape ${draft.step + 1} sur ${WIZARD_STEPS.length} — ${WIZARD_STEPS[draft.step]}`}
        actions={<Button variant="ghost" onClick={close}><X size={18} aria-hidden="true" />Fermer</Button>} />
      <div className="cat-form__body">
        <ol className="cat-steps cat-steps--wizard" aria-label="Étapes de la création">
          {WIZARD_STEPS.map((label, index) => (
            <li key={label} className={cn("cat-step", index < draft.step && "cat-step--done", index === draft.step && "cat-step--current")} aria-current={index === draft.step ? "step" : undefined}>
              <button type="button" className="cat-step__btn" onClick={() => go(index)} disabled={write.isPending}>
                <span className="cat-step__n">{index < draft.step ? <Check size={14} aria-hidden="true" /> : index + 1}</span>{label}
              </button>
            </li>
          ))}
        </ol>
        {resumed && draft.step === 0 && !write.error && (
          <Banner tone="info" icon={Info} title="Brouillon repris" compact
            action={<Button size="sm" onClick={restart}>Recommencer</Button>}>
            La saisie commencée pendant cette session a été conservée. Rien n&apos;est enregistré tant que vous n&apos;avez pas confirmé la création.
          </Banner>
        )}
        {write.error && (
          <Banner tone="err" icon={Info} title="Création refusée — rien n'a été enregistré" role="alert">
            {write.error.error.fieldErrors ? "Corrigez les champs signalés ; vos saisies sont conservées." : errorMessage(write.error.error)}
          </Banner>
        )}
        {tried === draft.step && Object.keys(problems).length > 0 && (
          <Banner tone="warn" icon={Info} title="Complétez cette étape pour continuer" role="alert" compact>
            {Object.values(problems)[0]}
          </Banner>
        )}

        {draft.step === 0 && (
          <Card title="Informations générales" icon={Info}>
            <div className="cat-grid">
              <SelectField label="Famille" name="familyCode" value={draft.familyCode} required errors={errors} onChange={changeFamily}
                options={families.map((f) => ({ value: f.code, label: f.label }))} hint="La famille fixe les politiques de stock admises et les caractéristiques proposées." />
              <Field label="Marque" name="brand" value={draft.brand} onChange={(brand) => patch({ brand })} errors={errors} placeholder="Ex. Ray-Ban (facultative)" />
              <Field label="Nom du modèle" name="name" value={draft.name} onChange={(name) => patch({ name })} required errors={errors} error={message("name")} placeholder="Ex. RX5228" span2 />
              {draft.familyCode === "ophthalmic_lenses" && (
                <SelectField label="Gamme de verres" name="lensRangeId" value={draft.lensRangeId} required errors={errors} onChange={(lensRangeId) => patch({ lensRangeId })} span2
                  hint="Les plages de fabrication et restrictions fournisseur se consultent dans le référentiel verres."
                  options={[{ value: "", label: "— Choisir une gamme —" }, ...(lens.data?.ranges ?? []).filter((r) => r.status === "active").map((r) => ({
                    value: r.id, label: `${lens.data?.manufacturers.find((m) => m.id === r.manufacturerId)?.name ?? "?"} — ${r.name}`,
                  }))]} />
              )}
              <Field label="Désignation / description" name="description" value={draft.description} onChange={(description) => patch({ description })} errors={errors} multiline span2
                placeholder="Description commerciale (facultative)" />
              {family?.modelAttributes.map((attribute) => (
                <Field key={attribute.key} label={attribute.label} name={`attributes.${attribute.key}`} value={draft.modelAttributes[attribute.key] ?? ""} errors={errors}
                  inputMode={attribute.kind === "text" ? "text" : "decimal"} onChange={(v) => patch({ modelAttributes: { ...draft.modelAttributes, [attribute.key]: v } })} />
              ))}
            </div>
          </Card>
        )}

        {draft.step === 1 && (
          <Card title={`Variantes (${draft.variants.length})`} icon={Layers}
            actions={<Button size="sm" onClick={addVariant}><Plus size={16} aria-hidden="true" />Ajouter une variante</Button>}>
            <div className="cat-stack">
              <p className="clients-field__hint" style={{ margin: 0 }}>
                Une variante par coloris, calibre ou conditionnement. SKU {skuPrefix(draft.familyCode)}-… généré et garanti unique par le serveur ; code-barres facultatif.
              </p>
              {draft.variants.map((variant, index) => (
                <div key={index} className="cat-variant">
                  <div className="cat-variant__head">
                    Variante {index + 1}
                    <span className="cat-formbar__spacer" />
                    {draft.variants.length > 1 && (
                      <Button size="sm" variant="danger" onClick={() => removeVariant(index)}><Trash2 size={14} aria-hidden="true" />Retirer</Button>
                    )}
                  </div>
                  <VariantFields family={family} familyCode={draft.familyCode} lens={lens.data} draft={variant} prefix={`variants[${index}].`} withPolicy
                    errors={{ ...(errors ?? {}), ...Object.fromEntries(Object.entries(problems).map(([k, v]) => [k, [v]])) }}
                    onChange={(next) => setVariant(index, next)} />
                </div>
              ))}
            </div>
          </Card>
        )}

        {draft.step === 2 && (
          <Card title="Fournisseurs" icon={Building2}>
            {access.suppliersRead ? (
              <div className="cat-stack">
                {(lensOffers.data ?? []).length > 0 && !draft.supplier && (
                  <div className="cat-checks">
                    <span className="clients-field__hint">Fournisseurs proposant cette gamme :</span>
                    {[...new Map((lensOffers.data ?? []).filter((o) => o.status === "active").map((o) => [o.supplierId, o.supplierName])).entries()].map(([id, supplierName]) => (
                      <Button key={id} size="sm" onClick={() => patch({ supplier: { id, code: "", name: supplierName } })}>{supplierName}</Button>
                    ))}
                  </div>
                )}
                <div className="cat-grid">
                  <div className="cat-span2">
                    <SupplierCombobox label="Fournisseur principal" value={draft.supplier} onChange={(supplier) => patch({ supplier })}
                      hint="Facultatif : une fiche sans fournisseur reste possible (signalée « incomplète »). D'autres fournisseurs s'ajoutent ensuite dans la fiche." />
                  </div>
                  {draft.supplier && (
                    <Field label="Délai indicatif (jours)" name="leadTimeDays" value={draft.leadTimeDays} onChange={(leadTimeDays) => patch({ leadTimeDays })} inputMode="numeric"
                      error={message("leadTimeDays")} hint="Facultatif." />
                  )}
                </div>
                {draft.supplier && (
                  <TableScroll label="Références fournisseur par variante">
                    <table className="clients-table cat-table cat-table--inner cat-table--compact">
                      <thead><tr><th scope="col">Variante</th><th scope="col">Référence chez {draft.supplier.name}</th></tr></thead>
                      <tbody>{draft.variants.map((v, i) => (
                        <tr key={i}>
                          <td>{v.label || `Variante ${i + 1}`}</td>
                          <td>
                            <Field label={`Référence fournisseur — ${v.label || `variante ${i + 1}`}`} name={`variants[${i}].suppliers[0].supplierReference`} value={draft.references[i] ?? ""}
                              onChange={(value) => patch({ references: draft.references.map((r, j) => (j === i ? value : r)) })} errors={errors} placeholder="Ex. 0045-HB (facultative, texte conservé tel quel)" />
                          </td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </TableScroll>
                )}
              </div>
            ) : <Lock>Rattachement fournisseur réservé au droit suppliers.read ; la fiche pourra être complétée plus tard.</Lock>}
          </Card>
        )}

        {draft.step === 3 && (
          <Card title={`Tarification — magasin ${store?.name ?? ""} (${currency})`} icon={Coins}
            actions={draft.variants.length > 1 && draft.prices[0].mode !== "none" && (
              <Button size="sm" onClick={copyFirstPrice}>Appliquer le tarif de la variante 1 à toutes</Button>
            )}>
            <div className="cat-stack">
              <Banner tone="info" icon={Calculator} title="Prix de vente HT = prix d'achat HT × coefficient, calculé et arrondi par le serveur" compact>
                Devise du magasin ({currency}), aucune conversion ; aucun TTC tant que la fiscalité du magasin n&apos;est pas validée. Tarif facultatif à la création.
              </Banner>
              {!coefficientAllowed && !access.pricingManual && (
                <Lock>Votre profil ne permet pas de fixer les prix : un compte autorisé les complétera dans la fiche produit.</Lock>
              )}
              {draft.variants.map((v, i) => {
                const p = draft.prices[i];
                const fresh = p.preview?.key === priceKey(p);
                return (
                  <div key={i} className="cat-variant">
                    <div className="cat-variant__head">{v.label || `Variante ${i + 1}`}<span className="cat-formbar__spacer" />
                      <div className="cat-seg cat-seg--sm" role="radiogroup" aria-label={`Mode de prix — ${v.label || `variante ${i + 1}`}`}>
                        {(["none", ...(coefficientAllowed ? ["coefficient"] : []), ...(access.pricingManual ? ["manual"] : [])] as PriceMode[]).map((mode) => (
                          <button key={mode} type="button" role="radio" aria-checked={p.mode === mode} className={cn("cat-seg__item", p.mode === mode && "is-active")}
                            onClick={() => setPrice(i, { mode })}>
                            {mode === "none" ? "Plus tard" : mode === "coefficient" ? "Coefficient" : "Prix manuel"}
                          </button>
                        ))}
                      </div>
                    </div>
                    {p.mode === "coefficient" && (
                      <div className="cat-grid cat-grid--3">
                        <Field label="Prix d'achat HT" name={`variants[${i}].price.purchasePriceHt`} value={p.purchase} onChange={(purchase) => setPrice(i, { purchase })} inputMode="decimal"
                          suffix={currency} required error={message(`variants[${i}].price.purchasePriceHt`)} hint="Jusqu'à 4 décimales." />
                        <Field label="Coefficient" name={`variants[${i}].price.coefficient`} value={p.coefficient} onChange={(coefficient) => setPrice(i, { coefficient })} inputMode="decimal"
                          required error={message(`variants[${i}].price.coefficient`)} hint="Ex. 2,5" />
                        <div className="clients-field">
                          <span className="clients-field__label">Aperçu du prix de vente HT</span>
                          <div className="cat-preview">
                            <span className={cn("cat-strong", !fresh && "clients-muted")}>{fresh && p.preview ? formatMoney(p.preview.value, currency) : "—"}</span>
                            <Button size="sm" onClick={() => runPreview(i)} disabled={preview.isPending}><Calculator size={14} aria-hidden="true" />{fresh ? "Recalculer" : "Calculer"}</Button>
                          </div>
                          {message(`variants[${i}].price.expectedSalePriceHt`) && <p className="clients-field__error">{message(`variants[${i}].price.expectedSalePriceHt`)}</p>}
                        </div>
                        {fresh && p.preview?.belowCost && (
                          <label className="cat-check cat-span2"><input type="checkbox" checked={p.acceptBelowCost} onChange={(e) => setPrice(i, { acceptBelowCost: e.target.checked })} />
                            Coefficient inférieur à 1 : je confirme une vente sous le prix d&apos;achat.</label>
                        )}
                      </div>
                    )}
                    {p.mode === "manual" && (
                      <div className="cat-grid cat-grid--3">
                        <Field label="Prix de vente HT" name={`variants[${i}].price.salePriceHt`} value={p.manual} onChange={(manual) => setPrice(i, { manual })} inputMode="decimal"
                          suffix={currency} required error={message(`variants[${i}].price.salePriceHt`)} />
                        {access.costsWrite && (
                          <Field label="Prix d'achat HT (facultatif)" name={`variants[${i}].price.purchasePriceHt`} value={p.purchase} onChange={(purchase) => setPrice(i, { purchase })}
                            inputMode="decimal" suffix={currency} errors={errors} />
                        )}
                        <Field label="Motif du prix manuel" name={`variants[${i}].price.reason`} value={p.reason} onChange={(reason) => setPrice(i, { reason })} errors={errors}
                          placeholder="Ex. prix catalogue fournisseur" />
                      </div>
                    )}
                    {p.mode === "none" && <p className="clients-muted" style={{ margin: 0 }}>Prix fixé plus tard dans la fiche produit (la variante restera « prix incomplet »).</p>}
                  </div>
                );
              })}
            </div>
          </Card>
        )}

        {draft.step === 4 && (
          <>
            <Card title="Stock initial" icon={Boxes}>
              {stockedIndexes.length === 0 ? (
                <p className="clients-muted" style={{ margin: 0 }}>Aucune variante stockée : pas de stock initial (articles commandés à la demande ou prestations).</p>
              ) : !access.stockMovementsWrite ? (
                <Lock>Saisie du stock réservée au droit stock.movements.write : il entrera plus tard par une réception ou une entrée motivée.</Lock>
              ) : (
                <TableScroll label="Stock initial par variante">
                  <table className="clients-table cat-table cat-table--inner cat-table--compact">
                    <thead><tr><th scope="col">Variante</th><th scope="col">Emplacement</th><th scope="col">Quantité initiale</th></tr></thead>
                    <tbody>{stockedIndexes.map((i) => (
                      <tr key={i}>
                        <td>{draft.variants[i].label || `Variante ${i + 1}`}</td>
                        <td>
                          <SelectField label={`Emplacement — ${draft.variants[i].label}`} name={`variants[${i}].stock.locationId`} value={draft.stocks[i].locationId} errors={errors}
                            onChange={(locationId) => setStock(i, { locationId })} options={activeLocations.map((l) => ({ value: l.id, label: l.name }))} />
                        </td>
                        <td>
                          <Field label={`Quantité — ${draft.variants[i].label}`} name={`variants[${i}].stock.quantity`} value={draft.stocks[i].quantity} inputMode="numeric"
                            onChange={(quantity) => setStock(i, { quantity })} error={message(`variants[${i}].stock.quantity`)} placeholder="0" />
                        </td>
                      </tr>
                    ))}</tbody>
                  </table>
                </TableScroll>
              )}
            </Card>
            <Card title="Résumé avant création" icon={ClipboardCheck}>
              <div className="cat-stack">
                <KeyValues rows={[
                  ["Modèle", <span key="m" className="cat-strong">{draft.name || "—"}</span>],
                  ["Famille", family?.label ?? draft.familyCode],
                  ["Marque", draft.brand || "—"],
                  ["Fournisseur principal", draft.supplier ? `${draft.supplier.name} (${draft.supplier.code})` : "Aucun — fiche incomplète"],
                  ["Magasin", store ? `${store.name} · ${store.currency}` : "—"],
                ]} />
                <TableScroll label="Variantes à créer">
                  <table className="clients-table cat-table cat-table--inner cat-table--compact">
                    <thead><tr><th scope="col">Variante</th><th scope="col">SKU</th><th scope="col">Politique</th><th scope="col" className="cat-num">Prix de vente HT</th><th scope="col" className="cat-num">Stock initial</th></tr></thead>
                    <tbody>{draft.variants.map((v, i) => {
                      const p = draft.prices[i];
                      const sale = p.mode === "coefficient" ? p.preview?.value ?? null : p.mode === "manual" ? decimalInput(p.manual) : null;
                      const quantity = v.stockPolicy === "stocked" && access.stockMovementsWrite ? Number(draft.stocks[i].quantity || "0") : 0;
                      return (
                        <tr key={i}>
                          <td>{v.label}</td>
                          <td className="cat-mono">{v.skuCustom ? v.sku : <span className="clients-muted">{skuPrefix(draft.familyCode)}-… (auto)</span>}</td>
                          <td><Badge tone="arch">{POLICY_LABELS[v.stockPolicy as keyof typeof POLICY_LABELS] ?? v.stockPolicy}</Badge></td>
                          <td className="cat-num">{sale ? formatMoney(sale, currency) : <span className="clients-muted">plus tard</span>}</td>
                          <td className="cat-num">{v.stockPolicy === "stocked" ? quantity : <span className="clients-muted">sans stock</span>}</td>
                        </tr>
                      );
                    })}</tbody>
                  </table>
                </TableScroll>
                <p className="clients-field__hint" style={{ margin: 0 }}>
                  <Sparkles size={12} aria-hidden="true" /> Tout est créé en une seule opération : si un élément est refusé, rien n&apos;est enregistré et vos saisies restent dans l&apos;assistant.
                </p>
              </div>
            </Card>
          </>
        )}
      </div>
      <FormBar dirty={dirty} hint="Les champs marqués * sont obligatoires · rien n'est enregistré avant la confirmation finale">
        <Button onClick={close}><X size={16} aria-hidden="true" />Annuler</Button>
        {draft.step > 0 && <Button onClick={() => go(draft.step - 1)} disabled={write.isPending}><ChevronLeft size={16} aria-hidden="true" />Précédent</Button>}
        {last ? (
          <Button variant="primary" disabled={write.isPending} onClick={submit}><Save size={16} aria-hidden="true" />{write.isPending ? "Création…" : "Créer le produit"}</Button>
        ) : (
          <Button variant="primary" onClick={() => go(draft.step + 1)}>Suivant<ChevronRight size={16} aria-hidden="true" /></Button>
        )}
      </FormBar>
      <UnsavedModal open={guard.pending !== null} onLeave={guard.confirm} onStay={guard.cancel} onSave={() => { guard.cancel(); if (last) submit(); else go(WIZARD_STEPS.length - 1); }} saving={write.isPending} />
    </section>
  );
}
