// Inventaires — Lot 6 groupes 24–27 (liste, nouvel inventaire, inventaire en cours avec barre de progression, saisie
// d'une quantité, comparaison théorique / constaté, liste des écarts, confirmation des ajustements, inventaire terminé,
// abandon, conflit multi-poste, historique) sur les règles V2 : sessions PERSISTÉES (historique réel, pas une cible),
// stock jamais gelé — chaque ligne mémorise le théorique au moment du comptage —, comptage (inventory.count) distinct
// de la justification / validation / abandon (inventory.validate), validation TOUT OU RIEN qui applique l'écart compté
// au stock COURANT (jamais d'écrasement des mouvements concurrents), ligne modifiée depuis son comptage signalée.
import {
  ArrowLeftRight, Box, ClipboardCheck, ClipboardList, Eye, EyeOff, Info, ListChecks, Loader, Play, Plus, Scale, TriangleAlert, X,
} from "lucide-react";
import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { type Family, type InventoryLine, type InventorySummary, type Page, query } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { FAMILY_SHORT_LABELS, formatDateTime, REASONS, reasonLabel } from "./catalogFormat.ts";
import {
  Badge, Banner, Diff, EmptyState, ErrorBanner, ErrorState, Field, Loading, Modal, PageHeader, RadioCard, RefreshBar, RowChevron, Switch, TableFoot, TablePanel,
} from "./CatalogUi.tsx";
import { errorMessage, rowLink, useActiveStore, useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";

function StatusBadge({ status }: { status: InventorySummary["status"] }) {
  if (status === "open") return <Badge tone="info" icon={Loader}>En cours</Badge>;
  if (status === "validated") return <Badge tone="ok" icon={ClipboardCheck}>Validé</Badge>;
  return <Badge tone="arch" icon={X}>Abandonné</Badge>;
}

const scopeLabel = (s: InventorySummary) =>
  `${s.scopeType === "full" ? "Tout le stock" : s.scopeType === "family" ? `Famille ${FAMILY_SHORT_LABELS[s.scopeFamilyCode ?? ""] ?? s.scopeFamilyCode}` : "Sélection"}${s.blind ? " · à l'aveugle" : ""}`;

// ---------- liste (invList, invNew) ----------

export function InventoriesView() {
  const access = useCatalogAccess();
  const navigate = useNavigate();
  const { store } = useActiveStore();
  const [page, setPage] = useState(1);
  const [opening, setOpening] = useState(false);
  const list = useCatalogQuery<Page<InventorySummary>>(store ? `/api/inventories${query({ storeId: store.id, page, pageSize: 50 })}` : null);
  return (
    <section className="cat-screen" aria-labelledby="inventories-title">
      <PageHeader title="Inventaires" subtitle="Comptages physiques et ajustements de stock — sessions conservées dans l'historique"
        actions={access.inventoryCount && <Button variant="primary" onClick={() => setOpening(true)}><Plus size={18} aria-hidden="true" />Nouvel inventaire</Button>} />
      {list.isPending ? <Loading label="Chargement des inventaires…" /> : list.isError ? (
        <ErrorState error={list.error} title="Impossible de charger les inventaires" onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <EmptyState icon={ClipboardList} title="Aucun inventaire" action={access.inventoryCount && <Button variant="primary" onClick={() => setOpening(true)}><Plus size={18} aria-hidden="true" />Nouvel inventaire</Button>}>
          Lancez un comptage complet ou limité à une famille ; les mouvements restent permis pendant le comptage.
        </EmptyState>
      ) : (
        <TablePanel fill foot={
          <TableFoot icon={ClipboardList} page={list.data.page} pageSize={list.data.pageSize} total={list.data.totalCount} onPage={setPage}>
            {list.data.totalCount} inventaire{list.data.totalCount > 1 ? "s" : ""} · validation tout ou rien
          </TableFoot>
        }>
          <table className="clients-table cat-table">
            <thead><tr><th scope="col">Référence</th><th scope="col">Ouvert le</th><th scope="col">Périmètre</th><th scope="col" className="cat-hide-sm">Par</th><th scope="col" className="cat-num">Comptées</th><th scope="col" className="cat-num">Écarts</th><th scope="col">Statut</th><th scope="col"><span className="sr-only">Ouvrir</span></th></tr></thead>
            <tbody>{list.data.items.map((s) => (
              <tr key={s.id} className={cn("cat-row", s.status === "open" && "is-selected")} {...rowLink(`/inventaire/inventaires/${s.id}`, (href) => navigate(href))}>
                <td><span className="cat-ref">{s.number}</span></td>
                <td className="clients-muted tnum">{formatDateTime(s.openedAtUtc)}</td>
                <td>{scopeLabel(s)}</td>
                <td className="cat-secondary cat-hide-sm">{s.openedBy.displayName}</td>
                <td className="cat-num tnum">{s.countedCount} / {s.lineCount}</td>
                <td className="cat-num">{s.status === "abandoned" ? <span className="clients-muted">—</span> : <span className={cn("cat-diff", s.differenceCount > 0 ? "cat-diff--neg" : "cat-diff--zero")}>{s.differenceCount}</span>}</td>
                <td><StatusBadge status={s.status} /></td>
                <td><RowChevron /></td>
              </tr>
            ))}</tbody>
          </table>
        </TablePanel>
      )}
      <OpenInventoryModal open={opening} onClose={() => setOpening(false)} onOpened={(id) => navigate(`/inventaire/inventaires/${id}`)} />
    </section>
  );
}

function OpenInventoryModal({ open, onClose, onOpened }: { open: boolean; onClose: () => void; onOpened: (id: string) => void }) {
  const { store } = useActiveStore();
  const toast = useToast();
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  const write = useCatalogWrite<{ id: string }>();
  const [scope, setScope] = useState("full");
  const [family, setFamily] = useState("");
  const [blind, setBlind] = useState(false);
  const [locations, setLocations] = useState<string[]>([]);
  if (!store) return null;
  const stockable = (Array.isArray(families.data) ? families.data : []).filter((f) => f.allowedPolicies.includes("stocked"));
  return (
    <Modal open={open} onClose={onClose} tone="accent" icon={ClipboardList} title="Nouvel inventaire" size="wide"
      description="Lancez un comptage physique. Les mouvements restent permis : chaque ligne mémorise le stock théorique au moment où elle est comptée ; la validation applique l'écart au stock courant."
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending || (scope === "family" && family === "")} onClick={() => write.mutate(
          { method: "POST", path: "/api/inventories", body: { storeId: store.id, locationIds: locations.length > 0 ? locations : null, scopeType: scope, scopeFamilyCode: scope === "family" ? family || null : null, blind } },
          { onSuccess: (r) => { toast.show("Inventaire ouvert", "Le comptage peut commencer."); onClose(); onOpened(r.id); } },
        )}><Play size={16} aria-hidden="true" />Démarrer le comptage</Button>
      </>}>
      <ErrorBanner error={write.error} />
      <div className="cat-radios">
        <RadioCard name="scope" value="full" checked={scope === "full"} onChange={setScope} label="Tout le stock" sub="Toutes les variantes stockées du magasin" />
        <RadioCard name="scope" value="family" checked={scope === "family"} onChange={setScope} label="Une famille" sub="Comptage limité à une famille de produits" />
      </div>
      {scope === "family" && (
        <div className="cat-radios cat-radios--row">
          {stockable.map((f) => <RadioCard key={f.code} name="family" value={f.code} checked={family === f.code} onChange={setFamily} label={f.label} />)}
        </div>
      )}
      <fieldset className="clients-field" style={{ border: 0, margin: 0, padding: 0 }}>
        <legend className="clients-field__label" style={{ marginBottom: 7 }}>Emplacements (aucun coché = tous)</legend>
        <div className="cat-checks">
          {store.locations.filter((l) => l.status === "active").map((l) => (
            <label key={l.id} className="cat-check"><input type="checkbox" checked={locations.includes(l.id)} onChange={(e) => setLocations((c) => (e.target.checked ? [...c, l.id] : c.filter((x) => x !== l.id)))} />{l.name}</label>
          ))}
        </div>
      </fieldset>
      <Switch label="Comptage à l'aveugle" checked={blind} onChange={setBlind} hint="Les quantités théoriques sont masquées pendant le comptage." />
    </Modal>
  );
}

// ---------- session (invInProgress, invCount, invCompare, invDiffs, invConfirm, invDone, invAbandon, invConflict, invHistory) ----------

type Mode = "count" | "compare" | "diffs";

export function InventorySessionView() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const access = useCatalogAccess();
  const toast = useToast();
  const [mode, setMode] = useState<Mode>("count");
  const detail = useCatalogQuery<{ summary: InventorySummary; lines: InventoryLine[] }>(`/api/inventories/${id}`);
  const write = useCatalogWrite();
  const [validating, setValidating] = useState(false);
  const [abandoning, setAbandoning] = useState(false);
  const [uncounted, setUncounted] = useState("exclude");
  const [acceptChanged, setAcceptChanged] = useState(false);
  const [abandonReason, setAbandonReason] = useState("");
  const [touched, setTouched] = useState(false);
  if (detail.isPending) return <Loading label="Chargement de l'inventaire…" />;
  if (detail.isError) return <ErrorState error={detail.error} title="Impossible de charger l'inventaire" onRetry={() => void detail.refetch()} />;
  const { summary, lines } = detail.data;
  const open = summary.status === "open";
  const blind = summary.blind && open;
  const changed = lines.filter((l) => l.changedSinceCount);
  const diffs = lines.filter((l) => l.difference !== null && l.difference !== 0);
  const unjustified = diffs.filter((l) => !l.reasonCode);
  const pct = summary.lineCount === 0 ? 0 : Math.round((summary.countedCount / summary.lineCount) * 100);
  const view: Mode = blind ? "count" : mode;
  const shown = view === "diffs" ? diffs : lines;

  return (
    <section className="cat-screen" aria-labelledby="inventory-title">
      <PageHeader
        back={{ label: "Inventaires", onClick: () => navigate("/inventaire/inventaires") }}
        title={`Inventaire ${summary.number}`}
        titleExtra={<StatusBadge status={summary.status} />}
        subtitle={`${scopeLabel(summary)} · ouvert le ${formatDateTime(summary.openedAtUtc)} par ${summary.openedBy.displayName}`}
        actions={open && access.inventoryValidate && <>
          <Button variant="danger" onClick={() => { write.reset(); setTouched(false); setAbandoning(true); }}><X size={16} aria-hidden="true" />Abandonner</Button>
          {view === "count" && !blind ? (
            <Button variant="primary" onClick={() => setMode("compare")}><ArrowLeftRight size={16} aria-hidden="true" />Comparer les quantités</Button>
          ) : (
            <Button variant="primary" onClick={() => { write.reset(); setAcceptChanged(false); setValidating(true); }}><Scale size={16} aria-hidden="true" />Valider les ajustements</Button>
          )}
        </>}
      />
      {summary.status === "validated" && (
        <Banner tone="ok" icon={ClipboardCheck} title="Inventaire terminé">
          Validé le {formatDateTime(summary.validatedAtUtc)} · {summary.differenceCount} écart{summary.differenceCount > 1 ? "s" : ""} appliqué{summary.differenceCount > 1 ? "s" : ""} au stock courant par des mouvements « correction d&apos;inventaire ».
          Session conservée dans l&apos;historique.
        </Banner>
      )}
      {summary.status === "abandoned" && (
        <Banner tone="neutral" icon={X} title="Inventaire abandonné">
          Abandonné le {formatDateTime(summary.abandonedAtUtc)}, sans effet sur le stock.{summary.abandonReason ? ` Motif : ${summary.abandonReason}.` : ""}
        </Banner>
      )}
      {open && changed.length > 0 && (
        <Banner tone="warn" icon={TriangleAlert} title="Stock modifié sur un autre poste pendant le comptage" role="status">
          {changed.length} ligne{changed.length > 1 ? "s ont" : " a"} bougé depuis {changed.length > 1 ? "leur" : "son"} comptage (vente, réception, mouvement). Recomptez ou acceptez explicitement à la validation :
          l&apos;écart compté sera appliqué au stock courant, sans effacer ces mouvements.
        </Banner>
      )}
      <ErrorBanner error={write.error && !validating && !abandoning ? write.error : null} onReload={() => void detail.refetch()} />
      {open && <RefreshBar asOf={new Date(detail.dataUpdatedAt).toISOString()} onRefresh={() => void detail.refetch()} refreshing={detail.isFetching} warn={changed.length > 0} />}
      <div className="cat-progress">
        <ClipboardCheck size={18} aria-hidden="true" />
        <div className="cat-progress__main">
          <div className="cat-progress__head"><span>Progression de la saisie</span><span className="tnum">{summary.countedCount} / {summary.lineCount} ({pct} %)</span></div>
          <div className="cat-progress__track" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Lignes comptées"><div className="cat-progress__bar" style={{ width: `${pct}%` }} /></div>
        </div>
        <span className="cat-ref">{summary.number}</span>
      </div>
      {!blind && (
        <div className="cat-seg" role="tablist" aria-label="Vue de l'inventaire" style={{ alignSelf: "flex-start" }}>
          <button type="button" role="tab" aria-selected={view === "count"} className="cat-seg__item" onClick={() => setMode("count")}><ListChecks size={14} aria-hidden="true" />Saisie</button>
          <button type="button" role="tab" aria-selected={view === "compare"} className="cat-seg__item" onClick={() => setMode("compare")}><ArrowLeftRight size={14} aria-hidden="true" />Comparaison</button>
          <button type="button" role="tab" aria-selected={view === "diffs"} className="cat-seg__item" onClick={() => setMode("diffs")}><Scale size={14} aria-hidden="true" />Écarts ({diffs.length})</button>
        </div>
      )}
      {blind && <Banner tone="info" icon={EyeOff} title="Comptage à l'aveugle" compact>Les quantités théoriques et les écarts restent masqués jusqu&apos;à la clôture.</Banner>}
      {view === "diffs" && shown.length === 0 ? (
        <EmptyState icon={ClipboardCheck} title="Aucun écart constaté">Les lignes comptées correspondent au stock théorique au moment du comptage.</EmptyState>
      ) : (
        <TablePanel fill foot={
          <TableFoot icon={Box} right={<span className="clients-muted">Écart compté appliqué au stock courant à la validation</span>}>
            {view === "count" ? `${summary.countedCount} / ${summary.lineCount} lignes comptées` : view === "compare" ? `${lines.length} lignes · ${diffs.length} écart${diffs.length > 1 ? "s" : ""}` : `${diffs.length} écart${diffs.length > 1 ? "s" : ""} · ${unjustified.length} à justifier`}
          </TableFoot>
        }>
          <table className="clients-table cat-table">
            <thead>
              <tr>
                <th scope="col">Produit</th>
                <th scope="col" className="cat-hide-sm">Emplacement</th>
                {!blind && <th scope="col" className="cat-num">Stock théorique</th>}
                <th scope="col" className="cat-num">{view === "count" ? "Quantité constatée" : "Stock constaté"}</th>
                {view !== "count" && <th scope="col" className="cat-num">Écart</th>}
                {view === "diffs" && <th scope="col">Motif de l&apos;écart</th>}
                <th scope="col">{view === "count" ? "Statut" : view === "diffs" ? "Résolution" : "Statut"}</th>
              </tr>
            </thead>
            <tbody>{shown.map((line) => (
              <InventoryLineRow key={`${line.variantId}-${line.locationId}`} sessionId={summary.id} line={line} open={open} blind={blind} mode={view} />
            ))}</tbody>
          </table>
        </TablePanel>
      )}

      <Modal open={validating} onClose={() => setValidating(false)} tone="warn" icon={Scale} title="Confirmer les ajustements ?" size="wide"
        description={`${diffs.length} écart${diffs.length > 1 ? "s" : ""} deviendra${diffs.length > 1 ? "ont" : ""} des mouvements « correction d'inventaire » appliqués au stock COURANT, en une seule transaction (tout ou rien). Aucun mouvement intervenu pendant le comptage n'est effacé.`}
        actions={<>
          <Button onClick={() => setValidating(false)}>Annuler</Button>
          <Button variant="primary" disabled={write.isPending || (changed.length > 0 && !acceptChanged)}
            onClick={() => write.mutate({ method: "POST", path: `/api/inventories/${summary.id}/validate`, body: { version: summary.version, uncounted, acceptChangedLines: acceptChanged } },
              { onSuccess: () => { setValidating(false); toast.show("Inventaire validé", `${diffs.length} ajustement(s) appliqué(s) au stock.`); } })}>
            <ClipboardCheck size={16} aria-hidden="true" />Appliquer les ajustements
          </Button>
        </>}>
        {write.error && <Banner tone={write.error.error.code === "version_conflict" ? "warn" : "err"} icon={Info} title="Validation refusée" role="alert" compact>{errorMessage(write.error.error)}</Banner>}
        {diffs.length > 0 && (
          <div className="cat-difflist">
            {diffs.slice(0, 8).map((l) => (
              <div key={`${l.variantId}-${l.locationId}`} className="cat-difflist__row">
                <span style={{ flex: 1, fontWeight: 600 }}>{l.modelName} · {l.label} <span className="cat-sub cat-mono">{l.sku} · {l.locationCode}</span></span>
                <span className="clients-muted tnum">{l.expectedAtCount ?? "—"} →</span>
                <span className="tnum" style={{ fontWeight: 800 }}>{l.countedQuantity}</span>
                <span style={{ minWidth: 34, textAlign: "right" }}><Diff value={l.difference} /></span>
                {l.reasonCode ? <Badge tone="ok">{reasonLabel(l.reasonCode)}</Badge> : <Badge tone="err">Motif manquant</Badge>}
              </div>
            ))}
            {diffs.length > 8 && <div className="cat-difflist__row clients-muted">… et {diffs.length - 8} autre(s) écart(s)</div>}
          </div>
        )}
        {unjustified.length > 0 && <Banner tone="err" icon={TriangleAlert} title={`${unjustified.length} écart(s) sans motif`} compact>Chaque écart doit être justifié (vue « Écarts ») ; le serveur refuse sinon la validation.</Banner>}
        <div className="cat-radios">
          <RadioCard name="uncounted" value="exclude" checked={uncounted === "exclude"} onChange={setUncounted} label="Lignes non comptées : exclure" sub="Aucun effet sur leur stock" />
          <RadioCard name="uncounted" value="set_to_zero" checked={uncounted === "set_to_zero"} onChange={setUncounted} label="Lignes non comptées : mettre à 0" sub="Écart justifié exigé pour chacune" />
        </div>
        {changed.length > 0 && (
          <label className="cat-check cat-check--plain"><input type="checkbox" checked={acceptChanged} onChange={(e) => setAcceptChanged(e.target.checked)} />
            J&apos;accepte les {changed.length} ligne(s) modifiées depuis le comptage (l&apos;écart compté sera appliqué au stock courant)</label>
        )}
      </Modal>
      <Modal open={abandoning} onClose={() => setAbandoning(false)} tone="err" icon={X} title="Abandonner l'inventaire ?"
        description="Abandon irréversible, sans aucun effet sur le stock ; la session et ses comptages restent consultables dans l'historique."
        actions={<>
          <Button onClick={() => setAbandoning(false)}>Continuer le comptage</Button>
          <Button className="cat-btn-danger-solid" disabled={write.isPending}
            onClick={() => { setTouched(true); if (abandonReason.trim() === "") return; write.mutate({ method: "POST", path: `/api/inventories/${summary.id}/abandon`, body: { version: summary.version, reason: abandonReason } },
              { onSuccess: () => { setAbandoning(false); toast.show("Inventaire abandonné", "Aucun effet sur le stock."); } }); }}>
            <X size={16} aria-hidden="true" />Abandonner l&apos;inventaire
          </Button>
        </>}>
        {write.error && <Banner tone="err" icon={Info} title="Abandon refusé" role="alert" compact>{errorMessage(write.error.error)}</Banner>}
        <Field label="Motif de l'abandon" name="reason" value={abandonReason} onChange={setAbandonReason} required multiline errors={write.error?.error.fieldErrors}
          error={touched && abandonReason.trim() === "" ? "Le motif est obligatoire." : null} />
      </Modal>
    </section>
  );
}

const RESOLUTIONS: Record<string, string> = { applied: "Appliqué", no_difference: "Sans écart", excluded: "Exclue", set_to_zero: "Mise à 0" };

function InventoryLineRow({ sessionId, line, open, blind, mode }: { sessionId: string; line: InventoryLine; open: boolean; blind: boolean; mode: Mode }) {
  const access = useCatalogAccess();
  const write = useCatalogWrite();
  const [counted, setCounted] = useState(line.countedQuantity?.toString() ?? "");
  const [editing, setEditing] = useState(false);
  const path = `/api/inventories/${sessionId}/lines/${line.variantId}/${line.locationId}`;
  const canCount = open && access.inventoryCount;
  const valid = /^\d+$/.test(counted.trim());
  const isCounted = line.countedQuantity !== null;
  const step = (delta: number) => setCounted((c) => String(Math.max(0, (Number.parseInt(c, 10) || 0) + delta)));
  const save = () => valid && write.mutate({ method: "PUT", path, body: { version: line.version, countedQuantity: Number(counted) } }, { onSuccess: () => setEditing(false) });
  const showInput = canCount && mode === "count" && (!isCounted || editing);
  const theoretical = line.expectedAtCount ?? line.expectedAtOpen;

  return (
    <tr className={line.changedSinceCount ? "is-warning" : showInput && isCounted ? "is-selected" : undefined}>
      <td>
        <div className="clients-cell">
          <span className="cat-thumb"><Box size={18} aria-hidden="true" /></span>
          <div>
            <div className="clients-cell__name" style={{ fontSize: 13.5 }}>{line.modelName} <span className="cat-secondary" style={{ fontWeight: 500 }}>· {line.label}</span></div>
            <div className="clients-cell__ref">{line.sku}</div>
          </div>
        </div>
      </td>
      <td className="cat-secondary cat-hide-sm">{line.locationCode}</td>
      {!blind && (
        <td className="cat-num tnum clients-muted">
          {theoretical ?? "—"}
          {line.changedSinceCount && <div className="cat-sub cat-tone-warn">actuel : {line.currentOnHand}</div>}
        </td>
      )}
      <td className="cat-num">
        {showInput ? (
          <form className="cat-count" onSubmit={(e) => { e.preventDefault(); save(); }}>
            <span className="cat-qty">
              <button type="button" className="cat-qty__btn" aria-label={`Retirer une unité ${line.sku}`} onClick={() => step(-1)} disabled={counted.trim() === "" || counted === "0"}>−</button>
              <input inputMode="numeric" aria-label={`Quantité constatée ${line.sku}`} value={counted} onChange={(e) => setCounted(e.target.value)} />
              <button type="button" className="cat-qty__btn" aria-label={`Ajouter une unité ${line.sku}`} onClick={() => step(1)}>+</button>
            </span>
            <Button size="sm" variant="primary" type="submit" disabled={write.isPending || !valid}>{isCounted ? "Recompter" : "Compter"}</Button>
          </form>
        ) : isCounted ? (
          <span className="cat-count">
            <span className="tnum cat-strong" style={{ fontWeight: 700 }}>{line.countedQuantity}</span>
            {canCount && mode === "count" && <button type="button" className="cat-input-action" onClick={() => setEditing(true)}>Recompter</button>}
          </span>
        ) : <span className="clients-muted">—</span>}
        {write.error && <div className="clients-field__error" style={{ justifyContent: "flex-end" }}>{write.error.error.code === "version_conflict" ? "Ligne comptée sur un autre poste : rechargée." : errorMessage(write.error.error)}</div>}
        {showInput && counted.trim() !== "" && !valid && <div className="clients-field__error" style={{ justifyContent: "flex-end" }}>Entier positif ou nul attendu.</div>}
      </td>
      {mode !== "count" && <td className="cat-num"><Diff value={line.difference} /></td>}
      {mode === "diffs" && (
        <td>
          {open && access.inventoryValidate && line.difference !== null && line.difference !== 0 ? (
            <select className="cat-inline-select" aria-label={`Motif ${line.sku}`} value={line.reasonCode ?? ""}
              onChange={(e) => write.mutate({ method: "PUT", path: `${path}/reason`, body: { version: line.version, reasonCode: e.target.value, comment: e.target.value === "other" ? "Écart d'inventaire" : null } })}>
              <option value="">— Justifier —</option>
              {REASONS.adjustment.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
            </select>
          ) : line.reasonCode ? reasonLabel(line.reasonCode) : <span className="clients-muted">—</span>}
        </td>
      )}
      <td>
        {line.resolution ? <Badge tone={line.resolution === "applied" ? "ok" : "arch"}>{RESOLUTIONS[line.resolution] ?? line.resolution}</Badge>
          : line.changedSinceCount ? <Badge tone="warn" icon={TriangleAlert}>Modifié depuis le comptage</Badge>
            : showInput && isCounted ? <Badge tone="accent">Saisie en cours</Badge>
              : isCounted ? <Badge tone="ok" icon={ClipboardCheck}>Compté</Badge>
                : <Badge tone="arch" icon={blind ? EyeOff : Eye}>À compter</Badge>}
      </td>
    </tr>
  );
}
