// Modules réels de la Phase 05, montés par le Shell seulement si l'interrupteur VITE_MMV_CATALOG_MODULE est activé et si
// la session détient catalog.read ou stock.read (catalogAccess.ts) :
// - « Produits » (/produits) : catalogue, fournisseurs, référentiel des verres, import CSV ;
// - « Inventaire » (/inventaire) : stock par emplacement, mouvements, réceptions, inventaires.
import { NavLink, Route, Routes } from "react-router";
import { ForbiddenView } from "../views/ForbiddenView.tsx";
import { CatalogAccessContext, catalogAccess } from "./catalogAccess.ts";
import { CatalogListView } from "./CatalogListView.tsx";
import { StoreProvider } from "./CatalogUi.tsx";
import { ImportView } from "./ImportView.tsx";
import { InventoriesView, InventorySessionView } from "./InventoryViews.tsx";
import { LensReferencesView } from "./LensReferencesView.tsx";
import { ModelCreateView, ModelDetailView } from "./ModelViews.tsx";
import { BalancesView, MovementsView, ReceiptsView } from "./StockViews.tsx";
import { SupplierCreateView, SupplierDetailView, SuppliersListView } from "./SupplierViews.tsx";
import "./Catalog.css";

function SubNav({ items }: { items: { to: string; label: string; end?: boolean }[] }) {
  return (
    <nav className="cat-subnav" aria-label="Sections">
      {items.map((item) => (
        <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => `cat-subnav__link${isActive ? " is-active" : ""}`}>
          {item.label}
        </NavLink>
      ))}
    </nav>
  );
}

export function ProductsModule({ permissions }: { permissions: readonly string[] }) {
  const access = catalogAccess(permissions);
  if (!access.catalogRead) return <ForbiddenView reason="Le catalogue exige le droit catalog.read." />;
  return (
    <CatalogAccessContext.Provider value={access}>
      <StoreProvider>
        <div className="cat-module">
          <SubNav items={[
            { to: "/produits", label: "Catalogue", end: true },
            ...(access.suppliersRead ? [{ to: "/produits/fournisseurs", label: "Fournisseurs" }] : []),
            { to: "/produits/verres", label: "Référentiel verres" },
            ...(access.catalogImport ? [{ to: "/produits/import", label: "Import CSV" }] : []),
          ]} />
          <Routes>
            <Route index element={<CatalogListView />} />
            <Route path="nouveau" element={access.catalogWrite ? <ModelCreateView /> : <ForbiddenView reason="Création réservée au droit catalog.write." />} />
            <Route path="modele/:id" element={<ModelDetailView />} />
            <Route path="fournisseurs" element={access.suppliersRead ? <SuppliersListView /> : <ForbiddenView reason="Droit suppliers.read requis." />} />
            <Route path="fournisseurs/nouveau" element={access.suppliersWrite ? <SupplierCreateView /> : <ForbiddenView reason="Droit suppliers.write requis." />} />
            <Route path="fournisseurs/:id" element={access.suppliersRead ? <SupplierDetailView /> : <ForbiddenView reason="Droit suppliers.read requis." />} />
            <Route path="verres" element={<LensReferencesView />} />
            <Route path="import" element={access.catalogImport ? <ImportView /> : <ForbiddenView reason="Droit catalog.import requis." />} />
          </Routes>
        </div>
      </StoreProvider>
    </CatalogAccessContext.Provider>
  );
}

export function StockModule({ permissions }: { permissions: readonly string[] }) {
  const access = catalogAccess(permissions);
  if (!access.stockRead) return <ForbiddenView reason="Le stock exige le droit stock.read." />;
  return (
    <CatalogAccessContext.Provider value={access}>
      <StoreProvider>
        <div className="cat-module">
          <SubNav items={[
            { to: "/inventaire", label: "Stock", end: true },
            { to: "/inventaire/mouvements", label: "Mouvements" },
            { to: "/inventaire/receptions", label: "Réceptions" },
            { to: "/inventaire/inventaires", label: "Inventaires" },
          ]} />
          <Routes>
            <Route index element={<BalancesView />} />
            <Route path="mouvements" element={<MovementsView />} />
            <Route path="receptions" element={<ReceiptsView />} />
            <Route path="inventaires" element={<InventoriesView />} />
            <Route path="inventaires/:id" element={<InventorySessionView />} />
          </Routes>
        </div>
      </StoreProvider>
    </CatalogAccessContext.Provider>
  );
}
