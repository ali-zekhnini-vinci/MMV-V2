// Onglets — composant shadcn/ui sur la primitive Radix Tabs (rôles tablist/tab/tabpanel, flèches gauche/droite,
// Début/Fin). Gabarit visuel : onglets de la fiche client du Lot 2 DRAFT (`.tabs`, soulignement accent 2 px).
import * as TabsPrimitive from "@radix-ui/react-tabs";
import type { ComponentProps } from "react";
import { cn } from "../../lib/utils.ts";
import "./overlays.css";

export const Tabs = TabsPrimitive.Root;

export function TabsList({ className, ...props }: ComponentProps<typeof TabsPrimitive.List>) {
  return <TabsPrimitive.List data-slot="tabs-list" className={cn("mmv-tabs", className)} {...props} />;
}

export function TabsTrigger({ className, ...props }: ComponentProps<typeof TabsPrimitive.Trigger>) {
  return <TabsPrimitive.Trigger data-slot="tabs-trigger" className={cn("mmv-tabs__tab", className)} {...props} />;
}

export function TabsContent({ className, ...props }: ComponentProps<typeof TabsPrimitive.Content>) {
  return <TabsPrimitive.Content data-slot="tabs-content" className={cn("mmv-tabs__panel", className)} {...props} />;
}
