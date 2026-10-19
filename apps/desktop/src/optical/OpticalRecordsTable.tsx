// PROTOTYPE 04-A — table des enregistrements optiques (liste globale, états 1–4 ; sections de l'onglet de la fiche,
// état 5). Tri et filtres décidés par le service (paramètres), jamais en local. Badge de type sur chaque ligne (Q-04-151).
// Colonne « Résumé OD/OG » commutable (D-04-13.b à trancher : résumé de la maquette ou minimisation).
import { Eye } from "lucide-react";
import { Button } from "../components/ui/button.tsx";
import { Highlight } from "../clients/highlight.tsx";
import { formatIsoDate } from "../clients/clientForm.ts";
import { cn } from "../lib/utils.ts";
import type { OpticalRecordSummary } from "./opticalContract.ts";
import { eyeStateLabel, formatDateTime, KIND_LABEL } from "./opticalLabels.ts";
import { KindBadge, OpticalStatusBadge } from "./OpticalBadges.tsx";
import { eyeSummary } from "./opticalUi.ts";

interface OpticalRecordsTableProps {
  rows: OpticalRecordSummary[];
  caption: string;
  /** Liste globale : colonne Client ; onglet de la fiche : client implicite. */
  showClient: boolean;
  showSummary: boolean;
  search?: string;
  onOpen: (row: OpticalRecordSummary) => void;
}

export function OpticalRecordsTable({ rows, caption, showClient, showSummary, search = "", onOpen }: OpticalRecordsTableProps) {
  return (
    <div className="clients-panel__scroll">
      <table className="clients-table optical-table">
        <caption className="visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Type</th>
            {showClient && <th scope="col">Client</th>}
            <th scope="col">Prescripteur / collaborateur</th>
            <th scope="col">Statut</th>
            {showSummary && <th scope="col">Résumé OD / OG</th>}
            <th scope="col">Saisi le</th>
            <th scope="col">
              <span className="clients-th-right">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const label = `${KIND_LABEL[row.kind]} ${row.reference}`;
            return (
              <tr key={row.id} className={cn(row.status === "invalidated" && "clients-row--archived optical-row--invalidated")}>
                <td className="tnum">
                  {row.date ? formatIsoDate(row.date) : <span className="clients-muted">Non renseignée</span>}
                  <div className="clients-cell__ref">{row.reference}</div>
                </td>
                <td>
                  <KindBadge kind={row.kind} />
                </td>
                {showClient && (
                  <td>
                    <div className="clients-cell__name">
                      <Highlight text={row.client.displayName} query={search} />
                    </div>
                    <div className="clients-cell__ref">
                      {row.client.reference}
                      {row.client.status === "archived" && " · client archivé"}
                    </div>
                  </td>
                )}
                <td>{row.source ? <Highlight text={row.source} query={search} /> : <span className="clients-muted">Non renseigné</span>}</td>
                <td>
                  <OpticalStatusBadge status={row.status} revisionNo={row.revisionNo} />
                </td>
                {showSummary && (
                  <td className="tnum optical-summary">
                    <div>{eyeSummary("od", row.od, eyeStateLabel(row.kind, row.od.state))}</div>
                    <div>{eyeSummary("og", row.og, eyeStateLabel(row.kind, row.og.state))}</div>
                  </td>
                )}
                <td className="tnum clients-muted">{formatDateTime(row.recordedAtUtc)}</td>
                <td>
                  <div className="clients-rowacts">
                    <Button variant="ghost" size="icon" aria-label={`Consulter ${label}`} title={`Consulter ${label}`} onClick={() => onOpen(row)}>
                      <Eye size={16} aria-hidden="true" />
                    </Button>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
