// Mise en évidence de la recherche dans une cellule (présentation uniquement : le filtrage est serveur).
// Comparaison insensible à la casse et aux accents, pour rester cohérente avec la recherche serveur (MMV.Domain ClientSearch).
import type { ReactNode } from "react";
import { findFolded } from "./searchText.ts";

export function Highlight({ text, query }: { text: string; query: string }): ReactNode {
  const range = query.trim().length >= 2 ? findFolded(text, query) : null;
  if (range === null) return text;
  const [start, end] = range;
  return (
    <>
      {text.slice(0, start)}
      <mark className="clients-mark">{text.slice(start, end)}</mark>
      {text.slice(end)}
    </>
  );
}
