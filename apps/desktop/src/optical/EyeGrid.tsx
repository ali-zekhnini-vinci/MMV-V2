// PROTOTYPE 04-A — grille OD / OG NEUTRE (PREPARATION_04A.md § 5). Saisie contrôlée conservée EXACTEMENT : aucun
// arrondi, aucune décimale ajoutée, aucun signe ajouté, aucune conversion (0° reste 0°), aucune borne, aucun pas
// (les flèches ne modifient rien), aucune dépendance entre champs, aucun champ désactivé (état 36 non implémenté,
// D-04-06 ouverte). Champ vide = non renseigné, distinct de la valeur « 0 ». État explicite par œil (D-04-02.c,
// proposition). Ordre DOM = ordre de Tab : OD (état puis valeurs) → OG.
import { useId } from "react";
import type { UseFormRegister } from "react-hook-form";
import { cn } from "../lib/utils.ts";
import { EYE_FIELDS, EYE_SIDES, EYE_STATES, type EyeEntry, type EyeField, type EyeSide, type OpticalKind, type RecordContent } from "./opticalContract.ts";
import { eyeStateLabel, FIELD_LABEL, FIELD_UNIT, FIELD_UNIT_SHORT, SIDE_LABEL, SIDE_SHORT } from "./opticalLabels.ts";
import { ProposedTag } from "./OpticalBadges.tsx";

const LATIN: Record<EyeSide, string> = { od: "Oculus dexter", og: "Oculus sinister" };

interface EyeGridProps {
  kind: OpticalKind;
  register: UseFormRegister<RecordContent>;
  /** Erreurs renvoyées par le double, par chemin (`od.sphere`, `og.state`…) ; aucune règle locale. */
  errors: Record<string, string>;
  disabled?: boolean;
}

export function EyeGrid({ kind, register, errors, disabled = false }: EyeGridProps) {
  return (
    <div className="optical-eyes">
      {EYE_SIDES.map((side) => (
        <EyeCard key={side} kind={kind} side={side} register={register} errors={errors} disabled={disabled} />
      ))}
    </div>
  );
}

function EyeCard({ kind, side, register, errors, disabled }: EyeGridProps & { side: EyeSide }) {
  const id = useId();
  const stateError = errors[`${side}.state`];
  return (
    <fieldset className={cn("optical-eye", `optical-eye--${side}`)} disabled={disabled}>
      <legend className="optical-eye__legend">
        <span className="optical-eye__side">{SIDE_SHORT[side]}</span>
        <span>
          <span className="optical-eye__title">{SIDE_LABEL[side]}</span>
          <span className="optical-eye__latin">{LATIN[side]}</span>
        </span>
      </legend>

      <div className="optical-eye__state" role="radiogroup" aria-labelledby={`${id}-state`} aria-describedby={stateError ? `${id}-state-error` : undefined}>
        <span id={`${id}-state`} className="clients-field__label">
          État de l&apos;œil <ProposedTag refs="D-04-02.c" />
        </span>
        <div className="optical-eye__choices">
          {EYE_STATES.map((state) => (
            <label key={state} className="optical-radio">
              <input type="radio" value={state} {...register(`${side}.state`)} />
              <span>{eyeStateLabel(kind, state)}</span>
            </label>
          ))}
        </div>
        {stateError && (
          <span id={`${id}-state-error`} className="clients-field__error" role="alert">
            {stateError}
          </span>
        )}
      </div>

      <div className="optical-eye__fields">
        {EYE_FIELDS.map((field) => (
          <EyeInput key={field} side={side} field={field} register={register} error={errors[`${side}.${field}`]} />
        ))}
      </div>
    </fieldset>
  );
}

function EyeInput({ side, field, register, error }: { side: EyeSide; field: EyeField; register: UseFormRegister<RecordContent>; error?: string }) {
  const id = useId();
  const unitId = `${id}-unit`;
  const errorId = `${id}-error`;
  const numeric = field !== "base" && field !== "acuity";
  return (
    <div className={cn("clients-field", "optical-eye__field", (field === "base" || field === "acuity") && "optical-eye__field--text")}>
      <label htmlFor={id} className="clients-field__label">
        {FIELD_LABEL[field]}
        <span className="visually-hidden"> {SIDE_SHORT[side]}</span>
      </label>
      <div className={cn("clients-input", "optical-input", error && "clients-input--error")}>
        <input
          id={id}
          type="text"
          // Clavier décimal sur écran tactile seulement : aucune contrainte, aucune conversion, aucun pas.
          inputMode={numeric ? "decimal" : "text"}
          autoComplete="off"
          spellCheck={false}
          aria-invalid={error ? true : undefined}
          aria-describedby={[unitId, error ? errorId : null].filter(Boolean).join(" ")}
          {...register(`${side}.values.${field}`)}
        />
        {FIELD_UNIT_SHORT[field] && (
          <span className="optical-input__unit" aria-hidden="true">
            {FIELD_UNIT_SHORT[field]}
          </span>
        )}
      </div>
      <span id={unitId} className="visually-hidden">
        Unité : {FIELD_UNIT[field]}. Vide = non renseigné.
      </span>
      {error && (
        <span id={errorId} className="clients-field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  );
}

/** Lecture seule : valeurs restituées telles que saisies ; l'absence est écrite en toutes lettres (état 21). */
export function EyeTable({
  kind,
  od,
  og,
  caption,
  highlight,
}: {
  kind: OpticalKind;
  od: EyeEntry;
  og: EyeEntry;
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
            <th scope="col">État</th>
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
            const entry = entries[side];
            const stateChanged = highlight?.has(`${side}.state`);
            return (
              <tr key={side}>
                <th scope="row">{SIDE_LABEL[side]}</th>
                <td className={cn(stateChanged && "optical-changed")}>
                  {eyeStateLabel(kind, entry.state)}
                  {stateChanged && <ChangedMark />}
                </td>
                {EYE_FIELDS.map((field) => {
                  const value = entry.values[field];
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
