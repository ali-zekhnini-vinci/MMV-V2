// Lecture d'un fichier CSV de fournisseur sur le poste (COR-05-04) : décodage de l'encodage (UTF-8 avec ou sans BOM,
// UTF-16 avec BOM, sinon Windows-1252 « ANSI » des tableurs français), découpage RFC 4180 identique au serveur, et
// réécriture après correction (nouvelle version du fichier = nouveau lot, empreinte propre ; l'original n'est jamais
// modifié). Toutes les valeurs restent des CHAÎNES : « 000123 » n'est jamais converti en nombre.

export type CsvEncoding = "utf-8" | "utf-16le" | "utf-16be" | "windows-1252";

export const ENCODING_LABELS: Record<CsvEncoding, string> = {
  "utf-8": "UTF-8",
  "utf-16le": "UTF-16 (LE)",
  "utf-16be": "UTF-16 (BE)",
  "windows-1252": "Windows-1252 (ANSI)",
};

/** Décodage : BOM d'abord ; UTF-8 strict ; à défaut Windows-1252 (jamais de caractère de remplacement silencieux en UTF-8). */
export function decodeCsv(bytes: Uint8Array): { text: string; encoding: CsvEncoding } {
  if (bytes[0] === 0xff && bytes[1] === 0xfe) return { text: new TextDecoder("utf-16le").decode(bytes.subarray(2)), encoding: "utf-16le" };
  if (bytes[0] === 0xfe && bytes[1] === 0xff) return { text: new TextDecoder("utf-16be").decode(bytes.subarray(2)), encoding: "utf-16be" };
  const body = bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf ? bytes.subarray(3) : bytes;
  try {
    return { text: new TextDecoder("utf-8", { fatal: true }).decode(body), encoding: "utf-8" };
  } catch {
    return { text: new TextDecoder("windows-1252").decode(body), encoding: "windows-1252" };
  }
}

// UX-IA : « | » ajouté, même ordre de préférence que le serveur (CsvTable.DetectDelimiter).
const DELIMITERS = [";", ",", "\t", "|"] as const;

/** Même règle que le serveur (`CsvTable.DetectDelimiter`) : nombre de colonnes constant et le plus grand. */
export function detectDelimiter(text: string): string {
  const lines = text.split("\n").map((l) => l.replace(/\r$/, "")).filter((l) => l.trim() !== "").slice(0, 20);
  const count = (line: string, delimiter: string) => {
    let quoted = false;
    let n = 0;
    for (const c of line) {
      if (c === '"') quoted = !quoted;
      else if (c === delimiter && !quoted) n += 1;
    }
    return n;
  };
  let best: string = DELIMITERS[0];
  let bestScore = -1;
  for (const delimiter of DELIMITERS) {
    const counts = lines.map((l) => count(l, delimiter));
    if (counts.length === 0 || counts[0] === 0) continue;
    const score = counts.filter((c) => c === counts[0]).length * 1000 + counts[0];
    if (score > bestScore) [best, bestScore] = [delimiter, score];
  }
  return best;
}

/** Découpage RFC 4180 (guillemets doublés, retours à la ligne dans les guillemets) ; lignes vides ignorées. */
export function parseCsv(text: string, delimiter: string): string[][] {
  const records: string[][] = [];
  let cells: string[] = [];
  let cell = "";
  let quoted = false;
  for (let i = 0; i < text.length; i += 1) {
    const c = text[i];
    if (quoted) {
      if (c === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i += 1;
        } else quoted = false;
      } else cell += c;
      continue;
    }
    if (c === '"' && cell === "") quoted = true;
    else if (c === delimiter) {
      cells.push(cell);
      cell = "";
    } else if (c === "\r") continue;
    else if (c === "\n") {
      cells.push(cell);
      records.push(cells);
      cells = [];
      cell = "";
    } else cell += c;
  }
  if (cell !== "" || cells.length > 0) {
    cells.push(cell);
    records.push(cells);
  }
  return records.filter((r) => r.some((v) => v.trim() !== ""));
}

/** Réécriture : guillemets seulement si nécessaires (séparateur, guillemet, retour à la ligne, espaces de bord). */
export function serializeCsv(records: string[][], delimiter: string): string {
  const quote = (value: string) =>
    value.includes(delimiter) || value.includes('"') || value.includes("\n") || value.includes("\r") || value !== value.trim()
      ? `"${value.replace(/"/g, '""')}"`
      : value;
  return records.map((r) => r.map(quote).join(delimiter)).join("\n") + "\n";
}

/**
 * Applique des corrections { numéro de ligne du fichier (en-tête = 1) → { colonne → valeur } } et renvoie le
 * nouveau contenu ; les lignes vides ignorées ne décalent pas la numérotation (même règle que le serveur).
 */
export function applyCorrections(text: string, corrections: Record<number, Record<string, string>>): string {
  const delimiter = detectDelimiter(text);
  const lines: { lineNo: number; cells: string[] }[] = [];
  let lineNo = 1;
  let recordStart = 1;
  let cells: string[] = [];
  let cell = "";
  let quoted = false;
  const push = () => {
    cells.push(cell);
    if (cells.some((v) => v.trim() !== "")) lines.push({ lineNo: recordStart, cells });
    cells = [];
    cell = "";
  };
  for (let i = 0; i < text.length; i += 1) {
    const c = text[i];
    if (quoted) {
      if (c === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i += 1;
        } else quoted = false;
      } else {
        if (c === "\n") lineNo += 1;
        cell += c;
      }
      continue;
    }
    if (c === '"' && cell === "") quoted = true;
    else if (c === delimiter) {
      cells.push(cell);
      cell = "";
    } else if (c === "\r") continue;
    else if (c === "\n") {
      push();
      lineNo += 1;
      recordStart = lineNo;
    } else cell += c;
  }
  if (cell !== "" || cells.length > 0) push();
  const headers = lines[0]?.cells.map((h) => h.trim()) ?? [];
  const records = lines.map(({ lineNo: n, cells: row }, index) => {
    const fix = index === 0 ? undefined : corrections[n];
    if (!fix) return row;
    const next = [...row];
    for (const [header, value] of Object.entries(fix)) {
      const column = headers.indexOf(header);
      if (column < 0) continue;
      while (next.length <= column) next.push("");
      next[column] = value;
    }
    return next;
  });
  return serializeCsv(records, delimiter);
}
