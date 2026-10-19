// Actions Clients proposées selon les permissions de la session (phase 03-A3, O-03-09). Garde d'AFFICHAGE
// uniquement : le serveur reste seul juge (policies clients.*) et `system.admin` n'accorde aucune action Clients.
import { PERMISSION_CLIENTS_ARCHIVE, PERMISSION_CLIENTS_WRITE } from "../api/auth.ts";
import { useAuth } from "../auth/authContext.ts";

export interface ClientPermissions {
  /** Créer et modifier (`clients.write`). */
  canWrite: boolean;
  /** Archiver et réactiver (`clients.archive`). */
  canArchive: boolean;
}

export function clientPermissions(permissions: readonly string[]): ClientPermissions {
  return {
    canWrite: permissions.includes(PERMISSION_CLIENTS_WRITE),
    canArchive: permissions.includes(PERMISSION_CLIENTS_ARCHIVE),
  };
}

export function useClientPermissions(): ClientPermissions {
  const { state } = useAuth();
  return clientPermissions(state.status === "authenticated" ? state.session.user.permissions : []);
}

export const READ_ONLY_HINT = "Consultation seule : votre compte ne permet pas de créer ni de modifier des clients.";
export const NO_WRITE_TITLE = "Votre compte ne permet pas de modifier les clients (clients.write)";
export const NO_ARCHIVE_TITLE = "Votre compte ne permet pas d'archiver ni de réactiver les clients (clients.archive)";
