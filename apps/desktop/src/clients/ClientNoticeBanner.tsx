// Confirmation d'une opération réussie (archivage, réactivation, enregistrement) — équivalent sobre du
// toast de la maquette (Lot 2 planches 24, 26), sans bibliothèque supplémentaire : région `status` annoncée.
import { CheckCircle2, X } from "lucide-react";
import { useClientsUi } from "./clientsUi.ts";

export function ClientNoticeBanner() {
  const { notice, setNotice } = useClientsUi();
  if (notice === null) return null;
  return (
    <div className="clients-notice" role="status">
      <span className="clients-notice__icon" aria-hidden="true">
        <CheckCircle2 size={20} />
      </span>
      <div>
        <div className="clients-notice__title">{notice.title}</div>
        <div className="clients-notice__text">{notice.message}</div>
      </div>
      <button type="button" className="clients-notice__close" aria-label="Fermer le message" onClick={() => setNotice(null)}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
