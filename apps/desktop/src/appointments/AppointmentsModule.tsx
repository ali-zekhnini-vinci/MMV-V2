// Module Rendez-vous (sous-lot 03-B, Lot 2 DRAFT planches 28–43) : agenda Jour / Semaine (FullCalendar
// Standard), panneau détail, formulaire, sélecteur de client, annulation / rétablissement, conflits, états.
// Service DÉSACTIVÉ tant que les décisions D-03-13 à D-03-18, le contrat et la migration ne sont pas validés
// par le propriétaire (appointmentsService.ts) : l'écran le dit, sans donnée fictive ni rendez-vous réel.
// Route unique `/rendez-vous` : vue, date et panneau sont un état d'écran (rien n'est persisté).
import { AlertTriangle, CalendarCheck, CalendarDays, CalendarX, CheckCircle2, ChevronLeft, ChevronRight, Info, Plus, RefreshCw, ServerOff, ShieldAlert, X } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useCallback, useId, useMemo, useState } from "react";
import { Temporal } from "temporal-polyfill";
import { type AppointmentDetail, type AppointmentOptions, type AppointmentSummary, getAppointment } from "../api/appointments.ts";
import { ApiRequestError, unwrap } from "../api/queryClient.ts";
import { useAuth } from "../auth/authContext.ts";
import { formatIsoDate } from "../clients/clientForm.ts";
import { LoadingBlock, StateBlock } from "../clients/ClientStates.tsx";
import "../clients/Clients.css";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import {
  appointmentClientName,
  type AppointmentFormValues,
  cancellationSchema,
  EMPTY_APPOINTMENT_FORM,
  toAppointmentFormValues,
  toAppointmentInputFrom,
} from "./appointmentForm.ts";
import { AppointmentDetailPanel } from "./AppointmentDetailPanel.tsx";
import { AppointmentFormPanel, type FormMode } from "./AppointmentFormPanel.tsx";
import { APPOINTMENTS_READ_ONLY_HINT, useAppointmentPermissions } from "./appointmentPermissions.ts";
import { appointmentKeys, useAppointmentList, useAppointmentOptions, useCancelAppointment, useRestoreAppointment, useUpdateAppointment } from "./appointmentQueries.ts";
import { AppointmentsCalendar } from "./AppointmentsCalendar.tsx";
import { useAppointmentsService } from "./appointmentsService.ts";
import { describeAppointmentFailure, type FailureView, formatWhen, reasonLabel } from "./appointmentsView.ts";
import {
  type CalendarView,
  type CivilDate,
  formatLongDate,
  formatShortDay,
  formatWeekShort,
  formatWeekTitle,
  minutesBetween,
  shiftDate,
  todayIn,
  utcToWall,
  viewRangeUtc,
  visibleDays,
} from "./time.ts";
import "./Appointments.css";

export function AppointmentsModule() {
  const { enabled } = useAppointmentsService();
  const options = useAppointmentOptions();

  if (!enabled) {
    return (
      <div className="clients-view rdv-view">
        <PageHead subtitle="Agenda du magasin" />
        <StateBlock tone="info" icon={ServerOff} title="Service Rendez-vous non activé">
          <p>
            L&apos;agenda n&apos;est pas encore raccordé au serveur MMV : les règles de rendez-vous (fuseau horaire, durées, responsables,
            conflits, annulation, droits) attendent la validation du propriétaire.
          </p>
          <p>Aucun rendez-vous n&apos;est créé ni affiché tant que ce service n&apos;est pas activé.</p>
        </StateBlock>
      </div>
    );
  }
  if (options.isPending) {
    return (
      <div className="clients-view rdv-view">
        <PageHead subtitle="Agenda du magasin" />
        <LoadingBlock label="Chargement de l'agenda…" />
      </div>
    );
  }
  if (options.isError) {
    const forbidden = options.error.error.kind === "forbidden";
    return (
      <div className="clients-view rdv-view">
        <PageHead subtitle="Agenda du magasin" />
        {forbidden ? (
          <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
            <p>Le serveur MMV a refusé l&apos;accès à l&apos;agenda pour votre compte (droit appointments.read).</p>
          </StateBlock>
        ) : (
          <StateBlock
            tone="error"
            icon={AlertTriangle}
            title="Impossible de charger l'agenda"
            role="alert"
            action={
              <Button variant="primary" onClick={() => void options.refetch()} disabled={options.isFetching}>
                <RefreshCw size={16} aria-hidden="true" />
                Réessayer
              </Button>
            }
          >
            <p>{options.error.error.message}</p>
          </StateBlock>
        )}
      </div>
    );
  }
  return <Agenda options={options.data} />;
}

function PageHead({ subtitle, actions, hint }: { subtitle: string; actions?: ReactNode; hint?: string }) {
  return (
    <header className="clients-head">
      <div>
        <h1 className="clients-head__title">Rendez-vous</h1>
        <p className="clients-head__sub">{subtitle}</p>
        {hint && (
          <p className="clients-head__hint" role="note">
            {hint}
          </p>
        )}
      </div>
      {actions && <div className="clients-actions">{actions}</div>}
    </header>
  );
}

type Panel =
  | { kind: "none" }
  | { kind: "detail"; id: string }
  | { kind: "form"; mode: FormMode; values: AppointmentFormValues; appointment?: AppointmentDetail; key: number };

interface Notice {
  title: string;
  message: string;
}

function Agenda({ options }: { options: AppointmentOptions }) {
  const { now } = useAppointmentsService();
  const permissions = useAppointmentPermissions();
  const { authorized } = useAuth();
  const queryClient = useQueryClient();
  const update = useUpdateAppointment();
  const cancel = useCancelAppointment();
  const restore = useRestoreAppointment();
  const today = useCallback(() => todayIn(options.timeZone, now ? Temporal.Instant.fromEpochMilliseconds(now().getTime()) : undefined), [options.timeZone, now]);

  const [view, setView] = useState<CalendarView>("day");
  const [date, setDate] = useState<CivilDate>(today);
  const [showCancelled, setShowCancelled] = useState(false);
  const [panel, setPanel] = useState<Panel>({ kind: "none" });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [gridFailure, setGridFailure] = useState<FailureView | null>(null);
  const [detailFailure, setDetailFailure] = useState<FailureView | null>(null);
  const [cancelTarget, setCancelTarget] = useState<AppointmentDetail | null>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelError, setCancelError] = useState<string | null>(null);
  const [formKey, setFormKey] = useState(0);
  const reasonId = useId();

  const range = useMemo(() => ({ ...viewRangeUtc(date, view, options.timeZone), status: showCancelled ? ("all" as const) : ("planned" as const) }), [date, view, options.timeZone, showCancelled]);
  const list = useAppointmentList(range);
  const appointments = useMemo(() => list.data ?? [], [list.data]);
  const days = visibleDays(date, view, options.hiddenWeekdays);
  const visibleCount = appointments.filter((appointment) => days.includes(utcToWall(appointment.startsAtUtc, options.timeZone).date)).length;

  const clearMessages = () => {
    setNotice(null);
    setGridFailure(null);
    setDetailFailure(null);
  };

  const openForm = (mode: FormMode, values: AppointmentFormValues, appointment?: AppointmentDetail) => {
    clearMessages();
    const key = formKey + 1;
    setFormKey(key);
    setPanel({ kind: "form", mode, values, appointment, key });
  };

  const openCreate = (preset?: Partial<AppointmentFormValues>) =>
    openForm("create", { ...EMPTY_APPOINTMENT_FORM, date: formatIsoDate(date), ...preset });

  const openDetail = (appointment: Pick<AppointmentSummary, "id">) => {
    clearMessages();
    setSelectedId(appointment.id);
    setPanel({ kind: "detail", id: appointment.id });
  };

  const navigateTo = (next: CivilDate, nextView: CalendarView = view) => {
    setDate(next);
    setView(nextView);
  };

  const onSaved = (saved: AppointmentDetail, mode: FormMode) => {
    const name = appointmentClientName(saved);
    const when = formatWhen(saved.startsAtUtc, options.timeZone);
    setNotice(
      mode === "create"
        ? { title: "Rendez-vous créé", message: `${name} · ${when} · ${reasonLabel(options, saved.reasonCode)}.` }
        : mode === "move"
          ? { title: "Rendez-vous déplacé", message: `${name} · ${when}.` }
          : { title: "Rendez-vous modifié", message: `${name} · ${when}.` },
    );
    setSelectedId(saved.id);
    setDate(utcToWall(saved.startsAtUtc, options.timeZone).date);
    setPanel(mode === "create" ? { kind: "none" } : { kind: "detail", id: saved.id });
  };

  const onDrop = (summary: AppointmentSummary, startsAtUtc: string, revert: () => void) => {
    clearMessages();
    void (async () => {
      let detail: AppointmentDetail;
      try {
        // Le corps PUT est complet (notes comprises) : le détail est relu avant d'écrire.
        detail = await queryClient.fetchQuery({
          queryKey: appointmentKeys.detail(summary.id),
          queryFn: async ({ signal }) => unwrap(await authorized((url, token) => getAppointment(url, token, summary.id, signal))),
          staleTime: 0,
        });
      } catch (error) {
        revert();
        if (error instanceof ApiRequestError) setGridFailure(describeAppointmentFailure(error));
        return;
      }
      const input = toAppointmentInputFrom(detail, startsAtUtc);
      update.mutate(
        { id: detail.id, input, version: detail.version },
        {
          onSuccess: (saved) => {
            setSelectedId(saved.id);
            setNotice({ title: "Rendez-vous déplacé", message: `${appointmentClientName(saved)} · ${formatWhen(saved.startsAtUtc, options.timeZone)}.` });
          },
          onError: (error) => {
            revert();
            const end = Temporal.Instant.from(startsAtUtc).add({ minutes: minutesBetween(detail.startsAtUtc, detail.endsAtUtc) }).toString();
            setGridFailure(describeAppointmentFailure(error, { startsAtUtc, endsAtUtc: end, timeZone: options.timeZone }));
          },
        },
      );
    })();
  };

  const onSelectSlot = (startUtc: string, endUtc: string) => {
    if (!permissions.canWrite) return;
    const wall = utcToWall(startUtc, options.timeZone);
    const minutes = minutesBetween(startUtc, endUtc);
    openCreate({
      date: formatIsoDate(wall.date),
      time: wall.time,
      durationMinutes: options.durationsMinutes.includes(minutes) ? String(minutes) : "",
    });
  };

  const confirmCancel = () => {
    if (cancelTarget === null) return;
    const parsed = cancellationSchema.safeParse({ reason: cancelReason });
    if (!parsed.success) {
      setCancelError(parsed.error.issues[0]?.message ?? "Motif invalide");
      return;
    }
    const reason = cancelReason.trim() === "" ? null : cancelReason.trim();
    cancel.mutate(
      { id: cancelTarget.id, version: cancelTarget.version, reason },
      {
        onSuccess: (saved) => {
          setCancelTarget(null);
          setNotice({ title: "Rendez-vous annulé", message: `${appointmentClientName(saved)} · ${formatWhen(saved.startsAtUtc, options.timeZone)}. Le créneau est libéré.` });
        },
        onError: (error) => {
          setCancelTarget(null);
          setDetailFailure(describeAppointmentFailure(error));
        },
      },
    );
  };

  const onRestore = (appointment: AppointmentDetail) => {
    clearMessages();
    restore.mutate(
      { id: appointment.id, version: appointment.version },
      {
        onSuccess: (saved) => setNotice({ title: "Rendez-vous rétabli", message: `${appointmentClientName(saved)} · ${formatWhen(saved.startsAtUtc, options.timeZone)}.` }),
        onError: (error) => setDetailFailure(describeAppointmentFailure(error, { startsAtUtc: appointment.startsAtUtc, endsAtUtc: appointment.endsAtUtc, timeZone: options.timeZone })),
      },
    );
  };

  const subtitle = view === "day" ? formatLongDate(date) : formatWeekTitle(days);
  const dateLabel = view === "day" ? formatShortDay(date) : formatWeekShort(days);
  const unit = view === "day" ? "Jour" : "Semaine";
  const listForbidden = list.isError && list.error.error.kind === "forbidden";

  return (
    <div className="clients-view rdv-view">
      <PageHead
        subtitle={subtitle}
        hint={permissions.canWrite ? undefined : APPOINTMENTS_READ_ONLY_HINT}
        actions={
          <>
            <Button onClick={() => navigateTo(today())}>
              <CalendarDays size={16} aria-hidden="true" />
              Aujourd&apos;hui
            </Button>
            {permissions.canWrite && (
              <Button variant="primary" onClick={() => openCreate()}>
                <Plus size={16} aria-hidden="true" />
                Nouveau rendez-vous
              </Button>
            )}
          </>
        }
      />
      {notice && (
        <div className="clients-notice" role="status">
          <span className="clients-notice__icon" aria-hidden="true">
            {notice.title === "Rendez-vous annulé" ? <CalendarX size={20} /> : notice.title === "Rendez-vous créé" ? <CalendarCheck size={20} /> : <CheckCircle2 size={20} />}
          </span>
          <div>
            <div className="clients-notice__title">{notice.title}</div>
            <div className="clients-notice__text">{notice.message}</div>
          </div>
          <button type="button" className="clients-notice__close" aria-label="Fermer le message" onClick={() => setNotice(null)}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
      )}
      {gridFailure && (
        <div className={cn("clients-banner", `clients-banner--${gridFailure.tone}`)} role="alert">
          <span className="clients-banner__icon" aria-hidden="true">
            <AlertTriangle size={18} />
          </span>
          <div>
            <div className="clients-banner__title">{gridFailure.slotConflict ? "Créneau indisponible" : gridFailure.title}</div>
            <div className="clients-banner__text">{gridFailure.text} Le rendez-vous a été remis à sa place.</div>
          </div>
          <button type="button" className="clients-notice__close" aria-label="Fermer le message" onClick={() => setGridFailure(null)}>
            <X size={16} aria-hidden="true" />
          </button>
        </div>
      )}
      <div className="rdv-main">
        <section className="rdv-cal" aria-label="Agenda">
          <div className="rdv-cal__top">
            <div className="rdv-cal__nav">
              <button type="button" className="rdv-iconbtn" aria-label={view === "day" ? "Jour précédent" : "Semaine précédente"} onClick={() => navigateTo(shiftDate(date, view, -1))}>
                <ChevronLeft size={18} aria-hidden="true" />
              </button>
              <button type="button" className="rdv-iconbtn" aria-label={view === "day" ? "Jour suivant" : "Semaine suivante"} onClick={() => navigateTo(shiftDate(date, view, 1))}>
                <ChevronRight size={18} aria-hidden="true" />
              </button>
            </div>
            <span className="rdv-cal__date" aria-live="polite">
              {dateLabel}
            </span>
            <span className="rdv-grow" />
            <button
              type="button"
              className={cn("clients-chip", showCancelled && "clients-chip--on")}
              aria-pressed={showCancelled}
              onClick={() => setShowCancelled((value) => !value)}
            >
              Afficher les annulés
            </button>
            <div className="rdv-seg" role="group" aria-label="Vue de l'agenda">
              {(["day", "week"] as const).map((option) => (
                <button key={option} type="button" aria-pressed={view === option} className={cn(view === option && "rdv-seg--on")} onClick={() => setView(option)}>
                  {option === "day" ? "Jour" : "Semaine"}
                </button>
              ))}
            </div>
            <span className="rdv-cal__hint" id="rdv-keyboard-hint">
              <Info size={14} aria-hidden="true" />
              Sélection clavier ↑↓ · ⏎ pour ouvrir
            </span>
          </div>
          <div className="rdv-cal__body" aria-busy={list.isFetching} aria-describedby="rdv-keyboard-hint">
            {listForbidden ? (
              <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
                <p>Le serveur MMV a refusé la lecture des rendez-vous pour votre compte.</p>
              </StateBlock>
            ) : list.isError && list.data === undefined ? (
              <StateBlock
                tone="error"
                icon={AlertTriangle}
                title="Impossible de charger les rendez-vous"
                role="alert"
                action={
                  <Button variant="primary" onClick={() => void list.refetch()} disabled={list.isFetching}>
                    <RefreshCw size={16} aria-hidden="true" />
                    Réessayer
                  </Button>
                }
              >
                <p>{list.error.error.message}</p>
              </StateBlock>
            ) : (
              <>
                <AppointmentsCalendar
                  view={view}
                  date={date}
                  options={options}
                  appointments={appointments}
                  selectedId={selectedId}
                  canWrite={permissions.canWrite}
                  now={now}
                  onOpen={openDetail}
                  onSelectSlot={onSelectSlot}
                  onDrop={onDrop}
                />
                {list.isPending && (
                  <p className="rdv-cal__loading" role="status">
                    Chargement des rendez-vous…
                  </p>
                )}
                {list.isSuccess && !list.isPlaceholderData && visibleCount === 0 && (
                  <div className="rdv-empty">
                    <div className="rdv-empty__card" role="status">
                      <span className="rdv-empty__icon" aria-hidden="true">
                        <CalendarCheck size={32} />
                      </span>
                      <h2 className="rdv-empty__title">{view === "day" ? "Aucun rendez-vous ce jour" : "Aucun rendez-vous cette semaine"}</h2>
                      <p className="rdv-empty__text">
                        {view === "day" ? "La journée est libre." : "La semaine est libre."} {permissions.canWrite ? "Planifiez un rendez-vous ou changez de date." : "Changez de date pour consulter l'agenda."}
                      </p>
                      {permissions.canWrite && (
                        <Button variant="primary" onClick={() => openCreate()}>
                          <Plus size={16} aria-hidden="true" />
                          Nouveau rendez-vous
                        </Button>
                      )}
                    </div>
                  </div>
                )}
              </>
            )}
            <p className="rdv-sr-only" aria-live="polite">
              {list.isSuccess ? `${unit} : ${visibleCount} rendez-vous affiché${visibleCount > 1 ? "s" : ""}.` : ""}
            </p>
          </div>
        </section>
        {panel.kind === "detail" && (
          <AppointmentDetailPanel
            key={panel.id}
            appointmentId={panel.id}
            options={options}
            permissions={permissions}
            failure={detailFailure}
            restoring={restore.isPending}
            onClose={() => setPanel({ kind: "none" })}
            onEdit={(appointment) => openForm("edit", toAppointmentFormValues(appointment, options.timeZone), appointment)}
            onMove={(appointment) => openForm("move", toAppointmentFormValues(appointment, options.timeZone), appointment)}
            onCancel={(appointment) => {
              setCancelReason("");
              setCancelError(null);
              setCancelTarget(appointment);
            }}
            onRestore={onRestore}
          />
        )}
        {panel.kind === "form" && (
          <AppointmentFormPanel
            key={panel.key}
            mode={panel.mode}
            options={options}
            initialValues={panel.values}
            appointment={panel.appointment}
            canPickClient={permissions.canPickClient}
            onClose={() => setPanel(panel.appointment ? { kind: "detail", id: panel.appointment.id } : { kind: "none" })}
            onSaved={onSaved}
          />
        )}
      </div>
      <ConfirmDialog
        open={cancelTarget !== null}
        onOpenChange={(open) => !open && !cancel.isPending && setCancelTarget(null)}
        tone="error"
        icon={<CalendarX size={28} />}
        title="Annuler ce rendez-vous ?"
        description={
          cancelTarget ? (
            <>
              Le rendez-vous de <b>{appointmentClientName(cancelTarget)}</b> du {formatWhen(cancelTarget.startsAtUtc, options.timeZone)} sera annulé. Il
              pourra être rétabli depuis le détail si le créneau est toujours libre.
            </>
          ) : null
        }
        cancelLabel="Conserver"
        confirmLabel={cancel.isPending ? "Annulation…" : "Annuler le rendez-vous"}
        confirmIcon={<CalendarX size={16} aria-hidden="true" />}
        pending={cancel.isPending}
        onConfirm={confirmCancel}
      >
        <div className="clients-field rdv-cancel__field">
          <label htmlFor={reasonId} className="clients-field__label">
            Motif d&apos;annulation (facultatif)
          </label>
          <div className={cn("clients-input clients-input--area", cancelError && "clients-input--error")}>
            <textarea
              id={reasonId}
              rows={2}
              maxLength={200}
              value={cancelReason}
              aria-invalid={cancelError ? true : undefined}
              aria-describedby={cancelError ? `${reasonId}-error` : undefined}
              onChange={(event) => {
                setCancelReason(event.target.value);
                setCancelError(null);
              }}
            />
          </div>
          {cancelError && (
            <div className="clients-field__error" id={`${reasonId}-error`}>
              {cancelError}
            </div>
          )}
        </div>
      </ConfirmDialog>
    </div>
  );
}
