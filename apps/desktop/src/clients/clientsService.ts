// Activation du service Clients côté client. Phase 03-A2 (GO G03-METIER A/B du 17/10/2026) : **activé**,
// l'écran interroge les routes réelles /api/clients de MMV.Api (contrat docs/phase/03/API_CLIENTS.md).
// Tant que la migration 0003_ClientsModule n'est pas appliquée, le serveur répond par une erreur technique,
// affichée comme telle (aucune donnée fictive). Le serveur reste seul juge des droits clients.*.
// L'état explicite « service non activé » est conservé comme interrupteur de repli (O-03-04) : les tests le
// couvrent en fournissant `{ enabled: false }`.
import { createContext, useContext } from "react";

export interface ClientsService {
  enabled: boolean;
}

/** Bascule unique : `true` depuis le sous-lot 03-A2 (API livrée). */
export const CLIENTS_API_ENABLED = true;

export const ClientsServiceContext = createContext<ClientsService>({ enabled: CLIENTS_API_ENABLED });

export const useClientsService = (): ClientsService => useContext(ClientsServiceContext);
