# 04 — Préparation du sous-lot 04-A (interface sur double de test)

**Statut : `PROPOSED — OWNER_GO_04A_REQUIRED`** (19/10/2026). Mandat : [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md). Ce document **prépare** 04-A ; aucun écran n'est construit, aucune ligne d'`apps/desktop/src/` n'est modifiée. Le démarrage de 04-A attend un GO explicite du propriétaire sur le périmètre ci-dessous (§ 3).

Sources : composants V2 existants ([`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 3), maquette Lot 3 DRAFT (36 états, [`STATE_MAP_36.md`](STATE_MAP_36.md)), parcours [`PARCOURS_04.md`](PARCOURS_04.md), études [`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md). Aucune lecture de l'ancien dépôt MMV ; les notes de la maquette qui en proviennent restent ignorées (D-000).

## 1. Bornes du sous-lot

| Autorisé en 04-A (sous GO) | Interdit |
|---|---|
| écrans, composants, styles et tests de l'interface dans `apps/desktop/src/optical/` ; extension du Shell et de la fiche client | SQL, migration, connexion à `mmv_v2_dev` / `mmv_v2_test` |
| double de test `apps/desktop/src/test/fakeOpticalApi.ts` (serveur `node:http` en mémoire, comme les doubles Clients / Rendez-vous / Audit) | toute route `/api/optical` dans MMV.Api ; toute permission `optical.*` dans `Permissions.cs` ou en base |
| types TypeScript **provisoires** du contrat proposé (`api/optical.ts`, marqués prototype) | toute donnée de santé réelle ; tout jeu de données fictif hors mémoire des tests |
| captures d'écran du double de test pour la validation visuelle | modification de la CSP du poste (O-04-01, reportée en 04-C avec preuve native) |
| — | ancien dépôt MMV ; push, PR, merge |

## 2. Garantie « service non activé »

1. **Aucun serveur réel** : MMV.Api n'expose aucune route `/api/optical` (04-B non commencé).
2. **Aucun droit possible** : `optical.read` n'existe ni dans `Permissions.Grantable` ni dans la contrainte `ck_user_permissions_permission` (8 permissions, [`../03/sql/0005_AuditJournal.sql`](../03/sql/0005_AuditJournal.sql)) ; aucun compte réel ne peut le détenir.
3. **Affichage conditionné** : l'onglet « Dossier optique » et l'entrée « Dossiers optiques » ne sont montés que pour une session portant `optical.read` — donc, en 04-A, uniquement face au double de test. Module chargé à la demande (O-04-10).
4. **Aucun changement visible pour les comptes réels** (proposition O-04-16) : sans `optical.read`, l'onglet reporté « Ordonnances — phase ultérieure » et l'entrée de menu `later` restent **tels qu'aujourd'hui** jusqu'à l'activation en 04-C ; leur remplacement (onglet absent, entrée masquée ou « accès non autorisé ») est décidé et testé à ce moment.
5. **Données fictives** : identités et valeurs manifestement fictives (`Fictif-…`), en mémoire du processus de test seulement, jamais écrites dans une base ni un fichier versionné.

## 3. Périmètre 04-A et dépendance aux décisions

Classement : **IND** = indépendant de toute décision ouverte ; **PROTO** = prototype réversible bâti sur une recommandation `PROPOSED` non clinique, libellé comme tel, sans effet serveur ; **BLOQUÉ** = dépend d'une décision clinique ou d'une liste à fournir par un opticien — aucune règle n'est inventée, l'élément reste neutre.

| # | Élément demandé | Classement | Décisions concernées | Traitement en 04-A |
|---|---|---|---|---|
| 1 | Onglet « Dossier optique » de la fiche, deux sections RX / MES, vide par section (GAP-04-04, états 5, 7) | IND | D-04-01 | complet |
| 2 | Choix explicite « Ordonnance médicale » / « Mesure magasin » avant toute saisie (GAP-04-01) | IND | D-04-01 | complet ; chaque choix visible selon le droit d'écriture du type (PROTO, D-04-05) |
| 3 | Liste globale « Dossiers optiques » : colonnes, type, statut, pagination serveur, recherche client / prescripteur, filtres, invalidés exclus par défaut (états 1–4, 6, 8, 9) | IND (structure) / PROTO (activation en 04, D-04-08.b ; résumé OD/OG, D-04-13.b) | D-04-08.b, D-04-13.b | colonne de résumé OD/OG **commutée** par une option, sans valeur par défaut figée |
| 4 | Listes par client (onglet) | IND | — | complet |
| 5 | Sélection du client depuis la liste globale (`ClientPicker`, clients actifs) (états 12, 13) | IND | D-04-07 (archivé : PROTO) | complet |
| 6 | Formulaire « Ordonnance médicale » : provenance (prescripteur, qualité, identifiant, dates de prescription / réception distinctes), notes (états 10, 11) | PROTO | D-04-03.a | champs **facultatifs** à l'écran ; caractère obligatoire à la confirmation affiché seulement si renvoyé par le serveur (`422`) ; liste « qualité du prescripteur » = **BLOQUÉ** → valeur unique « Non précisée » + mention « liste à fournir » |
| 7 | Formulaire « Mesure magasin » : date, collaborateur ayant mesuré, catégorie (GAP-04-02) | PROTO | D-04-03.b | collaborateur choisi parmi les comptes fournis par le double ; catégorie = **BLOQUÉ** → « Non précisée » + « liste à fournir » |
| 8 | Grille OD / OG (§ 5) | IND (structure) / **BLOQUÉ** (règles) | D-04-06, D-04-02.c | grille **neutre** : saisie conservée telle quelle, unités visibles, aucune borne, aucun pas imposé, aucune dépendance entre champs, aucun champ désactivé, aucune normalisation |
| 9 | États par œil (renseigné / non prescrit / non transcrit) | PROTO | D-04-02.c [AVIS OPTICIEN] | sélecteur à trois états libellé « proposition » ; absence jamais affichée « 0,00 » |
| 10 | Enregistrement en brouillon, confirmation (états 14, 16, GAP-04-10) | PROTO | D-04-02.a/b | critère de complétude **non codé** côté poste : le refus `422 incomplete` du double est affiché tel quel |
| 11 | Erreurs de champ (états 17, 24, 35) | IND (mécanisme) / BLOQUÉ (règles) | D-04-06 | affichage sous le champ des seules erreurs renvoyées par le serveur ; aucune règle locale ; le double ne produit que des erreurs de forme (« nombre attendu », longueur) |
| 12 | Détail RX / MES : badges, statut, provenance, OD / OG, notes (états 19–22, GAP-04-03, GAP-04-11) | IND / PROTO (statuts) | D-04-02.a, D-04-11 | complet |
| 13 | Lecture seule déduite des droits (état 22) | PROTO | D-04-05 | complet (affichage, jamais autorisation) |
| 14 | Révisions : liste, révision figée, comparaison champ à champ, rectification avec motif, rectification en cours (états 23, 27, GAP-04-07, GAP-04-13) | PROTO | D-04-04, D-04-11 | prototype sur le modèle E-02 ; liste de motifs = proposition D-04-04.b |
| 15 | Pièces source : liste par révision, ajout, refus de format / taille, aperçu, accès refusé, pièce invalidée (GAP-04-06, GAP-04-14) | PROTO | D-04-03.c, D-04-10, D-04-12, D-04-14 | aperçu **image** seulement dans le navigateur de test ; aperçu PDF et ouverture de CSP en 04-C (O-04-01) ; libellé « transmis », jamais « consulté » |
| 16 | Invalidation (menu secondaire, motif, bandeau) (états 28–31, GAP-04-08) | PROTO | D-04-04.c | complet ; aucune action « Supprimer » |
| 17 | Client archivé : refus de création (état 18) | PROTO | D-04-07 | complet |
| 18 | États vide, chargement, erreur, aucun résultat, accès refusé (états 6–9, GAP-04-05) | IND | — | complet |
| 19 | Conflit de version deux postes (GAP-04-09) | IND | — | complet (saisie conservée, rechargement proposé) |
| 20 | Retour à la fiche avec l'onglet « Dossier optique » rouvert, sans nouvelle recherche (J-04-01 étape 7) | IND | D-04-08.e | complet (mécanisme `returnTo` existant, [`../../../apps/desktop/src/appointments/appointmentLaunch.ts`](../../../apps/desktop/src/appointments/appointmentLaunch.ts)) |
| 21 | « Préparer une vente » désactivée « Phase ultérieure » (GAP-04-12) | IND | D-04-08.d | complet, inerte |
| 22 | Garde de sortie, clavier, focus, fenêtre réduite, thèmes (états 26, 32–35) | IND | — | complet ; ordre de Tab exact (état 33) ajusté après D-04-03 / D-04-06 |
| 23 | Libellés des événements optiques dans l'onglet « Historique » | PROTO | D-04-05.c, D-04-11, D-04-12 | libellés seulement, servis par le double ; filtrage serveur en 04-B |
| — | Normalisation d'axe (état 25), champs désactivés (état 36), bornes, pas, axe requis, base du prisme | **BLOQUÉ** | D-04-06 [AVIS OPTICIEN] | **non réalisés** : l'état 25 est remplacé par « axe affiché tel que transcrit » ; l'état 36 n'est pas implémenté |

**Synthèse.** Réellement indépendants des décisions cliniques : éléments 1–5, 8 (structure), 11 (mécanisme), 12, 18–22 et la mécanique de 13–17, 23. Les prototypes (`PROTO`) ne dépendent d'aucune règle clinique, seulement de propositions de gestion encore `PROPOSED` (cycle de vie, révisions, pièces, permissions, navigation) ; ils sont réversibles car aucun serveur, aucune donnée ni aucun droit réel n'en dépend. **Rien de clinique n'est codé** : bornes, pas, dépendances OD / OG, axe, prisme, critère de complétude, listes de qualités et de catégories restent ouverts.

## 4. Composition des états complémentaires (G04-DESIGN, voie acceptée par le propriétaire)

Le mandat autorise à concevoir les 12 états absents du Lot 3 à partir des composants existants, **sous réserve de validation visuelle du propriétaire**. Deux états nouveaux découlent des études (GAP-04-13, GAP-04-14) et suivent la même voie. Aucun n'est déclaré conçu ni approuvé avant les captures.

| Réf. | État | Composition proposée (composants V2 existants) |
|---|---|---|
| GAP-04-01 | choix du type | `AlertDialog` à deux options larges (icône Lucide, titre, phrase d'explication : « document reçu d'un prescripteur » / « relevé réalisé au magasin »), boutons `button.tsx` ; focus initial sur la première option, Échap annule |
| GAP-04-02 | formulaire mesure magasin | même gabarit que le formulaire RX du Lot 3 (sections, deux colonnes, `date-picker.tsx`), bloc « Provenance » remplacé par date / collaborateur / catégorie ; badge « Mesure magasin » dans l'en-tête |
| GAP-04-03 | détail mesure magasin | gabarit du détail RX (état 19) ; bloc provenance propre ; aucune zone « pièces » en 04 |
| GAP-04-04 | onglet de la fiche | `tabs.tsx` ; deux sections titrées, chacune avec sa table (`ClientsTable` comme modèle) et son `StateBlock` vide |
| GAP-04-05 | accès refusé | `views/ForbiddenView.tsx` pour l'accès direct ; dans la fiche, onglet absent ; pièces : ligne « N pièce(s) — accès non autorisé » |
| GAP-04-06 | pièces source | liste (type, taille, date, auteur), bouton « Joindre une pièce » (sélecteur de fichier natif), `StateBlock` d'erreur pour format / taille, panneau d'aperçu (`<img>`), notice de transmission |
| GAP-04-07 | révisions et comparaison | liste chronologique (numéro, date, auteur, motif) ; comparaison en deux colonnes « révision n » / « révision m » avec mise en évidence textuelle des seuls champs différents (pas uniquement par la couleur) |
| GAP-04-08 | invalidé | `ClientNoticeBanner` (ton avertissement) : motif, date, auteur ; badge « Invalidé » sur la ligne |
| GAP-04-09 | conflit de version | `ClientNoticeBanner` erreur + action « Recharger » ; saisie conservée (modèle du conflit Clients 03-A) |
| GAP-04-10 | confirmation | `AlertDialog` récapitulant type, client, provenance et états par œil ; « Confirmé » ne signifie pas « validé médicalement » (texte explicite) |
| GAP-04-11 | badges de statut | badge existant des clients (actif / archivé) décliné : Brouillon (neutre), Confirmé (succès), Invalidé (avertissement), Rectification en cours (info) ; texte toujours présent |
| GAP-04-12 | vente inerte | `button.tsx` désactivé + mention « Phase ultérieure » (modèle des entrées `later`) |
| GAP-04-13 | rectification en cours (nouveau, E-02) | bandeau info sur le détail (auteur, début, motif), actions « Poursuivre », « Comparer », « Abandonner » selon les droits |
| GAP-04-14 | pièce invalidée sur une révision (nouveau, E-01) | ligne de pièce barrée **et** libellée « invalidée le … — motif », avertissement avant transmission du contenu |

Validation visuelle : captures du double de test dans Edge sans interface (moteur de WebView2, méthode Q-02-40 / Q-03-91), thèmes clair et sombre, 1440×900 et 1920×1080, présentées au propriétaire. Statut de chaque état après captures : `COMPOSED — OWNER_VISUAL_VALIDATION_REQUIRED`.

## 5. Grille OD / OG neutre (sans validation clinique)

| Aspect | Spécification 04-A |
|---|---|
| Colonnes | Sphère, Cylindre, Axe, Addition, Prisme, Base (libellés du Lot 3) ; Acuité si D-04-06 la retient (affichée en PROTO, champ texte court) |
| Saisie | texte libre conservé **exactement** ; le poste n'arrondit pas, ne complète pas les décimales, n'ajoute pas de signe, ne convertit pas ; virgule ou point acceptés à l'affichage, valeur transmise telle quelle au double |
| Unités | dioptries (δ pour le prisme), degrés pour l'axe, affichées à côté du champ |
| Absence | champ vide = absent ; jamais « 0,00 » ; état par œil explicite (élément 9) |
| Flèches du clavier | **aucun pas** en 04-A (D-04-06.a ouverte) |
| Dépendances | aucune : l'axe n'est ni exigé ni désactivé, la base n'est ni exigée ni désactivée |
| Erreurs | seulement celles renvoyées par le serveur (double), sous le champ, `aria-describedby` |
| Mise en page | OD au-dessus d'OG en fenêtre réduite, aucun défilement horizontal (état 32) ; ordre de Tab OD → OG |
| Affichage | valeurs restituées telles que saisies ; aucun calcul (transposition, équivalent sphérique) |

## 6. Fichiers prévus (04-A, après GO)

| Fichier | Contenu |
|---|---|
| `apps/desktop/src/optical/OpticalModule.tsx` | routes du module (liste globale, détail, formulaires), chargé à la demande |
| `apps/desktop/src/optical/ClientOpticalTab.tsx` | onglet de la fiche |
| `apps/desktop/src/optical/OpticalTypeChooser.tsx` | GAP-04-01 |
| `apps/desktop/src/optical/PrescriptionForm.tsx`, `MeasurementForm.tsx`, `EyeGrid.tsx` | formulaires et grille neutre |
| `apps/desktop/src/optical/OpticalRecordDetail.tsx`, `RevisionsPanel.tsx`, `SourceDocumentsPanel.tsx`, `InvalidateDialog.tsx` | détail, révisions, pièces, invalidation |
| `apps/desktop/src/optical/opticalPermissions.ts`, `opticalQueries.ts`, `opticalLabels.ts` | affichage selon droits, TanStack Query, libellés |
| `apps/desktop/src/api/optical.ts` | types et appels **provisoires** (contrat proposé, sans OpenAPI tant que 04-B n'existe pas) |
| `apps/desktop/src/test/fakeOpticalApi.ts` | double en mémoire ; branché dans `fakeMmvApi.ts` |
| `apps/desktop/src/optical/*.test.tsx` | tests d'interface |
| modifications | `clients/ClientDetailView.tsx` (onglet conditionnel), `shell/navigation.ts` (entrée conditionnelle, O-04-16), `shell/Shell.tsx` (route), `audit/` (libellés), socle CSS commun (O-04-11) |

Aucune dépendance nouvelle (ADR-014) : React Hook Form, Zod, TanStack Query / Table, Radix, react-day-picker, Lucide suffisent.

## 7. Séquence proposée

| Étape | Contenu | Preuve |
|---|---|---|
| A1 | socle CSS commun (O-04-11) ; entrée de menu et onglet conditionnels (O-04-16) ; squelette du double | tests Shell / fiche existants PASS, aucun changement visible sans `optical.read` |
| A2 | onglet, liste globale, listes par client, états vide / chargement / erreur / aucun résultat / accès refusé | tests d'interface |
| A3 | choix du type, formulaires RX / MES, grille neutre, brouillon / confirmation, garde de sortie | tests d'interface, clavier |
| A4 | détail, révisions et comparaison, rectification en cours, invalidation, pièces (prototype image) | tests d'interface |
| A5 | captures pour validation visuelle, parcours J-04-01 / J-04-02 / J-04-04 / J-04-07 / J-04-08 sur le double, documentation | `QA_MATRIX` Q-04-150 à Q-04-158, suites existantes PASS |

## 8. Points encore bloquants

| Point | Bloque | Ne bloque pas |
|---|---|---|
| **GO 04-A** du propriétaire sur le § 3 | le démarrage de 04-A | — |
| O-04-16 (comportement de l'entrée de menu et de l'onglet sans `optical.read` pendant 04-A / 04-B) | étape A1 | le reste de la préparation |
| Validation visuelle des 14 états composés | la qualification « validé » des états GAP | la construction des prototypes |
| D-04-02 à D-04-05, D-04-07, D-04-08, D-04-10 à D-04-14 (ratification) | le passage des éléments `PROTO` à l'état définitif ; **04-B** (G04-METIER) | la construction de 04-A en prototype |
| **D-04-06**, D-04-02.b/c, listes D-04-03 [AVIS OPTICIEN] | toute règle de saisie, l'ordre de Tab définitif, états 16, 17, 21, 25, 33, 36 définitifs | la grille neutre |
| O-04-01 (CSP `blob:`) et O-04-18 (commandes de la visionneuse PDF) | l'aperçu PDF et l'aperçu dans le poste natif | l'aperçu image dans le navigateur de test |
| G04-LEGAL, O-04-08 (rôle SQL), O-04-09 (restauration) | tout pilote sur données réelles | 04-A, 04-B |
