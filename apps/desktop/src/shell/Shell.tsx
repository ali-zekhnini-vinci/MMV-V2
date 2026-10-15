// Shell global (référence visuelle DRAFT : MMV-Shell-*.png, MMV-Shell-handoff.md, MMV-Shell-states.md).
// Ordre DOM = ordre de tabulation recommandé (états §7) : barre supérieure, sidebar, contenu ;
// la grille CSS replace visuellement la sidebar à gauche. La barre d'état n'est pas focalisable.
import { RefreshCw, ServerCrash } from "lucide-react";
import { type ReactNode, useCallback, useState } from "react";
import type { SessionInfo } from "../api/auth.ts";
import { useAuth } from "../auth/authContext.ts";
import { AccountsView } from "../views/AccountsView.tsx";
import { DiagnosticView } from "../views/DiagnosticView.tsx";
import { ForbiddenView } from "../views/ForbiddenView.tsx";
import { HomeView } from "../views/HomeView.tsx";
import { UnavailableView } from "../views/UnavailableView.tsx";
import { findNavItem, type ViewId, viewAccess, visibleSections } from "./navigation.ts";
import { Sidebar, SIDEBAR_MENU_ID } from "./Sidebar.tsx";
import { StatusBar } from "./StatusBar.tsx";
import { useTheme } from "./theme.ts";
import { Topbar, TOPBAR_MENU_ID } from "./Topbar.tsx";
import { useServerHealth } from "./useServerHealth.ts";
import "./Shell.css";

/** En dessous de cette largeur (CSS px, zoom compris), la sidebar démarre réduite (handoff §10). */
export const COLLAPSE_BELOW_PX = 1280;

type MenuOwner = "topbar" | "sidebar" | null;

export function Shell({ session }: { session: SessionInfo }) {
  const { baseUrl, signOut, refreshSession } = useAuth();
  const [theme, toggleTheme] = useTheme();
  const health = useServerHealth(baseUrl);
  const [view, setView] = useState<ViewId>("home");
  const [collapsed, setCollapsed] = useState(() => window.innerWidth < COLLAPSE_BELOW_PX);
  const [menuOwner, setMenuOwner] = useState<MenuOwner>(null);
  const [verifyingSession, setVerifyingSession] = useState(false);

  const { user } = session;
  const sections = visibleSections(user.permissions);

  const navigate = useCallback(
    async (id: ViewId) => {
      setView(id);
      setMenuOwner(null);
      // L'écran Comptes interroge lui-même l'API protégée ; les autres revalident la session
      // (révocation ou désactivation détectée dès la navigation, pas seulement à l'expiration).
      if (id === "accounts") return;
      setVerifyingSession(true);
      await refreshSession();
      setVerifyingSession(false);
    },
    [refreshSession],
  );

  const closeMenu = useCallback((restoreFocus: boolean) => {
    setMenuOwner((owner) => {
      if (restoreFocus && owner !== null) {
        const anchorId = owner === "topbar" ? TOPBAR_MENU_ID : SIDEBAR_MENU_ID;
        window.setTimeout(() => document.querySelector<HTMLElement>(`[aria-controls='${anchorId}']`)?.focus(), 0);
      }
      return null;
    });
  }, []);

  const handleSignOut = useCallback(() => {
    setMenuOwner(null);
    void signOut();
  }, [signOut]);

  const access = viewAccess(view, user.permissions);
  let content: ReactNode;
  if (access === "forbidden") {
    content = <ForbiddenView reason="Votre compte ne dispose pas du droit requis pour cet écran." />;
  } else if (access === "later") {
    content = <UnavailableView label={findNavItem(view).label} />;
  } else if (view === "accounts") {
    content = <AccountsView />;
  } else if (view === "diagnostic") {
    content = <DiagnosticView baseUrl={baseUrl} health={health} />;
  } else {
    content = <HomeView user={user} />;
  }

  const serverDown = health.status?.state === "api-unreachable";

  return (
    <div className={`shell${collapsed ? " shell--collapsed" : ""}`}>
      <Topbar
        theme={theme}
        user={user}
        profileMenuOpen={menuOwner === "topbar"}
        onToggleTheme={toggleTheme}
        onToggleProfileMenu={() => setMenuOwner((owner) => (owner === "topbar" ? null : "topbar"))}
        onCloseProfileMenu={closeMenu}
        onSignOut={handleSignOut}
      />
      <Sidebar
        sections={sections}
        current={view}
        collapsed={collapsed}
        theme={theme}
        user={user}
        profileMenuOpen={menuOwner === "sidebar"}
        onNavigate={(id) => void navigate(id)}
        onToggleCollapsed={() => {
          setMenuOwner(null);
          setCollapsed((value) => !value);
        }}
        onToggleProfileMenu={() => setMenuOwner((owner) => (owner === "sidebar" ? null : "sidebar"))}
        onCloseProfileMenu={closeMenu}
        onSignOut={handleSignOut}
      />
      <main className="shell__content" aria-busy={verifyingSession}>
        {verifyingSession && <div className="shell__progress" role="progressbar" aria-label="Vérification de la session" />}
        {serverDown && (
          <div className="shell__banner" role="alert">
            <ServerCrash size={18} aria-hidden="true" />
            <span>Serveur MMV inaccessible. Les actions nécessitant le serveur sont indisponibles.</span>
            <button
              type="button"
              className="button button--secondary button--small"
              onClick={() => void health.reload()}
              disabled={health.checking}
            >
              <RefreshCw size={14} aria-hidden="true" />
              {health.checking ? "Nouvelle tentative…" : "Réessayer"}
            </button>
          </div>
        )}
        {content}
      </main>
      <StatusBar expiresAtUtc={session.expiresAtUtc} health={health.status} checking={health.checking} />
    </div>
  );
}
