// Onglet « Historique » de la fiche client (03-D2) : chronologie des actions RÉUSSIES sur le dossier, servie par
// MMV.Api (GET /api/audit/clients/{id}, droit dédié audit.read ; onglet absent sans ce droit). Jamais reconstituée
// par l'écran. Heure du poste, auteur (compte qui a exécuté l'action), action, champs concernés (valeurs seulement si
// le serveur les a conservées), accès au rendez-vous si le compte peut le lire. Filtres type d'action et période,
// chargement progressif. Les actions antérieures à la mise en service du journal ne sont pas reconstituées.
import { keepPreviousData, useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, CalendarDays, FileClock, History, Loader2, RefreshCw, ShieldAlert, UserRound } from "lucide-react";
import { useId, useState } from "react";
import { useNavigate } from "react-router";
import { getAppointment, getAppointmentOptions } from "../api/appointments.ts";
import { AUDIT_PAGE_SIZE, type AuditEvent, type AuditModule, listClientAuditEvents } from "../api/audit.ts";
import { unwrap } from "../api/queryClient.ts";
import { appointmentDetailLaunchFor } from "../appointments/appointmentLaunch.ts";
import { appointmentKeys } from "../appointments/appointmentQueries.ts";
import { utcToWall } from "../appointments/time.ts";
import { useAuth } from "../auth/authContext.ts";
import { LoadingBlock, StateBlock } from "../clients/ClientStates.tsx";
import { Button } from "../components/ui/button.tsx";
import { AUDIT_MODULES, AUDIT_PERIODS, type AuditPeriod, auditActionLabel, auditKeys, describeAuditChange, formatAuditInstant, periodStart } from "./auditView.ts";

interface ClientHistoryTabProps {
  client: { id: string };
  clientName: string;
  /** Lien « Voir le rendez-vous » (appointments.read). */
  canOpenAppointments: boolean;
}

export function ClientHistoryTab({ client, clientName, canOpenAppointments }: ClientHistoryTabProps) {
  const { authorized } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [module, setModule] = useState<AuditModule | "all">("all");
  const [period, setPeriod] = useState<AuditPeriod>("all");
  const [openError, setOpenError] = useState<string | null>(null);
  const moduleId = useId();
  const periodId = useId();
  const query = useInfiniteQuery({
    queryKey: auditKeys.client(client.id, { module, period }),
    initialPageParam: 1,
    queryFn: async ({ pageParam, signal }) =>
      unwrap(
        await authorized((url, token) =>
          listClientAuditEvents(
            url,
            token,
            client.id,
            { module: module === "all" ? undefined : module, fromUtc: periodStart(period, new Date()), page: pageParam, pageSize: AUDIT_PAGE_SIZE },
            signal,
          ),
        ),
      ),
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
    // Changement de filtre : la liste et les filtres restent affichés pendant le chargement.
    placeholderData: keepPreviousData,
  });

  const openAppointment = async (event: AuditEvent) => {
    setOpenError(null);
    try {
      const [detail, options] = await Promise.all([
        queryClient.fetchQuery({
          queryKey: appointmentKeys.detail(event.entityId),
          queryFn: async ({ signal }) => unwrap(await authorized((url, token) => getAppointment(url, token, event.entityId, signal))),
          staleTime: 0,
        }),
        queryClient.fetchQuery({
          queryKey: appointmentKeys.options(),
          queryFn: async ({ signal }) => unwrap(await authorized((url, token) => getAppointmentOptions(url, token, signal))),
        }),
      ]);
      navigate("/rendez-vous", {
        state: appointmentDetailLaunchFor(client, clientName, { id: detail.id, date: utcToWall(detail.startsAtUtc, options.timeZone).date }),
      });
    } catch {
      setOpenError("Ce rendez-vous ne peut pas être ouvert (droits, agenda non configuré ou serveur indisponible).");
    }
  };

  const filters = (
    <div className="client-rdv__tools">
      <div className="client-history__filters">
        <label className="clients-field__label" htmlFor={moduleId}>
          Type d&apos;action
        </label>
        <div className="clients-input rdv-select client-history__select">
          <select id={moduleId} value={module} onChange={(event) => setModule(event.target.value as AuditModule | "all")}>
            {AUDIT_MODULES.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
        <label className="clients-field__label" htmlFor={periodId}>
          Période
        </label>
        <div className="clients-input rdv-select client-history__select">
          <select id={periodId} value={period} onChange={(event) => setPeriod(event.target.value as AuditPeriod)}>
            {AUDIT_PERIODS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  );

  if (query.isError && query.data === undefined) {
    return query.error.error.kind === "forbidden" ? (
      <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
        <p>Le serveur MMV a refusé la lecture de l&apos;historique pour votre compte (droit audit.read).</p>
      </StateBlock>
    ) : (
      <StateBlock
        tone="error"
        icon={AlertTriangle}
        title="Impossible de charger l'historique"
        role="alert"
        action={
          <Button variant="primary" onClick={() => void query.refetch()} disabled={query.isFetching}>
            <RefreshCw size={16} aria-hidden="true" />
            Réessayer
          </Button>
        }
      >
        <p>{query.error.error.message}</p>
      </StateBlock>
    );
  }
  if (query.data === undefined) return <LoadingBlock label="Chargement de l'historique…" />;

  const events = query.data.pages.flatMap((page) => page.items);
  const total = query.data.pages[0]?.totalCount ?? 0;

  return (
    <section className="clients-card client-rdv" aria-labelledby={`client-history-${client.id}`} aria-busy={query.isFetching}>
      <div className="client-rdv__head">
        <h2 className="clients-card__title" id={`client-history-${client.id}`}>
          Historique des actions
        </h2>
        <span className="clients-muted tnum" aria-live="polite">
          {events.length} sur {total}
        </span>
      </div>
      {filters}
      {openError && (
        <div className="clients-banner clients-banner--error" role="alert">
          <span className="clients-banner__icon" aria-hidden="true">
            <AlertTriangle size={18} />
          </span>
          <div className="clients-banner__text">{openError}</div>
        </div>
      )}
      {events.length === 0 ? (
        <div className="client-rdv__empty" role="status">
          <History size={24} aria-hidden="true" />
          <p>Aucune action enregistrée pour ce dossier{module !== "all" || period !== "all" ? " avec ces filtres" : ""}.</p>
        </div>
      ) : (
        <ol className="client-history__list">
          {events.map((event) => (
            <li key={event.id} className="client-history__item">
              <span className="client-history__dot" aria-hidden="true">
                {event.module === "appointments" ? <CalendarDays size={14} /> : <FileClock size={14} />}
              </span>
              <div className="client-history__body">
                <div className="client-history__line">
                  <span className="client-history__action">{auditActionLabel(event.action)}</span>
                  <time className="client-history__time tnum" dateTime={event.occurredAtUtc}>
                    {formatAuditInstant(event.occurredAtUtc)}
                  </time>
                </div>
                <div className="client-history__actor">
                  <UserRound size={13} aria-hidden="true" />
                  <span>{event.actorDisplayName}</span>
                </div>
                {event.changes.length > 0 && (
                  <ul className="client-history__changes" aria-label="Champs concernés">
                    {event.changes.map((change) => (
                      <li key={change.field} className={change.masked ? "client-history__change client-history__change--masked" : "client-history__change"}>
                        {describeAuditChange(change)}
                      </li>
                    ))}
                  </ul>
                )}
                {event.entityType === "appointment" && canOpenAppointments && (
                  <Button size="sm" variant="tertiary" className="client-history__open" onClick={() => void openAppointment(event)}>
                    Voir le rendez-vous
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ol>
      )}
      {query.hasNextPage && (
        <div>
          <Button size="sm" onClick={() => void query.fetchNextPage()} disabled={query.isFetchingNextPage}>
            {query.isFetchingNextPage ? <Loader2 size={16} className="clients-spin" aria-hidden="true" /> : null}
            {query.isFetchingNextPage ? "Chargement…" : "Afficher les actions plus anciennes"}
          </Button>
        </div>
      )}
      <p className="clients-head__hint" role="note">
        Heures du poste. Valeurs personnelles non conservées (champ cité seulement). Les actions antérieures à la mise en service du journal
        d&apos;audit ne sont pas reconstituées.
      </p>
    </section>
  );
}
