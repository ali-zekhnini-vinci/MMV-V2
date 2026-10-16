# 03 — Contrat HTTP Clients (IMPLEMENTED, 03-A2, 17/10/2026)

**Statut : `IMPLEMENTED`** dans MMV.Api (`src/MMV.Api/Clients/ClientsEndpoints.cs`), testé sur le pipeline HTTP réel avec stores en mémoire (`tests/MMV.Api.Tests/Clients/`). **Données réelles indisponibles tant que la migration `0003_ClientsModule` n'est pas appliquée** (revue SQL du propriétaire) : jusque-là, toute route authentifiée et autorisée échoue côté base (500, erreur technique bornée côté interface). Décisions : G03-METIER A et B **acceptées**, C **acceptée sur le principe** (17/10/2026, [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md)). Remplace la proposition [`API_CLIENTS_PROPOSED.md`](API_CLIENTS_PROPOSED.md), conservée comme historique.

Schémas exacts : document OpenAPI généré par le serveur (`/openapi/v1.json`, Development), extrait versionné [`apps/desktop/src/api/contract/clients.openapi.json`](../../../apps/desktop/src/api/contract/clients.openapi.json), comparé au document réel par `tests/MMV.Api.Tests/Clients/ClientsOpenApiContractTests.cs` et aux types TypeScript par `apps/desktop/src/api/clientsContract.test.ts`.

## Règles communes

- Groupe `/api` existant : HTTPS obligatoire (403 `https_required`), `Authorization: Bearer <jeton opaque>` (ADR-007 `PROPOSED`), CORS `desktop-api` : GET, POST, **PUT** ; jamais DELETE.
- Autorisation **serveur** par policies `ClientsRead`, `ClientsWrite`, `ClientsArchive` (`src/MMV.Api/Auth/AuthSetup.cs`), chacune exigeant SA permission (`clients.read`, `clients.write`, `clients.archive`). `system.admin` n'en satisfait aucune (403 testé). 401 `{"code":"unauthenticated"}`, 403 `{"code":"forbidden"}`.
- Corps d'erreur : `ErrorResponse { code }` ; validation : `ValidationErrorResponse { code: "validation_failed", errors: { champ: [code] } }`, codes `required`, `too_long`, `invalid_format`, `out_of_range`. Aucune valeur saisie, aucun message libre, aucune trace.
- Corps JSON **stricts** : champ inconnu (ex. `socialSecurityNumber`), type incorrect, JSON illisible → 400 `invalid_body` ; `Content-Type` non JSON → 415 (routage). Nombres stricts (jamais lus depuis une chaîne).
- Dates : `…Utc` ISO 8601 (`+00:00`) ; `birthDate` `AAAA-MM-JJ`. Horodatages tronqués à la microseconde (précision PostgreSQL).
- Concurrence : `version` = `xmin` PostgreSQL (entier non signé), renvoyée à chaque lecture et exigée à chaque écriture ; différente de la version courante → 409 `version_conflict`, rien n'est écrit. Course entre lecture et écriture : `UPDATE … WHERE xmin = @lue` → 409 également.
- Journaux (D-03-10) : événements 3001 (écriture) et 3002 (conflit) avec identifiants techniques seulement (`ClientId`, `UserId`) ; aucun nom, téléphone, email, adresse, note ni terme de recherche (test `Logs_ContainTechnicalIdentifiersOnly`, niveaux `Microsoft.AspNetCore` / `Microsoft.EntityFrameworkCore` = Warning).

## Ressources (schémas OpenAPI ↔ types TypeScript)

| Schéma serveur | Type `apps/desktop/src/api/clients.ts` | Contenu |
|---|---|---|
| `ClientSummaryResponse` | `ClientSummary` | `id` (uuid), `reference`, `firstName`, `lastName`, `phone|null`, `email|null`, `city|null`, `status` (`ClientStatusValue` : `active|archived`), `createdAtUtc`, `version` |
| `ClientDetailResponse` | `ClientDetail` | Summary + `birthDate|null`, `addressLine|null`, `postalCode|null`, `notes|null`, `updatedAtUtc`, `archivedAtUtc|null` |
| `ClientPageResponse` | `ClientPage` | `items`, `page`, `pageSize`, `totalCount` |
| `ClientInputRequest` | `ClientInput` | `firstName`, `lastName`, `birthDate`, `phone`, `email`, `addressLine`, `city`, `postalCode`, `notes` (tous facultatifs dans le schéma ; prénom/nom contrôlés par le serveur → 422 `required`) |
| `ClientUpdateRequest` | `ClientInput & { version }` | idem + `version` (absente → 400 `version_required`) |
| `ClientVersionRequest` | `{ version }` | archivage / réactivation |

## Routes

| Méthode / route | Policy | Requête | Succès | Erreurs |
|---|---|---|---|---|
| `GET /api/clients` | `clients.read` | `q` (2–100 car. après rognage), `status=active|archived|all` (défaut `active`), `sort=name|createdAt|city` (défaut `name`), `dir=asc|desc`, `page` 1–100 000, `pageSize` 1–100 (défaut 20) | 200 `ClientPageResponse` | 400 `invalid_query` (valeur hors liste blanche, paramètre inconnu, répété ou de casse différente) |
| `GET /api/clients/{id}` | `clients.read` | — | 200 `ClientDetailResponse` | 404 `client_not_found` (id non UUID : 404 de routage) |
| `POST /api/clients` | `clients.write` | `ClientInputRequest` | **201** + `Location: /api/clients/{id}` | 400 `invalid_body` ; 415 ; 422 `validation_failed` |
| `PUT /api/clients/{id}` | `clients.write` | `ClientUpdateRequest` | 200 | 400 `invalid_body` / `version_required` ; 404 ; 409 `client_archived` (contrôlé avant la version) / `version_conflict` ; 422 |
| `POST /api/clients/{id}/archive` | `clients.archive` | `ClientVersionRequest` | 200 | 400 ; 404 ; 409 `version_conflict` / `already_archived` |
| `POST /api/clients/{id}/reactivate` | `clients.archive` | `ClientVersionRequest` | 200 | 400 ; 404 ; 409 `version_conflict` / `already_active` |

Pas de `DELETE` (405 sur `/api/clients/{id}`, D-03-05). Tous : 401 / 403.

## Recherche (D-03-04)

`search_text` = prénom, nom, email repliés (NFD sans marques diacritiques, minuscules invariantes, espaces réduits) + chiffres du téléphone, calculé par `MMV.Domain.Clients.ClientSearch` à chaque écriture. Requête repliée de la même manière : si elle ne contient que chiffres et séparateurs téléphoniques (`espace . - +`) et au moins 2 chiffres → `search_text LIKE '%<chiffres>%'` ; sinon **chaque mot** doit apparaître (`LIKE '%mot%'` combinés par ET), jokers `%`, `_`, `\` échappés (`ClientStore.SearchPatterns`). Aucune extension PostgreSQL ; balayage séquentiel (aucun index trigramme) — suffisant pour un magasin, à réévaluer au-delà de quelques dizaines de milliers de fiches (O-03-11).

Tri : `name` → `last_name, first_name, id` ; `createdAt` → `created_at_utc, id` ; `city` → `city, id` (NULL en dernier en ordre croissant, en premier en décroissant, règle PostgreSQL) ; ordre des chaînes selon la collation de la base.

## Côté client

| Élément | Chemin |
|---|---|
| Fonctions HTTP, parseurs stricts, listes blanches exportées (`CLIENT_SORT_FIELDS`, `CLIENT_STATUS_FILTERS`) | `apps/desktop/src/api/clients.ts` |
| Service activé (`CLIENTS_API_ENABLED = true`) ; état « non activé » conservé comme interrupteur de repli | `apps/desktop/src/clients/clientsService.ts` |
| Contrat vérifié (propriétés, nullabilité compilée, énumérations, bornes, statuts, parseurs) | `apps/desktop/src/api/clientsContract.test.ts` |
| Double de test aligné (ordre des contrôles, `invalid_body`) | `apps/desktop/src/test/fakeClientsApi.ts` |

Génération automatique de types (`openapi-typescript`) : **non installée** (aucune dépendance ajoutée) ; le contrôle croisé instantané ↔ types remplit le besoin de cohérence (O-03-01, réévaluable).
