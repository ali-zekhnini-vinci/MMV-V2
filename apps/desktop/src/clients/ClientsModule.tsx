// Module Clients (Phase 03-A) : routes imbriquées sous /clients dans le routeur mémoire du Shell.
//   /clients                liste            /clients/nouveau          création
//   /clients/:id            fiche            /clients/:id/modifier     modification
// La fiche et la modification chargent le client depuis l'API (jamais depuis la ligne de liste).
import { AlertTriangle, RefreshCw, SearchX, ServerOff, ShieldAlert } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import { Navigate, Route, Routes, useLocation, useNavigate, useParams } from "react-router";
import { Button } from "../components/ui/button.tsx";
import { ClientOpticalTab } from "../optical/ClientOpticalTab.tsx";
import { useOpticalActivation } from "../optical/opticalActivation.ts";
import { readClientTab } from "../optical/opticalUi.ts";
import { ClientDetailView } from "./ClientDetailView.tsx";
import { clientDisplayName } from "./clientForm.ts";
import { ClientFormView } from "./ClientFormView.tsx";
import { useClientPermissions } from "./clientPermissions.ts";
import { useClientDetail } from "./clientQueries.ts";
import { useClientsService } from "./clientsService.ts";
import { ClientsListView } from "./ClientsListView.tsx";
import { LoadingBlock, StateBlock } from "./ClientStates.tsx";
import { type ClientListState, type ClientNotice, ClientsUiContext, INITIAL_LIST_STATE, type ScopedClientNotice } from "./clientsUi.ts";
import "./Clients.css";

export function ClientsModule() {
  const [listState, setListState] = useState<ClientListState>(INITIAL_LIST_STATE);
  const { pathname } = useLocation();
  const [scoped, setScoped] = useState<ScopedClientNotice | null>(null);
  const [shownPath, setShownPath] = useState(pathname);
  // O-03-15 : un message de confirmation n'appartient qu'à l'écran pour lequel il a été émis ; il est
  // effacé dès que l'écran change (ajustement pendant le rendu, sans effet ni affichage intermédiaire).
  // O-03-43 (03-D3) : un message destiné à l'écran SUIVANT (modification → fiche) est émis avant la navigation, que
  // React Router mène en transition : il n'est effacé qu'au changement d'écran effectif, jamais avant.
  if (shownPath !== pathname) {
    setShownPath(pathname);
    if (scoped !== null && scoped.path !== pathname) setScoped(null);
  }
  const notice = scoped !== null && scoped.path === pathname ? scoped : null;
  const setNotice = useCallback(
    (next: ClientNotice | null, path?: string) => setScoped(next === null ? null : { ...next, path: path ?? pathname }),
    [pathname],
  );
  const ui = useMemo(() => ({ listState, setListState, notice, setNotice }), [listState, notice, setNotice]);

  return (
    <ClientsUiContext.Provider value={ui}>
      <Routes>
        <Route index element={<ClientsListView />} />
        <Route path="nouveau" element={<CreateRoute />} />
        <Route path=":clientId" element={<ClientRoute view="detail" />} />
        <Route path=":clientId/modifier" element={<ClientRoute view="edit" />} />
        <Route path="*" element={<Navigate to="/clients" replace />} />
      </Routes>
    </ClientsUiContext.Provider>
  );
}

// Garde d'affichage : sans `clients.write`, le formulaire n'est pas proposé (le serveur refuserait par 403).
function CreateRoute() {
  const { enabled } = useClientsService();
  const { canWrite } = useClientPermissions();
  return enabled && !canWrite ? <Navigate to="/clients" replace /> : <ClientFormView mode="create" />;
}

function ClientRoute({ view }: { view: "detail" | "edit" }) {
  const { clientId = "" } = useParams();
  const navigate = useNavigate();
  const { enabled } = useClientsService();
  const { canWrite } = useClientPermissions();
  const location = useLocation();
  const opticalOn = useOpticalActivation();
  const query = useClientDetail(clientId);
  const back = (
    <Button onClick={() => navigate("/clients")}>Retour à la liste</Button>
  );

  if (!enabled) {
    return (
      <StateBlock tone="info" icon={ServerOff} title="Service Clients non activé" action={back}>
        <p>Aucune fiche client n&apos;est disponible : le service Clients est désactivé dans cette version de l&apos;application.</p>
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
  if (view === "edit" && (client.status === "archived" || !canWrite)) return <Navigate to={`/clients/${client.id}`} replace />;
  // Le formulaire conserve la version lue à son ouverture (voir ClientFormView) : un rafraîchissement en
  // arrière-plan ne remplace ni les saisies ni le jeton de concurrence.
  if (view === "edit") return <ClientFormView key={client.id} mode="edit" client={client} />;
  // 04-B : onglet « Dossier optique » seulement si le dossier optique est activé pour la session ; sinon l'onglet
  // reporté « Ordonnances — phase ultérieure » est conservé (O-04-16).
  return (
    <ClientDetailView
      client={client}
      initialTab={readClientTab(location.state)}
      opticalTab={opticalOn ? <ClientOpticalTab client={client} clientName={clientDisplayName(client)} /> : undefined}
    />
  );
}
