// PROTOTYPE 04-A — actions du dossier optique proposées selon les droits SIMULÉS du banc. Noms de permissions de la
// proposition D-04-05 option B (PROPOSED) : ils n'existent ni dans MMV.Api (Permissions.cs) ni en base, aucun compte
// réel ne peut les détenir. Garde d'AFFICHAGE uniquement : le double (comme le futur serveur) refuse par 403.
import { createContext, useContext } from "react";

export const OPTICAL_PERMISSIONS = {
  read: "optical.read",
  prescriptionsWrite: "optical.prescriptions.write",
  measurementsWrite: "optical.measurements.write",
  invalidate: "optical.invalidate",
  documentsRead: "optical.documents.read",
  documentsWrite: "optical.documents.write",
} as const;

export interface OpticalAccess {
  canRead: boolean;
  canWritePrescriptions: boolean;
  canWriteMeasurements: boolean;
  canInvalidate: boolean;
  canReadDocuments: boolean;
  canWriteDocuments: boolean;
}

export function opticalAccess(permissions: readonly string[]): OpticalAccess {
  const has = (permission: string) => permissions.includes(permission);
  // `clients.read` est exigé avec chaque droit optique (D-04-05.b) : sans lui, rien n'est proposé.
  const read = has("clients.read") && has(OPTICAL_PERMISSIONS.read);
  return {
    canRead: read,
    canWritePrescriptions: read && has(OPTICAL_PERMISSIONS.prescriptionsWrite),
    canWriteMeasurements: read && has(OPTICAL_PERMISSIONS.measurementsWrite),
    canInvalidate: read && has(OPTICAL_PERMISSIONS.invalidate),
    canReadDocuments: read && has(OPTICAL_PERMISSIONS.documentsRead),
    canWriteDocuments: read && has(OPTICAL_PERMISSIONS.documentsWrite),
  };
}

export const canWriteKind = (access: OpticalAccess, kind: "prescription" | "measurement") =>
  kind === "prescription" ? access.canWritePrescriptions : access.canWriteMeasurements;

export const OpticalAccessContext = createContext<OpticalAccess>(opticalAccess([]));

export const useOpticalAccess = (): OpticalAccess => useContext(OpticalAccessContext);
