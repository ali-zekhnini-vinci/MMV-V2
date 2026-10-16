// Module Clients (Phase 03-A) : routes imbriquées sous /clients dans le routeur mémoire du Shell.
//   /clients                liste            /clients/nouveau          création
//   /clients/:id            fiche            /clients/:id/modifier     modification
// La fiche et la modification chargent le client depuis l'API (jamais depuis la ligne de liste).
import { AlertTriangle, RefreshCw, SearchX, ServerOff, ShieldAlert } from "lucide-react";
import { useMemo, useState } from "react";
import { Navigate, Route, Routes, useNavigate, useParams } from "react-router";
import { Button } from "../components/ui/button.tsx";
import { ClientDetailView } from "./ClientDetailView.tsx";
import { ClientFormView } from "./ClientFormView.tsx";
import { useClientDetail } from "./clientQueries.ts";
import { useClientsService } from "./clientsService.ts";
import { ClientsListView } from "./ClientsListView.tsx";
import { LoadingBlock, StateBlock } from "./ClientStates.tsx";
import { type ClientListState, type ClientNotice, ClientsUiContext, INITIAL_LIST_STATE } from "./clientsUi.ts";
import "./Clients.css";

export function ClientsModule() {
  const [listState, setListState] = useState<ClientListState>(INITIAL_LIST_STATE);
  const [notice, setNotice] = useState<ClientNotice | null>(null);
  const ui = useMemo(() => ({ listState, setListState, notice, setNotice }), [listState, notice]);

  return (
    <ClientsUiContext.Provider value={ui}>
      <Routes>
        <Route index element={<ClientsListView />} />
        <Route path="nouveau" element={<ClientFormView mode="create" />} />
        <Route path=":clientId" element={<ClientRoute view="detail" />} />
        <Route path=":clientId/modifier" element={<ClientRoute view="edit" />} />
        <Route path="*" element={<Navigate to="/clients" replace />} />
      </Routes>
    </ClientsUiContext.Provider>
  );
}

function ClientRoute({ view }: { view: "detail" | "edit" }) {
  const { clientId = "" } = useParams();
  const navigate = useNavigate();
  const { enabled } = useClientsService();
  const query = useClientDetail(clientId);
  const back = (
    <Button onClick={() => navigate("/clients")}>Retour à la liste</Button>
  );

  if (!enabled) {
    return (
      <StateBlock tone="info" icon={ServerOff} title="Service Clients non activé" action={back}>
        <p>Aucune fiche client n&apos;est disponible tant que le service Clients n&apos;est pas activé (G03-METIER).</p>
      </StateBlock>
    );
  }
  if (query.isPending) return <LoadingBlock label="Chargement de la fiche client…" />;
  if (query.isError) {
    const { kind, message } = query.error.error;
    if (kind === "not-found") {
      return (
        <StateBlock tone="info" icon={SearchX} title="Client introuvable" action={back}>
          <p>Ce client n&apos;existe pas ou n&apos;existe plus sur le serveur MMV.</p>
        </StateBlock>
      );
    }
    if (kind === "forbidden") {
      return (
        <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert" action={back}>
          <p>Le serveur MMV a refusé l&apos;accès à cette fiche pour votre compte.</p>
        </StateBlock>
      );
    }
    return (
      <StateBlock
        tone="error"
        icon={AlertTriangle}
        title="Impossible de charger la fiche"
        role="alert"
        action={
          <Button variant="primary" onClick={() => void query.refetch()} disabled={query.isFetching}>
            <RefreshCw size={16} aria-hidden="true" />
            Réessayer
          </Button>
        }
      >
        <p>{message}</p>
      </StateBlock>
    );
  }

  const client = query.data;
  // Un client archivé est en lecture seule : la modification renvoie à la fiche.
  if (view === "edit" && client.status === "archived") return <Navigate to={`/clients/${client.id}`} replace />;
  // Le formulaire conserve la version lue à son ouverture (voir ClientFormView) : un rafraîchissement en
  // arrière-plan ne remplace ni les saisies ni le jeton de concurrence.
  return view === "edit" ? <ClientFormView key={client.id} mode="edit" client={client} /> : <ClientDetailView client={client} />;
}
