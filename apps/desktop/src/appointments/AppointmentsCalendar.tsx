// Grille d'agenda (Lot 2 DRAFT, planches 28–34) : FullCalendar Standard 7.1.0 (MIT), vues `timeGridDay` /
// `timeGridWeek`, fuseau du magasin servi par MMV.Api. FullCalendar 7 ne fournit que la mise en page
// (skeleton.css) : le rendu est porté par les classes MMV (Appointments.css) et les jetons du thème.
// Clavier : chaque rendez-vous est un bouton (Tab, Entrée/Espace = détail, fourni par FullCalendar) ; ↑/↓
// parcourent les rendez-vous dans l'ordre chronologique. Le déplacement au clavier passe par la commande
// « Déplacer » du panneau détail ; le glisser-déposer n'en est que l'équivalent souris.
import FullCalendar from "@fullcalendar/react";
import type { CalendarRef, DateSelectInfo, EventClickInfo, EventDropInfo, EventInput } from "@fullcalendar/react";
import interactionPlugin from "@fullcalendar/react/interaction";
import timeGridPlugin from "@fullcalendar/react/timegrid";
import "@fullcalendar/react/skeleton.css";
import { type KeyboardEvent, useEffect, useMemo, useRef } from "react";
import type { AppointmentOptions, AppointmentSummary } from "../api/appointments.ts";
import { cn } from "../lib/utils.ts";
import { appointmentClientName } from "./appointmentForm.ts";
import { reasonLabel, reasonTone, shortClientName } from "./appointmentsView.ts";
import { type CalendarView, type CivilDate, minutesBetween, utcToWall } from "./time.ts";

const FC_VIEW: Record<CalendarView, string> = { day: "timeGridDay", week: "timeGridWeek" };

interface AppointmentsCalendarProps {
  view: CalendarView;
  date: CivilDate;
  options: AppointmentOptions;
  appointments: readonly AppointmentSummary[];
  selectedId: string | null;
  canWrite: boolean;
  now?: () => Date;
  onOpen: (appointment: AppointmentSummary) => void;
  onSelectSlot: (startUtc: string, endUtc: string) => void;
  /** Glisser-déposer : le parent appelle l'API puis `revert()` en cas de refus. */
  onDrop: (appointment: AppointmentSummary, startsAtUtc: string, revert: () => void) => void;
}

export function AppointmentsCalendar({
  view,
  date,
  options,
  appointments,
  selectedId,
  canWrite,
  now,
  onOpen,
  onSelectSlot,
  onDrop,
}: AppointmentsCalendarProps) {
  const calendar = useRef<CalendarRef>(null);
  const body = useRef<HTMLDivElement>(null);

  // Vue et date pilotées par la barre d'outils MMV (headerToolbar désactivée).
  useEffect(() => {
    const api = calendar.current?.getApi();
    if (!api) return;
    if (api.view.type !== FC_VIEW[view]) api.changeView(FC_VIEW[view], date);
    else api.gotoDate(date);
  }, [view, date]);

  const events = useMemo<EventInput[]>(
    () =>
      appointments.map((appointment) => ({
        id: appointment.id,
        title: appointmentClientName(appointment),
        start: appointment.startsAtUtc,
        end: appointment.endsAtUtc,
        // Seul un rendez-vous planifié se déplace, et seulement avec `appointments.write` (le serveur tranche).
        startEditable: canWrite && appointment.status === "planned",
        durationEditable: false,
        extendedProps: { appointment },
      })),
    [appointments, canWrite],
  );

  const moveFocus = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    const ordered = appointments
      .map((appointment) => body.current?.querySelector<HTMLElement>(`[data-appointment-id="${appointment.id}"]`)?.closest<HTMLElement>("[role=button]"))
      .filter((element): element is HTMLElement => element != null);
    if (ordered.length === 0) return;
    event.preventDefault();
    const current = ordered.findIndex((element) => element.contains(document.activeElement));
    const next =
      current < 0 ? (event.key === "ArrowDown" ? 0 : ordered.length - 1) : Math.min(ordered.length - 1, Math.max(0, current + (event.key === "ArrowDown" ? 1 : -1)));
    ordered[next].focus();
    ordered[next].scrollIntoView?.({ block: "nearest" });
  };

  return (
    <div ref={body} className="rdv-cal__grid" onKeyDown={moveFocus}>
      <FullCalendar
        ref={calendar}
        plugins={[timeGridPlugin, interactionPlugin]}
        initialView={FC_VIEW[view]}
        initialDate={date}
        timeZone={options.timeZone}
        locale="fr"
        firstDay={1}
        headerToolbar={false}
        allDaySlot={false}
        height="100%"
        expandRows
        slotMinTime={options.displayStartTime}
        slotMaxTime={options.displayEndTime}
        // Plage et jours affichés : paramètres d'AFFICHAGE non bloquants (D-03-13) ; un rendez-vous hors plage
        // reste accessible par Précédent/Suivant et par la liste clavier.
        hiddenDays={options.hiddenWeekdays}
        // Cotes des planches 28–31 : une heure = 64 px (deux créneaux de 30 min).
        slotMinHeight={32}
        slotDuration="00:30:00"
        snapDuration="00:15:00"
        slotHeaderInterval="01:00:00"
        slotHeaderFormat={{ hour: "2-digit", minute: "2-digit", hour12: false }}
        dayHeaderFormat={view === "day" ? { weekday: "long", day: "numeric", month: "long" } : { weekday: "short", day: "numeric" }}
        eventTimeFormat={{ hour: "2-digit", minute: "2-digit", hour12: false }}
        nowIndicator
        now={now ? () => now() : undefined}
        editable={canWrite}
        selectable={canWrite}
        selectMirror
        events={events}
        className="rdv-fc"
        dayHeaderClass="rdv-fc__dayhead"
        slotHeaderClass="rdv-fc__slothead"
        slotLaneClass="rdv-fc__slot"
        dayLaneClass="rdv-fc__daylane"
        nowIndicatorLineClass="rdv-fc__now"
        highlightClass="rdv-fc__highlight"
        eventClass={(info) => {
          const appointment = info.event.extendedProps.appointment as AppointmentSummary;
          const tone = reasonTone(options, appointment.reasonCode);
          return cn(
            "rdv-ev",
            tone >= 0 ? `rdv-ev--tone-${tone}` : "rdv-ev--tone-none",
            appointment.status === "cancelled" && "rdv-ev--cancelled",
            appointment.id === selectedId && "rdv-ev--selected",
            info.isMirror && "rdv-ev--mirror",
          );
        }}
        eventContent={(info) => {
          const appointment = info.event.extendedProps.appointment as AppointmentSummary | undefined;
          if (!appointment) return <div className="rdv-ev__inner">{info.timeText}</div>;
          const start = utcToWall(appointment.startsAtUtc, options.timeZone).time;
          const minutes = minutesBetween(appointment.startsAtUtc, appointment.endsAtUtc);
          const name = view === "week" ? shortClientName(appointment) : appointmentClientName(appointment);
          // Moins de 45 min (créneau de 64 px/h) : une seule ligne ; responsable à partir d'une heure (planche 28).
          const compact = minutes < 45;
          return (
            <div className={cn("rdv-ev__inner", compact && "rdv-ev__inner--compact")} data-appointment-id={appointment.id}>
              <div className="rdv-ev__title">{name}</div>
              <div className="rdv-ev__sub">
                {compact ? ` · ${start}` : start}
                {view === "day" && !compact && ` · ${reasonLabel(options, appointment.reasonCode)}`}
              </div>
              {view === "day" && minutes >= 60 && appointment.responsibleDisplayName && <div className="rdv-ev__resp">{appointment.responsibleDisplayName}</div>}
              {appointment.status === "cancelled" && <span className="rdv-ev__status">(annulé)</span>}
            </div>
          );
        }}
        eventClick={(info: EventClickInfo) => onOpen(info.event.extendedProps.appointment as AppointmentSummary)}
        select={(info: DateSelectInfo) => {
          calendar.current?.getApi().unselect();
          onSelectSlot(info.start.toISOString(), info.end.toISOString());
        }}
        eventDrop={(info: EventDropInfo) => {
          const appointment = info.event.extendedProps.appointment as AppointmentSummary;
          if (!info.event.start) return info.revert();
          onDrop(appointment, info.event.start.toISOString(), info.revert);
        }}
      />
    </div>
  );
}
