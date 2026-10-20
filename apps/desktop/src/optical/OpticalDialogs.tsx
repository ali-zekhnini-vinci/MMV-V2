// PROTOTYPE 04-A — boîtes de dialogue du dossier optique, sur ConfirmDialog (components/ui/alert-dialog.tsx) :
// - confirmation de la transcription (GAP-04-10, D-04-02 PROPOSED) : « Confirmé » ≠ « validé médicalement » ;
// - rectification avec motif obligatoire (état 23, D-04-04.b / D-04-11 PROPOSED) ;
// - invalidation motivée, jamais de suppression (états 28–29, D-04-04.c ; brouillon abandonné, D-04-11.e).
import { Ban, ClipboardCheck, PencilLine } from "lucide-react";
import { useId, useState } from "react";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { formatIsoDate } from "../clients/clientForm.ts";
import {
  INVALIDATION_REASONS,
  type InvalidationReason,
  type OpticalActor,
  type OpticalKind,
  RECTIFICATION_REASONS,
  type RectificationReason,
  type RecordContent,
} from "./opticalContract.ts";
import { INVALIDATION_REASON_LABEL, KIND_LABEL, RECTIFICATION_REASON_LABEL } from "./opticalLabels.ts";
import { eyeSummary } from "./opticalUi.ts";
import { ProposedTag } from "./OpticalBadges.tsx";

const COMMENT_MAX = 500;

export function ConfirmTranscriptionDialog({
  open,
  onOpenChange,
  content,
  clientLabel,
  measurers,
  pending,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  content: RecordContent | null;
  clientLabel: string;
  measurers: readonly OpticalActor[];
  pending: boolean;
  onConfirm: () => void;
}) {
  if (content === null) return null;
  const kind = content.kind;
  const provenance =
    content.kind === "prescription"
      ? `${content.provenance.prescriberName || "prescripteur non renseigné"} · prescrite le ${formatIsoDate(content.provenance.prescribedOn) || "date non renseignée"}`
      : `mesurée le ${formatIsoDate(content.provenance.measuredOn) || "date non renseignée"} par ${measurers.find((actor) => actor.id === content.provenance.measuredByUserId)?.displayName ?? "collaborateur non renseigné"}`;
  return (
    <ConfirmDialog
      open={open}
      onOpenChange={(next) => !pending && onOpenChange(next)}
      tone="accent"
      icon={<ClipboardCheck size={28} />}
      title={kind === "prescription" ? "Confirmer la transcription ?" : "Confirmer la mesure magasin ?"}
      description={
        kind === "prescription" ? (
          <>« Confirmé » signifie que la transcription a été vérifiée par rapport au document reçu. Cela ne signifie pas « validé médicalement ».</>
        ) : (
          <>La mesure est confirmée par le magasin. Elle reste une mesure magasin : ce n&apos;est pas une ordonnance médicale.</>
        )
      }
      cancelLabel="Revenir"
      confirmLabel={pending ? "Confirmation…" : "Confirmer"}
      confirmIcon={<ClipboardCheck size={16} aria-hidden="true" />}
      onConfirm={onConfirm}
      pending={pending}
    >
      <dl className="client-dl optical-recap">
        <dt>Type</dt>
        <dd>{KIND_LABEL[kind]}</dd>
        <dt>Client</dt>
        <dd>{clientLabel}</dd>
        <dt>Provenance</dt>
        <dd>{provenance}</dd>
        <dt>Correction</dt>
        <dd className="tnum">
          {eyeSummary("od", content.od)} · {eyeSummary("og", content.og)}
        </dd>
      </dl>
      <p className="optical-dialog__note">
        Critère de confirmation décidé par le service (proposition D-04-02.b, non codée dans le poste). <ProposedTag refs="D-04-02" />
      </p>
    </ConfirmDialog>
  );
}

function ReasonFields<T extends string>({
  reasons,
  labels,
  reason,
  onReason,
  comment,
  onComment,
  error,
}: {
  reasons: readonly T[];
  labels: Record<T, string>;
  reason: T | "";
  onReason: (reason: T) => void;
  comment: string;
  onComment: (comment: string) => void;
  error: string | null;
}) {
  const id = useId();
  return (
    <div className="optical-dialog__fields">
      <div className="clients-field">
        <label className="clients-field__label" htmlFor={`${id}-reason`} data-required="">
          Motif
        </label>
        <div className="clients-input">
          <select id={`${id}-reason`} className="optical-select" value={reason} onChange={(event) => onReason(event.target.value as T)} aria-describedby={error ? `${id}-error` : undefined}>
            <option value="" disabled>
              Choisir un motif…
            </option>
            {reasons.map((value) => (
              <option key={value} value={value}>
                {labels[value]}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="clients-field">
        <label className="clients-field__label" htmlFor={`${id}-comment`} data-required={reason === "other" ? "" : undefined}>
          Commentaire {reason === "other" ? "" : "(facultatif)"}
        </label>
        <div className="clients-input clients-input--area">
          <textarea id={`${id}-comment`} rows={3} maxLength={COMMENT_MAX} value={comment} onChange={(event) => onComment(event.target.value)} />
        </div>
      </div>
      {error && (
        <span id={`${id}-error`} className="clients-field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}

export function RectifyDialog({
  open,
  onOpenChange,
  revisionNo,
  pending,
  serverError,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  revisionNo: number;
  pending: boolean;
  serverError: string | null;
  onSubmit: (reason: RectificationReason, comment: string) => void;
}) {
  const [reason, setReason] = useState<RectificationReason | "">("");
  const [comment, setComment] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);
  const submit = () => {
    // Présence du motif : contrôle de forme du formulaire, le service revalide (422).
    if (reason === "") return setLocalError("Choisissez un motif de rectification.");
    if (reason === "other" && comment.trim() === "") return setLocalError("Précisez le motif dans le commentaire.");
    setLocalError(null);
    onSubmit(reason, comment.trim());
  };
  return (
    <ConfirmDialog
      open={open}
      onOpenChange={(next) => {
        if (pending) return;
        if (!next) {
          setReason("");
          setComment("");
          setLocalError(null);
        }
        onOpenChange(next);
      }}
      tone="info"
      icon={<PencilLine size={28} />}
      title="Rectifier cet enregistrement ?"
      description={
        <>
          Un brouillon de rectification est ouvert à partir de la révision {revisionNo}. La révision {revisionNo} reste affichée et inchangée tant que la
          rectification n&apos;est pas confirmée.
        </>
      }
      cancelLabel="Annuler"
      confirmLabel={pending ? "Ouverture…" : "Ouvrir la rectification"}
      confirmIcon={<PencilLine size={16} aria-hidden="true" />}
      onConfirm={submit}
      pending={pending}
    >
      <ReasonFields
        reasons={RECTIFICATION_REASONS}
        labels={RECTIFICATION_REASON_LABEL}
        reason={reason}
        onReason={setReason}
        comment={comment}
        onComment={setComment}
        error={localError ?? serverError}
      />
      <p className="optical-dialog__note">
        Motifs proposés, non ratifiés. <ProposedTag refs="D-04-04.b · D-04-11" />
      </p>
    </ConfirmDialog>
  );
}

export function InvalidateDialog({
  open,
  onOpenChange,
  kind,
  hasRectification,
  pending,
  serverError,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  kind: OpticalKind;
  hasRectification: boolean;
  pending: boolean;
  serverError: string | null;
  onSubmit: (reason: InvalidationReason, comment: string) => void;
}) {
  const [reason, setReason] = useState<InvalidationReason | "">("");
  const [comment, setComment] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);
  const submit = () => {
    if (reason === "") return setLocalError("Choisissez un motif d'invalidation.");
    if (reason === "other" && comment.trim() === "") return setLocalError("Précisez le motif dans le commentaire.");
    setLocalError(null);
    onSubmit(reason, comment.trim());
  };
  return (
    <ConfirmDialog
      open={open}
      onOpenChange={(next) => {
        if (pending) return;
        if (!next) {
          setReason("");
          setComment("");
          setLocalError(null);
        }
        onOpenChange(next);
      }}
      tone="warning"
      icon={<Ban size={28} />}
      title={`Invalider cette ${kind === "prescription" ? "ordonnance médicale" : "mesure magasin"} ?`}
      description={
        <>
          L&apos;enregistrement est conservé, reste consultable et sera signalé « Invalidé ». Il ne sera plus proposé par défaut dans les listes. Rien
          n&apos;est supprimé.
          {hasRectification && <> La rectification en cours sera abandonnée dans la même opération.</>}
        </>
      }
      cancelLabel="Annuler"
      confirmLabel={pending ? "Invalidation…" : "Invalider"}
      confirmIcon={<Ban size={16} aria-hidden="true" />}
      onConfirm={submit}
      pending={pending}
    >
      <ReasonFields
        reasons={INVALIDATION_REASONS}
        labels={INVALIDATION_REASON_LABEL}
        reason={reason}
        onReason={setReason}
        comment={comment}
        onComment={setComment}
        error={localError ?? serverError}
      />
      <p className="optical-dialog__note">
        Motifs proposés, non ratifiés. <ProposedTag refs="D-04-04.c" />
      </p>
    </ConfirmDialog>
  );
}
