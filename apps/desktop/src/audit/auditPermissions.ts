// Onglet Historique (03-D2) proposé selon la session : droit DÉDIÉ `audit.read`, distinct des droits Clients et
// Rendez-vous et de `system.admin`. Garde d'affichage seulement : le serveur reste seul juge.
import { PERMISSION_AUDIT_READ } from "../api/auth.ts";
import { useAuth } from "../auth/authContext.ts";

export const hasAuditRead = (permissions: readonly string[]) => permissions.includes(PERMISSION_AUDIT_READ);

export function useAuditPermission(): boolean {
  const { state } = useAuth();
  return state.status === "authenticated" && hasAuditRead(state.session.user.permissions);
}
