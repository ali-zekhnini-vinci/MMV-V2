// Table Clients — TanStack Table v8 en mode serveur : pagination et tri manuels (l'API renvoie la page
// triée), aucune donnée triée ou filtrée localement. Gabarit : Lot 2 DRAFT, planches 01–03 (`table.dt`).
// Colonnes : seulement des champs du contrat (docs/phase/03/API_CLIENTS.md, ClientSummaryResponse).
import {
  type ColumnDef,
  flexRender,
  getCoreRowModel,
  type OnChangeFn,
  type SortingState,
  useReactTable,
} from "@tanstack/react-table";
import { Archive, ArchiveRestore, ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight, Eye, Pencil } from "lucide-react";
import { type ReactNode, useMemo } from "react";
import type { ClientSummary } from "../api/clients.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { clientDisplayName, clientInitials, formatUtcDate } from "./clientForm.ts";
import { Highlight } from "./highlight.tsx";

export interface ClientRowActions {
  onOpen: (client: ClientSummary) => void;
  onEdit: (client: ClientSummary) => void;
  onArchive: (client: ClientSummary) => void;
  onReactivate: (client: ClientSummary) => void;
}

interface ClientsTableProps extends ClientRowActions {
  rows: ClientSummary[];
  search: string;
  sorting: SortingState;
  onSortingChange: OnChangeFn<SortingState>;
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  /** Requête de la page suivante en cours (ancienne page affichée). */
  fetching: boolean;
}

export function StatusBadge({ status }: { status: ClientSummary["status"] }) {
  return status === "archived" ? (
    <span className="clients-badge clients-badge--archived">
      <Archive size={12} aria-hidden="true" />
      Archivé
    </span>
  ) : (
    <span className="clients-badge clients-badge--active">
      <span className="clients-badge__dot" aria-hidden="true" />
      Actif
    </span>
  );
}

const SORT_LABEL: Record<string, string> = { name: "client", city: "ville", createdAt: "date de création" };

export function ClientsTable({
  rows,
  search,
  sorting,
  onSortingChange,
  page,
  pageSize,
  totalCount,
  onPageChange,
  fetching,
  onOpen,
  onEdit,
  onArchive,
  onReactivate,
}: ClientsTableProps) {
  const columns = useMemo<ColumnDef<ClientSummary>[]>(
    () => [
      {
        id: "name",
        // Accesseur requis par TanStack Table pour rendre la colonne triable (tri effectué par le serveur).
        accessorFn: (client) => `${client.lastName} ${client.firstName}`,
        header: "Client",
        enableSorting: true,
        cell: ({ row }) => {
          const client = row.original;
          return (
            <div className="clients-cell">
              <span className={cn("clients-avatar", client.status === "archived" && "clients-avatar--archived")} aria-hidden="true">
                {clientInitials(client)}
              </span>
              <div>
                <div className="clients-cell__name">
                  <Highlight text={clientDisplayName(client)} query={search} />
                </div>
                <div className="clients-cell__ref">{client.reference}</div>
              </div>
            </div>
          );
        },
      },
      {
        id: "phone",
        header: "Téléphone",
        enableSorting: false,
        cell: ({ row }) => (row.original.phone ? <Highlight text={row.original.phone} query={search} /> : <Missing />),
        meta: { className: "tnum" },
      },
      {
        id: "email",
        header: "Email",
        enableSorting: false,
        cell: ({ row }) => (row.original.email ? <Highlight text={row.original.email} query={search} /> : <Missing />),
      },
      { id: "city", accessorFn: (client) => client.city ?? "", header: "Ville", enableSorting: true, cell: ({ row }) => row.original.city ?? <Missing /> },
      {
        id: "createdAt",
        accessorFn: (client) => client.createdAtUtc,
        header: "Créé le",
        enableSorting: true,
        cell: ({ row }) => formatUtcDate(row.original.createdAtUtc),
        meta: { className: "tnum clients-muted" },
      },
      { id: "status", header: "Statut", enableSorting: false, cell: ({ row }) => <StatusBadge status={row.original.status} /> },
      {
        id: "actions",
        header: () => <span className="clients-th-right">Actions</span>,
        enableSorting: false,
        cell: ({ row }) => {
          const client = row.original;
          const name = clientDisplayName(client);
          return (
            <div className="clients-rowacts">
              <IconAction label={`Consulter ${name}`} onClick={() => onOpen(client)} icon={<Eye size={16} />} />
              {client.status === "archived" ? (
                <IconAction label={`Réactiver ${name}`} onClick={() => onReactivate(client)} icon={<ArchiveRestore size={16} />} accent />
              ) : (
                <>
                  <IconAction label={`Modifier ${name}`} onClick={() => onEdit(client)} icon={<Pencil size={16} />} />
                  <IconAction label={`Archiver ${name}`} onClick={() => onArchive(client)} icon={<Archive size={16} />} />
                </>
              )}
            </div>
          );
        },
      },
    ],
    [search, onOpen, onEdit, onArchive, onReactivate],
  );

  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));

  // Avertissement lint attendu (react-hooks/incompatible-library) : useReactTable renvoie des fonctions
  // non mémoïsables ; le React Compiler ignore alors ce composant, qui reste correct (rendu à chaque état).
  const table = useReactTable({
    data: rows,
    columns,
    getRowId: (row) => row.id,
    getCoreRowModel: getCoreRowModel(),
    manualSorting: true,
    manualPagination: true,
    enableMultiSort: false,
    // Premier clic : ordre croissant pour toutes les colonnes (alphabétique ou chronologique).
    sortDescFirst: false,
    pageCount,
    state: { sorting, pagination: { pageIndex: page - 1, pageSize } },
    onSortingChange,
  });

  const first = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const last = Math.min(totalCount, (page - 1) * pageSize + rows.length);

  return (
    <div className="clients-panel" aria-busy={fetching}>
      <div className="clients-panel__scroll">
        <table className="clients-table">
          <caption className="visually-hidden">
            Clients, page {page} sur {pageCount}
          </caption>
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => {
                  const sorted = header.column.getIsSorted();
                  const label = flexRender(header.column.columnDef.header, header.getContext());
                  return (
                    <th
                      key={header.id}
                      scope="col"
                      aria-sort={sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : undefined}
                    >
                      {header.column.getCanSort() ? (
                        <button
                          type="button"
                          className="clients-sort"
                          onClick={header.column.getToggleSortingHandler()}
                          aria-label={`Trier par ${SORT_LABEL[header.column.id] ?? header.column.id}`}
                        >
                          {label}
                          {sorted === "asc" ? (
                            <ArrowUp size={14} aria-hidden="true" />
                          ) : sorted === "desc" ? (
                            <ArrowDown size={14} aria-hidden="true" />
                          ) : (
                            <ArrowUpDown size={14} aria-hidden="true" className="clients-sort__idle" />
                          )}
                        </button>
                      ) : (
                        label
                      )}
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => (
              <tr key={row.id} className={cn(row.original.status === "archived" && "clients-row--archived")}>
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id} className={(cell.column.columnDef.meta as { className?: string } | undefined)?.className}>
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <nav className="clients-foot" aria-label="Pagination des clients">
        <span>
          Affichage de <b>{first === last ? first : `${first}–${last}`}</b> sur <b>{totalCount}</b> client{totalCount > 1 ? "s" : ""}
        </span>
        <span className="clients-foot__spacer" />
        <span className="clients-muted">{pageSize} par page</span>
        <Button
          variant="ghost"
          size="icon"
          aria-label="Page précédente"
          disabled={!table.getCanPreviousPage() || fetching}
          onClick={() => onPageChange(page - 1)}
        >
          <ChevronLeft size={16} aria-hidden="true" />
        </Button>
        <span className="clients-kbd" aria-current="page">
          {page} / {pageCount}
        </span>
        <Button
          variant="ghost"
          size="icon"
          aria-label="Page suivante"
          disabled={!table.getCanNextPage() || fetching}
          onClick={() => onPageChange(page + 1)}
        >
          <ChevronRight size={16} aria-hidden="true" />
        </Button>
      </nav>
    </div>
  );
}

function Missing() {
  return (
    <span className="clients-muted" aria-label="Non renseigné">
      —
    </span>
  );
}

function IconAction({ label, onClick, icon, accent = false }: { label: string; onClick: () => void; icon: ReactNode; accent?: boolean }) {
  return (
    <Button variant="ghost" size="icon" aria-label={label} title={label} onClick={onClick} className={cn(accent && "text-accent")}>
      {icon}
    </Button>
  );
}
