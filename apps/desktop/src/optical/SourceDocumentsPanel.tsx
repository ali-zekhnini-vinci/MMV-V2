// PROTOTYPE 04-A — pièces source d'une ordonnance médicale (GAP-04-06, GAP-04-14 ; D-04-03.c, D-04-10, D-04-12,
// D-04-14 PROPOSED). Une pièce appartient à l'enregistrement (stockée une fois) et chaque révision cite SES pièces.
// « Afficher la pièce » est une action explicite : aucun préchargement, aucune vignette ; la transmission est
// annoncée comme telle (« contenu transmis au poste »), jamais comme une preuve qu'une personne l'a vue.
// Banc : aperçu d'images SYNTHÉTIQUES générées localement ; sélecteur de fichier réel et aperçu PDF reportés (O-04-01).
import { AlertTriangle, Ban, Eye, FileText, Image as ImageIcon, Paperclip, ShieldAlert, X } from "lucide-react";
import { useContext, useId, useState } from "react";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { Button } from "../components/ui/button.tsx";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "../components/ui/dropdown-menu.tsx";
import { cn } from "../lib/utils.ts";
import { useOpticalAccess } from "./opticalAccess.ts";
import { ProposedTag } from "./OpticalBadges.tsx";
import {
  DOCUMENT_INVALIDATION_REASONS,
  type DocumentContent,
  type DocumentInvalidationReason,
  type OpticalRecord,
  type OpticalRevision,
  type SourceDocument,
  type SyntheticDocumentChoice,
} from "./opticalContract.ts";
import { DOCUMENT_INVALIDATION_REASON_LABEL, formatDateTime, formatSize, MEDIA_LABEL } from "./opticalLabels.ts";
import { DocumentSourceContext, useAttachDocument, useDocumentContent, useInvalidateDocument } from "./opticalQueries.ts";
import { describeOpticalFailure, type FailureView } from "./opticalUi.ts";

interface Preview {
  document: SourceDocument;
  content: DocumentContent;
}

export function SourceDocumentsPanel({ record, revision }: { record: OpticalRecord; revision: OpticalRevision }) {
  const id = useId();
  const access = useOpticalAccess();
  const choices = useContext(DocumentSourceContext);
  const attach = useAttachDocument();
  const invalidate = useInvalidateDocument();
  const content = useDocumentContent();
  const [failure, setFailure] = useState<FailureView | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [warnFor, setWarnFor] = useState<SourceDocument | null>(null);
  const [invalidating, setInvalidating] = useState<SourceDocument | null>(null);
  const [reason, setReason] = useState<DocumentInvalidationReason>("attached_by_mistake");
  const linked = revision.content.kind === "prescription" ? revision.content.linkedDocumentIds : [];
  const active = record.lifecycle === "active";

  const show = (document: SourceDocument) => {
    setFailure(null);
    setPreview(null);
    content.mutate(document.id, {
      onSuccess: (data) => setPreview({ document, content: data }),
      onError: (error) => setFailure(describeOpticalFailure(error.error)),
    });
  };
  const requestShow = (document: SourceDocument) => (document.invalidation ? setWarnFor(document) : show(document));

  const onAttach = (choice: SyntheticDocumentChoice) => {
    setFailure(null);
    attach.mutate({ id: record.id, choice }, { onError: (error) => setFailure(describeOpticalFailure(error.error)) });
  };

  const header = (
    <h2 className="clients-card__title" id={`${id}-title`}>
      Pièces source <ProposedTag refs="D-04-03.c · D-04-10 · D-04-12" />
    </h2>
  );

  // Sans `optical.documents.read` : le nombre seulement, aucun nom, aucun aperçu (GAP-04-05).
  if (!access.canReadDocuments || record.documents === null) {
    return (
      <section className="clients-card optical-docs" aria-labelledby={`${id}-title`}>
        {header}
        <p className="optical-docs__denied" role="note">
          <ShieldAlert size={16} aria-hidden="true" /> {record.documentCount} pièce{record.documentCount > 1 ? "s" : ""} — accès non autorisé.
        </p>
      </section>
    );
  }

  const shownOnRevision = record.documents.filter((document) => linked.includes(document.id));
  const others = record.documents.filter((document) => !linked.includes(document.id));

  return (
    <section className="clients-card optical-docs" aria-labelledby={`${id}-title`}>
      {header}
      <div className="optical-docs__bar">
        <p className="clients-muted">
          {shownOnRevision.length === 0 ? <b>Sans pièce source pour cette version.</b> : `${shownOnRevision.length} pièce${shownOnRevision.length > 1 ? "s" : ""} citée${shownOnRevision.length > 1 ? "s" : ""} par cette version.`}
        </p>
        {access.canWriteDocuments && active && (
          <DropdownMenu>
            <DropdownMenuTrigger asChild disabled={attach.isPending}>
              <Button>
                <Paperclip size={16} aria-hidden="true" />
                {attach.isPending ? "Envoi…" : "Joindre une pièce"}
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel className="clients-menu-label">Pièces synthétiques du banc (aucun fichier du poste n&apos;est lu)</DropdownMenuLabel>
              <DropdownMenuSeparator />
              {(choices ?? []).map((choice) => (
                <DropdownMenuItem key={choice.id} onSelect={() => onAttach(choice)}>
                  {choice.label}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>

      {failure && (
        <div className={cn("clients-banner", `clients-banner--${failure.tone}`)} role="alert">
          <span className="clients-banner__icon" aria-hidden="true">
            <AlertTriangle size={20} />
          </span>
          <div>
            <div className="clients-banner__title">{failure.title}</div>
            <div className="clients-banner__text">{failure.text}</div>
          </div>
        </div>
      )}

      <DocumentList title="Citées par la version affichée" documents={shownOnRevision} onShow={requestShow} onInvalidate={access.canWriteDocuments && active ? setInvalidating : undefined} pending={content.isPending} />
      {others.length > 0 && (
        <DocumentList
          title="Autres pièces du dossier (non rattachées à la version affichée)"
          documents={others}
          onShow={requestShow}
          onInvalidate={access.canWriteDocuments && active ? setInvalidating : undefined}
          pending={content.isPending}
        />
      )}

      {preview && (
        <div className="optical-preview" role="region" aria-label={`Aperçu : ${preview.document.originalName}`}>
          <div className="optical-preview__head">
            <span>
              <b>{preview.document.originalName}</b> · contenu transmis au poste. Cela n&apos;atteste pas qu&apos;une personne l&apos;a vu (D-04-12).
            </span>
            <Button variant="ghost" size="icon" aria-label="Fermer l'aperçu" onClick={() => setPreview(null)}>
              <X size={16} aria-hidden="true" />
            </Button>
          </div>
          {preview.document.invalidation && (
            <p className="optical-preview__warn" role="note">
              <AlertTriangle size={14} aria-hidden="true" /> Pièce invalidée — affichée comme preuve d&apos;une révision confirmée (D-04-10.c, proposé).
            </p>
          )}
          {preview.content.imageDataUrl ? (
            <img className="optical-preview__img" src={preview.content.imageDataUrl} alt={`Pièce synthétique ${preview.document.originalName} (aucune donnée réelle)`} />
          ) : (
            <p className="clients-muted">
              {preview.content.mediaType === "application/pdf"
                ? "Aperçu PDF non disponible dans le banc : visionneuse native et ouverture de la CSP à décider et prouver en 04-C (O-04-01, O-04-18)."
                : "Aperçu indisponible dans cet environnement (image synthétique non générée)."}
            </p>
          )}
        </div>
      )}

      <ConfirmDialog
        open={warnFor !== null}
        onOpenChange={(open) => !open && setWarnFor(null)}
        tone="warning"
        icon={<AlertTriangle size={28} />}
        title="Afficher une pièce invalidée ?"
        description={
          warnFor?.invalidation ? (
            <>
              Cette pièce a été invalidée le {formatDateTime(warnFor.invalidation.atUtc)} (motif : {DOCUMENT_INVALIDATION_REASON_LABEL[warnFor.invalidation.reasonCode]}). Elle reste
              consultable comme preuve d&apos;une révision confirmée. Son contenu sera transmis au poste.
            </>
          ) : (
            ""
          )
        }
        cancelLabel="Annuler"
        confirmLabel="Afficher quand même"
        onConfirm={() => {
          const target = warnFor;
          setWarnFor(null);
          if (target) show(target);
        }}
      />
      <ConfirmDialog
        open={invalidating !== null}
        onOpenChange={(open) => !open && !invalidate.isPending && setInvalidating(null)}
        tone="warning"
        icon={<Ban size={28} />}
        title="Invalider cette pièce ?"
        description="La pièce est conservée et reste citée par les révisions confirmées qui l'utilisaient. Elle ne pourra plus être liée à un nouveau brouillon."
        cancelLabel="Annuler"
        confirmLabel={invalidate.isPending ? "Invalidation…" : "Invalider la pièce"}
        pending={invalidate.isPending}
        onConfirm={() => {
          if (!invalidating) return;
          invalidate.mutate(
            { id: record.id, documentId: invalidating.id, reasonCode: reason },
            {
              onSuccess: () => setInvalidating(null),
              onError: (error) => {
                setInvalidating(null);
                setFailure(describeOpticalFailure(error.error));
              },
            },
          );
        }}
      >
        <div className="clients-field">
          <label className="clients-field__label" htmlFor={`${id}-reason`}>
            Motif
          </label>
          <div className="clients-input">
            <select id={`${id}-reason`} className="optical-select" value={reason} onChange={(event) => setReason(event.target.value as DocumentInvalidationReason)}>
              {DOCUMENT_INVALIDATION_REASONS.map((value) => (
                <option key={value} value={value}>
                  {DOCUMENT_INVALIDATION_REASON_LABEL[value]}
                </option>
              ))}
            </select>
          </div>
        </div>
      </ConfirmDialog>
    </section>
  );
}

function DocumentList({
  title,
  documents,
  onShow,
  onInvalidate,
  pending,
}: {
  title: string;
  documents: readonly SourceDocument[];
  onShow: (document: SourceDocument) => void;
  onInvalidate?: (document: SourceDocument) => void;
  pending: boolean;
}) {
  if (documents.length === 0) return null;
  return (
    <div className="optical-docs__group">
      <h3 className="optical-docs__subtitle">{title}</h3>
      <ul className="optical-docs__list">
        {documents.map((document) => {
          const invalid = document.invalidation;
          return (
            <li key={document.id} className={cn("optical-doc", invalid && "optical-doc--invalidated")}>
              <span className="optical-doc__icon" aria-hidden="true">
                {document.mediaType === "application/pdf" ? <FileText size={18} /> : <ImageIcon size={18} />}
              </span>
              <span className="optical-doc__main">
                <span className="optical-doc__name">{document.originalName}</span>
                <span className="clients-muted">
                  {MEDIA_LABEL[document.mediaType]} · {formatSize(document.sizeBytes)} · jointe le {formatDateTime(document.uploadedAtUtc)} par {document.uploadedBy.displayName}
                </span>
                {invalid && (
                  <span className="optical-doc__invalid">
                    Invalidée le {formatDateTime(invalid.atUtc)} — motif : {DOCUMENT_INVALIDATION_REASON_LABEL[invalid.reasonCode]}
                  </span>
                )}
              </span>
              <span className="clients-rowacts">
                <Button size="sm" onClick={() => onShow(document)} disabled={pending}>
                  <Eye size={14} aria-hidden="true" />
                  Afficher la pièce
                </Button>
                {onInvalidate && !invalid && (
                  <Button size="sm" variant="danger" onClick={() => onInvalidate(document)}>
                    <Ban size={14} aria-hidden="true" />
                    Invalider
                  </Button>
                )}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
