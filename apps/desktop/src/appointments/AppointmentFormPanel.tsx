// Panneau de saisie d'un rendez-vous (Lot 2 DRAFT, planches 35, 36, 40–42) : création, modification et
// DÉPLACEMENT AU CLAVIER (mode `move` : seules la date et l'heure changent). React Hook Form + Zod
// (appointmentForm.ts) ; listes (durées, motifs, collaborateurs éligibles) servies par MMV.Api ; le champ
// Responsable suit le mode du motif (D-03-14 : personnalisé → obligatoire ; accueil → facultatif si une capacité
// d'accueil est configurée) ; erreurs serveur par champ sous les champs ; conflit horaire, capacité d'accueil
// atteinte et autres refus 409 en bandeau. Échap ferme le panneau (confirmation si des modifications ne sont pas
// enregistrées, y compris depuis le menu du Shell). Rendez-vous commencé (B2-REV-02, 03-B3) : la modification ne
// présente que le motif et les notes, la réservation (client, date, heure, durée, responsable) étant figée.
// 03-D1 : date choisie dans un calendrier (dates passées du magasin désactivées), heure dans une liste au pas serveur
// couvrant toute la journée (heures passées, inexistantes ou ambiguës désactivées avec leur motif) ; aucune saisie
// manuelle ; valeurs inchangées dans le formulaire (`jj/mm/aaaa`, `hh:mm`), le serveur reste seul juge.
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, AlertTriangle, ArrowLeft, Check, Info, Search, Stethoscope, Timer, User, X } from "lucide-react";
import { type KeyboardEvent, type ReactNode, type Ref, useEffect, useId, useMemo, useRef, useState } from "react";
import { Controller, type FieldErrors, useForm, type UseFormRegisterReturn, useWatch } from "react-hook-form";
import { Temporal } from "temporal-polyfill";
import type { AppointmentDetail, AppointmentOptions } from "../api/appointments.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { clientDisplayName, formatIsoDate, parseFrenchDate } from "../clients/clientForm.ts";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { Button } from "../components/ui/button.tsx";
import { DatePicker } from "../components/ui/date-picker.tsx";
import { Label } from "../components/ui/label.tsx";
import { TimePicker } from "../components/ui/time-picker.tsx";
import { cn } from "../lib/utils.ts";
import { useLeaveGuard } from "../shell/navigationGuard.ts";
import {
  type AppointmentFormValues,
  appointmentFormSchema,
  appointmentServerFieldErrors,
  responsibleMode,
  toAppointmentInput,
} from "./appointmentForm.ts";
import { useCreateAppointment, useUpdateAppointment } from "./appointmentQueries.ts";
import { useAppointmentsService } from "./appointmentsService.ts";
import { describeAppointmentFailure, type FailureView, formatWhen, hasStarted, reasonLabel } from "./appointmentsView.ts";
import { ClientPicker } from "./ClientPicker.tsx";
import { addMinutesUtc, formatDuration, formatTimeRange, startTimeOptions, todayIn } from "./time.ts";

/** Navigation du calendrier : années proposées au-delà de l'année en cours (affichage seulement, aucune règle). */
const CALENDAR_YEARS_AHEAD = 5;

export type FormMode = "create" | "edit" | "move";

interface AppointmentFormPanelProps {
  mode: FormMode;
  options: AppointmentOptions;
  initialValues: AppointmentFormValues;
  /** Rendez-vous modifié ou déplacé (version figée à l'ouverture, D-03-06 par analogie). */
  appointment?: AppointmentDetail;
  canPickClient: boolean;
  /** Prise de rendez-vous depuis une fiche client : libellé du retour (fermer = revenir à la fiche). */
  returnLabel?: string;
  onClose: () => void;
  onSaved: (appointment: AppointmentDetail, mode: FormMode) => void;
}

const TITLES: Record<FormMode, string> = { create: "Nouveau rendez-vous", edit: "Modifier le rendez-vous", move: "Déplacer le rendez-vous" };
const SUBMIT: Record<FormMode, string> = { create: "Créer", edit: "Enregistrer", move: "Déplacer" };
const FIELD_ORDER: (keyof AppointmentFormValues)[] = ["clientId", "date", "time", "durationMinutes", "responsibleUserId", "reasonCode", "notes"];

export function AppointmentFormPanel({ mode, options, initialValues, appointment, canPickClient, returnLabel, onClose, onSaved }: AppointmentFormPanelProps) {
  const create = useCreateAppointment();
  const update = useUpdateAppointment();
  const { now } = useAppointmentsService();
  const [editing] = useState(appointment ?? null);
  const schema = useMemo(() => appointmentFormSchema(options, editing?.responsibleUserId), [options, editing]);
  // Rendez-vous commencé : seuls le motif et les notes se corrigent (le serveur refuse le reste : 409 appointment_past).
  const [locked] = useState(() => mode === "edit" && editing !== null && hasStarted(editing.startsAtUtc, now));
  // Responsable actuel qui n'est plus habilité : proposé tel quel (motif et notes restent corrigeables).
  const formerResponsible =
    editing?.responsibleUserId && !options.responsibles.some((person) => person.id === editing.responsibleUserId)
      ? { id: editing.responsibleUserId, displayName: `${editing.responsibleDisplayName ?? "Collaborateur"} (n'est plus habilité)` }
      : null;
  // Sans responsable, un rendez-vous commencé reste un rendez-vous d'accueil : motifs d'accueil seulement.
  const reasonChoices = locked && editing?.responsibleUserId === null ? options.reasons.filter((reason) => !reason.responsibleRequired) : options.reasons;
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    setValue,
    getValues,
    control,
    formState: { errors, isDirty, isSubmitSuccessful },
  } = useForm<AppointmentFormValues>({
    resolver: zodResolver(schema),
    defaultValues: initialValues,
    mode: "onSubmit",
    reValidateMode: "onChange",
    // Le focus du premier champ en erreur est géré ici (le client est un bouton, pas un champ enregistré).
    shouldFocusError: false,
  });
  const [picking, setPicking] = useState(false);
  const [failure, setFailure] = useState<FailureView | null>(null);
  const [leaveRequest, setLeaveRequest] = useState<(() => void) | null>(null);
  const clientButton = useRef<HTMLButtonElement>(null);
  const saving = create.isPending || update.isPending;
  const clientLabel = useWatch({ control, name: "clientLabel" });
  const clientId = useWatch({ control, name: "clientId" });
  const reasonCode = useWatch({ control, name: "reasonCode" });
  const civilDate = parseFrenchDate(useWatch({ control, name: "date" }) ?? "");
  const nowInstant = now ? Temporal.Instant.fromEpochMilliseconds(now().getTime()) : Temporal.Now.instant();
  const today = todayIn(options.timeZone, nowInstant);
  const nowMinute = Math.floor(nowInstant.epochMilliseconds / 60_000);
  // Recalculé quand la date ou la minute courante changent (le serveur revérifie à l'enregistrement).
  const timeOptions = useMemo(
    () => startTimeOptions(civilDate, options.startStepMinutes, options.timeZone, Temporal.Instant.fromEpochMilliseconds(nowMinute * 60_000)),
    [civilDate, options.startStepMinutes, options.timeZone, nowMinute],
  );
  const attribution = responsibleMode(options, reasonCode);
  const responsibleHint =
    attribution === "optional"
      ? `Rendez-vous d'accueil : sans responsable, il occupe une place d'accueil (${options.receptionCapacity} simultanée${options.receptionCapacity === 1 ? "" : "s"} au plus).`
      : attribution === "reception-unavailable"
        ? "Aucune capacité d'accueil n'est configurée : un responsable est nécessaire."
        : attribution === "required"
          ? "Rendez-vous personnalisé : responsable obligatoire."
          : "Choisissez d'abord le motif.";

  const dirtyRef = useRef(false);
  useEffect(() => {
    dirtyRef.current = isDirty && !isSubmitSuccessful;
  });
  useLeaveGuard((proceed) => {
    if (!dirtyRef.current) return false;
    setLeaveRequest(() => proceed);
    return true;
  });
  const requestClose = () => (dirtyRef.current ? setLeaveRequest(() => onClose) : onClose());

  const focusField = (field: keyof AppointmentFormValues) => {
    if (field === "clientId" || field === "clientLabel") clientButton.current?.focus();
    else setFocus(field);
  };

  const onInvalid = (invalid: FieldErrors<AppointmentFormValues>) => {
    const first = FIELD_ORDER.find((field) => invalid[field]);
    if (first) focusField(first);
  };

  const onValid = (values: AppointmentFormValues) => {
    setFailure(null);
    const input = toAppointmentInput(values, options);
    const slot = { startsAtUtc: input.startsAtUtc, endsAtUtc: addMinutesUtc(input.startsAtUtc, input.durationMinutes), timeZone: options.timeZone };
    const onError = (error: ApiRequestError) => {
      const fields = appointmentServerFieldErrors(error.error.fieldErrors);
      // Champs affichés : motif et notes (réservation figée), date et heure (déplacement), sinon tous.
      const shown: readonly string[] | null = locked ? ["reasonCode", "notes"] : mode === "move" ? ["date", "time"] : null;
      const hidden = shown ? fields.find(([field]) => !shown.includes(field)) : undefined;
      if (error.error.kind === "validation" && hidden) {
        // Champ non affiché (réservation figée) : message en bandeau plutôt que sous un champ invisible.
        setFailure({ tone: "error", title: "Données refusées", text: `${hidden[1]}.` });
        return;
      }
      if (error.error.kind === "validation" && fields.length > 0) {
        fields.forEach(([field, message]) => setError(field, { type: "server", message }));
        focusField(fields[0][0]);
        return;
      }
      setFailure(describeAppointmentFailure(error, slot));
    };
    const onSuccess = (saved: AppointmentDetail) => {
      dirtyRef.current = false;
      onSaved(saved, mode);
    };
    if (editing) update.mutate({ id: editing.id, input, version: editing.version }, { onSuccess, onError });
    else create.mutate(input, { onSuccess, onError });
  };

  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === "Escape" && !picking && leaveRequest === null) {
      event.preventDefault();
      requestClose();
    }
  };

  const move = mode === "move";
  const currentDuration = getValues("durationMinutes");

  return (
    <aside className="rdv-side rdv-side--form" aria-label={picking ? "Sélectionner un client" : TITLES[mode]} onKeyDown={onKeyDown}>
      {picking ? (
        <ClientPicker
          allowed={canPickClient}
          selectedId={clientId}
          onBack={() => {
            setPicking(false);
            setTimeout(() => clientButton.current?.focus(), 0);
          }}
          onClose={requestClose}
          onPick={(client) => {
            setValue("clientId", client.id, { shouldDirty: true, shouldValidate: errors.clientId !== undefined });
            setValue("clientLabel", `${clientDisplayName(client)} · ${client.reference}`, { shouldDirty: true });
            setPicking(false);
            setTimeout(() => clientButton.current?.focus(), 0);
          }}
        />
      ) : (
        <form className="rdv-side__form" noValidate onSubmit={(event) => void handleSubmit(onValid, onInvalid)(event)} aria-busy={saving}>
          <div className="rdv-side__head">
            <h2 className="rdv-side__title">{TITLES[mode]}</h2>
            <button type="button" className="rdv-iconbtn" aria-label="Fermer le panneau" onClick={requestClose}>
              <X size={18} aria-hidden="true" />
            </button>
          </div>
          {failure && (
            <div className={cn("clients-banner rdv-side__banner", `clients-banner--${failure.tone}`)} role="alert">
              <span className="clients-banner__icon" aria-hidden="true">
                <AlertTriangle size={18} />
              </span>
              <div>
                <div className="clients-banner__title">{failure.title}</div>
                <div className="clients-banner__text">{failure.text}</div>
              </div>
            </div>
          )}
          <div className="rdv-side__body rdv-form">
            {returnLabel && (
              <div>
                <Button variant="tertiary" size="sm" className="clients-back" onClick={requestClose}>
                  <ArrowLeft size={16} aria-hidden="true" />
                  {returnLabel}
                </Button>
              </div>
            )}
            {(move || locked) && editing ? (
              <div className="rdv-form__summary">
                <p>
                  <b>{clientLabel}</b>
                </p>
                <p>
                  {locked ? "Commencé" : "Actuellement"} : {formatWhen(editing.startsAtUtc, options.timeZone)} (
                  {formatTimeRange(editing.startsAtUtc, editing.endsAtUtc, options.timeZone)})
                </p>
                <p>
                  {formatDuration(Number(currentDuration))} · {reasonLabel(options, editing.reasonCode)}
                  {editing.responsibleDisplayName ? ` · ${editing.responsibleDisplayName}` : " · Non attribué"}
                </p>
                {locked && (
                  <p className="rdv-form__locked" role="note">
                    Rendez-vous commencé : seuls le motif et les notes peuvent être corrigés.
                  </p>
                )}
              </div>
            ) : (
              <ClientField
                buttonRef={clientButton}
                label={clientLabel}
                error={errors.clientId?.message}
                disabled={!canPickClient}
                onOpen={() => setPicking(true)}
              />
            )}
            {!locked && (
              <div className="rdv-form__row">
                <Controller
                  control={control}
                  name="date"
                  render={({ field }) => (
                    <PickerField label="Date" error={errors.date?.message}>
                      {(ids) => (
                        <DatePicker
                          {...ids}
                          buttonRef={field.ref}
                          autoFocus={move}
                          value={parseFrenchDate(field.value) ?? ""}
                          onChange={(civil) => field.onChange(formatIsoDate(civil))}
                          onBlur={field.onBlur}
                          today={today}
                          minDate={today}
                          maxYear={Number(today.slice(0, 4)) + CALENDAR_YEARS_AHEAD}
                          invalid={errors.date !== undefined}
                          required
                        />
                      )}
                    </PickerField>
                  )}
                />
                <Controller
                  control={control}
                  name="time"
                  render={({ field }) => (
                    <PickerField label="Heure" error={errors.time?.message}>
                      {(ids) => (
                        <TimePicker
                          {...ids}
                          buttonRef={field.ref}
                          value={field.value}
                          onChange={field.onChange}
                          onBlur={field.onBlur}
                          options={timeOptions}
                          initialFocus={options.displayStartTime}
                          invalid={errors.time !== undefined}
                          required
                          footnote="Disponibilité vérifiée par le serveur à l'enregistrement."
                        />
                      )}
                    </PickerField>
                  )}
                />
              </div>
            )}
            {!move && (
              <>
                {!locked && (
                <div className="rdv-form__row">
                  <SelectField label="Durée" required icon={<Timer size={18} />} error={errors.durationMinutes?.message} registration={register("durationMinutes")}>
                    <option value="">Choisir…</option>
                    {options.durationsMinutes.map((minutes) => (
                      <option key={minutes} value={String(minutes)}>
                        {formatDuration(minutes)}
                      </option>
                    ))}
                  </SelectField>
                  <SelectField
                    label="Responsable"
                    required={attribution !== "optional"}
                    icon={<User size={18} />}
                    error={errors.responsibleUserId?.message}
                    hint={responsibleHint}
                    registration={register("responsibleUserId")}
                  >
                    <option value="">{attribution === "optional" ? "Non attribué (accueil)" : "Choisir…"}</option>
                    {[...options.responsibles, ...(formerResponsible ? [formerResponsible] : [])].map((person) => (
                      <option key={person.id} value={person.id}>
                        {person.displayName}
                      </option>
                    ))}
                  </SelectField>
                </div>
                )}
                <SelectField label="Motif" required icon={<Stethoscope size={18} />} error={errors.reasonCode?.message} registration={register("reasonCode")}>
                  <option value="">Choisir un motif…</option>
                  {reasonChoices.map((reason) => (
                    <option key={reason.code} value={reason.code}>
                      {reason.label}
                    </option>
                  ))}
                </SelectField>
                <Field
                  label="Notes"
                  multiline
                  error={errors.notes?.message}
                  hint="Notes internes, sans donnée de santé (1000 caractères au maximum)."
                  registration={register("notes")}
                  placeholder="Notes internes (optionnel)…"
                  maxLength={1000}
                />
              </>
            )}
            <p className="rdv-form__tz">
              <Info size={14} aria-hidden="true" /> Heures du magasin ({options.timeZone}).
            </p>
          </div>
          <div className="rdv-side__foot">
            <Button className="rdv-grow" onClick={requestClose} disabled={saving}>
              Annuler
            </Button>
            <Button type="submit" variant="primary" className="rdv-grow" disabled={saving}>
              <Check size={16} aria-hidden="true" />
              {saving ? "Enregistrement…" : SUBMIT[mode]}
            </Button>
          </div>
        </form>
      )}
      <ConfirmDialog
        open={leaveRequest !== null}
        onOpenChange={(open) => !open && setLeaveRequest(null)}
        tone="warning"
        icon={<AlertTriangle size={28} />}
        title="Abandonner la saisie ?"
        description="Les informations saisies pour ce rendez-vous ne sont pas enregistrées et seront perdues."
        cancelLabel="Continuer la saisie"
        confirmLabel="Abandonner"
        onConfirm={() => {
          const proceed = leaveRequest;
          dirtyRef.current = false;
          setLeaveRequest(null);
          proceed?.();
        }}
      />
    </aside>
  );
}

function ClientField({
  buttonRef,
  label,
  error,
  disabled,
  onOpen,
}: {
  buttonRef: Ref<HTMLButtonElement>;
  label: string;
  error?: string;
  disabled: boolean;
  onOpen: () => void;
}) {
  const id = useId();
  return (
    <div className="clients-field">
      <span className="clients-field__label" id={`${id}-label`} data-required>
        Client
      </span>
      <button
        ref={buttonRef}
        type="button"
        className={cn("clients-input rdv-input--button", error && "clients-input--error", !label && "rdv-input--placeholder")}
        aria-labelledby={`${id}-label ${id}-value`}
        aria-describedby={error ? `${id}-error` : disabled ? `${id}-hint` : undefined}
        aria-invalid={error ? true : undefined}
        aria-required
        aria-haspopup="listbox"
        disabled={disabled}
        onClick={onOpen}
      >
        <span className="clients-input__icon" aria-hidden="true">
          <User size={18} />
        </span>
        <span id={`${id}-value`} className="rdv-grow rdv-input__value">
          {label || "Rechercher un client…"}
        </span>
        <Search size={16} aria-hidden="true" />
      </button>
      {error ? (
        <div className="clients-field__error" id={`${id}-error`}>
          <AlertCircle size={14} aria-hidden="true" />
          {error}
        </div>
      ) : disabled ? (
        <div className="clients-field__hint" id={`${id}-hint`}>
          Votre compte ne permet pas de rechercher des clients (clients.read).
        </div>
      ) : null}
    </div>
  );
}

/** Libellé et erreur d'un sélecteur (bouton déclencheur nommé par le libellé puis la valeur affichée). */
function PickerField({ label, error, children }: { label: string; error?: string; children: (ids: { labelledBy: string; describedBy?: string }) => ReactNode }) {
  const id = useId();
  return (
    <div className="clients-field">
      <span className="clients-field__label" id={`${id}-label`} data-required>
        {label}
      </span>
      {children({ labelledBy: `${id}-label`, describedBy: error ? `${id}-error` : undefined })}
      {error && (
        <div className="clients-field__error" id={`${id}-error`}>
          <AlertCircle size={14} aria-hidden="true" />
          {error}
        </div>
      )}
    </div>
  );
}

interface FieldProps {
  label: string;
  registration: UseFormRegisterReturn;
  error?: string;
  hint?: string;
  required?: boolean;
  icon?: ReactNode;
  multiline?: boolean;
  placeholder?: string;
  maxLength?: number;
  autoFocus?: boolean;
}

function Field({ label, registration, error, hint, required, icon, multiline, placeholder, maxLength, autoFocus }: FieldProps) {
  const id = useId();
  const common = {
    id,
    ...registration,
    placeholder,
    maxLength,
    autoFocus,
    autoComplete: "off",
    "aria-invalid": error ? true : undefined,
    "aria-describedby": error ? `${id}-error` : hint ? `${id}-hint` : undefined,
    "aria-required": required || undefined,
  };
  return (
    <div className="clients-field">
      <Label htmlFor={id} className="clients-field__label" data-required={required || undefined}>
        {label}
      </Label>
      <div className={cn("clients-input", multiline && "clients-input--area", error && "clients-input--error")}>
        {icon && <span className="clients-input__icon" aria-hidden="true">{icon}</span>}
        {multiline ? <textarea rows={4} {...common} /> : <input type="text" spellCheck={false} {...common} />}
      </div>
      {error ? (
        <div className="clients-field__error" id={`${id}-error`}>
          <AlertCircle size={14} aria-hidden="true" />
          {error}
        </div>
      ) : hint && multiline ? (
        <div className="clients-field__hint" id={`${id}-hint`}>
          {hint}
        </div>
      ) : hint ? (
        <span className="rdv-sr-only" id={`${id}-hint`}>
          Format {hint}
        </span>
      ) : null}
    </div>
  );
}

function SelectField({
  label,
  registration,
  error,
  hint,
  required,
  icon,
  children,
}: {
  label: string;
  registration: UseFormRegisterReturn;
  error?: string;
  hint?: string;
  required?: boolean;
  icon: ReactNode;
  children: ReactNode;
}) {
  const id = useId();
  return (
    <div className="clients-field">
      <Label htmlFor={id} className="clients-field__label" data-required={required || undefined}>
        {label}
      </Label>
      <div className={cn("clients-input rdv-select", error && "clients-input--error")}>
        <span className="clients-input__icon" aria-hidden="true">{icon}</span>
        <select
          id={id}
          {...registration}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          aria-required={required || undefined}
        >
          {children}
        </select>
      </div>
      {error ? (
        <div className="clients-field__error" id={`${id}-error`}>
          <AlertCircle size={14} aria-hidden="true" />
          {error}
        </div>
      ) : hint ? (
        <div className="clients-field__hint" id={`${id}-hint`}>
          {hint}
        </div>
      ) : null}
    </div>
  );
}

