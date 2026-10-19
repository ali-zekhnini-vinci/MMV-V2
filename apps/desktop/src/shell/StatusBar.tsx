// Barre d'état 32 px — MMV-Shell-handoff §8, états §6 : valeurs réelles uniquement (session ouverte,
// /health/live, /health/ready, version du paquet client). Informative, non focalisable.
import { Database, LoaderCircle, MonitorCheck, Server } from "lucide-react";
import type { ReactNode } from "react";
import type { TechnicalStatus } from "../api/health.ts";
import { compactIndicatorText, databaseIndicator, type Indicator, serverIndicator, sessionIndicator } from "./statusBarView.ts";

interface StatusBarProps {
  expiresAtUtc: string;
  health: TechnicalStatus | null;
  checking: boolean;
}

function Item({ icon, indicator }: { icon: ReactNode; indicator: Indicator }) {
  return (
    <span className={`statusbar__item statusbar__item--${indicator.tone}`} title={indicator.text}>
      {icon}
      <span className="statusbar__long">{indicator.text}</span>
      <span className="statusbar__short" aria-hidden="true">
        {compactIndicatorText(indicator.text)}
      </span>
    </span>
  );
}

export function StatusBar({ expiresAtUtc, health, checking }: StatusBarProps) {
  const server = serverIndicator(health, checking);
  return (
    <footer className="statusbar" aria-label="Barre d'état">
      <div role="status" aria-live="polite" className="statusbar__items">
        <Item icon={<MonitorCheck size={14} aria-hidden="true" />} indicator={sessionIndicator(expiresAtUtc)} />
        <Item
          icon={
            checking ? (
              <LoaderCircle size={14} aria-hidden="true" className="statusbar__spin" />
            ) : (
              <Server size={14} aria-hidden="true" />
            )
          }
          indicator={server}
        />
        <Item icon={<Database size={14} aria-hidden="true" />} indicator={databaseIndicator(health)} />
      </div>
      <span className="statusbar__product">MMV — ManageMyVision · client {__APP_VERSION__}</span>
    </footer>
  );
}
