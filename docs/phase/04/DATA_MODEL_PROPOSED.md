# 04 — Modèle de données proposé (conceptuel, sans SQL)

**Statut : `PROPOSED` — aucun SQL écrit ni exécuté, aucune migration générée, aucune table créée.** Ce document décrit des **agrégats et attributs candidats** ; leur liste exacte dépend de D-04-02, D-04-03, D-04-04 et D-04-06 ([`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md)). Seule D-04-01 (deux types distincts) est appliquée comme contrainte de conception.

Base réelle vérifiée le 19/10/2026 : migrations `0001` à `0005` (`src/MMV.Infrastructure/Persistence/Migrations/`, dernière `20261018163108_AuditJournal`) ; schémas `auth`, `clients`, `appointments`, `audit` ; PostgreSQL 18 seul (ADR-003), EF Core 10 / Npgsql dans MMV.Api uniquement.

## 1. Principes

1. **Deux agrégats distincts** (D-04-01) : `PrescriptionRecord` (ordonnance médicale reçue) et `StoreMeasurement` (mesure magasin). Pas de table commune avec un discriminant modifiable, pas d'opération de conversion. Le « dossier optique » d'un client est une **vue de lecture** (requête sur les deux agrégats), pas une entité.
2. **En-tête + révisions immuables** : l'en-tête porte l'identité, le client, le statut de cycle de vie et le numéro de la révision courante ; chaque révision confirmée est **figée** (aucun `UPDATE` applicatif après confirmation). Une vente future référencera `(type, id, numéro de révision)`.
3. **Valeurs exactes** : décimaux PostgreSQL `numeric` (jamais de flottant) ; signe conservé ; aucune normalisation à l'écriture (D-04-06.b).
4. **Absence ≠ zéro** : état explicite par œil (D-04-02.c) ; colonnes de valeurs nullables.
5. **Aucune donnée de pays en dur** : pas de code organisme, d'annexe INAMI, de présomption AMO / CNSS / CNOPS ; la prise en charge sera un domaine distinct (D-04-09.e).
6. **Concurrence** : `xmin` sur l'en-tête (comme Clients / Rendez-vous) **et** numéro de révision attendu pour rectifier ; 409 sans aucune écriture.
7. **Audit** : événement écrit dans la **même transaction** par le service métier (D-03-19 retenue) ; aucune valeur optique dans `changes`.

## 2. Schéma proposé : `optical`

### 2.1 `PrescriptionRecord` (en-tête)

| Attribut | Type candidat | Règle proposée | Décision |
|---|---|---|---|
| `id` | uuid | identifiant | — |
| `client_id` | uuid → `clients.clients` (`RESTRICT`) | client actif à la création | D-04-07 |
| `status` | code : `draft`, `confirmed`, `invalidated` | cycle de vie | D-04-02.a |
| `current_revision_no` | entier ≥ 1 | révision affichée par défaut | D-04-04 |
| `created_at_utc`, `created_by_user_id` | horodatage, uuid → `auth.users` | automatiques | — |
| `invalidated_at_utc`, `invalidated_by_user_id`, `invalidation_reason_code`, `invalidation_comment` | nullable | tous renseignés ensemble ; commentaire masqué dans l'audit | D-04-04.c |
| `xmin` | jeton de version | 409 `version_conflict` | — |

### 2.2 `PrescriptionRevision` (immuable après confirmation)

| Attribut | Type candidat | Règle proposée | Décision |
|---|---|---|---|
| `prescription_id`, `revision_no` | clé composée | séquentiel par ordonnance | D-04-04 |
| `prescriber_name` | texte borné | facultatif au brouillon, exigé à la confirmation (option A) | D-04-03.a |
| `prescriber_qualification_code` | code d'une liste fermée | liste à fournir | D-04-03.a [AVIS OPTICIEN] |
| `prescriber_identifier` | texte libre borné | aucun contrôle de format | D-04-03.a |
| `prescribed_on` | date civile | exigée à la confirmation ; aucune validité calculée | D-04-03.a, D-04-09.f |
| `received_on` | date civile, nullable | distincte de `prescribed_on` | D-04-03.a |
| `od_state`, `og_state` | code : `filled`, `not_prescribed`, `not_transcribed` | `not_transcribed` interdit à la confirmation | D-04-02.c |
| valeurs par œil (`od_*`, `og_*`) : sphère, cylindre, axe, addition, prisme (1 ou 2 composantes), base, acuité | `numeric(…, 2)` / entier / code / texte court | **bornes et dépendances non fixées** | D-04-06 [AVIS OPTICIEN] |
| `notes` | texte borné | masqué dans l'audit | — |
| `revision_reason_code`, `revision_comment` | nullable pour la révision 1 | obligatoires à partir de la révision 2 | D-04-04.b |
| `recorded_at_utc`, `recorded_by_user_id` | automatiques | auteur de la saisie ≠ prescripteur | cadrage § 6.6 |
| `confirmed_at_utc`, `confirmed_by_user_id` | nullable | renseignés à la confirmation | D-04-02.d |

Option de stockage des valeurs : colonnes typées (recommandé — contraintes `CHECK` possibles une fois D-04-06 ratifiée, requêtes simples) plutôt que `jsonb` (structure souple, contrôles applicatifs seulement). À trancher en 04-B.

### 2.3 `StoreMeasurement` (en-tête) et `StoreMeasurementRevision`

Même structure d'en-tête et de révision que l'ordonnance, **sans** les attributs de prescripteur et de dates de prescription / réception, et avec :

| Attribut | Type candidat | Règle proposée | Décision |
|---|---|---|---|
| `measured_on` | date civile | obligatoire | D-04-03.b |
| `measured_by_user_id` | uuid → `auth.users` | compte détenant `optical.measurements.write` (option A) | D-04-03.b |
| `measurement_category_code` | code d'une liste fermée | liste à fournir | D-04-03.b [AVIS OPTICIEN] |
| `method_note` | absent en 04 (option B) | — | D-04-03.b |

Aucune clé vers `PrescriptionRecord` en 04 (D-04-03.d option A).

### 2.4 `SourceDocument` (pièce source d'une ordonnance)

| Attribut | Type candidat | Règle proposée | Décision |
|---|---|---|---|
| `id` | uuid | identifiant **non devinable** ; jamais d'URL publique | — |
| `prescription_id` | uuid → en-tête (`RESTRICT`) | rattachement à l'ordonnance, pas à une révision (conservé quand une révision est créée) | J-04-06 |
| `media_type` | `application/pdf`, `image/jpeg`, `image/png` | contrôlé par signature du contenu | D-04-03.c |
| `size_bytes`, `sha256` | entier, empreinte | taille ≤ limite ratifiée ; empreinte pour l'intégrité | D-04-03.c |
| `content` | `bytea` dans une table séparée (option 1) | jamais renvoyé par les listes ; lu en flux par une route dédiée | D-04-03.c, O-04-02 |
| `original_file_name` | texte borné, nullable | **masqué** dans l'audit, non servi sans `optical.documents.read` | D-04-05 |
| `uploaded_at_utc`, `uploaded_by_user_id` | automatiques | — | — |
| `invalidated_*` | nullable | pièce jointe par erreur : invalidée, conservée | D-04-04.c |

## 3. Index et requêtes prévues (indicatif)

- Dossier d'un client : `(client_id, created_at_utc DESC, id DESC)` sur chaque en-tête.
- Liste globale : tri par date de prescription / mesure, filtre de statut, recherche par nom de prescripteur (`LIKE` sans extension, comme O-03-11 ; index à mesurer).

## 4. Audit (`audit.events`, sans migration)

Le journal 03-D accepte de nouveaux codes **sans migration** ([`../03/AUDIT_JOURNAL_03D.md`](../03/AUDIT_JOURNAL_03D.md) § 1). Codes proposés, module `optical` :

| Action | Entité | `changes` |
|---|---|---|
| `prescription.created`, `measurement.created` | `prescription` / `measurement` | **aucun nom de champ optique** ; au plus des groupes masqués (`provenance`, `values`, `notes`) |
| `prescription.draft_updated`, `measurement.draft_updated` | idem | groupes masqués |
| `prescription.confirmed`, `measurement.confirmed` | idem | `revisionNo` (valeur conservée : donnée de gestion, non clinique) |
| `prescription.revised`, `measurement.revised` | idem | `revisionNo`, `revisionReasonCode` (conservés) ; commentaire masqué |
| `prescription.invalidated`, `measurement.invalidated` | idem | `invalidationReasonCode` (conservé) ; commentaire masqué |
| `document.attached`, `document.invalidated`, `document.viewed` | `source_document` | `mediaType`, `sizeBytes` (conservés) ; nom de fichier masqué |

`AuditFieldPolicy` : la liste blanche des valeurs conservées n'est étendue qu'aux codes ci-dessus marqués « conservés » ; toute valeur optique, tout nom de prescripteur, toute note reste exclue. `client_id` renseigné pour chaque événement (historique de la fiche), filtré à la lecture selon D-04-05.c.

## 5. Ce qui n'est pas modélisé en 04

Écart pupillaire, hauteurs, centrage (sous-domaine équipement) ; configuration de verres ; lentilles de contact ; équipements existants ; prise en charge / tiers payant ; transposition ; validité ou renouvellement ; numérisation / OCR ; liens d'appareils de mesure.

## 6. Migration future (04-B, après GO SQL)

- Nom proposé : `0006_OpticalRecords` (la base réelle compte 5 migrations, dernière `0005_AuditJournal` ; à revérifier au moment de 04-B).
- Contenu attendu : `CREATE SCHEMA optical`, tables ci-dessus, clés étrangères `RESTRICT`, contraintes de format, remplacement de `ck_user_permissions_permission` (nouvelles permissions), aucune donnée lue ni reprise.
- Procédure inchangée : SQL exact + script idempotent + retour arrière revus par le propriétaire, empreinte imposée, `scripts/Invoke-MmvSqlScript.ps1`, jamais au démarrage, `mmv_v2_dev` puis `mmv_v2_test`.
- Déclencheurs empêchant la modification d'une révision confirmée : à discuter (même limite que l'audit : inefficaces contre le rôle propriétaire tant que O-B04 n'est pas traité).

## Amendement daté — 19/10/2026 (études préalables, modèle non figé)

Les §§ 1 à 6 ci-dessus sont conservés tels qu'écrits en 04-00. Les études [`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md) proposent, **sous réserve de D-04-10 à D-04-14** (`PROPOSED`), les changements suivants. Toujours aucun SQL.

| Élément 04-00 | Changement proposé | Étude |
|---|---|---|
| § 2.1 `status` (`draft` / `confirmed` / `invalidated`) et `current_revision_no` sur l'en-tête | en-tête : `lifecycle` `active` / `invalidated` ; pointeurs `last_confirmed_revision` (vide avant toute confirmation) et `open_draft` (au plus un) ; statut affiché **dérivé** | E-02 |
| § 2.2 révision « immuable après confirmation », clé `(prescription_id, revision_no)` | révision identifiée par un uuid ; `status` `draft` / `confirmed` / `discarded` ; `base_revision_no` (vide pour la première saisie) ; `revision_no` attribué **à la confirmation** (séquence sans trou) ; unicité « un seul `draft` par enregistrement » | E-02 |
| § 2.4 pièce rattachée à l'en-tête seulement | pièce toujours **possédée par l'en-tête** (contenu stocké une fois, immuable, SHA-256) **+** table de liens `revision ↔ pièce` (révision, pièce, auteur et date du lien), modifiable sur un brouillon, figée à la confirmation ; doublon d'empreinte refusé sur un même enregistrement ; plafond de pièces actives | E-01, E-05 |
| § 2.4 `content` en `bytea` séparé | inchangé ; précisé : contenu déjà compressé (stockage sans recompression à spécifier dans le SQL de 04-B), lecture en flux, aucun fichier temporaire serveur, seuil d'alerte de volume | E-05 |
| § 4 `document.viewed` | remplacé par `document.content_served` (« contenu transmis », jamais « vu ») ; nouveaux codes `prescription.rectification_started`, `prescription.rectification_discarded` (et `measurement.*`) ; aucune valeur optique | E-02, E-03 |
| § 2.3 mesures magasin | mêmes règles de cycle et de révision que l'ordonnance ; aucune pièce en 04 | E-02 |

## Amendement daté — 20/10/2026 (D-04-16 APPROVED : simplification)

Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). Les sections précédentes sont conservées comme historique. Le modèle proposé pour la migration future `0006` (toujours **non écrite**, aucun SQL) devient :

| Élément | Changement | Décision |
|---|---|---|
| § 1 principe 4 « état explicite par œil » | **retiré** ; reste : colonnes de valeurs **nullables une à une** — une valeur absente est `NULL`, jamais `0` ; aucune colonne d'état d'œil | D-04-16 (retire l'état de D-04-02.c) |
| § 2.2 `od_state`, `og_state` | **supprimés** du modèle proposé | D-04-16 |
| § 2.2 révision (provenance, valeurs, notes, motif, auteurs, dates de saisie et de confirmation) | **inchangée** : `prescriber_name`, `prescriber_qualification_code`, `prescriber_identifier`, `prescribed_on`, `received_on`, valeurs par œil, `notes`, motif (`revision_reason_code`, commentaire), `recorded_*` / `confirmed_*` ; cycle E-02 (`draft` / `confirmed` / `discarded`, `base_revision_no`, numéro à la confirmation) | D-04-04, D-04-11 (PROPOSED) |
| § 2.3 mesures magasin | inchangé (aucune pièce, aucun état d'œil) | D-04-01, D-04-03.b |
| § 2.4 `SourceDocument` et table de liens révision ↔ pièce (amendement du 19/10/2026) | **retirés du périmètre de `0006`** : aucune table de pièces, aucun `bytea`, aucun lien — reportés au backlog permanent BL-04-01 | D-04-16 (reporte D-04-03.c, D-04-10, D-04-14) |
| § 4 audit : `document.attached`, `document.invalidated`, `document.viewed` / `document.content_served` | **retirés** ; codes conservés : `prescription.*` / `measurement.*` (`created`, `draft_updated`, `confirmed`, `rectification_started`, `rectification_discarded`, `invalidated`) ; aucune valeur optique | D-04-16 |
| § 5 non modélisé en 04 | **ajouté** : pièces sources et gestion documentaire (BL-04-01) ; état par œil | D-04-16 |
| § 6 migration `0006` | contenu attendu réduit : schéma `optical`, en-têtes et révisions des deux types, contrainte de permissions (4 droits, [`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md)) ; **aucune** table de pièces. Toujours soumise à G04-METIER puis G04-SQL ; **aucune migration écrite ni appliquée** le 20/10/2026 | — |
