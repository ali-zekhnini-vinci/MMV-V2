// BANC 04-A — profils SIMULÉS pour l'affichage du prototype. Ils reprennent la matrice INDICATIVE de
// PERMISSIONS_04_PROPOSED.md (D-04-13.c, non validée) : ce ne sont ni des rôles MMV ni des attributions, et aucun
// droit réel n'existe (aucune permission optical.* dans MMV.Api ni en base). Seul le double en mémoire les lit.
import { OPTICAL_PERMISSIONS as P } from "../optical/opticalAccess.ts";
import type { OpticalActor } from "../optical/opticalContract.ts";
import { BENCH_MEASURERS } from "./benchSeed.ts";

export interface BenchProfile {
  id: string;
  label: string;
  actor: OpticalActor;
  permissions: readonly string[];
}

const CR = "clients.read";

export const BENCH_PROFILES: readonly BenchProfile[] = [
  { id: "all", label: "Démonstration — tous les droits optiques proposés", actor: BENCH_MEASURERS[0], permissions: [CR, ...Object.values(P)] },
  {
    id: "optician",
    label: "Opticien habilité à mesurer (indicatif)",
    actor: BENCH_MEASURERS[1],
    permissions: [CR, P.read, P.prescriptionsWrite, P.measurementsWrite, P.documentsRead, P.documentsWrite],
  },
  { id: "transcriber", label: "Collaborateur chargé de la transcription (indicatif)", actor: { id: "usr-fictif-3", displayName: "Fictif-Transcription" }, permissions: [CR, P.read, P.prescriptionsWrite, P.documentsRead, P.documentsWrite] },
  { id: "manager", label: "Responsable du magasin (indicatif, sans écriture)", actor: { id: "usr-fictif-4", displayName: "Fictif-Responsable" }, permissions: [CR, P.read, P.invalidate, P.documentsRead] },
  { id: "reader", label: "Lecture seule, sans les pièces (technicien atelier, indicatif)", actor: { id: "usr-fictif-5", displayName: "Fictif-Atelier" }, permissions: [CR, P.read] },
  { id: "none", label: "Sans droit optique (accueil, indicatif)", actor: { id: "usr-fictif-6", displayName: "Fictif-Accueil" }, permissions: [CR] },
];

export const findProfile = (id: string | null): BenchProfile => BENCH_PROFILES.find((profile) => profile.id === id) ?? BENCH_PROFILES[0];
