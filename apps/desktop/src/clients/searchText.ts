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
