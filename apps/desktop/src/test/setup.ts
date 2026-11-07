import { beforeEach, vi } from "vitest";

// Interrupteurs de modules neutralisés avant chaque test (O-05-11) : un fichier `.env*` personnel du poste ne change
// pas le résultat ; un test qui active un module le fait explicitement (vi.stubEnv dans le test, après ce crochet).
beforeEach(() => {
  vi.stubEnv("VITE_MMV_OPTICAL_MODULE", "");
  vi.stubEnv("VITE_MMV_CATALOG_MODULE", "");
});

// Compléments jsdom pour les tests d'écran (vitest setupFiles). jsdom n'implémente pas ResizeObserver,
// utilisé par le positionnement des primitives Radix (menu profil). Sans effet en environnement node.
if (typeof window !== "undefined" && !("ResizeObserver" in window)) {
  class ResizeObserverStub {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  Object.defineProperty(window, "ResizeObserver", { value: ResizeObserverStub, configurable: true });
  Object.defineProperty(globalThis, "ResizeObserver", { value: ResizeObserverStub, configurable: true });
}
