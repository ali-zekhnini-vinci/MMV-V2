import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { PERMISSION_SYSTEM_ADMIN } from "../api/auth.ts";
import { findNavItem, findNavItemByPath, initialOf, NAV_ITEMS, NAV_SECTIONS, roleLabel, routePattern, viewAccess, visibleSections } from "./navigation.ts";

const ADMIN = [PERMISSION_SYSTEM_ADMIN];
const USER: string[] = [];

describe("navigation et garde UI", () => {
  // Phase 03-A (17/10/2026) : Clients devient disponible (écrans, données soumises à G03-METIER).
  // Phase 03-B (17/10/2026) : écran Rendez-vous disponible, service désactivé tant que D-03-13 à D-03-18
  // ne sont pas ratifiées (03-B2 : décisions approuvées, service raccordé). Les autres modules restent « phase suivante ».
  test("modules d'une phase suivante : tous sauf Clients et Rendez-vous", () => {
    const later = NAV_SECTIONS.flatMap((section) => section.items).filter((item) => item.availability === "later");
    assert.deepEqual(
      later.map((item) => item.id),
      ["products", "prescriptions", "orders", "sales", "inventory", "notifications", "reports", "settings"],
    );
  });

  test("entrées disponibles : techniques + Clients + Rendez-vous", () => {
    const available = NAV_SECTIONS.flatMap((section) => section.items).filter((item) => item.availability === "available");
    assert.deepEqual(available.map((item) => item.id), ["home", "clients", "appointments", "accounts", "diagnostic"]);
  });

  test("Rendez-vous : aucune permission requise côté UI (droits appointments.* proposés, le serveur reste seul juge)", () => {
    assert.equal(findNavItem("appointments").requires, undefined);
    assert.equal(viewAccess("appointments", USER), "allowed");
  });

  test("Clients : aucune permission requise côté UI (le serveur reste seul juge, droits clients.* proposés)", () => {
    assert.equal(findNavItem("clients").requires, undefined);
    assert.equal(viewAccess("clients", USER), "allowed");
  });

  test("Comptes est masqué pour un compte sans system.admin", () => {
    const ids = visibleSections(USER).flatMap((section) => section.items.map((item) => item.id));
    assert.equal(ids.includes("accounts"), false);
    assert.equal(ids.includes("diagnostic"), true);
  });

  test("Comptes est visible pour system.admin", () => {
    const ids = visibleSections(ADMIN).flatMap((section) => section.items.map((item) => item.id));
    assert.equal(ids.includes("accounts"), true);
  });

  test("viewAccess : allowed / later / forbidden", () => {
    assert.equal(viewAccess("home", USER), "allowed");
    assert.equal(viewAccess("clients", ADMIN), "allowed");
    assert.equal(viewAccess("products", ADMIN), "later");
    assert.equal(viewAccess("accounts", USER), "forbidden");
    assert.equal(viewAccess("accounts", ADMIN), "allowed");
  });

  test("routes React Router : une route unique par entrée, Accueil à la racine, route inconnue sans entrée", () => {
    const paths = NAV_ITEMS.map((item) => item.path);
    assert.equal(new Set(paths).size, paths.length);
    assert.equal(findNavItemByPath("/")?.id, "home");
    assert.equal(findNavItemByPath("/comptes")?.id, "accounts");
    assert.equal(findNavItemByPath("/diagnostic")?.id, "diagnostic");
    assert.equal(findNavItemByPath("/inexistant"), undefined);
  });

  test("sous-routes du module Clients : entrée active, motif imbriqué, pas de faux préfixe", () => {
    assert.equal(findNavItemByPath("/clients")?.id, "clients");
    assert.equal(findNavItemByPath("/clients/nouveau")?.id, "clients");
    assert.equal(findNavItemByPath("/clients/0190-abc/modifier")?.id, "clients");
    assert.equal(findNavItemByPath("/clientsX"), undefined);
    assert.equal(findNavItemByPath("/comptes/x"), undefined);
    assert.equal(routePattern(findNavItem("clients")), "/clients/*");
    assert.equal(routePattern(findNavItem("accounts")), "/comptes");
  });

  test("libellés de rôle et initiale", () => {
    assert.equal(roleLabel(ADMIN), "Administrateur système");
    assert.equal(roleLabel(USER), "Utilisateur");
    assert.equal(initialOf("  élodie M."), "É");
    assert.equal(initialOf(""), "?");
  });
});
