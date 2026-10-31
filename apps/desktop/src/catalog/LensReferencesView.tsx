// Référentiel configurable des verres correcteurs (Q03, D-05-21) — écran V2 absent du Lot 6, dessiné avec ses
// composants (cartes .card à en-tête, table.dt, badges, ligne d'ajout, états) : fabricants, gammes, indices,
// traitements. Aucune valeur pré-remplie ; saisie par un compte détenant catalog.configure ; archivage, jamais de
// suppression.
import { Archive, Factory, Gem, Layers, type LucideIcon, Plus, RotateCcw, Sigma, Sparkles } from "lucide-react";
import { useState } from "react";
import type { LensReference, LensReferences } from "../api/catalog.ts";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import { Badge, Card, ErrorBanner, ErrorState, Field, Loading, PageHeader, SelectField } from "./CatalogUi.tsx";
import { useCatalogQuery, useCatalogWrite, useToast } from "./catalogData.ts";

const SECTIONS: { key: keyof LensReferences; segment: string; title: string; icon: LucideIcon; empty: string }[] = [
  { key: "manufacturers", segment: "manufacturers", title: "Fabricants", icon: Factory, empty: "Aucun fabricant configuré." },
  { key: "ranges", segment: "ranges", title: "Gammes", icon: Layers, empty: "Aucune gamme : une gamme est un modèle de la famille « Verres correcteurs »." },
  { key: "indices", segment: "indices", title: "Indices", icon: Sigma, empty: "Aucun indice configuré." },
  { key: "treatments", segment: "treatments", title: "Traitements", icon: Sparkles, empty: "Aucun traitement configuré." },
];

export function LensReferencesView() {
  const access = useCatalogAccess();
  const data = useCatalogQuery<LensReferences>("/api/catalog/lens-references");
  return (
    <section className="cat-screen" aria-labelledby="lens-title">
      <PageHeader title="Référentiel des verres" subtitle="Configurable, aucune valeur pré-remplie — une variante de verre = gamme + indice + traitements"
        titleExtra={!access.catalogConfigure && <Badge tone="arch">Lecture seule</Badge>} />
      {data.isPending ? <Loading label="Chargement du référentiel des verres…" /> : data.isError ? (
        <ErrorState error={data.error} title="Impossible de charger le référentiel" onRetry={() => void data.refetch()} />
      ) : (
        <div className="cat-refgrid">
          {SECTIONS.map((section) => (
            <LensSection key={section.key} section={section} items={data.data[section.key]} manufacturers={data.data.manufacturers} canConfigure={access.catalogConfigure} />
          ))}
        </div>
      )}
    </section>
  );
}

function LensSection({ section, items, manufacturers, canConfigure }: { section: (typeof SECTIONS)[number]; items: LensReference[]; manufacturers: LensReference[]; canConfigure: boolean }) {
  const toast = useToast();
  const write = useCatalogWrite();
  const [name, setName] = useState("");
  const [indexValue, setIndexValue] = useState("");
  const [manufacturerId, setManufacturerId] = useState("");
  const { segment } = section;
  const isIndex = segment === "indices";
  const needsParent = segment === "ranges" || segment === "treatments";
  const manufacturerName = (id: string | null) => manufacturers.find((m) => m.id === id)?.name;
  const active = items.filter((i) => i.status === "active").length;
  return (
    <Card title={section.title} icon={section.icon} flush actions={<Badge tone="accent" icon={Gem}>{active} actif{active > 1 ? "s" : ""}</Badge>}
      foot={canConfigure && (
        <div className="cat-addrow" style={{ width: "100%" }}>
          {needsParent && (
            <SelectField label={segment === "ranges" ? "Fabricant" : "Fabricant (facultatif)"} name={`${segment}-manufacturer`} value={manufacturerId} onChange={setManufacturerId} required={segment === "ranges"}
              options={[{ value: "", label: segment === "ranges" ? "— Choisir —" : "Générique" }, ...manufacturers.filter((m) => m.status === "active").map((m) => ({ value: m.id, label: m.name }))]} />
          )}
          {isIndex && <Field label="Valeur (ex. 1.600)" name="indexValue" value={indexValue} onChange={setIndexValue} inputMode="decimal" errors={write.error?.error.fieldErrors} />}
          <Field label={isIndex ? "Libellé (facultatif)" : "Nom"} name={`${segment}-name`} errorKey="name" value={name} onChange={setName} errors={write.error?.error.fieldErrors} required={!isIndex} />
          <Button disabled={write.isPending} onClick={() => write.mutate(
            { method: "POST", path: `/api/catalog/lens-references/${segment}`, body: { name: name.trim() || null, manufacturerId: manufacturerId || null, indexValue: isIndex ? indexValue.trim().replace(",", ".") : null } },
            { onSuccess: () => { toast.show(`${section.title} : élément ajouté`, name.trim() || indexValue.trim()); setName(""); setIndexValue(""); } },
          )}><Plus size={16} aria-hidden="true" />Ajouter</Button>
        </div>
      )}>
      {write.error && <div className="cat-card__body"><ErrorBanner error={write.error} /></div>}
      {items.length === 0 ? (
        <p className="cat-empty-mini clients-muted">{section.empty}</p>
      ) : (
        <table className="clients-table cat-table cat-table--inner cat-table--compact">
          <thead><tr><th scope="col">Nom</th>{needsParent && <th scope="col">Fabricant</th>}<th scope="col">Statut</th>{canConfigure && <th scope="col"><span className="sr-only">Actions</span></th>}</tr></thead>
          <tbody>{items.map((item) => (
            <tr key={item.id} className={item.status === "archived" ? "is-archived" : undefined}>
              <td className="cat-strong">{item.name}{item.indexValue && item.indexValue !== item.name ? <span className="cat-sub"> · {item.indexValue}</span> : null}</td>
              {needsParent && <td className="cat-secondary">{manufacturerName(item.manufacturerId) ?? "Générique"}</td>}
              <td>{item.status === "active" ? <Badge tone="ok">Actif</Badge> : <Badge tone="arch" icon={Archive}>Archivé</Badge>}</td>
              {canConfigure && (
                <td>
                  <div className="clients-rowacts">
                    <Button size="sm" variant="tertiary" disabled={write.isPending}
                      onClick={() => write.mutate({ method: "POST", path: `/api/catalog/lens-references/${segment}/${item.id}/${item.status === "active" ? "archive" : "reactivate"}`, body: { version: item.version } })}>
                      {item.status === "active" ? <><Archive size={14} aria-hidden="true" />Archiver</> : <><RotateCcw size={14} aria-hidden="true" />Réactiver</>}
                    </Button>
                  </div>
                </td>
              )}
            </tr>
          ))}</tbody>
        </table>
      )}
    </Card>
  );
}
