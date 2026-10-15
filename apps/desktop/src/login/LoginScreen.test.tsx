// @vitest-environment jsdom
// Écran de connexion : états et clavier (MMV-Login-states.md) contre un double node:http réel.
import assert from "node:assert/strict";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterAll, afterEach, beforeAll, describe, test } from "vitest";
import { Gate } from "../App.tsx";
import { AuthProvider } from "../auth/AuthProvider.tsx";
import { type FakeApi, startFakeApi } from "../test/fakeMmvApi.ts";

const PASSWORD = "Mot-de-passe-test-1";
let api: FakeApi;

beforeAll(async () => {
  api = await startFakeApi({
    accounts: [{ userName: "admin", password: PASSWORD, displayName: "Admin Test", permissions: ["system.admin"] }],
  });
});
afterAll(() => api.stop());
afterEach(() => {
  cleanup();
  api.options.loginDelayMs = undefined;
  api.options.loginStatus = undefined;
});

function renderLogin(url: URL = api.url) {
  render(
    <AuthProvider baseUrl={{ ok: true, url }}>
      <Gate />
    </AuthProvider>,
  );
  return {
    userName: screen.getByLabelText("Identifiant") as HTMLInputElement,
    password: screen.getByLabelText("Mot de passe") as HTMLInputElement,
    submit: screen.getByRole("button", { name: /Se connecter/ }) as HTMLButtonElement,
  };
}

describe("LoginScreen", () => {
  test("état initial : focus sur Identifiant, aucune erreur, aucune inscription ni réinitialisation", () => {
    const { userName } = renderLogin();
    assert.equal(document.activeElement, userName);
    assert.equal(screen.queryByRole("alert"), null);
    assert.equal(screen.queryByText(/oubli|inscri|créer un compte/i), null);
    assert.equal(screen.queryAllByRole("link").length, 0);
  });

  test("ordre de tabulation : Identifiant -> Mot de passe -> Afficher -> Se connecter, Maj+Tab inverse", async () => {
    const user = userEvent.setup();
    const { userName, password, submit } = renderLogin();
    await user.tab();
    assert.equal(document.activeElement, password);
    await user.tab();
    assert.equal(document.activeElement, screen.getByRole("button", { name: "Afficher le mot de passe" }));
    await user.tab();
    assert.equal(document.activeElement, submit);
    await user.tab({ shift: true });
    await user.tab({ shift: true });
    await user.tab({ shift: true });
    assert.equal(document.activeElement, userName);
  });

  test("Entrée avec champs vides : deux messages, focus sur Identifiant, aucune requête", async () => {
    const user = userEvent.setup();
    const before = api.requests.length;
    const { userName, password } = renderLogin();
    await user.keyboard("{Enter}");
    screen.getByText("Veuillez saisir votre identifiant.");
    screen.getByText("Veuillez saisir votre mot de passe.");
    assert.equal(userName.getAttribute("aria-invalid"), "true");
    assert.equal(password.getAttribute("aria-invalid"), "true");
    assert.equal(document.activeElement, userName);
    assert.equal(api.requests.length, before);
  });

  test("mot de passe seul vide : focus sur Mot de passe", async () => {
    const user = userEvent.setup();
    const { password } = renderLogin();
    await user.keyboard("admin{Enter}");
    screen.getByText("Veuillez saisir votre mot de passe.");
    assert.equal(screen.queryByText("Veuillez saisir votre identifiant."), null);
    assert.equal(document.activeElement, password);
  });

  test("identifiants incorrects : bandeau neutre, identifiant conservé, mot de passe vidé et focalisé", async () => {
    const user = userEvent.setup();
    const { userName, password } = renderLogin();
    await user.keyboard("admin");
    await user.tab();
    await user.keyboard("faux{Enter}");
    const alert = await screen.findByRole("alert");
    assert.equal(alert.textContent, "Identifiant ou mot de passe incorrect.");
    assert.equal(userName.value, "admin");
    assert.equal(password.value, "");
    assert.equal(userName.getAttribute("aria-invalid"), "true");
    await waitFor(() => assert.equal(document.activeElement, password));
  });

  test("connexion en cours : « Connexion… », bouton désactivé, champs en lecture seule", async () => {
    api.options.loginDelayMs = 300;
    const user = userEvent.setup();
    const { userName, password, submit } = renderLogin();
    await user.type(userName, "admin");
    await user.type(password, `${PASSWORD}{Enter}`);
    await screen.findByText("Connexion…");
    assert.equal(submit.disabled, true);
    assert.equal(userName.readOnly, true);
    await screen.findByRole("heading", { name: "Bonjour, Admin Test" });
  });

  test("erreur serveur (500) : bandeau technique, champs non marqués", async () => {
    api.options.loginStatus = 500;
    const user = userEvent.setup();
    const { userName, password } = renderLogin();
    await user.type(userName, "admin");
    await user.type(password, `${PASSWORD}{Enter}`);
    const alert = await screen.findByRole("alert");
    assert.equal(alert.textContent, "Connexion impossible pour le moment. Veuillez réessayer.");
    assert.equal(userName.getAttribute("aria-invalid"), "false");
  });

  test("API injoignable : bandeau technique", async () => {
    const down = await startFakeApi();
    const url = down.url;
    await down.stop();
    const user = userEvent.setup();
    const { userName, password } = renderLogin(url);
    await user.type(userName, "admin");
    await user.type(password, `${PASSWORD}{Enter}`);
    const alert = await screen.findByRole("alert");
    assert.equal(alert.textContent, "Connexion impossible pour le moment. Veuillez réessayer.");
  });

  test("afficher/masquer le mot de passe", async () => {
    const user = userEvent.setup();
    const { password } = renderLogin();
    assert.equal(password.type, "password");
    await user.click(screen.getByRole("button", { name: "Afficher le mot de passe" }));
    assert.equal(password.type, "text");
    await user.click(screen.getByRole("button", { name: "Masquer le mot de passe" }));
    assert.equal(password.type, "password");
  });

  test("succès : shell affiché, aucun jeton dans localStorage/sessionStorage", async () => {
    const user = userEvent.setup();
    const { userName, password } = renderLogin();
    await user.type(userName, "admin");
    await user.type(password, `${PASSWORD}{Enter}`);
    await screen.findByRole("heading", { name: "Bonjour, Admin Test" });
    const stored = [
      ...Object.keys(localStorage).map((key) => localStorage.getItem(key) ?? ""),
      ...Object.keys(sessionStorage).map((key) => sessionStorage.getItem(key) ?? ""),
    ].join("|");
    assert.doesNotMatch(stored, /fake-token/);
    assert.equal(document.cookie, "");
  });

  test("adresse du serveur absente : bandeau « non configuré », bouton désactivé", () => {
    render(
      <AuthProvider baseUrl={{ ok: false, reason: "absente" }}>
        <Gate />
      </AuthProvider>,
    );
    screen.getByText("Serveur MMV non configuré sur ce poste.");
    assert.equal((screen.getByRole("button", { name: /Se connecter/ }) as HTMLButtonElement).disabled, true);
  });
});
