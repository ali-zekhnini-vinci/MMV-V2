// Client temps réel (COR-05-02) : connexion SignalR au hub /hubs/changes de l'API locale, sans dépendance React.
// Le serveur n'envoie que des notifications « tel sujet a changé » APRÈS une écriture confirmée (jamais de prix, de
// coût ni de quantité) : le poste relit l'API, qui reste seule juge des droits. Règles :
// - reconnexion automatique sans fin (0, 1, 2, 5, 10 s puis toutes les 15 s) ; au-delà de 20 s sans connexion, l'état
//   devient « déconnecté » (données potentiellement obsolètes) tout en continuant d'essayer ;
// - resynchronisation complète après chaque reconnexion et après un redémarrage du serveur (identifiant d'instance
//   différent dans le message d'accueil) : les événements manqués ne sont jamais rejoués, l'état est relu ;
// - un événement d'un numéro déjà vu (même instance) est ignoré ; l'ordre d'arrivée importe peu, chaque événement
//   ne déclenche qu'une relecture.
import type { HubConnection } from "@microsoft/signalr";

export type RealtimeStatus = "connecting" | "connected" | "reconnecting" | "disconnected";

export interface ChangeEvent {
  seq: number;
  server: string;
  topics: string[];
  kind: string;
  ids: Record<string, string>;
  atUtc: string;
}

export interface RealtimeHandlers {
  onChange: (change: ChangeEvent) => void;
  /** Relecture complète : `reconnected` (événements possiblement manqués) ou `server-restarted`. */
  onResync: (reason: "reconnected" | "server-restarted") => void;
  onStatus: (status: RealtimeStatus) => void;
}

export interface RealtimeOptions extends RealtimeHandlers {
  hubUrl: string;
  /** Jeton de session courant (relu à chaque tentative de connexion). */
  token: () => Promise<string>;
}

export const RETRY_DELAYS_MS = [0, 1000, 2000, 5000, 10000] as const;
export const STEADY_RETRY_MS = 15000;
export const DISCONNECTED_AFTER_MS = 20000;

export function retryDelay(previousRetryCount: number): number {
  return RETRY_DELAYS_MS[previousRetryCount] ?? STEADY_RETRY_MS;
}

/** Démarre la connexion ; la fonction renvoyée l'arrête définitivement. */
export function startRealtime(options: RealtimeOptions): () => void {
  let stopped = false;
  let connection: HubConnection | null = null;
  let server: string | null = null;
  let lastSeq = 0;
  let downTimer: number | undefined;
  let restartTimer: number | undefined;
  let attempts = 0;

  const status = (value: RealtimeStatus) => {
    if (!stopped) options.onStatus(value);
  };
  const markDown = () => {
    window.clearTimeout(downTimer);
    downTimer = window.setTimeout(() => status("disconnected"), DISCONNECTED_AFTER_MS);
  };
  const markUp = () => {
    window.clearTimeout(downTimer);
    attempts = 0;
    status("connected");
  };

  const connect = async () => {
    const signalr = await import("@microsoft/signalr");
    if (stopped) return;
    const built = new signalr.HubConnectionBuilder()
      .withUrl(options.hubUrl, { accessTokenFactory: options.token, withCredentials: false })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount) })
      .configureLogging(signalr.LogLevel.None)
      .build();
    connection = built;
    built.on("hello", (hello: { server: string; seq: number }) => {
      if (server !== null && hello.server !== server) {
        lastSeq = 0;
        options.onResync("server-restarted");
      }
      server = hello.server;
    });
    built.on("changed", (change: ChangeEvent) => {
      if (change.server === server && change.seq <= lastSeq) return;
      server = change.server;
      lastSeq = Math.max(lastSeq, change.seq);
      options.onChange(change);
    });
    built.onreconnecting(() => {
      status("reconnecting");
      markDown();
    });
    built.onreconnected(() => {
      markUp();
      options.onResync("reconnected");
    });
    built.onclose(() => {
      if (stopped) return;
      status("reconnecting");
      markDown();
      scheduleRestart();
    });
    try {
      await built.start();
      if (stopped) {
        void built.stop();
        return;
      }
      const wasRetry = attempts > 0;
      markUp();
      if (wasRetry) options.onResync("reconnected");
    } catch {
      if (stopped) return;
      status(attempts === 0 ? "connecting" : "reconnecting");
      markDown();
      scheduleRestart();
    }
  };

  const scheduleRestart = () => {
    window.clearTimeout(restartTimer);
    const delay = retryDelay(attempts);
    attempts += 1;
    restartTimer = window.setTimeout(() => void connect(), Math.max(delay, 1000));
  };

  status("connecting");
  markDown();
  void connect();

  return () => {
    stopped = true;
    window.clearTimeout(downTimer);
    window.clearTimeout(restartTimer);
    void connection?.stop();
  };
}
