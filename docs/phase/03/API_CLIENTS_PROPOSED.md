# 03 — Contrat HTTP Clients (PROPOSED, 17/10/2026)

**Statut : `PROPOSED`** — non implémenté dans MMV.Api. Consommé par l'interface (`apps/desktop/src/api/clients.ts`) et par le double de test (`apps/desktop/src/test/fakeClientsApi.ts`), jamais par une route réelle tant que G03-METIER n'est pas ratifié (`CLIENTS_API_ENABLED = false`, `apps/desktop/src/clients/clientsService.ts`). Décisions associées : [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) D-03-01 à D-03-12.

## Règles communes

- HTTPS uniquement (filtre `/api` existant), `Authorization: Bearer <jeton opaque>` (variante de session Phase 02, ADR-007 `PROPOSED`), JSON, CORS `desktop-api` étendu à `PUT`.
- Autorisation **côté serveur** par policies : `clients.read`, `clients.write`, `clients.archive`. `system.admin` ne donne aucun de ces droits.
- Corps d'erreur : `{ "code": "<code_borné>" }`, plus `errors: { "<champ>": ["<code>"] }` pour 400/422. Aucun message libre, aucune trace, aucune donnée personnelle dans les erreurs ni les journaux.
- Dates : `…Utc` en ISO 8601 UTC ; `birthDate` en `AAAA-MM-JJ`.
- Concurrence : `version` (entier, `xmin`) lu puis renvoyé à chaque écriture ; différent de la version courante → **409** `version_conflict`.

## Ressources

`ClientSummary` (liste) : `id` (uuid), `reference`, `firstName`, `lastName`, `phone|null`, `email|null`, `city|null`, `status` (`active|archived`), `createdAtUtc`, `version`.

`ClientDetail` (fiche) : `ClientSummary` + `birthDate|null`, `addressLine|null`, `postalCode|null`, `notes|null`, `updatedAtUtc`, `archivedAtUtc|null`.

`ClientInput` (écriture) : `firstName`, `lastName`, `birthDate`, `phone`, `email`, `addressLine`, `city`, `postalCode`, `notes` (les facultatifs à `null` si vides).

## Routes

| Méthode / route | Droit | Requête | Succès | Erreurs |
|---|---|---|---|---|
| `GET /api/clients` | `clients.read` | `q` (2–100 car., facultatif), `status=active|archived|all` (défaut `active`), `sort=name|createdAt|city`, `dir=asc|desc`, `page` ≥ 1, `pageSize` 1–100 (défaut 20) | 200 `{ items: ClientSummary[], page, pageSize, totalCount }` | 400 `invalid_query` (toute valeur hors liste blanche) ; 401 ; 403 |
| `GET /api/clients/{id}` | `clients.read` | — | 200 `ClientDetail` | 401 ; 403 ; 404 `client_not_found` |
| `POST /api/clients` | `clients.write` | `ClientInput` | **201** `ClientDetail` (`Location: /api/clients/{id}`) | 400 corps illisible ; 422 `validation_failed` + `errors` ; 401 ; 403 |
| `PUT /api/clients/{id}` | `clients.write` | `ClientInput` + `version` | 200 `ClientDetail` | 400 `version_required` ; 404 ; 409 `version_conflict` / `client_archived` ; 422 ; 401 ; 403 |
| `POST /api/clients/{id}/archive` | `clients.archive` | `{ version }` | 200 `ClientDetail` | 404 ; 409 `version_conflict` / `already_archived` ; 401 ; 403 |
| `POST /api/clients/{id}/reactivate` | `clients.archive` | `{ version }` | 200 `ClientDetail` | 404 ; 409 `version_conflict` / `already_active` ; 401 ; 403 |

Pas de `DELETE` (D-03-05). Codes de champ proposés : `required`, `too_long`, `invalid_format`, `out_of_range` (l'interface affiche un message générique pour tout code inconnu).

Tri `name` : `last_name, first_name, id` ; `createdAt` : `created_at_utc, id` ; `city` : `city, id` (clé finale `id` pour une pagination stable). Recherche : `search_text` contient la valeur normalisée (minuscules, sans accents) ; les chiffres saisis (≥ 2) sont aussi cherchés dans le téléphone.

## Côté client (implémenté, 03-A)

| Élément | Chemin |
|---|---|
| Fonctions HTTP, parseurs stricts, liste blanche des paramètres | `apps/desktop/src/api/clients.ts` |
| Erreurs typées `not-found` / `conflict` / `validation` + `code` / `fieldErrors` bornés ; méthode `PUT` | `apps/desktop/src/api/client.ts` |
| TanStack Query : clés, annulation par `signal`, invalidation après écriture, rechargement sur 409/404 | `apps/desktop/src/clients/clientQueries.ts`, `apps/desktop/src/api/queryClient.ts` |

## OpenAPI et génération de types

Vérifié le 17/10/2026 (`docs/phase/03/evidence/P03_openapi_summary_20261017.txt`) : le document `/openapi/v1.json` (OpenAPI 3.1.1, développement uniquement) ne décrit que 4 routes d'authentification, **avec seulement des réponses 200 sans schéma** (les endpoints minimal API ne déclarent ni `Produces` ni `TypedResults`), et omet `/health/*`. Une génération `openapi-typescript` / `openapi-fetch` produirait des types incomplets et ne remplacerait pas les parseurs actuels : **non installée** (aucune dépendance arbitraire). Préalable proposé en 03-A2 : déclarer les réponses (`TypedResults`, `.Produces<T>()`, `.ProducesProblem()`), puis réévaluer la génération avec un test de contrat comparant le document aux parseurs.
