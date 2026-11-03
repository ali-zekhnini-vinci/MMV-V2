// Catalogue des verres — parcours « Nouvelle gamme » (UX-IA 03/11/2026), même composition que l'assistant produit
// (étapes .cat-steps, cartes, barre d'actions en pied) : 1 Fabricant et gamme · 2 Type de verre · 3 Indice, matériau et
// traitements · 4 Fournisseurs · 5 Plages et restrictions (mode simple par défaut, mode avancé à la demande) · 6 Prix,
// délais et validation. Une gamme et PLUSIEURS fournisseurs créés ensemble, en UNE requête `POST /api/catalog/lens-catalog`
// (tout ou rien). Les plages, restrictions, prix et délais appartiennent à l'OFFRE de chaque fournisseur, jamais à la gamme ;
// aucune règle clinique inventée. `?gamme=<id>` : ajout de fournisseurs à une gamme existante (étape 1 en lecture seule).
import { Check, ChevronLeft, ChevronRight, ClipboardCheck, Coins, Factory, FlaskConical, Info, Layers, Plus, Ruler, Save, Settings2, Sparkles, Trash2, X } from "lucide-react";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { type LensReferences, newRequestId } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { errorMessage, useActiveStore, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";
import { fieldError, formatMoney } from "./catalogFormat.ts";
import { Badge, Banner, Card, ErrorState, Field, FormBar, KeyValues, Loading, Lock, PageHeader, RadioCard, SelectField, Switch, TableScroll } from "./CatalogUi.tsx";
import { decimalOrNull, ints, LENS_TYPE_LABELS, lensStepOfField, PRODUCTION_LABELS, rangeText, ruleBody, type RuleDraft } from "./lensOffers.ts";
import { RangeFields, RulesEditor } from "./LensOffersView.tsx";
import { useUnsavedGuard } from "./productForm.ts";
import { UnsavedModal } from "./ModelViews.tsx";
import { type SupplierChoice, SupplierCombobox } from "./SupplierCombobox.tsx";

const STEPS = ["Fabricant et gamme", "Type de verre", "Indice, matériau et traitements", "Fournisseurs", "Plages et restrictions", "Prix, délais et validation"] as const;

const TYPE_HINTS: Record<string, string> = {
  single_vision: "Une seule puissance", progressive: "Vision de loin à près, addition", degressive: "Vision intermédiaire et près, addition",
  bifocal: "Deux zones de vision, addition", other: "Autre conception",
};

interface Span {
  min: string;
  max: string;
}

/** Offre d'un fournisseur pour la gamme : capacités, restrictions, prix et délai PROPRES à ce fournisseur. */
interface OfferDraft {
  supplier: SupplierChoice | null;
  sph: Span;
  cyl: Span;
  add: Span;
  diameters: string;
  rules: RuleDraft[];
  complete: boolean;
  source: string;
  validFrom: string;
  validUntil: string;
  price: string;
  currency: string;
  leadTime: string;
}

const emptySpan = (): Span => ({ min: "", max: "" });

const emptyOffer = (currency: string, supplier: SupplierChoice | null = null): OfferDraft => ({
  supplier, sph: emptySpan(), cyl: emptySpan(), add: emptySpan(), diameters: "", rules: [], complete: false, source: "", validFrom: "", validUntil: "",
  price: "", currency, leadTime: "",
});

const hasAddition = (lensType: string) => lensType !== "single_vision";

export function LensRangeWizard() {
  const navigate = useNavigate();
  const toast = useToast();
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const [params] = useSearchParams();
  const refs = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  const write = useCatalogWrite<{ manufacturerId: string; rangeId: string; offerIds: string[]; replayed: boolean }>();
  const currency = store?.currency ?? "EUR";
  const existingRangeId = params.get("gamme") ?? "";
  const [requestId] = useState(newRequestId);
  const [step, setStep] = useState(0);
  const [tried, setTried] = useState<number | null>(null);
  const [manufacturerId, setManufacturerId] = useState("");
  const [manufacturerName, setManufacturerName] = useState("");
  const [rangeName, setRangeName] = useState("");
  const [lensType, setLensType] = useState("single_vision");
  const [production, setProduction] = useState("on_order");
  const [indexId, setIndexId] = useState("");
  const [material, setMaterial] = useState("");
  const [treatments, setTreatments] = useState<string[]>([]);
  const [offers, setOffers] = useState<OfferDraft[]>([emptyOffer(currency)]);
  const [advanced, setAdvanced] = useState(false);
  const errors = write.error?.error.fieldErrors;
  const dirty = rangeName !== "" || manufacturerName !== "" || offers.some((o) => o.supplier !== null);
  const guard = useUnsavedGuard(dirty && !write.isSuccess);

  if (refs.isPending) return <Loading label="Chargement du catalogue des verres…" />;
  if (refs.isError) return <ErrorState error={refs.error} title="Impossible de charger le catalogue des verres" onRetry={() => void refs.refetch()} />;
  const data = refs.data;
  const existingRange = data.ranges.find((r) => r.id === existingRangeId && r.status === "active");
  const manufacturers = data.manufacturers.filter((m) => m.status === "active");
  const activeTreatments = data.treatments.filter((t) => t.status === "active");
  const indices = data.indices.filter((i) => i.status === "active");
  const name = (id: string | null) => data.manufacturers.find((m) => m.id === id)?.name ?? "—";
  const offerName = (o: OfferDraft, j: number) => o.supplier?.name ?? `Fournisseur ${j + 1}`;
  const setOffer = (j: number, next: Partial<OfferDraft>) => setOffers((list) => list.map((o, k) => (k === j ? { ...o, ...next } : o)));

  // ---------- contrôles d'étape (le serveur reste seul juge) ----------
  const decimal = (v: string) => v.trim() === "" || /^[+-]?\d{1,2}([.,]\d{1,2})?$/.test(v.trim());
  const stepProblems = (s: number): Record<string, string> => {
    const problems: Record<string, string> = {};
    if (s === 0 && !existingRange) {
      if (manufacturerId === "" && manufacturerName.trim() === "") problems.manufacturer = "Choisissez le fabricant ou saisissez un nouveau fabricant.";
      if (rangeName.trim() === "") problems.rangeName = "Le nom de la gamme est obligatoire.";
    }
    if (s === 3) {
      const ids = new Set<string>();
      if (offers.length === 0) problems.offers = "Ajoutez au moins un fournisseur.";
      offers.forEach((o, j) => {
        if (!o.supplier) problems[`offers[${j}].supplierId`] = "Choisissez le fournisseur ou retirez la ligne.";
        else if (ids.has(o.supplier.id)) problems[`offers[${j}].supplierId`] = `${o.supplier.name} figure deux fois.`;
        else ids.add(o.supplier.id);
      });
    }
    if (s === 4) {
      offers.forEach((o, j) => {
        for (const [key, span] of [["sphere", o.sph], ["cylinder", o.cyl], ["addition", o.add]] as const) {
          if (!decimal(span.min) || !decimal(span.max)) problems[`offers[${j}].${key}Min`] = `${offerName(o, j)} : valeur en dioptries invalide (ex. −10,00).`;
          else if ((span.min.trim() === "") !== (span.max.trim() === "")) problems[`offers[${j}].${key}Min`] = `${offerName(o, j)} : renseignez le minimum ET le maximum.`;
        }
      });
    }
    if (s === 5) {
      offers.forEach((o, j) => {
        if (o.leadTime.trim() !== "" && !/^\d{1,3}$/.test(o.leadTime.trim())) problems[`offers[${j}].leadTimeDays`] = `${offerName(o, j)} : délai en jours entiers.`;
        if (access.costsWrite && o.price.trim() !== "" && !/^\d{1,9}([.,]\d{1,4})?$/.test(o.price.trim())) problems[`offers[${j}].priceHt`] = `${offerName(o, j)} : prix HT invalide.`;
      });
    }
    return problems;
  };
  const problems = tried === step ? stepProblems(step) : {};
  const message = (key: string) => problems[key] ?? fieldError(errors, key) ?? undefined;
  const go = (target: number) => {
    if (target > step) {
      for (let s = step; s < target; s += 1) {
        if (Object.keys(stepProblems(s)).length > 0) {
          setTried(s);
          setStep(s);
          return;
        }
      }
    }
    setTried(null);
    setStep(target);
  };

  const submit = () => {
    for (let s = 0; s < STEPS.length; s += 1) {
      if (Object.keys(stepProblems(s)).length > 0) {
        setTried(s);
        setStep(s);
        return;
      }
    }
    const costsEditable = access.costsWrite;
    write.mutate({
      method: "POST", path: "/api/catalog/lens-catalog",
      body: {
        requestId,
        ...(existingRange ? { rangeId: existingRange.id } : {
          rangeName: rangeName.trim(), ...(manufacturerId !== "" ? { manufacturerId } : { manufacturerName: manufacturerName.trim() }),
        }),
        offers: offers.map((o) => ({
          supplierId: o.supplier?.id, lensType, production, indexId: indexId || null, material: material.trim() || null, treatmentIds: treatments,
          leadTimeDays: o.leadTime.trim() === "" ? null : Number(o.leadTime),
          ...(costsEditable && o.price.trim() !== "" ? { priceHt: decimalOrNull(o.price), priceCurrency: o.currency.trim().toUpperCase() || currency } : {}),
          sphereMin: decimalOrNull(o.sph.min), sphereMax: decimalOrNull(o.sph.max),
          // Mode simple : plages principales seulement (SPH, et ADD pour un verre à addition) ; le reste en mode avancé.
          cylinderMin: advanced ? decimalOrNull(o.cyl.min) : null, cylinderMax: advanced ? decimalOrNull(o.cyl.max) : null,
          additionMin: hasAddition(lensType) ? decimalOrNull(o.add.min) : null, additionMax: hasAddition(lensType) ? decimalOrNull(o.add.max) : null,
          diameters: advanced ? ints(o.diameters) : [],
          rules: advanced ? o.rules.map((r) => ruleBody(r, costsEditable)) : [],
          rulesComplete: advanced && o.complete,
          sourceReference: advanced ? o.source.trim() || null : null,
          validFrom: advanced ? o.validFrom || null : null, validUntil: advanced ? o.validUntil || null : null,
          reason: "Création de l'offre",
        })),
      },
    }, {
      onSuccess: (result) => {
        toast.show(existingRange ? "Fournisseurs ajoutés" : "Gamme créée", `${existingRange?.name ?? rangeName.trim()} · ${result.offerIds.length} fournisseur${result.offerIds.length > 1 ? "s" : ""}`);
        navigate("/produits/verres");
      },
      onError: (error) => {
        const fields = Object.keys(error.error.fieldErrors ?? {});
        if (fields.length > 0) setStep(Math.min(...fields.map(lensStepOfField)));
      },
    });
  };
  const close = () => guard.ask(() => navigate("/produits/verres"));
  const last = step === STEPS.length - 1;
  const title = existingRange ? `Ajouter des fournisseurs — ${name(existingRange.manufacturerId)} ${existingRange.name}` : "Nouvelle gamme de verres";

  if (!access.catalogConfigure || !access.suppliersRead) {
    return <Lock large>La création d&apos;une gamme est réservée aux droits catalog.configure et suppliers.read.</Lock>;
  }

  return (
    <section className="cat-screen cat-form" aria-labelledby="lens-range-wizard">
      <PageHeader title={title} subtitle={`Étape ${step + 1} sur ${STEPS.length} — ${STEPS[step]}`}
        actions={<Button variant="ghost" onClick={close}><X size={18} aria-hidden="true" />Fermer</Button>} />
      <div className="cat-form__body">
        <ol className="cat-steps cat-steps--wizard" aria-label="Étapes de la création">
          {STEPS.map((stepLabel, index) => (
            <li key={stepLabel} className={cn("cat-step", index < step && "cat-step--done", index === step && "cat-step--current")} aria-current={index === step ? "step" : undefined}>
              <button type="button" className="cat-step__btn" onClick={() => go(index)} disabled={write.isPending}>
                <span className="cat-step__n">{index < step ? <Check size={14} aria-hidden="true" /> : index + 1}</span>{stepLabel}
              </button>
            </li>
          ))}
        </ol>
        {write.error && (
          <Banner tone="err" icon={Info} title="Création refusée — rien n'a été enregistré" role="alert">
            {write.error.error.fieldErrors ? "Corrigez les champs signalés ; vos saisies sont conservées." : errorMessage(write.error.error)}
          </Banner>
        )}
        {tried === step && Object.keys(problems).length > 0 && (
          <Banner tone="warn" icon={Info} title="Complétez cette étape pour continuer" role="alert" compact>{Object.values(problems)[0]}</Banner>
        )}

        {step === 0 && (
          <Card title="Fabricant et gamme" icon={Factory}>
            {existingRange ? (
              <KeyValues rows={[["Fabricant", name(existingRange.manufacturerId)], ["Gamme", <span key="g" className="cat-strong">{existingRange.name}</span>]]} />
            ) : (
              <div className="cat-grid">
                <SelectField label="Fabricant" name="manufacturerId" value={manufacturerId} onChange={(v) => { setManufacturerId(v); if (v !== "") setManufacturerName(""); }}
                  options={[{ value: "", label: manufacturers.length > 0 ? "— Nouveau fabricant —" : "— Aucun fabricant : saisissez-le —" }, ...manufacturers.map((m) => ({ value: m.id, label: m.name }))]}
                  hint="Choisissez un fabricant connu, ou laissez « Nouveau fabricant » et saisissez son nom." />
                {manufacturerId === "" && (
                  <Field label="Nouveau fabricant" name="manufacturer.name" value={manufacturerName} onChange={setManufacturerName} required
                    error={message("manufacturer") ?? message("manufacturer.name")} placeholder="Ex. Essilor" />
                )}
                <Field label="Nom de la gamme" name="range.name" value={rangeName} onChange={setRangeName} required span2
                  error={message("rangeName") ?? message("range.name")} placeholder="Ex. Eyezen" hint="La gamme est le nom commercial ; chaque fournisseur déclarera ensuite ce qu'il fabrique." />
              </div>
            )}
          </Card>
        )}

        {step === 1 && (
          <Card title="Type de verre et caractéristiques" icon={Layers}>
            <div className="cat-stack">
              <div className="cat-radios cat-radios--row" role="radiogroup" aria-label="Type de verre">
                {Object.entries(LENS_TYPE_LABELS).map(([value, text]) => (
                  <RadioCard key={value} name="lensType" value={value} checked={lensType === value} onChange={setLensType} label={text} sub={TYPE_HINTS[value]} />
                ))}
              </div>
              <div className="cat-grid">
                <SelectField label="Politique" name="production" value={production} onChange={setProduction} required
                  options={Object.entries(PRODUCTION_LABELS).map(([value, text]) => ({ value, label: text }))} hint="La plupart des verres correcteurs sont commandés à la demande." />
              </div>
            </div>
          </Card>
        )}

        {step === 2 && (
          <Card title="Indice, matériau et traitements" icon={Sparkles}>
            <div className="cat-stack">
              <div className="cat-grid">
                <SelectField label="Indice" name="indexId" value={indexId} onChange={setIndexId}
                  options={[{ value: "", label: "— Non précisé —" }, ...indices.map((i) => ({ value: i.id, label: i.name }))]}
                  hint={indices.length === 0 ? "Aucun indice : ajoutez-en dans « Listes de référence » du catalogue des verres." : undefined} />
                <Field label="Matériau" name="material" value={material} onChange={setMaterial} placeholder="Ex. polycarbonate (facultatif)" />
              </div>
              <fieldset className="clients-field" style={{ border: 0, margin: 0, padding: 0 }}>
                <legend className="clients-field__label" style={{ marginBottom: 7 }}>Traitements disponibles</legend>
                <div className="cat-checks">{activeTreatments.map((t) => (
                  <label key={t.id} className="cat-check"><input type="checkbox" checked={treatments.includes(t.id)}
                    onChange={(e) => setTreatments(e.target.checked ? [...treatments, t.id] : treatments.filter((x) => x !== t.id))} />{t.name}</label>
                ))}{activeTreatments.length === 0 && <span className="clients-muted">Aucun traitement : ajoutez-en dans « Listes de référence ».</span>}</div>
              </fieldset>
            </div>
          </Card>
        )}

        {step === 3 && (
          <Card title={`Fournisseurs (${offers.filter((o) => o.supplier).length})`} icon={FlaskConical}
            actions={<Button size="sm" onClick={() => setOffers([...offers, emptyOffer(currency)])}><Plus size={16} aria-hidden="true" />Ajouter un fournisseur</Button>}>
            <div className="cat-stack">
              <p className="clients-field__hint" style={{ margin: 0 }}>
                Chaque fournisseur aura ses propres plages de fabrication, restrictions, prix et délais (étapes suivantes).
              </p>
              {offers.map((o, j) => (
                <div key={j} className="cat-addrow">
                  <div className="cat-span2" style={{ flex: 1 }}>
                    <SupplierCombobox label={`Fournisseur ${j + 1}`} value={o.supplier} onChange={(supplier) => setOffer(j, { supplier })} required
                      error={message(`offers[${j}].supplierId`) ?? null} />
                  </div>
                  {offers.length > 1 && <Button size="sm" variant="danger" onClick={() => setOffers(offers.filter((_, k) => k !== j))}><Trash2 size={14} aria-hidden="true" />Retirer</Button>}
                </div>
              ))}
            </div>
          </Card>
        )}

        {step === 4 && (
          <Card title="Plages de fabrication et restrictions" icon={Ruler}
            actions={<Switch label="Mode avancé" checked={advanced} onChange={setAdvanced} />}>
            <div className="cat-stack">
              <Banner tone="info" icon={Info} title="Ce que la fiche technique de chaque fournisseur déclare" compact>
                {advanced
                  ? "Mode avancé : toutes les plages, diamètres, restrictions conditionnelles, combinaisons et fabrication spéciale. Laissez vide ce qui n'est pas connu."
                  : `Mode simple : plage de sphère${hasAddition(lensType) ? " et d'addition" : ""} de chaque fournisseur. Le mode avancé ajoute cylindre, diamètres et restrictions.`}
                {" "}Aucune règle n&apos;est déduite : un champ vide donnera « à vérifier » lors d&apos;un test de correction.
              </Banner>
              {offers.map((o, j) => (
                <div key={j} className="cat-variant">
                  <div className="cat-variant__head">{offerName(o, j)}</div>
                  <div className="cat-grid cat-grid--3">
                    <RangeFields label="Sphère" name={`offers[${j}].sphere`} value={o.sph} onChange={(sph) => setOffer(j, { sph })} errors={errors} />
                    {advanced && <RangeFields label="Cylindre" name={`offers[${j}].cylinder`} value={o.cyl} onChange={(cyl) => setOffer(j, { cyl })} errors={errors} />}
                    {hasAddition(lensType) && <RangeFields label="Addition" name={`offers[${j}].addition`} value={o.add} onChange={(add) => setOffer(j, { add })} errors={errors} />}
                    {advanced && <Field label="Diamètres proposés (mm)" name={`offers[${j}].diameters`} value={o.diameters} onChange={(diameters) => setOffer(j, { diameters })} errors={errors} placeholder="60, 65, 70" />}
                  </div>
                  {(["sphere", "cylinder", "addition"] as const).map((k) => message(`offers[${j}].${k}Min`) && <p key={k} className="clients-field__error">{message(`offers[${j}].${k}Min`)}</p>)}
                  {advanced && (
                    <div className="cat-stack cat-advanced">
                      <div className="cat-grid cat-grid--3">
                        <Field label="Fiche technique (provenance)" name={`offers[${j}].sourceReference`} value={o.source} onChange={(source) => setOffer(j, { source })} errors={errors} placeholder="Ex. catalogue 2026, p. 14" />
                        <Field label="Valide du" name={`offers[${j}].validFrom`} type="date" value={o.validFrom} onChange={(validFrom) => setOffer(j, { validFrom })} errors={errors} />
                        <Field label="Valide jusqu'au" name={`offers[${j}].validUntil`} type="date" value={o.validUntil} onChange={(validUntil) => setOffer(j, { validUntil })} errors={errors} />
                      </div>
                      <Switch label="Fiche fournisseur complète" checked={o.complete} onChange={(complete) => setOffer(j, { complete })}
                        hint="Cochez seulement si TOUTES les restrictions de ce fournisseur sont saisies : sinon les tests restent « à vérifier »." />
                      <RulesEditor rules={o.rules} onChange={(rules) => setOffer(j, { rules })} treatments={activeTreatments.filter((t) => treatments.includes(t.id))}
                        costsEditable={access.costsWrite} errors={errors} prefix={`offers[${j}].`} />
                    </div>
                  )}
                </div>
              ))}
            </div>
          </Card>
        )}

        {step === 5 && (
          <>
            <Card title="Prix et délais par fournisseur" icon={Coins}>
              <div className="cat-stack">
                {!access.costsWrite && <Lock>Prix fournisseur réservé au droit costs.write : les délais restent saisissables.</Lock>}
                <TableScroll label="Prix et délais">
                  <table className="clients-table cat-table cat-table--inner cat-table--compact">
                    <thead><tr><th scope="col">Fournisseur</th>{access.costsWrite && <><th scope="col">Prix fournisseur HT</th><th scope="col">Devise</th></>}<th scope="col">Délai (jours)</th></tr></thead>
                    <tbody>{offers.map((o, j) => (
                      <tr key={j}>
                        <td className="cat-strong">{offerName(o, j)}</td>
                        {access.costsWrite && (
                          <>
                            <td><Field label="Prix fournisseur HT" name={`offers[${j}].priceHt`} value={o.price} onChange={(price) => setOffer(j, { price })} inputMode="decimal"
                              error={message(`offers[${j}].priceHt`)} placeholder="Facultatif" /></td>
                            <td><Field label="Devise" name={`offers[${j}].priceCurrency`} value={o.currency} errors={errors}
                              onChange={(value) => setOffer(j, { currency: value.toUpperCase().slice(0, 3) })} /></td>
                          </>
                        )}
                        <td><Field label="Délai (jours)" name={`offers[${j}].leadTimeDays`} value={o.leadTime} onChange={(leadTime) => setOffer(j, { leadTime })} inputMode="numeric"
                          error={message(`offers[${j}].leadTimeDays`)} placeholder="Ex. 7" /></td>
                      </tr>
                    ))}</tbody>
                  </table>
                </TableScroll>
              </div>
            </Card>
            <Card title="Récapitulatif avant création" icon={ClipboardCheck}>
              <div className="cat-stack">
                <KeyValues rows={[
                  ["Gamme", <span key="g" className="cat-strong">{existingRange ? `${name(existingRange.manufacturerId)} — ${existingRange.name}` : `${manufacturerId ? name(manufacturerId) : manufacturerName || "—"} — ${rangeName || "—"}`}</span>],
                  ["Type", `${LENS_TYPE_LABELS[lensType]} · ${PRODUCTION_LABELS[production]}`],
                  ["Indice · matériau", `${indices.find((i) => i.id === indexId)?.name ?? "non précisé"} · ${material.trim() || "non précisé"}`],
                  ["Traitements", treatments.map((id) => activeTreatments.find((t) => t.id === id)?.name ?? "?").join(", ") || "aucun"],
                  ["Saisie", advanced ? "Mode avancé" : "Mode simple"],
                ]} />
                <TableScroll label="Offres à créer">
                  <table className="clients-table cat-table cat-table--inner cat-table--compact">
                    <thead><tr>
                      <th scope="col">Fournisseur</th><th scope="col">Sphère</th>{advanced && <th scope="col">Cylindre</th>}{hasAddition(lensType) && <th scope="col">Addition</th>}
                      {advanced && <th scope="col" className="cat-num">Règles</th>}{access.costsWrite && <th scope="col" className="cat-num">Prix HT</th>}<th scope="col" className="cat-num">Délai</th>
                    </tr></thead>
                    <tbody>{offers.map((o, j) => (
                      <tr key={j}>
                        <td className="cat-strong">{offerName(o, j)}</td>
                        <td className="tnum cat-nowrap">{rangeText(decimalOrNull(o.sph.min), decimalOrNull(o.sph.max))}</td>
                        {advanced && <td className="tnum cat-nowrap">{rangeText(decimalOrNull(o.cyl.min), decimalOrNull(o.cyl.max))}</td>}
                        {hasAddition(lensType) && <td className="tnum cat-nowrap">{rangeText(decimalOrNull(o.add.min), decimalOrNull(o.add.max))}</td>}
                        {advanced && <td className="cat-num">{o.rules.length}{o.complete && " "}{o.complete && <Badge tone="ok">complète</Badge>}</td>}
                        {access.costsWrite && <td className="cat-num">{o.price.trim() ? formatMoney(decimalOrNull(o.price), o.currency || currency) : "—"}</td>}
                        <td className="cat-num">{o.leadTime.trim() ? `${o.leadTime.trim()} j` : "—"}</td>
                      </tr>
                    ))}</tbody>
                  </table>
                </TableScroll>
                <p className="clients-field__hint" style={{ margin: 0 }}>
                  <Settings2 size={12} aria-hidden="true" /> Gamme et offres créées en une seule opération : si un élément est refusé, rien n&apos;est enregistré.
                  Chaque offre reste modifiable ensuite (révision tracée).
                </p>
              </div>
            </Card>
          </>
        )}
      </div>
      <FormBar dirty={dirty} hint="Rien n'est enregistré avant la confirmation finale">
        <Button onClick={close}><X size={16} aria-hidden="true" />Annuler</Button>
        {step > 0 && <Button onClick={() => go(step - 1)} disabled={write.isPending}><ChevronLeft size={16} aria-hidden="true" />Précédent</Button>}
        {last ? (
          <Button variant="primary" disabled={write.isPending} onClick={submit}><Save size={16} aria-hidden="true" />{write.isPending ? "Création…" : existingRange ? "Ajouter les fournisseurs" : "Créer la gamme"}</Button>
        ) : (
          <Button variant="primary" onClick={() => go(step + 1)}>Suivant<ChevronRight size={16} aria-hidden="true" /></Button>
        )}
      </FormBar>
      <UnsavedModal open={guard.pending !== null} onLeave={guard.confirm} onStay={guard.cancel} onSave={() => { guard.cancel(); if (last) submit(); else go(STEPS.length - 1); }} saving={write.isPending} />
    </section>
  );
}
