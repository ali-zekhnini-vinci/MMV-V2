# 04 — Contrat d'API proposé : dossier optique (`/api/optical`)

**Statut : `PROPOSED`** — aucune route créée. Conventions reprises des contrats implémentés de la Phase 03 ([`../03/API_CLIENTS.md`](../03/API_CLIENTS.md), [`../03/API_APPOINTMENTS.md`](../03/API_APPOINTMENTS.md)) : HTTPS obligatoire (`https_required` sinon), session par jeton opaque (ADR-007 `PROPOSED`, variante dev), erreurs `ErrorResponse { code }` sans trace technique, `version` = `xmin` exigée à chaque écriture, 409 `version_conflict` sans écriture, OpenAPI généré par le serveur et extrait versionné comparé aux types TypeScript. Permissions : [`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md). Champs exacts des corps : selon D-04-02 / D-04-03 / D-04-06 ([`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md)).

## 1. Règles transverses proposées

- **Deux familles de routes** (D-04-01) : `/prescriptions` et `/measurements`. Aucune route de conversion, aucun corps commun où le type serait un simple champ.
- Toute route exige `clients.read` + `optical.read` en plus du droit d'opération.
- **Réponses de refus indiscernables** : sans `optical.read`, un identifiant existant et un identifiant inconnu donnent la même réponse (403 avant toute lecture) ; avec `optical.read`, 404 `not_found`.
- **Erreurs de validation** (422) : `{ code: "validation_failed", errors: [{ field, code }] }` — codes d'erreur seulement, **jamais la valeur saisie** renvoyée ni journalisée.
- **En-têtes** : `Cache-Control: no-store` sur toutes les réponses optiques ; pièces servies avec `Content-Disposition: inline` / `attachment` explicite et `X-Content-Type-Options: nosniff`.
- **Journaux techniques** : aucune journalisation de corps de requête ni de réponse (constat 19/10/2026 : pas de `UseHttpLogging`, pas d'`EnableSensitiveDataLogging`, EF Core au niveau `Warning`) ; à conserver et tester (Q-04-140).
- **Idempotence** : les créations acceptent un en-tête `Idempotency-Key` facultatif (double clic, reprise réseau) — à confirmer en 04-B (ADR-011).

## 2. Lecture

| Route | Droits | Réponse |
|---|---|---|
| `GET /api/optical/clients/{clientId}/records?type=&status=&page=&pageSize=` | `clients.read`, `optical.read` | dossier du client : éléments `{ type: "prescription"\|"measurement", id, status, currentRevisionNo, date, author/prescriber, summary }` ; invalidés exclus sauf `status=all` |
| `GET /api/optical/prescriptions?search=&status=&from=&to=&sort=&page=&pageSize=` | idem | liste globale (D-04-08.b) |
| `GET /api/optical/measurements?…` | idem | liste globale des mesures |
| `GET /api/optical/prescriptions/{id}` | idem | en-tête + révision courante + métadonnées des pièces (sans nom de fichier sans `optical.documents.read`) + `version` |
| `GET /api/optical/prescriptions/{id}/revisions` | idem | liste des révisions (numéro, motif, auteur, dates) |
| `GET /api/optical/prescriptions/{id}/revisions/{revisionNo}` | idem | révision figée (comparaison côté interface) |
| `GET /api/optical/measurements/{id}` (+ `/revisions`, `/revisions/{n}`) | idem | idem pour les mesures |

## 3. Écriture — ordonnances médicales

| Route | Droits | Corps | Succès | Refus |
|---|---|---|---|---|
| `POST /api/optical/prescriptions` | `optical.prescriptions.write` | `{ clientId, provenance…, od…, og…, notes }` | 201 brouillon, révision 1 | 404 client ; 409 `client_archived` ; 422 |
| `PUT /api/optical/prescriptions/{id}/draft` | idem | champs + `version` | 200 (corps identique → 200 sans écriture, comme O-03-44) | 409 `not_draft` / `version_conflict` ; 422 |
| `POST /api/optical/prescriptions/{id}/confirm` | idem | `{ version }` | 200 `confirmed` | 422 `incomplete` (critère D-04-02.b) ; 409 |
| `POST /api/optical/prescriptions/{id}/revisions` | idem | champs + `{ expectedRevisionNo, reasonCode, comment?, version }` | 201 nouvelle révision (brouillon de révision ou confirmée selon D-04-04.b) | 409 `revision_conflict` / `client_archived` (D-04-07) / `invalidated` ; 422 |
| `POST /api/optical/prescriptions/{id}/invalidate` | `optical.invalidate` | `{ reasonCode, comment?, version }` | 200 `invalidated` | 409 `already_invalidated` / `version_conflict` |

Mêmes routes sous `/api/optical/measurements` avec `optical.measurements.write` et les champs de provenance d'une mesure (D-04-03.b). **Aucune** route `DELETE`.

## 4. Pièces source (ordonnances seulement en 04)

| Route | Droits | Détail |
|---|---|---|
| `POST /api/optical/prescriptions/{id}/documents` | `optical.documents.write` | `multipart/form-data`, une pièce ; type par signature, taille ≤ limite (D-04-03.c) ; 201 `{ id, mediaType, sizeBytes, uploadedAtUtc }` ; 413 `too_large`, 415 `unsupported_media_type` |
| `GET /api/optical/documents/{documentId}/content` | `optical.documents.read` | flux binaire authentifié (jeton de session, jamais dans l'URL) ; événement `document.viewed` écrit **avant** l'envoi ; échec d'audit ⇒ pièce non servie |
| `POST /api/optical/documents/{documentId}/invalidate` | `optical.documents.write` | `{ reasonCode }` ; pièce conservée, masquée de l'aperçu par défaut |

Aucun lien public, aucune URL signée réutilisable, aucun chemin de fichier exposé. Le poste reçoit le contenu en mémoire et l'affiche localement (O-04-01 : CSP `blob:` à décider).

## 5. Historique (existant, modifié)

`GET /api/audit/clients/{clientId}` (policy `AuditRead`) : les événements du module `optical` ne sont servis que si la session détient aussi `optical.read` (D-04-05.c) ; le filtre `module` accepte `optical` dans ce seul cas. Aucune valeur optique n'existe dans le journal à servir.

## 6. Préparation de la vente (inerte en 04)

Aucune route. Le futur module Ventes consommera `GET …/{id}/revisions/{n}` et enregistrera `(type, id, revisionNo)` ; une mesure magasin ne sera jamais acceptée là où une ordonnance médicale est exigée (D-04-01). Contrat détaillé avec la phase Ventes.

## 7. Tests de contrat prévus

Extrait `apps/desktop/src/api/contract/optical.openapi.json`, comparaison au document réel (`OpticalOpenApiContractTests`), types TypeScript (`opticalContract.test.ts`) ; matrice HTTP 401 / 403 / 404 / 409 / 422 par route ([`QA_MATRIX.md`](QA_MATRIX.md)).

## Amendement daté — 19/10/2026 (études préalables)

Les §§ 1 à 7 sont conservés tels qu'écrits en 04-00. Si D-04-10 à D-04-12 sont ratifiées ([`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md)) :

| Route / réponse | Changement proposé |
|---|---|
| `GET …/prescriptions/{id}` | renvoie `lifecycle`, `lastConfirmedRevision` (révision complète, ou vide) et `openDraft` (résumé : auteur, début, motif, ou vide) ; le statut affiché est dérivé |
| `POST …/prescriptions/{id}/revisions` (04-00) | remplacé par `POST …/prescriptions/{id}/rectifications` `{ expectedRevisionNo, reasonCode, comment?, version }` → 201 brouillon de rectification ; `409 draft_exists` / `revision_conflict` / `client_archived` / `invalidated` |
| `PUT …/prescriptions/{id}/draft` | s'applique au brouillon ouvert (première saisie **ou** rectification) ; corps complété par `linkedDocumentIds` (pièces du même enregistrement, non invalidées) ; `409 not_draft` / `version_conflict` ; `409 client_archived` si le client a été archivé (brouillon gelé) |
| `POST …/prescriptions/{id}/confirm` | confirme le brouillon ouvert ; attribue le numéro de révision ; `409 revision_conflict` si la révision de base n'est plus la dernière confirmée ; `422 incomplete` |
| `POST …/prescriptions/{id}/draft/discard` (nouvelle) | abandonne une **rectification** (`discarded`, conservée) ; une première saisie s'abandonne par invalidation (D-04-04.d) |
| `POST …/prescriptions/{id}/invalidate` | abandonne le brouillon ouvert dans la même transaction (D-04-11.e) |
| `GET …/revisions/{revisionNo}` | inclut les pièces liées à la révision (métadonnées ; nom d'origine seulement avec `optical.documents.read`) et leur éventuelle invalidation |
| `POST …/prescriptions/{id}/documents` | `413 too_large`, `415 unsupported_media_type`, `409 duplicate_document`, `409 too_many_documents` ; type détecté par signature (D-04-14) |
| `GET /api/optical/documents/{documentId}/content` | événement **`document.content_served`** (au lieu de `document.viewed`) validé avant le premier octet ; `Content-Type` = type détecté ; avertissement d'invalidation porté par les métadonnées, pas par le contenu |

Mêmes changements pour `/api/optical/measurements` (sans pièces en 04). Toujours **aucune** route `DELETE`.

## Note datée — 19/10/2026 (04-A)

Toujours **aucune route créée**. Le prototype 04-A décrit ces échanges par des types TypeScript **provisoires** (`apps/desktop/src/optical/opticalContract.ts`, interface `OpticalGateway`) servis par un double **en mémoire** du banc isolé ; ces types ne sont pas un contrat et seront remplacés par l'extrait OpenAPI généré en 04-B (§ 7). Les codes de refus illustrés par le double (`version_conflict`, `draft_exists`, `revision_conflict`, `client_archived`, `invalidated`, `already_invalidated`, `not_draft`, `incomplete`, `too_large`, `unsupported_media_type`, `duplicate_document`, `too_many_documents`) sont ceux de ce document et de son amendement, sans nouveauté.

## Amendement daté — 20/10/2026 (D-04-16 APPROVED : simplification)

Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). Toujours **aucune route créée** ni aucun backend optique. Effets sur le contrat proposé (sections précédentes conservées comme historique) :

| Élément | Changement |
|---|---|
| § 4 « Pièces source » (`POST …/prescriptions/{id}/documents`, `GET /api/optical/documents/{documentId}/content`, `POST …/documents/{documentId}/invalidate`) et lignes « pièces » de l'amendement du 19/10/2026 | **retirés du périmètre de la Phase 04** — reportés au backlog permanent BL-04-01 ; aucune route, aucun multipart, aucun flux binaire |
| corps des brouillons (`POST …/prescriptions`, `PUT …/draft`, rectification) | aucun état par œil (`od.state` / `og.state` supprimés) ; aucun `linkedDocumentIds` ; valeurs par œil facultatives une à une (`""` ou absent = non renseigné, jamais `0`) |
| `GET …/prescriptions/{id}`, `GET …/revisions/{revisionNo}` | sans métadonnées de pièces ni nombre de pièces ; révisions, motif, auteurs (`startedBy`, `confirmedBy`, `discardedBy`) et dates **conservés** |
| `POST …/confirm` → `422 incomplete` | critère décidé par le serveur (D-04-02.b **ouverte**, O-04-30) ; plus aucun code `not_transcribed` ; le double du banc ne contrôle que la provenance (démonstration) |
| codes de refus | retirés : `too_large`, `unsupported_media_type`, `duplicate_document`, `too_many_documents`, `invalid_document` ; conservés : `version_conflict`, `draft_exists`, `revision_conflict`, `client_archived`, `invalidated`, `already_invalidated`, `not_draft`, `not_rectification`, `incomplete`, `validation_failed`, `kind_mismatch` |
| § 1 en-têtes | `Cache-Control: no-store` conservé ; `Content-Disposition` / `nosniff` des pièces sans objet en 04 |
| permissions exigées | 4 droits optiques ([`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md), amendement du 20/10/2026) |

Le prototype reflète ce contrat réduit : `apps/desktop/src/optical/opticalContract.ts` (`EyeValues` sans état, `OpticalGateway` sans opération documentaire) et le double `apps/desktop/src/bench/inMemoryOpticalGateway.ts`.

## Amendement daté — 27/10/2026 (04-B : contrat implémenté)

Contrat **implémenté** : [`API_OPTICAL.md`](API_OPTICAL.md) (ce document reste l'historique de la proposition). Écarts principaux : liste globale croisée `GET /api/optical/records` (au lieu d'une liste par type) ; `requestId` obligatoire sur toute écriture (idempotence, ADR / O-04-40) ; « Enregistrer » atomique — `POST /api/optical/{type}` avec `confirm: true` et `POST …/{id}/draft/confirm` avec contenu (O-04-34 résolu) ; abandon `POST …/{id}/draft/discard` ; `historyAvailable` et historique restreint sans `audit.read` ; codes ajoutés `request_id_reused`, `client_not_found`, `request_id_required` ; codes de complétude `optical_values_missing`, `sphere_zero_only`, `acuity_only`, `axis_required`, `base_required`, `required`, `not_eligible`. Toujours **aucune** route `DELETE`, de conversion ni documentaire.
