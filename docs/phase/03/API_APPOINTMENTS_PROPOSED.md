# 03-B — Contrat HTTP Rendez-vous (PROPOSED, 17/10/2026)

*Amendement 17/10/2026 (03-B2) : contrat **remplacé** par le contrat implémenté [`API_APPOINTMENTS.md`](API_APPOINTMENTS.md) après approbation de D-03-13 à D-03-18. Écarts principaux : `responsibleRequired` porté par chaque motif, `receptionCapacity`, `startStepMinutes`, `cancelledByDisplayName`, codes `reception_capacity_full`, `reception_not_configured`, `responsible_unavailable`, `time_change`, `not_eligible`, 503 `appointments_not_configured`, table `appointments.responsibles`. Le texte ci-dessous est conservé comme historique.*

**Statut : `PROPOSED` — non implémenté par MMV.Api.** Aucune route `/api/appointments` n'existe côté serveur ; le service est désactivé côté client (`apps/desktop/src/appointments/appointmentsService.ts`). Le contrat est consommé par l'interface 03-B et reproduit par le double de test `apps/desktop/src/test/fakeAppointmentsApi.ts`. Il dépend des décisions D-03-13 à D-03-18 ([`DECISIONS_03B_RDV_PROPOSITION.md`](DECISIONS_03B_RDV_PROPOSITION.md)) ; il sera aligné sur la décision du propriétaire, puis typé dans OpenAPI comme le contrat Clients ([`API_CLIENTS.md`](API_CLIENTS.md)).

## Règles communes (identiques à Clients)

- Groupe `/api` : HTTPS obligatoire, `Authorization: Bearer`, CORS GET / POST / PUT, **jamais DELETE**.
- Policies serveur `AppointmentsRead`, `AppointmentsWrite`, `AppointmentsCancel`, chacune exigeant SA permission (`appointments.read|write|cancel`) ; `system.admin` n'en satisfait aucune. 401 `unauthenticated`, 403 `forbidden`.
- Corps d'erreur `{ code }` ; validation `{ code: "validation_failed", errors: { champ: [code] } }` (422) ; corps JSON stricts (champ inconnu, type incorrect → 400 `invalid_body`) ; nombres stricts.
- Instants ISO 8601 UTC (`…Z` ou `+00:00`), fin **exclue**. Le serveur convertit et contrôle dans le fuseau du magasin (D-03-13).
- Concurrence : `version` = `xmin` ; différente → 409 `version_conflict`, rien n'est écrit.
- Journaux : identifiants techniques seulement (`AppointmentId`, `ClientId`, `UserId`), jamais nom, notes ni motif d'annulation.

## Ressources

| Schéma | Type TypeScript (`apps/desktop/src/api/appointments.ts`) | Contenu |
|---|---|---|
| `AppointmentOptionsResponse` | `AppointmentOptions` | `timeZone` (IANA), `durationsMinutes` (entiers > 0), `reasons` (`{ code, label }`), `responsibles` (`{ id, displayName }`), `responsibleRequired`, `displayStartTime` / `displayEndTime` (`HH:mm`), `hiddenWeekdays` (0 = dimanche) |
| `AppointmentSummaryResponse` | `AppointmentSummary` | `id`, `clientId`, `clientReference`, `clientFirstName`, `clientLastName`, `clientArchived`, `responsibleUserId|null`, `responsibleDisplayName|null`, `startsAtUtc`, `endsAtUtc`, `reasonCode`, `status` (`planned|cancelled`), `version` — **sans** notes ni coordonnées client |
| `AppointmentDetailResponse` | `AppointmentDetail` | Summary + `notes|null`, `cancelledAtUtc|null`, `cancellationReason|null`, `createdAtUtc`, `updatedAtUtc` |
| `AppointmentListResponse` | `AppointmentSummary[]` (champ `items`) | rendez-vous qui **recoupent** la période, triés par début puis `id` |
| `AppointmentInputRequest` | `AppointmentInput` | `clientId`, `responsibleUserId|null`, `startsAtUtc`, `durationMinutes`, `reasonCode`, `notes|null` |
| `AppointmentUpdateRequest` | `AppointmentInput & { version }` | modification et déplacement (corps complet) |
| `AppointmentCancelRequest` | `{ version, reason|null }` | annulation |
| `AppointmentVersionRequest` | `{ version }` | rétablissement |

Parseurs stricts côté client : intervalle vide, statut inconnu, version négative, fuseau inconnu de l'environnement, plage affichée inversée ou 7 jours masqués → réponse incohérente (`invalid-response`), jamais un affichage silencieusement faux (`appointmentForm.test.ts`).

## Routes

| Méthode / route | Policy | Requête | Succès | Erreurs |
|---|---|---|---|---|
| `GET /api/appointments/options` | `appointments.read` | — | 200 `AppointmentOptionsResponse` | — |
| `GET /api/appointments` | `appointments.read` | `from`, `to` (instants, `to > from`, écart ≤ 31 jours), `status=planned|all` (défaut `planned`) ; `responsibleId` facultatif (`DEFERRED` côté écran) | 200 `AppointmentListResponse` | 400 `invalid_query` |
| `GET /api/appointments/{id}` | `appointments.read` | — | 200 `AppointmentDetailResponse` | 404 `appointment_not_found` |
| `POST /api/appointments` | `appointments.write` | `AppointmentInputRequest` | **201** + `Location` | 400 ; 422 `validation_failed` ; 409 `client_archived` / `appointment_conflict` |
| `PUT /api/appointments/{id}` | `appointments.write` | `AppointmentUpdateRequest` | 200 | 400 / `version_required` ; 404 ; 409 `appointment_cancelled` / `appointment_past` / `client_archived` / `version_conflict` / `appointment_conflict` ; 422 |
| `POST /api/appointments/{id}/cancel` | `appointments.cancel` | `AppointmentCancelRequest` | 200 | 400 ; 404 ; 409 `version_conflict` / `appointment_cancelled` / `appointment_past` ; 422 `reason: too_long` |
| `POST /api/appointments/{id}/restore` | `appointments.cancel` | `AppointmentVersionRequest` | 200 | 400 ; 404 ; 409 `version_conflict` / `appointment_not_cancelled` / `appointment_past` / `client_archived` / `appointment_conflict` |

Ordre des contrôles proposé (écritures) : authentification → policy → existence (404) → corps (400) → version (409 `version_conflict`) → état (`appointment_cancelled`, `appointment_past`, `client_archived`) → validation (422) → conflit horaire sous verrou (409 `appointment_conflict`). Pas de `DELETE` (405).

## Codes de validation par champ (422)

| Champ | Codes | Message affiché (`appointmentForm.ts`) |
|---|---|---|
| `clientId` | `required`, `not_found`, `archived` | « Sélectionnez un client », « Client introuvable », « Ce client est archivé… » |
| `startsAtUtc` | `required`, `invalid_format`, `invalid_step`, `out_of_range`, `in_past` | sous le champ Heure ; codes non traduits → « Valeur refusée par le serveur » |
| `durationMinutes` | `required`, `not_allowed` | « Choisissez une durée », « Durée non proposée par le serveur » |
| `responsibleUserId` | `required`, `not_found` | « Choisissez un responsable », « Responsable inconnu ou inactif » |
| `reasonCode` | `required`, `not_allowed` | « Le motif est requis », « Motif non proposé par le serveur » |
| `notes` | `too_long` | « 1000 caractères au maximum » |

## Persistance proposée (migration `0004_AppointmentsModule`, non appliquée)

Table `appointments.appointments` (15 colonnes, aucune colonne `xmin` créée) : `id` uuid PK ; `client_id` → `clients.clients` ; `responsible_user_id` → `auth.users` (nullable tant que D-03-14 n'est pas tranchée : l'obligation est contrôlée par l'application) ; `starts_at_utc`, `ends_at_utc` ; `reason_code` varchar(32) ; `notes` varchar(1000) ; `status` varchar(16) ; `cancelled_at_utc`, `cancelled_by_user_id`, `cancellation_reason` varchar(200) ; audit `created_*` / `updated_*`. 7 `CHECK` (fin > début, durée ≤ 12 h, statut, cohérence de l'annulation, motif d'annulation réservé aux annulés, format du code motif, mise à jour ≥ création), 5 FK `ON DELETE RESTRICT`, 6 index dont le partiel `ix_appointments_responsible_planned (responsible_user_id, starts_at_utc) WHERE status = 'planned'`. Contrainte de permissions étendue aux trois droits `appointments.*`. Scripts et empreintes : [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Dossier de migration 0004.
