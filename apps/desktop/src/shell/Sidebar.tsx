// Sidebar 260 px (ouverte) / 72 px (réduite) — MMV-Shell-handoff §2, §3, §6, §6bis, §17, §18.
import { LogOut, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import logoHorizontal from "../assets/brand/logo-horizontal.png";
import logoHorizontalDark from "../assets/brand/logo-horizontal-dark-sidebar.png";
import logoMonogram from "../assets/brand/logo-monogram.png";
import type { SessionUser } from "../api/auth.ts";
import { initialOf, LATER_MESSAGE, type NavSection, roleLabel, type ViewId } from "./navigation.ts";
import { ProfileMenu } from "./ProfileMenu.tsx";
import type { Theme } from "./theme.ts";

interface SidebarProps {
  sections: NavSection[];
  current: ViewId;
  collapsed: boolean;
  theme: Theme;
  user: SessionUser;
  profileMenuOpen: boolean;
  onNavigate: (id: ViewId) => void;
  onToggleCollapsed: () => void;
  onToggleProfileMenu: () => void;
  onCloseProfileMenu: (restoreFocus: boolean) => void;
  onSignOut: () => void;
}

export const SIDEBAR_MENU_ID = "sidebar-profile-menu";

export function Sidebar({
  sections,
  current,
  collapsed,
  theme,
  user,
  profileMenuOpen,
  onNavigate,
  onToggleCollapsed,
  onToggleProfileMenu,
  onCloseProfileMenu,
  onSignOut,
}: SidebarProps) {
  const initial = initialOf(user.displayName);

  return (
    <aside className={`sidebar${collapsed ? " sidebar--collapsed" : ""}`} aria-label="Menu principal">
      <div className="sidebar__brand">
        {collapsed ? (
          <img className="sidebar__monogram" src={logoMonogram} alt="MMV" height={34} />
        ) : (
          <img
            className="sidebar__logo"
            src={theme === "dark" ? logoHorizontalDark : logoHorizontal}
            alt="MMV — ManageMyVision"
            height={30}
          />
        )}
        <button
          type="button"
          className="icon-button sidebar__toggle"
          aria-label={collapsed ? "Ouvrir le menu" : "Réduire le menu"}
          aria-expanded={!collapsed}
          data-tooltip={collapsed ? "Ouvrir le menu" : undefined}
          title={collapsed ? undefined : "Réduire le menu"}
          onClick={onToggleCollapsed}
        >
          {collapsed ? <PanelLeftOpen size={18} aria-hidden="true" /> : <PanelLeftClose size={18} aria-hidden="true" />}
        </button>
      </div>

      <nav className="sidebar__nav" aria-label="Navigation">
        {sections.map((section, index) => (
          <div key={section.id} className="sidebar__section">
            {index > 0 && <hr className="sidebar__separator" />}
            <p className="sidebar__eyebrow" id={`nav-section-${section.id}`}>
              {section.label}
            </p>
            <ul aria-labelledby={`nav-section-${section.id}`}>
              {section.items.map((item) => {
                const Icon = item.icon;
                const active = item.id === current;
                const later = item.availability === "later";
                const tooltip = later ? `${item.label} — ${LATER_MESSAGE}` : item.label;
                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      className={`nav-item${active ? " nav-item--active" : ""}${later ? " nav-item--later" : ""}`}
                      aria-current={active ? "page" : undefined}
                      aria-label={collapsed || later ? tooltip : undefined}
                      data-tooltip={collapsed ? tooltip : undefined}
                      title={!collapsed && later ? LATER_MESSAGE : undefined}
                      onClick={() => onNavigate(item.id)}
                    >
                      <Icon size={20} aria-hidden="true" />
                      <span className="nav-item__label">{item.label}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      <div className="sidebar__user">
        {collapsed ? (
          <div className="sidebar__user-anchor">
            <button
              type="button"
              className="avatar avatar--button"
              aria-label={`Menu utilisateur — ${user.displayName}`}
              aria-haspopup="true"
              aria-expanded={profileMenuOpen}
              aria-controls={SIDEBAR_MENU_ID}
              data-tooltip={user.displayName}
              onClick={onToggleProfileMenu}
            >
              {initial}
            </button>
            {profileMenuOpen && (
              <ProfileMenu
                id={SIDEBAR_MENU_ID}
                user={user}
                placement="sidebar"
                onClose={onCloseProfileMenu}
                onSignOut={onSignOut}
              />
            )}
          </div>
        ) : (
          <>
            <span className="avatar" aria-hidden="true">
              {initial}
            </span>
            <div className="sidebar__user-text">
              <span className="sidebar__user-name">{user.displayName}</span>
              <span className="sidebar__user-role">{roleLabel(user.permissions)}</span>
            </div>
            <button
              type="button"
              className="icon-button icon-button--small sidebar__logout"
              aria-label="Se déconnecter"
              title="Se déconnecter"
              onClick={onSignOut}
            >
              <LogOut size={18} aria-hidden="true" />
            </button>
          </>
        )}
      </div>
    </aside>
  );
}
