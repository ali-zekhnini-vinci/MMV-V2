# 02 — IMPLEMENTATION_SPEC

**Statut :** spécification de travail (16/10/2026). Les choix de sécurité sont **PROPOSED** tant que le propriétaire ne les a pas ratifiés.

## Architecture

```text
Fenêtre Tauri (WebView2, origine dev http://localhost:1420)
  apps/desktop/src/
    auth/       AuthProvider : jeton opaque en mémoire volatile (useRef), jamais persisté
    login/      LoginScreen + règles pures (loginForm.ts)
    shell/      Shell, Sidebar, Topbar, StatusBar, navigation (garde UI), santé réelle
    views/      Accueil sobre, Comptes (admin, lecture seule), Diagnostic 01B, Indisponible, Non autorisé
    api/        client JSON (GET/POST, Bearer, erreurs bornées), contrats auth + santé
        │  HTTPS validé (aucun contournement), Authorization: Bearer <jeton opaque>, credentials: omit
        ▼
MMV.Api (https://localhost:7267)   /health/live, /health/ready (anonymes, 01B)
                                   /api/auth/login, /api/auth/logout, /api/auth/session
                                   /api/admin/accounts (policy SystemAdmin)
        │  EF Core 10 / Npgsql 10
        ▼
PostgreSQL 18.6 localhost:5433 — mmv_v2_dev (API) / mmv_v2_test (tests)
```

## 02-A — Interface

### Design tokens
`apps/desktop/src/styles/tokens.css` reprend **sans modification de valeur** les jetons `colors.css`, `radii.css`, `shadows.css`, `spacing.css`, `typography.css` du design system fourni (provenance : `SOURCE_MAP.md`). Écarts : `@import` Google Fonts retiré ; ajout de `--mmv-blue-night-deep` (#0A2350, dégradé du login, valeur du handoff). Thème sombre : `<html data-theme="dark">`, appliqué **au shell uniquement** (le login n'a qu'une référence claire).

**Polices :** Inter et Source Sans 3 ne sont **pas embarquées** (aucune police distante, aucun fichier de police ajouté sans validation, O-02-05). La pile retombe sur `Segoe UI` : écart typographique visible et documenté dans la comparaison visuelle.

**Icônes :** `lucide-react` 1.49.0 (ISC), import local, aucun CDN. Version publiée le 29/09/2026, retenue plutôt que la dernière (14/10/2026) par prudence supply chain (≥ 2 semaines).

### Login (`apps/desktop/src/login/`)
Deux panneaux `1.1fr / 1fr` ; dégradé 150°, halo, logo officiel 300×132, titre 38 px, 3 arguments ; formulaire 400 px, champs 48 px, bouton 48 px, mention sécurité. États implémentés :

| État (MMV-Login-states) | Implémentation |
|---|---|
| Initial §1 | focus sur Identifiant, aucun message |
| Champ(s) vide(s) §2/§3/§12 | messages sous les champs, bordure `--error`, `aria-invalid`, focus sur le premier champ en erreur, **aucune requête** |
| Focus §4/§5 | bordure `--border-focus` + anneau 3 px `--shadow-focus` |
| Identifiants incorrects §6 | bandeau `role="alert"` sous le sous-titre, message neutre, deux champs marqués |
| Erreur technique §7 | bandeau « Connexion impossible pour le moment… » (réseau, timeout, 5xx, réponse incohérente) ; champs non marqués |
| En cours §8/§9 | « Connexion… » + spinner (`prefers-reduced-motion` respecté), bouton désactivé, champs en lecture seule |
| Entrée §10 | soumission du formulaire natif |
| Tabulation §11 | Identifiant → Mot de passe → **Afficher le mot de passe** → Se connecter (écart : bouton de visibilité facultatif demandé par le mandat) |
| Conservation §13 | identifiant conservé, **mot de passe vidé** et focalisé (variante recommandée ; à valider) |
| Session expirée (mandat) | bandeau « Votre session a expiré… » après un 401 ou l'échéance absolue |
| Trop de tentatives | bandeau dédié sur 429 |
| Serveur non configuré | bandeau + bouton désactivé (binaire release sans adresse, O-B11) |

Aucune inscription, aucun « mot de passe oublié », aucune mémorisation d'identifiant. Sous 980 px de large (zoom 150 % sur 1440 px), le panneau de marque est masqué (comportement non défini par la maquette, choix provisoire).

### Shell (`apps/desktop/src/shell/`)
Grille : sidebar **260 / 72 px**, barre supérieure **64 px**, barre d'état **32 px**, contenu `1fr` seul défilant (aucun défilement horizontal global). Ordre DOM = ordre de tabulation recommandé (barre supérieure, sidebar, contenu).

- **Navigation** (`navigation.ts`) : sections Principal / Administration. Modules métier (Clients, Produits, Ordonnances, Commandes, Ventes, Inventaire, Notifications, Rapports, Paramètres) marqués **« Disponible dans une phase suivante »** : entrée atténuée, écran explicite sans donnée ni CRUD. Entrées disponibles : Accueil, Comptes (si `system.admin`), Diagnostic technique.
- **Garde UI** : `viewAccess()` masque Comptes sans `system.admin` et affiche « Accès non autorisé » ; **le serveur reste seul juge** (un 403 de l'API produit le même écran).
- **Actif** : fond `--surface-selected` + barre d'accent 3 px + libellé accent (jamais la couleur seule). Rail réduit : icônes 48×48, infobulles au survol **et** au focus, bouton « Ouvrir le menu » explicite, avatar → menu utilisateur.
- **Barre supérieure** : recherche globale et notifications **visibles mais désactivées** (« Disponible dans une phase suivante », aucun badge chiffré inventé) ; bascule de thème ; menu profil (nom, identifiant, rôle technique, « Se déconnecter » uniquement).
- **Barre d'état** (non focalisable, `role="status"`) : « Session active — fin au plus tard à HH:MM » ; « Serveur MMV joignable / injoignable » (`/health/live`) ; « Base de données disponible / indisponible / non vérifiée » (`/health/ready`) ; « MMV — ManageMyVision · client 0.1.0 » (version réelle de `package.json`, injectée par Vite). Vérification au montage, toutes les 30 s et à la demande.
- **États** : serveur inaccessible (bandeau + bouton Réessayer), rechargement (indicateur de vérification de session à chaque navigation, « (vérification…) » dans la barre d'état), non autorisé (écran dédié), session expirée (retour au login).
- Sidebar réduite par défaut sous 1280 px CSS (handoff §10).
- **Accueil** : salutation et rôle technique, aucun chiffre ni statistique. Les cinq tableaux de bord du Lot 1 sont reportés (backlog).
- **Diagnostic technique** : écran 01B conservé (mêmes libellés `technicalStatusView.ts`, endpoints anonymes inchangés).

### Tests frontend
`npm test` (Vitest 5.0.3) : `.test.ts` en environnement node, `.test.tsx` en `jsdom` (30.1.1) avec `@testing-library/react` 16.3.3 / `user-event` 14.6.7 (clavier simulé au niveau DOM). Le double `src/test/fakeMmvApi.ts` est un **vrai serveur `node:http`** en mémoire reproduisant les contrats de l'API (pas sa sécurité) ; aucun mock de `fetch`. Exclu du bundle et du `tsconfig` applicatif.

## 02-B — Identité et autorisation (implémenté ; persistance réelle soumise à O-02-01)

### Contrat HTTP

| Méthode / route | Accès | Réponses |
|---|---|---|
| `POST /api/auth/login` `{userName, password}` | anonyme, HTTPS, limité en débit | 200 `{accessToken, tokenType:"Bearer", expiresAtUtc, idleTimeoutSeconds, user}` ; 400 champs vides ; **401 neutre** `{code:"invalid_credentials"}` (inconnu, mauvais mot de passe, désactivé, verrouillé) ; 429 |
| `POST /api/auth/logout` | authentifié | 204 (session révoquée) ; 401 |
| `GET /api/auth/session` | authentifié | 200 `{expiresAtUtc, idleTimeoutSeconds, user}` ; 401 |
| `GET /api/admin/accounts` | policy `SystemAdmin` | 200 liste technique ; 401 ; **403** |

`user` = `{id, userName, displayName, permissions[]}`. Aucune autre route `/api` n'existe ; aucune route métier exposée.

### Couches

| Couche | Chemin | Contenu |
|---|---|---|
| Domain | `src/MMV.Domain/Identity/` | `UserAccount` (verrouillage, activation, permissions), `PermissionGrant`, `Permissions` (`system.admin` seul), `UserSession` (validité, glissement, révocation), `BootstrapRecord` (singleton) |
| Application | `src/MMV.Application/Identity/` | `IIdentityStore`, `IPasswordHashing`, `AuthOptions`, `SessionTokens` (256 bits CSPRNG, Base64Url, SHA-256), `AuthService` (login neutre, authentification, logout), `BootstrapService` (règles de saisie, création unique) |
| Infrastructure | `src/MMV.Infrastructure/Identity/` | modèle EF explicite (schéma `auth`, snake_case), `IdentityStore` (Npgsql), `IdentityPasswordHashing` (`PasswordHasher<T>` d'ASP.NET Core Identity, paquet `Microsoft.Extensions.Identity.Core` 10.0.13 — pas les stores EF Identity) |
| API | `src/MMV.Api/Auth/` | schéma `MmvSession` (`SessionAuthenticationHandler`), policy `SystemAdmin`, endpoints, limiteur de débit `login` (10/min/IP par défaut, `Auth:LoginAttemptsPerMinute`), filtre HTTPS sur `/api` |
| Maintenance | `src/MMV.Maintenance/` | `status` (lecture seule), `bootstrap-admin` (interactif, masqué, entrée redirigée refusée, confirmation du nom de base ; codes 0/1/2/3/4) |

Choix : ASP.NET Core Identity complet (7 tables `AspNet*`) écarté au profit du seul `PasswordHasher` pour ne créer que les tables indispensables ; `MapIdentityApi` écarté (inscription publique, jetons non révocables côté serveur). Aucune cryptographie maison : PBKDF2 Identity V3, `RandomNumberGenerator`, `SHA256`.

### Sécurité des réponses
- Refus de connexion neutre (même code et corps) pour compte inconnu, mot de passe erroné, compte désactivé ou verrouillé ; vérification de hachage factice si le compte n'existe pas (temps comparable).
- Verrouillage : 5 échecs consécutifs → 15 min (le mot de passe correct est refusé pendant le verrouillage) ; remise à zéro après succès.
- Corps de requête bornés (identifiant ≤ 64, mot de passe ≤ 128) avant tout hachage.
- 401 `{code:"unauthenticated"}` + `WWW-Authenticate: Bearer` ; 403 `{code:"forbidden"}` ; aucun détail (absent/expiré/révoqué/désactivé indistincts).
- Les permissions sont relues à chaque requête (session + compte) : retrait de droit ou désactivation effectifs immédiatement.
- CORS : politique `desktop-api` distincte (GET/POST, en-têtes `Authorization`, `Content-Type`, origines exactes, sans credentials) ; la politique `desktop` des endpoints de santé reste GET seul (01B).

### Session — variante DEV PROVISOIRE (ADR-007 reste PROPOSED)
Voir comparaison et risques : `GATES_AND_DECISIONS.md` § O-02-02.

### Migrations et tests PostgreSQL
- Migration `20261015223849_AuthIdentity` générée hors connexion ; scripts relus `docs/phase/02/sql/` ; application **uniquement** par `scripts/Invoke-MmvSqlScript.ps1` après GO O-02-01 ; jamais au démarrage, jamais `EnsureCreated()`.
- Inventaire de schéma étendu (`tests/MMV.PostgreSql.Tests/SchemaInspection.cs`) : état accepté = base vide, baseline seule, ou baseline + `auth` **exact** (relations, propriétaires, ACL, colonnes, contraintes, index, historique).
- Ordre d'exécution : avant GO `--filter "Stage!=PostMigration&Stage!=IdentityWrite"` ; après GO, suite complète. `Stage=IdentityWrite` = `mmv_v2_test` uniquement (transactions annulées ; test HTTP avec comptes éphémères supprimés en fin de test).

## 02-C — Preuve native (banc versionné)

`apps/desktop/e2e/` (hors bundle, non lint TS) : `cdp.mjs` (pilote du protocole de débogage WebView2, frappes `Input.*` réelles), `run-native.mjs` (scénarios), `capture-window.ps1` (`PrintWindow(PW_RENDERFULLCONTENT)` sur la fenêtre du processus `mmv-desktop`, zone cliente exacte), `send-keys.ps1` (vraies frappes Windows pour le zoom WebView2 Ctrl +/−).

```sh
# apps/desktop, PowerShell : $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS="--remote-debugging-port=9222"; npx tauri dev
node e2e/run-native.mjs login-states --out ../../docs/phase/02/evidence
```

Le port de débogage n'est ouvert qu'en développement, sur la boucle locale, le temps de la preuve. Les comptes du parcours authentifié sont éphémères, sur `mmv_v2_test`, identifiants transmis par fichier temporaire hors dépôt (`MMV_E2E_CREDENTIALS_FILE`), jamais affichés.
