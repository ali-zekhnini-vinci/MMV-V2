# 05 — GATES_AND_DECISIONS

## GO et bornes

- **GO 05-00** (propriétaire, 29/10/2026) : vérification du dépôt après la fusion de la Phase 04 et cadrage ciblé — [`prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md`](../../../prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md).
- Interdits respectés en 05-00 : aucune migration, aucun changement de schéma, aucune table, aucune écriture ni connexion à `mmv_v2_dev` / `mmv_v2_test`, aucun processus du propriétaire touché, aucun code ni schéma de l'ancien logiciel consulté, aucun push / PR / merge, aucun fichier du propriétaire modifié, Phase 06 non commencée.

- **GO finalisation 05-00** (propriétaire, 30/10/2026) : réponses Q01–Q20, intégration des références Lot 6, création autorisée de `feat/phase-05-catalogue-stock`, commits documentaires locaux — [`prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md`](../../../prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md). Interdits respectés : aucun backend, aucune migration `0007` générée ni appliquée, aucune table, aucune écriture ni connexion à `mmv_v2_dev` / `mmv_v2_test`, aucun processus arrêté, `apps/desktop/.env.development` intact (SHA-256 `58d1eb06…c3481d` avant / après), aucun `reset --hard` / `clean` / `stash`, aucun push / PR / merge, ancien dépôt non consulté.

## Mise à jour des gates — 03/11/2026 (G05-SQL-0008 : GO conditionnel, application et preuves réelles)

- **GO G05-SQL-0008** (propriétaire, 03/11/2026, après audit de la migration) : application de `docs/phase/05/sql/0008_CatalogCorrections.idempotent.sql`, SHA-256 imposé `9a75acea0173a5019b520362cf04a5ca10e1de814753603f21d46c1c287e715c`, sur `mmv_v2_dev` puis `mmv_v2_test` — [`prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md`](../../../prompts/MMV_V2_G05_SQL_0008_GO_APPLICATION_2026-11-03.md). Le rapport d'audit cité (`MMV_V2_AUDIT_G05_SQL_0008_2026-11-03.md`) n'est pas présent dans l'espace de travail (O-05-25).
- Précontrôle « écriture concurrente » d'abord **non conforme** (API 7267 et `tauri dev` du propriétaire connectés à `mmv_v2_dev`) : STOP avant toute écriture, question au propriétaire, qui a arrêté lui-même ses processus ; revérifié (0 session) avant chaque application.

| Gate | État au 03/11/2026 |
|---|---|
| G05-SQL-0008 | **APPLIED** — dev 15:35:18, test 15:35:59 (+01:00), COMMIT, `verify.sql` 14 / 14 × 2, données préexistantes identiques ; preuves [`evidence/sql0008/`](evidence/sql0008/) |
| G05-CORR (corrections) | **`PHASE05_CORRECTIONS_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`** — registre [`CORRECTIONS_05.md`](CORRECTIONS_05.md), rapport [`LIVRAISON_05_CORRECTIONS.md`](LIVRAISON_05_CORRECTIONS.md) |
| G05-DESIGN | **OPEN** — inchangé (acceptation visuelle du propriétaire ; nouvelles captures natives import CSV et offres de verres à examiner) |
| G05-FISCAL / G05-LEGAL | **OPEN** — inchangés |
| G05-FINAL | **OWNER_FINAL_REVIEW_REQUIRED** — audit final du propriétaire ; Phase 05 non déclarée fusionnable ; aucun push, PR ni merge |

Les états ci-dessous sont conservés tels que publiés.

## Mise à jour des gates — 02/11/2026 (corrections COR-05-01 à COR-05-08)

- **GO corrections** (propriétaire, 02/11/2026) : huit corrections métier et ergonomiques validées, travail autonome — [`prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md`](../../../prompts/MMV_V2_MANDAT_CORRECTIONS_PHASE05_2026-11-02.md).

| Gate | État au 02/11/2026 |
|---|---|
| G05-CORR (corrections) | **`PHASE05_CORRECTIONS — OWNER_SQL_REVIEW_REQUIRED`** — registre [`CORRECTIONS_05.md`](CORRECTIONS_05.md) |
| G05-SQL-0008 | **OPEN** — migration additive `0008_CatalogCorrections` préparée, éprouvée sur cluster jetable (verify 14 / 14, PostgreSQL 20 / 20), **non appliquée** : [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md) |
| G05-DESIGN | **OPEN** — acceptation visuelle du propriétaire (Lot 6) toujours requise ; nouvelles surfaces des corrections à comparer |
| G05-FISCAL / G05-LEGAL | **OPEN** — inchangés |
| G05-FINAL | NOT_STARTED — après GO SQL 0008, application, suites complètes et revue finale |

Les états ci-dessous sont conservés tels que publiés.

## Mise à jour des gates — 30/10/2026

| Gate | État au 30/10/2026 |
|---|---|
| G05-00 | **`PHASE05_00_COMPLETE — OWNER_IMPLEMENTATION_REVIEW_REQUIRED`** |
| G05-METIER | **PASSED en partie** : Q01–Q20 ratifiées (D-05-01, 02, 03, 04, 05, 06, 10, 11, 12 en principe ou corrigées ; D-05-14 à D-05-21 nouvelles) ; restent ouverts D-05-07, D-05-09, D-05-13 et QR-01 à QR-13 ([`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md)) |
| G05-DESIGN | **OPEN** — GAP-05-01 à 09 et GAP-05-10 à 17 ([`MATRICE_LOT6.md`](MATRICE_LOT6.md) §§ 2 et 5), aucune planche fabriquée |
| G05-A | **NOT_STARTED** — GO explicite requis ; réponses préalables QR-01, 02, 05, 07, 08, 09, 12 |
| G05-SQL-A / B / C / D | NOT_STARTED — migrations `0007` / `0008` / `0009` / `0010`, aucune générée |
| G05-FISCAL (dans ADR-010) | **OPEN** — taux, catégories fiscales, arrondi de la taxe par magasin BE / MA : aucun ratifié ; bloque le TTC et toute vente réelle, pas 05-A |
| G05-LEGAL | **OPEN** — inchangé |
| G05-FINAL | NOT_STARTED |

Le tableau ci-dessous est l'état du 29/10/2026, conservé.

## Gates Phase 05

| Gate | Objet | Condition de passage | État au 29/10/2026 |
|---|---|---|---|
| G05-00 | cadrage | dossier `docs/phase/05/` livré, fusion Phase 04 vérifiée, Lot 6 authentifié | **`PHASE05_00 — OWNER_BUSINESS_REVIEW_REQUIRED`** |
| G05-METIER | D-05-01 à D-05-13 | réponse du propriétaire ([`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md)) ; ratification **explicite** des trois politiques (D-05-02) | **OPEN** |
| G05-DESIGN | états sans planche GAP-05-01 à 09 ([`MATRICE_LOT6.md`](MATRICE_LOT6.md) § 2) | accord pour composer à partir des composants V2 (voie de la Phase 04), validation visuelle après captures | **OPEN** |
| G05-A | démarrage de 05-A | GO explicite après G05-METIER | NOT_STARTED |
| G05-SQL-A / B / C | migrations `0007` / `0008` / `0009` | revue du SQL exact, empreinte imposée, sauvegarde vérifiée (procédé G04-SQL) | NOT_STARTED |
| G05-LEGAL | Maroc / Belgique | traçabilité des dispositifs médicaux (lot, identifiant unique), conservation des pièces comptables et des inventaires, identifiants légaux fournisseurs ; avis du comptable / juriste | **OPEN** — bloque toute vente réelle, pas 05-A / 05-B |
| G05-FINAL | PR Phase 05 | sous-lots livrés, tests réels, revue complète, GO propriétaire | NOT_STARTED |

Dépendance externe : **ADR-010 (`OPEN DECISION`)** — fiscalité ; ne bloque pas la Phase 05 (catégorie fiscale prévue vide), bloque la première vente réelle (Phase 06).

## Points ouverts

| ID | Point | Proposition | Décideur |
|---|---|---|---|
| O-05-01 | Le Lot 6 déclare dériver du code de l'ancien logiciel (entités, énumérations, DTO, règles) | Lot 6 = référence d'interface seulement (D-05-01) ; écarts listés en [`MATRICE_LOT6.md`](MATRICE_LOT6.md) § 3 | propriétaire |
| O-05-02 | Arbre de travail non propre : `apps/desktop/.env.development` modifié par le propriétaire (+ `VITE_MMV_OPTICAL_MODULE`) ; branche `feat/phase-05-catalogue-stock` **non créée** | le propriétaire choisit : (a) conserver sa modification locale et autoriser `git switch -c feat/phase-05-catalogue-stock` (n'altère aucun fichier) ; (b) la valider, la remiser ou l'annuler lui-même, puis création de la branche | propriétaire |
| O-05-03 | Détail des « compléments catalogue, achats et stock » du backlog Phase 00 absent du dépôt | fournir la liste ; en attendant, hors périmètre (D-05-12) | propriétaire |
| O-05-04 | Catégories, motifs de mouvement, devise de l'installation pilote | listes à fournir avec G05-METIER (D-05-05, D-05-09, D-05-08) | propriétaire |
| O-05-05 | Traçabilité réglementaire (lot / identifiant unique) des lentilles et verres ; inventaire annuel et conservation | G05-LEGAL ; BL-05-03 remonte avant la Phase 06 si obligatoire | conseil |
| O-05-06 | Retrait de permission ou désactivation d'un compte pendant une écriture de stock | reconduire la règle de la Phase 04 (droit contrôlé à l'entrée, session révocable) ; pas de verrou du compte sauf décision contraire | propriétaire |
| O-05-07 | Pack de cadrage Phase 04 du propriétaire toujours non suivi (`docs/phase/04/CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md`, `PROMPT_CLAUDE_CODE_PHASE04_00.md`, `README_PACK.md`) | inchangé (O-04-14) | propriétaire |

Mise à jour 30/10/2026 :

| ID | Point | État / proposition | Décideur |
|---|---|---|---|
| O-05-01 | Lot 6 dérivé de l'ancien logiciel | **CLOSED** — D-05-01 `APPROVED` | — |
| O-05-02 | Branche non créée (arbre non propre) | **CLOSED** — option (a) autorisée : `feat/phase-05-catalogue-stock` créée depuis `0a83a57` avec la modification locale du propriétaire conservée telle quelle (non commitée) | — |
| O-05-03 | Détail des « compléments catalogue, achats et stock » | **OPEN** — inchangé ; Q12 confirme commandes fournisseurs hors Phase 05 | propriétaire |
| O-05-04 | Catégories, motifs, devise | **en partie CLOSED** — six familles (Q01), devise par magasin EUR / MAD (Q14, Q20) ; motifs ouverts (QR-03) | propriétaire |
| O-05-05, O-05-06, O-05-07 | — | inchangés | — |
| O-05-08 | `references/phase05/` est une **copie de travail non versionnée** (convention des Phases 03 et 04) ; empreintes versionnées dans [`SOURCE_MAP.md`](SOURCE_MAP.md) | conserver la convention ; versionner les références = décision du propriétaire | propriétaire |
| O-05-09 | Le coefficient révèle le prix d'achat (prix HT ÷ coefficient) | coefficient traité comme un coût (`costs.read`) — intégré à la spécification | propriétaire (confirmation) |

## Backlog proposé (à inscrire au backlog permanent après G05-METIER)

| ID | Sujet | Origine |
|---|---|---|
| BL-05-01 | Commandes fournisseurs et réceptions rapprochées | D-05-12 |
| BL-05-02 | Réservations effectives (`stock.reservations`) | contrat 05-B, réalisation Phase 06 |
| BL-05-03 | Numéros de lot, dates de péremption, identifiants de dispositifs médicaux | D-05-12, G05-LEGAL |
| BL-05-04 | Multi-magasin, emplacements, transferts | D-05-12 |
| BL-05-05 | Lecture de codes-barres, étiquettes | D-05-12 |
| BL-05-06 | Valorisation du stock (méthode comptable), exports comptables | D-05-08, D-05-12 |

Mise à jour 30/10/2026 : BL-05-01 confirmé par Q12 (commandes fournisseurs complètes reportées) ; BL-05-02 : règle approuvée (D-05-17), réalisation Phase 06 ; BL-05-03 confirmé par Q07 (traçabilité individuelle ultérieure) ; **BL-05-04 réduit** aux transferts et opérations inter-emplacements / inter-magasins (l'architecture multi-magasins est préparée en Phase 05, D-05-15) ; BL-05-05 réduit à la lecture par scanner et aux étiquettes (champ code-barres en 05-A, D-05-14). Ajouts proposés :

| ID | Sujet | Origine |
|---|---|---|
| BL-05-07 | Permissions limitées à un magasin | D-05-15 |
| BL-05-08 | Arrondi commercial (0,05 / 0,10 / terminaisons) si souhaité | D-05-16, QR-09 |

## Journal

- 29/10/2026 : mandat 05-00 reçu ; `git fetch` ; fusion de la Phase 04 vérifiée (`0a83a57`, `dcc6f0d` inclus, arbre identique) ; arbre de travail non propre constaté (O-05-02), branche non créée ; Lot 6 authentifié (SHA-256 = registre) ; 37 groupes / 107 états cartographiés ; constat O-05-01 ; documents `docs/phase/05/` rédigés comme fichiers non suivis sur `main` (aucun commit) ; liens internes vérifiés ; aucune base, aucun processus, aucun fichier du propriétaire touchés. **STOP revue métier du propriétaire.**
- 30/10/2026 : réponses Q01–Q20 reçues, GO finalisation 05-00. `git fetch` : `main` = `origin/main` = `0a83a57`, 0 / 0. Branche `feat/phase-05-catalogue-stock` créée (`git switch -c`, aucun conflit, `.env.development` inchangé). Commit `6c207f9` : cadrage du 29/10 versionné **tel que livré** (historique). Lot 6 copié dans `references/phase05/lot6/` (15 fichiers, empreintes identiques aux originaux), index et manifeste générés. Registre D-05 mis à jour (APPROVED / PROPOSED / SUPERSEDED, D-05-14 à D-05-21 nouvelles, QR-01 à QR-13) ; modèle, API, spécification, QA, matrice révisés ; liens vérifiés. Aucune migration, aucune base, aucun processus touchés. **STOP : revue d'implémentation du propriétaire ; aucun développement sans GO 05-A.**

## Mise à jour des gates — 30/10/2026 (implémentation)

GO officiel d'implémentation complète ([`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md)) : sous-lots 05-A à 05-D autorisés sans GO intermédiaire ; seul arrêt obligatoire G05-SQL.

| Gate | État au 30/10/2026 (implémentation) |
|---|---|
| G05-A, 05-B, 05-C, 05-D | **IMPLEMENTED** (étapes internes ; [`LIVRAISON_05.md`](LIVRAISON_05.md)) |
| G05-SQL (unique, remplace G05-SQL-A/B/C/D) | **OWNER_REVIEW_REQUIRED** — [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md), manifeste [`sql/0007_MANIFEST.sha256`](sql/0007_MANIFEST.sha256) ; rien d'appliqué |
| G05-METIER (QR-01 à QR-13, D-05-07 / 09 / 13) | appliqués selon les règles du mandat ou des choix conservateurs **non ratifiés** ([`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md)) ; statuts du registre métier inchangés |
| G05-DESIGN | **OPEN** — écrans composés avec les composants V2 ; captures et revue visuelle NOT_TESTED |
| G05-FISCAL, G05-LEGAL | **OPEN** (inchangés) ; aucune vente réelle possible |
| G05-FINAL | NOT_STARTED |

Points ouverts ajoutés :

| ID | Point | Proposition | Décideur |
|---|---|---|---|
| O-05-10 | Maquette Lot 6 non versionnée : identifiants de l'ancien logiciel et coordonnées de fournisseurs dans le HTML et ses captures | conserver hors Git (procédure de récupération dans `references/phase05/README.md`) ou fournir une version expurgée | propriétaire |
| O-05-11 | Test Vitest `realAppIsolation` en échec à cause de la modification locale de `.env.development` (`VITE_MMV_OPTICAL_MODULE`) | modification du propriétaire, non touchée ; décider de la valider ou de la retirer | propriétaire |
| O-05-12 | Hypothèses non ratifiées (attributs, motifs, plafonds, compteur ≠ validateur non imposé…) | [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md) § 3 | propriétaire |

Backlog ajouté : BL-05-09 (exports CSV), BL-05-10 (écrans fiscaux après ADR-010) — [`docs/PERIMETRE_ET_SEQUENCE.md`](../../PERIMETRE_ET_SEQUENCE.md).

- 30/10/2026 (implémentation) : branche vérifiée (`feat/phase-05-catalogue-stock` descend de `0a83a57`) ; domaine, application, infrastructure, API, maintenance, écrans React, tests ; cluster PostgreSQL 18 jetable (port 55433) pour les essais ; aucune connexion à `mmv_v2_dev` / `mmv_v2_test` ; aucun processus du propriétaire arrêté (API observée sur 7267) ; `.env.development` intact ; références `references/phase05/` versionnées partiellement. **STOP G05-SQL — `PHASE05_IMPLEMENTED — OWNER_G05_SQL_REVIEW_REQUIRED`.**

## Mise à jour des gates — 30/10/2026 (G05-SQL accordée, intégration finale)

Mandat : [`prompts/MMV_V2_G05_SQL_GO_INTEGRATION_FINALE_2026-10-30.md`](../../../prompts/MMV_V2_G05_SQL_GO_INTEGRATION_FINALE_2026-10-30.md) — GO conditionnel du propriétaire sur le seul script `sql/0007_CatalogStockModule.idempotent.sql` (`39c9c224…1eb6`). Rapport : [`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md). Les tableaux ci-dessus sont conservés tels quels (historique).

| Gate | État au 30/10/2026 (intégration finale) |
|---|---|
| G05-SQL | **PASSED** — GO du propriétaire ; précontrôles conformes ; 0007 **appliquée** sur `mmv_v2_dev` (15:15:41) puis `mmv_v2_test` (15:16:07), `verify.sql` 27 / 27 ×2, données antérieures inchangées. [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md) est couvert par le manifeste revu : il reste **figé** et son « `NOT_APPLIED` » est l'état au moment de la revue — état courant : appliquée |
| G05-A, 05-B, 05-C, 05-D | **PASS sur cible réelle** — PostgreSQL `mmv_v2_test` 82 / 82 (dont HTTPS réel), Tauri natif 79 contrôles |
| G05-METIER (QR-01 à QR-13, D-05-07 / 09 / 13) | inchangé : choix conservateurs **non ratifiés** (R-F-06) |
| G05-DESIGN | **OPEN** — 16 captures clair / sombre livrées ; conformité au Lot 6 à confirmer par le propriétaire |
| G05-FISCAL, G05-LEGAL | **OPEN** (inchangés) ; aucune vente ni donnée réelle |
| G05-FINAL | **`PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`** — audit final du propriétaire ; aucun push / PR / merge |

Points ouverts — mise à jour :

| ID | Point | État / proposition | Décideur |
|---|---|---|---|
| O-05-11 | Vitest dépendant de `.env.development` du poste | **CLOSED (correctif)** — contrôle sur les `.env*` versionnés (index Git) + neutralisation des interrupteurs dans `src/test/setup.ts` ; `.env.development` non touché ; 311 / 311 ×2 ([`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md) § 5). La modification locale du propriétaire reste à valider ou retirer par lui | — |
| O-05-13 | Inventaire de schéma revu relevé sur un script CRLF : 8 empreintes de fonction ≠ base réelle (conforme au script LF approuvé) | **CLOSED (tests)** — inventaire revu conservé sous empreinte ; inventaire appliqué ajouté et vérifié par test ; à relire par l'auditeur | propriétaire (relecture) |
| O-05-14 | Aperçu de prix de l'interface refusé par l'API réelle (`requestId` en trop, 400 `invalid_body`) | **CLOSED (correctif)** — `previewCatalog` sans `requestId` ; test de non-régression ; API et SQL inchangés | — |
| O-05-15 | HTTP en clair : un compte **sans** le droit d'une route reçoit `403 forbidden` au lieu de `403 https_required` (l'autorisation précède le filtre HTTPS du groupe, ordre des Phases 02–04) ; aucune donnée servie | conserver (refus dans les deux cas) ou placer le contrôle HTTPS avant l'autorisation — décision du propriétaire | propriétaire |
| O-05-16 | Avertissement EF Core `QuerySplittingBehavior` (requête multi-collections) au journal de l'API | mesurer en pilote ; `AsSplitQuery` éventuel sans changement de schéma | technique |
| O-05-17 | Thème sombre : logo du shell sur fond blanc (`Sidebar.tsx`, non modifié par la Phase 05) | préexistant ; à traiter avec G05-DESIGN ou un lot d'interface | propriétaire |
| O-05-18 | Rapport `MMV_V2_AUDIT_G05_SQL_0007_2026-10-30.md` annoncé comme joint, absent de l'espace de travail | non lu ; le transmettre si des points d'audit doivent être vérifiés en plus du mandat | propriétaire |
| O-05-19 | Stock réservé : aucune API de réservation avant la Phase 06 ; la règle est éprouvée en posant `reserved` par SQL sur `mmv_v2_test` | à rejouer par l'API de réservation en Phase 06 (BL-05-02) | technique |

Réserves maintenues (R-F-01 à R-F-08, [`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md) § 7) ; backlog ajouté : BL-05-11 (contrainte emplacement ↔ magasin), BL-05-12 (conservation des contenus CSV) — [`docs/PERIMETRE_ET_SEQUENCE.md`](../../PERIMETRE_ET_SEQUENCE.md).

- 30/10/2026 (intégration finale) : précontrôles conformes ; sauvegardes `mmv_v2_dev` / `mmv_v2_test` hors dépôt (lisibles, restauration non éprouvée) ; 0007 appliquée dev puis test ; inventaire et contraintes rejoués (O-05-13) ; suites PostgreSQL complètes sur `mmv_v2_test` via MMV.Api réel ; Tauri natif ; correctifs O-05-11 et O-05-14 ; données fictives purgées (résidu 0) ; processus du propriétaire (MMV.Api 7267) jamais touchés ; `.env.development` intact. **STOP — `PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.**

## Mise à jour des gates — 31/10/2026 (G05-DESIGN : NO-GO puis refonte fidèle au Lot 6)

Mandat : [`prompts/MMV_V2_PROMPT_CLAUDE_PHASE05_REDESIGN_LOT6.md`](../../../prompts/MMV_V2_PROMPT_CLAUDE_PHASE05_REDESIGN_LOT6.md) — le propriétaire **refuse** le design livré le 30/10 (audit `MMV_V2_PHASE05_AUDIT_FIDELITE_LOT6.md`, comparaisons visuelles). Rapport : [`LIVRAISON_05_DESIGN.md`](LIVRAISON_05_DESIGN.md) ; matrice : [`G05_DESIGN_MATRIX.md`](G05_DESIGN_MATRIX.md). Les tableaux précédents sont conservés (historique).

| Gate | État au 31/10/2026 |
|---|---|
| G05-DESIGN | **`PHASE05_UX_LOT6_ALIGNED — OWNER_DESIGN_FINAL_REVIEW_REQUIRED`** — 107 cadres : 55 MATCH, 30 ADAPTED_APPROVED, 10 ADAPTED_PROPOSED, 3 DEFERRED_PROPOSED, 9 NOT_TESTED ; 146 captures natives appariées aux rendus du HTML original ; **acceptation visuelle du propriétaire requise** |
| G05-SQL | inchangé (PASSED) — aucun SQL, aucune migration, aucune empreinte modifiés (manifeste 12 / 12 OK le 31/10) |
| G05-A à 05-D | fonctionnel revérifié sous Tauri natif sur l'interface refondue (96 contrôles) ; tests backend NOT_RERUN (backend inchangé) |
| G05-FISCAL, G05-LEGAL | **OPEN** (inchangés) |
| G05-FINAL | en attente de l'acceptation G05-DESIGN puis de l'audit final ; aucun push / PR / merge |

Points ouverts — mise à jour :

| ID | Point | État / proposition | Décideur |
|---|---|---|---|
| O-05-17 | Logo du shell sur fond blanc en thème sombre | **OPEN** — actif approuvé du Shell-Handoff (Phase 02) ; le Lot 6 emploie `logo-dark.png` ; shell commun non modifié sans accord | propriétaire |
| O-05-20 | SKU interne : le mandat G05-DESIGN exige un SKU **généré** (correctif Q05) ; QR-08 avait retenu « saisi » | **appliqué côté poste** : proposition automatique (préfixe de famille + 6 symboles aléatoires), personnalisation facultative ; unicité inchangée côté serveur ; API et schéma inchangés | propriétaire (confirmation) |
| O-05-21 | Dix adaptations visuelles non prescrites (`ADAPTED_PROPOSED`) | à accepter ou à corriger ([`G05_DESIGN_MATRIX.md`](G05_DESIGN_MATRIX.md) § 6) | propriétaire |
| O-05-22 | Export CSV des cadres 14, 54, 102 non implémenté (BL-05-09 non ratifié) | confirmer le report ou demander l'export | propriétaire |

- 31/10/2026 (G05-DESIGN) : mandat, audit et ZIP de comparaison lus ; HTML Lot 6 original rendu (107 cadres) ; écrans Phase 05 recomposés (React / CSS seulement) ; banc natif (API copiée sur 7367, `mmv_v2_test`, données fictives, résidu 0) ; captures et planches ; suites frontend complètes PASS ; processus du propriétaire (MMV.Api 7267, `mmv-desktop`) jamais touchés ; `.env.development` intact. **STOP — `PHASE05_UX_LOT6_ALIGNED — OWNER_DESIGN_FINAL_REVIEW_REQUIRED`.**

## Mise à jour des gates — 03/11/2026 (lot UX-IA)

Mandat [`prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md`](../../../prompts/MMV_V2_MANDAT_UX_IA_PHASE05_2026-11-03.md) ; registre [`UX_IA_05.md`](UX_IA_05.md) ; livraison [`LIVRAISON_05_UX_IA.md`](LIVRAISON_05_UX_IA.md). Statut : **`PHASE05_UX_AI_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`**.

| Gate | État au 03/11/2026 (UX-IA) |
| --- | --- |
| G05-SQL (nouvelle migration) | **Non requise** : aucune donnée persistée nouvelle (seuil option A déjà dans `stock.variant_store_settings`, modèles d'import déjà dans `imports.supplier_profiles`, offres de verres déjà dans `catalog.lens_offers`). Aucune base réelle touchée par ce lot. |
| G05-DESIGN (nouveaux écrans) | **Revue visuelle du propriétaire requise** : assistant produit (étapes 3 à 5), import CSV (analyse, précisions, récapitulatif), catalogue des verres unifié et parcours « Nouvelle gamme ». Captures : `evidence/uxai/`. |
| Décisions métier du mandat | APPROVED (propriétaire) : magasin unique, tarification obligatoire, seuil option A — appliquées (D-UX-01, D-UX-02). |
| Décisions techniques du lot | D-UX-03 à D-UX-10 (`UX_IA_05.md` § 2), dont IA locale Ollama `llama3.2` sur le serveur, boucle locale seulement, validation serveur et confirmation des montants. |
| Questions ouvertes | Q-UX-01 (tarif aussi obligatoire hors assistant ?), Q-UX-02 (droit du seuil), Q-UX-03 (performances sur le matériel des magasins). |
