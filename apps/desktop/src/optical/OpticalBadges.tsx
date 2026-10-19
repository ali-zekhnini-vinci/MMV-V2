// PROTOTYPE 04-A — badges de type (D-04-01) et de statut dérivé (GAP-04-11, E-02). Texte toujours présent : la
// couleur n'est jamais le seul porteur d'information. Déclinaison du badge client (clients-badge, Clients.css).
import { Ban, CircleDashed, ClipboardCheck, FileText, PencilLine, Ruler } from "lucide-react";
import { cn } from "../lib/utils.ts";
import type { DisplayStatus, OpticalKind } from "./opticalContract.ts";
import { KIND_LABEL, STATUS_LABEL } from "./opticalLabels.ts";

export function KindBadge({ kind, className }: { kind: OpticalKind; className?: string }) {
  return (
    <span className={cn("clients-badge", `optical-kind optical-kind--${kind}`, className)}>
      {kind === "prescription" ? <FileText size={12} aria-hidden="true" /> : <Ruler size={12} aria-hidden="true" />}
      {KIND_LABEL[kind]}
    </span>
  );
}

const STATUS_ICON = { draft: CircleDashed, confirmed: ClipboardCheck, rectification: PencilLine, invalidated: Ban } as const;

export function OpticalStatusBadge({ status, revisionNo }: { status: DisplayStatus; revisionNo?: number | null }) {
  const Icon = STATUS_ICON[status];
  const suffix = (status === "confirmed" || status === "rectification") && revisionNo ? ` — révision ${revisionNo}` : "";
  return (
    <span className={cn("clients-badge", `optical-status optical-status--${status}`)}>
      <Icon size={12} aria-hidden="true" />
      {status === "rectification" ? `Confirmé${suffix} · rectification en cours` : `${STATUS_LABEL[status]}${suffix}`}
    </span>
  );
}

/** Mention permanente des éléments prototypes fondés sur des propositions non ratifiées. */
export function ProposedTag({ refs }: { refs: string }) {
  return (
    <span className="optical-proposed" title={`Proposition non ratifiée : ${refs}`}>
      Prototype · {refs} PROPOSED
    </span>
  );
}
