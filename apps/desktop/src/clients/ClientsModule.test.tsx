// @vitest-environment jsdom
// Écrans Clients (phase 03-A) avec le service ACTIVÉ pour le test uniquement, contre le double node:http
// du contrat PROPOSED (src/test/fakeClientsApi.ts) et des clients FICTIFS. En fonctionnement normal, le
// service reste désactivé (CLIENTS_API_ENABLED = false) tant que G03-METIER n'est pas ratifié.
import assert from "node:assert/strict";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test } from "vitest";
import { Gate } from "../App.tsx";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { CLIENT_PERMISSIONS, type FakeClientSeed } from "../test/fakeClientsApi.ts";
import { type FakeApi, type FakeApiOptions, startFakeApi } from "../test/fakeMmvApi.ts";
import { ClientsServiceContext } from "./clientsService.ts";

const PASSWORD = "Mot-de-passe-test-1";
const ALL = Object.values(CLIENT_PERMISSIONS);

const FICTIFS: FakeClientSeed[] = [
  { firstName: "Yasmine", lastName: "El Amrani", phone: "06 12 90 33 77", email: "yasmine@exemple.test", city: "Casablanca" },
  { firstName: "Youssef", lastName: "Amrani", phone: "06 21 66 30 58", city: "Oujda" },
  { firstName: "Omar", lastName: "Benali", phone: "06 00 44 21 02", email: "omar@exemple.test", city: "Rabat" },
  { firstName: "Nadia", lastName: "Cherkaoui", phone: "06 44 18 65 90", city: "Tanger", archived: true },
];

let api: FakeApi | null = null;

afterEach(async () => {
  cleanup();
  await api?.stop().catch(() => undefined);
  api = null;
});

async function openClients(options: FakeApiOptions & { permissions?: string[] } = {}) {
  window.innerWidth = 1920;
  const { permissions = ALL, ...rest } = options;
  api = await startFakeApi({
    accounts: [{ userName: "agent", password: PASSWORD, displayName: "Agent Test", permissions }],
    clients: FICTIFS,
    ...rest,
  });
  const user = userEvent.setup();
  render(
    <ClientsServiceContext.Provider value={{ enabled: true }}>
      <AuthProvider baseUrl={{ ok: true, url: api.url }}>
        <Gate />
      </AuthProvider>
    </ClientsServiceContext.Provider>,
  );
  await user.type(screen.getByLabelText("Identifiant"), "agent");
  await user.type(screen.getByLabelText("Mot de passe"), `${PASSWORD}{Enter}`);
  await screen.findByRole("heading", { name: "Bonjour, Agent Test" });
  await user.click(within(screen.getByRole("navigation", { name: "Navigation" })).getByRole("button", { name: /^Clients/ }));
  await screen.findByRole("heading", { name: "Clients", level: 1 });
  return { user, api };
}

const lastListRequest = (fake: FakeApi) => {
  const call = [...fake.requests].reverse().find((request) => request.path.startsWith("/api/clients?"));
  return new URLSearchParams(call?.path.split("?")[1]);
};

const rowNames = () =>
  within(screen.getByRole("table"))
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("cell")[0].querySelector(".clients-cell__name")?.textContent ?? "");

describe("liste des clients", () => {
  test("colonnes, actifs uniquement par défaut, badges et pied de pagination", async () => {
    const { api } = await openClients();
    const table = await screen.findByRole("table");
    const headers = within(table).getAllByRole("columnheader").map((header) => header.textContent);
    assert.deepEqual(headers, ["Client", "Téléphone", "Email", "Ville", "Créé le", "Statut", "Actions"]);
    assert.deepEqual(rowNames(), ["AMRANI Youssef", "BENALI Omar", "EL AMRANI Yasmine"]);
    screen.getByText("3 clients · actifs uniquement");
    assert.equal(within(table).getAllByText("Actif").length, 3);
    assert.equal(within(table).getByRole("columnheader", { name: /Client/ }).getAttribute("aria-sort"), "ascending");
    assert.match(screen.getByRole("navigation", { name: "Pagination des clients" }).textContent ?? "", /Affichage de 1–3 sur 3 clients/);
    assert.equal(lastListRequest(api).get("status"), "active");
  });

  test("tri serveur au clic sur l'en-tête puis via le menu de tri", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    await user.click(screen.getByRole("button", { name: "Trier par client" }));
    await waitFor(() => assert.equal(lastListRequest(api).get("dir"), "desc"));
    await waitFor(() => assert.equal(rowNames()[0], "EL AMRANI Yasmine"));
    assert.equal(screen.getByRole("columnheader", { name: /Client/ }).getAttribute("aria-sort"), "descending");

    await user.click(screen.getByRole("button", { name: /^Trier :/ }));
    await user.click(await screen.findByRole("menuitemradio", { name: "Ville (A → Z)" }));
    await waitFor(() => assert.equal(lastListRequest(api).get("sort"), "city"));
    await waitFor(() => assert.equal(rowNames()[0], "EL AMRANI Yasmine"));
  });

  test("recherche différée, mise en évidence, puis aucun résultat et effacement", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    const search = screen.getByRole("searchbox", { name: "Rechercher un client" });
    await user.type(search, "amr");
    await waitFor(() => assert.equal(lastListRequest(api).get("q"), "amr"));
    await waitFor(() => assert.equal(rowNames().length, 2));
    assert.equal(screen.getAllByText("AMR", { selector: "mark" }).length, 2);

    await user.clear(search);
    await user.type(search, "inconnu");
    await screen.findByRole("heading", { name: "Aucun résultat pour « inconnu »" });
    await user.click(screen.getByRole("button", { name: "Effacer la recherche" }));
    await waitFor(() => assert.equal(rowNames().length, 3));
    assert.equal((search as HTMLInputElement).value, "");
  });

  test("inclure les archivés : paramètre serveur, ligne et badge identifiables", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    const chip = screen.getByRole("button", { name: "Inclure les clients archivés" });
    assert.equal(chip.getAttribute("aria-pressed"), "false");
    await user.click(chip);
    await waitFor(() => assert.equal(lastListRequest(api).get("status"), "all"));
    await waitFor(() => assert.equal(rowNames().length, 4));
    assert.equal(chip.getAttribute("aria-pressed"), "true");
    within(screen.getByRole("table")).getByText("Archivé");
    screen.getByRole("button", { name: "Réactiver CHERKAOUI Nadia" });
    screen.getByText("4 clients · actifs et archivés");
  });

  test("pagination serveur : 20 par page, page suivante puis précédente", async () => {
    const many = Array.from({ length: 23 }, (_, index) => ({ firstName: "Test", lastName: `Fictif${String(index).padStart(2, "0")}` }));
    const { user, api } = await openClients({ clients: many });
    await screen.findByRole("table");
    assert.equal(rowNames().length, 20);
    const pagination = screen.getByRole("navigation", { name: "Pagination des clients" });
    assert.equal((within(pagination).getByRole("button", { name: "Page précédente" }) as HTMLButtonElement).disabled, true);
    await user.click(within(pagination).getByRole("button", { name: "Page suivante" }));
    await waitFor(() => assert.equal(rowNames().length, 3));
    assert.equal(lastListRequest(api).get("page"), "2");
    assert.match(pagination.textContent ?? "", /Affichage de 21–23 sur 23 clients/);
    assert.equal((within(pagination).getByRole("button", { name: "Page suivante" }) as HTMLButtonElement).disabled, true);
    await user.click(within(pagination).getByRole("button", { name: "Page précédente" }));
    await waitFor(() => assert.equal(rowNames().length, 20));
  });

  test("états : chargement puis vide", async () => {
    await openClients({ clients: [], clientsDelayMs: 150 });
    await screen.findByText("Chargement des clients…");
    await screen.findByRole("heading", { name: "Aucun client enregistré" });
    assert.equal(screen.queryByRole("table"), null);
  });

  test("erreur technique (500) puis Réessayer", async () => {
    const { user, api } = await openClients({ clientsListStatus: 500 });
    const alert = await screen.findByRole("alert");
    within(alert).getByRole("heading", { name: "Impossible de charger les clients" });
    api.options.clientsListStatus = undefined;
    await user.click(within(alert).getByRole("button", { name: "Réessayer" }));
    await screen.findByRole("table");
  });

  test("refus serveur (403) : écran d'accès non autorisé, aucune donnée", async () => {
    await openClients({ permissions: ["system.admin"] });
    await screen.findByRole("heading", { name: "Accès non autorisé" });
    assert.equal(screen.queryByRole("table"), null);
  });

  test("archivage depuis la liste : confirmation, requête versionnée, confirmation affichée", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    await user.click(screen.getByRole("button", { name: "Archiver BENALI Omar" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Archiver ce client ?" });
    await user.click(within(dialog).getByRole("button", { name: "Archiver le client" }));
    await screen.findByText("Client archivé");
    await waitFor(() => assert.equal(rowNames().length, 2));
    assert.equal(api.clients.find((client) => client.lastName === "Benali")?.status, "archived");
  });

  test("conflit de concurrence à l'archivage : message métier, données actualisées", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    // Un autre poste modifie la fiche entre l'affichage et l'action.
    const benali = api.clients.find((client) => client.lastName === "Benali");
    assert.ok(benali);
    benali.version += 1;
    await user.click(screen.getByRole("button", { name: "Archiver BENALI Omar" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Archiver le client" }));
    const failure = await screen.findByRole("alertdialog", { name: "Action impossible" });
    within(failure).getByText(/modifié depuis un autre poste/);
    assert.equal(benali.status, "active");
  });
});

describe("formulaire client", () => {
  test("validation : messages sous les champs, focus sur le premier champ en erreur, aucune requête", async () => {
    const { user, api } = await openClients();
    await user.click(screen.getByRole("button", { name: "Nouveau client" }));
    await screen.findByRole("heading", { name: /Nouveau client/ });
    const before = api.requests.length;
    await user.type(screen.getByLabelText("Téléphone"), "0612");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("Veuillez corriger les erreurs du formulaire");
    screen.getByText("Le prénom est requis");
    screen.getByText("Le nom est requis");
    screen.getByText(/entre 8 et 15 chiffres/);
    assert.equal(document.activeElement, screen.getByLabelText("Prénom"));
    assert.equal(screen.getByLabelText("Prénom").getAttribute("aria-invalid"), "true");
    assert.equal(api.requests.slice(before).some((request) => request.method === "POST"), false);
  });

  test("création au clavier (Ctrl+S) puis écran « Client créé » et fiche", async () => {
    const { user, api } = await openClients();
    await user.click(screen.getByRole("button", { name: "Nouveau client" }));
    await user.type(await screen.findByLabelText("Prénom"), "Anas");
    await user.type(screen.getByLabelText("Nom"), "Fictif");
    await user.type(screen.getByLabelText("Date de naissance"), "14/09/1991");
    screen.getByText("Modifications non enregistrées");
    await user.keyboard("{Control>}s{/Control}");
    await screen.findByRole("heading", { name: "Client créé" });
    screen.getByText("FICTIF Anas");
    const created = api.clients.at(-1);
    assert.equal(created?.birthDate, "1991-09-14");
    await user.click(screen.getByRole("button", { name: "Voir la fiche" }));
    await screen.findByRole("heading", { name: "FICTIF Anas", level: 1 });
  });

  test("erreur de validation serveur reportée sous le champ", async () => {
    const { user } = await openClients();
    await user.click(screen.getByRole("button", { name: "Nouveau client" }));
    await user.type(await screen.findByLabelText("Prénom"), "Anas");
    await user.type(screen.getByLabelText("Nom"), "Fictif");
    await user.type(screen.getByLabelText("Email"), "anas@exemple.invalid");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("Format invalide");
    assert.equal(document.activeElement, screen.getByLabelText("Email"));
  });

  test("modifications non enregistrées : confirmation sur Annuler et sur le menu du Shell", async () => {
    const { user } = await openClients();
    await user.click(screen.getByRole("button", { name: "Nouveau client" }));
    await user.type(await screen.findByLabelText("Prénom"), "Brouillon");

    await user.click(screen.getByRole("button", { name: "Annuler" }));
    let dialog = await screen.findByRole("alertdialog", { name: "Quitter sans enregistrer ?" });
    await user.click(within(dialog).getByRole("button", { name: "Continuer la modification" }));
    await waitFor(() => assert.equal(screen.queryByRole("alertdialog"), null));
    assert.equal((screen.getByLabelText("Prénom") as HTMLInputElement).value, "Brouillon");

    await user.click(within(screen.getByRole("navigation", { name: "Navigation" })).getByRole("button", { name: "Accueil" }));
    dialog = await screen.findByRole("alertdialog", { name: "Quitter sans enregistrer ?" });
    await user.click(within(dialog).getByRole("button", { name: "Quitter sans enregistrer" }));
    await screen.findByRole("heading", { name: "Bonjour, Agent Test" });
  });

  test("formulaire non modifié : Annuler revient à la liste sans confirmation", async () => {
    const { user } = await openClients();
    await user.click(screen.getByRole("button", { name: "Nouveau client" }));
    await screen.findByLabelText("Prénom");
    await user.click(screen.getByRole("button", { name: "Annuler" }));
    await screen.findByRole("table");
    assert.equal(screen.queryByRole("alertdialog"), null);
  });

  test("modification : pré-remplie, conflit de version -> rechargement de la fiche", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    await user.click(screen.getByRole("button", { name: "Modifier BENALI Omar" }));
    const city = await screen.findByLabelText("Ville");
    assert.equal((city as HTMLInputElement).value, "Rabat");
    screen.getByText("BENALI Omar · CLI-000003");
    await user.clear(city);
    await user.type(city, "Salé");
    const benali = api.clients.find((client) => client.lastName === "Benali");
    assert.ok(benali);
    benali.version += 1; // autre poste
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Action impossible" });
    await user.click(within(dialog).getByRole("button", { name: "Recharger la fiche" }));
    await screen.findByRole("heading", { name: "BENALI Omar", level: 1 });
    assert.equal(benali.city, "Rabat");
  });
});

describe("fiche client", () => {
  test("onglets au clavier ; ordonnances et ventes explicitement reportées, sans compteur", async () => {
    const { user } = await openClients();
    await screen.findByRole("table");
    await user.click(screen.getByRole("button", { name: "Consulter EL AMRANI Yasmine" }));
    await screen.findByRole("heading", { name: "EL AMRANI Yasmine", level: 1 });
    const tabs = screen.getAllByRole("tab").map((tab) => tab.textContent);
    assert.deepEqual(tabs, ["Informations", "Ordonnances", "Ventes & commandes"]);
    within(screen.getByRole("tabpanel")).getByText("Casablanca");

    screen.getByRole("tab", { name: "Informations" }).focus();
    await user.keyboard("{ArrowRight}");
    assert.equal(screen.getByRole("tab", { name: "Ordonnances" }).getAttribute("aria-selected"), "true");
    within(screen.getByRole("tabpanel")).getByRole("heading", { name: "Ordonnances — phase ultérieure" });
    await user.keyboard("{ArrowRight}");
    within(screen.getByRole("tabpanel")).getByRole("heading", { name: "Ventes et commandes — phase ultérieure" });
    assert.equal(screen.queryByRole("table"), null);
  });

  test("client archivé : lecture seule, réactivation confirmée", async () => {
    const { user, api } = await openClients();
    await screen.findByRole("table");
    await user.click(screen.getByRole("button", { name: "Inclure les clients archivés" }));
    await user.click(await screen.findByRole("button", { name: "Consulter CHERKAOUI Nadia" }));
    await screen.findByText("Client archivé");
    assert.equal((screen.getByRole("button", { name: "Modifier" }) as HTMLButtonElement).disabled, true);
    await user.click(screen.getByRole("button", { name: "Réactiver" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Réactiver le client" }));
    await screen.findByText("Client réactivé");
    await waitFor(() => assert.equal((screen.getByRole("button", { name: "Modifier" }) as HTMLButtonElement).disabled, false));
    assert.equal(api.clients.find((client) => client.lastName === "Cherkaoui")?.status, "active");
  });

  test("retour à la liste : critères de recherche conservés", async () => {
    const { user } = await openClients();
    await screen.findByRole("table");
    await user.type(screen.getByRole("searchbox", { name: "Rechercher un client" }), "benali");
    await waitFor(() => assert.equal(rowNames().length, 1));
    await user.click(screen.getByRole("button", { name: "Consulter BENALI Omar" }));
    await user.click(await screen.findByRole("button", { name: "Retour à la liste" }));
    await waitFor(() => assert.equal(rowNames().length, 1));
    assert.equal((screen.getByRole("searchbox", { name: "Rechercher un client" }) as HTMLInputElement).value, "benali");
  });
});

describe("session et cache", () => {
  test("déconnexion : le cache des clients est vidé, aucune donnée ne survit", async () => {
    const { user } = await openClients();
    await screen.findByRole("table");
    await user.click(screen.getByRole("button", { name: /^Menu utilisateur/ }));
    await user.click(await screen.findByRole("menuitem", { name: "Se déconnecter" }));
    await screen.findByLabelText("Identifiant");
    assert.equal(screen.queryByText("BENALI Omar"), null);
  });
});
