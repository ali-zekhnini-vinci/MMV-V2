import { ShieldAlert } from "lucide-react";

export function ForbiddenView({ reason }: { reason: string }) {
  return (
    <section className="view view--centered" aria-labelledby="forbidden-title">
      <div className="empty-state">
        <span className="empty-state__icon empty-state__icon--error" aria-hidden="true">
          <ShieldAlert size={24} />
        </span>
        <h1 id="forbidden-title" className="view__title">
          Accès non autorisé
        </h1>
        <p className="view__lead">{reason}</p>
      </div>
    </section>
  );
}
