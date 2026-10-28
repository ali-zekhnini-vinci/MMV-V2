# Phase 04 — derniers correctifs avant clôture (G04-FINAL)

**Date : 29/10/2026.** Mandat : [`prompts/MMV_V2_PHASE04_DERNIERS_CORRECTIFS_2026-10-29.md`](../../../prompts/MMV_V2_PHASE04_DERNIERS_CORRECTIFS_2026-10-29.md) — 04-C (`97bfd5e`) acceptée techniquement ; G04-FINAL ouvert pour trois correctifs : **O-04-53**, **O-04-54**, **O-04-52**.

**Statut : `PHASE04_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** Commit local sur `feat/phase-04-00-cadrage` ; aucun push, aucune PR, aucun merge. Phase 05 non commencée.

Le rapport d'audit cité par le mandat (`MMV_V2_AUDIT_FINAL_PHASE04_C_2026-10-28.md`) **n'est pas présent** dans l'espace de travail (dépôt, Téléchargements, Documents, Bureau) : les correctifs reposent sur le mandat et sur les constats O-04-52 à O-04-54 de [`LIVRAISON_04C.md`](LIVRAISON_04C.md) (O-04-56).

Aucune migration, aucune modification de schéma : le SQL `0006` approuvé et appliqué est **inchangé** (manifeste [`sql/0006_MANIFEST.sha256`](sql/0006_MANIFEST.sha256) : 11 / 11 OK, dont `OpticalModel.cs` et l'instantané EF Core). Les correctifs n'ajoutent que des verrous de ligne PostgreSQL dans les transactions existantes.

## 1. O-04-53 — archivage du client ‖ écriture optique

**Défaut (04-C)** : l'écriture optique lisait l'état du client sans verrou puis validait plus tard ; un archivage validé entre les deux n'était pas vu (9 révisions horodatées après `archived_at_utc` sur 10 paires HTTPS).

**Correctif** — contrôle qui fait foi DANS la transaction d'enregistrement, sous verrou :

| Côté | Verrou | Où |
|---|---|---|
| Écriture optique (création, modification de brouillon, confirmation, ouverture / abandon de rectification) | `SELECT (archived_at_utc IS NOT NULL) … FROM clients.clients WHERE id = … FOR SHARE`, **dans** la transaction d'enregistrement, avant l'en-tête ; archivé → rien n'est écrit, `409 client_archived` | `OpticalRecordStore.LockGuardAsync` ([`OpticalStore.cs`](../../../src/MMV.Infrastructure/Optical/OpticalStore.cs)), condition `OpticalCommitGuard` passée par `OpticalRecordService` ([`OpticalService.cs`](../../../src/MMV.Application/Optical/OpticalService.cs)) |
| Archivage / réactivation | transaction ouverte et `SELECT 1 … FOR NO KEY UPDATE` du client **avant** sa lecture, sa date et son enregistrement | `IClientStore.LockForLifecycleAsync` ([`ClientStore.cs`](../../../src/MMV.Infrastructure/Clients/ClientStore.cs), [`ClientService.cs`](../../../src/MMV.Application/Clients/ClientService.cs)) |
| Invalidation motivée | aucun contrôle ni verrou du client (permise pour un client archivé, D-04-07) | `RequireActiveClient: false` |

- **L'archivage gagne** (validé avant le verrou de l'écriture) : la ligne relue sous verrou (READ COMMITTED, version la plus récente après attente) est archivée → aucune ligne, version ni événement d'audit optique écrit.
- **L'écriture gagne** (verrou partagé pris d'abord) : l'archivage attend sa validation, puis aboutit ; sa date est prise **après** la validation optique (verrou avant lecture) — aucune inversion d'horodatage.
- La lecture simple préalable reste (réponse immédiate dans le cas courant) ; elle ne fait plus foi.
- Plusieurs écritures optiques sur un même client partagent le verrou `FOR SHARE` sans s'attendre.

## 2. O-04-54 — désactivation ou retrait d'habilitation ‖ confirmation de mesure

**Défaut (04-C)** : éligibilité du collaborateur lue sans verrou, avant l'enregistrement.

**Correctif** — quand un collaborateur est **nouvellement désigné** (provenance modifiée, règle inchangée), la transaction d'enregistrement verrouille son compte ET son droit :
`SELECT true FROM auth.users u JOIN auth.user_permissions p ON p.user_id = u.id WHERE u.id = … AND u.is_active AND p.permission = 'optical.measurements.write' FOR SHARE`. Aucune ligne → rien n'est écrit, `422 validation_failed` / `provenance.measuredByUserId = not_eligible`. Une désactivation (`UPDATE auth.users`) ou un retrait du droit (`DELETE auth.user_permissions`) concurrents attendent la fin de la confirmation.

Préservé (aucune règle rouverte) : nom du collaborateur historique (`citedUsers`), mesures confirmées intactes, rectification qui **conserve** le collaborateur historique admise sans contrôle ni verrou (désignation passée, D-04-03.b), refus de toute nouvelle désignation d'un collaborateur inéligible.

## 3. Ordre des verrous

Toute écriture optique : **client (`FOR SHARE`) → collaborateur désigné (compte puis droit, `FOR SHARE`) → en-tête(s) (`FOR UPDATE`, version `xmin`)**. L'archivage ne prend qu'un verrou (le client) ; la désactivation / le retrait de droit, un seul (la ligne visée). Aucun cycle possible entre ces opérations. Un interblocage avec une écriture SQL extérieure prenant ses verrous dans un autre ordre serait résolu par PostgreSQL (`40P01`) : transaction annulée, rien d'écrit, `409` — jamais une erreur 500 (chemin défensif, **NOT_TESTED**, O-04-58).

## 4. O-04-52 — libellé du formulaire

| Avant | Après |
|---|---|
| « Boutons ± : pas de démonstration 0,25 (paramétrable, D-04-06 ouverte) · ↑ / ↓ au clavier · saisie libre conservée telle quelle » + étiquette « Prototype · D-04-06 PROPOSED » | « Boutons ± : pas de 0,25 (Sphère, Cylindre, Addition) · ↑ / ↓ au clavier · saisie libre conservée telle quelle · Prisme en saisie libre » — sans étiquette |
| « Informations générales » + « Prototype · D-04-03.a / D-04-03.b PROPOSED » | sans étiquette (D-04-03.a / b APPROVED, mandat 27/10/2026) |

Design Lot 3 conservé (même composant, même place ; la règle CSS de l'étiquette devenue inutile est retirée). La liste de bases reste annoncée « Liste proposée (D-04-06, non approuvée) » : pas des prismes, bornes, conventions de base et composantes multiples **non arrêtés** (BL-04-03). Le Prisme reste sans ±. Le banc isolé garde ses mentions de démonstration (hors application réelle).

## 5. Concurrence — avant / après (PostgreSQL 18.6, 5433, `mmv_v2_test`)

Nouveaux tests [`OpticalLifecycleRacePostgreSqlTests`](../../../tests/MMV.PostgreSql.Tests/OpticalLifecycleRacePostgreSqlTests.cs) (`Stage=OpticalWrite`) : deux connexions, COMMIT réels, **barrières déterministes** sans délai — l'autre opération est validée entre le contrôle et l'enregistrement de A (store suspendu après la lecture réelle), ou A tient une transaction ouverte et l'attente de l'autre est **constatée** sur son processus serveur (`pg_blocking_pids`, `wait_event_type = Lock`). Données fictives validées puis purgées (résidu 0 à chaque test).

| Scénario (×) | Avant (code `97bfd5e`, tests seuls ajoutés) | Après |
|---|---|---|
| archivage validé après le contrôle → création, modification de brouillon, confirmation, ouverture / abandon de rectification, création de mesure (×6) | **FAIL** : écriture `Success` après l'archivage | **PASS** : `ClientArchived`, aucune ligne / version / événement, 0 écriture postérieure ; consultation et invalidation `Success` |
| écriture en cours → archivage (×6) | **FAIL** : archivage non bloqué (`blocking=[]`) | **PASS** : archivage bloqué puis `Success` ; date d'archivage > validation optique ; événements optiques complets ; nouvelle écriture → `ClientArchived` |
| désactivation / retrait du droit validé après le contrôle (×2) | **FAIL** : mesure `Success` | **PASS** : `not_eligible`, rien d'écrit |
| mesure en cours → désactivation / retrait du droit (×2) | **FAIL** : non bloqué | **PASS** : bloqué puis appliqué ; nouvelle mesure refusée ; nom servi ; rectification gardant le collaborateur historique admise (révisions 1 et 2 intactes) |

Preuve : [`evidence/P04F_race_postgresql_20261029.log`](evidence/P04F_race_postgresql_20261029.log). **Avant : 0 / 16 ; après : 16 / 16.**

HTTPS réel (outil 04-C inchangé, API de banc 7367 sur `mmv_v2_test`), sections 9–10 :

| Contrôle | 04-C (28/10) | 29/10 |
|---|---|---|
| 9.1 écriture ‖ archivage, 10 paires | 10 × 201 / 200 | 1 × 201, 9 × 409 `client_archived` ; jamais de 500 |
| 9.2 révisions confirmées horodatées après l'archivage | **9** | **0** |
| 10.1 confirmation ‖ désactivation, 10 paires décalées | 1 × 422, 9 × 201 | 1 × 422, 9 × 201 (le délai du banc laisse la confirmation finir avant la désactivation ; l'ordre est prouvé par les barrières PostgreSQL) ; jamais de 500 |

Preuve : [`evidence/P04F_http_matrix_20261029.log`](evidence/P04F_http_matrix_20261029.log).

## 6. Régression

| Contrôle | Résultat | Preuve |
|---|---|---|
| `dotnet build MMV.slnx -warnaserror` | **PASS** — 0 avertissement, 0 erreur | [`P04F_dotnet_20261029.log`](evidence/P04F_dotnet_20261029.log) |
| `MMV.Api.Tests` | **PASS 455 / 455** (+3 : condition transmise, refus sous verrou sans écriture ni audit, collaborateur historique non contrôlé) | idem |
| `MMV.Maintenance.Tests` | **PASS 50 / 50** | idem |
| `MMV.PostgreSql.Tests` (PostgreSQL 18.6) | **PASS 68 / 68** (52 + 16) | idem |
| Frontend : `npm run check`, `eslint .`, `vitest run` | **PASS** — 301 / 301 ; lint 0 erreur (1 avertissement préexistant) | [`P04F_frontend_20261029.log`](evidence/P04F_frontend_20261029.log) |
| Matrice HTTPS / HTTP en clair | **PASS 69 / 69** ; **6 / 6** refus `https_required` | [`P04F_http_matrix_20261029.log`](evidence/P04F_http_matrix_20261029.log) |
| Parcours Tauri natif `optique-04c` | **PASS 125 contrôles** (123 + 2 contrôles du libellé O-04-52), 0 échec | [`P04F_native_optique-04c_20261029.log`](evidence/P04F_native_optique-04c_20261029.log) |
| Captures natives (libellé corrigé) | formulaire vide 1920 × 1080 ; recadrage « Correction optique » | [`P04F_native_c04_02_ordonnance-vide_light_1920x1080.jpg`](evidence/P04F_native_c04_02_ordonnance-vide_light_1920x1080.jpg), [`P04F_native_O-04-52_correction-optique_crop.jpg`](evidence/P04F_native_O-04-52_correction-optique_crop.jpg) |
| Captures du banc 04-A (`plan.mjs`) | **NOT_RERUN** (libellé contrôlé par `vitest` et par le parcours natif) | — |

## 7. Données, bases et processus

- Écritures **uniquement** sur `mmv_v2_test` (cible relue côté serveur : base, rôle, port 5433, version 18.6) ; données et comptes fictifs ; purges contrôlées (déclencheurs `optical` / `audit` désactivés puis réactivés **dans** la transaction) ; **résidu final `mmv_v2_test` = 0**.
- `mmv_v2_dev` : lecture seule par les tests ; aucune écriture de l'agent. PostgreSQL 17 (5432) jamais contacté. Aucun compte réel modifié.
- Processus du propriétaire (`MMV.Api` 7267, Vite 1420) laissés en marche ; seuls les processus de banc de l'agent (API 7367 / 5367, exécutable `mmv-c04test`) ont été arrêtés, après vérification de leur ligne de commande.
- Module optique toujours inactif par défaut (`VITE_MMV_OPTICAL_MODULE` posé au seul build de banc) ; aucune donnée médicale réelle.

## 8. Réserves non bloquantes

| Réf. | Réserve |
|---|---|
| O-04-56 | rapport d'audit cité par le mandat absent de l'espace de travail (voir introduction) |
| O-04-57 | brouillon de mesure dont le collaborateur a été désigné quand il était éligible, puis confirmé après sa désactivation **sans** changer la provenance : accepté (règle « au moment où il est désigné », D-04-03.b, inchangée) — à confirmer par le propriétaire, non rouvert ici |
| O-04-58 | effets du verrou du client : une modification de fiche ou un archivage attend la fin d'une écriture optique en cours sur ce client (transaction courte), sans `lock_timeout` applicatif ; interblocage avec une écriture SQL extérieure → `409`, chemin **NOT_TESTED** |
| O-04-59 | désactivation et retrait de droits : par SQL seulement en Phase 04 (aucune API d'administration des comptes) ; couverts par le verrou (`UPDATE auth.users`, `DELETE auth.user_permissions`) |
| BL-04-07 | réduit à **O-04-40** (réémissions tardives) : O-04-53 et O-04-54 corrigés |
| BL-04-02 à BL-04-06, O-04-49, O-04-51 | inchangés (validation opticien, prismes, rôle SQL applicatif, restauration, G04-LEGAL ; journal 23505 ; délais `vitest` en parallèle) |

## 9. Livraison

- Commit local unique sur `feat/phase-04-00-cadrage` (message `fix(phase-04): …`) ; diff exact dans l'archive.
- Archive légère d'audit : `MMV-V2-phase04-final.zip` à la racine du dépôt (non suivie) — fichiers modifiés, patch du commit, preuves `P04F_*`.
- **STOP** : audit final du propriétaire (G04-FINAL). Ni Phase 05 ni autre module avant son GO.
