// Écran de connexion (référence visuelle DRAFT : MMV-Login-1920x1080.png, MMV-Login-handoff.md,
// MMV-Login-states.md). Aucune inscription, aucun « mot de passe oublié », aucune mémorisation.
import { AlertCircle, ArrowRight, Check, Eye, EyeOff, Lock, ShieldCheck, User, WifiOff } from "lucide-react";
import { type FormEvent, useEffect, useId, useRef, useState } from "react";
import logoLogin from "../assets/brand/logo-login.png";
import { useAuth } from "../auth/authContext.ts";
import { BANNERS, bannerForLoginError, type LoginBanner, type LoginFieldErrors, validateLogin } from "./loginForm.ts";
import "./LoginScreen.css";

const ARGUMENTS = [
  "Dossiers clients & ordonnances",
  "Ventes, commandes & atelier",
  "Stock et fournisseurs centralisés",
] as const;

export function LoginScreen() {
  const { state, baseUrl, signIn } = useAuth();
  const notice = state.status === "anonymous" ? state.notice : null;

  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<LoginFieldErrors>({});
  const [banner, setBanner] = useState<LoginBanner | null>(() =>
    !baseUrl.ok ? BANNERS["not-configured"] : notice === "session-expired" ? BANNERS["session-expired"] : null,
  );

  const userNameRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);
  const ids = useId();
  const userNameErrorId = `${ids}-user-error`;
  const passwordErrorId = `${ids}-password-error`;
  const bannerId = `${ids}-banner`;

  useEffect(() => {
    userNameRef.current?.focus();
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) return;

    const errors = validateLogin(userName, password);
    setFieldErrors(errors);
    if (errors.userName || errors.password) {
      setBanner(null);
      (errors.userName ? userNameRef : passwordRef).current?.focus();
      return;
    }

    setBanner(null);
    setSubmitting(true);
    const error = await signIn(userName.trim(), password);
    if (error === null) return; // l'application bascule sur le shell ; ce composant est démonté.

    setSubmitting(false);
    setBanner(bannerForLoginError(error));
    // Identifiant conservé, mot de passe vidé (MMV-Login-states §13, variante recommandée).
    setPassword("");
    window.setTimeout(() => passwordRef.current?.focus(), 0);
  }

  const markBoth = banner?.markFields === true;
  const userNameInvalid = Boolean(fieldErrors.userName) || markBoth;
  const passwordInvalid = Boolean(fieldErrors.password) || markBoth;
  const describedBy = (fieldErrorId: string, hasFieldError: boolean) =>
    [hasFieldError ? fieldErrorId : null, banner ? bannerId : null].filter(Boolean).join(" ") || undefined;

  return (
    <div className="login">
      <section className="login__brand" aria-label="MMV — ManageMyVision">
        <div className="login__glow" aria-hidden="true" />
        <div className="login__brand-content">
          <img className="login__logo" src={logoLogin} width={300} height={132} alt="MMV — ManageMyVision" />
          <p className="login__headline">La gestion intégrée de votre magasin d&apos;optique.</p>
          <ul className="login__arguments">
            {ARGUMENTS.map((argument) => (
              <li key={argument}>
                <span className="login__pastille" aria-hidden="true">
                  <Check size={16} strokeWidth={2.5} />
                </span>
                {argument}
              </li>
            ))}
          </ul>
        </div>
      </section>

      <main className="login__panel">
        <form className="login__form" onSubmit={(event) => void handleSubmit(event)} noValidate aria-busy={submitting}>
          <header>
            <h1 className="login__title">Bienvenue,</h1>
            <p className="login__subtitle">Veuillez entrer vos identifiants pour continuer.</p>
          </header>

          {banner && (
            <div id={bannerId} className="login__banner" role="alert">
              {banner.kind === "technical" || banner.kind === "not-configured" ? (
                <WifiOff size={16} aria-hidden="true" />
              ) : (
                <AlertCircle size={16} aria-hidden="true" />
              )}
              <span>{banner.message}</span>
            </div>
          )}

          <div className="login__field">
            <label htmlFor={`${ids}-user`}>Identifiant</label>
            <div className={`login__input${userNameInvalid ? " login__input--invalid" : ""}`}>
              <User size={18} aria-hidden="true" />
              <input
                ref={userNameRef}
                id={`${ids}-user`}
                name="username"
                type="text"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                value={userName}
                readOnly={submitting}
                aria-invalid={userNameInvalid}
                aria-describedby={describedBy(userNameErrorId, Boolean(fieldErrors.userName))}
                onChange={(event) => {
                  setUserName(event.target.value);
                  if (fieldErrors.userName) setFieldErrors((current) => ({ ...current, userName: undefined }));
                }}
              />
            </div>
            {fieldErrors.userName && (
              <p id={userNameErrorId} className="login__field-error">
                {fieldErrors.userName}
              </p>
            )}
          </div>

          <div className="login__field">
            <label htmlFor={`${ids}-password`}>Mot de passe</label>
            <div className={`login__input${passwordInvalid ? " login__input--invalid" : ""}`}>
              <Lock size={18} aria-hidden="true" />
              <input
                ref={passwordRef}
                id={`${ids}-password`}
                name="password"
                type={showPassword ? "text" : "password"}
                className={showPassword ? undefined : "login__secret"}
                autoComplete="current-password"
                value={password}
                readOnly={submitting}
                aria-invalid={passwordInvalid}
                aria-describedby={describedBy(passwordErrorId, Boolean(fieldErrors.password))}
                onChange={(event) => {
                  setPassword(event.target.value);
                  if (fieldErrors.password) setFieldErrors((current) => ({ ...current, password: undefined }));
                }}
              />
              <button
                type="button"
                className="login__reveal"
                aria-label={showPassword ? "Masquer le mot de passe" : "Afficher le mot de passe"}
                aria-pressed={showPassword}
                disabled={submitting}
                onClick={() => setShowPassword((visible) => !visible)}
              >
                {showPassword ? <EyeOff size={18} aria-hidden="true" /> : <Eye size={18} aria-hidden="true" />}
              </button>
            </div>
            {fieldErrors.password && (
              <p id={passwordErrorId} className="login__field-error">
                {fieldErrors.password}
              </p>
            )}
          </div>

          <button type="submit" className="login__submit" disabled={submitting || !baseUrl.ok}>
            {submitting ? (
              <>
                <span className="login__spinner" aria-hidden="true" />
                Connexion…
              </>
            ) : (
              <>
                Se connecter
                <ArrowRight size={16} aria-hidden="true" />
              </>
            )}
          </button>

          <p className="login__secure">
            <ShieldCheck size={16} aria-hidden="true" />
            Connexion sécurisée à votre espace MMV.
          </p>
        </form>
      </main>
    </div>
  );
}
