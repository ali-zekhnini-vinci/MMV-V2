import { useAuth } from "./auth/authContext.ts";
import { AuthProvider } from "./auth/AuthProvider.tsx";
import { LoginScreen } from "./login/LoginScreen.tsx";
import { Shell } from "./shell/Shell.tsx";
import "./styles/tokens.css";
import "./App.css";

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
