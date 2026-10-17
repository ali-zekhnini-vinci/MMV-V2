// Contrat Rendez-vous PROPOSÉ (parseurs stricts) et règles de saisie construites depuis les paramètres serveur.
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { type AppointmentOptions, appointmentListQuery, parseAppointmentDetail, parseAppointmentOptions, parseAppointmentSummary } from "../api/appointments.ts";
import { appointmentFormSchema, appointmentServerFieldErrors, EMPTY_APPOINTMENT_FORM, toAppointmentInput } from "./appointmentForm.ts";

const OPTIONS: AppointmentOptions = {
  timeZone: "Europe/Paris",
  durationsMinutes: [15, 30, 45],
  reasons: [{ code: "motif-a", label: "Motif fictif A" }],
  responsibles: [{ id: "r-1", displayName: "Responsable Un" }],
  responsibleRequired: false,
  displayStartTime: "09:00",
  displayEndTime: "19:00",
  hiddenWeekdays: [0],
};

const SUMMARY = {
  id: "a-1",
  clientId: "c-1",
  clientReference: "CLI-000001",
  clientFirstName: "Prénom",
  clientLastName: "Nom",
  clientArchived: false,
  responsibleUserId: null,
  responsibleDisplayName: null,
  startsAtUtc: "2026-07-16T08:30:00Z",
  endsAtUtc: "2026-07-16T09:15:00Z",
  reasonCode: "motif-a",
  status: "planned",
  version: 3,
};

describe("parseurs du contrat proposé", () => {
  test("résumé et détail valides ; intervalle vide, statut inconnu ou version négative refusés", () => {
    assert.deepEqual(parseAppointmentSummary(SUMMARY), SUMMARY);
    assert.equal(parseAppointmentSummary({ ...SUMMARY, endsAtUtc: SUMMARY.startsAtUtc }), null);
    assert.equal(parseAppointmentSummary({ ...SUMMARY, status: "honoured" }), null);
    assert.equal(parseAppointmentSummary({ ...SUMMARY, version: -1 }), null);
    const detail = { ...SUMMARY, notes: null, cancelledAtUtc: null, cancellationReason: null, createdAtUtc: "2026-07-01T08:00:00Z", updatedAtUtc: "2026-07-01T08:00:00Z" };
    assert.deepEqual(parseAppointmentDetail(detail), detail);
    assert.equal(parseAppointmentDetail({ ...detail, notes: 12 }), null);
  });

  test("paramètres : fuseau inconnu, plage inversée ou durée invalide refusés ; champs inconnus ignorés", () => {
    assert.deepEqual(parseAppointmentOptions({ ...OPTIONS, extra: true }), OPTIONS);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, timeZone: "Fuseau/Inconnu" }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, displayEndTime: "08:00" }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, durationsMinutes: [0] }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, hiddenWeekdays: [0, 1, 2, 3, 4, 5, 6] }), null);
  });

  test("requête de période", () => {
    assert.equal(
      appointmentListQuery({ fromUtc: "2026-07-15T22:00:00Z", toUtc: "2026-07-16T22:00:00Z", status: "planned" }),
      "from=2026-07-15T22%3A00%3A00Z&to=2026-07-16T22%3A00%3A00Z&status=planned",
    );
  });
});

describe("formulaire", () => {
  const filled = { ...EMPTY_APPOINTMENT_FORM, clientId: "c-1", clientLabel: "Nom", date: "16/07/2026", time: "10:30", durationMinutes: "45", reasonCode: "motif-a" };

  test("listes blanches servies par le serveur ; responsable facultatif ou obligatoire selon le paramètre", () => {
    const schema = appointmentFormSchema(OPTIONS);
    assert.equal(schema.safeParse(filled).success, true);
    assert.equal(schema.safeParse({ ...filled, durationMinutes: "20" }).success, false);
    assert.equal(schema.safeParse({ ...filled, reasonCode: "inconnu" }).success, false);
    assert.equal(appointmentFormSchema({ ...OPTIONS, responsibleRequired: true }).safeParse(filled).success, false);
    assert.equal(appointmentFormSchema({ ...OPTIONS, responsibleRequired: true }).safeParse({ ...filled, responsibleUserId: "r-1" }).success, true);
  });

  test("corps envoyé : instant UTC calculé dans le fuseau du magasin, notes vides → null", () => {
    assert.deepEqual(toAppointmentInput(filled, OPTIONS), {
      clientId: "c-1",
      responsibleUserId: null,
      startsAtUtc: "2026-07-16T08:30:00Z",
      durationMinutes: 45,
      reasonCode: "motif-a",
      notes: null,
    });
  });

  test("erreurs serveur par champ → messages bornés, champs inconnus ignorés", () => {
    assert.deepEqual(appointmentServerFieldErrors({ startsAtUtc: ["in_past"], reasonCode: ["not_allowed"], inconnu: ["x"] }), [
      ["time", "Ce créneau est déjà passé"],
      ["reasonCode", "Motif non proposé par le serveur"],
    ]);
  });
});
