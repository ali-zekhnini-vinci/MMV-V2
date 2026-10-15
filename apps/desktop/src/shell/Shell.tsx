// Shell global (référence visuelle DRAFT : MMV-Shell-*.png, MMV-Shell-handoff.md, MMV-Shell-states.md).
// Ordre DOM = ordre de tabulation recommandé (états §7) : barre supérieure, sidebar, contenu ;
// la grille CSS replace visuellement la sidebar à gauche. La barre d'état n'est pas focalisable.
// Navigation : React Router, routeur MÉMOIRE (client de bureau Tauri sans barre d'adresse) — chaque
// ouverture de session démarre sur l'Accueil, aucune route n'est restaurée après fermeture/rechargement.
import { RefreshCw, ServerCrash } from "lucide-react";
import { useCallback, useState } from "react";
import { MemoryRouter, Navigate, Route, Routes, useLocation, useNavigate } from "react-router";
import type { SessionInfo } from "../api/auth.ts";
import { useAuth } from "../auth/authContext.ts";
import { Button } from "../components/ui/button.tsx";
import { AccountsView } from "../views/AccountsView.tsx";
import { DiagnosticView } from "../views/DiagnosticView.tsx";
import { ForbiddenView } from "../views/ForbiddenView.tsx";
import { HomeView } from "../views/HomeView.tsx";
import { UnavailableView } from "../views/UnavailableView.tsx";
import { findNavItem, findNavItemByPath, NAV_ITEMS, type NavItem, type ViewId, viewAccess, visibleSections } from "./navigation.ts";
import { Sidebar } from "./Sidebar.tsx";
import { StatusBar } from "./StatusBar.tsx";
import { useTheme } from "./theme.ts";
import { Topbar } from "./Topbar.tsx";
import { useServerHealth } from "./useServerHealth.ts";
import "./Shell.css";

/** En dessous de cette largeur (CSS px, zoom compris), la sidebar démarre réduite (handoff §10). */
export const COLLAPSE_BELOW_PX = 1280;

export function Shell({ session }: { session: SessionInfo }) {
  return (
    <MemoryRouter>
      <ShellLayout session={session} />
    </MemoryRouter>
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
  const sections = visibleSections(user.permissions);
  const current: ViewId = findNavItemByPath(location.pathname)?.id ?? "home";

  const navigate = useCallback(
    async (id: ViewId) => {
      routerNavigate(findNavItem(id).path);
      // L'écran Comptes interroge lui-même l'API protégée ; les autres revalident la session
      // (révocation ou désactivation détectée dès la navigation, pas seulement à l'expiration).
      if (id === "accounts") return;
      setVerifyingSession(true);
      await refreshSession();
      setVerifyingSession(false);
    },
    [refreshSession, routerNavigate],
  );

  const handleSignOut = useCallback(() => {
    void signOut();
  }, [signOut]);

  // Garde UI par route : elle choisit l'écran à monter, le serveur reste seul juge des droits.
  const renderView = (item: NavItem) => {
    const access = viewAccess(item.id, user.permissions);
    if (access === "forbidden") return <ForbiddenView reason="Votre compte ne dispose pas du droit requis pour cet écran." />;
    if (access === "later") return <UnavailableView label={item.label} />;
    if (item.id === "accounts") return <AccountsView />;
    if (item.id === "diagnostic") return <DiagnosticView baseUrl={baseUrl} health={health} />;
    return <HomeView user={user} />;
  };

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
        onNavigate={(id) => void navigate(id)}
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
        <Routes>
          {NAV_ITEMS.map((item) => (
            <Route key={item.id} path={item.path} element={renderView(item)} />
          ))}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      <StatusBar expiresAtUtc={session.expiresAtUtc} health={health.status} checking={health.checking} />
    </div>
  );
}
