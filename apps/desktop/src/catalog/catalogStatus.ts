// Correspondance des états V2 avec le vocabulaire visuel du Lot 6 (stockState : En stock / Stock faible / Rupture /
// Seuil non défini ; badges b-ok, b-warn, b-err, b-arch ; icônes check, triangle-alert, circle-x, minus). La
// disponibilité est TOUJOURS calculée par le serveur ; le poste ne fait que l'habiller.
import { Check, CircleSlash, CircleX, Clock, Info, type LucideIcon, Minus, TriangleAlert, Wrench } from "lucide-react";
import type { AvailabilityCode } from "../api/catalog.ts";
import type { BadgeTone } from "./CatalogUi.tsx";

export function availabilityBadge(code: AvailabilityCode): { tone: BadgeTone; icon: LucideIcon } {
  switch (code) {
    case "in_stock":
      return { tone: "ok", icon: Check };
    case "low_stock":
      return { tone: "warn", icon: TriangleAlert };
    case "out_of_stock":
      return { tone: "err", icon: CircleX };
    case "on_order":
      return { tone: "info", icon: Clock };
    case "incomplete":
      return { tone: "warn", icon: Info };
    case "available":
      return { tone: "teal", icon: Wrench };
    case "archived":
      return { tone: "arch", icon: CircleSlash };
    default:
      return { tone: "arch", icon: Minus };
  }
}

export function stockTone(code: AvailabilityCode): "ok" | "warn" | "err" | "muted" {
  if (code === "out_of_stock") return "err";
  if (code === "low_stock") return "warn";
  if (code === "in_stock") return "ok";
  return "muted";
}
