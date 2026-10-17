// Détail d'un rendez-vous (Lot 2 DRAFT, planches 32, 37, 38–39) : panneau latéral chargé depuis l'API
// (jamais depuis la ligne d'agenda, qui n'a pas les notes). Actions selon les permissions de session (garde
// d'affichage) et l'état : planifié → Modifier / Déplacer / Annuler ; annulé → Rétablir. Les règles fines
// (passé, client archivé, créneau libre au rétablissement) sont décidées par le serveur (D-03-15, D-03-16).
// Écart volontaire : badge « Planifié » au lieu de « Confirmé » (aucun circuit de confirmation décidé).
import { AlertTriangle, Calendar, CalendarX, Clock, Move, Pencil, RefreshCw, RotateCcw, Stethoscope, Timer, User, X } from "lucide-react";
import { type KeyboardEvent, type ReactNode, useEffect, useRef } from "react";
import { useNavigate } from "react-router";
import type { AppointmentDetail, AppointmentOptions } from "../api/appointments.ts";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { appointmentClientName } from "./appointmentForm.ts";
import type { AppointmentPermissions } from "./appointmentPermissions.ts";
import { NO_APPOINTMENT_CANCEL_TITLE, NO_APPOINTMENT_WRITE_TITLE } from "./appointmentPermissions.ts";
import { useAppointmentDetail } from "./appointmentQueries.ts";
import { clientInitials, type FailureView, reasonLabel, STATUS_LABEL } from "./appointmentsView.ts";
import { formatDuration, formatNumericDate, formatTimeRange, minutesBetween, utcToWall } from "./time.ts";

interface AppointmentDetailPanelProps {
  appointmentId: string;
  options: AppointmentOptions;
  permissions: AppointmentPermissions;
  failure: FailureView | null;
  restoring: boolean;
  onClose: () => void;
  onEdit: (appointment: AppointmentDetail) => void;
  onMove: (appointment: AppointmentDetail) => void;
  onCancel: (appointment: AppointmentDetail) => void;
  onRestore: (appointment: AppointmentDetail) => void;
}

export function AppointmentDetailPanel({
  appointmentId,
  options,
  permissions,
  failure,
  restoring,
  onClose,
  onEdit,
  onMove,
  onCancel,
  onRestore,
}: AppointmentDetailPanelProps) {
  const navigate = useNavigate();
  const query = useAppointmentDetail(appointmentId);
  const heading = useRef<HTMLHeadingElement>(null);

  // Le focus suit l'ouverture du panneau (Entrée sur un rendez-vous de la grille).
  useEffect(() => {
    heading.current?.focus();
  }, [appointmentId, query.isSuccess]);

  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      onClose();
    }
  };

  const head = (badge: ReactNode) => (
    <div className="rdv-side__head">
      {badge}
      <span className="rdv-grow" />
      <button type="button" className="rdv-iconbtn" aria-label="Fermer le détail" onClick={onClose}>
        <X size={18} aria-hidden="true" />
      </button>
    </div>
  );

  if (query.isPending) {
    return (
      <aside className="rdv-side" aria-label="Détail du rendez-vous" onKeyDown={onKeyDown} aria-busy="true">
        {head(null)}
        <p className="rdv-side__body clients-muted" role="status">
          Chargement du rendez-vous…
        </p>
      </aside>
    );
  }
  if (query.isError) {
    return (
      <aside className="rdv-side" aria-label="Détail du rendez-vous" onKeyDown={onKeyDown}>
        {head(null)}
        <div className="rdv-side__body" role="alert">
          <p>{query.error.error.kind === "not-found" ? "Ce rendez-vous n'existe plus sur le serveur MMV." : query.error.error.message}</p>
          <Button onClick={() => void query.refetch()} disabled={query.isFetching}>
            <RefreshCw size={16} aria-hidden="true" />
            Réessayer
          </Button>
        </div>
      </aside>
    );
  }

  const appointment = query.data;
  const cancelled = appointment.status === "cancelled";
  const wall = utcToWall(appointment.startsAtUtc, options.timeZone);
  const name = appointmentClientName(appointment);

  return (
    <aside className="rdv-side" aria-label={`Détail du rendez-vous de ${name}`} onKeyDown={onKeyDown}>
      {head(
        <span className={cn("clients-badge", cancelled ? "clients-badge--archived" : "clients-badge--info")}>
          <span className="clients-badge__dot" aria-hidden="true" />
          {STATUS_LABEL[appointment.status]}
        </span>,
      )}
      {cancelled && (
        <div className="clients-banner clients-banner--error rdv-side__banner">
          <span className="clients-banner__icon" aria-hidden="true">
            <CalendarX size={18} />
          </span>
          <div>
            <div className="clients-banner__title">Rendez-vous annulé</div>
            <div className="clients-banner__text">Motif : {appointment.cancellationReason ?? "non précisé"}</div>
          </div>
        </div>
      )}
      {failure && (
        <div className={cn("clients-banner rdv-side__banner", `clients-banner--${failure.tone}`)} role="alert">
          <span className="clients-banner__icon" aria-hidden="true">
            <AlertTriangle size={18} />
          </span>
          <div>
            <div className="clients-banner__title">{failure.title}</div>
            <div className="clients-banner__text">{failure.text}</div>
          </div>
        </div>
      )}
      <div className="rdv-side__body">
        <div className="rdv-detail__client">
          <span className="clients-avatar rdv-detail__avatar" aria-hidden="true">
            {clientInitials(appointment)}
          </span>
          <div>
            <h2 ref={heading} tabIndex={-1} className="rdv-detail__name">
              {name}
            </h2>
            <div className="rdv-detail__ref">
              {appointment.clientReference}
              {appointment.clientArchived && <span className="clients-badge clients-badge--archived rdv-detail__archived">Client archivé</span>}
            </div>
            {permissions.canPickClient && (
              <Button variant="tertiary" size="sm" className="rdv-detail__link" onClick={() => navigate(`/clients/${appointment.clientId}`)}>
                Voir la fiche client
              </Button>
            )}
          </div>
        </div>
        <dl className="rdv-detail__rows">
          <Row icon={<Stethoscope size={18} />} label="Motif" value={reasonLabel(options, appointment.reasonCode)} />
          <Row icon={<Calendar size={18} />} label="Date" value={formatNumericDate(wall.date)} />
          <Row icon={<Clock size={18} />} label="Heure" value={formatTimeRange(appointment.startsAtUtc, appointment.endsAtUtc, options.timeZone)} />
          <Row icon={<Timer size={18} />} label="Durée" value={formatDuration(minutesBetween(appointment.startsAtUtc, appointment.endsAtUtc))} />
          <Row icon={<User size={18} />} label="Responsable" value={appointment.responsibleDisplayName ?? "Non attribué"} />
        </dl>
        <div className="rdv-detail__notes">
          <div className="rdv-detail__label">Notes</div>
          <p>{appointment.notes ?? "Aucune note."}</p>
        </div>
      </div>
      <div className="rdv-side__foot rdv-side__foot--stack">
        {cancelled ? (
          <Button
            variant="primary"
            disabled={!permissions.canCancel || restoring}
            title={permissions.canCancel ? undefined : NO_APPOINTMENT_CANCEL_TITLE}
            onClick={() => onRestore(appointment)}
          >
            <RotateCcw size={16} aria-hidden="true" />
            {restoring ? "Rétablissement…" : "Rétablir le rendez-vous"}
          </Button>
        ) : (
          <>
            <div className="rdv-side__pair">
              <Button className="rdv-grow" disabled={!permissions.canWrite} title={permissions.canWrite ? undefined : NO_APPOINTMENT_WRITE_TITLE} onClick={() => onEdit(appointment)}>
                <Pencil size={16} aria-hidden="true" />
                Modifier
              </Button>
              <Button className="rdv-grow" disabled={!permissions.canWrite} title={permissions.canWrite ? undefined : NO_APPOINTMENT_WRITE_TITLE} onClick={() => onMove(appointment)}>
                <Move size={16} aria-hidden="true" />
                Déplacer
              </Button>
            </div>
            <Button variant="danger" disabled={!permissions.canCancel} title={permissions.canCancel ? undefined : NO_APPOINTMENT_CANCEL_TITLE} onClick={() => onCancel(appointment)}>
              <CalendarX size={16} aria-hidden="true" />
              Annuler le rendez-vous
            </Button>
          </>
        )}
        {(!permissions.canWrite || !permissions.canCancel) && (
          <p className="rdv-side__hint" role="note">
            {!permissions.canWrite && !permissions.canCancel
              ? "Consultation seule : votre compte ne permet pas de modifier ni d'annuler les rendez-vous."
              : !permissions.canWrite
                ? "Votre compte ne permet pas de modifier ni de déplacer les rendez-vous."
                : "Votre compte ne permet pas d'annuler ni de rétablir les rendez-vous."}
          </p>
        )}
      </div>
    </aside>
  );
}

function Row({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="rdv-detail__row">
      <dt className="rdv-detail__label">
        <span className="rdv-detail__icon" aria-hidden="true">
          {icon}
        </span>
        {label}
      </dt>
      <dd className="rdv-detail__value">{value}</dd>
    </div>
  );
}
