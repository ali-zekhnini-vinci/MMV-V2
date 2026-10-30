# 05 — GATES_AND_DECISIONS

## GO et bornes

- **GO 05-00** (propriétaire, 29/10/2026) : vérification du dépôt après la fusion de la Phase 04 et cadrage ciblé — [`prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md`](../../../prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md).
- Interdits respectés en 05-00 : aucune migration, aucun changement de schéma, aucune table, aucune écriture ni connexion à `mmv_v2_dev` / `mmv_v2_test`, aucun processus du propriétaire touché, aucun code ni schéma de l'ancien logiciel consulté, aucun push / PR / merge, aucun fichier du propriétaire modifié, Phase 06 non commencée.

- **GO finalisation 05-00** (propriétaire, 30/10/2026) : réponses Q01–Q20, intégration des références Lot 6, création autorisée de `feat/phase-05-catalogue-stock`, commits documentaires locaux — [`prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md`](../../../prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md). Interdits respectés : aucun backend, aucune migration `0007` générée ni appliquée, aucune table, aucune écriture ni connexion à `mmv_v2_dev` / `mmv_v2_test`, aucun processus arrêté, `apps/desktop/.env.development` intact (SHA-256 `58d1eb06…c3481d` avant / après), aucun `reset --hard` / `clean` / `stash`, aucun push / PR / merge, ancien dépôt non consulté.

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
