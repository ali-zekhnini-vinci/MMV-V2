# 03 — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 uniquement)

| Source | Usage |
|---|---|
| `prompts/PHASE03_GO_CLAUDE_CODE.md` | Mandat Phase 03 (GO du 17/10/2026), versionné avec la phase |
| `prompts/PHASE03_A2_GO_CLAUDE_CODE.md` | Mandat 03-A2 « GO MÉTIER CLIENTS » (17/10/2026, message de session reproduit à l'identique) : G03-METIER A, B acceptés, C sur le principe, D en attente |
| `docs/phase/03/proposed/*.txt`, `docs/phase/03/sql/*.PROPOSED.*` | Proposition 03-A reprise dans `src/` puis régénérée (référence corrigée) ; fichiers conservés comme historique |
| `docs/phase/02/GATES_AND_DECISIONS.md` § O-02-01, `scripts/Invoke-MmvSqlScript.ps1` | procédure d'application (reprise, non exécutée) |
| `CLAUDE.md`, `README.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/ADR_ET_DECISIONS.md` | état Phase 02, DOCUMENTATION GATE, backlog technique (TanStack, FullCalendar Standard), ADR-014 |
| `docs/phase/02/IMPLEMENTATION_SPEC.md`, `docs/phase/02/GATES_AND_DECISIONS.md` | architecture implémentée (session, client HTTP, Shell, jetons), procédure de migration O-02-01 |
| `references/phase03/` (copie de travail, **non versionnée**) | cadrage et références visuelles DRAFT (ci-dessous) |

Aucune lecture de l'ancien dépôt, code, base ou règles MMV. La maquette du Lot 2 cite un ancien dépôt, une entité `Customer` et des « champs présents dans le code » : **ignorés** ; seuls les cotes, couleurs, libellés et états visuels sont exploités, sans règle métier déduite.

## Références (`references/phase03/`, empreintes SHA-256 relevées le 17/10/2026)

| Fichier | SHA-256 | Statut / usage |
|---|---|---|
| `PHASE03_CADRAGE.md` | `d508c7902c525c7c734f40a9eb1d93b0db758713aa851b203d82257601672770` | PROPOSED — lots, gates, interdits |
| `DECISIONS_METIER_A_RATIFIER.md` | `215381aa58ce8177c8d758ea382919f59176abae502353357912ea2a66d43d0f` | questions reprises dans `DECISIONS_G03_METIER.md` |
| `LOT2_ETATS_VISUELS.md` | `efcfb218c5e3b63111eac946e1eab55db6c4e17f217c76653faeae2d2762fc7e` | inventaire des 47 états → `STATE_MAP_47.md` |
| `lot2/MMV-Lot2-Clients-RendezVous.dc.html` | `26df60b6a1ed7422830df493adcf7b4665ca920b6525d8cc6a7ade417b0ec0a1` | **DRAFT** ; identique à l'entrée lot 2 de `registres/DESIGN_SOURCES_SHA256.csv` ; cotes et libellés lus, **aucun code repris** |
| `lot2/support.js` | `ae4f0ac8449655e17cca1e3b179effcb6817a3b0d8dc47f112a9c39c25c39fd7` | moteur de la planche, non lu ni repris |
| `lot2/_ds/` (12 fichiers) | — | design system de la planche ; jetons déjà intégrés en Phase 02 (`apps/desktop/src/styles/tokens.css`), non réimportés |

`prompts/PHASE03_GO_CLAUDE_CODE.md` : `f82f7c138d775ad256a4c81085b72f0ef0df689f831228e40034f3c0949e11d9`.
`prompts/PHASE03_A2_GO_CLAUDE_CODE.md` : `173182acf59b1ed59292a45d8b3395d4f445129862fcac7c294e0dd252e4a3df`.

## Dépendances ajoutées (ADR-014)

| Paquet | Version | Licence | Raison |
|---|---|---|---|
| `@tanstack/react-query` | 5.104.1 | MIT | données serveur (mandat) |
| `@tanstack/react-table` | 8.21.3 | MIT | listing Clients (mandat v8) |
| `@radix-ui/react-alert-dialog` (+ `react-dialog` 1.3.1) | 1.2.1 | MIT | confirmations accessibles (shadcn/ui AlertDialog) |
| `@radix-ui/react-tabs` | 1.2.1 | MIT | onglets de la fiche (shadcn/ui Tabs) |

Non installés en 03-A : FullCalendar (installé en 03-B, ci-dessous), `openapi-typescript` / `openapi-fetch` (O-03-01), Sonner, Zustand/Redux, dnd-kit, SignalR.

03-B (17/10/2026) — ajoutées à versions figées (`npm install --save-exact`, Q-03-103) :

| Paquet | Version | Licence | Raison |
|---|---|---|---|
| `@fullcalendar/react` (+ `@fullcalendar/core`, `@full-ui/headless-calendar` 7.1.0) | 7.1.0 | MIT (`LICENSE.md`, © 2026 Adam Shaw) | agenda Jour / Semaine, FullCalendar **Standard** (mandat) ; aucun paquet Premium (`resource*`, timeline) |
| `temporal-polyfill` (+ `temporal-spec` 1.0.1 Apache-2.0, `temporal-utils` 1.0.3 MIT) | 1.0.5 | MIT | pair obligatoire de FullCalendar 7 ; calculs de fuseau (`appointments/time.ts`) |

Choix de version : 7.1.0 (05/09/2026) est la seule 7.x publiée depuis au moins deux semaines le 17/10/2026 (7.1.1 : 06/10, 7.1.2 : 12/10, 7.1.3 : 16/10) ; `temporal-polyfill` 1.0.5 (11/09/2026) plutôt que 1.0.6 (13/10/2026). Repli 6.1.22 non retenu (publiée le 16/10/2026).

03-A2 (17/10/2026) : **aucune dépendance ajoutée** (npm ni NuGet ; verrous inchangés). Backend : EF Core 10.0.4, Npgsql EF 10.0.3, `Microsoft.AspNetCore.OpenApi` 10.0.13 (Microsoft.OpenApi 2.x transitif) déjà présents.

## Traçabilité exigence → chemin

| Exigence du mandat | Chemin |
|---|---|
| §1 Préflight, branche | `QA_MATRIX.md` Q-03-01 à Q-03-06 |
| §2 TanStack Query v5 | `apps/desktop/src/api/queryClient.ts`, `apps/desktop/src/clients/clientQueries.ts`, `apps/desktop/src/shell/Shell.tsx` |
| §2 TanStack Table v8 | `apps/desktop/src/clients/ClientsTable.tsx` |
| §2 Navigation `/clients` | `apps/desktop/src/shell/navigation.ts`, `apps/desktop/src/clients/ClientsModule.tsx` |
| §2 Liste, filtres, tri, pagination, états | `apps/desktop/src/clients/ClientsListView.tsx`, `ClientStates.tsx` |
| §2 Formulaire unique, dirty, fermeture, clavier | `apps/desktop/src/clients/ClientFormView.tsx`, `clientForm.ts`, `apps/desktop/src/shell/navigationGuard.ts` |
| §2 Fiche et onglets reportés | `apps/desktop/src/clients/ClientDetailView.tsx`, `apps/desktop/src/components/ui/tabs.tsx` |
| §2 Archivage / réactivation | `apps/desktop/src/clients/ClientLifecycle.tsx`, `apps/desktop/src/components/ui/alert-dialog.tsx` |
| §2 Erreurs 401/403/409, cache, annulation | `apps/desktop/src/api/client.ts`, `apps/desktop/src/api/queryClient.ts`, `apps/desktop/src/clients/clientQueries.ts` |
| §2 Aucune donnée fictive en fonctionnement normal | `apps/desktop/src/clients/clientsService.ts` ; fixtures : `apps/desktop/src/test/fakeClientsApi.ts` |
| §3 Correspondance des états | `docs/phase/03/STATE_MAP_47.md` |
| §3 Modèle, permissions, arbitrage | `docs/phase/03/DECISIONS_G03_METIER.md` |
| §3 API | `docs/phase/03/API_CLIENTS_PROPOSED.md`, `apps/desktop/src/api/clients.ts` |
| §3 EF Core / SQL non appliqué | `docs/phase/03/proposed/`, `docs/phase/03/sql/` |
| §4 Rendez-vous | `docs/phase/03/RDV_03B_PREPARATION.md` |
| §5 Tests | `apps/desktop/src/**/*.test.{ts,tsx}`, `docs/phase/03/evidence/` |

## Traçabilité 03-A2 (mandat `prompts/PHASE03_A2_GO_CLAUDE_CODE.md`)

| Exigence | Chemin |
|---|---|
| §1 Modèle Clients | `src/MMV.Domain/Clients/Client.cs`, `src/MMV.Domain/Clients/ClientSearch.cs`, `src/MMV.Application/Clients/ClientInputValidator.cs` |
| §1 Référence PostgreSQL, concurrence | `src/MMV.Infrastructure/Clients/ClientModel.cs`, `src/MMV.Infrastructure/Clients/ClientStore.cs`, `src/MMV.Application/Clients/ClientService.cs` |
| §2 Permissions, `system.admin` sans droit Clients | `src/MMV.Domain/Identity/Permissions.cs`, `src/MMV.Api/Auth/AuthSetup.cs` |
| §2 Attribution par l'outil de maintenance | `src/MMV.Application/Identity/PermissionAdministrationService.cs`, `src/MMV.Maintenance/Program.cs` |
| §3 Endpoints, validations | `src/MMV.Api/Clients/ClientsEndpoints.cs`, `src/MMV.Api/Auth/AuthEndpoints.cs`, `src/MMV.Api/Cors/DesktopCors.cs` |
| §3 Migration 0003 et SQL exact (non appliqués) | `src/MMV.Infrastructure/Persistence/Migrations/20261016230630_ClientsModule.cs`, `docs/phase/03/sql/0003_ClientsModule.sql`, `.idempotent.sql`, `.rollback.sql` |
| §3 OpenAPI et types TypeScript | `src/MMV.Api/Program.cs`, `apps/desktop/src/api/contract/clients.openapi.json`, `tests/MMV.Api.Tests/Clients/ClientsOpenApiContractTests.cs`, `apps/desktop/src/api/clientsContract.test.ts`, `docs/phase/03/API_CLIENTS.md` |
| §3 TanStack sur la vraie API | `apps/desktop/src/clients/clientsService.ts` |
| §4 Interface, états | `apps/desktop/src/clients/`, `apps/desktop/src/shell/Shell.test.tsx` |
| §5 Tests | `tests/MMV.Api.Tests/Clients/`, `tests/MMV.Maintenance.Tests/BootstrapCommandProcessTests.cs`, `tests/MMV.PostgreSql.Tests/ClientsPostgreSqlTests.cs`, `tests/MMV.PostgreSql.Tests/SchemaInspection.cs`, `tests/MMV.PostgreSql.Tests/RolledBackScope.cs`, `docs/phase/03/QA_MATRIX.md` Q-03-50 à Q-03-77 |
| §6 Rendez-vous non développé | aucun code ; `docs/phase/03/RDV_03B_PREPARATION.md` inchangé |
| §7 DOCUMENTATION GATE | `docs/phase/03/GATES_AND_DECISIONS.md` (G03-DOC) |

## Traçabilité 03-A3 (mandat `prompts/PHASE03_A3_GO_CLAUDE_CODE.md`)

Sources supplémentaires lues : `prompts/PHASE03_A3_GO_CLAUDE_CODE.md` (message de session du 17/10/2026, reproduit à l'identique), `docs/phase/02/QA_MATRIX.md` § Exécution O-02-01 et `docs/phase/02/IMPLEMENTATION_SPEC.md` § banc natif (méthode reprise), `references/phase03/LOT2_ETATS_VISUELS.md` (correspondance `data-mount`). Planche `lot2/MMV-Lot2-Clients-RendezVous.dc.html` : empreinte revérifiée **inchangée** (`26df60b6…0ec0a1`) ; rendue dans Edge headless 155 (profil temporaire hors dépôt, `support.js` exécuté par la planche, Lucide chargé depuis unpkg par la planche elle-même) pour 17 états Clients ; rendus conservés **hors dépôt** (`references/` non versionné) ; aucun code ni texte de la planche repris.

| Exigence | Chemin |
|---|---|
| §1 Script autorisé, application, preuves | `docs/phase/03/sql/0003_ClientsModule.idempotent.sql`, `scripts/Invoke-MmvSqlScript.ps1` (inchangé), `docs/phase/03/evidence/P03A3_0003_apply_mmv_v2_{dev,test}_20261017T1055Z.log`, `docs/phase/03/evidence/P03A3_postmigration_readonly_20261017T1056Z.log` |
| §2 Tests réels, résidus | `tests/MMV.PostgreSql.Tests/ClientsPostgreSqlTests.cs`, `tests/MMV.Api.Tests/Clients/ClientTestDoubles.cs`, `tests/MMV.Api.Tests/Clients/ClientServiceTests.cs`, `docs/phase/03/evidence/P03A3_dotnet_20261017.log`, `docs/phase/03/evidence/P03A3_frontend_20261017.log` |
| §3 Procédure de permissions | `docs/phase/03/PROCEDURE_PERMISSIONS_CLIENTS.md`, `src/MMV.Maintenance/Program.cs` (inchangé), `docs/phase/03/evidence/P03A3_maintenance_permissions_readonly_20261017T1110Z.log` |
| §3 Actions masquées / désactivées | `apps/desktop/src/clients/clientPermissions.ts`, `apps/desktop/src/api/auth.ts`, `apps/desktop/src/clients/ClientsListView.tsx`, `apps/desktop/src/clients/ClientsTable.tsx`, `apps/desktop/src/clients/ClientDetailView.tsx`, `apps/desktop/src/clients/ClientsModule.tsx`, `apps/desktop/src/clients/Clients.css`, `apps/desktop/src/clients/ClientsModule.test.tsx` |
| §4 Chaîne native, captures, comparaison | `apps/desktop/e2e/run-native.mjs`, `docs/phase/03/evidence/P03A3_native_clients_e2e_20261017T1120Z.log`, `docs/phase/03/evidence/P03_native_clients_*.png` (22), `docs/phase/03/STATE_MAP_47.md` § Amendement 03-A3 |
| §4 Écart corrigé (surlignage) | `apps/desktop/src/clients/searchText.ts`, `apps/desktop/src/clients/highlight.tsx`, `apps/desktop/src/clients/clientForm.test.ts` |
| §5 DOCUMENTATION GATE | `docs/phase/03/GATES_AND_DECISIONS.md` (G03-DOC) |
| §6 Livraison | branche locale `feat/phase-03-clients-rdv` ; archive `git archive` hors dépôt |

## Traçabilité 03-B (mandat `prompts/PHASE03_B_GO_CLAUDE_CODE.md`)

Sources supplémentaires lues : `prompts/PHASE03_B_GO_CLAUDE_CODE.md` (message de session du 17/10/2026, reproduit à l'identique), `docs/phase/03/RDV_03B_PREPARATION.md`, `DECISIONS_G03_METIER.md`, `STATE_MAP_47.md`, planches 28–43 de `references/phase03/lot2/MMV-Lot2-Clients-RendezVous.dc.html` (fonctions `calDay`, `calWeek`, `rdvDetailSide`, `rdvFormSide`, `rdvPickerSide`, `cancelRdvScreen`, `rdvSuccessScreen` ; empreinte inchangée, § Références), `node_modules/@fullcalendar/react` (types, `skeleton.css`, `LICENSE.md`). Ancien MMV non consulté.

| Exigence | Chemin |
|---|---|
| §2 Notice persistante (O-03-15) | `apps/desktop/src/clients/ClientsModule.tsx`, `apps/desktop/src/clients/clientsUi.ts`, `apps/desktop/src/clients/ClientFormView.tsx`, `apps/desktop/src/clients/ClientsModule.test.tsx` |
| §3 FullCalendar Standard, licence | `apps/desktop/package.json`, `apps/desktop/package-lock.json`, `apps/desktop/src/appointments/AppointmentsCalendar.tsx` |
| §3 Vues, détail, formulaires, états | `apps/desktop/src/appointments/` (`AppointmentsModule.tsx`, `AppointmentDetailPanel.tsx`, `AppointmentFormPanel.tsx`, `ClientPicker.tsx`, `Appointments.css`), `apps/desktop/src/shell/Shell.tsx`, `apps/desktop/src/shell/navigation.ts`, `apps/desktop/src/components/ui/alert-dialog.tsx` |
| §3 TanStack Query, RHF + Zod | `apps/desktop/src/appointments/appointmentQueries.ts`, `apps/desktop/src/appointments/appointmentForm.ts` |
| §3 Clavier (création, déplacement) | `AppointmentsCalendar.tsx` (↑/↓), `AppointmentFormPanel.tsx` (mode `move`), `ClientPicker.tsx` (combobox) |
| §3 Pas de règle déduite des maquettes | `apps/desktop/src/api/appointments.ts` (`AppointmentOptions` servies par le serveur), `apps/desktop/src/appointments/appointmentsView.ts` (teintes par position) |
| §4 Proposition consolidée | `docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md` |
| §4 Modèles, contrat, migration proposés | `docs/phase/03/API_APPOINTMENTS_PROPOSED.md`, `docs/phase/03/proposed/Appointment.cs.txt`, `docs/phase/03/proposed/AppointmentModel.cs.txt`, `docs/phase/03/proposed/20261017141136_AppointmentsModule.cs.txt`, `docs/phase/03/proposed/permissions_dbcontext_snapshot_0004.diff.txt`, `docs/phase/03/sql/0004_AppointmentsModule.PROPOSED.sql`, `.idempotent.sql`, `.rollback.sql` |
| §4 Service désactivé, aucun rendez-vous réel | `apps/desktop/src/appointments/appointmentsService.ts` |
| §5 Tests | `apps/desktop/src/appointments/*.test.ts(x)`, `apps/desktop/src/test/fakeAppointmentsApi.ts`, `apps/desktop/src/test/fakeMmvApi.ts`, `docs/phase/03/evidence/P03B_frontend_20261017.log`, `docs/phase/03/evidence/P03B_dotnet_proposal_20261017.log`, `docs/phase/03/evidence/P03B_bench_rdv_*_1440x900_20261017.jpg` |
| §5 DOCUMENTATION GATE | `docs/phase/03/GATES_AND_DECISIONS.md` (G03-DOC) |

