// États transverses du module Clients (Lot 2 DRAFT, section 7) : vide, chargement, erreur technique,
// service non activé. Aucune donnée inventée : chaque état décrit honnêtement la situation.
import { Loader2, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "../lib/utils.ts";

export type StateTone = "accent" | "info" | "error" | "warning" | "neutral";

interface StateBlockProps {
  tone: StateTone;
  icon: LucideIcon;
  title: string;
  children: ReactNode;
  action?: ReactNode;
  /** `alert` pour une erreur, `status` sinon. */
  role?: "alert" | "status";
}

export function StateBlock({ tone, icon: Icon, title, children, action, role = "status" }: StateBlockProps) {
  return (
    <div className="clients-panel">
      <div className="clients-state" role={role}>
        <span className={cn("clients-state__icon", `tone--${tone}`)} aria-hidden="true">
          <Icon size={32} />
        </span>
        <h2 className="clients-state__title">{title}</h2>
        <div className="clients-state__text">{children}</div>
        {action && <div className="clients-state__actions">{action}</div>}
      </div>
    </div>
  );
}

const SKELETON_WIDTHS = ["22%", "16%", "24%", "12%", "12%", "10%"];

export function LoadingBlock({ label }: { label: string }) {
  return (
    <div className="clients-panel" aria-busy="true">
      <div className="clients-loading__head" role="status">
        <Loader2 size={16} className="clients-spin" aria-hidden="true" />
        {label}
      </div>
      <div aria-hidden="true">
        {Array.from({ length: 9 }, (_, row) => (
          <div key={row} className="clients-loading__row">
            <span className="clients-skel clients-skel--avatar" />
            {SKELETON_WIDTHS.map((width, column) => (
              <span key={column} className="clients-skel" style={{ width }} />
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
