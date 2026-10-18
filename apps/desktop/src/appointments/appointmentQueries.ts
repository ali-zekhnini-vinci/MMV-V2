// Requêtes et mutations Rendez-vous (TanStack Query v5), sur le modèle de clients/clientQueries.ts : appels
// via `authorized` (jeton en mémoire, 401 → fin de session), annulation par signal, invalidation de toutes les
// clés `appointments` après écriture. Interrupteur de repli désactivé : aucune requête n'est émise.
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  type AppointmentDetail,
  type AppointmentInput,
  type AppointmentRange,
  cancelAppointment,
  type ClientAppointmentQuery,
  listClientAppointments,
  createAppointment,
  getAppointment,
  getAppointmentOptions,
  listAppointments,
  restoreAppointment,
  updateAppointment,
} from "../api/appointments.ts";
import { CLIENT_PAGE_SIZE, listClients } from "../api/clients.ts";
import { unwrap } from "../api/queryClient.ts";
import { useAuth } from "../auth/authContext.ts";
import { useAppointmentsService } from "./appointmentsService.ts";

export const appointmentKeys = {
  all: ["appointments"] as const,
  options: () => [...appointmentKeys.all, "options"] as const,
  lists: () => [...appointmentKeys.all, "list"] as const,
  list: (range: AppointmentRange) => [...appointmentKeys.lists(), range] as const,
  detail: (id: string) => [...appointmentKeys.all, "detail", id] as const,
};

export function useAppointmentOptions() {
  const { authorized } = useAuth();
  const { enabled } = useAppointmentsService();
  return useQuery({
    queryKey: appointmentKeys.options(),
    queryFn: async ({ signal }) => unwrap(await authorized((url, token) => getAppointmentOptions(url, token, signal))),
    enabled,
    staleTime: 5 * 60_000,
  });
}

export function useAppointmentList(range: AppointmentRange | null) {
  const { authorized } = useAuth();
  const { enabled } = useAppointmentsService();
  return useQuery({
    queryKey: appointmentKeys.list(range ?? { fromUtc: "", toUtc: "", status: "planned" }),
    queryFn: async ({ signal }) => unwrap(await authorized((url, token) => listAppointments(url, token, range as AppointmentRange, signal))),
    enabled: enabled && range !== null,
    // Navigation jour/semaine : la période précédente reste affichée pendant le chargement de la suivante.
    placeholderData: keepPreviousData,
  });
}

/**
 * Historique des rendez-vous d'un client (03-D1). Clé sous `appointmentKeys.lists()` : toute écriture de rendez-vous
 * l'invalide. `allowed` = droit appointments.read de la session (garde d'affichage ; le serveur décide).
 */
export function useClientAppointments(clientId: string, query: ClientAppointmentQuery, allowed: boolean) {
  const { authorized } = useAuth();
  const { enabled } = useAppointmentsService();
  return useQuery({
    queryKey: [...appointmentKeys.lists(), "client", clientId, query] as const,
    queryFn: async ({ signal }) => unwrap(await authorized((url, token) => listClientAppointments(url, token, clientId, query, signal))),
    enabled: enabled && allowed,
    placeholderData: keepPreviousData,
  });
}

export function useAppointmentDetail(id: string | null) {
  const { authorized } = useAuth();
  const { enabled } = useAppointmentsService();
  return useQuery({
    queryKey: appointmentKeys.detail(id ?? ""),
    queryFn: async ({ signal }) => unwrap(await authorized((url, token) => getAppointment(url, token, id as string, signal))),
    enabled: enabled && id !== null,
  });
}

/** Sélecteur de client (planche 40) : clients ACTIFS seulement (D-03-05), recherche serveur ≥ 2 caractères. */
export function useClientPicker(search: string, allowed: boolean) {
  const { authorized } = useAuth();
  const { enabled } = useAppointmentsService();
  const term = search.trim();
  return useQuery({
    queryKey: ["clients", "picker", term] as const,
    queryFn: async ({ signal }) =>
      unwrap(
        await authorized((url, token) =>
          listClients(url, token, { search: term, status: "active", sort: "name", direction: "asc", page: 1, pageSize: CLIENT_PAGE_SIZE }, signal),
        ),
      ),
    enabled: enabled && allowed && term.length >= 2,
    placeholderData: keepPreviousData,
  });
}

function useAppointmentWrite<TVariables>(write: (url: URL, token: string, variables: TVariables) => ReturnType<typeof getAppointment>) {
  const { authorized } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: TVariables) => unwrap(await authorized((url, token) => write(url, token, variables))),
    onSuccess: async (appointment: AppointmentDetail) => {
      queryClient.setQueryData(appointmentKeys.detail(appointment.id), appointment);
      await queryClient.invalidateQueries({ queryKey: appointmentKeys.lists() });
    },
    onError: async (error) => {
      // Version périmée ou disparition : la vérité serveur est rechargée (un conflit horaire ne change rien).
      if (error.error.code === "version_conflict" || error.error.kind === "not-found") {
        await queryClient.invalidateQueries({ queryKey: appointmentKeys.all });
      }
      // Collaborateur retiré entre-temps (03-B3, constaté en natif) : la liste des éligibles est relue sans attendre
      // l'expiration du cache, pour ne plus le proposer comme éligible.
      if (error.error.code === "responsible_unavailable" || error.error.fieldErrors?.responsibleUserId?.includes("not_eligible")) {
        await queryClient.invalidateQueries({ queryKey: appointmentKeys.options() });
      }
    },
  });
}

export const useCreateAppointment = () => useAppointmentWrite<AppointmentInput>((url, token, input) => createAppointment(url, token, input));

export const useUpdateAppointment = () =>
  useAppointmentWrite<{ id: string; input: AppointmentInput; version: number }>((url, token, { id, input, version }) =>
    updateAppointment(url, token, id, input, version),
  );

export const useCancelAppointment = () =>
  useAppointmentWrite<{ id: string; version: number; reason: string | null }>((url, token, { id, version, reason }) =>
    cancelAppointment(url, token, id, version, reason),
  );

export const useRestoreAppointment = () =>
  useAppointmentWrite<{ id: string; version: number }>((url, token, { id, version }) => restoreAppointment(url, token, id, version));
