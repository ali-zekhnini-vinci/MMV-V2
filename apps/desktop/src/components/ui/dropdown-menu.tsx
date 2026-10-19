// Menu déroulant — composant shadcn/ui sur la primitive Radix DropdownMenu (focus, flèches, Échap,
// clic extérieur, retour du focus au déclencheur). Styles : cotes du menu profil MMV (MMV-Shell-handoff
// §12), jetons MMV uniquement.
import * as DropdownMenuPrimitive from "@radix-ui/react-dropdown-menu";
import { Check } from "lucide-react";
import type { ComponentProps } from "react";
import { cn } from "../../lib/utils.ts";

export const DropdownMenu = DropdownMenuPrimitive.Root;
export const DropdownMenuTrigger = DropdownMenuPrimitive.Trigger;

export function DropdownMenuContent({
  className,
  sideOffset = 6,
  ...props
}: ComponentProps<typeof DropdownMenuPrimitive.Content>) {
  return (
    <DropdownMenuPrimitive.Portal>
      <DropdownMenuPrimitive.Content
        data-slot="dropdown-menu-content"
        sideOffset={sideOffset}
        className={cn(
          "z-20 min-w-60 rounded-md border border-border bg-surface-raised p-2 font-body text-body text-text-primary shadow-3 outline-none",
          className,
        )}
        {...props}
      />
    </DropdownMenuPrimitive.Portal>
  );
}

export function DropdownMenuLabel({ className, ...props }: ComponentProps<typeof DropdownMenuPrimitive.Label>) {
  return (
    <DropdownMenuPrimitive.Label
      data-slot="dropdown-menu-label"
      className={cn("flex flex-col gap-0.5 px-3 pt-2 pb-3", className)}
      {...props}
    />
  );
}

export function DropdownMenuSeparator({ className, ...props }: ComponentProps<typeof DropdownMenuPrimitive.Separator>) {
  return (
    <DropdownMenuPrimitive.Separator
      data-slot="dropdown-menu-separator"
      className={cn("mb-2 h-px bg-border", className)}
      {...props}
    />
  );
}

export function DropdownMenuItem({
  className,
  variant = "default",
  ...props
}: ComponentProps<typeof DropdownMenuPrimitive.Item> & { variant?: "default" | "destructive" }) {
  return (
    <DropdownMenuPrimitive.Item
      data-slot="dropdown-menu-item"
      data-variant={variant}
      className={cn(
        "flex h-10 w-full cursor-pointer items-center gap-2 rounded-sm px-3 text-body text-text-primary outline-none select-none",
        "data-highlighted:bg-surface-hover data-disabled:pointer-events-none data-disabled:text-text-disabled",
        "data-[variant=destructive]:data-highlighted:text-error",
        className,
      )}
      {...props}
    />
  );
}

export const DropdownMenuRadioGroup = DropdownMenuPrimitive.RadioGroup;

/** Choix exclusif (filtres et tri de liste, phase 03) : coche visible et `aria-checked` fournis par Radix. */
export function DropdownMenuRadioItem({ className, children, ...props }: ComponentProps<typeof DropdownMenuPrimitive.RadioItem>) {
  return (
    <DropdownMenuPrimitive.RadioItem
      data-slot="dropdown-menu-radio-item"
      className={cn(
        "flex h-10 w-full cursor-pointer items-center gap-2 rounded-sm px-3 text-body text-text-primary outline-none select-none",
        "data-highlighted:bg-surface-hover data-[state=checked]:font-semibold data-[state=checked]:text-accent",
        className,
      )}
      {...props}
    >
      <span className="inline-flex size-4 items-center justify-center" aria-hidden="true">
        <DropdownMenuPrimitive.ItemIndicator>
          <Check size={16} />
        </DropdownMenuPrimitive.ItemIndicator>
      </span>
      {children}
    </DropdownMenuPrimitive.RadioItem>
  );
}
