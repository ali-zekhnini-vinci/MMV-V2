// @vitest-environment jsdom
// Onglet « Historique » de la fiche client (03-D2) contre le double node:http du contrat implémenté
// (src/test/fakeAuditApi.ts), événements FICTIFS. Droit dédié audit.read : onglet absent et aucune requête sans lui.
import assert from "node:assert/strict";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test } from "vitest";
import { Gate } from "../App.tsx";
import { AppointmentsServiceContext } from "../appointments/appointmentsService.ts";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { APPOINTMENT_PERMISSIONS, type FakeAppointmentSeed } from "../test/fakeAppointmentsApi.ts";
import { AUDIT_PERMISSION, type FakeAuditSeed } from "../test/fakeAuditApi.ts";
import { CLIENT_PERMISSIONS, type FakeClientSeed } from "../test/fakeClientsApi.ts";
import { type FakeApi, type FakeApiOptions, startFakeApi } from "../test/fakeMmvApi.ts";
import { AUDIT_ACTION_LABELS, describeAuditChange, formatAuditInstant, periodStart } from "./auditView.ts";

const PASSWORD = "Mot-de-passe-test-1";
const NOW = new Date("2026-07-16T07:00:00Z");
const CLIENTS: FakeClientSeed[] = [{ firstName: "Omar", lastName: "Benali", phone: "06 00 44 21 02", city: "Rabat" }];
const APPOINTMENTS: FakeAppointmentSeed[] = [{ clientLastName: "Benali", startsAtUtc: "2026-07-17T08:00:00Z", durationMinutes: 30, reasonCode: "retrait_commande" }];
const EVENTS: FakeAuditSeed[] = [
  {
    clientLastName: "Benali",
    occurredAtUtc: "2026-07-01T08:00:00Z",
    actorDisplayName: "Agent Accueil",
    module: "clients",
    action: "client.created",
    entityType: "client",
    changes: [
      { field: "firstName", masked: true, old: null, new: null, oldDisplay: null, newDisplay: null },
      { field: "phone", masked: true, old: null, new: null, oldDisplay: null, newDisplay: null },
    ],
  },
  {
    clientLastName: "Benali",
    occurredAtUtc: "2026-07-10T09:30:00Z",
    actorDisplayName: "Agent Accueil",
    module: "appointments",
    action: "appointment.moved",
    entityType: "appointment",
    entityId: "0199c000-0000-7000-8000-000000000001",
    changes: [{ field: "startsAtUtc", masked: false, old: "2026-07-16T08:00:00Z", new: "2026-07-17T08:00:00Z", oldDisplay: "16/07/2026 10:00", newDisplay: "17/07/2026 10:00" }],
  },
  {
    clientLastName: "Benali",
    occurredAtUtc: "2026-07-12T10:00:00Z",
    actorDisplayName: "Responsable Deux",
    module: "appointments",
    action: "appointment.responsible_changed",
    entityType: "appointment",
    entityId: "0199c000-0000-7000-8000-000000000001",
    changes: [{ field: "responsibleUserId", masked: false, old: null, new: "x", oldDisplay: "Non attribué", newDisplay: "Responsable Un" }],
  },
];

let api: FakeApi | null = null;

afterEach(async () => {
  cleanup();
  await api?.stop().catch(() => undefined);
  api = null;
});

async function openFiche(options: FakeApiOptions & { permissions?: string[] } = {}) {
  window.innerWidth = 1920;
  const { permissions = [CLIENT_PERMISSIONS.read, APPOINTMENT_PERMISSIONS.read, AUDIT_PERMISSION], ...rest } = options;
  api = await startFakeApi({
    accounts: [{ userName: "agent", password: PASSWORD, displayName: "Agent Test", permissions }],
    clients: CLIENTS,
    appointments: APPOINTMENTS,
    auditEvents: EVENTS,
    ...rest,
  });
  const user = userEvent.setup();
  render(
    <AppointmentsServiceContext.Provider value={{ enabled: true, now: () => NOW }}>
      <AuthProvider baseUrl={{ ok: true, url: api.url }}>
        <Gate />
      </AuthProvider>
    </AppointmentsServiceContext.Provider>,
  );
  await user.type(screen.getByLabelText("Identifiant"), "agent");
  await user.type(screen.getByLabelText("Mot de passe"), `${PASSWORD}{Enter}`);
  await screen.findByRole("heading", { name: "Bonjour, Agent Test" });
  await user.click(within(screen.getByRole("navigation", { name: "Navigation" })).getByRole("button", { name: /^Clients/ }));
  await user.click(await screen.findByRole("button", { name: "Consulter BENALI Omar" }));
  await screen.findByRole("heading", { name: "BENALI Omar", level: 1 });
  return { user, api };
}

const auditRequests = (fake: FakeApi) => fake.requests.filter((request) => request.path.startsWith("/api/audit/"));

describe("onglet Historique (03-D2)", () => {
  test("chronologie servie par le serveur : plus récent d'abord, auteur, action, champs (valeurs seulement si conservées)", async () => {
    const { user, api } = await openFiche();
    await user.click(screen.getByRole("tab", { name: "Historique" }));
    const tab = await screen.findByRole("region", { name: "Historique des actions" });
    const items = await within(tab).findAllByRole("listitem", { name: undefined });
    const entries = within(tab).getAllByRole("time").map((time) => time.closest("li") as HTMLElement);
    assert.equal(entries.length, 3);
    assert.match(entries[0].textContent ?? "", /Changement de responsable.*Responsable Deux.*Responsable : Non attribué → Responsable Un/);
    assert.match(entries[1].textContent ?? "", /Rendez-vous déplacé.*Agent Accueil.*Date et heure : 16\/07\/2026 10:00 → 17\/07\/2026 10:00/);
    assert.match(entries[2].textContent ?? "", /Création du dossier client.*Agent Accueil/);
    // Champs masqués : nom seulement, aucune valeur.
    within(entries[2]).getByText("Prénom");
    within(entries[2]).getByText("Téléphone");
    assert.doesNotMatch(tab.textContent ?? "", /06 00 44|Rabat/);
    within(tab).getByText("3 sur 3");
    within(tab).getByText(/ne sont pas reconstituées/);
    assert.ok(items.length >= 3);
    assert.equal(auditRequests(api).length, 1);
  });

  test("filtres type d'action et période : requête serveur, état vide", async () => {
    const { user, api } = await openFiche();
    await user.click(screen.getByRole("tab", { name: "Historique" }));
    const tab = await screen.findByRole("region", { name: "Historique des actions" });
    await within(tab).findByText("3 sur 3");
    await user.selectOptions(within(tab).getByLabelText("Type d'action"), "Dossier client");
    await within(tab).findByText("1 sur 1");
    assert.equal(new URLSearchParams(auditRequests(api).at(-1)?.path.split("?")[1]).get("module"), "clients");
    await user.selectOptions(within(tab).getByLabelText("Période"), "7 derniers jours");
    await within(tab).findByText(/Aucune action enregistrée pour ce dossier avec ces filtres/);
    assert.ok(new URLSearchParams(auditRequests(api).at(-1)?.path.split("?")[1]).get("from"));
  });

  test("chargement progressif : 20 actions puis les plus anciennes à la demande", async () => {
    const many: FakeAuditSeed[] = Array.from({ length: 25 }, (_, index) => ({
      clientLastName: "Benali",
      occurredAtUtc: `2026-06-${String(index + 1).padStart(2, "0")}T08:00:00Z`,
      actorDisplayName: "Agent Accueil",
      module: "clients",
      action: "client.updated",
      entityType: "client",
      changes: [{ field: "city", masked: true, old: null, new: null, oldDisplay: null, newDisplay: null }],
    }));
    const { user } = await openFiche({ auditEvents: many });
    await user.click(screen.getByRole("tab", { name: "Historique" }));
    const tab = await screen.findByRole("region", { name: "Historique des actions" });
    await within(tab).findByText("20 sur 25");
    await user.click(within(tab).getByRole("button", { name: "Afficher les actions plus anciennes" }));
    await within(tab).findByText("25 sur 25");
    assert.equal(within(tab).queryByRole("button", { name: "Afficher les actions plus anciennes" }), null);
    assert.equal(within(tab).getAllByRole("time").length, 25);
  });

  test("« Voir le rendez-vous » ouvre l'agenda sur ce rendez-vous, avec retour à la fiche", async () => {
    const { user } = await openFiche({ permissions: [CLIENT_PERMISSIONS.read, APPOINTMENT_PERMISSIONS.read, AUDIT_PERMISSION] });
    await user.click(screen.getByRole("tab", { name: "Historique" }));
    const tab = await screen.findByRole("region", { name: "Historique des actions" });
    await within(tab).findByText("3 sur 3");
    await user.click(within(tab).getAllByRole("button", { name: "Voir le rendez-vous" })[0]);
    // Module différé (agenda) : première transformation plus longue que le délai par défaut.
    const detail = await screen.findByRole("complementary", { name: "Détail du rendez-vous de BENALI Omar" }, { timeout: 10_000 });
    await within(detail).findByText("10:00 – 10:30");
    screen.getByRole("button", { name: "Retour à la fiche de BENALI Omar" });
  });

  test("séparation des droits : sans audit.read, onglet absent et aucune requête ; sans appointments.read, pas de lien vers le rendez-vous", async () => {
    const { api: first } = await openFiche({ permissions: [CLIENT_PERMISSIONS.read, APPOINTMENT_PERMISSIONS.read, APPOINTMENT_PERMISSIONS.write] });
    assert.equal(screen.queryByRole("tab", { name: "Historique" }), null);
    screen.getByRole("tab", { name: "Rendez-vous" });
    assert.equal(auditRequests(first).length, 0);

    cleanup();
    await api?.stop();
    const { user } = await openFiche({ permissions: [CLIENT_PERMISSIONS.read, AUDIT_PERMISSION] });
    assert.equal(screen.queryByRole("tab", { name: "Rendez-vous" }), null);
    await user.click(screen.getByRole("tab", { name: "Historique" }));
    const tab = await screen.findByRole("region", { name: "Historique des actions" });
    await within(tab).findByText("3 sur 3");
    assert.equal(within(tab).queryByRole("button", { name: "Voir le rendez-vous" }), null);
  });

  test("erreur technique : message borné et Réessayer", async () => {
    const { user } = await openFiche({ auditStatus: 500 });
    await user.click(screen.getByRole("tab", { name: "Historique" }));
    await screen.findByRole("heading", { name: "Impossible de charger l'historique" });
    screen.getByRole("button", { name: "Réessayer" });
  });
});

describe("présentation de l'historique", () => {
  test("libellés des dix actions journalisées, champs masqués ou valorisés", () => {
    assert.equal(Object.keys(AUDIT_ACTION_LABELS).length, 10);
    assert.equal(describeAuditChange({ field: "notes", masked: true, old: null, new: null, oldDisplay: null, newDisplay: null }), "Notes");
    assert.equal(describeAuditChange({ field: "reasonCode", masked: false, old: null, new: "conseil_devis", oldDisplay: null, newDisplay: "Conseil / Devis" }), "Motif : Conseil / Devis");
    assert.equal(
      describeAuditChange({ field: "durationMinutes", masked: false, old: "30", new: "45", oldDisplay: "30 min", newDisplay: "45 min" }),
      "Durée : 30 min → 45 min",
    );
  });

  test("heure du poste et début de période", () => {
    assert.equal(formatAuditInstant("2026-07-16T08:00:00Z", "fr-FR", "Europe/Brussels"), "16/07/2026 10:00");
    assert.equal(formatAuditInstant("2026-07-16T08:00:00Z", "fr-FR", "Africa/Casablanca"), "16/07/2026 09:00");
    assert.equal(periodStart("all", NOW), undefined);
    assert.equal(periodStart("7d", NOW), "2026-07-09T07:00:00.000Z");
  });
});
