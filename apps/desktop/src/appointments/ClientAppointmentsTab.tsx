// Onglet « Rendez-vous » de la fiche client (03-D1) : historique servi par MMV.Api (GET
// /api/appointments/by-client/{id}, droit appointments.read), jamais reconstitué par l'écran. À venir / passés /
// annulés / tous, tri par date, pagination, états vide, chargement, erreur, accès refusé, agenda non configuré.
// Heures : heure murale du magasin calculée par le SERVEUR ; si ce poste en calcule une autre (données de fuseaux
// divergentes, O-03-22), un avertissement l'indique. « Voir le détail » ouvre l'agenda sur ce rendez-vous, avec
// retour à la fiche. Lignes minimisées : ni notes ni motif d'annulation (détail du rendez-vous).
import { AlertTriangle, ArrowDownWideNarrow, ArrowUpNarrowWide, CalendarClock, CalendarPlus, ChevronLeft, ChevronRight, Clock, RefreshCw, Settings, ShieldAlert } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { type ClientAppointmentItem, type ClientAppointmentScope, CLIENT_APPOINTMENTS_PAGE_SIZE } from "../api/appointments.ts";
import { LoadingBlock, StateBlock } from "../clients/ClientStates.tsx";
import { Button } from "../components/ui/button.tsx";
import { cn } from "../lib/utils.ts";
import { appointmentDetailLaunchFor } from "./appointmentLaunch.ts";
import { useAppointmentOptions, useClientAppointments } from "./appointmentQueries.ts";
import { useAppointmentsService } from "./appointmentsService.ts";
import { isStoreNotConfigured, reasonLabel } from "./appointmentsView.ts";
import { formatNumericDate, formatShortDay, utcToWall } from "./time.ts";

const SCOPES: { value: ClientAppointmentScope; label: string; empty: string }[] = [
  { value: "upcoming", label: "À venir", empty: "Aucun rendez-vous à venir pour ce client." },
  { value: "past", label: "Passés", empty: "Aucun rendez-vous passé pour ce client." },
  { value: "cancelled", label: "Annulés", empty: "Aucun rendez-vous annulé pour ce client." },
  { value: "all", label: "Tous", empty: "Aucun rendez-vous enregistré pour ce client." },
];

/** Ordre par défaut du serveur : à venir du plus proche au plus lointain, sinon du plus récent au plus ancien. */
const defaultDirection = (scope: ClientAppointmentScope) => (scope === "upcoming" ? "asc" : "desc");

interface ClientAppointmentsTabProps {
  client: { id: string; reference: string };
  clientName: string;
  /** Proposer « Prendre un rendez-vous » (appointments.write, client actif) ; `null` = action absente. */
  onBook: (() => void) | null;
}

export function ClientAppointmentsTab({ client, clientName, onBook }: ClientAppointmentsTabProps) {
  const navigate = useNavigate();
  const [scope, setScope] = useState<ClientAppointmentScope>("upcoming");
  const [direction, setDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const options = useAppointmentOptions();
  const { now } = useAppointmentsService();
  const nowMs = (now ? now() : new Date()).getTime();
  const query = useClientAppointments(client.id, { scope, direction, page, pageSize: CLIENT_APPOINTMENTS_PAGE_SIZE }, true);

  const failure = options.error ?? query.error;
  if (failure && (query.data === undefined || options.data === undefined)) {
    if (isStoreNotConfigured(failure)) {
      return (
        <StateBlock tone="info" icon={Settings} title="Agenda non configuré">
          <p>Les paramètres du magasin ne sont pas encore configurés sur le serveur MMV : l&apos;historique des rendez-vous n&apos;est pas disponible.</p>
        </StateBlock>
      );
    }
    if (failure.error.kind === "forbidden") {
      return (
        <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
          <p>Le serveur MMV a refusé la lecture des rendez-vous pour votre compte (droit appointments.read).</p>
        </StateBlock>
      );
    }
    return (
      <StateBlock
        tone="error"
        icon={AlertTriangle}
        title="Impossible de charger les rendez-vous du client"
        role="alert"
        action={
          <Button
            variant="primary"
            onClick={() => {
              void options.refetch();
              void query.refetch();
            }}
            disabled={query.isFetching || options.isFetching}
          >
            <RefreshCw size={16} aria-hidden="true" />
            Réessayer
          </Button>
        }
      >
        <p>{failure.error.message}</p>
      </StateBlock>
    );
  }
  if (query.data === undefined || options.data === undefined) return <LoadingBlock label="Chargement des rendez-vous du client…" />;

  const { items, totalCount, counts } = query.data;
  const timeZone = options.data.timeZone;
  const countOf = (value: ClientAppointmentScope) => (value === "all" ? counts.upcoming + counts.past + counts.cancelled : counts[value]);
  const pages = Math.max(1, Math.ceil(totalCount / CLIENT_APPOINTMENTS_PAGE_SIZE));
  const first = totalCount === 0 ? 0 : (page - 1) * CLIENT_APPOINTMENTS_PAGE_SIZE + 1;
  const last = Math.min(page * CLIENT_APPOINTMENTS_PAGE_SIZE, totalCount);
  const empty = SCOPES.find((candidate) => candidate.value === scope)?.empty ?? "";
  // Garde de cohérence des fuseaux : l'heure du serveur fait foi, un écart avec ce poste est signalé.
  const diverging = items.some((item) => {
    const wall = utcToWall(item.startsAtUtc, timeZone);
    return `${wall.date}T${wall.time}` !== item.startsAtLocal;
  });

  return (
    <section className="clients-card client-rdv" aria-labelledby={`client-rdv-${client.id}`} aria-busy={query.isFetching}>
      <div className="client-rdv__head">
        <h2 className="clients-card__title" id={`client-rdv-${client.id}`}>
          Rendez-vous du client
        </h2>
        {onBook && (
          <Button size="sm" variant="primary" onClick={onBook}>
            <CalendarPlus size={16} aria-hidden="true" />
            Prendre un rendez-vous
          </Button>
        )}
      </div>
      <div className="client-rdv__tools">
        <div className="client-rdv__scopes" role="group" aria-label="Catégorie de rendez-vous">
          {SCOPES.map((candidate) => (
            <button
              key={candidate.value}
              type="button"
              className={cn("clients-chip", scope === candidate.value && "clients-chip--on")}
              aria-pressed={scope === candidate.value}
              onClick={() => {
                setScope(candidate.value);
                setDirection(defaultDirection(candidate.value));
                setPage(1);
              }}
            >
              {candidate.label} <span className="tnum">({countOf(candidate.value)})</span>
            </button>
          ))}
        </div>
        <Button
          size="sm"
          variant="tertiary"
          onClick={() => {
            setDirection((value) => (value === "asc" ? "desc" : "asc"));
            setPage(1);
          }}
        >
          {direction === "asc" ? <ArrowUpNarrowWide size={16} aria-hidden="true" /> : <ArrowDownWideNarrow size={16} aria-hidden="true" />}
          {direction === "asc" ? "Plus anciens d'abord" : "Plus récents d'abord"}
        </Button>
      </div>
      {diverging && (
        <div className="clients-banner clients-banner--warning" role="alert">
          <span className="clients-banner__icon" aria-hidden="true">
            <Clock size={18} />
          </span>
          <div>
            <div className="clients-banner__title">Heure incohérente entre ce poste et le serveur MMV</div>
            <div className="clients-banner__text">
              Les heures affichées sont celles du serveur ({timeZone}). Mettez à jour Windows et Microsoft Edge WebView2 sur ce poste et sur le serveur.
            </div>
          </div>
        </div>
      )}
      {items.length === 0 ? (
        <div className="client-rdv__empty" role="status">
          <CalendarClock size={24} aria-hidden="true" />
          <p>{empty}</p>
        </div>
      ) : (
        <div className="client-rdv__scroll">
          <table className="client-rdv__table">
            <caption className="visually-hidden">
              Rendez-vous du client, {SCOPES.find((candidate) => candidate.value === scope)?.label.toLocaleLowerCase("fr")}, page {page} sur {pages}
            </caption>
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col">Heure</th>
                <th scope="col">Motif</th>
                <th scope="col">Responsable</th>
                <th scope="col">Statut</th>
                <th scope="col">
                  <span className="visually-hidden">Action</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <Row
                  key={item.id}
                  item={item}
                  reason={reasonLabel(options.data, item.reasonCode)}
                  past={scope === "past" || (scope === "all" && Date.parse(item.endsAtUtc) <= nowMs)}
                  onOpen={() =>
                    navigate("/rendez-vous", { state: appointmentDetailLaunchFor(client, clientName, { id: item.id, date: item.startsAtLocal.slice(0, 10) }) })
                  }
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
      {totalCount > 0 && (
        <nav className="client-rdv__pager" aria-label="Pagination des rendez-vous">
          <span className="tnum" aria-live="polite">
            {first}–{last} sur {totalCount}
          </span>
          <span className="rdv-grow" />
          <Button size="sm" onClick={() => setPage((value) => value - 1)} disabled={page <= 1 || query.isFetching}>
            <ChevronLeft size={16} aria-hidden="true" />
            Précédents
          </Button>
          <Button size="sm" onClick={() => setPage((value) => value + 1)} disabled={page >= pages || query.isFetching}>
            Suivants
            <ChevronRight size={16} aria-hidden="true" />
          </Button>
        </nav>
      )}
    </section>
  );
}

function Row({ item, reason, past, onOpen }: { item: ClientAppointmentItem; reason: string; past: boolean; onOpen: () => void }) {
  const date = item.startsAtLocal.slice(0, 10);
  const status = item.status === "cancelled" ? "cancelled" : past ? "past" : "planned";
  return (
    <tr className={cn(status === "cancelled" && "client-rdv__row--cancelled")}>
      <td className="tnum">
        <span className="client-rdv__day">{formatShortDay(date)}</span> {formatNumericDate(date)}
      </td>
      <td className="tnum">
        {item.startsAtLocal.slice(11)} – {item.endsAtLocal.slice(11)}
      </td>
      <td>{reason}</td>
      <td>{item.responsibleDisplayName ?? <span className="clients-muted">Non attribué</span>}</td>
      <td>
        <span className={cn("clients-badge", status === "cancelled" ? "clients-badge--archived" : status === "past" ? "clients-badge--neutral" : "clients-badge--info")}>
          <span className="clients-badge__dot" aria-hidden="true" />
          {status === "cancelled" ? "Annulé" : status === "past" ? "Passé" : "Planifié"}
        </span>
      </td>
      <td className="client-rdv__action">
        <Button size="sm" variant="tertiary" onClick={onOpen} aria-label={`Voir le détail du rendez-vous du ${formatNumericDate(date)} à ${item.startsAtLocal.slice(11)}`}>
          Voir le détail
        </Button>
      </td>
    </tr>
  );
}
