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

Toujours aucune lecture de l'ancien dépôt, aucune connexion PostgreSQL (17 ou 18), aucune dépendance ajoutée. Données du banc : identités, prescripteurs, collaborateurs, valeurs optiques et pièces **fictives et synthétiques**, générées en mémoire (`apps/desktop/src/bench/benchSeed.ts`, `benchImages.ts`).

