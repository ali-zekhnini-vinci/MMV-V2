// Menu utilisateur : seules des informations réelles de la session (nom, identifiant, rôle technique)
// et la déconnexion. Aucun lien « profil » ou « préférences » inventé (MMV-Shell-handoff §12, §18).
// Primitive Radix DropdownMenu (composant shadcn/ui components/ui/dropdown-menu.tsx) : ouverture au
// clic, à Entrée ou Espace, navigation aux flèches, Échap et clic extérieur ferment le menu et rendent
// le focus au déclencheur.
import { LogOut } from "lucide-react";
import type { ReactElement } from "react";
import type { SessionUser } from "../api/auth.ts";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../components/ui/dropdown-menu.tsx";
import { roleLabel } from "./navigation.ts";

interface ProfileMenuProps {
  user: SessionUser;
  /** Barre supérieure : sous le déclencheur ; sidebar réduite : à droite de l'avatar (handoff §12). */
  placement: "topbar" | "sidebar";
  onSignOut: () => void;
  /** Bouton déclencheur (reçoit les attributs ARIA du menu). */
  children: ReactElement;
}

export function ProfileMenu({ user, placement, onSignOut, children }: ProfileMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>{children}</DropdownMenuTrigger>
      <DropdownMenuContent
        side={placement === "topbar" ? "bottom" : "right"}
        align="end"
        sideOffset={placement === "topbar" ? 6 : 10}
      >
        <DropdownMenuLabel>
          <span className="font-semibold text-text-primary">{user.displayName}</span>
          <span className="text-xs text-text-secondary">{user.userName}</span>
          <span className="text-xs text-text-secondary">{roleLabel(user.permissions)}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onSelect={onSignOut}>
          <LogOut size={16} aria-hidden="true" />
          Se déconnecter
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
