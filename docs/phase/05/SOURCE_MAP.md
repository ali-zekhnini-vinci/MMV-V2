# 05 — SOURCE_MAP

## Sources lues (dépôt neuf MMV V2 et maquette Lot 6 uniquement)

| Source | Usage |
|---|---|
| `prompts/MMV_V2_PHASE05_00_DEMARRAGE_2026-10-29.md` | mandat 05-00 (copie versionnée du message du propriétaire) |
| `CLAUDE.md`, `README.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md`, `docs/phase/00.md` | gouvernance, DOCUMENTATION GATE, registre des décisions, backlog et séquence (Phase 05 « avant le premier encaissement réel ») |
| `docs/phase/04.md`, `docs/phase/04/GATES_AND_DECISIONS.md`, `SOURCE_MAP.md`, `QA_MATRIX.md`, `API_OPTICAL.md`, `IMPLEMENTATION_SPEC.md` (impacts Ventes) | conventions de documentation, d'API, d'idempotence, de concurrence et de verrous (O-04-53 / O-04-54) |
| `docs/phase/03/sql/0005_AuditJournal.sql` | formats des codes d'audit |
| `src/MMV.Domain/Identity/Permissions.cs` | permissions existantes |
| `src/MMV.Infrastructure/*/…Model.cs`, `…Store.cs` (recherche ciblée) | schémas existants (`auth`, `clients`, `appointments`, `audit`, `optical`), `xmin`, `archived_at_utc`, verrous |
| `registres/DESIGN_SOURCES_SHA256.csv` | empreinte attendue du Lot 6 |
| `references/phase04/catalogue/MMV_V2_Registre_134_Groupes.csv`, `MMV_V2_Registre_324_Etats.csv` | 37 groupes B-098 à B-134, 107 états `MMV-L06-*` |
| `Downloads/MMV initialisation projet V1/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` (dossier du propriétaire, lecture seule) | sections, légendes de planches, notes de conception ; **référence d'interface seulement** (D-05-01) |

Aucune lecture de l'ancien dépôt, de son code ou de ses bases (les mentions de l'ancien code figurent dans le texte même de la maquette, O-05-01). Aucune connexion à `mmv_v2_dev` ni `mmv_v2_test`.

## Maquette Lot 6

| Fichier | SHA-256 | Taille | Registre |
|---|---|---|---|
| `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` | `6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7` | 175 994 octets | identique à `registres/DESIGN_SOURCES_SHA256.csv` (37 groupes, 107 états, `DRAFT_NON_APPROVED`) |

La maquette n'a pas été copiée dans le dépôt ; un rendu de contrôle (procédé de la Phase 04 : serveur statique de travail hors dépôt) n'a pas été nécessaire au cadrage et sera fait avec G05-DESIGN.
