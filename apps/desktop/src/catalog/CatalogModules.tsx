// Modules réels de la Phase 05, montés par le Shell seulement si l'interrupteur VITE_MMV_CATALOG_MODULE est activé et si
// la session détient catalog.read ou stock.read (catalogAccess.ts) :
// - « Produits » (/produits) : catalogue, fournisseurs, référentiel des verres, import CSV ;
// - « Inventaire » (/inventaire) : stock par emplacement, mouvements, réceptions, inventaires.
// Le Lot 6 range Produits, Inventaire et Fournisseurs dans la barre latérale ; le shell commun des Phases 02–04 n'est
// pas modifié : les sections de chaque module forment une commande segmentée (.seg du Lot 6), alignée avec le SEUL
// badge du magasin de l'INSTALLATION (COR-05-01 : un serveur = un magasin, aucun sélecteur ; devise EUR ou MAD).
import type { ReactNode } from "react";
import { NavLink, Route, Routes } from "react-router";
import { ForbiddenView } from "../views/ForbiddenView.tsx";
import { CatalogAccessContext, catalogAccess } from "./catalogAccess.ts";
import { CatalogListView } from "./CatalogListView.tsx";
import { RealtimeIndicator, StoreBadge, StoreProvider, ToastProvider } from "./CatalogUi.tsx";
import { ImportView } from "./ImportView.tsx";
import { InventoriesView, InventorySessionView } from "./InventoryViews.tsx";
import { LensRangeWizard } from "./LensRangeWizard.tsx";
import { LensReferencesView } from "./LensReferencesView.tsx";
import { ModelDetailView, ModelEditView } from "./ModelViews.tsx";
import { ProductWizardView } from "./ProductWizard.tsx";
import { BalancesView, MovementsView, ReceiptsView } from "./StockViews.tsx";
import { SupplierCreateView, SupplierDetailView, SupplierEditView, SuppliersListView } from "./SupplierViews.tsx";
import "../clients/Clients.css";
import "./Catalog.css";

function ModuleFrame({ sections, children }: { sections: { to: string; label: string; end?: boolean }[]; children: ReactNode }) {
  return (
    <ToastProvider>
      <StoreProvider>
        <div className="cat-module">
          <div className="cat-context">
            <nav className="cat-seg" aria-label="Sections">
              {sections.map((item) => (
                <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => `cat-seg__item${isActive ? " is-active" : ""}`}>
                  {item.label}
                </NavLink>
              ))}
            </nav>
            <RealtimeIndicator compact />
            <StoreBadge />
          </div>
          {children}
        </div>
      </StoreProvider>
    </ToastProvider>
  );
}

export function ProductsModule({ permissions }: { permissions: readonly string[] }) {
  const access = catalogAccess(permissions);
  if (!access.catalogRead) return <ForbiddenView reason="Le catalogue exige le droit catalog.read." />;
  return (
    <CatalogAccessContext.Provider value={access}>
      <ModuleFrame sections={[
        { to: "/produits", label: "Catalogue", end: true },
        ...(access.suppliersRead ? [{ to: "/produits/fournisseurs", label: "Fournisseurs" }] : []),
        { to: "/produits/verres", label: "Catalogue des verres" },
        ...(access.catalogImport ? [{ to: "/produits/import", label: "Import CSV" }] : []),
      ]}>
        <Routes>
          <Route index element={<CatalogListView />} />
          <Route path="nouveau" element={access.catalogWrite ? <ProductWizardView /> : <ForbiddenView reason="Création réservée au droit catalog.write." />} />
          <Route path="modele/:id" element={<ModelDetailView />} />
          <Route path="modele/:id/modifier" element={access.catalogWrite ? <ModelEditView /> : <ForbiddenView reason="Modification réservée au droit catalog.write." />} />
          <Route path="fournisseurs" element={access.suppliersRead ? <SuppliersListView /> : <ForbiddenView reason="Droit suppliers.read requis." />} />
          <Route path="fournisseurs/nouveau" element={access.suppliersWrite ? <SupplierCreateView /> : <ForbiddenView reason="Droit suppliers.write requis." />} />
          <Route path="fournisseurs/:id" element={access.suppliersRead ? <SupplierDetailView /> : <ForbiddenView reason="Droit suppliers.read requis." />} />
          <Route path="fournisseurs/:id/modifier" element={access.suppliersWrite ? <SupplierEditView /> : <ForbiddenView reason="Droit suppliers.write requis." />} />
          <Route path="verres" element={<LensReferencesView />} />
          <Route path="verres/nouvelle" element={access.catalogConfigure && access.suppliersRead ? <LensRangeWizard /> : <ForbiddenView reason="Droits catalog.configure et suppliers.read requis." />} />
          <Route path="import" element={access.catalogImport ? <ImportView /> : <ForbiddenView reason="Droit catalog.import requis." />} />
        </Routes>
      </ModuleFrame>
    </CatalogAccessContext.Provider>
  );
}

export function StockModule({ permissions }: { permissions: readonly string[] }) {
  const access = catalogAccess(permissions);
  if (!access.stockRead) return <ForbiddenView reason="Le stock exige le droit stock.read." />;
  return (
    <CatalogAccessContext.Provider value={access}>
      <ModuleFrame sections={[
        { to: "/inventaire", label: "Stock", end: true },
        { to: "/inventaire/mouvements", label: "Mouvements" },
        { to: "/inventaire/receptions", label: "Réceptions" },
        { to: "/inventaire/inventaires", label: "Inventaires" },
      ]}>
        <Routes>
          <Route index element={<BalancesView />} />
          <Route path="mouvements" element={<MovementsView />} />
          <Route path="receptions" element={<ReceiptsView />} />
          <Route path="inventaires" element={<InventoriesView />} />
          <Route path="inventaires/:id" element={<InventorySessionView />} />
        </Routes>
      </ModuleFrame>
    </CatalogAccessContext.Provider>
  );
}
