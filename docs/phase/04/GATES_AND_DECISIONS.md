# 04 — GATES_AND_DECISIONS

## GO et bornes

- **GO 04-00** (propriétaire, 19/10/2026) : préflight et cadrage documentaire — [`prompts/PHASE04_00_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_GO_CLAUDE_CODE.md).
- **Suite de 04-00** (propriétaire, 19/10/2026, après revue indépendante) : études préalables au modèle et préparation de 04-A — [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md). Mêmes interdits ; en outre aucune écriture de données de santé et aucune activation réelle du service.
- **GO G04-A** (propriétaire, 19/10/2026) : **G04-A APPROVED — UI ONLY**, O-04-16 APPROVED — [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md). N'approuve pas D-04-02 à D-04-14 ; aucune migration, aucune API optique, aucune permission `optical.*` attribuée, aucune donnée de santé réelle.
- **GO refonte UX Lot 3** (propriétaire, 20/10/2026, après son essai du banc) : **D-04-15 APPROVED** — [`prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md). Reste dans 04-A (banc isolé) ; mêmes interdits : aucune migration `0006`, aucun schéma, aucune API optique, aucune donnée réelle, aucune base, aucun droit sur un compte réel, aucune activation en production (O-04-16).
- **GO simplification du dossier optique** (propriétaire, 20/10/2026) : **D-04-16 APPROVED** — [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). Suppression de « État de l'œil », report de la gestion des pièces sources au backlog permanent (BL-04-01), révisions conservées. Mêmes interdits : aucune migration `0006`, aucun backend optique, aucune table ni API de pièces, aucune activation réelle ; règles cliniques ouvertes non modifiées.
- **GO officiel d'implémentation 04-B / 04-C** (propriétaire, 27/10/2026) — [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md) : décisions métier et UX approuvées (ne plus les redemander), règles cliniques maintenues **sous réserve de validation professionnelle** ; code, SQL 0006 et raccordement autorisés ; **une seule validation intermédiaire : G04-SQL** (SQL exact, empreintes, dossier d'audit) avant toute application sur `mmv_v2_dev` / `mmv_v2_test` ; commits locaux seulement, aucun push / PR / merge ; aucune attribution de droits aux comptes réels sans autorisation.
- Interdits respectés en 04-00 : aucun code applicatif, aucune migration, aucun SQL, aucune connexion à `mmv_v2_dev` / `mmv_v2_test` / PostgreSQL 17, aucun accès à l'ancien dépôt, aucun push / PR / merge, aucun fichier non suivi du propriétaire modifié, aucun processus du propriétaire interrompu.

## Gates Phase 04

| Gate | Objet | Condition de passage | État au 19/10/2026 |
|---|---|---|---|
| G04-00 | préflight et cadrage | dossier `docs/phase/04/` livré, merge vérifié, Lot 3 authentifié | **`PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`** ; *19/10/2026 (suite) :* revue indépendante — **« cadrage techniquement acceptable »** ; complément livré (études, préparation 04-A) : `PHASE04_04A_PREPARED — OWNER_GO_REQUIRED` |
| G04-METIER | D-04-02 à D-04-09 | réponse consolidée du propriétaire ; avis opticien pour D-04-06 (et listes fermées D-04-03) | **OPEN** — demande unique : [`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md) ; *20/10/2026 :* périmètre réduit par D-04-16 (état par œil de D-04-02.c retiré ; D-04-03.c, D-04-10, D-04-12.a/b/d, D-04-14 et `optical.documents.*` de D-04-05.a reportés avec BL-04-01) — reste **OPEN** pour le reste, D-04-02.b à reformuler sans état d'œil (O-04-30) |
| G04-DESIGN | 12 états absents du Lot 3 (GAP-04-01 à 12) | complément Claude Design **ou** accord pour composer à partir des composants V2 | **OPEN** ; *19/10/2026 (suite) :* **voie accordée** par le propriétaire — composition à partir des composants V2 (y compris GAP-04-13, 14), **validation visuelle du propriétaire requise** après captures ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 4) ; *19/10/2026 (04-A) :* 14 états composés et capturés (navigateur) — `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED` ([`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md)) |
| G04-SQL | migration `0006` | revue du SQL exact par le propriétaire, empreinte imposée | NOT_STARTED (04-B non commencé) |
| G04-LEGAL | Maroc / Belgique : données de santé | avis juridique, formalités (CNDP, registre RGPD / APD), durées | **OPEN** — bloque tout pilote sur données réelles, pas 04-A / 04-B |
| G04-FINAL | PR Phase 04 | 04-D, revue complète, GO propriétaire | NOT_STARTED |
| G04-A *(ajoutée 19/10/2026)* | démarrage de 04-A (interface sur double de test) | GO explicite du propriétaire sur le périmètre de [`PREPARATION_04A.md`](PREPARATION_04A.md) § 3, et O-04-16 | **OPEN** — 04-A non commencé ; *19/10/2026 (GO) :* **APPROVED — UI ONLY** (mandat [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md)) ; livré : **`PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md)) ; *20/10/2026 :* revue du propriétaire — structure acceptée, ergonomie des formulaires refusée → D-04-15 ; refonte livrée : **`PHASE04_A_UX_LOT3_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 11) ; *20/10/2026 (D-04-16) :* simplification livrée : **`PHASE04_A_SIMPLIFICATION_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 12) ; *20/10/2026 (D-04-17, dernier mandat de 04-A) :* enregistrement simplifié et clôture technique du lot frontend : **`PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 13) — **ne vaut pas GO pour 04-B** (G04-METIER, G04-SQL toujours requis) |

## Décisions métier

| ID | Sujet | État |
|---|---|---|
| D-04-01 | ordonnances médicales reçues et mesures magasin : deux types distincts | **APPROVED — propriétaire, 19/10/2026** (ne pas redemander) |
| D-04-02 | complétude, un seul œil, confirmation | PROPOSED [AVIS OPTICIEN] ; *20/10/2026 :* **état par œil de D-04-02.c retiré par D-04-16** (« absence ≠ 0 » conservée) ; a, b, d inchangées — b à reformuler sans état d'œil (O-04-30) ; *20/10/2026 (D-04-17) :* le banc applique une **règle minimale de démonstration** (aucune valeur optique → confirmation refusée), qui **ne tranche pas** D-04-02.b ni D-04-03.a/b |
| D-04-03 | provenance, prescripteur, pièces source | PROPOSED [AVIS OPTICIEN] [CONSEIL JURIDIQUE] partiel ; *20/10/2026 :* **c (pièces source) REPORTÉE — backlog permanent BL-04-01** (D-04-16) ; a, b, d inchangées |
| D-04-04 | rectification, révisions, invalidation (pas de suppression physique) | PROPOSED |
| D-04-05 | permissions optiques | PROPOSED ; *20/10/2026 :* `optical.documents.read` / `.write` retirées de l'option B (D-04-16, reportées avec BL-04-01) ; d (journal des pièces) sans objet en 04 |
| D-04-06 | validations OD/OG, axe, cylindre, addition, prisme / base | PROPOSED [AVIS OPTICIEN] — aucune règle par défaut |
| D-04-07 | client archivé | PROPOSED |
| D-04-08 | navigation, « Préparer une vente » inerte | PROPOSED |
| D-04-09 | pays, confidentialité, conservation | PROPOSED [CONSEIL JURIDIQUE] |
| D-04-10 *(19/10/2026)* | rattachement historisé des pièces aux révisions | PROPOSED ([`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md) E-01) ; *20/10/2026 :* **REPORTÉE — hors Phase 04, backlog permanent BL-04-01** (D-04-16) ; étude conservée |
| D-04-11 *(19/10/2026)* | révision confirmée et rectification en brouillon | PROPOSED (E-02) |
| D-04-12 *(19/10/2026)* | événements d'accès aux pièces (« transmis », jamais « vu ») | PROPOSED (E-03) ; c [CONSEIL JURIDIQUE] avant pilote ; *20/10/2026 :* **a, b, d REPORTÉES avec BL-04-01** (D-04-16) ; **c** (journalisation des lectures de dossiers) reste PROPOSED, avant pilote (O-04-20) |
| D-04-13 *(19/10/2026)* | granularité d'`optical.read`, profils indicatifs | PROPOSED (E-04) ; *20/10/2026 :* colonnes « pièces » de la matrice sans objet (D-04-16) |
| D-04-14 *(19/10/2026)* | contrôles des fichiers, limites de stockage, sauvegardes | PROPOSED (E-05) ; *20/10/2026 :* **REPORTÉE — hors Phase 04, backlog permanent BL-04-01** (D-04-16) ; étude conservée |
| D-04-15 *(20/10/2026)* | **référence UX officielle des formulaires optiques** : maquette HTML Claude Design Lot 3, avec contrôles rapides ± et saisie clavier complémentaire | **APPROVED — propriétaire, 20/10/2026**. Décision d'interface **seulement** : n'approuve ni pas, ni bornes, ni format, ni règle croisée, ni liste de bases (le pas 0,25 est un paramètre de démonstration) — **D-04-06 reste PROPOSED** ; n'importe aucune règle ni aucun code de l'ancien MMV. Identifiant vérifié libre avant inscription |
| D-04-16 *(20/10/2026)* | **simplification du dossier optique** : (a) suppression du champ « État de l'œil » des formulaires OD / OG, de la validation du prototype et du modèle proposé — chaque champ optique peut rester vide, jamais assimilé à zéro ; (b) report de la gestion documentaire (téléchargement, consultation, invalidation, rattachement des pièces sources aux révisions) au **backlog permanent BL-04-01**, aucune table ni API de pièces en Phase 04 ; (c) **conservés** : révisions, comparaison des valeurs, motif de rectification, traçabilité des auteurs, provenance (prescripteur, date de prescription…), séparation D-04-01, design D-04-15 | **APPROVED — propriétaire, 20/10/2026** ([`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md)). Ne modifie **aucune** règle clinique ouverte (D-04-02.b, D-04-03.a/b, D-04-06 restent PROPOSED). Identifiant vérifié libre avant inscription (D-04-15 dernier utilisé) |
| D-04-17 *(20/10/2026)* | **enregistrement optique simplifié** : action principale « Enregistrer », option secondaire discrète « Enregistrer comme brouillon », **sans suppression du cycle de révisions**. Dans le prototype, « Enregistrer » (bouton bleu, Ctrl+S, Entrée) enregistre le brouillon puis le confirme ; un échec de la confirmation après un brouillon enregistré est annoncé « Brouillon enregistré — confirmation refusée » (ou « non aboutie » pour une erreur de communication) ; la dernière révision confirmée reste préservée jusqu'à la confirmation d'une nouvelle version. Libellés simplifiés (« Modifier », « Historique des modifications », « Dernière révision confirmée » ; « en vigueur » retiré) | **APPROVED — propriétaire, 20/10/2026** ([`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md)). Décision d'interface : ne fixe **aucun** critère clinique — la règle du banc (« un enregistrement dont toutes les données optiques sont absentes ne peut pas être confirmé ») est une **démonstration**, pas une validation médicale ; D-04-02, D-04-03.a/b et D-04-06 restent PROPOSED. Identifiant vérifié libre avant inscription (D-04-16 dernier utilisé) |

## Conflits maquette ↔ propositions (Lot 3 DRAFT)

| Réf. | Maquette | Proposition | Décision |
|---|---|---|---|
| C-04-01 | normalisation de l'axe 0° → 180° à l'enregistrement | axe conservé tel que transcrit | D-04-06.b |
| C-04-02 | suppression physique, confirmation « irréversible » | invalidation motivée, conservation | D-04-04.c |
| C-04-03 | « Créer une vente » action principale après création | « Préparer une vente » désactivée, « Phase ultérieure » | D-04-08.c/d |
| C-04-04 | lecture seule liée au rôle « technicien atelier » | lecture seule dérivée des permissions serveur | D-04-05.e |
| C-04-05 | un seul type « ordonnance » | deux types distincts | D-04-01 (APPROVED) |
| C-04-06 | plages numériques et règles croisées issues de l'ancien validateur | ignorées ; à définir avec un opticien | D-04-06, D-000 |
| C-04-07 *(20/10/2026)* | boutons ± au pas de 0,25, signe « + » explicite (non repris le 19/10/2026) | repris comme **contrôles rapides** au pas de démonstration paramétrable ; aucun arrondi ni alignement ; la saisie au clavier n'est jamais reformatée ; « + » produit seulement par un clic | D-04-15 (APPROVED, interface) ; pas et format : D-04-06 |

## Points techniques ouverts

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-01 | CSP du poste (`img-src 'self' data:`) n'autorise ni `blob:` ni cadre pour l'aperçu des pièces | ouverture minimale (`img-src blob:`, cadre PDF local) à décider et tester en natif — 04-A ; *19/10/2026 (suite) :* reportée en **04-C** (preuve native) — 04-A prototypera l'aperçu image dans le navigateur de test, sans modifier la CSP ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 1) ; *20/10/2026 :* sans objet en Phase 04 — reporté avec BL-04-01 (D-04-16) |
| O-04-02 | stockage des pièces dans PostgreSQL : taille de la base, durée des sauvegardes | mesurer en 04-C ; seuil de bascule à décider ; *20/10/2026 :* sans objet en Phase 04 — reporté avec BL-04-01 (D-04-16) |
| O-04-03 | limites de requête (Kestrel, multipart) pour l'envoi des pièces | limite explicite = taille ratifiée — 04-B ; *20/10/2026 :* sans objet en Phase 04 — reporté avec BL-04-01 (D-04-16) |
| O-04-04 | historique : filtrage serveur des événements `optical` sans `optical.read` | 04-B (D-04-05.c) |
| O-04-05 | `AuditFieldPolicy` : aucune valeur optique ; liste blanche étendue aux seuls codes de gestion | 04-B |
| O-04-06 | réponses 422 sans écho des valeurs | 04-B, test Q-04-140 |
| O-04-07 | immutabilité des révisions : contrôle applicatif + éventuels déclencheurs (inefficaces contre le rôle propriétaire) | 04-B, lié à O-B04 |
| O-04-08 | rôle SQL applicatif distinct (O-B04, hérité) | **avant tout pilote** sur données de santé |
| O-04-09 | restauration de sauvegarde jamais testée (hérité) | avant tout pilote |
| O-04-10 | taille du bundle (O-03-03) | route optique chargée à la demande — 04-A ; *19/10/2026 (04-A) :* sans objet en 04-A — le module optique n'est pas dans le bundle de production (banc hors `vite build`), à traiter à l'activation 04-C |
| O-04-11 | socle CSS commun (O-03-19) | extraction au début de 04-A ; *19/10/2026 (04-A) :* **reportée** — le prototype réutilise directement `clients/Clients.css` et n'ajoute que `optical/Optical.css` ; extraction lors de l'activation 04-C (éviter de toucher les modules fusionnés pour un prototype) |
| O-04-12 | idempotence des créations (`Idempotency-Key`) | 04-B, ADR-011 |
| O-04-13 | contrôle antivirus des pièces | aucun outil local ; formats restreints en attendant (D-04-03.c) ; *20/10/2026 :* sans objet en Phase 04 — reporté avec BL-04-01 (D-04-16) |
| O-04-14 | pack du propriétaire non suivi dans `docs/phase/04/` (`CADRAGE…`, `PROMPT…`, `README_PACK.md`) | versionner, déplacer ou retirer : décision du propriétaire ; laissé intact |
| O-04-15 | 403 / 404 indiscernables sans `optical.read` | 04-B, test Q-04-106 |
| O-04-16 *(19/10/2026)* | pendant 04-A / 04-B, sans `optical.read` : onglet reporté « Ordonnances » et entrée `later` conservés tels qu'aujourd'hui (aucun changement visible pour les comptes réels) ; remplacement décidé en 04-C | décision du propriétaire avant l'étape A1 ([`PREPARATION_04A.md`](PREPARATION_04A.md) § 2) ; *19/10/2026 :* **APPROVED** (G04-A) — comptes réels inchangés, prototypes **uniquement** dans le banc isolé `apps/desktop/optical-bench.html` (hors `vite build`) ; preuve Q-04-157 |
| O-04-17 *(19/10/2026)* | analyse antivirus par AMSI (Windows) du tampon reçu | preuve technique avant pilote (D-04-14.e) ; *20/10/2026 :* reporté avec BL-04-01 (D-04-16) |
| O-04-18 *(19/10/2026)* | commandes propres de la visionneuse PDF de WebView2 (impression, enregistrement) et menu contextuel du poste | constat et réglage en natif — 04-C ; aucune protection contre la copie n'est promise (E-03) ; *20/10/2026 :* reporté avec BL-04-01 (D-04-16) |
| O-04-19 *(19/10/2026)* | sauvegarde / restauration avec pièces : taille, durées, empreintes | mesures sur fichiers synthétiques (1 Go, 5 Go) en 04-C ; restauration complète avant pilote (prolonge O-04-09) ; *20/10/2026 :* volet « pièces » reporté avec BL-04-01 (D-04-16) ; la restauration sans pièces reste exigée avant pilote (O-04-09) |
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
| 20/10/2026 | Essai du banc par le propriétaire : structure acceptée, ergonomie des formulaires de saisie refusée. **D-04-15 APPROVED** (référence UX : maquette Lot 3, boutons ±). Préflight : HEAD `26e515f`, aucune modification suivie, maquette revérifiée (`c103df9e…c06c213`). Réalisé dans le banc seulement : `opticalStep.ts` (pas de démonstration paramétrable), `EyeGrid.tsx` (cartes OD / OG, saisie ±), `OpticalRecordForm.tsx` (mise en page de la maquette), `Optical.css`, commande « Pas des boutons ± » du banc, plan de captures `plan-lot3.mjs`, tests (Q-04-170 à Q-04-179). Vitest 279 / 279, typage, lint, build PASS ; 34 + 54 captures navigateur ; natif NOT_TESTED ; .NET / PostgreSQL NOT_RERUN. D-04-02 à D-04-14 restent `PROPOSED`. État : **`PHASE04_A_UX_LOT3_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 11). |
| 20/10/2026 | **D-04-16 APPROVED** (simplification du dossier optique, [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md)). Préflight : HEAD `9be2fda`, aucune modification suivie, non suivis du propriétaire intacts. Réalisé dans le banc seulement : « État de l'œil » retiré (contrat provisoire `EyeValues` sans état, grille, tableau de lecture, récapitulatif de confirmation, comparaison, double) ; pièces sources retirées (panneau, carte « Pièces liées », opérations du double, droits simulés `optical.documents.*`, jeu fictif, `SourceDocumentsPanel.tsx` et `benchImages.ts` supprimés) ; révisions, comparaison, motif, auteurs et provenance conservés. Tests (Q-04-180 à Q-04-189) ; Vitest 279 / 279, typage, lint, build PASS ; 34 + 51 captures navigateur ; natif NOT_TESTED ; .NET / PostgreSQL NOT_RERUN. Aucune règle clinique ouverte modifiée. État : **`PHASE04_A_SIMPLIFICATION_READY — OWNER_VISUAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 12). |
| 20/10/2026 | **D-04-17 APPROVED** (enregistrement optique simplifié, [`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md), dernier mandat de 04-A). Préflight : HEAD `64cedc4`, aucune modification suivie, non suivis du propriétaire intacts. Réalisé dans le banc seulement : `OpticalRecordForm.tsx` (« Enregistrer » = brouillon puis confirmation, sans récapitulatif intermédiaire ; « Enregistrer comme brouillon » en bouton tertiaire ; Ctrl+S et Entrée = « Enregistrer » ; garde contre les doubles déclenchements ; échec partiel : formulaire resynchronisé sur le brouillon enregistré, bandeau « Brouillon enregistré — confirmation refusée / non aboutie », action « Laisser en brouillon ») ; libellés « Modifier », « Historique des modifications », « Dernière révision confirmée » ; double en mémoire : règle minimale de démonstration (aucune valeur optique → `422 incomplete`) à la place de la règle de provenance, pannes simulées de la confirmation seule (communication, 409). Aucun fichier .NET, SQL ni de configuration serveur modifié ; application réelle inchangée ; aucune migration. Frontend **288 / 288** ; captures 58 + 34, 0 erreur console, 0 défilement horizontal. État : **`PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`** ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 13). |

## Points ouverts ajoutés — 19/10/2026 (04-A)

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-21 | premier lancement d'un serveur Vite de banc avec le cache par défaut : le pré-bundle des dépendances `apps/desktop/node_modules/.vite/deps` (partagé avec le serveur 1420 du propriétaire) a été régénéré le 19/10/2026 à 19:52 (« Re-optimizing dependencies because vite config has changed ») ; le banc a ensuite été servi par `e2e/bench-04a/serve.mjs` (cache hors dépôt, 127.0.0.1:1520) ; *constat de clôture :* ce premier serveur (`vite --port 1520`, PID 39580) n'avait pas été arrêté par l'arrêt de la tâche et a tourné jusqu'à la fin de la session, où il a été arrêté ; il n'a écrit que dans ce cache | sans effet attendu sur les fichiers sources ; en cas d'erreur « Outdated Optimize Dep » dans `tauri dev`, recharger la fenêtre (Vite régénère son cache) ; constat à confirmer par le propriétaire |
| O-04-22 | GAP-04-05 « onglet absent sans `optical.read` » | non appliqué en 04-A (O-04-16 : l'onglet reporté reste) ; décision et test à l'activation 04-C |
| O-04-23 | sélecteur de fichier réel, lecture en flux, aperçu PDF, ouverture de la CSP | non construits (banc : pièces synthétiques seulement) ; 04-B (envoi multipart sans fichier temporaire) et 04-C (CSP, visionneuse, preuve native), avec O-04-01, O-04-18 ; *20/10/2026 :* sans objet en Phase 04 — reporté avec BL-04-01 (D-04-16) |
| O-04-24 | libellé « Dossier optique » présent dans le bundle de production (branche facultative de `ClientDetailView`, jamais rendue sans `opticalTab`) | sans effet visible ; retiré ou activé en 04-C |
| O-04-25 | revue visuelle du propriétaire des 50 états capturés et examen métier consolidé avec un opticien (D-04-02, D-04-06, listes D-04-03) sur le banc | préalable à G04-METIER / 04-B ([`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 9) |
| O-04-26 | test Phase 03 `ClientsModule.test.tsx` « O-03-43 » : 1 échec intermittent sous charge (délai de `findByRole`), PASS à chaque rejeu | surveiller ; si récurrent, allonger le délai d'attente de ce test (aucune modification faite en 04-A) ; *20/10/2026 :* non reproduit (suite complète 279 / 279), toujours non modifié |

## Points ouverts ajoutés — 20/10/2026 (refonte UX Lot 3, D-04-15)

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-27 | test 04-A « erreurs techniques bornées (états 9, 31) » intermittent : **reproduit sur le code d'origine `26e515f`** (2 échecs sur 4 exécutions isolées) ; cause : la panne de liste est réessayée une fois après 1 s (`shouldRetry`), l'attente par défaut de Testing Library est de 1 s | **corrigé** dans le test seul (attente de 4 s pour ce titre) ; 4 / 4 PASS ensuite ; aucun code applicatif modifié |
| O-04-28 | en-tête collant du formulaire : son recouvrement suppose le rembourrage de la zone défilante (`--space-6`, `--space-4` sous 1 100 px) ; le Shell réel a d'autres paliers | à revérifier à l'activation 04-C (intégration au Shell réel) |
| O-04-29 | état de l'œil (D-04-02.c) : une valeur saisie par ± ou au clavier ne fait **pas** passer l'œil à « Renseigné » (aucun couplage automatique inventé) | à trancher avec D-04-02.c lors de l'examen métier ; *20/10/2026 :* **clos — sans objet** (D-04-16 : plus d'état de l'œil) |

## Points ouverts ajoutés — 20/10/2026 (simplification, D-04-16)

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-30 | D-04-02.b : la recommandation **B** (« les deux yeux renseignés ou explicitement marqués », chaque œil ayant un état explicite) reposait sur l'état par œil retiré par D-04-16 | critère clinique **non modifié** ici (consigne 8) : à reformuler par le propriétaire avec l'opticien à G04-METIER ; en attendant, le double du banc n'applique **aucun** critère sur les valeurs OD / OG (règle de démonstration réduite à la provenance D-04-03.a/b) |
| O-04-31 | captures du 20/10/2026 prises sur le serveur de banc **déjà actif** sur 127.0.0.1:1520 (`node e2e/bench-04a/serve.mjs`, PID 33856, lancé à 14:09, pas par cette session ; un navigateur Chrome du poste y était connecté) | serveur utilisé en lecture seule et **laissé actif** ; son rechargement à chaud a pu actualiser une page ouverte du banc ; aucun autre processus touché |
| O-04-32 | motif d'invalidation « Document non recevable » (D-04-04.c) conservé : il qualifie l'ordonnance reçue elle-même, pas la gestion d'une pièce | à confirmer par le propriétaire avec D-04-04 |
| O-04-33 | preuves antérieures (captures 04-A du 19/10/2026 et de la refonte du 20/10/2026, rapports JSON) montrant l'état de l'œil et les pièces | conservées **telles quelles** comme historique ; état courant : captures `evidence/P04A_SIMPL_*` |

## Points ouverts ajoutés — 20/10/2026 (clôture 04-A, D-04-17)

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-34 | « Enregistrer » enchaîne deux opérations du contrat proposé (`PUT …/draft` puis `POST …/confirm`) : un refus de la confirmation laisse un brouillon enregistré (annoncé comme tel, sans doublon à la reprise) | 04-B : décider si l'API offre une opération atomique « enregistrer et confirmer » ou conserve les deux appels (le prototype fonctionne dans les deux cas) ; à trancher avec le contrat définitif, pas en 04-A |
| O-04-35 | le récapitulatif « Confirmer la transcription ? » (mention « ne signifie pas validé médicalement ») n'apparaît plus sur le parcours principal du formulaire ; il reste sur le détail pour confirmer un brouillon existant | conforme à D-04-17 (« saisir, Enregistrer, poursuivre ») ; à revoir seulement si G04-METIER (D-04-02.d) exige une relecture explicite avant confirmation |
| O-04-36 | la règle de démonstration du double sur la provenance (D-04-03.a/b : prescripteur et date, ou date et collaborateur) n'est plus appliquée ; seule reste « aucune valeur optique → confirmation refusée » | D-04-03.a/b et D-04-02.b restent PROPOSED ; les critères définitifs seront fixés à G04-METIER puis codés côté serveur en 04-B |
| O-04-37 | captures de la clôture prises sur le serveur de banc **déjà actif** sur 127.0.0.1:1520 (`node e2e/bench-04a/serve.mjs`, PID 27732, lancé à 15:11, pas par cette session) | serveur utilisé en lecture seule et **laissé actif** ; aucun autre processus touché |

## Amendement daté — 27/10/2026 (GO d'implémentation 04-B, livraison `PHASE04_B_READY — OWNER_SQL_REVIEW_REQUIRED`)

Les sections précédentes sont conservées comme historique.

### Gates

| Gate | État au 27/10/2026 |
|---|---|
| G04-METIER | **franchie par le mandat du propriétaire** pour les décisions produit et UX (orientations listées au mandat § 1) ; **validation professionnelle (opticien) OUVERTE** pour les règles cliniques ([`LIVRAISON_04B.md`](LIVRAISON_04B.md) § 2, O-04-38) — ni certification médicale, ni autorisation d'exploitation sur données réelles |
| G04-SQL | **OPEN — `OWNER_SQL_REVIEW_REQUIRED`** : `docs/phase/04/sql/0006_OpticalModule.idempotent.sql`, SHA-256 `927ed7ccb975b15eed18f6b2bcee41faf08ecb23c1c4f9f1f80a2efa1706c013` ; dossier [`G04_SQL_DOSSIER.md`](G04_SQL_DOSSIER.md) ; **non appliqué** |
| G04-LEGAL | inchangée : **OPEN**, bloque tout pilote sur données réelles |
| G04-FINAL | NOT_STARTED ; `PHASE04_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` proposable seulement après migration et tests réels (PostgreSQL, HTTP, Tauri) attestés ; clôture commerciale / pilote distincte |

### Décisions — état au 27/10/2026

| ID | État |
|---|---|
| D-04-02 | **APPROVED (orientation produit, mandat 27/10/2026)** : un œil suffit, l'autre reste vide ; sphère unique à zéro seule insuffisante ; acuité seule suffisante pour une mesure, insuffisante pour une ordonnance ; confirmation explicite (« Enregistrer ») — **validation opticien ouverte** (O-04-38) |
| D-04-03 | a, b **APPROVED** : nom et date de prescription, date et collaborateur ayant mesuré exigés pour confirmer ; qualité et catégorie libres, facultatives ; date de réception distincte ; auteur rempli par le serveur ; c reportée (BL-04-01) ; d inchangée (aucun lien mesure ↔ ordonnance) |
| D-04-04 | **APPROVED** : révisions confirmées immuables, rectification en brouillon distinct avec motif, abandon d'une rectification, invalidation motivée sans suppression ; listes de motifs du prototype reprises (O-04-32 inchangé) |
| D-04-05 | **APPROVED** : 4 permissions explicites (`optical.read`, `optical.prescriptions.write`, `optical.measurements.write`, `optical.invalidate`) combinées avec `clients.read` ; aucune via `system.admin` ; historique optique = `audit.read` ET `optical.read` ; aucune attribution aux comptes réels |
| D-04-06 | **APPROVED partiellement** : pas de 0,25 pour Sphère, Cylindre, Addition (boutons ±) ; saisie manuelle jamais arrondie ; cylindre non nul → axe requis ; prisme non nul → base requise ; axe 0° / 180° conservé, aucune conversion silencieuse ; **pas des prismes, bornes, conventions de base et composantes multiples : NON ARRÊTÉS** (avis opticien) |
| D-04-07 | **APPROVED** : client archivé consultable par les personnes autorisées ; écriture refusée ; invalidation autorisée |
| D-04-11 | **APPROVED** (cycle E-02 : révision confirmée et rectification en brouillon coexistantes ; numéro attribué à la confirmation) |
| D-04-17 | **précisée** : « Enregistrer » atomique côté API (création ou modification + confirmation en une transaction ; refus = rien d'enregistré) ; O-04-34 **résolu** |
| D-04-08, D-04-09, D-04-12.c, D-04-13 | inchangées (PROPOSED) ; D-04-09 / D-04-12.c relèvent de G04-LEGAL |

### Points ouverts ajoutés — 27/10/2026 (04-B)

| Réf. | Sujet | Proposition / échéance |
|---|---|---|
| O-04-38 | règles cliniques codées côté serveur (`OpticalConfirmationRules`) = orientations produit ; cas « deux sphères à zéro sans autre valeur » **accepté** (lecture littérale « une seule Sphère ») | validation par un opticien avant tout usage réel ; toute évolution = code serveur + tests, sans migration |
| O-04-39 | Prisme : boutons ± **retirés** (pas non arrêté, mandat § 1.2) ; saisie libre conservée | rétablir les ± quand le pas des prismes sera arrêté |
| O-04-40 | création confirmée réémise avec une saisie **modifiée** après une issue incertaine : l'enregistrement déjà confirmé est renvoyé tel qu'enregistré (aucun doublon) | l'utilisateur vérifie sur le détail et rectifie ; à revoir si constaté en recette |
| O-04-41 | listes : projection légère des en-têtes et révisions puis filtre / tri / pagination dans le service | mesurer en pilote ; colonnes de synthèse ou requête SQL paginée si nécessaire (sans changement de contrat) |
| O-04-42 | pré-essais 04-B sur un cluster PostgreSQL 18.6 **jetable** (initdb de l'agent, `localhost:55433`, authentification locale `trust`, données fictives, supprimé en fin de session) et API locale `https://localhost:7567` (environnement `Scratch`, sans user-secrets) | transparence : aucune base ni serveur du propriétaire touché ; ne remplace pas l'étape B |
| O-04-43 | « historique » interprété comme : révisions antérieures / abandonnées et journal de gestion, servis avec `audit.read` + `optical.read` ; la dernière révision confirmée et le brouillon ouvert restent lisibles avec `optical.read` (nécessaires au dossier) | à confirmer par le propriétaire |
| O-04-44 | nom du collaborateur ayant mesuré affiché d'après la liste des comptes habilités : un compte qui perd `optical.measurements.write` n'apparaît plus par son nom sur une mesure ancienne | servir le nom affiché avec la révision si constaté gênant |
| O-04-45 | onglet « Historique » de la fiche : pas d'option de filtre « Dossier optique » ajoutée (les événements optiques apparaissent sous « Toutes les actions » quand les droits le permettent) | à décider en 04-C (aucune refonte) |
| O-04-46 | `scripts/Invoke-MmvSqlScript.ps1` modifié : `lock_timeout` (défaut 5s) transmis par `PGOPTIONS`, paramètre `-LockTimeout` ; test 16 / 16 avec psql simulé | revue du propriétaire avec le dossier G04-SQL |
| O-04-47 | `MMV.PostgreSql.Tests` attend désormais 6 migrations : `TestDatabase_AllMigrationsApplied` / `DevDatabase_AllMigrationsApplied` échouent **par construction** tant que 0006 n'est pas appliquée ; tests `OpticalWrite` « Skipped » (NOT_TESTED) | lever à l'étape B |

### Journal

- 27/10/2026 : GO d'implémentation reçu ; préflight (HEAD `3bb25a6`, ascendance de `6924860` confirmée, fichiers non suivis du propriétaire intacts) ; 04-B réalisé ; SQL 0006 généré, régénéré octet pour octet identique, pré-essayé sur cluster jetable ; **STOP G04-SQL**.

## Amendement daté — 28/10/2026 (G04-SQL accordée, 04-C, livraison `PHASE04_C_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`)

Les sections précédentes sont conservées comme historique. Mandat : [`prompts/MMV_V2_G04_SQL_GO_PHASE04_C_2026-10-28.md`](../../../prompts/MMV_V2_G04_SQL_GO_PHASE04_C_2026-10-28.md). Rapport : [`LIVRAISON_04C.md`](LIVRAISON_04C.md).

### Gates

| Gate | État au 28/10/2026 |
|---|---|
| G04-SQL | **APPROVED — GO conditionnel du propriétaire (28/10/2026)** pour l'application locale du seul script `0006_OpticalModule.idempotent.sql` (`927ed7cc…706c013`). Conditions remplies (précontrôles conformes, sauvegarde vérifiée de dev) ; **appliqué** : `mmv_v2_dev` 22:47:28 puis `mmv_v2_test` 22:48:28 (+01:00), COMMIT, `verify.sql` 18 / 18, inventaire exact — preuves `evidence/P04C_*` |
| G04-METIER | inchangée : franchie pour le produit et l'UX ; **validation professionnelle (opticien) OUVERTE** |
| G04-LEGAL | inchangée : **OPEN** — module non activé sur données médicales réelles |
| G04-FINAL | **OWNER_FINAL_REVIEW_REQUIRED** : 04-C livré ; clôture globale de la Phase 04 soumise à l'audit final du propriétaire ; aucun push, PR ni merge |

### Points ouverts — mise à jour du 28/10/2026 (04-C)

| Réf. | Constat | Suite |
|---|---|---|
| O-04-44 | **RÉSOLU (28/10/2026)** par O-04-48 | — |
| O-04-47 | **LEVÉ (28/10/2026)** : 0006 appliquée ; `AllMigrationsApplied` ×2 et `OpticalWrite` ×5 **PASS** sur PostgreSQL 18.6 | — |
| O-04-48 | **CORRIGÉ** : nom du collaborateur ayant mesuré résolu seulement par la liste active `/measurers` → « Non renseigné » après désactivation, comparaison vide, sélection perdue en rectification. Champ additif `citedUsers` de la réponse d'enregistrement + `knownActors` côté poste ; contrat OpenAPI mis à jour (ajout seul) | revue du propriétaire |
| O-04-49 | double création réémise simultanée : réponse correcte (201 + 200, un enregistrement) mais 23505 `ux_*_creation_request_id` journalisé `fail` par EF Core (paramètres et détail masqués) | filtrage du journal ou insertion conditionnelle, à décider |
| O-04-50 | **CORRIGÉ (tests seulement)** : `SchemaStateTests.AllMigrationsApplied` et `AuditPostgreSqlTests.AuditJournalScript_ProducesExactlyTheExpectedSchema` comparaient l'inventaire 0005 → FAIL sur base correctement migrée ; assertion sur `OpticalModuleDifferences` | — |
| O-04-51 | `vitest` en parallèle des tests .NET : 4 délais de 5 s dépassés (Shell, Clients, Historique, activation) ; seul : 100 % | relancer seul ; délais à revoir si récurrent |
| O-04-52 | libellé du formulaire « pas de démonstration 0,25 (paramétrable, D-04-06 ouverte) » et étiquette « Prototype · D-04-06 PROPOSED » alors que le mandat 04-B fixe le pas 0,25 (Sphère, Cylindre, Addition) | libellé à arbitrer par le propriétaire (non modifié) |
| O-04-53 | archivage ‖ écriture optique (10 paires) : jamais de 500 ; une écriture commencée avant l'archivage peut être validée après (9 révisions horodatées après `archived_at_utc`) — ordre sérialisable écriture → archivage | **réserve BL-04-07** (verrou du client à envisager) |
| O-04-54 | désactivation du mesureur ‖ confirmation (10 paires décalées) : 1 × 422 `not_eligible`, 9 × 201 (contrôle avant désactivation) — ordre sérialisable | **réserve BL-04-07** |
| O-04-55 | HTTP en clair : toute route `/api` refusée 403 `https_required` (conception phase 03) ; matrice complète en HTTPS, HTTP limité au contrôle de refus | — |

### Journal

- 28/10/2026 : GO G04-SQL reçu ; précontrôles conformes ; sauvegarde `mmv_v2_dev_avant_0006_20261028_224659.dump` (SHA-256 `5FF611B3…1066`, hors dépôt) ; 0006 appliquée dev puis test ; tests réels PostgreSQL, HTTPS, Tauri natif ; O-04-48 et O-04-50 corrigés ; **STOP audit final du propriétaire**. Constat : `MMV.Api` et `tauri dev` du propriétaire démarrés après l'application (écritures `clients` sur dev à 23:14, non issues de l'agent).

## 29/10/2026 — derniers correctifs avant clôture (G04-FINAL)

Mandat : [`prompts/MMV_V2_PHASE04_DERNIERS_CORRECTIFS_2026-10-29.md`](../../../prompts/MMV_V2_PHASE04_DERNIERS_CORRECTIFS_2026-10-29.md) — 04-C acceptée techniquement ; trois correctifs ciblés. Rapport : [`LIVRAISON_04_FINAL.md`](LIVRAISON_04_FINAL.md). Les lignes du 28/10/2026 ci-dessus sont conservées telles quelles.

| Gate | État au 29/10/2026 |
|---|---|
| G04-SQL | inchangée (APPROVED, 28/10/2026) ; **aucune nouvelle migration ni modification de schéma** : SQL 0006 inchangé, manifeste 11 / 11 OK |
| G04-METIER | inchangée : validation professionnelle (opticien) **OUVERTE** |
| G04-LEGAL | inchangée : **OPEN** — module non activé sur données médicales réelles |
| G04-FINAL | **OWNER_FINAL_REVIEW_REQUIRED** : correctifs O-04-52 à O-04-54 réalisés ; statut proposé `PHASE04_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` ; aucun push, PR ni merge ; Phase 05 non commencée |

| Réf. | Mise à jour du 29/10/2026 | Suite |
|---|---|---|
| O-04-52 | **CORRIGÉ** : « Boutons ± : pas de 0,25 (Sphère, Cylindre, Addition) · ↑ / ↓ au clavier · saisie libre conservée telle quelle · Prisme en saisie libre » ; étiquettes « Prototype · D-04-06 PROPOSED » et « Prototype · D-04-03.a/b PROPOSED » retirées du formulaire réel ; liste de bases toujours « non approuvée » (D-04-06 partielle) ; design Lot 3 conservé | revue du propriétaire |
| O-04-53 | **CORRIGÉ** : contrôle du client sous verrou `FOR SHARE` dans la transaction d'enregistrement optique (toutes écritures sauf invalidation) ; archivage / réactivation verrouillent le client (`FOR NO KEY UPDATE`) avant lecture et date ; barrières PostgreSQL : 0 / 12 avant, 12 / 12 après ; HTTPS 9.2 : 9 → 0 | revue du propriétaire |
| O-04-54 | **CORRIGÉ** : compte et droit `optical.measurements.write` du collaborateur nouvellement désigné verrouillés `FOR SHARE` dans la même transaction ; désactivation et retrait du droit, deux ordres chacun : 0 / 4 avant, 4 / 4 après ; collaborateur historique conservé sans contrôle (règle inchangée) | revue du propriétaire |
| O-04-56 | rapport d'audit `MMV_V2_AUDIT_FINAL_PHASE04_C_2026-10-28.md` cité « joint » par le mandat, absent de l'espace de travail | fournir le rapport si des points s'ajoutent |
| O-04-57 | brouillon de mesure désignant un collaborateur alors éligible, confirmé après sa désactivation sans changer la provenance : accepté (D-04-03.b « au moment où il est désigné », non rouvert) | à confirmer par le propriétaire |
| O-04-58 | verrou du client : modification de fiche ou archivage attend une écriture optique en cours sur ce client (transaction courte), sans `lock_timeout` applicatif ; interblocage avec une écriture SQL extérieure → `409`, chemin **NOT_TESTED** | à observer en pilote |
| O-04-59 | désactivation et retrait de droits : par SQL seulement en Phase 04 ; couverts par le verrou (`UPDATE auth.users`, `DELETE auth.user_permissions`) | administration des comptes : phase ultérieure |

### Journal (29/10/2026)

- 29/10/2026 : mandat des derniers correctifs reçu ; tests de concurrence à barrières écrits puis exécutés **sur le code 04-C** (0 / 16, défauts reproduits) ; correctifs O-04-53 / O-04-54 / O-04-52 ; 16 / 16, .NET, PostgreSQL, frontend, HTTPS, HTTP, Tauri natif rejoués ; données de test purgées (résidu 0) ; processus du propriétaire (7267, 1420) non touchés ; **STOP audit final du propriétaire**.
