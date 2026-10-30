// Inventaires (Lot 6 B-121 à B-124, adaptés : sessions persistées, historique réel, pas de gel du stock). Comptage
// (inventory.count) et justification / validation / abandon (inventory.validate) sont des droits DISTINCTS. Une ligne
// dont le stock a bougé depuis son comptage est signalée : recompter ou accepter explicitement ; la validation applique
// l'écart constaté au stock courant, jamais un écrasement.
import { ArrowLeft, ClipboardCheck, Plus } from "lucide-react";
import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { type Family, type InventoryLine, type InventorySummary, type Page, query } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import { formatDateTime, REASONS } from "./catalogFormat.ts";
import {
  Badge, Dialog, ErrorBanner, ErrorState, Field, Loading, PageHeader, Pager, Panel, SelectField, StoreSelect,
} from "./CatalogUi.tsx";
import { useActiveStore, useCatalogQuery, useCatalogWrite } from "./catalogData.ts";

const STATUS: Record<InventorySummary["status"], { label: string; tone: "info" | "success" | "neutral" }> = {
  open: { label: "En cours", tone: "info" },
  validated: { label: "Validé", tone: "success" },
  abandoned: { label: "Abandonné", tone: "neutral" },
};

export function InventoriesView() {
  const access = useCatalogAccess();
  const navigate = useNavigate();
  const { store } = useActiveStore();
  const [page, setPage] = useState(1);
  const [opening, setOpening] = useState(false);
  const list = useCatalogQuery<Page<InventorySummary>>(store ? `/api/inventories${query({ storeId: store.id, page, pageSize: 50 })}` : null);
  return (
    <section>
      <PageHeader title="Inventaires" actions={<>
        <StoreSelect />
        {access.inventoryCount && <Button variant="primary" onClick={() => setOpening(true)}><Plus size={16} aria-hidden="true" />Nouvel inventaire</Button>}
      </>} />
      {list.isPending ? <Loading label="Chargement des inventaires…" /> : list.isError ? <ErrorState error={list.error} /> : list.data.items.length === 0 ? <p className="cat-muted">Aucun inventaire pour ce magasin.</p> : (
        <>
          <table className="cat-table">
            <thead><tr><th scope="col">Numéro</th><th scope="col">Ouvert le</th><th scope="col">Par</th><th scope="col">Périmètre</th><th scope="col" className="cat-num">Lignes</th><th scope="col" className="cat-num">Comptées</th><th scope="col" className="cat-num">Écarts</th><th scope="col">Statut</th></tr></thead>
            <tbody>{list.data.items.map((s) => (
              <tr key={s.id} className="cat-row" tabIndex={0} onClick={() => navigate(`/inventaire/inventaires/${s.id}`)} onKeyDown={(e) => e.key === "Enter" && navigate(`/inventaire/inventaires/${s.id}`)}>
                <td className="cat-mono">{s.number}</td><td>{formatDateTime(s.openedAtUtc)}</td><td>{s.openedBy.displayName}</td>
                <td>{{ full: "Complet", family: `Famille ${s.scopeFamilyCode}`, selection: "Sélection" }[s.scopeType] ?? s.scopeType}{s.blind ? " · à l'aveugle" : ""}</td>
                <td className="cat-num">{s.lineCount}</td><td className="cat-num">{s.countedCount}</td><td className="cat-num">{s.differenceCount}</td>
                <td><Badge tone={STATUS[s.status].tone}>{STATUS[s.status].label}</Badge></td>
              </tr>
            ))}</tbody>
          </table>
          <Pager page={list.data.page} pageSize={list.data.pageSize} total={list.data.totalCount} onPage={setPage} />
        </>
      )}
      <OpenInventoryDialog open={opening} onClose={() => setOpening(false)} onOpened={(id) => navigate(`/inventaire/inventaires/${id}`)} />
    </section>
  );
}

function OpenInventoryDialog({ open, onClose, onOpened }: { open: boolean; onClose: () => void; onOpened: (id: string) => void }) {
  const { store } = useActiveStore();
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  const write = useCatalogWrite<{ id: string }>();
  const [scope, setScope] = useState("full");
  const [family, setFamily] = useState("");
  const [blind, setBlind] = useState(false);
  const [locations, setLocations] = useState<string[]>([]);
  if (!store) return null;
  return (
    <Dialog title="Nouvel inventaire" open={open} onClose={onClose}>
      <p className="cat-muted">Les mouvements restent permis pendant le comptage ; chaque ligne mémorise le stock théorique au moment où elle est comptée.</p>
      <ErrorBanner error={write.error} />
      <SelectField label="Périmètre" name="scopeType" value={scope} onChange={setScope} options={[{ value: "full", label: "Complet (toutes les variantes stockées)" }, { value: "family", label: "Une famille" }]} />
      {scope === "family" && (
        <SelectField label="Famille" name="scopeFamilyCode" value={family} onChange={setFamily} errors={write.error?.error.fieldErrors}
          options={[{ value: "", label: "— Choisir —" }, ...(Array.isArray(families.data) ? families.data : []).filter((f) => f.allowedPolicies.includes("stocked")).map((f) => ({ value: f.code, label: f.label }))]} />
      )}
      <fieldset className="cat-fieldset">
        <legend className="cat-label">Emplacements (aucun coché = tous)</legend>
        {store.locations.filter((l) => l.status === "active").map((l) => (
          <label key={l.id} className="cat-check"><input type="checkbox" checked={locations.includes(l.id)} onChange={(e) => setLocations((c) => (e.target.checked ? [...c, l.id] : c.filter((x) => x !== l.id)))} />{l.name}</label>
        ))}
      </fieldset>
      <label className="cat-check"><input type="checkbox" checked={blind} onChange={(e) => setBlind(e.target.checked)} />Comptage à l&apos;aveugle (quantités théoriques masquées)</label>
      <div className="cat-actions">
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending} onClick={() => write.mutate(
          { method: "POST", path: "/api/inventories", body: { storeId: store.id, locationIds: locations.length > 0 ? locations : null, scopeType: scope, scopeFamilyCode: scope === "family" ? family || null : null, blind } },
          { onSuccess: (r) => { onClose(); onOpened(r.id); } },
        )}>Ouvrir</Button>
      </div>
    </Dialog>
  );
}

export function InventorySessionView() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const access = useCatalogAccess();
  const [onlyDifferences, setOnlyDifferences] = useState(false);
  const detail = useCatalogQuery<{ summary: InventorySummary; lines: InventoryLine[] }>(`/api/inventories/${id}${query({ onlyDifferences: onlyDifferences ? "true" : null })}`);
  const write = useCatalogWrite();
  const [validating, setValidating] = useState(false);
  const [abandoning, setAbandoning] = useState(false);
  const [uncounted, setUncounted] = useState("exclude");
  const [acceptChanged, setAcceptChanged] = useState(false);
  const [abandonReason, setAbandonReason] = useState("");
  if (detail.isPending) return <Loading label="Chargement de l'inventaire…" />;
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
  const { summary, lines } = detail.data;
  const open = summary.status === "open";
  const changed = lines.filter((l) => l.changedSinceCount).length;

  return (
    <section>
      <PageHeader title={`Inventaire ${summary.number}`}
        subtitle={<><Badge tone={STATUS[summary.status].tone}>{STATUS[summary.status].label}</Badge> · {summary.countedCount} / {summary.lineCount} lignes comptées{summary.blind ? " · à l'aveugle" : ""}{summary.abandonReason ? ` · motif : ${summary.abandonReason}` : ""}</>}
        actions={<>
          <Button variant="tertiary" onClick={() => navigate("/inventaire/inventaires")}><ArrowLeft size={16} aria-hidden="true" />Inventaires</Button>
          {open && access.inventoryValidate && <Button variant="primary" onClick={() => setValidating(true)}><ClipboardCheck size={16} aria-hidden="true" />Valider</Button>}
          {open && access.inventoryValidate && <Button variant="danger" onClick={() => setAbandoning(true)}>Abandonner</Button>}
        </>} />
      <ErrorBanner error={write.error} />
      {changed > 0 && <p className="cat-alert" role="status">{changed} ligne(s) ont changé depuis leur comptage (mouvement sur un autre poste) : recomptez-les ou acceptez explicitement à la validation.</p>}
      {!summary.blind && <label className="cat-check"><input type="checkbox" checked={onlyDifferences} onChange={(e) => setOnlyDifferences(e.target.checked)} />Écarts seulement</label>}
      <Panel>
        <div className="cat-table-wrap">
          <table className="cat-table">
            <thead><tr><th scope="col">SKU</th><th scope="col">Produit</th><th scope="col">Emplacement</th>{!summary.blind || !open ? <th scope="col" className="cat-num">Théorique (au comptage)</th> : null}<th scope="col">Constaté</th>{!summary.blind || !open ? <th scope="col" className="cat-num">Écart</th> : null}<th scope="col">Motif</th><th scope="col">Résolution</th></tr></thead>
            <tbody>{lines.map((line) => <InventoryLineRow key={`${line.variantId}-${line.locationId}`} sessionId={summary.id} line={line} open={open} blind={summary.blind && open} />)}</tbody>
          </table>
        </div>
      </Panel>
      <Dialog title="Valider l'inventaire (tout ou rien)" open={validating} onClose={() => setValidating(false)}>
        <p>Chaque écart justifié devient un mouvement « correction d&apos;inventaire » appliqué au stock courant ; aucun mouvement intervenu pendant le comptage n&apos;est effacé.</p>
        <SelectField label="Lignes non comptées" name="uncounted" value={uncounted} onChange={setUncounted}
          options={[{ value: "exclude", label: "Exclure (aucun effet)" }, { value: "set_to_zero", label: "Mettre à 0 (écart justifié exigé)" }]} />
        {changed > 0 && <label className="cat-check"><input type="checkbox" checked={acceptChanged} onChange={(e) => setAcceptChanged(e.target.checked)} />J&apos;accepte les {changed} ligne(s) modifiées depuis le comptage (l&apos;écart compté sera appliqué au stock courant)</label>}
        <ErrorBanner error={write.error} />
        <div className="cat-actions">
          <Button onClick={() => setValidating(false)}>Annuler</Button>
          <Button variant="primary" disabled={write.isPending} onClick={() => write.mutate({ method: "POST", path: `/api/inventories/${summary.id}/validate`, body: { version: summary.version, uncounted, acceptChangedLines: acceptChanged } }, { onSuccess: () => setValidating(false) })}>Valider</Button>
        </div>
      </Dialog>
      <Dialog title="Abandonner l'inventaire" open={abandoning} onClose={() => setAbandoning(false)}>
        <p>Abandon irréversible, sans effet sur le stock ; la session reste dans l&apos;historique.</p>
        <Field label="Motif" name="reason" value={abandonReason} onChange={setAbandonReason} required multiline errors={write.error?.error.fieldErrors} />
        <div className="cat-actions">
          <Button onClick={() => setAbandoning(false)}>Annuler</Button>
          <Button variant="danger" onClick={() => write.mutate({ method: "POST", path: `/api/inventories/${summary.id}/abandon`, body: { version: summary.version, reason: abandonReason } }, { onSuccess: () => setAbandoning(false) })}>Abandonner</Button>
        </div>
      </Dialog>
    </section>
  );
}

function InventoryLineRow({ sessionId, line, open, blind }: { sessionId: string; line: InventoryLine; open: boolean; blind: boolean }) {
  const access = useCatalogAccess();
  const write = useCatalogWrite();
  const [counted, setCounted] = useState(line.countedQuantity?.toString() ?? "");
  const path = `/api/inventories/${sessionId}/lines/${line.variantId}/${line.locationId}`;
  const resolution: Record<string, string> = { applied: "Appliqué", no_difference: "Sans écart", excluded: "Exclue", set_to_zero: "Mise à 0" };
  return (
    <tr className={line.changedSinceCount ? "is-warning" : undefined}>
      <td className="cat-mono">{line.sku}</td>
      <td>{line.modelName} <span className="cat-muted">· {line.label}</span></td>
      <td>{line.locationCode}</td>
      {!blind && <td className="cat-num">{line.expectedAtCount ?? <span className="cat-muted">{line.expectedAtOpen ?? "—"}</span>}{line.changedSinceCount && <div className="cat-muted">actuel : {line.currentOnHand}</div>}</td>}
      <td>
        {open && access.inventoryCount ? (
          <form className="cat-inline" onSubmit={(e) => { e.preventDefault(); write.mutate({ method: "PUT", path, body: { version: line.version, countedQuantity: Number(counted) } }); }}>
            <input className="cat-input cat-input--qty" inputMode="numeric" aria-label={`Quantité constatée ${line.sku}`} value={counted} onChange={(e) => setCounted(e.target.value)} />
            <Button size="sm" type="submit" disabled={write.isPending || counted.trim() === ""}>{line.countedQuantity === null ? "Compter" : "Recompter"}</Button>
          </form>
        ) : (line.countedQuantity ?? <span className="cat-muted">non comptée</span>)}
        {write.error && <div className="cat-field__error">{write.error.error.code === "version_conflict" ? "Ligne comptée sur un autre poste : rechargée." : write.error.error.message}</div>}
      </td>
      {!blind && <td className={`cat-num ${line.difference && line.difference < 0 ? "cat-neg" : line.difference ? "cat-pos" : ""}`}>{line.difference === null ? "—" : line.difference > 0 ? `+${line.difference}` : line.difference}</td>}
      <td>
        {open && access.inventoryValidate && line.difference !== null && line.difference !== 0 ? (
          <select className="cat-input" aria-label={`Motif ${line.sku}`} value={line.reasonCode ?? ""}
            onChange={(e) => write.mutate({ method: "PUT", path: `${path}/reason`, body: { version: line.version, reasonCode: e.target.value, comment: e.target.value === "other" ? "Écart d'inventaire" : null } })}>
            <option value="">— Justifier —</option>
            {REASONS.adjustment.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
          </select>
        ) : (line.reasonCode ?? "—")}
      </td>
      <td>{line.resolution ? resolution[line.resolution] ?? line.resolution : "—"}</td>
    </tr>
  );
}
