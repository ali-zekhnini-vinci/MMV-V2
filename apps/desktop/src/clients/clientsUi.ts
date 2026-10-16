// État d'interface partagé par les écrans Clients pendant la session : critères de liste (conservés au
// retour de la fiche) et message de confirmation d'une opération. Rien n'est persisté.
import type { SortingState } from "@tanstack/react-table";
import { createContext, useContext } from "react";
import { CLIENT_PAGE_SIZE, type ClientListParams, type ClientSortField, type ClientStatusFilter } from "../api/clients.ts";

export interface ClientListState {
  search: string;
  status: ClientStatusFilter;
  sorting: SortingState;
  page: number;
}

export const INITIAL_LIST_STATE: ClientListState = {
  search: "",
  status: "active",
  sorting: [{ id: "name", desc: false }],
  page: 1,
};

const SORTABLE: readonly ClientSortField[] = ["name", "createdAt", "city"];

/** État d'écran → paramètres du contrat (liste blanche de tri, page 1-indexée). */
export function toListParams(state: ClientListState): ClientListParams {
  const sort = state.sorting[0];
  const field = SORTABLE.find((candidate) => candidate === sort?.id) ?? "name";
  return {
    search: state.search,
    status: state.status,
    sort: field,
    direction: sort?.desc ? "desc" : "asc",
    page: state.page,
    pageSize: CLIENT_PAGE_SIZE,
  };
}

export interface ClientNotice {
  title: string;
  message: string;
}

export interface ClientsUi {
  listState: ClientListState;
  setListState: (update: (current: ClientListState) => ClientListState) => void;
  notice: ClientNotice | null;
  setNotice: (notice: ClientNotice | null) => void;
}

export const ClientsUiContext = createContext<ClientsUi | null>(null);

export function useClientsUi(): ClientsUi {
  const value = useContext(ClientsUiContext);
  if (value === null) throw new Error("useClientsUi doit être utilisé sous <ClientsModule>.");
  return value;
}
