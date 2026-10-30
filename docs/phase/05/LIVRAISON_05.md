# Phase 05 — Livraison de l'implémentation (30/10/2026)

**Statut : `PHASE05_IMPLEMENTED — OWNER_G05_SQL_REVIEW_REQUIRED`.**

Mandat : [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE05_2026-10-30.md). Branche locale `feat/phase-05-catalogue-stock` (partie de `main` = `0a83a57`, Phase 04 fusionnée par la PR #5) ; commits locaux seulement, aucun push, PR ni merge. `apps/desktop/.env.development` (modification locale du propriétaire) intact et non commité. Ancien logiciel MMV non consulté. Phase 06 non commencée.

## 1. Livré

| Sous-lot | Contenu réel |
|---|---|
| 05-A Catalogue, tarifs, fournisseurs | six familles ; modèles + variantes (SKU unique, référence fabricant, code-barres GTIN contrôlé, attributs par famille) ; trois politiques par variante, changement contrôlé ; magasins (devise EUR / MAD immuable) et emplacements, créés par `MMV.Maintenance stores create` / `locations create` ; référentiel verres configurable ; fournisseurs (identifiants légaux par pays, contact minimal), rattachements multiples, un principal, fiche « incomplète » ; tarifs par (variante, magasin) : prix d'achat HT, coefficient, aperçu serveur, prix calculé à un arrondi, recalcul explicite unitaire et par lot, prix manuel, historique ; TTC seulement avec fiscalité validée (aucun taux) ; archivage, aucune suppression ; projection des coûts selon `costs.read` |
| 05-B Stock et réceptions | soldes par (variante, emplacement), réservé = 0 (contrat Phase 06) ; entrées / sorties motivées, ajustement quantité constatée + attendue ; journal en ajout seul ; réceptions simples, partielles avec complément ; seuils et alertes ; stock négatif refusé sous verrou ET par contrainte ; sorties concurrentes sérialisées |
| 05-C Inventaires | sessions complètes / par famille / par sélection, par emplacements, à l'aveugle en option ; comptage par ligne versionné ; écarts justifiés ; validation atomique (permission distincte) appliquant l'écart au stock courant ; signalement des lignes modifiées depuis le comptage ; abandon motivé |
| 05-D Import CSV | dépôt, aperçu, correspondance des colonnes, contrôle à blanc avec rapport par ligne, application tout ou rien rejouée contre l'état courant, même fichier jamais appliqué deux fois, coûts sous `costs.write`, prix manuels sous `pricing.manual`, formules refusées, aucune conversion de devise |
| Contrats Phase 06 | `ICatalogQuery` implémenté ; `IStockLedger`, `IStockReservations` définis (interfaces seulement) |
| Interface | modules « Produits » et « Stock & inventaires » (React, derrière `VITE_MMV_CATALOG_MODULE=enabled`), sélection du magasin actif, aucun calcul monétaire côté poste |
| Références | `references/phase05/` versionné partiellement (maquette HTML et captures exclues : identifiants hérités et coordonnées de fournisseurs ; procédure de récupération dans son README) |
| SQL | migration unique `20261030005557_CatalogStockModule` **non appliquée** ; dossier [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md) |

Choix techniques et hypothèses non ratifiées : [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md).

## 2. Tests

### PASS (exécutés le 30/10/2026 sur le poste de développement)

| Suite | Résultat |
|---|---|
| `dotnet build MMV.slnx` (avertissements = erreurs, analyseurs `latest-recommended`) | PASS |
| `MMV.Api.Tests` (dont `CatalogRulesTests` : prix, arrondi unique, chaînes décimales, état de prix, TTC, GTIN, familles, disponibilité, stock négatif / réservé, décision d'inventaire, CSV, correspondance d'import, validation) | 499 / 499 PASS |
| `MMV.Maintenance.Tests` | 50 / 50 PASS |
| `MMV.PostgreSql.Tests --filter Stage=Phase05` sur cluster PostgreSQL 18.6 **jetable** (port 55433) : catalogue / tarifs / projection des coûts / EUR et MAD / contrôle croisé de l'arrondi avec `round(numeric,2)` / audit sans coût ; fournisseurs et principal ; stock négatif, réceptions partielles, idempotence, ajustement concurrent, alertes, invariant `on_hand = Σ écarts`, politiques et archivage refusés, déclencheurs ; **deux sorties concurrentes** (une seule aboutit) ; inventaire (aveugle, conflit de comptage, mouvement concurrent, permissions distinctes, validation atomique) ; import (contrôle à blanc, tout ou rien, réémission, fichier déjà appliqué, coûts, devise) ; matrice de permissions ; inventaire exact du schéma | 8 / 8 PASS, résidu 0 |
| Script 0007 idempotent appliqué deux fois sur la base jetable ; `verify.sql` | PASS ; 27 / 27 OK |
| `dotnet ef migrations has-pending-model-changes` | PASS (aucun écart) |
| Frontend `tsc` (application, tests) ; `eslint` sur les fichiers de la Phase 05 et du shell | PASS |
| Vitest (dont `apps/desktop/src/catalog/catalog.test.ts`) | 308 / 309 (voir FAIL) |

### FAIL

| Test | Cause |
|---|---|
| `apps/desktop/src/optical/realAppIsolation.test.tsx` — « le banc est une page distincte… aucune activation par défaut » | **Préexistant, non causé par la Phase 05** : la modification locale du propriétaire de `apps/desktop/.env.development` ajoute `VITE_MMV_OPTICAL_MODULE`, que le test interdit. Fichier non modifié par l'agent (consigne). Sur 3 passages complets de Vitest, 2 ont en outre montré 3 tests lents en échec (`Shell.test.tsx` « Rendez-vous : system.admin seul… », `ClientHistoryTab.test.tsx` « Voir le rendez-vous… », `realAppIsolation.test.tsx` « application réelle AVEC interrupteur… », 6 à 9 s chacun) ; relancés seuls, ils passent (charge machine, cf. O-04-51). Preuve : `evidence/P05_vitest*.txt` de l'archive. |

### NOT_TESTED

| Élément | Raison |
|---|---|
| Application de 0007 sur `mmv_v2_dev` / `mmv_v2_test`, `verify.sql` sur ces bases | **G05-SQL** : GO du propriétaire requis |
| Suites PostgreSQL complètes sur `mmv_v2_test` (non-régression Phases 02–04 + Phase 05 sur la cible officielle) | idem ; aucune connexion aux bases du propriétaire pendant la Phase 05 |
| API en processus réel HTTPS (Kestrel, certificat de développement) et Tauri natif | prévus après application (étape d'intégration réelle, procédé 04-C) ; le pipeline HTTPS est couvert en processus de test |
| Revue visuelle des écrans contre les planches Lot 6 (captures clair / sombre, 1920×1080 et 1024×700) | G05-DESIGN ; maquette non versionnée |
| Commandes `stores create` / `locations create` sur une vraie base | cible d'écriture = `mmv_v2_dev` seulement, après G05-SQL |

## 3. Réserves et backlog

- **BL-05-09** (nouveau) : exports CSV (catalogue, prix, fournisseurs, soldes, mouvements, inventaire) avec filtrage des coûts — prévus par le cadrage 05-C, non demandés par le mandat du 30/10, non livrés.
- **BL-05-10** (nouveau) : écrans de saisie des catégories fiscales et validation du profil fiscal d'un magasin — après ADR-010 seulement.
- Listes de variantes, soldes et alertes calculées en mémoire après filtre SQL : volume à mesurer en pilote.
- Hypothèses non ratifiées : [`DECISIONS_TECHNIQUES_05.md`](DECISIONS_TECHNIQUES_05.md) § 3.
- Rôle SQL applicatif propriétaire des tables (R-05-17 / BL-04-04, inchangé).

## 4. Étape suivante

GO G05-SQL du propriétaire sur les empreintes de [`sql/0007_MANIFEST.sha256`](sql/0007_MANIFEST.sha256), puis application (sauvegarde, dev, test), suites PostgreSQL complètes sur `mmv_v2_test`, API HTTPS réelle, Tauri natif, captures ; statut visé ensuite : `PHASE05_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.
