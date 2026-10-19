// Règles de saisie PROPOSED du formulaire Client et utilitaires de présentation.
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { ApiRequestError, shouldRetry } from "../api/queryClient.ts";
import { apiError } from "../api/client.ts";
import {
  clientDisplayName,
  clientFormSchema,
  clientInitials,
  EMPTY_CLIENT_FORM,
  formatIsoDate,
  parseFrenchDate,
  serverFieldErrors,
  toClientInput,
} from "./clientForm.ts";
import { toListParams, INITIAL_LIST_STATE } from "./clientsUi.ts";
import { findFolded, findFoldedWords } from "./searchText.ts";

const valid = { ...EMPTY_CLIENT_FORM, firstName: "Test", lastName: "Fictif" };
const issues = (values: typeof EMPTY_CLIENT_FORM) => {
  const result = clientFormSchema.safeParse(values);
  return result.success ? {} : Object.fromEntries(result.error.issues.map((issue) => [String(issue.path[0]), issue.message]));
};

describe("formulaire Client (règles PROPOSED)", () => {
  test("prénom et nom requis, rognés", () => {
    assert.deepEqual(issues({ ...EMPTY_CLIENT_FORM, firstName: "  " }), { firstName: "Le prénom est requis", lastName: "Le nom est requis" });
    assert.deepEqual(issues(valid), {});
    assert.equal(toClientInput({ ...valid, firstName: "  Test  " }).firstName, "Test");
  });

  test("téléphone : 8 à 15 chiffres, séparateurs usuels admis", () => {
    assert.deepEqual(issues({ ...valid, phone: "06 12 34 56 78" }), {});
    assert.deepEqual(issues({ ...valid, phone: "+212 6.12.34.56.78" }), {});
    assert.match(issues({ ...valid, phone: "0612" }).phone, /entre 8 et 15 chiffres/);
    assert.match(issues({ ...valid, phone: "06 12 AB 56 78" }).phone, /chiffres/);
  });

  test("email facultatif mais valide", () => {
    assert.deepEqual(issues({ ...valid, email: "" }), {});
    assert.equal(issues({ ...valid, email: "nom.exemple" }).email, "Format d'email invalide");
  });

  test("date de naissance jj/mm/aaaa, existante, passée, après 1900", () => {
    assert.equal(parseFrenchDate("29/02/2024"), "2024-02-29");
    assert.equal(parseFrenchDate("29/02/2025"), null);
    assert.equal(parseFrenchDate("2024-02-29"), null);
    assert.equal(issues({ ...valid, birthDate: "31/04/1990" }).birthDate, "Date attendue au format jj/mm/aaaa");
    assert.match(issues({ ...valid, birthDate: "01/01/1899" }).birthDate, /postérieure au 01\/01\/1900/);
    assert.match(issues({ ...valid, birthDate: "01/01/2999" }).birthDate, /passée/);
    assert.equal(formatIsoDate("1991-09-14"), "14/09/1991");
  });

  test("conversion : chaînes vides -> null, date ISO", () => {
    assert.deepEqual(toClientInput({ ...valid, birthDate: "14/09/1991", city: "  ", notes: " Note " }), {
      firstName: "Test",
      lastName: "Fictif",
      birthDate: "1991-09-14",
      phone: null,
      email: null,
      addressLine: null,
      city: null,
      postalCode: null,
      notes: "Note",
    });
  });

  test("erreurs serveur : seulement les champs du formulaire, jamais le code brut", () => {
    assert.deepEqual(serverFieldErrors({ lastName: ["required"], email: ["inconnu"], password: ["x"] }), [
      ["lastName", "Champ requis"],
      ["email", "Valeur refusée par le serveur"],
    ]);
    assert.deepEqual(serverFieldErrors(undefined), []);
  });

  test("présentation : NOM Prénom, initiales", () => {
    assert.equal(clientDisplayName({ firstName: "Élise", lastName: "de la Fontaine" }), "DE LA FONTAINE Élise");
    assert.equal(clientInitials({ firstName: "élise", lastName: "fontaine" }), "ÉF");
  });
});

describe("recherche et requêtes", () => {
  test("mise en évidence insensible à la casse et aux accents", () => {
    assert.deepEqual(findFolded("EL AMRANI Yasmine", "amr"), [3, 6]);
    assert.deepEqual(findFolded("Hélène", "helene"), [0, 6]);
    assert.equal(findFolded("Fictif", "zz"), null);
  });

  test("mise en évidence mot par mot, comme la recherche serveur (03-A3)", () => {
    assert.deepEqual(findFoldedWords("DUCASTEL-AB12 Noémie", "noemie ducastel-ab12"), [[0, 13], [14, 20]]);
    assert.deepEqual(findFoldedWords("Hélène Martin", "hel hele"), [[0, 4]]);
    assert.deepEqual(findFoldedWords("Fictif", "zz x"), []);
  });

  test("état de liste -> paramètres de contrat (tri en liste blanche)", () => {
    assert.deepEqual(toListParams(INITIAL_LIST_STATE), { search: "", status: "active", sort: "name", direction: "asc", page: 1, pageSize: 20 });
    assert.equal(toListParams({ ...INITIAL_LIST_STATE, sorting: [{ id: "email", desc: true }] }).sort, "name");
    assert.equal(toListParams({ ...INITIAL_LIST_STATE, sorting: [{ id: "createdAt", desc: true }] }).direction, "desc");
  });

  test("TanStack Query : un seul nouvel essai, et seulement pour les erreurs de transport", () => {
    assert.equal(shouldRetry(0, new ApiRequestError(apiError("network"))), true);
    assert.equal(shouldRetry(1, new ApiRequestError(apiError("timeout"))), false);
    for (const kind of ["unauthorized", "forbidden", "not-found", "conflict", "validation", "http"] as const) {
      assert.equal(shouldRetry(0, new ApiRequestError(apiError(kind))), false, kind);
    }
  });
});
