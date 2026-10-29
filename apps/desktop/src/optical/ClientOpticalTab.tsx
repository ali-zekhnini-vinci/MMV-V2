// PROTOTYPE 04-A — onglet « Dossier optique » de la fiche client (GAP-04-04, état 5 ; D-04-08.a PROPOSED) : deux
// sections DISTINCTES « Ordonnances médicales » / « Mesures magasin » (D-04-01), chacune avec sa table et son état
// vide (état 7). « Ajouter… » ouvre le choix explicite du type (GAP-04-01). Client archivé : consultation seule,
// ajout refusé (état 18, D-04-07 PROPOSED). Monté par l'hôte seulement pour une session portant `optical.read`.
import { AlertTriangle, Archive, CheckSquare, FileText, Plus, RefreshCw, Ruler, ShieldAlert, Square } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { Button } from "../components/ui/button.tsx";
import { LoadingBlock, StateBlock } from "../clients/ClientStates.tsx";
import { cn } from "../lib/utils.ts";
import { canWriteKind, useOpticalAccess } from "./opticalAccess.ts";
import { OPTICAL_KINDS, type OpticalKind, type OpticalRecordSummary } from "./opticalContract.ts";
import { KIND_PLURAL } from "./opticalLabels.ts";
import { ProposedTag } from "./OpticalBadges.tsx";
import { useClientOpticalRecords } from "./opticalQueries.ts";
import { OpticalRecordsTable } from "./OpticalRecordsTable.tsx";
import { OpticalTypeChooser } from "./OpticalTypeChooser.tsx";
import { clientCardPath, opticalDetailPath, opticalNewPath, type OpticalLaunchState } from "./opticalUi.ts";

interface ClientOpticalTabProps {
  client: { id: string; reference: string; status: "active" | "archived" };
  clientName: string;
}

const EMPTY_TEXT: Record<OpticalKind, string> = {
  prescription: "Aucune ordonnance médicale n'est enregistrée pour ce client.",
  measurement: "Aucune mesure magasin n'est enregistrée pour ce client.",
};

export function ClientOpticalTab({ client, clientName }: ClientOpticalTabProps) {
  const navigate = useNavigate();
  const access = useOpticalAccess();
  const [includeInvalidated, setIncludeInvalidated] = useState(false);
  const [choosing, setChoosing] = useState(false);
  const query = useClientOpticalRecords(client.id, includeInvalidated, access.canRead);
  const archived = client.status === "archived";
  const writableKinds = OPTICAL_KINDS.filter((kind) => canWriteKind(access, kind));
  const launch: OpticalLaunchState = { returnTo: clientCardPath(client.id), returnLabel: clientName, client: { id: client.id, label: `${clientName} · ${client.reference}` } };

  const open = (row: OpticalRecordSummary) => navigate(opticalDetailPath(row.kind, row.id), { state: launch });
  const choose = (kind: OpticalKind) => {
    setChoosing(false);
    navigate(opticalNewPath(kind), { state: launch });
  };

  if (query.isPending) return <LoadingBlock label="Chargement du dossier optique…" />;
  if (query.isError) {
    return query.error.error.kind === "forbidden" ? (
      <StateBlock tone="warning" icon={ShieldAlert} title="Accès non autorisé" role="alert">
        <p>Le service a refusé l&apos;accès au dossier optique de ce client pour ce profil.</p>
      </StateBlock>
    ) : (
      <StateBlock
        tone="error"
        icon={AlertTriangle}
        title="Impossible de charger le dossier optique"
        role="alert"
        action={
          <Button variant="primary" onClick={() => void query.refetch()} disabled={query.isFetching}>
            <RefreshCw size={16} aria-hidden="true" />
            Réessayer
          </Button>
        }
      >
        <p>{query.error.error.message}</p>
      </StateBlock>
    );
  }

  const rows = query.data;
  return (
    <div className="optical-tab">
      <div className="optical-tab__head">
        <p className="clients-head__hint" role="note">
          Dossier optique du client : ordonnances médicales reçues et mesures réalisées au magasin, toujours distinctes. <ProposedTag refs="D-04-08" />
        </p>
        <div className="clients-actions clients-actions--wrap">
          <button
            type="button"
            className={cn("clients-chip", includeInvalidated && "clients-chip--on")}
            aria-pressed={includeInvalidated}
            onClick={() => setIncludeInvalidated((value) => !value)}
          >
            {includeInvalidated ? <CheckSquare size={16} aria-hidden="true" /> : <Square size={16} aria-hidden="true" />}
            Inclure les invalidés
          </button>
          {writableKinds.length > 0 && (
            <Button
              variant={archived ? undefined : "primary"}
              disabled={archived}
              title={archived ? "Client archivé : réactivez le client avant d'ajouter un enregistrement" : undefined}
              onClick={() => setChoosing(true)}
            >
              <Plus size={16} aria-hidden="true" />
              Ajouter…
            </Button>
          )}
        </div>
      </div>
      {archived && (
        <div className="clients-banner clients-banner--neutral" role="note">
          <span className="clients-banner__icon" aria-hidden="true">
            <Archive size={20} />
          </span>
          <div>
            <div className="clients-banner__title">Client archivé — consultation seule</div>
            <div className="clients-banner__text">Réactivez le client avant d&apos;ajouter une ordonnance médicale ou une mesure magasin (D-04-07, proposé).</div>
          </div>
        </div>
      )}

      {OPTICAL_KINDS.map((kind) => {
        const items = rows.filter((row) => row.kind === kind);
        const Icon = kind === "prescription" ? FileText : Ruler;
        return (
          <section key={kind} className="clients-card optical-section" aria-labelledby={`optical-section-${kind}`}>
            <h2 className="clients-card__title optical-section__title" id={`optical-section-${kind}`}>
              <Icon size={18} aria-hidden="true" />
              {KIND_PLURAL[kind]}
              <span className="clients-kbd">{items.length}</span>
            </h2>
            {items.length === 0 ? (
              <p className="optical-section__empty">
                {EMPTY_TEXT[kind]}
                {canWriteKind(access, kind) && !archived && (
                  <Button variant="tertiary" size="sm" onClick={() => choose(kind)}>
                    <Plus size={14} aria-hidden="true" />
                    {kind === "prescription" ? "Transcrire une ordonnance médicale" : "Saisir une mesure magasin"}
                  </Button>
                )}
              </p>
            ) : (
              <OpticalRecordsTable rows={items} caption={`${KIND_PLURAL[kind]} de ${clientName}`} showClient={false} showSummary={false} onOpen={open} />
            )}
          </section>
        );
      })}

      <OpticalTypeChooser open={choosing} onOpenChange={setChoosing} kinds={writableKinds} clientLabel={clientName} onChoose={choose} />
    </div>
  );
}

