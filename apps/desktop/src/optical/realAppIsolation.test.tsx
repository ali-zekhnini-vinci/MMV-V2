// @vitest-environment jsdom
// O-04-16 (APPROVED) — l'application RÉELLE ne monte aucun écran optique pendant 04-A : même une session qui
// porterait les noms de permissions proposés (impossible aujourd'hui : ils n'existent pas dans MMV.Api) garde l'onglet
// reporté « Ordonnances — phase ultérieure » et l'entrée de menu `later`. Les prototypes ne vivent que dans le banc
// isolé (optical-bench.html), absent de `vite build`. Double node:http des Phases 02/03, clients FICTIFS.
import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test } from "vitest";
import { Gate } from "../App.tsx";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { ClientsServiceContext } from "../clients/clientsService.ts";
import { findNavItem, findNavItemByPath, viewAccess } from "../shell/navigation.ts";
import { CLIENT_PERMISSIONS } from "../test/fakeClientsApi.ts";
import { type FakeApi, startFakeApi } from "../test/fakeMmvApi.ts";
import { OPTICAL_PERMISSIONS } from "./opticalAccess.ts";

const SRC = join(import.meta.dirname, "..");
const DESKTOP = join(SRC, "..");
const PASSWORD = "Mot-de-passe-test-1";
let api: FakeApi | null = null;

afterEach(async () => {
  cleanup();
  await api?.stop().catch(() => undefined);
  api = null;
});

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? sourceFiles(path) : /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

describe("isolement du prototype optique (O-04-16)", () => {
  test("aucun module de l'application n'importe le prototype ni le banc", () => {
    const offenders = sourceFiles(SRC)
      .map((path) => relative(SRC, path).replaceAll("\\", "/"))
      .filter((path) => !path.startsWith("optical/") && !path.startsWith("bench/") && !/\.test\.tsx?$/.test(path))
      .filter((path) => /from\s+["'][^"']*\/(optical|bench)\//.test(readFileSync(join(SRC, path), "utf8")));
    assert.deepEqual(offenders, []);
  });

  test("le banc est une page distincte, hors de `vite build` (index.html seul construit)", () => {
    assert.match(readFileSync(join(DESKTOP, "index.html"), "utf8"), /src="\/src\/main\.tsx"/);
    assert.doesNotMatch(readFileSync(join(DESKTOP, "index.html"), "utf8"), /bench|optical/i);
    const config = readFileSync(join(DESKTOP, "vite.config.ts"), "utf8");
    assert.doesNotMatch(config, /optical-bench|rollupOptions|input\s*:/);
    assert.match(readFileSync(join(DESKTOP, "optical-bench.html"), "utf8"), /BANC DE DÉMONSTRATION 04-A/);
  });

  test("navigation réelle inchangée : « Ordonnances » reste `later`, aucune route /optique", () => {
    const permissions = ["clients.read", ...Object.values(OPTICAL_PERMISSIONS)];
    assert.equal(findNavItem("prescriptions").label, "Ordonnances");
    assert.equal(viewAccess("prescriptions", permissions), "later");
    assert.equal(findNavItemByPath("/optique"), undefined);
    assert.equal(findNavItemByPath("/optique/ordonnance/rx-1"), undefined);
  });

  test("application réelle : fiche client avec l'onglet reporté, menu « Ordonnances » indisponible, même avec les noms proposés", async () => {
    window.innerWidth = 1920;
    api = await startFakeApi({
      accounts: [{ userName: "agent", password: PASSWORD, displayName: "Agent Test", permissions: [...Object.values(CLIENT_PERMISSIONS), ...Object.values(OPTICAL_PERMISSIONS)] }],
      clients: [{ firstName: "Camille", lastName: "Fictif-Alpha", city: "Ville-Démo" }],
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
    const nav = screen.getByRole("navigation", { name: "Navigation" });
    await user.click(within(nav).getByRole("button", { name: /^Clients/ }));
    await user.click(await screen.findByRole("button", { name: "Consulter FICTIF-ALPHA Camille" }));
    await screen.findByRole("heading", { level: 1, name: "FICTIF-ALPHA Camille" });
    assert.equal(screen.queryByRole("tab", { name: "Dossier optique" }), null);
    await user.click(screen.getByRole("tab", { name: "Ordonnances" }));
    await screen.findByText("Ordonnances — phase ultérieure");
    await user.click(within(nav).getByRole("button", { name: /^Ordonnances/ }));
    await screen.findByRole("heading", { level: 1, name: "Ordonnances" });
    screen.getByText(/Aucun écran ni aucune donnée de ce module n'existe encore/);
    assert.equal(api.requests.some((request) => request.path.includes("optical")), false, "aucune requête optique");
  });
});
