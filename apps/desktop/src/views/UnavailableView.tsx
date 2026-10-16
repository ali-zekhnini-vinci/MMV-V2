import { Clock } from "lucide-react";
import { LATER_MESSAGE } from "../shell/navigation.ts";

export function UnavailableView({ label, note }: { label: string; note?: string }) {
  return (
    <section className="view view--centered" aria-labelledby="unavailable-title">
      <div className="empty-state">
        <span className="empty-state__icon" aria-hidden="true">
          <Clock size={24} />
        </span>
        <h1 id="unavailable-title" className="view__title">
          {label}
        </h1>
        <p className="view__lead">{LATER_MESSAGE}. Aucun écran ni aucune donnée de ce module n&apos;existe encore.</p>
        {note && <p className="view__muted">{note}</p>}
      </div>
    </section>
  );
}
