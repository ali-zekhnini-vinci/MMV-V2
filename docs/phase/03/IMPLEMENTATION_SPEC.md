# 03 — IMPLEMENTATION_SPEC

**Statut (17/10/2026, 03-A2) :** sous-lots 03-A1 et 03-A2 **implémentés**. G03-METIER A et B acceptés, C accepté sur le principe : routes `/api/clients`, policies et permissions `clients.*` existent dans le code ; la migration `0003_ClientsModule` est **préparée, non appliquée** — aucune table ni permission n'existe encore en base. Section 03-A2 en fin de document.

*Historique 03-A :* spécification de travail du sous-lot 03-A (17/10/2026). Les choix métier, le contrat HTTP, les permissions et la migration étaient **PROPOSED** (dossier [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md)) ; aucune route, table ni permission nouvelle n'existait côté serveur ou en base. Les sections suivantes décrivent l'état 03-A ; les écarts 03-A2 sont signalés dans la dernière section.

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

## Plan 03-A2 (après GO G03-METIER) — EF Core / Npgsql — *historique 03-A, réalisé : voir « Réalisation 03-A2 »*

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

## Réalisation 03-A2 (17/10/2026)

```text
MMV.Domain/Clients        Client (Create/Update/Archive/Reactivate, lecture seule si archivé, xmin → Version)
                          ClientDetails (saisie validée) ; ClientSearch (repliement NFD/minuscules, chiffres, search_text)
MMV.Domain/Identity       Permissions : + clients.read / clients.write / clients.archive ; Grantable (sans system.admin)
MMV.Application/Clients   ClientInputValidator (D-03-02, codes bornés) ; ClientService (UUID v7, horodatage µs, conflits)
                          IClientStore ; ClientListQuery / ClientPage / ClientWriteResult
MMV.Application/Identity  PermissionAdministrationService (grant/revoke, permissions attribuables seulement)
MMV.Infrastructure        Clients/ClientModel (schéma clients, séquence, contraintes, index, FK RESTRICT, IsRowVersion)
                          Clients/ClientStore (EF Core 10 / Npgsql : filtres, LIKE échappés, tri à clé finale id,
                          DbUpdateConcurrencyException → false) ; MmvDbContext.Clients ; migration 20261016230630_ClientsModule
MMV.Api                   Clients/ClientsEndpoints (6 routes, TypedResults, corps JSON stricts, liste blanche, journaux
                          LoggerMessage 3001/3002 à identifiants techniques) ; AuthSetup : policies ClientsRead/Write/Archive ;
                          DesktopCors : + PUT ; Program : nombres JSON stricts
MMV.Maintenance           permissions list | grant | revoke <identifiant> <permission> (cible stricte, 3 contrôles, code 6)
apps/desktop              CLIENTS_API_ENABLED = true ; contrat src/api/contract/clients.openapi.json + clientsContract.test.ts ;
                          libellés « non activé » neutres (interrupteur de repli) ; double fakeClientsApi aligné
```

| Exigence du GO 03-A2 | Mise en œuvre | Preuve |
|---|---|---|
| Prénom/nom obligatoires, autres facultatifs | `ClientInputValidator` ; contraintes `ck_clients_*_not_blank` | Q-03-53, Q-03-55 |
| Référence `CLI` par PostgreSQL, unique, non réutilisée | séquence `NO CYCLE`, défaut `to_char`, `ux_clients_reference` ; `Reference` jamais envoyée par EF (`PropertySaveBehavior.Ignore`), relue par `RETURNING` | Q-03-62 ; `ClientsWrite` NOT_RUN |
| Recherche insensible aux accents et à la casse | `ClientSearch` + `ClientStore.SearchPatterns` | Q-03-53, Q-03-54 |
| Aucune unicité email/téléphone | aucun index unique hors `reference` | `SchemaInspection.ClientsIndexesExpected` |
| Aucune donnée médicale / sécurité sociale / mutuelle | absentes du modèle ; champ inconnu → 400 | Q-03-55, `ClientsColumnsExpected` |
| Archivage/réactivation, pas de suppression | `Client.Archive/Reactivate` ; aucune route DELETE (405) | Q-03-54, Q-03-55 |
| Concurrence stricte par version | contrôle `Version` avant écriture + `WHERE xmin` à l'enregistrement → 409 | Q-03-54, Q-03-55 ; conflit `xmin` réel : `ClientsWrite` NOT_RUN |
| `clients.*`, `system.admin` sans droit implicite | policies distinctes | Q-03-55 (403 ×6) |
| Attribution par l'outil de maintenance | `permissions grant|revoke` | Q-03-54, Q-03-58 |
| EF Core 10, Npgsql, PostgreSQL 18 exclusivement | aucun autre fournisseur (`ProjectReferenceRulesTests`) | Q-03-61 |
| Migration 0003 + SQL exact, non appliqués | `docs/phase/03/sql/0003_ClientsModule*.sql` | Q-03-64, Q-03-67 |
| OpenAPI à schémas réels, types TS cohérents | `TypedResults`, `Accepts<T>`, transformateur de paramètres ; instantané + tests croisés | Q-03-57, Q-03-73 |
| TanStack Query / Table sur la vraie API | `CLIENTS_API_ENABLED = true` (aucun changement des requêtes : le contrat consommé est celui implémenté) | Q-03-72 |
| États chargement / erreur / conflit / permission refusée | inchangés et testés ; `system.admin` seul → « Accès non autorisé » | Q-03-72 |

Comportement sans migration : la session, la navigation et les droits fonctionnent ; un compte disposant de `clients.read` obtiendrait une erreur technique (500, table absente) affichée « Impossible de charger les clients » + Réessayer ; un compte sans droit obtient 403 « Accès non autorisé ». Les droits `clients.*` ne peuvent d'ailleurs pas être attribués avant la migration (contrainte `ck_user_permissions_permission`).

Écarts de rendu : aucun écran nouveau ni modifié visuellement (seuls les textes de l'état de repli « Service Clients non activé » sont neutralisés) ; `STATE_MAP_47.md` § Amendement 03-A2.
