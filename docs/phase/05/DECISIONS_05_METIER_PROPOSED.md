# 05 — Registre court des décisions métier (PROPOSED)

Toutes les décisions ci-dessous sont **`PROPOSED`** au 29/10/2026. Aucune n'est appliquée : aucune migration, aucune table, aucune permission n'a été créée. Les **trois politiques d'article (D-05-02) ne sont pas approuvées** tant que le propriétaire ne les a pas ratifiées.

Seules figurent les décisions qui changent le modèle de données, les contrats de la Phase 06 ou le découpage. Les détails d'interface (libellés, ordre des colonnes, densité) suivent la maquette Lot 6 et ne demandent pas d'arbitrage préalable.

Réponse attendue : pour chaque ligne, **A (recommandée)**, une autre option, ou une correction libre. Formulaire en fin de document.

---

## D-05-01 — Statut du Lot 6 : référence UX seulement

**Constat (O-05-01).** L'en-tête et les notes de conception de `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` déclarent avoir été dessinés « champs limités au code réel » d'un **dépôt de l'ancien logiciel** (noms d'entités, d'énumérations, de cas d'usage et de DTO, catégories figées, suppression physique en cascade, fournisseur sans statut, seuil par défaut, formule de marge, visibilité des prix par rôle). Ces informations proviennent du texte de la maquette elle-même ; l'ancien dépôt n'a pas été consulté.

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Le Lot 6 est la référence **d'interface** (mise en page, états, interactions, accessibilité). Toutes les listes de champs, énumérations et règles sont redécidées par ce cadrage ; aucun nom d'entité ou de DTO de la maquette n'est repris. | Conforme à D-000 et `NEW_RULES_ONLY`. Les planches qui illustrent une limite de l'ancien code sont **adaptées ou remplacées** (suppression physique, fournisseur sans statut, historique d'inventaire « reconstitué », conflit « cible UI ») — voir [`MATRICE_LOT6.md`](MATRICE_LOT6.md). |
| B | Adopter les listes de champs du Lot 6 telles quelles. | Incompatible avec D-000 (règles héritées). Non recevable. |

## D-05-02 — Trois politiques d'article

| Politique | Code proposé | Stock suivi | Disponibilité affichée | Fournisseur | Exemples |
|---|---|---|---|---|---|
| Article stocké | `stocked` | oui (quantité en main, mouvements, seuil, inventaire) | en stock / stock faible / rupture / seuil non défini | facultatif | montures, solaires, étuis, produits d'entretien |
| Article sur commande | `order_only` | **non** (aucune quantité, aucun mouvement, exclu des inventaires) | « sur commande » (+ délai indicatif du fournisseur principal s'il est connu) | **fournisseur principal obligatoire** | verres de gamme, lentilles non tenues en stock, montures sur catalogue fournisseur |
| Service non stockable | `service` | non | toujours disponible tant que l'article est actif | aucun | montage, ajustage, réparation, prestations |

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Politique **obligatoire, portée par chaque article**. Changement autorisé seulement si : vers ou depuis `stocked`, quantité en main = 0 et aucun inventaire ouvert sur l'article ; vers `order_only`, fournisseur principal présent. Chaque changement est audité. | Une même catégorie peut contenir des articles stockés et sur commande (lentilles courantes stockées, autres sur commande). La Phase 06 lit la politique **au moment de la vente** et la fige sur la ligne. |
| B | Politique déduite de la catégorie. | Plus simple à saisir, mais impossible de stocker une partie seulement d'une gamme. |
| C | Deux politiques (stocké / non stocké). | Perd la distinction « sur commande » ↔ « service » nécessaire aux Commandes (approvisionnement) et à l'atelier. |

## D-05-03 — Stock négatif

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Interdit.** Toute sortie (manuelle, puis vente en Phase 06) qui rendrait la quantité négative est refusée (`422 stock_insufficient`), contrôle dans la transaction verrouillée. | Intégrité garantie par contrainte SQL `on_hand >= 0`. En Phase 06, un article stocké en rupture ne peut pas être vendu « en stock » : enregistrer la réception d'abord, ou le vendre comme commande (décision de la Phase 06). |
| B | Autorisé avec alerte. | Vente possible avant saisie de la réception ; les inventaires deviennent la seule correction ; écarts difficiles à expliquer. |
| C | Interdit, sauf dérogation par une permission dédiée avec motif. | Ajoutable plus tard à partir de A sans migration de données. |

## D-05-04 — Variantes

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Un article = une unité de stock** (une couleur, un calibre). Regroupement **facultatif** sous un « modèle » (marque + nom du modèle) pour afficher les autres coloris / tailles. Attributs propres à l'article (couleur, calibre, pont, branche…). | Le stock, les prix et l'inventaire restent au niveau article ; aucune quantité au niveau modèle. Simple pour la Phase 06 (une ligne = un article). |
| B | Produit parent obligatoire + table de variantes portant le stock. | Plus structuré, mais double niveau partout (prix par variante ou par parent, archivage partiel). |
| C | Aucun regroupement. | Le plus simple ; doublons de saisie pour les coloris. Migrable vers A plus tard (colonne facultative). |

## D-05-05 — Catégories et profondeur du catalogue verres / lentilles

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Liste fermée de catégories** fournie par le propriétaire (proposition : Montures optiques, Solaires, Verres, Lentilles de contact, Produits d'entretien, Accessoires, Services), un niveau, chaque catégorie ayant un **jeu d'attributs défini dans le code** et validé par le serveur. Verres = articles de gamme `order_only` **sans grille de puissances** (les paramètres viennent de l'ordonnance au moment de la vente / commande). Lentilles : `order_only` par défaut ; une puissance tenue en stock = un article `stocked` distinct. | Pas de catalogue fabricant de verres en Phase 05 ; la tarification des verres « sur devis » relève de la Phase 06 (D-05-08). Ajouter une catégorie = petite évolution, pas de saisie libre d'attributs. |
| B | Catégories et attributs entièrement paramétrables par l'utilisateur (clé / valeur). | Souple mais non validable ; recherche et contrôles faibles ; complexité d'écran. |
| C | Catalogue verres complet (indices, traitements, plages de puissances, tarifs fabricant). | Hors périmètre (Phase Commandes / verres) ; volume et mises à jour fabricants. |

## D-05-06 — Fournisseurs et rattachement article ↔ fournisseur

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Fiche : nom, code interne unique, pays, contact (un nom facultatif, e-mail, téléphone), adresse, notes, **identifiants légaux facultatifs selon le pays** (Maroc : ICE, IF, RC ; Belgique : numéro d'entreprise / TVA), **statut actif / archivé**. Un article peut avoir **plusieurs fournisseurs** ; pour chacun : référence fournisseur, prix d'achat fournisseur (protégé), délai indicatif ; **au plus un fournisseur principal**. | Diffère volontairement du Lot 6 (un seul fournisseur, pas de statut, pas d'identifiants) : planches B-128 et B-129 adaptées. Prépare la Phase Commandes sans la développer. |
| B | Un seul fournisseur par article (référence directe). | Plus simple ; bloque le multi-sourcing et l'historique des prix par fournisseur. |

## D-05-07 — Archivage plutôt que suppression

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Aucune suppression physique** d'article, de fournisseur, de mouvement ni d'inventaire. Archivage + réactivation, motif facultatif, audit. Archiver un article `stocked` est **refusé si la quantité en main > 0** ou s'il figure dans un inventaire ouvert. Un article archivé n'est ni vendable ni proposé aux nouveaux inventaires ; il reste visible dans l'historique. Un fournisseur archivé ne peut plus être choisi comme principal ; ses rattachements restent lisibles. | Cohérent avec Clients (archivage, D-03) et avec la conservation comptable. Les planches de suppression (B-109, B-129) deviennent des planches d'archivage. |
| B | Suppression physique tant qu'aucune référence n'existe (comportement du Lot 6). | Ajoute un chemin destructif et des refus « clé étrangère » ; peu d'intérêt une fois l'archivage disponible. |

## D-05-08 — Prix, devise, fiscalité et coûts

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Prix de vente public TTC**, montant décimal à 2 décimales, dans la **devise unique de l'installation** (MAD ou EUR, fixée à l'installation, enregistrée avec chaque prix). **Catégorie fiscale** de l'article : champ prévu, **renseigné après ADR-010** (taux réels Maroc / Belgique à confirmer par un fiscaliste). Prix de vente **facultatif seulement pour `order_only`** (« prix à la commande »). **Prix d'achat de référence** et prix d'achat fournisseur **protégés** (permission dédiée, D-05-11). **Historique des prix** (ancien → nouveau, auteur, date). Marge calculée par le serveur, visible avec la seule permission des coûts. **Aucune valorisation de stock** (CUMP / FIFO) en Phase 05 : coût unitaire saisi à la réception, conservé pour plus tard. | La Phase 06 copie libellé, référence, prix et catégorie fiscale sur la ligne de vente (instantané) et ne relit plus le catalogue. Une vente réelle reste bloquée tant qu'ADR-010 n'est pas tranché. |
| B | Prix HT + taux de TVA par article. | Calculs TTC à l'affichage ; arrondis à maîtriser ; prématuré sans ADR-010. |
| C | Valorisation CUMP dès 05-B. | Méthode comptable à valider par le comptable ; complexité des corrections. |

## D-05-09 — Entrées, sorties, ajustements et motifs

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Journal de mouvements **en ajout seul** ; chaque mouvement porte quantité avant / après, écart signé, motif codé, commentaire facultatif, auteur, date, référence de source. **Entrée** (réception fournisseur ou entrée manuelle) et **sortie manuelle** : quantité > 0, motif obligatoire. **Ajustement** : l'utilisateur saisit la **quantité constatée** ; le serveur calcule l'écart à partir de la quantité verrouillée et exige la **quantité attendue** affichée à l'utilisateur (sinon `409` « modifié sur un autre poste ») ; confirmation obligatoire. Aucune modification d'un mouvement : correction = nouveau mouvement motivé. **Liste fermée de motifs** à ratifier (proposition : réception fournisseur, retour fournisseur, casse, perte, vol constaté, erreur de saisie, usage interne / démonstration, correction d'inventaire, autre + commentaire obligatoire). | Répond aux questions ouvertes de la maquette (quantité avant / après, auteur visible). Une sortie liée à une vente (Phase 06) ne sera jamais saisie à la main. |
| B | Ajustement saisi comme écart signé (+3 / −2). | Pas de détection naturelle du conflit ; risque de double correction. |

## D-05-10 — Inventaires

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Sessions d'inventaire persistées** et numérotées ; périmètre : tout le stock, une catégorie, ou une sélection d'articles `stocked` actifs ; **un seul inventaire ouvert par article**. Comptage **non aveugle** par défaut (quantité théorique affichée), option « comptage à l'aveugle » à l'ouverture. Les mouvements restent **autorisés** pendant la session : chaque ligne mémorise la quantité théorique **au moment du comptage** ; à la validation, si la quantité a changé depuis, la ligne est signalée (recompter ou accepter explicitement). **Validation atomique** de tous les écarts (mouvements « correction d'inventaire » liés à la session), motif obligatoire pour chaque écart, permission **distincte du comptage**. Session close ou abandonnée = **immuable** ; abandon irréversible, sans effet sur le stock, conservé. | Fournit une preuve d'inventaire exploitable (inventaire annuel des commerçants au Maroc et en Belgique, durées de conservation à confirmer avec le comptable). Les planches « historique reconstitué (cible) » deviennent un historique réel. |
| B | Gel du stock : sorties bloquées pendant la session. | Plus simple à valider, mais bloque les ventes du magasin pendant le comptage. |
| C | Pas de session : ajustements simples (comportement de base du Lot 6). | Pas d'historique d'inventaire ni de preuve de comptage. |

## D-05-11 — Permissions

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Douze permissions explicites, aucune n'en impliquant une autre, `system.admin` n'en donne aucune : `catalog.read`, `catalog.write`, `catalog.archive`, `catalog.costs.read`, `catalog.costs.write`, `suppliers.read`, `suppliers.write`, `stock.read`, `stock.movements.write`, `stock.adjust`, `stock.inventory.count`, `stock.inventory.validate`. Historique d'audit : `audit.read` **et** le droit de lecture du module. Les profils du Lot 1 (propriétaire, responsable, opticien, vendeur, technicien / accueil) ne sont que des **modèles d'attribution** documentés, jamais des droits. | Les prix d'achat sont **retirés des réponses par le serveur** (jamais masqués par l'interface seulement). Une migration doit étendre la contrainte de permissions (`DROP` + `ADD CONSTRAINT`, procédé O-03-24). |
| B | Permissions grossières (`catalog.*`, `stock.*`). | Moins d'attributions, mais impossible de séparer comptage et validation, ou de cacher les coûts aux vendeurs. |

## D-05-12 — Hors périmètre de la Phase 05

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **OUT** : commandes fournisseurs, réceptions rapprochées d'une commande, réservations effectives (contrat seulement, Phase 06), ventes, multi-magasin et emplacements, numéros de lot / dates de péremption, numéros de série, lecture de codes-barres (seul un champ EAN / GTIN facultatif est proposé), étiquettes, temps réel, import de catalogues fabricants, valorisation comptable, exports comptables. | Inscrits au backlog BL-05-01 à BL-05-06. **À vérifier par G05-LEGAL** : traçabilité des dispositifs médicaux (lentilles, verres) par lot ou identifiant unique au Maroc et en Belgique ; si elle s'avère obligatoire avant la vente, elle remonte avant la Phase 06. |
| B | Inclure lots / péremption des lentilles et produits d'entretien dès 05-B. | Sécurise la traçabilité, mais alourdit chaque mouvement et l'inventaire. |

## D-05-13 — Découpage et revues SQL

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Trois sous-lots, **une migration par sous-lot**, chacune soumise à sa propre revue SQL : 05-A Catalogue + Fournisseurs (`0007`), 05-B Stock et mouvements (`0008`), 05-C Inventaires + exports + clôture (`0009`). Module visible derrière un interrupteur de build tant que la phase n'est pas close. | Revues SQL plus courtes ; 05-A utilisable seul (référentiel). |
| B | Une seule migration en fin de phase. | Une revue, mais tardive et volumineuse ; 05-A non testable sur PostgreSQL réel avant la fin. |

---

## Formulaire de réponse

```text
D-05-01 : A / B / correction :
D-05-02 : A / B / C / correction :   (ratification explicite des trois politiques)
D-05-03 : A / B / C / correction :
D-05-04 : A / B / C / correction :
D-05-05 : A / B / C / correction :   liste des catégories :
D-05-06 : A / B / correction :
D-05-07 : A / B / correction :
D-05-08 : A / B / C / correction :   devise de l'installation pilote (MAD / EUR) :
D-05-09 : A / B / correction :       liste des motifs :
D-05-10 : A / B / C / correction :
D-05-11 : A / B / correction :
D-05-12 : A / B / correction :
D-05-13 : A / B / correction :
```
