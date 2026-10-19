// Valeurs des sélecteurs de date et d'heure (03-D1), module pur testé par appointments/pickers.test.ts.
// Dates : dates CIVILES `AAAA-MM-JJ` du fuseau du magasin, portées par un `Date` local à midi et relues par ses
// composantes locales — jamais de conversion UTC, donc aucun décalage d'un jour quel que soit le fuseau du poste.

const CIVIL = /^(\d{4})-(\d{2})-(\d{2})$/;

/** `AAAA-MM-JJ` → jour du calendrier (`Date` local à midi : jamais à cheval sur un changement d'heure). */
export function civilToCalendarDate(civil: string): Date | undefined {
  const match = CIVIL.exec(civil);
  if (!match) return undefined;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]), 12);
  return calendarDateToCivil(date) === civil ? date : undefined;
}

/** Jour du calendrier → `AAAA-MM-JJ` (composantes locales, jamais `toISOString`). */
export function calendarDateToCivil(date: Date): string {
  return `${String(date.getFullYear()).padStart(4, "0")}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

/** `AAAA-MM-JJ` → `JJ/MM/AAAA`. */
export const frenchDate = (civil: string) => civil.replace(CIVIL, "$3/$2/$1");

export interface TimeOption {
  /** `HH:mm`. */
  value: string;
  /** Motif d'indisponibilité affiché à côté de l'heure ; absent = sélectionnable. */
  disabledReason?: string;
}

/** Chiffres tapés → heure visée `HH:mm` (« 9 » → 09:00, « 14 » → 14:00, « 143 » → 14:30, « 1435 » → 14:35). */
export function typedTime(digits: string): string | null {
  if (!/^\d{1,4}$/.test(digits)) return null;
  const hours = digits.length <= 2 ? digits : digits.slice(0, 2);
  const minutes = digits.length <= 2 ? "00" : digits.length === 3 ? `${digits[2]}0` : digits.slice(2);
  const h = Number(hours);
  const m = Number(minutes);
  if (h > 23 || m > 59) return null;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
}
