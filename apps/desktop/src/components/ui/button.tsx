// Bouton — composant shadcn/ui (structure cva + Radix Slot) adapté au Design System MMV : variantes et
// cotes reprises des styles validés (login : MMV-Login-handoff ; shell : MMV-Shell-handoff §7), couleurs
// exclusivement issues des jetons MMV (styles/index.css). Aucun thème shadcn par défaut.
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import type { ComponentProps } from "react";
import { cn } from "../../lib/utils.ts";

const buttonVariants = cva(
  "inline-flex shrink-0 cursor-pointer items-center justify-center whitespace-nowrap font-semibold outline-none disabled:cursor-not-allowed focus-visible:shadow-focus [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        // Action principale (Se connecter) : accent MMV, accent atténué une fois désactivé.
        primary:
          "rounded-sm bg-accent text-accent-contrast enabled:hover:bg-accent-hover enabled:active:translate-y-[0.5px] enabled:active:bg-accent-pressed disabled:bg-accent-disabled",
        // Action secondaire (Réessayer, Revérifier) : carte bordée.
        secondary:
          "rounded-sm border border-border-strong bg-surface-card text-text-primary enabled:hover:bg-surface-hover disabled:text-text-disabled",
        // Bouton d'icône (barre supérieure, sidebar) : transparent, survol discret.
        ghost:
          "rounded-sm bg-transparent font-normal text-text-secondary enabled:hover:bg-surface-hover enabled:hover:text-text-primary disabled:text-text-disabled",
      },
      size: {
        default: "h-10 gap-1.5 px-4",
        sm: "h-8 gap-1.5 px-3 text-sm",
        lg: "h-12 gap-2 px-4 text-[15px]",
        icon: "size-8 p-0",
        "icon-lg": "size-[42px] p-0",
      },
    },
    defaultVariants: { variant: "secondary", size: "default" },
  },
);

export interface ButtonProps extends ComponentProps<"button">, VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

export function Button({ className, variant, size, asChild = false, type = "button", ...props }: ButtonProps) {
  const Comp = asChild ? Slot : "button";
  return <Comp data-slot="button" type={type} className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}
