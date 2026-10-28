// PROTOTYPE 04-A — détail d'une ordonnance médicale (états 19–22, 27–31) ou d'une mesure magasin (GAP-04-03) :
// badges de type et de statut dérivé (GAP-04-11), provenance propre au type, OD / OG tels que saisis, notes,
// révisions et comparaison (GAP-04-07). Aucune pièce source (D-04-16 : gestion documentaire reportée au backlog). La DERNIÈRE RÉVISION CONFIRMÉE reste affichée par
// défaut même si un brouillon de rectification plus récent existe (GAP-04-13, E-02) ; le brouillon se consulte
// séparément, libellé « non confirmé ». Invalidation au menu secondaire, jamais de suppression (GAP-04-08).
// Lecture seule déduite des droits simulés (état 22), jamais d'un rôle visuel. Cycle de vie : décisions PROPOSED.
import { AlertTriangle, ArrowLeft, Ban, ClipboardCheck, GitCompare, Info, MoreHorizontal, PencilLine, ShoppingCart, Trash2 } from "lucide-react";
import { type ReactNode, useState } from "react";
import { useNavigate } from "react-router";
import type { ApiError } from "../api/client.ts";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { Button } from "../components/ui/button.tsx";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "../components/ui/dropdown-menu.tsx";
import { ClientNoticeBanner } from "../clients/ClientNoticeBanner.tsx";
import { useClientsUi } from "../clients/clientsUi.ts";
import { formatIsoDate } from "../clients/clientForm.ts";
import { cn } from "../lib/utils.ts";
import { EyeTable } from "./EyeGrid.tsx";
import { canWriteKind, useOpticalAccess } from "./opticalAccess.ts";
import { KindBadge, OpticalStatusBadge, ProposedTag } from "./OpticalBadges.tsx";
import { displayStatus, knownActors, type OpticalActor, type OpticalRecord, type OpticalRevision } from "./opticalContract.ts";
import { ConfirmTranscriptionDialog, InvalidateDialog, RectifyDialog } from "./OpticalDialogs.tsx";
import { eventLabel, formatDateTime, INVALIDATION_REASON_LABEL, KIND_LABEL, RECTIFICATION_REASON_LABEL } from "./opticalLabels.ts";
import { useConfirmDraft, useDiscardRectification, useInvalidateRecord, useMeasurers, useStartRectification } from "./opticalQueries.ts";
import { revisionLabel } from "./opticalRevisions.ts";
import { RevisionsPanel } from "./RevisionsPanel.tsx";
import { describeOpticalFailure, type FailureView, fieldErrorMessage, fieldPathLabel, opticalDraftPath, opticalListPath, type OpticalLaunchState, returnToCardState } from "./opticalUi.ts";

export function OpticalRecordDetail({ record, launch }: { record: OpticalRecord; launch: OpticalLaunchState | null }) {
  const navigate = useNavigate();
  const access = useOpticalAccess();
  const { setNotice } = useClientsUi();
  const measurerList = useMeasurers(record.kind === "measurement");
  const measurers = knownActors(measurerList.data ?? [], record.citedUsers);
  const confirm = useConfirmDraft();
  const rectify = useStartRectification();
  const discard = useDiscardRectification();
  const invalidate = useInvalidateRecord();
  const [view, setView] = useState<"current" | "draft">("current");
  const [dialog, setDialog] = useState<"confirm" | "rectify" | "discard" | "invalidate" | null>(null);
  const [failure, setFailure] = useState<(FailureView & { fields: [string, string][] }) | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);

  const status = displayStatus(record);
  const canWrite = canWriteKind(access, record.kind);
  const archived = record.client.status === "archived";
  const active = record.lifecycle === "active";
  const draft = record.openDraft;
  const firstEntry = draft !== null && draft.baseRevisionNo === null;
  const rectification = draft !== null && draft.baseRevisionNo !== null ? draft : null;
  const shown: OpticalRevision = view === "draft" && draft ? draft : (record.lastConfirmed ?? draft ?? record.revisions[record.revisions.length - 1]);
  const title = `${KIND_LABEL[record.kind]} ${record.reference}`;

  const fail = (error: ApiError) => {
    const view = describeOpticalFailure(error);
    setFailure({ ...view, fields: Object.entries(error.fieldErrors ?? {}).map(([path, codes]) => [path, fieldErrorMessage(codes[0], path)]) });
  };
  const goBack = () => {
    if (launch?.returnTo.startsWith("/clients/")) navigate(launch.returnTo, { state: returnToCardState() });
    else navigate(opticalListPath());
  };
  const notify = (noticeTitle: string, message: string) => setNotice({ title: noticeTitle, message });

  const onConfirm = () =>
    confirm.mutate(
      { kind: record.kind, id: record.id, version: record.version },
      {
        onSuccess: (saved) => {
          setDialog(null);
          setView("current");
          notify(`Révision ${saved.lastConfirmed?.revisionNo ?? 1} enregistrée`, `${title} : version confirmée et désormais affichée.`);
        },
        onError: (error) => {
          setDialog(null);
          fail(error.error);
        },
      },
    );

  const onRectify = (reasonCode: OpticalRevision["reasonCode"] & string, comment: string) => {
    if (!record.lastConfirmed?.revisionNo) return;
    rectify.mutate(
      { kind: record.kind, id: record.id, request: { expectedRevisionNo: record.lastConfirmed.revisionNo, reasonCode, comment, version: record.version } },
      {
        onSuccess: (saved) => {
          setDialog(null);
          navigate(opticalDraftPath(saved.kind, saved.id), { state: launch ?? undefined });
        },
        onError: (error) => {
          if (error.error.kind === "validation") return setDialogError(fieldErrorMessage(error.error.fieldErrors?.comment?.[0] ?? "required", "comment"));
          setDialog(null);
          fail(error.error);
        },
      },
    );
  };

  const onDiscard = () =>
    discard.mutate(
      { kind: record.kind, id: record.id, version: record.version },
      {
        onSuccess: () => {
          setDialog(null);
          setView("current");
          notify("Modification abandonnée", `La dernière révision confirmée (révision ${record.lastConfirmed?.revisionNo ?? ""}) est inchangée. Le brouillon abandonné reste visible dans l'historique des modifications.`);
        },
        onError: (error) => {
          setDialog(null);
          fail(error.error);
        },
      },
    );

  const onInvalidate = (reasonCode: Parameters<typeof invalidate.mutate>[0]["request"]["reasonCode"], comment: string) =>
    invalidate.mutate(
      { kind: record.kind, id: record.id, request: { reasonCode, comment, version: record.version } },
      {
        onSuccess: () => {
          setDialog(null);
          notify("Enregistrement invalidé", `${title} est conservé et signalé « Invalidé ». Il n'apparaît plus par défaut dans les listes.`);
        },
        onError: (error) => {
          if (error.error.kind === "validation") return setDialogError(fieldErrorMessage(error.error.fieldErrors?.comment?.[0] ?? "required", "comment"));
          setDialog(null);
          fail(error.error);
        },
      },
    );

  const openDialog = (name: typeof dialog) => {
    setFailure(null);
    setDialogError(null);
    setDialog(name);
  };

  return (
    <section className="clients-view" aria-labelledby="optical-detail-title">
      <div>
        <Button variant="tertiary" size="sm" className="clients-back" onClick={goBack}>
          <ArrowLeft size={16} aria-hidden="true" />
          {launch?.returnTo.startsWith("/clients/") ? `Retour à la fiche — ${launch.returnLabel}` : "Retour à la liste"}
        </Button>
      </div>

      <ClientNoticeBanner />

      {!active && record.invalidation && (
        <Banner tone="warning" icon={<Ban size={20} />} title={`Invalidé le ${formatDateTime(record.invalidation.atUtc)} par ${record.invalidation.by.displayName}`}>
          Motif : {INVALIDATION_REASON_LABEL[record.invalidation.reasonCode]}
          {record.invalidation.comment && ` — ${record.invalidation.comment}`}. L&apos;enregistrement est conservé et consultable ; il ne peut plus être utilisé.
          {record.invalidation.discardedDraft && " La rectification ouverte a été abandonnée dans la même opération."}
        </Banner>
      )}
      {active && rectification && (
        <Banner
          tone="info"
          icon={<PencilLine size={20} />}
          title={`Modification en cours — ouverte le ${formatDateTime(rectification.startedAtUtc)} par ${rectification.startedBy.displayName}`}
          action={
            <div className="clients-actions clients-actions--wrap">
              {canWrite && !archived && (
                <Button size="sm" variant="primary" onClick={() => navigate(opticalDraftPath(record.kind, record.id), { state: launch ?? undefined })}>
                  Modifier
                </Button>
              )}
              <Button size="sm" onClick={() => setView(view === "draft" ? "current" : "draft")}>
                <GitCompare size={14} aria-hidden="true" />
                {view === "draft" ? `Revenir à la révision ${record.lastConfirmed?.revisionNo}` : "Voir le brouillon"}
              </Button>
              {canWrite && (
                <Button size="sm" variant="danger" onClick={() => openDialog("discard")}>
                  <Trash2 size={14} aria-hidden="true" />
                  Abandonner
                </Button>
              )}
            </div>
          }
        >
          Motif : {rectification.reasonCode ? RECTIFICATION_REASON_LABEL[rectification.reasonCode] : "—"}
          {rectification.reasonComment && ` — ${rectification.reasonComment}`}. La dernière révision confirmée (révision {record.lastConfirmed?.revisionNo}) reste affichée par défaut.
          {archived && " Client archivé : le brouillon est gelé (abandon possible)."}
        </Banner>
      )}
      {active && firstEntry && (
        <Banner tone="neutral" icon={<Info size={20} />} title="Brouillon non confirmé">
          Cette saisie n&apos;a jamais été confirmée : elle ne peut être utilisée nulle part (une vente future exigera une révision confirmée).
        </Banner>
      )}
      {failure && (
        <Banner tone={failure.tone === "info" ? "info" : failure.tone} icon={<AlertTriangle size={20} />} title={failure.title} role="alert">
          {failure.text}
          {failure.fields.length > 0 && (
            <ul className="optical-errors">
              {failure.fields.map(([path, message]) => (
                <li key={path}>
                  <b>{fieldPathLabel(path)}</b> : {message}
                </li>
              ))}
            </ul>
          )}
        </Banner>
      )}

      <header className="client-head optical-head">
        <div className="client-head__main">
          <div className="client-head__line">
            <h1 id="optical-detail-title" className="client-head__name">
              {title}
            </h1>
            <KindBadge kind={record.kind} />
            <OpticalStatusBadge status={status} revisionNo={record.lastConfirmed?.revisionNo} />
          </div>
          <p className="clients-head__sub">
            Client : {record.client.displayName} · {record.client.reference}
            {archived && " · client archivé"}
          </p>
        </div>
        <div className="clients-actions clients-actions--wrap">
          {active && firstEntry && canWrite && (
            <>
              <Button onClick={() => navigate(opticalDraftPath(record.kind, record.id), { state: launch ?? undefined })} disabled={archived}>
                <PencilLine size={16} aria-hidden="true" />
                Modifier
              </Button>
              <Button variant="primary" onClick={() => openDialog("confirm")} disabled={archived}>
                <ClipboardCheck size={16} aria-hidden="true" />
                {record.kind === "prescription" ? "Confirmer la transcription" : "Confirmer la mesure"}
              </Button>
            </>
          )}
          {active && rectification && canWrite && view === "draft" && (
            <Button variant="primary" onClick={() => openDialog("confirm")} disabled={archived}>
              <ClipboardCheck size={16} aria-hidden="true" />
              Confirmer la modification
            </Button>
          )}
          {active && record.lastConfirmed && !draft && canWrite && (
            <Button
              variant="primary"
              onClick={() => openDialog("rectify")}
              disabled={archived}
              title={archived ? "Client archivé : réactivez le client avant de modifier (D-04-07, proposé)" : undefined}
            >
              <PencilLine size={16} aria-hidden="true" />
              Modifier
            </Button>
          )}
          {record.lastConfirmed && (
            <Button disabled title="Phase ultérieure : aucun contexte n'est transmis, aucune vente n'est créée (D-04-08.d)">
              <ShoppingCart size={16} aria-hidden="true" />
              Préparer une vente · Phase ultérieure
            </Button>
          )}
          {access.canInvalidate && active && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Plus d'actions">
                  <MoreHorizontal size={18} aria-hidden="true" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onSelect={() => openDialog("invalidate")}>
                  <Ban size={16} aria-hidden="true" />
                  Invalider…
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </header>
      {!canWrite && (
        <p className="clients-head__hint" role="note">
          Consultation seule : ce profil ne permet pas de modifier les {record.kind === "prescription" ? "ordonnances médicales" : "mesures magasin"} (droit d&apos;écriture proposé, D-04-05).
          Le service refuserait toute modification.
        </p>
      )}

      <div className="optical-detail">
        <section className="clients-card" aria-labelledby="optical-shown-title">
          <h2 className="clients-card__title" id="optical-shown-title">
            {shown.status === "confirmed" ? `Dernière révision confirmée — ${revisionLabel(shown).toLocaleLowerCase("fr")}` : revisionLabel(shown)}
          </h2>
          <p className="clients-muted">
            {shown.status === "confirmed" && shown.confirmedAtUtc
              ? `Confirmée le ${formatDateTime(shown.confirmedAtUtc)} par ${shown.confirmedBy?.displayName ?? "—"} · saisie par ${shown.startedBy.displayName}`
              : `Modifié le ${formatDateTime(shown.updatedAtUtc)} par ${shown.updatedBy.displayName} · non confirmé`}
          </p>
          <ProvenanceList revision={shown} measurers={measurers} />
          <EyeTable od={shown.content.od} og={shown.content.og} caption={`Correction optique — ${revisionLabel(shown)}`} />
          <h3 className="optical-detail__subtitle">Notes</h3>
          <p className="client-info__notes">{shown.content.notes || <span className="clients-muted">Aucune note.</span>}</p>
        </section>

        {record.historyAvailable ? (
          <>
            <RevisionsPanel key={`${record.id}-${record.version}`} record={record} measurers={measurers} />
            <EventsPanel record={record} />
          </>
        ) : (
          <section className="clients-card" aria-labelledby="optical-history-title">
            <h2 className="clients-card__title" id="optical-history-title">
              Historique des modifications
            </h2>
            <p className="clients-field__hint" role="note">
              L&apos;historique (révisions antérieures, auteurs, motifs) exige le droit de consultation de l&apos;historique en plus de
              l&apos;accès au dossier optique. Seules la dernière révision confirmée et la modification en cours sont affichées.
            </p>
          </section>
        )}
      </div>

      <ConfirmTranscriptionDialog
        open={dialog === "confirm"}
        onOpenChange={(open) => !open && setDialog(null)}
        content={draft?.content ?? null}
        clientLabel={`${record.client.displayName} · ${record.client.reference}`}
        measurers={measurers}
        pending={confirm.isPending}
        onConfirm={onConfirm}
      />
      {record.lastConfirmed?.revisionNo && (
        <RectifyDialog
          open={dialog === "rectify"}
          onOpenChange={(open) => !open && setDialog(null)}
          revisionNo={record.lastConfirmed.revisionNo}
          pending={rectify.isPending}
          serverError={dialogError}
          onSubmit={onRectify}
        />
      )}
      <InvalidateDialog
        open={dialog === "invalidate"}
        onOpenChange={(open) => !open && setDialog(null)}
        kind={record.kind}
        hasRectification={rectification !== null}
        pending={invalidate.isPending}
        serverError={dialogError}
        onSubmit={onInvalidate}
      />
      <ConfirmDialog
        open={dialog === "discard"}
        onOpenChange={(open) => !open && !discard.isPending && setDialog(null)}
        tone="warning"
        icon={<Trash2 size={28} />}
        title="Abandonner la modification ?"
        description={`Le brouillon est conservé comme « rectification abandonnée » (rien n'est supprimé). La dernière révision confirmée (révision ${record.lastConfirmed?.revisionNo ?? ""}) est inchangée.`}
        cancelLabel="Conserver le brouillon"
        confirmLabel={discard.isPending ? "Abandon…" : "Abandonner la modification"}
        pending={discard.isPending}
        onConfirm={onDiscard}
      />
    </section>
  );
}

function Banner({
  tone,
  icon,
  title,
  children,
  action,
  role = "note",
}: {
  tone: "warning" | "info" | "error" | "neutral";
  icon: ReactNode;
  title: string;
  children: ReactNode;
  action?: ReactNode;
  role?: "note" | "alert";
}) {
  return (
    <div className={cn("clients-banner", `clients-banner--${tone}`, "optical-banner")} role={role}>
      <span className="clients-banner__icon" aria-hidden="true">
        {icon}
      </span>
      <div className="optical-banner__body">
        <div className="clients-banner__title">{title}</div>
        <div className="clients-banner__text">{children}</div>
      </div>
      {action}
    </div>
  );
}

function ProvenanceList({ revision, measurers }: { revision: OpticalRevision; measurers: readonly OpticalActor[] }) {
  const content = revision.content;
  const missing = <span className="clients-muted">Non renseigné</span>;
  if (content.kind === "prescription") {
    const p = content.provenance;
    return (
      <dl className="client-dl optical-provenance">
        <dt>Prescripteur</dt>
        <dd>{p.prescriberName || missing}</dd>
        <dt>Qualité</dt>
        <dd>{p.prescriberQualification || missing}</dd>
        <dt>Identifiant professionnel</dt>
        <dd>{p.prescriberIdentifier || missing}</dd>
        <dt>Date de prescription</dt>
        <dd className="tnum">{formatIsoDate(p.prescribedOn) || missing}</dd>
        <dt>Date de réception</dt>
        <dd className="tnum">{formatIsoDate(p.receivedOn) || missing}</dd>
        <dt>Saisie</dt>
        <dd>
          {formatDateTime(revision.startedAtUtc)} par {revision.startedBy.displayName} (auteur de la transcription, distinct du prescripteur)
        </dd>
      </dl>
    );
  }
  const p = content.provenance;
  return (
    <dl className="client-dl optical-provenance">
      <dt>Date de la mesure</dt>
      <dd className="tnum">{formatIsoDate(p.measuredOn) || missing}</dd>
      <dt>Collaborateur ayant mesuré</dt>
      <dd>{measurers.find((actor) => actor.id === p.measuredByUserId)?.displayName || missing}</dd>
      <dt>Catégorie</dt>
      <dd>{p.category || missing}</dd>
      <dt>Saisie</dt>
      <dd>
        {formatDateTime(revision.startedAtUtc)} par {revision.startedBy.displayName}
      </dd>
    </dl>
  );
}

function EventsPanel({ record }: { record: OpticalRecord }) {
  return (
    <section className="clients-card" aria-labelledby="optical-events-title">
      <h2 className="clients-card__title" id="optical-events-title">
        Journal de gestion <ProposedTag refs="D-04-12" />
      </h2>
      <p className="clients-field__hint">Actions enregistrées par le serveur ; aucune valeur optique.</p>
      <ol className="optical-events">
        {[...record.events].reverse().map((event) => (
          <li key={event.id}>
            <span className="tnum clients-muted">{formatDateTime(event.atUtc)}</span>
            <span>
              {eventLabel(event.code)}
              {event.revisionNo !== undefined && ` — révision ${event.revisionNo}`}
            </span>
            <span className="clients-muted">{event.actor.displayName}</span>
          </li>
        ))}
      </ol>
    </section>
  );
}
