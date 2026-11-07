// BANC GATE B — données FICTIVES de la maquette de l'assistant d'import CSV (CSV-IMPORT-AI-01).
// Les types ci-dessous illustrent les contrats API PROPOSÉS au dossier Gate B (docs/phase/05/CSV_IA_05_GATE_B.md § 7) :
// chaque information affichée par la maquette provient d'un champ que le serveur devra fournir. Aucune règle métier ici :
// les statuts, diagnostics, compteurs et actions licites sont ceux que le SERVEUR calculera ; la maquette les affiche.
// Fournisseur, fichier, références et montants inventés (aucune marque ni donnée réelle).

export type StepKey = "deposit" | "analysis" | "review" | "check" | "import";

/** État d'une étape dans la navigation horizontale (dérivé de l'état serveur du lot, jamais d'un état local seul). */
export type StepState = "todo" | "current" | "done" | "attention" | "blocked" | "stale" | "locked";

/** États de l'analyse IA (table proposée imports.batch_analyses.status). */
export type AnalysisStatus =
  | "not_started" | "queued" | "running" | "done" | "incomplete" | "unavailable" | "timeout" | "invalid_output" | "cancelled" | "rules_only";

/** Événement OBSERVABLE émis par le serveur (aucun événement inventé, aucune trace de raisonnement du modèle). */
export interface AnalysisEvent {
  seq: number;
  /** Millisecondes depuis la réception du fichier (horloge serveur). */
  atMs: number;
  code:
    | "file_received" | "encoding_detected" | "structure_read" | "rules_validated" | "sample_prepared" | "ollama_queued"
    | "ollama_running" | "ollama_received" | "validation_running" | "analysis_done" | "ollama_unavailable" | "rules_only_chosen";
  title: string;
  detail: string;
  tone: "ok" | "info" | "warn" | "err" | "accent";
}

/** Progression MESURABLE (compteurs réels) ; une opération non mesurable n'a pas de barre mais un indicateur indéterminé. */
export interface MeasuredProgress {
  label: string;
  done: number;
  total: number;
}

/** Modèle : configuré, disponible, actif (confirmé par la réponse d'Ollama), ou règles seules. */
export interface ModelState {
  mode: "ai" | "rules_only" | "disabled";
  configured: string | null;
  available: boolean | null;
  /** Renseigné UNIQUEMENT quand la réponse d'Ollama a confirmé le modèle exécuté (nom et empreinte). */
  active: { name: string; digest: string } | null;
  ollamaVersion: string | null;
  endpoint: string;
}

export type RowStatus = "valid" | "warning" | "ambiguous" | "invalid" | "unrecognized" | "ignored";

export interface ReviewRow {
  line: number;
  family: { label: string; status: RowStatus; source: "règle" | "mot-clé" | "à confirmer" | "votre choix" };
  product: string;
  variant: string;
  reference: string;
  amounts: string;
  status: RowStatus;
  issues: string[];
  duplicate?: boolean;
  unrecognized?: boolean;
  corrected?: string;
}

export const MODEL_LABEL = "qwen3.5:4b";
export const MODEL_DIGEST = "d8b0f5e9760c";
export const FILE = {
  name: "catalogue_lumen_optique_2026-11.csv",
  size: "18,4 Kio",
  bytes: 18_842,
  sha256: "3f9a6c0e…b8d2c21e",
  encoding: "UTF-8 (sans BOM)",
  delimiter: "« ; »",
  lines: 48,
  dataLines: 47,
  columns: 11,
  supplier: "Lumen Optique (fictif)",
};

/** Les 8 premières lignes du fichier ORIGINAL (octets décodés, aucune interprétation). */
export const ORIGINAL_PREVIEW: string[][] = [
  ["Code", "Désignation", "Coloris", "Cal.", "Pont", "Branche", "Réf. interne", "EAN", "Prix net HT", "PVC TTC", "Remarque"],
  ["LU2041", "Monture LU-2041 acétate", "Noir", "52", "18", "145", "MON-LU2041", "3760000000017", "42,50", "149,00", ""],
  ["LU2041", "Monture LU-2041 acétate", "Écaille", "52", "18", "145", "MON-LU2041-EC", "3760000000024", "42,50", "149,00", ""],
  ["LU2042", "LU-2042 métal", "Or", "50", "19", "140", "MON-LU2042", "3760000000031", "38,00", "129,00", "nouveauté"],
  ["SX310", "Solaire SX-310 polarisée", "Havane", "55", "17", "140", "SOL-SX310", "3760000000048", "51,20", "189,00", ""],
  ["SX310", "Solaire SX-310 polarisée", "Gris", "55", "17", "140", "SOL-SX310-GR", "3760000000055", "51,20", "189,00", ""],
  ["LU2050", "LU-2050 titane", "Bleu nuit", "49", "20", "145", "MON-LU2050", "3760000000062", "64,00 €", "229,00", ""],
  ["", "", "Argent", "49", "20", "145", "MON-LU2050-AR", "3760000000079", "64,00", "229,00", ""],
];

export const MODEL_OK: ModelState = { mode: "ai", configured: MODEL_LABEL, available: true, active: null, ollamaVersion: "0.40.14", endpoint: "127.0.0.1:11434" };
export const MODEL_DONE: ModelState = { ...MODEL_OK, active: { name: MODEL_LABEL, digest: MODEL_DIGEST } };
export const MODEL_DOWN: ModelState = { mode: "ai", configured: MODEL_LABEL, available: false, active: null, ollamaVersion: null, endpoint: "127.0.0.1:11434" };
export const MODEL_RULES: ModelState = { ...MODEL_DOWN, mode: "rules_only" };

const EVENTS_ALL: AnalysisEvent[] = [
  { seq: 1, atMs: 0, code: "file_received", title: "Fichier reçu", detail: `${FILE.name} · ${FILE.size} · SHA-256 ${FILE.sha256} (octets d'origine conservés)`, tone: "ok" },
  { seq: 2, atMs: 40, code: "encoding_detected", title: "Encodage détecté", detail: `${FILE.encoding}, séparateur ${FILE.delimiter}`, tone: "ok" },
  { seq: 3, atMs: 95, code: "structure_read", title: "Structure analysée", detail: "48 lignes lues, 11 colonnes, 1 ligne irrégulière (L31 : 12 cellules), aucune ligne retirée", tone: "warn" },
  { seq: 4, atMs: 210, code: "rules_validated", title: "Première lecture par les règles", detail: "47 lignes d'articles · 2 familles par mot-clé (solaires 12 lignes) · 35 lignes sans mot-clé de famille", tone: "info" },
  { seq: 5, atMs: 260, code: "sample_prepared", title: "Échantillon préparé", detail: "40 lignes sur 47 transmises au modèle · couverture complète (début, 10 positions, 4 structures, 3 anomalies)", tone: "ok" },
  { seq: 6, atMs: 300, code: "ollama_queued", title: "Analyse Ollama en attente", detail: "Aucune autre analyse en cours sur ce serveur : départ immédiat", tone: "info" },
  { seq: 7, atMs: 340, code: "ollama_running", title: "Interprétation des colonnes en cours", detail: `Requête envoyée à ${MODEL_LABEL} (serveur local 127.0.0.1) · durée non prévisible`, tone: "accent" },
  { seq: 8, atMs: 24_120, code: "ollama_received", title: "Interprétation reçue", detail: `Réponse conforme au contrat mmv.csvai.v2 · modèle confirmé ${MODEL_LABEL} (${MODEL_DIGEST}) · 23,8 s · 341 jetons`, tone: "ok" },
  { seq: 9, atMs: 24_180, code: "validation_running", title: "Validation métier en cours", detail: "Revalidation des 47 lignes avec la proposition du modèle (le modèle ne peut qu'ajouter des doutes)", tone: "info" },
  { seq: 10, atMs: 24_390, code: "analysis_done", title: "Analyse terminée", detail: "9 colonnes reconnues · 2 à confirmer · 1 non reconnue · 5 lignes à corriger", tone: "ok" },
];

export const EVENTS_QUEUED: AnalysisEvent[] = [
  ...EVENTS_ALL.slice(0, 5),
  { seq: 6, atMs: 300, code: "ollama_queued", title: "Analyse Ollama en attente", detail: "Une analyse d'un autre poste est en cours sur ce serveur : 1 analyse devant la vôtre", tone: "warn" },
];
export const EVENTS_RUNNING: AnalysisEvent[] = EVENTS_ALL.slice(0, 7);
export const EVENTS_DONE: AnalysisEvent[] = EVENTS_ALL;
export const EVENTS_DOWN: AnalysisEvent[] = [
  ...EVENTS_ALL.slice(0, 5),
  { seq: 6, atMs: 3_310, code: "ollama_unavailable", title: "Ollama ne répond pas", detail: "Aucune réponse de 127.0.0.1:11434 en 3 s (connexion refusée) · aucune donnée envoyée hors du serveur", tone: "err" },
];

export const FAMILIES_FOUND = [
  { label: "Solaires", count: 12, status: "valid" as RowStatus, note: "mot-clé « solaire »" },
  { label: "Montures optiques", count: 35, status: "ambiguous" as RowStatus, note: "aucun mot-clé : suggestion à confirmer" },
];

export const STRUCTURE_ANOMALIES = [
  { line: 31, text: "12 cellules au lieu de 11 (cellule en trop conservée, ligne non regroupée)" },
  { line: 8, text: "identité vide : ligne de continuation probable de L7 (à confirmer)" },
];

export const REVIEW_ROWS: ReviewRow[] = [
  { line: 2, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2041 acétate", variant: "Noir · 52□18 145", reference: "MON-LU2041", amounts: "42,50 (à confirmer)", status: "ambiguous", issues: ["Famille à confirmer (35 lignes)", "Prix net HT : nature du montant à confirmer"] },
  { line: 3, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2041 acétate", variant: "Écaille · 52□18 145", reference: "MON-LU2041-EC", amounts: "42,50 (à confirmer)", status: "ambiguous", issues: ["Famille à confirmer (35 lignes)"] },
  { line: 5, family: { label: "Solaires", status: "valid", source: "mot-clé" }, product: "SX-310 polarisée", variant: "Havane · 55□17 140", reference: "SOL-SX310", amounts: "51,20 (à confirmer)", status: "ambiguous", issues: ["Prix net HT : nature du montant à confirmer"] },
  { line: 7, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2050 titane", variant: "Bleu nuit · 49□20 145", reference: "MON-LU2050", amounts: "64,00 € → 64,00", status: "warning", issues: ["Symbole « € » retiré (valeur d'origine conservée)"] },
  { line: 8, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2050 titane (repris de L7)", variant: "Argent · 49□20 145", reference: "MON-LU2050-AR", amounts: "64,00 (à confirmer)", status: "ambiguous", issues: ["Identité reprise de la ligne 7 : à confirmer"] },
  { line: 10, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2041 acétate", variant: "Havane · 52□18 145", reference: "MON-LU2041", amounts: "42,50 (à confirmer)", status: "invalid", issues: ["SKU interne déjà utilisé ligne 2 (même fichier)"], duplicate: true },
  { line: 14, family: { label: "Solaires", status: "valid", source: "mot-clé" }, product: "SX-318 sport", variant: "Noir mat · 62□15 130", reference: "SOL-SX318", amounts: "48,00 (à confirmer)", status: "ambiguous", issues: ["Prix net HT : nature du montant à confirmer"] },
  { line: 19, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2060 acétate", variant: "Rouge · 51□19 140", reference: "MON-LU2060", amounts: "39,90 (à confirmer)", status: "invalid", issues: ["SKU déjà attribué à un autre modèle du catalogue (SX-310)"], duplicate: true },
  { line: 23, family: { label: "Solaires", status: "valid", source: "mot-clé" }, product: "SX-322 pilote", variant: "Doré · 58□14 140", reference: "SOL-SX322", amounts: "55,00 (à confirmer)", status: "ambiguous", issues: ["Prix net HT : nature du montant à confirmer"] },
  { line: 27, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2072 enfant", variant: "Bleu · 44□16 125", reference: "MON-LU2072", amounts: "1.290 (illisible)", status: "invalid", issues: ["Montant « 1.290 » : milliers ou décimales ? jamais deviné"] },
  { line: 31, family: { label: "—", status: "invalid", source: "règle" }, product: "LU-2080 métal", variant: "—", reference: "MON-LU2080", amounts: "—", status: "invalid", issues: ["12 cellules au lieu de 11 : ligne mal alignée, jamais regroupée"] },
  { line: 36, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2041 acétate", variant: "Noir · 52□18 145", reference: "MON-LU2041-B", amounts: "42,50 (à confirmer)", status: "ambiguous", issues: ["Mêmes déclinaisons que la ligne 2 : doublon potentiel"], duplicate: true },
  { line: 41, family: { label: "Montures optiques", status: "ambiguous", source: "à confirmer" }, product: "LU-2090 acétate", variant: "Vert · 53□17 145", reference: "MON-LU2090", amounts: "44,00 (à confirmer)", status: "ambiguous", issues: ["Famille à confirmer (35 lignes)"] },
  { line: 48, family: { label: "—", status: "unrecognized", source: "règle" }, product: "—", variant: "—", reference: "—", amounts: "—", status: "unrecognized", issues: ["Ligne « Total : 47 articles » : aucune donnée d'article reconnue (conservée, non importée)"], unrecognized: true },
];

/** Exemples représentatifs choisis par le SERVEUR pour la confirmation groupée « Prix net HT » (premier, min, médiane, max, cas particuliers). */
export const PRICE_EXAMPLES = [
  { line: 2, raw: "42,50", read: "42,50", why: "première ligne" },
  { line: 44, raw: "12,90", read: "12,90", why: "plus petite valeur" },
  { line: 19, raw: "39,90", read: "39,90", why: "valeur médiane" },
  { line: 33, raw: "118,00", read: "118,00", why: "plus grande valeur" },
  { line: 7, raw: "64,00 €", read: "64,00", why: "symbole retiré (avertissement)" },
  { line: 27, raw: "1.290", read: "—", why: "illisible : reste à corriger (exclue de la confirmation)" },
];

export interface BenchState {
  id: string;
  label: string;
  step: StepKey;
  group: string;
}

export const BENCH_STATES: BenchState[] = [
  { id: "depot-vide", label: "Dépôt — zone vide", step: "deposit", group: "1 · Déposer" },
  { id: "depot-fichier", label: "Dépôt — fichier choisi + aperçu", step: "deposit", group: "1 · Déposer" },
  { id: "depot-refus", label: "Erreur technique — fichier refusé", step: "deposit", group: "1 · Déposer" },
  { id: "analyse-attente", label: "Analyse en attente (file)", step: "analysis", group: "2 · Analyse IA" },
  { id: "analyse-cours", label: "Analyse en cours", step: "analysis", group: "2 · Analyse IA" },
  { id: "analyse-terminee", label: "Analyse terminée", step: "analysis", group: "2 · Analyse IA" },
  { id: "analyse-indispo", label: "Erreur technique — Ollama indisponible", step: "analysis", group: "2 · Analyse IA" },
  { id: "verif-liste", label: "Vérifier — tableau et filtres", step: "review", group: "3 · Vérifier et corriger" },
  { id: "verif-sku", label: "Correction — SKU en double (L10)", step: "review", group: "3 · Vérifier et corriger" },
  { id: "verif-commercial", label: "Correction groupée — Prix net HT", step: "review", group: "3 · Vérifier et corriger" },
  { id: "controle-bloque", label: "Vérification — erreurs restantes", step: "check", group: "4 · Vérification" },
  { id: "controle-pret", label: "Vérification — prêt (avant/après)", step: "check", group: "4 · Vérification" },
  { id: "import-confirmation", label: "Confirmation explicite", step: "import", group: "5 · Import" },
  { id: "import-cours", label: "Import en cours", step: "import", group: "5 · Import" },
  { id: "import-reussi", label: "Import réussi", step: "import", group: "5 · Import" },
];

export const STEP_LABELS: Record<StepKey, string> = {
  deposit: "Déposer le fichier",
  analysis: "Analyse par l'IA",
  review: "Vérifier et corriger",
  check: "Vérification avant import",
  import: "Confirmer et importer",
};

export const STEP_ORDER: StepKey[] = ["deposit", "analysis", "review", "check", "import"];

/** Historique (table existante, inchangée) : affichée seulement à l'ouverture de la section. */
export const HISTORY = {
  summary: { total: 12, last: "03/11/2026 à 14:05", resumable: 2 },
  rows: [
    { date: "04/11/2026 10:12", file: FILE.name, kind: "Catalogue", lines: 47, status: "En cours de vérification", tone: "accent" as const, model: `${MODEL_LABEL} · analyse terminée`, who: "A. Martin", resume: true },
    { date: "03/11/2026 14:05", file: "tarifs_novembre.csv", kind: "Prix du magasin", lines: 212, status: "Appliqué", tone: "ok" as const, model: "Règles seules", who: "A. Martin", resume: false },
    { date: "03/11/2026 11:40", file: "fournisseurs_2026.csv", kind: "Fournisseurs", lines: 18, status: "Appliqué", tone: "ok" as const, model: "llama3.2 (parcours actuel)", who: "C. Durand", resume: false },
    { date: "02/11/2026 16:22", file: "catalogue_essai.csv", kind: "Catalogue", lines: 8, status: "Contrôlé", tone: "accent" as const, model: "llama3.2 (parcours actuel)", who: "C. Durand", resume: true },
    { date: "31/10/2026 09:03", file: "montures_automne.csv", kind: "Catalogue", lines: 64, status: "Abandonné", tone: "arch" as const, model: "Assistant indisponible — règles seules", who: "A. Martin", resume: false },
  ],
};
