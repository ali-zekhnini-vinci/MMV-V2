// PROTOTYPE 04-A — liste globale « Dossiers optiques » (Lot 3 planches 01–08, états 1–4, 6, 8, 9 ; D-04-08.b PROPOSED).
// Recherche (client, prescripteur / collaborateur), type, statut, tri et pagination sont des paramètres du service ;
// les invalidés sont exclus par défaut et inclus explicitement. Mêmes gabarits que la liste Clients (Clients.css).
import { ArrowUpDown, CheckSquare, ChevronLeft, ChevronRight, FolderOpen, ListChecks, Plus, RefreshCw, Search, SearchX, ServerOff, ShieldAlert, SlidersHorizontal, Square, WifiOff, X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { useNavigate } from "react-router";
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
import { LoadingBlock, StateBlock } from "../clients/ClientStates.tsx";
import { cn } from "../lib/utils.ts";
import { canWriteKind, useOpticalAccess } from "./opticalAccess.ts";
import { OPTICAL_KINDS, OPTICAL_PAGE_SIZE, type OpticalSort, type OpticalStatusFilter } from "./opticalContract.ts";
import { KIND_PLURAL, PROPOSED_NOTE } from "./opticalLabels.ts";
import { ProposedTag } from "./OpticalBadges.tsx";
import { useOpticalList } from "./opticalQueries.ts";
import { OpticalRecordsTable } from "./OpticalRecordsTable.tsx";
import { opticalDetailPath, opticalListPath, opticalNewPath } from "./opticalUi.ts";
import { toOpticalParams, useOpticalUi } from "./opticalUiState.ts";

const SEARCH_DEBOUNCE_MS = 300;

const SORT_OPTIONS: { value: OpticalSort; label: string }[] = [
  { value: "date:desc", label: "Date (récent → ancien)" },
  { value: "date:asc", label: "Date (ancien → récent)" },
  { value: "client:asc", label: "Client (A → Z)" },
  { value: "client:desc", label: "Client (Z → A)" },
  { value: "recorded:desc", label: "Saisie (récente d'abord)" },
];

const STATUS_OPTIONS: { value: OpticalStatusFilter; label: string }[] = [
  { value: "all", label: "Tous les statuts" },
  { value: "draft", label: "Brouillons" },
  { value: "confirmed", label: "Confirmés" },
  { value: "rectification", label: "Rectification en cours" },
];

export function OpticalListView() {
  const navigate = useNavigate();
  const access = useOpticalAccess();
  const { list, setList } = useOpticalUi();
  const [searchInput, setSearchInput] = useState(list.search);
  const searchId = useId();
  const query = useOpticalList(toOpticalParams(list), access.canRead);
  const canCreate = OPTICAL_KINDS.some((kind) => canWriteKind(access, kind));
  const activeSearch = list.search.trim().length >= 2 ? list.search.trim() : "";
  const filtered = list.kind !== "all" || list.status !== "all" || list.includeInvalidated;

  useEffect(() => {
    if (searchInput === list.search) return;
    const timer = window.setTimeout(() => setList((current) => ({ ...current, search: searchInput, page: 1 })), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [searchInput, list.search, setList]);

  const clearSearch = () => {
    setSearchInput("");
    setList((current) => ({ ...current, search: "", page: 1 }));
  };
  const resetFilters = () => setList((current) => ({ ...current, kind: "all", status: "all", includeInvalidated: false, page: 1 }));

  const createButton = canCreate ? (
    <Button variant="primary" onClick={() => navigate(opticalNewPath(), { state: { returnTo: opticalListPath(), returnLabel: "Dossiers optiques" } })}>
      <Plus size={16} aria-hidden="true" />
      Nouveau…
    </Button>
  ) : null;

  if (!access.canRead) {
    return (
      <section className="clients-view" aria-labelledby="optical-title">
        <h1 id="optical-title" className="clients-head__title">
          Dossiers optiques
        </h1>
        <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
          <p>Ce profil ne détient pas le droit de lecture du dossier optique (optical.read, proposé). Aucune donnée n&apos;est affichée.</p>
        </StateBlock>
      </section>
    );
  }

  let body;
  if (query.isPending) {
    body = <LoadingBlock label="Chargement des dossiers optiques…" />;
  } else if (query.isError) {
    const { kind, message } = query.error.error;
    body =
      kind === "not-configured" ? (
        <StateBlock tone="info" icon={ServerOff} title="Service optique non activé">
          <p>Aucune route optique n&apos;existe sur le serveur MMV (Phase 04-B non commencée). Aucune donnée n&apos;est affichée.</p>
        </StateBlock>
      ) : kind === "forbidden" ? (
        <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
          <p>Le service a refusé l&apos;accès à la liste des dossiers optiques pour ce profil.</p>
        </StateBlock>
      ) : (
        <StateBlock
          tone="error"
          icon={WifiOff}
          title="Impossible de charger les dossiers optiques"
          role="alert"
          action={
            <Button variant="primary" onClick={() => void query.refetch()} disabled={query.isFetching}>
              <RefreshCw size={16} aria-hidden="true" />
              {query.isFetching ? "Nouvelle tentative…" : "Réessayer"}
            </Button>
          }
        >
          <p>{message} Aucun détail technique n&apos;est affiché. Vérifiez la connexion, puis réessayez.</p>
        </StateBlock>
      );
  } else if (query.data.totalCount === 0 && (activeSearch !== "" || filtered)) {
    body = (
      <StateBlock
        tone="info"
        icon={SearchX}
        title={activeSearch ? `Aucun résultat pour « ${activeSearch} »` : "Aucun résultat pour ces filtres"}
        action={
          <div className="clients-actions">
            {activeSearch && (
              <Button onClick={clearSearch}>
                <X size={16} aria-hidden="true" />
                Effacer la recherche
              </Button>
            )}
            {filtered && <Button onClick={resetFilters}>Réinitialiser les filtres</Button>}
          </div>
        }
      >
        <p>Vérifiez l&apos;orthographe du client ou du prescripteur, ou élargissez les filtres.</p>
      </StateBlock>
    );
  } else if (query.data.totalCount === 0) {
    body = (
      <StateBlock tone="accent" icon={FolderOpen} title="Aucun dossier optique enregistré" action={createButton}>
        <p>Aucune ordonnance médicale ni mesure magasin n&apos;est encore enregistrée.</p>
      </StateBlock>
    );
  } else {
    const { items, totalCount, page } = query.data;
    const pageCount = Math.max(1, Math.ceil(totalCount / OPTICAL_PAGE_SIZE));
    const first = (page - 1) * OPTICAL_PAGE_SIZE + 1;
    const last = first + items.length - 1;
    body = (
      <div className="clients-panel" aria-busy={query.isFetching}>
        <OpticalRecordsTable
          rows={items}
          caption={`Dossiers optiques, page ${page} sur ${pageCount}`}
          showClient
          showSummary={list.showSummary}
          search={activeSearch}
          onOpen={(row) => navigate(opticalDetailPath(row.kind, row.id), { state: { returnTo: opticalListPath(), returnLabel: "Dossiers optiques" } })}
        />
        <nav className="clients-foot" aria-label="Pagination des dossiers optiques">
          <span>
            Affichage de <b>{first === last ? first : `${first}–${last}`}</b> sur <b>{totalCount}</b> enregistrement{totalCount > 1 ? "s" : ""}
          </span>
          <span className="clients-foot__spacer" />
          <span className="clients-muted">{OPTICAL_PAGE_SIZE} par page</span>
          <Button variant="ghost" size="icon" aria-label="Page précédente" disabled={page <= 1 || query.isFetching} onClick={() => setList((current) => ({ ...current, page: page - 1 }))}>
            <ChevronLeft size={16} aria-hidden="true" />
          </Button>
          <span className="clients-kbd" aria-current="page">
            {page} / {pageCount}
          </span>
          <Button variant="ghost" size="icon" aria-label="Page suivante" disabled={page >= pageCount || query.isFetching} onClick={() => setList((current) => ({ ...current, page: page + 1 }))}>
            <ChevronRight size={16} aria-hidden="true" />
          </Button>
        </nav>
      </div>
    );
  }

  const sortLabel = SORT_OPTIONS.find((option) => option.value === list.sort)?.label ?? SORT_OPTIONS[0].label;
  const kindLabel = list.kind === "all" ? "tous types" : KIND_PLURAL[list.kind].toLocaleLowerCase("fr");

  return (
    <section className="clients-view" aria-labelledby="optical-title">
      <header className="clients-head">
        <div>
          <h1 id="optical-title" className="clients-head__title">
            Dossiers optiques
          </h1>
          <p className="clients-head__sub">
            {query.data ? `${query.data.totalCount} enregistrement${query.data.totalCount > 1 ? "s" : ""} · ` : ""}
            {kindLabel}
            {list.includeInvalidated ? " · invalidés inclus" : " · invalidés exclus"}
          </p>
          <p className="clients-head__hint" role="note">
            {PROPOSED_NOTE} <ProposedTag refs="D-04-08.b" />
          </p>
        </div>
        {createButton && <div className="clients-actions">{createButton}</div>}
      </header>

      <div className="clients-toolbar" role="search">
        <label htmlFor={searchId} className="visually-hidden">
          Rechercher par client ou prescripteur
        </label>
        <div className="clients-search">
          <Search size={18} aria-hidden="true" />
          <input
            id={searchId}
            type="search"
            value={searchInput}
            maxLength={100}
            placeholder="Rechercher par client ou prescripteur…"
            onChange={(event) => setSearchInput(event.target.value)}
            onKeyDown={(event) => event.key === "Escape" && searchInput !== "" && clearSearch()}
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
          className={cn("clients-chip", list.includeInvalidated && "clients-chip--on")}
          aria-pressed={list.includeInvalidated}
          onClick={() => setList((current) => ({ ...current, includeInvalidated: !current.includeInvalidated, page: 1 }))}
        >
          {list.includeInvalidated ? <CheckSquare size={16} aria-hidden="true" /> : <Square size={16} aria-hidden="true" />}
          Inclure les invalidés
        </button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button type="button" className={cn("clients-chip", (list.kind !== "all" || list.status !== "all") && "clients-chip--on")}>
              <SlidersHorizontal size={16} aria-hidden="true" />
              Filtres
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            <DropdownMenuLabel className="clients-menu-label">Type</DropdownMenuLabel>
            <DropdownMenuRadioGroup value={list.kind} onValueChange={(value) => setList((current) => ({ ...current, kind: value as typeof current.kind, page: 1 }))}>
              <DropdownMenuRadioItem value="all">Tous les types</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="prescription">{KIND_PLURAL.prescription}</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="measurement">{KIND_PLURAL.measurement}</DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator />
            <DropdownMenuLabel className="clients-menu-label">Statut</DropdownMenuLabel>
            <DropdownMenuRadioGroup value={list.status} onValueChange={(value) => setList((current) => ({ ...current, status: value as OpticalStatusFilter, page: 1 }))}>
              {STATUS_OPTIONS.map((option) => (
                <DropdownMenuRadioItem key={option.value} value={option.value}>
                  {option.label}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
        <button
          type="button"
          className={cn("clients-chip", list.showSummary && "clients-chip--on")}
          aria-pressed={list.showSummary}
          title="D-04-13.b à trancher : résumé OD/OG dans la liste (maquette) ou détail seulement (minimisation)"
          onClick={() => setList((current) => ({ ...current, showSummary: !current.showSummary }))}
        >
          <ListChecks size={16} aria-hidden="true" />
          Résumé OD/OG (D-04-13.b)
        </button>
        <span className="clients-toolbar__spacer" />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button type="button" className="clients-chip" aria-label={`Trier : ${sortLabel}`}>
              <ArrowUpDown size={16} aria-hidden="true" />
              {sortLabel}
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel className="clients-menu-label">Trier par</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuRadioGroup value={list.sort} onValueChange={(value) => setList((current) => ({ ...current, sort: value as OpticalSort, page: 1 }))}>
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
    </section>
  );
}
