// Données serveur des écrans Catalogue & stock (TanStack Query), magasin actif et notification ; séparé des composants
// (Fast Refresh).
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createContext, type KeyboardEvent, useContext } from "react";
import { type Authorized, getCatalog, previewCatalog, type Store, writeCatalog } from "../api/catalog.ts";
import type { ApiError } from "../api/client.ts";
import { unwrap } from "../api/queryClient.ts";
import { useAuth } from "../auth/authContext.ts";
import { ERROR_MESSAGES } from "./catalogFormat.ts";

export const KEY = ["catalog"] as const;

export function useAuthorized(): Authorized {
  return useAuth().authorized as Authorized;
}

export function useCatalogQuery<T>(path: string | null, key: readonly unknown[] = []) {
  const authorized = useAuthorized();
  return useQuery({
    queryKey: [...KEY, path, ...key],
    queryFn: async () => unwrap(await getCatalog<T>(authorized, path ?? "")),
    enabled: path !== null,
  });
}

/** Écriture : invalide tout le module (listes, fiches, soldes) après succès ou issue incertaine. */
export function useCatalogWrite<T = unknown>() {
  const authorized = useAuthorized();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ method, path, body }: { method: "POST" | "PUT"; path: string; body: Record<string, unknown> }) =>
      unwrap(await writeCatalog<T>(authorized, method, path, body)),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: [...KEY] }),
  });
}

/** Aperçu serveur (aucune écriture, aucun `requestId`, aucune invalidation). */
export function useCatalogPreview<T = unknown>() {
  const authorized = useAuthorized();
  return useMutation({
    mutationFn: async ({ path, body }: { path: string; body: Record<string, unknown> }) => unwrap(await previewCatalog<T>(authorized, path, body)),
  });
}

export interface StoreContextValue {
  stores: Store[];
  store: Store | null;
  setStoreId: (id: string) => void;
}

export const StoreContext = createContext<StoreContextValue>({ stores: [], store: null, setStoreId: () => undefined });

export const useActiveStore = () => useContext(StoreContext);

export interface ToastValue {
  show: (title: string, text?: string, tone?: "ok" | "err") => void;
}

export const ToastContext = createContext<ToastValue>({ show: () => undefined });

export const useToast = () => useContext(ToastContext);

export function errorMessage(error: ApiError | undefined): string {
  if (!error) return "";
  return (error.code && ERROR_MESSAGES[error.code]) ?? error.message;
}

/** Ligne de table ouvrant une fiche (souris, Entrée) : focalisable au clavier, anneau de focus visible. */
export function rowLink(href: string, open: (href: string) => void) {
  return {
    "data-href": href,
    tabIndex: 0,
    onClick: () => open(href),
    onKeyDown: (event: KeyboardEvent) => {
      if (event.key === "Enter") open(href);
    },
  };
}
