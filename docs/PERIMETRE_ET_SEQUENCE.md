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
