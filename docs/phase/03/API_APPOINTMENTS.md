# 03 — Contrat HTTP Rendez-vous (IMPLEMENTED, 03-B2, 17/10/2026)

**Statut : `IMPLEMENTED` dans MMV.Api — migration `0004_AppointmentsModule` NON appliquée.** Tant que la migration n'est pas appliquée et que les paramètres du magasin ne sont pas configurés, les routes répondent `503 appointments_not_configured` (après authentification et autorisation) et aucune donnée n'est lue ni écrite. Décisions : D-03-13 à D-03-18 approuvées le 17/10/2026 ([`DECISIONS_03B_RDV_PROPOSITION.md`](DECISIONS_03B_RDV_PROPOSITION.md) § Décision du propriétaire). Remplace le contrat proposé [`API_APPOINTMENTS_PROPOSED.md`](API_APPOINTMENTS_PROPOSED.md) (historique 03-B).

Sources : `src/MMV.Api/Appointments/AppointmentsEndpoints.cs`, `src/MMV.Application/Appointments/`, `src/MMV.Domain/Appointments/`, `src/MMV.Infrastructure/Appointments/`. Instantané OpenAPI contrôlé des deux côtés : [`apps/desktop/src/api/contract/appointments.openapi.json`](../../../apps/desktop/src/api/contract/appointments.openapi.json) (`AppointmentsOpenApiContractTests.cs` côté .NET, `appointmentsContract.test.ts` côté TypeScript).

## Règles communes (identiques à Clients)

- Groupe `/api` : HTTPS obligatoire (`403 https_required`), `Authorization: Bearer`, CORS GET / POST / PUT, **jamais DELETE** (405).
- Policies `AppointmentsRead`, `AppointmentsWrite`, `AppointmentsCancel`, chacune exigeant SA permission (`appointments.read|write|cancel`) ; `system.admin` n'en satisfait aucune. 401 `unauthenticated`, 403 `forbidden`.
- Paramètres du magasin absents ou invalides : **503 `appointments_not_configured`** (filtre de groupe, après la policy).
- Corps d'erreur `{ code }` ; validation `{ code: "validation_failed", errors: { champ: [code] } }` (422) ; corps JSON stricts (champ inconnu, type incorrect, nombre en chaîne → 400 `invalid_body`).
- Instants ISO 8601 explicitement UTC (`…Z` ou `…+00:00`), fin **exclue** ; contrôles dans le fuseau du magasin (D-03-13), jamais celui du poste.
- Concurrence : `version` = `xmin` ; différente → 409 `version_conflict`, rien n'est écrit.
- Journaux : identifiants techniques seulement (`AppointmentId`, `UserId`) — jamais nom de client, notes ni motif d'annulation (D-03-17).

## Paramètres du magasin (section serveur `Appointments`, D-03-13)

Une installation MMV = un magasin en V1. **Aucune valeur par défaut n'est inventée** ; les valeurs réelles sont à renseigner par le propriétaire ([`PROCEDURE_RENDEZ_VOUS_ACTIVATION.md`](PROCEDURE_RENDEZ_VOUS_ACTIVATION.md)).

| Clé | Obligatoire | Contrôle | Absente |
|---|---|---|---|
| `Appointments:TimeZone` | oui | `Africa/Casablanca` ou `Europe/Brussels`, connu du système | 503 |
| `Appointments:DisplayStartTime` / `DisplayEndTime` | oui | `HH:mm` (fin `24:00` admise), fin > début ; affichage seulement | 503 |
| `Appointments:HiddenWeekdays` | non | jours 0–6 distincts, au moins un jour affiché ; affichage seulement | aucun jour masqué |
| `Appointments:ReceptionCapacity` | non | entier ≥ 1 | aucun rendez-vous sans responsable |

## Ressources

| Schéma | Type TypeScript (`apps/desktop/src/api/appointments.ts`) | Contenu |
|---|---|---|
| `AppointmentOptionsResponse` | `AppointmentOptions` | `timeZone`, `durationsMinutes` (15, 30, 45, 60, 90, 120), `startStepMinutes` (5), `reasons` (`{ code, label, responsibleRequired }`, liste fermée D-03-18), `responsibles` (actifs ET éligibles : `{ id, displayName }`), `receptionCapacity` (`int | null`), `displayStartTime`, `displayEndTime`, `hiddenWeekdays` |
| `AppointmentSummaryResponse` | `AppointmentSummary` | `id`, `clientId`, `clientReference`, `clientFirstName`, `clientLastName`, `clientArchived`, `responsibleUserId|null`, `responsibleDisplayName|null`, `startsAtUtc`, `endsAtUtc`, `reasonCode`, `status` (`planned|cancelled`), `version` — **ni** téléphone, **ni** email, **ni** notes |
| `AppointmentDetailResponse` | `AppointmentDetail` | Summary + `notes|null`, `cancelledAtUtc|null`, `cancelledByDisplayName|null`, `cancellationReason|null`, `createdAtUtc`, `updatedAtUtc` |
| `AppointmentListResponse` | `AppointmentSummary[]` (`items`) | rendez-vous qui **recoupent** la période, triés par début puis `id` |
| `AppointmentInputRequest` | `AppointmentInput` | `clientId`, `responsibleUserId|null`, `startsAtUtc`, `durationMinutes`, `reasonCode`, `notes|null` |
| `AppointmentUpdateRequest` | `AppointmentInput & { version }` | modification et déplacement (corps complet) |
| `AppointmentCancelRequest` | `{ version, reason|null }` | annulation (motif ≤ 200, rogné, vide → `null`) |
| `AppointmentVersionRequest` | `{ version }` | rétablissement |

## Routes

| Méthode / route | Policy | Succès | Erreurs |
|---|---|---|---|
| `GET /api/appointments/options` | `appointments.read` | 200 | 503 |
| `GET /api/appointments?from&to[&status=planned|all]` | `appointments.read` | 200 | 400 `invalid_query` (paramètre inconnu ou répété, instant non UTC, `to ≤ from`, écart > 31 jours) |
| `GET /api/appointments/{id}` | `appointments.read` | 200 | 404 `appointment_not_found` |
| `POST /api/appointments` | `appointments.write` | **201** + `Location` | 400 ; 422 ; 409 `client_archived` / `appointment_conflict` / `reception_capacity_full` |
| `PUT /api/appointments/{id}` | `appointments.write` | 200 | 400 / `version_required` ; 404 ; 409 `version_conflict` / `appointment_cancelled` / `client_archived` / `appointment_conflict` / `reception_capacity_full` ; 422 |
| `POST /api/appointments/{id}/cancel` | `appointments.cancel` | 200 | 400 ; 404 ; 409 `version_conflict` / `appointment_cancelled` ; 422 `reason: too_long` |
| `POST /api/appointments/{id}/restore` | `appointments.cancel` | 200 | 400 ; 404 ; 409 `version_conflict` / `appointment_not_cancelled` / `appointment_past` / `client_archived` / `reception_not_configured` / `responsible_unavailable` / `appointment_conflict` / `reception_capacity_full` |

Ordre des contrôles (écritures) : authentification → policy → paramètres du magasin (503) → corps (400) → existence (404) → version (409) → état (annulé / non annulé) → validation de saisie (422) → passé → **verrou de la ressource visée** → client (verrou partagé ; introuvable 422, archivé 409) → éligibilité du responsable (422) → chevauchement ou capacité (409) → enregistrement.

## Codes de validation par champ (422)

| Champ | Codes |
|---|---|
| `clientId` | `required`, `not_found` |
| `startsAtUtc` | `required`, `invalid_format` (non UTC), `invalid_step` (pas de 5 min, heure du magasin), `out_of_range` (franchit minuit ; finir à 00:00 est admis), `time_change` (début ou fin dans une heure ambiguë, ou créneau enjambant un changement d'heure), `in_past` (création ; modification qui change le créneau) |
| `durationMinutes` | `required`, `not_allowed` |
| `responsibleUserId` | `required` (motif personnalisé), `reception_not_configured` (motif d'accueil sans capacité configurée), `not_eligible` (compte inexistant, désactivé ou non éligible) |
| `reasonCode` | `required`, `not_allowed` |
| `notes` | `too_long` (> 1000) |
| `reason` (annulation) | `too_long` (> 200) |

## Attribution, conflits et capacité (D-03-14, D-03-15)

- **Rendez-vous personnalisé** (`conseil_devis`, `choix_equipement`, `prise_mesures`, `essayage_ajustement`, `autre`) : responsable obligatoire, actif et éligible ; chevauchement refusé avec un autre rendez-vous **planifié** du même collaborateur (`a.début < b.fin ET b.début < a.fin` : bout à bout admis) → 409 `appointment_conflict`.
- **Rendez-vous d'accueil** (`retrait_commande`, `sav_reparation`) : avec responsable → même contrôle que ci-dessus ; sans responsable → **capacité d'accueil** : l'occupation maximale simultanée des autres rendez-vous d'accueil planifiés, calculée **à chaque instant** du créneau (balayage des bornes), plus un, ne doit pas dépasser `ReceptionCapacity` → sinon 409 `reception_capacity_full`. Capacité absente → aucun rendez-vous sans responsable.
- **Transactions** : `READ COMMITTED` ; `pg_advisory_xact_lock(1296913746, clé)` sur la ressource visée (agenda du collaborateur : clé dérivée de son identifiant ; pool d'accueil : clé 0), clés croissantes sans doublon (ordre stable, aucun interblocage) ; puis `SELECT … FROM clients.clients … FOR SHARE` (un archivage concurrent attend) ; puis contrôles et écriture. Deux postes simultanés : le second attend le verrou du premier, puis voit sa réservation.
- Une modification qui ne change ni le créneau ni le responsable (motif, notes) ne réserve rien : ni verrou, ni contrôle de chevauchement, de capacité, d'éligibilité ou de passé.

## Annulation et rétablissement (D-03-16)

Annulation logique (`cancelled`, auteur et date conservés, motif facultatif ≤ 200), permise pour un client archivé et pour un rendez-vous passé ; libère le créneau ou la place d'accueil. Rétablissement : nouveau contrôle complet (client actif, collaborateur actif et éligible, chevauchement ou capacité) ; refusé si le créneau a commencé (`appointment_past`, interprétation de « création dans le passé interdite » — O-03-23). Rendez-vous annulé non modifiable. Aucune suppression physique.

## Éligibilité des collaborateurs (D-03-14, D-03-17)

Table `appointments.responsibles` (`user_id`, `is_eligible`, dates), indépendante des permissions ; gérée par `MMV.Maintenance responsibles list | enable <identifiant> | disable <identifiant>` (cible stricte `mmv_v2_dev`, confirmation interactive ; retrait = `is_eligible = false`, jamais de suppression). Un collaborateur désactivé ou retiré n'est plus proposé ; ses rendez-vous existants restent affichés avec son nom ; leurs notes et motif restent modifiables, leur créneau ne peut être modifié qu'en réattribuant à un collaborateur éligible.

## Persistance (migration `0004_AppointmentsModule`, NON appliquée)

Dossier SQL exact et empreintes : [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Dossier de migration 0004 — version 03-B2.
