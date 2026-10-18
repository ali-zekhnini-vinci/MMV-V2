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

/**
 * Heure murale du magasin `AAAA-MM-JJTHH:mm` calculée par CE poste (ICU de WebView2) pour un instant UTC. Envoyée
 * avec chaque saisie (`startsAtLocal`) : le serveur la recalcule avec ses propres données de fuseaux (Windows) et
 * refuse tout écart (422 `time_zone_mismatch`, B2-REV-03, 03-B3).
 */
export function localWallDateTime(utc: string, timeZone: string): string {
  const wall = utcToWall(utc, timeZone);
  return `${wall.date}T${wall.time}`;
}

/** Décalage UTC constant du magasin, en minutes, sur [fromUtc, toUtc[. */
export interface OffsetSegment {
  fromUtc: string;
  toUtc: string;
  offsetMinutes: number;
}

const offsetAt = (instant: Temporal.Instant, timeZone: string) => instant.toZonedDateTimeISO(timeZone).offsetNanoseconds / 60_000_000_000;

/** Décalages selon CE poste sur [fromUtc, toUtc[ : segments contigus, découpés aux transitions ICU. */
export function localOffsetSegments(fromUtc: string, toUtc: string, timeZone: string): OffsetSegment[] {
  const end = Temporal.Instant.from(toUtc);
  const segments: OffsetSegment[] = [];
  let start = Temporal.Instant.from(fromUtc);
  while (Temporal.Instant.compare(start, end) < 0) {
    const next = start.toZonedDateTimeISO(timeZone).getTimeZoneTransition("next")?.toInstant() ?? null;
    const stop = next !== null && Temporal.Instant.compare(next, end) < 0 ? next : end;
    segments.push({ fromUtc: start.toString(), toUtc: stop.toString(), offsetMinutes: offsetAt(start, timeZone) });
    start = stop;
  }
  return segments;
}

/** Premier écart entre les décalages du serveur et ceux de ce poste, ou `null` s'ils concordent. */
export interface OffsetDivergence {
  atUtc: string;
  serverOffsetMinutes: number | null;
  localOffsetMinutes: number | null;
}

/**
 * B2-REV-03 (03-B3) : compare, sur toute la période couverte par le serveur, ses décalages à ceux de ce poste
 * (bornes de segments ET décalages). Toute différence signifie que serveur et poste ne placent pas les mêmes heures
 * murales aux mêmes instants (O-03-22) : l'agenda de cette période est alors bloqué. Une période réclamée non
 * couverte (`range`) est aussi un écart.
 */
export function findOffsetDivergence(
  server: readonly OffsetSegment[],
  timeZone: string,
  range?: { fromUtc: string; toUtc: string },
): OffsetDivergence | null {
  if (server.length === 0) return range ? { atUtc: range.fromUtc, serverOffsetMinutes: null, localOffsetMinutes: null } : null;
  const same = (a: string, b: string) => Temporal.Instant.compare(Temporal.Instant.from(a), Temporal.Instant.from(b)) === 0;
  const first = server[0].fromUtc;
  const last = server[server.length - 1].toUtc;
  if (range && (!same(first, range.fromUtc) || !same(last, range.toUtc))) {
    return { atUtc: range.fromUtc, serverOffsetMinutes: server[0].offsetMinutes, localOffsetMinutes: offsetAt(Temporal.Instant.from(range.fromUtc), timeZone) };
  }
  const earlier = (a: string, b: string) => (Temporal.Instant.compare(Temporal.Instant.from(a), Temporal.Instant.from(b)) <= 0 ? a : b);
  const serverAt = (utc: string) => {
    const instant = Temporal.Instant.from(utc);
    const segment = server.find(
      (candidate) => Temporal.Instant.compare(Temporal.Instant.from(candidate.fromUtc), instant) <= 0 && Temporal.Instant.compare(instant, Temporal.Instant.from(candidate.toUtc)) < 0,
    );
    return segment?.offsetMinutes ?? null;
  };
  const divergence = (atUtc: string): OffsetDivergence => ({
    atUtc,
    serverOffsetMinutes: serverAt(atUtc),
    localOffsetMinutes: offsetAt(Temporal.Instant.from(atUtc), timeZone),
  });
  const local = localOffsetSegments(first, last, timeZone);
  for (let index = 0; index < Math.max(local.length, server.length); index += 1) {
    const mine = local[index];
    const theirs = server[index];
    if (!mine || !theirs) return divergence((mine ?? theirs).fromUtc);
    // Début ou décalage différents : écart dès le début du segment ; sinon à la première des deux fins.
    if (!same(mine.fromUtc, theirs.fromUtc) || mine.offsetMinutes !== theirs.offsetMinutes) return divergence(earlier(mine.fromUtc, theirs.fromUtc));
    if (!same(mine.toUtc, theirs.toUtc)) return divergence(earlier(mine.toUtc, theirs.toUtc));
  }
  return null;
}

export const TIME_OPTION_REASONS = {
  past: "passée",
  nonexistent: "n'existe pas ce jour-là",
  ambiguous: "existe deux fois ce jour-là",
} as const;

/**
 * Heures de début proposées (03-D1) : journée ENTIÈRE du magasin au pas serveur (jamais limitée à la plage affichée
 * de la grille). Désactivées, avec leur motif : heures déjà passées le jour même (fuseau du magasin), heures
 * inexistantes ou ambiguës d'un jour de changement d'heure (D-03-13). Sans date choisie, toutes sont proposées. Le
 * serveur revérifie tout (passé, créneau entier, conflits, capacité) : aucune heure n'est dite « disponible ».
 */
export function startTimeOptions(
  date: CivilDate | null,
  stepMinutes: number,
  timeZone: string,
  now: Temporal.Instant = Temporal.Now.instant(),
): { value: string; disabledReason?: string }[] {
  const values: string[] = [];
  for (let minutes = 0; minutes < 24 * 60; minutes += stepMinutes) {
    values.push(`${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`);
  }
  if (date === null || !isCivilDate(date)) return values.map((value) => ({ value }));
  const nowWall = now.toZonedDateTimeISO(timeZone);
  const today = nowWall.toPlainDate().toString();
  const nowTime = nowWall.toPlainTime().toString({ smallestUnit: "minute" });
  // Jour sans changement d'heure (cas courant) : décalage identique à 00:00 et le lendemain, journée de 24 h.
  const day = Temporal.PlainDate.from(date);
  const start = day.toZonedDateTime({ timeZone });
  const next = day.add({ days: 1 }).toZonedDateTime({ timeZone });
  const regular = start.offsetNanoseconds === next.offsetNanoseconds && start.until(next).total("hours") === 24 && start.hour === 0;
  return values.map((value) => {
    if (date < today || (date === today && value <= nowTime)) return { value, disabledReason: TIME_OPTION_REASONS.past };
    if (!regular) {
      const zoned = wallTimeToUtc(date, value, timeZone);
      if (!zoned.ok && zoned.reason !== "invalid") return { value, disabledReason: TIME_OPTION_REASONS[zoned.reason] };
    }
    return { value };
  });
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
