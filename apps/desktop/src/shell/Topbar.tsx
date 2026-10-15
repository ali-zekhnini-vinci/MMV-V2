// Barre supérieure 64 px — MMV-Shell-handoff §7. La recherche globale et les notifications n'existent
// pas encore côté serveur : contrôles visibles mais désactivés, sans badge ni résultat simulé.
import { Bell, ChevronDown, Moon, Search, Sun } from "lucide-react";
import type { SessionUser } from "../api/auth.ts";
import { initialOf, LATER_MESSAGE } from "./navigation.ts";
import { ProfileMenu } from "./ProfileMenu.tsx";
import type { Theme } from "./theme.ts";

interface TopbarProps {
  theme: Theme;
  user: SessionUser;
  profileMenuOpen: boolean;
  onToggleTheme: () => void;
  onToggleProfileMenu: () => void;
  onCloseProfileMenu: (restoreFocus: boolean) => void;
  onSignOut: () => void;
}

export const TOPBAR_MENU_ID = "topbar-profile-menu";

export function Topbar({
  theme,
  user,
  profileMenuOpen,
  onToggleTheme,
  onToggleProfileMenu,
  onCloseProfileMenu,
  onSignOut,
}: TopbarProps) {
  return (
    <header className="topbar" aria-label="Barre supérieure">
      <div className="search" title={`Recherche globale — ${LATER_MESSAGE}`}>
        <Search size={18} aria-hidden="true" />
        <input
          type="search"
          aria-label={`Recherche globale — ${LATER_MESSAGE}`}
          placeholder="Recherche — disponible dans une phase suivante"
          disabled
        />
      </div>

      <div className="topbar__actions">
        <button
          type="button"
          className="icon-button icon-button--large"
          aria-label={theme === "dark" ? "Thème — passer en mode clair" : "Thème — passer en mode sombre"}
          title={theme === "dark" ? "Mode clair" : "Mode sombre"}
          onClick={onToggleTheme}
        >
          {theme === "dark" ? <Sun size={20} aria-hidden="true" /> : <Moon size={20} aria-hidden="true" />}
        </button>
        <button
          type="button"
          className="icon-button icon-button--large"
          aria-label={`Notifications — ${LATER_MESSAGE}`}
          title={`Notifications — ${LATER_MESSAGE}`}
          disabled
        >
          <Bell size={20} aria-hidden="true" />
        </button>
        <div className="topbar__profile">
          <button
            type="button"
            className="profile-chip"
            aria-label={`Menu utilisateur — ${user.displayName}`}
            aria-haspopup="true"
            aria-expanded={profileMenuOpen}
            aria-controls={TOPBAR_MENU_ID}
            onClick={onToggleProfileMenu}
          >
            <span className="avatar avatar--small" aria-hidden="true">
              {initialOf(user.displayName)}
            </span>
            <ChevronDown size={16} aria-hidden="true" />
          </button>
          {profileMenuOpen && (
            <ProfileMenu
              id={TOPBAR_MENU_ID}
              user={user}
              placement="topbar"
              onClose={onCloseProfileMenu}
              onSignOut={onSignOut}
            />
          )}
        </div>
      </div>
    </header>
  );
}
