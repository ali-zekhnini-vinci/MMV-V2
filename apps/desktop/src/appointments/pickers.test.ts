// 03-D1 : heures proposées dans le fuseau du magasin (Belgique, Maroc), préremplissage depuis la grille, saisie
// clavier de la liste des heures, dates du calendrier sans décalage quel que soit le fuseau du poste.
import assert from "node:assert/strict";
import { Temporal } from "temporal-polyfill";
import { afterEach, describe, test } from "vitest";
import type { AppointmentOptions } from "../api/appointments.ts";
import { calendarDateToCivil, civilToCalendarDate, typedTime } from "../components/ui/picker-values.ts";
import { slotFormPreset } from "./appointmentForm.ts";
import { eventClassName } from "./appointmentsView.ts";
import { startTimeOptions, TIME_OPTION_REASONS } from "./time.ts";

const BRUSSELS = "Europe/Brussels";
const CASABLANCA = "Africa/Casablanca";
const NOW = Temporal.Instant.from("2026-07-16T07:00:00Z"); // 09:00 à Bruxelles, 08:00 à Casablanca

const disabled = (options: ReturnType<typeof startTimeOptions>, reason: string) =>
  options.filter((option) => option.disabledReason === reason).map((option) => option.value);

describe("heures proposées (fuseau du magasin)", () => {
  test("journée entière au pas serveur, jamais limitée à la plage affichée de la grille", () => {
    const options = startTimeOptions("2026-07-20", 5, BRUSSELS, NOW);
    assert.equal(options.length, 288);
    assert.equal(options[0].value, "00:00");
    assert.equal(options.at(-1)?.value, "23:55");
    assert.ok(options.every((option) => option.disabledReason === undefined));
    assert.equal(startTimeOptions(null, 15, BRUSSELS, NOW).length, 96);
  });

  test("jour même : heures passées désactivées selon l'heure du MAGASIN (Bruxelles 09:00, Casablanca 08:00)", () => {
    const brussels = disabled(startTimeOptions("2026-07-16", 5, BRUSSELS, NOW), TIME_OPTION_REASONS.past);
    assert.equal(brussels.at(-1), "09:00");
    assert.equal(brussels.length, 9 * 12 + 1);
    const casablanca = disabled(startTimeOptions("2026-07-16", 5, CASABLANCA, NOW), TIME_OPTION_REASONS.past);
    assert.equal(casablanca.at(-1), "08:00");
    // Veille dans le fuseau du magasin : tout est passé.
    assert.equal(disabled(startTimeOptions("2026-07-15", 5, BRUSSELS, NOW), TIME_OPTION_REASONS.past).length, 288);
  });

  test("Belgique : 02:00–02:55 inexistantes (28/03/2027) et ambiguës (25/10/2026)", () => {
    const spring = disabled(startTimeOptions("2027-03-28", 5, BRUSSELS, NOW), TIME_OPTION_REASONS.nonexistent);
    assert.deepEqual([spring[0], spring.at(-1), spring.length], ["02:00", "02:55", 12]);
    const autumn = disabled(startTimeOptions("2026-10-25", 5, BRUSSELS, NOW), TIME_OPTION_REASONS.ambiguous);
    assert.deepEqual([autumn[0], autumn.at(-1), autumn.length], ["02:00", "02:55", 12]);
    assert.equal(disabled(startTimeOptions("2026-10-24", 5, BRUSSELS, NOW), TIME_OPTION_REASONS.ambiguous).length, 0);
  });

  test("Maroc (données ICU de l'environnement, comme time.test.ts) : 02:00–02:55 inexistantes le 22/03/2026, 01:00–01:55 ambiguës le 20/09/2026", () => {
    const january = Temporal.Instant.from("2026-01-10T10:00:00Z");
    const spring = disabled(startTimeOptions("2026-03-22", 5, CASABLANCA, january), TIME_OPTION_REASONS.nonexistent);
    assert.deepEqual([spring[0], spring.at(-1), spring.length], ["02:00", "02:55", 12]);
    const autumn = disabled(startTimeOptions("2026-09-20", 5, CASABLANCA, NOW), TIME_OPTION_REASONS.ambiguous);
    assert.deepEqual([autumn[0], autumn.at(-1), autumn.length], ["01:00", "01:55", 12]);
    assert.equal(startTimeOptions("2026-10-20", 5, CASABLANCA, NOW).filter((option) => option.disabledReason).length, 0);
  });
});

describe("préremplissage depuis la grille FullCalendar", () => {
  const options = (timeZone: string): AppointmentOptions => ({
    timeZone,
    durationsMinutes: [15, 30, 45, 60],
    startStepMinutes: 5,
    reasons: [{ code: "x", label: "X", responsibleRequired: false }],
    responsibles: [],
    receptionCapacity: 1,
    displayStartTime: "09:00",
    displayEndTime: "19:00",
    hiddenWeekdays: [],
  });

  test("date, heure et durée dans le fuseau du magasin (Belgique et Maroc)", () => {
    assert.deepEqual(slotFormPreset("2026-07-16T08:30:00Z", "2026-07-16T09:00:00Z", options(BRUSSELS)), { date: "16/07/2026", time: "10:30", durationMinutes: "30" });
    assert.deepEqual(slotFormPreset("2026-07-16T08:30:00Z", "2026-07-16T09:00:00Z", options(CASABLANCA)), { date: "16/07/2026", time: "09:30", durationMinutes: "30" });
    // Créneau de 23:30 heure de Bruxelles : date du magasin (et non la date UTC).
    assert.deepEqual(slotFormPreset("2026-07-16T21:30:00Z", "2026-07-16T22:00:00Z", options(BRUSSELS)).date, "16/07/2026");
    assert.deepEqual(slotFormPreset("2026-07-16T22:30:00Z", "2026-07-16T22:45:00Z", options(BRUSSELS)), { date: "17/07/2026", time: "00:30", durationMinutes: "15" });
  });

  test("aperçu de sélection d'un créneau libre (sans rendez-vous) : classes sans plantage (défaut trouvé en natif 03-D1)", () => {
    assert.equal(eventClassName(options(BRUSSELS), undefined, null, true), "rdv-ev rdv-ev--tone-none rdv-ev--mirror");
  });

  test("durée non proposée par le serveur : laissée à choisir", () => {
    assert.equal(slotFormPreset("2026-07-16T08:30:00Z", "2026-07-16T10:00:00Z", options(BRUSSELS)).durationMinutes, "");
  });
});

describe("liste des heures au clavier", () => {
  test("chiffres tapés → heure visée", () => {
    assert.equal(typedTime("9"), "09:00");
    assert.equal(typedTime("14"), "14:00");
    assert.equal(typedTime("143"), "14:30");
    assert.equal(typedTime("1435"), "14:35");
    assert.equal(typedTime("0905"), "09:05");
    assert.equal(typedTime("25"), null);
    assert.equal(typedTime("1275"), null);
    assert.equal(typedTime(""), null);
  });
});

describe("dates du calendrier sans décalage", () => {
  const original = process.env.TZ;
  afterEach(() => {
    process.env.TZ = original;
  });

  test("aller-retour exact quel que soit le fuseau du poste (Kiritimati UTC+14, Los Angeles, Casablanca, Bruxelles)", () => {
    for (const zone of ["Pacific/Kiritimati", "America/Los_Angeles", CASABLANCA, BRUSSELS, "UTC"]) {
      process.env.TZ = zone;
      for (const civil of ["2026-01-01", "2026-02-15", "2026-03-29", "2026-10-25", "2026-12-31", "2028-02-29"]) {
        const date = civilToCalendarDate(civil);
        assert.ok(date, `${zone} ${civil}`);
        assert.equal(calendarDateToCivil(date), civil, `${zone} ${civil}`);
        assert.equal(date.getDate(), Number(civil.slice(8)), `${zone} ${civil} : jour local`);
      }
    }
  });

  test("dates invalides refusées", () => {
    assert.equal(civilToCalendarDate("2026-02-30"), undefined);
    assert.equal(civilToCalendarDate("16/07/2026"), undefined);
    assert.equal(civilToCalendarDate(""), undefined);
  });
});
