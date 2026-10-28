# 04 — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 uniquement)

| Source | Usage |
|---|---|
| `prompts/PHASE04_00_GO_CLAUDE_CODE.md` | mandat 04-00 (copie versionnée du `PROMPT_CLAUDE_CODE_PHASE04_00.md` du pack, identique octet pour octet) |
| `CLAUDE.md`, `README.md`, `docs/ARCHITECTURE_ET_STACK.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md` | gouvernance, DOCUMENTATION GATE, état Phase 03, backlog |
| `docs/phase/03.md`, `docs/phase/03/AUDIT_JOURNAL_03D.md`, `API_CLIENTS.md`, `API_APPOINTMENTS.md`, `STATE_MAP_47.md`, `RESERVES_PHASES_FUTURES.md`, `GATES_AND_DECISIONS.md` | conventions d'API, journal `audit.events`, réserves héritées, états Lot 2 liés aux ordonnances |
| `docs/phase/03/sql/0005_AuditJournal.sql` | formats des codes d'audit, contrainte de permissions |
| `src/MMV.Domain/Identity/Permissions.cs`, `src/MMV.Api/Auth/AuthSetup.cs` | permissions et policies existantes |
| `src/MMV.Domain/Audit/AuditEvent.cs`, `src/MMV.Api/Audit/AuditEndpoints.cs` | catalogue d'actions, politique de champs, lecture de l'historique |
| `src/MMV.Api/Clients/ClientsEndpoints.cs`, `src/MMV.Api/Appointments/AppointmentsEndpoints.cs` | routes et policies |
| `src/MMV.Api/appsettings.json`, recherche dans `src/` | niveaux de journalisation, absence de journalisation des corps |
| `apps/desktop/src/clients/ClientDetailView.tsx`, `shell/navigation.ts`, `shell/Shell.tsx`, `components/ui/*`, `appointments/ClientPicker.tsx`, `package.json`, `src-tauri/tauri.conf.json` | composants réutilisables, onglet reporté, menu, cache, CSP |

Aucune lecture de l'ancien dépôt, de son code, de ses bases ou de PostgreSQL 17. Aucune connexion à `mmv_v2_dev` ni `mmv_v2_test`.

## Pack de cadrage du propriétaire (non suivi, non modifié)

| Fichier | SHA-256 | Remarque |
|---|---|---|
| `docs/phase/04/CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md` | `d0b25124fec3c09692dfa6df89416c7ee3379a23c572a4ca95757e195e8b2703` | v0.2 ; **non suivi**, laissé en place |
| `docs/phase/04/PROMPT_CLAUDE_CODE_PHASE04_00.md` | `3c286f74340f27ea9e37bfdb257b4e6a45d2b7058fa4e743ee5683bde099ba97` | non suivi, laissé en place |
| `docs/phase/04/README_PACK.md` | `c2e39e00755a7d21b59b28b83a920b0d31ce03d6a29c1122a2820f2d5145541a` | non suivi, laissé en place |

Installation recommandée par `README_PACK.md`, appliquée par **copie** (originaux intacts) : `references/phase04/CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md` (même empreinte, non versionné, comme `references/phase03/`) et `prompts/PHASE04_00_GO_CLAUDE_CODE.md` (versionné). Versionner ou retirer le pack de `docs/phase/04/` : décision du propriétaire (O-04-14).

## Références visuelles et catalogue (`references/phase04/`, copies de travail non versionnées, 19/10/2026)

| Fichier | Provenance | SHA-256 | Statut / usage |
|---|---|---|---|
| `lot3/MMV-Lot3-Ordonnances.dc.html` | `C:\Users\…\Downloads\MMV-Lot3-Ordonnances.dc.html` ; identique à `MMV initialisation projet V1/` et à `sources/claude-design/MMV-Lot3-Ordonnances.dc.html` de `MMV_V2_Dossier_Maitre_PostgreSQL_Only.zip` (archive `df5b1439…a0d8bda7c0`) | `c103df9e434245cdcba5b6246c5ec3032301e323c6ec1797826deaf8fd06c213` | **DRAFT**, empreinte attendue **vérifiée** ; = `registres/DESIGN_SOURCES_SHA256.csv` lot 3 (93 625 octets, 17 groupes, 36 états) ; composition, libellés et états lus ; **aucun code repris** ; dépendances de rendu (`support.js`, `_ds/`) non copiées, planche non rendue |
| `catalogue/MMV_V2_Registre_324_Etats.csv` | `MMV_V2_Catalogue_Design_Complet_Zero.zip` (archive `81ddc10a…25e531f4`) | `ee25db39fddaccbe6b50ab0c46af5575568f8a71766104db4b3368b36e8ae920` | 36 lignes `MMV-L03-*` → [`STATE_MAP_36.md`](STATE_MAP_36.md) |
| `catalogue/MMV_V2_Registre_134_Groupes.csv` | idem | `5fe6d2e8a0d8b7451e32096710893a614cbfd6378390439e754bf6330e9577dc` | groupes `B-033` à `B-049` ; colonnes « dépendances / phase historiques » **non normatives**, ignorées |

Lus dans le dossier maître (extraction temporaire hors dépôt, non copiés) : `docs/design-reference/lot-3/LOT3_SOURCE_MAP.md` (`b22dae32…dbada059`), `LOT3_GATES_AND_DECISIONS.md` (`bdfeeae6…46653ed`) — pré-handoff « DESIGN SUPPLIED / APPROVAL UNKNOWN » ; leurs conditions de démarrage qui supposent un audit de l'ancien backend sont **caduques** (D-000).

## Contenu de la maquette écarté

Les notes de conception (`data-mount="notes"`) et l'en-tête de la planche citent un ancien dépôt, une entité, un validateur, des plages numériques, une normalisation d'axe, une suppression physique sans refus métier et des rôles visuels. **Rien de cela n'est repris** comme règle (D-000, `NEW_RULES_ONLY`) : les plages numériques ne sont même pas recopiées dans les propositions ; normalisation et suppression sont traitées comme conflits (`REPLACE`, [`STATE_MAP_36.md`](STATE_MAP_36.md)).

## Dépendances (ADR-014)

Aucune dépendance ajoutée ni proposée en 04-00. 04-A est prévu sans nouvelle bibliothèque ([`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 3).

## Pistes réglementaires (cadrage § 11, non consultées, non avis juridique)

INAMI article 30 et formulaires opticiens (Belgique) ; CNDP et loi 09-08 (Maroc) — URL dans le cadrage. À instruire par le conseil du propriétaire (D-04-09, G04-LEGAL).

## Complément daté — 19/10/2026 (suite de la revue, préparation 04-A)

| Source | Usage |
|---|---|
| `prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md` | mandat de suite (message de session du propriétaire, reproduit à l'identique) |
| documents 04-00 de ce dossier | base des études E-01 à E-05 et des amendements datés |
| `docs/INSTALLATION_RESEAU_SECURITE.md` § 5, `docs/ADR_ET_DECISIONS.md` (ADR-008) | sauvegardes (`pg_dump`, copie hors serveur, restauration à prouver) — impact des pièces |
| `docs/phase/03/AUDIT_JOURNAL_03D.md`, `docs/phase/03/sql/0005_AuditJournal.sql`, `src/MMV.Domain/Audit/AuditEvent.cs` | format des codes d'action (`module.action`), contrainte des 8 permissions, journal des seules actions réussies |
| `apps/desktop/src-tauri/tauri.conf.json` | CSP (`img-src 'self' data:`, aucun `blob:` ni cadre) |
| `apps/desktop/src/shell/navigation.ts`, `clients/ClientDetailView.tsx`, `appointments/appointmentLaunch.ts`, `views/ForbiddenView.tsx`, `clients/*`, `components/ui/*`, `test/fakeMmvApi.ts` | composants et mécanismes réutilisés par la préparation 04-A (entrées filtrées par droit, `returnTo`, double de test `node:http`) |
| `references/phase04/lot3/MMV-Lot3-Ordonnances.dc.html` (empreinte inchangée) | libellés des colonnes de la grille (Sphère, Cylindre, Axe, Addition, Prisme, Base) ; aucune règle reprise |

Toujours aucune lecture de l'ancien dépôt, aucune connexion PostgreSQL, aucune dépendance ajoutée. Les volumes de stockage de l'étude E-05 sont des **hypothèses** de l'agent, à remplacer par les chiffres du propriétaire (D-04-14.g).

## Complément daté — 19/10/2026 (04-A, GO G04-A — UI ONLY)

| Source | Usage |
|---|---|
| `prompts/PHASE04_A_GO_CLAUDE_CODE.md` | mandat 04-A (message de session du propriétaire, reproduit à l'identique) |
| tous les documents de `docs/phase/04/` (y compris le pack non suivi `CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md`, `README_PACK.md`, lus sans modification) | périmètre, propositions, états, parcours |
| `references/phase04/lot3/MMV-Lot3-Ordonnances.dc.html` (SHA-256 `c103df9e…c06c213` revérifié le 19/10/2026) | libellés du Lot 3 repris : « Informations générales », « Correction optique », « Œil droit (OD) » / « Oculus dexter », Sphère, Cylindre, Axe, Addition, Prisme, Base, Acuité visuelle, « Remarques complémentaires (facultatif) », « Rechercher par client ou médecin » (adapté : prescripteur), « Veuillez corriger les erreurs avant d'enregistrer » ; **non repris** : pas de 0,25 et boutons ± , signe « + » imposé, plages numériques, axe / base désactivés, normalisation d'axe, suppression, « Créer une vente », identités de démonstration de la maquette (remplacées par des identités `Fictif-…`) |
| `apps/desktop/src/` (Phase 03 fusionnée) | composants réutilisés (voir [`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 9) |

Toujours aucune lecture de l'ancien dépôt, aucune connexion PostgreSQL (17 ou 18), aucune dépendance ajoutée. Données du banc : identités, prescripteurs, collaborateurs, valeurs optiques et pièces **fictives et synthétiques**, générées en mémoire (`apps/desktop/src/bench/benchSeed.ts`, `benchImages.ts` *(supprimé le 20/10/2026, D-04-16)*).

## Complément daté — 20/10/2026 (04-A, refonte UX Lot 3, D-04-15)

| Source | Usage |
|---|---|
| `prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md` | mandat de la refonte (message de session du propriétaire, reproduit à l'identique) ; **D-04-15 APPROVED** |
| `references/phase04/lot3/MMV-Lot3-Ordonnances.dc.html` (SHA-256 `c103df9e434245cdcba5b6246c5ec3032301e323c6ec1797826deaf8fd06c213` revérifié le 20/10/2026, 93 625 octets) | **référence UX officielle** : proportions et composants repris — `.phead` (titre, badge Ctrl + S, actions), `.card` / `.card-b` 20 × 22 px, titre de section à pastille 30 px, `.clictx` (avatar 44 px, 560 px max), `genInfo` (3 colonnes, 16 × 20 px), `.eyes` (2 colonnes, 22 px), `.eye-h` / `.eye-tag` (40 px), `.eye-b` (16 × 18 px, 13 px), `.orow` (118 px + contrôle), `.stepper` / `.stepbtn` (62 px) / `.stepval` (42 px), `.oinp` + suffixe « degrés », `.ohelp` / `.oerr`, empilement en fenêtre réduite ; libellés « Œil droit » / « Œil gauche », « Oculus dexter / sinister », « Fiche client », « Référence » / « Attribuée à l'enregistrement », « Nasal / Temporal / Supérieur / Inférieur » (liste **proposée** seulement). **Toujours non repris** : règles Cylindre ⇄ Axe et Prisme ⇄ Base, champs désactivés, normalisation d'axe, plages, astérisque de date, suppression, « Créer une vente », repères « 0,00 » / « 0–180 » / « 10/10 » ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 11.4) |
| `C:\Users\…\Downloads\MMV initialisation projet V1\` (dossier de conception du propriétaire, **lecture seule**, non copié) : `MMV-Lot3-Ordonnances.dc.html` (même empreinte), `support.js` (SHA-256 `ae4f0ac8449655e17cca1e3b179effcb6817a3b0d8dc47f112a9c39c25c39fd7`), `_ds/mmv-managemyvision-design-system-90800da0-…/` | **rendu** de la maquette pour les captures comparatives (serveur statique local 127.0.0.1:1530, Edge sans interface) ; `support.js` charge React 18.3.1 et Babel depuis unpkg, la maquette charge Lucide 0.544.0 depuis unpkg : ce chargement concerne **le rendu de la maquette seulement**, jamais le banc (garde réseau inchangée) |

Toujours aucune lecture de l'ancien dépôt ni de l'ancien code MMV, aucune connexion PostgreSQL, aucune dépendance ajoutée. Les cadres capturés de la maquette contiennent ses identités de démonstration (conception du propriétaire) ; le banc n'emploie que des données `Fictif-…`.

## Complément daté — 20/10/2026 (04-A, simplification, D-04-16)

| Source | Usage |
|---|---|
| `prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md` | mandat de la simplification (message de session du propriétaire, reproduit à l'identique) ; **D-04-16 APPROVED** |
| code du prototype `apps/desktop/src/optical/`, `apps/desktop/src/bench/`, `apps/desktop/e2e/bench-04a/` (HEAD `9be2fda`) | état de départ modifié (§ 12 de [`LIVRAISON_04A.md`](LIVRAISON_04A.md)) ; `SourceDocumentsPanel.tsx` et `src/bench/benchImages.ts` supprimés |

Maquette Lot 3 non modifiée et non rouverte (D-04-15 inchangée : les cartes OD / OG de la maquette ne comportent pas d'état de l'œil). Toujours aucune lecture de l'ancien dépôt, aucune connexion PostgreSQL, aucune dépendance ajoutée.

## Complément daté — 20/10/2026 (04-A, enregistrement simplifié et clôture, D-04-17)

| Source | Usage |
|---|---|
| `prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md` | mandat de clôture de 04-A (message de session du propriétaire, reproduit à l'identique) ; **D-04-17 APPROVED** |
| code du prototype `apps/desktop/src/optical/`, `apps/desktop/src/bench/`, `apps/desktop/e2e/bench-04a/` (HEAD `64cedc4`) | état de départ modifié (§ 13 de [`LIVRAISON_04A.md`](LIVRAISON_04A.md)) |
| `apps/desktop/src/components/ui/button.tsx` (lecture seule) | variantes du Design System : `primary` pour « Enregistrer », `tertiary` pour « Enregistrer comme brouillon » |

Maquette Lot 3 non modifiée et non rouverte (l'en-tête « Annuler / Enregistrer » de la maquette est conservé ; seule l'action discrète « Enregistrer comme brouillon » s'y ajoute). Toujours aucune lecture de l'ancien dépôt, aucune connexion PostgreSQL, aucune dépendance ajoutée ; données du banc fictives et synthétiques.

## Complément daté — 27/10/2026 (04-B, GO d'implémentation)

| Source | Usage |
|---|---|
| [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md) | mandat officiel d'implémentation 04-B / 04-C (copie du propriétaire, suivie dans Git avec cette livraison) |
| code 04-A (HEAD `3bb25a6`) : `apps/desktop/src/optical/`, `apps/desktop/src/bench/` | écrans réutilisés sans refonte ; contrat TypeScript étendu (`createAndConfirm`, `saveAndConfirm`, `historyAvailable`, provenance libre) |
| conventions Phase 03 : `src/MMV.*/Clients/`, `Appointments/`, `Audit/` ; `tests/MMV.PostgreSql.Tests/` | modèles de couches, stores, tests, nettoyage des données validées |
| [`sql/0006_OpticalModule.idempotent.sql`](sql/0006_OpticalModule.idempotent.sql) | `927ed7ccb975b15eed18f6b2bcee41faf08ecb23c1c4f9f1f80a2efa1706c013` |
| [`sql/0006_OpticalModule.sql`](sql/0006_OpticalModule.sql) | `7c5aba78035c39bf834311cc089c4eabc71e1f69363b685a41c3c9fb78aa221b` |
| [`sql/0006_MANIFEST.sha256`](sql/0006_MANIFEST.sha256) | empreintes de tous les fichiers du dossier G04-SQL |
| PostgreSQL 18.6 local (`C:\Program Files\PostgreSQL\18\bin` : `initdb`, `pg_ctl`, `psql`, `pg_dump`) | cluster **jetable** de pré-essai (port 55433, répertoire temporaire de l'agent) — le serveur du propriétaire (5433) et PostgreSQL 17 n'ont pas été touchés |

Toujours aucune lecture de l'ancien dépôt, aucune dépendance ajoutée (ADR-014), maquette Lot 3 non modifiée.

## Complément daté — 28/10/2026 (04-C, G04-SQL accordée)

| Source | Usage |
|---|---|
| [`prompts/MMV_V2_G04_SQL_GO_PHASE04_C_2026-10-28.md`](../../../prompts/MMV_V2_G04_SQL_GO_PHASE04_C_2026-10-28.md) | mandat G04-SQL / 04-C (copie fidèle du message du propriétaire) |
| [`sql/0006_OpticalModule.idempotent.sql`](sql/0006_OpticalModule.idempotent.sql), [`sql/0006_OpticalModule.verify.sql`](sql/0006_OpticalModule.verify.sql), [`sql/0006_MANIFEST.sha256`](sql/0006_MANIFEST.sha256) | script appliqué (inchangé, empreinte vérifiée), contrôle en lecture seule, manifeste (12 / 12 OK) — [`G04_SQL_DOSSIER.md`](G04_SQL_DOSSIER.md) conservé tel qu'approuvé |
| PostgreSQL 18.6 du propriétaire, port 5433 (`mmv_v2_dev`, `mmv_v2_test`) | précontrôles en lecture seule, sauvegarde `pg_dump -Fc` de dev hors dépôt, application, contrôles ; PostgreSQL 17 (5432) jamais contacté |
| [`LIVRAISON_04C.md`](LIVRAISON_04C.md) | rapport 04-C |
| `evidence/P04C_*` | [`P04C_preflight_20261028.log`](evidence/P04C_preflight_20261028.log), [`P04C_0006_apply_mmv_v2_dev_20261028T2147Z.log`](evidence/P04C_0006_apply_mmv_v2_dev_20261028T2147Z.log), [`P04C_0006_apply_mmv_v2_test_20261028T2148Z.log`](evidence/P04C_0006_apply_mmv_v2_test_20261028T2148Z.log), [`P04C_0006_verify_20261028.log`](evidence/P04C_0006_verify_20261028.log), [`P04C_counts_20261028.log`](evidence/P04C_counts_20261028.log), [`P04C_postgresql_20261028.log`](evidence/P04C_postgresql_20261028.log), [`P04C_dotnet_20261028.log`](evidence/P04C_dotnet_20261028.log), [`P04C_frontend_20261028.log`](evidence/P04C_frontend_20261028.log), [`P04C_http_matrix_20261028.log`](evidence/P04C_http_matrix_20261028.log), [`P04C_harness_c04http.cs.txt`](evidence/P04C_harness_c04http.cs.txt), [`P04C_native_optique-04c_20261028.log`](evidence/P04C_native_optique-04c_20261028.log) et 12 captures `P04C_native_c04_*.jpg` |
| [`apps/desktop/e2e/run-native.mjs`](../../../apps/desktop/e2e/run-native.mjs) | scénario natif `optique-04c` ajouté |
| [`apps/desktop/src/api/contract/optical.openapi.json`](../../../apps/desktop/src/api/contract/optical.openapi.json) | instantané régénéré (`MMV_UPDATE_OPENAPI_SNAPSHOT=1`) : ajout `citedUsers` seulement |

Toujours aucune lecture de l'ancien dépôt, aucune dépendance ajoutée (ADR-014), maquette Lot 3 non modifiée.
