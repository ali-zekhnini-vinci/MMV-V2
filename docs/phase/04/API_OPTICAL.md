# 04-B — Contrat d'API du dossier optique (`/api/optical`) — IMPLÉMENTÉ

**Statut (27/10/2026) : implémenté dans MMV.Api (04-B), opérationnel après application de `0006_OpticalModule` (G04-SQL).** Remplace, pour l'état courant, le contrat proposé [`API_OPTICAL_PROPOSED.md`](API_OPTICAL_PROPOSED.md) (conservé comme historique). Source de vérité : [`src/MMV.Api/Optical/OpticalEndpoints.cs`](../../../src/MMV.Api/Optical/OpticalEndpoints.cs), [`OpticalContracts.cs`](../../../src/MMV.Api/Optical/OpticalContracts.cs) ; extrait OpenAPI versionné [`apps/desktop/src/api/contract/optical.openapi.json`](../../../apps/desktop/src/api/contract/optical.openapi.json), comparé au document réel par `OpticalOpenApiContractTests` et aux types TypeScript par `apps/desktop/src/api/optical.test.ts`.

## 1. Règles transverses

- Groupe `/api` : HTTPS obligatoire (403 `https_required`), CORS du poste, session par jeton (`Authorization: Bearer`).
- **Droits vérifiés AVANT toute lecture** (policies cumulatives) : lecture = `clients.read` + `optical.read` ; écriture ordonnance = + `optical.prescriptions.write` ; écriture mesure = + `optical.measurements.write` ; invalidation (les deux types) = + `optical.invalidate`. `system.admin` ne satisfait aucune policy. Sans droit : 403 identique pour un identifiant existant ou inconnu.
- `Cache-Control: no-store` sur toutes les réponses ; corps JSON **stricts** (membre inconnu → 400 `invalid_body`, nombres stricts) ; aucune route `DELETE`, aucune route de conversion, aucune route documentaire.
- Écritures : `requestId` (uuid) **obligatoire** (400 `request_id_required`) ; `version` (`xmin` lu) obligatoire sauf création (400 `version_required`). Réémission d'une requête déjà appliquée : état courant renvoyé, rien n'est réécrit.
- Erreurs à codes bornés, jamais une valeur saisie : 404 `not_found` / `client_not_found` ; 409 `version_conflict`, `client_archived`, `invalidated`, `already_invalidated`, `not_draft`, `not_rectification`, `draft_exists`, `revision_conflict`, `request_id_reused` ; 422 `validation_failed` (`errors` : `number_expected`, `too_long`, `invalid_format`, `out_of_range`, `not_eligible`, `required`) ou `incomplete` (`errors` : `optical_values_missing`, `sphere_zero_only`, `acuity_only`, `axis_required`, `base_required`, `required`) — clés `od.<champ>`, `og.<champ>`, `provenance.<champ>`, `correction`, `notes`, `reasonCode`, `comment`.
- Valeurs : chaînes telles que saisies ; absent = `null` (jamais `0`). Dates métier `AAAA-MM-JJ`.

## 2. Lecture

| Route | Réponse |
|---|---|
| `GET /api/optical/records?q&kind=all\|prescription\|measurement&status=all\|draft\|confirmed\|rectification&includeInvalidated=false\|true&sort=date\|client\|recorded&dir=desc\|asc&page&pageSize` | `OpticalSummaryPageResponse` (liste blanche stricte, 400 `invalid_query` sinon) ; invalidés exclus par défaut ; dates absentes en fin de liste |
| `GET /api/optical/clients/{clientId}/records?includeInvalidated=` | `OpticalSummaryResponse[]` du plus récent au plus ancien ; 404 `client_not_found` |
| `GET /api/optical/measurers` | comptes actifs détenant `optical.measurements.write` (`id`, `displayName`) |
| `GET /api/optical/prescriptions/{id}` · `GET /api/optical/measurements/{id}` | `OpticalRecordResponse` : client, cycle, invalidation, `lastConfirmed`, `openDraft`, `revisions`, `historyAvailable`, `events`, `version`. **Sans `audit.read`** : `historyAvailable=false`, `revisions` limitées à la dernière confirmée et au brouillon ouvert, `events` vide |

Ligne de liste : révision affichée = dernière confirmée, sinon brouillon de première saisie ; statut dérivé (`draft`, `confirmed`, `rectification`, `invalidated`) ; `source` = prescripteur (ordonnance) ou collaborateur ayant mesuré (mesure).

## 3. Écriture (mêmes routes sous `/prescriptions` et `/measurements`, corps propres à chaque type)

| Route | Corps | Succès |
|---|---|---|
| `POST /api/optical/{type}` | `{ requestId, clientId, confirm?, content }` | 201 (brouillon, ou révision 1 confirmée si `confirm: true` — **atomique**, refus = rien de créé) ; 200 si réémission |
| `PUT /api/optical/{type}/{id}/draft` | `{ requestId, version, content }` | 200 ; contenu inchangé → aucune écriture (version identique) |
| `POST /api/optical/{type}/{id}/draft/confirm` | `{ requestId, version, content? }` | 200 ; avec `content` : modification + confirmation **atomiques** (« Enregistrer ») ; sans : confirmation du brouillon tel quel |
| `POST /api/optical/{type}/{id}/rectifications` | `{ requestId, version, expectedRevisionNo, reasonCode, comment? }` | 201 brouillon de rectification (contenu de la révision de base) ; 200 si réémission |
| `POST /api/optical/{type}/{id}/draft/discard` | `{ requestId, version }` | 200 ; rectification seulement (`not_rectification` sinon) ; conservée, statut `discarded` |
| `POST /api/optical/{type}/{id}/invalidate` | `{ requestId, version, reasonCode, comment? }` | 200 ; abandonne la rectification ouverte dans la même opération ; permise pour un client archivé |

`content` : `{ provenance, od, og, notes }` ; ordonnance `provenance = { prescriberName, prescriberQualification, prescriberIdentifier, prescribedOn, receivedOn }` ; mesure `provenance = { measuredOn, measuredByUserId, category }` ; `od` / `og` = `{ sphere, cylinder, axis, addition, prism, base, acuity }`. Motifs : rectification `transcription_error` | `additional_information` | `other` ; invalidation `entry_error` | `duplicate` | `unacceptable_document` | `other` (commentaire exigé pour `other`).

## 4. Historique (route existante modifiée)

`GET /api/audit/clients/{clientId}` (policy `AuditRead`) : événements `optical` **exclus** si la session n'a pas `clients.read` + `optical.read` ; `module=optical` → 403 dans ce cas. Codes `prescription.*` / `measurement.*` : `created`, `draft_updated`, `confirmed`, `rectification_started`, `rectification_discarded`, `invalidated` ; champs conservés : `revisionNo`, `baseRevisionNo`, `rectificationReasonCode`, `invalidationReasonCode` ; masqués : `provenance`, `values`, `notes`, `comment`.

## 5. Préparation de la vente (inerte)

Aucune route. Une vente future référencera `(type, id, revisionNo)` d'une révision confirmée (immuable) ; une mesure magasin ne sera jamais acceptée là où une ordonnance médicale est exigée (D-04-01).

*Ajout daté 28/10/2026 (04-C, O-04-48) :* la réponse d'enregistrement (`GET`/écritures `…/prescriptions/{id}`, `…/measurements/{id}`) porte en plus `citedUsers` : liste `{ id, displayName }` des comptes cités dans la provenance des révisions servies (collaborateur ayant mesuré), **y compris** s'ils ne sont plus actifs ni habilités ; `/api/optical/measurers` reste limité aux comptes actifs habilités. Ajout seul (instantané [`apps/desktop/src/api/contract/optical.openapi.json`](../../../apps/desktop/src/api/contract/optical.openapi.json)).

