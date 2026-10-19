// Sélection du client d'un rendez-vous (Lot 2 DRAFT, planche 40). Motif ARIA « combobox » : le focus reste
// dans le champ de recherche, ↑/↓ déplacent l'option active, Entrée la choisit, Échap revient au formulaire.
// Recherche serveur sur les clients ACTIFS uniquement (D-03-05 acceptée), `clients.read` requis.
// Écart volontaire : « Créer un nouveau client » absent (intégration client ↔ rendez-vous = sous-lot 03-C).
import { ArrowLeft, Check, Loader2, Search, X } from "lucide-react";
import { type KeyboardEvent, useEffect, useId, useState } from "react";
import type { ClientSummary } from "../api/clients.ts";
import { clientDisplayName } from "../clients/clientForm.ts";
import { cn } from "../lib/utils.ts";
import { useClientPicker } from "./appointmentQueries.ts";

interface ClientPickerProps {
  allowed: boolean;
  selectedId: string;
  onPick: (client: ClientSummary) => void;
  onBack: () => void;
  onClose: () => void;
}

/** Recherche différée (300 ms) pour ne pas interroger le serveur à chaque frappe. */
function useDeferredSearch(value: string, delay = 300): string {
  const [deferred, setDeferred] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDeferred(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return deferred;
}

export function ClientPicker({ allowed, selectedId, onPick, onBack, onClose }: ClientPickerProps) {
  const [search, setSearch] = useState("");
  const [active, setActive] = useState(0);
  const deferred = useDeferredSearch(search);
  const query = useClientPicker(deferred, allowed);
  const listId = useId();
  const items = deferred.trim().length >= 2 ? (query.data?.items ?? []) : [];
  const activeIndex = Math.min(active, Math.max(0, items.length - 1));

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (items.length > 0) setActive(Math.min(items.length - 1, Math.max(0, activeIndex + (event.key === "ArrowDown" ? 1 : -1))));
    } else if (event.key === "Enter") {
      event.preventDefault();
      if (items[activeIndex]) onPick(items[activeIndex]);
    } else if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      onBack();
    }
  };

  let status: string;
  if (!allowed) status = "Votre compte ne permet pas de rechercher des clients (clients.read).";
  else if (deferred.trim().length < 2) status = "Saisissez au moins 2 caractères (nom, prénom, email ou téléphone).";
  else if (query.isError) status = query.error.error.kind === "forbidden" ? "Le serveur MMV a refusé la recherche de clients pour votre compte." : query.error.error.message;
  else if (query.isPending) status = "Recherche en cours…";
  else if (items.length === 0) status = "Aucun client actif ne correspond à cette recherche.";
  else status = `${query.data?.totalCount ?? items.length} client${(query.data?.totalCount ?? items.length) > 1 ? "s" : ""} trouvé${(query.data?.totalCount ?? items.length) > 1 ? "s" : ""}`;

  return (
    <>
      <div className="rdv-side__head">
        <button type="button" className="rdv-iconbtn" aria-label="Retour au formulaire" onClick={onBack}>
          <ArrowLeft size={18} aria-hidden="true" />
        </button>
        <h2 className="rdv-side__title">Sélectionner un client</h2>
        <button type="button" className="rdv-iconbtn" aria-label="Fermer le panneau" onClick={onClose}>
          <X size={18} aria-hidden="true" />
        </button>
      </div>
      <div className="rdv-picker__search">
        <div className="rdv-input rdv-input--focusable">
          <Search size={18} aria-hidden="true" />
          <input
            type="search"
            role="combobox"
            aria-label="Rechercher un client actif"
            aria-expanded={items.length > 0}
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={items[activeIndex] ? `${listId}-${items[activeIndex].id}` : undefined}
            placeholder="Nom, prénom, email ou téléphone…"
            value={search}
            maxLength={100}
            autoFocus
            disabled={!allowed}
            onChange={(event) => {
              setSearch(event.target.value);
              setActive(0);
            }}
            onKeyDown={onKeyDown}
          />
          {query.isFetching && <Loader2 size={16} className="clients-spin" aria-hidden="true" />}
        </div>
      </div>
      <div className="rdv-side__body rdv-picker__body">
        <ul id={listId} role="listbox" aria-label="Clients actifs" className="rdv-picker__list">
          {items.map((client, index) => (
            <li
              key={client.id}
              id={`${listId}-${client.id}`}
              role="option"
              aria-selected={index === activeIndex}
              className={cn("rdv-picker__option", index === activeIndex && "rdv-picker__option--active")}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => onPick(client)}
            >
              <span className="clients-avatar" aria-hidden="true">
                {`${client.firstName.charAt(0)}${client.lastName.charAt(0)}`.toLocaleUpperCase("fr")}
              </span>
              <span className="rdv-picker__text">
                <span className="rdv-picker__name">{clientDisplayName(client)}</span>
                <span className="rdv-picker__meta">{[client.reference, client.phone, client.city].filter(Boolean).join(" · ")}</span>
              </span>
              {client.id === selectedId && <Check size={18} aria-label="Client actuellement choisi" />}
            </li>
          ))}
        </ul>
        <p className="rdv-picker__status" role="status">
          {status}
        </p>
      </div>
    </>
  );
}
