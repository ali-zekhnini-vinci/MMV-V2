# 04-A — Livraison : interfaces du dossier optique (prototype sur banc isolé)

**Statut (20/10/2026, clôture technique) : `PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md) — **D-04-17 APPROVED** (enregistrement optique simplifié : « Enregistrer » principal, « Enregistrer comme brouillon » discret, cycle de révisions conservé). Clôture technique du lot frontend 04-A ; **ne vaut pas GO pour 04-B**. Rapport : § 13.

*Historique —* **Statut (20/10/2026, simplification) : `PHASE04_A_SIMPLIFICATION_READY — OWNER_VISUAL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md) — **D-04-16 APPROVED** : champ « État de l'œil » supprimé, gestion des pièces sources reportée au backlog permanent (BL-04-01), révisions, comparaison, motif et auteurs conservés. Toujours **uniquement** dans le banc isolé ; aucune règle clinique ouverte modifiée. Détail : **§ 12**.

*Historique —* **Statut (20/10/2026, refonte UX Lot 3) : `PHASE04_A_UX_LOT3_READY — OWNER_VISUAL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_UX_LOT3_GO_CLAUDE_CODE.md) — **D-04-15 APPROVED** (référence UX officielle des formulaires optiques : maquette Claude Design Lot 3, contrôles rapides ± et saisie clavier complémentaire). Formulaires « Ordonnance médicale » et « Mesure magasin » refondus selon la maquette, toujours **uniquement** dans le banc isolé ; D-04-02 à D-04-14 restent `PROPOSED` (le pas des boutons est un paramètre de démonstration, D-04-06 ouverte). Détail : **§ 11**.

*Historique —* **Statut (19/10/2026) : `PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE04_A_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_GO_CLAUDE_CODE.md) — **G04-A APPROVED — UI ONLY**, **O-04-16 APPROVED**. D-04-01 APPROVED ; **D-04-02 à D-04-14 restent `PROPOSED`** (aucune n'est tranchée par ce lot). Branche locale `feat/phase-04-00-cadrage` ; commits locaux seulement (aucun push, PR ni merge).

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
| aucune donnée de santé réelle | identités `Fictif-…`, domaines `exemple.test`, prescripteurs `Dr Fictif-Prescripteur …`, collaborateurs `Fictif-…`, valeurs de démonstration ; pièces **synthétiques** dessinées localement (mention « Aucune donnée réelle ») ; aucun fichier du poste n'est lu | `src/bench/benchSeed.ts`, `benchImages.ts` *(supprimé le 20/10/2026, D-04-16)* ; captures |

## 3. Fichiers livrés

| Fichier | Rôle |
|---|---|
| `apps/desktop/src/optical/opticalContract.ts` | contrat **provisoire** (types seulement, aucun HTTP) : deux types distincts (union discriminée `prescription` / `measurement`), révisions `draft` / `confirmed` / `discarded`, pièces, interface `OpticalGateway` |
| `apps/desktop/src/optical/opticalLabels.ts`, `opticalUi.ts`, `opticalAccess.ts`, `opticalUiState.ts`, `opticalQueries.ts`, `opticalRevisions.ts` | libellés (vocabulaire D-04-01), chemins et contexte de retour validé, messages bornés, droits simulés (affichage), état de liste, TanStack Query, comparaison |
| `apps/desktop/src/optical/OpticalModule.tsx`, `OpticalListView.tsx`, `ClientOpticalTab.tsx`, `OpticalTypeChooser.tsx`, `OpticalRecordForm.tsx`, `EyeGrid.tsx`, `OpticalRecordDetail.tsx`, `RevisionsPanel.tsx`, `SourceDocumentsPanel.tsx` *(supprimé le 20/10/2026, D-04-16)*, `OpticalDialogs.tsx`, `OpticalBadges.tsx`, `OpticalRecordsTable.tsx`, `Optical.css` | écrans du prototype |
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
- *Amendement 20/10/2026 (D-04-15) : la grille est désormais présentée selon la maquette (cartes OD / OG, boutons ±) ; les garanties ci-dessous restent vraies pour la saisie au clavier ; seul un clic ± (ou ↑ / ↓) produit une valeur, avec un pas de démonstration paramétrable — voir § 11.*
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
- Constat O-04-21 : le premier lancement d'un serveur de banc a régénéré le cache de pré-bundle Vite partagé (`node_modules/.vite/deps`) à 19:52 ; le banc a ensuite été servi par un serveur isolé (cache hors dépôt) ; ce premier serveur, resté actif malgré l'arrêt de sa tâche, a été arrêté en fin de session ; aucun fichier source touché, serveurs du propriétaire (1420, 7267) jamais interrompus.
- Constat O-04-26 : un test existant de la Phase 03 (O-03-43) a échoué une fois sous charge (délai d'attente par défaut de Testing Library) puis passé à chaque rejeu ; aucun lien établi avec 04-A (la fiche réelle est rendue sans `opticalTab`) — à surveiller.
- Dans le banc, une panne simulée affiche le message technique générique du poste (« Serveur MMV injoignable… ») : c'est le message que verrait l'application réelle, la panne reste simulée.
- Défaut trouvé et corrigé pendant les tests : au changement de profil simulé, la première lecture partait encore avec les droits de l'ancien profil (noms de pièces visibles un instant pour un profil sans `optical.documents.read`) ; la référence du profil est désormais mise à jour avant le rendu ; test de régression ajouté.

## 11. Refonte UX selon la maquette Lot 3 — 20/10/2026 (D-04-15 APPROVED)

Le propriétaire a testé le banc : structure fonctionnelle satisfaisante, **ergonomie des formulaires de saisie non validée**. Décision **D-04-15 — APPROVED (propriétaire, 20/10/2026)** : la maquette HTML Claude Design du Lot 3 est la référence visuelle et ergonomique des formulaires optiques, avec contrôles rapides ± et saisie au clavier complémentaire. D-04-15 est une décision d'**interface** : elle n'approuve ni pas, ni bornes, ni format, ni règle croisée — **D-04-06 reste `PROPOSED` [AVIS OPTICIEN]**. Identifiant vérifié libre avant inscription (D-04-01 à D-04-14 déjà utilisés, aucun D-04-15 antérieur).

### 11.1 Préflight et référence

| Contrôle | Résultat |
|---|---|
| HEAD au départ | `26e515f` sur `feat/phase-04-00-cadrage` ; aucune modification suivie ; non suivis du propriétaire intacts |
| maquette | `references/phase04/lot3/MMV-Lot3-Ordonnances.dc.html` : SHA-256 `c103df9e434245cdcba5b6246c5ec3032301e323c6ec1797826deaf8fd06c213`, 93 625 octets = registre ([`SOURCE_MAP.md`](SOURCE_MAP.md)) ; code de rendu analysé (`stepField`, `axisField`, `baseField`, `avField`, `eyeCardForm`, `genInfo`, `formPage`, `reducedWindow`, `tabOrderScreen`, `fieldStateScreen`) |
| rendu de la maquette | servie en **lecture seule** depuis le dossier de conception du propriétaire `Downloads/MMV initialisation projet V1/` (même empreinte, `support.js` et `_ds/` présents), serveur local 127.0.0.1:1530, Edge sans interface ; 6 cadres capturés (`newClientLight`, `newClientDark`, `tabOrder`, `focusField`, `formErrors`, `reducedWindow`) ; rien n'est copié dans le dépôt hors captures |
| ancien MMV, PostgreSQL, bases | **non consultés** ; aucune règle de l'ancien validateur reprise (§ 11.4) |

### 11.2 Composants créés ou modifiés (prototype, banc isolé)

| Fichier | Changement |
|---|---|
| `apps/desktop/src/optical/opticalStep.ts` *(nouveau)* | logique pure des boutons ± : `stepValue(valeur, ±1, pas)` (calcul décimal exact en entiers), `isSteppable`, `stepLabel`, `DEMO_STEP = 0.25`, contexte `OpticalStepContext` (pas **paramétrable**), `STEPPER_FIELDS` (Sphère, Cylindre, Addition, Prisme) |
| `apps/desktop/src/optical/EyeGrid.tsx` | grille de la maquette, partagée par les deux types : carte par œil (pastille 40 px, « Œil droit » / « Oculus dexter »), rangées libellé 118 px + contrôle ; composant de saisie ± `StepperInput` (− à gauche, valeur éditable au centre, + à droite, « Effacer »), `TextInput` (Axe avec suffixe « degrés », Acuité), `BaseInput` (saisie libre + liste **proposée**), annonce `aria-live` ; `EyeTable` (lecture seule) inchangé |
| `apps/desktop/src/optical/OpticalRecordForm.tsx` | mise en page de la maquette (`formPage`) : en-tête (titre, badge de type, « Ctrl + S », sous-titre, actions Annuler / Enregistrer et confirmer… / Enregistrer le brouillon, collé en haut si la hauteur ≥ 720 px) ; carte « Informations générales » avec le **client concerné** (avatar, nom, référence, retour « Fiche client ») et grille 3 colonnes (Date · Prescripteur · Référence en lecture seule) ; section « Correction optique » ; cartes « Pièces liées » et « Notes ». Provenance **propre à chaque type** inchangée (D-04-01) ; mesure magasin : mention « ce n'est pas une prescription délivrée par un médecin » |
| `apps/desktop/src/optical/Optical.css` | cotes de la maquette (`.card`, `.clictx`, `.eyes`, `.eye-h`, `.eye-tag`, `.orow`, `.stepper`, `.stepbtn`, `.stepval`, `.oinp`) en jetons MMV (clair / sombre) ; requêtes de conteneur : OD au-dessus d'OG sous 940 px de formulaire, grille générale 3 → 2 → 1 colonnes, libellé au-dessus du contrôle dans une carte de moins de 400 px |
| `apps/desktop/src/optical/opticalLabels.ts` | `BASE_SUGGESTIONS` (Nasal, Temporal, Supérieur, Inférieur — libellés de la maquette, **liste non approuvée**) |
| `apps/desktop/src/bench/OpticalBench.tsx`, `main.tsx` | commande « Pas des boutons ± (démonstration) » (0,125 / 0,25 / 0,5) et paramètre d'URL `step` |
| `apps/desktop/e2e/bench-04a/plan-lot3.mjs` *(nouveau)* | plan de captures de la refonte (matrice et états ±), exécuté par `shoot.mjs` |
| tests | `src/optical/opticalStep.test.ts` *(nouveau, 7)* ; `src/bench/OpticalBench.test.tsx` (+7 parcours ±, 3 tests adaptés au nouvel en-tête / à la grille, attente allongée d'un test intermittent, O-04-27) |

Aucune dépendance ajoutée ; aucun fichier hors `apps/desktop/src/optical/`, `apps/desktop/src/bench/`, `apps/desktop/e2e/bench-04a/`, `docs/`, `prompts/`, `README.md` modifié. Application réelle inchangée (aucun import de `optical/` ni de `bench/` ; test d'isolement et recherche dans le bundle).

### 11.3 Comportement retenu des boutons ± (prototype, réversible)

| Cas | Comportement | Test |
|---|---|---|
| pas | **0,25 par défaut (démonstration)**, paramétrable (contexte `OpticalStepContext`, commande du banc, `?step=`) ; aucune valeur approuvée (D-04-06) | Q-04-176 |
| champ vide + clic | nouvelle saisie **explicite** partant de 0 : `+` → `+0,25`, `−` → `-0,25` ; le vide reste affiché « — », jamais « 0,00 » | Q-04-171 |
| valeur numérique + clic | calcul décimal exact : **aucun arrondi, aucun alignement sur le pas** (`-2,13` + 0,25 = `-1,88`) ; décimales = max(saisie, pas) ; séparateur `,` / `.` et signe `−` typographique conservés ; signe `+` explicite sur un résultat positif, `0,00` sans signe | Q-04-171, `opticalStep.test.ts` |
| valeur saisie au clavier | **jamais modifiée** sans clic ni flèche (aucune normalisation à la sortie du champ) ; la saisie au clavier reprend la main à tout moment | Q-04-171, Q-04-158 |
| valeur non numérique (« abc ») | boutons inactifs (`aria-disabled`), valeur inchangée, aide sous le champ | Q-04-172 |
| bornes, transposition, axe | aucune borne ; aucune conversion de signe du cylindre (`+0,75` − 4 × 0,25 = `-0,25` par simple calcul) ; Axe, Base, Acuité : **aucun pas**, axe non normalisé (`0` reste `0`) | Q-04-171 |
| effacer | bouton « Effacer » (×) par champ → non renseigné ; au clavier : effacer le texte | Q-04-173 |
| clavier | Tab / Maj+Tab parcourent les **seuls champs** (Sphère → Acuité, OD puis OG ; boutons ±, « Effacer » et liste des bases hors de l'ordre) ; ↑ / ↓ dans un champ ± = ± pas, le focus reste ; Alt+↓ ouvre la liste des bases ; Ctrl+S inchangé | Q-04-174 |
| information affichée | mise à jour immédiate, annonce polie « Sphère OD : +0,75 » ; noms accessibles « Augmenter Sphère OD de 0,25 » | Q-04-170 |
| conservation | un clic rend le formulaire « modifié » (garde de sortie) ; valeurs enregistrées telles qu'affichées | Q-04-175 |
| yeux | OD et OG indépendants | Q-04-170 |

### 11.4 Écarts avec la maquette et justification

| Élément de la maquette | Traitement | Raison |
|---|---|---|
| Shell (barre latérale 260 px, barre supérieure, barre d'état) | non repris dans le banc | le banc a son propre cadre (bannière permanente, commandes) ; l'application réelle n'affiche aucun écran optique (O-04-16) ; intégration au Shell à l'activation 04-C (O-04-28) |
| titre « Nouvelle ordonnance », action « Enregistrer l'ordonnance » | « Nouvelle ordonnance médicale » / « Nouvelle mesure magasin » + badge de type ; « Annuler », « Enregistrer et confirmer… », « Enregistrer le brouillon » | D-04-01 (deux types) ; cycle brouillon / confirmation du prototype (E-02, proposé) |
| « Date d'émission * » | « Date de prescription », **sans** astérisque | aucune obligation inventée (D-04-03.a proposée) |
| « Médecin prescripteur » (+ « Facultatif ») | nom, qualité, identifiant du prescripteur, date de réception | cadrage D-04-03.a ; rien n'est obligatoire, la mention « Facultatif » devient inutile |
| écran « mesure » | absent de la maquette : même gabarit, provenance propre (date, collaborateur, catégorie), mention « pas une prescription » | D-04-01 |
| valeur du stepper (affichage seul) | champ **éditable** + bouton « Effacer » | exigences du propriétaire (saisie clavier, effacement) |
| repère « 0,00 » d'un champ vide ; « 0–180 » (Axe) ; « 10/10 » (Acuité) ; « Choisir… » (Base) | « — » | une absence n'est pas un zéro ; aucune borne ni aucun format imposés (D-04-06) |
| note « Pas de 0,25 dpt · signe + explicite » | « Boutons ± : pas de démonstration 0,25 (paramétrable, D-04-06 ouverte) · ↑ / ↓ au clavier · saisie libre conservée telle quelle » | le pas n'est pas une règle ; le signe `+` n'est produit que par un clic |
| Axe désactivé / requis selon le cylindre ; Base désactivée / requise selon le prisme ; messages correspondants ; normalisation 0° → 180° (états 25, 36) | **non repris** | règles croisées de l'ancien validateur (C-04-01, C-04-06) ; D-04-06 ouverte ; seules des erreurs de **forme** renvoyées par le service s'affichent |
| Base = liste fermée (Nasal / Temporal / Supérieur / Inférieur) | saisie libre + liste **proposée** (chevron ou Alt+↓) | notation et choix définitifs à fixer (D-04-06) |
| — | rangée « État de l'œil » **ajoutée** en tête de chaque carte | D-04-02.c (proposée, déjà présente en 04-A) |
| — | étiquettes « Prototype · D-04-xx PROPOSED » **ajoutées** | aucune fonction fictive présentée comme opérationnelle |
| — | carte « Pièces liées à cette révision » **ajoutée** (brouillon d'ordonnance) | D-04-10 (proposée, déjà présente en 04-A) |
| fenêtre réduite : OD au-dessus d'OG à ≈ 900 px de fenêtre | empilement selon la **largeur réelle du formulaire** (< 940 px) | dans le banc à 1024 px, les commandes passent au-dessus et le formulaire garde ≈ 960 px : OD / OG restent côte à côte et lisibles ; empilés à 1024 px × 125 / 150 % ; aucun défilement horizontal |
| « Créer une vente », « Supprimer » | non repris (inchangé) | C-04-02, C-04-03 |

### 11.5 Qualité et preuves (20/10/2026)

| Contrôle | Statut |
|---|---|
| typage `npm run check` | PASS |
| lint `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02) |
| Vitest (toutes suites) | **PASS 279 / 279** (25 fichiers ; 265 précédents + 14 nouveaux) — [`evidence/P04A_UX_frontend_20261020.log`](evidence/P04A_UX_frontend_20261020.log) |
| build `npm run build` | PASS (avertissement connu de taille de bloc) ; banc et composants ± absents du bundle — [`evidence/P04A_UX_bundle_20261020.log`](evidence/P04A_UX_bundle_20261020.log) |
| non-intégration en production | PASS — `realAppIsolation.test.tsx` (4) + recherche dans le bundle (seul « Dossier optique », O-04-24) |
| captures de la refonte (navigateur) | PASS — **34 captures** : 2 types × 2 thèmes × 1920×1080 / 1024×700 × zooms 100 / 125 / 150 %, + états ± ; 0 erreur console, 0 défilement horizontal — [`evidence/P04A_UX_captures_rapport_20261020.json`](evidence/P04A_UX_captures_rapport_20261020.json) |
| non-régression visuelle 04-A | PASS — plan d'origine rejoué (**54 captures**, 0 erreur, 0 défilement horizontal) — [`evidence/P04A_UX_regression_captures_rapport_20261020.json`](evidence/P04A_UX_regression_captures_rapport_20261020.json) |
| interface Tauri native | **NOT_TESTED** (banc hors application, O-04-16) |
| tests .NET, PostgreSQL | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié) |

Rejeu après le dernier changement de code : voir [`QA_MATRIX.md`](QA_MATRIX.md), section « 04-A — refonte UX Lot 3 ».

### 11.6 Captures comparatives

Planches côte à côte (gauche : maquette rendue ; droite : banc) dans [`evidence/`](evidence/) :

| Planche | Objet |
|---|---|
| `P04A_UX_compare_01_ordonnance_clair.jpg`, `P04A_UX_compare_02_ordonnance_sombre.jpg` | hiérarchie, en-tête, carte « Informations générales », cartes OD / OG (1920×1080) |
| `P04A_UX_compare_03_correction_optique.jpg` | OD / OG renseignés : maquette (état 33) ↔ saisie par boutons ± |
| `P04A_UX_compare_04_mesure_clair.jpg`, `P04A_UX_compare_05_mesure_sombre.jpg` | mesure magasin (gabarit de la maquette ; aucun écran mesure dans le Lot 3) |
| `P04A_UX_compare_06_fenetre_reduite.jpg` | fenêtre réduite (maquette ≈ 900 px ↔ banc 1024×700) |
| `P04A_UX_compare_07_focus.jpg`, `P04A_UX_compare_08_erreurs.jpg` | focus visible ; erreurs (maquette : règles de l'ancien validateur ↔ banc : erreur de forme seulement) |
| `P04A_UX_avant_apres_01_ordonnance.jpg` à `P04A_UX_avant_apres_04_1024.jpg` | avant (04-A du 19/10/2026) ↔ après |

Captures brutes : `P04A_UX_navigateur_apres-*.jpg` (34), `P04A_UX_navigateur_avant-*.jpg` (9), `P04A_UX_maquette_lot3_*.jpg` (6). Les cadres de la maquette montrent ses propres identités de démonstration (conception du propriétaire) ; le banc n'utilise que des identités `Fictif-…`.

### 11.7 Lancement du banc

1. `cd apps/desktop && node e2e/bench-04a/serve.mjs` (port 1520, cache hors dépôt ; ne pas utiliser `npx vite --port …`, O-04-21).
2. Ouvrir **http://127.0.0.1:1520/optical-bench.html** → scénario « Fiche FICTIF-… → Dossier optique » → « Ajouter… » → type ; ou directement `?route=/clients/cli-fictif-3`, onglet « Dossier optique ».
3. Paramètres : `theme=dark`, `step=0.125` (pas de démonstration), `profile=…`, `latency=800` ; commande latérale « Pas des boutons ± (démonstration) ».
4. Captures : `node e2e/bench-04a/plan-lot3.mjs plan.json apres && node e2e/bench-04a/shoot.mjs plan.json <sortie> <profil-edge-jetable>`.

### 11.8 Points encore dépendants d'une validation métier

1. **D-04-06 [AVIS OPTICIEN]** — pas réel par grandeur (0,25 partout ? prisme ?), bornes, format d'affichage (signe `+` explicite, deux décimales), axe (bornes, 0 ou 180), règles Cylindre ⇄ Axe et Prisme ⇄ Base (désactivation, obligation), notation de la base (libellés de la maquette, BI / BO / BU / BD, degrés, une ou deux composantes), format de l'acuité.
2. **D-04-02.c** — l'état de l'œil doit-il passer à « Renseigné » lorsqu'une valeur est saisie ? Aucun couplage automatique dans le prototype (O-04-29).
3. **D-04-03.a / b** — date de prescription obligatoire (astérisque de la maquette) ; listes fermées des qualités du prescripteur et des catégories de mesure.
4. Ordre de Tab exact (état 33) après D-04-03 / D-04-06.
5. Validation visuelle de la refonte par le propriétaire (`OWNER_VISUAL_REVIEW_REQUIRED`).

## 12. Simplification du dossier optique — 20/10/2026 (D-04-16 APPROVED)

Décision **D-04-16 — APPROVED (propriétaire, 20/10/2026)** : *« Je conserve le système de révisions, mais je demande la suppression du champ « État de l'œil » et le report de la gestion des « Pièces sources ». »* Objectif : simplifier le parcours quotidien de l'opticien sans compromettre la traçabilité des corrections. Identifiant vérifié libre avant inscription. Les §§ 1 à 11 sont conservés comme historique ; ce qu'ils décrivent de l'état de l'œil et des pièces n'est plus l'état courant.

### 12.1 Préflight

| Contrôle | Résultat |
|---|---|
| HEAD au départ | `9be2fda` sur `feat/phase-04-00-cadrage` ; aucune modification suivie ; non suivis du propriétaire intacts |
| lecture | code du prototype (`apps/desktop/src/optical/`, `apps/desktop/src/bench/`, plans de captures) et documents de `docs/phase/04/` concernés, avant modification |
| PostgreSQL, ancien MMV | **non consultés** ; aucune migration `0006`, aucun backend optique, aucune activation réelle |

### 12.2 Changements (prototype, banc isolé)

| Fichier | Changement |
|---|---|
| `apps/desktop/src/optical/opticalContract.ts` | `EyeState` / `EYE_STATES` supprimés ; un œil = `EyeValues` (sept chaînes, `""` = non renseigné) ; `linkedDocumentIds`, `SourceDocument`, `documents` / `documentCount`, `SyntheticDocumentChoice`, `DocumentContent`, motifs d'invalidation de pièce et opérations `attachDocument` / `invalidateDocument` / `fetchDocumentContent` supprimés |
| `EyeGrid.tsx` | rangée « État de l'œil » (boutons radio) supprimée ; colonne « État » du tableau de lecture supprimée ; Tab va directement de l'Acuité OD à la Sphère OG ; boutons ± et design Lot 3 inchangés |
| `OpticalRecordForm.tsx` | carte « Pièces liées à cette révision » supprimée ; provenance (prescripteur, date de prescription, date de réception ; date, collaborateur, catégorie pour la mesure) inchangée |
| `OpticalRecordDetail.tsx`, `RevisionsPanel.tsx`, `opticalRevisions.ts` | panneau « Pièces source » supprimé ; comparaison sans lignes « état » ni « Pièces liées » ; liste des révisions (numéro, motif, auteur, dates), comparaison des valeurs et journal de gestion conservés |
| `OpticalDialogs.tsx` | récapitulatif de confirmation : « États par œil » remplacé par « Correction » (résumé OD / OG tel que saisi, « non renseigné » pour un œil vide) |
| `opticalUi.ts`, `opticalLabels.ts`, `OpticalRecordsTable.tsx` | résumé OD / OG sans état (œil vide = « non renseigné », `0` reste `0`) ; messages et libellés des pièces et de l'état supprimés |
| `opticalAccess.ts`, `opticalQueries.ts` | droits simulés `optical.documents.*`, hooks et contexte des pièces supprimés |
| `SourceDocumentsPanel.tsx`, `src/bench/benchImages.ts` | **supprimés** |
| `Optical.css` | styles des pièces (`optical-docs*`, `optical-doc*`, `optical-preview*`, `optical-links`) et de l'état (`optical-radio`, `optical-eye__choices`) supprimés ; sous-titre renommé `optical-detail__subtitle` |
| `src/bench/inMemoryOpticalGateway.ts` | stockage et opérations de pièces supprimés ; règle de démonstration `422 incomplete` réduite à la provenance (aucun critère sur les valeurs OD / OG, D-04-02.b ouverte, O-04-30) |
| `src/bench/benchSeed.ts`, `benchProfiles.ts`, `OpticalBench.tsx` | jeu fictif sans pièces ni état (RX-FICTIF-0001 : révision 2 « Complément d'information » ajoutant l'addition ; RX-FICTIF-0002 : rectification en cours ; brouillons avec un œil laissé vide) ; profils simulés sans droits de pièces ; scénarios de l'accueil mis à jour |
| `e2e/bench-04a/plan.mjs` | captures 22, 33, 34 (pièces) retirées ; 35 renommée `35-etat22-lecture-seule` |
| tests | `OpticalBench.test.tsx` : 2 parcours « pièces » remplacés par 3 parcours « simplification » ; ordre de Tab, refus 422 et récapitulatif adaptés ; `inMemoryOpticalGateway.test.ts` : 3 tests « pièces » remplacés par 2 tests « simplification » ; `opticalUi.test.ts` adapté |

Aucune dépendance ajoutée ; aucun fichier .NET, SQL ni de configuration serveur modifié ; application réelle inchangée.

### 12.3 Ce qui est conservé

- **Révisions** (E-02, D-04-11 PROPOSED) : brouillon → confirmation, rectification motivée en brouillon, dernière révision confirmée affichée par défaut, un seul brouillon, numéro à la confirmation, abandon conservé, invalidation motivée.
- **Comparaison des valeurs** champ à champ (OD / OG, provenance, notes), « Différent » écrit en toutes lettres.
- **Motif de rectification** (liste D-04-04.b proposée, commentaire) et **traçabilité des auteurs** (saisie, modification, confirmation, abandon ; journal de gestion).
- **Provenance** : prescripteur (nom, qualité « Non précisée », identifiant), date de prescription, date de réception ; mesure : date, collaborateur, catégorie.
- **D-04-01** (deux types distincts) et **D-04-15** (maquette Lot 3, boutons ±, pas de démonstration).
- **Absence ≠ 0** : chaque champ peut rester vide (« — », nom accessible « Non renseigné »), jamais assimilé à `0`.

### 12.4 Qualité et preuves (20/10/2026, après le dernier changement de code)

| Contrôle | Statut |
|---|---|
| typage `npm run check` | PASS |
| lint `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02) |
| Vitest (toutes suites) | **PASS 279 / 279** (25 fichiers) — [`evidence/P04A_SIMPL_frontend_20261020.log`](evidence/P04A_SIMPL_frontend_20261020.log) |
| build `vite build` | PASS ; banc, pièces et état de l'œil absents du bundle — [`evidence/P04A_SIMPL_bundle_20261020.log`](evidence/P04A_SIMPL_bundle_20261020.log) |
| captures navigateur | PASS — `plan-lot3.mjs` (34) et `plan.mjs` (51), 0 erreur console, 0 défilement horizontal — [`evidence/P04A_SIMPL_captures_rapport_20261020.json`](evidence/P04A_SIMPL_captures_rapport_20261020.json) ; sélection versionnée : `evidence/P04A_SIMPL_navigateur_*.jpg` (15) |
| interface Tauri native | **NOT_TESTED** (banc hors application, O-04-16) |
| tests .NET, PostgreSQL | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié) |

Détail : [`QA_MATRIX.md`](QA_MATRIX.md), section « 04-A — simplification (D-04-16) ». Captures prises sur le serveur de banc déjà actif du poste (O-04-31).

### 12.5 Points encore dépendants d'une validation métier

1. **D-04-02.b [AVIS OPTICIEN]** — critère de confirmation à reformuler sans état d'œil (O-04-30) ; aucune règle n'est inventée dans le prototype.
2. **D-04-06 [AVIS OPTICIEN]** — inchangé (§ 11.8, point 1).
3. **D-04-03.a / b** — inchangé (§ 11.8, point 3).
4. Motif d'invalidation « Document non recevable » conservé (O-04-32).
5. Validation visuelle de la simplification par le propriétaire (`OWNER_VISUAL_REVIEW_REQUIRED`).

Reporté au **backlog permanent BL-04-01** (gestion documentaire) : D-04-03.c, D-04-10, D-04-12.a/b/d, D-04-14, droits `optical.documents.*`, O-04-01 / 02 / 03 / 13 / 17 / 18 / 23, volet « pièces » d'O-04-19 ; études E-01, E-03 (pièces) et E-05 conservées pour la reprise.

## 13. Enregistrement simplifié et clôture technique — 20/10/2026 (D-04-17 APPROVED)

Décision **D-04-17 — APPROVED (propriétaire, 20/10/2026)** : *« Enregistrement optique simplifié : action principale Enregistrer, option secondaire discrète Enregistrer comme brouillon, sans suppression du cycle de révisions. »* Identifiant vérifié libre avant inscription (D-04-16 dernier utilisé). Dernier mandat de développement de 04-A. Les §§ 1 à 12 sont conservés comme historique.

**État : `PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`** — clôture technique du lot frontend ; ne constitue **pas** un GO pour 04-B (G04-METIER et G04-SQL restent requis).

### 13.1 Préflight

| Contrôle | Résultat |
|---|---|
| HEAD au départ | `64cedc4` sur `feat/phase-04-00-cadrage` ; aucune modification suivie ; non suivis du propriétaire intacts |
| lecture | `OpticalRecordForm.tsx`, double en mémoire, tests du banc, plans de captures et documents de `docs/phase/04/` concernés, avant modification |
| PostgreSQL, ancien MMV, API | **non consultés** ; aucune migration `0006`, aucun backend optique, aucune activation réelle, aucun compte modifié |

### 13.2 Comportement livré (prototype, banc isolé)

| Point du mandat | Réalisation |
|---|---|
| action principale | bouton bleu **« Enregistrer »** (variante `primary`, `aria-keyshortcuts="Control+S"`) sur « Nouvelle ordonnance médicale », « Nouvelle mesure magasin », brouillons et modifications : enregistre le brouillon puis le confirme, **sans récapitulatif intermédiaire** ; succès → notification « Ordonnance médicale enregistrée » / « Mesure magasin enregistrée » (« RX-… : révision N confirmée. ») et retour au détail (puis « Retour à la fiche » selon le parcours) |
| action secondaire | **« Enregistrer comme brouillon »** : bouton tertiaire (texte accent, sans fond) entre « Annuler » et « Enregistrer » — visible, atteignable au Tab, utilisable à Entrée ; choisi plutôt qu'un menu (aucun menu d'actions dans l'en-tête du formulaire Lot 3, une action de moins à découvrir) |
| Ctrl+S, Entrée | = « Enregistrer » (jamais le brouillon) |
| règle de démonstration | double en mémoire : un enregistrement dont **toutes** les données optiques (OD et OG, sept champs chacun, espaces ignorés) sont absentes ne peut pas être confirmé (`422 incomplete`, chemin `correction`, message « … règle de démonstration du banc, pas une validation médicale ») ; un seul œil suffit ; `0` est une valeur ; aucune règle sur la provenance (O-04-36) ; le poste ne code aucune règle |
| échec partiel (brouillon enregistré, confirmation refusée) | formulaire **resynchronisé** sur le brouillon enregistré (plus de faux avertissement « Quitter sans enregistrer ? ») ; bandeau **« Brouillon enregistré — confirmation refusée »** (409, 422, 403…) ou **« … confirmation non aboutie »** (erreur de communication), qui ne dit jamais que rien n'a été enregistré ; actions « Laisser en brouillon » (aucune nouvelle écriture) et, si une version plus récente existe, « Recharger » ; nouvelle tentative sur le **même** enregistrement (aucun doublon) ; dernière révision confirmée inchangée |
| doubles déclenchements | garde unique (référence + état) : double clic, Ctrl+S répété, Entrée pendant l'enregistrement ignorés, y compris entre l'enregistrement du brouillon et la confirmation ; boutons désactivés et libellé « Enregistrement… » |
| libellés | « Modifier » (au lieu de « Rectifier », « Poursuivre », « Poursuivre le brouillon »), « Modifier cet enregistrement ? », « Modification en cours », « Confirmer la modification », « Abandonner la modification », **« Historique des modifications »** (au lieu de « Révisions »), **« Dernière révision confirmée — révision N »** ; « en vigueur » **retiré** de toute l'interface du prototype ; termes internes (révision, rectification, brouillon) conservés dans le modèle et le journal de gestion |
| banc | commandes « Échec de la prochaine confirmation » et « Conflit à la prochaine confirmation » (la confirmation seule échoue, l'enregistrement du brouillon qui la précède aboutit) ; scénarios de l'accueil mis à jour |
| inchangé | design Lot 3 (D-04-15), contrôles ± et pas de démonstration, absence d'état de l'œil et de pièces (D-04-16), provenance propre à chaque type (D-04-01), révisions, comparaison, motif, auteurs, invalidation, « Préparer une vente » désactivé |

### 13.3 Fichiers modifiés

| Fichier | Changement |
|---|---|
| `apps/desktop/src/optical/OpticalRecordForm.tsx` | actions « Enregistrer » / « Enregistrer comme brouillon », Ctrl+S et Entrée = « Enregistrer », garde `busyRef`, échec partiel resynchronisé, « Laisser en brouillon », récapitulatif retiré du formulaire, titre « Modifier — … », mention « dernière révision confirmée » |
| `apps/desktop/src/optical/opticalUi.ts` | `describeConfirmFailure` (messages d'échec après brouillon enregistré) ; code `optical_values_missing` et libellé « Correction optique » ; `required_to_confirm` retiré |
| `apps/desktop/src/optical/OpticalRecordDetail.tsx`, `OpticalDialogs.tsx`, `RevisionsPanel.tsx` | libellés simplifiés (§ 13.2) ; plus aucun « en vigueur » |
| `apps/desktop/src/bench/inMemoryOpticalGateway.ts`, `benchSeed.ts`, `OpticalBench.tsx` | règle minimale de démonstration ; panne simulée `nextConfirmFault` (`network` / `conflict`) ; commandes du banc ; commentaires du jeu fictif |
| tests | `OpticalBench.test.tsx` : 10 tests adaptés aux nouveaux libellés (le test « 422 provenance » devient « un seul œil suffit »), 7 tests « enregistrement simplifié (D-04-17) » ajoutés ; `inMemoryOpticalGateway.test.ts` : test de la règle remplacé, 2 tests ajoutés (espaces, panne de la confirmation seule) — aucun test désactivé |
| `apps/desktop/e2e/bench-04a/plan.mjs`, `plan-lot3.mjs` | libellés mis à jour ; capture 27 refaite sur la nouvelle règle ; 7 captures `70`–`76` (D-04-17) |

Aucune dépendance ajoutée ; aucun fichier .NET, SQL ni de configuration serveur modifié ; **application réelle inchangée** (aucun fichier hors `src/optical/`, `src/bench/` et `e2e/bench-04a/` ; bundle de production sans le banc).

### 13.4 Vérification des critères de clôture (§ 7 du mandat)

| # | Parcours | Preuve | Résultat |
|---|---|---|---|
| 1–4 | fiche fictive → nouvelle ordonnance → ± sur OD et OG → « Enregistrer » → retrouvée (détail, liste, onglet de la fiche) | test « une action principale « Enregistrer »… », J-04-01 ; captures `70`, `72` | PASS |
| 5 | modifier les valeurs, ancienne révision retrouvée | J-04-04 « modifier → brouillon… révision 2 », test 409 D-04-17 (révision 1 visible dans l'historique) ; captures `23`, `42`, `75` | PASS |
| 6–7 | brouillon incomplet (aucune valeur) au clavier, repris puis confirmé | test « brouillon incomplet enregistré au clavier… » | PASS |
| 8 | erreurs et conflits : 422 (aucune valeur), 409 à l'enregistrement (autre poste), 409 et erreur de communication à la confirmation, double clic, Ctrl+S | 4 tests D-04-17, J-04-06, états 9 / 31 ; captures `27`, `40`, `73`, `74` | PASS |
| 9–10 | mesure magasin, même design, provenance distincte | test « Ctrl+S = « Enregistrer »… mesure magasin », contrôles ± (pas paramétrable) ; captures `71`, matrice Lot 3 « mesure » | PASS |
| 11 | ni « État de l'œil » ni « Pièces sources » | suite « simplification (D-04-16) », bundle | PASS |
| 12 | listes, recherche, détails, droits simulés, invalidation | J-04-02, J-04-07, suite « accès refusé », changement de profil | PASS |
| 13 | retours vers la fiche, avertissements de sortie (vrais et absence de faux) | J-04-01, test « conservation » (garde), test 409 D-04-17 (pas de faux avertissement) | PASS |
| 14 | « Préparer une vente » désactivé | J-04-04 | PASS |
| — | contrôles ± : sphère, cylindre, addition, prisme, axe, base, acuité, saisie manuelle, clavier, valeurs négatives / positives / nulles / absentes, sans arrondi | suite « contrôles rapides ± » (5 tests, inchangés), « grille OD / OG neutre » ; captures saisie rapide | PASS |
| — | thèmes clair / sombre, zoom 100 / 125 / 150 %, 1920×1080 et 1024×700 | `plan-lot3.mjs` (34 captures) + `plan.mjs` (58) : **0 erreur console, 0 défilement horizontal** | PASS (navigateur) |
| — | clavier, ordre de Tab, noms accessibles | tests « ordre de Tab », « clavier », « brouillon… au clavier » ; ordre de l'en-tête : Annuler → Enregistrer comme brouillon → Enregistrer | PASS |

### 13.5 Qualité et preuves (20/10/2026, après le dernier changement de code)

| Contrôle | Statut |
|---|---|
| typage strict `npm run check` | PASS |
| lint `eslint .` | PASS — 0 erreur, 1 avertissement connu (O-03-02) |
| Vitest (suite frontend complète, composants communs inclus) | **PASS 288 / 288** (25 fichiers) — [`evidence/P04A_CLOT_frontend_20261020.log`](evidence/P04A_CLOT_frontend_20261020.log) |
| build `vite build` | PASS ; banc, double, jeu fictif et chaînes D-04-17 absents du bundle ; seule occurrence « Dossier optique » : libellé de l'onglet réservé au banc dans `ClientDetailView` (inchangé, non affiché par l'application réelle qui garde « Ordonnances — phase ultérieure ») — [`evidence/P04A_CLOT_bundle_20261020.log`](evidence/P04A_CLOT_bundle_20261020.log) |
| isolement du prototype | PASS — `realAppIsolation.test.tsx`, garde réseau du banc (0 appel émis dans chaque test) |
| captures navigateur | PASS — 58 + 34, 0 erreur console, 0 défilement horizontal — [`evidence/P04A_CLOT_captures_rapport_20261020.json`](evidence/P04A_CLOT_captures_rapport_20261020.json) ; sélection versionnée : `evidence/P04A_CLOT_navigateur_*.jpg` (18) |
| interface Tauri native | **NOT_TESTED** (banc hors application, O-04-16 ; aucune déclaration de réussite) |
| tests .NET, PostgreSQL | **NOT_RERUN** (aucun fichier .NET, SQL ni configuration serveur modifié) |

Captures finales des deux formulaires : `P04A_CLOT_navigateur_70-d0417-formulaire-ordonnance-actions.jpg`, `P04A_CLOT_navigateur_71-d0417-formulaire-mesure-actions.jpg`, `P04A_CLOT_navigateur_apres-ordonnance-1920x1080-z100-clair.jpg` / `-sombre.jpg`, `P04A_CLOT_navigateur_apres-mesure-1920x1080-z100-clair.jpg`, `P04A_CLOT_navigateur_apres-ordonnance-1024x700-z100-clair.jpg`, `P04A_CLOT_navigateur_apres-mesure-1024x700-z150-sombre.jpg`. Captures prises sur le serveur de banc déjà actif du poste (O-04-37).

### 13.6 Réserves (non bloquantes pour 04-A)

1. Tauri natif **NOT_TESTED** (le prototype n'est pas monté dans l'application, O-04-16).
2. O-04-34 : enchaînement brouillon → confirmation (deux appels) ; atomicité à décider avec l'API de 04-B.
3. O-04-35 : récapitulatif de confirmation retiré du parcours principal du formulaire (conservé sur le détail).
4. O-04-36 : la règle de démonstration ne couvre plus la provenance ; critères définitifs à G04-METIER.

### 13.7 Décisions métier restantes pour préparer 04-B (G04-METIER)

1. **D-04-02 [AVIS OPTICIEN]** — statuts, **critère minimal de confirmation** (b, à reformuler sans état d'œil, O-04-30 — la règle du banc n'en tient pas lieu), un seul œil, qui confirme (d).
2. **D-04-03.a / b** — champs de provenance requis et listes fermées (qualité du prescripteur, catégorie de mesure).
3. **D-04-04** — motifs de rectification et d'invalidation (dont « Document non recevable », O-04-32).
4. **D-04-05** — permissions (`optical.read`, `.prescriptions.write`, `.measurements.write`, `.invalidate`) et profils.
5. **D-04-06 [AVIS OPTICIEN]** — pas, bornes, règles croisées, liste des bases (le pas 0,25 du banc reste un réglage de démonstration).
6. **D-04-07 à D-04-09** — client archivé, navigation et « Préparer une vente » inerte, pays, confidentialité et conservation.
7. **D-04-11, D-04-12.c, D-04-13** (révisions, volet encore applicable des événements, granularité de lecture) ; puis G04-SQL (revue de la migration `0006`, non générée ni appliquée) et G04-LEGAL avant toute donnée réelle.

Toujours au **backlog permanent BL-04-01** : gestion documentaire des pièces sources (D-04-03.c, D-04-10, D-04-12.a/b/d, D-04-14, `optical.documents.*`).
