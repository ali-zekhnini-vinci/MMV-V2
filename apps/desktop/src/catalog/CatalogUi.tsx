// Briques de composition des écrans Catalogue, stock & fournisseurs (Phase 05) — traduction du Lot 6 Claude Design
// (.phead, .toolbar, .chipbtn, .tbl / table.dt / .tbl-foot, .card, .form-grid / .field / .inp, .modal, .banner, .refbar,
// .statmini, .kv2, .cat, .toast) en RÉUTILISANT le vocabulaire V2 déjà porté pour les Lots 2–3 (Clients.css : en-tête,
// recherche, puces, panneau de table, états, champs) et les composants V2 (Button, DropdownMenu, AlertDialog Radix).
// Règles inchangées : magasin ACTIF choisi par le poste (devise du magasin, aucune conversion) ; données serveur par
// TanStack Query ; erreurs bornées traduites en français sans détail technique ; actions masquées selon les droits
// (garde d'affichage, le serveur reste seul juge).
import * as AlertDialogPrimitive from "@radix-ui/react-alert-dialog";
import {
  AlertTriangle, ArrowUpDown, Check, ChevronDown, ChevronLeft, ChevronRight, CircleAlert, History, type LucideIcon, RotateCw, Search, ShieldAlert,
  Store as StoreIcon, TriangleAlert, Wifi, WifiOff, X,
} from "lucide-react";
import { type ReactNode, useEffect, useMemo, useRef, useState } from "react";
import type { ActiveStore } from "../api/catalog.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { LoadingBlock, StateBlock, type StateTone } from "../clients/ClientStates.tsx";
import { SEARCH_DEBOUNCE_MS } from "../clients/clientsUi.ts";
import { REALTIME_LABELS, useRealtime } from "../realtime/realtimeContext.ts";
import { Button } from "../components/ui/button.tsx";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuTrigger,
} from "../components/ui/dropdown-menu.tsx";
import { cn } from "../lib/utils.ts";
import { errorMessage, StoreContext, type StoreContextValue, ToastContext, type ToastValue, useActiveStore, useCatalogQuery } from "./catalogData.ts";
import { FAMILY_SHORT_LABELS, fieldError, formatTime } from "./catalogFormat.ts";

// ---------- magasin de l'installation ----------

/** Motifs bornés d'une installation sans magasin résolu (`503 store_not_configured`). */
const STORE_PROBLEMS: Record<string, string> = {
  ambiguous: "Plusieurs magasins existent dans la base et aucun n'est désigné pour ce serveur.",
  no_store: "Aucun magasin n'existe encore dans la base de ce serveur.",
  unknown_code: "Le code magasin configuré pour ce serveur n'existe pas.",
  archived: "Le magasin configuré pour ce serveur est archivé.",
};

export function StoreProvider({ children }: { children: ReactNode }) {
  const query = useCatalogQuery<ActiveStore>("/api/store");
  const value = useMemo<StoreContextValue>(() => ({ store: query.data?.store ?? null, source: query.data?.source ?? null }), [query.data]);

  if (query.isPending) return <LoadingBlock label="Chargement du magasin…" />;
  if (query.isError && query.error.error.code === "store_not_configured") {
    return (
      <StateBlock tone="info" icon={StoreIcon} title="Magasin du serveur non configuré">
        <p>
          {STORE_PROBLEMS[query.error.error.reason ?? ""] ?? "Le serveur MMV n'a pas de magasin actif."} Chaque serveur MMV sert un seul magasin :
          l&apos;administrateur renseigne <code>Installation:StoreCode</code> dans la configuration du serveur (magasins créés par{" "}
          <code>MMV.Maintenance stores create</code>), puis redémarre l&apos;API. Aucun magasin n&apos;est choisi automatiquement en cas d&apos;ambiguïté.
        </p>
      </StateBlock>
    );
  }
  if (query.isError) return <ErrorState error={query.error} title="Impossible de charger le magasin" onRetry={() => void query.refetch()} />;
  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}

/** Magasin de l'installation (lecture seule, une fois par module) : nom et devise, jamais sélectionnable. */
export function StoreBadge() {
  const { store } = useActiveStore();
  if (!store) return null;
  return (
    <span className="cat-store" title={`Magasin de ce serveur MMV : ${store.name} (${store.code}) · devise ${store.currency}`}>
      <StoreIcon size={16} aria-hidden="true" />
      <span className="cat-store__label">Magasin</span>
      <span className="cat-store__name">{store.name}</span>
      <span className="cat-store__currency">{store.currency}</span>
    </span>
  );
}

// ---------- toast (.toast) ----------

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<{ tone: "ok" | "err"; title: string; text?: string; id: number } | null>(null);
  const value = useMemo<ToastValue>(() => ({ show: (title, text, tone = "ok") => setToast({ title, text, tone, id: Date.now() }) }), []);
  useEffect(() => {
    if (!toast) return undefined;
    const timer = window.setTimeout(() => setToast(null), 6000);
    return () => window.clearTimeout(timer);
  }, [toast]);
  return (
    <ToastContext.Provider value={value}>
      {children}
      {toast && (
        <div className={cn("cat-toast", toast.tone === "err" && "cat-toast--err")} role="status" aria-live="polite">
          <span className={cn("cat-toast__icon", toast.tone === "err" ? "cat-ic--err" : "cat-ic--ok")} aria-hidden="true">
            {toast.tone === "err" ? <CircleAlert size={20} /> : <Check size={20} />}
          </span>
          <div>
            <div className="cat-toast__title">{toast.title}</div>
            {toast.text && <div className="cat-toast__text">{toast.text}</div>}
          </div>
          <button type="button" className="cat-ract" aria-label="Fermer la notification" onClick={() => setToast(null)}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
      )}
    </ToastContext.Provider>
  );
}

// ---------- en-tête de page (.phead) ----------

export function PageHeader({ title, subtitle, actions, titleExtra, back }: {
  title: string;
  subtitle?: ReactNode;
  actions?: ReactNode;
  titleExtra?: ReactNode;
  back?: { label: string; onClick: () => void };
}) {
  return (
    <>
      {back && (
        <button type="button" className="cat-back" onClick={back.onClick}>
          <ChevronLeft size={16} aria-hidden="true" />
          {back.label}
        </button>
      )}
      <header className="clients-head">
        <div>
          <h1 className="clients-head__title">
            {title}
            {titleExtra && <span className="cat-title-extra">{titleExtra}</span>}
          </h1>
          {subtitle && <p className="clients-head__sub">{subtitle}</p>}
        </div>
        {actions && <div className="clients-actions clients-actions--wrap">{actions}</div>}
      </header>
    </>
  );
}

// ---------- barre d'outils (.toolbar, .tb-search, .chipbtn) ----------

/**
 * Recherche du Lot 6 sans bouton « Rechercher », MÊME COMPORTEMENT que la liste Clients (COR-05-07) : application
 * pendant la frappe après {@link SEARCH_DEBOUNCE_MS} ms ou sur Entrée, Échap vide le champ, espaces superflus retirés ;
 * la recherche est serveur (accents, casse, mots combinés) et l'écran revient à la page 1. `busy` : recherche en cours.
 */
export function SearchBox({ value, onSearch, placeholder, label, busy = false }: {
  value: string;
  onSearch: (value: string) => void;
  placeholder: string;
  label: string;
  busy?: boolean;
}) {
  const [text, setText] = useState(value);
  const latest = useRef(onSearch);
  useEffect(() => {
    latest.current = onSearch;
  });
  // Valeur réinitialisée par l'écran (ex. « Réinitialiser les filtres ») : le champ suit.
  const [shown, setShown] = useState(value);
  if (shown !== value) {
    setShown(value);
    if (text.trim() !== value) setText(value);
  }
  useEffect(() => {
    if (text.trim().replace(/\s+/g, " ") === value) return undefined;
    const timer = window.setTimeout(() => latest.current(text.trim().replace(/\s+/g, " ")), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [text, value]);
  const clear = () => { setText(""); onSearch(""); };
  return (
    <form className="clients-search" role="search" aria-busy={busy} onSubmit={(event) => { event.preventDefault(); onSearch(text.trim().replace(/\s+/g, " ")); }}>
      <Search size={18} aria-hidden="true" />
      <input type="search" value={text} placeholder={placeholder} aria-label={label} onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => { if (event.key === "Escape" && text !== "") { event.preventDefault(); clear(); } }} />
      {busy && <span className="cat-search-busy" role="status" aria-label="Recherche en cours" />}
      {text !== "" && (
        <button type="button" className="clients-search__clear" aria-label="Effacer la recherche" onClick={clear}>
          <X size={16} aria-hidden="true" />
        </button>
      )}
    </form>
  );
}

export interface ChipOption {
  value: string;
  label: string;
}

/**
 * Puce de filtre du Lot 6 (.chipbtn) : icône, libellé, puis badge accent portant la valeur choisie (ou chevron si aucune).
 * Menu Radix (flèches, Échap, retour du focus) ; la première option est la valeur « aucun filtre ».
 */
export function FilterChip({ icon: Icon, label, value, options, onChange, defaultValue = "" }: {
  icon: LucideIcon;
  label: string;
  value: string;
  options: ChipOption[];
  onChange: (value: string) => void;
  defaultValue?: string;
}) {
  const active = value !== defaultValue;
  const current = options.find((option) => option.value === value);
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className={cn("clients-chip", active && "clients-chip--on")} aria-label={`${label}${active && current ? ` : ${current.label}` : ""}`}>
          <Icon size={16} aria-hidden="true" />
          {label}
          {active && current ? <span className="cat-badge cat-badge--sm cat-badge--accent cat-chip__count">{current.label}</span> : <ChevronDown size={14} aria-hidden="true" />}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        <DropdownMenuRadioGroup value={value} onValueChange={onChange}>
          {options.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              {option.label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Puce bascule (.chipbtn.on) : « Stock faible », « Rupture », « Par famille ». */
export function ToggleChip({ icon: Icon, label, on, onToggle }: { icon: LucideIcon; label: string; on: boolean; onToggle: () => void }) {
  return (
    <button type="button" className={cn("clients-chip", on && "clients-chip--on")} aria-pressed={on} onClick={onToggle}>
      <Icon size={16} aria-hidden="true" />
      {label}
    </button>
  );
}

export function SortChip({ value, options, onChange }: { value: string; options: ChipOption[]; onChange: (value: string) => void }) {
  const current = options.find((option) => option.value === value);
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className="clients-chip" aria-label={`Trier : ${current?.label ?? ""}`}>
          <ArrowUpDown size={16} aria-hidden="true" />
          {current?.label ?? "Trier"}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuRadioGroup value={value} onValueChange={onChange}>
          {options.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              {option.label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function Toolbar({ children }: { children: ReactNode }) {
  return <div className="cat-toolbar">{children}</div>;
}

export function ToolbarSpacer() {
  return <span className="cat-toolbar__spacer" aria-hidden="true" />;
}

// ---------- table en carte (.tbl, table.dt, .tbl-foot) ----------

/** Table en carte ; `fill` : occupe la hauteur restante de l'écran (listes du Lot 6), défilement interne. */
export function TablePanel({ children, foot, className, fill }: { children: ReactNode; foot?: ReactNode; className?: string; fill?: boolean }) {
  return (
    <div className={cn("clients-panel cat-panel", fill && "cat-panel--fill", className)}>
      <div className="clients-panel__scroll">{children}</div>
      {foot}
    </div>
  );
}

export function TableFoot({ icon: Icon, children, page, pageSize, total, onPage, right }: {
  icon: LucideIcon;
  children: ReactNode;
  page?: number;
  pageSize?: number;
  total?: number;
  onPage?: (page: number) => void;
  right?: ReactNode;
}) {
  const pages = page !== undefined && pageSize && total !== undefined ? Math.max(1, Math.ceil(total / pageSize)) : null;
  return (
    <div className="clients-foot">
      <Icon size={16} aria-hidden="true" />
      <span>{children}</span>
      <span className="clients-foot__spacer" />
      {right}
      {pages !== null && page !== undefined && (
        <nav className="cat-foot__pager" aria-label="Pagination">
          <span className="clients-muted">
            Page {page} / {pages}
          </span>
          {pages > 1 && onPage && (
            <>
              <button type="button" className="cat-ract" aria-label="Page précédente" disabled={page <= 1} onClick={() => onPage(page - 1)}>
                <ChevronLeft size={18} aria-hidden="true" />
              </button>
              <button type="button" className="cat-ract" aria-label="Page suivante" disabled={page >= pages} onClick={() => onPage(page + 1)}>
                <ChevronRight size={18} aria-hidden="true" />
              </button>
            </>
          )}
        </nav>
      )}
    </div>
  );
}

export function SortHeader({ label, sort, current, dir, onSort, numeric, className }: {
  label: string;
  sort: string;
  current: string;
  dir: string;
  onSort: (sort: string) => void;
  numeric?: boolean;
  className?: string;
}) {
  const active = current === sort;
  return (
    <th scope="col" className={cn(numeric && "cat-num", className) || undefined} aria-sort={active ? (dir === "asc" ? "ascending" : "descending") : undefined}>
      <button type="button" className="clients-sort" onClick={() => onSort(sort)}>
        {label}
        <ChevronDown size={12} aria-hidden="true" className={active ? undefined : "clients-sort__idle"} style={active && dir === "asc" ? { transform: "rotate(180deg)" } : undefined} />
      </button>
    </th>
  );
}

export function RowChevron({ label = "Ouvrir" }: { label?: string }) {
  return (
    <div className="clients-rowacts">
      <span className="cat-ract" aria-hidden="true" title={label}>
        <ChevronRight size={18} />
      </span>
    </div>
  );
}

// ---------- badges, familles, stock ----------

export type BadgeTone = "ok" | "warn" | "err" | "info" | "accent" | "arch" | "teal";

export function Badge({ tone, children, icon: Icon, small = true }: { tone: BadgeTone; children: ReactNode; icon?: LucideIcon; small?: boolean }) {
  return (
    <span className={cn("cat-badge", small && "cat-badge--sm", `cat-badge--${tone}`)}>
      {Icon && <Icon size={12} aria-hidden="true" />}
      {children}
    </span>
  );
}

export function FamilyTag({ code, label }: { code: string; label?: string }) {
  return (
    <span className="cat-family">
      <span className="cat-family__dot" style={{ background: `var(--cat-family-${code}, var(--text-tertiary))` }} aria-hidden="true" />
      {FAMILY_SHORT_LABELS[code] ?? label ?? code}
    </span>
  );
}

export function Lock({ children, large }: { children: ReactNode; large?: boolean }) {
  return (
    <span className={cn("cat-lock", large && "cat-lock--lg")}>
      <ShieldAlert size={large ? 14 : 12} aria-hidden="true" />
      {children}
    </span>
  );
}

// ---------- cartes (.card) ----------

/**
 * Conteneur de tableau dans une carte (COR-05-03) : le tableau ne dépasse JAMAIS de sa carte ; s'il est plus large que
 * l'espace disponible, il défile horizontalement DANS ce conteneur (une seule barre, horizontale), au clavier aussi
 * (région focalisable). En-têtes et colonnes restent dans l'ordre ; rien n'est masqué en silence.
 */
export function TableScroll({ label, children, side = false }: { label: string; children: ReactNode; side?: boolean }) {
  return (
    <div className={cn("cat-tscroll", side && "cat-tscroll--side")} role="region" aria-label={label} tabIndex={0}>
      {children}
    </div>
  );
}

/**
 * Texte long d'une cellule (motif, commentaire) : une ligne coupée « … », détail consultable au clic ou à Entrée
 * (le texte complet se déplie dans la cellule), et au survol (info-bulle). Jamais de texte qui élargit le tableau.
 */
export function ClipText({ text, className }: { text: string; className?: string }) {
  const [open, setOpen] = useState(false);
  return (
    <button type="button" className={cn("cat-clip", className)} aria-expanded={open} title={open ? undefined : text}
      onClick={(event) => { event.stopPropagation(); setOpen((v) => !v); }}>
      {text}
    </button>
  );
}

export function Card({ title, icon: Icon, actions, children, foot, flush, className, id }: {
  title?: ReactNode;
  icon?: LucideIcon;
  actions?: ReactNode;
  children: ReactNode;
  foot?: ReactNode;
  flush?: boolean;
  className?: string;
  id?: string;
}) {
  return (
    <section className={cn("cat-card", className)} aria-labelledby={id}>
      {(title || actions) && (
        <div className="cat-card__head">
          {title && (
            <h2 className="cat-card__title" id={id}>
              {Icon && <Icon size={18} aria-hidden="true" />}
              {title}
            </h2>
          )}
          {actions}
        </div>
      )}
      <div className={cn("cat-card__body", flush && "cat-card__body--flush")}>{children}</div>
      {foot && <div className="cat-card__foot">{foot}</div>}
    </section>
  );
}

/** Section de formulaire (formSection) : carte, titre capitales 11,5 px avec icône, grille deux colonnes. */
export function FormSection({ title, icon: Icon, children, actions, grid = true }: { title: string; icon: LucideIcon; children: ReactNode; actions?: ReactNode; grid?: boolean }) {
  return (
    <section className="cat-card" aria-label={title}>
      <div className="cat-card__body">
        <p className="cat-fsec__title">
          <Icon size={16} aria-hidden="true" />
          {title}
          {actions && <span className="cat-fsec__title-spacer" />}
          {actions}
        </p>
        {grid ? <div className="cat-grid">{children}</div> : children}
      </div>
    </section>
  );
}

export function KeyValues({ rows, left, compact }: { rows: [ReactNode, ReactNode][]; left?: boolean; compact?: boolean }) {
  return (
    <dl className={cn("cat-kv", left && "cat-kv--left", compact && "cat-kv--compact")}>
      {rows.map(([key, value], index) => (
        <div key={index} style={{ display: "contents" }}>
          <dt>{key}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

// ---------- bandeaux (.banner) ----------

export type BannerTone = "warn" | "info" | "accent" | "err" | "ok" | "neutral";

export function Banner({ tone, icon: Icon, title, children, action, compact, role }: {
  tone: BannerTone;
  icon: LucideIcon;
  title: ReactNode;
  children?: ReactNode;
  action?: ReactNode;
  compact?: boolean;
  role?: "alert" | "status";
}) {
  return (
    <div className={cn("cat-banner", `cat-banner--${tone}`, compact && "cat-banner--compact")} role={role}>
      <span className={cn("cat-banner__icon", `cat-ic--${tone}`)} aria-hidden="true">
        <Icon size={compact ? 18 : 22} />
      </span>
      <div className="cat-banner__main">
        <div className="cat-banner__title">{title}</div>
        {children && <div className="cat-banner__text">{children}</div>}
      </div>
      {action}
    </div>
  );
}

/** Erreur d'écriture (code serveur borné) : conflit multi-poste en avertissement, refus métier en erreur. */
export function ErrorBanner({ error, onReload }: { error: ApiRequestError | null | undefined; onReload?: () => void }) {
  if (!error) return null;
  const code = error.error.code;
  const conflict = code === "version_conflict" || code === "stock_changed";
  return (
    <Banner
      tone={conflict ? "warn" : "err"}
      icon={conflict ? AlertTriangle : CircleAlert}
      title={conflict ? "Modifié sur un autre poste" : "Opération refusée"}
      role="alert"
      compact
      action={conflict && onReload && (
        <Button size="sm" className="cat-btn-warn" onClick={onReload}>
          <RotateCw size={16} aria-hidden="true" />
          Recharger
        </Button>
      )}
    >
      {errorMessage(error.error)}
    </Banner>
  );
}

// ---------- états (.state) ----------

export function ErrorState({ error, title = "Chargement impossible", onRetry }: { error: ApiRequestError; title?: string; onRetry?: () => void }) {
  if (error.error.kind === "forbidden") {
    return (
      <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
        <p>Le serveur a refusé cet accès pour votre profil. Aucune donnée n&apos;est affichée.</p>
      </StateBlock>
    );
  }
  return (
    <StateBlock
      tone="error"
      icon={TriangleAlert}
      title={title}
      role="alert"
      action={onRetry && (
        <Button onClick={onRetry}>
          <RotateCw size={16} aria-hidden="true" />
          Réessayer
        </Button>
      )}
    >
      <p>{errorMessage(error.error)} Aucun détail technique n&apos;est affiché.</p>
    </StateBlock>
  );
}

export function EmptyState({ tone = "accent", icon, title, children, action }: { tone?: StateTone; icon: LucideIcon; title: string; children: ReactNode; action?: ReactNode }) {
  return (
    <StateBlock tone={tone} icon={icon} title={title} action={action}>
      <p>{children}</p>
    </StateBlock>
  );
}

export function Loading({ label }: { label: string }) {
  return <LoadingBlock label={label} />;
}

// ---------- bandeau d'actualisation (.refbar) et KPI (.statmini) ----------

/**
 * Indicateur discret du temps réel (COR-05-02) : connecté, reconnexion en cours, déconnecté — données potentiellement
 * obsolètes. Remplace l'ancienne mention « pas de temps réel ». Annonce polie des changements d'état (lecteurs d'écran).
 */
export function RealtimeIndicator({ compact = false }: { compact?: boolean }) {
  const { status } = useRealtime();
  const tone = status === "connected" ? "ok" : status === "disconnected" || status === "off" ? "err" : "warn";
  return (
    <span className={cn("cat-live", `cat-live--${tone}`, compact && "cat-live--compact")} role="status" aria-live="polite" title={REALTIME_LABELS[status]}>
      <span className="cat-live__dot" aria-hidden="true" />
      {status === "connected" ? <Wifi size={14} aria-hidden="true" /> : <WifiOff size={14} aria-hidden="true" />}
      <span className={compact ? "sr-only" : undefined}>{REALTIME_LABELS[status]}</span>
    </span>
  );
}

export function RefreshBar({ asOf, onRefresh, refreshing, warn, children }: { asOf: string | undefined; onRefresh: () => void; refreshing?: boolean; warn?: boolean; children?: ReactNode }) {
  const { status } = useRealtime();
  return (
    <div className="cat-refbar">
      <span className={cn("cat-refbar__dot", (warn || status !== "connected") && "cat-refbar__dot--warn")} aria-hidden="true" />
      <History size={16} aria-hidden="true" />
      <span>{asOf ? `Dernier état chargé : aujourd'hui à ${formatTime(asOf)}${status === "connected" ? "" : " — actualisez avant d'agir"}` : "Chargement…"}</span>
      <RealtimeIndicator />
      {children}
      <span className="cat-refbar__spacer" />
      <Button size="sm" onClick={onRefresh} disabled={refreshing}>
        <RotateCw size={16} aria-hidden="true" className={refreshing ? "clients-spin" : undefined} />
        Actualiser
      </Button>
    </div>
  );
}

export function Kpi({ icon: Icon, tone, label, value, note, onClick, on }: {
  icon: LucideIcon;
  tone: "accent" | "neutral" | "warn" | "err";
  label: string;
  value: ReactNode;
  note?: string;
  onClick?: () => void;
  on?: boolean;
}) {
  const body = (
    <>
      <div className="cat-kpi__key">
        <span className={cn("cat-kpi__icon", `cat-ic--${tone}`)} aria-hidden="true">
          <Icon size={16} />
        </span>
        {label}
      </div>
      <div className="cat-kpi__value">{value}</div>
      {note && <div className="cat-kpi__note">{note}</div>}
    </>
  );
  return onClick ? (
    <button type="button" className={cn("cat-kpi cat-kpi--button", on && "cat-kpi--on")} aria-pressed={on} onClick={onClick}>
      {body}
    </button>
  ) : (
    <div className="cat-kpi">{body}</div>
  );
}

// ---------- champs (.field, .inp) ----------

interface FieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  errors?: Record<string, string[]>;
  errorKey?: string;
  type?: string;
  required?: boolean;
  hint?: ReactNode;
  inputMode?: "decimal" | "numeric" | "text";
  disabled?: boolean;
  readOnly?: boolean;
  multiline?: boolean;
  placeholder?: string;
  suffix?: ReactNode;
  span2?: boolean;
  autoFocus?: boolean;
  /** Message d'erreur local (validation de saisie avant envoi), prioritaire sur celui du serveur. */
  error?: string | null;
}

export function Field({
  label, name, value, onChange, errors, errorKey, type = "text", required, hint, inputMode, disabled, readOnly, multiline, placeholder, suffix, span2, autoFocus, error,
}: FieldProps) {
  const id = `cat-${name.replace(/[^a-zA-Z0-9_-]/g, "-")}`;
  const message = error ?? fieldError(errors, errorKey ?? name);
  const describedBy = message ? `${id}-error` : hint ? `${id}-hint` : undefined;
  return (
    <div className={cn("clients-field", span2 && "cat-span2")}>
      <label htmlFor={id} className="clients-field__label" data-required={required ? "" : undefined}>
        {label}
      </label>
      <div className={cn("clients-input", multiline && "clients-input--area", message && "clients-input--error")}>
        {multiline ? (
          <textarea id={id} rows={3} value={value} disabled={disabled} readOnly={readOnly} placeholder={placeholder} aria-invalid={message !== null} aria-describedby={describedBy}
            onChange={(e) => onChange(e.target.value)} />
        ) : (
          <input
            id={id}
            type={type}
            inputMode={inputMode}
            value={value}
            required={required}
            disabled={disabled}
            readOnly={readOnly}
            placeholder={placeholder}
            autoFocus={autoFocus}
            aria-invalid={message !== null}
            aria-describedby={describedBy}
            onChange={(e) => onChange(e.target.value)}
          />
        )}
        {suffix && <span className="cat-input-suffix">{suffix}</span>}
      </div>
      {message ? (
        <p id={`${id}-error`} className="clients-field__error">
          <CircleAlert size={12} aria-hidden="true" />
          {message}
        </p>
      ) : (
        hint && (
          <p id={`${id}-hint`} className="clients-field__hint">
            {hint}
          </p>
        )
      )}
    </div>
  );
}

export function SelectField({ label, name, value, onChange, options, errors, required, hint, disabled, span2 }: {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  errors?: Record<string, string[]>;
  required?: boolean;
  hint?: ReactNode;
  disabled?: boolean;
  span2?: boolean;
}) {
  const id = `cat-${name.replace(/[^a-zA-Z0-9_-]/g, "-")}`;
  const message = fieldError(errors, name);
  return (
    <div className={cn("clients-field", span2 && "cat-span2")}>
      <label htmlFor={id} className="clients-field__label" data-required={required ? "" : undefined}>
        {label}
      </label>
      <div className={cn("clients-input", message && "clients-input--error")}>
        <select id={id} value={value} disabled={disabled} aria-invalid={message !== null} aria-describedby={message ? `${id}-error` : undefined} onChange={(e) => onChange(e.target.value)}>
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>
      {message ? (
        <p id={`${id}-error`} className="clients-field__error">
          <CircleAlert size={12} aria-hidden="true" />
          {message}
        </p>
      ) : (
        hint && <p className="clients-field__hint">{hint}</p>
      )}
    </div>
  );
}

export function Switch({ label, checked, onChange, hint }: { label: string; checked: boolean; onChange: (checked: boolean) => void; hint?: string }) {
  return (
    <div className="clients-field">
      <label className="cat-switch">
        <input type="checkbox" role="switch" checked={checked} onChange={(e) => onChange(e.target.checked)} />
        <span className="cat-switch__track" aria-hidden="true" />
        {label}
      </label>
      {hint && <p className="clients-field__hint">{hint}</p>}
    </div>
  );
}

export function RadioCard({ name, value, checked, onChange, label, sub }: { name: string; value: string; checked: boolean; onChange: (value: string) => void; label: string; sub?: string }) {
  return (
    <label className="cat-radio">
      <input type="radio" name={name} value={value} checked={checked} onChange={() => onChange(value)} />
      <span>
        <span className="cat-radio__label">{label}</span>
        {sub && <span className="cat-radio__sub" style={{ display: "block" }}>{sub}</span>}
      </span>
    </label>
  );
}

/** Pied de formulaire contextualisé (productForm footer) : état à gauche, Annuler + action principale à droite. */
export function FormBar({ dirty, hint, children, narrow }: { dirty: boolean; hint: string; children: ReactNode; narrow?: boolean }) {
  return (
    <div className={cn("cat-formbar", narrow && "cat-formbar--narrow")}>
      {dirty ? <Badge tone="warn" small={false} icon={CircleAlert}>Modifications non enregistrées</Badge> : <span className="cat-formbar__hint">{hint}</span>}
      <span className="cat-formbar__spacer" />
      {children}
    </div>
  );
}

// ---------- modale (.modal, .modal.wide) ----------

export type ModalTone = "ok" | "warn" | "err" | "info" | "accent" | "neutral";

/**
 * Modale du Lot 6 sur la primitive Radix AlertDialog (piège de focus, Échap, retour du focus) : icône 60 px teintée,
 * titre 21 px, texte, corps libre, actions alignées à droite. `wide` = 660 px, `xl` = 860 px (réception multi-lignes).
 */
export function Modal({ open, onClose, tone, icon: Icon, title, description, children, actions, size = "default", busy }: {
  open: boolean;
  onClose: () => void;
  tone: ModalTone;
  icon: LucideIcon;
  title: string;
  description?: ReactNode;
  children?: ReactNode;
  actions: ReactNode;
  size?: "default" | "wide" | "xl";
  busy?: boolean;
}) {
  return (
    <AlertDialogPrimitive.Root open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <AlertDialogPrimitive.Portal>
        <AlertDialogPrimitive.Overlay className="mmv-dialog__overlay" />
        <AlertDialogPrimitive.Content className={cn("cat-modal", size === "wide" && "cat-modal--wide", size === "xl" && "cat-modal--xl")} aria-busy={busy}>
          <span className={cn("cat-modal__icon", `cat-ic--${tone}`)} aria-hidden="true">
            <Icon size={28} />
          </span>
          <AlertDialogPrimitive.Title className="cat-modal__title">{title}</AlertDialogPrimitive.Title>
          {description ? (
            <AlertDialogPrimitive.Description className="cat-modal__text">{description}</AlertDialogPrimitive.Description>
          ) : (
            <AlertDialogPrimitive.Description className="sr-only">{title}</AlertDialogPrimitive.Description>
          )}
          {children && <div className="cat-modal__body">{children}</div>}
          <div className="cat-modal__actions">{actions}</div>
        </AlertDialogPrimitive.Content>
      </AlertDialogPrimitive.Portal>
    </AlertDialogPrimitive.Root>
  );
}

// ---------- montants et quantités ----------

export function StockCell({ value, tone }: { value: ReactNode; tone: "ok" | "warn" | "err" | "muted" }) {
  return (
    <span className="cat-stock">
      <span className={cn("cat-dot", `cat-bg-${tone}`)} aria-hidden="true" />
      <span className={`cat-tone-${tone}`}>{value}</span>
    </span>
  );
}

export function Diff({ value }: { value: number | null }) {
  if (value === null) return <span className="cat-diff cat-diff--zero">—</span>;
  return <span className={cn("cat-diff", value > 0 ? "cat-diff--pos" : value < 0 ? "cat-diff--neg" : "cat-diff--zero")}>{value > 0 ? `+${value}` : value}</span>;
}
