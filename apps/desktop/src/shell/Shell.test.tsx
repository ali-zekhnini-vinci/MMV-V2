// @vitest-environment jsdom
// Shell : navigation, garde UI, déconnexion, révocation, 403, redémarrage, serveur inaccessible.
// Les droits réels sont prouvés côté serveur (tests/MMV.Api.Tests, tests/MMV.PostgreSql.Tests).
import assert from "node:assert/strict";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, test } from "vitest";
import { Gate } from "../App.tsx";
import { AppointmentsServiceContext } from "../appointments/appointmentsService.ts";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { ClientsServiceContext } from "../clients/clientsService.ts";
import { type FakeApi, startFakeApi } from "../test/fakeMmvApi.ts";

const ADMIN = { userName: "admin", password: "Mot-de-passe-admin-1", displayName: "Admin Test", permissions: ["system.admin"] };
const SIMPLE = { userName: "simple", password: "Mot-de-passe-simple-1", displayName: "Simple Test", permissions: [] };

let api: FakeApi;

beforeEach(async () => {
  window.innerWidth = 1920;
  localStorage.clear();
  api = await startFakeApi({ accounts: [{ ...ADMIN, permissions: [...ADMIN.permissions] }, { ...SIMPLE }] });
});
afterEach(async () => {
  cleanup();
  await api.stop().catch(() => undefined);
});

function mount() {
  return render(
    <AuthProvider baseUrl={{ ok: true, url: api.url }}>
      <Gate />
    </AuthProvider>,
  );
}

async function signIn(account: { userName: string; password: string; displayName: string }) {
  const user = userEvent.setup();
  mount();
  await user.type(screen.getByLabelText("Identifiant"), account.userName);
  await user.type(screen.getByLabelText("Mot de passe"), `${account.password}{Enter}`);
  await screen.findByRole("heading", { name: `Bonjour, ${account.displayName}` });
  return user;
}

const nav = () => screen.getByRole("navigation", { name: "Navigation" });

describe("Shell", () => {
  test("structure : menu, accueil actif, barre d'état réelle, aucun chiffre inventé", async () => {
    await signIn(ADMIN);
    const home = within(nav()).getByRole("button", { name: "Accueil" });
    assert.equal(home.getAttribute("aria-current"), "page");
    await screen.findByText("Base de données disponible");
    screen.getByText("Serveur MMV joignable");
    screen.getByText(/^Session active — fin au plus tard à/);
    screen.getByText(/MMV — ManageMyVision · client \d+\.\d+\.\d+/);
    // La recherche et les notifications n'existent pas encore : désactivées, sans badge.
    assert.equal((screen.getByRole("searchbox") as HTMLInputElement).disabled, true);
    assert.equal(
      (within(screen.getByRole("banner", { name: "Barre supérieure" })).getByRole("button", { name: /^Notifications — Disponible/ }) as HTMLButtonElement).disabled,
      true,
    );
  });

  test("module d'une phase suivante : écran explicite, aucun CRUD", async () => {
    const user = await signIn(ADMIN);
    await user.click(within(nav()).getByRole("button", { name: /^Produits/ }));
    await screen.findByRole("heading", { name: "Produits" });
    screen.getByText(/Disponible dans une phase suivante/);
    assert.equal(screen.queryByRole("table"), null);
  });

  // 03-B2 : service activé par défaut (APPOINTMENTS_API_ENABLED = true). *Historique 03-B : service désactivé,
  // aucune requête /api/appointments (test conservé ci-dessous sous forme d'interrupteur de repli).*
  test("Rendez-vous : system.admin seul n'a aucun droit Rendez-vous -> 403 -> « Accès non autorisé », aucun calendrier", async () => {
    const user = await signIn(ADMIN);
    await user.click(within(nav()).getByRole("button", { name: /^Rendez-vous/ }));
    // Module différé (FullCalendar, calendrier 03-D1) : première transformation plus longue que le délai par défaut.
    await screen.findByRole("heading", { name: "Rendez-vous", level: 1 }, { timeout: 10_000 });
    await screen.findByRole("heading", { name: "Accès non autorisé" });
    assert.equal(screen.queryByRole("grid"), null);
    const optionsCall = api.requests.find((request) => request.path === "/api/appointments/options");
    assert.ok(optionsCall?.bearer, "requête /api/appointments/options authentifiée attendue");
  });

  test("Rendez-vous (interrupteur de repli désactivé) : service non activé, aucune requête /api/appointments", async () => {
    const user = userEvent.setup();
    render(
      <AppointmentsServiceContext.Provider value={{ enabled: false }}>
        <AuthProvider baseUrl={{ ok: true, url: api.url }}>
          <Gate />
        </AuthProvider>
      </AppointmentsServiceContext.Provider>,
    );
    await user.type(screen.getByLabelText("Identifiant"), ADMIN.userName);
    await user.type(screen.getByLabelText("Mot de passe"), `${ADMIN.password}{Enter}`);
    await screen.findByRole("heading", { name: `Bonjour, ${ADMIN.displayName}` });
    await user.click(within(nav()).getByRole("button", { name: /^Rendez-vous/ }));
    await screen.findByRole("heading", { name: "Service Rendez-vous non activé" });
    assert.equal(screen.queryByRole("grid"), null);
    assert.equal(api.requests.some((request) => request.path.startsWith("/api/appointments")), false);
  });

  // 03-A2 : le service Clients est activé par défaut (CLIENTS_API_ENABLED = true).
  test("Clients : system.admin seul n'a aucun droit Clients -> le serveur répond 403 -> « Accès non autorisé »", async () => {
    const user = await signIn(ADMIN);
    await user.click(within(nav()).getByRole("button", { name: /^Clients/ }));
    await screen.findByRole("heading", { name: "Clients", level: 1 });
    await screen.findByRole("heading", { name: "Accès non autorisé" });
    assert.equal(screen.queryByRole("table"), null);
    const listCall = api.requests.find((request) => request.path.startsWith("/api/clients?"));
    assert.ok(listCall?.bearer, "requête /api/clients authentifiée attendue");
  });

  test("Clients (interrupteur de repli désactivé) : état honnête, aucune donnée ni requête /api/clients", async () => {
    const user = userEvent.setup();
    render(
      <ClientsServiceContext.Provider value={{ enabled: false }}>
        <AuthProvider baseUrl={{ ok: true, url: api.url }}>
          <Gate />
        </AuthProvider>
      </ClientsServiceContext.Provider>,
    );
    await user.type(screen.getByLabelText("Identifiant"), ADMIN.userName);
    await user.type(screen.getByLabelText("Mot de passe"), `${ADMIN.password}{Enter}`);
    await screen.findByRole("heading", { name: `Bonjour, ${ADMIN.displayName}` });
    await user.click(within(nav()).getByRole("button", { name: /^Clients/ }));
    await screen.findByRole("heading", { name: "Clients" });
    screen.getByRole("heading", { name: "Service Clients non activé" });
    assert.equal(screen.queryByRole("table"), null);
    assert.equal((screen.getByRole("searchbox", { name: "Rechercher un client" }) as HTMLInputElement).disabled, true);
    assert.equal(api.requests.some((request) => request.path.startsWith("/api/clients")), false);
  });

  test("admin : Comptes liste les comptes renvoyés par le serveur", async () => {
    const user = await signIn(ADMIN);
    await user.click(within(nav()).getByRole("button", { name: "Comptes" }));
    const table = await screen.findByRole("table");
    within(table).getByText("Admin Test");
    within(table).getByText("Simple Test");
  });

  test("non-admin : Comptes absent du menu", async () => {
    await signIn(SIMPLE);
    assert.equal(within(nav()).queryByRole("button", { name: "Comptes" }), null);
    within(screen.getByRole("complementary", { name: "Menu principal" })).getByText("Utilisateur");
  });

  test("droit retiré pendant la session : le serveur répond 403 -> « Accès non autorisé »", async () => {
    const user = await signIn(ADMIN);
    api.removePermission("admin", "system.admin");
    await user.click(within(nav()).getByRole("button", { name: "Comptes" }));
    await screen.findByRole("heading", { name: "Accès non autorisé" });
  });

  test("réduction/ouverture de la sidebar au clavier", async () => {
    const user = await signIn(ADMIN);
    const collapse = screen.getByRole("button", { name: "Réduire le menu" });
    collapse.focus();
    await user.keyboard("{Enter}");
    const open = screen.getByRole("button", { name: "Ouvrir le menu" });
    assert.equal(open.getAttribute("aria-expanded"), "false");
    // En mode réduit, chaque entrée garde un nom accessible (infobulle au focus).
    within(nav()).getByRole("button", { name: "Accueil" });
    await user.click(open);
    screen.getByRole("button", { name: "Réduire le menu" });
  });

  test("ordre de tabulation : thème, profil, réduction, puis navigation", async () => {
    const user = await signIn(ADMIN);
    (document.activeElement as HTMLElement | null)?.blur();
    await user.tab();
    assert.match(document.activeElement?.getAttribute("aria-label") ?? "", /^Thème/);
    await user.tab();
    assert.match(document.activeElement?.getAttribute("aria-label") ?? "", /^Menu utilisateur/);
    await user.tab();
    assert.equal(document.activeElement?.getAttribute("aria-label"), "Réduire le menu");
    await user.tab();
    assert.equal(document.activeElement?.textContent, "Accueil");
  });

  test("thème sombre : bascule et mémorisation de la préférence (sans donnée sensible)", async () => {
    const user = await signIn(ADMIN);
    await user.click(screen.getByRole("button", { name: /passer en mode sombre/ }));
    assert.equal(document.documentElement.dataset.theme, "dark");
    assert.equal(localStorage.getItem("mmv.theme"), "dark");
    screen.getByRole("button", { name: /passer en mode clair/ });
  });

  test("déconnexion : révocation serveur, retour au login, aucune session active", async () => {
    const user = await signIn(ADMIN);
    assert.equal(api.activeSessions(), 1);
    await user.click(screen.getByRole("button", { name: "Se déconnecter" }));
    await screen.findByRole("heading", { name: "Bienvenue," });
    assert.equal(api.activeSessions(), 0);
    assert.equal(document.documentElement.dataset.theme, undefined);
  });

  test("déconnexion depuis le menu profil (Échap referme le menu)", async () => {
    const user = await signIn(ADMIN);
    await user.click(screen.getByRole("button", { name: /^Menu utilisateur/ }));
    await user.keyboard("{Escape}");
    assert.equal(screen.queryByRole("menu"), null);
    await user.click(screen.getByRole("button", { name: /^Menu utilisateur/ }));
    await user.click(within(screen.getByRole("menu")).getByRole("menuitem", { name: "Se déconnecter" }));
    await screen.findByRole("heading", { name: "Bienvenue," });
  });

  test("session révoquée côté serveur : la navigation ramène au login « session expirée »", async () => {
    const user = await signIn(ADMIN);
    api.revokeAll();
    await user.click(within(nav()).getByRole("button", { name: "Diagnostic technique" }));
    await screen.findByText("Votre session a expiré. Veuillez vous reconnecter.");
  });

  test("redémarrage du client : aucune session restaurée", async () => {
    await signIn(ADMIN);
    cleanup();
    mount();
    screen.getByRole("heading", { name: "Bienvenue," });
  });

  test("serveur inaccessible : bandeau et barre d'état en erreur, diagnostic 01B conservé", async () => {
    const user = await signIn(ADMIN);
    await user.click(within(nav()).getByRole("button", { name: "Diagnostic technique" }));
    await screen.findByText("Serveur MMV et base de données disponibles");
    await api.stop();
    await user.click(screen.getByRole("button", { name: "Revérifier" }));
    await screen.findByText("Serveur MMV inaccessible. Les actions nécessitant le serveur sont indisponibles.");
    await waitFor(() => assert.ok(screen.getAllByText("Serveur MMV injoignable").length >= 1));
  });
});
