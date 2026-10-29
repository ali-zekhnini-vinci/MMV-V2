// BANC 04-A — garde réseau : dans le banc, `fetch` ne sort JAMAIS du processus. La seule requête servie est la
// recherche de clients du sélecteur réutilisé (appointments/ClientPicker.tsx → GET /api/clients), répondue en mémoire
// depuis les clients FICTIFS du banc, à une origine réservée et non résolvable (`.invalid`, RFC 6761). Toute autre
// requête est refusée sans être émise et comptée (preuve « aucun accès au serveur ni à mmv_v2_dev »).
import { type ClientDetail, type ClientSummary, CLIENT_MAX_PAGE_SIZE } from "../api/clients.ts";
import { findFolded } from "../clients/searchText.ts";

export const BENCH_API_ORIGIN = "https://banc-04a.invalid";

export interface BenchNetwork {
  /** Requêtes refusées sans émission (méthode et chemin seulement). */
  blocked: string[];
  /** Requêtes servies en mémoire (recherche de clients fictifs). */
  served: string[];
  restore: () => void;
}

const summaryOf = ({ id, reference, firstName, lastName, phone, email, city, status, createdAtUtc, version }: ClientDetail): ClientSummary => ({
  id,
  reference,
  firstName,
  lastName,
  phone,
  email,
  city,
  status,
  createdAtUtc,
  version,
});

function clientsPage(url: URL, clients: readonly ClientDetail[]): Response {
  const term = url.searchParams.get("q")?.trim() ?? "";
  const status = url.searchParams.get("status") ?? "active";
  const page = Math.max(1, Number(url.searchParams.get("page")) || 1);
  const pageSize = Math.min(CLIENT_MAX_PAGE_SIZE, Math.max(1, Number(url.searchParams.get("pageSize")) || 20));
  const words = term.split(/\s+/).filter((word) => [...word].length >= 2);
  const rows = clients
    .filter((candidate) => status === "all" || candidate.status === status)
    .filter((candidate) => {
      const haystack = [candidate.firstName, candidate.lastName, candidate.reference, candidate.email ?? "", candidate.phone ?? ""].join(" ");
      return words.every((word) => findFolded(haystack, word) !== null);
    })
    .sort((a, b) => a.lastName.localeCompare(b.lastName, "fr"))
    .map(summaryOf);
  const body = { items: rows.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: rows.length };
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" } });
}

export function installBenchNetwork(clients: () => readonly ClientDetail[]): BenchNetwork {
  const original = globalThis.fetch;
  const network: BenchNetwork = {
    blocked: [],
    served: [],
    restore: () => {
      globalThis.fetch = original;
    },
  };
  globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof Request ? input.url : String(input), BENCH_API_ORIGIN);
    const method = (init?.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    if (url.origin === BENCH_API_ORIGIN && url.pathname === "/api/clients" && method === "GET") {
      network.served.push(`${method} ${url.pathname}`);
      return clientsPage(url, clients());
    }
    network.blocked.push(`${method} ${url.origin === BENCH_API_ORIGIN ? url.pathname : url.origin + url.pathname}`);
    throw new TypeError("Banc 04-A isolé : aucun appel réseau n'est autorisé.");
  };
  return network;
}
