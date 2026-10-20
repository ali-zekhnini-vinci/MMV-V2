// PROTOTYPE 04-A — contrôles rapides ± de la grille OD / OG (D-04-15 APPROVED : référence UX Lot 3). Le PAS est un
// paramètre de DÉMONSTRATION (0,25 par défaut, comme la maquette) : D-04-06 reste ouverte, aucun pas n'est une règle
// métier. Calcul exact en entiers décimaux : aucun arrondi, aucun alignement sur le pas (−2,13 + 0,25 = −1,88), aucune
// borne, aucune conversion de signe. Un clic depuis un champ vide est une nouvelle saisie explicite qui part de 0
// (+ → +0,25 ; − → −0,25) ; le vide n'est jamais affiché ni enregistré comme 0. Une valeur non numérique n'est
// jamais modifiée par un clic (null). Seule la valeur produite par un clic est mise en forme ; une saisie au clavier
// est conservée telle quelle.
import { createContext } from "react";

/** Pas de démonstration des boutons ± (dioptries). Paramétrable ; aucune valeur n'est approuvée (D-04-06). */
export const DEMO_STEP = 0.25;

/** Même forme que celle acceptée par le double du banc (inMemoryOpticalGateway, NUMBER_SHAPE). */
const NUMBER = /^([+\-−]?)\s?(\d+)(?:([.,])(\d+))?$/u;

interface ParsedNumber {
  /** Valeur en unités de 10^-decimals. */
  scaled: bigint;
  decimals: number;
  separator: "," | ".";
  minus: "-" | "−";
}

function parse(value: string): ParsedNumber | null {
  const match = NUMBER.exec(value.trim());
  if (!match) return null;
  const [, sign = "", whole = "0", separator, fraction = ""] = match;
  const scaled = BigInt(whole + fraction);
  return {
    scaled: sign === "-" || sign === "−" ? -scaled : scaled,
    decimals: fraction.length,
    separator: separator === "." ? "." : ",",
    minus: sign === "−" ? "−" : "-",
  };
}

function decimalsOf(step: number): number {
  const text = String(step);
  return text.includes(".") ? text.split(".")[1].length : 0;
}

function scale(value: bigint, from: number, to: number): bigint {
  return value * 10n ** BigInt(to - from);
}

/** Valeur vide (non renseignée) ou numérique au sens du banc ; une autre saisie désactive les boutons ±. */
export function isSteppable(value: string): boolean {
  return value.trim() === "" || parse(value) !== null;
}

/** Applique `direction × step` à la valeur saisie ; `null` si la valeur n'est pas numérique (inchangée). */
export function stepValue(value: string, direction: 1 | -1, step: number = DEMO_STEP): string | null {
  if (!(step > 0) || !Number.isFinite(step)) return null;
  const stepDecimals = decimalsOf(step);
  const current: ParsedNumber | null = value.trim() === "" ? { scaled: 0n, decimals: 0, separator: ",", minus: "-" } : parse(value);
  if (current === null) return null;
  const decimals = Math.max(current.decimals, stepDecimals);
  const stepScaled = BigInt(Math.round(step * 10 ** stepDecimals));
  const result = scale(current.scaled, current.decimals, decimals) + BigInt(direction) * scale(stepScaled, stepDecimals, decimals);
  return format(result, decimals, current.separator, current.minus);
}

function format(scaled: bigint, decimals: number, separator: string, minus: string): string {
  const negative = scaled < 0n;
  const digits = (negative ? -scaled : scaled).toString().padStart(decimals + 1, "0");
  const whole = digits.slice(0, digits.length - decimals);
  const fraction = digits.slice(digits.length - decimals);
  const body = decimals > 0 ? `${whole}${separator}${fraction}` : whole;
  // Signe explicite sur les valeurs produites par un clic, comme la maquette (« +0,25 ») ; 0 sans signe.
  if (scaled === 0n) return body;
  return `${negative ? minus : "+"}${body}`;
}

/** Libellé d'un pas pour les boutons et les noms accessibles : 0.25 → « 0,25 ». */
export function stepLabel(step: number): string {
  return String(step).replace(".", ",");
}

/** Pas de démonstration en vigueur dans le prototype (le banc peut le changer : `?step=0.125`). */
export const OpticalStepContext = createContext<number>(DEMO_STEP);

/** Champs proposés aux boutons ± (dioptries) ; Axe, Base et Acuité restent en saisie libre (aucun pas imposé). */
export const STEPPER_FIELDS: ReadonlySet<string> = new Set(["sphere", "cylinder", "addition", "prism"]);
