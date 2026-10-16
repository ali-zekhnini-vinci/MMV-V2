// État réel de MMV.Api (/health/live) et de PostgreSQL (/health/ready) pour la barre d'état et
// l'écran de diagnostic. Vérification au montage, à la demande, et toutes les 30 s tant que le shell
// est affiché (endpoints anonymes, sans effet sur la session). Aucun voyant simulé.
import { useCallback, useEffect, useRef, useState } from "react";
import type { ApiBaseUrlResult } from "../api/config.ts";
import { checkTechnicalStatus, type TechnicalStatus } from "../api/health.ts";

export const HEALTH_INTERVAL_MS = 30_000;

export interface ServerHealth {
  /** Dernier état connu ; `null` avant la première réponse. */
  status: TechnicalStatus | null;
  /** Vérification en cours (premier chargement ou rechargement). */
  checking: boolean;
  reload: () => Promise<void>;
}

export function useServerHealth(baseUrl: ApiBaseUrlResult, intervalMs = HEALTH_INTERVAL_MS): ServerHealth {
  const [status, setStatus] = useState<TechnicalStatus | null>(() =>
    baseUrl.ok ? null : { state: "not-configured", reason: baseUrl.reason },
  );
  const [checking, setChecking] = useState(baseUrl.ok);
  const inFlight = useRef<AbortController | null>(null);

  const reload = useCallback(async () => {
    if (!baseUrl.ok) return;
    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;
    setChecking(true);
    const next = await checkTechnicalStatus(baseUrl.url, controller.signal);
    if (controller.signal.aborted) return;
    setStatus(next);
    setChecking(false);
  }, [baseUrl]);

  useEffect(() => {
    if (!baseUrl.ok) return;
    const initial = window.setTimeout(() => void reload(), 0);
    const timer = window.setInterval(() => void reload(), intervalMs);
    return () => {
      window.clearTimeout(initial);
      window.clearInterval(timer);
      inFlight.current?.abort();
    };
  }, [baseUrl, reload, intervalMs]);

  return { status, checking, reload };
}
