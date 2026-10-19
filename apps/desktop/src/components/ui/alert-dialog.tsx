// Boîte de confirmation — composant shadcn/ui sur la primitive Radix AlertDialog (piège de focus, Échap,
// retour du focus au déclencheur, rôle `alertdialog`). Gabarit visuel : modale du Lot 2 DRAFT (`.modal`,
// 480–520 px, rayon 16, icône 60 px), couleurs exclusivement issues des jetons MMV.
// O-03-33 (03-D1) : boîte ouverte par état (sans `Trigger` Radix) — l'élément qui avait le focus à l'ouverture est
// mémorisé et le reçoit de nouveau à la fermeture (Échap, Conserver), s'il est toujours présent et actif.
import * as AlertDialogPrimitive from "@radix-ui/react-alert-dialog";
import { type ComponentProps, type ReactNode, useRef } from "react";
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
  /** Contenu facultatif entre le texte et les actions (ex. motif d'annulation d'un rendez-vous, 03-B). */
  children?: ReactNode;
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
  children,
}: ConfirmDialogProps) {
  const opener = useRef<HTMLElement | null>(null);
  return (
    <AlertDialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <AlertDialogPrimitive.Portal>
        <AlertDialogPrimitive.Overlay className="mmv-dialog__overlay" />
        <AlertDialogPrimitive.Content
          className="mmv-dialog"
          aria-busy={pending}
          onOpenAutoFocus={() => {
            // Appelé avant le déplacement du focus dans la boîte : l'élément actif est encore le déclencheur.
            opener.current = document.activeElement instanceof HTMLElement && document.activeElement !== document.body ? document.activeElement : null;
          }}
          onCloseAutoFocus={(event) => {
            const target = opener.current;
            opener.current = null;
            if (target && target.isConnected && !(target as HTMLButtonElement).disabled) {
              event.preventDefault();
              target.focus();
            }
          }}
        >
          <span className={cn("mmv-dialog__icon", `mmv-dialog__icon--${tone}`)} aria-hidden="true">
            {icon}
          </span>
          <AlertDialogPrimitive.Title className="mmv-dialog__title">{title}</AlertDialogPrimitive.Title>
          <AlertDialogPrimitive.Description className="mmv-dialog__text">{description}</AlertDialogPrimitive.Description>
          {children}
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
