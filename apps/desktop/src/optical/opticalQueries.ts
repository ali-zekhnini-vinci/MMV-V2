// PROTOTYPE 04-A — données du dossier optique (TanStack Query, mêmes conventions que clientQueries.ts) au-dessus
// d'un `OpticalGateway` fourni par contexte. En 04-A, seul le banc isolé en fournit un (double en mémoire) : sans
// fournisseur, aucune requête n'est possible (passerelle « non activée », refus explicite, jamais de données).
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createContext, useContext } from "react";
import { apiError, type ApiResult } from "../api/client.ts";
import { unwrap } from "../api/queryClient.ts";
import type { InvalidationRequest, OpticalGateway, OpticalKind, OpticalListParams, OpticalRecord, RecordContent, RectificationRequest } from "./opticalContract.ts";

const notActivated = <T>(): Promise<ApiResult<T>> => Promise.resolve({ ok: false, error: apiError("not-configured") });

/** Passerelle par défaut : service optique NON ACTIVÉ (aucune route /api/optical n'existe en 04-A). */
export const DISABLED_OPTICAL_GATEWAY: OpticalGateway = {
  listRecords: notActivated,
  listClientRecords: notActivated,
  getRecord: notActivated,
  listMeasurers: notActivated,
  createRecord: notActivated,
  saveDraft: notActivated,
  confirmDraft: notActivated,
  startRectification: notActivated,
  discardRectification: notActivated,
  invalidateRecord: notActivated,
};

export const OpticalGatewayContext = createContext<OpticalGateway>(DISABLED_OPTICAL_GATEWAY);

export const useOpticalGateway = (): OpticalGateway => useContext(OpticalGatewayContext);

const KEY = ["optical"] as const;

export function useOpticalList(params: OpticalListParams, enabled: boolean) {
  const gateway = useOpticalGateway();
  return useQuery({
    queryKey: [...KEY, "list", params] as const,
    queryFn: async () => unwrap(await gateway.listRecords(params)),
    enabled,
    placeholderData: keepPreviousData,
  });
}

export function useClientOpticalRecords(clientId: string, includeInvalidated: boolean, enabled: boolean) {
  const gateway = useOpticalGateway();
  return useQuery({
    queryKey: [...KEY, "client", clientId, includeInvalidated] as const,
    queryFn: async () => unwrap(await gateway.listClientRecords(clientId, includeInvalidated)),
    enabled,
  });
}

export function useOpticalRecord(kind: OpticalKind, id: string, enabled = true) {
  const gateway = useOpticalGateway();
  return useQuery({
    queryKey: [...KEY, "record", kind, id] as const,
    queryFn: async () => unwrap(await gateway.getRecord(kind, id)),
    enabled,
  });
}

export function useMeasurers(enabled: boolean) {
  const gateway = useOpticalGateway();
  return useQuery({ queryKey: [...KEY, "measurers"] as const, queryFn: async () => unwrap(await gateway.listMeasurers()), enabled, staleTime: 5 * 60_000 });
}

/** Écriture : la réponse du double remplace le détail en cache ; listes invalidées (rechargées à l'affichage). */
function useRecordWrite<TVariables>(write: (gateway: OpticalGateway, variables: TVariables) => Promise<ApiResult<OpticalRecord>>) {
  const gateway = useOpticalGateway();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: TVariables) => unwrap(await write(gateway, variables)),
    onSuccess: (record) => {
      queryClient.setQueryData([...KEY, "record", record.kind, record.id], record);
      void queryClient.invalidateQueries({ queryKey: [...KEY, "list"] });
      void queryClient.invalidateQueries({ queryKey: [...KEY, "client"] });
    },
    onError: (error) => {
      // Conflit ou refus : la version en cache est périmée ; la saisie à l'écran est conservée par le formulaire.
      if (error.error.kind === "conflict") void queryClient.invalidateQueries({ queryKey: [...KEY, "record"] });
    },
  });
}

export const useCreateRecord = () => useRecordWrite((gateway, input: { clientId: string; content: RecordContent }) => gateway.createRecord(input.clientId, input.content));

export const useSaveDraft = () =>
  useRecordWrite((gateway, input: { kind: OpticalKind; id: string; content: RecordContent; version: number }) => gateway.saveDraft(input.kind, input.id, input.content, input.version));

export const useConfirmDraft = () => useRecordWrite((gateway, input: { kind: OpticalKind; id: string; version: number }) => gateway.confirmDraft(input.kind, input.id, input.version));

export const useStartRectification = () =>
  useRecordWrite((gateway, input: { kind: OpticalKind; id: string; request: RectificationRequest }) => gateway.startRectification(input.kind, input.id, input.request));

export const useDiscardRectification = () =>
  useRecordWrite((gateway, input: { kind: OpticalKind; id: string; version: number }) => gateway.discardRectification(input.kind, input.id, input.version));

export const useInvalidateRecord = () =>
  useRecordWrite((gateway, input: { kind: OpticalKind; id: string; request: InvalidationRequest }) => gateway.invalidateRecord(input.kind, input.id, input.request));
