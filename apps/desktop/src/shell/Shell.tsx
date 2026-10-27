// Shell global (référence visuelle DRAFT : MMV-Shell-*.png, MMV-Shell-handoff.md, MMV-Shell-states.md).
// Ordre DOM = ordre de tabulation recommandé (états §7) : barre supérieure, sidebar, contenu ;
// la grille CSS replace visuellement la sidebar à gauche. La barre d'état n'est pas focalisable.
// Navigation : React Router, routeur MÉMOIRE (client de bureau Tauri sans barre d'adresse) — chaque
// ouverture de session démarre sur l'Accueil, aucune route n'est restaurée après fermeture/rechargement.
import { QueryClientProvider } from "@tanstack/react-query";
import { RefreshCw, ServerCrash } from "lucide-react";
import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { MemoryRouter, Navigate, Route, Routes, useLocation, useNavigate } from "react-router";
import type { SessionInfo } from "../api/auth.ts";
import { createMmvQueryClient } from "../api/queryClient.ts";
import { useAuth } from "../auth/authContext.ts";
import { Button } from "../components/ui/button.tsx";
import { AccountsView } from "../views/AccountsView.tsx";
import { DiagnosticView } from "../views/DiagnosticView.tsx";
import { ForbiddenView } from "../views/ForbiddenView.tsx";
import { HomeView } from "../views/HomeView.tsx";
import { UnavailableView } from "../views/UnavailableView.tsx";
import { type LeaveGuard, NavigationGuardContext, type NavigationGuardRegistry } from "./navigationGuard.ts";
import { isOpticalModuleActive, OpticalActivationContext } from "../optical/opticalActivation.ts";
import { OpticalHost } from "../optical/OpticalHost.tsx";
import { findNavItem, findNavItemByPath, type NavItem, navItems, routePattern, type ViewId, viewAccess, visibleSections } from "./navigation.ts";
import { Sidebar } from "./Sidebar.tsx";
import { StatusBar } from "./StatusBar.tsx";
import { useTheme } from "./theme.ts";
import { Topbar } from "./Topbar.tsx";
import { useServerHealth } from "./useServerHealth.ts";
import "./Shell.css";

// Module Clients chargé à la demande (bundle séparé : TanStack Table, dialogues, onglets), O-02-13.
const ClientsModule = lazy(() => import("../clients/ClientsModule.tsx").then((module) => ({ default: module.ClientsModule })));
// Module Rendez-vous (03-B) chargé à la demande : FullCalendar et Temporal restent hors du bundle initial.
const AppointmentsModule = lazy(() => import("../appointments/AppointmentsModule.tsx").then((module) => ({ default: module.AppointmentsModule })));
// Dossier optique (04-B) : écrans chargés à la demande, montés seulement s'il est activé pour la session.
const OpticalRoute = lazy(() => import("../optical/OpticalRoute.tsx").then((module) => ({ default: module.OpticalRoute })));

/** En dessous de cette largeur (CSS px, zoom compris), la sidebar démarre réduite (handoff §10). */
export const COLLAPSE_BELOW_PX = 1280;

export function Shell({ session }: { session: SessionInfo }) {
  // Cache TanStack Query propre à la session : créé à l'ouverture du Shell, vidé à sa fermeture
  // (déconnexion, expiration) — aucune donnée serveur ne passe d'une session à l'autre ni sur disque.
  const [queryClient] = useState(createMmvQueryClient);
  useEffect(() => () => queryClient.clear(), [queryClient]);
  return (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ShellLayout session={session} />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

function ShellLayout({ session }: { session: SessionInfo }) {
  const { baseUrl, signOut, refreshSession } = useAuth();
  const [theme, toggleTheme] = useTheme();
  const health = useServerHealth(baseUrl);
  const location = useLocation();
  const routerNavigate = useNavigate();
  const [collapsed, setCollapsed] = useState(() => window.innerWidth < COLLAPSE_BELOW_PX);
  const [verifyingSession, setVerifyingSession] = useState(false);

  const { user } = session;
  // Dossier optique réel : interrupteur de build ET droits de la session ; sinon entrée « Ordonnances » reportée.
  const opticalOn = isOpticalModuleActive(user.permissions);
  const sections = visibleSections(user.permissions, opticalOn);
  const current: ViewId = findNavItemByPath(location.pathname, opticalOn)?.id ?? "home";

  // Garde de sortie (formulaire modifié) : consultée avant tout changement d'entrée de menu.
  const leaveGuard = useRef<LeaveGuard | null>(null);
  const guardRegistry = useMemo<NavigationGuardRegistry>(
    () => ({
      register: (guard) => {
        leaveGuard.current = guard;
        return () => {
          if (leaveGuard.current === guard) leaveGuard.current = null;
        };
      },
    }),
    [],
  );

  const goTo = useCallback(
    async (id: ViewId) => {
      routerNavigate(findNavItem(id, opticalOn).path);
      // L'écran Comptes interroge lui-même l'API protégée ; les autres revalident la session
      // (révocation ou désactivation détectée dès la navigation, pas seulement à l'expiration).
      if (id === "accounts") return;
      setVerifyingSession(true);
      await refreshSession();
      setVerifyingSession(false);
    },
    [opticalOn, refreshSession, routerNavigate],
  );

  const navigate = useCallback(
    (id: ViewId) => {
      const guard = leaveGuard.current;
      // La garde affiche sa confirmation et rappellera `goTo` seulement si l'utilisateur confirme.
      if (guard !== null && guard(() => void goTo(id))) return;
      void goTo(id);
    },
    [goTo],
  );

  const handleSignOut = useCallback(() => {
    void signOut();
  }, [signOut]);

  // Garde UI par route : elle choisit l'écran à monter, le serveur reste seul juge des droits.
  const renderView = (item: NavItem) => {
    const access = viewAccess(item.id, user.permissions, opticalOn);
    if (access === "forbidden") return <ForbiddenView reason="Votre compte ne dispose pas du droit requis pour cet écran." />;
    if (access === "later") return <UnavailableView label={item.label} note={item.note} />;
    if (item.id === "clients") {
      return (
        <Suspense fallback={<p role="status" className="view__muted">Chargement du module Clients…</p>}>
          <ClientsModule />
        </Suspense>
      );
    }
    if (item.id === "appointments") {
      return (
        <Suspense fallback={<p role="status" className="view__muted">Chargement du module Rendez-vous…</p>}>
          <AppointmentsModule />
        </Suspense>
      );
    }
    if (item.id === "prescriptions" && opticalOn) {
      return (
        <Suspense fallback={<p role="status" className="view__muted">Chargement du dossier optique…</p>}>
          <OpticalRoute />
        </Suspense>
      );
    }
    if (item.id === "accounts") return <AccountsView />;
    if (item.id === "diagnostic") return <DiagnosticView baseUrl={baseUrl} health={health} />;
    return <HomeView user={user} />;
  };

  const routes = (
    <Routes>
      {navItems(opticalOn).map((item) => (
        <Route key={item.id} path={routePattern(item)} element={renderView(item)} />
      ))}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );

  const serverDown = health.status?.state === "api-unreachable";

  return (
    <div className={`shell${collapsed ? " shell--collapsed" : ""}`}>
      <Topbar theme={theme} user={user} onToggleTheme={toggleTheme} onSignOut={handleSignOut} />
      <Sidebar
        sections={sections}
        current={current}
        collapsed={collapsed}
        theme={theme}
        user={user}
        onNavigate={navigate}
        onToggleCollapsed={() => setCollapsed((value) => !value)}
        onSignOut={handleSignOut}
      />
      <main className="shell__content" aria-busy={verifyingSession}>
        {verifyingSession && <div className="shell__progress" role="progressbar" aria-label="Vérification de la session" />}
        {serverDown && (
          <div className="shell__banner" role="alert">
            <ServerCrash size={18} aria-hidden="true" />
            <span>Serveur MMV inaccessible. Les actions nécessitant le serveur sont indisponibles.</span>
            <Button size="sm" onClick={() => void health.reload()} disabled={health.checking}>
              <RefreshCw size={14} aria-hidden="true" />
              {health.checking ? "Nouvelle tentative…" : "Réessayer"}
            </Button>
          </div>
        )}
        <NavigationGuardContext.Provider value={guardRegistry}>
          <OpticalActivationContext.Provider value={opticalOn}>
            {opticalOn ? <OpticalHost permissions={user.permissions}>{routes}</OpticalHost> : routes}
          </OpticalActivationContext.Provider>
        </NavigationGuardContext.Provider>
      </main>
      <StatusBar expiresAtUtc={session.expiresAtUtc} health={health.status} checking={health.checking} />
    </div>
  );
}
