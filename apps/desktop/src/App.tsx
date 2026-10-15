import "./App.css";

// Écran de socle technique (phase 01A) : aucune donnée de magasin, aucun appel API,
// aucun écran Login/Shell. Il prouve uniquement que la fenêtre Tauri charge l'UI React.
function App() {
  return (
    <main className="socle">
      <header className="socle__header">
        <h1 className="socle__title">MMV V2</h1>
        <span className="socle__badge">Socle technique</span>
      </header>

      <p className="socle__lead">
        Environnement de développement de la phase 01A. Cette fenêtre vérifie
        uniquement le chargement de l'interface dans le client Windows.
      </p>

      <dl className="socle__facts">
        <div>
          <dt>Connexion au serveur MMV</dt>
          <dd>Non configurée dans cette phase</dd>
        </div>
        <div>
          <dt>Données</dt>
          <dd>Aucune donnée affichée</dd>
        </div>
      </dl>
    </main>
  );
}

export default App;
