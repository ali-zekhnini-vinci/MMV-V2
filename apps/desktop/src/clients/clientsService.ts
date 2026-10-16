// Activation du service Clients côté client. Phase 03-A : **désactivé** tant que le dossier G03-METIER
// (modèle, permissions, migration) n'est pas ratifié et que MMV.Api n'expose pas /api/clients.
// Désactivé, l'écran affiche un état explicite « service non activé » : aucune requête vers une route
// inexistante, aucune donnée fictive. Les tests fournissent `{ enabled: true }` avec un double HTTP.
import { createContext, useContext } from "react";

export interface ClientsService {
  enabled: boolean;
}

/** Bascule unique à passer à `true` au sous-lot 03-A2, après GO G03-METIER et livraison de l'API. */
export const CLIENTS_API_ENABLED = false;

export const ClientsServiceContext = createContext<ClientsService>({ enabled: CLIENTS_API_ENABLED });

export const useClientsService = (): ClientsService => useContext(ClientsServiceContext);
