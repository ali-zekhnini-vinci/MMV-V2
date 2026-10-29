// Présentation de l'historique des actions (03-D2) : libellés français des actions et des champs, périodes de filtre.
// Module pur. Les valeurs affichées sont celles que le SERVEUR a conservées et libellées ; une valeur masquée n'est
// jamais reconstituée ici.
import type { AuditChange, AuditModule } from "../api/audit.ts";

export const AUDIT_ACTION_LABELS: Record<string, string> = {
  "client.created": "Création du dossier client",
  "client.updated": "Modification des informations",
  "client.archived": "Archivage du dossier",
  "client.reactivated": "Réactivation du dossier",
  "appointment.created": "Rendez-vous créé",
  "appointment.updated": "Rendez-vous modifié",
  "appointment.moved": "Rendez-vous déplacé",
  "appointment.responsible_changed": "Changement de responsable",
  "appointment.cancelled": "Rendez-vous annulé",
  "appointment.restored": "Rendez-vous rétabli",
  // 04-B : dossier optique (servi avec audit.read ET optical.read ; aucune valeur optique dans le journal).
  "prescription.created": "Ordonnance médicale créée",
  "prescription.draft_updated": "Ordonnance médicale : brouillon modifié",
  "prescription.confirmed": "Ordonnance médicale confirmée",
  "prescription.rectification_started": "Ordonnance médicale : modification ouverte",
  "prescription.rectification_discarded": "Ordonnance médicale : modification abandonnée",
  "prescription.invalidated": "Ordonnance médicale invalidée",
  "measurement.created": "Mesure magasin créée",
  "measurement.draft_updated": "Mesure magasin : brouillon modifié",
  "measurement.confirmed": "Mesure magasin confirmée",
  "measurement.rectification_started": "Mesure magasin : modification ouverte",
  "measurement.rectification_discarded": "Mesure magasin : modification abandonnée",
  "measurement.invalidated": "Mesure magasin invalidée",
};

export const AUDIT_FIELD_LABELS: Record<string, string> = {
  firstName: "Prénom",
  lastName: "Nom",
  birthDate: "Date de naissance",
  phone: "Téléphone",
  email: "Email",
  addressLine: "Adresse",
  city: "Ville",
  postalCode: "Code postal",
  notes: "Notes",
  startsAtUtc: "Date et heure",
  durationMinutes: "Durée",
  reasonCode: "Motif",
  responsibleUserId: "Responsable",
  cancellationReason: "Motif d'annulation",
  clientId: "Client",
  provenance: "Provenance",
  values: "Valeurs optiques",
  comment: "Commentaire",
  revisionNo: "Révision",
  baseRevisionNo: "Révision de départ",
  rectificationReasonCode: "Motif de la modification",
  invalidationReasonCode: "Motif d'invalidation",
};

export const auditActionLabel = (action: string) => AUDIT_ACTION_LABELS[action] ?? `Action « ${action} »`;
export const auditFieldLabel = (field: string) => AUDIT_FIELD_LABELS[field] ?? field;

/** « Téléphone » (masqué), « Motif : Conseil / Devis » (création), « Date et heure : 16/07/2026 10:00 → 16/07/2026 11:00 ». */
export function describeAuditChange(change: AuditChange): string {
  const label = auditFieldLabel(change.field);
  if (change.masked) return label;
  if (change.oldDisplay === null && change.old === null) return `${label} : ${change.newDisplay ?? change.new ?? "—"}`;
  return `${label} : ${change.oldDisplay ?? change.old ?? "—"} → ${change.newDisplay ?? change.new ?? "—"}`;
}

/** Horodatage UTC → date et heure du POSTE (« 16/07/2026 10:00 »). */
export function formatAuditInstant(utc: string, locale = "fr-FR", timeZone?: string): string {
  const date = new Date(utc);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString(locale, { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", timeZone });
}

export type AuditPeriod = "all" | "7d" | "30d" | "12m";

export const AUDIT_PERIODS: { value: AuditPeriod; label: string }[] = [
  { value: "all", label: "Toute la période" },
  { value: "7d", label: "7 derniers jours" },
  { value: "30d", label: "30 derniers jours" },
  { value: "12m", label: "12 derniers mois" },
];

export const AUDIT_MODULES: { value: AuditModule | "all"; label: string }[] = [
  { value: "all", label: "Toutes les actions" },
  { value: "clients", label: "Dossier client" },
  { value: "appointments", label: "Rendez-vous" },
];

/** Début de période (UTC) ; `undefined` = sans borne. */
export function periodStart(period: AuditPeriod, now: Date): string | undefined {
  const days = period === "7d" ? 7 : period === "30d" ? 30 : period === "12m" ? 365 : null;
  return days === null ? undefined : new Date(now.getTime() - days * 86_400_000).toISOString();
}

/** Clés TanStack Query de l'historique (invalidées après toute écriture Clients ou Rendez-vous). */
export const auditKeys = {
  all: ["audit"] as const,
  client: (clientId: string, filters: { module: AuditModule | "all"; period: AuditPeriod }) => [...auditKeys.all, "client", clientId, filters] as const,
};
