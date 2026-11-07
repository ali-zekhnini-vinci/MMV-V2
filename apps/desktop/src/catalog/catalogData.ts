// Données serveur des écrans Catalogue & stock (TanStack Query), magasin actif et notification ; séparé des composants
// (Fast Refresh).
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createContext, type Dispatch, type KeyboardEvent, type SetStateAction, useContext, useState } from "react";
import { type Authorized, getCatalog, previewCatalog, type Store, writeCatalog } from "../api/catalog.ts";
import type { ApiError } from "../api/client.ts";
import { unwrap } from "../api/queryClient.ts";
import { useAuth } from "../auth/authContext.ts";
import { ERROR_MESSAGES } from "./catalogFormat.ts";

export const KEY = ["catalog"] as const;

export function useAuthorized(): Authorized {
  return useAuth().authorized as Authorized;
}

/**
 * Lecture serveur. `keepPrevious` (listes paginées et recherchées, COR-05-07, comme la liste Clients) : la page
 * affichée reste visible pendant le chargement de la suivante ; la clé de requête étant le chemin complet, une réponse
 * tardive d'une ancienne recherche n'est jamais affichée à la place de la recherche courante.
 */
export function useCatalogQuery<T>(path: string | null, key: readonly unknown[] = [], options: { keepPrevious?: boolean } = {}) {
  const authorized = useAuthorized();
  return useQuery({
    queryKey: [...KEY, path, ...key],
    queryFn: async () => unwrap(await getCatalog<T>(authorized, path ?? "")),
    enabled: path !== null,
    placeholderData: options.keepPrevious ? keepPreviousData : undefined,
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

/**
 * Magasin de l'INSTALLATION (COR-05-01) : un serveur local = un magasin, résolu par le serveur (`GET /api/store`).
 * Le poste ne choisit jamais de magasin ; l'identifiant transmis aux routes n'est qu'une assertion vérifiée par l'API.
 */
/**
 * Saisie face au temps réel (COR-05-02). La version et les valeurs de départ sont FIGÉES à l'ouverture du formulaire :
 * une actualisation venue d'un autre poste ne remplace jamais la saisie et l'enregistrement part toujours de la
 * version vue par l'utilisateur (le serveur refuse alors `409 version_conflict`, jamais d'écrasement silencieux).
 * `remoteChanged` : la fiche a changé sur le serveur pendant la saisie. `rebase()` intègre la dernière version : les
 * champs que l'utilisateur n'a pas modifiés reprennent la valeur enregistrée, ses saisies sont conservées.
 */
export function useEditBase<T extends object>(version: number, values: T, setDraft: Dispatch<SetStateAction<T>>) {
  const [base, setBase] = useState(() => ({ version, values }));
  const rebase = () => {
    setDraft((draft) => mergeDraft(draft, base.values, values));
    setBase({ version, values });
  };
  return { version: base.version, initial: base.values, remoteChanged: version !== base.version, rebase };
}

/** Fusion à trois voies, clé par clé : valeur de l'utilisateur si modifiée, sinon nouvelle valeur du serveur. */
export function mergeDraft<T extends object>(draft: T, before: T, after: T): T {
  const merged = { ...draft };
  for (const key of Object.keys(after) as (keyof T)[]) {
    if (JSON.stringify(draft[key]) === JSON.stringify(before[key])) merged[key] = after[key];
  }
  return merged;
}

export interface StoreContextValue {
  store: Store | null;
  /** Origine de la résolution côté serveur : `configuration` (Installation:StoreCode) ou `single_store`. */
  source: string | null;
}

export const StoreContext = createContext<StoreContextValue>({ store: null, source: null });

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
