// Formats et libellés de la Phase 05. AUCUN calcul monétaire : les montants arrivent du serveur en chaînes décimales à
// échelle fixe (« 1234.50 ») et ne sont que mis en forme (« 1 234,50 € » / « 1 234,50 MAD ») par manipulation de texte ;
// aucune conversion entre devises, aucun total multi-devises.
import type { AvailabilityCode, StockPolicy } from "../api/catalog.ts";

const DECIMAL = /^(-?)(\d+)(?:\.(\d+))?$/;
/** Espace fine insécable (milliers) et espace insécable (devise), écrites par leur code pour rester visibles dans le source. */
export const THIN_SPACE = String.fromCharCode(0x202f);
export const NO_BREAK_SPACE = String.fromCharCode(0xa0);

/** « 1234.5000 » → « 1 234,5000 » (séparateur des milliers : espace fine insécable). */
export function formatDecimal(value: string): string {
  const match = DECIMAL.exec(value);
  if (!match) return value;
  const [, sign, integer, fraction] = match;
  const grouped = integer.replace(/\B(?=(\d{3})+(?!\d))/g, THIN_SPACE);
  return `${sign}${grouped}${fraction === undefined ? "" : `,${fraction}`}`;
}

export function formatMoney(value: string | null | undefined, currency: string): string {
  if (value === null || value === undefined) return "—";
  const text = formatDecimal(value);
  return currency === "EUR" ? `${text}${NO_BREAK_SPACE}€` : `${text}${NO_BREAK_SPACE}${currency}`;
}

/** Saisie française tolérée (« 41,25 ») → chaîne décimale stricte envoyée au serveur (« 41.25 ») ; vide → null. */
export function decimalInput(value: string): string | null {
  const text = value.trim().replace(/\s/g, "").replace(",", ".");
  return text === "" ? null : text;
}

export const POLICY_LABELS: Record<StockPolicy, string> = {
  stocked: "Stocké",
  order_only: "Commandé à la demande",
  service: "Prestation (non stockable)",
};

export const AVAILABILITY_LABELS: Record<AvailabilityCode, string> = {
  archived: "Archivé",
  available: "Disponible",
  on_order: "Sur commande",
  incomplete: "Fiche incomplète",
  in_stock: "En stock",
  low_stock: "Stock faible",
  out_of_stock: "Rupture",
};

export const AVAILABILITY_TONES: Record<AvailabilityCode, "success" | "warning" | "error" | "info" | "neutral"> = {
  archived: "neutral",
  available: "success",
  on_order: "info",
  incomplete: "warning",
  in_stock: "success",
  low_stock: "warning",
  out_of_stock: "error",
};

export const PRICE_STATUS_LABELS: Record<string, string> = {
  up_to_date: "À jour",
  recalculation_pending: "Recalcul en attente",
  incomplete: "Prix incomplet",
};

export const MOVEMENT_LABELS: Record<string, string> = {
  receipt: "Réception",
  manual_in: "Entrée manuelle",
  manual_out: "Sortie manuelle",
  adjustment: "Ajustement",
  inventory_adjustment: "Correction d'inventaire",
};

/** Motifs codés (liste du serveur, D-05-09 / QR-03) et libellés. */
export const REASONS: Record<string, { code: string; label: string }[]> = {
  manual_in: [
    { code: "found", label: "Article retrouvé" },
    { code: "internal_use_return", label: "Retour d'usage interne / démonstration" },
    { code: "customer_return_unsold", label: "Retour client (article non vendu)" },
    { code: "entry_error", label: "Erreur de saisie" },
    { code: "other", label: "Autre (commentaire obligatoire)" },
  ],
  manual_out: [
    { code: "breakage", label: "Casse" },
    { code: "loss", label: "Perte" },
    { code: "theft", label: "Vol constaté" },
    { code: "internal_use", label: "Usage interne / démonstration" },
    { code: "supplier_return", label: "Retour fournisseur" },
    { code: "entry_error", label: "Erreur de saisie" },
    { code: "other", label: "Autre (commentaire obligatoire)" },
  ],
  adjustment: [
    { code: "count_correction", label: "Correction après comptage" },
    { code: "breakage", label: "Casse" },
    { code: "loss", label: "Perte" },
    { code: "theft", label: "Vol constaté" },
    { code: "entry_error", label: "Erreur de saisie" },
    { code: "other", label: "Autre (commentaire obligatoire)" },
  ],
};

export function reasonLabel(code: string): string {
  for (const list of Object.values(REASONS)) {
    const found = list.find((reason) => reason.code === code);
    if (found) return found.label;
  }
  return code === "supplier_receipt" ? "Réception fournisseur" : code;
}

/** Codes d'erreur bornés du serveur → messages français (aucun détail technique affiché). */
export const ERROR_MESSAGES: Record<string, string> = {
  version_conflict: "Cet élément a été modifié depuis un autre poste. Rechargez avant de recommencer.",
  request_id_reused: "Cette demande a déjà été utilisée pour une autre opération. Rechargez.",
  archived: "Élément archivé : aucune modification possible.",
  not_archived: "Élément déjà actif.",
  stock_insufficient: "Stock insuffisant : le stock ne peut pas devenir négatif.",
  stock_reserved: "Quantité réservée : la sortie entamerait le stock réservé.",
  stock_changed: "Le stock a changé sur un autre poste depuis l'affichage. Rechargez et vérifiez.",
  not_stocked: "Cette variante n'est pas stockée (commandée à la demande ou prestation) : aucun stock physique.",
  currency_mismatch: "Devise différente de celle du magasin : aucune conversion n'est effectuée.",
  price_preview_stale: "Le prix aperçu n'est plus à jour. Relancez l'aperçu avant d'enregistrer.",
  coefficient_below_one_unconfirmed: "Coefficient inférieur à 1 (vente sous le prix d'achat) : confirmation explicite requise.",
  costs_forbidden: "Votre compte ne détient pas le droit d'écrire les coûts.",
  manual_price_forbidden: "Votre compte ne détient pas le droit de fixer un prix manuel.",
  policy_change_refused: "Changement de politique refusé : stock en main, réservé ou inventaire ouvert.",
  archive_refused: "Archivage refusé : stock en main, réservé ou inventaire ouvert.",
  incomplete_variants_unconfirmed: "Des variantes commandées à la demande perdraient leur fournisseur principal : confirmation requise.",
  inventory_open: "Un inventaire est déjà ouvert sur une partie de ce périmètre.",
  inventory_closed: "Inventaire clos : aucune modification possible.",
  reason_required: "Chaque écart doit être justifié par un motif avant la validation.",
  empty_scope: "Aucune variante stockée dans ce périmètre.",
  scope_too_large: "Périmètre trop large : réduisez-le (famille ou sélection).",
  import_has_errors: "Le fichier contient des lignes en erreur : rien n'a été importé (tout ou rien).",
  import_already_applied: "Ce fichier a déjà été importé : aucun doublon n'est créé.",
  import_not_checked: "Lancez d'abord le contrôle à blanc.",
  import_closed: "Lot d'import clos.",
  recalculation_not_applicable: "Recalcul impossible : prix manuel ou prix d'achat / coefficient manquant.",
  validation_failed: "Le serveur a refusé certaines valeurs : vérifiez les champs signalés.",
};

export const FIELD_ERRORS: Record<string, string> = {
  required: "Obligatoire.",
  too_long: "Trop long.",
  invalid_format: "Format invalide.",
  out_of_range: "Hors limites.",
  duplicate: "Déjà utilisé.",
  unknown: "Inconnu.",
  not_allowed: "Non admis.",
  archived: "Archivé.",
};

export function fieldError(errors: Record<string, string[]> | undefined, field: string): string | null {
  const codes = errors?.[field];
  return codes && codes.length > 0 ? (FIELD_ERRORS[codes[0]] ?? codes[0]) : null;
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  return new Intl.DateTimeFormat("fr-FR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}

export function formatTime(value: string): string {
  return new Intl.DateTimeFormat("fr-FR", { timeStyle: "short" }).format(new Date(value));
}

/** Libellés courts des six familles (pastille .cat du Lot 6) ; le libellé complet vient du serveur. */
export const FAMILY_SHORT_LABELS: Record<string, string> = {
  optical_frames: "Monture",
  sunglasses: "Solaire",
  ophthalmic_lenses: "Verre",
  contact_lenses_solutions: "Lentille / produit",
  accessories: "Accessoire",
  services: "Prestation",
};

/** Préfixe du SKU interne proposé par famille. */
const SKU_PREFIXES: Record<string, string> = {
  optical_frames: "MON",
  sunglasses: "SOL",
  ophthalmic_lenses: "VER",
  contact_lenses_solutions: "LEN",
  accessories: "ACC",
  services: "PRE",
};

/** Alphabet sans caractères ambigus (0/O, 1/I/L) : 31 symboles, 6 tirages ⇒ ~8,9 × 10⁸ combinaisons par famille. */
const SKU_ALPHABET = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

/**
 * SKU interne généré automatiquement par MMV (correctif Q05 demandé par le propriétaire) : préfixe de famille + 6 symboles
 * tirés par le générateur cryptographique du poste. Aucune saisie exigée ; l'unicité reste garantie par le SERVEUR
 * (contrainte `ux_product_variants_sku`) — un doublon improbable est refusé et un nouveau SKU est proposé.
 */
export function proposeSku(familyCode: string, random: (length: number) => ArrayLike<number> = (length) => crypto.getRandomValues(new Uint8Array(length))): string {
  const bytes = Array.from(random(6));
  const body = bytes.map((byte) => SKU_ALPHABET[byte % SKU_ALPHABET.length]).join("");
  return `${SKU_PREFIXES[familyCode] ?? "ART"}-${body}`;
}
