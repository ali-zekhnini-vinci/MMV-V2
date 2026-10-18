// Sélecteur de date (03-D1) : calendrier compact React DayPicker dans un Popover Radix, sans saisie manuelle.
// Les dates manipulées sont des dates CIVILES `AAAA-MM-JJ` du fuseau du magasin (jamais des instants) : le
// calendrier ne reçoit que des jours, portés par un `Date` local à midi et relus par ses composantes locales —
// aucune conversion UTC, donc aucun décalage d'un jour quel que soit le fuseau du poste. « Aujourd'hui » et la
// première date admise sont calculés par l'appelant dans le fuseau du magasin. Le serveur reste seul juge.
// Clavier (DayPicker) : flèches = jour / semaine, Page préc./suiv. = mois, Maj+Page = année, Début/Fin = semaine,
// Entrée ou Espace = choisir, Échap = fermer (focus rendu au déclencheur).
import { Calendar, ChevronDown } from "lucide-react";
import { type Ref, useId, useState } from "react";
import { DayPicker } from "react-day-picker";
import { fr } from "react-day-picker/locale";
import "react-day-picker/style.css";
import { cn } from "../../lib/utils.ts";
import { calendarDateToCivil, civilToCalendarDate, frenchDate } from "./picker-values.ts";
import { Popover, PopoverContent, PopoverTrigger } from "./popover.tsx";

interface DatePickerProps {
  /** Date civile choisie (`AAAA-MM-JJ`) ou `""`. */
  value: string;
  onChange: (civil: string) => void;
  /** Aujourd'hui dans le fuseau du magasin (`AAAA-MM-JJ`). */
  today: string;
  /** Première date admise (incluse) ; les précédentes sont désactivées. */
  minDate?: string;
  /** Dernière année proposée dans la liste des années (navigation seulement, aucune règle métier). */
  maxYear: number;
  labelledBy: string;
  describedBy?: string;
  /** Référence du bouton déclencheur (focus d'un champ en erreur par React Hook Form). */
  buttonRef?: Ref<HTMLButtonElement>;
  autoFocus?: boolean;
  invalid?: boolean;
  required?: boolean;
  disabled?: boolean;
  onBlur?: () => void;
  placeholder?: string;
}

export function DatePicker({
  value,
  onChange,
  today,
  minDate,
  maxYear,
  labelledBy,
  describedBy,
  buttonRef,
  autoFocus,
  invalid,
  required,
  disabled,
  onBlur,
  placeholder = "Choisir une date",
}: DatePickerProps) {
  const [open, setOpen] = useState(false);
  const valueId = useId();
  const selected = civilToCalendarDate(value);
  const todayDate = civilToCalendarDate(today);
  const min = minDate ? civilToCalendarDate(minDate) : undefined;
  // Mois affichés : depuis le plus ancien de (première date admise, date choisie) jusqu'à décembre de `maxYear`.
  const first = [min, selected].filter((date): date is Date => date !== undefined).sort((a, b) => a.getTime() - b.getTime())[0] ?? todayDate;
  const startMonth = first ? new Date(first.getFullYear(), first.getMonth(), 1, 12) : undefined;
  const endMonth = new Date(maxYear, 11, 1, 12);
  const [month, setMonth] = useState<Date | undefined>(selected ?? todayDate);

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) setMonth(selected ?? todayDate);
        else onBlur?.();
      }}
    >
      <PopoverTrigger asChild>
        <button
          ref={buttonRef}
          type="button"
          autoFocus={autoFocus}
          className={cn("clients-input rdv-input--button mmv-picker__trigger", invalid && "clients-input--error", !selected && "rdv-input--placeholder")}
          aria-labelledby={`${labelledBy} ${valueId}`}
          aria-describedby={describedBy}
          aria-invalid={invalid || undefined}
          aria-required={required || undefined}
          aria-haspopup="dialog"
          disabled={disabled}
        >
          <span className="clients-input__icon" aria-hidden="true">
            <Calendar size={18} />
          </span>
          <span id={valueId} className="rdv-grow rdv-input__value tnum">
            {selected ? frenchDate(value) : placeholder}
          </span>
          <ChevronDown size={16} aria-hidden="true" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        className="mmv-popover--calendar"
        aria-label="Calendrier"
        // Le focus va au jour choisi (ou à aujourd'hui) par DayPicker, pas à la première liste déroulante.
        onOpenAutoFocus={(event) => event.preventDefault()}
      >
        <DayPicker
          className="mmv-calendar"
          mode="single"
          locale={fr}
          weekStartsOn={1}
          captionLayout="dropdown"
          navLayout="around"
          startMonth={startMonth}
          endMonth={endMonth}
          month={month}
          onMonthChange={setMonth}
          today={todayDate}
          selected={selected}
          disabled={min ? { before: min } : undefined}
          showOutsideDays
          fixedWeeks
          autoFocus
          required
          onSelect={(date) => {
            if (!date) return;
            onChange(calendarDateToCivil(date));
            setOpen(false);
          }}
          footer={
            todayDate && (!min || todayDate >= min) ? (
              <button
                type="button"
                className="mmv-calendar__today"
                onClick={() => {
                  onChange(today);
                  setOpen(false);
                }}
              >
                Aujourd&apos;hui
              </button>
            ) : undefined
          }
        />
      </PopoverContent>
    </Popover>
  );
}
