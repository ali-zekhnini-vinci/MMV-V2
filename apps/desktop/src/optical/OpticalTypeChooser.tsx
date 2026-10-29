// PROTOTYPE 04-A — GAP-04-01 : choix EXPLICITE du type avant toute saisie (D-04-01 APPROVED). Boîte Radix
// AlertDialog (piège de focus, Échap annule, focus rendu au déclencheur) au gabarit des confirmations (overlays.css).
// Chaque option n'est proposée qu'avec le droit d'écriture du type (affichage seulement, D-04-05 PROPOSED).
import * as AlertDialogPrimitive from "@radix-ui/react-alert-dialog";
import { FilePlus2, FileText, Ruler } from "lucide-react";
import { useRef } from "react";
import { Button } from "../components/ui/button.tsx";
import "../components/ui/overlays.css";
import type { OpticalKind } from "./opticalContract.ts";
import { KIND_EXPLANATION, KIND_LABEL } from "./opticalLabels.ts";

interface OpticalTypeChooserProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Types proposés (droit d'écriture du type). */
  kinds: readonly OpticalKind[];
  clientLabel: string | null;
  onChoose: (kind: OpticalKind) => void;
}

export function OpticalTypeChooser({ open, onOpenChange, kinds, clientLabel, onChoose }: OpticalTypeChooserProps) {
  const first = useRef<HTMLButtonElement | null>(null);
  return (
    <AlertDialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <AlertDialogPrimitive.Portal>
        <AlertDialogPrimitive.Overlay className="mmv-dialog__overlay" />
        <AlertDialogPrimitive.Content
          className="mmv-dialog optical-chooser"
          onOpenAutoFocus={(event) => {
            // Focus initial sur la première option proposée (PREPARATION_04A § 4), pas sur « Annuler ».
            event.preventDefault();
            first.current?.focus();
          }}
        >
          <span className="mmv-dialog__icon mmv-dialog__icon--accent" aria-hidden="true">
            <FilePlus2 size={28} />
          </span>
          <AlertDialogPrimitive.Title className="mmv-dialog__title">Que souhaitez-vous ajouter ?</AlertDialogPrimitive.Title>
          <AlertDialogPrimitive.Description className="mmv-dialog__text">
            Choisissez le type avant toute saisie{clientLabel ? <> pour <b>{clientLabel}</b></> : null}. Les deux types restent distincts : une mesure
            magasin n&apos;est jamais convertie en ordonnance médicale.
          </AlertDialogPrimitive.Description>
          <div className="optical-chooser__options">
            {kinds.map((kind, index) => (
              <button
                key={kind}
                type="button"
                className="optical-chooser__option"
                ref={index === 0 ? first : undefined}
                onClick={() => onChoose(kind)}
              >
                <span className={`optical-chooser__icon optical-kind--${kind}`} aria-hidden="true">
                  {kind === "prescription" ? <FileText size={22} /> : <Ruler size={22} />}
                </span>
                <span>
                  <span className="optical-chooser__name">{KIND_LABEL[kind]}</span>
                  <span className="optical-chooser__text">{KIND_EXPLANATION[kind]}</span>
                </span>
              </button>
            ))}
          </div>
          <div className="mmv-dialog__actions">
            <AlertDialogPrimitive.Cancel asChild>
              <Button>Annuler</Button>
            </AlertDialogPrimitive.Cancel>
          </div>
        </AlertDialogPrimitive.Content>
      </AlertDialogPrimitive.Portal>
    </AlertDialogPrimitive.Root>
  );
}
