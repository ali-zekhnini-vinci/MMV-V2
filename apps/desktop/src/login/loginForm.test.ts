import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { apiError } from "../api/client.ts";
import { BANNERS, bannerForLoginError, FIELD_MESSAGES, validateLogin } from "./loginForm.ts";

describe("validateLogin (états §2, §3)", () => {
  test("deux champs vides -> deux messages", () => {
    assert.deepEqual(validateLogin("", ""), {
      userName: FIELD_MESSAGES.userNameRequired,
      password: FIELD_MESSAGES.passwordRequired,
    });
  });

  test("identifiant fait d'espaces = vide ; mot de passe non tronqué", () => {
    assert.deepEqual(validateLogin("   ", " "), { userName: FIELD_MESSAGES.userNameRequired });
  });

  test("champs remplis -> aucune erreur", () => {
    assert.deepEqual(validateLogin("admin", "secret"), {});
  });
});

describe("bannerForLoginError (états §6, §7)", () => {
  test("401 -> message neutre, deux champs marqués", () => {
    const banner = bannerForLoginError(apiError("unauthorized", 401));
    assert.equal(banner, BANNERS["invalid-credentials"]);
    assert.equal(banner.markFields, true);
  });

  for (const kind of ["network", "timeout", "http", "invalid-response", "forbidden"] as const) {
    test(`${kind} -> erreur technique, champs non marqués, aucun détail serveur`, () => {
      const banner = bannerForLoginError(apiError(kind, 500));
      assert.equal(banner.kind, "technical");
      assert.equal(banner.markFields, false);
      assert.equal(banner.message, "Connexion impossible pour le moment. Veuillez réessayer.");
    });
  }

  test("429 -> trop de tentatives", () => {
    assert.equal(bannerForLoginError(apiError("rate-limited", 429)).kind, "rate-limited");
  });
});
