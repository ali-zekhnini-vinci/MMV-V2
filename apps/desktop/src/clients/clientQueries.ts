// Requêtes et mutations Clients (TanStack Query v5). Chaque appel passe par `authorized` (jeton en mémoire,
// 401 → fin de session) ; l'annulation suit le signal fourni par TanStack Query (changement de page, de
// recherche ou démontage). Toute écriture réussie invalide l'ensemble des clés `clients`.
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  archiveClient,
  type ClientDetail,
  type ClientInput,
  type ClientListParams,
  createClient,
  getClient,
  listClients,
  reactivateClient,
  updateClient,
} from "../api/clients.ts";
import { unwrap } from "../api/queryClient.ts";
import { useAuth } from "../auth/authContext.ts";
import { useClientsService } from "./clientsService.ts";

export const clientKeys = {
  all: ["clients"] as const,
  lists: () => [...clientKeys.all, "list"] as const,
  list: (params: ClientListParams) => [...clientKeys.lists(), params] as const,
  detail: (id: string) => [...clientKeys.all, "detail", id] as const,
};

export function useClientList(params: ClientListParams) {
  const { authorized } = useAuth();
  const { enabled } = useClientsService();
  return useQuery({
    queryKey: clientKeys.list(params),
    queryFn: async ({ signal }) => unwrap(await authorized((url, token) => listClients(url, token, params, signal))),
    enabled,
    // Pagination/tri : l'ancienne page reste affichée pendant le chargement de la suivante.
    placeholderData: keepPreviousData,
  });
}

export function useClientDetail(id: string) {
  const { authorized } = useAuth();
  const { enabled } = useClientsService();
  return useQuery({
    queryKey: clientKeys.detail(id),
    queryFn: async ({ signal }) => unwrap(await authorized((url, token) => getClient(url, token, id, signal))),
    enabled,
  });
}

function useClientWrite<TVariables>(write: (url: URL, token: string, variables: TVariables) => ReturnType<typeof getClient>) {
  const { authorized } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: TVariables) => unwrap(await authorized((url, token) => write(url, token, variables))),
    onSuccess: async (client: ClientDetail) => {
      queryClient.setQueryData(clientKeys.detail(client.id), client);
      await queryClient.invalidateQueries({ queryKey: clientKeys.lists() });
      // 03-D2 : l'historique du dossier a changé côté serveur.
      await queryClient.invalidateQueries({ queryKey: ["audit"] });
    },
    onError: async (error) => {
      // Conflit (modifié ailleurs) ou disparition : la vérité serveur est rechargée.
      if (error.error.kind === "conflict" || error.error.kind === "not-found") {
        await queryClient.invalidateQueries({ queryKey: clientKeys.all });
      }
    },
  });
}

export const useCreateClient = () => useClientWrite<ClientInput>((url, token, input) => createClient(url, token, input));

export const useUpdateClient = () =>
  useClientWrite<{ id: string; input: ClientInput; version: number }>((url, token, { id, input, version }) =>
    updateClient(url, token, id, input, version),
  );

export const useArchiveClient = () =>
  useClientWrite<{ id: string; version: number }>((url, token, { id, version }) => archiveClient(url, token, id, version));

export const useReactivateClient = () =>
  useClientWrite<{ id: string; version: number }>((url, token, { id, version }) => reactivateClient(url, token, id, version));
