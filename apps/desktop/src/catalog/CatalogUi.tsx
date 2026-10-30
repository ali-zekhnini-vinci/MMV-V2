// Briques communes des écrans Catalogue & stock (Phase 05, référence d'ergonomie : Lot 6 — D-05-01, interface seulement).
// Magasin ACTIF choisi par le poste (devise du magasin, aucune conversion) ; données serveur par TanStack Query ; erreurs
// bornées traduites en français, jamais de détail technique ; actions masquées selon les droits (garde d'affichage, le
// serveur reste seul juge).
import { AlertTriangle, Loader2, RefreshCw, ShieldAlert, WifiOff } from "lucide-react";
import { type ReactNode, useMemo, useState } from "react";
import type { Store } from "../api/catalog.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { StateBlock } from "../clients/ClientStates.tsx";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { errorMessage, StoreContext, type StoreContextValue, useActiveStore, useCatalogQuery } from "./catalogData.ts";
import { fieldError } from "./catalogFormat.ts";

export function StoreProvider({ children }: { children: ReactNode }) {
  const query = useCatalogQuery<Store[]>("/api/stores");
  const [storeId, setStoreId] = useState<string | null>(null);
  const value = useMemo<StoreContextValue>(() => {
    const stores = Array.isArray(query.data) ? query.data : [];
    return { stores, store: stores.find((s) => s.id === storeId) ?? stores[0] ?? null, setStoreId };
  }, [query.data, storeId]);

  if (query.isPending) return <p role="status" className="view__muted">Chargement des magasins…</p>;
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  if (value.stores.length === 0) {
    return (
      <StateBlock tone="info" icon={AlertTriangle} title="Aucun magasin configuré">
        <p>
          Les magasins sont créés par la commande de maintenance (<code>MMV.Maintenance stores create</code>), avec leur devise (EUR ou MAD).
          Aucun magasin fictif n&apos;est créé automatiquement.
        </p>
      </StateBlock>
    );
  }
  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}

export function StoreSelect() {
  const { stores, store, setStoreId } = useActiveStore();
  if (!store) return null;
  return (
    <label className="flex items-center gap-2 text-sm text-text-secondary">
      Magasin
      <select className="cat-input w-auto" value={store.id} onChange={(event) => setStoreId(event.target.value)} aria-label="Magasin actif">
        {stores.map((s) => (
          <option key={s.id} value={s.id}>
            {s.name} — {s.currency}
          </option>
        ))}
      </select>
    </label>
  );
}

// ---------- présentation ----------

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <header className="cat-head">
      <div>
        <h1 className="cat-head__title">{title}</h1>
        {subtitle && <p className="cat-head__subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="cat-head__actions">{actions}</div>}
    </header>
  );
}

export function Panel({ title, children, actions, className }: { title?: string; children: ReactNode; actions?: ReactNode; className?: string }) {
  return (
    <section className={cn("cat-panel", className)}>
      {(title || actions) && (
        <div className="cat-panel__head">
          {title && <h2 className="cat-panel__title">{title}</h2>}
          {actions}
        </div>
      )}
      {children}
    </section>
  );
}

export function Badge({ tone, children }: { tone: "success" | "warning" | "error" | "info" | "neutral"; children: ReactNode }) {
  return <span className={cn("cat-badge", `cat-badge--${tone}`)}>{children}</span>;
}

export function ErrorBanner({ error }: { error: ApiRequestError | null | undefined }) {
  if (!error) return null;
  return (
    <div className="cat-alert" role="alert">
      <AlertTriangle size={16} aria-hidden="true" />
      <span>{errorMessage(error.error)}</span>
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: ApiRequestError; onRetry?: () => void }) {
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
      icon={WifiOff}
      title="Chargement impossible"
      role="alert"
      action={onRetry && (
        <Button variant="primary" onClick={onRetry}>
          <RefreshCw size={16} aria-hidden="true" />
          Réessayer
        </Button>
      )}
    >
      <p>{errorMessage(error.error)} Aucun détail technique n&apos;est affiché.</p>
    </StateBlock>
  );
}

export function Loading({ label }: { label: string }) {
  return (
    <p role="status" className="cat-loading">
      <Loader2 size={16} className="clients-spin" aria-hidden="true" />
      {label}
    </p>
  );
}

interface FieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  errors?: Record<string, string[]>;
  errorKey?: string;
  type?: string;
  required?: boolean;
  hint?: string;
  inputMode?: "decimal" | "numeric" | "text";
  disabled?: boolean;
  multiline?: boolean;
}

export function Field({ label, name, value, onChange, errors, errorKey, type = "text", required, hint, inputMode, disabled, multiline }: FieldProps) {
  const id = `cat-${name}`;
  const message = fieldError(errors, errorKey ?? name);
  return (
    <div className="cat-field">
      <label htmlFor={id} className="cat-label">
        {label}
        {required && <span aria-hidden="true"> *</span>}
      </label>
      {multiline ? (
        <textarea id={id} className="cat-input" rows={3} value={value} disabled={disabled} aria-invalid={message !== null} onChange={(e) => onChange(e.target.value)} />
      ) : (
        <input
          id={id}
          className="cat-input"
          type={type}
          inputMode={inputMode}
          value={value}
          required={required}
          disabled={disabled}
          aria-invalid={message !== null}
          aria-describedby={message ? `${id}-error` : hint ? `${id}-hint` : undefined}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
      {message ? (
        <p id={`${id}-error`} className="cat-field__error">
          {message}
        </p>
      ) : (
        hint && (
          <p id={`${id}-hint`} className="cat-field__hint">
            {hint}
          </p>
        )
      )}
    </div>
  );
}

export function SelectField({ label, name, value, onChange, options, errors, required }: {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  errors?: Record<string, string[]>;
  required?: boolean;
}) {
  const id = `cat-${name}`;
  const message = fieldError(errors, name);
  return (
    <div className="cat-field">
      <label htmlFor={id} className="cat-label">
        {label}
        {required && <span aria-hidden="true"> *</span>}
      </label>
      <select id={id} className="cat-input" value={value} aria-invalid={message !== null} onChange={(e) => onChange(e.target.value)}>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {message && <p className="cat-field__error">{message}</p>}
    </div>
  );
}

export function Pager({ page, pageSize, total, onPage }: { page: number; pageSize: number; total: number; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <nav className="cat-pager" aria-label="Pagination">
      <span>
        {total} élément{total > 1 ? "s" : ""} · page {page} / {pages}
      </span>
      <Button size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Précédente
      </Button>
      <Button size="sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Suivante
      </Button>
    </nav>
  );
}

/** Fenêtre de dialogue simple (confirmation, saisie courte) : focus piégé par le navigateur via <dialog modal>. */
export function Dialog({ title, open, onClose, children }: { title: string; open: boolean; onClose: () => void; children: ReactNode }) {
  if (!open) return null;
  return (
    <div className="cat-dialog__backdrop" role="presentation" onKeyDown={(e) => e.key === "Escape" && onClose()}>
      <div className="cat-dialog" role="dialog" aria-modal="true" aria-label={title}>
        <h2 className="cat-dialog__title">{title}</h2>
        {children}
      </div>
    </div>
  );
}
