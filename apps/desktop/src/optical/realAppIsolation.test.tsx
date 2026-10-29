// @vitest-environment jsdom
// O-04-16 (APPROVED) puis activation contrôlée 04-B (mandat GO du 27/10/2026). Sans l'interrupteur de build
// `VITE_MMV_OPTICAL_MODULE=enabled` (absent par défaut), l'application RÉELLE garde l'onglet reporté « Ordonnances —
// phase ultérieure » et l'entrée de menu `later`, même pour une session portant les droits optiques ; aucune requête
// optique. Avec l'interrupteur ET `clients.read` + `optical.read`, l'onglet « Dossier optique » et le menu « Dossiers
// optiques » interrogent les routes réelles /api/optical (refus et erreurs affichés tels quels). Le banc reste une page
// isolée, hors de `vite build`, jamais importée par l'application. Double node:http des Phases 02/03, clients FICTIFS.
import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test, vi } from "vitest";
import { Gate } from "../App.tsx";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { ClientsServiceContext } from "../clients/clientsService.ts";
import { findNavItem, findNavItemByPath, viewAccess } from "../shell/navigation.ts";
import { CLIENT_PERMISSIONS } from "../test/fakeClientsApi.ts";
import { type FakeApi, startFakeApi } from "../test/fakeMmvApi.ts";
import { OPTICAL_PERMISSIONS } from "./opticalAccess.ts";
import { isOpticalModuleActive } from "./opticalActivation.ts";

const SRC = join(import.meta.dirname, "..");
const DESKTOP = join(SRC, "..");
const PASSWORD = "Mot-de-passe-test-1";
let api: FakeApi | null = null;

afterEach(async () => {
  cleanup();
  vi.unstubAllEnvs();
  await api?.stop().catch(() => undefined);
  api = null;
});

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? sourceFiles(path) : /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

async function openClientCard(permissions: readonly string[]) {
  window.innerWidth = 1920;
  api = await startFakeApi({
    accounts: [{ userName: "agent", password: PASSWORD, displayName: "Agent Test", permissions: [...permissions] }],
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
  return { user, nav, server: api };
}

const ALL_OPTICAL = [...Object.values(CLIENT_PERMISSIONS), ...Object.values(OPTICAL_PERMISSIONS)];

describe("activation contrôlée du dossier optique (O-04-16, 04-B)", () => {
  test("aucun module de l'application n'importe le banc de démonstration", () => {
    const offenders = sourceFiles(SRC)
      .map((path) => relative(SRC, path).replaceAll("\\", "/"))
      .filter((path) => !path.startsWith("bench/") && !/\.test\.tsx?$/.test(path))
      .filter((path) => /from\s+["'][^"']*\/bench\//.test(readFileSync(join(SRC, path), "utf8")));
    assert.deepEqual(offenders, []);
  });

  test("le banc est une page distincte, hors de `vite build` ; aucune activation par défaut", () => {
    assert.match(readFileSync(join(DESKTOP, "index.html"), "utf8"), /src="\/src\/main\.tsx"/);
    assert.doesNotMatch(readFileSync(join(DESKTOP, "index.html"), "utf8"), /bench|optical/i);
    const config = readFileSync(join(DESKTOP, "vite.config.ts"), "utf8");
    assert.doesNotMatch(config, /optical-bench|rollupOptions|input\s*:/);
    assert.match(readFileSync(join(DESKTOP, "optical-bench.html"), "utf8"), /BANC DE DÉMONSTRATION 04-A/);
    assert.doesNotMatch(readFileSync(join(DESKTOP, ".env.development"), "utf8"), /VITE_MMV_OPTICAL_MODULE/);
  });

  test("interrupteur ET droits : sans l'un des deux, « Ordonnances » reste `later` et aucune route /optique", () => {
    assert.equal(isOpticalModuleActive(ALL_OPTICAL, undefined), false, "interrupteur absent");
    assert.equal(isOpticalModuleActive(ALL_OPTICAL, "true"), false, "seule la valeur « enabled » active");
    assert.equal(isOpticalModuleActive(["clients.read", "optical.prescriptions.write"], "enabled"), false, "sans optical.read");
    assert.equal(isOpticalModuleActive(["optical.read"], "enabled"), false, "sans clients.read");
    assert.equal(isOpticalModuleActive(["clients.read", "optical.read"], "enabled"), true);

    assert.equal(findNavItem("prescriptions").label, "Ordonnances");
    assert.equal(viewAccess("prescriptions", ALL_OPTICAL), "later");
    assert.equal(findNavItemByPath("/optique"), undefined);
    assert.equal(findNavItem("prescriptions", true).label, "Dossiers optiques");
    assert.equal(viewAccess("prescriptions", ALL_OPTICAL, true), "allowed");
    assert.equal(findNavItemByPath("/optique/ordonnance/rx-1", true)?.id, "prescriptions");
  });

  test("application réelle SANS interrupteur : onglet reporté et menu indisponible, même avec les droits optiques ; aucune requête optique", async () => {
    const { user, nav, server } = await openClientCard(ALL_OPTICAL);
    assert.equal(screen.queryByRole("tab", { name: "Dossier optique" }), null);
    await user.click(screen.getByRole("tab", { name: "Ordonnances" }));
    await screen.findByText("Ordonnances — phase ultérieure");
    await user.click(within(nav).getByRole("button", { name: /^Ordonnances/ }));
    await screen.findByRole("heading", { level: 1, name: "Ordonnances" });
    screen.getByText(/Aucun écran ni aucune donnée de ce module n'existe encore/);
    assert.equal(server.requests.some((request) => request.path.includes("optical")), false, "aucune requête optique");
  });

  test("application réelle AVEC interrupteur mais sans optical.read : onglet reporté conservé, aucune requête optique", async () => {
    vi.stubEnv("VITE_MMV_OPTICAL_MODULE", "enabled");
    const { server } = await openClientCard(Object.values(CLIENT_PERMISSIONS));
    assert.equal(screen.queryByRole("tab", { name: "Dossier optique" }), null);
    screen.getByRole("tab", { name: "Ordonnances" });
    assert.equal(server.requests.some((request) => request.path.includes("optical")), false);
  });

  test("application réelle AVEC interrupteur et droits : onglet « Dossier optique » et menu interrogent /api/optical (session porteuse)", async () => {
    vi.stubEnv("VITE_MMV_OPTICAL_MODULE", "enabled");
    const { user, nav, server } = await openClientCard(ALL_OPTICAL);
    await user.click(screen.getByRole("tab", { name: "Dossier optique" }));
    await screen.findByRole("heading", { name: "Impossible de charger le dossier optique" }, { timeout: 4000 });
    const tabRequest = server.requests.find((request) => request.path.startsWith("/api/optical/clients/"));
    assert.ok(tabRequest, "dossier du client demandé au serveur");
    assert.equal(tabRequest.bearer, true, "requête authentifiée");

    await user.click(within(nav).getByRole("button", { name: /^Dossiers optiques/ }));
    await screen.findByRole("heading", { level: 1, name: "Dossiers optiques" }, { timeout: 5000 });
    await screen.findByRole("heading", { name: "Impossible de charger les dossiers optiques" }, { timeout: 5000 });
    assert.ok(server.requests.some((request) => request.path.startsWith("/api/optical/records")), "liste globale demandée au serveur");
  });
});
