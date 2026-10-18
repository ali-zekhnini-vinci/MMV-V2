# 03 — IMPLEMENTATION_SPEC

**Statut (18/10/2026, 03-B3) :** migration `0004_AppointmentsModule` **appliquée** (`mmv_v2_dev`, `mmv_v2_test`) ; corrections d'audit B2-REV-01 à B2-REV-04, cohérence des fuseaux serveur ↔ poste, tests PostgreSQL réels et parcours natif Rendez-vous — voir « Réalisation 03-B3 » en fin de document. *Mise à jour 18/10/2026 : ce statut d'en-tête n'avait pas été actualisé en 03-B et 03-B2 (sections « Réalisation 03-B » et « Réalisation 03-B2 » à jour) ; corrigé sans modifier les statuts historiques ci-dessous.*

*Historique — statut 03-A3 (17/10/2026) :* migration `0003_ClientsModule` **appliquée** (`mmv_v2_dev`, `mmv_v2_test`) ; intégration réelle vérifiée de bout en bout ; voir « Réalisation 03-A3 » en fin de document.

*Historique — statut 03-A2 (17/10/2026) :* sous-lots 03-A1 et 03-A2 **implémentés**. G03-METIER A et B acceptés, C accepté sur le principe : routes `/api/clients`, policies et permissions `clients.*` existent dans le code ; la migration `0003_ClientsModule` est **préparée, non appliquée** — aucune table ni permission n'existe encore en base. Section 03-A2 en fin de document.

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

## Réalisation 03-A3 (17/10/2026) — intégration réelle

**Base.** Migration `20261016230630_ClientsModule` appliquée par `scripts/Invoke-MmvSqlScript.ps1` (script idempotent approuvé) à `mmv_v2_dev` puis `mmv_v2_test` ; jamais au démarrage, jamais `EnsureCreated()` (inchangé). Le « comportement sans migration » décrit ci-dessus est désormais historique.

**Permissions de session (O-03-09).** `apps/desktop/src/clients/clientPermissions.ts` : `clientPermissions(permissions)` → `{ canWrite: clients.write, canArchive: clients.archive }`, lu depuis la session (`useAuth`). Garde **d'affichage** seulement :

| Écran | Sans `clients.write` | Sans `clients.archive` |
|---|---|---|
| Liste | « Nouveau client » absent (y compris dans les états vide / aucun résultat) ; icône Modifier absente ; note « Consultation seule… » | icônes Archiver / Réactiver absentes |
| Fiche | Modifier désactivé (infobulle `clients.write`) ; note « Consultation seule… » | Archiver / Réactiver désactivés (infobulle `clients.archive`) ; note dédiée |
| Routes | `/clients/nouveau` → liste ; `/clients/:id/modifier` → fiche | — |

`clients.read` absent : inchangé, le serveur répond 403 et l'écran affiche « Accès non autorisé ». L'entrée de navigation « Clients » reste visible (le refus explique au propriétaire qu'un droit manque).

**Recherche.** Surlignage mot par mot (`findFoldedWords`), insensible aux accents et à la casse, aligné sur la recherche serveur (chaque mot doit apparaître).

**Banc natif.** `apps/desktop/e2e/run-native.mjs` : option `--prefix` (défaut `P02_native`, inchangé pour la phase 02), scénarios `clients-admin-only`, `clients-full`, `clients-read`, `clients-write`, aides `fillField`, `setTheme` (thème imposé : la préférence `mmv.theme` persiste dans la WebView), `otherPostUpdate` (écriture d'un « autre poste » sur l'API réelle pour provoquer le 409). Exécution :

```powershell
# MMV.Api sur mmv_v2_test (chaîne passée au seul processus, jamais affichée)
$env:ConnectionStrings__Mmv = '<ConnectionStrings:MmvTest des user-secrets mmv-v2-postgresql-tests>'
dotnet run --project src/MMV.Api --launch-profile https
# apps/desktop
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"; npx tauri dev
$env:MMV_E2E_CREDENTIALS_FILE = '<fichier temporaire hors dépôt { suffix, admin, read, write, full }>'
node --use-system-ca e2e/run-native.mjs clients-full --prefix P03_native --out ../../docs/phase/03/evidence
```

Les comptes éphémères `e2e-c{admin,read,write,full}-<suffixe>` sont créés et supprimés par un outil local **non versionné** (même principe qu'en phase 02 : `MMV.Infrastructure`, hachage Identity, refus de toute cible autre que `mmv_v2_test` / 5433 / boucle locale avant et après connexion) ; le dépôt ne contient toujours aucun chemin de création de compte hors `bootstrap-admin`.

**Tests ajoutés / corrigés.** `ClientsPostgreSqlTests` : `Duplicates_AreAllowed_WithDistinctReferences`, `ClientsHttpPermissionsAndLifecycle_OnRealPostgreSql`, attente `23001` (O-03-14) ; `ClientServiceTests` / `InMemoryClientStore(TimeProvider?)` : semis sur l'horloge du test ; `ClientsModule.test.tsx` : lecture seule, écriture sans archivage ; `clientForm.test.ts` : surlignage mot par mot.

| Exigence du GO 03-A3 | Mise en œuvre | Preuve |
|---|---|---|
| Empreinte, correspondance, 0001/0002 seules, cible réelle, format de référence | contrôles lecture seule + régénération hors connexion | Q-03-80, Q-03-81 |
| Application séparée dev puis test, rôles propriétaires, script sécurisé | `Invoke-MmvSqlScript.ps1` ×2 | Q-03-82, Q-03-83 |
| 3 migrations et objets | inspection des catalogues ; `PostMigration` | Q-03-84, Q-03-85 |
| `ClientsWrite` sur `mmv_v2_test` ; accents, doublons, archivage, réactivation, 409, permissions, champ inconnu, résidus | tests PostgreSQL + matrice HTTP réelle | Q-03-86, Q-03-89, Q-03-96 |
| Suites complètes | .NET 260/260, frontend 137/137 | Q-03-94, Q-03-95 |
| Procédure de permissions, aucun compte réel modifié | `PROCEDURE_PERMISSIONS_CLIENTS.md` ; commandes en lecture / refus | Q-03-88 |
| Actions interdites masquées ou désactivées | `clientPermissions.ts` | Q-03-90, Q-03-93 |
| Chaîne Tauri → HTTPS → API → PostgreSQL 18, captures, comparaison | banc natif | Q-03-90, Q-03-91 |

## Réalisation 03-B (17/10/2026) — Rendez-vous, interface initiale

Mandat : [`prompts/PHASE03_B_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_B_GO_CLAUDE_CODE.md). Décisions D-03-13 à D-03-18 **PROPOSED** ([`DECISIONS_03B_RDV_PROPOSITION.md`](DECISIONS_03B_RDV_PROPOSITION.md)) : seuls les composants indépendants de ces décisions sont développés ; le service est désactivé.

**Clients (O-03-15).** `clients/ClientsModule.tsx` rattache la notice à l'écran pour lequel elle est émise (`ScopedClientNotice.path`, `clients/clientsUi.ts`) et l'efface dès que le chemin change (ajustement pendant le rendu, sans effet) ; `setNotice(notice, path?)` permet au formulaire de viser la fiche qu'il ouvre (`ClientFormView.tsx`). Aucun autre changement du module Clients.

**Architecture Rendez-vous.**

```
apps/desktop/src/
  api/appointments.ts            contrat PROPOSÉ : types, parseurs stricts, fonctions HTTP
  appointments/
    appointmentsService.ts       APPOINTMENTS_API_ENABLED = false ; horloge injectable (tests)
    appointmentPermissions.ts    garde d'affichage appointments.write / cancel + clients.read (sélecteur)
    appointmentQueries.ts        TanStack Query : options, période, détail, sélecteur client, 4 mutations
    time.ts                      Temporal : fuseau du magasin, bornes de vue, libellés français
    appointmentForm.ts           Zod construit depuis les options serveur ; conversions ; erreurs par champ
    appointmentsView.ts          libellés, teintes par position du motif, messages d'échec bornés
    AppointmentsModule.tsx       page /rendez-vous : en-tête, barre d'outils, agenda, panneaux, annulation
    AppointmentsCalendar.tsx     FullCalendar 7.1.0 (timeGridDay / timeGridWeek), clavier ↑/↓
    AppointmentDetailPanel.tsx   panneau détail (planches 32, 37–39)
    AppointmentFormPanel.tsx     création / modification / déplacement (planches 35, 36, 41, 42)
    ClientPicker.tsx             combobox ARIA, clients actifs (planche 40)
    Appointments.css             cotes du Lot 2, jetons MMV clair / sombre
  test/fakeAppointmentsApi.ts    double node:http du contrat proposé (règle de conflit proposée)
```

Chargement à la demande depuis `shell/Shell.tsx` (chunk `AppointmentsModule`, FullCalendar et Temporal hors du bundle initial) ; entrée de navigation `appointments` passée à `available` (`shell/navigation.ts`). `components/ui/alert-dialog.tsx` : contenu facultatif `children` (motif d'annulation), rétrocompatible. `api/auth.ts` : constantes `clients.read`, `appointments.write`, `appointments.cancel`.

**Comportements.**

| Sujet | Mise en œuvre |
|---|---|
| Fuseau | `timeZone` des options passé à FullCalendar (Temporal natif) ; saisie `jj/mm/aaaa` + `hh:mm` convertie par `wallTimeToUtc` (heure inexistante refusée localement) ; « Aujourd'hui » = date du magasin |
| Période | `viewRangeUtc` (minuit du magasin, fin exclue) → `GET /api/appointments?from&to&status` ; la période précédente reste affichée pendant le chargement |
| Clavier | rendez-vous = boutons FullCalendar (Tab, Entrée / Espace = détail) ; ↑/↓ dans l'ordre chronologique ; focus sur le titre du détail ; Échap ferme panneau ou formulaire ; création et déplacement entièrement au clavier |
| Souris | sélection d'un créneau → formulaire pré-rempli (durée reprise si elle figure dans la liste serveur) ; glisser = déplacement (relecture du détail, `PUT` complet, `revert()` sur refus) ; aucun redimensionnement |
| Conflits et refus | traduits depuis les codes 409/422 (`describeAppointmentFailure`, `appointmentServerFieldErrors`) ; rien n'est déduit localement |
| Permissions | sans `appointments.write` : pas de « Nouveau rendez-vous », sélection et glisser inactifs, Modifier / Déplacer désactivés avec motif ; sans `appointments.cancel` : Annuler / Rétablir désactivés ; sans `clients.read` : sélecteur indisponible, « Voir la fiche client » absent |
| Annulés | masqués par défaut (`status=planned`), puce « Afficher les annulés » (`status=all`), rendu barré et atténué |
| États | service non activé, chargement, erreur technique + Réessayer, accès refusé, journée / semaine vide, succès (bandeau `status`) |

**FullCalendar 7 (constats).** Paquet unique `@fullcalendar/react` (vues par sous-chemins) ; mise en page seule (`skeleton.css`), classes internes hachées et bordures / marges neutralisées par `!important` : le style passe par les options `*Class` (`dayHeaderClass`, `slotLaneClass`, `eventClass`…) et `box-shadow` ; `slotMinHeight` 32 px (64 px/h comme les planches). Option `controller` non utilisée : vue et date pilotées par `ref.getApi()`.

**Serveur proposé (texte, non compilé dans `src/`).** Généré et compilé dans un worktree jetable puis copié : [`proposed/Appointment.cs.txt`](proposed/Appointment.cs.txt), [`proposed/AppointmentModel.cs.txt`](proposed/AppointmentModel.cs.txt), [`proposed/20261017141136_AppointmentsModule.cs.txt`](proposed/20261017141136_AppointmentsModule.cs.txt) (avec `#pragma warning disable CA1861` comme 0003), [`proposed/permissions_dbcontext_snapshot_0004.diff.txt`](proposed/permissions_dbcontext_snapshot_0004.diff.txt) (permissions, `DbSet`, instantané EF, test `Grantable`). Commandes (racine du worktree) : `dotnet ef migrations add AppointmentsModule --project src/MMV.Infrastructure --startup-project src/MMV.Infrastructure --output-dir Persistence/Migrations`, puis `dotnet ef migrations script 20261016230630_ClientsModule 20261017141136_AppointmentsModule [--idempotent]` et le script inverse. Service applicatif, validation, routes et tests serveur : **après ratification** (étape 4 de `RDV_03B_PREPARATION.md`).

**Tests ajoutés.** `appointments/time.test.ts` (8), `appointments/appointmentForm.test.ts` (6), `appointments/AppointmentsModule.test.tsx` (18), `clients/ClientsModule.test.tsx` (+1, O-03-15) ; `shell/navigation.test.ts` et `shell/Shell.test.tsx` adaptés (Rendez-vous disponible, service non activé). Frontend 171/171 (Q-03-111).


## Réalisation 03-B2 (17/10/2026) — Rendez-vous métier

Mandat : [`prompts/PHASE03_B2_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_B2_GO_CLAUDE_CODE.md). Décisions D-03-13 à D-03-18 **ACCEPTED** ([`DECISIONS_03B_RDV_PROPOSITION.md`](DECISIONS_03B_RDV_PROPOSITION.md) § Décision). Contrat : [`API_APPOINTMENTS.md`](API_APPOINTMENTS.md). Aucune dépendance ajoutée (ni NodaTime, ni FullCalendar Premium, ni microservice) ; architecture modulaire de 03-A2 reprise couche par couche.

**Serveur.**

```
src/MMV.Domain/Appointments/
  Appointment.cs                 entité (planifié / annulé, xmin, intervalle semi-ouvert, Overlaps)
  AppointmentReasons.cs          7 motifs approuvés + mode (Personal / Reception), durées, pas de 5 min
  AppointmentResponsible.cs      éligibilité explicite d'un compte (retrait logique)
  ReceptionOccupancy.cs          occupation maximale simultanée sur un créneau (balayage des bornes)
src/MMV.Domain/Identity/Permissions.cs   + appointments.read / write / cancel (All, Grantable)
src/MMV.Application/Appointments/
  AppointmentSettings.cs         section « Appointments » validée (fuseau admis, plage, jours, capacité), aucun défaut
  AppointmentSchedule.cs         instants UTC stricts ; pas, minuit, changements d'heure dans le fuseau du magasin
  AppointmentInputValidator.cs   422 par champ ; mode d'attribution ; notes ; motif d'annulation
  AppointmentService.cs          cas d'usage ; IAppointmentStore / IAppointmentWriteSession ; AppointmentLock (clés)
  ResponsibleAdministrationService.cs   liste / activation / retrait d'éligibilité (outil de maintenance)
src/MMV.Infrastructure/Appointments/
  AppointmentModel.cs            schéma appointments : 2 tables, contraintes, index partiels
  AppointmentStore.cs            lectures jointes ; session d'écriture : transaction READ COMMITTED,
                                 pg_advisory_xact_lock(1296913746, clé) par clés croissantes, FOR SHARE client
                                 (correction 03-B3 : la valeur réelle de 0x4D4D5652 est 1296914002, O-03-27)
src/MMV.Infrastructure/Persistence/Migrations/20261017150504_AppointmentsModule(.Designer).cs, instantané du modèle
src/MMV.Api/Appointments/
  AppointmentsSetup.cs           lecture des paramètres au démarrage, journal 3100 / 3103 (jamais d'arrêt de l'API)
  AppointmentsEndpoints.cs       7 routes, filtre 503, corps stricts, TypedResults, journaux 3101 / 3102
src/MMV.Api/Auth/AuthSetup.cs    policies AppointmentsRead / Write / Cancel
src/MMV.Maintenance/Program.cs   responsibles list | enable | disable <identifiant>
```

**Garanties de réservation (D-03-15).** Toute écriture qui réserve (création, changement de créneau ou de responsable, rétablissement) : `BEGIN` (`READ COMMITTED`) → verrou consultatif transactionnel de la ressource visée (collaborateur, ou pool d'accueil clé 0) pris par `AppointmentStore.LockSequence` (clés croissantes, sans doublon) → `SELECT … FROM clients.clients WHERE id = … FOR SHARE` → éligibilité → chevauchement (`ix_appointments_responsible_planned`) ou occupation (`ix_appointments_unassigned_planned` + `ReceptionOccupancy`) → `INSERT`/`UPDATE … WHERE xmin = …` → `COMMIT`. Chaque instruction relit un instantané frais après l'obtention du verrou : une réservation concurrente validée est vue, une non validée fait attendre. Refus → `ROLLBACK` à la libération (verrous relâchés). Transaction englobante existante (tests PostgreSQL annulés) : réutilisée, jamais validée.

**Interface (adaptation de 03-B1).**

| Fichier | Changement |
|---|---|
| `api/appointments.ts` | contrat implémenté : `reasons[].responsibleRequired`, `startStepMinutes`, `receptionCapacity`, `cancelledByDisplayName`, nouveaux codes 409, 503 `appointments_not_configured` ; fin de plage `24:00` admise |
| `api/client.ts` | code d'erreur borné lu aussi sur 503 |
| `appointments/time.ts` | heure ambiguë **refusée** (`reason: "ambiguous"`) — D-03-13 |
| `appointments/appointmentForm.ts` | `responsibleMode` (requis / facultatif / accueil indisponible) ; pas de 5 min ; messages des codes serveur `invalid_step`, `out_of_range`, `time_change`, `not_eligible`, `reception_not_configured` |
| `appointments/AppointmentFormPanel.tsx` | champ Responsable selon le motif (« Non attribué (accueil) » seulement si une capacité existe), aide contextuelle, bouton de retour à la fiche |
| `appointments/appointmentsView.ts` | « Capacité d'accueil atteinte », « Accueil non configuré », « Responsable indisponible », « Créneau passé » ; `isStoreNotConfigured` |
| `appointments/AppointmentsModule.tsx` | état « Agenda non configuré » (503) ; arrivée depuis la fiche client : formulaire ouvert avec le client, retour à la fiche (abandon ou bandeau de confirmation) |
| `appointments/appointmentLaunch.ts` | état de navigation validé (`readAppointmentLaunch`, `appointmentLaunchFor`) |
| `appointments/AppointmentDetailPanel.tsx` | date et auteur de l'annulation |
| `appointments/appointmentsService.ts` | `APPOINTMENTS_API_ENABLED = true` |
| `clients/ClientDetailView.tsx` | « Prendre un rendez-vous » (avec `appointments.write` ; désactivé pour un client archivé) |
| `test/fakeAppointmentsApi.ts`, `test/fakeMmvApi.ts` | double aligné : motifs approuvés, modes, capacité instant par instant, codes, 503, auteur d'annulation |

**Tests ajoutés ou adaptés.** .NET : `tests/MMV.Api.Tests/Appointments/` — `AppointmentRulesTests` (27), `AppointmentServiceTests` (37), `AppointmentsEndpointsTests` (39), `AppointmentsOpenApiContractTests` (2), `AppointmentTestDoubles.cs` (double transactionnel à verrous réels) ; `ClientsOpenApiContractTests` (extraction partagée), 2 tests préexistants mis à jour (commentaires datés). PostgreSQL : `AppointmentsPostgreSqlTests` (`Stage=AppointmentsWrite`, 6, NOT_TESTED), `SchemaInspection` / `SchemaStateTests` étendus à l'état 0004. Frontend : `time.test.ts` (9), `appointmentForm.test.ts` (7), `AppointmentsModule.test.tsx` (25), `api/appointmentsContract.test.ts` (4), `Shell.test.tsx` (+1). Résultats : QA Q-03-120 à Q-03-138.

**Construction sans perturber le propriétaire.** `MMV.Api.exe` du propriétaire actif : compilation et tests .NET dans un worktree jetable (`git worktree add --detach <bloc-notes>/wt HEAD`, copie des fichiers modifiés) ; migration et scripts générés hors connexion dans ce worktree puis copiés dans `src/` et `docs/phase/03/sql/`.

## Réalisation 03-B3 (17–18/10/2026) — migration 0004 et validation réelle

GO : [`prompts/PHASE03_B3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_B3_GO_CLAUDE_CODE.md). Commits locaux `c2387ff` (corrections serveur, interface et tests) et `990d04f` (correctifs issus du parcours natif, scénarios natifs) ; documentation dans le commit suivant.

**Migration.** `0004_AppointmentsModule.idempotent.sql` appliqué par `scripts/Invoke-MmvSqlScript.ps1` à `mmv_v2_dev` puis `mmv_v2_test` après contrôles (empreinte, régénération hors connexion octet pour octet identique, identité serveur, migrations 0001–0003 seules, absence d'objets `appointments`, structures dev = test) — [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Exécution 03-B3. Aucun nouveau modèle EF : les corrections 03-B3 ne changent pas le schéma.

**Corrections serveur.**

| Fichier | Changement |
|---|---|
| `src/MMV.Application/Appointments/AppointmentService.cs` | `UpdateAsync` : `rebooks` = créneau, durée ou responsable modifiés → verrou, éligibilité (B2-REV-01), disponibilité ; rendez-vous commencé + changement de client / créneau / durée / responsable → `AppointmentPast` (B2-REV-02) ; `AppointmentView.UpdatedByDisplayName` |
| `src/MMV.Application/Appointments/AppointmentSchedule.cs` | `LocalWallTime`, `IsLocalWallTime`, `OffsetSegments` (décalages du serveur par segments, pas de 15 min, transition au tick près), `UtcOffsetSegment` |
| `src/MMV.Application/Appointments/AppointmentInputValidator.cs` | `AppointmentInput.StartsAtLocal` obligatoire, format strict, `time_zone_mismatch` si l'heure murale du serveur diffère |
| `src/MMV.Infrastructure/Appointments/AppointmentStore.cs` | `Views()` : tri `orderby` **avant** la projection (défaut trouvé sur PostgreSQL réel : tri après projection non traduisible) ; jointure de l'auteur de la dernière modification |
| `src/MMV.Api/Appointments/AppointmentsEndpoints.cs` | `startsAtLocal` dans les corps ; `timeZoneOffsets` dans la liste ; `updatedByDisplayName` dans le détail ; instantané OpenAPI régénéré (3 ajouts) |

**Cohérence des fuseaux (B2-REV-03).** Détail et limites : [`API_APPOINTMENTS.md`](API_APPOINTMENTS.md) § Cohérence des fuseaux. Lecture : décalages du serveur comparés par l'interface (`findOffsetDivergence`, `localOffsetSegments` dans `apps/desktop/src/appointments/time.ts`), période bloquée en cas d'écart ; écriture : `startsAtLocal` recalculé par le serveur.

**Interface.**

| Fichier | Changement |
|---|---|
| `api/appointments.ts` | `AppointmentInput.startsAtLocal`, `AppointmentList` / `TimeZoneOffset` (parseur strict : segments contigus, non vides, entiers), `updatedByDisplayName` |
| `appointments/time.ts` | `localWallDateTime`, `localOffsetSegments` (transitions ICU via Temporal), `findOffsetDivergence` |
| `appointments/AppointmentsModule.tsx` | contrôle des décalages de la période (y compris pendant le chargement, sur la couverture de la période affichée) ; état bloquant « Heure incohérente… » ; aucune création ni déplacement sur une période divergente |
| `appointments/appointmentForm.ts` | `startsAtLocal` dans les deux constructeurs de saisie ; message `time_zone_mismatch` ; responsable actuel admis même s'il n'est plus éligible |
| `appointments/AppointmentFormPanel.tsx` | rendez-vous commencé : résumé figé, seuls motif et notes ; responsable retiré proposé « (n'est plus habilité) » ; refus serveur sur un champ masqué (déplacement, réservation figée) affiché en bandeau |
| `appointments/AppointmentDetailPanel.tsx` | « Dernière modification le … par … » ; « Déplacer » et « Rétablir » désactivés pour un rendez-vous commencé, avec motif |
| `appointments/AppointmentsCalendar.tsx`, `appointmentsView.ts` | glisser-déposer désactivé pour un rendez-vous commencé (`hasStarted`) ; message `appointment_past` |
| `appointments/appointmentQueries.ts` | liste des éligibles relue après un refus `not_eligible` / `responsible_unavailable` |
| `test/fakeAppointmentsApi.ts` | décalages servis (ou divergents, option `serverOffsets`), `startsAtLocal`, auteur de modification, garde du passé (option `appointmentsNow`), éligibilité non revérifiée sans changement de réservation |

**Tests.** .NET 314 (+28) et 50 ; PostgreSQL 40 dont `AppointmentsCommittedPostgreSqlTests.cs` (nouveau : données validées `CommittedTestData`, B2-REV-04 `AppointmentsConcurrencyPostgreSqlTests`, matrice HTTP `AppointmentsHttpPostgreSqlTests`) ; assembly PostgreSQL exécutée séquentiellement (O-03-28) ; `PostgreSqlTheoryAttribute`. Frontend 196 (+11). Témoin négatif rendu déterministe (`InMemoryAppointmentStore.RendezvousBeforeCommit`). QA : Q-03-140 à Q-03-169.

**Parcours natif.**

```powershell
# Bloc-notes (hors dépôt) : outil jetable des données fictives (seed / eligibility / cleanup), mmv_v2_test seulement
dotnet run <bloc-notes>/native/b3native.cs -- seed <bloc-notes>/native/creds.json
# MMV.Api (worktree, code 03-B3) : chaîne mmv_v2_test et paramètres du magasin passés au seul processus
$env:ConnectionStrings__Mmv = '<secret mmv-v2-postgresql-tests>'; $env:Appointments__TimeZone = 'Europe/Brussels'
$env:Appointments__DisplayStartTime = '09:00'; $env:Appointments__DisplayEndTime = '19:00'; $env:Appointments__ReceptionCapacity = '1'
dotnet run --project src/MMV.Api --launch-profile https --no-build
# apps/desktop
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"; npx tauri dev
$env:MMV_E2E_CREDENTIALS_FILE = '<bloc-notes>/native/creds.json'; $env:MMV_E2E_TOOL = '<bloc-notes>/native/b3native.cs'
node --use-system-ca e2e/run-native.mjs rdv-b3 --prefix P03B3_native --out <dossier>
node --use-system-ca e2e/run-native.mjs rdv-b3-guard --prefix P03B3_native --out <dossier>   # API de test en Africa/Casablanca
dotnet run <bloc-notes>/native/b3native.cs -- cleanup <suffixe>
```

`e2e/cdp.mjs` : `drag()` (événements souris réels du moteur). `e2e/run-native.mjs` : scénarios `rdv-b3` (parcours complet) et `rdv-b3-guard` (garde-fou de fuseau).

**Construction sans perturber le propriétaire.** Worktrees jetables du bloc-notes (`wt` : HEAD + corrections ; `wt-head` : HEAD 03-B2 pour mesurer l'instabilité préexistante) ; aucune compilation dans `src/` en place.

## Réalisation 03-C (18/10/2026) — validation fonctionnelle

**Aucun changement applicatif.** `src/` et `apps/desktop/src/` inchangés ; changements limités à l'outillage et à la documentation.

**O-03-16 — `scripts/Invoke-MmvSqlScript.ps1`.** Cause : Windows PowerShell 5.1 ne protège pas les guillemets doubles d'un argument passé à un exécutable natif ; `-c 'SELECT … "__EFMigrationsHistory"'` arrivait à psql sans guillemets (nom replié en minuscules, relation introuvable). Correction : la requête de contrôle, en lecture seule, est transmise **par l'entrée standard** (`'…' | & $Psql @common -A -t`), indépendante de la version de PowerShell ; son échec après `COMMIT` produit un avertissement explicite (le script ne prétend jamais que la transaction a été annulée). Contrôles préalables, empreinte, cibles et application `-f` **inchangés**. Tests : `scripts/tests/Invoke-MmvSqlScript.Tests.ps1` — faux `psql.exe` compilé à la volée (`Add-Type -OutputType ConsoleApplication`) qui journalise les arguments et l'entrée réellement reçus et refuse, comme PostgreSQL, le nom sans guillemets ; user-secrets fictifs dans un `APPDATA` temporaire ; script exécuté dans un processus `powershell.exe -File` distinct (flux capturés au niveau du processus) ; `-Target` permet de rejouer une autre version (preuve avant / après).

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Invoke-MmvSqlScript.Tests.ps1
```

**Banc natif.**

| Fichier | Changement |
|---|---|
| `apps/desktop/e2e/run-native.mjs` | scénarios `parcours-03c` et `matrice-03c` ; aides `targetDay` (jour ouvré cible, fuseau du magasin), `storeWall` (heure murale via l'ICU de Node, indépendante du poste), `resize`, `gotoDay`, `tabTo` (tabulations réelles), `mouseScale` (étalonnage CDP ↔ CSS sous zoom), `dragEvent`, `backgroundLuminance`, `reachable` ; une session API par compte (`apiLogoutAll` en fin de scénario) ; `rdv-b3-guard` : `--client-first`, `--responsible` |
| `apps/desktop/e2e/send-keys.ps1` | levée du verrou de premier plan (frappe Alt) et échec explicite si la fenêtre n'est pas au premier plan |
| `apps/desktop/e2e/capture-window.ps1` | `-ResizeOnly` (résolution d'essai sans capture) |

```powershell
# Bloc-notes (hors dépôt) : outil jetable c3native.cs (seed refusé si mmv_v2_test non vide / cleanup / residue), mmv_v2_test seulement
dotnet run <bloc-notes>/native/c3native.cs -- seed <bloc-notes>/native/creds.json
# MMV.Api (worktree) : chaîne mmv_v2_test et magasin Europe/Brussels 09:00–19:00, capacité 1, passés au seul processus
dotnet run --project src/MMV.Api --launch-profile https --no-build
# apps/desktop (worktree ; CARGO_TARGET_DIR court si le chemin dépasse 260 caractères)
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"; npx tauri dev
$env:MMV_E2E_CREDENTIALS_FILE = '<bloc-notes>/native/creds.json'
node --use-system-ca e2e/run-native.mjs parcours-03c --prefix P03C_native --out <dossier> --theme light --size 1920x1080 --zoom 1 --day-offset 5 --first-name Clair
node --use-system-ca e2e/run-native.mjs parcours-03c --prefix P03C_native --out <dossier> --theme dark --size 1440x900 --zoom 1.5 --day-offset 4 --first-name Zoom --captures key
node --use-system-ca e2e/run-native.mjs matrice-03c --prefix P03C_native --out <dossier> --day-offset 2   # après un parcours « Parcours » au jour +2
node --use-system-ca e2e/run-native.mjs rdv-b3-guard --prefix P03C_native --out <dossier> --client-first Existant --responsible "C3N Responsable Deux"   # API en Africa/Casablanca
dotnet run <bloc-notes>/native/c3native.cs -- cleanup <suffixe>
```

**Build release.** `npx tauri build --no-bundle` ; dans un worktree profond, `CARGO_TARGET_DIR` court (sinon `LNK1104`, O-03-38). L'exécutable release n'a ni adresse d'API ni `connect-src` : écran « Serveur MMV non configuré sur ce poste » (attendu avant empaquetage).

**Tests.** Aucun test applicatif ajouté (code inchangé) ; `Invoke-MmvSqlScript.Tests.ps1` 15 contrôles. QA : Q-03-170 à Q-03-190.

## Réalisation 03-D (18/10/2026) — UX Clients et traçabilité

**03-D1 — sélecteurs, préremplissage, historique des rendez-vous.**

| Élément | Mise en œuvre |
|---|---|
| Date | `components/ui/date-picker.tsx` : React DayPicker 10 (`mode="single"`, `locale=fr`, semaine au lundi, listes Mois / Année, `fixedWeeks`) dans `PopoverContent` ; valeur = date civile `AAAA-MM-JJ` du magasin portée par un `Date` local **à midi** (`picker-values.ts`) — jamais `toISOString` ; « aujourd'hui » et la première date admise = `todayIn(timeZone)` ; affichage `JJ/MM/AAAA` ; le formulaire garde ses valeurs `jj/mm/aaaa` (schéma Zod et contrat inchangés) |
| Heure | `components/ui/time-picker.tsx` : `listbox` ARIA à `aria-activedescendant`, 8 lignes visibles, flèches / Page (± 1 h) / Début / Fin / chiffres tapés, Entrée ou Espace ; options = `startTimeOptions(date, pas serveur, fuseau, maintenant)` : journée entière, motif d'indisponibilité (passée, inexistante, existe deux fois) calculé seulement les jours de changement d'heure ; focus et défilement initiaux dans `onOpenAutoFocus` (contenu de portail monté un rendu plus tard) |
| Échap | `PopoverContent` arrête la propagation d'Échap : le panneau parent ne se ferme pas |
| Grille → formulaire | `slotFormPreset` (date, heure, durée si proposée) ; aperçu de sélection sans rendez-vous géré par `eventClassName` (O-03-39) |
| Historique des rendez-vous | `GET /api/appointments/by-client/{clientId}` (`ClientAppointmentQuery`, filtres `ClientAppointmentFilters` partagés par EF et les doubles, effectifs par catégorie, page découpée sur les rendez-vous triés avant jointures, heures murales calculées par le serveur) ; onglet `ClientAppointmentsTab` (catégories, tri, pagination, avertissement d'écart de fuseau, « Voir le détail » → agenda positionné, annulés affichés, Échap → retour à la fiche) |
| O-03-33 à O-03-36 | `ConfirmDialog` (élément ouvrant mémorisé dans `onOpenAutoFocus`, refocalisé dans `onCloseAutoFocus`) ; `compactIndicatorText` + bascule CSS sous 1100 px ; panneau `position: absolute` sous 1100 px ; `appointmentClientName` = `clientDisplayName` |

**03-D2 — journal d'audit.** Voir [`AUDIT_JOURNAL_03D.md`](AUDIT_JOURNAL_03D.md). Points d'implémentation : `IAuditRecorder` enregistré en portée de requête sur le **même** `MmvDbContext` ; `ClientService` / `AppointmentService` reçoivent l'enregistreur (constructeur, dépendance obligatoire — aucun repli silencieux « sans audit ») ; `HttpAuditContext` (MMV.Api) fournit `SessionId` et `TraceId` ; `NoAuditContext` hors HTTP ; identifiants UUIDv7 strictement croissants dans l'unité de travail ; lecture `AuditStore` triée par instant puis identifiant décroissants, jointure du nom affiché de l'auteur ; `AuditEndpoints` résout les libellés (heure murale du magasin si configuré, nom affiché actuel des responsables, libellé du motif).

**Tests PostgreSQL avant 0005.** `RolledBackScope` exécute le script **à appliquer** (`0005_AuditJournal.idempotent.sql`, sans `START TRANSACTION` / `COMMIT`) dans la transaction du test si `audit.events` n'existe pas, puis l'annule (`lock_timeout` 10 s). Les tests à données validées ou à deux connexions portent `requiresAuditJournal: true` et sont **ignorés** (`NOT_TESTED`) tant que 0005 n'est pas appliquée. `SchemaInspection` connaît l'état 0001–0005 ; `AuditJournalScript_ProducesExactlyTheExpectedSchema` le vérifie dans les catalogues de la transaction et prouve l'idempotence.

**Génération de la migration sans compiler dans `src/`.** `UseArtifactsOutput=true` et `ArtifactsPath=<dépôt>/.agent-artifacts-ef` (variables d'environnement MSBuild), puis `dotnet restore src/MMV.Infrastructure`, `dotnet tool run dotnet-ef migrations add AuditJournal …` et `migrations script` (direct, `--idempotent`, retour arrière) ; fins de ligne normalisées en LF ; déclencheurs ajoutés à la main dans `Up` / `Down`.

**Banc natif 03-D1.**

```powershell
# Bloc-notes (hors dépôt) : outil jetable d1native.cs (seed refusé si mmv_v2_test non vide / cleanup / residue / api), mmv_v2_test seulement
dotnet run <bloc-notes>/native/d1native.cs -- seed <bloc-notes>/native/creds.json
dotnet build MMV.slnx --artifacts-path .agent-artifacts           # aucune compilation dans src/
dotnet run <bloc-notes>/native/d1native.cs -- api .agent-artifacts/bin/MMV.Api/debug/MMV.Api.dll <bloc-notes>/native/api.log   # chaîne mmv_v2_test et magasin passés au seul processus
# apps/desktop
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"; npx tauri dev
$env:MMV_E2E_CREDENTIALS_FILE = '<bloc-notes>/native/creds.json'
node --use-system-ca e2e/run-native.mjs parcours-03d1 --prefix P03D_native --out <dossier> --theme light --size 1920x1080 --zoom 1 --day-offset 9 --time 14:00
node --use-system-ca e2e/run-native.mjs parcours-03d1 --prefix P03D_native --out <dossier> --theme light --size 1440x900 --zoom 1.25 --day-offset 10 --time 16:30
node --use-system-ca e2e/run-native.mjs parcours-03d1 --prefix P03D_native --out <dossier> --theme dark --size 1440x900 --zoom 1.5 --day-offset 8 --time 11:45
dotnet run <bloc-notes>/native/d1native.cs -- cleanup <suffixe>
```

**Tests.** .NET 349 (+35) et 50 ; PostgreSQL 45 (38 PASS, 5 `NOT_TESTED`, 2 `PostMigration` en échec attendu) ; frontend 225 (+29). QA : Q-03-200 à Q-03-232.
