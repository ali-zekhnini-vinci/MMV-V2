// Formulaire unique de création / modification d'un client (Lot 2 DRAFT, planches 10–15). React Hook Form +
// Zod (règles D-03-02 de clientForm.ts), libellés toujours visibles, focus sur le premier champ en erreur,
// Ctrl+S pour enregistrer, confirmation avant de quitter avec des modifications non enregistrées (bouton
// Annuler et menu du Shell). Le serveur revalide tout ; ses erreurs par champ sont reportées sous les champs.
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, AlertTriangle, ArrowLeft, Calendar, Check, Command, Info, LogOut, Mail, MapPin, Phone, Save, Shield, User, UserCheck } from "lucide-react";
import { type ReactNode, useCallback, useEffect, useId, useRef, useState } from "react";
import { type FieldErrors, useForm, type UseFormRegisterReturn } from "react-hook-form";
import { useNavigate } from "react-router";
import type { ClientDetail } from "../api/clients.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { Button } from "../components/ui/button.tsx";
import { Label } from "../components/ui/label.tsx";
import { cn } from "../lib/utils.ts";
import { useLeaveGuard } from "../shell/navigationGuard.ts";
import {
  CLIENT_LIMITS,
  clientDisplayName,
  clientFormSchema,
  type ClientFormValues,
  clientInitials,
  EMPTY_CLIENT_FORM,
  serverFieldErrors,
  toClientInput,
  toFormValues,
} from "./clientForm.ts";
import { describeWriteFailure } from "./ClientLifecycle.tsx";
import { useCreateClient, useUpdateClient } from "./clientQueries.ts";
import { useClientsService } from "./clientsService.ts";
import { useClientsUi } from "./clientsUi.ts";

type ClientFormViewProps = { mode: "create" } | { mode: "edit"; client: ClientDetail };

const FIELD_ORDER: (keyof ClientFormValues)[] = ["firstName", "lastName", "birthDate", "phone", "email", "addressLine", "city", "postalCode", "notes"];

export function ClientFormView(props: ClientFormViewProps) {
  const navigate = useNavigate();
  const { enabled } = useClientsService();
  const { setNotice } = useClientsUi();
  const create = useCreateClient();
  const update = useUpdateClient();
  // Version lue à l'ouverture du formulaire, figée : si un autre poste modifie la fiche entre-temps, même
  // après un rafraîchissement du cache, l'enregistrement est refusé (409) au lieu d'écraser ses changements.
  const [editing] = useState<ClientDetail | null>(props.mode === "edit" ? props.client : null);

  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    reset,
    formState: { errors, isDirty, isSubmitted, isSubmitSuccessful },
  } = useForm<ClientFormValues>({
    resolver: zodResolver(clientFormSchema),
    defaultValues: editing ? toFormValues(editing) : EMPTY_CLIENT_FORM,
    mode: "onSubmit",
    reValidateMode: "onChange",
  });

  const [leaveRequest, setLeaveRequest] = useState<(() => void) | null>(null);
  const [failure, setFailure] = useState<{ title: string; text: string; conflict: boolean } | null>(null);
  const [created, setCreated] = useState<ClientDetail | null>(null);
  const saving = create.isPending || update.isPending;
  const hasErrors = Object.keys(errors).length > 0;
  const backTo = editing ? `/clients/${editing.id}` : "/clients";

  // Garde de sortie (menu du Shell) : intercepte seulement s'il existe des modifications non enregistrées.
  const dirtyRef = useRef(isDirty);
  useEffect(() => {
    dirtyRef.current = isDirty && !isSubmitSuccessful;
  });
  useLeaveGuard((proceed) => {
    if (!dirtyRef.current) return false;
    setLeaveRequest(() => proceed);
    return true;
  });

  const leave = () => {
    if (dirtyRef.current) setLeaveRequest(() => () => navigate(backTo));
    else navigate(backTo);
  };

  const onInvalid = (invalid: FieldErrors<ClientFormValues>) => {
    const first = FIELD_ORDER.find((field) => invalid[field]);
    if (first) setFocus(first);
  };

  const onValid = (values: ClientFormValues) => {
    if (!enabled) return;
    const input = toClientInput(values);
    const onError = (error: ApiRequestError) => {
      const fields = serverFieldErrors(error.error.fieldErrors);
      if (error.error.kind === "validation" && fields.length > 0) {
        fields.forEach(([field, message]) => setError(field, { type: "server", message }));
        setFocus(fields[0][0]);
        return;
      }
      const view = describeWriteFailure(error);
      setFailure({ title: view.title, text: view.text, conflict: error.error.kind === "conflict" });
    };
    if (editing) {
      update.mutate(
        { id: editing.id, input, version: editing.version },
        {
          onSuccess: (client) => {
            reset(toFormValues(client));
            setNotice(
              { title: "Modifications enregistrées", message: `La fiche de ${client.firstName} ${client.lastName} est à jour.` },
              `/clients/${client.id}`,
            );
            navigate(`/clients/${client.id}`);
          },
          onError,
        },
      );
    } else {
      create.mutate(input, {
        onSuccess: (client) => {
          reset(values);
          setCreated(client);
        },
        onError,
      });
    }
  };

  const submit = handleSubmit(onValid, onInvalid);
  const submitRef = useRef(submit);
  useEffect(() => {
    submitRef.current = submit;
  });

  // Ctrl+S : enregistrer depuis n'importe quel champ (raccourci affiché dans l'en-tête).
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

  if (created) {
    return <CreatedPanel client={created} onOpen={() => navigate(`/clients/${created.id}`)} onCreateAnother={() => {
      reset(EMPTY_CLIENT_FORM);
      setCreated(null);
    }} />;
  }

  const title = editing ? "Modifier le client" : "Nouveau client";
  const subtitle = editing ? `${clientDisplayName(editing)} · ${editing.reference}` : "Renseignez au minimum le prénom et le nom";

  return (
    <section className="clients-view" aria-labelledby="client-form-title">
      <header className="clients-head">
        <div>
          <h1 id="client-form-title" className="clients-head__title">
            {title}
            <span className="clients-badge clients-badge--info clients-head__kbd">
              <Command size={12} aria-hidden="true" />
              Ctrl + S
            </span>
          </h1>
          <p className="clients-head__sub">{subtitle}</p>
        </div>
        <div className="clients-actions">
          <Button onClick={leave} disabled={saving}>
            <ArrowLeft size={16} aria-hidden="true" />
            Annuler
          </Button>
          <Button variant="primary" onClick={() => void submit()} disabled={!enabled || saving} aria-describedby={enabled ? undefined : "client-form-preview"}>
            <Check size={16} aria-hidden="true" />
            {saving ? "Enregistrement…" : "Enregistrer"}
          </Button>
        </div>
      </header>

      <div className="clients-form-scroll">
        <form className="clients-card clients-form" noValidate onSubmit={(event) => void submit(event)} aria-label={title}>
          {!enabled && (
            <Banner tone="info" icon={<Info size={20} />} title="Aperçu du formulaire" id="client-form-preview">
              L&apos;enregistrement est indisponible : le service Clients est désactivé dans cette version de l&apos;application.
            </Banner>
          )}
          {isSubmitted && hasErrors && (
            <Banner tone="error" icon={<AlertTriangle size={20} />} title="Veuillez corriger les erreurs du formulaire" role="alert">
              Les données saisies sont conservées.
            </Banner>
          )}
          {isDirty && !isSubmitSuccessful && (
            <Banner tone="warning" icon={<Save size={20} />} title="Modifications non enregistrées">
              Vos changements ne seront conservés qu&apos;après enregistrement.
            </Banner>
          )}

          <div className="clients-form__grid">
            <SectionTitle icon={<User size={16} />}>Identité</SectionTitle>
            <Field label="Prénom" required error={errors.firstName?.message} registration={register("firstName")} autoComplete="off" placeholder="Prénom du client" maxLength={CLIENT_LIMITS.name} autoFocus />
            <Field label="Nom" required error={errors.lastName?.message} registration={register("lastName")} autoComplete="off" placeholder="Nom de famille" maxLength={CLIENT_LIMITS.name} />
            <Field label="Date de naissance" icon={<Calendar size={18} />} hint="Optionnel · jj/mm/aaaa" error={errors.birthDate?.message} registration={register("birthDate")} placeholder="jj/mm/aaaa" inputMode="numeric" maxLength={10} />
            <Field label="Téléphone" icon={<Phone size={18} />} hint="Optionnel" error={errors.phone?.message} registration={register("phone")} type="tel" placeholder="06 00 00 00 00" maxLength={32} />
            <Field label="Email" full icon={<Mail size={18} />} hint="Optionnel" error={errors.email?.message} registration={register("email")} type="email" placeholder="nom@exemple.ma" maxLength={CLIENT_LIMITS.email} />

            <SectionTitle icon={<MapPin size={16} />}>Coordonnées</SectionTitle>
            <Field label="Adresse" full error={errors.addressLine?.message} registration={register("addressLine")} placeholder="Numéro et rue" maxLength={CLIENT_LIMITS.addressLine} />
            <Field label="Ville" error={errors.city?.message} registration={register("city")} placeholder="Ville" maxLength={CLIENT_LIMITS.city} />
            <Field label="Code postal" error={errors.postalCode?.message} registration={register("postalCode")} placeholder="00000" maxLength={CLIENT_LIMITS.postalCode} />

            <SectionTitle icon={<Shield size={16} />}>Informations complémentaires</SectionTitle>
            <Field
              label="Notes"
              full
              multiline
              hint={`Remarques internes, visibles des utilisateurs autorisés — aucune donnée de santé (${CLIENT_LIMITS.notes} caractères au maximum)`}
              error={errors.notes?.message}
              registration={register("notes")}
              placeholder="Remarques internes sur le client…"
              maxLength={CLIENT_LIMITS.notes}
            />
          </div>
          {/* Soumission au clavier (Entrée dans un champ) sans bouton visible en double. */}
          <button type="submit" hidden disabled={!enabled || saving} tabIndex={-1} />
        </form>
      </div>

      <ConfirmDialog
        open={leaveRequest !== null}
        onOpenChange={(open) => !open && setLeaveRequest(null)}
        tone="warning"
        icon={<AlertTriangle size={28} />}
        title="Quitter sans enregistrer ?"
        description="Vous avez des modifications non enregistrées sur cette fiche client. Si vous quittez maintenant, elles seront perdues."
        cancelLabel="Continuer la modification"
        confirmLabel="Quitter sans enregistrer"
        confirmIcon={<LogOut size={16} aria-hidden="true" />}
        onConfirm={() => {
          const proceed = leaveRequest;
          dirtyRef.current = false;
          setLeaveRequest(null);
          proceed?.();
        }}
      />
      <ConfirmDialog
        open={failure !== null}
        onOpenChange={(open) => !open && setFailure(null)}
        tone="error"
        icon={<AlertTriangle size={28} />}
        title={failure?.title ?? ""}
        description={failure?.text ?? ""}
        cancelLabel={failure?.conflict ? "Rester sur le formulaire" : "Fermer"}
        confirmLabel={failure?.conflict && editing ? "Recharger la fiche" : undefined}
        onConfirm={() => {
          dirtyRef.current = false;
          setFailure(null);
          navigate(backTo);
        }}
      />
    </section>
  );
}

function CreatedPanel({ client, onOpen, onCreateAnother }: { client: ClientDetail; onOpen: () => void; onCreateAnother: () => void }) {
  return (
    <section className="clients-view clients-view--center" aria-labelledby="client-created-title">
      <div className="clients-card clients-created" role="status">
        <span className="clients-created__icon" aria-hidden="true">
          <UserCheck size={32} />
        </span>
        <h1 id="client-created-title" className="clients-created__title">
          Client créé
        </h1>
        <div className="clients-created__who">
          <span className="clients-avatar" aria-hidden="true">
            {clientInitials(client)}
          </span>
          <div>
            <div className="clients-cell__name">{clientDisplayName(client)}</div>
            <div className="clients-cell__ref">{client.reference}</div>
          </div>
        </div>
        <p className="clients-created__text">La fiche est enregistrée.</p>
        <div className="clients-actions clients-actions--center">
          <Button variant="primary" onClick={onOpen} autoFocus>
            <User size={16} aria-hidden="true" />
            Voir la fiche
          </Button>
          <Button variant="tertiary" onClick={onCreateAnother}>
            Créer un autre client
          </Button>
        </div>
      </div>
    </section>
  );
}

function SectionTitle({ icon, children }: { icon: ReactNode; children: ReactNode }) {
  return (
    <h2 className="clients-form__section">
      <span aria-hidden="true">{icon}</span>
      {children}
    </h2>
  );
}

function Banner({
  tone,
  icon,
  title,
  children,
  role,
  id,
}: {
  tone: "info" | "warning" | "error";
  icon: ReactNode;
  title: string;
  children: ReactNode;
  role?: "alert";
  id?: string;
}) {
  return (
    <div className={cn("clients-banner", `clients-banner--${tone}`)} role={role} id={id}>
      <span className="clients-banner__icon" aria-hidden="true">
        {icon}
      </span>
      <div>
        <div className="clients-banner__title">{title}</div>
        <div className="clients-banner__text">{children}</div>
      </div>
    </div>
  );
}

interface FieldProps {
  label: string;
  registration: UseFormRegisterReturn;
  error?: string;
  hint?: string;
  required?: boolean;
  full?: boolean;
  multiline?: boolean;
  icon?: ReactNode;
  type?: "text" | "email" | "tel";
  placeholder?: string;
  maxLength?: number;
  inputMode?: "numeric" | "text";
  autoComplete?: string;
  autoFocus?: boolean;
}

function Field({ label, registration, error, hint, required, full, multiline, icon, type = "text", placeholder, maxLength, inputMode, autoComplete = "off", autoFocus }: FieldProps) {
  const id = useId();
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;
  const common = {
    id,
    ...registration,
    placeholder,
    maxLength,
    autoComplete,
    autoFocus,
    "aria-invalid": error ? true : undefined,
    "aria-describedby": describedBy,
    "aria-required": required || undefined,
  };
  return (
    <div className={cn("clients-field", full && "clients-field--full")}>
      {/* Astérisque décoratif en CSS (::after) : le nom accessible reste le libellé seul, l'obligation est portée par aria-required. */}
      <Label htmlFor={id} className="clients-field__label" data-required={required || undefined}>
        {label}
      </Label>
      <div className={cn("clients-input", multiline && "clients-input--area", error && "clients-input--error")}>
        {icon && <span className="clients-input__icon" aria-hidden="true">{icon}</span>}
        {multiline ? <textarea rows={4} {...common} /> : <input type={type} inputMode={inputMode} spellCheck={false} {...common} />}
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
