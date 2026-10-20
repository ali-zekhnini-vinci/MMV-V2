// Contrôles rapides ± (D-04-15) : calcul exact, sans arrondi ni alignement sur le pas de démonstration (D-04-06
// ouverte), vide ≠ 0, aucune modification d'une valeur non numérique.
import assert from "node:assert/strict";
import { describe, test } from "vitest";
import { DEMO_STEP, isSteppable, stepLabel, stepValue } from "./opticalStep.ts";

describe("opticalStep — boutons ± (pas de démonstration)", () => {
  test("pas par défaut de la maquette, libellé à la française", () => {
    assert.equal(DEMO_STEP, 0.25);
    assert.equal(stepLabel(0.25), "0,25");
    assert.equal(stepLabel(0.125), "0,125");
  });

  test("champ vide : nouvelle saisie explicite à partir de 0, jamais « 0 » implicite", () => {
    assert.equal(stepValue("", 1), "+0,25");
    assert.equal(stepValue("", -1), "-0,25");
    assert.equal(stepValue("   ", 1), "+0,25");
  });

  test("valeurs positives, négatives et nulles ; signe explicite sur la valeur produite, 0 sans signe", () => {
    assert.equal(stepValue("-2,25", -1), "-2,50");
    assert.equal(stepValue("-2,25", 1), "-2,00");
    assert.equal(stepValue("+1,75", 1), "+2,00");
    assert.equal(stepValue("-0,25", 1), "0,00");
    assert.equal(stepValue("0", 1), "+0,25");
    assert.equal(stepValue("0,00", -1), "-0,25");
  });

  test("valeur hors pas : aucun arrondi, aucun alignement, précision conservée", () => {
    assert.equal(stepValue("-2,13", 1), "-1,88");
    assert.equal(stepValue("1,1", 1), "+1,35");
    assert.equal(stepValue("+0,125", 1), "+0,375");
    assert.equal(stepValue("-1.10", -1), "-1.35", "séparateur saisi conservé");
    assert.equal(stepValue("−0,50", -1), "−0,75", "signe moins typographique conservé");
    assert.equal(stepValue("+ 1,25", 1), "+1,50", "forme acceptée par le double (espace après le signe)");
  });

  test("aucune borne clinique : grandes valeurs et passage par zéro sans conversion de signe imposée", () => {
    assert.equal(stepValue("-30,00", -1), "-30,25");
    assert.equal(stepValue("+0,10", -1), "-0,15");
  });

  test("pas paramétrable", () => {
    assert.equal(stepValue("", 1, 0.125), "+0,125");
    assert.equal(stepValue("-2,25", 1, 0.5), "-1,75");
    assert.equal(stepValue("+1", 1, 1), "+2");
    assert.equal(stepValue("1", 1, 0), null, "pas nul refusé");
  });

  test("valeur non numérique : jamais modifiée par un clic", () => {
    for (const value of ["abc", "-2,25 D", "1/2", "+-1", "1,", ",5"]) {
      assert.equal(isSteppable(value), false, value);
      assert.equal(stepValue(value, 1), null, value);
    }
    assert.equal(isSteppable(""), true);
    assert.equal(isSteppable("-2,13"), true);
  });
});
