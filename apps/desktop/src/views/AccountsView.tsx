// Comptes (administration technique, lecture seule) : liste réelle renvoyée par GET /api/admin/accounts,
// autorisée côté serveur par la policy SystemAdmin. Aucune création/modification ici (hors phase 02).
import { useCallback, useEffect, useState } from "react";
import { type AccountSummary, fetchAccounts, PERMISSION_SYSTEM_ADMIN } from "../api/auth.ts";
import type { ApiError } from "../api/client.ts";
import { useAuth } from "../auth/authContext.ts";
import { ForbiddenView } from "./ForbiddenView.tsx";

type AccountsState =
  | { state: "loading" }
  | { state: "loaded"; accounts: AccountSummary[] }
  | { state: "error"; error: ApiError };

export function AccountsView() {
  const { authorized } = useAuth();
  const [state, setState] = useState<AccountsState>({ state: "loading" });

  const load = useCallback(async (signal?: AbortSignal) => {
    setState({ state: "loading" });
    const result = await authorized((url, token) => fetchAccounts(url, token, signal));
    if (signal?.aborted) return;
    setState(result.ok ? { state: "loaded", accounts: result.data } : { state: "error", error: result.error });
  }, [authorized]);

  useEffect(() => {
    const controller = new AbortController();
    const initial = window.setTimeout(() => void load(controller.signal), 0);
    return () => {
      window.clearTimeout(initial);
      controller.abort();
    };
  }, [load]);

  if (state.state === "error" && state.error.kind === "forbidden") {
    return <ForbiddenView reason="Le serveur MMV a refusé l'accès à la liste des comptes (droit d'administration technique requis)." />;
  }

  return (
    <section className="view" aria-labelledby="accounts-title" aria-busy={state.state === "loading"}>
      <header className="view__header">
        <h1 id="accounts-title" className="view__title">
          Comptes
        </h1>
        <p className="view__lead">Comptes d&apos;accès à MMV (administration technique, lecture seule).</p>
      </header>

      <div className="card">
        {state.state === "loading" && (
          <p role="status" className="view__muted">
            Chargement des comptes…
          </p>
        )}
        {state.state === "error" && (
          <div role="alert" className="inline-alert">
            <span>{state.error.message}</span>
            <button type="button" className="button button--secondary" onClick={() => void load()}>
              Réessayer
            </button>
          </div>
        )}
        {state.state === "loaded" && (
          <table className="table">
            <caption className="visually-hidden">Comptes d&apos;accès</caption>
            <thead>
              <tr>
                <th scope="col">Nom affiché</th>
                <th scope="col">Identifiant</th>
                <th scope="col">Droits techniques</th>
                <th scope="col">État</th>
              </tr>
            </thead>
            <tbody>
              {state.accounts.map((account) => (
                <tr key={account.id}>
                  <td>{account.displayName}</td>
                  <td>{account.userName}</td>
                  <td>
                    {account.permissions.includes(PERMISSION_SYSTEM_ADMIN)
                      ? "Administration technique"
                      : "Accès à l'espace authentifié"}
                  </td>
                  <td>{account.isActive ? "Actif" : "Désactivé"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </section>
  );
}
