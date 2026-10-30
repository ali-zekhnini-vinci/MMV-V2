# 05 — GATES_AND_DECISIONS

## GO et bornes

- **GO 05-00** (propriétaire, 29/10/2026) : vérification du dépôt après la fusion de la Phase 04 et cadrage ciblé — [`prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md`](../../../prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md).
- Interdits respectés en 05-00 : aucune migration, aucun changement de schéma, aucune table, aucune écriture ni connexion à `mmv_v2_dev` / `mmv_v2_test`, aucun processus du propriétaire touché, aucun code ni schéma de l'ancien logiciel consulté, aucun push / PR / merge, aucun fichier du propriétaire modifié, Phase 06 non commencée.

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

## Backlog proposé (à inscrire au backlog permanent après G05-METIER)

| ID | Sujet | Origine |
|---|---|---|
| BL-05-01 | Commandes fournisseurs et réceptions rapprochées | D-05-12 |
| BL-05-02 | Réservations effectives (`stock.reservations`) | contrat 05-B, réalisation Phase 06 |
| BL-05-03 | Numéros de lot, dates de péremption, identifiants de dispositifs médicaux | D-05-12, G05-LEGAL |
| BL-05-04 | Multi-magasin, emplacements, transferts | D-05-12 |
| BL-05-05 | Lecture de codes-barres, étiquettes | D-05-12 |
| BL-05-06 | Valorisation du stock (méthode comptable), exports comptables | D-05-08, D-05-12 |

## Journal

- 29/10/2026 : mandat 05-00 reçu ; `git fetch` ; fusion de la Phase 04 vérifiée (`0a83a57`, `dcc6f0d` inclus, arbre identique) ; arbre de travail non propre constaté (O-05-02), branche non créée ; Lot 6 authentifié (SHA-256 = registre) ; 37 groupes / 107 états cartographiés ; constat O-05-01 ; documents `docs/phase/05/` rédigés comme fichiers non suivis sur `main` (aucun commit) ; liens internes vérifiés ; aucune base, aucun processus, aucun fichier du propriétaire touchés. **STOP revue métier du propriétaire.**
