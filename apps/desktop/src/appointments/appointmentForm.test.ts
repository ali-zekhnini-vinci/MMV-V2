// Contrat Rendez-vous (parseurs stricts) et règles de saisie construites depuis les paramètres serveur (03-B,
// aligné en 03-B2 sur D-03-13 à D-03-18 approuvées : mode d'attribution par motif, capacité d'accueil, pas de
// cinq minutes, heures ambiguës refusées).
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { type AppointmentOptions, appointmentListQuery, parseAppointmentDetail, parseAppointmentOptions, parseAppointmentSummary } from "../api/appointments.ts";
import { appointmentFormSchema, appointmentServerFieldErrors, EMPTY_APPOINTMENT_FORM, responsibleMode, toAppointmentInput } from "./appointmentForm.ts";

const OPTIONS: AppointmentOptions = {
  timeZone: "Europe/Brussels",
  durationsMinutes: [15, 30, 45],
  startStepMinutes: 5,
  reasons: [
    { code: "conseil_devis", label: "Conseil / Devis", responsibleRequired: true },
    { code: "retrait_commande", label: "Retrait de commande", responsibleRequired: false },
  ],
  responsibles: [{ id: "r-1", displayName: "Responsable Un" }],
  receptionCapacity: 1,
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
  reasonCode: "retrait_commande",
  status: "planned",
  version: 3,
};

describe("parseurs du contrat", () => {
  test("résumé et détail valides ; intervalle vide, statut inconnu ou version négative refusés", () => {
    assert.deepEqual(parseAppointmentSummary(SUMMARY), SUMMARY);
    assert.equal(parseAppointmentSummary({ ...SUMMARY, endsAtUtc: SUMMARY.startsAtUtc }), null);
    assert.equal(parseAppointmentSummary({ ...SUMMARY, status: "honoured" }), null);
    assert.equal(parseAppointmentSummary({ ...SUMMARY, version: -1 }), null);
    const detail = {
      ...SUMMARY,
      notes: null,
      cancelledAtUtc: null,
      cancelledByDisplayName: null,
      cancellationReason: null,
      createdAtUtc: "2026-07-01T08:00:00Z",
      updatedAtUtc: "2026-07-01T08:00:00Z",
      updatedByDisplayName: "Agent Fictif",
    };
    assert.deepEqual(parseAppointmentDetail(detail), detail);
    // Auteur de la dernière modification obligatoire (O-03-23, 03-B3).
    assert.equal(parseAppointmentDetail({ ...detail, updatedByDisplayName: null }), null);
    assert.equal(parseAppointmentDetail({ ...detail, notes: 12 }), null);
    assert.equal(parseAppointmentDetail({ ...detail, cancelledByDisplayName: 1 }), null);
  });

  test("paramètres : fuseau inconnu, plage inversée, durée, capacité ou mode invalides refusés ; champs inconnus ignorés", () => {
    assert.deepEqual(parseAppointmentOptions({ ...OPTIONS, extra: true }), OPTIONS);
    assert.deepEqual(parseAppointmentOptions({ ...OPTIONS, receptionCapacity: null, displayEndTime: "24:00" }), {
      ...OPTIONS,
      receptionCapacity: null,
      displayEndTime: "24:00",
    });
    assert.equal(parseAppointmentOptions({ ...OPTIONS, timeZone: "Fuseau/Inconnu" }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, displayEndTime: "08:00" }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, durationsMinutes: [0] }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, hiddenWeekdays: [0, 1, 2, 3, 4, 5, 6] }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, receptionCapacity: 0 }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, startStepMinutes: "5" }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, reasons: [{ code: "x", label: "X" }] }), null);
    assert.equal(parseAppointmentOptions({ ...OPTIONS, reasons: [] }), null);
  });

  test("requête de période", () => {
    assert.equal(
      appointmentListQuery({ fromUtc: "2026-07-15T22:00:00Z", toUtc: "2026-07-16T22:00:00Z", status: "planned" }),
      "from=2026-07-15T22%3A00%3A00Z&to=2026-07-16T22%3A00%3A00Z&status=planned",
    );
  });
});

describe("formulaire", () => {
  const reception = {
    ...EMPTY_APPOINTMENT_FORM,
    clientId: "c-1",
    clientLabel: "Nom",
    date: "16/07/2026",
    time: "10:30",
    durationMinutes: "45",
    reasonCode: "retrait_commande",
  };
  const personal = { ...reception, reasonCode: "conseil_devis" };
  const issues = (options: AppointmentOptions, values: typeof reception) => {
    const result = appointmentFormSchema(options).safeParse(values);
    return result.success ? [] : result.error.issues.map((issue) => `${String(issue.path[0])}: ${issue.message}`);
  };

  test("mode d'attribution par motif (D-03-14) et capacité d'accueil (D-03-15)", () => {
    assert.equal(responsibleMode(OPTIONS, "conseil_devis"), "required");
    assert.equal(responsibleMode(OPTIONS, "retrait_commande"), "optional");
    assert.equal(responsibleMode({ ...OPTIONS, receptionCapacity: null }, "retrait_commande"), "reception-unavailable");
    assert.equal(responsibleMode(OPTIONS, ""), "unknown");

    assert.deepEqual(issues(OPTIONS, reception), []);
    assert.deepEqual(issues(OPTIONS, personal), ["responsibleUserId: Choisissez un responsable : ce motif est un rendez-vous personnalisé"]);
    assert.deepEqual(issues(OPTIONS, { ...personal, responsibleUserId: "r-1" }), []);
    assert.deepEqual(issues({ ...OPTIONS, receptionCapacity: null }, reception), [
      "responsibleUserId: Aucune capacité d'accueil n'est configurée pour ce magasin : choisissez un responsable",
    ]);
    assert.deepEqual(issues(OPTIONS, { ...reception, responsibleUserId: "inconnu" }), [
      "responsibleUserId: Collaborateur inactif ou non habilité à recevoir des rendez-vous",
    ]);
  });

  test("listes blanches servies par le serveur ; pas de cinq minutes ; heures inexistantes et ambiguës refusées", () => {
    assert.equal(appointmentFormSchema(OPTIONS).safeParse({ ...reception, durationMinutes: "20" }).success, false);
    assert.equal(appointmentFormSchema(OPTIONS).safeParse({ ...reception, reasonCode: "controle_vue" }).success, false);
    assert.deepEqual(issues(OPTIONS, { ...reception, time: "10:32" }), ["time: Heure de début au pas de 5 minutes (ex. 10:05)"]);
    assert.deepEqual(issues(OPTIONS, { ...reception, date: "29/03/2026", time: "02:30" }), ["time: Cette heure n'existe pas ce jour-là (changement d'heure)"]);
    assert.deepEqual(issues(OPTIONS, { ...reception, date: "25/10/2026", time: "02:30" }), [
      "time: Cette heure existe deux fois ce jour-là (changement d'heure) : choisissez une autre heure",
    ]);
  });

  test("corps envoyé : instant UTC calculé dans le fuseau du magasin, notes vides → null", () => {
    assert.deepEqual(toAppointmentInput(reception, OPTIONS), {
      clientId: "c-1",
      responsibleUserId: null,
      startsAtUtc: "2026-07-16T08:30:00Z",
      // Heure murale annoncée au serveur, qui la recalcule (B2-REV-03).
      startsAtLocal: "2026-07-16T10:30",
      durationMinutes: 45,
      reasonCode: "retrait_commande",
      notes: null,
    });
  });

  test("erreurs serveur par champ → messages bornés, champs inconnus ignorés", () => {
    assert.deepEqual(
      appointmentServerFieldErrors({
        startsAtUtc: ["time_change"],
        responsibleUserId: ["reception_not_configured"],
        reasonCode: ["not_allowed"],
        inconnu: ["x"],
      }),
      [
        ["time", "Ce créneau touche un changement d'heure : choisissez un autre horaire"],
        ["responsibleUserId", "Aucune capacité d'accueil n'est configurée pour ce magasin : choisissez un responsable"],
        ["reasonCode", "Motif non proposé par le serveur"],
      ],
    );
    assert.deepEqual(appointmentServerFieldErrors({ startsAtUtc: ["in_past"], responsibleUserId: ["not_eligible"] }), [
      ["time", "Ce créneau est déjà passé"],
      ["responsibleUserId", "Collaborateur inactif ou non habilité à recevoir des rendez-vous"],
    ]);
  });
});

describe("refus serveur des fuseaux (03-B3)", () => {
  test("startsAtLocal : time_zone_mismatch affiché sous l'heure, sans le texte du serveur", () => {
    assert.deepEqual(appointmentServerFieldErrors({ startsAtLocal: ["time_zone_mismatch"] }), [
      ["time", "L'heure de ce poste diffère de celle du serveur MMV pour cette date (données de fuseau horaire différentes) : rien n'a été enregistré"],
    ]);
  });
});
