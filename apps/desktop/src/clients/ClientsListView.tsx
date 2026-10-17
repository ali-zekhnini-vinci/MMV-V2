// Liste des clients (Lot 2 DRAFT, planches 01–09). Recherche, statut, tri et pagination sont des
// paramètres SERVEUR (/api/clients, sous-lot 03-A2) ; l'écran ne filtre ni ne trie rien localement. Si le
// service est désactivé (interrupteur de repli), un état explicite remplace la table : aucune ligne fictive.
import type { SortingState, Updater } from "@tanstack/react-table";
import { ArrowUpDown, CheckSquare, Plus, RefreshCw, Search, SearchX, ServerOff, ShieldAlert, SlidersHorizontal, Square, Users, WifiOff, X } from "lucide-react";
import { useCallback, useEffect, useId, useState } from "react";
import { useNavigate } from "react-router";
import { CLIENT_SEARCH_MAX_LENGTH, CLIENT_SEARCH_MIN_LENGTH, type ClientStatusFilter, type ClientSummary } from "../api/clients.ts";
import { Button } from "../components/ui/button.tsx";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../components/ui/dropdown-menu.tsx";
import { cn } from "../lib/utils.ts";
import { ClientNoticeBanner } from "./ClientNoticeBanner.tsx";
import { useClientLifecycle } from "./ClientLifecycle.tsx";
import { READ_ONLY_HINT, useClientPermissions } from "./clientPermissions.ts";
import { useClientList } from "./clientQueries.ts";
import { useClientsService } from "./clientsService.ts";
import { ClientsTable } from "./ClientsTable.tsx";
import { LoadingBlock, StateBlock } from "./ClientStates.tsx";
import { toListParams, useClientsUi } from "./clientsUi.ts";

/** Délai de saisie avant d'interroger le serveur (une requête par pause, l'ancienne est annulée). */
export const SEARCH_DEBOUNCE_MS = 300;

const STATUS_LABEL: Record<ClientStatusFilter, string> = {
  active: "actifs uniquement",
  archived: "archivés uniquement",
  all: "actifs et archivés",
};

const SORT_OPTIONS = [
  { value: "name:asc", label: "Nom (A → Z)" },
  { value: "name:desc", label: "Nom (Z → A)" },
  { value: "createdAt:desc", label: "Création (récent d'abord)" },
  { value: "createdAt:asc", label: "Création (ancien d'abord)" },
  { value: "city:asc", label: "Ville (A → Z)" },
  { value: "city:desc", label: "Ville (Z → A)" },
] as const;

export function ClientsListView() {
  const navigate = useNavigate();
  const { enabled } = useClientsService();
  const { listState, setListState } = useClientsUi();
  const lifecycle = useClientLifecycle();
  const { canWrite, canArchive } = useClientPermissions();
  const [searchInput, setSearchInput] = useState(listState.search);
  const searchId = useId();

  // Recherche différée : seule la valeur stabilisée devient un paramètre serveur (et remet la page à 1).
  useEffect(() => {
    if (searchInput === listState.search) return;
    const timer = window.setTimeout(() => setListState((current) => ({ ...current, search: searchInput, page: 1 })), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [searchInput, listState.search, setListState]);

  const query = useClientList(toListParams(listState));
  const sortKey = `${listState.sorting[0]?.id ?? "name"}:${listState.sorting[0]?.desc ? "desc" : "asc"}`;
  const sortLabel = SORT_OPTIONS.find((option) => option.value === sortKey)?.label ?? SORT_OPTIONS[0].label;
  const activeSearch = listState.search.trim().length >= CLIENT_SEARCH_MIN_LENGTH ? listState.search.trim() : "";

  const onSortingChange = useCallback(
    (updater: Updater<SortingState>) =>
      setListState((current) => {
        const next = typeof updater === "function" ? updater(current.sorting) : updater;
        // Une colonne triée revient toujours à un tri explicite (jamais « non trié » côté serveur).
        const sorting = next.length > 0 ? next : [{ id: current.sorting[0]?.id ?? "name", desc: false }];
        return { ...current, sorting, page: 1 };
      }),
    [setListState],
  );

  const setStatus = (status: ClientStatusFilter) => setListState((current) => ({ ...current, status, page: 1 }));
  const setSort = (value: string) => {
    const [id, direction] = value.split(":");
    setListState((current) => ({ ...current, sorting: [{ id, desc: direction === "desc" }], page: 1 }));
  };
  const clearSearch = () => {
    setSearchInput("");
    setListState((current) => ({ ...current, search: "", page: 1 }));
  };

  const open = useCallback((client: ClientSummary) => navigate(client.id), [navigate]);
  const edit = useCallback((client: ClientSummary) => navigate(`${client.id}/modifier`), [navigate]);
  // Création proposée aux seuls comptes `clients.write` (aperçu du formulaire conservé si le service est désactivé).
  const createButton =
    canWrite || !enabled ? (
      <Button variant="primary" onClick={() => navigate("nouveau")}>
        <Plus size={16} aria-hidden="true" />
        Nouveau client
      </Button>
    ) : null;

  const subtitle = !enabled
    ? "Service Clients désactivé"
    : query.data
      ? `${query.data.totalCount} client${query.data.totalCount > 1 ? "s" : ""} · ${STATUS_LABEL[listState.status]}`
      : STATUS_LABEL[listState.status];

  let body;
  if (!enabled) {
    body = (
      <StateBlock tone="info" icon={ServerOff} title="Service Clients non activé">
        <p>
          Le service Clients est désactivé dans cette version de l&apos;application : aucune requête n&apos;est envoyée au serveur
          MMV.
        </p>
        <p>Aucune donnée n&apos;est affichée ni enregistrée. Le formulaire « Nouveau client » est consultable en aperçu.</p>
      </StateBlock>
    );
  } else if (query.isPending) {
    body = <LoadingBlock label="Chargement des clients…" />;
  } else if (query.isError) {
    body =
      query.error.error.kind === "forbidden" ? (
        <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
          <p>Le serveur MMV a refusé l&apos;accès à la liste des clients pour votre compte.</p>
        </StateBlock>
      ) : (
        <StateBlock
          tone="error"
          icon={WifiOff}
          title="Impossible de charger les clients"
          role="alert"
          action={
            <Button variant="primary" onClick={() => void query.refetch()} disabled={query.isFetching}>
              <RefreshCw size={16} aria-hidden="true" />
              {query.isFetching ? "Nouvelle tentative…" : "Réessayer"}
            </Button>
          }
        >
          <p>{query.error.error.message} Vérifiez la connexion, puis réessayez.</p>
        </StateBlock>
      );
  } else if (query.data.totalCount === 0 && activeSearch !== "") {
    body = (
      <StateBlock
        tone="info"
        icon={SearchX}
        title={`Aucun résultat pour « ${activeSearch} »`}
        action={
          <div className="clients-actions">
            <Button onClick={clearSearch}>
              <X size={16} aria-hidden="true" />
              Effacer la recherche
            </Button>
            {createButton}
          </div>
        }
      >
        <p>
          Vérifiez l&apos;orthographe ou essayez un autre nom, numéro de téléphone ou email.
          {canWrite && " Vous pouvez aussi créer ce client."}
        </p>
      </StateBlock>
    );
  } else if (query.data.totalCount === 0) {
    body =
      listState.status === "archived" ? (
        <StateBlock tone="neutral" icon={Users} title="Aucun client archivé">
          <p>Aucun client n&apos;est actuellement archivé.</p>
        </StateBlock>
      ) : (
        <StateBlock tone="accent" icon={Users} title="Aucun client enregistré" action={createButton}>
          <p>{canWrite ? "Commencez par créer votre premier client." : "Aucun client n'est encore enregistré sur le serveur MMV."}</p>
        </StateBlock>
      );
  } else {
    body = (
      <ClientsTable
        rows={query.data.items}
        search={activeSearch}
        sorting={listState.sorting}
        onSortingChange={onSortingChange}
        page={listState.page}
        pageSize={query.data.pageSize}
        totalCount={query.data.totalCount}
        onPageChange={(page) => setListState((current) => ({ ...current, page }))}
        fetching={query.isFetching}
        onOpen={open}
        onEdit={canWrite ? edit : undefined}
        onArchive={canArchive ? lifecycle.requestArchive : undefined}
        onReactivate={canArchive ? lifecycle.requestReactivate : undefined}
      />
    );
  }

  const includeArchived = listState.status !== "active";

  return (
    <section className="clients-view" aria-labelledby="clients-title">
      <header className="clients-head">
        <div>
          <h1 id="clients-title" className="clients-head__title">
            Clients
          </h1>
          <p className="clients-head__sub">{subtitle}</p>
          {enabled && !canWrite && (
            <p className="clients-head__hint" role="note">
              {READ_ONLY_HINT}
            </p>
          )}
        </div>
        {createButton && <div className="clients-actions">{createButton}</div>}
      </header>

      <ClientNoticeBanner />

      <div className="clients-toolbar" role="search">
        <label htmlFor={searchId} className="visually-hidden">
          Rechercher un client
        </label>
        <div className="clients-search">
          <Search size={18} aria-hidden="true" />
          <input
            id={searchId}
            type="search"
            value={searchInput}
            maxLength={CLIENT_SEARCH_MAX_LENGTH}
            placeholder="Rechercher par nom, téléphone ou email…"
            onChange={(event) => setSearchInput(event.target.value)}
            onKeyDown={(event) => event.key === "Escape" && searchInput !== "" && clearSearch()}
            disabled={!enabled}
            autoComplete="off"
            spellCheck={false}
          />
          {searchInput !== "" && (
            <button type="button" className="clients-search__clear" aria-label="Vider le champ de recherche" onClick={clearSearch}>
              <X size={16} aria-hidden="true" />
            </button>
          )}
        </div>
        <button
          type="button"
          className={cn("clients-chip", includeArchived && "clients-chip--on")}
          aria-pressed={includeArchived}
          onClick={() => setStatus(includeArchived ? "active" : "all")}
          disabled={!enabled}
        >
          {includeArchived ? <CheckSquare size={16} aria-hidden="true" /> : <Square size={16} aria-hidden="true" />}
          Inclure les clients archivés
        </button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild disabled={!enabled}>
            <button type="button" className="clients-chip">
              <SlidersHorizontal size={16} aria-hidden="true" />
              Filtres
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            <DropdownMenuLabel className="clients-menu-label">Statut</DropdownMenuLabel>
            <DropdownMenuRadioGroup value={listState.status} onValueChange={(value) => setStatus(value as ClientStatusFilter)}>
              <DropdownMenuRadioItem value="active">Actifs uniquement</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="archived">Archivés uniquement</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="all">Tous les clients</DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
        <span className="clients-toolbar__spacer" />
        <DropdownMenu>
          <DropdownMenuTrigger asChild disabled={!enabled}>
            <button type="button" className="clients-chip" aria-label={`Trier : ${sortLabel}`}>
              <ArrowUpDown size={16} aria-hidden="true" />
              {sortLabel}
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel className="clients-menu-label">Trier par</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuRadioGroup value={sortKey} onValueChange={setSort}>
              {SORT_OPTIONS.map((option) => (
                <DropdownMenuRadioItem key={option.value} value={option.value}>
                  {option.label}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {body}
      {lifecycle.dialogs}
    </section>
  );
}
