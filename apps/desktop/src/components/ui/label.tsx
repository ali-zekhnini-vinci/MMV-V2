// Étiquette de champ — composant shadcn/ui sur la primitive Radix Label. Aucun style imposé : l'écran
// appelant garde sa typographie MMV (ex. .login__field label).
import * as LabelPrimitive from "@radix-ui/react-label";
import type { ComponentProps } from "react";
import { cn } from "../../lib/utils.ts";

export function Label({ className, ...props }: ComponentProps<typeof LabelPrimitive.Root>) {
  return <LabelPrimitive.Root data-slot="label" className={cn("select-none", className)} {...props} />;
}
