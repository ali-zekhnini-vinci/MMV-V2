// Règles de saisie du formulaire Client — D-03-02 acceptée (G03-METIER A, 17/10/2026 ;
// docs/phase/03/DECISIONS_G03_METIER.md). Validation UX seulement : MMV.Api revalide tout côté serveur
// (MMV.Application ClientInputValidator, mêmes règles ; 400/422 + codes par champ).
// Champs volontairement absents (D-03-03) : n° de sécurité sociale et mutuelle, toute donnée optique ou
// médicale (Phase 04).
import { z } from "zod";
import type { ClientDetail, ClientInput } from "../api/clients.ts";

export const CLIENT_LIMITS = {
  name: 100,
  email: 254,
  phoneMinDigits: 8,
  phoneMaxDigits: 15,
  addressLine: 200,
  city: 100,
  postalCode: 10,
  notes: 1000,
} as const;

const MESSAGES = {
  firstNameRequired: "Le prénom est requis",
  lastNameRequired: "Le nom est requis",
  tooLong: (max: number) => `${max} caractères au maximum`,
  phone: `Le numéro doit contenir entre ${CLIENT_LIMITS.phoneMinDigits} et ${CLIENT_LIMITS.phoneMaxDigits} chiffres`,
  email: "Format d'email invalide",
  birthDateFormat: "Date attendue au format jj/mm/aaaa",
  birthDateRange: "La date de naissance doit être passée et postérieure au 01/01/1900",
  postalCode: "Code postal invalide (lettres, chiffres, espace ou tiret)",
};

/** Valeurs du formulaire : toujours des chaînes (champs contrôlés), converties en ClientInput à l'envoi. */
export interface ClientFormValues {
  firstName: string;
  lastName: string;
  birthDate: string;
  phone: string;
  email: string;
  addressLine: string;
  city: string;
  postalCode: string;
  notes: string;
}

export const EMPTY_CLIENT_FORM: ClientFormValues = {
  firstName: "",
  lastName: "",
  birthDate: "",
  phone: "",
  email: "",
  addressLine: "",
  city: "",
  postalCode: "",
  notes: "",
};

/** `jj/mm/aaaa` → `AAAA-MM-JJ` si la date civile existe, sinon `null`. */
export function parseFrenchDate(value: string): string | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value.trim());
  if (!match) return null;
  const [, dd, mm, yyyy] = match;
  const date = new Date(Date.UTC(Number(yyyy), Number(mm) - 1, Number(dd)));
  if (date.getUTCFullYear() !== Number(yyyy) || date.getUTCMonth() !== Number(mm) - 1 || date.getUTCDate() !== Number(dd)) return null;
  return `${yyyy}-${mm}-${dd}`;
}

/** `AAAA-MM-JJ` → `jj/mm/aaaa` (affichage). */
export function formatIsoDate(value: string | null): string {
  if (!value) return "";
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : "";
}

const todayIso = () => new Date().toISOString().slice(0, 10);

const optionalText = (max: number) => z.string().trim().max(max, MESSAGES.tooLong(max));

export const clientFormSchema = z.object({
  firstName: z.string().trim().min(1, MESSAGES.firstNameRequired).max(CLIENT_LIMITS.name, MESSAGES.tooLong(CLIENT_LIMITS.name)),
  lastName: z.string().trim().min(1, MESSAGES.lastNameRequired).max(CLIENT_LIMITS.name, MESSAGES.tooLong(CLIENT_LIMITS.name)),
  birthDate: z
    .string()
    .trim()
    .refine((value) => value === "" || parseFrenchDate(value) !== null, MESSAGES.birthDateFormat)
    .refine((value) => {
      const iso = value === "" ? null : parseFrenchDate(value);
      return iso === null || (iso >= "1900-01-01" && iso <= todayIso());
    }, MESSAGES.birthDateRange),
  phone: z
    .string()
    .trim()
    .refine((value) => {
      if (value === "") return true;
      if (!/^\+?[\d\s.-]+$/.test(value)) return false;
      const digits = value.replace(/\D/g, "").length;
      return digits >= CLIENT_LIMITS.phoneMinDigits && digits <= CLIENT_LIMITS.phoneMaxDigits;
    }, MESSAGES.phone),
  email: z
    .string()
    .trim()
    .max(CLIENT_LIMITS.email, MESSAGES.tooLong(CLIENT_LIMITS.email))
    .refine((value) => value === "" || z.email().safeParse(value).success, MESSAGES.email),
  addressLine: optionalText(CLIENT_LIMITS.addressLine),
  city: optionalText(CLIENT_LIMITS.city),
  postalCode: optionalText(CLIENT_LIMITS.postalCode).refine((value) => value === "" || /^[A-Za-z0-9][A-Za-z0-9 -]*$/.test(value), MESSAGES.postalCode),
  notes: optionalText(CLIENT_LIMITS.notes),
});

const nullIfEmpty = (value: string): string | null => {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
};

/** Valeurs validées → corps de requête (chaînes vides → null, date convertie en ISO). */
export function toClientInput(values: ClientFormValues): ClientInput {
  return {
    firstName: values.firstName.trim(),
    lastName: values.lastName.trim(),
    birthDate: values.birthDate.trim() === "" ? null : parseFrenchDate(values.birthDate),
    phone: nullIfEmpty(values.phone),
    email: nullIfEmpty(values.email),
    addressLine: nullIfEmpty(values.addressLine),
    city: nullIfEmpty(values.city),
    postalCode: nullIfEmpty(values.postalCode),
    notes: nullIfEmpty(values.notes),
  };
}

export function toFormValues(client: ClientDetail): ClientFormValues {
  return {
    firstName: client.firstName,
    lastName: client.lastName,
    birthDate: formatIsoDate(client.birthDate),
    phone: client.phone ?? "",
    email: client.email ?? "",
    addressLine: client.addressLine ?? "",
    city: client.city ?? "",
    postalCode: client.postalCode ?? "",
    notes: client.notes ?? "",
  };
}

const FIELD_NAMES = Object.keys(EMPTY_CLIENT_FORM) as (keyof ClientFormValues)[];

/** Codes serveur (ClientFieldErrors) → message affiché. Un code inconnu donne un message générique (jamais le code brut). */
const SERVER_CODE_MESSAGES: Record<string, string> = {
  required: "Champ requis",
  too_long: "Valeur trop longue",
  invalid_format: "Format invalide",
  out_of_range: "Valeur hors limites",
};

/** Erreurs par champ renvoyées par le serveur (400/422), restreintes aux champs du formulaire. */
export function serverFieldErrors(fieldErrors: Record<string, string[]> | undefined): [keyof ClientFormValues, string][] {
  if (!fieldErrors) return [];
  return FIELD_NAMES.filter((field) => fieldErrors[field]?.length).map((field) => [
    field,
    SERVER_CODE_MESSAGES[fieldErrors[field][0]] ?? "Valeur refusée par le serveur",
  ]);
}

/** Nom d'affichage conforme à la maquette : NOM Prénom. */
export const clientDisplayName = (client: { firstName: string; lastName: string }) =>
  `${client.lastName.toLocaleUpperCase("fr")} ${client.firstName}`;

export const clientInitials = (client: { firstName: string; lastName: string }) =>
  `${client.firstName.trim().charAt(0)}${client.lastName.trim().charAt(0)}`.toLocaleUpperCase("fr");

/** Horodatage UTC → date locale jj/mm/aaaa (le fuseau d'affichage du magasin reste une décision ouverte). */
export function formatUtcDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleDateString("fr-FR", { day: "2-digit", month: "2-digit", year: "numeric" });
}
