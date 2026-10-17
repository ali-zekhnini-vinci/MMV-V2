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
