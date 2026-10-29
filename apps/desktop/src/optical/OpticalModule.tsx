// PROTOTYPE 04-A — routes du dossier optique, montées par l'HÔTE sous /optique (en 04-A : uniquement le banc isolé
// src/bench/ ; le Shell réel ne les monte pas, O-04-16). Le détail et le brouillon sont chargés depuis le service,
// jamais depuis une ligne de liste.
//   /optique                      liste globale          /optique/nouveau            choix du type puis du client
//   /optique/nouveau/:type        formulaire             /optique/:type/:id          détail
//   /optique/:type/:id/brouillon  brouillon (première saisie ou rectification)
import { AlertTriangle, RefreshCw, SearchX } from "lucide-react";
import { useState } from "react";
import { Navigate, Route, Routes, useLocation, useNavigate, useParams } from "react-router";
import { ClientPicker } from "../appointments/ClientPicker.tsx";
import "../appointments/Appointments.css";
import "../clients/Clients.css";
import { Button } from "../components/ui/button.tsx";
import { LoadingBlock, StateBlock } from "../clients/ClientStates.tsx";
import { clientDisplayName } from "../clients/clientForm.ts";
import { ForbiddenView } from "../views/ForbiddenView.tsx";
import { canWriteKind, useOpticalAccess } from "./opticalAccess.ts";
import { OPTICAL_KINDS, type OpticalKind } from "./opticalContract.ts";
import { kindFromSegment, KIND_LABEL } from "./opticalLabels.ts";
import { OpticalListView } from "./OpticalListView.tsx";
import { useOpticalRecord } from "./opticalQueries.ts";
import { OpticalRecordDetail } from "./OpticalRecordDetail.tsx";
import { OpticalRecordForm } from "./OpticalRecordForm.tsx";
import { OpticalTypeChooser } from "./OpticalTypeChooser.tsx";
import { opticalDetailPath, opticalListPath, opticalNewPath, readOpticalLaunch } from "./opticalUi.ts";
import "./Optical.css";

const FORBIDDEN = "Ce profil ne détient pas le droit de lecture du dossier optique (optical.read, proposé). L'accès direct est refusé.";

export function OpticalModule() {
  const access = useOpticalAccess();
  // Accès direct sans droit : page 403, aucune donnée chargée (GAP-04-05).
  if (!access.canRead) return <ForbiddenView reason={FORBIDDEN} />;
  return (
    <Routes>
      <Route index element={<OpticalListView />} />
      <Route path="nouveau" element={<GlobalCreate />} />
      <Route path="nouveau/:type" element={<CreateRoute />} />
      <Route path=":type/:id" element={<RecordRoute view="detail" />} />
      <Route path=":type/:id/brouillon" element={<RecordRoute view="draft" />} />
      <Route path="*" element={<Navigate to={opticalListPath()} replace />} />
    </Routes>
  );
}

/** Liste globale → choix explicite du type → client ACTIF (ClientPicker, D-04-07) → formulaire (états 12–13). */
function GlobalCreate() {
  const navigate = useNavigate();
  const access = useOpticalAccess();
  const kinds = OPTICAL_KINDS.filter((kind) => canWriteKind(access, kind));
  const [kind, setKind] = useState<OpticalKind | null>(null);
  if (kinds.length === 0) return <Navigate to={opticalListPath()} replace />;
  const close = () => navigate(opticalListPath());

  return (
    <section className="clients-view" aria-labelledby="optical-new-title">
      <header className="clients-head">
        <div>
          <h1 id="optical-new-title" className="clients-head__title">
            {kind ? `Nouvelle ${KIND_LABEL[kind].toLocaleLowerCase("fr")}` : "Nouveau…"}
          </h1>
          <p className="clients-head__sub">{kind ? "Sélectionnez le client : seuls les clients actifs sont proposés." : "Choisissez d'abord le type d'enregistrement."}</p>
        </div>
      </header>
      {kind && (
        <div className="rdv-side optical-picker">
          <ClientPicker
            allowed
            selectedId=""
            onPick={(client) =>
              navigate(opticalNewPath(kind), {
                state: { returnTo: opticalListPath(), returnLabel: "Dossiers optiques", client: { id: client.id, label: `${clientDisplayName(client)} · ${client.reference}` } },
              })
            }
            onBack={() => setKind(null)}
            onClose={close}
          />
        </div>
      )}
      <OpticalTypeChooser open={kind === null} onOpenChange={(open) => !open && kind === null && close()} kinds={kinds} clientLabel={null} onChoose={setKind} />
    </section>
  );
}

function CreateRoute() {
  const { type } = useParams();
  const { state } = useLocation();
  const access = useOpticalAccess();
  const kind = kindFromSegment(type);
  const launch = readOpticalLaunch(state);
  if (kind === null || !canWriteKind(access, kind)) return <Navigate to={opticalListPath()} replace />;
  // Client obligatoire et conservé pendant la saisie ; sans lui, retour au choix (aucune seconde recherche cachée).
  if (!launch?.client) return <Navigate to={opticalNewPath()} replace />;
  return <OpticalRecordForm key={`${kind}-${launch.client.id}`} mode="create" kind={kind} client={launch.client} launch={launch} />;
}

function RecordRoute({ view }: { view: "detail" | "draft" }) {
  const { type, id = "" } = useParams();
  const { state } = useLocation();
  const navigate = useNavigate();
  const access = useOpticalAccess();
  const kind = kindFromSegment(type);
  const launch = readOpticalLaunch(state);
  const query = useOpticalRecord(kind ?? "prescription", id, kind !== null);
  const back = <Button onClick={() => navigate(opticalListPath())}>Retour à la liste</Button>;

  if (kind === null) return <Navigate to={opticalListPath()} replace />;
  if (query.isPending) return <LoadingBlock label="Chargement de l'enregistrement…" />;
  if (query.isError) {
    const { kind: errorKind, message } = query.error.error;
    if (errorKind === "not-found") {
      return (
        <StateBlock tone="info" icon={SearchX} title="Enregistrement introuvable" action={back}>
          <p>Cet enregistrement n&apos;existe pas ou plus.</p>
        </StateBlock>
      );
    }
    if (errorKind === "forbidden") return <ForbiddenView reason="Le service a refusé l'accès à cet enregistrement pour ce profil." />;
    return (
      <StateBlock
        tone="error"
        icon={AlertTriangle}
        title="Impossible de charger l'enregistrement"
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
  const record = query.data;
  if (view === "draft") {
    // Brouillon modifiable seulement s'il existe, avec le droit du type, client actif et enregistrement actif.
    const editable = record.openDraft !== null && canWriteKind(access, record.kind) && record.lifecycle === "active" && record.client.status === "active";
    if (!editable) return <Navigate to={opticalDetailPath(record.kind, record.id)} replace state={state} />;
    // Le formulaire fige la version lue à son ouverture : un rafraîchissement ne remplace pas la saisie.
    return <OpticalRecordForm key={record.openDraft?.id} mode="draft" record={record} launch={launch} />;
  }
  return <OpticalRecordDetail record={record} launch={launch} />;
}
