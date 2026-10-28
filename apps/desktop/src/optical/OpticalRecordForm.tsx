// PROTOTYPE 04-A — formulaire « Ordonnance médicale » (états 10–11, 14, 16–17, 23–24, 26, 32–35) et « Mesure magasin »
// (GAP-04-02), un seul gabarit, provenance PROPRE à chaque type (D-04-01). Le type est choisi AVANT la saisie et ne
// change jamais. Mise en page de la maquette Lot 3 (D-04-15 APPROVED) : en-tête avec Annuler / Enregistrer, carte
// « Informations générales » avec le client concerné, cartes OD / OG côte à côte avec boutons ±, carte Notes.
// Aucun « état de l'œil » ni pièce source (D-04-16 APPROVED : parcours simplifié, révisions conservées).
// Enregistrement simplifié (D-04-17 APPROVED) : « Enregistrer » (principal, Ctrl+S, Entrée) enregistre ET confirme en UNE
// opération atomique du service (04-B) — un refus n'enregistre rien (aucun brouillon orphelin, saisie conservée) ;
// « Enregistrer comme brouillon » (discret) enregistre sans confirmer. Réponse incertaine (réseau, délai) : l'état réel
// est relu avant toute nouvelle tentative, qui reprend la même demande sans doublon (passerelle src/api/optical.ts).
// Aucune règle locale : champs facultatifs à l'écran ; seules les erreurs renvoyées par le service
// s'affichent sous les champs (aria-describedby) ; critère de confirmation décidé par le service (422 affiché tel quel).
// Version lue à l'ouverture figée (409 sans écrasement, saisie conservée, GAP-04-09). Garde de sortie et Ctrl+S
// comme ClientFormView.
import { AlertTriangle, ArrowLeft, Check, Command, FilePen, Glasses, Info, NotebookPen, RefreshCw, Ruler, Shield, User, X } from "lucide-react";
import { type ReactNode, useCallback, useContext, useEffect, useId, useRef, useState } from "react";
import { Controller, useForm } from "react-hook-form";
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
import { KindBadge } from "./OpticalBadges.tsx";
import { emptyContent, knownActors, type OpticalKind, type OpticalRecord, type RecordContent } from "./opticalContract.ts";
import { KIND_LABEL, RECTIFICATION_REASON_LABEL } from "./opticalLabels.ts";
import { useCreateAndConfirm, useCreateRecord, useMeasurers, useOpticalGateway, useSaveAndConfirm, useSaveDraft } from "./opticalQueries.ts";
import { OpticalStepContext, stepLabel } from "./opticalStep.ts";
import { describeOpticalFailure, describeSaveFailure, type FailureView, fieldErrorMessage, fieldPathLabel, opticalDetailPath, type OpticalLaunchState } from "./opticalUi.ts";

type OpticalRecordFormProps =
  | { mode: "create"; kind: OpticalKind; client: { id: string; label: string }; launch: OpticalLaunchState | null }
  | { mode: "draft"; record: OpticalRecord; launch: OpticalLaunchState | null };

const civilToday = () => calendarDateToCivil(new Date());
const MAX_YEAR = new Date().getFullYear() + 1;

/** Erreurs de champ du service (`{ champ: [codes] }`) → messages par chemin. */
function toFieldMessages(error: ApiError): Record<string, string> {
  const messages: Record<string, string> = {};
  for (const [field, codes] of Object.entries(error.fieldErrors ?? {})) messages[field] = fieldErrorMessage(codes[0], field);
  return messages;
}

export function OpticalRecordForm(props: OpticalRecordFormProps) {
  const navigate = useNavigate();
  const gateway = useOpticalGateway();
  const { setNotice } = useClientsUi();
  const create = useCreateRecord();
  const save = useSaveDraft();
  const createConfirm = useCreateAndConfirm();
  const saveConfirm = useSaveAndConfirm();
  const kind: OpticalKind = props.mode === "create" ? props.kind : props.record.kind;
  const measurers = useMeasurers(kind === "measurement");
  // Enregistrement et version lus à l'ouverture, figés jusqu'à un rechargement explicite (GAP-04-09).
  const [record, setRecord] = useState<OpticalRecord | null>(props.mode === "draft" ? props.record : null);
  const draft = record?.openDraft ?? null;
  // 04-C (O-04-48) : un collaborateur cité mais plus habilité reste sélectionnable pour une rectification (le serveur
  // admet le collaborateur inchangé), signalé comme tel ; la liste active seule l'aurait fait disparaître du champ.
  const measurerOptions = knownActors(
    measurers.data ?? [],
    (record?.citedUsers ?? []).map((actor) => (measurers.isSuccess ? { ...actor, displayName: `${actor.displayName} (n'est plus habilité à mesurer)` } : actor)),
  );
  const [clientName, clientReference = ""] = props.mode === "create" ? props.client.label.split(" · ") : [props.record.client.displayName, props.record.client.reference];
  const step = useContext(OpticalStepContext);
  const formId = useId();
  const initial: RecordContent = draft?.content ?? emptyContent(kind);

  const {
    register,
    control,
    handleSubmit,
    reset,
    getValues,
    formState: { isDirty },
  } = useForm<RecordContent>({ defaultValues: initial });

  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<FailureView | null>(null);
  const [leaveRequest, setLeaveRequest] = useState<(() => void) | null>(null);
  const [reloadAsk, setReloadAsk] = useState(false);
  const [done, setDone] = useState(false);
  // Une seule opération à la fois : double clic, Ctrl+S répété ou Entrée pendant l'enregistrement sont ignorés (aucun
  // doublon ; le serveur reconnaît en outre la réémission d'une même demande).
  const busyRef = useRef(false);
  const [busy, setBusy] = useState(false);
  const saving = busy || create.isPending || save.isPending || createConfirm.isPending || saveConfirm.isPending;
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

  const showFailure = (error: ApiError, view: FailureView = describeOpticalFailure(error)) => {
    const fields = toFieldMessages(error);
    setFieldErrors(fields);
    setFailure(view);
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

  const exclusive = async (run: () => Promise<void>) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    try {
      await run();
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  const draftSaved = (saved: OpticalRecord) =>
    finish(saved, "Brouillon enregistré", `${KIND_LABEL[saved.kind]} ${saved.reference} : brouillon non confirmé, consultable et modifiable.`);

  // `handleSubmit` appelé au clic seulement (la garde `busyRef` n'est jamais lue pendant le rendu).
  const onSaveDraft = () =>
    handleSubmit((values) =>
      exclusive(async () => {
        const saved = await persist(values);
        if (saved) draftSaved(saved);
      }),
    )();

  // Action principale : enregistrer ET confirmer en une opération atomique (critère décidé par le service).
  const onSave = () =>
    handleSubmit((values) =>
      exclusive(async () => {
        setFailure(null);
        setFieldErrors({});
        try {
          const confirmed =
            record === null
              ? props.mode === "create"
                ? await createConfirm.mutateAsync({ clientId: props.client.id, content: values })
                : null
              : await saveConfirm.mutateAsync({ kind: record.kind, id: record.id, content: values, version: record.version });
          if (confirmed === null) return;
          const revisionNo = confirmed.lastConfirmed?.revisionNo ?? 1;
          finish(confirmed, `${KIND_LABEL[confirmed.kind]} enregistrée`, `${confirmed.reference} : révision ${revisionNo} confirmée.`);
        } catch (error) {
          // Rien n'a été enregistré (refus) ou l'issue est incertaine (réseau) : saisie conservée à l'écran ; pour un
          // brouillon existant, l'état réel est relu (version) avant de proposer une nouvelle tentative.
          const refused = (error as { error: ApiError }).error;
          let view = describeSaveFailure(refused);
          if (record !== null && ["network", "timeout", "http", "invalid-response"].includes(refused.kind)) {
            const latest = await gateway.getRecord(record.kind, record.id);
            if (latest.ok && latest.data.version !== record.version) view = { ...view, reload: true, text: `${view.text} L'enregistrement a changé sur le serveur : rechargez-le pour vérifier son état.` };
          }
          showFailure(refused, view);
        }
      }),
    )();

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

  // Ctrl+S = action principale « Enregistrer » (jamais le brouillon).
  const submitRef = useRef(onSave);
  useEffect(() => {
    submitRef.current = onSave;
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
        ? `Modifier — ${KIND_LABEL[kind].toLocaleLowerCase("fr")}`
        : `Brouillon — ${KIND_LABEL[kind].toLocaleLowerCase("fr")}`;
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;

  return (
    <section className="clients-view optical-formpage" aria-labelledby="optical-form-title">
      <header className="clients-head optical-formhead">
        <div>
          <div className="optical-title-line">
            <h1 id="optical-form-title" className="clients-head__title">
              {title}
            </h1>
            <KindBadge kind={kind} />
            <span className="clients-badge optical-kbd" aria-hidden="true">
              <Command size={12} /> Ctrl + S
            </span>
          </div>
          <p className="clients-head__sub">
            {kind === "prescription" ? "Rattachée à un client existant" : "Rattachée à un client existant · mesure réalisée au magasin, distincte d'une ordonnance"}
            {record && <> · {record.reference}</>}
          </p>
          {draft?.baseRevisionNo && draft.reasonCode && (
            <p className="clients-head__hint" role="note">
              Motif : {RECTIFICATION_REASON_LABEL[draft.reasonCode]}
              {draft.reasonComment && ` — ${draft.reasonComment}`}. La dernière révision confirmée (révision {draft.baseRevisionNo}) reste affichée jusqu&apos;à
              l&apos;enregistrement.
            </p>
          )}
        </div>
        <div className="clients-actions clients-actions--wrap">
          <Button onClick={leave} disabled={saving}>
            <X size={16} aria-hidden="true" />
            Annuler
          </Button>
          <Button variant="tertiary" onClick={() => void onSaveDraft()} disabled={saving}>
            <FilePen size={16} aria-hidden="true" />
            Enregistrer comme brouillon
          </Button>
          <Button variant="primary" type="submit" form={formId} disabled={saving} aria-keyshortcuts="Control+S">
            <Check size={16} aria-hidden="true" />
            {saving ? "Enregistrement…" : "Enregistrer"}
          </Button>
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
            <div className="clients-actions clients-actions--wrap">
              {failure.reload && record && (
                <Button onClick={() => setReloadAsk(true)}>
                  <RefreshCw size={16} aria-hidden="true" />
                  Recharger
                </Button>
              )}
            </div>
          )}
        </div>
      )}

      <form
        id={formId}
        ref={formRef}
        className="optical-form"
        onSubmit={(event) => {
          event.preventDefault();
          void onSave();
        }}
        noValidate
      >
        <fieldset className="optical-form__body" disabled={saving}>
          <FormSection card icon={<User size={16} />} title="Informations générales">
            <div className="optical-client" role="group" aria-label="Client concerné">
              <span className="optical-client__avatar" aria-hidden="true">
                {initials(clientName)}
              </span>
              <div className="optical-client__main">
                <div className="optical-client__name">{clientName}</div>
                <div className="optical-client__sub">
                  {clientReference && <>{clientReference} · </>}
                  {kind === "prescription" ? "Client de l'ordonnance" : "Client mesuré"}
                </div>
              </div>
              <Button variant="tertiary" size="sm" onClick={leave}>
                <ArrowLeft size={14} aria-hidden="true" />
                {props.launch?.returnTo.startsWith("/clients/") ? "Fiche client" : props.launch ? `Retour — ${props.launch.returnLabel}` : "Retour"}
              </Button>
            </div>
            {kind === "prescription" ? (
              <PrescriptionProvenanceFields register={register} control={control} errors={fieldErrors} reference={record?.reference} />
            ) : (
              <MeasurementProvenanceFields register={register} control={control} errors={fieldErrors} measurers={measurerOptions} reference={record?.reference} />
            )}
          </FormSection>

          <FormSection
            icon={<Glasses size={16} />}
            title="Correction optique"
            aside={
              <>
                <Info size={14} aria-hidden="true" />
                Boutons ± : pas de {stepLabel(step)} (Sphère, Cylindre, Addition) · ↑ / ↓ au clavier · saisie libre conservée telle quelle · Prisme en saisie libre
              </>
            }
          >
            <EyeGrid control={control} errors={fieldErrors} />
          </FormSection>

          <FormSection card icon={<NotebookPen size={16} />} title="Notes">
            <NotesField register={register} error={fieldErrors.notes} />
          </FormSection>
        </fieldset>
      </form>

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

/** Section de la maquette : titre à pastille d'icône (30 px, accent atténué), dans une carte ou à nu (Correction optique). */
function FormSection({ icon, title, aside, card = false, children }: { icon: ReactNode; title: string; aside?: ReactNode; card?: boolean; children: ReactNode }) {
  const id = useId();
  return (
    <section className={cn("optical-form__section", card && "optical-form__card")} aria-labelledby={id}>
      <div className="optical-form__title">
        <span className="optical-form__icon" aria-hidden="true">
          {icon}
        </span>
        <h2 id={id}>{title}</h2>
        {aside && <p className="optical-form__aside">{aside}</p>}
      </div>
      {children}
    </section>
  );
}

const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0])
    .join("")
    .toLocaleUpperCase("fr");

/** Référence en lecture seule (maquette : « Attribuée à l'enregistrement »). */
function ReferenceField({ reference }: { reference?: string }) {
  const id = useId();
  return (
    <div className="clients-field">
      <span className="clients-field__label" id={id}>
        Référence
      </span>
      <div className="clients-input optical-readonly" role="group" aria-labelledby={id}>
        {reference ?? "Attribuée à l'enregistrement"}
      </div>
    </div>
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

// Grille de la maquette (genInfo) : trois colonnes, Date · Prescripteur · Référence en première ligne. Aucun champ
// marqué obligatoire (la maquette marquait la date ; D-04-03.a non ratifiée).
function PrescriptionProvenanceFields({ register, control, errors, reference }: FieldProps & { reference?: string }) {
  return (
    <div className="optical-geninfo">
      <DateField label="Date de prescription" name="provenance.prescribedOn" control={control} error={errors["provenance.prescribedOn"]} hint="Date portée sur le document. Aucune validité n'est calculée." />
      <TextField label="Prescripteur — nom (tel que sur le document)" name="provenance.prescriberName" register={register} error={errors["provenance.prescriberName"]} />
      <ReferenceField reference={reference} />
      <TextField
        label="Prescripteur — qualité"
        name="provenance.prescriberQualification"
        register={register}
        error={errors["provenance.prescriberQualification"]}
        hint="Facultatif, texte libre (aucune liste imposée)."
      />
      <TextField
        label="Prescripteur — identifiant professionnel"
        name="provenance.prescriberIdentifier"
        register={register}
        error={errors["provenance.prescriberIdentifier"]}
        hint="Tel qu'il figure sur le document ; aucun contrôle de format ni de registre."
      />
      <DateField label="Date de réception au magasin" name="provenance.receivedOn" control={control} error={errors["provenance.receivedOn"]} hint="Distincte de la date de prescription (facultative)." />
    </div>
  );
}

function MeasurementProvenanceFields({ register, control, errors, measurers, reference }: FieldProps & { measurers: readonly { id: string; displayName: string }[]; reference?: string }) {
  const id = useId();
  const error = errors["provenance.measuredByUserId"];
  return (
    <div className="optical-geninfo">
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
          Comptes habilités à mesurer ; l&apos;auteur de la saisie est tracé séparément par le serveur.
        </span>
        {error && (
          <span id={`${id}-error`} className="clients-field__error" role="alert">
            {error}
          </span>
        )}
      </div>
      <ReferenceField reference={reference} />
      <TextField label="Catégorie de mesure" name="provenance.category" register={register} error={errors["provenance.category"]} hint="Facultatif, texte libre (aucune liste imposée)." />
      <p className="optical-geninfo__note">
        <Ruler size={14} aria-hidden="true" />
        Mesure réalisée au magasin par un collaborateur : ce n&apos;est pas une prescription délivrée par un médecin (D-04-01).
      </p>
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
