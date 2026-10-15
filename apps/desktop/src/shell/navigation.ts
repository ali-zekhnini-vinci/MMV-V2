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
      { id: "home", label: "Accueil", icon: LayoutDashboard, availability: "available" },
      { id: "clients", label: "Clients", icon: Users, availability: "later" },
      { id: "products", label: "Produits", icon: Package, availability: "later" },
      { id: "prescriptions", label: "Ordonnances", icon: ClipboardList, availability: "later" },
      { id: "orders", label: "Commandes", icon: ShoppingBag, availability: "later" },
      { id: "sales", label: "Ventes", icon: ShoppingCart, availability: "later" },
      { id: "inventory", label: "Inventaire", icon: Boxes, availability: "later" },
      { id: "notifications", label: "Notifications", icon: Bell, availability: "later" },
    ],
  },
  {
    id: "admin",
    label: "Administration",
    items: [
      { id: "reports", label: "Rapports", icon: BarChart3, availability: "later" },
      { id: "settings", label: "Paramètres", icon: Settings, availability: "later" },
      { id: "accounts", label: "Comptes", icon: UserCog, availability: "available", requires: PERMISSION_SYSTEM_ADMIN },
      { id: "diagnostic", label: "Diagnostic technique", icon: Activity, availability: "available" },
    ],
  },
];

const ALL_ITEMS: readonly NavItem[] = NAV_SECTIONS.flatMap((section) => section.items);

export function findNavItem(id: ViewId): NavItem {
  const item = ALL_ITEMS.find((candidate) => candidate.id === id);
  if (!item) throw new Error(`Entrée de navigation inconnue : ${id}`);
  return item;
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
