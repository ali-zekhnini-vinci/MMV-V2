# 04 — QA_MATRIX

Statuts : `PASS` (exécuté, preuve citée) ; `NOT_TESTED` (prévu, rien d'exécuté) ; `NOT_RERUN` (non rejoué depuis le dernier changement). Aucun test applicatif n'a été exécuté en 04-00 : seul le code de documentation a changé.

## 04-00 — contrôles exécutés le 19/10/2026

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-04-001 | `origin/main` à jour | `git fetch origin` : `cb79d1f..6924860 main` | PASS — `origin/main` = `6924860ee0fb77678cdd959a1de9b39735785fcc` (merge PR #4) |
| Q-04-002 | `93df475` inclus | `git merge-base --is-ancestor 93df475 origin/main` | PASS (et `abd0352` inclus) |
| Q-04-003 | merge sans écart | `git diff --stat abd0352 6924860` | PASS — vide (arbre identique à la tête de la branche Phase 03) |
| Q-04-004 | arbre de travail propre | `git status --short` | PASS — aucune modification suivie ; non suivis du propriétaire présents et intacts (zips de phase, `.claude*`, `.serena/`, `apps/desktop/desktop.zip`, `apps/desktop/desktop.recovered.zip`, `docs/phase/04/` pack, `references/`) |
| Q-04-005 | branche dédiée | `git fetch origin main:main` (avance rapide `c0c3d6b..6924860`) puis `git switch -c feat/phase-04-00-cadrage main` | PASS |
| Q-04-006 | maquette Lot 3 authentique | `sha256sum` sur `Downloads/MMV-Lot3-Ordonnances.dc.html`, sa copie `MMV initialisation projet V1/`, l'entrée du dossier maître | PASS — `c103df9e…c06c213` (attendu), 93 625 octets = registre |
| Q-04-007 | catalogue Lot 3 | `MMV_V2_Registre_324_Etats.csv` : 36 lignes `MMV-L03-*` ; `MMV_V2_Registre_134_Groupes.csv` : `B-033`…`B-049` | PASS — 36 états, 17 groupes ; `data-mount` de la maquette : 36 + `notes` |
| Q-04-008 | couverture | [`STATE_MAP_36.md`](STATE_MAP_36.md) | PASS — 36 / 36 cartographiés ; 12 lacunes identifiées (GAP-04-01 à 12) |
| Q-04-009 | codes d'audit proposés compatibles | lecture de `ck_events_action_format`, `ck_events_module_format`, `ck_events_entity_type_format` (`docs/phase/03/sql/0005_AuditJournal.sql`) | PASS — tous les codes de [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) § 4 respectent les formats |
| Q-04-010 | constat journaux techniques | recherche `UseHttpLogging`, `EnableSensitiveDataLogging`, journalisation de corps dans `src/` ; `appsettings.json` | PASS — aucun ; EF Core `Warning` |
| Q-04-011 | aucune modification de code, SQL ou base | `git diff --stat main` limité à `docs/`, `prompts/`, `README.md` | PASS (à la livraison) |
| Q-04-012 | liens internes des documents 04 | vérification de chaque chemin relatif cité | PASS (à la livraison, voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |
| Q-04-013 | aucune connexion PostgreSQL ni ancien dépôt | aucune commande `psql`, `dotnet test`, ni lecture du dépôt `mmv-desktop` | PASS |

Suites existantes (.NET, PostgreSQL réel, frontend, natif) : **`NOT_RERUN`** en 04-00 — aucun code modifié depuis `6924860` (dernière exécution : préparation du merge Phase 03, [`../03/QA_MATRIX.md`](../03/QA_MATRIX.md) Q-03-259 à Q-03-266).

## Tests prévus (tous `NOT_TESTED`)

### Accès et permissions (04-B / 04-C)

| ID | Cas | Attendu |
|---|---|---|
| Q-04-100 | anonyme sur chaque route `/api/optical/*` | 401 |
| Q-04-101 | `system.admin` seul ; `clients.*` seul ; `appointments.*` seul ; `audit.read` seul | 403 partout |
| Q-04-102 | `optical.read` sans `clients.read` | 403 |
| Q-04-103 | lecture avec `clients.read` + `optical.read` | 200 ; aucune écriture possible (403) |
| Q-04-104 | écriture RX avec `optical.measurements.write` seul, et inversement | 403 (D-04-01) |
| Q-04-105 | pièce sans `optical.documents.read` | 403 ; métadonnées sans nom de fichier |
| Q-04-106 | identifiant inexistant vs existant sans droit | réponses indiscernables |
| Q-04-107 | historique : `audit.read` sans `optical.read` | aucun événement `optical` servi |
| Q-04-108 | HTTP non chiffré | `https_required` |

### Cycle de vie, intégrité, concurrence

| ID | Cas | Attendu |
|---|---|---|
| Q-04-110 | brouillon incomplet | enregistré `draft` ; non proposable à la vente |
| Q-04-111 | confirmation sans le critère minimal | 422 `incomplete` |
| Q-04-112 | rectification d'un confirmé | révision n+1 ; révision n inchangée octet pour octet |
| Q-04-113 | deux postes, même version | un 200, un 409 ; un seul événement ; aucune perte |
| Q-04-114 | client archivé | création refusée (409) ; consultation permise |
| Q-04-115 | invalidation | conservée, exclue par défaut, visible avec filtre ; aucune route `DELETE` |
| Q-04-116 | valeurs | aucune normalisation ; signe et décimales préservés ; absence ≠ 0 |
| Q-04-117 | règles D-04-06 | une ligne de test par règle **ratifiée** (aucune avant ratification) |
| Q-04-118 | pièce : format par signature, taille, type déclaré trompeur | 415 / 413 ; aucun stockage partiel |
| Q-04-119 | pièce conservée lors d'une nouvelle révision | rattachement intact |

### Audit et confidentialité

| ID | Cas | Attendu |
|---|---|---|
| Q-04-130 | chaque écriture réussie | un événement dans la même transaction ; aucune valeur optique, aucun nom de prescripteur, aucune note dans `changes` (recherche de toutes les valeurs fictives saisies dans le JSON) |
| Q-04-131 | échec (validation, conflit, contrainte, panne) | ni écriture métier ni événement |
| Q-04-132 | `document.viewed` | écrit avant l'envoi ; échec d'audit ⇒ pièce non servie |
| Q-04-140 | journaux techniques pendant un parcours complet | aucune valeur saisie, aucun contenu de pièce, aucun jeton |
| Q-04-141 | réponses optiques | `Cache-Control: no-store` ; pièces `nosniff` |
| Q-04-142 | déconnexion | cache du poste vidé ; aucune donnée optique en stockage local |

### Interface et natif (04-A / 04-C)

| ID | Cas | Attendu |
|---|---|---|
| Q-04-150 | 36 états + 12 états GAP | conformes à [`STATE_MAP_36.md`](STATE_MAP_36.md) (double de test) |
| Q-04-151 | type toujours visible | badge sur ligne, détail, formulaire, historique |
| Q-04-152 | J-04-01 sans seconde recherche | retour sur l'onglet « Dossier optique » |
| Q-04-153 | clavier et focus | ordre de Tab, erreurs sous champ, Échap, aucune souris |
| Q-04-154 | fenêtre réduite | OD sur OG, aucun défilement horizontal |
| Q-04-155 | natif 2 thèmes × 1440×900 / 1920×1080 × 100 / 125 / 150 % | captures après le dernier changement, sinon `NOT_TESTED` |
| Q-04-156 | « Préparer une vente » | désactivée, aucun effet |
| Q-04-160 | non-régression Clients, Rendez-vous, Historique | suites existantes PASS |

## Complément 04-00 — contrôles exécutés le 19/10/2026 (suite de la revue)

| ID | Contrôle | Méthode | Résultat |
|---|---|---|---|
| Q-04-014 | aucun code, SQL ni base touchés | `git status --short` / `git diff --stat` : seuls `docs/`, `README.md` et `prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md` modifiés ; aucune commande `psql`, `dotnet`, `npm` | PASS |
| Q-04-015 | fichiers non suivis du propriétaire intacts | `git status --short` : mêmes non suivis qu'au départ (zips, `.claude*`, `.serena/`, `apps/desktop/desktop*.zip`, pack `docs/phase/04/`, `references/`) ; aucun écrit | PASS |
| Q-04-016 | liens internes des documents 04 et des documents modifiés | résolution de chaque chemin relatif Markdown | PASS (voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Journal) |
| Q-04-017 | statuts cohérents | D-04-01 APPROVED ; D-04-02 à D-04-14 PROPOSED ; aucun état GAP déclaré conçu ; 04-A non commencé dans tous les documents | PASS |
| Q-04-018 | composants cités par la préparation 04-A existants | lecture de `apps/desktop/src/` (`components/ui/*`, `clients/ClientStates.tsx`, `ClientNoticeBanner.tsx`, `ClientsTable.tsx`, `views/ForbiddenView.tsx`, `appointments/ClientPicker.tsx`, `appointmentLaunch.ts`, `shell/navigationGuard.ts`) | PASS |

Suites existantes : **`NOT_RERUN`** (aucun code modifié depuis `6924860`).

### Tests prévus ajoutés (tous `NOT_TESTED`, sous réserve de D-04-10 à D-04-14)

| ID | Cas | Attendu |
|---|---|---|
| Q-04-121 | pièce liée à la révision 1, verso ajouté au brouillon puis confirmé | révision 1 cite le recto seul, révision 2 recto + verso ; contenu stocké une fois |
| Q-04-122 | rectification ouverte | détail et listes servent la dernière révision confirmée ; bandeau « rectification en cours » |
| Q-04-123 | deuxième rectification simultanée | `409 draft_exists`, aucune écriture, aucun événement |
| Q-04-124 | confirmation d'un brouillon dont la base n'est plus la dernière confirmée | `409 revision_conflict` |
| Q-04-125 | abandon d'une rectification | `discarded` conservé, numéro non consommé, révision n inchangée |
| Q-04-126 | invalidation avec brouillon ouvert | brouillon abandonné dans la même transaction ; deux événements ou aucun |
| Q-04-127 | client archivé avec brouillon ouvert | modification et confirmation refusées, abandon permis |
| Q-04-133 | `document.content_served` | validé avant le premier octet ; aucun événement pour la liste des pièces, un refus 403 ou un préchargement ; libellé « transmis » |
| Q-04-143 | fichiers : signature ≠ extension, SVG / HTML renommés, PDF à marqueurs actifs, image hors dimensions, 11ᵉ pièce, doublon d'empreinte | 415 / 415 / selon D-04-14.c / 415 / `409 too_many_documents` / `409 duplicate_document` ; aucun stockage partiel, aucun fichier temporaire |
| Q-04-144 | `Content-Type` servi | type détecté par le serveur, `nosniff`, `no-store` |
| Q-04-145 | sauvegarde / restauration avec pièces synthétiques | restauration complète, empreintes identiques, durées et tailles consignées (04-C, O-04-19) |
| Q-04-157 | 04-A : aucun changement visible sans `optical.read` | onglet reporté et entrée `later` inchangés (O-04-16) |
| Q-04-158 | 04-A : grille neutre | valeur saisie restituée à l'identique ; aucune borne, aucun pas, aucun champ désactivé, aucune normalisation |
