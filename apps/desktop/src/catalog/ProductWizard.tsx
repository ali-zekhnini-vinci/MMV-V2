// Assistant de création produit en cinq étapes (COR-05-06, revu UX-IA 03/11/2026), dans la composition du Lot 6
// (cartes, grilles à deux colonnes, barre d'actions en pied, étapes .cat-steps) :
// 1 Informations générales · 2 Variantes · 3 Fournisseurs · 4 Tarification · 5 Stock, seuil et validation.
// Règles : Précédent / Suivant, progression visible, contrôle propre à chaque étape, saisies conservées entre étapes et
// en BROUILLON DE SESSION (mémoire du poste, jamais sur disque — les coûts n'y sont pas persistés) repris si l'on revient ;
// SKU généré et garanti unique par le serveur ; fournisseurs facultatifs (plusieurs, un principal) ; TARIF OBLIGATOIRE pour
// chaque variante (coefficient avec aperçu serveur, ou prix manuel — plus de « Plus tard ») ; quantité initiale par
// emplacement et seuil d'alerte par variante pour le magasin (jamais pour une prestation ou un article commandé) ; aucun
// calcul monétaire ici. Enregistrement final : UNE requête `POST /api/catalog/products`, tout ou rien, `requestId`
// conservé pour la réémission (aucun doublon).
import { Bell, Boxes, Building2, Calculator, Check, ChevronLeft, ChevronRight, ClipboardCheck, Coins, Info, Layers, Plus, Save, Sparkles, Star, Trash2, X } from "lucide-react";
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

type PriceMode = "coefficient" | "manual";

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
  threshold: string;
}

/** Fournisseur du produit : référence et prix fournisseur HT par variante (même ordre que les variantes). */
interface SupplierDraft {
  supplier: SupplierChoice | null;
  leadTimeDays: string;
  currency: string;
  references: string[];
  prices: string[];
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
  suppliers: SupplierDraft[];
  primary: number;
  prices: PriceDraft[];
  stocks: StockDraft[];
}

const MAX_THRESHOLD = 100_000;

const emptyPrice = (mode: PriceMode): PriceDraft => ({ mode, purchase: "", coefficient: "", manual: "", reason: "", acceptBelowCost: false, preview: null });

const emptyStock = (locationId: string): StockDraft => ({ locationId, quantity: "", threshold: "" });

const emptySupplier = (variants: number, currency: string): SupplierDraft => ({
  supplier: null, leadTimeDays: "", currency, references: Array<string>(variants).fill(""), prices: Array<string>(variants).fill(""),
});

const priceKey = (price: PriceDraft) => `${decimalInput(price.purchase) ?? ""}|${decimalInput(price.coefficient) ?? ""}`;

const isWhole = (text: string, max: number) => /^\d{1,6}$/.test(text.trim()) && Number(text.trim()) <= max;

function freshDraft(defaultLocation: string, mode: PriceMode): WizardDraft {
  return {
    requestId: newRequestId(), step: 0, familyCode: "optical_frames", brand: "", name: "", description: "", lensRangeId: "", modelAttributes: {},
    variants: [emptyVariant("stocked")], suppliers: [], primary: 0, prices: [emptyPrice(mode)], stocks: [emptyStock(defaultLocation)],
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
  const activeLocations = useMemo(() => store?.locations.filter((l) => l.status === "active") ?? [], [store]);
  const defaultLocation = activeLocations.find((l) => l.isDefault)?.id ?? activeLocations[0]?.id ?? "";
  const coefficientAllowed = access.pricingWrite && access.costsRead;
  const priceAllowed = coefficientAllowed || access.pricingManual;
  const defaultMode: PriceMode = coefficientAllowed || !access.pricingManual ? "coefficient" : "manual";
  const [resumed] = useState(() => drafts.has(userId));
  // Étape de reprise du brouillon : le bandeau « Recommencer » y reste visible (et non seulement à l'étape 1).
  const [resumedStep] = useState(() => drafts.get(userId)?.step ?? 0);
  const [draft, setDraft] = useState<WizardDraft>(() => drafts.get(userId) ?? freshDraft(defaultLocation, defaultMode));
  const [tried, setTried] = useState<number | null>(null);
  // Verres : fournisseurs ayant déclaré une offre pour la gamme choisie (catalogue des verres), proposés à l'étape 3.
  const lensOffers = useCatalogQuery<LensOffer[]>(draft.familyCode === "ophthalmic_lenses" && draft.lensRangeId !== "" && access.suppliersRead
    ? `/api/catalog/lens-offers?rangeId=${draft.lensRangeId}` : null);
  const family = families.find((f) => f.code === draft.familyCode);
  const errors = write.error?.error.fieldErrors;
  const dirty = draft.name !== "" || draft.brand !== "" || draft.variants.some((v) => v.label !== "") || draft.suppliers.length > 0;
  const guard = useUnsavedGuard(dirty && !write.isSuccess);
  const currency = store?.currency ?? "";
  const stocked = (i: number) => draft.variants[i]?.stockPolicy === "stocked";
  const stockedIndexes = draft.variants.map((_, i) => (stocked(i) ? i : -1)).filter((i) => i >= 0);
  const variantName = (i: number) => draft.variants[i]?.label.trim() || `Variante ${i + 1}`;
  /** Fournisseurs effectivement choisis (lignes vides écartées), avec leur fiche. */
  const chosenSuppliers = draft.suppliers.flatMap((entry) => (entry.supplier ? [{ entry, supplier: entry.supplier }] : []));

  // Brouillon de session : chaque changement est mémorisé (jusqu'à la création ou à « Recommencer »).
  useEffect(() => {
    if (userId !== "" && !write.isSuccess) drafts.set(userId, draft);
  }, [draft, userId, write.isSuccess]);

  const patch = (next: Partial<WizardDraft>) => setDraft((current) => ({ ...current, ...next }));
  const setVariant = (index: number, next: VariantDraft) => patch({ variants: draft.variants.map((v, i) => (i === index ? next : v)) });
  const setPrice = (index: number, next: Partial<PriceDraft>) => patch({ prices: draft.prices.map((p, i) => (i === index ? { ...p, ...next } : p)) });
  const setStock = (index: number, next: Partial<StockDraft>) => patch({ stocks: draft.stocks.map((s, i) => (i === index ? { ...s, ...next } : s)) });
  const setSupplier = (index: number, next: Partial<SupplierDraft>) => patch({ suppliers: draft.suppliers.map((s, i) => (i === index ? { ...s, ...next } : s)) });
  const setSupplierCell = (index: number, field: "references" | "prices", variant: number, value: string) =>
    setSupplier(index, { [field]: draft.suppliers[index][field].map((x, j) => (j === variant ? value : x)) });

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
    suppliers: draft.suppliers.map((s) => ({ ...s, references: [...s.references, ""], prices: [...s.prices, ""] })),
    prices: [...draft.prices, emptyPrice(defaultMode)], stocks: [...draft.stocks, emptyStock(defaultLocation)],
  });
  const removeVariant = (index: number) => patch({
    variants: draft.variants.filter((_, i) => i !== index),
    suppliers: draft.suppliers.map((s) => ({ ...s, references: s.references.filter((_, i) => i !== index), prices: s.prices.filter((_, i) => i !== index) })),
    prices: draft.prices.filter((_, i) => i !== index), stocks: draft.stocks.filter((_, i) => i !== index),
  });
  const addSupplier = (choice: SupplierChoice | null = null) => patch({
    suppliers: [...draft.suppliers, { ...emptySupplier(draft.variants.length, currency), supplier: choice }],
    primary: draft.suppliers.length === 0 ? 0 : draft.primary,
  });
  const removeSupplier = (index: number) => patch({
    suppliers: draft.suppliers.filter((_, i) => i !== index),
    primary: draft.primary === index ? 0 : draft.primary > index ? draft.primary - 1 : draft.primary,
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
    if (step === 2) {
      const ids = new Set<string>();
      draft.suppliers.forEach((s, j) => {
        if (!s.supplier) problems[`suppliers[${j}]`] = "Choisissez le fournisseur ou retirez la ligne.";
        else if (ids.has(s.supplier.id)) problems[`suppliers[${j}]`] = `${s.supplier.name} figure deux fois.`;
        else ids.add(s.supplier.id);
        if (s.leadTimeDays.trim() !== "" && !/^\d{1,3}$/.test(s.leadTimeDays.trim())) problems[`suppliers[${j}].leadTimeDays`] = "Délai : nombre de jours entier (0 à 999).";
        if (access.costsWrite) {
          s.prices.forEach((p, i) => {
            if (p.trim() !== "" && !/^\d{1,9}([.,]\d{1,4})?$/.test(p.trim().replace(/\s/g, ""))) problems[`variants[${i}].suppliers[${j}].supplierPriceHt`] = `Prix fournisseur HT invalide (${variantName(i)}).`;
          });
          if (s.prices.some((p) => p.trim() !== "") && !/^[A-Za-z]{3}$/.test(s.currency.trim())) problems[`suppliers[${j}].currency`] = "Devise du prix fournisseur : code à trois lettres (ex. EUR).";
        }
      });
    }
    if (step === 3) {
      if (!priceAllowed) problems["variants[0].price"] = "Votre profil ne permet pas de fixer un prix de vente : la création d'un produit exige un tarif. Demandez à un responsable.";
      draft.prices.forEach((p, i) => {
        if (p.mode === "coefficient") {
          if (decimalInput(p.purchase) === null) problems[`variants[${i}].price.purchasePriceHt`] = `Prix d'achat HT requis (${variantName(i)}).`;
          if (decimalInput(p.coefficient) === null) problems[`variants[${i}].price.coefficient`] = `Coefficient requis (${variantName(i)}).`;
          else if (p.preview?.key !== priceKey(p)) problems[`variants[${i}].price.expectedSalePriceHt`] = `Calculez l'aperçu du prix de vente (${variantName(i)}).`;
          else if (p.preview.belowCost && !p.acceptBelowCost) problems[`variants[${i}].price.acceptBelowCost`] = `Prix sous le prix d'achat : confirmation requise (${variantName(i)}).`;
        }
        if (p.mode === "manual" && decimalInput(p.manual) === null) problems[`variants[${i}].price.salePriceHt`] = `Prix de vente HT requis (${variantName(i)}).`;
      });
    }
    if (step === 4) {
      stockedIndexes.forEach((i) => {
        const s = draft.stocks[i];
        if (s.quantity.trim() !== "" && !isWhole(s.quantity, 100_000)) problems[`variants[${i}].stock.quantity`] = `Quantité : entier positif (${variantName(i)}).`;
        if (s.quantity.trim() !== "" && Number(s.quantity) > 0 && s.locationId === "") problems[`variants[${i}].stock.locationId`] = `Choisissez l'emplacement (${variantName(i)}).`;
        if (s.threshold.trim() !== "" && !isWhole(s.threshold, MAX_THRESHOLD)) problems[`variants[${i}].lowStockThreshold`] = `Seuil : entier positif ou nul (${variantName(i)}).`;
      });
    }
    return problems;
  };
  const problems = tried === draft.step ? stepProblems(draft.step) : {};
  const message = (name: string) => problems[name] ?? fieldError(errors, name) ?? undefined;

  // Prix d'achat repris du fournisseur principal (même devise que le magasin) quand il n'est pas encore saisi.
  const withPurchaseFromSupplier = (prices: PriceDraft[]) => {
    const main = draft.suppliers[draft.primary];
    if (!coefficientAllowed || !main?.supplier || main.currency.trim().toUpperCase() !== currency) return prices;
    return prices.map((p, i) => (p.mode === "coefficient" && p.purchase.trim() === "" && (main.prices[i] ?? "").trim() !== "" ? { ...p, purchase: main.prices[i].trim() } : p));
  };

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
    patch({ step, ...(step === 3 && draft.step < 3 ? { prices: withPurchaseFromSupplier(draft.prices) } : {}) });
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
            suppliers: chosenSuppliers.map(({ entry, supplier }) => {
              const supplierPrice = access.costsWrite ? decimalInput(entry.prices[i] ?? "") : null;
              return {
                supplierId: supplier.id, supplierReference: orNull(entry.references[i] ?? ""), isPrimary: entry === draft.suppliers[draft.primary],
                leadTimeDays: entry.leadTimeDays.trim() === "" ? null : Number(entry.leadTimeDays),
                ...(supplierPrice ? { supplierPriceHt: supplierPrice, supplierCurrency: entry.currency.trim().toUpperCase() } : {}),
              };
            }),
            // Toujours un tarif (obligatoire) : le serveur refuse la variante qui n'en a pas.
            price: p.mode === "coefficient"
              ? { mode: "coefficient", purchasePriceHt: decimalInput(p.purchase), coefficient: decimalInput(p.coefficient), expectedSalePriceHt: p.preview?.value, acceptBelowCost: p.acceptBelowCost }
              : { mode: "manual", salePriceHt: decimalInput(p.manual), reason: orNull(p.reason), ...(access.costsWrite && decimalInput(p.purchase) ? { purchasePriceHt: decimalInput(p.purchase) } : {}) },
            ...(v.stockPolicy === "stocked" && access.stockMovementsWrite && quantity > 0 ? { stock: { locationId: s.locationId, quantity } } : {}),
            ...(v.stockPolicy === "stocked" && access.stockAdjust && s.threshold.trim() !== "" ? { lowStockThreshold: Number(s.threshold) } : {}),
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
    setDraft(freshDraft(defaultLocation, defaultMode));
  };
  const close = () => guard.ask(() => { drafts.delete(userId); navigate("/produits"); });
  const last = draft.step === WIZARD_STEPS.length - 1;
  const saleOf = (i: number) => {
    const p = draft.prices[i];
    return p.mode === "coefficient" ? (p.preview?.key === priceKey(p) ? p.preview.value : null) : decimalInput(p.manual);
  };
  const offerSuppliers = [...new Map((lensOffers.data ?? []).filter((o) => o.status === "active").map((o) => [o.supplierId, o.supplierName])).entries()]
    .filter(([id]) => !draft.suppliers.some((s) => s.supplier?.id === id));

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
        {resumed && draft.step === resumedStep && !write.error && (
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
            {Object.keys(problems).length > 1 && ` (+ ${Object.keys(problems).length - 1} autre${Object.keys(problems).length > 2 ? "s" : ""} point${Object.keys(problems).length > 2 ? "s" : ""} signalé${Object.keys(problems).length > 2 ? "s" : ""} ci-dessous)`}
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
                  hint="Les plages de fabrication et restrictions de chaque fournisseur se consultent dans le catalogue des verres."
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
                Une variante par coloris, calibre ou conditionnement ; chacune aura son prix, son stock et son seuil. SKU {skuPrefix(draft.familyCode)}-… généré et garanti unique par le serveur (personnalisable) ; code-barres facultatif.
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
          <Card title={`Fournisseurs (${chosenSuppliers.length})`} icon={Building2}
            actions={access.suppliersRead && <Button size="sm" onClick={() => addSupplier()}><Plus size={16} aria-hidden="true" />Ajouter un fournisseur</Button>}>
            {access.suppliersRead ? (
              <div className="cat-stack">
                <p className="clients-field__hint" style={{ margin: 0 }}>
                  Facultatif : une fiche sans fournisseur reste possible (signalée « incomplète »). Plusieurs fournisseurs possibles ; le fournisseur principal sert aux commandes.
                </p>
                {offerSuppliers.length > 0 && (
                  <div className="cat-checks">
                    <span className="clients-field__hint">Fournisseurs proposant cette gamme :</span>
                    {offerSuppliers.map(([id, supplierName]) => (
                      <Button key={id} size="sm" onClick={() => addSupplier({ id, code: "", name: supplierName })}><Plus size={14} aria-hidden="true" />{supplierName}</Button>
                    ))}
                  </div>
                )}
                {draft.suppliers.length === 0 && (
                  <p className="clients-muted" style={{ margin: 0 }}>Aucun fournisseur rattaché. Utilisez « Ajouter un fournisseur » ou passez à l&apos;étape suivante.</p>
                )}
                {draft.suppliers.map((entry, j) => (
                  <div key={j} className="cat-variant">
                    <div className="cat-variant__head">
                      {entry.supplier?.name ?? `Fournisseur ${j + 1}`}
                      {draft.primary === j && entry.supplier && <Badge tone="accent" icon={Star}>Principal</Badge>}
                      <span className="cat-formbar__spacer" />
                      {draft.primary !== j && entry.supplier && <Button size="sm" onClick={() => patch({ primary: j })}><Star size={14} aria-hidden="true" />Définir principal</Button>}
                      <Button size="sm" variant="danger" onClick={() => removeSupplier(j)}><Trash2 size={14} aria-hidden="true" />Retirer</Button>
                    </div>
                    <div className="cat-grid cat-grid--3">
                      <div className="cat-span2">
                        <SupplierCombobox label="Fournisseur" value={entry.supplier} onChange={(supplier) => setSupplier(j, { supplier })} required
                          error={problems[`suppliers[${j}]`] ?? null} />
                      </div>
                      <Field label="Délai indicatif (jours)" name={`suppliers[${j}].leadTimeDays`} value={entry.leadTimeDays} onChange={(leadTimeDays) => setSupplier(j, { leadTimeDays })}
                        inputMode="numeric" error={message(`suppliers[${j}].leadTimeDays`)} hint="Facultatif." />
                      {access.costsWrite && (
                        <Field label="Devise des prix fournisseur" name={`suppliers[${j}].currency`} value={entry.currency} error={message(`suppliers[${j}].currency`)}
                          onChange={(value) => setSupplier(j, { currency: value.toUpperCase().slice(0, 3) })} hint="Aucune conversion." />
                      )}
                    </div>
                    {entry.supplier && ((chosen) => (
                      <TableScroll label={`Références chez ${chosen.name}`}>
                        <table className="clients-table cat-table cat-table--inner cat-table--compact">
                          <thead><tr>
                            <th scope="col">Variante</th><th scope="col">Référence fournisseur</th>
                            {access.costsWrite && <th scope="col">Prix fournisseur HT</th>}
                          </tr></thead>
                          <tbody>{draft.variants.map((_, i) => (
                            <tr key={i}>
                              <td>{variantName(i)}</td>
                              <td>
                                <Field label="Référence fournisseur" name={`variants[${i}].suppliers[${j}].supplierReference`} value={entry.references[i] ?? ""}
                                  onChange={(value) => setSupplierCell(j, "references", i, value)} errors={errors} placeholder="Ex. 0045-HB (facultative)" />
                              </td>
                              {access.costsWrite && (
                                <td>
                                  <Field label="Prix fournisseur HT" name={`variants[${i}].suppliers[${j}].supplierPriceHt`} value={entry.prices[i] ?? ""} inputMode="decimal"
                                    onChange={(value) => setSupplierCell(j, "prices", i, value)} error={message(`variants[${i}].suppliers[${j}].supplierPriceHt`)} suffix={entry.currency || undefined}
                                    placeholder="Facultatif" />
                                </td>
                              )}
                            </tr>
                          ))}</tbody>
                        </table>
                      </TableScroll>
                    ))(entry.supplier)}
                  </div>
                ))}
              </div>
            ) : <Lock>Rattachement fournisseur réservé au droit suppliers.read ; la fiche pourra être complétée plus tard.</Lock>}
          </Card>
        )}

        {draft.step === 3 && (
          <Card title={`Tarification — magasin ${store?.name ?? ""} (${currency})`} icon={Coins}
            actions={draft.variants.length > 1 && priceAllowed && (
              <Button size="sm" onClick={copyFirstPrice}>Appliquer le tarif de la variante 1 à toutes</Button>
            )}>
            <div className="cat-stack">
              <Banner tone="info" icon={Calculator} title="Obligatoire : chaque variante doit avoir un prix de vente HT" compact>
                Mode coefficient : prix de vente HT = prix d&apos;achat HT × coefficient, calculé et arrondi au centime par le serveur (aperçu requis). Mode manuel : prix saisi,
                réservé au droit pricing.manual. Devise du magasin ({currency}), aucune conversion ; aucun TTC tant que la fiscalité du magasin n&apos;est pas validée.
              </Banner>
              {!priceAllowed ? (
                <Lock large>Votre profil ne permet pas de fixer un prix de vente. La création d&apos;un produit exige un tarif pour chaque variante : demandez à un responsable de créer ce produit.</Lock>
              ) : draft.variants.map((_, i) => {
                const p = draft.prices[i];
                const fresh = p.preview?.key === priceKey(p);
                const modes: PriceMode[] = [...(coefficientAllowed ? ["coefficient" as const] : []), ...(access.pricingManual ? ["manual" as const] : [])];
                return (
                  <div key={i} className="cat-variant">
                    <div className="cat-variant__head">{variantName(i)}<span className="cat-formbar__spacer" />
                      {modes.length > 1 ? (
                        <div className="cat-seg cat-seg--sm" role="radiogroup" aria-label={`Mode de prix — ${variantName(i)}`}>
                          {modes.map((mode) => (
                            <button key={mode} type="button" role="radio" aria-checked={p.mode === mode} className={cn("cat-seg__item", p.mode === mode && "is-active")}
                              onClick={() => setPrice(i, { mode })}>
                              {mode === "coefficient" ? "Coefficient" : "Prix manuel"}
                            </button>
                          ))}
                        </div>
                      ) : <Badge tone="arch">{modes[0] === "coefficient" ? "Coefficient" : "Prix manuel"}</Badge>}
                    </div>
                    {p.mode === "coefficient" && coefficientAllowed && (
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
                    {p.mode === "manual" && access.pricingManual && (
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
                    {message(`variants[${i}].price`) && <p className="clients-field__error">{message(`variants[${i}].price`)}</p>}
                  </div>
                );
              })}
            </div>
          </Card>
        )}

        {draft.step === 4 && (
          <>
            <Card title="Stock initial et seuil d'alerte" icon={Boxes}>
              <div className="cat-stack">
                {stockedIndexes.length === 0 ? (
                  <p className="clients-muted" style={{ margin: 0 }}>Aucune variante stockée : ni stock initial ni seuil (articles commandés à la demande ou prestations).</p>
                ) : (
                  <>
                    <p className="clients-field__hint" style={{ margin: 0 }}>
                      <Bell size={12} aria-hidden="true" /> Le seuil vaut pour tout le magasin {store?.name ?? ""} : l&apos;alerte « stock faible » apparaît quand le stock disponible
                      (en main moins réservé, tous emplacements actifs confondus) atteint ou passe sous le seuil. Champs facultatifs.
                    </p>
                    {!access.stockMovementsWrite && <Lock>Quantité initiale réservée au droit stock.movements.write : le stock entrera plus tard par une réception.</Lock>}
                    {!access.stockAdjust && <Lock>Seuil d&apos;alerte réservé au droit stock.adjust : il pourra être fixé plus tard dans la fiche produit.</Lock>}
                    {(access.stockMovementsWrite || access.stockAdjust) && (
                      <TableScroll label="Stock initial et seuil par variante">
                        <table className="clients-table cat-table cat-table--inner cat-table--compact">
                          <thead><tr>
                            <th scope="col">Variante</th>
                            {access.stockMovementsWrite && <><th scope="col">Emplacement</th><th scope="col">Quantité initiale</th></>}
                            {access.stockAdjust && <th scope="col">Seuil d&apos;alerte (magasin)</th>}
                          </tr></thead>
                          <tbody>{stockedIndexes.map((i) => (
                            <tr key={i}>
                              <td>{variantName(i)}</td>
                              {access.stockMovementsWrite && (
                                <>
                                  <td>
                                    {activeLocations.length === 1 ? <span className="cat-secondary">{activeLocations[0].name}</span> : (
                                      <SelectField label="Emplacement" name={`variants[${i}].stock.locationId`} value={draft.stocks[i].locationId} errors={errors}
                                        onChange={(locationId) => setStock(i, { locationId })} options={activeLocations.map((l) => ({ value: l.id, label: l.name }))} />
                                    )}
                                  </td>
                                  <td>
                                    <Field label="Quantité initiale" name={`variants[${i}].stock.quantity`} value={draft.stocks[i].quantity} inputMode="numeric"
                                      onChange={(quantity) => setStock(i, { quantity })} error={message(`variants[${i}].stock.quantity`)} placeholder="0" suffix="unités" />
                                  </td>
                                </>
                              )}
                              {access.stockAdjust && (
                                <td>
                                  <Field label="Seuil d'alerte" name={`variants[${i}].lowStockThreshold`} value={draft.stocks[i].threshold} inputMode="numeric"
                                    onChange={(threshold) => setStock(i, { threshold })} error={message(`variants[${i}].lowStockThreshold`)} placeholder="Non défini" suffix="unités" />
                                </td>
                              )}
                            </tr>
                          ))}</tbody>
                        </table>
                      </TableScroll>
                    )}
                  </>
                )}
                {stockedIndexes.length > 0 && stockedIndexes.length < draft.variants.length && (
                  <p className="clients-muted" style={{ margin: 0 }}>
                    Sans stock : {draft.variants.map((v, i) => (stocked(i) ? null : `${variantName(i)} (${POLICY_LABELS[v.stockPolicy as keyof typeof POLICY_LABELS] ?? v.stockPolicy})`)).filter(Boolean).join(", ")}.
                  </p>
                )}
              </div>
            </Card>
            <Card title="Récapitulatif avant création" icon={ClipboardCheck}>
              <div className="cat-stack">
                <KeyValues rows={[
                  ["Modèle", <span key="m" className="cat-strong">{draft.name || "—"}</span>],
                  ["Famille", family?.label ?? draft.familyCode],
                  ["Marque", draft.brand || "—"],
                  ["Description", draft.description.trim() || "—"],
                  ["Fournisseurs", chosenSuppliers.length === 0 ? "Aucun — fiche incomplète"
                    : chosenSuppliers.map(({ entry, supplier }) => `${supplier.name}${entry === draft.suppliers[draft.primary] ? " (principal)" : ""}${entry.leadTimeDays.trim() ? ` · ${entry.leadTimeDays.trim()} j` : ""}`).join(" · ")],
                  ["Magasin", store ? `${store.name} · ${store.currency}` : "—"],
                ]} />
                <TableScroll label="Variantes à créer">
                  <table className="clients-table cat-table cat-table--inner cat-table--compact">
                    <thead><tr>
                      <th scope="col">Variante</th><th scope="col">SKU</th><th scope="col">Politique</th><th scope="col" className="cat-num">Prix de vente HT</th>
                      <th scope="col" className="cat-num">Stock initial</th><th scope="col" className="cat-num">Seuil d&apos;alerte</th>
                    </tr></thead>
                    <tbody>{draft.variants.map((v, i) => {
                      const sale = saleOf(i);
                      const quantity = stocked(i) && access.stockMovementsWrite ? Number(draft.stocks[i].quantity || "0") : 0;
                      const location = activeLocations.find((l) => l.id === draft.stocks[i].locationId)?.name;
                      return (
                        <tr key={i}>
                          <td>{variantName(i)}</td>
                          <td className="cat-mono">{v.skuCustom ? v.sku : <span className="clients-muted">{skuPrefix(draft.familyCode)}-… (auto)</span>}</td>
                          <td><Badge tone="arch">{POLICY_LABELS[v.stockPolicy as keyof typeof POLICY_LABELS] ?? v.stockPolicy}</Badge></td>
                          <td className="cat-num">{sale ? formatMoney(sale, currency) : <span className="cat-tone-err">manquant</span>}</td>
                          <td className="cat-num">{stocked(i) ? <>{quantity}{quantity > 0 && location && activeLocations.length > 1 && <div className="cat-sub">{location}</div>}</> : <span className="clients-muted">sans stock</span>}</td>
                          <td className="cat-num">{stocked(i) ? (draft.stocks[i].threshold.trim() !== "" && access.stockAdjust ? draft.stocks[i].threshold.trim() : <span className="clients-muted">non défini</span>) : <span className="clients-muted">—</span>}</td>
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
          <Button variant="primary" disabled={write.isPending || !priceAllowed} onClick={submit}><Save size={16} aria-hidden="true" />{write.isPending ? "Création…" : "Créer le produit"}</Button>
        ) : (
          <Button variant="primary" onClick={() => go(draft.step + 1)}>Suivant<ChevronRight size={16} aria-hidden="true" /></Button>
        )}
      </FormBar>
      <UnsavedModal open={guard.pending !== null} onLeave={guard.confirm} onStay={guard.cancel} onSave={() => { guard.cancel(); if (last) submit(); else go(WIZARD_STEPS.length - 1); }} saving={write.isPending} />
    </section>
  );
}
