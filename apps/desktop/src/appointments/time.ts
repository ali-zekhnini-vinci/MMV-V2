// Dates et heures des rendez-vous (03-B ; D-03-13 approuvée en 03-B2) : stockage UTC, affichage et saisie dans le
// fuseau IANA du MAGASIN fourni par le serveur (jamais celui du poste). Calculs par Temporal (polyfill déjà
// requis par FullCalendar 7) : pas d'arithmétique manuelle sur des décalages ; heures inexistantes ET ambiguës
// refusées. Module pur, testé par time.test.ts.
import { Temporal } from "temporal-polyfill";

export type CalendarView = "day" | "week";

/** Date civile `AAAA-MM-JJ` dans le fuseau du magasin. */
export type CivilDate = string;

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

export const isCivilDate = (value: string): boolean => {
  if (!DATE_PATTERN.test(value)) return false;
  try {
    Temporal.PlainDate.from(value, { overflow: "reject" });
    return true;
  } catch {
    return false;
  }
};

export const isWallTime = (value: string): boolean => TIME_PATTERN.test(value);

export function todayIn(timeZone: string, now: Temporal.Instant = Temporal.Now.instant()): CivilDate {
  return now.toZonedDateTimeISO(timeZone).toPlainDate().toString();
}

export type ZonedResult = { ok: true; utc: string } | { ok: false; reason: "invalid" | "nonexistent" | "ambiguous" };

/**
 * Heure murale du magasin → instant UTC (`…Z`). D-03-13 : une heure inexistante (avance de l'heure) ou ambiguë
 * (recul de l'heure, deux occurrences) est REFUSÉE plutôt que décalée ou choisie en silence. Le serveur refait le
 * contrôle sur tout le créneau (422 `startsAtUtc: time_change`).
 */
export function wallTimeToUtc(date: CivilDate, time: string, timeZone: string): ZonedResult {
  if (!isCivilDate(date) || !isWallTime(time)) return { ok: false, reason: "invalid" };
  const wall = Temporal.PlainDateTime.from(`${date}T${time}`);
  let earlier: Temporal.ZonedDateTime;
  let later: Temporal.ZonedDateTime;
  try {
    earlier = wall.toZonedDateTime(timeZone, { disambiguation: "earlier" });
    later = wall.toZonedDateTime(timeZone, { disambiguation: "later" });
  } catch {
    return { ok: false, reason: "invalid" };
  }
  if (!earlier.toPlainDateTime().equals(wall)) return { ok: false, reason: "nonexistent" };
  if (!earlier.toInstant().equals(later.toInstant())) return { ok: false, reason: "ambiguous" };
  return { ok: true, utc: earlier.toInstant().toString() };
}

/** Instant UTC → date civile et heure murale du magasin. */
export function utcToWall(utc: string, timeZone: string): { date: CivilDate; time: string } {
  const zoned = Temporal.Instant.from(utc).toZonedDateTimeISO(timeZone);
  return { date: zoned.toPlainDate().toString(), time: zoned.toPlainTime().toString({ smallestUnit: "minute" }) };
}

export function addMinutesUtc(utc: string, minutes: number): string {
  return Temporal.Instant.from(utc).add({ minutes }).toString();
}

export function minutesBetween(startUtc: string, endUtc: string): number {
  return Math.round(Temporal.Instant.from(startUtc).until(Temporal.Instant.from(endUtc)).total("minutes"));
}

/** Premier jour (lundi) de la semaine ISO contenant `date`. */
export function weekStart(date: CivilDate): CivilDate {
  const day = Temporal.PlainDate.from(date);
  return day.subtract({ days: day.dayOfWeek - 1 }).toString();
}

export function shiftDate(date: CivilDate, view: CalendarView, step: number): CivilDate {
  return Temporal.PlainDate.from(date)
    .add(view === "day" ? { days: step } : { weeks: step })
    .toString();
}

/** Jours visibles de la vue (semaine : lundi → dimanche moins les jours masqués). */
export function visibleDays(date: CivilDate, view: CalendarView, hiddenWeekdays: readonly number[]): CivilDate[] {
  if (view === "day") return [date];
  const monday = Temporal.PlainDate.from(weekStart(date));
  return Array.from({ length: 7 }, (_, index) => monday.add({ days: index })).filter(
    (day) => !hiddenWeekdays.includes(day.dayOfWeek % 7),
  ).map((day) => day.toString());
}

/** Bornes UTC de la vue, `to` exclu : début de journée du magasin, changements d'heure compris. */
export function viewRangeUtc(date: CivilDate, view: CalendarView, timeZone: string): { fromUtc: string; toUtc: string } {
  const first = view === "day" ? Temporal.PlainDate.from(date) : Temporal.PlainDate.from(weekStart(date));
  const last = first.add(view === "day" ? { days: 1 } : { weeks: 1 });
  const startOf = (day: Temporal.PlainDate) => day.toZonedDateTime({ timeZone }).toInstant().toString();
  return { fromUtc: startOf(first), toUtc: startOf(last) };
}

const capitalize = (text: string) => (text ? text.charAt(0).toLocaleUpperCase("fr") + text.slice(1) : text);
const plain = (date: CivilDate) => Temporal.PlainDate.from(date);

/** « Jeudi 16 juillet 2026 ». */
export const formatLongDate = (date: CivilDate): string =>
  capitalize(plain(date).toLocaleString("fr-FR", { weekday: "long", day: "numeric", month: "long", year: "numeric" }));

/** « Jeu. 16 juil. ». */
export const formatShortDay = (date: CivilDate): string =>
  capitalize(plain(date).toLocaleString("fr-FR", { weekday: "short", day: "numeric", month: "short" }));

/** « 16/07/2026 ». */
export const formatNumericDate = (date: CivilDate): string =>
  plain(date).toLocaleString("fr-FR", { day: "2-digit", month: "2-digit", year: "numeric" });

/** « Semaine du 13 au 18 juillet 2026 » (premier et dernier jours visibles). */
export function formatWeekTitle(days: readonly CivilDate[]): string {
  if (days.length === 0) return "";
  const first = plain(days[0]);
  const last = plain(days[days.length - 1]);
  const lastText = last.toLocaleString("fr-FR", { day: "numeric", month: "long", year: "numeric" });
  const firstText =
    first.month === last.month
      ? String(first.day)
      : first.toLocaleString("fr-FR", first.year === last.year ? { day: "numeric", month: "long" } : { day: "numeric", month: "long", year: "numeric" });
  return `Semaine du ${firstText} au ${lastText}`;
}

/** « 13 – 18 juil. 2026 ». */
export function formatWeekShort(days: readonly CivilDate[]): string {
  if (days.length === 0) return "";
  const first = plain(days[0]);
  const last = plain(days[days.length - 1]);
  const lastText = last.toLocaleString("fr-FR", { day: "numeric", month: "short", year: "numeric" });
  const firstText = first.month === last.month ? String(first.day) : first.toLocaleString("fr-FR", { day: "numeric", month: "short" });
  return `${firstText} – ${lastText}`;
}

/** « 10:30 – 11:15 » dans le fuseau du magasin. */
export function formatTimeRange(startUtc: string, endUtc: string, timeZone: string): string {
  return `${utcToWall(startUtc, timeZone).time} – ${utcToWall(endUtc, timeZone).time}`;
}

/** « 45 minutes », « 1 h », « 1 h 30 ». */
export function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} minutes`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} h` : `${hours} h ${String(rest).padStart(2, "0")}`;
}
