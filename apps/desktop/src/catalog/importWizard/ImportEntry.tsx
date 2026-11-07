// Écran « Import CSV » : nouvel assistant en cinq étapes (CSV-IMPORT-AI-01) SEULEMENT si le serveur l'active
// (`CatalogInterpreter:WizardEnabled`, après application de 0009) ; sinon le parcours actuel, inchangé, reste servi.
// L'activation est une décision du serveur : le poste ne la devine jamais.
import { useEffect, useState } from "react";
import { ImportView } from "../ImportView.tsx";
import { useAssistantApi } from "./assistantApi.ts";
import { ImportWizard } from "./ImportWizard.tsx";

export function ImportEntry() {
  const api = useAssistantApi();
  const [enabled, setEnabled] = useState<boolean | null>(null);
  useEffect(() => {
    let alive = true;
    void api.status().then((answer) => { if (alive) setEnabled(answer.ok && answer.data.wizardEnabled); });
    return () => { alive = false; };
    // Une seule lecture à l'ouverture de l'écran.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  if (enabled === null) return <p className="clients-muted" role="status">Chargement de l&apos;import…</p>;
  return enabled ? <ImportWizard /> : <ImportView />;
}
