// BANC DE DÉMONSTRATION ET DE TEST 04-A — hôte ISOLÉ des prototypes du dossier optique (O-04-16 APPROVED).
// - Page distincte (optical-bench.html), servie par le serveur de développement Vite seulement : absente de
//   `vite build`, donc de l'application Tauri ; l'application réelle ne monte aucune route optique.
// - Aucune authentification réelle n'est contournée : le banc n'ouvre AUCUNE session MMV.Api et ne contacte aucun
//   serveur (src/bench/benchNetwork.ts refuse tout appel réseau). Les « droits » sont des profils SIMULÉS lus par le
//   seul double en mémoire ; ils ne correspondent à aucun compte ni à aucune permission existante.
// - Données : jeu EXPLICITEMENT FICTIF reconstruit en mémoire à chaque ouverture (src/bench/benchSeed.ts).
// - Comportements fondés sur des décisions PROPOSED (D-04-02 à D-04-14), étiquetés comme tels à l'écran.
import { QueryClientProvider } from "@tanstack/react-query";
import { Beaker, ClipboardList, FolderOpen, Home, Moon, RotateCcw, Sun, Users } from "lucide-react";
import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { MemoryRouter, Navigate, Route, Routes, useLocation, useNavigate, useParams } from "react-router";
import type { SessionInfo } from "../api/auth.ts";
import { apiError, type ApiResult } from "../api/client.ts";
import type { ClientDetail } from "../api/clients.ts";
import { createMmvQueryClient } from "../api/queryClient.ts";
import { AuthContext, type AuthContextValue } from "../auth/authContext.ts";
import { Button } from "../components/ui/button.tsx";
import { ClientDetailView } from "../clients/ClientDetailView.tsx";
import { clientDisplayName } from "../clients/clientForm.ts";
import { StateBlock } from "../clients/ClientStates.tsx";
import { type ClientNotice, ClientsUiContext, INITIAL_LIST_STATE, type ScopedClientNotice } from "../clients/clientsUi.ts";
import "../clients/Clients.css";
import { ClientOpticalTab } from "../optical/ClientOpticalTab.tsx";
import { OpticalAccessContext, opticalAccess, useOpticalAccess } from "../optical/opticalAccess.ts";
import { OpticalModule } from "../optical/OpticalModule.tsx";
import { OpticalGatewayContext } from "../optical/opticalQueries.ts";
import { DEMO_STEP, OpticalStepContext, stepLabel } from "../optical/opticalStep.ts";
import { kindFromSegment } from "../optical/opticalLabels.ts";
import { opticalDetailPath, readClientTab } from "../optical/opticalUi.ts";
import { INITIAL_OPTICAL_LIST, type OpticalListState, OpticalUiContext } from "../optical/opticalUiState.ts";
import { type LeaveGuard, NavigationGuardContext, type NavigationGuardRegistry } from "../shell/navigationGuard.ts";
import "../shell/Shell.css";
import { BENCH_API_ORIGIN, type BenchNetwork } from "./benchNetwork.ts";
import { BENCH_PROFILES, type BenchProfile, findProfile } from "./benchProfiles.ts";
import { BENCH_OTHER_WORKSTATION, createSeededGateway } from "./benchSeed.ts";
import type { BenchFaults, InMemoryOpticalGateway } from "./inMemoryOpticalGateway.ts";
import "./bench.css";

export interface OpticalBenchProps {
  clients: readonly ClientDetail[];
  network: BenchNetwork | null;
  initialRoute?: string;
  profileId?: string;
  theme?: "light" | "dark";
  latencyMs?: number;
  /** Pas de démonstration des boutons ± (D-04-15 ; aucune valeur approuvée, D-04-06). */
  step?: number;
}

/** Pannes et latence simulées, modifiées par les commandes du banc et lues par le double à chaque réponse. */
class BenchControl {
  readonly faults: BenchFaults;

  constructor(latencyMs: number) {
    this.faults = { latencyMs, failedListReads: 0, failNextWrite: false, nextConfirmFault: null };
  }

  setLatency(ms: number) {
    this.faults.latencyMs = ms;
  }

  armListFailure() {
    this.faults.failedListReads = 2;
  }

  armWriteFailure() {
    this.faults.failNextWrite = true;
  }

  armConfirmFault(fault: "network" | "conflict") {
    this.faults.nextConfirmFault = fault;
  }
}

interface BenchState {
  gateway: InMemoryOpticalGateway;
  generation: number;
}

/** Construit un double neuf et son jeu fictif (horloge de démonstration pendant la préparation seulement). */
function useBenchGateway(clients: readonly ClientDetail[], profileRef: { current: BenchProfile }, control: BenchControl) {
  const [state, setState] = useState<BenchState | null>(null);
  const [generation, setGeneration] = useState(0);
  const [failure, setFailure] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    createSeededGateway({
      clients,
      session: () => ({ actor: profileRef.current.actor, permissions: profileRef.current.permissions }),
      faults: () => control.faults,
    })
      .then((gateway) => !cancelled && setState({ gateway, generation }))
      .catch((error: unknown) => !cancelled && setFailure(error instanceof Error ? error.message : "Préparation impossible."));
    return () => {
      cancelled = true;
    };
  }, [clients, profileRef, control, generation]);

  return { state, failure, reset: () => setGeneration((value) => value + 1) };
}

export function OpticalBench({ clients, network, initialRoute = "/", profileId = "all", theme: initialTheme = "light", latencyMs = 0, step: initialStep = DEMO_STEP }: OpticalBenchProps) {
  const [profile, setProfile] = useState<BenchProfile>(() => findProfile(profileId));
  const profileRef = useRef(profile);
  // Mise à jour AVANT le rendu des écrans du nouveau profil (dans le gestionnaire, pas dans un effet) : sinon leurs
  // premières lectures partiraient encore avec les droits simulés de l'ancien profil.
  const changeProfile = useCallback((next: BenchProfile) => {
    profileRef.current = next;
    setProfile(next);
  }, []);
  const [control] = useState(() => new BenchControl(latencyMs));
  const [theme, setTheme] = useState(initialTheme);
  const [step, setStep] = useState(initialStep);
  const bench = useBenchGateway(clients, profileRef, control);

  useEffect(() => {
    const root = document.documentElement;
    root.dataset.theme = theme;
    return () => {
      delete root.dataset.theme;
    };
  }, [theme]);

  if (bench.failure) return <p role="alert">Banc 04-A : {bench.failure}</p>;
  if (bench.state === null) return <p role="status">Préparation du jeu de démonstration fictif…</p>;

  return (
    <MemoryRouter initialEntries={[initialRoute]}>
      <OpticalStepContext.Provider value={step}>
        <BenchProviders key={`${profile.id}-${bench.state.generation}`} gateway={bench.state.gateway} profile={profile}>
          <BenchLayout
            gateway={bench.state.gateway}
            clients={clients}
            profile={profile}
            onProfile={changeProfile}
            theme={theme}
            onTheme={() => setTheme((value) => (value === "dark" ? "light" : "dark"))}
            control={control}
            network={network}
            onReset={bench.reset}
            step={step}
            onStep={setStep}
          />
        </BenchProviders>
      </OpticalStepContext.Provider>
    </MemoryRouter>
  );
}

const BENCH_SESSION_EXPIRY = "2099-12-31T23:00:00Z";

/** Contextes attendus par les écrans réutilisés (fiche client, sélecteur de client) et par le module optique. */
function BenchProviders({ gateway, profile, children }: { gateway: InMemoryOpticalGateway; profile: BenchProfile; children: ReactNode }) {
  // Cache propre au profil simulé : aucun résultat ne passe d'un profil à l'autre (changement = nouveau cache).
  const [queryClient] = useState(createMmvQueryClient);
  useEffect(() => () => queryClient.clear(), [queryClient]);
  const { pathname } = useLocation();
  const [list, setListState] = useState<OpticalListState>(INITIAL_OPTICAL_LIST);
  const [scoped, setScoped] = useState<ScopedClientNotice | null>(null);
  const [shownPath, setShownPath] = useState(pathname);
  // Même règle que ClientsModule (O-03-15 / O-03-43) : un message n'appartient qu'à l'écran visé.
  if (shownPath !== pathname) {
    setShownPath(pathname);
    if (scoped !== null && scoped.path !== pathname) setScoped(null);
  }
  const notice = scoped !== null && scoped.path === pathname ? scoped : null;
  const setNotice = useCallback((next: ClientNotice | null, path?: string) => setScoped(next === null ? null : { ...next, path: path ?? pathname }), [pathname]);
  const clientsUi = useMemo(() => ({ listState: INITIAL_LIST_STATE, setListState: () => undefined, notice, setNotice }), [notice, setNotice]);
  const opticalUi = useMemo(() => ({ list, setList: (update: (current: OpticalListState) => OpticalListState) => setListState(update) }), [list]);
  const access = useMemo(() => opticalAccess(profile.permissions), [profile]);

  // Session SIMULÉE pour les écrans Clients réutilisés : `clients.read` seulement (aucune écriture client possible),
  // jeton fictif, origine `.invalid` ; seule la recherche de clients fictifs est servie, en mémoire (benchNetwork.ts).
  const auth = useMemo<AuthContextValue>(() => {
    const session: SessionInfo = {
      expiresAtUtc: BENCH_SESSION_EXPIRY,
      idleTimeoutSeconds: 3600,
      user: { id: profile.actor.id, userName: "banc-04a-fictif", displayName: profile.actor.displayName, permissions: profile.permissions.filter((permission) => permission === "clients.read") },
    };
    const url = new URL(BENCH_API_ORIGIN);
    return {
      state: { status: "authenticated", session },
      baseUrl: { ok: true, url },
      signIn: async () => apiError("not-configured"),
      signOut: async () => undefined,
      authorized: <T,>(call: (baseUrl: URL, token: string) => Promise<ApiResult<T>>) => call(url, "jeton-fictif-banc-04a"),
      refreshSession: async () => ({ ok: true, status: 200, data: session }),
    };
  }, [profile]);

  return (
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={auth}>
        <ClientsUiContext.Provider value={clientsUi}>
          <OpticalGatewayContext.Provider value={gateway}>
            <OpticalAccessContext.Provider value={access}>
              <OpticalUiContext.Provider value={opticalUi}>{children}</OpticalUiContext.Provider>
            </OpticalAccessContext.Provider>
          </OpticalGatewayContext.Provider>
        </ClientsUiContext.Provider>
      </AuthContext.Provider>
    </QueryClientProvider>
  );
}

interface BenchLayoutProps {
  gateway: InMemoryOpticalGateway;
  clients: readonly ClientDetail[];
  profile: BenchProfile;
  onProfile: (profile: BenchProfile) => void;
  theme: "light" | "dark";
  onTheme: () => void;
  control: BenchControl;
  network: BenchNetwork | null;
  onReset: () => void;
  step: number;
  onStep: (step: number) => void;
}

const DETAIL_PATH = /^\/optique\/(ordonnance|mesure)\/([^/]+)/;

function BenchLayout({ gateway, clients, profile, onProfile, theme, onTheme, control, network, onReset, step, onStep }: BenchLayoutProps) {
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [message, setMessage] = useState<string | null>(null);
  const [latency, setLatency] = useState(control.faults.latencyMs);

  // Garde de sortie (formulaire modifié), comme le Shell : consultée avant chaque navigation du banc.
  const leaveGuard = useRef<LeaveGuard | null>(null);
  const registry = useMemo<NavigationGuardRegistry>(
    () => ({
      register: (guard) => {
        leaveGuard.current = guard;
        return () => {
          if (leaveGuard.current === guard) leaveGuard.current = null;
        };
      },
    }),
    [],
  );
  const go = (path: string) => {
    const guard = leaveGuard.current;
    if (guard !== null && guard(() => navigate(path))) return;
    navigate(path);
  };

  const match = DETAIL_PATH.exec(pathname);
  const current = match ? { kind: kindFromSegment(match[1]), id: match[2] } : null;
  const otherWorkstation = () => {
    if (!current?.kind) return;
    gateway.simulateOtherWorkstation(current.kind, current.id, BENCH_OTHER_WORKSTATION);
    setMessage(`Écriture simulée depuis ${BENCH_OTHER_WORKSTATION.displayName} : la version ouverte à l'écran est désormais périmée (409 au prochain enregistrement).`);
  };

  const nav = [
    { path: "/", label: "Accueil du banc", icon: Home, active: pathname === "/" },
    { path: "/clients", label: "Clients fictifs", icon: Users, active: pathname.startsWith("/clients") },
    { path: "/optique", label: "Dossiers optiques", icon: FolderOpen, active: pathname.startsWith("/optique") },
  ];

  return (
    <div className="bench">
      <div className="bench__banner" role="note" aria-label="Banc de démonstration">
        <Beaker size={18} aria-hidden="true" />
        <span>
          <b>BANC DE DÉMONSTRATION ET DE TEST 04-A — PROTOTYPE.</b> Données fictives en mémoire · aucun serveur, aucune base, aucun appel réseau · droits
          simulés · décisions D-04-02 à D-04-14 <b>PROPOSED</b>. L&apos;application réelle conserve « Ordonnances — phase ultérieure ».
        </span>
      </div>
      <aside className="bench__side" aria-label="Commandes du banc">
        <nav aria-label="Navigation du banc" className="bench__nav">
          {nav.map((item) => (
            <button key={item.path} type="button" className="bench__navitem" aria-current={item.active ? "page" : undefined} onClick={() => go(item.path)}>
              <item.icon size={16} aria-hidden="true" />
              {item.label}
            </button>
          ))}
        </nav>
        <div className="bench__controls">
          <label className="clients-field__label" htmlFor="bench-profile">
            Profil simulé (affichage seulement)
          </label>
          <div className="clients-input">
            <select id="bench-profile" className="optical-select" value={profile.id} onChange={(event) => onProfile(findProfile(event.target.value))}>
              {BENCH_PROFILES.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <p className="bench__hint">Matrice indicative D-04-13.c, non validée. Aucun droit réel.</p>
          <label className="clients-field__label" htmlFor="bench-latency">
            Latence simulée
          </label>
          <div className="clients-input">
            <select
              id="bench-latency"
              className="optical-select"
              value={latency}
              onChange={(event) => {
                const ms = Number(event.target.value);
                control.setLatency(ms);
                setLatency(ms);
              }}
            >
              <option value={0}>Aucune</option>
              <option value={800}>0,8 s (chargement, enregistrement)</option>
              <option value={3000}>3 s</option>
            </select>
          </div>
          <label className="clients-field__label" htmlFor="bench-step">
            Pas des boutons ± (démonstration)
          </label>
          <div className="clients-input">
            <select id="bench-step" className="optical-select" value={step} onChange={(event) => onStep(Number(event.target.value))}>
              {[...new Set([0.125, 0.25, 0.5, step])].sort((a, b) => a - b).map((value) => (
                <option key={value} value={value}>
                  {stepLabel(value)}
                </option>
              ))}
            </select>
          </div>
          <p className="bench__hint">D-04-15 : référence UX Lot 3 (APPROVED). Le pas reste un paramètre de démonstration : D-04-06 ouverte.</p>
          <Button
            size="sm"
            onClick={() => {
              control.armListFailure();
              setMessage("La prochaine lecture de liste échouera (erreur technique simulée).");
            }}
          >
            Échec de la prochaine liste
          </Button>
          <Button
            size="sm"
            onClick={() => {
              control.armWriteFailure();
              setMessage("La prochaine écriture échouera (erreur technique simulée).");
            }}
          >
            Échec de la prochaine écriture
          </Button>
          <Button
            size="sm"
            onClick={() => {
              control.armConfirmFault("network");
              setMessage("La prochaine confirmation échouera (erreur de communication simulée) ; l'enregistrement du brouillon qui la précède aboutira.");
            }}
          >
            Échec de la prochaine confirmation
          </Button>
          <Button
            size="sm"
            onClick={() => {
              control.armConfirmFault("conflict");
              setMessage("La prochaine confirmation sera refusée (conflit 409 simulé) ; l'enregistrement du brouillon qui la précède aboutira.");
            }}
          >
            Conflit à la prochaine confirmation
          </Button>
          <Button size="sm" disabled={!current?.kind} onClick={otherWorkstation} title="Disponible sur le détail ou le brouillon d'un enregistrement">
            Autre poste : modifier l&apos;enregistrement ouvert
          </Button>
          <Button size="sm" onClick={onTheme}>
            {theme === "dark" ? <Sun size={14} aria-hidden="true" /> : <Moon size={14} aria-hidden="true" />}
            Thème {theme === "dark" ? "clair" : "sombre"}
          </Button>
          <Button size="sm" variant="danger" onClick={onReset}>
            <RotateCcw size={14} aria-hidden="true" />
            Réinitialiser le jeu fictif
          </Button>
          <p className="bench__hint" role="status">
            Réseau : {network ? `${network.blocked.length} appel(s) bloqué(s) · ${network.served.length} recherche(s) de clients fictifs servie(s) en mémoire` : "garde non installée"}
          </p>
          {message && (
            <p className="bench__message" role="status">
              {message}
            </p>
          )}
        </div>
      </aside>
      <main className="bench__content">
        <NavigationGuardContext.Provider value={registry}>
          <Routes>
            <Route path="/" element={<BenchHome gateway={gateway} clients={clients} />} />
            <Route path="/clients" element={<BenchClientList clients={clients} />} />
            <Route path="/clients/:clientId" element={<BenchClientCard clients={clients} />} />
            <Route path="/optique/*" element={<OpticalModule />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </NavigationGuardContext.Provider>
      </main>
    </div>
  );
}

function BenchHome({ gateway, clients }: { gateway: InMemoryOpticalGateway; clients: readonly ClientDetail[] }) {
  const navigate = useNavigate();
  const openRef = (reference: string) => {
    const found = gateway.findByReference(reference);
    if (found) navigate(opticalDetailPath(found.kind, found.id));
  };
  const card = (id: string) => navigate(`/clients/${id}`, { state: { clientTab: "optical" } });
  const name = (id: string) => {
    const client = clients.find((candidate) => candidate.id === id);
    return client ? clientDisplayName(client) : id;
  };
  const scenarios: { label: string; detail: string; run: () => void }[] = [
    { label: `Fiche ${name("cli-fictif-1")} → Dossier optique`, detail: "J-04-01, état 5, GAP-04-04 : deux sections distinctes, ajout avec choix du type", run: () => card("cli-fictif-1") },
    { label: `Fiche ${name("cli-fictif-3")} (aucun enregistrement)`, detail: "État 7 : vide par section", run: () => card("cli-fictif-3") },
    { label: `Fiche ${name("cli-fictif-4")} (client archivé)`, detail: "État 18 : consultation seule, ajout et rectification refusés", run: () => card("cli-fictif-4") },
    { label: "Liste globale", detail: "J-04-02, états 1–4, 6, 8, 9 ; « Nouveau… » → type → client actif", run: () => navigate("/optique") },
    { label: "RX-FICTIF-0001 — révisions 1 et 2", detail: "GAP-04-07 : comparaison des valeurs, motif et auteurs", run: () => openRef("RX-FICTIF-0001") },
    { label: "RX-FICTIF-0002 — rectification en cours", detail: "GAP-04-13 : dernière révision confirmée (1) affichée malgré le brouillon", run: () => openRef("RX-FICTIF-0002") },
    { label: "RX-FICTIF-0003 — invalidée (doublon)", detail: "GAP-04-08, état 30", run: () => openRef("RX-FICTIF-0003") },
    { label: "RX-FICTIF-0004 — brouillon partiel", detail: "États 16, 21 : OG laissé vide ; confirmable (un œil suffit pour la règle de démonstration D-04-17)", run: () => openRef("RX-FICTIF-0004") },
    { label: "MES-FICTIF-0001 — mesure magasin confirmée", detail: "GAP-04-03 : provenance propre, jamais une ordonnance", run: () => openRef("MES-FICTIF-0001") },
    {
      label: `Saisie forcée pour ${name("cli-fictif-4")} (archivé)`,
      detail: "Démonstration du refus du service (409 client_archived), écran inaccessible normalement",
      run: () =>
        navigate("/optique/nouveau/ordonnance", {
          state: { returnTo: "/clients/cli-fictif-4", returnLabel: name("cli-fictif-4"), client: { id: "cli-fictif-4", label: `${name("cli-fictif-4")} · CLI-FICTIF-004` } },
        }),
    },
  ];
  return (
    <section className="clients-view" aria-labelledby="bench-title">
      <header className="clients-head">
        <div>
          <h1 id="bench-title" className="clients-head__title">
            Banc 04-A — dossier optique (prototype)
          </h1>
          <p className="clients-head__sub">Parcours de démonstration sur données fictives. Couverture des états : docs/phase/04/STATE_COVERAGE_04A.md.</p>
        </div>
      </header>
      <ul className="bench__scenarios">
        {scenarios.map((scenario) => (
          <li key={scenario.label}>
            <Button variant="tertiary" onClick={scenario.run}>
              <ClipboardList size={16} aria-hidden="true" />
              {scenario.label}
            </Button>
            <span className="clients-muted">{scenario.detail}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function BenchClientList({ clients }: { clients: readonly ClientDetail[] }) {
  const navigate = useNavigate();
  return (
    <section className="clients-view" aria-labelledby="bench-clients-title">
      <header className="clients-head">
        <div>
          <h1 id="bench-clients-title" className="clients-head__title">
            Clients fictifs du banc
          </h1>
          <p className="clients-head__sub">Aucune donnée réelle : fiches reconstruites en mémoire à chaque ouverture.</p>
        </div>
      </header>
      <div className="clients-panel">
        <ul className="bench__clients">
          {clients.map((client) => (
            <li key={client.id}>
              <Button variant="tertiary" onClick={() => navigate(`/clients/${client.id}`)}>
                {clientDisplayName(client)}
              </Button>
              <span className="clients-cell__ref">
                {client.reference} · {client.status === "archived" ? "archivé" : "actif"}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

function BenchClientCard({ clients }: { clients: readonly ClientDetail[] }) {
  const { clientId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const client = clients.find((candidate) => candidate.id === clientId);
  return client ? (
    <BenchCard key={`${client.id}-${location.key}`} client={client} initialTab={readClientTab(location.state)} />
  ) : (
    <StateBlock tone="info" icon={Users} title="Client fictif introuvable" action={<Button onClick={() => navigate("/clients")}>Retour à la liste</Button>}>
      <p>Ce client n&apos;existe pas dans le jeu de démonstration.</p>
    </StateBlock>
  );
}

function BenchCard({ client, initialTab }: { client: ClientDetail; initialTab: string | undefined }) {
  const access = useOpticalAccess();
  // Sans `optical.read` simulé : l'onglet reporté actuel est conservé, comme pour tous les comptes réels (O-04-16).
  return (
    <ClientDetailView
      client={client}
      initialTab={initialTab}
      opticalTab={access.canRead ? <ClientOpticalTab client={client} clientName={clientDisplayName(client)} /> : undefined}
    />
  );
}
