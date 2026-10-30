// Stock par emplacement (Lot 6 B-112 à B-120, adaptés) : vue « dernier état chargé à HH:MM » + Actualiser (jamais
// présentée comme temps réel), alertes de stock faible, entrées / sorties motivées, ajustement par quantité CONSTATÉE
// et ATTENDUE (409 si le stock a changé sur un autre poste), seuils, journal des mouvements, réceptions simples et
// partielles. Aucun stock pour les variantes commandées à la demande ni les prestations.
import { PackagePlus, RefreshCw } from "lucide-react";
import { useState } from "react";
import { type BalanceRow, type Movement, type Page, query, type ReceiptSummary, type SupplierRow, type VariantRow } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import { AVAILABILITY_LABELS, AVAILABILITY_TONES, formatDateTime, formatMoney, MOVEMENT_LABELS, REASONS, reasonLabel } from "./catalogFormat.ts";
import {
  Badge, Dialog, ErrorBanner, ErrorState, Field, Loading, PageHeader, Pager, Panel, SelectField, StoreSelect,
} from "./CatalogUi.tsx";
import { useActiveStore, useCatalogQuery, useCatalogWrite } from "./catalogData.ts";

type Action = { kind: "manual_in" | "manual_out" | "adjustment" | "threshold"; row: BalanceRow } | null;

export function BalancesView() {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const [availability, setAvailability] = useState("");
  const [q, setQ] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [action, setAction] = useState<Action>(null);
  const balances = useCatalogQuery<Page<BalanceRow> & { asOfUtc: string }>(store ? `/api/stock/balances${query({ storeId: store.id, q: search, availability, page, pageSize: 50 })}` : null);
  const alerts = useCatalogQuery<{ lowStockCount: number; outOfStockCount: number }>(store ? `/api/stock/alerts${query({ storeId: store.id })}` : null);

  return (
    <section>
      <PageHeader title="Stock" subtitle={balances.data ? `Dernier état chargé à ${formatDateTime(balances.data.asOfUtc)} — pas de temps réel : actualisez avant d'agir.` : undefined}
        actions={<>
          <StoreSelect />
          <Button onClick={() => { void balances.refetch(); void alerts.refetch(); }}><RefreshCw size={16} aria-hidden="true" />Actualiser</Button>
        </>} />
      {alerts.data && (alerts.data.lowStockCount > 0 || alerts.data.outOfStockCount > 0) && (
        <div className="cat-alert" role="status">
          Alertes : {alerts.data.lowStockCount} en stock faible, {alerts.data.outOfStockCount} en rupture.
          <Button size="sm" variant="tertiary" onClick={() => { setAvailability("low_stock"); setPage(1); }}>Voir le stock faible</Button>
          <Button size="sm" variant="tertiary" onClick={() => { setAvailability("out_of_stock"); setPage(1); }}>Voir les ruptures</Button>
        </div>
      )}
      <form className="cat-toolbar" role="search" onSubmit={(e) => { e.preventDefault(); setSearch(q.trim()); setPage(1); }}>
        <input className="cat-input cat-toolbar__search" placeholder="SKU, désignation, code-barres…" aria-label="Rechercher dans le stock" value={q} onChange={(e) => setQ(e.target.value)} />
        <select className="cat-input" aria-label="Disponibilité" value={availability} onChange={(e) => { setAvailability(e.target.value); setPage(1); }}>
          <option value="">Toutes</option><option value="in_stock">En stock</option><option value="low_stock">Stock faible</option><option value="out_of_stock">Rupture</option>
        </select>
        <Button type="submit">Rechercher</Button>
      </form>
      {balances.isPending ? <Loading label="Chargement du stock…" /> : balances.isError ? <ErrorState error={balances.error} onRetry={() => void balances.refetch()} /> : balances.data.items.length === 0 ? (
        <p className="cat-muted">Aucune variante stockée pour ces critères (les variantes commandées à la demande et les prestations n&apos;ont pas de stock).</p>
      ) : (
        <>
          <div className="cat-table-wrap">
            <table className="cat-table">
              <thead><tr><th scope="col">SKU</th><th scope="col">Produit</th><th scope="col">Emplacement</th><th scope="col" className="cat-num">En main</th><th scope="col" className="cat-num">Réservé</th><th scope="col" className="cat-num">Disponible</th><th scope="col" className="cat-num">Seuil</th><th scope="col">État</th><th scope="col">Actions</th></tr></thead>
              <tbody>{balances.data.items.map((row) => (
                <tr key={`${row.variantId}-${row.locationId}`}>
                  <td className="cat-mono">{row.sku}</td>
                  <td>{row.modelName} <span className="cat-muted">· {row.label}</span></td>
                  <td>{row.locationCode}</td>
                  <td className="cat-num">{row.onHand}</td><td className="cat-num">{row.reserved}</td><td className="cat-num cat-strong">{row.available}</td>
                  <td className="cat-num">{row.lowStockThreshold ?? <span className="cat-muted">non défini</span>}</td>
                  <td><Badge tone={AVAILABILITY_TONES[row.availability]}>{AVAILABILITY_LABELS[row.availability]}</Badge></td>
                  <td className="cat-actions-cell">
                    {access.stockMovementsWrite && <Button size="sm" onClick={() => setAction({ kind: "manual_in", row })}>Entrée</Button>}
                    {access.stockMovementsWrite && <Button size="sm" onClick={() => setAction({ kind: "manual_out", row })}>Sortie</Button>}
                    {access.stockAdjust && <Button size="sm" onClick={() => setAction({ kind: "adjustment", row })}>Ajuster</Button>}
                    {access.stockAdjust && <Button size="sm" variant="tertiary" onClick={() => setAction({ kind: "threshold", row })}>Seuil</Button>}
                  </td>
                </tr>
              ))}</tbody>
            </table>
          </div>
          <Pager page={balances.data.page} pageSize={balances.data.pageSize} total={balances.data.totalCount} onPage={setPage} />
        </>
      )}
      {action && <StockActionDialog action={action} onClose={() => setAction(null)} />}
    </section>
  );
}

function StockActionDialog({ action, onClose }: { action: NonNullable<Action>; onClose: () => void }) {
  const { store } = useActiveStore();
  const write = useCatalogWrite();
  const { row, kind } = action;
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState(kind === "threshold" ? "" : REASONS[kind === "adjustment" ? "adjustment" : kind][0].code);
  const [comment, setComment] = useState("");
  const [threshold, setThreshold] = useState(row.lowStockThreshold?.toString() ?? "");
  const errors = write.error?.error.fieldErrors;
  const title = kind === "threshold" ? "Seuil d'alerte" : kind === "adjustment" ? "Ajustement (quantité constatée)" : MOVEMENT_LABELS[kind];
  const submit = () => {
    const done = { onSuccess: onClose };
    if (kind === "threshold") {
      write.mutate({ method: "PUT", path: `/api/stock/thresholds/${row.variantId}`, body: { storeId: store?.id, version: row.thresholdVersion, lowStockThreshold: threshold.trim() === "" ? null : Number(threshold) } }, done);
    } else if (kind === "adjustment") {
      write.mutate({ method: "POST", path: "/api/stock/adjustments", body: { variantId: row.variantId, locationId: row.locationId, expectedOnHand: row.onHand, countedQuantity: Number(quantity), reasonCode: reason, comment: comment.trim() || null } }, done);
    } else {
      write.mutate({ method: "POST", path: "/api/stock/movements", body: { variantId: row.variantId, locationId: row.locationId, type: kind, quantity: Number(quantity), reasonCode: reason, comment: comment.trim() || null } }, done);
    }
  };
  return (
    <Dialog title={`${title} — ${row.sku}`} open onClose={onClose}>
      <p className="cat-muted">{row.modelName} · {row.label} · {row.locationCode} — en main affiché : <strong>{row.onHand}</strong>, réservé : {row.reserved}.</p>
      <ErrorBanner error={write.error} />
      {kind === "threshold" ? (
        <Field label="Seuil de stock faible (vide = non défini)" name="lowStockThreshold" value={threshold} onChange={setThreshold} inputMode="numeric" errors={errors} />
      ) : (
        <>
          <Field label={kind === "adjustment" ? "Quantité constatée" : "Quantité"} name={kind === "adjustment" ? "countedQuantity" : "quantity"} value={quantity} onChange={setQuantity} inputMode="numeric" required errors={errors}
            hint={kind === "adjustment" ? "L'écart est calculé par le serveur à partir du stock verrouillé ; refusé si le stock a changé depuis l'affichage." : undefined} />
          <SelectField label="Motif" name="reasonCode" value={reason} onChange={setReason} options={REASONS[kind === "adjustment" ? "adjustment" : kind].map((r) => ({ value: r.code, label: r.label }))} required errors={errors} />
          <Field label="Commentaire" name="comment" value={comment} onChange={setComment} errors={errors} multiline />
        </>
      )}
      <div className="cat-actions">
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending} onClick={submit}>{write.isPending ? "Enregistrement…" : "Confirmer"}</Button>
      </div>
    </Dialog>
  );
}

export function MovementsView() {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const [type, setType] = useState("");
  const [page, setPage] = useState(1);
  const movements = useCatalogQuery<Page<Movement>>(store ? `/api/stock/movements${query({ storeId: store.id, type, page, pageSize: 50 })}` : null);
  return (
    <section>
      <PageHeader title="Historique des mouvements" subtitle="Journal en ajout seul : un mouvement n'est jamais modifié ; une correction est un nouveau mouvement motivé." actions={<StoreSelect />} />
      <div className="cat-toolbar">
        <select className="cat-input" aria-label="Type de mouvement" value={type} onChange={(e) => { setType(e.target.value); setPage(1); }}>
          <option value="">Tous les types</option>
          {Object.entries(MOVEMENT_LABELS).map(([code, label]) => <option key={code} value={code}>{label}</option>)}
        </select>
      </div>
      {movements.isPending ? <Loading label="Chargement des mouvements…" /> : movements.isError ? <ErrorState error={movements.error} /> : movements.data.items.length === 0 ? <p className="cat-muted">Aucun mouvement.</p> : (
        <>
          <div className="cat-table-wrap">
            <table className="cat-table">
              <thead><tr><th scope="col">Date</th><th scope="col">SKU</th><th scope="col">Emplacement</th><th scope="col">Type</th><th scope="col" className="cat-num">Écart</th><th scope="col" className="cat-num">Avant</th><th scope="col" className="cat-num">Après</th><th scope="col">Motif</th><th scope="col">Source</th><th scope="col">Auteur</th>{access.costsRead && <th scope="col" className="cat-num">Coût unitaire HT</th>}</tr></thead>
              <tbody>{movements.data.items.map((m) => (
                <tr key={m.id}>
                  <td>{formatDateTime(m.performedAtUtc)}</td><td className="cat-mono">{m.sku}</td><td>{m.locationCode}</td><td>{MOVEMENT_LABELS[m.type] ?? m.type}</td>
                  <td className={`cat-num ${m.quantityDelta < 0 ? "cat-neg" : "cat-pos"}`}>{m.quantityDelta > 0 ? `+${m.quantityDelta}` : m.quantityDelta}</td>
                  <td className="cat-num">{m.quantityBefore}</td><td className="cat-num">{m.quantityAfter}</td>
                  <td>{reasonLabel(m.reasonCode)}{m.comment ? <span className="cat-muted"> — {m.comment}</span> : null}</td>
                  <td>{m.sourceNumber ?? m.supplierName ?? "—"}</td><td>{m.performedBy.displayName}</td>
                  {access.costsRead && <td className="cat-num">{m.unitCostHt && m.currency ? formatMoney(m.unitCostHt, m.currency) : "—"}</td>}
                </tr>
              ))}</tbody>
            </table>
          </div>
          <Pager page={movements.data.page} pageSize={movements.data.pageSize} total={movements.data.totalCount} onPage={setPage} />
        </>
      )}
    </section>
  );
}

interface ReceiptLineDraft { variantId: string; sku: string; announced: string; received: string; cost: string }

export function ReceiptsView() {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState<string | null | false>(false);
  const receipts = useCatalogQuery<Page<ReceiptSummary>>(store ? `/api/stock/receipts${query({ storeId: store.id, page, pageSize: 50 })}` : null);
  return (
    <section>
      <PageHeader title="Réceptions fournisseurs" subtitle="Réceptions simples (sans commande fournisseur). Réception partielle : quantité annoncée supérieure à la quantité reçue ; le reliquat se reçoit par une réception de complément."
        actions={<>
          <StoreSelect />
          {access.stockMovementsWrite && access.suppliersRead && <Button variant="primary" onClick={() => setCreating(null)}><PackagePlus size={16} aria-hidden="true" />Nouvelle réception</Button>}
        </>} />
      {creating !== false && <ReceiptForm completes={creating} onClose={() => setCreating(false)} />}
      {receipts.isPending ? <Loading label="Chargement des réceptions…" /> : receipts.isError ? <ErrorState error={receipts.error} /> : receipts.data.items.length === 0 ? <p className="cat-muted">Aucune réception.</p> : (
        <>
          <table className="cat-table">
            <thead><tr><th scope="col">Numéro</th><th scope="col">Date</th><th scope="col">Fournisseur</th><th scope="col">Bon de livraison</th><th scope="col">Emplacement</th><th scope="col" className="cat-num">Reçu</th><th scope="col">Reliquat</th><th scope="col" /></tr></thead>
            <tbody>{receipts.data.items.map((r) => (
              <tr key={r.id}>
                <td className="cat-mono">{r.number}{r.completesReceiptId && <span className="cat-muted"> (complément)</span>}</td>
                <td>{formatDateTime(r.receivedAtUtc)}</td><td>{r.supplierName}</td><td>{r.supplierDocumentReference ?? "—"}</td><td>{r.locationCode}</td>
                <td className="cat-num">{r.receivedQuantity}</td>
                <td>{r.outstandingQuantity === null ? "—" : r.outstandingQuantity > 0 ? <Badge tone="warning">{r.outstandingQuantity} à recevoir</Badge> : <Badge tone="success">Complète</Badge>}</td>
                <td>{access.stockMovementsWrite && (r.outstandingQuantity ?? 0) > 0 && <Button size="sm" variant="tertiary" onClick={() => setCreating(r.id)}>Recevoir le reliquat</Button>}</td>
              </tr>
            ))}</tbody>
          </table>
          <Pager page={receipts.data.page} pageSize={receipts.data.pageSize} total={receipts.data.totalCount} onPage={setPage} />
        </>
      )}
    </section>
  );
}

function ReceiptForm({ completes, onClose }: { completes: string | null; onClose: () => void }) {
  const access = useCatalogAccess();
  const { store } = useActiveStore();
  const suppliers = useCatalogQuery<Page<SupplierRow>>("/api/suppliers?pageSize=200");
  const write = useCatalogWrite();
  const [supplierId, setSupplierId] = useState("");
  const [locationId, setLocationId] = useState(store?.locations.find((l) => l.isDefault)?.id ?? "");
  const [document, setDocument] = useState("");
  const [lines, setLines] = useState<ReceiptLineDraft[]>([]);
  const [skuSearch, setSkuSearch] = useState("");
  const variants = useCatalogQuery<Page<VariantRow>>(store && skuSearch.length >= 2 ? `/api/catalog/variants${query({ storeId: store.id, q: skuSearch, policy: "stocked", pageSize: 10 })}` : null);
  const errors = write.error?.error.fieldErrors;
  if (!store) return null;
  return (
    <Panel title={completes ? "Réception de complément (reliquat)" : "Nouvelle réception"}>
      <ErrorBanner error={write.error} />
      <div className="cat-grid">
        <SelectField label="Fournisseur" name="supplierId" value={supplierId} onChange={setSupplierId} required errors={errors}
          options={[{ value: "", label: "— Choisir —" }, ...(suppliers.data?.items ?? []).filter((s) => s.status === "active").map((s) => ({ value: s.id, label: s.name }))]} />
        <SelectField label="Emplacement" name="locationId" value={locationId} onChange={setLocationId} required errors={errors}
          options={store.locations.filter((l) => l.status === "active").map((l) => ({ value: l.id, label: l.name }))} />
        <Field label="N° de bon de livraison" name="supplierDocumentReference" value={document} onChange={setDocument} errors={errors} />
      </div>
      <div className="cat-inline">
        <Field label="Ajouter une variante stockée (SKU ou désignation)" name="receiptSearch" value={skuSearch} onChange={setSkuSearch} />
        {(variants.data?.items ?? []).filter((v) => !lines.some((l) => l.variantId === v.variantId)).slice(0, 5).map((v) => (
          <Button key={v.variantId} size="sm" onClick={() => { setLines((c) => [...c, { variantId: v.variantId, sku: v.sku, announced: "", received: "", cost: "" }]); setSkuSearch(""); }}>
            {v.sku} · {v.label}
          </Button>
        ))}
      </div>
      {lines.length > 0 && (
        <table className="cat-table">
          <thead><tr><th scope="col">SKU</th><th scope="col">Quantité annoncée (BL)</th><th scope="col">Quantité reçue</th>{access.costsWrite && <th scope="col">Coût unitaire HT ({store.currency})</th>}<th scope="col" /></tr></thead>
          <tbody>{lines.map((line, i) => {
            const update = (patch: Partial<ReceiptLineDraft>) => setLines((c) => c.map((l, j) => (j === i ? { ...l, ...patch } : l)));
            return (
              <tr key={line.variantId}>
                <td className="cat-mono">{line.sku}</td>
                <td><input className="cat-input" inputMode="numeric" aria-label={`Annoncé ${line.sku}`} value={line.announced} onChange={(e) => update({ announced: e.target.value })} /></td>
                <td><input className="cat-input" inputMode="numeric" aria-label={`Reçu ${line.sku}`} value={line.received} onChange={(e) => update({ received: e.target.value })} /></td>
                {access.costsWrite && <td><input className="cat-input" inputMode="decimal" aria-label={`Coût ${line.sku}`} value={line.cost} onChange={(e) => update({ cost: e.target.value })} /></td>}
                <td><Button size="sm" variant="danger" onClick={() => setLines((c) => c.filter((_, j) => j !== i))}>Retirer</Button></td>
              </tr>
            );
          })}</tbody>
        </table>
      )}
      <div className="cat-actions">
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={lines.length === 0 || write.isPending}
          onClick={() => write.mutate({ method: "POST", path: "/api/stock/receipts", body: {
            locationId, supplierId: supplierId || null, currency: store.currency, supplierDocumentReference: document.trim() || null, completesReceiptId: completes,
            lines: lines.map((l) => ({
              variantId: l.variantId, announcedQuantity: l.announced.trim() === "" ? null : Number(l.announced), receivedQuantity: Number(l.received || "0"),
              ...(access.costsWrite && l.cost.trim() !== "" ? { unitCostHt: l.cost.trim().replace(",", ".") } : {}),
            })),
          } }, { onSuccess: onClose })}>
          Enregistrer la réception
        </Button>
      </div>
    </Panel>
  );
}
