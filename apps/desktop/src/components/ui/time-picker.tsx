// Sélecteur d'heure (03-D1) : liste déroulante accessible (motif « listbox » ARIA) dans un Popover Radix, sans
// saisie manuelle. Liste défilante de hauteur bornée (une dizaine de lignes visibles), jamais un panneau géant ; les
// heures sont fournies par l'appelant (pas du serveur, journée entière : la liste n'est PAS limitée à la plage
// affichée de la grille). Une heure désactivée reste visible avec son motif (« n'existe pas ce jour-là »…).
// Aucune heure n'est présentée comme « disponible » : la disponibilité est décidée par le serveur à l'enregistrement.
// Clavier : flèches = pas suivant / précédent, Page préc./suiv. = une heure, Début / Fin, chiffres = aller à l'heure
// tapée (« 9 » → 09:00, « 14 » → 14:00, « 1430 » → 14:30), Entrée ou Espace = choisir, Échap = fermer.
import { ChevronDown, Clock } from "lucide-react";
import { type KeyboardEvent, type Ref, useEffect, useId, useRef, useState } from "react";
import { cn } from "../../lib/utils.ts";
import { type TimeOption, typedTime } from "./picker-values.ts";
import { Popover, PopoverContent, PopoverTrigger } from "./popover.tsx";

interface TimePickerProps {
  value: string;
  onChange: (time: string) => void;
  options: readonly TimeOption[];
  /** Heure mise en évidence à l'ouverture quand aucune n'est choisie (ex. début de la plage affichée). */
  initialFocus: string;
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
  /** Mention en pied de liste. */
  footnote?: string;
}

const PAGE = 12;
const TYPEAHEAD_MS = 1000;

export function TimePicker({
  value,
  onChange,
  options,
  initialFocus,
  labelledBy,
  describedBy,
  buttonRef,
  autoFocus,
  invalid,
  required,
  disabled,
  onBlur,
  placeholder = "Choisir",
  footnote,
}: TimePickerProps) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const id = useId();
  const listRef = useRef<HTMLUListElement>(null);
  const typed = useRef<{ digits: string; at: number }>({ digits: "", at: 0 });
  const enabled = (index: number) => index >= 0 && index < options.length && options[index].disabledReason === undefined;

  /** Premier indice sélectionnable à partir de `start` dans le sens `step` (±1), ou -1. */
  const seek = (start: number, step: 1 | -1) => {
    for (let index = start; index >= 0 && index < options.length; index += step) if (enabled(index)) return index;
    return -1;
  };
  /** Premier indice sélectionnable à l'heure `time` ou après (sinon le dernier sélectionnable avant). */
  const atOrAfter = (time: string) => {
    const index = options.findIndex((option) => option.value >= time);
    const after = index === -1 ? -1 : seek(index, 1);
    return after !== -1 ? after : seek(options.length - 1, -1);
  };

  /** Rend l'option visible en ne faisant défiler que la liste (jamais la page) ; `center` : à l'ouverture. */
  const reveal = (index: number, center = false) => {
    const list = listRef.current;
    const item = list?.querySelector<HTMLElement>(`[data-index="${index}"]`);
    if (!list || !item) return;
    const top = item.offsetTop;
    const bottom = top + item.offsetHeight;
    if (center) list.scrollTop = Math.max(0, top - (list.clientHeight - item.offsetHeight) / 2);
    else if (top < list.scrollTop) list.scrollTop = top;
    else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight;
  };

  // L'option active reste visible pendant la navigation au clavier.
  useEffect(() => {
    if (open && active >= 0) reveal(active);
  }, [open, active]);

  const choose = (index: number) => {
    if (!enabled(index)) return;
    onChange(options[index].value);
    setOpen(false);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLUListElement>) => {
    const move = (next: number) => {
      event.preventDefault();
      if (next !== -1) setActive(next);
    };
    switch (event.key) {
      case "ArrowDown":
        return move(seek(active + 1, 1));
      case "ArrowUp":
        return move(seek(active - 1, -1));
      case "PageDown": {
        const target = Math.min(active + PAGE, options.length - 1);
        const next = seek(target, 1);
        return move(next !== -1 ? next : seek(target, -1));
      }
      case "PageUp": {
        const target = Math.max(active - PAGE, 0);
        const next = seek(target, -1);
        return move(next !== -1 ? next : seek(target, 1));
      }
      case "Home":
        return move(seek(0, 1));
      case "End":
        return move(seek(options.length - 1, -1));
      case "Enter":
      case " ":
        event.preventDefault();
        choose(active);
        return;
      default:
        if (/^\d$/.test(event.key)) {
          event.preventDefault();
          const now = Date.now();
          const digits = (now - typed.current.at > TYPEAHEAD_MS ? "" : typed.current.digits) + event.key;
          typed.current = { digits: digits.slice(-4), at: now };
          const target = typedTime(typed.current.digits) ?? typedTime(event.key);
          if (target) setActive(atOrAfter(target));
        }
    }
  };

  const listId = `${id}-list`;
  const optionId = (index: number) => `${id}-option-${index}`;

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) {
          // À l'ouverture : l'heure choisie (même désactivée, pour la situer), sinon l'heure de départ proposée.
          const current = options.findIndex((option) => option.value === value);
          setActive(current !== -1 ? current : atOrAfter(initialFocus));
          typed.current = { digits: "", at: 0 };
        } else onBlur?.();
      }}
    >
      <PopoverTrigger asChild>
        <button
          ref={buttonRef}
          type="button"
          autoFocus={autoFocus}
          className={cn("clients-input rdv-input--button mmv-picker__trigger", invalid && "clients-input--error", !value && "rdv-input--placeholder")}
          aria-labelledby={`${labelledBy} ${id}-value`}
          aria-describedby={describedBy}
          aria-invalid={invalid || undefined}
          aria-required={required || undefined}
          aria-haspopup="listbox"
          disabled={disabled}
        >
          <span className="clients-input__icon" aria-hidden="true">
            <Clock size={18} />
          </span>
          <span id={`${id}-value`} className="rdv-grow rdv-input__value tnum">
            {value || placeholder}
          </span>
          <ChevronDown size={16} aria-hidden="true" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        className="mmv-popover--time"
        onOpenAutoFocus={(event) => {
          // Le contenu du portail n'existe qu'à partir d'ici (montage différé) : focus et défilement initial.
          event.preventDefault();
          listRef.current?.focus({ preventScroll: true });
          if (active >= 0) reveal(active, true);
        }}
      >
        <ul
          ref={listRef}
          id={listId}
          role="listbox"
          tabIndex={0}
          aria-labelledby={labelledBy}
          aria-activedescendant={active >= 0 ? optionId(active) : undefined}
          className="mmv-timelist"
          onKeyDown={onKeyDown}
        >
          {options.map((option, index) => (
            <li
              key={option.value}
              id={optionId(index)}
              data-index={index}
              role="option"
              aria-selected={option.value === value}
              aria-disabled={option.disabledReason !== undefined || undefined}
              className={cn(
                "mmv-timelist__option tnum",
                index === active && "mmv-timelist__option--active",
                option.value === value && "mmv-timelist__option--selected",
              )}
              onMouseMove={() => enabled(index) && index !== active && setActive(index)}
              onClick={() => choose(index)}
            >
              <span>{option.value}</span>
              {option.disabledReason && <span className="mmv-timelist__reason">{option.disabledReason}</span>}
            </li>
          ))}
        </ul>
        {footnote && <p className="mmv-timelist__foot">{footnote}</p>}
      </PopoverContent>
    </Popover>
  );
}
