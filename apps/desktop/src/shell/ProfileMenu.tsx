// Menu utilisateur : seules des informations réelles de la session (nom, identifiant, rôle technique)
// et la déconnexion. Aucun lien « profil » ou « préférences » inventé (MMV-Shell-handoff §12, §18).
import { LogOut } from "lucide-react";
import { useEffect, useRef } from "react";
import type { SessionUser } from "../api/auth.ts";
import { roleLabel } from "./navigation.ts";

interface ProfileMenuProps {
  id: string;
  user: SessionUser;
  placement: "topbar" | "sidebar";
  onClose: (restoreFocus: boolean) => void;
  onSignOut: () => void;
}

export function ProfileMenu({ id, user, placement, onClose, onSignOut }: ProfileMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    menuRef.current?.querySelector<HTMLElement>("[role='menuitem']")?.focus();
    const onPointerDown = (event: PointerEvent) => {
      const anchor = document.querySelector(`[aria-controls='${id}']`);
      const target = event.target as Node;
      if (!menuRef.current?.contains(target) && !anchor?.contains(target)) onClose(false);
    };
    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [id, onClose]);

  return (
    <div
      ref={menuRef}
      id={id}
      className={`profile-menu profile-menu--${placement}`}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.stopPropagation();
          onClose(true);
        }
        if (event.key === "Tab") onClose(false);
      }}
    >
      <div className="profile-menu__identity">
        <span className="profile-menu__name">{user.displayName}</span>
        <span className="profile-menu__meta">{user.userName}</span>
        <span className="profile-menu__meta">{roleLabel(user.permissions)}</span>
      </div>
      <div role="menu" aria-label="Menu utilisateur">
        <button type="button" role="menuitem" className="profile-menu__item profile-menu__item--danger" onClick={onSignOut}>
          <LogOut size={16} aria-hidden="true" />
          Se déconnecter
        </button>
      </div>
    </div>
  );
}
