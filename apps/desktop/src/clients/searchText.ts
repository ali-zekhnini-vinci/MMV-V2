// Recherche de texte insensible à la casse et aux accents (présentation de la recherche Clients).
const fold = (char: string) => char.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase("fr");

/** Position [début, fin[ de `needle` dans `text`, en indices de `text`, ou null. */
export function findFolded(text: string, needle: string): [number, number] | null {
  const target = [...needle.trim()].map(fold).join("");
  if (target === "") return null;
  const chars = [...text];
  const folded = chars.map(fold);
  for (let start = 0; start < chars.length; start += 1) {
    let acc = "";
    for (let end = start; end < chars.length && acc.length < target.length; end += 1) {
      acc += folded[end];
      if (acc === target) {
        const offset = chars.slice(0, start).join("").length;
        return [offset, offset + chars.slice(start, end + 1).join("").length];
      }
      if (!target.startsWith(acc)) break;
    }
  }
  return null;
}

/**
 * Plages [début, fin[ de chaque mot de la requête (≥ 2 caractères) présent dans `text`, triées et fusionnées.
 * Même découpage que le serveur (chaque mot doit apparaître) : « noemie ducastel » surligne les deux mots.
 */
export function findFoldedWords(text: string, query: string): [number, number][] {
  const ranges = query
    .trim()
    .split(/\s+/)
    .filter((word) => [...word].length >= 2)
    .map((word) => findFolded(text, word))
    .filter((range): range is [number, number] => range !== null)
    .sort((a, b) => a[0] - b[0]);
  const merged: [number, number][] = [];
  for (const [start, end] of ranges) {
    const last = merged[merged.length - 1];
    if (last && start <= last[1]) last[1] = Math.max(last[1], end);
    else merged.push([start, end]);
  }
  return merged;
}
