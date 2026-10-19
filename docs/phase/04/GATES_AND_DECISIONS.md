# 04 — GATES_AND_DECISIONS

## GO et bornes

- **GO 04-00** (propriétaire, 19/10/2026) : préflight et cadrage documentaire — [`prompts/PHASE04_00_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_GO_CLAUDE_CODE.md).
- **Suite de 04-00** (propriétaire, 19/10/2026, après revue indépendante) : études préalables au modèle et préparation de 04-A — [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md). Mêmes interdits ; en outre aucune écriture de données de santé et aucune activation réelle du service.
- Interdits respectés en 04-00 : aucun code applicatif, aucune migration, aucun SQL, aucune connexion à `mmv_v2_dev` / `mmv_v2_test` / PostgreSQL 17, aucun accès à l'ancien dépôt, aucun push / PR / merge, aucun fichier non suivi du propriétaire modifié, aucun processus du propriétaire interrompu.

## Gates Phase 04

| Gate | Objet | Condition de passage | État au 19/10/2026 |
|---|---|---|---|
| G04-00 | préflight et cadrage | dossier `docs/phase/04/` livré, merge vérifié, Lot 3 authentifié | **`PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`** ; *19/10/2026 (suite) :* revue indépendante — **« cadrage techniquement acceptable »** ; complément livré (études, préparation 04-A) : `PHASE04_04A_PREPARED — OWNER_GO_REQUIRED` |
| G04-METIER | D-04-02 à D-04-09 | réponse consolidée du propriétaire ; avis opticien pour D-04-06 (et listes fermées D-04-03) | **OPEN** — demande unique : [`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md) |
| G04-DESIGN | 12 états absents du Lot 3 (GAP-04-01 à 12) | complément Claude Design **ou** accord pour composer à partir des composants V2 | **OPEN** ; *19/10/2026 (suite) :* **voie accordée** par le propriétaire — composition à partir des composants V2 (y compris GAP-04-13, 14), **validation visuelle du propriétaire requise** après captures ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 4) |
| G04-SQL | migration `0006` | revue du SQL exact par le propriétaire, empreinte imposée | NOT_STARTED (04-B non commencé) |
| G04-LEGAL | Maroc / Belgique : données de santé | avis juridique, formalités (CNDP, registre RGPD / APD), durées | **OPEN** — bloque tout pilote sur données réelles, pas 04-A / 04-B |
| G04-FINAL | PR Phase 04 | 04-D, revue complète, GO propriétaire | NOT_STARTED |
| G04-A *(ajoutée 19/10/2026)* | démarrage de 04-A (interface sur double de test) | GO explicite du propriétaire sur le périmètre de [`PREPARATION_04A.md`](PREPARATION_04A.md) § 3, et O-04-16 | **OPEN** — 04-A non commencé |

## Décisions métier

| ID | Sujet | État |
|---|---|---|
| D-04-01 | ordonnances médicales reçues et mesures magasin : deux types distincts | **APPROVED — propriétaire, 19/10/2026** (ne pas redemander) |
| D-04-02 | complétude, un seul œil, confirmation | PROPOSED [AVIS OPTICIEN] |
| D-04-03 | provenance, prescripteur, pièces source | PROPOSED [AVIS OPTICIEN] [CONSEIL JURIDIQUE] partiel |
| D-04-04 | rectification, révisions, invalidation (pas de suppression physique) | PROPOSED |
| D-04-05 | permissions optiques | PROPOSED |
| D-04-06 | validations OD/OG, axe, cylindre, addition, prisme / base | PROPOSED [AVIS OPTICIEN] — aucune règle par défaut |
| D-04-07 | client archivé | PROPOSED |
| D-04-08 | navigation, « Préparer une vente » inerte | PROPOSED |
| D-04-09 | pays, confidentialité, conservation | PROPOSED [CONSEIL JURIDIQUE] |
| D-04-10 *(19/10/2026)* | rattachement historisé des pièces aux révisions | PROPOSED ([`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md) E-01) |
| D-04-11 *(19/10/2026)* | révision confirmée et rectification en brouillon | PROPOSED (E-02) |
| D-04-12 *(19/10/2026)* | événements d'accès aux pièces (« transmis », jamais « vu ») | PROPOSED (E-03) ; c [CONSEIL JURIDIQUE] avant pilote |
| D-04-13 *(19/10/2026)* | granularité d'`optical.read`, profils indicatifs | PROPOSED (E-04) |
| D-04-14 *(19/10/2026)* | contrôles des fichiers, limites de stockage, sauvegardes | PROPOSED (E-05) |

## Conflits maquette ↔ propositions (Lot 3 DRAFT)

| Réf. | Maquette | Proposition | Décision |
|---|---|---|---|
| C-04-01 | normalisation de l'axe 0° → 180° à l'enregistrement | axe conservé tel que transcrit | D-04-06.b |
| C-04-02 | suppression physique, confirmation « irréversible » | invalidation motivée, conservation | D-04-04.c |
| C-04-03 | « Créer une vente » action principale après création | « Préparer une vente » désactivée, « Phase ultérieure » | D-04-08.c/d |
| C-04-04 | lecture seule liée au rôle « technicien atelier » | lecture seule dérivée des permissions serveur | D-04-05.e |
| C-04-05 | un seul type « ordonnance » | deux types distincts | D-04-01 (APPROVED) |
| C-04-06 | plages numériques et règles croisées issues de l'ancien validateur | ignorées ; à définir avec un opticien | D-04-06, D-000 |

## Points techniques ouverts

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-01 | CSP du poste (`img-src 'self' data:`) n'autorise ni `blob:` ni cadre pour l'aperçu des pièces | ouverture minimale (`img-src blob:`, cadre PDF local) à décider et tester en natif — 04-A ; *19/10/2026 (suite) :* reportée en **04-C** (preuve native) — 04-A prototypera l'aperçu image dans le navigateur de test, sans modifier la CSP ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 1) |
| O-04-02 | stockage des pièces dans PostgreSQL : taille de la base, durée des sauvegardes | mesurer en 04-C ; seuil de bascule à décider |
| O-04-03 | limites de requête (Kestrel, multipart) pour l'envoi des pièces | limite explicite = taille ratifiée — 04-B |
| O-04-04 | historique : filtrage serveur des événements `optical` sans `optical.read` | 04-B (D-04-05.c) |
| O-04-05 | `AuditFieldPolicy` : aucune valeur optique ; liste blanche étendue aux seuls codes de gestion | 04-B |
| O-04-06 | réponses 422 sans écho des valeurs | 04-B, test Q-04-140 |
| O-04-07 | immutabilité des révisions : contrôle applicatif + éventuels déclencheurs (inefficaces contre le rôle propriétaire) | 04-B, lié à O-B04 |
| O-04-08 | rôle SQL applicatif distinct (O-B04, hérité) | **avant tout pilote** sur données de santé |
| O-04-09 | restauration de sauvegarde jamais testée (hérité) | avant tout pilote |
| O-04-10 | taille du bundle (O-03-03) | route optique chargée à la demande — 04-A |
| O-04-11 | socle CSS commun (O-03-19) | extraction au début de 04-A |
| O-04-12 | idempotence des créations (`Idempotency-Key`) | 04-B, ADR-011 |
| O-04-13 | contrôle antivirus des pièces | aucun outil local ; formats restreints en attendant (D-04-03.c) |
| O-04-14 | pack du propriétaire non suivi dans `docs/phase/04/` (`CADRAGE…`, `PROMPT…`, `README_PACK.md`) | versionner, déplacer ou retirer : décision du propriétaire ; laissé intact |
| O-04-15 | 403 / 404 indiscernables sans `optical.read` | 04-B, test Q-04-106 |
| O-04-16 *(19/10/2026)* | pendant 04-A / 04-B, sans `optical.read` : onglet reporté « Ordonnances » et entrée `later` conservés tels qu'aujourd'hui (aucun changement visible pour les comptes réels) ; remplacement décidé en 04-C | décision du propriétaire avant l'étape A1 ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 2) |
| O-04-17 *(19/10/2026)* | analyse antivirus par AMSI (Windows) du tampon reçu | preuve technique avant pilote (D-04-14.e) |
| O-04-18 *(19/10/2026)* | commandes propres de la visionneuse PDF de WebView2 (impression, enregistrement) et menu contextuel du poste | constat et réglage en natif — 04-C ; aucune protection contre la copie n'est promise (E-03) |
| O-04-19 *(19/10/2026)* | sauvegarde / restauration avec pièces : taille, durées, empreintes | mesures sur fichiers synthétiques (1 Go, 5 Go) en 04-C ; restauration complète avant pilote (prolonge O-04-09) |
| O-04-20 *(19/10/2026)* | journalisation des lectures de dossiers | décision avant pilote avec G04-LEGAL (D-04-12.c) |

## Journal

| Date | Événement |
|---|---|
| 19/10/2026 | Merge Phase 03 vérifié : `origin/main` = `6924860` (PR #4), `93df475` et `abd0352` inclus, arbre identique à `abd0352`. Branche `feat/phase-04-00-cadrage` créée depuis `main` (avancé de `c0c3d6b` à `6924860` par avance rapide). |
| 19/10/2026 | Maquette `MMV-Lot3-Ordonnances.dc.html` trouvée localement, SHA-256 attendu **vérifié** ; catalogue 324 / 134 retrouvé dans `MMV_V2_Catalogue_Design_Complet_Zero.zip` ; copies de travail dans `references/phase04/`. |
| 19/10/2026 | D-04-01 inscrite **APPROVED** ; D-04-02 à D-04-09 présentées en une demande consolidée. Dossier 04-00 livré : `STATE_MAP_36.md`, `DECISIONS_04_METIER_PROPOSED.md`, `PARCOURS_04.md`, `DATA_MODEL_PROPOSED.md`, `API_OPTICAL_PROPOSED.md`, `PERMISSIONS_04_PROPOSED.md`, `QA_MATRIX.md`, `SOURCE_MAP.md`, `IMPLEMENTATION_SPEC.md`, ce document, `../04.md`. Liens internes vérifiés (Q-04-012). État : **`PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`**. |
| 19/10/2026 | Revue indépendante du dossier 04-00 : **G04-00 « cadrage techniquement acceptable »** ; D-04-01 APPROVED définitive ; D-04-02 à D-04-09 maintenues `PROPOSED`. Mandat de suite : [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md). G04-DESIGN : composition à partir des composants V2 accordée, validation visuelle du propriétaire requise. |
| 19/10/2026 | Complément livré (documentation seulement) : [`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md) (E-01 à E-05 → D-04-10 à D-04-14 `PROPOSED`), [`PREPARATION_04A.md`](PREPARATION_04A.md) (périmètre IND / PROTO / BLOQUÉ, 14 états composés, grille neutre, points bloquants) ; amendements datés du modèle, de l'API, des permissions (matrice par profil), des états, des parcours, de la spécification, de la QA et des sources. Aucun SQL, aucune migration, aucune base, aucun code. État : **`PHASE04_04A_PREPARED — OWNER_GO_REQUIRED`**. |
