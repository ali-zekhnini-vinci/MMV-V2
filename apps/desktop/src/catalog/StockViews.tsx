// Stock — Lot 6 groupes 15–23 (vue globale clair/sombre, stock faible, rupture, par catégorie, recherche, dernier
// rafraîchissement, modifié sur un autre poste, vide / chargement / erreur ; historique des mouvements, filtres, détail ;
// entrée, sortie, ajustement, confirmation, motif obligatoire, quantité invalide, succès, conflit, erreur métier et
// technique) traduits sur les règles V2 : stock PAR EMPLACEMENT avec quantité RÉSERVÉE et disponible, magasin actif,
// motifs codés, ajustement par quantité CONSTATÉE et ATTENDUE (409 si le stock a bougé ailleurs), stock jamais
// négatif ni sous le réservé, journal en ajout seul, réceptions simples et partielles. « Dernier état chargé » + Actualiser
// (jamais présenté comme temps réel). Les seules sommes calculées sur le poste sont des QUANTITÉS (aucun montant).
import {
  ArrowDownToLine, ArrowLeftRight, ArrowRight, ArrowUpFromLine, Bell, Box, Boxes, Calendar, CircleX, Filter, Info, Layers, MapPin, PackagePlus, Plus,
  RotateCw, Scale, Search, SlidersHorizontal, Tag, TriangleAlert, Truck, X,
} from "lucide-react";
import { type ReactNode, useState } from "react";
import { useLocation, useNavigate } from "react-router";
import {
  type BalanceRow, type Family, type Movement, type Page, query, type ReceiptSummary, type SupplierRow, type VariantRow,
} from "../api/catalog.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { Button } from "../components/ui/button.tsx";
import { Popover, PopoverContent, PopoverTrigger } from "../components/ui/popover.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { AVAILABILITY_LABELS, formatDateTime, formatMoney, MOVEMENT_LABELS, REASONS, reasonLabel } from "./catalogFormat.ts";
import { availabilityBadge, stockTone } from "./catalogStatus.ts";
import {
  Badge, Banner, Diff, EmptyState, ErrorState, FamilyTag, Field, FilterChip, KeyValues, Kpi, Loading, Modal, PageHeader, RefreshBar, RowChevron, SearchBox,
  SelectField, TableFoot, TablePanel, ToggleChip, Toolbar, ToolbarSpacer,
} from "./CatalogUi.tsx";
import { errorMessage, useActiveStore, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";

type MovementKind = "manual_in" | "manual_out" | "adjustment";

const KIND_META: Record<MovementKind, { label: string; title: string; icon: typeof ArrowDownToLine; tone: "ok" | "err" | "info"; qty: string }> = {
  manual_in: { label: "Entrée", title: "Entrée de stock", icon: ArrowDownToLine, tone: "ok", qty: "Quantité à ajouter" },
  manual_out: { label: "Sortie", title: "Sortie de stock", icon: ArrowUpFromLine, tone: "err", qty: "Quantité à retirer" },
  adjustment: { label: "Ajustement", title: "Ajustement de stock", icon: Scale, tone: "info", qty: "Nouvelle quantité constatée" },
};

type Action = { kind: MovementKind | "threshold"; row: BalanceRow | null } | null;

// ---------- vue globale du stock (stockLight … stockError) ----------

export function BalancesView() {
  const access = useCatalogAccess();
  const navigate = useNavigate();
  const { store } = useActiveStore();
  const [q, setQ] = useState("");
  const [availability, setAvailability] = useState("");
  const [familyCode, setFamilyCode] = useState("");
  const [locationId, setLocationId] = useState("");
  const [byFamily, setByFamily] = useState(false);
  const [page, setPage] = useState(1);
  const [action, setAction] = useState<Action>(null);
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  const balances = useCatalogQuery<Page<BalanceRow> & { asOfUtc: string }>(store
    ? `/api/stock/balances${query({ storeId: store.id, q, availability, familyCode, locationId, sort: byFamily ? "family" : "name", page, pageSize: 50 })}` : null);
  // KPI : toutes les lignes du magasin (quantités entières, aucun montant) + alertes calculées par le serveur.
  const all = useCatalogQuery<Page<BalanceRow>>(store ? `/api/stock/balances${query({ storeId: store.id, pageSize: 200 })}` : null);
  const alerts = useCatalogQuery<{ lowStockCount: number; outOfStockCount: number; asOfUtc: string }>(store ? `/api/stock/alerts${query({ storeId: store.id })}` : null);
  const refresh = () => { void balances.refetch(); void all.refetch(); void alerts.refetch(); };
  const reset = (patch: () => void) => { patch(); setPage(1); };
  const allRows = all.data?.items ?? [];
  const complete = all.data ? all.data.totalCount <= all.data.items.length : false;
  const references = new Set(allRows.map((r) => r.variantId)).size;
  const units = allRows.reduce((sum, r) => sum + r.onHand, 0);
  const reserved = allRows.reduce((sum, r) => sum + r.reserved, 0);
  const locations = store?.locations.filter((l) => l.status === "active") ?? [];
  const empty = all.data !== undefined && all.data.totalCount === 0;
  const loadFailed = balances.isError;

  return (
    <section className="cat-screen" aria-labelledby="stock-title">
      <PageHeader title="Stock" subtitle="Vue d'ensemble des niveaux de stock par emplacement"
        actions={<>
          {access.stockMovementsWrite && access.suppliersRead && (
            <Button onClick={() => navigate("/inventaire/receptions", { state: { create: true } })}><PackagePlus size={16} aria-hidden="true" />Nouvelle réception</Button>
          )}
          {(access.stockMovementsWrite || access.stockAdjust) && (
            <Button variant="primary" onClick={() => setAction({ kind: access.stockMovementsWrite ? "manual_in" : "adjustment", row: null })}>
              <ArrowLeftRight size={16} aria-hidden="true" />Nouveau mouvement
            </Button>
          )}
        </>} />
      {!empty && !loadFailed && !balances.isPending && (
        <>
          <div className="cat-kpis">
            <Kpi icon={Box} tone="accent" label="Références suivies" value={all.data ? (complete ? references : `${references}+`) : "—"} note="variantes stockées du magasin" />
            <Kpi icon={Layers} tone="neutral" label="Unités en stock" value={all.data ? (complete ? units : "—") : "—"} note={complete ? `dont ${reserved} réservée${reserved > 1 ? "s" : ""}` : "plus de 200 lignes : filtrez"} />
            <Kpi icon={TriangleAlert} tone="warn" label="Stock faible" value={alerts.data?.lowStockCount ?? "—"} on={availability === "low_stock"}
              onClick={() => reset(() => setAvailability(availability === "low_stock" ? "" : "low_stock"))} />
            <Kpi icon={CircleX} tone="err" label="Rupture" value={alerts.data?.outOfStockCount ?? "—"} on={availability === "out_of_stock"}
              onClick={() => reset(() => setAvailability(availability === "out_of_stock" ? "" : "out_of_stock"))} />
          </div>
          <RefreshBar asOf={balances.data?.asOfUtc} onRefresh={refresh} refreshing={balances.isFetching} />
          <Toolbar>
            <SearchBox value={q} onSearch={(value) => reset(() => setQ(value))} placeholder="Rechercher un produit dans le stock (SKU, nom, code-barres)…" label="Rechercher dans le stock" />
            <ToggleChip icon={Layers} label="Par famille" on={byFamily} onToggle={() => reset(() => setByFamily(!byFamily))} />
            <ToggleChip icon={TriangleAlert} label="Stock faible" on={availability === "low_stock"} onToggle={() => reset(() => setAvailability(availability === "low_stock" ? "" : "low_stock"))} />
            <ToggleChip icon={CircleX} label="Rupture" on={availability === "out_of_stock"} onToggle={() => reset(() => setAvailability(availability === "out_of_stock" ? "" : "out_of_stock"))} />
            <ToolbarSpacer />
            <FilterChip icon={Tag} label="Famille" value={familyCode} onChange={(value) => reset(() => setFamilyCode(value))}
              options={[{ value: "", label: "Toutes les familles" }, ...(Array.isArray(families.data) ? families.data : []).filter((f) => f.allowedPolicies.includes("stocked")).map((f) => ({ value: f.code, label: f.label }))]} />
            {locations.length > 1 && (
              <FilterChip icon={MapPin} label="Emplacement" value={locationId} onChange={(value) => reset(() => setLocationId(value))}
                options={[{ value: "", label: "Tous les emplacements" }, ...locations.map((l) => ({ value: l.id, label: l.name }))]} />
            )}
          </Toolbar>
        </>
      )}
      {balances.isPending ? <Loading label="Chargement du stock…" /> : balances.isError ? (
        <ErrorState error={balances.error} title="Impossible de charger le stock" onRetry={refresh} />
      ) : empty ? (
        <EmptyState icon={Boxes} title="Aucun stock à afficher" action={access.catalogRead && <Button variant="primary" onClick={() => navigate("/produits")}><Box size={16} aria-hidden="true" />Voir le catalogue</Button>}>
          Aucune variante stockée dans ce magasin. Les variantes commandées à la demande et les prestations n&apos;ont pas de stock physique.
        </EmptyState>
      ) : balances.data.items.length === 0 ? (
        <EmptyState tone="info" icon={Search} title="Aucun résultat"
          action={<Button onClick={() => { setQ(""); setAvailability(""); setFamilyCode(""); setLocationId(""); setPage(1); }}><RotateCw size={16} aria-hidden="true" />Réinitialiser les filtres</Button>}>
          Aucune ligne de stock ne correspond à ces critères.
        </EmptyState>
      ) : (
        <TablePanel fill foot={
          <TableFoot icon={Boxes} page={balances.data.page} pageSize={balances.data.pageSize} total={balances.data.totalCount} onPage={setPage}
            right={<span className="clients-muted">Disponible = en main − réservé</span>}>
            {balances.data.totalCount} ligne{balances.data.totalCount > 1 ? "s" : ""} (variante × emplacement)
            {availability ? ` · ${AVAILABILITY_LABELS[availability as keyof typeof AVAILABILITY_LABELS].toLowerCase()}` : ""}
          </TableFoot>
        }>
          <table className="clients-table cat-table">
            <thead>
              <tr>
                <th scope="col">Produit</th>
                <th scope="col" className="cat-hide-sm">Famille</th>
                <th scope="col">Emplacement</th>
                <th scope="col" className="cat-num">En main</th>
                <th scope="col" className="cat-num">Réservé</th>
                <th scope="col" className="cat-num">Disponible</th>
                <th scope="col" className="cat-num cat-hide-sm">Seuil</th>
                <th scope="col">État</th>
                <th scope="col"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {groupRows(balances.data.items, byFamily).map((item) => item.kind === "group" ? (
                <tr key={`g-${item.code}`} className="cat-group-row">
                  <td colSpan={9}>
                    <div className="cat-group-row__inner"><FamilyTag code={item.code} /><span className="clients-muted">{item.count} ligne{item.count > 1 ? "s" : ""} · {item.units} unité{item.units > 1 ? "s" : ""} en main</span></div>
                  </td>
                </tr>
              ) : (
                <BalanceLine key={`${item.row.variantId}-${item.row.locationId}`} row={item.row} onAction={(kind) => setAction({ kind, row: item.row })}
                  onOpen={() => navigate(`/produits/modele/${item.row.modelId}?variante=${item.row.variantId}`)} />
              ))}
            </tbody>
          </table>
        </TablePanel>
      )}
      {action && action.kind === "threshold" && action.row && <ThresholdModal row={action.row} onClose={() => setAction(null)} />}
      {action && action.kind !== "threshold" && <MovementModal initialKind={action.kind} row={action.row} onClose={() => setAction(null)} onRefresh={refresh} />}
    </section>
  );
}

type GroupItem = { kind: "group"; code: string; count: number; units: number } | { kind: "row"; row: BalanceRow };

function groupRows(rows: BalanceRow[], byFamily: boolean): GroupItem[] {
  if (!byFamily) return rows.map((row) => ({ kind: "row", row }));
  const out: GroupItem[] = [];
  for (const code of [...new Set(rows.map((r) => r.familyCode))]) {
    const members = rows.filter((r) => r.familyCode === code);
    out.push({ kind: "group", code, count: members.length, units: members.reduce((s, r) => s + r.onHand, 0) });
    for (const row of members) out.push({ kind: "row", row });
  }
  return out;
}

function BalanceLine({ row, onAction, onOpen }: { row: BalanceRow; onAction: (kind: MovementKind | "threshold") => void; onOpen: () => void }) {
  const access = useCatalogAccess();
  const badge = availabilityBadge(row.availability);
  const tone = stockTone(row.availability);
  return (
    <tr>
      <td>
        <div className="clients-cell">
          <span className="cat-thumb"><Box size={18} aria-hidden="true" /></span>
          <div>
            <div className="clients-cell__name">{row.modelName} <span className="cat-secondary" style={{ fontWeight: 500 }}>· {row.label}</span></div>
            <div className="clients-cell__ref">{row.sku}</div>
          </div>
        </div>
      </td>
      <td className="cat-hide-sm"><FamilyTag code={row.familyCode} /></td>
      <td className="cat-secondary">{row.locationCode}</td>
      <td className="cat-num tnum">{row.onHand}</td>
      <td className="cat-num tnum cat-secondary">{row.reserved}</td>
      <td className="cat-num"><span className={cn("cat-strong", `cat-tone-${tone}`)} style={{ fontWeight: 800 }}>{row.available}</span></td>
      <td className="cat-num clients-muted cat-hide-sm">{row.lowStockThreshold ?? "—"}</td>
      <td><Badge tone={badge.tone} icon={badge.icon}>{AVAILABILITY_LABELS[row.availability]}</Badge></td>
      <td>
        <div className="clients-rowacts">
          {access.stockMovementsWrite && <RowAction label="Entrée" icon={ArrowDownToLine} onClick={() => onAction("manual_in")} />}
          {access.stockMovementsWrite && <RowAction label="Sortie" icon={ArrowUpFromLine} onClick={() => onAction("manual_out")} />}
          {access.stockAdjust && <RowAction label="Ajuster" icon={Scale} onClick={() => onAction("adjustment")} />}
          {access.stockAdjust && <RowAction label="Seuil" icon={Bell} onClick={() => onAction("threshold")} />}
          {access.catalogRead && <RowAction label="Ouvrir la fiche" icon={ArrowRight} onClick={onOpen} />}
        </div>
      </td>
    </tr>
  );
}

function RowAction({ label, icon: Icon, onClick }: { label: string; icon: typeof Box; onClick: () => void }) {
  return (
    <button type="button" className="cat-ract" aria-label={label} title={label} onClick={onClick}>
      <Icon size={16} aria-hidden="true" />
    </button>
  );
}

function ThresholdModal({ row, onClose }: { row: BalanceRow; onClose: () => void }) {
  const { store } = useActiveStore();
  const toast = useToast();
  const write = useCatalogWrite();
  const [threshold, setThreshold] = useState(row.lowStockThreshold?.toString() ?? "");
  const invalid = threshold.trim() !== "" && !/^\d+$/.test(threshold.trim());
  return (
    <Modal open onClose={onClose} tone="warn" icon={Bell} title="Seuil d'alerte"
      description={`${row.modelName} · ${row.label} (${row.sku}) — magasin ${store?.name ?? ""}. Alerte « stock faible » quand le disponible atteint ce seuil.`}
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={invalid || write.isPending}
          onClick={() => write.mutate({ method: "PUT", path: `/api/stock/thresholds/${row.variantId}`, body: { storeId: store?.id, version: row.thresholdVersion, lowStockThreshold: threshold.trim() === "" ? null : Number(threshold) } },
            { onSuccess: () => { toast.show("Seuil enregistré", row.sku); onClose(); } })}>
          Enregistrer le seuil
        </Button>
      </>}>
      <MovementError error={write.error} />
      <Field label="Seuil de stock faible" name="lowStockThreshold" value={threshold} onChange={setThreshold} inputMode="numeric" suffix="unités" errors={write.error?.error.fieldErrors}
        error={invalid ? "Le seuil doit être un entier positif ou nul." : null} hint="Vide = seuil non défini." />
    </Modal>
  );
}

// ---------- saisie d'un mouvement (movEntry, movExit, movAdjust, movAdjustConfirm, movReasonReq, movQtyInvalid, movSuccess, movConflict, movErrBiz, movErrTech) ----------

function MovementError({ error, onRefresh }: { error: ApiRequestError | null; onRefresh?: () => void }) {
  if (!error) return null;
  const code = error.error.code;
  if (code === "stock_changed" || code === "version_conflict") {
    return (
      <Banner tone="warn" icon={TriangleAlert} title="Le stock a changé sur un autre poste" role="alert" compact
        action={onRefresh && <Button size="sm" className="cat-btn-warn" onClick={onRefresh}><RotateCw size={16} aria-hidden="true" />Actualiser</Button>}>
        {errorMessage(error.error)} Rien n&apos;a été enregistré.
      </Banner>
    );
  }
  if (code === "stock_insufficient" || code === "stock_reserved") {
    return (
      <Banner tone="err" icon={CircleX} title="Stock insuffisant" role="alert" compact>
        {errorMessage(error.error)} Rien n&apos;a été enregistré.
      </Banner>
    );
  }
  const technical = !code && ["network", "timeout", "http", "invalid-response"].includes(error.error.kind);
  return (
    <Banner tone="err" icon={technical ? TriangleAlert : Info} title={technical ? "Erreur technique" : "Opération refusée"} role="alert" compact>
      {errorMessage(error.error)}{technical ? " Réessayez ; une requête incertaine n'est jamais appliquée deux fois." : ""}
    </Banner>
  );
}

function MovementModal({ initialKind, row: initialRow, onClose, onRefresh }: { initialKind: MovementKind; row: BalanceRow | null; onClose: () => void; onRefresh: () => void }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const { store } = useActiveStore();
  const write = useCatalogWrite();
  const [kind, setKind] = useState<MovementKind>(initialKind);
  const [picked, setPicked] = useState<{ variantId: string; sku: string; name: string } | null>(initialRow ? { variantId: initialRow.variantId, sku: initialRow.sku, name: `${initialRow.modelName} · ${initialRow.label}` } : null);
  const [locationId, setLocationId] = useState(initialRow?.locationId ?? store?.locations.find((l) => l.isDefault)?.id ?? "");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");
  const [comment, setComment] = useState("");
  const [confirming, setConfirming] = useState(false);
  const [touched, setTouched] = useState(false);
  // Solde de la ligne (variante × emplacement) relu au serveur : base de l'aperçu et quantité ATTENDUE de l'ajustement.
  const balance = useCatalogQuery<Page<BalanceRow>>(store && picked && locationId ? `/api/stock/balances${query({ storeId: store.id, q: picked.sku, locationId, pageSize: 5 })}` : null);
  const current = balance.data?.items.find((b) => b.variantId === picked?.variantId && b.locationId === locationId) ?? null;
  const onHand = current?.onHand ?? (initialRow && initialRow.locationId === locationId ? initialRow.onHand : null);
  const meta = KIND_META[kind];
  const qtyText = quantity.trim();
  const qtyValid = kind === "adjustment" ? /^\d+$/.test(qtyText) : /^\d+$/.test(qtyText) && Number(qtyText) > 0;
  const qtyError = touched && !qtyValid ? (kind === "adjustment" ? "La quantité constatée doit être un entier positif ou nul." : "La quantité doit être un entier strictement positif.") : null;
  const reasonError = touched && reason === "" ? "Le motif est obligatoire." : null;
  const commentError = touched && reason === "other" && comment.trim() === "" ? "Commentaire obligatoire pour le motif « Autre »." : null;
  const next = onHand === null || !qtyValid ? null : kind === "manual_in" ? onHand + Number(qtyText) : kind === "manual_out" ? onHand - Number(qtyText) : Number(qtyText);
  const ready = picked !== null && locationId !== "" && qtyValid && reason !== "" && !(reason === "other" && comment.trim() === "");
  const allowed = (k: MovementKind) => (k === "adjustment" ? access.stockAdjust : access.stockMovementsWrite);

  const submit = () => {
    setTouched(true);
    if (!ready || !picked) return;
    if (kind === "adjustment" && !confirming) { setConfirming(true); return; }
    const done = { onSuccess: () => { toast.show("Mouvement enregistré", `${meta.label} · ${picked.sku} · ${kind === "adjustment" ? `stock constaté ${qtyText}` : `${kind === "manual_in" ? "+" : "−"}${qtyText}`}`); onRefresh(); onClose(); }, onError: () => setConfirming(false) };
    if (kind === "adjustment") {
      write.mutate({ method: "POST", path: "/api/stock/adjustments", body: { variantId: picked.variantId, locationId, expectedOnHand: onHand, countedQuantity: Number(qtyText), reasonCode: reason, comment: comment.trim() || null } }, done);
    } else {
      write.mutate({ method: "POST", path: "/api/stock/movements", body: { variantId: picked.variantId, locationId, type: kind, quantity: Number(qtyText), reasonCode: reason, comment: comment.trim() || null } }, done);
    }
  };

  if (confirming && kind === "adjustment" && picked && next !== null && onHand !== null) {
    return (
      <Modal open onClose={() => setConfirming(false)} tone="warn" icon={Scale} title="Confirmer l'ajustement ?" size="wide"
        description="Le serveur applique l'écart au stock verrouillé ; refusé si le stock a changé depuis l'affichage. Opération tracée dans le journal (ajout seul)."
        actions={<>
          <Button onClick={() => setConfirming(false)}>Revenir</Button>
          <Button variant="primary" disabled={write.isPending} onClick={submit}><Scale size={16} aria-hidden="true" />Confirmer l&apos;ajustement</Button>
        </>}>
        <div className="cat-difflist">
          <div className="cat-difflist__row">
            <span style={{ flex: 1, fontWeight: 600 }}>{picked.name} <span className="cat-sub cat-mono">{picked.sku}</span></span>
            <span className="clients-muted tnum">{onHand} →</span>
            <span className="tnum" style={{ fontWeight: 800 }}>{next}</span>
            <span style={{ minWidth: 34, textAlign: "right" }}><Diff value={next - onHand} /></span>
          </div>
        </div>
        <KeyValues compact rows={[["Motif", reasonLabel(reason)], ["Commentaire", comment.trim() || "—"]]} />
      </Modal>
    );
  }

  return (
    <Modal open onClose={onClose} tone={meta.tone} icon={meta.icon} title={meta.title} size="wide" busy={write.isPending}
      description={kind === "adjustment" ? "Corrige le stock à la quantité constatée. Un motif et une confirmation sont requis." : "Enregistre un mouvement manuel motivé pour la variante et l'emplacement choisis."}
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending || !allowed(kind)} onClick={submit}>
          {kind === "adjustment" ? <><Scale size={16} aria-hidden="true" />Ajuster le stock</> : <><meta.icon size={16} aria-hidden="true" />Enregistrer le mouvement</>}
        </Button>
      </>}>
      <div className="cat-seg" role="tablist" aria-label="Type de mouvement" style={{ alignSelf: "flex-start" }}>
        {(Object.keys(KIND_META) as MovementKind[]).map((k) => {
          const Icon = KIND_META[k].icon;
          return (
            <button key={k} type="button" role="tab" aria-selected={kind === k} className="cat-seg__item" disabled={!allowed(k)}
              onClick={() => { setKind(k); setReason(""); setConfirming(false); write.reset(); }}>
              <Icon size={14} aria-hidden="true" />{KIND_META[k].label}
            </button>
          );
        })}
      </div>
      {kind === "manual_out" && (
        <Banner tone="info" icon={Info} title="Sortie manuelle" compact>
          Le stock ne peut pas devenir négatif ni entamer la quantité réservée. Les sorties liées à une vente seront automatiques et ne se saisissent pas ici.
        </Banner>
      )}
      <MovementError error={write.error} onRefresh={() => { void balance.refetch(); onRefresh(); write.reset(); }} />
      <div className="cat-grid">
        {initialRow ? (
          <Field label="Produit" name="movementProduct" value={`${picked?.name ?? ""} · ${picked?.sku ?? ""}`} onChange={() => undefined} readOnly required span2 />
        ) : (
          <VariantSearch picked={picked} onPick={setPicked} />
        )}
        <SelectField label="Emplacement" name="movementLocation" value={locationId} onChange={setLocationId} required disabled={initialRow !== null}
          options={[{ value: "", label: "— Choisir —" }, ...(store?.locations.filter((l) => l.status === "active") ?? []).map((l) => ({ value: l.id, label: l.name }))]} />
        <Field label={meta.qty} name={kind === "adjustment" ? "countedQuantity" : "quantity"} value={quantity} onChange={setQuantity} inputMode="numeric" required suffix="unités"
          errors={write.error?.error.fieldErrors} error={qtyError} autoFocus={initialRow !== null} />
        <SelectField label="Motif" name="reasonCode" value={reason} onChange={setReason} required errors={write.error?.error.fieldErrors}
          options={[{ value: "", label: "— Choisir un motif —" }, ...REASONS[kind].map((r) => ({ value: r.code, label: r.label }))]}
          hint={reasonError ? undefined : "Motifs codés ; enregistrés avec le mouvement."} />
        <Field label={`Commentaire${reason === "other" ? "" : " (facultatif)"}`} name="comment" value={comment} onChange={setComment} multiline span2 required={reason === "other"}
          errors={write.error?.error.fieldErrors} error={commentError ?? reasonError} />
      </div>
      <div className="cat-delta" aria-live="polite">
        <div><div className="cat-delta__label">Stock en main (dernier état chargé)</div><div className="cat-delta__value">{onHand ?? "—"}</div></div>
        <ArrowRight size={18} aria-hidden="true" />
        <div><div className="cat-delta__label">Nouveau stock (estimé)</div><div className={cn("cat-delta__value", next !== null && next < 0 ? "cat-tone-err" : meta.tone === "ok" ? "cat-tone-ok" : meta.tone === "err" ? "cat-tone-err" : "")}>{next === null ? "—" : next < 0 ? "Refusé" : next}</div></div>
        <span className="cat-delta__end">
          {next !== null && next < 0 ? <Badge tone="err" icon={CircleX}>Stock insuffisant — le serveur refusera</Badge>
            : kind === "adjustment" && next !== null && onHand !== null ? <Badge tone={next - onHand >= 0 ? "ok" : "err"}>{next - onHand >= 0 ? "+" : "−"}{Math.abs(next - onHand)} écart</Badge>
              : <Badge tone={meta.tone}>{meta.label}</Badge>}
        </span>
      </div>
      {current && current.reserved > 0 && <p className="clients-field__hint">Réservé sur cet emplacement : {current.reserved} — disponible {current.available}.</p>}
    </Modal>
  );
}

/** Recherche de variante STOCKÉE dans la modale (sans fenêtre flottante imbriquée) : liste de résultats en ligne. */
function VariantSearch({ picked, onPick }: { picked: { variantId: string; sku: string; name: string } | null; onPick: (value: { variantId: string; sku: string; name: string } | null) => void }) {
  const { store } = useActiveStore();
  const [text, setText] = useState("");
  const results = useCatalogQuery<Page<VariantRow>>(store && text.trim().length >= 2 ? `/api/catalog/variants${query({ storeId: store.id, q: text.trim(), policy: "stocked", pageSize: 6 })}` : null);
  if (picked) {
    return (
      <Field label="Produit" name="movementProduct" value={`${picked.name} · ${picked.sku}`} onChange={() => undefined} readOnly required span2
        suffix={<button type="button" className="cat-input-action" onClick={() => onPick(null)}>Changer</button>} />
    );
  }
  return (
    <div className="clients-field cat-span2">
      <Field label="Produit" name="movementSearch" value={text} onChange={setText} required placeholder="Rechercher une variante stockée (SKU, nom, code-barres)…" />
      {(results.data?.items ?? []).length > 0 && (
        <div className="cat-menu-list" role="listbox" aria-label="Variantes trouvées">
          {(results.data?.items ?? []).map((v) => (
            <button key={v.variantId} type="button" role="option" aria-selected={false} className="cat-menu-option" onClick={() => onPick({ variantId: v.variantId, sku: v.sku, name: `${v.modelName} · ${v.label}` })}>
              <span className="cat-ref">{v.sku}</span>
              <span>{v.modelName} · {v.label}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ---------- historique des mouvements (movLight, movDark, movFilters, movDetail) ----------

const PERIODS = [
  { value: "", label: "Toute la période" },
  { value: "7", label: "7 derniers jours" },
  { value: "30", label: "30 derniers jours" },
  { value: "90", label: "90 derniers jours" },
];

export function MovementsView() {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const [type, setType] = useState("");
  const [period, setPeriodState] = useState<{ value: string; from: string | null }>({ value: "", from: null });
  // Borne « depuis » calculée au CHOIX de la période (événement), jamais au rendu : clé de requête stable.
  const setPeriod = (value: string) => setPeriodState({ value, from: value === "" ? null : new Date(Date.now() - Number(value) * 86_400_000).toISOString() });
  const [locationId, setLocationId] = useState("");
  const [variant, setVariant] = useState<{ id: string; sku: string } | null>(null);
  const [page, setPage] = useState(1);
  const [detail, setDetail] = useState<Movement | null>(null);
  const [creating, setCreating] = useState<MovementKind | null>(null);
  const movements = useCatalogQuery<Page<Movement>>(store ? `/api/stock/movements${query({ storeId: store.id, type, from: period.from, locationId, variantId: variant?.id, page, pageSize: 50 })}` : null);
  const locations = store?.locations.filter((l) => l.status === "active") ?? [];
  const filtered = type !== "" || period.value !== "" || locationId !== "" || variant !== null;

  return (
    <section className="cat-screen" aria-labelledby="movements-title">
      <PageHeader title="Mouvements de stock" subtitle="Journal des entrées, sorties et ajustements — en ajout seul : une correction est un nouveau mouvement motivé"
        actions={<>
          {access.stockMovementsWrite && <Button onClick={() => setCreating("manual_out")}><ArrowUpFromLine size={16} aria-hidden="true" />Sortie</Button>}
          {access.stockAdjust && <Button onClick={() => setCreating("adjustment")}><Scale size={16} aria-hidden="true" />Ajustement</Button>}
          {access.stockMovementsWrite && <Button variant="primary" onClick={() => setCreating("manual_in")}><ArrowDownToLine size={16} aria-hidden="true" />Entrée de stock</Button>}
        </>} />
      <Toolbar>
        <FilterChip icon={Filter} label="Type" value={type} onChange={(value) => { setType(value); setPage(1); }}
          options={[{ value: "", label: "Tous les types" }, ...Object.entries(MOVEMENT_LABELS).map(([value, label]) => ({ value, label }))]} />
        <ProductChip value={variant} onChange={(value) => { setVariant(value); setPage(1); }} />
        <FilterChip icon={Calendar} label="Période" value={period.value} onChange={(value) => { setPeriod(value); setPage(1); }} options={PERIODS} />
        {locations.length > 1 && (
          <FilterChip icon={MapPin} label="Emplacement" value={locationId} onChange={(value) => { setLocationId(value); setPage(1); }}
            options={[{ value: "", label: "Tous les emplacements" }, ...locations.map((l) => ({ value: l.id, label: l.name }))]} />
        )}
        <ToolbarSpacer />
        {filtered && <Button size="sm" variant="tertiary" onClick={() => { setType(""); setPeriod(""); setLocationId(""); setVariant(null); setPage(1); }}><X size={14} aria-hidden="true" />Effacer les filtres</Button>}
      </Toolbar>
      {movements.isPending ? <Loading label="Chargement des mouvements…" /> : movements.isError ? (
        <ErrorState error={movements.error} title="Impossible de charger les mouvements" onRetry={() => void movements.refetch()} />
      ) : movements.data.items.length === 0 ? (
        <EmptyState tone={filtered ? "info" : "accent"} icon={ArrowLeftRight} title={filtered ? "Aucun mouvement pour ces filtres" : "Aucun mouvement"}>
          {filtered ? "Élargissez la période ou retirez un filtre." : "Les entrées, sorties, ajustements et réceptions de ce magasin apparaîtront ici."}
        </EmptyState>
      ) : (
        <TablePanel fill foot={
          <TableFoot icon={ArrowLeftRight} page={movements.data.page} pageSize={movements.data.pageSize} total={movements.data.totalCount} onPage={setPage}>
            {movements.data.totalCount} mouvement{movements.data.totalCount > 1 ? "s" : ""} · tri par date décroissante
          </TableFoot>
        }>
          <table className="clients-table cat-table">
            <thead>
              <tr>
                <th scope="col">Date &amp; heure</th>
                <th scope="col">Produit</th>
                <th scope="col">Type</th>
                <th scope="col" className="cat-num">Quantité</th>
                <th scope="col" className="cat-num cat-hide-sm">Avant → après</th>
                <th scope="col" className="cat-hide-sm">Emplacement</th>
                <th scope="col">Motif</th>
                <th scope="col" className="cat-hide-md">Auteur</th>
                {access.costsRead && <th scope="col" className="cat-num cat-hide-md">Coût unitaire HT</th>}
                <th scope="col"><span className="sr-only">Détail</span></th>
              </tr>
            </thead>
            <tbody>
              {movements.data.items.map((m) => (
                <tr key={m.id} data-href="detail" tabIndex={0} onClick={() => setDetail(m)} onKeyDown={(e) => e.key === "Enter" && setDetail(m)}>
                  <td className="clients-muted tnum" style={{ whiteSpace: "nowrap" }}>{formatDateTime(m.performedAtUtc)}</td>
                  <td>
                    <div className="clients-cell__name" style={{ fontSize: 13.5 }}>{m.label}</div>
                    <div className="clients-cell__ref">{m.sku}</div>
                  </td>
                  <td><MovementBadge type={m.type} delta={m.quantityDelta} /></td>
                  <td className="cat-num"><Diff value={m.quantityDelta} /></td>
                  <td className="cat-num tnum cat-secondary cat-hide-sm">{m.quantityBefore} → {m.quantityAfter}</td>
                  <td className="cat-secondary cat-hide-sm">{m.locationCode}</td>
                  <td className="cat-secondary" style={{ fontSize: 13 }}>{reasonLabel(m.reasonCode)}{m.comment ? ` — ${m.comment}` : ""}{m.sourceNumber ? <div className="cat-sub">{m.sourceNumber}</div> : null}</td>
                  <td className="cat-secondary cat-hide-md">{m.performedBy.displayName}</td>
                  {access.costsRead && <td className="cat-num cat-secondary cat-hide-md">{m.unitCostHt && m.currency ? formatMoney(m.unitCostHt, m.currency) : "—"}</td>}
                  <td><RowChevron label="Détail" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </TablePanel>
      )}
      {detail && <MovementDetailModal movement={detail} onClose={() => setDetail(null)} />}
      {creating && <MovementModal initialKind={creating} row={null} onClose={() => setCreating(null)} onRefresh={() => void movements.refetch()} />}
    </section>
  );
}

function MovementBadge({ type, delta }: { type: string; delta: number }) {
  const icon = type === "manual_in" || type === "receipt" ? ArrowDownToLine : type === "manual_out" ? ArrowUpFromLine : Scale;
  const tone = type === "adjustment" || type === "inventory_adjustment" ? "info" : delta >= 0 ? "ok" : "err";
  return <Badge tone={tone} icon={icon}>{MOVEMENT_LABELS[type] ?? type}</Badge>;
}

function ProductChip({ value, onChange }: { value: { id: string; sku: string } | null; onChange: (value: { id: string; sku: string } | null) => void }) {
  const { store } = useActiveStore();
  const [text, setText] = useState("");
  const [open, setOpen] = useState(false);
  const results = useCatalogQuery<Page<VariantRow>>(store && text.trim().length >= 2 ? `/api/catalog/variants${query({ storeId: store.id, q: text.trim(), policy: "stocked", pageSize: 8 })}` : null);
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button type="button" className={cn("clients-chip", value && "clients-chip--on")} aria-label={`Produit${value ? ` : ${value.sku}` : ""}`}>
          <Box size={16} aria-hidden="true" />Produit
          {value ? <span className="cat-badge cat-badge--sm cat-badge--accent cat-chip__count">{value.sku}</span> : <SlidersHorizontal size={14} aria-hidden="true" />}
        </button>
      </PopoverTrigger>
      <PopoverContent style={{ width: 340 }}>
        <div className="cat-menu-search">
          <Search size={16} aria-hidden="true" />
          <input value={text} onChange={(e) => setText(e.target.value)} placeholder="SKU, nom, code-barres…" aria-label="Rechercher un produit" autoFocus />
        </div>
        <div className="cat-menu-list">
          {value && <button type="button" className="cat-menu-option" onClick={() => { onChange(null); setOpen(false); }}><X size={14} aria-hidden="true" />Tous les produits</button>}
          {(results.data?.items ?? []).map((v) => (
            <button key={v.variantId} type="button" className="cat-menu-option" aria-pressed={value?.id === v.variantId} onClick={() => { onChange({ id: v.variantId, sku: v.sku }); setOpen(false); }}>
              <span className="cat-ref">{v.sku}</span><span>{v.modelName} · {v.label}</span>
            </button>
          ))}
          {text.trim().length < 2 && <p className="clients-field__hint" style={{ padding: "4px 10px" }}>Saisissez au moins 2 caractères.</p>}
        </div>
      </PopoverContent>
    </Popover>
  );
}

function MovementDetailModal({ movement: m, onClose }: { movement: Movement; onClose: () => void }) {
  const access = useCatalogAccess();
  const tone = m.type === "adjustment" || m.type === "inventory_adjustment" ? "info" : m.quantityDelta >= 0 ? "ok" : "err";
  const icon = m.type === "manual_in" || m.type === "receipt" ? ArrowDownToLine : m.type === "manual_out" ? ArrowUpFromLine : Scale;
  const rows: [ReactNode, ReactNode][] = [
    ["Date & heure", formatDateTime(m.performedAtUtc)],
    ["Produit", <span key="p">{m.label} <span className="clients-muted cat-mono">· {m.sku}</span></span>],
    ["Emplacement", m.locationCode],
    ["Type", <MovementBadge key="t" type={m.type} delta={m.quantityDelta} />],
    ["Quantité", <Diff key="q" value={m.quantityDelta} />],
    ["Stock avant → après", `${m.quantityBefore} → ${m.quantityAfter}`],
    ["Motif", reasonLabel(m.reasonCode)],
    ["Commentaire", m.comment ?? "—"],
    ["Source", m.sourceNumber ?? m.supplierName ?? "—"],
    ["Utilisateur", m.performedBy.displayName],
  ];
  if (access.costsRead) rows.push(["Coût unitaire HT", m.unitCostHt && m.currency ? formatMoney(m.unitCostHt, m.currency) : "—"]);
  return (
    <Modal open onClose={onClose} tone={tone} icon={icon} title="Détail du mouvement" size="wide" actions={<Button onClick={onClose}>Fermer</Button>}>
      <KeyValues rows={rows} />
      <Banner tone="info" icon={Info} title="Journal en ajout seul" compact>Un mouvement n&apos;est jamais modifié ni supprimé ; une correction passe par un nouveau mouvement motivé.</Banner>
    </Modal>
  );
}

// ---------- réceptions fournisseurs (adaptation V2 : réception simple et partielle) ----------

interface ReceiptLineDraft { variantId: string; sku: string; name: string; announced: string; received: string; cost: string }

export function ReceiptsView() {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const [page, setPage] = useState(1);
  const location = useLocation();
  const [creating, setCreating] = useState<{ completes: ReceiptSummary | null } | null>(() =>
    (location.state as { create?: boolean } | null)?.create ? { completes: null } : null);
  const receipts = useCatalogQuery<Page<ReceiptSummary>>(store ? `/api/stock/receipts${query({ storeId: store.id, page, pageSize: 50 })}` : null);
  const canReceive = access.stockMovementsWrite && access.suppliersRead;
  return (
    <section className="cat-screen" aria-labelledby="receipts-title">
      <PageHeader title="Réceptions fournisseurs" subtitle="Réceptions simples, sans commande fournisseur ; une réception partielle laisse un reliquat à recevoir"
        actions={canReceive && <Button variant="primary" onClick={() => setCreating({ completes: null })}><PackagePlus size={16} aria-hidden="true" />Nouvelle réception</Button>} />
      {receipts.isPending ? <Loading label="Chargement des réceptions…" /> : receipts.isError ? (
        <ErrorState error={receipts.error} title="Impossible de charger les réceptions" onRetry={() => void receipts.refetch()} />
      ) : receipts.data.items.length === 0 ? (
        <EmptyState icon={Truck} title="Aucune réception" action={canReceive && <Button variant="primary" onClick={() => setCreating({ completes: null })}><Plus size={18} aria-hidden="true" />Nouvelle réception</Button>}>
          Les réceptions fournisseurs de ce magasin apparaîtront ici, avec leurs reliquats.
        </EmptyState>
      ) : (
        <TablePanel fill foot={
          <TableFoot icon={Truck} page={receipts.data.page} pageSize={receipts.data.pageSize} total={receipts.data.totalCount} onPage={setPage}>
            {receipts.data.totalCount} réception{receipts.data.totalCount > 1 ? "s" : ""}
          </TableFoot>
        }>
          <table className="clients-table cat-table">
            <thead><tr><th scope="col">Numéro</th><th scope="col">Date</th><th scope="col">Fournisseur</th><th scope="col">Bon de livraison</th><th scope="col">Emplacement</th><th scope="col" className="cat-num">Reçu</th><th scope="col">Reliquat</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead>
            <tbody>{receipts.data.items.map((r) => (
              <tr key={r.id}>
                <td><span className="cat-ref">{r.number}</span>{r.completesReceiptId && <div className="cat-sub">complément</div>}</td>
                <td className="clients-muted tnum">{formatDateTime(r.receivedAtUtc)}</td>
                <td className="cat-strong">{r.supplierName}</td>
                <td className="cat-secondary">{r.supplierDocumentReference ?? "—"}</td>
                <td className="cat-secondary">{r.locationCode}</td>
                <td className="cat-num tnum cat-strong">{r.receivedQuantity}</td>
                <td>{r.outstandingQuantity === null ? <span className="clients-muted">—</span> : r.outstandingQuantity > 0 ? <Badge tone="warn" icon={TriangleAlert}>{r.outstandingQuantity} à recevoir</Badge> : <Badge tone="ok">Complète</Badge>}</td>
                <td>{canReceive && (r.outstandingQuantity ?? 0) > 0 && <Button size="sm" variant="tertiary" onClick={() => setCreating({ completes: r })}>Recevoir le reliquat</Button>}</td>
              </tr>
            ))}</tbody>
          </table>
        </TablePanel>
      )}
      {creating && <ReceiptModal completes={creating.completes} onClose={() => setCreating(null)} />}
    </section>
  );
}

function ReceiptModal({ completes, onClose }: { completes: ReceiptSummary | null; onClose: () => void }) {
  const access = useCatalogAccess();
  const toast = useToast();
  const { store } = useActiveStore();
  const suppliers = useCatalogQuery<Page<SupplierRow>>("/api/suppliers?status=active&pageSize=200");
  const write = useCatalogWrite();
  const [supplierId, setSupplierId] = useState("");
  const [locationId, setLocationId] = useState(store?.locations.find((l) => l.isDefault)?.id ?? "");
  const [document, setDocument] = useState("");
  const [lines, setLines] = useState<ReceiptLineDraft[]>([]);
  const errors = write.error?.error.fieldErrors;
  if (!store) return null;
  const addLine = (picked: { variantId: string; sku: string; name: string } | null) => {
    if (picked) setLines((c) => (c.some((l) => l.variantId === picked.variantId) ? c : [...c, { variantId: picked.variantId, sku: picked.sku, name: picked.name, announced: "", received: "", cost: "" }]));
  };
  return (
    <Modal open onClose={onClose} tone="ok" icon={Truck} size="xl" busy={write.isPending}
      title={completes ? `Réception de complément — ${completes.number}` : "Nouvelle réception"}
      description={completes ? `Reliquat annoncé : ${completes.outstandingQuantity ?? 0} unité(s) chez ${completes.supplierName}.` : "Quantité annoncée (bon de livraison) facultative : si elle dépasse la quantité reçue, un reliquat reste à recevoir."}
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={lines.length === 0 || write.isPending}
          onClick={() => write.mutate({ method: "POST", path: "/api/stock/receipts", body: {
            locationId, supplierId: supplierId || null, currency: store.currency, supplierDocumentReference: document.trim() || null, completesReceiptId: completes?.id ?? null,
            lines: lines.map((l) => ({
              variantId: l.variantId, announcedQuantity: l.announced.trim() === "" ? null : Number(l.announced), receivedQuantity: Number(l.received || "0"),
              ...(access.costsWrite && l.cost.trim() !== "" ? { unitCostHt: l.cost.trim().replace(",", ".") } : {}),
            })),
          } }, { onSuccess: () => { toast.show("Réception enregistrée", `${lines.length} ligne(s) · stock mis à jour`); onClose(); } })}>
          <PackagePlus size={16} aria-hidden="true" />Enregistrer la réception
        </Button>
      </>}>
      <MovementError error={write.error} />
      <div className="cat-grid cat-grid--3">
        <SelectField label="Fournisseur" name="supplierId" value={supplierId} onChange={setSupplierId} required errors={errors}
          options={[{ value: "", label: "— Choisir —" }, ...(suppliers.data?.items ?? []).map((s) => ({ value: s.id, label: s.name }))]} />
        <SelectField label="Emplacement" name="locationId" value={locationId} onChange={setLocationId} required errors={errors}
          options={store.locations.filter((l) => l.status === "active").map((l) => ({ value: l.id, label: l.name }))} />
        <Field label="N° de bon de livraison" name="supplierDocumentReference" value={document} onChange={setDocument} errors={errors} />
      </div>
      <VariantSearch picked={null} onPick={addLine} />
      {lines.length > 0 && (
        <div className="clients-panel">
          <table className="clients-table cat-table cat-table--inner cat-table--compact">
            <thead><tr><th scope="col">Produit</th><th scope="col">Annoncé (BL)</th><th scope="col">Reçu</th>{access.costsWrite && <th scope="col">Coût unitaire HT ({store.currency})</th>}<th scope="col"><span className="sr-only">Retirer</span></th></tr></thead>
            <tbody>{lines.map((line, i) => {
              const update = (patch: Partial<ReceiptLineDraft>) => setLines((c) => c.map((l, j) => (j === i ? { ...l, ...patch } : l)));
              return (
                <tr key={line.variantId}>
                  <td><div className="clients-cell__name" style={{ fontSize: 13.5 }}>{line.name}</div><div className="clients-cell__ref">{line.sku}</div></td>
                  <td><QtyInput label={`Annoncé ${line.sku}`} value={line.announced} onChange={(announced) => update({ announced })} /></td>
                  <td><QtyInput label={`Reçu ${line.sku}`} value={line.received} onChange={(received) => update({ received })} /></td>
                  {access.costsWrite && <td><QtyInput label={`Coût ${line.sku}`} value={line.cost} onChange={(cost) => update({ cost })} decimal /></td>}
                  <td><button type="button" className="cat-ract" aria-label={`Retirer ${line.sku}`} onClick={() => setLines((c) => c.filter((_, j) => j !== i))}><X size={16} aria-hidden="true" /></button></td>
                </tr>
              );
            })}</tbody>
          </table>
        </div>
      )}
    </Modal>
  );
}

function QtyInput({ label, value, onChange, decimal }: { label: string; value: string; onChange: (value: string) => void; decimal?: boolean }) {
  return (
    <div className="clients-input" style={{ height: 36, width: decimal ? 140 : 110 }}>
      <input inputMode={decimal ? "decimal" : "numeric"} aria-label={label} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

