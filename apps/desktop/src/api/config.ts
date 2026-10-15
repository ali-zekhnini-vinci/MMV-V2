// Origine de MMV.Api. Développement uniquement : VITE_MMV_API_BASE_URL (non secrète) est lue depuis
// apps/desktop/.env.development, chargé par `vite` en mode dev et ignoré par `vite build`.
// Le choix DNS/IP sécurisé du poste client (production) n'est pas implémenté en 01B.

export type ApiBaseUrlResult = { ok: true; url: URL } | { ok: false; reason: string };

export function resolveApiBaseUrl(raw: string | undefined): ApiBaseUrlResult {
  if (!raw?.trim()) {
    return { ok: false, reason: "VITE_MMV_API_BASE_URL absente." };
  }

  let url: URL;
  try {
    url = new URL(raw.trim());
  } catch {
    return { ok: false, reason: "VITE_MMV_API_BASE_URL n'est pas une URL absolue." };
  }

  if (url.protocol !== "https:") {
    return { ok: false, reason: "HTTPS obligatoire : aucun repli HTTP." };
  }
  if (url.username || url.password || url.search || url.hash || (url.pathname !== "/" && url.pathname !== "")) {
    return { ok: false, reason: "Origine seule attendue (https://hôte[:port])." };
  }

  return { ok: true, url: new URL(url.origin) };
}

export const apiBaseUrl: ApiBaseUrlResult = resolveApiBaseUrl(
  // `env` est absent hors Vite (ex. tests `node --test`).
  import.meta.env?.VITE_MMV_API_BASE_URL,
);
