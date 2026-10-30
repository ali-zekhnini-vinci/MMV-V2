/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origine HTTPS de MMV.Api en développement (non secrète). */
  readonly VITE_MMV_API_BASE_URL?: string;
  /** Interrupteur du dossier optique réel (04-B) : `enabled` seulement ; absent = « Ordonnances — phase ultérieure ». */
  readonly VITE_MMV_OPTICAL_MODULE?: string;
  /** Interrupteur du module Catalogue & stock (Phase 05) : `enabled` seulement ; absent = « Produits » et « Inventaire » reportés. */
  readonly VITE_MMV_CATALOG_MODULE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

/** Version du paquet client (apps/desktop/package.json), injectée par vite.config.ts. */
declare const __APP_VERSION__: string;
