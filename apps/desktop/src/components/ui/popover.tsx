// Fenêtre contextuelle — composant shadcn/ui sur la primitive Radix Popover (positionnement anti-débordement,
// Échap, clic ou focus extérieur, retour du focus au déclencheur). Phase 03-D1 : support du calendrier et de la
// liste des heures du formulaire Rendez-vous. Jetons MMV uniquement (clair et sombre).
import * as PopoverPrimitive from "@radix-ui/react-popover";
import type { ComponentProps } from "react";
import { cn } from "../../lib/utils.ts";
import "./overlays.css";

export const Popover = PopoverPrimitive.Root;
export const PopoverTrigger = PopoverPrimitive.Trigger;
export const PopoverAnchor = PopoverPrimitive.Anchor;

export function PopoverContent({ className, align = "start", sideOffset = 6, onKeyDown, ...props }: ComponentProps<typeof PopoverPrimitive.Content>) {
  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        data-slot="popover-content"
        align={align}
        sideOffset={sideOffset}
        collisionPadding={12}
        className={cn("mmv-popover", className)}
        onKeyDown={(event) => {
          onKeyDown?.(event);
          // Échap ferme la fenêtre contextuelle seulement : les événements React traversent le portail, sans cet arrêt
          // le panneau parent (formulaire Rendez-vous) traiterait aussi Échap comme « fermer le panneau ».
          if (event.key === "Escape") event.stopPropagation();
        }}
        {...props}
      />
    </PopoverPrimitive.Portal>
  );
}
