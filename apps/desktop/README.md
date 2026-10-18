# MMV V2 — client Windows (Tauri 2 + React + TypeScript)

Socle technique de la phase 01A. Généré avec `npm create tauri-app@4.7.5` (template `react-ts`, npm), puis réduit :
aucune commande Rust métier, aucun plugin, aucun accès PostgreSQL, aucune donnée de magasin.

Identifiant provisoire : `com.mmv.v2.dev` / produit `MMV V2 Dev` (non définitif, voir `docs/phase/01/GATES_AND_DECISIONS.md`).

## Commandes

```sh
npm ci              # dépendances exactes depuis package-lock.json
npm run lint        # ESLint (typescript-eslint strict)
npm test            # Vitest (node + jsdom), doubles node:http locaux
npm run check       # tsc strict (src + vite.config.ts)
npm run build       # check + build Vite (dist/)
npm run tauri dev   # fenêtre native avec rechargement
npx tauri build --debug --no-bundle   # binaire src-tauri/target/debug/mmv-desktop.exe
```

Prérequis Windows : Node 22, Rust stable `x86_64-pc-windows-msvc`, Visual Studio C++ Build Tools, WebView2.

Prérequis Node : **22.22 ou plus** (exigence de `react-router` 8).

## Pile frontend (phase 02, installée ET utilisée — 16/10/2026)

| Technologie | Version | Utilisation réelle |
|---|---|---|
| Tailwind CSS 4 + `@tailwindcss/vite` | 4.3.3 | `vite.config.ts` ; entrée `src/styles/index.css` (`@import "tailwindcss"`) ; thème `@theme inline reference` = jetons MMV de `tokens.css` (palettes, tailles, rayons, ombres et polices par défaut retirés) |
| shadcn/ui (copie locale adaptée, `components.json`) | style new-york, sans thème par défaut | `src/components/ui/{button,label,dropdown-menu}.tsx`, `src/lib/utils.ts` (`cn`) |
| Radix (`react-slot` 1.5.0, `react-label` 2.1.17, `react-dropdown-menu` 2.2.1) | — | `Button asChild`, étiquettes du login, menu profil (`src/shell/ProfileMenu.tsx`) |
| `class-variance-authority` 0.7.1, `clsx` 2.1.1, `tailwind-merge` 3.7.0 | — | variantes et fusion de classes des composants shadcn/ui |
| React Hook Form 7.90.0 + Zod 4.6.7 + `@hookform/resolvers` 5.9.1 | — | `src/login/LoginScreen.tsx`, schéma `loginSchema` de `src/login/loginForm.ts` |
| React Router 8.4.0 | — | `src/shell/Shell.tsx` : routeur mémoire, une route par entrée (`navigation.ts`, `path`), route inconnue → Accueil |
| Lucide React 1.49.0 | — | icônes (inchangé) |
| Inter / Source Sans 3 (`@fontsource-variable/*` 5.3.0 / 5.3.1, SIL OFL 1.1) | — | `src/styles/fonts.css` : woff2 intégrés au bundle, aucun Google Fonts ; licences copiées dans `public/licenses/` |

Le CSS spécifique au design (login, shell : grille 260/72/64/32, dégradé, infobulles du rail) reste dans `src/login/LoginScreen.css` et `src/shell/Shell.css`, **hors couche** : il prime sur les utilitaires Tailwind et sur le preflight. Ajout d'un composant shadcn : `npx shadcn add <composant>` puis remplacement des classes du thème par défaut (`bg-primary`, `text-muted-foreground`…) par les jetons MMV (`bg-accent`, `text-text-secondary`…) — un test (`src/styles/styles.test.ts`) refuse les variables du thème shadcn par défaut.

Non installés volontairement (aucun cas d'usage réel en phase 02) : TanStack Query/Table, Sonner, Zustand, FullCalendar, Recharts. *(TanStack installé en 03-A, FullCalendar en 03-B : voir plus bas.)*

## Ajouts phase 03-A (17/10/2026)

| Technologie | Version | Utilisation réelle |
|---|---|---|
| TanStack Query | 5.104.1 | `src/api/queryClient.ts` (politique : nouvel essai unique sur erreur de transport, aucun persister), `src/clients/clientQueries.ts`, `QueryClientProvider` par session dans `src/shell/Shell.tsx` |
| TanStack Table | 8.21.3 | `src/clients/ClientsTable.tsx` (tri et pagination serveur) |
| Radix `react-alert-dialog` / `react-tabs` | 1.2.1 / 1.2.1 | `src/components/ui/alert-dialog.tsx`, `src/components/ui/tabs.tsx` (styles `overlays.css`, jetons MMV) |

Module Clients : `src/clients/` (chargé à la demande). *(État au 03-A ; service activé en 03-A2, voir ci-dessous.)* Le service était **désactivé** (`src/clients/clientsService.ts`, `CLIENTS_API_ENABLED = false`) tant que le dossier G03-METIER n'est pas ratifié ; les tests l'activent contre le double `src/test/fakeClientsApi.ts` (données fictives de test uniquement). Toujours non installés : Sonner, Zustand, FullCalendar (03-B), Recharts, `openapi-typescript`.

## Ajouts phase 03-A2 (17/10/2026)

- Service Clients **activé** (`src/clients/clientsService.ts`, `CLIENTS_API_ENABLED = true`) : TanStack Query / Table interrogent les routes réelles `/api/clients` de MMV.Api (contrat `docs/phase/03/API_CLIENTS.md`). Tant que la migration `0003_ClientsModule` n'est pas appliquée, aucun compte ne peut recevoir de droit `clients.*` : l'écran affiche « Accès non autorisé » (403 serveur). L'état « Service Clients non activé » reste disponible comme interrupteur de repli (tests).
- Contrat : `src/api/contract/clients.openapi.json` (extrait du document OpenAPI réel, mis à jour par `MMV_UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/MMV.Api.Tests --filter "FullyQualifiedName~OpenApiContract"`) et `src/api/clientsContract.test.ts` (types, nullabilité, énumérations, bornes, parseurs).
- Aucune dépendance ajoutée.

## Ajouts phase 03-A3 (17/10/2026)

- Actions Clients selon les permissions de la session (`src/clients/clientPermissions.ts`) : sans `clients.write`, « Nouveau client » et Modifier sont masqués (liste) ou désactivés (fiche) avec motif ; sans `clients.archive`, Archiver/Réactiver de même. Garde d'affichage uniquement, le serveur décide.
- Surlignage de recherche mot par mot, insensible aux accents (`src/clients/searchText.ts`).
- Banc natif `e2e/run-native.mjs` : scénarios `clients-admin-only`, `clients-full`, `clients-read`, `clients-write`, option `--prefix`, thème imposé ; exécution avec `node --use-system-ca` (certificat de développement approuvé, aucun contournement TLS). Mode d'emploi : `docs/phase/03/IMPLEMENTATION_SPEC.md` § Réalisation 03-A3.
- Aucune dépendance ajoutée.

## Ajouts phase 03-B (17/10/2026)

- Dépendances (versions figées, MIT) : `@fullcalendar/react` **7.1.0** (FullCalendar Standard, vues par sous-chemins `timegrid` / `interaction`, `skeleton.css`) et `temporal-polyfill` **1.0.5** (pair de FullCalendar 7, utilisé par `src/appointments/time.ts`). Aucun plugin Premium.
- Module Rendez-vous : `src/appointments/` (chargé à la demande, chunk `AppointmentsModule`), route `/rendez-vous`. *(État 03-B ; service activé en 03-B2, voir ci-dessous.)* **Service désactivé** (`src/appointments/appointmentsService.ts`, `APPOINTMENTS_API_ENABLED = false`) : contrat `docs/phase/03/API_APPOINTMENTS_PROPOSED.md` non implémenté par MMV.Api, décisions D-03-13 à D-03-18 `PROPOSED`. Les tests activent le service avec le double `src/test/fakeAppointmentsApi.ts` et une horloge fixe.
- Clavier : rendez-vous focalisables (Entrée / Espace = détail), ↑/↓ entre rendez-vous, Échap ferme les panneaux, création et déplacement par formulaire, sélecteur de client en combobox.
- Module Clients : notice de confirmation rattachée à l'écran (O-03-15).
- `src/components/ui/alert-dialog.tsx` : contenu facultatif entre le texte et les actions.


## Ajouts phase 03-B2 (17/10/2026)

- Service Rendez-vous **activé** (`APPOINTMENTS_API_ENABLED = true`) sur le contrat implémenté `docs/phase/03/API_APPOINTMENTS.md` ; tant que le serveur n'a pas les paramètres du magasin, l'écran affiche « Agenda non configuré » (503 `appointments_not_configured`). L'état « service non activé » reste un interrupteur de repli testé.
- Formulaire aligné sur D-03-13 à D-03-18 : champ Responsable selon le mode du motif (personnalisé : obligatoire ; accueil : « Non attribué (accueil) » seulement si une capacité est configurée), pas de 5 minutes, heures inexistantes **et ambiguës** refusées (`src/appointments/time.ts`), messages des nouveaux codes serveur (capacité d'accueil atteinte, collaborateur non éligible, changement d'heure).
- Fiche client : « Prendre un rendez-vous » (`src/clients/ClientDetailView.tsx`, `src/appointments/appointmentLaunch.ts`) ouvre l'agenda avec le client présélectionné et permet de revenir à la fiche ; proposé avec `appointments.write`, désactivé pour un client archivé.
- Contrat OpenAPI : `src/api/contract/appointments.openapi.json`, contrôlé par `src/api/appointmentsContract.test.ts` (et côté .NET). Aucune dépendance ajoutée.

## Ajouts phase 03-B3 (18/10/2026)

- Contrat Rendez-vous élargi (`docs/phase/03/API_APPOINTMENTS.md`) : chaque saisie envoie `startsAtLocal` ; la liste reçoit `timeZoneOffsets`, comparés aux conversions du poste (`src/appointments/time.ts`, `findOffsetDivergence`) — en cas d'écart, l'agenda de la période est bloqué (« Heure incohérente entre ce poste et le serveur MMV »). Le poste et MMV.Api doivent être à la même version.
- Rendez-vous commencé : « Déplacer », glisser-déposer et « Rétablir » désactivés ; « Modifier » limité au motif et aux notes ; « Dernière modification le … par … » dans le détail.
- Parcours natif : `e2e/cdp.mjs` (`drag`, glisser-déposer à la souris), `e2e/run-native.mjs` scénarios `rdv-b3` et `rdv-b3-guard` ; données fictives créées et nettoyées par un outil jetable hors dépôt (variables `MMV_E2E_CREDENTIALS_FILE`, `MMV_E2E_TOOL`) — commandes : `docs/phase/03/IMPLEMENTATION_SPEC.md` § Réalisation 03-B3.
