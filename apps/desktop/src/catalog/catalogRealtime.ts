// Table du temps réel du module Catalogue & stock (COR-05-02) : chemins d'API relus quand un sujet change.
// Les sujets reçus sont déjà filtrés par le serveur selon les droits de la session ; la relecture repasse par l'API.
import type { RealtimeRouting } from "../realtime/realtimeContext.ts";
import { KEY } from "./catalogData.ts";

export const CATALOG_REALTIME: RealtimeRouting = {
  keyRoot: KEY[0],
  topicQueries: {
    catalog: ["/api/catalog", "/api/pricing", "/api/stock", "/api/suppliers", "/api/inventories"],
    pricing: ["/api/pricing", "/api/catalog/variants", "/api/catalog/models"],
    stock: ["/api/stock", "/api/catalog/variants", "/api/catalog/models", "/api/inventories"],
    suppliers: ["/api/suppliers", "/api/catalog/variants", "/api/catalog/models"],
    inventory: ["/api/inventories", "/api/stock"],
    imports: ["/api/imports"],
  },
};
