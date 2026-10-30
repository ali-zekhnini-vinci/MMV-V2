# 05 — Spécification d'implémentation proposée

**PROPOSED, 29/10/2026 — aucune implémentation avant validation du cadrage et GO de 05-A.**

## 1. Architecture (ADR-001, ADR-003, inchangés)

Monolithe modulaire existant, mêmes couches que Clients / Rendez-vous / Optique ; aucun microservice, aucune extension PostgreSQL, aucune dépendance nouvelle prévue (ADR-014).

| Couche | Ajouts proposés (dossiers **à créer**, inexistants au 29/10/2026) |
|---|---|
| `src/MMV.Domain/Catalog/`, `Suppliers/`, `Stock/` | entités, politiques, calcul de disponibilité, règles d'archivage et de changement de politique, motifs |
| `src/MMV.Application/Catalog/`, `Suppliers/`, `Stock/` | validateurs (attributs par catégorie, GTIN, identifiants légaux), cas d'usage, `ICatalogQuery`, `IStockLedger`, `IStockReservations` (interface) |
| `src/MMV.Infrastructure/Catalog/`, `Stock/` | modèles EF Core 10 (schémas `catalog`, `stock`), stores, verrous SQL, migrations `0007` à `0009` (jamais au démarrage) |
| `src/MMV.Api/Catalog/`, `Suppliers/`, `Stock/` | routes minimales, policies, filtrage des coûts à la projection, exports CSV |
| `src/MMV.Domain/Identity/Permissions.cs` | douze permissions (D-05-11) ajoutées à `All` et `Grantable` |
| `apps/desktop/src/catalog/`, `stock/` | écrans Lot 6, TanStack Query / Table, React Hook Form + Zod, composants existants (garde de navigation, confirmations Radix, états transverses) ; entrée de menu derrière `VITE_MMV_CATALOG_MODULE` |

## 2. Permissions et modèles d'attribution

Droits effectifs = permissions serveur. Les profils du Lot 1 ne sont que des **modèles d'attribution suggérés** (à ratifier avec D-05-11) :

| Permission | Propriétaire | Responsable | Opticien | Vendeur | Technicien / accueil |
|---|:-:|:-:|:-:|:-:|:-:|
| `catalog.read` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `catalog.write` | ✓ | ✓ | ✓ | | |
| `catalog.archive` | ✓ | ✓ | | | |
| `catalog.costs.read` | ✓ | ✓ | ✓ | | |
| `catalog.costs.write` | ✓ | ✓ | | | |
| `suppliers.read` | ✓ | ✓ | ✓ | | |
| `suppliers.write` | ✓ | ✓ | | | |
| `stock.read` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `stock.movements.write` | ✓ | ✓ | ✓ | | |
| `stock.adjust` | ✓ | ✓ | | | |
| `stock.inventory.count` | ✓ | ✓ | ✓ | ✓ | |
| `stock.inventory.validate` | ✓ | ✓ | | | |

Attribution par `MMV.Maintenance permissions grant|revoke` (inchangé), après application de la migration correspondante.

## 3. Protection des prix d'achat

1. Projection serveur distincte selon `catalog.costs.read` : les champs de coût ne sont **jamais sérialisés** sans le droit (pas de masquage côté poste, pas de `null` révélateur).
2. Écriture de coût sans `catalog.costs.write` → `403 costs_forbidden`, transaction annulée.
3. Exports CSV : mêmes projections.
4. Audit : noms de champs seulement, jamais de valeur de coût ; valeurs dans `catalog.price_history` sous permission.
5. Journaux techniques : aucune journalisation des corps (constat Q-04-010 à reconduire).
6. Poste : aucun coût en cache persistant ; TanStack Query en mémoire, purge à la déconnexion (comportement existant).
7. Tests : pour chaque route et export, absence des clés de coût sans le droit (Q-05-10x).

## 4. Transactions et concurrence multi-postes

| Opération | Mécanisme |
|---|---|
| Modification d'article / fournisseur | concurrence optimiste `xmin` (`UPDATE … WHERE id = @id AND xmin = @version`), `409 version_conflict` |
| Mouvement manuel, réception | transaction : article `FOR SHARE` (statut, politique) → solde `FOR UPDATE` → contrôle `on_hand + delta >= 0` → insertion du mouvement (avant / après) → mise à jour du solde → audit → validation. Tout ou rien |
| Ajustement | idem + comparaison `expectedOnHand` à la valeur verrouillée → `409 stock_changed` |
| Archivage / changement de politique | article `FOR NO KEY UPDATE` **avant** lecture du solde et des inventaires ouverts (leçon O-04-53) ; un mouvement concurrent attend ou échoue proprement |
| Plusieurs articles (validation d'inventaire, futures ventes) | verrous des articles puis des soldes **dans l'ordre croissant des identifiants** ; ordre unique documenté pour toutes les phases (anti-interblocage) |
| Ouverture d'inventaire | `pg_advisory_xact_lock` dédié + contrôle « un seul inventaire ouvert par article » |
| Saisie d'une ligne d'inventaire | `xmin` de la ligne ; `expected_at_count` lu sous `FOR SHARE` du solde |
| Validation d'inventaire | session `FOR UPDATE` (statut `open`) → articles et soldes ordonnés → comparaison de chaque solde à `expected_at_count` → mouvements `inventory_adjustment` → statut `validated` → audit ; `lock_timeout` borné |
| Idempotence | `creation_request_id` / `last_request_id` (fiches, sessions), `(request_id, product_id)` unique (mouvements) ; réémission = résultat déjà enregistré |
| Lecture « vue stock » | instantané (`asOf`), aucun temps réel ; les conflits apparaissent à l'action, jamais en silence |

Niveau d'isolation : `READ COMMITTED` + verrous explicites (comme Phase 04) ; `SERIALIZABLE` non retenu (réessais applicatifs généralisés inutiles ici).

## 5. Disponibilité

Calcul unique dans le domaine (`Availability.From(policy, archived, onHand, reserved = 0, threshold)`), utilisé par la liste, la fiche, la vue stock, `ICatalogQuery` et les exports ; jamais recalculé par le poste.

## 6. Maroc / Belgique et données sensibles

- Devise de l'installation (paramètre de déploiement, comme le fuseau D-03-13) ; tout prix porte sa devise ; refus d'un prix dans une autre devise.
- Format des montants à l'affichage selon la langue française de l'installation (`1 234,50 MAD` / `1 234,50 €`).
- Identifiants légaux fournisseurs : contrôles de format seulement, aucune vérification en ligne.
- Aucune donnée client ni de santé dans les modules 05.
- G05-LEGAL : traçabilité des dispositifs médicaux, conservation des pièces comptables et des inventaires.

## 7. Outillage de preuve (reconduit de la Phase 04)

Tests .NET unitaires et d'API ; tests PostgreSQL 18 réels sur `mmv_v2_test` (après chaque G05-SQL) avec **barrières déterministes** pour la concurrence et preuve « échoue avant / passe après » pour chaque correctif ; HTTPS réel ; Tauri natif ; captures navigateur comparées aux planches Lot 6 en clair / sombre, 1920×1080 et 1024×700 ; résidu de données de test = 0 en fin de session.
