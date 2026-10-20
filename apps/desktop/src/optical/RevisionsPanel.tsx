// PROTOTYPE 04-A — révisions et comparaison champ à champ (GAP-04-07, D-04-04.f ; E-02 / D-04-11 PROPOSED). Liste
// chronologique (numéro, date, auteur, motif) ; rectifications abandonnées conservées, masquées par défaut ;
// comparaison en deux colonnes, champs différents signalés par le TEXTE « Différent » (pas seulement la couleur).
import { GitCompare } from "lucide-react";
import { useId, useState } from "react";
import { cn } from "../lib/utils.ts";
import type { OpticalActor, OpticalRecord, OpticalRevision } from "./opticalContract.ts";
import { formatDateTime, RECTIFICATION_REASON_LABEL } from "./opticalLabels.ts";
import { compareRows, revisionLabel } from "./opticalRevisions.ts";
import { ProposedTag } from "./OpticalBadges.tsx";

export function RevisionsPanel({ record, measurers }: { record: OpticalRecord; measurers: readonly OpticalActor[] }) {
  const id = useId();
  const [showDiscarded, setShowDiscarded] = useState(false);
  const comparable = record.revisions.filter((revision) => revision.status !== "discarded" || showDiscarded);
  const discardedCount = record.revisions.filter((revision) => revision.status === "discarded").length;
  const confirmed = record.revisions.filter((revision) => revision.status === "confirmed");
  // Par défaut : dernière confirmée ↔ brouillon ouvert, sinon les deux dernières confirmées.
  const defaults = record.openDraft && record.lastConfirmed ? [record.lastConfirmed.id, record.openDraft.id] : confirmed.slice(-2).map((revision) => revision.id);
  const [leftId, setLeftId] = useState(defaults[0] ?? "");
  const [rightId, setRightId] = useState(defaults[1] ?? "");
  const [onlyDifferent, setOnlyDifferent] = useState(true);
  const left = record.revisions.find((revision) => revision.id === leftId);
  const right = record.revisions.find((revision) => revision.id === rightId);
  const rows = left && right ? compareRows(left.content, right.content, measurers) : [];
  const different = rows.filter((row) => row.left !== row.right);
  const shown = onlyDifferent ? different : rows;

  return (
    <section className="clients-card optical-revisions" aria-labelledby={`${id}-title`}>
      <h2 className="clients-card__title" id={`${id}-title`}>
        Révisions <ProposedTag refs="D-04-04 · D-04-11" />
      </h2>
      <ol className="optical-revisions__list">
        {record.revisions
          .filter((revision) => revision.status !== "discarded" || showDiscarded)
          .map((revision) => (
            <li key={revision.id} className={cn("optical-revisions__item", `optical-revisions__item--${revision.status}`)}>
              <span className="optical-revisions__name">{revisionLabel(revision)}</span>
              <span className="clients-muted">
                {revision.status === "confirmed" && revision.confirmedAtUtc
                  ? `confirmée le ${formatDateTime(revision.confirmedAtUtc)} par ${revision.confirmedBy?.displayName ?? "—"}`
                  : revision.status === "discarded" && revision.discardedAtUtc
                    ? `abandonnée le ${formatDateTime(revision.discardedAtUtc)} par ${revision.discardedBy?.displayName ?? "—"}`
                    : `ouvert le ${formatDateTime(revision.startedAtUtc)} par ${revision.startedBy.displayName}`}
                {revision.reasonCode && ` · motif : ${RECTIFICATION_REASON_LABEL[revision.reasonCode]}${revision.reasonComment ? ` — ${revision.reasonComment}` : ""}`}
              </span>
            </li>
          ))}
      </ol>
      {discardedCount > 0 && (
        <label className="optical-check">
          <input type="checkbox" checked={showDiscarded} onChange={(event) => setShowDiscarded(event.target.checked)} />
          <span>Afficher les rectifications abandonnées ({discardedCount})</span>
        </label>
      )}

      {comparable.length >= 2 ? (
        <div className="optical-compare">
          <h3 className="optical-compare__title">
            <GitCompare size={16} aria-hidden="true" /> Comparer
          </h3>
          <div className="optical-compare__pickers">
            <RevisionSelect label="Version de gauche" value={leftId} onChange={setLeftId} revisions={comparable} />
            <RevisionSelect label="Version de droite" value={rightId} onChange={setRightId} revisions={comparable} />
            <label className="optical-check">
              <input type="checkbox" checked={onlyDifferent} onChange={(event) => setOnlyDifferent(event.target.checked)} />
              <span>Champs différents seulement</span>
            </label>
          </div>
          {left && right && (
            <>
              <p role="status" className="clients-muted">
                {different.length === 0 ? "Aucune différence entre ces deux versions." : `${different.length} champ${different.length > 1 ? "s" : ""} différent${different.length > 1 ? "s" : ""}.`}
              </p>
              {shown.length > 0 && (
                <div className="clients-panel__scroll">
                  <table className="clients-table optical-compare__table">
                    <caption className="visually-hidden">
                      Comparaison : {revisionLabel(left)} et {revisionLabel(right)}
                    </caption>
                    <thead>
                      <tr>
                        <th scope="col">Champ</th>
                        <th scope="col">{revisionLabel(left)}</th>
                        <th scope="col">{revisionLabel(right)}</th>
                        <th scope="col">Écart</th>
                      </tr>
                    </thead>
                    <tbody>
                      {shown.map((row) => {
                        const changed = row.left !== row.right;
                        return (
                          <tr key={row.key} className={cn(changed && "optical-changed")}>
                            <th scope="row">{row.label}</th>
                            <td className="tnum">{row.left || <span className="clients-muted" aria-label="Non renseigné">—</span>}</td>
                            <td className="tnum">{row.right || <span className="clients-muted" aria-label="Non renseigné">—</span>}</td>
                            <td>{changed ? <b>Différent</b> : <span className="clients-muted">Identique</span>}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </div>
      ) : (
        <p className="clients-muted">Une seule version existe : aucune comparaison possible.</p>
      )}
    </section>
  );
}

function RevisionSelect({ label, value, onChange, revisions }: { label: string; value: string; onChange: (id: string) => void; revisions: readonly OpticalRevision[] }) {
  const id = useId();
  return (
    <div className="clients-field">
      <label className="clients-field__label" htmlFor={id}>
        {label}
      </label>
      <div className="clients-input">
        <select id={id} className="optical-select" value={value} onChange={(event) => onChange(event.target.value)}>
          {revisions.map((revision) => (
            <option key={revision.id} value={revision.id}>
              {revisionLabel(revision)}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}
