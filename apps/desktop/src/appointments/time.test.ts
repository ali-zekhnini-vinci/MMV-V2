// Fuseau du magasin (proposition D-03-13) : conversions, bornes de vue, changements d'heure, libellés français.
import assert from "node:assert/strict";
import { Temporal } from "temporal-polyfill";
import { describe, test } from "vitest";
import {
  formatDuration,
  formatLongDate,
  formatNumericDate,
  formatShortDay,
  formatTimeRange,
  formatWeekShort,
  formatWeekTitle,
  isCivilDate,
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

  test("heure inexistante (passage à l'heure d'été) refusée, heure ambiguë = première occurrence", () => {
    assert.deepEqual(wallTimeToUtc("2026-03-29", "02:30", "Europe/Paris"), { ok: false, reason: "nonexistent" });
    assert.deepEqual(wallTimeToUtc("2026-10-25", "02:30", "Europe/Paris"), { ok: true, utc: "2026-10-25T00:30:00Z" });
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
