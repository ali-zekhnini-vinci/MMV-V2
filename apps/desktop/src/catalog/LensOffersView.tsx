// Référentiel verres, niveau 1 (COR-05-05) : par gamme, les OFFRES de chaque fournisseur — type, matériau / indice,
// traitements, politique stocké ou commandé, plages SPH / CYL / ADD, diamètres, délai, prix connu (coût : costs.read) —
// et un testeur de correction aux TROIS issues (compatible selon les règles complètes enregistrées / incompatible selon
// une règle explicite / à vérifier faute de données), jamais présenté comme une validation clinique. Niveau 2 (mode
// avancé, à la demande) : règles structurées, fabrication standard ou spéciale, suppléments, provenance et validité de
// la fiche, déclaration « fiche complète », motif de révision ; historique des révisions consultable.
import { Beaker, ChevronDown, ChevronRight, FlaskConical, History, Info, Plus, Settings2, ShieldCheck, Trash2 } from "lucide-react";
import { useState } from "react";
import type { LensReference, LensReferences } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { errorMessage, useCatalogPreview, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";
import { formatDateTime, formatMoney } from "./catalogFormat.ts";
import { Badge, Banner, Card, EmptyState, ErrorBanner, ErrorState, Field, KeyValues, Loading, Lock, Modal, SelectField, Switch, TableScroll } from "./CatalogUi.tsx";
import {
  CHECK_LABELS, decimalOrNull, LENS_TYPE_LABELS, type LensCheck, type LensOffer, type LensOfferDetail, type LensRule, PRODUCTION_LABELS, RULE_KIND_LABELS, rangeText, reasonText,
} from "./lensOffers.ts";
import { type SupplierChoice, SupplierCombobox } from "./SupplierCombobox.tsx";

export function LensOffersView({ refs }: { refs: LensReferences }) {
  const access = useCatalogAccess();
  const offers = useCatalogQuery<LensOffer[]>("/api/catalog/lens-offers?status=all");
  const [editing, setEditing] = useState<{ rangeId: string; offer: LensOffer | null } | null>(null);
  const [opened, setOpened] = useState<string | null>(null);
  const ranges = refs.ranges.filter((r) => r.status === "active");
  const list = Array.isArray(offers.data) ? offers.data : [];
  const name = (items: LensReference[], id: string | null) => items.find((i) => i.id === id)?.name ?? "—";

  if (offers.isPending) return <Loading label="Chargement des offres fournisseur…" />;
  if (offers.isError) return <ErrorState error={offers.error} title="Impossible de charger les offres" onRetry={() => void offers.refetch()} />;
  if (ranges.length === 0) {
    return (
      <EmptyState icon={FlaskConical} title="Aucune gamme de verres">
        Créez d&apos;abord le fabricant et la gamme dans « Référentiel de base », puis déclarez ici ce que chaque fournisseur propose pour cette gamme.
      </EmptyState>
    );
  }

  return (
    <div className="cat-stack">
      <Banner tone="info" icon={Info} title="Une offre = ce qu'un fournisseur fabrique réellement pour une gamme" compact>
        Plages de puissance, diamètres, traitements, délais et prix viennent de la fiche technique du fournisseur. Le testeur indique si une correction
        entre dans les règles ENREGISTRÉES — ce n&apos;est ni une validation clinique ni une garantie du fabricant.
      </Banner>
      {ranges.map((range) => {
        const items = list.filter((o) => o.rangeId === range.id);
        return (
          <Card key={range.id} title={`${name(refs.manufacturers, range.manufacturerId)} — ${range.name}`} icon={FlaskConical} flush
            actions={<>
              <Badge tone="accent">{items.filter((o) => o.status === "active").length} offre(s)</Badge>
              {access.catalogConfigure && access.suppliersRead && (
                <Button size="sm" onClick={() => setEditing({ rangeId: range.id, offer: null })}><Plus size={14} aria-hidden="true" />Offre fournisseur</Button>
              )}
            </>}>
            {items.length === 0 ? <p className="cat-empty-mini clients-muted">Aucune offre déclarée pour cette gamme.</p> : (
              <TableScroll label={`Offres — ${range.name}`}>
                <table className="clients-table cat-table cat-table--inner cat-table--compact">
                  <thead><tr>
                    <th scope="col"><span className="sr-only">Détail</span></th><th scope="col">Fournisseur</th><th scope="col">Type · indice</th><th scope="col">Traitements</th>
                    <th scope="col">Politique</th><th scope="col">Sphère</th><th scope="col">Cylindre</th><th scope="col">Addition</th><th scope="col">Ø</th>
                    <th scope="col" className="cat-num">Délai</th>{access.costsRead && <th scope="col" className="cat-num">Prix HT</th>}<th scope="col">Fiche</th>
                  </tr></thead>
                  <tbody>{items.map((o) => (
                    <OfferRows key={o.id} offer={o} refs={refs} open={opened === o.id} onToggle={() => setOpened(opened === o.id ? null : o.id)}
                      onEdit={access.catalogConfigure ? () => setEditing({ rangeId: range.id, offer: o }) : undefined} />
                  ))}</tbody>
                </table>
              </TableScroll>
            )}
          </Card>
        );
      })}
      {editing && <OfferEditor rangeId={editing.rangeId} offer={editing.offer} refs={refs} onClose={() => setEditing(null)} />}
    </div>
  );
}

function OfferRows({ offer, refs, open, onToggle, onEdit }: { offer: LensOffer; refs: LensReferences; open: boolean; onToggle: () => void; onEdit?: () => void }) {
  const access = useCatalogAccess();
  const treatment = (id: string) => refs.treatments.find((t) => t.id === id)?.name ?? "?";
  const index = refs.indices.find((i) => i.id === offer.indexId)?.name;
  const columns = 11 + (access.costsRead ? 1 : 0);
  return (
    <>
      <tr className={cn("cat-row", offer.status === "archived" && "is-archived")} onClick={onToggle}>
        <td>
          <button type="button" className="cat-ract" aria-expanded={open} aria-label={`${open ? "Masquer" : "Afficher"} le détail de l'offre ${offer.supplierName}`}
            onClick={(e) => { e.stopPropagation(); onToggle(); }}>
            {open ? <ChevronDown size={16} aria-hidden="true" /> : <ChevronRight size={16} aria-hidden="true" />}
          </button>
        </td>
        <td className="cat-strong">{offer.supplierName}</td>
        <td>{LENS_TYPE_LABELS[offer.lensType] ?? offer.lensType}{index && <div className="cat-sub">indice {index}{offer.material ? ` · ${offer.material}` : ""}</div>}</td>
        <td className="cat-td-fill cat-secondary">{offer.treatmentIds.map(treatment).join(", ") || "—"}</td>
        <td><Badge tone={offer.production === "stocked" ? "ok" : "info"}>{offer.production === "stocked" ? "Stocké" : "Sur commande"}</Badge></td>
        <td className="tnum cat-nowrap">{rangeText(offer.sphereMin, offer.sphereMax)}</td>
        <td className="tnum cat-nowrap">{rangeText(offer.cylinderMin, offer.cylinderMax)}</td>
        <td className="tnum cat-nowrap">{rangeText(offer.additionMin, offer.additionMax)}</td>
        <td className="tnum cat-nowrap">{offer.diameters.join(" / ") || "—"}</td>
        <td className="cat-num tnum">{offer.leadTimeDays !== null ? `${offer.leadTimeDays} j` : "—"}</td>
        {access.costsRead && <td className="cat-num">{offer.priceHt && offer.priceCurrency ? formatMoney(offer.priceHt, offer.priceCurrency) : "—"}</td>}
        <td>{offer.status === "archived" ? <Badge tone="arch">Archivée</Badge> : offer.rulesComplete ? <Badge tone="ok" icon={ShieldCheck}>Complète</Badge> : <Badge tone="warn">Incomplète</Badge>}</td>
      </tr>
      {open && (
        <tr className="cat-row-detail">
          <td colSpan={columns}><OfferDetail offer={offer} refs={refs} onEdit={onEdit} /></td>
        </tr>
      )}
    </>
  );
}

function OfferDetail({ offer, refs, onEdit }: { offer: LensOffer; refs: LensReferences; onEdit?: () => void }) {
  const detail = useCatalogQuery<LensOfferDetail>(`/api/catalog/lens-offers/${offer.id}`);
  const treatment = (id: string) => refs.treatments.find((t) => t.id === id)?.name ?? "?";
  return (
    <div className="cat-lens-detail">
      <div className="cat-stack">
        <KeyValues compact rows={[
          ["Fiche technique", offer.sourceReference ?? "—"],
          ["Validité", offer.validFrom || offer.validUntil ? `${offer.validFrom ?? "…"} → ${offer.validUntil ?? "…"}` : "—"],
          ["Règles avancées", offer.rules.length === 0 ? "aucune" : `${offer.rules.length} règle(s)`],
          ["Révision", `n° ${offer.revision} · ${formatDateTime(offer.updatedAtUtc)}`],
        ]} />
        {offer.rules.length > 0 && (
          <ul className="cat-rule-list">{offer.rules.map((r, i) => (
            <li key={i}>
              <Badge tone={r.effect === "deny" ? "err" : r.manufacturing === "special" ? "warn" : "ok"}>{r.effect === "deny" ? "Interdit" : r.manufacturing === "special" ? "Fabrication spéciale" : "Autorisé"}</Badge>
              {" "}{RULE_KIND_LABELS[r.kind] ?? r.kind}{r.range ? ` ${rangeText(r.range.min, r.range.max)}` : ""}{r.diameters ? ` ${r.diameters.join(" / ")} mm` : ""}
              {r.maxSphereCylinderSum ? ` ≤ ${r.maxSphereCylinderSum} D` : ""}
              {r.conditions?.treatmentIds?.length ? ` · avec ${r.conditions.treatmentIds.map(treatment).join(" + ")}` : ""}
              {r.conditions?.cylinder ? ` · si CYL ${rangeText(r.conditions.cylinder.min, r.conditions.cylinder.max)}` : ""}
              {r.extraLeadDays ? ` · +${r.extraLeadDays} j` : ""}{r.note ? ` — ${r.note}` : ""}
            </li>
          ))}</ul>
        )}
        {onEdit && offer.status === "active" && <div><Button size="sm" onClick={onEdit}><Settings2 size={14} aria-hidden="true" />Modifier l&apos;offre</Button></div>}
        <div>
          <p className="cat-fsec__title"><History size={14} aria-hidden="true" />Historique des révisions</p>
          {detail.isPending ? <span className="clients-muted">Chargement…</span> : detail.isError ? <span className="clients-muted">Indisponible.</span> : (
            <ul className="cat-rule-list">{detail.data.revisions.map((r) => (
              <li key={r.revision}>n° {r.revision} · {formatDateTime(r.changedAtUtc)} · {r.changedBy.displayName}{r.reason ? ` — ${r.reason}` : ""}</li>
            ))}</ul>
          )}
        </div>
      </div>
      <CompatibilityTester offer={offer} refs={refs} />
    </div>
  );
}

function CompatibilityTester({ offer, refs }: { offer: LensOffer; refs: LensReferences }) {
  const check = useCatalogPreview<LensCheck>();
  const [sphere, setSphere] = useState("");
  const [cylinder, setCylinder] = useState("");
  const [addition, setAddition] = useState("");
  const [diameter, setDiameter] = useState("");
  const [treatments, setTreatments] = useState<string[]>([]);
  const treatment = (id: string) => refs.treatments.find((t) => t.id === id)?.name ?? "?";
  const result = check.data;
  return (
    <div className="cat-tester">
      <p className="cat-fsec__title"><Beaker size={14} aria-hidden="true" />Tester une correction</p>
      <div className="cat-grid cat-grid--3">
        <Field label="Sphère (D)" name="t-sphere" value={sphere} onChange={setSphere} inputMode="decimal" placeholder="−6,00" />
        <Field label="Cylindre (D)" name="t-cylinder" value={cylinder} onChange={setCylinder} inputMode="decimal" placeholder="−1,00" />
        <Field label="Addition (D)" name="t-addition" value={addition} onChange={setAddition} inputMode="decimal" placeholder="2,00" />
        <Field label="Diamètre (mm)" name="t-diameter" value={diameter} onChange={setDiameter} inputMode="numeric" placeholder="65" />
      </div>
      {offer.treatmentIds.length > 0 && (
        <div className="cat-checks">{offer.treatmentIds.map((id) => (
          <label key={id} className="cat-check"><input type="checkbox" checked={treatments.includes(id)}
            onChange={(e) => setTreatments(e.target.checked ? [...treatments, id] : treatments.filter((t) => t !== id))} />{treatment(id)}</label>
        ))}</div>
      )}
      <div>
        <Button size="sm" variant="primary" disabled={check.isPending} onClick={() => check.mutate({ path: `/api/catalog/lens-offers/${offer.id}/check`, body: {
          sphere: decimalOrNull(sphere), cylinder: decimalOrNull(cylinder), addition: decimalOrNull(addition),
          diameter: diameter.trim() === "" ? null : Number(diameter), treatmentIds: treatments,
        } })}><Beaker size={14} aria-hidden="true" />Évaluer</Button>
      </div>
      {check.error && <p className="clients-field__error">{errorMessage(check.error.error)}</p>}
      {result && (
        <Banner tone={CHECK_LABELS[result.status].tone} icon={result.status === "compatible" ? ShieldCheck : Info} title={CHECK_LABELS[result.status].title} compact role="status">
          <ul className="cat-rule-list">{result.reasons.map((r, i) => <li key={i}>{reasonText(r, treatment)}</li>)}
            {result.manufacturing === "special" && <li>Fabrication spéciale{result.extraLeadDays ? ` (+${result.extraLeadDays} j)` : ""}{result.surchargeHt ? ` · supplément ${formatMoney(result.surchargeHt, offer.priceCurrency ?? "EUR")}` : ""}</li>}
          </ul>
          <span className="clients-muted">{result.disclaimer}</span>
        </Banner>
      )}
    </div>
  );
}

// ---------- édition (niveau simple + mode avancé) ----------

function RangeFields({ label, value, onChange, name, errors }: {
  label: string;
  value: { min: string; max: string };
  onChange: (value: { min: string; max: string }) => void;
  name: string;
  errors?: Record<string, string[]>;
}) {
  return (
    <>
      <Field label={`${label} min.`} name={`${name}Min`} value={value.min} onChange={(min) => onChange({ ...value, min })} inputMode="decimal" errors={errors} errorKey={name} />
      <Field label={`${label} max.`} name={`${name}Max`} value={value.max} onChange={(max) => onChange({ ...value, max })} inputMode="decimal" errors={errors} />
    </>
  );
}

const range = (r: { min: number; max: number } | null | undefined) => (r ? { min: String(r.min), max: String(r.max) } : undefined);
const text = (n: number | null | undefined) => (n === null || n === undefined ? undefined : String(n));

/** Règle avancée existante renvoyée telle quelle (mode avancé replié) : nombres en chaînes décimales. */
function keptRule(r: LensRule) {
  return {
    kind: r.kind, effect: r.effect, manufacturing: r.manufacturing, range: range(r.range), diameters: r.diameters ?? undefined,
    maxSphereCylinderSum: text(r.maxSphereCylinderSum), surchargeHt: text(r.surchargeHt), extraLeadDays: r.extraLeadDays ?? undefined, note: r.note ?? undefined,
    conditions: r.conditions ? {
      treatmentIds: r.conditions.treatmentIds ?? undefined, indexIds: r.conditions.indexIds ?? undefined, material: r.conditions.material ?? undefined,
      sphere: range(r.conditions.sphere), cylinder: range(r.conditions.cylinder), addition: range(r.conditions.addition),
    } : undefined,
  };
}

interface RuleDraft {
  kind: string;
  effect: string;
  manufacturing: string;
  min: string;
  max: string;
  diameters: string;
  maxSum: string;
  treatmentIds: string[];
  condCylMin: string;
  condCylMax: string;
  surcharge: string;
  extraDays: string;
  note: string;
}

const ruleDraft = (r?: LensRule): RuleDraft => ({
  kind: r?.kind ?? "sphere_range", effect: r?.effect ?? "allow", manufacturing: r?.manufacturing ?? "standard",
  min: r?.range ? String(r.range.min) : "", max: r?.range ? String(r.range.max) : "", diameters: r?.diameters?.join(", ") ?? "",
  maxSum: r?.maxSphereCylinderSum ? String(r.maxSphereCylinderSum) : "", treatmentIds: r?.conditions?.treatmentIds ?? [],
  condCylMin: r?.conditions?.cylinder ? String(r.conditions.cylinder.min) : "", condCylMax: r?.conditions?.cylinder ? String(r.conditions.cylinder.max) : "",
  surcharge: r?.surchargeHt ? String(r.surchargeHt) : "", extraDays: r?.extraLeadDays ? String(r.extraLeadDays) : "", note: r?.note ?? "",
});

const ints = (text: string) => text.split(/[,;/\s]+/).filter(Boolean).map(Number).filter(Number.isInteger);

function OfferEditor({ rangeId, offer, refs, onClose }: { rangeId: string; offer: LensOffer | null; refs: LensReferences; onClose: () => void }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const write = useCatalogWrite();
  const [supplier, setSupplier] = useState<SupplierChoice | null>(offer ? { id: offer.supplierId, code: "", name: offer.supplierName } : null);
  const [lensType, setLensType] = useState(offer?.lensType ?? "single_vision");
  const [indexId, setIndexId] = useState(offer?.indexId ?? "");
  const [material, setMaterial] = useState(offer?.material ?? "");
  const [production, setProduction] = useState(offer?.production ?? "on_order");
  const [leadTime, setLeadTime] = useState(offer?.leadTimeDays?.toString() ?? "");
  const [price, setPrice] = useState(offer?.priceHt ?? "");
  const [currency, setCurrency] = useState(offer?.priceCurrency ?? "");
  const [sph, setSph] = useState({ min: offer?.sphereMin ?? "", max: offer?.sphereMax ?? "" });
  const [cyl, setCyl] = useState({ min: offer?.cylinderMin ?? "", max: offer?.cylinderMax ?? "" });
  const [add, setAdd] = useState({ min: offer?.additionMin ?? "", max: offer?.additionMax ?? "" });
  const [diameters, setDiameters] = useState(offer?.diameters.join(", ") ?? "");
  const [treatments, setTreatments] = useState<string[]>(offer?.treatmentIds ?? []);
  const [advanced, setAdvanced] = useState((offer?.rules.length ?? 0) > 0 || offer?.rulesComplete === true);
  const [rules, setRules] = useState<RuleDraft[]>(() => (offer?.rules ?? []).map(ruleDraft));
  const [complete, setComplete] = useState(offer?.rulesComplete ?? false);
  const [source, setSource] = useState(offer?.sourceReference ?? "");
  const [validFrom, setValidFrom] = useState(offer?.validFrom ?? "");
  const [validUntil, setValidUntil] = useState(offer?.validUntil ?? "");
  const [reason, setReason] = useState("");
  const errors = write.error?.error.fieldErrors;
  const activeTreatments = refs.treatments.filter((t) => t.status === "active");
  const costsEditable = access.costsWrite;
  const setRule = (i: number, patch: Partial<RuleDraft>) => setRules(rules.map((r, j) => (j === i ? { ...r, ...patch } : r)));

  const body = {
    ...(offer ? { version: offer.version } : { rangeId, supplierId: supplier?.id }),
    lensType, indexId: indexId || null, material: material.trim() || null, production, leadTimeDays: leadTime.trim() === "" ? null : Number(leadTime),
    ...(costsEditable ? { priceHt: decimalOrNull(price), priceCurrency: price.trim() === "" ? null : currency.trim().toUpperCase() || null }
      : offer?.priceHt ? { priceHt: offer.priceHt, priceCurrency: offer.priceCurrency } : {}),
    diameters: ints(diameters), sphereMin: decimalOrNull(sph.min), sphereMax: decimalOrNull(sph.max), cylinderMin: decimalOrNull(cyl.min), cylinderMax: decimalOrNull(cyl.max),
    additionMin: decimalOrNull(add.min), additionMax: decimalOrNull(add.max), treatmentIds: treatments,
    rules: advanced ? rules.map((r) => ({
      kind: r.kind, effect: r.effect, manufacturing: r.manufacturing,
      ...(["sphere_range", "cylinder_range", "addition_range"].includes(r.kind) ? { range: { min: decimalOrNull(r.min), max: decimalOrNull(r.max) } } : {}),
      ...(r.kind === "diameters" ? { diameters: ints(r.diameters) } : {}),
      ...(r.kind === "combination_limit" ? { maxSphereCylinderSum: decimalOrNull(r.maxSum) } : {}),
      ...(r.treatmentIds.length > 0 || r.condCylMin || r.condCylMax ? { conditions: {
        ...(r.treatmentIds.length > 0 ? { treatmentIds: r.treatmentIds } : {}),
        ...(r.condCylMin || r.condCylMax ? { cylinder: { min: decimalOrNull(r.condCylMin), max: decimalOrNull(r.condCylMax) } } : {}),
      } } : {}),
      ...(costsEditable && r.surcharge.trim() !== "" ? { surchargeHt: decimalOrNull(r.surcharge) } : {}),
      ...(r.extraDays.trim() !== "" ? { extraLeadDays: Number(r.extraDays) } : {}),
      ...(r.note.trim() !== "" ? { note: r.note.trim() } : {}),
    })) : (offer?.rules ?? []).map(keptRule),
    rulesComplete: advanced ? complete : offer?.rulesComplete ?? false,
    sourceReference: source.trim() || null, validFrom: validFrom || null, validUntil: validUntil || null, reason: reason.trim() || null,
  };
  const save = () => write.mutate(
    { method: offer ? "PUT" : "POST", path: offer ? `/api/catalog/lens-offers/${offer.id}` : "/api/catalog/lens-offers", body },
    { onSuccess: () => { toast.show(offer ? "Offre mise à jour" : "Offre créée", supplier?.name); onClose(); } },
  );
  return (
    <Modal open onClose={onClose} tone="accent" icon={FlaskConical} size="xl" busy={write.isPending}
      title={offer ? `Offre ${offer.supplierName}` : "Nouvelle offre fournisseur"}
      description="Saisissez ce que la fiche technique du fournisseur déclare. Laissez vide ce qui n'est pas connu : l'évaluation indiquera « à vérifier »."
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending || (!offer && !supplier)} onClick={save}>{offer ? "Enregistrer la révision" : "Créer l'offre"}</Button>
      </>}>
      <ErrorBanner error={write.error} />
      <div className="cat-stack">
        <div className="cat-grid cat-grid--3">
          {!offer && <div className="cat-span2"><SupplierCombobox label="Fournisseur" value={supplier} onChange={setSupplier} required error={errors?.supplierId ? "Fournisseur requis ou inconnu." : null} /></div>}
          <SelectField label="Type de verre" name="lensType" value={lensType} onChange={setLensType} required errors={errors}
            options={Object.entries(LENS_TYPE_LABELS).map(([value, label]) => ({ value, label }))} />
          <SelectField label="Indice" name="indexId" value={indexId} onChange={setIndexId} errors={errors}
            options={[{ value: "", label: "— Non précisé —" }, ...refs.indices.filter((i) => i.status === "active").map((i) => ({ value: i.id, label: i.name }))]} />
          <Field label="Matériau" name="material" value={material} onChange={setMaterial} errors={errors} placeholder="Ex. polycarbonate" />
          <SelectField label="Politique" name="production" value={production} onChange={setProduction} required errors={errors}
            options={Object.entries(PRODUCTION_LABELS).map(([value, label]) => ({ value, label }))} />
          <Field label="Délai (jours)" name="leadTimeDays" value={leadTime} onChange={setLeadTime} inputMode="numeric" errors={errors} />
          {costsEditable ? (
            <>
              <Field label="Prix fournisseur HT" name="priceHt" value={price} onChange={setPrice} inputMode="decimal" errors={errors} hint="Facultatif ; jusqu'à 4 décimales." />
              <Field label="Devise du prix" name="priceCurrency" value={currency} onChange={(v) => setCurrency(v.toUpperCase().slice(0, 3))} errors={errors} placeholder="EUR" />
            </>
          ) : <Lock>Prix fournisseur réservé au droit costs.write</Lock>}
        </div>
        <p className="cat-fsec__title">Plages déclarées (D)</p>
        <div className="cat-grid cat-grid--3">
          <RangeFields label="Sphère" name="sphere" value={sph} onChange={setSph} errors={errors} />
          <RangeFields label="Cylindre" name="cylinder" value={cyl} onChange={setCyl} errors={errors} />
          <RangeFields label="Addition" name="addition" value={add} onChange={setAdd} errors={errors} />
          <Field label="Diamètres proposés (mm)" name="diameters" value={diameters} onChange={setDiameters} errors={errors} placeholder="60, 65, 70" />
        </div>
        <fieldset className="clients-field" style={{ border: 0, margin: 0, padding: 0 }}>
          <legend className="clients-field__label" style={{ marginBottom: 7 }}>Traitements disponibles</legend>
          <div className="cat-checks">{activeTreatments.map((t) => (
            <label key={t.id} className="cat-check"><input type="checkbox" checked={treatments.includes(t.id)}
              onChange={(e) => setTreatments(e.target.checked ? [...treatments, t.id] : treatments.filter((x) => x !== t.id))} />{t.name}</label>
          ))}{activeTreatments.length === 0 && <span className="clients-muted">Aucun traitement dans le référentiel de base.</span>}</div>
        </fieldset>
        <div className="cat-grid cat-grid--3">
          <Field label="Fiche technique (provenance)" name="sourceReference" value={source} onChange={setSource} errors={errors} placeholder="Ex. catalogue 2026, p. 14" span2 />
          <Field label="Motif de la révision" name="reason" value={reason} onChange={setReason} errors={errors} placeholder="Ex. nouvelle fiche reçue" />
        </div>
        <Switch label="Mode avancé : règles et restrictions de fabrication" checked={advanced} onChange={setAdvanced}
          hint="Plages multiples, fabrication spéciale, diamètres conditionnels, restrictions de combinaison, traitements, validité de la fiche." />
        {advanced && (
          <div className="cat-stack cat-advanced">
            <div className="cat-grid cat-grid--3">
              <Field label="Valide du" name="validFrom" type="date" value={validFrom} onChange={setValidFrom} errors={errors} />
              <Field label="Valide jusqu'au" name="validUntil" type="date" value={validUntil} onChange={setValidUntil} errors={errors} />
              <Switch label="Fiche fournisseur complète" checked={complete} onChange={setComplete}
                hint="Cochez seulement si TOUTES les restrictions du fournisseur sont saisies : sinon les évaluations restent « à vérifier »." />
            </div>
            {rules.map((rule, i) => (
              <div key={i} className="cat-variant">
                <div className="cat-variant__head">Règle {i + 1}<span className="cat-formbar__spacer" />
                  <Button size="sm" variant="danger" onClick={() => setRules(rules.filter((_, j) => j !== i))}><Trash2 size={14} aria-hidden="true" />Retirer</Button>
                </div>
                <div className="cat-grid cat-grid--3">
                  <SelectField label="Type" name={`rules[${i}].kind`} value={rule.kind} onChange={(kind) => setRule(i, { kind })} errors={errors}
                    options={Object.entries(RULE_KIND_LABELS).map(([value, label]) => ({ value, label }))} />
                  <SelectField label="Effet" name={`rules[${i}].effect`} value={rule.effect} onChange={(effect) => setRule(i, { effect })} errors={errors}
                    options={[{ value: "allow", label: "Autoriser" }, { value: "deny", label: "Interdire" }]} />
                  <SelectField label="Fabrication" name={`rules[${i}].manufacturing`} value={rule.manufacturing} onChange={(manufacturing) => setRule(i, { manufacturing })} errors={errors}
                    options={[{ value: "standard", label: "Standard" }, { value: "special", label: "Spéciale" }]} />
                  {["sphere_range", "cylinder_range", "addition_range"].includes(rule.kind) && (
                    <>
                      <Field label="Min. (D)" name={`rules[${i}].rangeMin`} value={rule.min} onChange={(min) => setRule(i, { min })} inputMode="decimal" errors={errors} errorKey={`rules[${i}].range`} />
                      <Field label="Max. (D)" name={`rules[${i}].rangeMax`} value={rule.max} onChange={(max) => setRule(i, { max })} inputMode="decimal" errors={errors} />
                    </>
                  )}
                  {rule.kind === "diameters" && <Field label="Diamètres (mm)" name={`rules[${i}].diameters`} value={rule.diameters} onChange={(diameters) => setRule(i, { diameters })} errors={errors} />}
                  {rule.kind === "combination_limit" && <Field label="|SPH| + |CYL| max. (D)" name={`rules[${i}].maxSphereCylinderSum`} value={rule.maxSum} onChange={(maxSum) => setRule(i, { maxSum })} inputMode="decimal" errors={errors} />}
                  <Field label="Si CYL entre (min.)" name={`rules[${i}].conditions.cylinderMin`} value={rule.condCylMin} onChange={(condCylMin) => setRule(i, { condCylMin })} inputMode="decimal" errors={errors} />
                  <Field label="et (max.)" name={`rules[${i}].conditions.cylinderMax`} value={rule.condCylMax} onChange={(condCylMax) => setRule(i, { condCylMax })} inputMode="decimal" errors={errors} />
                  {costsEditable && <Field label="Supplément HT" name={`rules[${i}].surchargeHt`} value={rule.surcharge} onChange={(surcharge) => setRule(i, { surcharge })} inputMode="decimal" errors={errors} />}
                  <Field label="Délai supplémentaire (j)" name={`rules[${i}].extraLeadDays`} value={rule.extraDays} onChange={(extraDays) => setRule(i, { extraDays })} inputMode="numeric" errors={errors} />
                  <Field label="Note fournisseur" name={`rules[${i}].note`} value={rule.note} onChange={(note) => setRule(i, { note })} errors={errors} span2 />
                </div>
                {activeTreatments.length > 0 && (
                  <div className="cat-checks"><span className="clients-field__hint">S&apos;applique avec :</span>{activeTreatments.filter((t) => treatments.includes(t.id)).map((t) => (
                    <label key={t.id} className="cat-check"><input type="checkbox" checked={rule.treatmentIds.includes(t.id)}
                      onChange={(e) => setRule(i, { treatmentIds: e.target.checked ? [...rule.treatmentIds, t.id] : rule.treatmentIds.filter((x) => x !== t.id) })} />{t.name}</label>
                  ))}</div>
                )}
              </div>
            ))}
            <div><Button size="sm" onClick={() => setRules([...rules, ruleDraft()])}><Plus size={14} aria-hidden="true" />Ajouter une règle</Button></div>
          </div>
        )}
      </div>
    </Modal>
  );
}
