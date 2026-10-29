// Grille OD / OG selon la maquette Lot 3 (D-04-15 APPROVED : cartes OD et OG côte à côte, rangées libellé 118 px +
// contrôle, boutons ± de part et d'autre de la valeur pour Sphère, Cylindre et Addition — pas de 0,25 approuvé par le
// mandat 04-B ; Prisme en saisie libre, son pas n'étant pas arrêté). Axe conservé avec un Cylindre absent ou nul :
// contrôle VISIBLE demandant l'intervention de l'utilisateur, l'axe n'est jamais effacé ni converti en silence.
// Saisie au clavier conservée EXACTEMENT : aucun arrondi, aucun signe ajouté, aucune conversion (0° reste 0°), aucune
// borne, aucune dépendance entre champs, aucun champ désactivé (états 25 et 36 non repris, D-04-06 ouverte). Seul un
// clic ± (ou ↑ / ↓ dans le champ) produit une nouvelle valeur, calculée sans alignement sur le pas (opticalStep.ts).
// Champ vide = non renseigné, affiché « — », distinct de la valeur « 0 » ; chaque champ peut rester vide. Aucun
// « état de l'œil » (D-04-16 APPROVED, retire la proposition D-04-02.c). Ordre de Tab = ordre DOM : OD (Sphère →
// Acuité) → OG ; les boutons ± et « Effacer » sont des raccourcis souris hors de l'ordre de Tab (le clavier dispose de
// ↑ / ↓ et de la saisie directe).
import { ChevronDown, X } from "lucide-react";
import { type KeyboardEvent, type ReactNode, useContext, useId, useRef, useState } from "react";
import { type Control, Controller, useWatch } from "react-hook-form";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "../components/ui/dropdown-menu.tsx";
import { cn } from "../lib/utils.ts";
import { EYE_FIELDS, EYE_SIDES, type EyeField, type EyeSide, type EyeValues, type RecordContent } from "./opticalContract.ts";
import { BASE_SUGGESTIONS, FIELD_LABEL, FIELD_UNIT, FIELD_UNIT_SHORT, SIDE_LABEL, SIDE_SHORT } from "./opticalLabels.ts";
import { isEmptyOrZero, isSteppable, OpticalStepContext, STEPPER_FIELDS, stepLabel, stepValue } from "./opticalStep.ts";

const LATIN: Record<EyeSide, string> = { od: "Oculus dexter", og: "Oculus sinister" };
const EYE_TITLE: Record<EyeSide, string> = { od: "Œil droit", og: "Œil gauche" };

interface EyeGridProps {
  control: Control<RecordContent>;
  /** Erreurs renvoyées par le double, par chemin (`od.sphere`, `og.axis`…) ; aucune règle locale. */
  errors: Record<string, string>;
}

export function EyeGrid({ control, errors }: EyeGridProps) {
  // Annonce de la valeur produite par un bouton ± (le champ ne reçoit pas le focus lors d'un clic).
  const [announce, setAnnounce] = useState("");
  return (
    <>
      <div className="optical-eyes">
        {EYE_SIDES.map((side) => (
          <EyeCard key={side} side={side} control={control} errors={errors} onAnnounce={setAnnounce} />
        ))}
      </div>
      <div className="visually-hidden" aria-live="polite">
        {announce}
      </div>
    </>
  );
}

type EyeCardProps = EyeGridProps & { side: EyeSide; onAnnounce: (message: string) => void };

function EyeCard({ side, control, errors, onAnnounce }: EyeCardProps) {
  return (
    <fieldset className={cn("optical-eye", `optical-eye--${side}`)}>
      <legend className="optical-eye__head">
        <span className="optical-eye__tag" aria-hidden="true">
          {SIDE_SHORT[side]}
        </span>
        <span>
          <span className="optical-eye__title">
            {EYE_TITLE[side]}
            <span className="visually-hidden"> ({SIDE_SHORT[side]})</span>
          </span>
          <span className="optical-eye__latin">{LATIN[side]}</span>
        </span>
      </legend>

      <div className="optical-eye__body">
        {EYE_FIELDS.map((field) => (
          <Controller
            key={field}
            control={control}
            name={`${side}.${field}`}
            render={({ field: input }) => (
              <EyeRow
                side={side}
                field={field}
                value={input.value ?? ""}
                onChange={input.onChange}
                onBlur={input.onBlur}
                inputRef={input.ref}
                error={errors[`${side}.${field}`]}
                onAnnounce={onAnnounce}
              />
            )}
          />
        ))}
        <AxisCheck side={side} control={control} />
      </div>
    </fieldset>
  );
}

/** Axe saisi alors que le Cylindre est absent ou nul : signalé, conservé tel quel (l'utilisateur décide). */
function AxisCheck({ side, control }: { side: EyeSide; control: Control<RecordContent> }) {
  const [cylinder = "", axis = ""] = useWatch({ control, name: [`${side}.cylinder`, `${side}.axis`] });
  if (axis.trim() === "" || !isEmptyOrZero(cylinder)) return null;
  return (
    <p className="optical-row__hint" role="note">
      Axe {SIDE_SHORT[side]} renseigné sans cylindre (ou cylindre nul) : vérifiez la source. L&apos;axe est conservé tel quel ; effacez-le
      vous-même s&apos;il ne doit pas figurer.
    </p>
  );
}

interface EyeRowProps {
  side: EyeSide;
  field: EyeField;
  value: string;
  onChange: (value: string) => void;
  onBlur: () => void;
  inputRef: (element: HTMLInputElement | null) => void;
  error?: string;
  onAnnounce: (message: string) => void;
}

/** Rangée de la maquette (`.orow`) : libellé à gauche, contrôle adapté à la nature de la donnée à droite. */
function EyeRow(props: EyeRowProps) {
  const { side, field, error } = props;
  const id = useId();
  const ids = { input: id, unit: `${id}-unit`, hint: `${id}-hint`, error: `${id}-error` };
  const steppable = STEPPER_FIELDS.has(field);
  const unparsable = steppable && !isSteppable(props.value);
  const describedBy = [ids.unit, unparsable ? ids.hint : null, error ? ids.error : null].filter(Boolean).join(" ");
  return (
    <div className="optical-row">
      <label htmlFor={ids.input} className="optical-row__label">
        {FIELD_LABEL[field]}
        <span className="visually-hidden"> {SIDE_SHORT[side]}</span>
      </label>
      <div className="optical-row__ctrl">
        {steppable ? (
          <StepperInput {...props} id={ids.input} describedBy={describedBy} />
        ) : field === "base" ? (
          <BaseInput {...props} id={ids.input} describedBy={describedBy} />
        ) : (
          <TextInput {...props} id={ids.input} describedBy={describedBy} suffix={field === "axis" ? "degrés" : undefined} />
        )}
        <span id={ids.unit} className="visually-hidden">
          Unité : {FIELD_UNIT[field]}. Vide = non renseigné.
          {steppable && " Flèches haut et bas : plus ou moins le pas."}
        </span>
        {unparsable && (
          <span id={ids.hint} className="optical-row__hint">
            Valeur non numérique conservée telle quelle : boutons ± inactifs, modifiez-la au clavier.
          </span>
        )}
        {error && (
          <span id={ids.error} className="clients-field__error" role="alert">
            {error}
          </span>
        )}
      </div>
    </div>
  );
}

type InputProps = EyeRowProps & { id: string; describedBy: string };

/** Composant de saisie optique ± (Sphère, Cylindre, Addition, Prisme) : − à gauche, valeur au centre, + à droite. */
function StepperInput({ side, field, value, onChange, onBlur, inputRef, error, onAnnounce, id, describedBy }: InputProps) {
  const step = useContext(OpticalStepContext);
  const label = `${FIELD_LABEL[field]} ${SIDE_SHORT[side]}`;
  const steppable = isSteppable(value);
  const apply = (direction: 1 | -1) => {
    const next = stepValue(value, direction, step);
    if (next === null) return;
    onChange(next);
    onAnnounce(`${label} : ${next}`);
  };
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.altKey || event.ctrlKey || event.metaKey || (event.key !== "ArrowUp" && event.key !== "ArrowDown")) return;
    if (!steppable) return;
    event.preventDefault();
    apply(event.key === "ArrowUp" ? 1 : -1);
  };
  const text = stepLabel(step);
  return (
    <div className="optical-stepper">
      <StepButton label={`Diminuer ${label} de ${text}`} disabled={!steppable} onClick={() => apply(-1)}>
        −{text}
      </StepButton>
      <div className={cn("optical-stepper__value", error && "optical-stepper__value--error")}>
        <input
          ref={inputRef}
          id={id}
          type="text"
          // Clavier décimal sur écran tactile seulement : aucune contrainte, aucune conversion.
          inputMode="decimal"
          autoComplete="off"
          spellCheck={false}
          placeholder="—"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onBlur={onBlur}
          onKeyDown={onKeyDown}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
        />
        {value !== "" && <ClearButton label={label} onClear={() => onChange("")} />}
      </div>
      <StepButton label={`Augmenter ${label} de ${text}`} disabled={!steppable} onClick={() => apply(1)}>
        +{text}
      </StepButton>
    </div>
  );
}

function StepButton({ label, disabled, onClick, children }: { label: string; disabled: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      className="optical-stepper__btn"
      tabIndex={-1}
      aria-label={label}
      aria-disabled={disabled || undefined}
      // Le focus reste où il est : un clic ± n'interrompt pas la saisie en cours.
      onMouseDown={(event) => event.preventDefault()}
      onClick={() => !disabled && onClick()}
    >
      {children}
    </button>
  );
}

function ClearButton({ label, onClear }: { label: string; onClear: () => void }) {
  return (
    <button type="button" className="optical-clear" tabIndex={-1} aria-label={`Effacer ${label}`} title="Effacer (non renseigné)" onMouseDown={(event) => event.preventDefault()} onClick={onClear}>
      <X size={14} aria-hidden="true" />
    </button>
  );
}

function TextInput({
  side,
  field,
  value,
  onChange,
  onBlur,
  inputRef,
  error,
  id,
  describedBy,
  suffix,
  onKeyDown,
}: InputProps & { suffix?: string; onKeyDown?: (event: KeyboardEvent<HTMLInputElement>) => void }) {
  return (
    <div className={cn("optical-box", error && "optical-box--error")}>
      <input
        ref={inputRef}
        id={id}
        type="text"
        inputMode={field === "axis" ? "numeric" : "text"}
        autoComplete="off"
        spellCheck={false}
        placeholder="—"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onBlur={onBlur}
        onKeyDown={onKeyDown}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
      />
      {value !== "" && <ClearButton label={`${FIELD_LABEL[field]} ${SIDE_SHORT[side]}`} onClear={() => onChange("")} />}
      {suffix && (
        <span className="optical-box__suffix" aria-hidden="true">
          {suffix}
        </span>
      )}
    </div>
  );
}

/** Base : saisie libre (telle que sur la source) + liste de libellés PROPOSÉS par la maquette (Alt + ↓ l'ouvre). */
function BaseInput(props: InputProps) {
  const { side, onChange, onAnnounce } = props;
  const [open, setOpen] = useState(false);
  const input = useRef<HTMLInputElement | null>(null);
  return (
    <div className="optical-base">
      <TextInput
        {...props}
        inputRef={(element) => {
          input.current = element;
          props.inputRef(element);
        }}
        onKeyDown={(event) => {
          if (!event.altKey || event.key !== "ArrowDown") return;
          event.preventDefault();
          setOpen(true);
        }}
      />
      <DropdownMenu open={open} onOpenChange={setOpen} modal={false}>
        <DropdownMenuTrigger asChild>
          <button type="button" className="optical-base__trigger" tabIndex={-1} aria-label={`Choisir une base proposée pour ${SIDE_SHORT[side]}`}>
            <ChevronDown size={16} aria-hidden="true" />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="end"
          className="min-w-48"
          // Retour au champ Base (le déclencheur est hors de l'ordre de Tab).
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            input.current?.focus();
          }}
        >
          {BASE_SUGGESTIONS.map((label) => (
            <DropdownMenuItem
              key={label}
              onSelect={() => {
                onChange(label);
                onAnnounce(`Base ${SIDE_SHORT[side]} : ${label}`);
              }}
            >
              {label}
            </DropdownMenuItem>
          ))}
          <p className="optical-base__note">Liste proposée (D-04-06, non approuvée) · saisie libre possible</p>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

/** Lecture seule : valeurs restituées telles que saisies ; l'absence est écrite en toutes lettres (état 21). */
export function EyeTable({
  od,
  og,
  caption,
  highlight,
}: {
  od: EyeValues;
  og: EyeValues;
  caption: string;
  /** Champs à signaler comme différents (comparaison de révisions, GAP-04-07). */
  highlight?: ReadonlySet<string>;
}) {
  const entries = { od, og };
  return (
    <div className="clients-panel__scroll">
      <table className="clients-table optical-eyetable">
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">Œil</th>
            {EYE_FIELDS.map((field) => (
              <th key={field} scope="col">
                {FIELD_LABEL[field]}
                {FIELD_UNIT_SHORT[field] && <span className="optical-eyetable__unit"> ({FIELD_UNIT_SHORT[field]})</span>}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {EYE_SIDES.map((side) => {
            const values = entries[side];
            return (
              <tr key={side}>
                <th scope="row">{SIDE_LABEL[side]}</th>
                {EYE_FIELDS.map((field) => {
                  const value = values[field];
                  const changed = highlight?.has(`${side}.${field}`);
                  return (
                    <td key={field} className={cn("tnum", changed && "optical-changed")}>
                      {value === "" ? (
                        <span className="clients-muted" aria-label="Non renseigné">
                          —
                        </span>
                      ) : (
                        <span className="optical-value">{value}</span>
                      )}
                      {changed && <ChangedMark />}
                    </td>
                  );
                })}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export function ChangedMark() {
  return <span className="optical-changed__mark"> (modifié)</span>;
}
