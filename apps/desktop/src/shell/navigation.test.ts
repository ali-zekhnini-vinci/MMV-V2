import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { PERMISSION_SYSTEM_ADMIN } from "../api/auth.ts";
import { initialOf, NAV_SECTIONS, roleLabel, viewAccess, visibleSections } from "./navigation.ts";

const ADMIN = [PERMISSION_SYSTEM_ADMIN];
const USER: string[] = [];

describe("navigation et garde UI", () => {
  test("les modules métier sont tous marqués « phase suivante » (aucun faux écran)", () => {
    const later = NAV_SECTIONS.flatMap((section) => section.items).filter((item) => item.availability === "later");
    assert.deepEqual(
      later.map((item) => item.id),
      ["clients", "products", "prescriptions", "orders", "sales", "inventory", "notifications", "reports", "settings"],
    );
  });

  test("seules les entrées techniques sont disponibles", () => {
    const available = NAV_SECTIONS.flatMap((section) => section.items).filter((item) => item.availability === "available");
    assert.deepEqual(available.map((item) => item.id), ["home", "accounts", "diagnostic"]);
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
    assert.equal(viewAccess("clients", ADMIN), "later");
    assert.equal(viewAccess("accounts", USER), "forbidden");
    assert.equal(viewAccess("accounts", ADMIN), "allowed");
  });

  test("libellés de rôle et initiale", () => {
    assert.equal(roleLabel(ADMIN), "Administrateur système");
    assert.equal(roleLabel(USER), "Utilisateur");
    assert.equal(initialOf("  élodie M."), "É");
    assert.equal(initialOf(""), "?");
  });
});
