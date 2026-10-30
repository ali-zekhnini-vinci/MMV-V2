// Liste du catalogue (Lot 6 B-098 à B-101 : recherche, filtres, tri, états vide / chargement / erreur) au niveau
// variante pour le magasin actif : prix de vente HT dans la devise du magasin, disponibilité calculée par le serveur,
// colonnes de coût seulement si le serveur les a servies (costs.read).
import { Plus, SearchX } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { type Family, type Page, query, type VariantRow } from "../api/catalog.ts";
import { StateBlock } from "../clients/ClientStates.tsx";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import { AVAILABILITY_LABELS, AVAILABILITY_TONES, formatMoney, POLICY_LABELS, PRICE_STATUS_LABELS } from "./catalogFormat.ts";
import {
  Badge, ErrorState, Loading, PageHeader, Pager, StoreSelect,
} from "./CatalogUi.tsx";
import { useActiveStore, useCatalogQuery } from "./catalogData.ts";

interface Filters {
  q: string;
  familyCode: string;
  policy: string;
  status: string;
  availability: string;
  incomplete: string;
  sort: string;
  dir: string;
  page: number;
}

const INITIAL: Filters = { q: "", familyCode: "", policy: "", status: "active", availability: "", incomplete: "", sort: "name", dir: "asc", page: 1 };

export function CatalogListView() {
  const access = useCatalogAccess();
  const navigate = useNavigate();
  const { store } = useActiveStore();
  const [filters, setFilters] = useState<Filters>(INITIAL);
  const [search, setSearch] = useState("");
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  const list = useCatalogQuery<Page<VariantRow>>(
    store ? `/api/catalog/variants${query({ storeId: store.id, ...filters, pageSize: 50 })}` : null,
  );
  const set = (patch: Partial<Filters>) => setFilters((current) => ({ ...current, page: 1, ...patch }));
  const sortBy = (sort: string) => set({ sort, dir: filters.sort === sort && filters.dir === "asc" ? "desc" : "asc" });
  const familyLabel = (code: string) => (Array.isArray(families.data) ? families.data.find((f) => f.code === code)?.label : undefined) ?? code;
  const costs = list.data?.items.some((item) => item.price.costsVisible) ?? access.costsRead;

  return (
    <section aria-labelledby="catalog-title">
      <PageHeader
        title="Catalogue"
        subtitle={store ? `Prix et disponibilités du magasin ${store.name} (${store.currency}) — aucun montant converti.` : undefined}
        actions={
          <>
            <StoreSelect />
            {access.catalogWrite && (
              <Button variant="primary" onClick={() => navigate("/produits/nouveau")}>
                <Plus size={16} aria-hidden="true" />
                Nouveau produit
              </Button>
            )}
          </>
        }
      />
      <form
        className="cat-toolbar"
        role="search"
        onSubmit={(event) => {
          event.preventDefault();
          set({ q: search.trim() });
        }}
      >
        <input className="cat-input cat-toolbar__search" placeholder="SKU, désignation, marque, code-barres…" aria-label="Rechercher" value={search} onChange={(e) => setSearch(e.target.value)} />
        <select className="cat-input" aria-label="Famille" value={filters.familyCode} onChange={(e) => set({ familyCode: e.target.value })}>
          <option value="">Toutes les familles</option>
          {Array.isArray(families.data) && families.data.map((family) => (
            <option key={family.code} value={family.code}>
              {family.label}
            </option>
          ))}
        </select>
        <select className="cat-input" aria-label="Politique" value={filters.policy} onChange={(e) => set({ policy: e.target.value })}>
          <option value="">Toutes politiques</option>
          {Object.entries(POLICY_LABELS).map(([code, label]) => (
            <option key={code} value={code}>
              {label}
            </option>
          ))}
        </select>
        <select className="cat-input" aria-label="Disponibilité" value={filters.availability} onChange={(e) => set({ availability: e.target.value })}>
          <option value="">Toutes disponibilités</option>
          {Object.entries(AVAILABILITY_LABELS).filter(([code]) => code !== "archived").map(([code, label]) => (
            <option key={code} value={code}>
              {label}
            </option>
          ))}
        </select>
        <select className="cat-input" aria-label="Statut" value={filters.status} onChange={(e) => set({ status: e.target.value })}>
          <option value="active">Actifs</option>
          <option value="archived">Archivés</option>
          <option value="all">Tous</option>
        </select>
        <Button type="submit">Rechercher</Button>
      </form>

      {list.isPending ? (
        <Loading label="Chargement du catalogue…" />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.totalCount === 0 ? (
        <StateBlock tone="info" icon={SearchX} title={filters.q ? `Aucun résultat pour « ${filters.q} »` : "Aucun produit"}>
          <p>{filters.q || filters.familyCode || filters.policy || filters.availability ? "Élargissez la recherche ou les filtres." : "Le catalogue est vide pour ces critères."}</p>
        </StateBlock>
      ) : (
        <>
          <div className="cat-table-wrap">
            <table className="cat-table">
              <thead>
                <tr>
                  <SortHeader label="SKU" sort="sku" filters={filters} onSort={sortBy} />
                  <SortHeader label="Désignation" sort="name" filters={filters} onSort={sortBy} />
                  <SortHeader label="Famille" sort="family" filters={filters} onSort={sortBy} />
                  <th scope="col">Politique</th>
                  <SortHeader label="Prix de vente HT" sort="price" filters={filters} onSort={sortBy} numeric />
                  {costs && <th scope="col" className="cat-num">Prix d&apos;achat HT</th>}
                  {costs && <th scope="col" className="cat-num">Coef.</th>}
                  {access.stockRead && <SortHeader label="Disponible" sort="stock" filters={filters} onSort={sortBy} numeric />}
                  <th scope="col">Disponibilité</th>
                </tr>
              </thead>
              <tbody>
                {list.data.items.map((row) => (
                  <tr key={row.variantId} className="cat-row" tabIndex={0} onClick={() => navigate(`/produits/modele/${row.modelId}?variante=${row.variantId}`)}
                    onKeyDown={(e) => e.key === "Enter" && navigate(`/produits/modele/${row.modelId}?variante=${row.variantId}`)}>
                    <td className="cat-mono">{row.sku}</td>
                    <td>
                      <span className="cat-strong">{row.modelName}</span>
                      <span className="cat-muted"> · {row.label}{row.brand ? ` · ${row.brand}` : ""}</span>
                    </td>
                    <td>{familyLabel(row.familyCode)}</td>
                    <td>{POLICY_LABELS[row.stockPolicy]}</td>
                    <td className="cat-num">
                      {formatMoney(row.price.salePriceHt, row.price.currency)}
                      {row.price.priceStatus !== "up_to_date" && <div className="cat-muted">{PRICE_STATUS_LABELS[row.price.priceStatus]}</div>}
                    </td>
                    {costs && <td className="cat-num">{formatMoney(row.price.purchasePriceHt ?? null, row.price.currency)}</td>}
                    {costs && <td className="cat-num">{row.price.coefficient ?? "—"}</td>}
                    {access.stockRead && <td className="cat-num">{row.stock ? row.stock.available : "—"}</td>}
                    <td>
                      <Badge tone={AVAILABILITY_TONES[row.availability]}>{AVAILABILITY_LABELS[row.availability]}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={list.data.page} pageSize={list.data.pageSize} total={list.data.totalCount} onPage={(page) => setFilters((c) => ({ ...c, page }))} />
        </>
      )}
    </section>
  );
}

function SortHeader({ label, sort, filters, onSort, numeric }: { label: string; sort: string; filters: Filters; onSort: (sort: string) => void; numeric?: boolean }) {
  const active = filters.sort === sort;
  return (
    <th scope="col" className={numeric ? "cat-num" : undefined} aria-sort={active ? (filters.dir === "asc" ? "ascending" : "descending") : "none"}>
      <button type="button" className="cat-sort" onClick={() => onSort(sort)}>
        {label}
        {active && <span aria-hidden="true">{filters.dir === "asc" ? " ▲" : " ▼"}</span>}
      </button>
    </th>
  );
}
