/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origine HTTPS de MMV.Api en développement (non secrète). */
  readonly VITE_MMV_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
