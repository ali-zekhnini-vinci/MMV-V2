// Mise en évidence de la recherche dans une cellule (présentation uniquement : le filtrage est serveur).
// Comparaison insensible à la casse et aux accents, mot par mot, pour rester cohérente avec la recherche serveur
// (MMV.Domain ClientSearch : chaque mot de la requête doit apparaître).
import { Fragment, type ReactNode } from "react";
import { findFoldedWords } from "./searchText.ts";

export function Highlight({ text, query }: { text: string; query: string }): ReactNode {
  const ranges = query.trim().length >= 2 ? findFoldedWords(text, query) : [];
  if (ranges.length === 0) return text;
  const parts: ReactNode[] = [];
  let cursor = 0;
  for (const [start, end] of ranges) {
    parts.push(text.slice(cursor, start), <mark className="clients-mark">{text.slice(start, end)}</mark>);
    cursor = end;
  }
  parts.push(text.slice(cursor));
  return parts.map((part, index) => <Fragment key={index}>{part}</Fragment>);
}
