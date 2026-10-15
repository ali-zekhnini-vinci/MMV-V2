// Règles de l'écran de connexion (références : MMV-Login-states.md §2, §3, §6, §7, §13).
// Module pur, sans React ni réseau : testé par loginForm.test.ts.
import type { ApiError } from "../api/client.ts";

export interface LoginFieldErrors {
  userName?: string;
  password?: string;
}

export const FIELD_MESSAGES = {
  userNameRequired: "Veuillez saisir votre identifiant.",
  passwordRequired: "Veuillez saisir votre mot de passe.",
} as const;

export function validateLogin(userName: string, password: string): LoginFieldErrors {
  const errors: LoginFieldErrors = {};
  if (!userName.trim()) errors.userName = FIELD_MESSAGES.userNameRequired;
  if (!password) errors.password = FIELD_MESSAGES.passwordRequired;
  return errors;
}

export type LoginBannerKind = "invalid-credentials" | "technical" | "rate-limited" | "session-expired" | "not-configured";

export interface LoginBanner {
  kind: LoginBannerKind;
  message: string;
  /** Bordure d'erreur sur les deux champs (identifiants refusés, sans dire lequel). */
  markFields: boolean;
}

export const BANNERS: Record<LoginBannerKind, LoginBanner> = {
  "invalid-credentials": {
    kind: "invalid-credentials",
    message: "Identifiant ou mot de passe incorrect.",
    markFields: true,
  },
  technical: {
    kind: "technical",
    message: "Connexion impossible pour le moment. Veuillez réessayer.",
    markFields: false,
  },
  "rate-limited": {
    kind: "rate-limited",
    message: "Trop de tentatives. Veuillez patienter avant de réessayer.",
    markFields: false,
  },
  "session-expired": {
    kind: "session-expired",
    message: "Votre session a expiré. Veuillez vous reconnecter.",
    markFields: false,
  },
  "not-configured": {
    kind: "not-configured",
    message: "Serveur MMV non configuré sur ce poste.",
    markFields: false,
  },
};

/** Un 401 de /api/auth/login est un refus neutre ; toute autre erreur est technique (jamais relayée brute). */
export function bannerForLoginError(error: ApiError): LoginBanner {
  switch (error.kind) {
    case "unauthorized":
      return BANNERS["invalid-credentials"];
    case "rate-limited":
      return BANNERS["rate-limited"];
    case "not-configured":
      return BANNERS["not-configured"];
    default:
      return BANNERS.technical;
  }
}
