// Navigation du shell (référence visuelle DRAFT : MMV-Shell-handoff.md §6). Module pur, testé par
// navigation.test.ts. La garde UI ne remplace JAMAIS l'autorisation serveur : elle masque une entrée
// ou affiche « non autorisé », mais chaque appel protégé est décidé par MMV.Api (policies).
import type { LucideIcon } from "lucide-react";
import {
  Activity,
  BarChart3,
  Bell,
  Boxes,
  ClipboardList,
  LayoutDashboard,
  Package,
  Settings,
  ShoppingBag,
  ShoppingCart,
  UserCog,
  Users,
} from "lucide-react";
import { PERMISSION_SYSTEM_ADMIN } from "../api/auth.ts";

export type ViewId =
  | "home"
  | "diagnostic"
  | "accounts"
  | "clients"
  | "products"
  | "prescriptions"
  | "orders"
  | "sales"
  | "inventory"
  | "notifications"
  | "reports"
  | "settings";

export interface NavItem {
  id: ViewId;
  /** Route React Router du shell (routeur mémoire, Shell.tsx). */
  path: string;
  label: string;
  icon: LucideIcon;
  /** `later` : module d'une phase suivante, aucun écran ni donnée. */
  availability: "available" | "later";
  /** Permission technique requise pour AFFICHER l'entrée (le serveur reste seul juge). */
  requires?: string;
}

export interface NavSection {
  id: string;
  label: string;
  items: NavItem[];
}

export const LATER_MESSAGE = "Disponible dans une phase suivante";

export const NAV_SECTIONS: readonly NavSection[] = [
  {
    id: "main",
    label: "Principal",
    items: [
      { id: "home", path: "/", label: "Accueil", icon: LayoutDashboard, availability: "available" },
      { id: "clients", path: "/clients", label: "Clients", icon: Users, availability: "later" },
      { id: "products", path: "/produits", label: "Produits", icon: Package, availability: "later" },
      { id: "prescriptions", path: "/ordonnances", label: "Ordonnances", icon: ClipboardList, availability: "later" },
      { id: "orders", path: "/commandes", label: "Commandes", icon: ShoppingBag, availability: "later" },
      { id: "sales", path: "/ventes", label: "Ventes", icon: ShoppingCart, availability: "later" },
      { id: "inventory", path: "/inventaire", label: "Inventaire", icon: Boxes, availability: "later" },
      { id: "notifications", path: "/notifications", label: "Notifications", icon: Bell, availability: "later" },
    ],
  },
  {
    id: "admin",
    label: "Administration",
    items: [
      { id: "reports", path: "/rapports", label: "Rapports", icon: BarChart3, availability: "later" },
      { id: "settings", path: "/parametres", label: "Paramètres", icon: Settings, availability: "later" },
      { id: "accounts", path: "/comptes", label: "Comptes", icon: UserCog, availability: "available", requires: PERMISSION_SYSTEM_ADMIN },
      { id: "diagnostic", path: "/diagnostic", label: "Diagnostic technique", icon: Activity, availability: "available" },
    ],
  },
];

export const NAV_ITEMS: readonly NavItem[] = NAV_SECTIONS.flatMap((section) => section.items);

export function findNavItem(id: ViewId): NavItem {
  const item = NAV_ITEMS.find((candidate) => candidate.id === id);
  if (!item) throw new Error(`Entrée de navigation inconnue : ${id}`);
  return item;
}

/** Entrée correspondant à une route du shell ; `undefined` pour une route inconnue (redirigée vers l'Accueil). */
export function findNavItemByPath(pathname: string): NavItem | undefined {
  return NAV_ITEMS.find((candidate) => candidate.path === pathname);
}

export function hasPermission(permissions: readonly string[], permission: string | undefined): boolean {
  return permission === undefined || permissions.includes(permission);
}

/** Sections visibles pour ces permissions ; une section vide disparaît. */
export function visibleSections(permissions: readonly string[]): NavSection[] {
  return NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter((item) => hasPermission(permissions, item.requires)),
  })).filter((section) => section.items.length > 0);
}

export type ViewAccess = "allowed" | "later" | "forbidden";

/** Garde UI : décide de l'écran à monter, sans jamais accorder un droit côté serveur. */
export function viewAccess(id: ViewId, permissions: readonly string[]): ViewAccess {
  const item = findNavItem(id);
  if (!hasPermission(permissions, item.requires)) return "forbidden";
  return item.availability === "later" ? "later" : "allowed";
}

export function roleLabel(permissions: readonly string[]): string {
  return permissions.includes(PERMISSION_SYSTEM_ADMIN) ? "Administrateur système" : "Utilisateur";
}

export function initialOf(displayName: string): string {
  const first = displayName.trim().charAt(0);
  return first ? first.toLocaleUpperCase("fr") : "?";
}
