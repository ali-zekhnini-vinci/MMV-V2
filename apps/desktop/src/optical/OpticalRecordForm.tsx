// PROTOTYPE 04-A — formulaire « Ordonnance médicale » (états 10–11, 14, 16–17, 23–24, 26, 32–35) et « Mesure magasin »
// (GAP-04-02), un seul gabarit, provenance PROPRE à chaque type (D-04-01). Le type est choisi AVANT la saisie et ne
// change jamais. Aucune règle locale : champs facultatifs à l'écran ; seules les erreurs renvoyées par le service
// s'affichent sous les champs (aria-describedby) ; critère de confirmation décidé par le service (422 affiché tel quel).
// Version lue à l'ouverture figée (409 sans écrasement, saisie conservée, GAP-04-09). Garde de sortie et Ctrl+S
// comme ClientFormView.
import { AlertTriangle, ArrowLeft, ClipboardCheck, Command, FileText, Info, LogOut, RefreshCw, Ruler, Save, Shield } from "lucide-react";
import { type ReactNode, useCallback, useEffect, useId, useRef, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate } from "react-router";
import type { ApiError } from "../api/client.ts";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { Button } from "../components/ui/button.tsx";
import { DatePicker } from "../components/ui/date-picker.tsx";
import { calendarDateToCivil } from "../components/ui/picker-values.ts";
import { useClientsUi } from "../clients/clientsUi.ts";
import { cn } from "../lib/utils.ts";
import { useLeaveGuard } from "../shell/navigationGuard.ts";
import { EyeGrid } from "./EyeGrid.tsx";
import { useOpticalAccess } from "./opticalAccess.ts";
import { KindBadge, ProposedTag } from "./OpticalBadges.tsx";
import { emptyContent, type OpticalKind, type OpticalRecord, type RecordContent } from "./opticalContract.ts";
import { ConfirmTranscriptionDialog } from "./OpticalDialogs.tsx";
import { KIND_LABEL, LIST_PENDING_NOTE, MEDIA_LABEL, RECTIFICATION_REASON_LABEL, UNSPECIFIED_LABEL } from "./opticalLabels.ts";
import { useConfirmDraft, useCreateRecord, useMeasurers, useOpticalGateway, useSaveDraft } from "./opticalQueries.ts";
import { describeOpticalFailure, type FailureView, fieldErrorMessage, fieldPathLabel, opticalDetailPath, type OpticalLaunchState } from "./opticalUi.ts";

type OpticalRecordFormProps =
  | { mode: "create"; kind: OpticalKind; client: { id: string; label: string }; launch: OpticalLaunchState | null }
  | { mode: "draft"; record: OpticalRecord; launch: OpticalLaunchState | null };

const civilToday = () => calendarDateToCivil(new Date());
const MAX_YEAR = new Date().getFullYear() + 1;

/** Erreurs de champ du service (`{ champ: [codes] }`) → messages par chemin. */
function toFieldMessages(error: ApiError): Record<string, string> {
  const messages: Record<string, string> = {};
  for (const [field, codes] of Object.entries(error.fieldErrors ?? {})) messages[field] = fieldErrorMessage(codes[0]);
  return messages;
}

export function OpticalRecordForm(props: OpticalRecordFormProps) {
  const navigate = useNavigate();
  const gateway = useOpticalGateway();
  const access = useOpticalAccess();
  const { setNotice } = useClientsUi();
  const create = useCreateRecord();
  const save = useSaveDraft();
  const confirm = useConfirmDraft();
  const kind: OpticalKind = props.mode === "create" ? props.kind : props.record.kind;
  const measurers = useMeasurers(kind === "measurement");
  // Enregistrement et version lus à l'ouverture, figés jusqu'à un rechargement explicite (GAP-04-09).
  const [record, setRecord] = useState<OpticalRecord | null>(props.mode === "draft" ? props.record : null);
  const draft = record?.openDraft ?? null;
  const clientLabel = props.mode === "create" ? props.client.label : `${props.record.client.displayName} · ${props.record.client.reference}`;
  const initial: RecordContent = draft?.content ?? emptyContent(kind);

  const {
    register,
    control,
    handleSubmit,
    reset,
    getValues,
    setValue,
    formState: { isDirty },
  } = useForm<RecordContent>({ defaultValues: initial });

  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<FailureView | null>(null);
  const [confirming, setConfirming] = useState<RecordContent | null>(null);
  const [leaveRequest, setLeaveRequest] = useState<(() => void) | null>(null);
  const [reloadAsk, setReloadAsk] = useState(false);
  const [done, setDone] = useState(false);
  const saving = create.isPending || save.isPending || confirm.isPending;
  const back = props.launch?.returnTo ?? (record ? opticalDetailPath(record.kind, record.id) : "/optique");

  // Après un refus : focus sur le premier champ en erreur (convention 03-A), sinon bandeau ramené à l'écran.
  const failureRef = useRef<HTMLDivElement | null>(null);
  const formRef = useRef<HTMLFormElement | null>(null);
  useEffect(() => {
    if (failure === null) return;
    const invalid = formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]');
    if (invalid) invalid.focus();
    else failureRef.current?.scrollIntoView?.({ block: "nearest" });
  }, [failure, fieldErrors]);

  const dirtyRef = useRef(isDirty);
  useEffect(() => {
    dirtyRef.current = isDirty && !done;
  });
  useLeaveGuard((proceed) => {
    if (!dirtyRef.current) return false;
    setLeaveRequest(() => proceed);
    return true;
  });
  const leave = () => {
    const target = record ? opticalDetailPath(record.kind, record.id) : back;
    const go = () => navigate(target, { state: props.launch ?? undefined });
    if (dirtyRef.current) setLeaveRequest(() => go);
    else go();
  };

  const showFailure = (error: ApiError) => {
    const fields = toFieldMessages(error);
    setFieldErrors(fields);
    setFailure(describeOpticalFailure(error));
  };

  const finish = (saved: OpticalRecord, title: string, message: string) => {
    setDone(true);
    reset(getValues());
    const path = opticalDetailPath(saved.kind, saved.id);
    setNotice({ title, message }, path);
    navigate(path, { state: props.launch ?? undefined });
  };

  /** Enregistre le brouillon (création ou mise à jour) ; renvoie l'enregistrement à jour ou `null` en cas d'échec. */
  const persist = async (values: RecordContent): Promise<OpticalRecord | null> => {
    setFailure(null);
    setFieldErrors({});
    try {
      if (record === null) {
        if (props.mode !== "create") return null;
        const created = await create.mutateAsync({ clientId: props.client.id, content: values });
        setRecord(created);
        return created;
      }
      const updated = await save.mutateAsync({ kind: record.kind, id: record.id, content: values, version: record.version });
      setRecord(updated);
      return updated;
    } catch (error) {
      showFailure((error as { error: ApiError }).error);
      return null;
    }
  };

  const onSaveDraft = handleSubmit(async (values) => {
    const saved = await persist(values);
    if (saved) finish(saved, "Brouillon enregistré", `${KIND_LABEL[saved.kind]} ${saved.reference} : brouillon non confirmé, consultable et modifiable.`);
  });

  const onConfirmRequest = handleSubmit((values) => setConfirming(values));

  const onConfirm = async () => {
    if (confirming === null) return;
    const saved = await persist(confirming);
    if (!saved) {
      setConfirming(null);
      return;
    }
    try {
      const confirmed = await confirm.mutateAsync({ kind: saved.kind, id: saved.id, version: saved.version });
      setConfirming(null);
      const revisionNo = confirmed.lastConfirmed?.revisionNo ?? 1;
      finish(confirmed, `Révision ${revisionNo} enregistrée`, `${KIND_LABEL[confirmed.kind]} ${confirmed.reference} confirmée (révision ${revisionNo}).`);
    } catch (error) {
      setConfirming(null);
      // Brouillon enregistré mais confirmation refusée (422 incomplete) : erreurs sous les champs, saisie conservée.
      showFailure((error as { error: ApiError }).error);
    }
  };

  // Rechargement explicite après un conflit : la saisie locale est remplacée seulement après confirmation.
  const reload = async () => {
    if (record === null) return;
    const latest = await gateway.getRecord(record.kind, record.id);
    setReloadAsk(false);
    if (!latest.ok) return showFailure(latest.error);
    setRecord(latest.data);
    reset(latest.data.openDraft?.content ?? getValues());
    setFailure(null);
    setFieldErrors({});
  };

  const submitRef = useRef(onSaveDraft);
  useEffect(() => {
    submitRef.current = onSaveDraft;
  });
  const onKeyDown = useCallback((event: KeyboardEvent) => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault();
      void submitRef.current();
    }
  }, []);
  useEffect(() => {
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onKeyDown]);

  const title =
    props.mode === "create"
      ? kind === "prescription"
        ? "Nouvelle ordonnance médicale"
        : "Nouvelle mesure magasin"
      : draft?.baseRevisionNo
        ? `Rectification de la révision ${draft.baseRevisionNo}`
        : `Brouillon — ${KIND_LABEL[kind].toLocaleLowerCase("fr")}`;
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;
  const documents = record?.documents?.filter((document) => document.invalidation === null) ?? [];
  const linkedWatch = useWatch({ control, name: "linkedDocumentIds" as never }) as unknown as string[] | undefined;
  const linked = kind === "prescription" ? (linkedWatch ?? []) : [];

  return (
    <section className="clients-view" aria-labelledby="optical-form-title">
      <div>
        <Button variant="tertiary" size="sm" className="clients-back" onClick={leave}>
          <ArrowLeft size={16} aria-hidden="true" />
          {props.launch ? `Retour — ${props.launch.returnLabel}` : "Retour"}
        </Button>
      </div>
      <header className="clients-head">
        <div>
          <div className="optical-title-line">
            <h1 id="optical-form-title" className="clients-head__title">
              {title}
            </h1>
            <KindBadge kind={kind} />
          </div>
          <p className="clients-head__sub">
            Client : {clientLabel} · rattaché à un client existant
            {record && <> · {record.reference}</>}
          </p>
          {draft?.baseRevisionNo && draft.reasonCode && (
            <p className="clients-head__hint" role="note">
              Motif : {RECTIFICATION_REASON_LABEL[draft.reasonCode]}
              {draft.reasonComment && ` — ${draft.reasonComment}`}. La révision {draft.baseRevisionNo} reste en vigueur jusqu&apos;à la confirmation.
            </p>
          )}
        </div>
        <div className="clients-head__kbd" aria-hidden="true">
          <Command size={14} /> Ctrl + S
        </div>
      </header>

      {failure && (
        <div ref={failureRef} className={cn("clients-banner", `clients-banner--${failure.tone === "info" ? "info" : failure.tone}`)} role="alert">
          <span className="clients-banner__icon" aria-hidden="true">
            {failure.tone === "info" ? <Shield size={20} /> : <AlertTriangle size={20} />}
          </span>
          <div>
            <div className="clients-banner__title">{hasFieldErrors && failure.title === "Saisie refusée" ? "Veuillez corriger les erreurs avant d'enregistrer" : failure.title}</div>
            <div className="clients-banner__text">{failure.text}</div>
            {hasFieldErrors && (
              <ul className="optical-errors">
                {Object.entries(fieldErrors).map(([path, message]) => (
                  <li key={path}>
                    <b>{fieldPathLabel(path)}</b> : {message}
                  </li>
                ))}
              </ul>
            )}
          </div>
          {failure.reload && record && (
            <Button onClick={() => setReloadAsk(true)}>
              <RefreshCw size={16} aria-hidden="true" />
              Recharger
            </Button>
          )}
        </div>
      )}

      <form
        ref={formRef}
        className="clients-card optical-form"
        onSubmit={(event) => {
          event.preventDefault();
          void onSaveDraft();
        }}
        noValidate
      >
        <fieldset className="optical-form__body" disabled={saving}>
          <FormSection icon={kind === "prescription" ? <FileText size={18} /> : <Ruler size={18} />} title="Informations générales" refs={kind === "prescription" ? "D-04-03.a" : "D-04-03.b"}>
            {kind === "prescription" ? (
              <PrescriptionProvenanceFields register={register} control={control} errors={fieldErrors} />
            ) : (
              <MeasurementProvenanceFields register={register} control={control} errors={fieldErrors} measurers={measurers.data ?? []} />
            )}
          </FormSection>

          <FormSection icon={<Info size={18} />} title="Correction optique" refs="D-04-06" note="Saisie conservée telle quelle : aucune borne, aucun pas, aucune conversion, aucun champ dépendant (règles en attente d'avis opticien).">
            <EyeGrid kind={kind} register={register} errors={fieldErrors} />
          </FormSection>

          {kind === "prescription" && record && (
            <FormSection icon={<FileText size={18} />} title="Pièces liées à cette révision" refs="D-04-10">
              {!access.canReadDocuments ? (
                <p className="clients-muted">{record.documentCount} pièce(s) — accès non autorisé.</p>
              ) : documents.length === 0 ? (
                <p className="clients-muted">Aucune pièce disponible. Les pièces se joignent depuis le détail de l&apos;enregistrement.</p>
              ) : (
                <ul className="optical-links">
                  {documents.map((document) => (
                    <li key={document.id}>
                      <label className="optical-check">
                        <input
                          type="checkbox"
                          checked={linked.includes(document.id)}
                          onChange={(event) =>
                            setValue(
                              "linkedDocumentIds" as never,
                              (event.target.checked ? [...linked, document.id] : linked.filter((id) => id !== document.id)) as never,
                              { shouldDirty: true },
                            )
                          }
                        />
                        <span>
                          {document.originalName} · {MEDIA_LABEL[document.mediaType]}
                        </span>
                      </label>
                    </li>
                  ))}
                </ul>
              )}
            </FormSection>
          )}

          <FormSection icon={<Info size={18} />} title="Notes">
            <NotesField register={register} error={fieldErrors.notes} />
          </FormSection>
        </fieldset>

        <div className="optical-form__actions">
          <Button onClick={leave} disabled={saving}>
            <LogOut size={16} aria-hidden="true" />
            Annuler
          </Button>
          <span className="clients-toolbar__spacer" />
          <Button onClick={() => void onConfirmRequest()} disabled={saving}>
            <ClipboardCheck size={16} aria-hidden="true" />
            Enregistrer et confirmer…
          </Button>
          <Button variant="primary" type="submit" disabled={saving}>
            <Save size={16} aria-hidden="true" />
            {saving ? "Enregistrement…" : "Enregistrer le brouillon"}
          </Button>
        </div>
      </form>

      <ConfirmTranscriptionDialog
        open={confirming !== null}
        onOpenChange={(open) => !open && setConfirming(null)}
        content={confirming}
        clientLabel={clientLabel}
        measurers={measurers.data ?? []}
        pending={saving}
        onConfirm={() => void onConfirm()}
      />
      <ConfirmDialog
        open={leaveRequest !== null}
        onOpenChange={(open) => !open && setLeaveRequest(null)}
        tone="warning"
        icon={<AlertTriangle size={28} />}
        title="Quitter sans enregistrer ?"
        description="Les modifications non enregistrées de ce formulaire seront perdues."
        cancelLabel="Continuer la saisie"
        confirmLabel="Quitter sans enregistrer"
        onConfirm={() => {
          const proceed = leaveRequest;
          setLeaveRequest(null);
          setDone(true);
          dirtyRef.current = false;
          proceed?.();
        }}
      />
      <ConfirmDialog
        open={reloadAsk}
        onOpenChange={setReloadAsk}
        tone="warning"
        icon={<RefreshCw size={28} />}
        title="Recharger la version actuelle ?"
        description="Votre saisie à l'écran sera remplacée par la version enregistrée depuis l'autre poste. Notez-la avant de continuer si nécessaire."
        cancelLabel="Garder ma saisie à l'écran"
        confirmLabel="Recharger"
        onConfirm={() => void reload()}
      />
    </section>
  );
}

function FormSection({ icon, title, refs, note, children }: { icon: ReactNode; title: string; refs?: string; note?: string; children: ReactNode }) {
  const id = useId();
  return (
    <section className="optical-form__section" aria-labelledby={id}>
      <h2 className="clients-form__section" id={id}>
        <span aria-hidden="true">{icon}</span>
        {title}
        {refs && <ProposedTag refs={refs} />}
      </h2>
      {note && <p className="clients-field__hint">{note}</p>}
      {children}
    </section>
  );
}

type FieldProps = {
  register: ReturnType<typeof useForm<RecordContent>>["register"];
  control: ReturnType<typeof useForm<RecordContent>>["control"];
  errors: Record<string, string>;
};

function TextField({ label, name, register, error, hint }: { label: string; name: string; register: FieldProps["register"]; error?: string; hint?: string }) {
  const id = useId();
  return (
    <div className="clients-field">
      <label className="clients-field__label" htmlFor={id}>
        {label}
      </label>
      <div className={cn("clients-input", error && "clients-input--error")}>
        <input
          id={id}
          type="text"
          autoComplete="off"
          spellCheck={false}
          aria-invalid={error ? true : undefined}
          aria-describedby={[hint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(" ") || undefined}
          {...register(name as never)}
        />
      </div>
      {hint && (
        <span id={`${id}-hint`} className="clients-field__hint">
          {hint}
        </span>
      )}
      {error && (
        <span id={`${id}-error`} className="clients-field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}

function DateField({ label, name, control, error, hint }: { label: string; name: string; control: FieldProps["control"]; error?: string; hint?: string }) {
  const id = useId();
  return (
    <div className="clients-field">
      <span className="clients-field__label" id={id}>
        {label}
      </span>
      <Controller
        control={control}
        name={name as never}
        render={({ field }) => (
          <div className="optical-date">
            <DatePicker
              value={(field.value as string) ?? ""}
              onChange={field.onChange}
              today={civilToday()}
              maxYear={MAX_YEAR}
              labelledBy={id}
              describedBy={[hint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(" ") || undefined}
              invalid={Boolean(error)}
              onBlur={field.onBlur}
              placeholder="Non renseignée"
            />
            {field.value && (
              <Button variant="tertiary" size="sm" onClick={() => field.onChange("")} aria-label={`Effacer : ${label}`}>
                Effacer
              </Button>
            )}
          </div>
        )}
      />
      {hint && (
        <span id={`${id}-hint`} className="clients-field__hint">
          {hint}
        </span>
      )}
      {error && (
        <span id={`${id}-error`} className="clients-field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}

function PendingListField({ label, name, register }: { label: string; name: string; register: FieldProps["register"] }) {
  const id = useId();
  return (
    <div className="clients-field">
      <label className="clients-field__label" htmlFor={id}>
        {label}
      </label>
      <div className="clients-input">
        <select id={id} className="optical-select" aria-describedby={`${id}-hint`} {...register(name as never)}>
          <option value="unspecified">{UNSPECIFIED_LABEL}</option>
        </select>
      </div>
      <span id={`${id}-hint`} className="clients-field__hint">
        Seule valeur disponible : {LIST_PENDING_NOTE}.
      </span>
    </div>
  );
}

function PrescriptionProvenanceFields({ register, control, errors }: FieldProps) {
  return (
    <div className="clients-form__grid">
      <TextField label="Prescripteur — nom (tel que sur le document)" name="provenance.prescriberName" register={register} error={errors["provenance.prescriberName"]} />
      <PendingListField label="Prescripteur — qualité" name="provenance.prescriberQualification" register={register} />
      <TextField
        label="Prescripteur — identifiant professionnel"
        name="provenance.prescriberIdentifier"
        register={register}
        error={errors["provenance.prescriberIdentifier"]}
        hint="Tel qu'il figure sur le document ; aucun contrôle de format ni de registre."
      />
      <span />
      <DateField label="Date de prescription" name="provenance.prescribedOn" control={control} error={errors["provenance.prescribedOn"]} hint="Date portée sur le document. Aucune validité n'est calculée." />
      <DateField label="Date de réception au magasin" name="provenance.receivedOn" control={control} error={errors["provenance.receivedOn"]} hint="Distincte de la date de prescription (facultative)." />
    </div>
  );
}

function MeasurementProvenanceFields({ register, control, errors, measurers }: FieldProps & { measurers: readonly { id: string; displayName: string }[] }) {
  const id = useId();
  const error = errors["provenance.measuredByUserId"];
  return (
    <div className="clients-form__grid">
      <DateField label="Date de la mesure" name="provenance.measuredOn" control={control} error={errors["provenance.measuredOn"]} />
      <div className="clients-field">
        <label className="clients-field__label" htmlFor={id}>
          Collaborateur ayant mesuré
        </label>
        <div className={cn("clients-input", error && "clients-input--error")}>
          <select id={id} className="optical-select" aria-describedby={`${id}-hint${error ? ` ${id}-error` : ""}`} {...register("provenance.measuredByUserId" as never)}>
            <option value="">Non renseigné</option>
            {measurers.map((actor) => (
              <option key={actor.id} value={actor.id}>
                {actor.displayName}
              </option>
            ))}
          </select>
        </div>
        <span id={`${id}-hint`} className="clients-field__hint">
          Comptes fictifs du banc ; l&apos;auteur de la saisie est tracé séparément (D-04-03.b, proposé).
        </span>
        {error && (
          <span id={`${id}-error`} className="clients-field__error" role="alert">
            {error}
          </span>
        )}
      </div>
      <PendingListField label="Catégorie de mesure" name="provenance.category" register={register} />
    </div>
  );
}

function NotesField({ register, error }: { register: FieldProps["register"]; error?: string }) {
  const id = useId();
  return (
    <div className="clients-field clients-field--full">
      <label className="clients-field__label" htmlFor={id}>
        Remarques complémentaires (facultatif)
      </label>
      <div className={cn("clients-input clients-input--area", error && "clients-input--error")}>
        <textarea id={id} rows={3} aria-describedby={error ? `${id}-error` : undefined} {...register("notes")} />
      </div>
      {error && (
        <span id={`${id}-error`} className="clients-field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}
