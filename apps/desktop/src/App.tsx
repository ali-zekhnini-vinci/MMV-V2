// Feuilles globales en premier : Tailwind (couches), polices et jetons MMV, puis base de l'application.
import "./styles/index.css";
import "./App.css";
import { useAuth } from "./auth/authContext.ts";
import { AuthProvider } from "./auth/AuthProvider.tsx";
import { LoginScreen } from "./login/LoginScreen.tsx";
import { Shell } from "./shell/Shell.tsx";

// Porte d'entrée (phase 02) : écran de connexion tant qu'aucune session n'est ouverte en mémoire,
// shell ensuite. Aucune session ne survit à la fermeture ou au rechargement de la fenêtre.
export function Gate() {
  const { state } = useAuth();
  return state.status === "authenticated" ? <Shell session={state.session} /> : <LoginScreen />;
}

function App() {
  return (
    <AuthProvider>
      <Gate />
    </AuthProvider>
  );
}

export default App;
