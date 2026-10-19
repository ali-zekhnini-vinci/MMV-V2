// Conformité de la pile de style (phase 02) : polices embarquées localement, aucune ressource distante,
// thème Tailwind = jetons MMV (aucun thème shadcn/ui par défaut).
import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, test } from "vitest";

const SRC = new URL("..", import.meta.url);
const read = (relative: string) => readFileSync(new URL(relative, SRC), "utf8");

function filesUnder(directory: string, extensions: readonly string[]): string[] {
  return readdirSync(new URL(directory, SRC), { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && extensions.some((extension) => entry.name.endsWith(extension)))
    .map((entry) => join(entry.parentPath, entry.name));
}

describe("pile de style", () => {
  test("aucune ressource distante (Google Fonts, CDN) dans les CSS, le code client et index.html", () => {
    const sources = [...filesUnder(".", [".css", ".ts", ".tsx"]), new URL("../index.html", SRC)];
    for (const file of sources) {
      const content = readFileSync(file, "utf8");
      assert.doesNotMatch(content, /fonts\.(googleapis|gstatic)\.com|@import\s+url\(\s*["']?https?:/i, String(file));
    }
  });

  test("Inter et Source Sans 3 chargées localement, prioritaires dans les jetons MMV", () => {
    const fonts = read("styles/fonts.css");
    assert.match(fonts, /@import "@fontsource-variable\/inter\/wght\.css";/);
    assert.match(fonts, /@import "@fontsource-variable\/source-sans-3\/wght\.css";/);
    const tokens = read("styles/tokens.css");
    assert.match(tokens, /--font-heading: "Inter Variable"/);
    assert.match(tokens, /--font-body: "Source Sans 3 Variable"/);
  });

  test("Tailwind CSS 4 importé ; thème réduit aux jetons MMV, sans variables du thème shadcn par défaut", () => {
    const entry = read("styles/index.css");
    assert.match(entry, /@import "tailwindcss";/);
    assert.match(entry, /@theme inline reference \{/);
    for (const namespace of ["--color-*", "--radius-*", "--shadow-*", "--text-*", "--font-*"]) {
      assert.ok(entry.includes(`${namespace}: initial;`), `${namespace} non réinitialisé`);
    }
    assert.doesNotMatch(entry, /--(background|foreground|primary|ring|sidebar)\s*:|oklch\(/);
  });
});

describe("module Clients (phase 03-A)", () => {
  test("CSS Clients et boîtes de dialogue : couleurs exclusivement issues des jetons (clair et sombre)", () => {
    for (const file of ["clients/Clients.css", "components/ui/overlays.css"]) {
      const css = read(file).replace(/\/\*[\s\S]*?\*\//g, "");
      assert.doesNotMatch(css, /#[0-9a-f]{3,8}\b|rgba?\(|hsla?\(|oklch\(/i, `${file} : couleur littérale`);
      assert.match(css, /var\(--/, file);
    }
  });

  test("le thème sombre redéfinit chaque jeton de couleur utilisé par le module Clients", () => {
    const tokens = read("styles/tokens.css");
    const darkStart = tokens.indexOf('[data-theme="dark"]');
    assert.ok(darkStart > 0);
    const dark = tokens.slice(darkStart);
    // Jetons de COULEUR seulement (les jetons typographiques --text-body, --text-sm… sont communs aux deux thèmes).
    const colorToken = /var\((--(?:accent[a-z-]*|surface-[a-z]+|text-(?:primary|secondary|tertiary|disabled|on-accent)|border[a-z-]*|success[a-z-]*|warning[a-z-]*|error[a-z-]*|info[a-z-]*))\)/g;
    const used = new Set(
      [...read("clients/Clients.css").matchAll(colorToken), ...read("components/ui/overlays.css").matchAll(colorToken)].map((match) => match[1]),
    );
    assert.ok(used.size > 10);
    for (const token of used) assert.match(dark, new RegExp(`${token}:`), `${token} absent du thème sombre`);
  });
});
