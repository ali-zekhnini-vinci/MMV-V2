# 04 — Décisions métier : D-04-01 (APPROVED) et D-04-02 à D-04-09 (PROPOSED)

**Date : 19/10/2026 (04-00).** Mandat : [`prompts/PHASE04_00_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_GO_CLAUDE_CODE.md). Cadrage : `references/phase04/CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md` (v0.2, copie de travail non versionnée, empreinte dans [`SOURCE_MAP.md`](SOURCE_MAP.md)).

> **Statut de ce document : `PROPOSED — OWNER_METIER_REVIEW_REQUIRED`.** Seule D-04-01 est approuvée. Tout le reste est une **proposition à ratifier** : aucune règle ci-dessous n'est appliquée, codée ou migrée. Les points marqués **[AVIS OPTICIEN]** ne peuvent pas être « adoptés par défaut » : ils exigent la réponse d'un opticien (ou d'un professionnel habilité) choisi par le propriétaire. Les points marqués **[CONSEIL JURIDIQUE]** exigent un avis juridique pour le pays concerné (Maroc, Belgique). Aucune règle n'est tirée d'un ancien code : les règles et plages numériques citées par les notes de la maquette Lot 3 (dépôt de l'ancien MMV) sont **irrecevables** (D-000) et ne sont pas reprises ici.

## D-04-01 — APPROVED (propriétaire, 19/10/2026) — non redemandée

MMV V2 conserve **les ordonnances médicales reçues** et **les mesures réalisées en magasin** comme **deux types métier distincts**, séparés dans le dossier optique et non interchangeables. Une mesure magasin ne devient jamais une ordonnance médicale par saisie, conversion automatique ou affichage. Provenance, auteur et statut des deux catégories restent explicites. Cette décision n'autorise aucun SQL.

**Conséquences appliquées dans tout le dossier 04-00 :**

| Domaine | Application |
|---|---|
| Vocabulaire | « Ordonnance médicale » (document reçu d'un prescripteur, transcrit dans MMV) ; « Mesure magasin » (relevé réalisé au magasin par un collaborateur). Abréviations internes : `RX` et `MES`. Le mot « prescription » n'est jamais appliqué à une mesure magasin. |
| Modèle proposé | deux agrégats distincts, chacun avec ses révisions ([`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md)) ; aucune table « polymorphe » où un champ `type` suffirait à basculer d'une catégorie à l'autre ; aucune opération de conversion. |
| API proposée | routes séparées `/api/optical/prescriptions` et `/api/optical/measurements` ; le dossier client agrège en lecture en conservant le type de chaque élément ([`API_OPTICAL_PROPOSED.md`](API_OPTICAL_PROPOSED.md)). |
| Interface | choix explicite du type avant toute saisie ; badge de type sur chaque ligne, détail et formulaire ; deux sections dans le dossier optique de la fiche ([`PARCOURS_04.md`](PARCOURS_04.md)). |
| Ventes (futur) | une vente référencera `(type, identifiant, numéro de révision)` ; une mesure magasin n'est jamais présentée comme ordonnance éligible à une prise en charge ([`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 7). |
| Audit | codes d'action distincts par type (`prescription.*`, `measurement.*`) ; aucune valeur optique dans `audit.events`. |

## Demande consolidée au propriétaire — D-04-02 à D-04-09

Une seule réponse est attendue, par groupe (formulaire en fin de document). Pour chaque question : options, recommandation de cadrage, et dépendance éventuelle à un avis externe. Une recommandation n'est **pas** une décision.

---

### Groupe A — Provenance et pièces source (D-04-03)

**D-04-03.a — Provenance d'une ordonnance médicale.**

| Champ proposé | Rôle | Proposition |
|---|---|---|
| Prescripteur — nom | tel qu'il figure sur le document | texte libre ; facultatif au brouillon, **obligatoire à la confirmation** (option A) ou toujours facultatif (option B) |
| Prescripteur — qualité | ophtalmologue, médecin, autre professionnel… | liste fermée **[AVIS OPTICIEN] [CONSEIL JURIDIQUE]** : MMV ne présume pas qui peut prescrire dans chaque pays ; option « non précisée » toujours possible |
| Prescripteur — identifiant professionnel | numéro figurant sur le document | texte libre facultatif, **aucun contrôle de format** ni rattachement à un registre (aucune présomption INAMI / ordre marocain) |
| Date de prescription (`prescribed_on`) | date portée sur le document | date civile ; obligatoire à la confirmation ; **ne pas** calculer de validité |
| Date de réception (`received_on`) | date où le magasin a reçu le document | date civile, facultative, distincte de la précédente |
| Saisie (`recorded_at_utc`, auteur) | trace technique | automatique, compte connecté ; jamais saisie à la main |
| Établissement du prescripteur | — | **non proposé** en 04 (pas de besoin démontré) |

Recommandation : **option A** (nom du prescripteur et date de prescription exigés pour confirmer, libres au brouillon).

**D-04-03.b — Provenance d'une mesure magasin.**

| Champ proposé | Proposition |
|---|---|
| Date de la mesure | date civile, obligatoire |
| Collaborateur ayant mesuré | compte MMV **désigné parmi les comptes habilités** à mesurer (option A) ; ou toujours le compte qui saisit (option B) — recommandation A, l'auteur de la saisie restant tracé à part |
| Catégorie de mesure | liste fermée **[AVIS OPTICIEN]** ; exemples de catégories à confirmer ou rejeter : réfraction réalisée en magasin, autre relevé ; la lecture d'un équipement existant relève du backlog `MMV-N-050` et **n'est pas** proposée en 04 |
| Méthode / appareil | texte libre facultatif (option A) ou absent en 04 (option B) — recommandation B (intégration d'appareils hors périmètre) |
| Finalité / usage | aucun champ ; une mesure magasin n'ouvre aucun droit de prise en charge (D-04-01) |

**D-04-03.c — Pièce source (document reçu).**

| Question | Options | Recommandation |
|---|---|---|
| Pièce obligatoire avant confirmation ? | oui / non, mais mention « sans pièce source » visible | **non**, mention visible |
| Nombre de pièces | 0..1 / 0..n | **0..n** (recto / verso) |
| Formats | PDF, JPEG, PNG / plus large | **PDF, JPEG, PNG** ; contrôle par signature du contenu, pas seulement par extension |
| Taille maximale par pièce | à fixer | **10 Mo** proposé, à confirmer |
| Stockage | (1) table PostgreSQL dédiée (`bytea`), sauvegardée avec la base ; (2) fichiers sur le serveur, chemin jamais exposé | **(1)** : cohérence avec la sauvegarde PostgreSQL (ADR-008), aucun lien de fichier, contrôle d'accès unique — impact sur la taille de la base à mesurer (O-04-02) |
| Analyse antivirus | aucun outil local défini | **à décider** ; à défaut : formats restreints, aucune exécution, affichage dans le poste seulement |
| Numérisation directe (scanner), OCR | — | **hors 04** (cadrage § 5) |
| Pièce jointe à une mesure magasin | oui / non | **non en 04** |

**D-04-03.d — Lien entre une mesure magasin et une ordonnance.** Options : (A) aucun lien en 04, les deux coexistent dans le dossier ; (B) lien facultatif explicite « mesure réalisée en lien avec l'ordonnance X », sans effet sur ses valeurs. Recommandation : **A** (B reportée, à revoir avec le module Ventes).

---

### Groupe B — Qualité des données optiques (D-04-02, D-04-06) — **[AVIS OPTICIEN] requis sur tout le groupe**

**D-04-02.a — Statuts d'un enregistrement.**
Proposition : `brouillon` (sauvegardable incomplet) → `confirmé` (transcription vérifiée par un utilisateur habilité, ou mesure validée par son auteur) ; `invalidé` (D-04-04). Seul un enregistrement `confirmé` et non invalidé pourra être proposé au futur module Ventes. « Confirmé » ne signifie **jamais** « validé médicalement ».

**D-04-02.b — Critère minimal de confirmation.** Options : (A) au moins un œil renseigné + provenance minimale (D-04-03.a/b) ; (B) les deux yeux renseignés ou explicitement marqués ; (C) critère fourni par l'opticien. Recommandation : **B**, chaque œil ayant un état explicite (D-04-02.c).

**D-04-02.c — Un seul œil et valeurs absentes.** Proposition : état par œil parmi `renseigné`, `non prescrit sur le document` (ordonnance) / `non mesuré` (mesure), `non transcrit` (brouillon uniquement). Une valeur absente n'est **jamais** convertie en 0,00 ; « plan » se saisit explicitement comme valeur.

**D-04-02.d — Qui confirme ?** Options : (A) l'utilisateur qui transcrit peut confirmer ; (B) confirmation par un second utilisateur habilité (« quatre yeux »). Recommandation : **A** en 04, B réévaluée avant pilote.

**D-04-06 — Validations par grandeur.** Aucune borne ni règle n'est proposée comme acquise. Pour chaque ligne : la proposition de structure, et la question à trancher.

| Grandeur | Proposition de structure | À trancher **[AVIS OPTICIEN]** |
|---|---|---|
| Sphère | décimal exact (2 décimales), signe obligatoire et affiché (`+`/`−`) | bornes min/max ; pas imposé ou non (D-04-06.a) |
| Cylindre | idem ; transcrit **avec le signe du document** ; aucune transposition (hors 04) | bornes ; acceptation des deux conventions de signe sur un même dossier |
| Axe | entier en degrés | plage acceptée (0–180 ou 1–180) ; traitement du 0 (D-04-06.b) ; axe requis si cylindre ≠ 0 ; axe vide et désactivé si cylindre vide ou nul |
| Addition | décimal positif, par œil | valeurs OD/OG différentes admises ou non ; bornes |
| Prisme | décimal positif ; **base requise** si prisme > 0 | une ou deux composantes par œil (horizontale + verticale) ; notation de la base (D-04-06.c) ; bornes |
| Acuité visuelle | texte court tel qu'écrit (notations variables) | conservée ou non ; par œil et/ou binoculaire |
| Vision de près / intermédiaire | non proposée en 04 hors addition | besoin d'un bloc distinct de valeurs de près |
| Écart pupillaire, hauteurs | **hors modèle d'ordonnance** (sous-domaine équipement, cadrage § 3) | confirmer l'exclusion en 04 |
| Contrôles croisés OD/OG | **aucun** contrôle clinique (pas d'alerte d'anisométropie, pas de calcul) | confirmer |

- **D-04-06.a — Pas de saisie.** Options : (A) pas de 0,25 imposé, toute autre valeur refusée ; (B) toute valeur au centième acceptée, avertissement non bloquant hors pas de 0,25 ; (C) autre pas fourni par l'opticien. Recommandation de cadrage : **B** (le cadrage interdit d'imposer 0,25 aux données atypiques sans validation métier). Les flèches du clavier peuvent proposer un pas de 0,25 sans contraindre la saisie.
- **D-04-06.b — Axe à 0°.** La maquette illustre une normalisation 0° → 180° **non approuvée**. Options : (A) enregistrer tel que transcrit, 0 et 180 acceptés, aucune normalisation ; (B) refuser 0 et demander 180 ; (C) proposer la conversion avec confirmation explicite. Recommandation : **A** — aucune modification silencieuse d'une valeur clinique (cadrage § 6.2).
- **D-04-06.c — Notation de la base du prisme.** Options : (A) direction nasale / temporale / supérieure / inférieure (comme la maquette) ; (B) angle en degrés (0–360) ; (C) les deux, au choix de la transcription, sans conversion. Recommandation : **C limitée** si l'opticien confirme que les deux notations se rencontrent sur les documents reçus ; sinon A.
- **D-04-06.d — Mesures magasin.** Proposition : même structure de valeurs que l'ordonnance (comparabilité à l'écran), règles éventuellement différentes ; type et provenance restent distincts.

---

### Groupe C — Cycle de vie (D-04-04, D-04-07, D-04-08)

**D-04-04 — Rectification, versions, invalidation.**

| Question | Options | Recommandation |
|---|---|---|
| a. Modification d'un brouillon | en place, auditée, sans version / avec version | **en place, auditée** |
| b. Modification d'un enregistrement confirmé | en place / **nouvelle révision immuable** avec motif | **nouvelle révision**, motif obligatoire (liste : erreur de transcription, complément d'information, autre + commentaire) ; la révision repasse par la confirmation |
| c. Suppression | suppression physique (maquette) / **invalidation** | **aucune suppression physique par l'utilisateur** ; invalidation motivée (saisie erronée, doublon, document non recevable, autre), enregistrement conservé et visible comme invalidé ; le droit d'effacement légal sera une procédure distincte (D-04-09) |
| d. Brouillon jamais confirmé | suppression autorisée / invalidation | **invalidation** (une seule règle, traçable) |
| e. Nouvelle ordonnance du même prescripteur | révision / **nouvel enregistrement** | **nouvel enregistrement** : rectifier une transcription ≠ recevoir une nouvelle prescription |
| f. Comparaison de versions | toutes les révisions visibles avec la lecture optique / restreintes | **toutes visibles** avec `optical.read` ; comparaison champ à champ à l'écran (aucune valeur dans l'audit) |
| g. Référence par une vente (futur) | — | la vente référencera une révision précise, figée ; une invalidation ultérieure n'altère pas la vente mais l'affiche (règle détaillée avec le module Ventes) |

**D-04-07 — Client archivé.** Proposition : consultation autorisée (avec `optical.read`) ; création **refusée** pour les deux types (« Réactivez le client avant d'ajouter… ») ; rectification : options (A) refusée, (B) autorisée. Recommandation : **A** (refusée), invalidation **autorisée** (corriger une erreur reste possible).

**D-04-08 — Destinations et navigation.**

| Question | Recommandation |
|---|---|
| a. Accès principal | onglet **« Dossier optique »** de la fiche client (remplace l'onglet « Ordonnances » reporté de la Phase 03), deux sections « Ordonnances médicales » / « Mesures magasin » |
| b. Liste globale (menu « Ordonnances », aujourd'hui « phase suivante ») | activée en 04 sous le libellé **« Dossiers optiques »**, filtre de type, mêmes droits ; options : activer en 04 (recommandé) ou reporter à 04-D |
| c. Après création | détail de l'enregistrement, lien « Retour à la fiche » ; **pas** d'action principale « Créer une vente » (contrairement à la maquette) |
| d. « Préparer une vente » | visible mais **désactivée**, mention « Phase ultérieure » ; aucun contexte transmis, aucun brouillon de vente créé |
| e. Retour | retour à la fiche avec l'onglet « Dossier optique » ouvert, sans nouvelle recherche client |

---

### Groupe D — Accès, confidentialité et pays (D-04-05, D-04-09)

**D-04-05 — Permissions.** Détail et matrice : [`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md).

| Question | Options | Recommandation |
|---|---|---|
| a. Découpage | (A) 4 droits du cadrage : `optical.read`, `optical.write`, `optical.invalidate`, `optical.document.read` ; (B) 6 droits séparant les deux types et les pièces | **B** : `optical.read`, `optical.prescriptions.write`, `optical.measurements.write`, `optical.invalidate`, `optical.documents.read`, `optical.documents.write` — la mesure en magasin et la transcription d'un document relèvent d'habilitations différentes (D-04-01) |
| b. Dépendance à `clients.read` | — | toute route optique exige **aussi** `clients.read` (le dossier s'ouvre depuis la fiche ; aucune fuite d'identité par la liste optique) |
| c. Historique (`audit.read`) | événements optiques visibles avec `audit.read` seul / avec `audit.read` **et** `optical.read` | **les deux droits** : l'existence même d'une ordonnance est une donnée de santé |
| d. Journal de consultation | aucun / pièces seulement / pièces et détails | **pièces seulement** (`document.viewed`), à réévaluer avec D-04-09 |
| e. Rôles visuels (vendeur, accueil, technicien) | droits par défaut / aucun droit par défaut | **aucun droit par défaut** ; attribution individuelle par `MMV.Maintenance permissions grant` |

**D-04-09 — Pays et confidentialité — [CONSEIL JURIDIQUE].**

| Sujet | Proposition | État |
|---|---|---|
| a. Qualification | ordonnances et mesures = **données relatives à la santé** dans les deux pays (Belgique : RGPD art. 9, APD ; Maroc : loi 09-08, données sensibles, CNDP) | à confirmer par le conseil |
| b. Formalités | Maroc : déclaration / autorisation CNDP à vérifier ; Belgique : registre des traitements, analyse d'impact éventuelle | **bloquant pour un pilote réel** (G04-LEGAL), non bloquant pour 04-A/04-B sur données fictives |
| c. Durées de conservation | aucune purge en 04 ; durées à fixer (prolonge D-03-22) | OUVERT |
| d. Chiffrement au repos | chiffrement du disque du serveur et des sauvegardes comme exigence d'installation (ADR-008) ; chiffrement applicatif par colonne reporté | à valider |
| e. Données propres à un pays | aucun code organisme, numéro administratif, modèle INAMI, présomption AMO / CNSS / CNOPS dans les tables optiques communes ; paramètres de magasin / pays ; prise en charge = futur domaine | proposé |
| f. Validité d'une ordonnance | **aucune durée calculée** ni alerte d'expiration en 04 (aucune règle nationale validée) | proposé |
| g. Impression / export | **hors 04** | proposé |

---

## Formulaire de réponse (une seule réponse consolidée)

Indiquer pour chaque ligne : `OK` (recommandation retenue), la lettre d'une autre option, ou une consigne. Les lignes [AVIS OPTICIEN] peuvent être répondues « en attente d'avis » : elles bloquent alors les sous-lots qui en dépendent (voir [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md)), pas le reste.

| Réf. | Sujet | Recommandation | Avis externe | Réponse du propriétaire |
|---|---|---|---|---|
| D-04-03.a | provenance ordonnance | A | opticien (qualité du prescripteur), juridique | |
| D-04-03.b | provenance mesure | A (collaborateur habilité) ; méthode absente | opticien (catégories) | |
| D-04-03.c | pièces source | facultative, 0..n, PDF/JPEG/PNG, 10 Mo, PostgreSQL | — | |
| D-04-03.d | lien mesure ↔ ordonnance | A (aucun lien) | — | |
| D-04-02.a–d | statuts, confirmation, un seul œil | brouillon / confirmé / invalidé ; B ; états par œil ; A | opticien | |
| D-04-06 (tableau) | bornes, dépendances | aucune borne fixée sans opticien | **opticien** | |
| D-04-06.a | pas de saisie | B | opticien | |
| D-04-06.b | axe à 0° | A (tel que transcrit) | opticien | |
| D-04-06.c | base du prisme | A ou C selon les documents reçus | opticien | |
| D-04-06.d | valeurs des mesures | même structure, type distinct | opticien | |
| D-04-04.a–g | versions, invalidation | voir tableau | — | |
| D-04-07 | client archivé | création et rectification refusées, invalidation permise | — | |
| D-04-08.a–e | navigation | onglet « Dossier optique », liste globale en 04, vente inerte | — | |
| D-04-05.a–e | permissions | B, + `clients.read`, `audit.read` + `optical.read`, journal des pièces, aucun droit par défaut | — | |
| D-04-09.a–g | pays, confidentialité | voir tableau | **juridique** | |

*Aucune ligne de ce formulaire n'est pré-remplie : l'absence de réponse vaut `PROPOSED`, jamais `APPROVED`.*

---

## Complément daté — 19/10/2026 (suite de la revue indépendante de 04-00)

Mandat : [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md). La revue indépendante qualifie le cadrage 04-00 de **« techniquement acceptable »** (G04-00) ; D-04-01 reste **APPROVED**, définitive ; **D-04-02 à D-04-09 restent `PROPOSED`**, inchangées (le texte ci-dessus n'est pas modifié). Cinq questions nouvelles, issues des études préalables au modèle ([`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md)), s'ajoutent à la même réponse consolidée. Elles sont **`PROPOSED`** et ne touchent à aucune règle clinique.

| Réf. | Sujet | Recommandation | Avis externe | Réponse du propriétaire |
|---|---|---|---|---|
| D-04-10.a–d | rattachement historisé des pièces aux révisions (E-01) | B (pièce de l'en-tête + lien par révision figé à la confirmation) ; brouillon initialisé avec les liens ; pièce invalidée servie avec avertissement ; doublon refusé | — | |
| D-04-11.a–f | révision confirmée et rectification en brouillon (E-02) | cycle de l'enregistrement séparé des révisions ; numéro à la confirmation ; brouillon partagé ; abandon `discarded` conservé ; abandon du brouillon dans la transaction d'invalidation ; brouillon visible avec `optical.read` | — | |
| D-04-12.a–d | événements d'accès aux pièces (E-03) | `document.content_served`, libellés « transmis » ; aucun préchargement ni vignette ; lectures de dossiers : pièces seulement en 04, décision avant pilote ; refus non journalisés | juridique (avant pilote, c) | |
| D-04-13.a–c | granularité d'`optical.read`, profils (E-04) | `optical.read` unique ; listes sans résumé OD/OG (minimisation) ; matrice indicative des profils à valider compte par compte | — | |
| D-04-14.a–g | contrôles des fichiers, limites, sauvegardes (E-05) | liste blanche par signature ; 10 Mo et 10 pièces actives ; marqueurs PDF actifs refusés ; pièce conservée telle que reçue ; antivirus A puis B étudiée ; seuil d'alerte 20 Go ; volumes réels à fournir | — | |

*Comme pour le formulaire principal, aucune ligne n'est pré-remplie : l'absence de réponse vaut `PROPOSED`.*

---

## Note datée — 19/10/2026 (04-A réalisé, UI ONLY)

Aucune réponse n'a été reçue : **D-04-02 à D-04-14 restent `PROPOSED`**, les formulaires ci-dessus restent vierges. Le prototype 04-A les illustre sans les trancher (banc isolé, données fictives). L'**examen métier consolidé avec un opticien** (D-04-02.b/c, D-04-06 a–d, listes fermées de D-04-03.a/b) peut se tenir **sur le banc** : ordre du jour et points à observer dans [`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 9. Les règles et la migration `0006` ne seront figées qu'après cet examen (G04-METIER).

## Décision datée — 20/10/2026 : D-04-16 APPROVED (simplification du dossier optique)

Mandat : [`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md). **D-04-16 — APPROVED (propriétaire, 20/10/2026)** : *« Je conserve le système de révisions, mais je demande la suppression du champ « État de l'œil » et le report de la gestion des « Pièces sources ». »* Objectif : simplifier le parcours quotidien de l'opticien sans compromettre la traçabilité des corrections. Les sections précédentes sont **conservées telles qu'écrites** (historique des choix) ; leur effet est le suivant.

| Réf. | Effet de D-04-16 | État après le 20/10/2026 |
|---|---|---|
| D-04-02.c | l'**état par œil** (`renseigné` / `non prescrit sur le document` / `non mesuré` / `non transcrit`) est **retiré** des formulaires, de la validation du prototype et du modèle proposé. Reste acquis le principe « une valeur absente n'est jamais convertie en 0,00 » : chaque champ optique peut rester vide ; « plan » se saisit explicitement | partie « état » **RETIRÉE** ; principe « absence ≠ 0 » maintenu (déjà appliqué) |
| D-04-02.b | **non modifiée** (règle clinique ouverte, consigne 8). Sa recommandation B renvoyait à l'état par œil : elle devra être reformulée (par exemple au regard des valeurs elles-mêmes) — aucune reformulation n'est proposée ici | **PROPOSED** [AVIS OPTICIEN] ; O-04-30 |
| D-04-02.a, d | inchangées | PROPOSED |
| D-04-03.a, b | inchangées ; prescripteur (nom, qualité, identifiant), date de prescription, date de réception, et provenance de la mesure **conservés** | PROPOSED [AVIS OPTICIEN] |
| D-04-03.c | pièce source : **reportée** au backlog permanent **BL-04-01** (gestion documentaire) ; aucune table ni API en Phase 04 ; aucune mention « sans pièce source » | **REPORTÉE** (hors Phase 04) |
| D-04-04 | inchangée : révisions immuables, motif de rectification, invalidation motivée, comparaison ; motif d'invalidation « document non recevable » conservé (il qualifie l'ordonnance reçue, O-04-32) | PROPOSED |
| D-04-05 | option B réduite à **4 droits** : `optical.read`, `optical.prescriptions.write`, `optical.measurements.write`, `optical.invalidate` ; `optical.documents.read` / `.write` reportés avec BL-04-01 ; d (« journal des pièces seulement ») sans objet en 04 | PROPOSED |
| D-04-06 | **non modifiée** (bornes, pas, axe, prisme / base, acuité) | PROPOSED [AVIS OPTICIEN] |
| D-04-10 | rattachement historisé des pièces aux révisions : **reporté** avec BL-04-01 (étude E-01 conservée pour la reprise) | **REPORTÉE** |
| D-04-11 | inchangée : révision confirmée et rectification en brouillon, numéro attribué à la confirmation, abandon conservé | PROPOSED |
| D-04-12 | a, b, d (événements d'accès aux pièces) **reportées** avec BL-04-01 ; **c** (journalisation des lectures de dossiers, avant pilote, G04-LEGAL) maintenue | a, b, d **REPORTÉES** ; c PROPOSED |
| D-04-13 | inchangée ; la mention « métadonnées des pièces » d'`optical.read` est sans objet | PROPOSED |
| D-04-14 | contrôles de fichiers, limites, volumes de pièces : **reportés** avec BL-04-01 (étude E-05 conservée) | **REPORTÉE** |
| D-04-01, D-04-15 | inchangées : deux types distincts ; maquette Lot 3 avec boutons ± | APPROVED |

**Formulaire de réponse.** Les lignes D-04-03.c, D-04-10, D-04-12.a/b/d et D-04-14 des formulaires ci-dessus **n'appellent plus de réponse en Phase 04** (elles seront reposées à la reprise de BL-04-01). Les autres lignes restent attendues ; D-04-02.a–d se lit désormais sans état par œil.
