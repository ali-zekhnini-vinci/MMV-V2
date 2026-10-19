# 04-A — Livraison : interfaces du dossier optique (prototype sur banc isolé)

**Statut (19/10/2026) : `PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md) — **G04-A APPROVED — UI ONLY**, **O-04-16 APPROVED**. D-04-01 APPROVED ; **D-04-02 à D-04-14 restent `PROPOSED`** (aucune n'est tranchée par ce lot). Branche locale `feat/phase-04-00-cadrage` ; commits locaux seulement (aucun push, PR ni merge).

## 1. Préflight

| Contrôle | Résultat |
|---|---|
| HEAD local au départ | `1d18b81` (dernier rapport), branche `feat/phase-04-00-cadrage` |
| ascendance | `origin/main` (`6924860`) ancêtre de HEAD ; 2 commits d'avance (`44efb14`, `1d18b81`), aucun de retard |
| modifications non autorisées | aucune modification suivie ; non suivis du propriétaire présents et **intacts** (zips de phase, `.claude*`, `.serena/`, `apps/desktop/desktop.zip`, `apps/desktop/desktop.recovered.zip`, pack de `docs/phase/04/`, `references/`) ; aucune transformation globale de texte |
| lecture | `docs/phase/04/` intégralement (y compris le pack non suivi) ; maquette Lot 3 `references/phase04/lot3/MMV-Lot3-Ordonnances.dc.html`, SHA-256 revérifié `c103df9e…c06c213` ; notes de conception de l'ancien projet **ignorées** |
| ancien dépôt MMV, PostgreSQL 17 / 18, `mmv_v2_dev` / `mmv_v2_test` | **non consultés** ; aucune commande `psql`, `dotnet ef`, aucune connexion |
| référence de non-régression avant code | `npm run check` PASS, lint 0 erreur (1 avertissement connu O-03-02), Vitest **226 / 226** PASS (20 fichiers) |

## 2. Isolement (O-04-16 APPROVED)

| Garantie | Mise en œuvre | Preuve |
|---|---|---|
| comptes réels inchangés | l'application ne monte **aucun** écran optique : fiche avec l'onglet reporté « Ordonnances — phase ultérieure », menu « Ordonnances » `later`, aucune route `/optique` (redirection vers l'Accueil) — **même** pour une session qui porterait les noms de permissions proposés | `src/optical/realAppIsolation.test.tsx` (application réelle + double `node:http` des Phases 02/03) |
| prototypes dans un banc isolé, étiqueté | page distincte `apps/desktop/optical-bench.html` → `src/bench/main.tsx` ; bannière permanente « BANC DE DÉMONSTRATION ET DE TEST 04-A — PROTOTYPE », données fictives, droits simulés, décisions PROPOSED | `src/bench/OpticalBench.test.tsx` (isolement) ; toutes les captures |
| banc absent de l'application livrée | `vite build` ne construit que `index.html` ; aucun module de l'application n'importe `optical/` ni `bench/` | test d'imports + recherche dans le bundle de production : aucune occurrence de `BANC DE D`, `optical-bench`, `RX-FICTIF`, `Fictif-`, `InMemoryOpticalGateway`, `/optique` ([`evidence/P04A_bundle_20261019.log`](evidence/P04A_bundle_20261019.log)) ; seul le libellé « Dossier optique » de la branche facultative inutilisée de `ClientDetailView` y figure (O-04-24) |
| aucune authentification contournée | le banc n'ouvre **aucune** session MMV.Api et ne contacte aucun serveur : `fetch` est remplacé avant le premier rendu ; seule la recherche de clients du `ClientPicker` réutilisé est servie **en mémoire** (clients fictifs, origine réservée `.invalid`), tout autre appel est refusé sans émission et compté | chaque test de parcours vérifie `blocked = []` ; compteur affiché dans le banc |
| aucune API ni permission optique | aucune route `/api/optical`, aucun ajout à `Permissions.cs` ni à la contrainte SQL ; les noms `optical.*` n'existent que dans le prototype (affichage) et le double en mémoire | `git diff --stat` : aucun fichier de `src/` (.NET) ni SQL modifié |
| aucune donnée de santé réelle | identités `Fictif-…`, domaines `exemple.test`, prescripteurs `Dr Fictif-Prescripteur …`, collaborateurs `Fictif-…`, valeurs de démonstration ; pièces **synthétiques** dessinées localement (mention « Aucune donnée réelle ») ; aucun fichier du poste n'est lu | `src/bench/benchSeed.ts`, `benchImages.ts` ; captures |

## 3. Fichiers livrés

| Fichier | Rôle |
|---|---|
| `apps/desktop/src/optical/opticalContract.ts` | contrat **provisoire** (types seulement, aucun HTTP) : deux types distincts (union discriminée `prescription` / `measurement`), révisions `draft` / `confirmed` / `discarded`, pièces, interface `OpticalGateway` |
| `apps/desktop/src/optical/opticalLabels.ts`, `opticalUi.ts`, `opticalAccess.ts`, `opticalUiState.ts`, `opticalQueries.ts`, `opticalRevisions.ts` | libellés (vocabulaire D-04-01), chemins et contexte de retour validé, messages bornés, droits simulés (affichage), état de liste, TanStack Query, comparaison |
| `apps/desktop/src/optical/OpticalModule.tsx`, `OpticalListView.tsx`, `ClientOpticalTab.tsx`, `OpticalTypeChooser.tsx`, `OpticalRecordForm.tsx`, `EyeGrid.tsx`, `OpticalRecordDetail.tsx`, `RevisionsPanel.tsx`, `SourceDocumentsPanel.tsx`, `OpticalDialogs.tsx`, `OpticalBadges.tsx`, `OpticalRecordsTable.tsx`, `Optical.css` | écrans du prototype |
| `apps/desktop/optical-bench.html`, `apps/desktop/src/bench/*` | banc isolé : hôte, double en mémoire, jeu fictif, profils simulés, garde réseau, aperçus synthétiques, styles |
| `apps/desktop/src/clients/ClientDetailView.tsx` | **seule modification de code existant** : props facultatives `opticalTab` / `initialTab` (sans elles, rendu inchangé) |
| `apps/desktop/e2e/bench-04a/serve.mjs`, `plan.mjs`, `shoot.mjs` | serveur Vite isolé (port 1520, cache hors dépôt), plan et prise des captures (Edge sans interface, CDP) |
| tests : `src/bench/inMemoryOpticalGateway.test.ts`, `src/bench/OpticalBench.test.tsx`, `src/optical/opticalUi.test.ts`, `src/optical/realAppIsolation.test.tsx` | double, parcours, outils, isolement |

## 4. Parcours démontrable (sans accès à `mmv_v2_dev`)

1. `cd apps/desktop && node e2e/bench-04a/serve.mjs` (n'utilise ni le port 1420 ni le cache de `tauri dev`).
2. Ouvrir `http://127.0.0.1:1520/optical-bench.html` (paramètres facultatifs : `route=/clients/cli-fictif-1`, `profile=reader|none|…`, `theme=dark`, `latency=800`).
3. L'accueil du banc propose les scénarios : fiche → onglet « Dossier optique » (J-04-01), fiche vide, client archivé, liste globale (J-04-02), RX-FICTIF-0001 (révisions 1 et 2, pièces par révision), RX-FICTIF-0002 (rectification en cours + pièce invalidée), RX-FICTIF-0003 (invalidée), RX-FICTIF-0004 (brouillon partiel, refus 422), MES-FICTIF-0001 (mesure magasin), saisie forcée sur client archivé (refus 409).
4. Commandes du banc : profil simulé, latence, échec de la prochaine liste / écriture, « Autre poste : modifier l'enregistrement ouvert » (409), thème, réinitialisation du jeu fictif.

## 5. Ce que le prototype montre — et ne décide pas

- **Deux types distincts** partout (badge sur chaque ligne, détail, formulaire ; sections séparées ; provenance propre : prescripteur et dates de prescription / réception pour l'ordonnance ; date, collaborateur, catégorie pour la mesure) ; aucune conversion, le double refuse un contenu d'un autre type.
- **Grille OD / OG neutre** : valeurs conservées à l'identique (signe, virgule ou point, décimales, `0` et `180`), aucun pas (flèches sans effet), aucune borne, aucun champ désactivé ni requis, unités affichées à côté ; vide = « non renseigné », jamais `0,00` ; état par œil explicite (proposition D-04-02.c) ; seules des erreurs de **forme** (nombre attendu, longueur) renvoyées par le double.
- **Cycle E-02 (PROPOSED)** : brouillon → confirmation (critère décidé par le service : refus `422 incomplete` affiché tel quel, la règle de démonstration du double reprend les recommandations D-04-02.b / D-04-03, jamais codée dans le poste) → rectification motivée en brouillon, **la dernière révision confirmée reste affichée** ; un seul brouillon (`409 draft_exists`) ; numéro attribué à la confirmation ; abandon conservé ; invalidation motivée (brouillon abandonné dans la même opération), jamais de suppression.
- **Pièces (E-01, E-03, E-05 — PROPOSED)** : stockées une fois, citées par révision ; pièce invalidée barrée et libellée, avertissement avant transmission ; « Afficher la pièce » = action explicite, annoncée « contenu transmis au poste — cela n'atteste pas qu'une personne l'a vu » ; refus 413 / 415 / doublon ; sans `optical.documents.read` : nombre seulement.
- **Non décidé / non construit** : bornes, pas, dépendances, axe à 0°, base du prisme, critère de complétude, listes de qualités du prescripteur et de catégories de mesure (« Non précisée » seulement), validité d'une ordonnance, prise en charge, sélecteur de fichier réel, aperçu PDF, CSP, rôle SQL, sauvegardes.

## 6. Points de conception à conserver pour 04-B (D-04-10 à D-04-14, PROPOSED)

| Point | Où le prototype le rend visible | À prouver en 04-B / 04-C |
|---|---|---|
| D-04-10 — pièce stockée une fois, liée à chaque révision confirmée (lien figé à la confirmation) | panneau « Pièces source » : « Citées par la version affichée » / « Autres pièces du dossier » ; comparaison « Pièces liées » | table de liens, contenu immuable + SHA-256, doublon refusé, Q-04-121 |
| D-04-11 — dernière révision confirmée distincte du brouillon de rectification | bandeau GAP-04-13, « Révision n — en vigueur », brouillon consultable séparément | pointeurs `last_confirmed` / `open_draft`, unicité du brouillon, Q-04-122 à Q-04-127 |
| D-04-12 — `document.content_served` ≠ « vu » : demande autorisée, transmission commencée, réception **non prouvée** | libellés « transmis » du panneau et du journal ; aucun préchargement ni vignette | événement validé avant le premier octet, Q-04-133 ; aucune promesse anti-copie |
| D-04-13 — permissions évaluées selon les profils réels, sans accès universel | profils **simulés** du banc (lecture seule, sans pièces, sans droit) | matrice validée compte par compte ; 403 indiscernables (Q-04-106) |
| D-04-14 — les signatures de fichiers ne suffisent pas : analyse, parseurs, CSP, antivirus avant pilote ; flux multipart sans fichier temporaire involontaire | refus simulés 413 / 415 / doublon seulement (pièces synthétiques) | contrôles serveur réels (Q-04-143, Q-04-144), AMSI (O-04-17), CSP (O-04-01), envoi en flux (O-04-03) |
| sauvegarde / restauration éprouvée avant pilote | — | Q-04-145, O-04-09, O-04-19 ; **aucune** promesse de conformité juridique ou de sécurité non vérifiée |

## 7. Qualité et preuves

Voir [`QA_MATRIX.md`](QA_MATRIX.md) § 04-A (Q-04-019 à Q-04-030) et les journaux [`evidence/`](evidence/).

| Contrôle | Statut |
|---|---|
| typage `npm run check` | PASS |
| lint `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02, `ClientsTable.tsx`) |
| tests frontend Vitest (toutes suites) | **PASS au rejeu : 265 / 265** (24 fichiers ; 226 existants + 39 nouveaux). Exécution précédente de la même chaîne : 264 / 265 — **1 échec intermittent** d'un test Phase 03 non modifié (`ClientsModule.test.tsx`, O-03-43, délai de `findByRole` dépassé sous charge) ; rejoué seul 3 × 24 / 24 PASS puis suite complète PASS (O-04-26) |
| build `vite build` (sortie hors dépôt) | PASS ; contrôle du bundle ci-dessus |
| tests .NET, PostgreSQL réel | **NOT_RERUN** — aucun fichier .NET, SQL ni de configuration serveur modifié |
| captures **navigateur** (Edge sans interface, moteur de WebView2) | PASS — **54 captures** après le dernier changement de code, 0 erreur console, 0 défilement horizontal de page mesuré |
| interface **Tauri native** | **NOT_TESTED** — le banc n'est pas construit dans l'application Tauri (par conception, O-04-16) ; preuve native prévue en 04-C |

## 8. Captures (navigateur, pas natif)

`docs/phase/04/evidence/P04A_navigateur_*.jpg` : clair et sombre (accueil, fiche, liste, détail, rectification, formulaires des deux types), un état de chaque catégorie (vide, aucun résultat, chargement, erreur, accès refusé, lecture seule, archivé), révisions (comparaison, rectification en cours, révision 2 enregistrée), pièces (aperçu synthétique, format refusé, pièce invalidée), invalidation, conflit, garde de sortie, focus, fenêtre réduite 1024×700 (clair / sombre), 1920×1080, zooms 125 / 150 % (facteur d'échelle de l'appareil). Rapport machine : [`evidence/P04A_captures_rapport_20261019.json`](evidence/P04A_captures_rapport_20261019.json). Correspondance avec les états : [`STATE_COVERAGE_04A.md`](STATE_COVERAGE_04A.md).

## 9. Questions métier véritablement bloquantes pour 04-B (G04-METIER)

Examen consolidé à tenir **avec un opticien** sur le banc (une séance, formulaire de [`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md)) :

1. **D-04-06 [AVIS OPTICIEN]** — bornes et pas par grandeur, axe à 0° (tel que transcrit ?), axe requis / désactivé selon le cylindre, base du prisme (notation, une ou deux composantes), acuité conservée ou non, contrôles croisés : *à observer sur la grille neutre (états 17, 25, 33, 36)*.
2. **D-04-02.b / c [AVIS OPTICIEN]** — critère de confirmation, états par œil et leurs libellés (« Non prescrit sur le document », « Non transcrit », « Non mesuré ») : *brouillon partiel RX-FICTIF-0004 et refus 422*.
3. **D-04-03.a / b [AVIS OPTICIEN] [CONSEIL JURIDIQUE partiel]** — listes fermées « qualité du prescripteur » et « catégorie de mesure » (aujourd'hui « Non précisée » seulement), champs obligatoires à la confirmation.
4. **D-04-04 / D-04-11** — rectification en brouillon, numérotation à la confirmation, brouillon partagé, abandon conservé, invalidation avec brouillon ouvert : *RX-FICTIF-0002, RX-FICTIF-0006*.
5. **D-04-05 / D-04-13** — découpage des droits et matrice **par compte réel** ; résumé OD/OG dans les listes (D-04-13.b) : *bouton « Résumé OD/OG (D-04-13.b) »*.
6. **D-04-07, D-04-08** — client archivé (rectification refusée ?), liste globale activée en 04 et libellé « Dossiers optiques ».
7. **D-04-10.c, D-04-12, D-04-14** — contenu d'une pièce invalidée servi avec avertissement ou refusé ; journalisation des lectures (avant pilote, avec G04-LEGAL) ; limites (10 Mo, 10 pièces), marqueurs PDF actifs, antivirus, volumes réels (D-04-14.g).
8. **D-04-09 [CONSEIL JURIDIQUE]** — bloque un **pilote** sur données réelles, pas 04-B.

Ne bloquent pas 04-B : la validation visuelle des 50 états (O-04-25), le socle CSS (O-04-11), le sélecteur de fichier réel et la CSP (O-04-23, 04-C).

## 10. Limites et constats

- Prototype réversible : comportements de cycle, de révision, de pièces et de droits fondés sur des propositions ; aucun n'est une décision.
- Captures produites dans un **navigateur** ; aucune preuve native (WebView2 dans Tauri) — NOT_TESTED.
- Zoom simulé par le facteur d'échelle de l'appareil (équivalent CSS d'un zoom), pas par le zoom de WebView2.
- Constat O-04-21 : le premier lancement d'un serveur de banc a régénéré le cache de pré-bundle Vite partagé (`node_modules/.vite/deps`) à 19:52 ; arrêté aussitôt, relancé avec un cache hors dépôt ; aucun fichier source touché.
- Constat O-04-26 : un test existant de la Phase 03 (O-03-43) a échoué une fois sous charge (délai d'attente par défaut de Testing Library) puis passé à chaque rejeu ; aucun lien établi avec 04-A (la fiche réelle est rendue sans `opticalTab`) — à surveiller.
- Dans le banc, une panne simulée affiche le message technique générique du poste (« Serveur MMV injoignable… ») : c'est le message que verrait l'application réelle, la panne reste simulée.
- Défaut trouvé et corrigé pendant les tests : au changement de profil simulé, la première lecture partait encore avec les droits de l'ancien profil (noms de pièces visibles un instant pour un profil sans `optical.documents.read`) ; la référence du profil est désormais mise à jour avant le rendu ; test de régression ajouté.
