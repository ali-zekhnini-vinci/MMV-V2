// Contrastes de l'assistant d'import (post-audit CSV-IMPORT-AI-01, 06/11/2026) : calcul WCAG 2.x sur les jetons MMV
// réellement employés par importWizard.css, thèmes clair et sombre. Calcul statique, pas une mesure native.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, test } from "vitest";

const read = (relative: string) => readFileSync(new URL(relative, import.meta.url), "utf8");
const tokens = read("../../styles/tokens.css");
const wizard = read("./importWizard.css");

function block(selector: string): Record<string, string> {
  const start = tokens.indexOf(`${selector} {`);
  const body = tokens.slice(start, tokens.indexOf("}", start));
  return Object.fromEntries([...body.matchAll(/--([a-z0-9-]+):\s*([^;]+);/g)].map((m) => [m[1], m[2].trim()]));
}

const light = block(":root");
const dark = { ...light, ...block(':root[data-theme="dark"]') };

function rgb(value: string, under: number[]): number[] {
  const hex = /^#([0-9a-f]{6})$/i.exec(value);
  if (hex) return [0, 2, 4].map((k) => parseInt(hex[1].slice(k, k + 2), 16));
  const rgba = /rgba?\(([^)]+)\)/.exec(value);
  if (!rgba) throw new Error(`couleur non lue : ${value}`);
  const [r, g, b, a = 1] = rgba[1].split(",").map(Number);
  return [r, g, b].map((c, k) => Math.round(c * a + under[k] * (1 - a)));
}

const luminance = (c: number[]) =>
  c.map((x) => x / 255).map((x) => (x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4)).reduce((sum, x, k) => sum + x * [0.2126, 0.7152, 0.0722][k], 0);

function ratio(theme: Record<string, string>, fg: string, bg: string): number {
  const card = rgb(theme["surface-card"], [255, 255, 255]);
  const back = rgb(theme[bg], card);
  const [a, b] = [luminance(rgb(theme[fg], back)), luminance(back)].sort((x, y) => y - x);
  return (a + 0.05) / (b + 0.05);
}

describe("assistant d'import : contrastes (texte de petite taille, seuil AA 4,5:1)", () => {
  const pairs: [string, string][] = [
    ["text-primary", "surface-card"], ["text-secondary", "surface-card"], ["text-primary", "surface-sunken"], ["text-secondary", "surface-sunken"],
    ["text-secondary", "surface-hover"], ["warning-text", "warning-soft"], ["error-text", "error-soft"],
  ];
  for (const [name, theme] of [["clair", light], ["sombre", dark]] as const) {
    test(`thème ${name}`, () => {
      for (const [fg, bg] of pairs) assert.ok(ratio(theme, fg, bg) >= 4.5, `${fg} / ${bg} = ${ratio(theme, fg, bg).toFixed(2)}`);
    });
  }

  test("étape courante : accent en clair, accent-hover en sombre (accent seul < 4,5:1 sur la carte sombre)", () => {
    assert.ok(ratio(light, "accent", "surface-card") >= 4.5);
    assert.ok(ratio(dark, "accent", "surface-card") < 4.5);
    assert.ok(ratio(dark, "accent-hover", "surface-card") >= 4.5);
    assert.match(wizard, /:root\[data-theme="dark"\] \.cat-step--current \.iw-step__sub \{\s*color: var\(--accent-hover\);/);
  });

  test("aucun texte de l'assistant en --text-tertiary (3,03:1 en clair)", () => {
    assert.ok(ratio(light, "text-tertiary", "surface-card") < 4.5);
    assert.doesNotMatch(wizard.replace(/\/\*[\s\S]*?\*\//g, ""), /color:\s*var\(--text-tertiary\)/);
  });

  test("GATE D : étape courante sur --accent-soft (mesure native 4,21:1 en clair ; 4,15:1 en sombre avec --accent-hover)", () => {
    assert.ok(ratio(light, "accent", "accent-soft") < 4.5);
    assert.ok(ratio(light, "accent-pressed", "accent-soft") >= 4.5, `clair ${ratio(light, "accent-pressed", "accent-soft").toFixed(2)}`);
    assert.ok(ratio(dark, "accent-hover", "accent-soft") < 4.5);
    assert.ok(ratio(dark, "text-primary", "accent-soft") >= 4.5, `sombre ${ratio(dark, "text-primary", "accent-soft").toFixed(2)}`);
    assert.match(wizard, /\.iw-steps \.cat-step--current \.iw-step__label,\s*\.iw-steps \.cat-step--current \.iw-step__sub \{\s*color: var\(--accent-pressed\);/);
  });

  test("GATE D : textes d'aide et textes atténués PARTAGÉS ramenés à --text-secondary sur l'écran de l'assistant", () => {
    assert.match(wizard, /\.iw-screen \.clients-field__hint,\s*\.iw-screen \.clients-muted \{\s*color: var\(--text-secondary\);/);
    for (const theme of [light, dark]) {
      assert.ok(ratio(theme, "text-secondary", "surface-card") >= 4.5);
      assert.ok(ratio(theme, "text-secondary", "surface-sunken") >= 4.5);
    }
  });
});
