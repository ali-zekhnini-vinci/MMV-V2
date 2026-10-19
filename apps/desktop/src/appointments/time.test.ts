// Fuseau du magasin (proposition D-03-13) : conversions, bornes de vue, changements d'heure, libellés français.
import assert from "node:assert/strict";
import { Temporal } from "temporal-polyfill";
import { describe, test } from "vitest";
import {
  findOffsetDivergence,
  formatDuration,
  formatLongDate,
  formatNumericDate,
  formatShortDay,
  formatTimeRange,
  formatWeekShort,
  formatWeekTitle,
  isCivilDate,
  localOffsetSegments,
  localWallDateTime,
  minutesBetween,
  shiftDate,
  todayIn,
  utcToWall,
  viewRangeUtc,
  visibleDays,
  wallTimeToUtc,
  weekStart,
} from "./time.ts";

describe("conversions dans le fuseau du magasin", () => {
  test("heure murale ↔ UTC, indépendantes du fuseau du poste", () => {
    assert.deepEqual(wallTimeToUtc("2026-07-16", "10:30", "Europe/Paris"), { ok: true, utc: "2026-07-16T08:30:00Z" });
    assert.deepEqual(wallTimeToUtc("2026-01-16", "10:30", "Europe/Paris"), { ok: true, utc: "2026-01-16T09:30:00Z" });
    assert.deepEqual(utcToWall("2026-07-16T08:30:00Z", "Europe/Paris"), { date: "2026-07-16", time: "10:30" });
    assert.deepEqual(utcToWall("2026-07-16T23:30:00Z", "Europe/Paris"), { date: "2026-07-17", time: "01:30" });
  });

  // 03-B2 (D-03-13 approuvée) : l'heure ambiguë n'est plus résolue à la première occurrence, elle est refusée.
  test("Belgique : heure inexistante et heure ambiguë refusées", () => {
    assert.deepEqual(wallTimeToUtc("2026-03-29", "02:30", "Europe/Brussels"), { ok: false, reason: "nonexistent" });
    assert.deepEqual(wallTimeToUtc("2026-10-25", "02:30", "Europe/Brussels"), { ok: false, reason: "ambiguous" });
    assert.deepEqual(wallTimeToUtc("2026-03-29", "03:00", "Europe/Brussels"), { ok: true, utc: "2026-03-29T01:00:00Z" });
    assert.deepEqual(wallTimeToUtc("2026-10-25", "03:00", "Europe/Brussels"), { ok: true, utc: "2026-10-25T02:00:00Z" });
  });

  test("Maroc : heure inexistante et heure ambiguë refusées (données ICU de l'environnement)", () => {
    // 22/03/2026 : 02:00 (UTC+0) → 03:00 (UTC+1) ; 20/09/2026 : 02:00 (UTC+1) → 01:00 (UTC+0).
    assert.deepEqual(wallTimeToUtc("2026-03-22", "02:30", "Africa/Casablanca"), { ok: false, reason: "nonexistent" });
    assert.deepEqual(wallTimeToUtc("2026-09-20", "01:30", "Africa/Casablanca"), { ok: false, reason: "ambiguous" });
    assert.deepEqual(wallTimeToUtc("2026-03-22", "03:30", "Africa/Casablanca"), { ok: true, utc: "2026-03-22T02:30:00Z" });
    assert.deepEqual(wallTimeToUtc("2026-10-20", "10:00", "Africa/Casablanca"), { ok: true, utc: "2026-10-20T10:00:00Z" });
  });

  test("saisies invalides", () => {
    assert.equal(isCivilDate("2026-02-30"), false);
    assert.equal(isCivilDate("16/07/2026"), false);
    assert.deepEqual(wallTimeToUtc("2026-07-16", "25:00", "Europe/Paris"), { ok: false, reason: "invalid" });
    assert.deepEqual(wallTimeToUtc("2026-07-16", "10:30", "Fuseau/Inconnu"), { ok: false, reason: "invalid" });
  });

  test("aujourd'hui dans le fuseau du magasin", () => {
    const instant = Temporal.Instant.from("2026-07-16T23:30:00Z");
    assert.equal(todayIn("Europe/Paris", instant), "2026-07-17");
    assert.equal(todayIn("UTC", instant), "2026-07-16");
  });

  test("durée réelle sur un changement d'heure", () => {
    const { fromUtc, toUtc } = viewRangeUtc("2026-10-25", "day", "Europe/Paris");
    assert.equal(minutesBetween(fromUtc, toUtc), 25 * 60);
  });
});

describe("vues jour et semaine", () => {
  test("semaine ISO (lundi), navigation, jours masqués", () => {
    assert.equal(weekStart("2026-07-16"), "2026-07-13");
    assert.equal(weekStart("2026-07-19"), "2026-07-13");
    assert.equal(shiftDate("2026-07-16", "day", 1), "2026-07-17");
    assert.equal(shiftDate("2026-07-16", "week", -1), "2026-07-09");
    assert.deepEqual(visibleDays("2026-07-16", "week", [0]), ["2026-07-13", "2026-07-14", "2026-07-15", "2026-07-16", "2026-07-17", "2026-07-18"]);
    assert.deepEqual(visibleDays("2026-07-16", "day", [0]), ["2026-07-16"]);
  });

  test("bornes UTC de la vue, fin exclue", () => {
    assert.deepEqual(viewRangeUtc("2026-07-16", "day", "Europe/Paris"), { fromUtc: "2026-07-15T22:00:00Z", toUtc: "2026-07-16T22:00:00Z" });
    assert.deepEqual(viewRangeUtc("2026-07-16", "week", "UTC"), { fromUtc: "2026-07-13T00:00:00Z", toUtc: "2026-07-20T00:00:00Z" });
  });

  test("libellés français des maquettes", () => {
    assert.equal(formatLongDate("2026-07-16"), "Jeudi 16 juillet 2026");
    assert.equal(formatShortDay("2026-07-16"), "Jeu. 16 juil.");
    assert.equal(formatNumericDate("2026-07-16"), "16/07/2026");
    const week = visibleDays("2026-07-16", "week", [0]);
    assert.equal(formatWeekTitle(week), "Semaine du 13 au 18 juillet 2026");
    assert.equal(formatWeekShort(week), "13 – 18 juil. 2026");
    assert.equal(formatWeekTitle(visibleDays("2026-07-30", "week", [0])), "Semaine du 27 juillet au 1 août 2026");
    assert.equal(formatTimeRange("2026-07-16T08:30:00Z", "2026-07-16T09:15:00Z", "Europe/Paris"), "10:30 – 11:15");
    assert.equal(formatDuration(45), "45 minutes");
    assert.equal(formatDuration(60), "1 h");
    assert.equal(formatDuration(90), "1 h 30");
  });
});

// B2-REV-03 (03-B3) : cohérence des conversions horaires entre le serveur (Windows) et ce poste (ICU). Les dates de
// transition citées sont celles des données ICU 2026c de Node 24 (constat Q-03-121, relevé en 03-B3) ; WebView2 est
// contrôlée à l'exécution par la comparaison des décalages servis avec chaque liste.
describe("cohérence des fuseaux serveur ↔ poste", () => {
  test("décalages du poste : semaines avec et sans transition, Bruxelles et Casablanca, dates futures", () => {
    assert.deepEqual(localOffsetSegments("2026-10-18T22:00:00Z", "2026-10-25T23:00:00Z", "Europe/Brussels"), [
      { fromUtc: "2026-10-18T22:00:00Z", toUtc: "2026-10-25T01:00:00Z", offsetMinutes: 120 },
      { fromUtc: "2026-10-25T01:00:00Z", toUtc: "2026-10-25T23:00:00Z", offsetMinutes: 60 },
    ]);
    assert.deepEqual(localOffsetSegments("2027-03-21T23:00:00Z", "2027-03-28T22:00:00Z", "Europe/Brussels"), [
      { fromUtc: "2027-03-21T23:00:00Z", toUtc: "2027-03-28T01:00:00Z", offsetMinutes: 60 },
      { fromUtc: "2027-03-28T01:00:00Z", toUtc: "2027-03-28T22:00:00Z", offsetMinutes: 120 },
    ]);
    assert.deepEqual(localOffsetSegments("2026-09-13T23:00:00Z", "2026-09-21T00:00:00Z", "Africa/Casablanca"), [
      { fromUtc: "2026-09-13T23:00:00Z", toUtc: "2026-09-20T01:00:00Z", offsetMinutes: 60 },
      { fromUtc: "2026-09-20T01:00:00Z", toUtc: "2026-09-21T00:00:00Z", offsetMinutes: 0 },
    ]);
    assert.deepEqual(localOffsetSegments("2027-02-07T00:00:00Z", "2027-02-14T00:00:00Z", "Africa/Casablanca"), [
      { fromUtc: "2027-02-07T00:00:00Z", toUtc: "2027-02-14T00:00:00Z", offsetMinutes: 0 },
    ]);
    assert.deepEqual(localOffsetSegments("2028-07-02T22:00:00Z", "2028-07-09T22:00:00Z", "Europe/Brussels"), [
      { fromUtc: "2028-07-02T22:00:00Z", toUtc: "2028-07-09T22:00:00Z", offsetMinutes: 120 },
    ]);
  });

  test("décalages concordants (y compris aux transitions) : aucun écart", () => {
    const week = { fromUtc: "2026-10-18T22:00:00Z", toUtc: "2026-10-25T23:00:00Z" };
    const server = [
      { fromUtc: "2026-10-18T22:00:00+00:00", toUtc: "2026-10-25T01:00:00+00:00", offsetMinutes: 120 },
      { fromUtc: "2026-10-25T01:00:00+00:00", toUtc: "2026-10-25T23:00:00+00:00", offsetMinutes: 60 },
    ];
    assert.equal(findOffsetDivergence(server, "Europe/Brussels", week), null);
    assert.equal(findOffsetDivergence(localOffsetSegments("2026-09-13T23:00:00Z", "2026-09-21T00:00:00Z", "Africa/Casablanca"), "Africa/Casablanca"), null);
  });

  test("divergence réelle constatée (O-03-22) : Windows place Casablanca à UTC+0 dès le 01/01/2026, ICU le 15/02/2026", () => {
    // Décalages tels que MMV.Api (Windows) les calcule pour la semaine du 05/01/2026 (sonde 03-B3).
    const windows = [{ fromUtc: "2026-01-04T23:00:00Z", toUtc: "2026-01-11T23:00:00Z", offsetMinutes: 0 }];
    assert.deepEqual(findOffsetDivergence(windows, "Africa/Casablanca", { fromUtc: "2026-01-04T23:00:00Z", toUtc: "2026-01-11T23:00:00Z" }), {
      atUtc: "2026-01-04T23:00:00Z",
      serverOffsetMinutes: 0,
      localOffsetMinutes: 60,
    });
  });

  test("transition déplacée d'une heure, transition manquante, couverture incomplète : écart signalé", () => {
    const tz = "Europe/Brussels";
    const shifted = [
      { fromUtc: "2026-10-18T22:00:00Z", toUtc: "2026-10-25T02:00:00Z", offsetMinutes: 120 },
      { fromUtc: "2026-10-25T02:00:00Z", toUtc: "2026-10-25T23:00:00Z", offsetMinutes: 60 },
    ];
    assert.deepEqual(findOffsetDivergence(shifted, tz), { atUtc: "2026-10-25T01:00:00Z", serverOffsetMinutes: 120, localOffsetMinutes: 60 });
    const missing = [{ fromUtc: "2026-10-18T22:00:00Z", toUtc: "2026-10-25T23:00:00Z", offsetMinutes: 120 }];
    assert.deepEqual(findOffsetDivergence(missing, tz), { atUtc: "2026-10-25T01:00:00Z", serverOffsetMinutes: 120, localOffsetMinutes: 60 });
    const partial = [{ fromUtc: "2026-11-02T23:00:00Z", toUtc: "2026-11-05T23:00:00Z", offsetMinutes: 60 }];
    assert.notEqual(findOffsetDivergence(partial, tz, { fromUtc: "2026-11-01T23:00:00Z", toUtc: "2026-11-08T23:00:00Z" }), null);
    assert.notEqual(findOffsetDivergence([], tz, { fromUtc: "2026-11-01T23:00:00Z", toUtc: "2026-11-08T23:00:00Z" }), null);
  });

  test("heure murale annoncée au serveur : celle du magasin, de part et d'autre des transitions", () => {
    assert.equal(localWallDateTime("2026-10-25T00:59:00Z", "Europe/Brussels"), "2026-10-25T02:59");
    assert.equal(localWallDateTime("2026-10-25T01:00:00Z", "Europe/Brussels"), "2026-10-25T02:00");
    assert.equal(localWallDateTime("2026-09-20T00:59:00Z", "Africa/Casablanca"), "2026-09-20T01:59");
    assert.equal(localWallDateTime("2026-09-20T01:00:00Z", "Africa/Casablanca"), "2026-09-20T01:00");
    assert.equal(localWallDateTime("2027-02-10T09:00:00Z", "Africa/Casablanca"), "2027-02-10T09:00");
  });
});
