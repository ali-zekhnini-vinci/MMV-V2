// Référentiel configurable des verres correcteurs (Q03, D-05-21) : fabricants, gammes, indices, traitements. Aucune
// valeur pré-remplie ; saisie par un compte détenant catalog.configure ; archivage, jamais de suppression.
import { useState } from "react";
import type { LensReference, LensReferences } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import {
  Badge, ErrorBanner, ErrorState, Field, Loading, PageHeader, Panel, SelectField,
} from "./CatalogUi.tsx";
import { useCatalogQuery, useCatalogWrite } from "./catalogData.ts";

const SECTIONS = [
  { key: "manufacturers", segment: "manufacturers", title: "Fabricants" },
  { key: "ranges", segment: "ranges", title: "Gammes (par fabricant)" },
  { key: "indices", segment: "indices", title: "Indices" },
  { key: "treatments", segment: "treatments", title: "Traitements" },
] as const;

export function LensReferencesView() {
  const access = useCatalogAccess();
  const data = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  if (data.isPending) return <Loading label="Chargement du référentiel des verres…" />;
  if (data.isError) return <ErrorState error={data.error} onRetry={() => void data.refetch()} />;
  return (
    <section>
      <PageHeader title="Référentiel des verres" subtitle="Configurable, aucune valeur pré-remplie. Une gamme est un modèle de la famille « Verres correcteurs » ; une variante = indice + traitements." />
      {SECTIONS.map((section) => (
        <LensSection key={section.key} title={section.title} segment={section.segment} items={data.data[section.key]} manufacturers={data.data.manufacturers} canConfigure={access.catalogConfigure} />
      ))}
    </section>
  );
}

function LensSection({ title, segment, items, manufacturers, canConfigure }: { title: string; segment: string; items: LensReference[]; manufacturers: LensReference[]; canConfigure: boolean }) {
  const write = useCatalogWrite();
  const [name, setName] = useState("");
  const [indexValue, setIndexValue] = useState("");
  const [manufacturerId, setManufacturerId] = useState("");
  const isIndex = segment === "indices";
  const needsParent = segment === "ranges" || segment === "treatments";
  const manufacturerName = (id: string | null) => manufacturers.find((m) => m.id === id)?.name;
  return (
    <Panel title={title}>
      <ErrorBanner error={write.error} />
      {items.length === 0 ? <p className="cat-muted">Aucun élément.</p> : (
        <ul className="cat-chips">
          {items.map((item) => (
            <li key={item.id} className="cat-chip">
              {item.name}{item.manufacturerId && <span className="cat-muted"> · {manufacturerName(item.manufacturerId)}</span>}
              {item.status === "archived" && <Badge tone="neutral">Archivé</Badge>}
              {canConfigure && (
                <Button size="sm" variant="tertiary" onClick={() => write.mutate({ method: "POST", path: `/api/catalog/lens-references/${segment}/${item.id}/${item.status === "active" ? "archive" : "reactivate"}`, body: { version: item.version } })}>
                  {item.status === "active" ? "Archiver" : "Réactiver"}
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      {canConfigure && (
        <div className="cat-inline">
          {needsParent && (
            <SelectField label={segment === "ranges" ? "Fabricant" : "Fabricant (facultatif)"} name={`${segment}-manufacturer`} value={manufacturerId} onChange={setManufacturerId}
              options={[{ value: "", label: segment === "ranges" ? "— Choisir —" : "Générique" }, ...manufacturers.filter((m) => m.status === "active").map((m) => ({ value: m.id, label: m.name }))]} />
          )}
          {isIndex && <Field label="Valeur (ex. 1.600)" name="indexValue" value={indexValue} onChange={setIndexValue} inputMode="decimal" errors={write.error?.error.fieldErrors} />}
          <Field label={isIndex ? "Libellé (facultatif)" : "Nom"} name={`${segment}-name`} errorKey="name" value={name} onChange={setName} errors={write.error?.error.fieldErrors} />
          <Button onClick={() => write.mutate(
            { method: "POST", path: `/api/catalog/lens-references/${segment}`, body: { name: name.trim() || null, manufacturerId: manufacturerId || null, indexValue: isIndex ? indexValue.trim().replace(",", ".") : null } },
            { onSuccess: () => { setName(""); setIndexValue(""); } },
          )}>Ajouter</Button>
        </div>
      )}
    </Panel>
  );
}
