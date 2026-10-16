# 03 — IMPLEMENTATION_SPEC

**Statut :** spécification de travail du sous-lot 03-A (17/10/2026). Les choix métier, le contrat HTTP, les permissions et la migration sont **PROPOSED** (dossier [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md)). Aucune route, table ni permission nouvelle n'existe côté serveur ou en base.

## Architecture 03-A

```text
Fenêtre Tauri (WebView2) — React 19 / TS strict / Vite
  shell/Shell.tsx        QueryClientProvider (1 QueryClient par session, vidé à la fermeture du Shell)
                         NavigationGuardContext (garde « modifications non enregistrées » du menu)
                         route /clients/* → clients/ClientsModule (chargé à la demande, chunk séparé)
                         route /rendez-vous → écran « indisponible » (sous-lot 03-B)
  clients/
    clientsService.ts    CLIENTS_API_ENABLED = false → état « Service Clients non activé », aucune requête
    clientsUi.ts         critères de liste (recherche, statut, tri, page) conservés pendant la session
    ClientsModule.tsx    routes /clients, /clients/nouveau, /clients/:id, /clients/:id/modifier
    ClientsListView.tsx  barre d'outils (recherche différée 300 ms, archivés, filtres, tri) + états
    ClientsTable.tsx     TanStack Table v8, tri et pagination MANUELS (serveur)
    ClientFormView.tsx   React Hook Form + Zod, dirty, Ctrl+S, confirmation de sortie, erreurs serveur
    ClientDetailView.tsx fiche + onglets Radix (Ordonnances / Ventes : « phase ultérieure »)
    ClientLifecycle.tsx  archivage / réactivation (AlertDialog), échecs 409/403/404/technique
    clientQueries.ts     TanStack Query v5 : clés, signal d'annulation, invalidation, mutations
    clientForm.ts        règles de saisie PROPOSED, conversions, mapping des erreurs serveur
  api/clients.ts         contrat PROPOSED (fonctions HTTP, parseurs stricts, liste blanche)
  api/client.ts          + PUT, erreurs not-found / conflict / validation, { code, errors } bornés
  api/queryClient.ts     politique TanStack Query (retry transport seul, staleTime 30 s, aucun persister)
  components/ui/         + alert-dialog.tsx, tabs.tsx, overlays.css ; dropdown-menu (+ radio) ; button (+ tertiary, danger)
        │ HTTPS + Bearer (inchangé, Phase 02)
        ▼
MMV.Api — inchangé en 03-A (aucune route /api/clients)
```

## Pile frontend ajoutée (versions figées, `apps/desktop/package-lock.json`)

| Paquet | Version | Licence | Publication | Usage réel |
|---|---|---|---|---|
| `@tanstack/react-query` | 5.104.1 | MIT | 02/10/2026 (≥ 2 semaines) | `api/queryClient.ts`, `clients/clientQueries.ts`, `shell/Shell.tsx` |
| `@tanstack/react-table` | 8.21.3 | MIT | 14/04/2025 (dernière v8 ; v9 non retenue, mandat v8) | `clients/ClientsTable.tsx` |
| `@radix-ui/react-alert-dialog` | 1.2.1 | MIT | 12/10/2026 | `components/ui/alert-dialog.tsx` |
| `@radix-ui/react-tabs` | 1.2.1 | MIT | 12/10/2026 | `components/ui/tabs.tsx` |

Les deux paquets Radix appartiennent à la même vague de publication (12/10/2026) que `@radix-ui/react-dropdown-menu` 2.2.1 déjà installé en Phase 02 : primitives partagées dédupliquées, aucune autre version introduite (seul `@radix-ui/react-dialog` 1.3.1 est ajouté en dépendance). Écart à la règle de prudence « ≥ 2 semaines » consigné (O-03-06). Aucune seconde bibliothèque de design ; pas de Sonner, Zustand, Redux, dnd-kit, SignalR, FullCalendar, `openapi-typescript`.

## Comportements

**Service non activé (fonctionnement normal actuel).** Liste et fiche affichent « Service Clients non activé » avec l'explication G03-METIER ; recherche, filtres et tri désactivés ; aucune requête `/api/clients` (test `Shell.test.tsx`). Le formulaire « Nouveau client » est consultable en **aperçu** : validation, état modifié, confirmation de sortie et Ctrl+S fonctionnent, bouton Enregistrer désactivé, bandeau « Aperçu du formulaire ».

**Service activé (tests uniquement, double `src/test/fakeClientsApi.ts`).**
- Liste : paramètres serveur `q` (≥ 2 caractères, différé de 300 ms, requête précédente annulée), `status`, `sort`/`dir` (en-têtes triables avec `aria-sort` + menu de tri), `page`/`pageSize` (20) ; l'ancienne page reste affichée pendant le chargement (`keepPreviousData`) ; surlignage de la recherche insensible casse/accents (présentation seulement).
- États : chargement (squelette), vide, aucun résultat (Effacer la recherche), erreur technique (Réessayer), 403 (« Accès non autorisé »), 401 (fin de session par `AuthProvider`).
- Formulaire : sections Identité / Coordonnées / Informations complémentaires ; libellés toujours visibles ; astérisque décoratif en CSS (`aria-required`) ; `aria-invalid` + `aria-describedby` ; focus sur le premier champ en erreur ; erreurs serveur 422 reportées sous les champs ; Ctrl+S ; « Client créé » puis fiche.
- Concurrence : la version lue à l'ouverture du formulaire est **figée** ; 409 → dialogue « Action impossible… modifié depuis un autre poste », rechargement de la fiche (aucun écrasement silencieux, même après un rafraîchissement du cache en arrière-plan).
- Garde de sortie : Annuler et menu du Shell demandent confirmation si le formulaire est modifié (le routeur mémoire n'offre pas `useBlocker` ; registre `shell/navigationGuard.ts`). La déconnexion n'est pas bloquée (sécurité de session).
- Archivage / réactivation : depuis la liste ou la fiche, dialogue de confirmation, requête versionnée, bandeau de confirmation ; client archivé en lecture seule.
- Cache : `QueryClient` créé par le Shell, `clear()` à son démontage (déconnexion, expiration) ; aucune persistance disque.

**Accessibilité.** Dialogues Radix `alertdialog` (piège de focus, Échap, retour du focus) ; onglets Radix (flèches, Début/Fin) ; menus Radix (radio `menuitemradio`) ; pagination dans une région `navigation` nommée ; bandeaux `role="status"` / `role="alert"` ; contrastes : textes d'erreur/avertissement en `--error-text` / `--warning-text`.

**Style.** `clients/Clients.css` et `components/ui/overlays.css` : cotes des planches du Lot 2, **uniquement** des jetons MMV (test `styles.test.ts` : aucune couleur littérale, chaque jeton couleur redéfini en thème sombre). Écarts de rendu : [`STATE_MAP_47.md`](STATE_MAP_47.md).

## Tests ajoutés (Vitest + Testing Library)

| Fichier | Portée |
|---|---|
| `apps/desktop/src/api/clients.test.ts` | liste blanche des paramètres, parseurs stricts, corps d'erreur bornés, parcours HTTP sur double : pagination, 403 pour `system.admin` seul, 404, 422, 201, 409, archivage/réactivation, Bearer sans jeton dans l'URL |
| `apps/desktop/src/clients/clientForm.test.ts` | règles de saisie, conversions, erreurs serveur, surlignage, paramètres de liste, politique de nouvel essai |
| `apps/desktop/src/clients/ClientsModule.test.tsx` | 20 parcours d'écran : liste, tri, recherche, archivés, pagination, chargement/vide/erreur/403, archivage, conflit, formulaire (validation, Ctrl+S, erreurs serveur, sortie), modification avec conflit, fiche (onglets clavier, archivé), retour à la liste, cache vidé à la déconnexion |
| `apps/desktop/src/shell/navigation.test.ts`, `Shell.test.tsx` | Clients disponible, Rendez-vous annoncé 03-B, sous-routes, état « service non activé » sans requête |
| `apps/desktop/src/styles/styles.test.ts` | jetons uniquement, couverture du thème sombre |

Résultats : [`QA_MATRIX.md`](QA_MATRIX.md).

## Plan 03-A2 (après GO G03-METIER) — EF Core / Npgsql

Proposition générée **hors connexion** dans un worktree jetable (fabrique design-time sans chaîne de connexion), **non intégrée** au dépôt :

| Élément | Référence |
|---|---|
| Entité `MMV.Domain.Clients.Client` | [`proposed/Client.cs.txt`](proposed/Client.cs.txt) |
| Configuration EF `ClientModel` (schéma `clients`, séquence, contraintes, index, FK audit, `xmin` via `IsRowVersion`) | [`proposed/ClientModel.cs.txt`](proposed/ClientModel.cs.txt) |
| Permissions `clients.*` et `DbSet` | [`proposed/permissions_and_dbcontext.diff.txt`](proposed/permissions_and_dbcontext.diff.txt) |
| Migration `20261016223616_ClientsModule` (avec `#pragma warning disable CA1861`, tableaux de colonnes d'index composites) | [`proposed/20261016223616_ClientsModule.cs.txt`](proposed/20261016223616_ClientsModule.cs.txt) |
| Scripts SQL (direct, idempotent, retour arrière) | [`sql/0003_ClientsModule.PROPOSED.sql`](sql/0003_ClientsModule.PROPOSED.sql), [`sql/0003_ClientsModule.PROPOSED.idempotent.sql`](sql/0003_ClientsModule.PROPOSED.idempotent.sql), [`sql/0003_ClientsModule.PROPOSED.rollback.sql`](sql/0003_ClientsModule.PROPOSED.rollback.sql) |

Commandes (worktree, depuis la racine) : `dotnet ef migrations add ClientsModule --project src/MMV.Infrastructure --startup-project src/MMV.Infrastructure --output-dir Persistence/Migrations` puis `dotnet ef migrations script 20261015223849_AuthIdentity 20261016223616_ClientsModule [--idempotent] …` et `dotnet ef migrations script 20261016223616_ClientsModule 20261015223849_AuthIdentity …` (retour arrière). `migrations list --no-connect` : `InitialBaseline`, `AuthIdentity`, `ClientsModule`. Build du worktree 0 avertissement / 0 erreur ; `MMV.Api.Tests` 70/70.

Relecture du SQL : schéma `clients` ; séquence `clients.client_reference_seq` ; table `clients.clients` (17 colonnes, 6 contraintes `CHECK`, 2 FK `RESTRICT` vers `auth.users`) ; index `ux_clients_reference`, `ix_clients_active_name` (partiel, actifs), `ix_clients_created_at`, `ix_clients_created_by_user_id`, `ix_clients_updated_by_user_id` ; contrainte `auth.user_permissions.ck_user_permissions_permission` recréée avec `clients.read|write|archive`. Points d'attention : le retour arrière échoue tant que des attributions `clients.*` existent (les supprimer d'abord) et ne supprime pas le schéma `clients` vide.

Reste à faire en 03-A2 (GO requis) : services Application (création, modification, archivage, recherche normalisée `search_text`), endpoints et policies, CORS `PUT`, réponses OpenAPI déclarées, journaux sans donnée personnelle, commande `MMV.Maintenance permissions grant|revoke` (D-03-09), extension de `SchemaInspection` au schéma `clients`, tests d'autorisation serveur, tests Npgsql sur `mmv_v2_test` (transactions annulées, données fictives), application de la migration par `scripts/Invoke-MmvSqlScript.ps1`, bascule `CLIENTS_API_ENABLED = true`, preuves natives.
