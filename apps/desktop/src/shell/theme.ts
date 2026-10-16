// Thème clair/sombre du shell. La préférence (« light » | « dark », non sensible) est mémorisée dans
// localStorage ; tout échec d'accès au stockage retombe sur la préférence système.
import { useCallback, useEffect, useState } from "react";

export type Theme = "light" | "dark";

export const THEME_STORAGE_KEY = "mmv.theme";

export function readStoredTheme(): Theme | null {
  try {
    const value = window.localStorage.getItem(THEME_STORAGE_KEY);
    return value === "light" || value === "dark" ? value : null;
  } catch {
    return null;
  }
}

function systemTheme(): Theme {
  return typeof window.matchMedia === "function" && window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

export function useTheme(): [Theme, () => void] {
  const [theme, setTheme] = useState<Theme>(() => readStoredTheme() ?? systemTheme());

  useEffect(() => {
    const root = document.documentElement;
    root.dataset.theme = theme;
    // Le login n'a qu'une référence claire : le thème ne s'applique que pendant l'affichage du shell.
    return () => {
      delete root.dataset.theme;
    };
  }, [theme]);

  const toggle = useCallback(() => {
    setTheme((current) => {
      const next: Theme = current === "dark" ? "light" : "dark";
      try {
        window.localStorage.setItem(THEME_STORAGE_KEY, next);
      } catch {
        // Préférence non mémorisée : sans conséquence.
      }
      return next;
    });
  }, []);

  return [theme, toggle];
}
