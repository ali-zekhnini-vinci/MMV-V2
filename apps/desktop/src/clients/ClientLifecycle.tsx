// Archivage et réactivation (Lot 2 DRAFT, planches 22, 24–27). Le serveur décide du droit (`clients.archive`,
// D-03-08) et de la concurrence (jeton `version`, D-03-06). Aucune suppression physique (D-03-05 acceptée).
import { AlertTriangle, Archive, ArchiveRestore, ShieldAlert } from "lucide-react";
import { type ReactNode, useState } from "react";
import type { ClientSummary } from "../api/clients.ts";
import type { ApiRequestError } from "../api/queryClient.ts";
import { ConfirmDialog } from "../components/ui/alert-dialog.tsx";
import { clientDisplayName } from "./clientForm.ts";
import { useArchiveClient, useReactivateClient } from "./clientQueries.ts";
import { useClientsUi } from "./clientsUi.ts";

type Pending = { action: "archive" | "reactivate"; client: ClientSummary } | null;

interface FailureView {
  tone: "error" | "info";
  title: string;
  text: string;
}

/** Message d'échec d'une écriture : jamais le texte du serveur, seulement le type d'erreur borné. */
export function describeWriteFailure(error: ApiRequestError): FailureView {
  switch (error.error.kind) {
    case "conflict":
      return {
        tone: "error",
        title: "Action impossible",
        text: "Ce client a été modifié depuis un autre poste et n'est plus disponible pour cette opération. Les données ont été actualisées. Veuillez réessayer.",
      };
    case "forbidden":
      return { tone: "info", title: "Action non autorisée", text: "Le serveur MMV a refusé cette action pour votre compte." };
    case "not-found":
      return { tone: "error", title: "Client introuvable", text: "Ce client n'existe plus sur le serveur MMV. Les données ont été actualisées." };
    default:
      return { tone: "error", title: "Erreur technique", text: `${error.error.message} Aucune modification n'a été enregistrée.` };
  }
}

export function useClientLifecycle(): {
  requestArchive: (client: ClientSummary) => void;
  requestReactivate: (client: ClientSummary) => void;
  dialogs: ReactNode;
} {
  const { setNotice } = useClientsUi();
  const archive = useArchiveClient();
  const reactivate = useReactivateClient();
  const [pending, setPending] = useState<Pending>(null);
  const [failure, setFailure] = useState<FailureView | null>(null);

  const run = () => {
    if (pending === null) return;
    const { action, client } = pending;
    const mutation = action === "archive" ? archive : reactivate;
    const name = `${client.firstName} ${client.lastName}`;
    mutation.mutate(
      { id: client.id, version: client.version },
      {
        onSuccess: () => {
          setPending(null);
          setNotice(
            action === "archive"
              ? { title: "Client archivé", message: `${name} a été retiré(e) des listes actives. Son historique est conservé.` }
              : { title: "Client réactivé", message: `${name} est de nouveau actif(ve).` },
          );
        },
        onError: (error) => {
          setPending(null);
          setFailure(describeWriteFailure(error));
        },
      },
    );
  };

  const busy = archive.isPending || reactivate.isPending;
  const target = pending ? <b>{clientDisplayName(pending.client)}</b> : null;

  const dialogs = (
    <>
      <ConfirmDialog
        open={pending?.action === "archive"}
        onOpenChange={(open) => !open && !busy && setPending(null)}
        tone="warning"
        icon={<Archive size={28} />}
        title="Archiver ce client ?"
        description={
          <>
            Le client {target} sera retiré des listes actives. Sa fiche est conservée et vous pourrez le réactiver à tout moment.
          </>
        }
        cancelLabel="Annuler"
        confirmLabel={busy ? "Archivage…" : "Archiver le client"}
        confirmIcon={<Archive size={16} aria-hidden="true" />}
        onConfirm={run}
        pending={busy}
      />
      <ConfirmDialog
        open={pending?.action === "reactivate"}
        onOpenChange={(open) => !open && !busy && setPending(null)}
        tone="accent"
        icon={<ArchiveRestore size={28} />}
        title="Réactiver ce client ?"
        description={<>Le client {target} réapparaîtra dans les listes actives et pourra de nouveau être modifié.</>}
        cancelLabel="Annuler"
        confirmLabel={busy ? "Réactivation…" : "Réactiver le client"}
        confirmIcon={<ArchiveRestore size={16} aria-hidden="true" />}
        onConfirm={run}
        pending={busy}
      />
      <ConfirmDialog
        open={failure !== null}
        onOpenChange={(open) => !open && setFailure(null)}
        tone={failure?.tone ?? "error"}
        icon={failure?.tone === "info" ? <ShieldAlert size={28} /> : <AlertTriangle size={28} />}
        title={failure?.title ?? ""}
        description={failure?.text ?? ""}
        cancelLabel="Fermer"
      />
    </>
  );

  return {
    requestArchive: (client) => setPending({ action: "archive", client }),
    requestReactivate: (client) => setPending({ action: "reactivate", client }),
    dialogs,
  };
}
