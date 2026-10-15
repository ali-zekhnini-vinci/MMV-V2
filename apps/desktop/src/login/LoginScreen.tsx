// Écran de connexion (référence visuelle DRAFT : MMV-Login-1920x1080.png, MMV-Login-handoff.md,
// MMV-Login-states.md). Aucune inscription, aucun « mot de passe oublié », aucune mémorisation.
// Formulaire : React Hook Form + schéma Zod (loginForm.ts) ; étiquettes et bouton : shadcn/ui adaptés MMV.
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, ArrowRight, Check, Eye, EyeOff, Lock, ShieldCheck, User, WifiOff } from "lucide-react";
import { type FormEvent, useEffect, useId, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import logoLogin from "../assets/brand/logo-login.png";
import { useAuth } from "../auth/authContext.ts";
import { Button } from "../components/ui/button.tsx";
import { Label } from "../components/ui/label.tsx";
import { BANNERS, bannerForLoginError, type LoginBanner, type LoginFormInput, type LoginFormValues, loginSchema } from "./loginForm.ts";
import "./LoginScreen.css";

const ARGUMENTS = [
  "Dossiers clients & ordonnances",
  "Ventes, commandes & atelier",
  "Stock et fournisseurs centralisés",
] as const;

export function LoginScreen() {
  const { state, baseUrl, signIn } = useAuth();
  const notice = state.status === "anonymous" ? state.notice : null;

  const {
    register,
    handleSubmit,
    setValue,
    setFocus,
    formState: { errors, isSubmitting: submitting },
  } = useForm<LoginFormInput, unknown, LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { userName: "", password: "" },
    // Saisie invalide : focus sur le premier champ en erreur (Identifiant, sinon Mot de passe).
    shouldFocusError: true,
  });
  const [showPassword, setShowPassword] = useState(false);
  const [banner, setBanner] = useState<LoginBanner | null>(() =>
    !baseUrl.ok ? BANNERS["not-configured"] : notice === "session-expired" ? BANNERS["session-expired"] : null,
  );

  const ids = useId();
  const userNameErrorId = `${ids}-user-error`;
  const passwordErrorId = `${ids}-password-error`;
  const bannerId = `${ids}-banner`;
  const fieldErrors = { userName: errors.userName?.message, password: errors.password?.message };

  // Focus initial synchrone sur Identifiant (setFocus de React Hook Form est différé) : référence locale
  // fusionnée avec celle de register.
  const userNameField = register("userName");
  const userNameRef = useRef<HTMLInputElement | null>(null);
  useEffect(() => {
    userNameRef.current?.focus();
  }, []);

  async function onValid({ userName, password }: LoginFormValues) {
    setBanner(null);
    const error = await signIn(userName, password);
    if (error === null) return; // l'application bascule sur le shell ; ce composant est démonté.

    setBanner(bannerForLoginError(error));
    // Identifiant conservé, mot de passe vidé (MMV-Login-states §13, variante recommandée).
    setValue("password", "");
    window.setTimeout(() => setFocus("password"), 0);
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    if (submitting) {
      event.preventDefault();
      return;
    }
    void handleSubmit(onValid, () => setBanner(null))(event);
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
        <form className="login__form" onSubmit={onSubmit} noValidate aria-busy={submitting}>
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
            <Label htmlFor={`${ids}-user`}>Identifiant</Label>
            <div className={`login__input${userNameInvalid ? " login__input--invalid" : ""}`}>
              <User size={18} aria-hidden="true" />
              <input
                {...userNameField}
                ref={(element) => {
                  userNameField.ref(element);
                  userNameRef.current = element;
                }}
                id={`${ids}-user`}
                type="text"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                readOnly={submitting}
                aria-invalid={userNameInvalid}
                aria-describedby={describedBy(userNameErrorId, Boolean(fieldErrors.userName))}
              />
            </div>
            {fieldErrors.userName && (
              <p id={userNameErrorId} className="login__field-error">
                {fieldErrors.userName}
              </p>
            )}
          </div>

          <div className="login__field">
            <Label htmlFor={`${ids}-password`}>Mot de passe</Label>
            <div className={`login__input${passwordInvalid ? " login__input--invalid" : ""}`}>
              <Lock size={18} aria-hidden="true" />
              <input
                {...register("password")}
                id={`${ids}-password`}
                type={showPassword ? "text" : "password"}
                className={showPassword ? undefined : "login__secret"}
                autoComplete="current-password"
                readOnly={submitting}
                aria-invalid={passwordInvalid}
                aria-describedby={describedBy(passwordErrorId, Boolean(fieldErrors.password))}
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

          <Button type="submit" variant="primary" size="lg" className="w-full" disabled={submitting || !baseUrl.ok}>
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
          </Button>

          <p className="login__secure">
            <ShieldCheck size={16} aria-hidden="true" />
            Connexion sécurisée à votre espace MMV.
          </p>
        </form>
      </main>
    </div>
  );
}
