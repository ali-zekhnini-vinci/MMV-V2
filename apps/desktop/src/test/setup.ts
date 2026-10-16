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
