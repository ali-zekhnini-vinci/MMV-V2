// Fiche client (Lot 2 DRAFT, planches 16–21) : en-tête permanent, onglets Informations / Ordonnances /
// Ventes & commandes. Les deux derniers onglets appartiennent à des phases ultérieures : ils l'indiquent
// explicitement, sans compteur ni ligne simulés. Un client archivé est en lecture seule.
// 03-B2 : « Prendre un rendez-vous » ouvre l'agenda avec ce client présélectionné (appointmentLaunch.ts), proposé
// avec `appointments.write` ; désactivé pour un client archivé (D-03-16). Le serveur reste seul juge.
import { Archive, ArchiveRestore, ArrowLeft, Calendar, CalendarPlus, ClipboardList, Mail, MapPin, Pencil, Phone, ShoppingCart, User } from "lucide-react";
import type { ReactNode } from "react";
import { useNavigate } from "react-router";
import type { ClientDetail } from "../api/clients.ts";
import { Button } from "../components/ui/button.tsx";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "../components/ui/tabs.tsx";
import { appointmentLaunchFor } from "../appointments/appointmentLaunch.ts";
import { useAppointmentPermissions } from "../appointments/appointmentPermissions.ts";
import { cn } from "../lib/utils.ts";
import { clientDisplayName, clientInitials, formatIsoDate, formatUtcDate } from "./clientForm.ts";
import { ClientNoticeBanner } from "./ClientNoticeBanner.tsx";
import { useClientLifecycle } from "./ClientLifecycle.tsx";
import { NO_ARCHIVE_TITLE, NO_WRITE_TITLE, READ_ONLY_HINT, useClientPermissions } from "./clientPermissions.ts";
import { StateBlock } from "./ClientStates.tsx";
import { StatusBadge } from "./ClientsTable.tsx";

export function ClientDetailView({ client }: { client: ClientDetail }) {
  const navigate = useNavigate();
  const lifecycle = useClientLifecycle();
  const { canWrite, canArchive } = useClientPermissions();
  const { canWrite: canBook } = useAppointmentPermissions();
  const archived = client.status === "archived";
  const name = clientDisplayName(client);

  return (
    <section className="clients-view" aria-labelledby="client-title">
      <div>
        <Button variant="tertiary" size="sm" className="clients-back" onClick={() => navigate("/clients")}>
          <ArrowLeft size={16} aria-hidden="true" />
          Retour à la liste
        </Button>
      </div>

      <ClientNoticeBanner />

      {archived && (
        <div className="clients-banner clients-banner--neutral">
          <span className="clients-banner__icon" aria-hidden="true">
            <Archive size={20} />
          </span>
          <div>
            <div className="clients-banner__title">Client archivé</div>
            <div className="clients-banner__text">
              Consultation en lecture seule. Sa fiche est conservée. Réactivez-le pour le modifier.
            </div>
          </div>
        </div>
      )}

      <header className="client-head">
        <span className={cn("client-head__avatar", archived && "client-head__avatar--archived")} aria-hidden="true">
          {clientInitials(client)}
        </span>
        <div className="client-head__main">
          <div className="client-head__line">
            <h1 id="client-title" className="client-head__name">
              {name}
            </h1>
            <span className="clients-cell__ref">{client.reference}</span>
            <StatusBadge status={client.status} />
          </div>
          <ul className="client-head__meta" aria-label="Coordonnées principales">
            <Meta icon={<Phone size={14} />} label="Téléphone" value={client.phone} />
            <Meta icon={<Mail size={14} />} label="Email" value={client.email} />
            <Meta icon={<MapPin size={14} />} label="Ville" value={client.city} />
            <Meta icon={<Calendar size={14} />} label="Créé le" value={formatUtcDate(client.createdAtUtc)} />
          </ul>
        </div>
        <div className="clients-actions clients-actions--wrap">
          {canBook && (
            <Button
              variant={archived ? undefined : "primary"}
              disabled={archived}
              title={archived ? "Client archivé : aucun nouveau rendez-vous n'est possible" : undefined}
              onClick={() => navigate("/rendez-vous", { state: appointmentLaunchFor(client, name) })}
            >
              <CalendarPlus size={16} aria-hidden="true" />
              Prendre un rendez-vous
            </Button>
          )}
          {archived ? (
            <>
              <Button disabled title={canWrite ? "Réactivez le client pour le modifier" : NO_WRITE_TITLE}>
                <Pencil size={16} aria-hidden="true" />
                Modifier
              </Button>
              <Button
                variant="primary"
                onClick={() => lifecycle.requestReactivate(client)}
                disabled={!canArchive}
                title={canArchive ? undefined : NO_ARCHIVE_TITLE}
              >
                <ArchiveRestore size={16} aria-hidden="true" />
                Réactiver
              </Button>
            </>
          ) : (
            <>
              <Button
                onClick={() => navigate(`/clients/${client.id}/modifier`)}
                disabled={!canWrite}
                title={canWrite ? undefined : NO_WRITE_TITLE}
              >
                <Pencil size={16} aria-hidden="true" />
                Modifier
              </Button>
              <Button
                variant="danger"
                onClick={() => lifecycle.requestArchive(client)}
                disabled={!canArchive}
                title={canArchive ? undefined : NO_ARCHIVE_TITLE}
              >
                <Archive size={16} aria-hidden="true" />
                Archiver
              </Button>
            </>
          )}
        </div>
      </header>
      {(!canWrite || !canArchive) && (
        <p className="clients-head__hint" role="note">
          {!canWrite ? READ_ONLY_HINT : "Votre compte ne permet pas d'archiver ni de réactiver les clients."}
        </p>
      )}

      <Tabs defaultValue="info" className="client-tabs">
        <TabsList aria-label="Sections de la fiche client">
          <TabsTrigger value="info">
            <User size={16} aria-hidden="true" />
            Informations
          </TabsTrigger>
          <TabsTrigger value="prescriptions">
            <ClipboardList size={16} aria-hidden="true" />
            Ordonnances
          </TabsTrigger>
          <TabsTrigger value="sales">
            <ShoppingCart size={16} aria-hidden="true" />
            Ventes &amp; commandes
          </TabsTrigger>
        </TabsList>
        <TabsContent value="info">
          <InformationTab client={client} />
        </TabsContent>
        <TabsContent value="prescriptions">
          <StateBlock tone="neutral" icon={ClipboardList} title="Ordonnances — phase ultérieure">
            <p>
              Les ordonnances et dossiers optiques relèvent de la Phase 04 (règles de confidentialité et droits spécifiques). Aucune
              donnée n&apos;est disponible ici pour le moment.
            </p>
          </StateBlock>
        </TabsContent>
        <TabsContent value="sales">
          <StateBlock tone="neutral" icon={ShoppingCart} title="Ventes et commandes — phase ultérieure">
            <p>Les ventes, paiements et commandes seront rattachés au client dans une phase ultérieure. Aucune donnée n&apos;est disponible ici pour le moment.</p>
          </StateBlock>
        </TabsContent>
      </Tabs>

      {lifecycle.dialogs}
    </section>
  );
}

function Meta({ icon, label, value }: { icon: ReactNode; label: string; value: string | null }) {
  return (
    <li className="client-head__meta-item">
      <span aria-hidden="true">{icon}</span>
      <span className="visually-hidden">{label} : </span>
      {value ?? <span aria-label="Non renseigné">—</span>}
    </li>
  );
}

function InformationTab({ client }: { client: ClientDetail }) {
  return (
    <div className="client-info">
      <div className="client-info__col">
        <InfoCard title="Coordonnées">
          <Entry label="Téléphone" value={client.phone} numeric />
          <Entry label="Email" value={client.email} />
          <Entry label="Adresse" value={client.addressLine} />
          <Entry label="Ville" value={client.city} />
          <Entry label="Code postal" value={client.postalCode} numeric />
        </InfoCard>
        <InfoCard title="Informations complémentaires">
          <Entry label="Date de naissance" value={formatIsoDate(client.birthDate) || null} numeric />
        </InfoCard>
        <section className="clients-card" aria-labelledby={`notes-${client.id}`}>
          <h2 className="clients-card__title" id={`notes-${client.id}`}>
            Notes
          </h2>
          <p className="client-info__notes">{client.notes ?? <span className="clients-muted">Aucune note.</span>}</p>
        </section>
      </div>
      <div className="client-info__col">
        <InfoCard title="Repères">
          <Entry label="Fiche créée le" value={formatUtcDate(client.createdAtUtc)} numeric />
          <Entry label="Dernière modification" value={formatUtcDate(client.updatedAtUtc)} numeric />
          {client.archivedAtUtc && <Entry label="Archivé le" value={formatUtcDate(client.archivedAtUtc)} numeric />}
          <Entry label="Statut" value={client.status === "archived" ? "Archivé" : "Actif"} />
        </InfoCard>
      </div>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="clients-card">
      <h2 className="clients-card__title">{title}</h2>
      <dl className="client-dl">{children}</dl>
    </section>
  );
}

function Entry({ label, value, numeric = false }: { label: string; value: string | null; numeric?: boolean }) {
  return (
    <>
      <dt>{label}</dt>
      <dd className={cn(numeric && "tnum")}>{value ?? <span className="clients-muted" aria-label="Non renseigné">—</span>}</dd>
    </>
  );
}
