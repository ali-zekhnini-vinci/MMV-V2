# Phase 00 — Périmètre, MVP progressif et ordre des dépendances

## Statut des sources UI

| Référence | Sujet | Groupes design | États visuels métier | Statut |
|---|---|---:|---:|---|
| Lot 1 | Auth, Shell, dashboards des cinq profils | 11 | 22 | DRAFT |
| Lot 2 | Clients, rendez-vous | 21 | 47 | DRAFT |
| Lot 3 | Ordonnances | 17 | 36 | DRAFT |
| Lot 4 | Ventes, paiements | 15 | 31 | DRAFT |
| Lot 5 | Commandes, atelier | 33 | 81 | DRAFT |
| Lot 6 | Catalogue, stocks, fournisseurs | 37 | 107 | DRAFT |
| **Total** | | **134** | **324** | Pas d'approbation présumée |

Les sept `data-mount` supplémentaires des `.dc.html` sont de la documentation/notes, hors inventaire métier. Les **153 nouvelles vues + 23 compléments** sont reportés. Cette décision ne supprime ni leur backlog ni les fonctionnalités techniques nécessaires aux lots à implémenter.

## Démarrage minimal pour réduire le risque

- **Phase 00 (maintenant) :** cadrage, gouvernance, ADR et plan POC, sans code.
- **Phase 01 (après GO) :** initialisation du dépôt **neuf**, dev tools et Graphify/Serena, builds Tauri/.NET ; un slice minimal client ↔ API ↔ PostgreSQL réel ; réseau sécurisé et démarrage Windows Service en environnement isolé. Pas de vraie vente.
- **Phase 02 :** Shell/Login/designtokens, statut serveur, bootstrap administrateur, auth et permissions minimales sur un backend neuf ; captures visuelles aux résolutions définies.
- **Phase 03 :** clients et rendez-vous ; validations et modèle de données nouveaux avant implémentation.
- **Phase 04 :** ordonnances, dossiers optiques, confidentialité et permissions spécifiques.
- **Phase 05 :** catalogue/fournisseurs/stock minimal à définir ; **à livrer avant le premier encaissement réel**.
- **Phase 06 :** vente/paiement avec ordre de numérotation, transactions et idempotence ; atelier ensuite selon parcours métier ratifié.
- **Phases suivantes :** commandes, atelier, impression, retours, inventaires, reporting, administration avancée et écrans complémentaires.

> Cette séquence **est une recommandation de dépendances**, pas une renumérotation automatique des `docs/phase/01..16` hérités du dossier maître. Les phases 01..16 doivent être réécrites dans le **nouveau** référentiel avant leurs GO ; ne pas conserver leurs références au logiciel précédent ni leurs hypothèses métier.

## Dépendances métier à fixer avant les lots concernés

```text
Identité & rôles ──> consultation clients / données optiques
Magasin / paramètres ──> numérotation & fiscalité
Client ──> dossier optique / ordonnance / rendez-vous
Catalogue & disponibilités ──> panier de vente
Stock + transaction serveur ──> vente finalisée, mouvements
Vente finalisée + règles optiques ──> commande client / fabrication
Commande / atelier ──> QC ──> retrait / remise
Fournisseur + catalogue ──> commande fournisseur (phase complémentaire)
Sauvegarde & audit = transversal dès le socle
```

## Politique de backlog

- `DEFERRED`: 153 vues nouvelles (`MMV-N-001..153`) et 23 compléments ; aucune génération automatique de toutes les pages.
- `FOUNDATION_MUST`: identités/permissions **serveur**, health monitoring, configuration réseau/TLS, mécanisme de backup, transactions et idempotence ; l'UI d'administration détaillée peut attendre.
- `MVP_DECISION`: distinguer **premier environnement technique qui tourne** et **produit prêt à exploiter des ventes réelles**. Un login et un tableau de bord fonctionnels ne sont pas un MVP commercial.
- `NEW_RULES_ONLY`: aucune catégorie, propriété, autorisation ni entité C# anciennement existante ne peut justifier un choix V2.

## Critères minimum de préparation par écran/livraison

1. ID du board source et état `DRAFT`/`APPROVED`.
2. Cas d'utilisation et droit serveur définis ; état normal/chargement/vide/erreur/conflit.
3. Contrat API documenté ou explicitement non nécessaire.
4. Persistance ou absence de persistance explicite.
5. Validation métier neuve ratifiée, tests prévus et cas dégradés.
6. Comparaison visuelle capturée, tests clavier, résolutions 1440×900 et 1920×1080.
7. Revue humaine et merge seulement après approbation.

> Note datée 15/10/2026 (soir) : l'objectif « slice minimal client ↔ API ↔ PostgreSQL réel » de la phase 01 est démontré en **développement local monoposte** (01A merged, 01B en revue propriétaire). Les éléments « réseau sécurisé » (TLS LAN, ADR-006 OPEN) et « démarrage Windows Service » ne sont **pas** réalisés : leur placement (avant ou pendant la phase 02) est une décision du propriétaire (O-B12, `docs/phase/01b/GATES_AND_DECISIONS.md`). Le périmètre de la phase 02 n'est pas approuvé par cette note.

> Note datée 16/10/2026 (phase 02 `IN_PROGRESS`) : 01B est mergé dans `main` (`c0c3d6b`). Le Shell de la phase 02 affiche un accueil sobre sans chiffre ; les **cinq tableaux de bord riches du Lot 1** (un par profil visuel) restent au backlog (`DEFERRED`), comme la recherche globale et le panneau de notifications (contrôles visibles mais désactivés). Les entrées de menu des modules métier indiquent « Disponible dans une phase suivante ». Voir `docs/phase/02/GATES_AND_DECISIONS.md` (O-02-07, O-02-08).

> Note datée 16/10/2026 (phase 02, corrections finales) : la pile frontend des écrans est en place (Tailwind CSS 4, shadcn/ui + Radix, React Hook Form + Zod, React Router, polices locales). **Backlog technique** rattaché aux lots qui en auront l'usage réel, non installé en phase 02 : TanStack Query (premier écran de données serveur), TanStack Table (premières listes), Sonner (notifications d'opération), FullCalendar Standard (agenda), dnd-kit (atelier), Recharts (rapports). État de la phase 02 : `PHASE02_STACK_AND_AUTH_READY — OWNER_SQL_GO_REQUIRED` ; aucun travail Phase 03 engagé.

> Note datée 16/10/2026 (phase 02, finalisation) : GO O-02-01 exécuté (migration d'identité, premier administrateur, preuve native authentifiée). État : `PHASE02_IMPLEMENTED — READY_FOR_OWNER_REVIEW`, action propriétaire SEC-02-01 (rotation des mots de passe PostgreSQL) ouverte. Backlog inchangé ; O-02-14 (503 explicite si la base est injoignable sur `/api`) et O-02-15 (affichage final du script SQL) ajoutés au backlog technique. Aucun travail Phase 03 engagé.

> Amendement daté 16/10/2026 (clôture phase 02) : SEC-02-01 clos (rotation vérifiée) ; état `PHASE02_READY_FOR_MANUAL_MERGE`, branche publiée, merge par le propriétaire. Phase 03 non commencée.

> Note datée 17/10/2026 (phase 03) : Phase 02 **MERGED** (`origin/main` = `cb79d1f`). Phase 03 démarrée (GO du propriétaire) sur `feat/phase-03-clients-rdv` : sous-lot **03-A1** (écrans Clients, TanStack Query/Table) implémenté, service Clients désactivé jusqu'à la validation du dossier G03-METIER (`docs/phase/03/DECISIONS_G03_METIER.md`). Backlog technique : TanStack Query et Table **installés** (premier cas d'usage réel) ; FullCalendar Standard cadré pour 03-B (`docs/phase/03/RDV_03B_PREPARATION.md`), non installé ; Sonner, dnd-kit, Recharts inchangés. Reportés explicitement : import de clients, filtre Ville, alerte de doublons, historique détaillé des modifications, suppression/effacement légal, données d'assurance/mutuelle. État : `PHASE03_A_UI_READY — OWNER_BUSINESS_RULES_AND_SQL_REVIEW_REQUIRED`.

> Note datée 17/10/2026 (phase 03-A2) : G03-METIER A et B acceptés, C accepté sur le principe, D en attente de 03-B. Sous-lot **03-A2** implémenté sur `feat/phase-03-clients-rdv` : backend Clients (`/api/clients`, permissions `clients.*`, attribution par `MMV.Maintenance permissions`), OpenAPI typé, interface sur la vraie API ; migration `0003_ClientsModule` et SQL exact préparés, **non appliqués** (revue du propriétaire). Backlog : inchangé, plus O-03-09 (actions d'écriture selon les permissions de la session) et O-03-11 (index de recherche si le volume l'exige). Toujours reportés : import de clients, filtre Ville, alerte de doublons, historique détaillé, effacement légal, assurance/mutuelle. Rendez-vous (03-B) non commencé. État : `PHASE03_A2_READY — OWNER_SQL_REVIEW_REQUIRED`.

> Note datée 17/10/2026 (phase 03-A3) : GO « intégration réelle Clients ». Migration `0003_ClientsModule` **appliquée** à `mmv_v2_dev` puis `mmv_v2_test` ; tests réels PASS (`ClientsWrite` 7/7, .NET 260/260, frontend 137/137, matrice HTTP réelle 31/31, parcours natif Tauri → HTTPS → API → PostgreSQL 18) ; actions Clients selon les permissions de la session ; attribution des droits `clients.*` au compte réel = **action du propriétaire** (`docs/phase/03/PROCEDURE_PERMISSIONS_CLIENTS.md`). État : `PHASE03_A3_COMPLETE — OWNER_REVIEW_REQUIRED`. Restent : 03-B (Rendez-vous, D-03-13 à D-03-17 `PROPOSED`), 03-C. Backlog ajouté : O-03-15 (notice persistante après navigation), O-03-16 (= O-02-15, requête d'affichage du script SQL).

> Note datée 17/10/2026 (phase 03-B) : GO « Rendez-vous ». O-03-15 corrigé ; interface Rendez-vous initiale (`/rendez-vous`, FullCalendar Standard 7.1.0, Jour / Semaine, détail, formulaire, sélecteur client, annulation / rétablissement, clavier) sur double de test, **service désactivé** ; proposition consolidée D-03-13 à D-03-18 `PROPOSED` (`docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md`) ; contrat et migration `0004_AppointmentsModule` proposés, **non appliqués**. État : `PHASE03_B_UI_READY — OWNER_BUSINESS_REVIEW_REQUIRED`. Restent : décision du propriétaire, intégration serveur 03-B (service, routes, migration après revue SQL), 03-C. Backlog ajouté : O-03-17 (montée FullCalendar 7.1.x après le 30/10/2026), O-03-18 (glisser-déposer à éprouver en natif), O-03-19 (socle CSS commun Clients / Rendez-vous), O-03-21 (parcours natif Rendez-vous) ; durée par motif, statuts « honoré / absent », horaires d'ouverture, filtre par responsable : `DEFERRED`.


> Note datée 17/10/2026 (phase 03-B2) : GO métier « Rendez-vous » ; D-03-13 à D-03-18 **approuvées** (`docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md` § Décision). Réalisé sur `feat/phase-03-clients-rdv` : serveur Rendez-vous (`/api/appointments`, deux modes d'attribution, éligibilité explicite des collaborateurs, capacité d'accueil instant par instant, réservations simultanées sérialisées, heures ambiguës refusées), interface alignée, **prise de rendez-vous depuis la fiche client** (anticipe une partie de 03-C) ; migration `0004_AppointmentsModule` et SQL exact préparés, **non appliqués** (revue SQL du propriétaire). État : `PHASE03_B2_READY — OWNER_SQL_REVIEW_REQUIRED`. Restent : revue et application de 0004, paramètres du magasin (fuseau, plage affichée, capacité d'accueil), collaborateurs éligibles, droits, parcours natif, reste de 03-C (rendez-vous visibles sur la fiche client). Toujours `DEFERRED` : horaires d'ouverture bloquants (module Paramètres), statuts « honoré / absent », durée par motif, filtre par responsable, table de personnalisation des motifs, multi-magasin. Backlog ajouté : O-03-22 (concordance des données de fuseaux serveur / poste), O-03-23 (interprétations à confirmer), O-03-24 (`DROP CONSTRAINT` à la revue SQL).

> Note datée 18/10/2026 (phase 03-B3) : GO « migration et validation réelle ». Migration `0004_AppointmentsModule` **appliquée** (`mmv_v2_dev`, `mmv_v2_test`) ; anomalies d'audit B2-REV-01 à B2-REV-04 corrigées ; cohérence des fuseaux vérifiable (O-03-22 maîtrisé) ; tests réels PASS (.NET 314 + 50, PostgreSQL 40, frontend 196) ; parcours natif Rendez-vous PASS sur `mmv_v2_test` avec les paramètres fournis par le propriétaire (Europe/Brussels, 09:00–19:00, capacité 1). État **`PHASE03_B3_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`**. **Backlog ajouté :** historique détaillé des modifications, annulations et rétablissements de rendez-vous (dette produit, O-03-30) ; base de fuseaux embarquée côté serveur (alternative à O-03-22, non retenue à ce stade). **Restent :** enregistrement des paramètres du magasin, droits `appointments.*` et collaborateurs éligibles sur les comptes réels (propriétaire) ; 03-C (rendez-vous sur la fiche client, QA native de l'intégration) ; Phase 04 non commencée.

> Note datée 18/10/2026 (phase 03-C) : GO « validation fonctionnelle finale » (GO technique après audit de 03-B3). Aucun code applicatif modifié ; régression, build release, HTTPS et natif (parcours complet, 2 thèmes × 2 résolutions × 3 zooms, clavier, glisser-déposer, garde-fou de fuseau rejoué) PASS sur `mmv_v2_test` ; O-03-16 corrigé et testé sans toucher aux bases ; paramètres du magasin écrits dans les user-secrets à la demande du propriétaire. État : `PHASE03_C — OWNER_FUNCTIONAL_VALIDATION_REQUIRED` (`docs/phase/03/VALIDATION_PROPRIETAIRE_03C.md`). Backlog / décisions : liste des rendez-vous sur la fiche client (O-03-37, D1), constats UX O-03-33 à O-03-36 (D2), installateur et adresse d'API de la version release (O-03-38) ; dette « historique détaillé » (O-03-30) inchangée. Phase 04 non commencée.

> Note datée 18/10/2026 (phase 03-D) : GO « UX Clients et traçabilité » après le parcours du propriétaire. Réalisé sur `feat/phase-03-clients-rdv` : sélecteurs de date et d'heure, préremplissage depuis la grille, onglet « Rendez-vous » de la fiche (dette O-03-37 levée), O-03-33 à O-03-36 ; journal d'audit des actions Clients et Rendez-vous et onglet « Historique » (dette O-03-30 levée **sous réserve** de l'application de 0005, revue du propriétaire requise). Backlog : durée de conservation et purge du journal (D-03-22), événements des modules futurs (Ordonnances, Ventes, Commandes, Stock — à définir avec leurs propres décisions), build release / installateur (O-03-38). Phase 03 **non achevée** (`PHASE03_COMPLETE` subordonné à la validation finale du propriétaire). Aucun push, PR ni merge ; Phase 04 non commencée.

> Note datée 19/10/2026 (phase 03-D3) : GO « migration 0005 et clôture » après audit indépendant de `b0f7abc`. Migration `0005_AuditJournal` **appliquée** (`mmv_v2_dev`, `mmv_v2_test`) ; modifications sans changement sans écriture (O-03-44) ; vérifications réelles et parcours natif de l'historique PASS. État : `PHASE03_D3 — OWNER_FINAL_REVIEW_REQUIRED` ; `PHASE03_COMPLETE` non déclaré. **Backlog ajouté** (`FOUNDATION_MUST`) : rôle SQL applicatif distinct du rôle de migration (O-B04) ; politique de rétention contrôlée du journal d'audit et procédure de purge revue (D-03-22 ouverte : durées, RGPD / APD, loi 09-08 / CNDP). Phase 04 non commencée.

> Note datée 19/10/2026 (Phase 03, préparation du merge) : audit indépendant de l'archive 03-D3 — **GO technique**. Branche `feat/phase-03-clients-rdv` vérifiée (ascendance depuis `origin/main`, tests, secrets, documentation) ; push préparé, exécuté seulement après le dernier essai manuel du propriétaire ; PR et merge par le propriétaire. État : `PHASE03 — READY FOR OWNER SMOKE TEST AND PR` ; `PHASE03_COMPLETE` non déclaré. **Backlog inchangé** ; réserves de la Phase 03 regroupées dans `docs/phase/03/RESERVES_PHASES_FUTURES.md`. Phase 04 non commencée.

> Amendement daté 19/10/2026 : essai manuel du propriétaire OK ; branche `feat/phase-03-clients-rdv` publiée sur `origin`. PR et merge par le propriétaire. Phase 04 non commencée.

> Note datée 19/10/2026 (phase 04-00) : Phase 03 **MERGED** (`origin/main` = `6924860`, PR #4). GO 04-00 : préflight et cadrage seulement, branche `feat/phase-04-00-cadrage`. **D-04-01 APPROVED** (ordonnances médicales / mesures magasin distinctes) ; D-04-02 à D-04-09 `PROPOSED` (`docs/phase/04/DECISIONS_04_METIER_PROPOSED.md`). Lot 3 : 36 / 36 états cartographiés (`docs/phase/04/STATE_MAP_36.md`), 12 états absents de la maquette à concevoir (G04-DESIGN). État : `PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`. **Backlog ajouté :** mesures magasin (type distinct, D-04-01), pièces source sécurisées, révisions et invalidation, permissions optiques ; restent **hors Phase 04** : prise en charge INAMI / AMO et tiers payant, transposition, OCR / numérisation, validité / renouvellement, centrage et configuration de verres (`MMV-N-043` à `MMV-N-052` pour la part relevant d'autres sous-lots), lentilles, appareils de mesure, impression / export. `FOUNDATION_MUST` rappelés comme **préalables à un pilote sur données de santé** : rôle SQL applicatif distinct (O-B04), restauration de sauvegarde éprouvée, avis juridique Maroc / Belgique (G04-LEGAL). 04-A / 04-B non commencés.

> Note datée 19/10/2026 (Phase 04, suite de 04-00) : revue indépendante — G04-00 « cadrage techniquement acceptable » ; D-04-01 APPROVED définitive ; D-04-02 à D-04-09 `PROPOSED`. Études préalables au modèle (`docs/phase/04/ETUDES_PRE_MODELE_04.md`) → D-04-10 à D-04-14 `PROPOSED` ; 04-A préparé (`docs/phase/04/PREPARATION_04A.md`), non commencé, démarrage sur GO G04-A. État : `PHASE04_04A_PREPARED — OWNER_GO_REQUIRED`. **Backlog ajouté :** analyse antivirus AMSI des pièces (O-04-17, avant pilote) ; mesures de sauvegarde / restauration avec pièces (O-04-19) ; décision sur la journalisation des lectures de dossiers (O-04-20, avec G04-LEGAL) ; droit `optical.browse` éventuel (D-04-13, non proposé en 04) ; journal de sécurité des refus d'accès. Inchangé : `FOUNDATION_MUST` préalables à un pilote (O-B04, restauration éprouvée, G04-LEGAL).

> Note datée 19/10/2026 (Phase 04-A) : GO **G04-A — UI ONLY**, O-04-16 APPROVED. Interfaces du dossier optique réalisées en **prototype** sur banc isolé (données fictives, droits simulés, aucun serveur ni base) ; application réelle inchangée. État : `PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED` (`docs/phase/04/LIVRAISON_04A.md`). **Backlog ajouté :** sélecteur de fichier réel, envoi en flux, aperçu PDF et CSP (O-04-23, 04-B / 04-C) ; remplacement de l'onglet reporté à l'activation (O-04-22) ; socle CSS commun (O-04-11, reporté à 04-C) ; libellé inutilisé dans le bundle (O-04-24) ; examen métier opticien et revue visuelle (O-04-25) ; test Phase 03 intermittent (O-04-26). Inchangé : 04-B soumis à G04-METIER ; `FOUNDATION_MUST` préalables à un pilote (O-B04, restauration éprouvée, G04-LEGAL).

> Note datée 20/10/2026 (Phase 04-A, refonte UX Lot 3) : essai du banc par le propriétaire ; **D-04-15 APPROVED** (maquette Lot 3 = référence UX des formulaires optiques, contrôles rapides ±). Formulaires refondus dans le banc isolé ; application réelle inchangée. État : `PHASE04_A_UX_LOT3_READY — OWNER_VISUAL_REVIEW_REQUIRED` (`docs/phase/04/LIVRAISON_04A.md` § 11). **Backlog ajouté :** pas, bornes, format et règles croisées des champs optiques à fixer avec D-04-06 (aucune valeur définitive) ; couplage éventuel état de l'œil / valeurs (O-04-29, D-04-02.c) ; en-tête collant à revérifier dans le Shell réel à l'activation (O-04-28). Inchangé : 04-B soumis à G04-METIER ; `FOUNDATION_MUST` préalables à un pilote.

> Note datée 20/10/2026 (Phase 04-A, simplification) : **D-04-16 APPROVED** (propriétaire, `prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`) — « État de l'œil » supprimé ; gestion des pièces sources reportée ; révisions, comparaison, motif et auteurs conservés. Prototype simplifié dans le banc isolé ; application réelle inchangée ; aucune migration `0006`, aucun backend optique. État : `PHASE04_A_SIMPLIFICATION_READY — OWNER_VISUAL_REVIEW_REQUIRED` (`docs/phase/04/LIVRAISON_04A.md` § 12). Le point de backlog « couplage éventuel état de l'œil / valeurs (O-04-29) » de la note précédente est **clos, sans objet**. **Backlog permanent ajouté :** BL-04-01 ci-dessous. Inchangé : 04-B soumis à G04-METIER ; `FOUNDATION_MUST` préalables à un pilote.

> Note datée 20/10/2026 (Phase 04-A, clôture) : **D-04-17 APPROVED** (`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`) — « Enregistrer » principal, « Enregistrer comme brouillon » discret, révisions conservées. Lot frontend 04-A : `PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` ; prochaine étape : G04-METIER (D-04-02 à D-04-14), puis 04-B et G04-SQL. Aucun ajout au backlog ; BL-04-01 inchangé.

## Backlog permanent

Éléments reportés **hors de la phase en cours** par décision du propriétaire ; ils ne sont pas abandonnés et seront rouverts par un GO explicite, avec leurs décisions remises à l'état `PROPOSED`.

| Réf. | Élément | Origine | Contenu reporté | Matériaux conservés pour la reprise |
|---|---|---|---|---|
| BL-04-01 | Gestion documentaire des pièces sources du dossier optique | D-04-16 APPROVED (20/10/2026) | téléchargement, consultation (aperçu), invalidation et rattachement des pièces aux révisions ; stockage (table, `bytea`, liens révision ↔ pièce) ; routes `/api/optical/…/documents` ; droits `optical.documents.read` / `.write` ; événements d'accès ; contrôles de fichiers, limites, antivirus, CSP, visionneuse, sauvegardes avec pièces | décisions D-04-03.c, D-04-10, D-04-12.a/b/d, D-04-14 ; études E-01, E-03, E-05 (`docs/phase/04/ETUDES_PRE_MODELE_04.md`) ; points O-04-01 / 02 / 03 / 13 / 17 / 18 / 19 / 23 ; tests prévus Q-04-105, 118, 119, 121, 132, 133, 141 (pièces), 143, 144, 145 ; prototype de 04-A au commit `9be2fda` (historique Git) |

> Note datée 27/10/2026 (Phase 04-B) : GO officiel d'implémentation ([`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md`](../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md)). Dossier optique réel réalisé (backend .NET, deux types distincts, révisions, permissions, audit, idempotence) ; migration `0006_OpticalModule` **préparée, non appliquée** ; écrans 04-A raccordés derrière un interrupteur absent par défaut. État : `PHASE04_B_READY — OWNER_SQL_REVIEW_REQUIRED` ([`docs/phase/04/LIVRAISON_04B.md`](phase/04/LIVRAISON_04B.md)). **Backlog ajouté :** validation opticien des règles cliniques (O-04-38) ; pas des prismes et ± associés (O-04-39) ; mesure des listes en pilote (O-04-41) ; filtre « Dossier optique » de l'historique (O-04-45). **Inchangé :** BL-04-01 (pièces sources) ; `FOUNDATION_MUST` préalables à un pilote (rôle SQL applicatif distinct O-B04, restauration éprouvée, G04-LEGAL).
