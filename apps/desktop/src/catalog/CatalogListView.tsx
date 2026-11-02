// Liste du catalogue — Lot 6 groupes 01–14 (catLight/catDark, recherche, filtres, tri, liste compacte, vide, aucun
// résultat, chargement, erreur) au niveau VARIANTE pour le magasin actif : prix de vente HT dans la devise du magasin,
// disponibilité calculée par le serveur, colonnes de coût seulement si le serveur les a servies (costs.read).
// Adaptations V2 : six familles (au lieu des catégories figées), politique de stock par variante, fournisseur principal
// (plusieurs fournisseurs possibles), coefficient, statut archivé (pas de suppression). Export CSV du Lot 6 = cible non
// livrée : à sa place, l'import CSV contrôlé (05-D) pour les comptes autorisés.
import { Box, CircleCheck, FileUp, Layers, Plus, RotateCcw, Rows3, SearchX, Tag } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { type Family, type Page, query, type VariantRow } from "../api/catalog.ts";
import { Highlight } from "../clients/highlight.tsx";
import { Button } from "../components/ui/button.tsx";
import { useCatalogAccess } from "./catalogAccess.ts";
import { AVAILABILITY_LABELS, formatMoney, POLICY_LABELS, PRICE_STATUS_LABELS } from "./catalogFormat.ts";
import { availabilityBadge, stockTone } from "./catalogStatus.ts";
import {
  Badge, EmptyState, ErrorState, FamilyTag, FilterChip, Loading, PageHeader, RowChevron, SearchBox, SortChip, SortHeader, StockCell, TableFoot, TablePanel,
  ToggleChip, Toolbar, ToolbarSpacer,
} from "./CatalogUi.tsx";
import { rowLink, useActiveStore, useCatalogQuery } from "./catalogData.ts";
import { type SupplierChoice, SupplierCombobox } from "./SupplierCombobox.tsx";

interface Filters {
  q: string;
  familyCode: string;
  supplierId: string;
  policy: string;
  status: string;
  availability: string;
  sort: string;
  dir: string;
  page: number;
}

const INITIAL: Filters = { q: "", familyCode: "", supplierId: "", policy: "", status: "active", availability: "", sort: "name", dir: "asc", page: 1 };

const SORTS = [
  { value: "name:asc", label: "Nom (A → Z)" },
  { value: "sku:asc", label: "Référence" },
  { value: "family:asc", label: "Famille" },
  { value: "price:desc", label: "Prix de vente ↓" },
  { value: "price:asc", label: "Prix de vente ↑" },
  { value: "stock:asc", label: "Stock disponible ↑" },
  { value: "updated:desc", label: "Dernières modifications" },
];

export function CatalogListView() {
  const access = useCatalogAccess();
  const navigate = useNavigate();
  const { store } = useActiveStore();
  const [filters, setFilters] = useState<Filters>(INITIAL);
  const [compact, setCompact] = useState(false);
  const families = useCatalogQuery<Family[]>("/api/catalog/families");
  // Fournisseur filtré : combobox serveur (COR-05-08) au lieu d'un menu de 200 noms ; seul l'identifiant part en filtre.
  const [supplier, setSupplier] = useState<SupplierChoice | null>(null);
  const list = useCatalogQuery<Page<VariantRow>>(
    store ? `/api/catalog/variants${query({ storeId: store.id, ...filters, pageSize: 50 })}` : null, [], { keepPrevious: true },
  );
  const set = (patch: Partial<Filters>) => setFilters((current) => ({ ...current, page: 1, ...patch }));
  const sortBy = (sort: string) => set({ sort, dir: filters.sort === sort && filters.dir === "asc" ? "desc" : "asc" });
  const familyList = Array.isArray(families.data) ? families.data : [];
  const costs = !compact && (list.data?.items.some((item) => item.price.costsVisible) ?? access.costsRead);
  const showSupplier = access.suppliersRead && !compact;
  const filtered = filters.q !== "" || filters.familyCode !== "" || filters.supplierId !== "" || filters.policy !== "" || filters.availability !== "" || filters.status !== "active";
  const open = (href: string) => navigate(href);

  return (
    <section className="cat-screen" aria-labelledby="catalog-title">
      <PageHeader
        title="Produits"
        actions={
          <>
            {access.catalogImport && (
              <Button onClick={() => navigate("/produits/import")}>
                <FileUp size={16} aria-hidden="true" />
                Importer un CSV
              </Button>
            )}
            {access.catalogWrite && (
              <Button variant="primary" onClick={() => navigate("/produits/nouveau")}>
                <Plus size={18} aria-hidden="true" />
                Nouveau produit
              </Button>
            )}
          </>
        }
      />
      <Toolbar>
        <SearchBox value={filters.q} onSearch={(q) => set({ q })} busy={list.isPlaceholderData} placeholder="Rechercher par nom, marque, modèle, coloris, SKU, référence ou code-barres…" label="Rechercher dans le catalogue" />
        <FilterChip icon={Tag} label="Famille" value={filters.familyCode} onChange={(familyCode) => set({ familyCode })}
          options={[{ value: "", label: "Toutes les familles" }, ...familyList.map((f) => ({ value: f.code, label: f.label }))]} />
        {access.suppliersRead && (
          <SupplierCombobox compact label="Filtrer par fournisseur" placeholder="Fournisseur…" allowCreate={false} value={supplier}
            onChange={(choice) => { setSupplier(choice); set({ supplierId: choice?.id ?? "" }); }} />
        )}
        <FilterChip icon={CircleCheck} label="Disponibilité" value={filters.availability} onChange={(availability) => set({ availability })}
          options={[{ value: "", label: "Toutes" }, ...Object.entries(AVAILABILITY_LABELS).filter(([code]) => code !== "archived").map(([value, label]) => ({ value, label }))]} />
        <FilterChip icon={Layers} label="Politique" value={filters.policy} onChange={(policy) => set({ policy })}
          options={[{ value: "", label: "Toutes les politiques" }, ...Object.entries(POLICY_LABELS).map(([value, label]) => ({ value, label }))]} />
        <ToolbarSpacer />
        <ToggleChip icon={Rows3} label="Compact" on={compact} onToggle={() => setCompact(!compact)} />
        <FilterChip icon={Box} label="Statut" value={filters.status} defaultValue="active" onChange={(status) => set({ status })}
          options={[{ value: "active", label: "Actifs" }, { value: "archived", label: "Archivés" }, { value: "all", label: "Tous" }]} />
        <SortChip value={`${filters.sort}:${filters.dir}`} options={SORTS} onChange={(value) => { const [sort, dir] = value.split(":"); set({ sort, dir }); }} />
      </Toolbar>

      {list.isPending ? (
        <Loading label="Chargement du catalogue…" />
      ) : list.isError ? (
        <ErrorState error={list.error} title="Impossible de charger le catalogue" onRetry={() => void list.refetch()} />
      ) : list.data.totalCount === 0 ? (
        filtered ? (
          <EmptyState tone="info" icon={SearchX} title="Aucun résultat"
            action={<Button onClick={() => { setFilters(INITIAL); setSupplier(null); }}><RotateCcw size={16} aria-hidden="true" />Réinitialiser les filtres</Button>}>
            {filters.q ? `Aucun produit ne correspond à « ${filters.q} ». Vérifiez l'orthographe ou réinitialisez les filtres.` : "Aucun produit ne correspond à ces filtres."}
          </EmptyState>
        ) : (
          <EmptyState icon={Box} title="Aucun produit dans le catalogue"
            action={access.catalogWrite && <Button variant="primary" onClick={() => navigate("/produits/nouveau")}><Plus size={18} aria-hidden="true" />Nouveau produit</Button>}>
            Commencez par ajouter votre premier produit (modèle et variantes) pour construire le catalogue.
          </EmptyState>
        )
      ) : (
        <TablePanel fill
          foot={
            <TableFoot icon={Box} page={list.data.page} pageSize={list.data.pageSize} total={list.data.totalCount} onPage={(page) => setFilters((c) => ({ ...c, page }))}
              right={store && <span className="clients-muted">Prix HT · {store.currency} · aucun montant converti</span>}>
              {list.data.totalCount} variante{list.data.totalCount > 1 ? "s" : ""} · {SORTS.find((s) => s.value === `${filters.sort}:${filters.dir}`)?.label.toLowerCase() ?? "tri par nom"}{compact ? " · affichage compact" : ""}
            </TableFoot>
          }
        >
          <table className={compact ? "clients-table cat-table cat-table--compact" : "clients-table cat-table"} aria-busy={list.isPlaceholderData}>
            <thead>
              <tr>
                <SortHeader label="Référence" sort="sku" current={filters.sort} dir={filters.dir} onSort={sortBy} />
                <SortHeader label="Nom" sort="name" current={filters.sort} dir={filters.dir} onSort={sortBy} />
                {!compact && <SortHeader label="Famille" sort="family" current={filters.sort} dir={filters.dir} onSort={sortBy} className="cat-hide-sm" />}
                {showSupplier && <th scope="col" className="cat-hide-md">Fournisseur</th>}
                {costs && <th scope="col" className="cat-num cat-hide-sm">Prix d&apos;achat HT</th>}
                {costs && <th scope="col" className="cat-num cat-hide-sm">Coef.</th>}
                <SortHeader label="Prix de vente HT" sort="price" current={filters.sort} dir={filters.dir} onSort={sortBy} numeric />
                {access.stockRead && <SortHeader label="Stock" sort="stock" current={filters.sort} dir={filters.dir} onSort={sortBy} numeric />}
                <th scope="col">Statut</th>
                <th scope="col"><span className="sr-only">Ouvrir</span></th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((row) => {
                const badge = availabilityBadge(row.availability);
                return (
                  <tr key={row.variantId} className={row.status === "archived" ? "is-archived" : undefined} {...rowLink(`/produits/modele/${row.modelId}?variante=${row.variantId}`, open)}>
                    <td><span className="cat-ref"><Highlight text={row.sku} query={filters.q} /></span></td>
                    <td>
                      <div className="clients-cell__name"><Highlight text={row.modelName} query={filters.q} /></div>
                      {!compact && <div className="cat-sub"><Highlight text={[row.label, row.brand].filter(Boolean).join(" · ")} query={filters.q} /></div>}
                    </td>
                    {!compact && <td className="cat-hide-sm"><FamilyTag code={row.familyCode} /></td>}
                    {showSupplier && (
                      <td className="cat-hide-md">
                        {row.primarySupplier ? <span className="cat-secondary">{row.primarySupplier.name}</span> : row.isIncomplete ? <Badge tone="warn">Fiche incomplète</Badge> : <span className="clients-muted">—</span>}
                      </td>
                    )}
                    {costs && <td className="cat-num cat-secondary cat-hide-sm">{formatMoney(row.price.purchasePriceHt ?? null, row.price.currency)}</td>}
                    {costs && <td className="cat-num cat-secondary cat-hide-sm">{row.price.coefficient ? `× ${row.price.coefficient.replace(".", ",")}` : "—"}</td>}
                    <td className="cat-num">
                      <span className="cat-strong">{formatMoney(row.price.salePriceHt, row.price.currency)}</span>
                      {row.price.priceStatus !== "up_to_date" && <div className="cat-sub">{PRICE_STATUS_LABELS[row.price.priceStatus]}</div>}
                    </td>
                    {access.stockRead && (
                      <td className="cat-num">
                        {row.stock ? <StockCell value={row.stock.available} tone={stockTone(row.availability)} /> : <span className="clients-muted" title={POLICY_LABELS[row.stockPolicy]}>—</span>}
                      </td>
                    )}
                    <td><Badge tone={badge.tone} icon={badge.icon}>{AVAILABILITY_LABELS[row.availability]}</Badge></td>
                    <td><RowChevron /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </TablePanel>
      )}
    </section>
  );
}
