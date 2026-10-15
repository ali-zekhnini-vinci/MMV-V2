// Accueil sobre : aucune statistique, aucun chiffre ni donnée métier (les cinq tableaux de bord du
// Lot 1 sont des références DRAFT reportées au backlog).
import type { SessionUser } from "../api/auth.ts";
import { roleLabel } from "../shell/navigation.ts";

export function HomeView({ user }: { user: SessionUser }) {
  return (
    <section className="view" aria-labelledby="home-title">
      <header className="view__header">
        <h1 id="home-title" className="view__title">
          Bonjour, {user.displayName}
        </h1>
        <p className="view__lead">Votre session MMV est ouverte.</p>
      </header>

      <div className="card">
        <h2 className="card__title">Votre accès</h2>
        <dl className="facts">
          <div>
            <dt>Identifiant</dt>
            <dd>{user.userName}</dd>
          </div>
          <div>
            <dt>Rôle technique</dt>
            <dd>{roleLabel(user.permissions)}</dd>
          </div>
        </dl>
        <p className="view__muted">
          Les modules métier (clients, rendez-vous, ordonnances, ventes, stock…) seront ajoutés dans les phases
          suivantes. Les entrées correspondantes du menu l&apos;indiquent.
        </p>
      </div>
    </section>
  );
}
