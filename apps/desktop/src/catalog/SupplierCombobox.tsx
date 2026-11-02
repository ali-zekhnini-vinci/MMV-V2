// Combobox fournisseur (COR-05-08), UN composant réutilisé par l'assistant de création, le rattachement d'une
// variante, les réceptions et le filtre du catalogue. Motif ARIA « combobox + listbox » : saisie → suggestions
// serveur (nom, code, ville, référence fournisseur ; casse et accents ignorés, même recherche que les listes),
// flèches haut/bas, Entrée pour choisir, Échap pour fermer ; au plus 8 suggestions (« Affiner la recherche » au-delà).
// Création rapide réservée à `suppliers.write` : nom repris de la saisie, code proposé, pays du magasin ; le serveur
// refuse un nom déjà porté par un fournisseur actif (aucun doublon) et le nouveau fournisseur est choisi d'office.
import { Building2, Check, ChevronDown, Loader2, Plus, X } from "lucide-react";
import { type KeyboardEvent, useEffect, useId, useMemo, useState } from "react";
import { type Page, query, type SupplierRow } from "../api/catalog.ts";
import { SEARCH_DEBOUNCE_MS } from "../clients/clientsUi.ts";
import { Highlight } from "../clients/highlight.tsx";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { useCatalogAccess } from "./catalogAccess.ts";
import { errorMessage, useActiveStore, useCatalogQuery, useCatalogWrite } from "./catalogData.ts";
import { fieldError, suggestSupplierCode, SUPPLIER_SUGGESTIONS as SUGGESTION_LIMIT } from "./catalogFormat.ts";
import { ErrorBanner, Field, Modal } from "./CatalogUi.tsx";

export interface SupplierChoice {
  id: string;
  code: string;
  name: string;
}

function useDebounced(value: string): string {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [value]);
  return debounced;
}

export function SupplierCombobox({ label, value, onChange, required, error, hint, allowCreate = true, exclude = [], placeholder, compact = false, autoFocus }: {
  label: string;
  value: SupplierChoice | null;
  onChange: (supplier: SupplierChoice | null) => void;
  required?: boolean;
  error?: string | null;
  hint?: string;
  allowCreate?: boolean;
  /** Fournisseurs à ne pas proposer (déjà rattachés). */
  exclude?: readonly string[];
  placeholder?: string;
  compact?: boolean;
  autoFocus?: boolean;
}) {
  const access = useCatalogAccess();
  const id = useId();
  const listId = `${id}-list`;
  const [text, setText] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const [creating, setCreating] = useState<string | null>(null);
  const searched = useDebounced(text.trim().replace(/\s+/g, " "));
  const suggestions = useCatalogQuery<Page<SupplierRow>>(
    open ? `/api/suppliers${query({ q: searched, status: "active", pageSize: SUGGESTION_LIMIT + exclude.length })}` : null, [], { keepPrevious: true },
  );
  const items = useMemo(
    () => (suggestions.data?.items ?? []).filter((s) => !exclude.includes(s.id)).slice(0, SUGGESTION_LIMIT),
    [suggestions.data, exclude],
  );
  const canCreate = allowCreate && access.suppliersWrite && searched.length >= 2
    && !items.some((s) => s.name.localeCompare(searched, "fr", { sensitivity: "base" }) === 0);
  const options = items.length + (canCreate ? 1 : 0);
  const more = (suggestions.data?.totalCount ?? 0) > items.length + exclude.length;
  const loading = open && (suggestions.isPending || suggestions.isPlaceholderData || searched !== text.trim().replace(/\s+/g, " "));

  const choose = (supplier: SupplierChoice) => {
    onChange(supplier);
    setText("");
    setOpen(false);
  };
  const pick = (index: number) => {
    if (index < items.length) {
      const s = items[index];
      choose({ id: s.id, code: s.code, name: s.name });
    } else if (canCreate) {
      setOpen(false);
      setCreating(searched);
    }
  };
  const onKey = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      if (!open) setOpen(true);
      else setActive((i) => (options === 0 ? 0 : (i + 1) % options));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActive((i) => (options === 0 ? 0 : (i - 1 + options) % options));
    } else if (event.key === "Enter") {
      if (open && options > 0) {
        event.preventDefault();
        pick(Math.min(active, options - 1));
      }
    } else if (event.key === "Escape" && open) {
      event.preventDefault();
      setOpen(false);
    }
  };

  const message = error ?? null;
  return (
    <div className={cn("clients-field", "cat-combo", compact && "cat-combo--compact")}>
      {!compact && (
        <label htmlFor={id} className="clients-field__label" data-required={required ? "" : undefined}>
          {label}
        </label>
      )}
      {value ? (
        <div className={cn("clients-input", "cat-combo__chosen", message && "clients-input--error")}>
          <Building2 size={16} aria-hidden="true" />
          <span className="cat-combo__name">{value.name}</span>
          <span className="cat-mono cat-sub">{value.code}</span>
          <button type="button" className="cat-ract" aria-label={`Retirer le fournisseur ${value.name}`} onClick={() => onChange(null)}>
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      ) : (
        <div className={cn("clients-input", "cat-combo__box", message && "clients-input--error")}>
          <input
            id={id}
            role="combobox"
            aria-label={compact ? label : undefined}
            aria-expanded={open}
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={open && options > 0 ? `${listId}-${Math.min(active, options - 1)}` : undefined}
            aria-invalid={message !== null}
            aria-describedby={message ? `${id}-error` : hint ? `${id}-hint` : undefined}
            autoComplete="off"
            autoFocus={autoFocus}
            value={text}
            placeholder={placeholder ?? "Nom, code ou référence fournisseur…"}
            onChange={(event) => { setText(event.target.value); setOpen(true); setActive(0); }}
            onFocus={() => setOpen(true)}
            onBlur={() => window.setTimeout(() => setOpen(false), 150)}
            onKeyDown={onKey}
          />
          {loading ? <Loader2 size={16} className="clients-spin" aria-hidden="true" /> : <ChevronDown size={16} aria-hidden="true" />}
        </div>
      )}
      {open && !value && (
        <ul id={listId} role="listbox" className="cat-combo__list" aria-label={`Suggestions — ${label}`}>
          {suggestions.isError && <li className="cat-combo__state" role="presentation">{errorMessage(suggestions.error.error)}</li>}
          {!suggestions.isError && !loading && items.length === 0 && (
            <li className="cat-combo__state" role="presentation">{searched === "" ? "Aucun fournisseur actif." : `Aucun fournisseur ne correspond à « ${searched} ».`}</li>
          )}
          {items.map((s, index) => (
            <li key={s.id} id={`${listId}-${index}`} role="option" aria-selected={index === active}
              className={cn("cat-combo__option", index === active && "is-active")}
              onMouseDown={(event) => event.preventDefault()} onMouseEnter={() => setActive(index)} onClick={() => pick(index)}>
              <span className="cat-combo__name"><Highlight text={s.name} query={searched} /></span>
              <span className="cat-mono cat-sub"><Highlight text={s.code} query={searched} /></span>
              {s.city && <span className="cat-sub">{s.city}</span>}
            </li>
          ))}
          {more && <li className="cat-combo__state" role="presentation">D&apos;autres fournisseurs correspondent : affinez la recherche.</li>}
          {canCreate && (
            <li id={`${listId}-${items.length}`} role="option" aria-selected={active === items.length}
              className={cn("cat-combo__option", "cat-combo__create", active === items.length && "is-active")}
              onMouseDown={(event) => event.preventDefault()} onMouseEnter={() => setActive(items.length)} onClick={() => pick(items.length)}>
              <Plus size={14} aria-hidden="true" />Créer le fournisseur « {searched} »
            </li>
          )}
        </ul>
      )}
      {message ? (
        <p id={`${id}-error`} className="clients-field__error">{message}</p>
      ) : hint ? (
        <p id={`${id}-hint`} className="clients-field__hint">{hint}</p>
      ) : null}
      {creating !== null && <QuickCreateSupplier initialName={creating} onClose={() => setCreating(null)} onCreated={(s) => { setCreating(null); choose(s); }} />}
    </div>
  );
}

/** Création rapide : nom, code, pays (du magasin par défaut). Le reste de la fiche se complète plus tard. */
function QuickCreateSupplier({ initialName, onClose, onCreated }: { initialName: string; onClose: () => void; onCreated: (supplier: SupplierChoice) => void }) {
  const { store } = useActiveStore();
  const write = useCatalogWrite<{ id: string }>();
  const [name, setName] = useState(initialName);
  const [code, setCode] = useState(suggestSupplierCode(initialName));
  const [country, setCountry] = useState(store?.countryCode ?? "");
  const errors = write.error?.error.fieldErrors;
  const nameTaken = fieldError(errors, "name") !== null && errors?.name?.includes("duplicate");
  const create = () => write.mutate(
    { method: "POST", path: "/api/suppliers", body: { code: code.trim().toUpperCase(), name: name.trim(), countryCode: country.trim().toUpperCase() } },
    { onSuccess: (r) => onCreated({ id: r.id, code: code.trim().toUpperCase(), name: name.trim() }) },
  );
  return (
    <Modal open onClose={onClose} tone="accent" icon={Building2} title="Nouveau fournisseur" busy={write.isPending}
      description="Création rapide : seuls le nom, le code et le pays sont requis ; la fiche se complète ensuite depuis Fournisseurs."
      actions={<>
        <Button onClick={onClose}>Annuler</Button>
        <Button variant="primary" disabled={write.isPending || name.trim() === "" || code.trim() === "" || country.trim().length !== 2} onClick={create}>
          <Check size={16} aria-hidden="true" />Créer et sélectionner
        </Button>
      </>}>
      <ErrorBanner error={nameTaken ? null : write.error} />
      <div className="cat-grid">
        <Field label="Nom" name="name" value={name} onChange={setName} required span2 autoFocus errors={errors}
          error={nameTaken ? "Un fournisseur actif porte déjà ce nom : sélectionnez-le dans la liste plutôt que d'en créer un second." : undefined} />
        <Field label="Code" name="code" value={code} onChange={(v) => setCode(v.toUpperCase())} required errors={errors} hint="Unique ; proposé d'après le nom." />
        <Field label="Pays (ISO)" name="countryCode" value={country} onChange={(v) => setCountry(v.toUpperCase().slice(0, 2))} required errors={errors} hint="BE, MA, FR…" />
      </div>
    </Modal>
  );
}
