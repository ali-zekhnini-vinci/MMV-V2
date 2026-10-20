// PROTOTYPE 04-A — outils d'écran du dossier optique : chemins, contexte de retour vers la fiche client (validé à la
// réception, comme appointmentLaunch.ts), messages d'échec bornés (jamais le texte du serveur), résumé OD/OG restitué
// tel que saisi. Module pur.
import type { ApiError } from "../api/client.ts";
import { EYE_FIELDS, type EyeValues, type OpticalKind } from "./opticalContract.ts";
import { FIELD_LABEL, FIELD_UNIT_SHORT, KIND_SEGMENT, SIDE_SHORT } from "./opticalLabels.ts";

export const OPTICAL_ROOT = "/optique";
export const opticalListPath = () => OPTICAL_ROOT;
export const opticalNewPath = (kind?: OpticalKind) => (kind ? `${OPTICAL_ROOT}/nouveau/${KIND_SEGMENT[kind]}` : `${OPTICAL_ROOT}/nouveau`);
export const opticalDetailPath = (kind: OpticalKind, id: string) => `${OPTICAL_ROOT}/${KIND_SEGMENT[kind]}/${id}`;
export const opticalDraftPath = (kind: OpticalKind, id: string) => `${opticalDetailPath(kind, id)}/brouillon`;
export const clientCardPath = (clientId: string) => `/clients/${clientId}`;

/** Onglet de la fiche à rouvrir au retour (J-04-01 étape 7, D-04-08.e). */
export const OPTICAL_TAB = "optical";

export interface OpticalLaunchState {
  /** Fiche client d'origine et nom affiché pour le bouton de retour. */
  returnTo: string;
  returnLabel: string;
  /** Client présélectionné pour une création (depuis la fiche ou après sélection dans la liste globale). */
  client?: { id: string; label: string };
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);

export function readOpticalLaunch(state: unknown): OpticalLaunchState | null {
  if (!isRecord(state)) return null;
  const { returnTo, returnLabel, client } = state;
  if (typeof returnTo !== "string" || !(returnTo.startsWith("/clients/") || returnTo === OPTICAL_ROOT) || typeof returnLabel !== "string") return null;
  if (client === undefined) return { returnTo, returnLabel };
  if (!isRecord(client) || typeof client.id !== "string" || client.id === "" || typeof client.label !== "string") return null;
  return { returnTo, returnLabel, client: { id: client.id, label: client.label } };
}

/** État de navigation de la fiche : onglet à ouvrir (validé par la fiche selon les onglets montés). */
export function readClientTab(state: unknown): string | undefined {
  return isRecord(state) && typeof state.clientTab === "string" ? state.clientTab : undefined;
}

export const returnToCardState = () => ({ clientTab: OPTICAL_TAB });

const SUMMARY_ABBR = { sphere: "Sph", cylinder: "Cyl", axis: "Axe", addition: "Add" } as const;

/**
 * Résumé d'un œil, valeurs restituées EXACTEMENT telles que saisies (aucun formatage, aucun calcul). Sans état d'œil
 * (D-04-16) : un œil dont aucun champ n'est rempli est écrit « non renseigné », jamais « 0 ».
 */
export function eyeSummary(side: "od" | "og", values: EyeValues): string {
  if (EYE_FIELDS.every((field) => values[field] === "")) return `${SIDE_SHORT[side]} : non renseigné`;
  // Unité affichée à côté de la valeur (°), jamais intégrée à la valeur enregistrée.
  const parts = (["sphere", "cylinder", "axis", "addition"] as const)
    .filter((field) => values[field] !== "")
    .map((field) => `${SUMMARY_ABBR[field]} ${values[field]}${field === "axis" ? FIELD_UNIT_SHORT.axis : ""}`);
  return `${SIDE_SHORT[side]} : ${parts.length > 0 ? parts.join(" · ") : "aucune valeur principale"}`;
}

export interface FailureView {
  tone: "error" | "warning" | "info";
  title: string;
  text: string;
  /** Une version plus récente existe : proposer « Recharger » en conservant la saisie (GAP-04-09). */
  reload?: boolean;
}

/** Message d'échec d'une écriture : codes bornés du contrat proposé, jamais un texte serveur. */
export function describeOpticalFailure(error: ApiError): FailureView {
  switch (error.code) {
    case "version_conflict":
      return {
        tone: "error",
        title: "Modifié depuis un autre poste",
        text: "Cet enregistrement a été modifié depuis un autre poste. Votre saisie est conservée à l'écran ; rien n'a été enregistré. Rechargez la version actuelle avant de réessayer.",
        reload: true,
      };
    case "draft_exists":
      return { tone: "warning", title: "Rectification déjà ouverte", text: "Une rectification est déjà en cours sur cet enregistrement (un seul brouillon à la fois). Rechargez pour la consulter.", reload: true };
    case "revision_conflict":
      return { tone: "error", title: "Révision dépassée", text: "Une autre révision a été confirmée entre-temps. Rien n'a été enregistré. Rechargez avant de réessayer.", reload: true };
    case "client_archived":
      return { tone: "warning", title: "Client archivé", text: "Réactivez le client avant d'ajouter ou de rectifier une ordonnance médicale ou une mesure magasin." };
    case "invalidated":
    case "already_invalidated":
      return { tone: "warning", title: "Enregistrement invalidé", text: "Cet enregistrement est invalidé : il reste consultable mais ne peut plus être modifié.", reload: true };
    case "not_draft":
    case "not_rectification":
      return { tone: "warning", title: "Aucun brouillon ouvert", text: "Il n'y a plus de brouillon à modifier sur cet enregistrement.", reload: true };
    case "incomplete":
      return { tone: "warning", title: "Confirmation refusée", text: "Le double de test a refusé la confirmation (règle de démonstration du banc, pas une validation médicale). Le brouillon est conservé." };
    default:
      break;
  }
  switch (error.kind) {
    case "forbidden":
      return { tone: "info", title: "Action non autorisée", text: "Le service a refusé cette action pour ce profil." };
    case "not-found":
      return { tone: "error", title: "Enregistrement introuvable", text: "Cet enregistrement n'existe pas ou plus." };
    case "validation":
      return { tone: "error", title: "Saisie refusée", text: "Certaines valeurs ont été refusées. Les messages sont affichés sous les champs ; votre saisie est conservée." };
    default:
      return { tone: "error", title: "Erreur technique", text: `${error.message} Aucune modification n'a été enregistrée ; votre saisie est conservée.` };
  }
}

/**
 * « Enregistrer » en deux temps (D-04-17) : le brouillon A ÉTÉ enregistré, puis la confirmation a échoué. Le message ne
 * dit jamais que rien n'a été enregistré ; la dernière révision confirmée reste inchangée.
 */
export function describeConfirmFailure(error: ApiError): FailureView {
  const saved = "Vos valeurs sont enregistrées en brouillon, non confirmé ; la dernière révision confirmée éventuelle est inchangée.";
  if (error.code === "incomplete")
    return {
      tone: "warning",
      title: "Brouillon enregistré — confirmation refusée",
      text: `${saved} Complétez la saisie puis cliquez de nouveau sur « Enregistrer », ou laissez l'enregistrement en brouillon.`,
    };
  if (error.kind === "network" || error.kind === "timeout" || error.kind === "http" || error.kind === "invalid-response")
    return {
      tone: "error",
      title: "Brouillon enregistré — confirmation non aboutie",
      text: `${saved} La confirmation n'a pas abouti : ${error.message} Cliquez de nouveau sur « Enregistrer » pour confirmer, ou laissez le brouillon.`,
    };
  const reason = describeOpticalFailure(error);
  return {
    tone: reason.tone,
    title: "Brouillon enregistré — confirmation refusée",
    text: `${saved} Motif : ${reason.title.toLocaleLowerCase("fr")}.${reason.reload ? " Rechargez la version actuelle avant de réessayer." : ""}`,
    reload: reason.reload,
  };
}

const FIELD_ERROR: Record<string, string> = {
  number_expected: "Nombre attendu, tel qu'il figure sur la source (ex. +1,25).",
  too_long: "Valeur trop longue.",
  optical_values_missing: "Aucune valeur n'est renseignée, ni pour OD ni pour OG (règle de démonstration du banc, pas une validation médicale).",
  required: "Précisez le motif.",
};

export const fieldErrorMessage = (code: string): string => FIELD_ERROR[code] ?? "Valeur refusée.";

/** Libellé lisible d'un chemin de champ renvoyé par le double (`od.sphere`, `provenance.prescribedOn`…). */
export function fieldPathLabel(path: string): string {
  const [head, tail] = path.split(".");
  if (head === "od" || head === "og") return `${SIDE_SHORT[head]} — ${FIELD_LABEL[tail as keyof typeof FIELD_LABEL] ?? tail}`;
  const labels: Record<string, string> = {
    prescriberName: "Prescripteur — nom",
    prescriberIdentifier: "Prescripteur — identifiant",
    prescribedOn: "Date de prescription",
    measuredOn: "Date de la mesure",
    measuredByUserId: "Collaborateur ayant mesuré",
    correction: "Correction optique",
    notes: "Notes",
    comment: "Commentaire",
  };
  return labels[tail ?? head] ?? path;
}
