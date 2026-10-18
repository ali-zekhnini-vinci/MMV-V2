// @vitest-environment jsdom
// Écrans Rendez-vous (03-B, 03-B2) contre le double node:http du contrat implémenté (src/test/fakeAppointmentsApi.ts),
// avec des clients et rendez-vous FICTIFS, motifs et durées approuvés (D-03-13, D-03-18). Le contexte fournit une
// horloge fixe. Fuseau du double : Europe/Brussels (UTC+2 en juillet), distinct du fuseau du poste pour prouver que
// l'affichage suit le fuseau servi par le serveur. Le glisser-déposer n'est pas éprouvable sous jsdom (géométrie
// absente). Capacité d'accueil du double : 1.
import assert from "node:assert/strict";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test } from "vitest";
import { Gate } from "../App.tsx";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { APPOINTMENT_PERMISSIONS, FAKE_APPOINTMENT_OPTIONS, type FakeAppointmentSeed } from "../test/fakeAppointmentsApi.ts";
import { CLIENT_PERMISSIONS, type FakeClientSeed } from "../test/fakeClientsApi.ts";
import { type FakeApi, type FakeApiOptions, startFakeApi } from "../test/fakeMmvApi.ts";
import { AppointmentsServiceContext } from "./appointmentsService.ts";
// Préchargement du module différé (FullCalendar) : le premier test ne paie pas la transformation dans son délai.
import "./AppointmentsModule.tsx";

const PASSWORD = "Mot-de-passe-test-1";
const ALL = [...Object.values(APPOINTMENT_PERMISSIONS), CLIENT_PERMISSIONS.read];
const NOW = new Date("2026-07-16T07:00:00Z"); // jeudi 16/07/2026, 09:00 à Paris

const CLIENTS: FakeClientSeed[] = [
  { firstName: "Yasmine", lastName: "El Amrani", phone: "06 12 90 33 77", city: "Casablanca" },
  { firstName: "Omar", lastName: "Benali", phone: "06 00 44 21 02", city: "Rabat" },
  { firstName: "Nadia", lastName: "Cherkaoui", city: "Tanger", archived: true },
];

// 10:30–11:15 et 14:00–15:00 (heure de Paris) le jeudi, 10:00 le vendredi.
const APPOINTMENTS: FakeAppointmentSeed[] = [
  { clientLastName: "El Amrani", startsAtUtc: "2026-07-16T08:30:00Z", durationMinutes: 45, reasonCode: "essayage_ajustement", responsible: 0, notes: "Note interne fictive." },
  { clientLastName: "Benali", startsAtUtc: "2026-07-16T12:00:00Z", durationMinutes: 60, reasonCode: "conseil_devis", responsible: 1 },
  { clientLastName: "Benali", startsAtUtc: "2026-07-17T08:00:00Z", durationMinutes: 30, reasonCode: "retrait_commande" },
];

let api: FakeApi | null = null;

afterEach(async () => {
  cleanup();
  await api?.stop().catch(() => undefined);
  api = null;
});

async function openAgenda(options: FakeApiOptions & { permissions?: string[]; enabled?: boolean } = {}) {
  window.innerWidth = 1920;
  const { permissions = ALL, enabled = true, ...rest } = options;
  api = await startFakeApi({
    accounts: [{ userName: "agent", password: PASSWORD, displayName: "Agent Test", permissions }],
    clients: CLIENTS,
    appointments: APPOINTMENTS,
    ...rest,
  });
  const user = userEvent.setup();
  render(
    <AppointmentsServiceContext.Provider value={{ enabled, now: () => NOW }}>
      <AuthProvider baseUrl={{ ok: true, url: api.url }}>
        <Gate />
      </AuthProvider>
    </AppointmentsServiceContext.Provider>,
  );
  await user.type(screen.getByLabelText("Identifiant"), "agent");
  await user.type(screen.getByLabelText("Mot de passe"), `${PASSWORD}{Enter}`);
  await screen.findByRole("heading", { name: "Bonjour, Agent Test" });
  await user.click(within(screen.getByRole("navigation", { name: "Navigation" })).getByRole("button", { name: /^Rendez-vous/ }));
  await screen.findByRole("heading", { name: "Rendez-vous", level: 1 });
  return { user, api };
}

const agenda = () => screen.getByRole("region", { name: "Agenda" });
const eventButton = async (name: RegExp) => within(await screen.findByRole("region", { name: "Agenda" })).findByRole("button", { name });
const lastRequest = (fake: FakeApi, prefix: string) => [...fake.requests].reverse().find((request) => request.path.startsWith(prefix));
/** Panneau latéral Rendez-vous (l'aside du Shell est aussi un « complementary »). */
const sidePanel = () => screen.queryByRole("complementary", { name: /rendez-vous|Sélectionner un client/i });
/** Identité stricte sans inspection : un échec d'assert.equal sur un nœud jsdom construirait un message énorme. */
const isSame = (actual: Element | null, expected: Element | null, label: string) => assert.ok(actual === expected, label);
const writes = (fake: FakeApi) => fake.requests.filter((request) => request.method !== "GET" && request.path.startsWith("/api/appointments"));

describe("agenda jour / semaine", () => {
  test("service non activé : état explicite, aucune requête", async () => {
    const { api } = await openAgenda({ enabled: false });
    await screen.findByRole("heading", { name: "Service Rendez-vous non activé" });
    assert.equal(api.requests.some((request) => request.path.startsWith("/api/appointments")), false);
    assert.equal(screen.queryByRole("grid"), null);
  });

  test("vue Jour : date du magasin, rendez-vous à l'heure de Bruxelles, période demandée en UTC", async () => {
    const { api } = await openAgenda();
    await screen.findByText("Jeudi 16 juillet 2026");
    const first = await eventButton(/Yasmine El Amrani/);
    assert.match(first.textContent ?? "", /10:30 · Essayage \/ Ajustement/);
    // Responsable affiché sur la grille à partir d'une heure (planche 28) ; toujours visible dans le détail.
    assert.doesNotMatch(first.textContent ?? "", /Responsable Un/);
    assert.match((await eventButton(/Omar Benali/)).textContent ?? "", /14:00 · Conseil \/ Devis.*Responsable Deux/);
    const query = new URLSearchParams(lastRequest(api, "/api/appointments?")?.path.split("?")[1]);
    assert.equal(query.get("from"), "2026-07-15T22:00:00Z");
    assert.equal(query.get("to"), "2026-07-16T22:00:00Z");
    assert.equal(query.get("status"), "planned");
    assert.equal(screen.getByRole("button", { name: "Jour" }).getAttribute("aria-pressed"), "true");
  });

  test("vue Semaine, navigation Précédent / Suivant / Aujourd'hui", async () => {
    const { user, api } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getByRole("button", { name: "Semaine" }));
    await screen.findByText("Semaine du 13 au 18 juillet 2026");
    screen.getByText("13 – 18 juil. 2026");
    // La journée précédente reste affichée pendant le chargement de la semaine (keepPreviousData) : attendre la semaine
    // (course du test constatée en 03-B3, 1 rendez-vous au lieu de 2 sur une exécution complète sur six).
    await waitFor(() => assert.equal(within(agenda()).getAllByRole("button", { name: /O\. Benali/ }).length, 2));
    const query = new URLSearchParams(lastRequest(api, "/api/appointments?")?.path.split("?")[1]);
    assert.equal(query.get("from"), "2026-07-12T22:00:00Z");
    assert.equal(query.get("to"), "2026-07-19T22:00:00Z");
    await user.click(screen.getByRole("button", { name: "Semaine suivante" }));
    await screen.findByText("Semaine du 20 au 25 juillet 2026");
    await user.click(screen.getByRole("button", { name: "Jour" }));
    await user.click(screen.getByRole("button", { name: "Aujourd'hui" }));
    await screen.findByText("Jeudi 16 juillet 2026");
  });

  test("journée vide : état dédié, création proposée", async () => {
    const { user } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getByRole("button", { name: "Jour précédent" }));
    await screen.findByText("Aucun rendez-vous ce jour");
    within(agenda()).getByRole("button", { name: "Nouveau rendez-vous" });
  });

  test("erreur technique de chargement : message borné et Réessayer", async () => {
    await openAgenda({ appointmentsListStatus: 500 });
    await screen.findByRole("heading", { name: "Impossible de charger les rendez-vous" });
    screen.getByRole("button", { name: "Réessayer" });
  });

  test("magasin non configuré côté serveur (503) : « Agenda non configuré », aucun calendrier", async () => {
    const { api } = await openAgenda({ appointmentsNotConfigured: true });
    await screen.findByRole("heading", { name: "Agenda non configuré" });
    assert.equal(screen.queryByRole("region", { name: "Agenda" }), null);
    assert.ok(api.requests.some((request) => request.path === "/api/appointments/options"));
  });

  test("droit appointments.read absent : « Accès non autorisé »", async () => {
    await openAgenda({ permissions: ["system.admin"] });
    await screen.findByRole("heading", { name: "Accès non autorisé" });
    assert.equal(screen.queryByRole("region", { name: "Agenda" }), null);
  });
});

describe("clavier et détail", () => {
  test("↑/↓ parcourent les rendez-vous, Entrée ouvre le détail (notes chargées), Échap le ferme", async () => {
    const { user, api } = await openAgenda();
    const first = await eventButton(/Yasmine El Amrani/);
    first.focus();
    await user.keyboard("{ArrowDown}");
    assert.match(document.activeElement?.textContent ?? "", /Omar Benali/);
    await user.keyboard("{ArrowUp}");
    assert.match(document.activeElement?.textContent ?? "", /Yasmine El Amrani/);
    await user.keyboard("{Enter}");
    const panel = await screen.findByRole("complementary", { name: /Détail du rendez-vous de Yasmine El Amrani/ });
    within(panel).getByText("Note interne fictive.");
    within(panel).getByText("10:30 – 11:15");
    within(panel).getByText("45 minutes");
    within(panel).getByText("16/07/2026");
    within(panel).getByText("Planifié");
    assert.ok(lastRequest(api, "/api/appointments/0199c000"), "détail relu depuis l'API");
    await waitFor(() => isSame(document.activeElement, within(panel).getByRole("heading", { name: "Yasmine El Amrani" }), "focus attendu"));
    await user.keyboard("{Escape}");
    await waitFor(() => assert.ok(sidePanel() === null, "panneau fermé"));
  });
});

describe("création", () => {
  test("au clavier : erreurs de validation, sélection du client, envoi en UTC, confirmation", async () => {
    const { user, api } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    assert.equal((within(panel).getByLabelText("Date") as HTMLInputElement).value, "16/07/2026");
    await user.click(within(panel).getByRole("button", { name: "Créer" }));
    within(panel).getByText("Sélectionnez un client");
    within(panel).getByText("Heure requise");
    within(panel).getByText("Choisissez une durée");
    within(panel).getByText("Le motif est requis");
    const clientButton = within(panel).getByRole("button", { name: /Client/ });
    isSame(document.activeElement, clientButton, "focus attendu");
    assert.equal(writes(api).length, 0);

    await user.keyboard("{Enter}");
    const picker = await screen.findByRole("complementary", { name: "Sélectionner un client" });
    const search = within(picker).getByRole("combobox", { name: "Rechercher un client actif" });
    await user.type(search, "benal");
    await within(picker).findByRole("option", { name: /BENALI Omar/ });
    within(picker).getByText("1 client trouvé");
    await user.keyboard("{Enter}");
    const form = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await waitFor(() => assert.match(document.activeElement?.textContent ?? "", /BENALI Omar · CLI-000002/));
    const searches = api.requests.filter((request) => request.path.startsWith("/api/clients?"));
    assert.ok(searches.every((request) => new URLSearchParams(request.path.split("?")[1]).get("status") === "active"));

    await user.type(within(form).getByLabelText("Heure"), "16:00");
    await user.selectOptions(within(form).getByLabelText("Durée"), "30 minutes");
    await user.selectOptions(within(form).getByLabelText("Responsable"), "Responsable Deux");
    await user.selectOptions(within(form).getByLabelText("Motif"), "Retrait de commande");
    await user.type(within(form).getByLabelText("Notes"), "Créneau fictif.");
    await user.click(within(form).getByRole("button", { name: "Créer" }));

    await screen.findByText("Rendez-vous créé");
    screen.getByText("Omar Benali · 16/07/2026 à 16:00 · Retrait de commande.");
    const created = api.appointments.at(-1);
    assert.equal(created?.startsAtUtc, "2026-07-16T14:00:00Z");
    assert.equal(created?.endsAtUtc, "2026-07-16T14:30:00Z");
    assert.equal(created?.responsibleDisplayName, "Responsable Deux");
    assert.equal(created?.notes, "Créneau fictif.");
    await eventButton(/Omar Benali.*16:00/);
  });

  test("conflit horaire (même responsable) : bandeau, rien n'est créé", async () => {
    const { user, api } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.click(within(panel).getByRole("button", { name: /Client/ }));
    await user.type(await screen.findByRole("combobox"), "yasm");
    await user.click(await screen.findByRole("option", { name: /EL AMRANI Yasmine/ }));
    const form = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.type(within(form).getByLabelText("Heure"), "10:45");
    await user.selectOptions(within(form).getByLabelText("Durée"), "30 minutes");
    await user.selectOptions(within(form).getByLabelText("Responsable"), "Responsable Un");
    await user.selectOptions(within(form).getByLabelText("Motif"), "Conseil / Devis");
    const before = api.appointments.length;
    await user.click(within(form).getByRole("button", { name: "Créer" }));
    const alert = await within(form).findByRole("alert");
    assert.match(alert.textContent ?? "", /Conflit horaire/);
    assert.match(alert.textContent ?? "", /10:45 – 11:15 chevauche un autre rendez-vous de ce responsable/);
    assert.equal(api.appointments.length, before);
  });

  test("rendez-vous personnalisé sans responsable : refusé localement (D-03-14)", async () => {
    const { user, api } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.selectOptions(within(panel).getByLabelText("Motif"), "Prise de mesures");
    within(panel).getByText("Rendez-vous personnalisé : responsable obligatoire.");
    await user.click(within(panel).getByRole("button", { name: "Créer" }));
    within(panel).getByText("Choisissez un responsable : ce motif est un rendez-vous personnalisé");
    assert.equal(writes(api).length, 0);
  });

  test("rendez-vous d'accueil sans responsable : capacité atteinte → bandeau, rien n'est créé (D-03-15)", async () => {
    const { user, api } = await openAgenda({
      appointments: [...APPOINTMENTS, { clientLastName: "Benali", startsAtUtc: "2026-07-16T13:00:00Z", durationMinutes: 30, reasonCode: "sav_reparation" }],
    });
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.click(within(panel).getByRole("button", { name: /Client/ }));
    await user.type(await screen.findByRole("combobox"), "yasm");
    await user.click(await screen.findByRole("option", { name: /EL AMRANI Yasmine/ }));
    const form = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.type(within(form).getByLabelText("Heure"), "15:15");
    await user.selectOptions(within(form).getByLabelText("Durée"), "30 minutes");
    await user.selectOptions(within(form).getByLabelText("Motif"), "Retrait de commande");
    within(form).getByText(/Rendez-vous d'accueil : sans responsable, il occupe une place d'accueil \(1 simultanée au plus\)/);
    assert.equal((within(form).getByLabelText("Responsable") as HTMLSelectElement).selectedOptions[0]?.textContent, "Non attribué (accueil)");
    const before = api.appointments.length;
    await user.click(within(form).getByRole("button", { name: "Créer" }));
    const alert = await within(form).findByRole("alert");
    assert.match(alert.textContent ?? "", /Capacité d'accueil atteinte/);
    assert.match(alert.textContent ?? "", /15:15 – 15:45/);
    assert.equal(api.appointments.length, before);

    // 15:30, bout à bout avec la réservation d'accueil existante (15:00–15:30) : la place est libre.
    const time = within(form).getByLabelText("Heure");
    await user.clear(time);
    await user.type(time, "15:30");
    await user.click(within(form).getByRole("button", { name: "Créer" }));
    await screen.findByText("Rendez-vous créé");
    assert.equal(api.appointments.at(-1)?.responsibleUserId, null);
  });

  test("heure ambiguë (retour à l'heure d'hiver) refusée localement", async () => {
    const { user, api } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    const date = within(panel).getByLabelText("Date");
    await user.clear(date);
    await user.type(date, "25/10/2026");
    await user.type(within(panel).getByLabelText("Heure"), "02:30");
    await user.click(within(panel).getByRole("button", { name: "Créer" }));
    within(panel).getByText("Cette heure existe deux fois ce jour-là (changement d'heure) : choisissez une autre heure");
    assert.equal(writes(api).length, 0);
  });

  test("heure inexistante (passage à l'heure d'été) refusée localement", async () => {
    const { user, api } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    const date = within(panel).getByLabelText("Date");
    await user.clear(date);
    await user.type(date, "29/03/2026");
    await user.type(within(panel).getByLabelText("Heure"), "02:30");
    await user.click(within(panel).getByRole("button", { name: "Créer" }));
    within(panel).getByText("Cette heure n'existe pas ce jour-là (changement d'heure)");
    assert.equal(writes(api).length, 0);
  });

  test("fermeture avec saisie en cours : confirmation", async () => {
    const { user } = await openAgenda();
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.type(within(panel).getByLabelText("Heure"), "12:00");
    await user.keyboard("{Escape}");
    const dialog = await screen.findByRole("alertdialog", { name: "Abandonner la saisie ?" });
    await user.click(within(dialog).getByRole("button", { name: "Abandonner" }));
    await waitFor(() => assert.ok(sidePanel() === null, "panneau fermé"));
  });
});

describe("modification, déplacement, annulation", () => {
  async function openDetail(user: ReturnType<typeof userEvent.setup>, name: RegExp) {
    await user.click(await eventButton(name));
    const panel = await screen.findByRole("complementary", { name: /Détail du rendez-vous de / });
    await within(panel).findByText("Responsable");
    return panel;
  }

  test("déplacer au clavier : seules la date et l'heure changent, confirmation", async () => {
    const { user, api } = await openAgenda();
    const detail = await openDetail(user, /Yasmine El Amrani/);
    await user.click(within(detail).getByRole("button", { name: "Déplacer" }));
    const form = await screen.findByRole("complementary", { name: "Déplacer le rendez-vous" });
    within(form).getByText(/Actuellement : 16\/07\/2026 à 10:30/);
    assert.equal(within(form).queryByLabelText("Motif"), null);
    const time = within(form).getByLabelText("Heure");
    isSame(document.activeElement, within(form).getByLabelText("Date"), "focus attendu");
    await user.clear(time);
    await user.type(time, "12:30{Enter}");
    await screen.findByText("Rendez-vous déplacé");
    const moved = api.appointments.find((item) => item.clientLastName === "El Amrani");
    assert.equal(moved?.startsAtUtc, "2026-07-16T10:30:00Z");
    assert.equal(moved?.endsAtUtc, "2026-07-16T11:15:00Z");
    assert.equal(moved?.notes, "Note interne fictive.");
    assert.equal(moved?.version, 2);
  });

  test("déplacement en conflit : bandeau « Conflit horaire », rendez-vous inchangé", async () => {
    const { user, api } = await openAgenda({ appointments: [...APPOINTMENTS, { clientLastName: "Benali", startsAtUtc: "2026-07-16T10:00:00Z", durationMinutes: 30, reasonCode: "conseil_devis", responsible: 0 }] });
    const detail = await openDetail(user, /Yasmine El Amrani/);
    await user.click(within(detail).getByRole("button", { name: "Déplacer" }));
    const form = await screen.findByRole("complementary", { name: "Déplacer le rendez-vous" });
    const time = within(form).getByLabelText("Heure");
    await user.clear(time);
    await user.type(time, "11:45{Enter}");
    const alert = await within(form).findByRole("alert");
    assert.match(alert.textContent ?? "", /Le créneau 11:45 – 12:30 chevauche/);
    assert.equal(api.appointments[0].startsAtUtc, "2026-07-16T08:30:00Z");
  });

  test("modification pré-remplie ; version périmée → « Action impossible »", async () => {
    const { user, api } = await openAgenda();
    const detail = await openDetail(user, /Yasmine El Amrani/);
    await user.click(within(detail).getByRole("button", { name: "Modifier" }));
    const form = await screen.findByRole("complementary", { name: "Modifier le rendez-vous" });
    assert.equal((within(form).getByLabelText("Heure") as HTMLInputElement).value, "10:30");
    assert.equal((within(form).getByLabelText("Durée") as HTMLSelectElement).value, "45");
    assert.equal((within(form).getByLabelText("Notes") as HTMLTextAreaElement).value, "Note interne fictive.");
    api.appointments[0].version = 7; // un autre poste a modifié le rendez-vous
    await user.selectOptions(within(form).getByLabelText("Motif"), "Conseil / Devis");
    await user.click(within(form).getByRole("button", { name: "Enregistrer" }));
    const alert = await within(form).findByRole("alert");
    assert.match(alert.textContent ?? "", /Action impossible/);
    assert.equal(api.appointments[0].reasonCode, "essayage_ajustement");
  });

  test("annulation avec motif puis rétablissement", async () => {
    const { user, api } = await openAgenda();
    const detail = await openDetail(user, /Yasmine El Amrani/);
    await user.click(within(detail).getByRole("button", { name: "Annuler le rendez-vous" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Annuler ce rendez-vous ?" });
    within(dialog).getByText(/du 16\/07\/2026 à 10:30 sera annulé/);
    await user.type(within(dialog).getByLabelText("Motif d'annulation (facultatif)"), "Indisponibilité fictive");
    await user.click(within(dialog).getByRole("button", { name: "Annuler le rendez-vous" }));
    await screen.findByText("Rendez-vous annulé", { selector: ".clients-notice__title" });
    const panel = screen.getByRole("complementary", { name: /Détail du rendez-vous/ });
    await within(panel).findByText("Motif : Indisponibilité fictive");
    // Auteur et date de l'annulation conservés et affichés (D-03-16).
    await within(panel).findByText(/Annulé le .* par Agent Test/);
    assert.equal(api.appointments[0].status, "cancelled");
    // Masqué de la grille par défaut, visible avec « Afficher les annulés ».
    await waitFor(() => assert.equal(within(agenda()).queryByRole("button", { name: /Yasmine El Amrani/ }), null));
    await user.click(screen.getByRole("button", { name: "Afficher les annulés" }));
    assert.match((await eventButton(/Yasmine El Amrani/)).textContent ?? "", /\(annulé\)/);

    await user.click(within(panel).getByRole("button", { name: "Rétablir le rendez-vous" }));
    await screen.findByText("Rendez-vous rétabli");
    assert.equal(api.appointments[0].status, "planned");
  });

  test("rétablissement refusé (créneau repris entre-temps) : bandeau de conflit", async () => {
    const { user } = await openAgenda({
      appointments: [
        { clientLastName: "El Amrani", startsAtUtc: "2026-07-16T08:30:00Z", durationMinutes: 45, reasonCode: "essayage_ajustement", responsible: 0, cancelled: { reason: null } },
        { clientLastName: "Benali", startsAtUtc: "2026-07-16T08:30:00Z", durationMinutes: 30, reasonCode: "conseil_devis", responsible: 0 },
      ],
    });
    await user.click(await eventButton(/Omar Benali/));
    await screen.findByRole("complementary", { name: /Omar Benali/ });
    await user.click(screen.getByRole("button", { name: "Afficher les annulés" }));
    await user.click(await eventButton(/Yasmine El Amrani.*annulé/));
    const panel = await screen.findByRole("complementary", { name: /Yasmine El Amrani/ });
    within(panel).getByText("Motif : non précisé");
    await user.click(within(panel).getByRole("button", { name: "Rétablir le rendez-vous" }));
    const alert = await within(panel).findByRole("alert");
    assert.match(alert.textContent ?? "", /Conflit horaire/);
  });
});

describe("prise de rendez-vous depuis la fiche client (03-B2)", () => {
  async function openClientFiche(options: FakeApiOptions & { permissions?: string[] } = {}, name = "BENALI Omar") {
    window.innerWidth = 1920;
    const { permissions = ALL, ...rest } = options;
    api = await startFakeApi({
      accounts: [{ userName: "agent", password: PASSWORD, displayName: "Agent Test", permissions }],
      clients: CLIENTS,
      appointments: APPOINTMENTS,
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
    if (name.startsWith("CHERKAOUI")) await user.click(await screen.findByRole("button", { name: "Inclure les clients archivés" }));
    await user.click(await screen.findByRole("button", { name: `Consulter ${name}` }));
    await screen.findByRole("heading", { name, level: 1 });
    return { user, api };
  }

  test("client conservé (aucune seconde recherche), création, retour à la fiche", async () => {
    const { user, api } = await openClientFiche();
    await user.click(screen.getByRole("button", { name: "Prendre un rendez-vous" }));
    const form = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    within(form).getByText("BENALI Omar · CLI-000002");
    assert.equal((within(form).getByLabelText("Date") as HTMLInputElement).value, "16/07/2026");
    const searchesBefore = api.requests.filter((request) => request.path.startsWith("/api/clients?")).length;
    await user.type(within(form).getByLabelText("Heure"), "17:00");
    await user.selectOptions(within(form).getByLabelText("Durée"), "45 minutes");
    await user.selectOptions(within(form).getByLabelText("Motif"), "Choix de monture et verres");
    await user.selectOptions(within(form).getByLabelText("Responsable"), "Responsable Un");
    await user.click(within(form).getByRole("button", { name: "Créer" }));
    await screen.findByText("Rendez-vous créé");
    const created = api.appointments.at(-1);
    assert.equal(created?.clientLastName, "Benali");
    assert.equal(created?.startsAtUtc, "2026-07-16T15:00:00Z");
    assert.equal(api.requests.filter((request) => request.path.startsWith("/api/clients?")).length, searchesBefore, "aucune seconde recherche");

    await user.click(screen.getByRole("button", { name: "Retour à la fiche de BENALI Omar" }));
    await screen.findByRole("heading", { name: "BENALI Omar", level: 1 });
  });

  test("abandon de la saisie : retour direct à la fiche", async () => {
    const { user } = await openClientFiche();
    await user.click(screen.getByRole("button", { name: "Prendre un rendez-vous" }));
    const form = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    await user.click(within(form).getByRole("button", { name: "Retour à la fiche de BENALI Omar" }));
    await screen.findByRole("heading", { name: "BENALI Omar", level: 1 });
  });

  test("client archivé : action désactivée avec motif ; sans appointments.write : action absente", async () => {
    await openClientFiche({}, "CHERKAOUI Nadia");
    const button = screen.getByRole("button", { name: "Prendre un rendez-vous" }) as HTMLButtonElement;
    assert.equal(button.disabled, true);
    assert.match(button.title, /Client archivé/);
    cleanup();
    await api?.stop();
    await openClientFiche({ permissions: [APPOINTMENT_PERMISSIONS.read, CLIENT_PERMISSIONS.read] });
    assert.equal(screen.queryByRole("button", { name: "Prendre un rendez-vous" }), null);
  });
});

describe("permissions de session (garde d'affichage, D-03-17)", () => {
  test("lecture seule : pas de création, actions du détail désactivées avec motif, aucune écriture", async () => {
    const { user, api } = await openAgenda({ permissions: [APPOINTMENT_PERMISSIONS.read] });
    await eventButton(/Yasmine El Amrani/);
    assert.match(screen.getByRole("note").textContent ?? "", /Consultation seule/);
    assert.equal(screen.queryByRole("button", { name: "Nouveau rendez-vous" }), null);
    await user.click(await eventButton(/Yasmine El Amrani/));
    const panel = await screen.findByRole("complementary", { name: /Détail du rendez-vous de Yasmine El Amrani/ });
    await within(panel).findByRole("button", { name: "Modifier" });
    for (const name of ["Modifier", "Déplacer", "Annuler le rendez-vous"]) {
      assert.equal((within(panel).getByRole("button", { name }) as HTMLButtonElement).disabled, true);
    }
    assert.equal(within(panel).queryByRole("button", { name: "Voir la fiche client" }), null);
    assert.equal(writes(api).length, 0);
  });

  test("écriture sans clients.read : sélection du client indisponible, motif affiché", async () => {
    const { user } = await openAgenda({ permissions: [APPOINTMENT_PERMISSIONS.read, APPOINTMENT_PERMISSIONS.write] });
    await eventButton(/Yasmine El Amrani/);
    await user.click(screen.getAllByRole("button", { name: "Nouveau rendez-vous" })[0]);
    const panel = await screen.findByRole("complementary", { name: "Nouveau rendez-vous" });
    assert.equal((within(panel).getByRole("button", { name: /Client/ }) as HTMLButtonElement).disabled, true);
    within(panel).getByText(/ne permet pas de rechercher des clients \(clients\.read\)/);
  });
});

describe("cohérence des fuseaux et rendez-vous commencés (03-B3)", () => {
  async function openDetail(user: ReturnType<typeof userEvent.setup>, name: RegExp) {
    await user.click(await eventButton(name));
    const panel = await screen.findByRole("complementary", { name: /Détail du rendez-vous de / });
    await within(panel).findByText("Responsable");
    return panel;
  }

  test("décalages du serveur différents de ceux du poste : agenda de la période bloqué, aucune création proposée", async () => {
    // Serveur dont les données de fuseaux placeraient Bruxelles à UTC+1 en juillet (écart d'une heure).
    const { api } = await openAgenda({ serverOffsets: (fromUtc, toUtc) => [{ fromUtc, toUtc, offsetMinutes: 60 }] });
    const alert = await screen.findByRole("heading", { name: "Heure incohérente entre ce poste et le serveur MMV" });
    const block = alert.closest("[role='alert']");
    assert.match(block?.textContent ?? "", /UTC\+1 selon le serveur, UTC\+2 selon ce poste/);
    assert.equal(within(agenda()).queryByRole("button", { name: /Yasmine El Amrani/ }), null);
    assert.equal(screen.queryByRole("button", { name: "Nouveau rendez-vous" }), null);
    assert.equal(writes(api).length, 0);
  });

  test("décalages concordants : agenda affiché ; l'écriture annonce l'heure murale au serveur", async () => {
    const { user, api } = await openAgenda();
    const detail = await openDetail(user, /Yasmine El Amrani/);
    within(detail).getByText(/Dernière modification le 01\/07\/2026 à 10:00 par Agent Fictif/);
    await user.click(within(detail).getByRole("button", { name: "Déplacer" }));
    const form = await screen.findByRole("complementary", { name: "Déplacer le rendez-vous" });
    const time = within(form).getByLabelText("Heure");
    await user.clear(time);
    await user.type(time, "12:30{Enter}");
    await screen.findByText("Rendez-vous déplacé");
    // Le double refuse (422 time_zone_mismatch) toute heure murale qui ne correspond pas à l'instant UTC envoyé.
    const moved = api.appointments.find((item) => item.clientLastName === "El Amrani");
    assert.equal(moved?.startsAtUtc, "2026-07-16T10:30:00Z");
    assert.equal(moved?.updatedByDisplayName, "Agent Test");
  });

  test("rendez-vous commencé : Déplacer désactivé avec motif, modification limitée au motif et aux notes", async () => {
    // Commencé à 08:30 (Bruxelles) ; « maintenant » = 09:00.
    const started: FakeAppointmentSeed = { clientLastName: "Benali", startsAtUtc: "2026-07-16T06:30:00Z", durationMinutes: 60, reasonCode: "prise_mesures", responsible: 1 };
    const { user, api } = await openAgenda({ appointments: [...APPOINTMENTS, started], appointmentsNow: NOW.toISOString() });
    await user.click(screen.getByRole("button", { name: "Jour" }));
    const panel = await openDetail(user, /Omar Benali.*08:30|08:30.*Omar Benali/);
    const move = within(panel).getByRole("button", { name: "Déplacer" }) as HTMLButtonElement;
    assert.equal(move.disabled, true);
    assert.match(move.title, /Rendez-vous commencé/);
    within(panel).getByText("Rendez-vous commencé : seuls le motif et les notes peuvent être corrigés.");

    await user.click(within(panel).getByRole("button", { name: "Modifier" }));
    const form = await screen.findByRole("complementary", { name: "Modifier le rendez-vous" });
    for (const label of ["Date", "Heure", "Durée", "Responsable"]) assert.equal(within(form).queryByLabelText(label), null, `${label} affiché`);
    within(form).getByText(/Commencé : 16\/07\/2026 à 08:30/);
    await user.type(within(form).getByLabelText("Notes"), "Mesures prises");
    await user.click(within(form).getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("Rendez-vous modifié");
    const saved = api.appointments.find((item) => item.startsAtUtc === "2026-07-16T06:30:00Z");
    assert.equal(saved?.notes, "Mesures prises");
    assert.equal(saved?.endsAtUtc, "2026-07-16T07:30:00Z");
    assert.equal(saved?.responsibleDisplayName, "Responsable Deux");
  });

  test("responsable qui n'est plus habilité : conservé dans la modification, motif et notes corrigeables", async () => {
    const { user, api } = await openAgenda({
      appointmentOptions: { ...FAKE_APPOINTMENT_OPTIONS, responsibles: [FAKE_APPOINTMENT_OPTIONS.responsibles[1]] },
    });
    // Rendez-vous attribué à « Responsable Un », retiré depuis de la liste des collaborateurs éligibles.
    const former = FAKE_APPOINTMENT_OPTIONS.responsibles[0];
    Object.assign(api.appointments[0], { responsibleUserId: former.id, responsibleDisplayName: former.displayName });
    const panel = await openDetail(user, /Yasmine El Amrani/);
    await user.click(within(panel).getByRole("button", { name: "Modifier" }));
    const form = await screen.findByRole("complementary", { name: "Modifier le rendez-vous" });
    const select = within(form).getByLabelText("Responsable") as HTMLSelectElement;
    assert.equal(select.selectedOptions[0]?.textContent, "Responsable Un (n'est plus habilité)");
    await user.clear(within(form).getByLabelText("Notes"));
    await user.type(within(form).getByLabelText("Notes"), "Note corrigée");
    await user.click(within(form).getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("Rendez-vous modifié");
    const saved = api.appointments.find((item) => item.clientLastName === "El Amrani");
    assert.equal(saved?.notes, "Note corrigée");
    assert.equal(saved?.responsibleDisplayName, "Responsable Un");
    assert.equal(saved?.startsAtUtc, "2026-07-16T08:30:00Z");
  });
});

describe("refus serveur sur un champ non affiché (03-B3)", () => {
  test("déplacement refusé car le responsable n'est plus habilité : message en bandeau, rendez-vous inchangé", async () => {
    const { user, api } = await openAgenda();
    await user.click(await eventButton(/Yasmine El Amrani/));
    const panel = await screen.findByRole("complementary", { name: /Détail du rendez-vous de / });
    await within(panel).findByText("Responsable");
    // Le collaborateur est retiré de l'éligibilité après l'ouverture du détail (autre poste, outil de maintenance).
    api.options.appointmentOptions = { ...FAKE_APPOINTMENT_OPTIONS, responsibles: [FAKE_APPOINTMENT_OPTIONS.responsibles[1]] };
    await user.click(within(panel).getByRole("button", { name: "Déplacer" }));
    const form = await screen.findByRole("complementary", { name: "Déplacer le rendez-vous" });
    const time = within(form).getByLabelText("Heure");
    await user.clear(time);
    await user.type(time, "15:30{Enter}");
    const alert = await within(form).findByRole("alert");
    assert.match(alert.textContent ?? "", /Données refusées.*Collaborateur inactif ou non habilité/);
    assert.equal(api.appointments[0].startsAtUtc, "2026-07-16T08:30:00Z");
    // La liste des éligibles est relue après le refus : le collaborateur n'est plus proposé comme éligible.
    await waitFor(() => assert.ok(api.requests.filter((request) => request.path === "/api/appointments/options").length >= 2));
    await user.click(within(form).getByRole("button", { name: "Annuler" }));
    const detail = await screen.findByRole("complementary", { name: /Détail du rendez-vous de / });
    await user.click(await within(detail).findByRole("button", { name: "Modifier" }));
    const edit = await screen.findByRole("complementary", { name: "Modifier le rendez-vous" });
    assert.equal((within(edit).getByLabelText("Responsable") as HTMLSelectElement).selectedOptions[0]?.textContent, "Responsable Un (n'est plus habilité)");
  });
});
