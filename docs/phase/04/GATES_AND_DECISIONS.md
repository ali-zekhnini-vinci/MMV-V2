# 04 — GATES_AND_DECISIONS

## GO et bornes

- **GO 04-00** (propriétaire, 19/10/2026) : préflight et cadrage documentaire — [`prompts/PHASE04_00_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_GO_CLAUDE_CODE.md).
- **Suite de 04-00** (propriétaire, 19/10/2026, après revue indépendante) : études préalables au modèle et préparation de 04-A — [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md). Mêmes interdits ; en outre aucune écriture de données de santé et aucune activation réelle du service.
- **GO G04-A** (propriétaire, 19/10/2026) : **G04-A APPROVED — UI ONLY**, O-04-16 APPROVED — [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md). N'approuve pas D-04-02 à D-04-14 ; aucune migration, aucune API optique, aucune permission `optical.*` attribuée, aucune donnée de santé réelle.
- Interdits respectés en 04-00 : aucun code applicatif, aucune migration, aucun SQL, aucune connexion à `mmv_v2_dev` / `mmv_v2_test` / PostgreSQL 17, aucun accès à l'ancien dépôt, aucun push / PR / merge, aucun fichier non suivi du propriétaire modifié, aucun processus du propriétaire interrompu.

## Gates Phase 04

| Gate | Objet | Condition de passage | État au 19/10/2026 |
|---|---|---|---|
| G04-00 | préflight et cadrage | dossier `docs/phase/04/` livré, merge vérifié, Lot 3 authentifié | **`PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`** ; *19/10/2026 (suite) :* revue indépendante — **« cadrage techniquement acceptable »** ; complément livré (études, préparation 04-A) : `PHASE04_04A_PREPARED — OWNER_GO_REQUIRED` |
| G04-METIER | D-04-02 à D-04-09 | réponse consolidée du propriétaire ; avis opticien pour D-04-06 (et listes fermées D-04-03) | **OPEN** — demande unique : [`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md) |
| G04-DESIGN | 12 états absents du Lot 3 (GAP-04-01 à 12) | complément Claude Design **ou** accord pour composer à partir des composants V2 | **OPEN** ; *19/10/2026 (suite) :* **voie accordée** par le propriétaire — composition à partir des composants V2 (y compris GAP-04-13, 14), **validation visuelle du propriétaire requise** après captures ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 4) ; *19/10/2026 (04-A) :* 14 états composés et capturés (navigateur) — `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED` ([`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md)) |
| G04-SQL | migration `0006` | revue du SQL exact par le propriétaire, empreinte imposée | NOT_STARTED (04-B non commencé) |
| G04-LEGAL | Maroc / Belgique : données de santé | avis juridique, formalités (CNDP, registre RGPD / APD), durées | **OPEN** — bloque tout pilote sur données réelles, pas 04-A / 04-B |
| G04-FINAL | PR Phase 04 | 04-D, revue complète, GO propriétaire | NOT_STARTED |
| G04-A *(ajoutée 19/10/2026)* | démarrage de 04-A (interface sur double de test) | GO explicite du propriétaire sur le périmètre de [`PREPARATION_04A.md`](PREPARATION_04A.md) § 3, et O-04-16 | **OPEN** — 04-A non commencé ; *19/10/2026 (GO) :* **APPROVED — UI ONLY** (mandat [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md)) ; livré : **`PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md)) |

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
| O-04-10 | taille du bundle (O-03-03) | route optique chargée à la demande — 04-A ; *19/10/2026 (04-A) :* sans objet en 04-A — le module optique n'est pas dans le bundle de production (banc hors `vite build`), à traiter à l'activation 04-C |
| O-04-11 | socle CSS commun (O-03-19) | extraction au début de 04-A ; *19/10/2026 (04-A) :* **reportée** — le prototype réutilise directement `clients/Clients.css` et n'ajoute que `optical/Optical.css` ; extraction lors de l'activation 04-C (éviter de toucher les modules fusionnés pour un prototype) |
| O-04-12 | idempotence des créations (`Idempotency-Key`) | 04-B, ADR-011 |
| O-04-13 | contrôle antivirus des pièces | aucun outil local ; formats restreints en attendant (D-04-03.c) |
| O-04-14 | pack du propriétaire non suivi dans `docs/phase/04/` (`CADRAGE…`, `PROMPT…`, `README_PACK.md`) | versionner, déplacer ou retirer : décision du propriétaire ; laissé intact |
| O-04-15 | 403 / 404 indiscernables sans `optical.read` | 04-B, test Q-04-106 |
| O-04-16 *(19/10/2026)* | pendant 04-A / 04-B, sans `optical.read` : onglet reporté « Ordonnances » et entrée `later` conservés tels qu'aujourd'hui (aucun changement visible pour les comptes réels) ; remplacement décidé en 04-C | décision du propriétaire avant l'étape A1 ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 2) ; *19/10/2026 :* **APPROVED** (G04-A) — comptes réels inchangés, prototypes **uniquement** dans le banc isolé `apps/desktop/optical-bench.html` (hors `vite build`) ; preuve Q-04-157 |
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
| 19/10/2026 | **GO G04-A — UI ONLY** ; O-04-16 APPROVED. Préflight : HEAD `1d18b81`, `origin/main` (`6924860`) ancêtre, aucune modification suivie, non suivis du propriétaire intacts. Réalisé : module prototype `apps/desktop/src/optical/` et banc isolé `apps/desktop/src/bench/` + `optical-bench.html` (servi par Vite en développement seulement), seule modification du code existant : emplacement facultatif `opticalTab` / `initialTab` de `clients/ClientDetailView.tsx` (non utilisé par l'application). 50 états tracés ([`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md)) ; captures navigateur ; tests ([`QA_MATRIX.md`](QA_MATRIX.md) Q-04-019 à Q-04-030). Aucun SQL, aucune migration, aucune base, aucune route ni permission optique. D-04-02 à D-04-14 restent `PROPOSED`. État : **`PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md)). |

## Points ouverts ajoutés — 19/10/2026 (04-A)

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-21 | premier lancement d'un serveur Vite de banc avec le cache par défaut : le pré-bundle des dépendances `apps/desktop/node_modules/.vite/deps` (partagé avec le serveur 1420 du propriétaire) a été régénéré le 19/10/2026 à 19:52 (« Re-optimizing dependencies because vite config has changed ») ; le banc a ensuite été servi par `e2e/bench-04a/serve.mjs` (cache hors dépôt, 127.0.0.1:1520) ; *constat de clôture :* ce premier serveur (`vite --port 1520`, PID 39580) n'avait pas été arrêté par l'arrêt de la tâche et a tourné jusqu'à la fin de la session, où il a été arrêté ; il n'a écrit que dans ce cache | sans effet attendu sur les fichiers sources ; en cas d'erreur « Outdated Optimize Dep » dans `tauri dev`, recharger la fenêtre (Vite régénère son cache) ; constat à confirmer par le propriétaire |
| O-04-22 | GAP-04-05 « onglet absent sans `optical.read` » | non appliqué en 04-A (O-04-16 : l'onglet reporté reste) ; décision et test à l'activation 04-C |
| O-04-23 | sélecteur de fichier réel, lecture en flux, aperçu PDF, ouverture de la CSP | non construits (banc : pièces synthétiques seulement) ; 04-B (envoi multipart sans fichier temporaire) et 04-C (CSP, visionneuse, preuve native), avec O-04-01, O-04-18 |
| O-04-24 | libellé « Dossier optique » présent dans le bundle de production (branche facultative de `ClientDetailView`, jamais rendue sans `opticalTab`) | sans effet visible ; retiré ou activé en 04-C |
| O-04-25 | revue visuelle du propriétaire des 50 états capturés et examen métier consolidé avec un opticien (D-04-02, D-04-06, listes D-04-03) sur le banc | préalable à G04-METIER / 04-B ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 9) |
| O-04-26 | test Phase 03 `ClientsModule.test.tsx` « O-03-43 » : 1 échec intermittent sous charge (délai de `findByRole`), PASS à chaque rejeu | surveiller ; si récurrent, allonger le délai d'attente de ce test (aucune modification faite en 04-A) |
