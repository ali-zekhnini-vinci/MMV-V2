// Boîte de confirmation — composant shadcn/ui sur la primitive Radix AlertDialog (piège de focus, Échap,
// retour du focus au déclencheur, rôle `alertdialog`). Gabarit visuel : modale du Lot 2 DRAFT (`.modal`,
// 480–520 px, rayon 16, icône 60 px), couleurs exclusivement issues des jetons MMV.
import * as AlertDialogPrimitive from "@radix-ui/react-alert-dialog";
import type { ComponentProps, ReactNode } from "react";
import { cn } from "../../lib/utils.ts";
import "./overlays.css";
import { Button } from "./button.tsx";

export type AlertTone = "warning" | "accent" | "error" | "info";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tone: AlertTone;
  icon: ReactNode;
  title: string;
  description: ReactNode;
  /** Libellé de l'action principale ; absent = boîte d'information à un seul bouton. */
  confirmLabel?: string;
  confirmIcon?: ReactNode;
  cancelLabel: string;
  onConfirm?: () => void;
  /** Action en cours : boutons désactivés, libellé conservé. */
  pending?: boolean;
}

export function ConfirmDialog({
  open,
  onOpenChange,
  tone,
  icon,
  title,
  description,
  confirmLabel,
  confirmIcon,
  cancelLabel,
  onConfirm,
  pending = false,
}: ConfirmDialogProps) {
  return (
    <AlertDialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <AlertDialogPrimitive.Portal>
        <AlertDialogPrimitive.Overlay className="mmv-dialog__overlay" />
        <AlertDialogPrimitive.Content className="mmv-dialog" aria-busy={pending}>
          <span className={cn("mmv-dialog__icon", `mmv-dialog__icon--${tone}`)} aria-hidden="true">
            {icon}
          </span>
          <AlertDialogPrimitive.Title className="mmv-dialog__title">{title}</AlertDialogPrimitive.Title>
          <AlertDialogPrimitive.Description className="mmv-dialog__text">{description}</AlertDialogPrimitive.Description>
          <div className="mmv-dialog__actions">
            <AlertDialogPrimitive.Cancel asChild>
              <Button variant={confirmLabel ? "secondary" : "primary"} disabled={pending}>
                {cancelLabel}
              </Button>
            </AlertDialogPrimitive.Cancel>
            {confirmLabel && (
              <DialogAction
                onClick={(event) => {
                  // La fermeture est pilotée par l'appelant (succès, erreur) : pas de fermeture automatique.
                  event.preventDefault();
                  onConfirm?.();
                }}
                disabled={pending}
              >
                {confirmIcon}
                {confirmLabel}
              </DialogAction>
            )}
          </div>
        </AlertDialogPrimitive.Content>
      </AlertDialogPrimitive.Portal>
    </AlertDialogPrimitive.Root>
  );
}

function DialogAction({ children, ...props }: ComponentProps<"button">) {
  return (
    <AlertDialogPrimitive.Action asChild>
      <Button variant="primary" {...props}>
        {children}
      </Button>
    </AlertDialogPrimitive.Action>
  );
}
