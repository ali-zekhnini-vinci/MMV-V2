# 05 — Registre des décisions métier de la Phase 05

**Mise à jour 30/10/2026 — validation métier du propriétaire (G05-METIER).** Le propriétaire a répondu aux questions Q01 à Q20 ([`prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md`](../../../prompts/MMV_V2_PHASE05_00_VALIDATION_METIER_2026-10-30.md)). Chaque décision porte désormais un bloc **Statut 30/10/2026** ; le texte proposé le 29/10/2026 est **conservé à l'identique** sous chaque bloc (historique, version initiale au commit `6c207f9`). Aucun identifiant n'est renuméroté ; les décisions nouvelles reçoivent les numéros D-05-14 à D-05-21. Le nom du fichier est conservé pour ne pas casser les liens.

Statuts : **`APPROVED`** (ratifié par le propriétaire, citation Qnn) · **`APPROVED (corrigée)`** (ratifié avec une règle différente de la recommandation du 29/10) · **`PROPOSED`** (non ratifié, question ouverte) · **`SUPERSEDED`** (remplacé par une autre décision). Aucune règle clinique, fiscale ou juridique n'est déclarée approuvée sans validation : taux et catégories fiscales, traçabilité réglementaire des dispositifs médicaux et durées de conservation restent ouverts (G05-LEGAL, ADR-010).

Toujours vrai au 30/10/2026 : aucune migration, aucune table, aucune permission n'a été créée ; aucune donnée pilote n'a été écrite.

## Correspondance Q01–Q20 → D-05

| Q | Réponse du propriétaire (résumé) | Décision | Statut |
|---|---|---|---|
| Q01 | six familles | D-05-05 | APPROVED (corrigée) |
| Q02 | modèle produit + variantes ; SKU, prix, stock par variante | D-05-04 | APPROVED (corrigée : option B) |
| Q03 | catalogue des verres configurable (fabricant, gamme, indice, traitements) | D-05-21 (nouvelle) ; D-05-05 | APPROVED |
| Q04 | politique de stock par variante : stocké, commandé à la demande, service | D-05-02 | APPROVED (corrigée : portée par la variante) |
| Q05 | SKU interne unique, références fabricant et fournisseur, code-barres facultatif | D-05-14 (nouvelle) | APPROVED |
| Q06 | multi-emplacements et multi-magasins préparés | D-05-15 (nouvelle) ; D-05-12 | APPROVED |
| Q07 | stock par quantité et variante ; traçabilité individuelle ultérieure | D-05-04 ; D-05-12 | APPROVED |
| Q08 | stock négatif interdit | D-05-03 | APPROVED (option A) |
| Q09 | réservation à la validation d'une vente ou commande, pas au devis | D-05-17 (nouvelle) | APPROVED |
| Q10 | inventaires complets ou partiels, comptages, écarts justifiés, validation | D-05-10 | APPROVED (principe) |
| Q11 | plusieurs fournisseurs par article, dont un principal | D-05-06 | APPROVED (principe) |
| Q12 | fournisseurs, prix d'achat, réceptions simples ; commandes fournisseurs reportées | D-05-12 ; D-05-06 | APPROVED |
| Q13 | prix par variante, coefficient multiplicateur, prix manuel autorisé | D-05-16 (nouvelle) | APPROVED |
| Q14 | devises et fiscalité configurables par magasin | D-05-15 (nouvelle) | APPROVED (structure) ; contenu fiscal non ratifié |
| Q15 | historique des prix, aucune modification rétroactive des ventes | D-05-16 ; D-05-08 | APPROVED |
| Q16 | permissions distinctes : catalogue, tarifs, coûts, fournisseurs, stock, inventaires | D-05-11 | APPROVED (principe) ; liste exacte PROPOSED |
| Q17 | import CSV contrôlé : architecture prévue, réalisation avant le premier déploiement | D-05-18 (nouvelle) | APPROVED |
| Q18 | alertes de stock faible, seuil configurable | D-05-19 (nouvelle) | APPROVED |
| Q19 | prix de vente HT = prix d'achat HT × coefficient (11 règles) | D-05-16 (nouvelle) | APPROVED ; règle d'arrondi PROPOSED |
| Q20 | deux environnements pilotes fictifs : Belgique EUR, Maroc MAD ; aucune conversion | D-05-20 (nouvelle) ; D-05-15 | APPROVED |
| mandat § 2 | Lot 6 = référence visuelle et ergonomique uniquement | D-05-01 | APPROVED (option A) |

Non répondues (toujours `PROPOSED`) : D-05-07 (archivage), D-05-09 (motifs et mode d'ajustement), D-05-13 (découpage). Questions ouvertes consolidées : § « Questions restantes » en fin de document.

---

## D-05-01 — Statut du Lot 6 : référence UX seulement

> **Statut 30/10/2026 : `APPROVED` — option A** (mandat de validation, § 2 : « Il sert uniquement de référence visuelle et ergonomique » ; interdiction de consulter ou réutiliser l'ancien dépôt, son code, ses DTO, son modèle de données ou ses migrations). Copie de travail de la maquette : `references/phase05/lot6/` (non versionnée ; empreintes dans [`SOURCE_MAP.md`](SOURCE_MAP.md)).

**Constat (O-05-01).** L'en-tête et les notes de conception de `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` déclarent avoir été dessinés « champs limités au code réel » d'un **dépôt de l'ancien logiciel** (noms d'entités, d'énumérations, de cas d'usage et de DTO, catégories figées, suppression physique en cascade, fournisseur sans statut, seuil par défaut, formule de marge, visibilité des prix par rôle). Ces informations proviennent du texte de la maquette elle-même ; l'ancien dépôt n'a pas été consulté.

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Le Lot 6 est la référence **d'interface** (mise en page, états, interactions, accessibilité). Toutes les listes de champs, énumérations et règles sont redécidées par ce cadrage ; aucun nom d'entité ou de DTO de la maquette n'est repris. | Conforme à D-000 et `NEW_RULES_ONLY`. Les planches qui illustrent une limite de l'ancien code sont **adaptées ou remplacées** (suppression physique, fournisseur sans statut, historique d'inventaire « reconstitué », conflit « cible UI ») — voir [`MATRICE_LOT6.md`](MATRICE_LOT6.md). |
| B | Adopter les listes de champs du Lot 6 telles quelles. | Incompatible avec D-000 (règles héritées). Non recevable. |

## D-05-02 — Trois politiques d'article

> **Statut 30/10/2026 : `APPROVED (corrigée)` — Q04.** Les trois politiques sont **ratifiées explicitement** : `stocked` (stocké), `order_only` (commandé à la demande), `service` (service non stockable). **Correction : la politique est configurable par variante** (D-05-04), pas par article unique ni par famille. Deux variantes d'un même modèle peuvent avoir des politiques différentes.
>
> Restent `PROPOSED` : (a) conditions de changement de politique (proposition inchangée : vers ou depuis `stocked`, quantité en main = 0 dans tous les emplacements, aucune réservation, aucun inventaire ouvert ; audit) ; (b) la ligne « fournisseur principal obligatoire » du tableau ci-dessous est **remplacée par la règle proposée en D-05-06** (fiche incomplète permise, fournisseur principal exigé à la commande effective) — question ouverte QR-01.

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

> **Statut 30/10/2026 : `APPROVED` — option A** (Q08 : « Stock négatif interdit »). Contrainte appliquée par **(variante, emplacement)** (D-05-15). L'option C (dérogation) n'est pas retenue.

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Interdit.** Toute sortie (manuelle, puis vente en Phase 06) qui rendrait la quantité négative est refusée (`422 stock_insufficient`), contrôle dans la transaction verrouillée. | Intégrité garantie par contrainte SQL `on_hand >= 0`. En Phase 06, un article stocké en rupture ne peut pas être vendu « en stock » : enregistrer la réception d'abord, ou le vendre comme commande (décision de la Phase 06). |
| B | Autorisé avec alerte. | Vente possible avant saisie de la réception ; les inventaires deviennent la seule correction ; écarts difficiles à expliquer. |
| C | Interdit, sauf dérogation par une permission dédiée avec motif. | Ajoutable plus tard à partir de A sans migration de données. |

## D-05-04 — Variantes

> **Statut 30/10/2026 : `APPROVED (corrigée)` — option B** (Q02 : « Modèle produit + variantes. SKU, prix et stock par variante » ; Q07 : « Stock physique géré par quantité et variante ; traçabilité individuelle possible ultérieurement »). Le **modèle produit** est obligatoire et ne porte ni prix ni stock ; la **variante** est l'unité de vente, de prix (par magasin, D-05-16) et de stock (par emplacement, D-05-15). Un modèle peut n'avoir qu'une variante. Pas de numéro de série ni de lot en Phase 05 (BL-05-03).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Un article = une unité de stock** (une couleur, un calibre). Regroupement **facultatif** sous un « modèle » (marque + nom du modèle) pour afficher les autres coloris / tailles. Attributs propres à l'article (couleur, calibre, pont, branche…). | Le stock, les prix et l'inventaire restent au niveau article ; aucune quantité au niveau modèle. Simple pour la Phase 06 (une ligne = un article). |
| B | Produit parent obligatoire + table de variantes portant le stock. | Plus structuré, mais double niveau partout (prix par variante ou par parent, archivage partiel). |
| C | Aucun regroupement. | Le plus simple ; doublons de saisie pour les coloris. Migrable vers A plus tard (colonne facultative). |

## D-05-05 — Catégories et profondeur du catalogue verres / lentilles

> **Statut 30/10/2026 : `APPROVED (corrigée)` — Q01 et Q03.** Liste fermée de **six familles** : montures optiques, lunettes de soleil, verres correcteurs, lentilles et solutions, accessoires, prestations (codes proposés : `optical_frames`, `sunglasses`, `ophthalmic_lenses`, `contact_lenses_solutions`, `accessories`, `services`). « Produits d'entretien » est absorbé par « lentilles et solutions » ; « Services » devient « prestations ». **Correction de la profondeur verres** : le catalogue des verres est **configurable** par fabricant, gamme, indice et traitements (D-05-21) — l'option A (« sans catalogue fabricant ») n'est plus retenue sur ce point.
>
> Restent `PROPOSED` : jeu d'attributs de chaque famille (défini dans le code, validé par le serveur) ; politiques admises par famille (proposition : `prestations` → `service` seulement, autres familles → `stocked` ou `order_only`) ; paramètres des lentilles de contact (QR-07).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Liste fermée de catégories** fournie par le propriétaire (proposition : Montures optiques, Solaires, Verres, Lentilles de contact, Produits d'entretien, Accessoires, Services), un niveau, chaque catégorie ayant un **jeu d'attributs défini dans le code** et validé par le serveur. Verres = articles de gamme `order_only` **sans grille de puissances** (les paramètres viennent de l'ordonnance au moment de la vente / commande). Lentilles : `order_only` par défaut ; une puissance tenue en stock = un article `stocked` distinct. | Pas de catalogue fabricant de verres en Phase 05 ; la tarification des verres « sur devis » relève de la Phase 06 (D-05-08). Ajouter une catégorie = petite évolution, pas de saisie libre d'attributs. |
| B | Catégories et attributs entièrement paramétrables par l'utilisateur (clé / valeur). | Souple mais non validable ; recherche et contrôles faibles ; complexité d'écran. |
| C | Catalogue verres complet (indices, traitements, plages de puissances, tarifs fabricant). | Hors périmètre (Phase Commandes / verres) ; volume et mises à jour fabricants. |

## D-05-06 — Fournisseurs et rattachement article ↔ fournisseur

> **Statut 30/10/2026 : `APPROVED` (principe) — Q11, Q05, Q12.** Plusieurs fournisseurs possibles **par variante**, dont **au plus un fournisseur principal** ; pour chaque rattachement : référence fournisseur, prix d'achat fournisseur HT (protégé, avec sa devise), délai indicatif. Références fabricant et fournisseur distinctes (D-05-14). Périmètre fournisseur de la Phase 05 limité aux fiches, prix d'achat et **réceptions simples** (Q12).
>
> **Divergence corrigée — fournisseur principal des variantes commandées (`order_only`).** La proposition du 29/10 (principal obligatoire dès l'enregistrement) est remplacée par la règle suivante, **`PROPOSED`** (QR-01) :
> - **création ou modification de fiche** : une variante `order_only` peut être enregistrée **sans fournisseur principal** ; elle est alors signalée **« fiche incomplète — fournisseur principal manquant »** (indicateur calculé par le serveur, filtre de liste) ;
> - **commande effective** : la commande fournisseur (phase Commandes, BL-05-01) **exige** un fournisseur principal actif ; proposé aussi : la validation d'une vente ou commande client contenant cette variante (Phase 06) est refusée tant que la fiche est incomplète (`422 primary_supplier_required`), le devis restant possible ;
> - archiver l'unique fournisseur principal d'une variante `order_only` active n'est plus refusé : la variante redevient « incomplète » (avertissement explicite avant confirmation).
>
> Restent `PROPOSED` : champs de la fiche fournisseur, identifiants légaux selon le pays (formats seulement, aucune règle juridique déclarée), statut actif / archivé.

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Fiche : nom, code interne unique, pays, contact (un nom facultatif, e-mail, téléphone), adresse, notes, **identifiants légaux facultatifs selon le pays** (Maroc : ICE, IF, RC ; Belgique : numéro d'entreprise / TVA), **statut actif / archivé**. Un article peut avoir **plusieurs fournisseurs** ; pour chacun : référence fournisseur, prix d'achat fournisseur (protégé), délai indicatif ; **au plus un fournisseur principal**. | Diffère volontairement du Lot 6 (un seul fournisseur, pas de statut, pas d'identifiants) : planches B-128 et B-129 adaptées. Prépare la Phase Commandes sans la développer. |
| B | Un seul fournisseur par article (référence directe). | Plus simple ; bloque le multi-sourcing et l'historique des prix par fournisseur. |

## D-05-07 — Archivage plutôt que suppression

> **Statut 30/10/2026 : `PROPOSED`** — non traité par les réponses Q01–Q20 (QR-02). Adaptation proposée au modèle validé : l'archivage s'applique au modèle, à la variante et au fournisseur ; archiver une variante `stocked` est refusé si sa quantité en main ou réservée est > 0 dans un emplacement quelconque, ou si elle figure dans un inventaire ouvert ; archiver un modèle archive ses variantes sous les mêmes conditions.

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Aucune suppression physique** d'article, de fournisseur, de mouvement ni d'inventaire. Archivage + réactivation, motif facultatif, audit. Archiver un article `stocked` est **refusé si la quantité en main > 0** ou s'il figure dans un inventaire ouvert. Un article archivé n'est ni vendable ni proposé aux nouveaux inventaires ; il reste visible dans l'historique. Un fournisseur archivé ne peut plus être choisi comme principal ; ses rattachements restent lisibles. | Cohérent avec Clients (archivage, D-03) et avec la conservation comptable. Les planches de suppression (B-109, B-129) deviennent des planches d'archivage. |
| B | Suppression physique tant qu'aucune référence n'existe (comportement du Lot 6). | Ajoute un chemin destructif et des refus « clé étrangère » ; peu d'intérêt une fois l'archivage disponible. |

## D-05-08 — Prix, devise, fiscalité et coûts

> **Statut 30/10/2026 : `SUPERSEDED` en partie.** L'option A du 29/10 (prix de vente **TTC** saisi, **devise unique de l'installation**) **n'est pas retenue** : remplacée par **D-05-16** (prix de vente HT = prix d'achat HT × coefficient, TTC calculé séparément — Q13, Q19) et **D-05-15** (devise et fiscalité **par magasin** — Q14, Q20). Restent valables de l'option A : prix d'achat protégés par permission (Q16), **historique des prix** et **aucune modification rétroactive des ventes** (Q15, `APPROVED`), catégorie fiscale renseignée seulement après validation (ADR-010). Reste `PROPOSED` : aucune valorisation comptable du stock (CUMP / FIFO) en Phase 05, coût unitaire de réception conservé (cohérent avec Q12, non ratifié explicitement — BL-05-06).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Prix de vente public TTC**, montant décimal à 2 décimales, dans la **devise unique de l'installation** (MAD ou EUR, fixée à l'installation, enregistrée avec chaque prix). **Catégorie fiscale** de l'article : champ prévu, **renseigné après ADR-010** (taux réels Maroc / Belgique à confirmer par un fiscaliste). Prix de vente **facultatif seulement pour `order_only`** (« prix à la commande »). **Prix d'achat de référence** et prix d'achat fournisseur **protégés** (permission dédiée, D-05-11). **Historique des prix** (ancien → nouveau, auteur, date). Marge calculée par le serveur, visible avec la seule permission des coûts. **Aucune valorisation de stock** (CUMP / FIFO) en Phase 05 : coût unitaire saisi à la réception, conservé pour plus tard. | La Phase 06 copie libellé, référence, prix et catégorie fiscale sur la ligne de vente (instantané) et ne relit plus le catalogue. Une vente réelle reste bloquée tant qu'ADR-010 n'est pas tranché. |
| B | Prix HT + taux de TVA par article. | Calculs TTC à l'affichage ; arrondis à maîtriser ; prématuré sans ADR-010. |
| C | Valorisation CUMP dès 05-B. | Méthode comptable à valider par le comptable ; complexité des corrections. |

## D-05-09 — Entrées, sorties, ajustements et motifs

> **Statut 30/10/2026 : `PROPOSED`** — non traité par Q01–Q20, sauf deux points `APPROVED` : réceptions simples (Q12) et écarts d'inventaire justifiés (Q10, motif obligatoire). Liste des motifs et mode d'ajustement (quantité constatée + attendue) à ratifier (QR-03). Adaptation au modèle validé : chaque mouvement porte la variante **et l'emplacement** (D-05-15) ; une sortie manuelle ne peut pas descendre sous la quantité réservée (`422 stock_reserved`, D-05-17).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Journal de mouvements **en ajout seul** ; chaque mouvement porte quantité avant / après, écart signé, motif codé, commentaire facultatif, auteur, date, référence de source. **Entrée** (réception fournisseur ou entrée manuelle) et **sortie manuelle** : quantité > 0, motif obligatoire. **Ajustement** : l'utilisateur saisit la **quantité constatée** ; le serveur calcule l'écart à partir de la quantité verrouillée et exige la **quantité attendue** affichée à l'utilisateur (sinon `409` « modifié sur un autre poste ») ; confirmation obligatoire. Aucune modification d'un mouvement : correction = nouveau mouvement motivé. **Liste fermée de motifs** à ratifier (proposition : réception fournisseur, retour fournisseur, casse, perte, vol constaté, erreur de saisie, usage interne / démonstration, correction d'inventaire, autre + commentaire obligatoire). | Répond aux questions ouvertes de la maquette (quantité avant / après, auteur visible). Une sortie liée à une vente (Phase 06) ne sera jamais saisie à la main. |
| B | Ajustement saisi comme écart signé (+3 / −2). | Pas de détection naturelle du conflit ; risque de double correction. |

## D-05-10 — Inventaires

> **Statut 30/10/2026 : `APPROVED` (principe) — Q10** : inventaires **complets ou partiels**, **comptages**, **écarts justifiés** (motif obligatoire), **validation**. Adaptation : une session porte sur **un magasin** et un ou plusieurs de ses emplacements (D-05-15) ; périmètre complet ou partiel (famille, sélection de variantes).
>
> Restent `PROPOSED` (QR-04) : sessions persistées et numérotées ; mouvements autorisés pendant la session (pas de gel) avec détection des lignes modifiées depuis le comptage ; comptage à l'aveugle en option ; traitement explicite des lignes non comptées ; **séparation des permissions de comptage et de validation** (voir D-05-11).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **Sessions d'inventaire persistées** et numérotées ; périmètre : tout le stock, une catégorie, ou une sélection d'articles `stocked` actifs ; **un seul inventaire ouvert par article**. Comptage **non aveugle** par défaut (quantité théorique affichée), option « comptage à l'aveugle » à l'ouverture. Les mouvements restent **autorisés** pendant la session : chaque ligne mémorise la quantité théorique **au moment du comptage** ; à la validation, si la quantité a changé depuis, la ligne est signalée (recompter ou accepter explicitement). **Validation atomique** de tous les écarts (mouvements « correction d'inventaire » liés à la session), motif obligatoire pour chaque écart, permission **distincte du comptage**. Session close ou abandonnée = **immuable** ; abandon irréversible, sans effet sur le stock, conservé. | Fournit une preuve d'inventaire exploitable (inventaire annuel des commerçants au Maroc et en Belgique, durées de conservation à confirmer avec le comptable). Les planches « historique reconstitué (cible) » deviennent un historique réel. |
| B | Gel du stock : sorties bloquées pendant la session. | Plus simple à valider, mais bloque les ventes du magasin pendant le comptage. |
| C | Pas de session : ajustements simples (comportement de base du Lot 6). | Pas d'historique d'inventaire ni de preuve de comptage. |

## D-05-11 — Permissions

> **Statut 30/10/2026 : `APPROVED` (principe) — Q16 et Q19** : permissions **distinctes** pour six domaines — catalogue, tarifs, coûts, fournisseurs, stock, inventaires — et **permission spécifique pour le prix manuel**. Aucune permission n'en implique une autre ; `system.admin` n'en donne aucune ; coûts retirés des réponses par le serveur (inchangé).
>
> **Liste exacte `PROPOSED`** (QR-05), remplaçant la liste de douze du 29/10 : `catalog.read`, `catalog.write`, `catalog.archive`, `catalog.configure` (référentiel verres, D-05-21), `catalog.import` (D-05-18) · `pricing.write` (coefficient, recalcul explicite), `pricing.manual` (prix manuel) · `costs.read`, `costs.write` · `suppliers.read`, `suppliers.write` · `stock.read`, `stock.movements.write` (réceptions simples, entrées, sorties), `stock.adjust` · `inventory.count`, `inventory.validate`. **Divergence comptage / validation** : Q10 et Q16 imposent des permissions d'inventaire distinctes du stock ; la séparation `inventory.count` ≠ `inventory.validate` est recommandée (le compteur ne valide pas ses propres écarts) mais **reste à confirmer**. Les magasins et la configuration fiscale sont administrés en Phase 05 par la commande de maintenance (aucune permission applicative). Les profils du Lot 1 restent de simples modèles d'attribution ([`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md) § 2).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Douze permissions explicites, aucune n'en impliquant une autre, `system.admin` n'en donne aucune : `catalog.read`, `catalog.write`, `catalog.archive`, `catalog.costs.read`, `catalog.costs.write`, `suppliers.read`, `suppliers.write`, `stock.read`, `stock.movements.write`, `stock.adjust`, `stock.inventory.count`, `stock.inventory.validate`. Historique d'audit : `audit.read` **et** le droit de lecture du module. Les profils du Lot 1 (propriétaire, responsable, opticien, vendeur, technicien / accueil) ne sont que des **modèles d'attribution** documentés, jamais des droits. | Les prix d'achat sont **retirés des réponses par le serveur** (jamais masqués par l'interface seulement). Une migration doit étendre la contrainte de permissions (`DROP` + `ADD CONSTRAINT`, procédé O-03-24). |
| B | Permissions grossières (`catalog.*`, `stock.*`). | Moins d'attributions, mais impossible de séparer comptage et validation, ou de cacher les coûts aux vendeurs. |

## D-05-12 — Hors périmètre de la Phase 05

> **Statut 30/10/2026 : `APPROVED (corrigée)`.** Confirmés **OUT** : commandes fournisseurs complètes et réceptions rapprochées (Q12 → BL-05-01) ; réservations effectives et ventes (Phase 06 ; règle de réservation approuvée en D-05-17) ; traçabilité individuelle — numéros de série, lots, péremption (Q07 « ultérieurement » → BL-05-03, sous réserve de G05-LEGAL) ; valorisation et exports comptables (BL-05-06). **Ne sont plus OUT** : multi-magasins et multi-emplacements, dont l'**architecture est préparée** dès la Phase 05 (Q06, D-05-15 ; les transferts entre emplacements ou magasins restent OUT, BL-05-04) ; **import CSV contrôlé**, dont l'architecture est conçue en Phase 05 et la réalisation exigée **avant le premier déploiement** (Q17, D-05-18) ; code-barres comme champ facultatif (Q05 ; lecture par scanner et étiquettes restent OUT, BL-05-05).

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | **OUT** : commandes fournisseurs, réceptions rapprochées d'une commande, réservations effectives (contrat seulement, Phase 06), ventes, multi-magasin et emplacements, numéros de lot / dates de péremption, numéros de série, lecture de codes-barres (seul un champ EAN / GTIN facultatif est proposé), étiquettes, temps réel, import de catalogues fabricants, valorisation comptable, exports comptables. | Inscrits au backlog BL-05-01 à BL-05-06. **À vérifier par G05-LEGAL** : traçabilité des dispositifs médicaux (lentilles, verres) par lot ou identifiant unique au Maroc et en Belgique ; si elle s'avère obligatoire avant la vente, elle remonte avant la Phase 06. |
| B | Inclure lots / péremption des lentilles et produits d'entretien dès 05-B. | Sécurise la traçabilité, mais alourdit chaque mouvement et l'inventaire. |

## D-05-13 — Découpage et revues SQL

> **Statut 30/10/2026 : `PROPOSED` (révisé)** — non traité par Q01–Q20 (QR-06). Séquence révisée pour intégrer magasins, variantes, tarifs et import : **05-A** référentiels, catalogue, tarifs et fournisseurs (`0007`) → **05-B** stock par emplacement, réceptions simples, alertes (`0008`) → **05-C** inventaires, exports, intégration réelle et clôture (`0009`) ; **import CSV** en lot distinct (`0010`), avant le premier déploiement. Détail : [`CADRAGE_05_00.md`](CADRAGE_05_00.md) § 8.5.

| Option | Contenu | Implications |
|---|---|---|
| **A (recommandée)** | Trois sous-lots, **une migration par sous-lot**, chacune soumise à sa propre revue SQL : 05-A Catalogue + Fournisseurs (`0007`), 05-B Stock et mouvements (`0008`), 05-C Inventaires + exports + clôture (`0009`). Module visible derrière un interrupteur de build tant que la phase n'est pas close. | Revues SQL plus courtes ; 05-A utilisable seul (référentiel). |
| B | Une seule migration en fin de phase. | Une revue, mais tardive et volumineuse ; 05-A non testable sur PostgreSQL réel avant la fin. |

---

## Décisions nouvelles (30/10/2026)

## D-05-14 — Identifiants des variantes

> **Statut : `APPROVED` — Q05.**

- **SKU interne** obligatoire, **unique** dans toute l'installation (tous magasins), porté par la variante ; comparaison insensible à la casse et aux espaces de bord.
- **Référence fabricant** (sur la variante, facultative) et **références fournisseur** (sur chaque rattachement fournisseur, D-05-06) : distinctes du SKU, non uniques entre fournisseurs.
- **Code-barres facultatif** (EAN / GTIN), unique s'il est renseigné ; contrôle de format et de clé proposé (`PROPOSED`). Aucune lecture par scanner en Phase 05.
- Règle de génération du SKU (saisie libre ou numérotation proposée par le serveur) : `PROPOSED` (QR-08).

## D-05-15 — Magasins, emplacements, devise et fiscalité par magasin

> **Statut : `APPROVED` (architecture et devise) — Q06, Q14, Q20.** **Contenu fiscal non ratifié** (ADR-010 `OPEN DECISION`, G05-LEGAL).

- Entité **magasin** : pays, **devise** (la devise appartient au magasin), profil fiscal. Entité **emplacement** rattachée à un magasin ; chaque magasin a un emplacement par défaut.
- Le catalogue (modèles, variantes, SKU, fournisseurs) est **commun** à l'installation ; les **prix et coefficients** sont par (variante, magasin) ; le **stock** est par (variante, emplacement) ; seuils par (variante, magasin).
- **Aucune conversion automatique** entre devises ; aucun montant de devises différentes n'est additionné ni comparé ; aucun taux de change stocké.
- Fiscalité **configurable par magasin** : structure prévue, **aucun taux, aucune catégorie fiscale, aucune règle d'arrondi de TVA inventés** ; le TTC n'est calculé que lorsque la fiscalité du magasin est validée (D-05-16).
- Préparé mais hors Phase 05 : transferts entre emplacements / magasins (BL-05-04), permissions limitées à un magasin (BL-05-07). Spécification : [`CADRAGE_05_00.md`](CADRAGE_05_00.md) § 8.3.

## D-05-16 — Prix de vente HT par coefficient multiplicateur

> **Statut : `APPROVED` — Q13, Q15, Q19.** Règle d'arrondi exacte : **`PROPOSED`** (Q19 : « Arrondi monétaire explicite à spécifier » — QR-09).

**Prix de vente HT = prix d'achat HT × coefficient multiplicateur**, par variante et par magasin : prix d'achat HT enregistré ; coefficient configurable par variante ; prix de vente HT calculé ; prix manuel possible avec permission spécifique ; recalcul explicite, jamais silencieux ; historisation de toutes les modifications ; précision décimale adaptée, aucun calcul monétaire en flottants binaires ; arrondi monétaire explicite ; prix TTC calculé séparément à partir de la fiscalité **validée** du magasin ; aucun taux inventé ; aucune modification rétroactive des ventes. Spécification complète : [`CADRAGE_05_00.md`](CADRAGE_05_00.md) § 8.2.

## D-05-17 — Réservations

> **Statut : `APPROVED` (règle) — Q09.** Réalisation : Phase 06 (ventes) et phase Commandes.

- Une réservation est créée **à la validation d'une vente ou d'une commande client**, **jamais automatiquement au devis**.
- Disponible = quantité en main − quantité réservée, par (variante, emplacement) ; calcul unique serveur.
- Proposé (`PROPOSED`, QR-10) : en 05-B, le solde porte déjà la colonne `reserved` (toujours 0) pour figer la formule ; les écritures de réservation et la table `stock.reservations` sont livrées par la Phase 06 ; les réservations ne concernent que les variantes `stocked` ; une sortie manuelle ne peut pas entamer le réservé.

## D-05-18 — Import CSV contrôlé

> **Statut : `APPROVED` — Q17** : architecture prévue en Phase 05, **réalisation avant le premier déploiement**.

Architecture proposée (`PROPOSED` dans le détail) : dépôt du fichier → zone de préparation → **contrôle à blanc** avec rapport par ligne (aucune écriture au catalogue) → **application explicite**, tout ou rien, idempotente, auditée ; permission `catalog.import` ; coûts importés seulement avec `costs.write` ; aucun calcul monétaire en flottant ; aucune conversion de devise (la devise du fichier doit être celle du magasin cible) ; neutralisation des formules ; aucune donnée réelle importée avant l'autorisation du propriétaire. Spécification : [`CADRAGE_05_00.md`](CADRAGE_05_00.md) § 8.6.

## D-05-19 — Alertes de stock faible

> **Statut : `APPROVED` — Q18.**

Seuil **configurable par variante** (et par magasin, D-05-15), facultatif ; état « stock faible » quand 0 < disponible ≤ seuil, « rupture » à 0, « seuil non défini » si vide ; liste et compteur d'alertes calculés par le serveur. Proposé (`PROPOSED`) : aucune notification active (courriel, système) en Phase 05 ; aucun seuil par défaut imposé.

## D-05-20 — Environnements pilotes fictifs Belgique (EUR) et Maroc (MAD)

> **Statut : `APPROVED` — Q20.**

Deux environnements pilotes **fictifs** sont prévus : **Belgique — EUR** et **Maroc — MAD**. La devise appartient au magasin ; aucune conversion automatique. **À cette étape : aucun vrai compte créé, aucune donnée pilote écrite dans PostgreSQL** (ni `mmv_v2_dev`, ni `mmv_v2_test`). Proposé (`PROPOSED`, QR-11) : les tests automatisés de 05-A à 05-C créent les deux magasins fictifs dans `mmv_v2_test` (données fictives, résidu 0), après chaque G05-SQL ; la forme des pilotes réels (deux installations ou une installation à deux magasins) reste à décider.

## D-05-21 — Catalogue des verres configurable

> **Statut : `APPROVED` — Q03.**

Référentiel configurable des verres correcteurs : **fabricants**, **gammes** (par fabricant), **indices**, **traitements**. Aucune valeur pré-remplie (ni fabricant, ni indice, ni traitement) : saisie par un utilisateur autorisé (`catalog.configure`, proposé). Proposé (`PROPOSED`, QR-07) : une gamme est un modèle produit de la famille « verres correcteurs » ; une variante = indice + combinaison de traitements, avec SKU, prix et politique (`order_only` par défaut) ; les puissances viennent de la prescription au moment de la vente ou de la commande (pas de grille de puissances en Phase 05) ; les tarifs fabricant importés relèvent de D-05-18.

---

## Questions restantes (owner)

| ID | Question | Décision concernée | Proposition |
|---|---|---|---|
| QR-01 | Fournisseur principal d'une variante `order_only` : exigé à la commande effective (et à la validation d'une vente / commande client), pas à la création de la fiche ? | D-05-02, D-05-06 | oui ; fiche « incomplète » signalée |
| QR-02 | Archivage au lieu de suppression (modèle, variante, fournisseur) | D-05-07 | option A adaptée |
| QR-03 | Liste des motifs de mouvement ; ajustement par quantité constatée + attendue | D-05-09 | option A |
| QR-04 | Sessions d'inventaire : pas de gel, comptage à l'aveugle optionnel, lignes non comptées exclues ou mises à 0 sur choix explicite | D-05-10 | option A |
| QR-05 | Liste exacte des permissions, dont séparation `inventory.count` / `inventory.validate` | D-05-11 | liste du statut D-05-11 |
| QR-06 | Séquence 05-A / 05-B / 05-C + lot import | D-05-13 | séquence révisée |
| QR-07 | Paramètres des lentilles de contact ; tarification des traitements de verres (inclus dans la variante ou supplément) | D-05-05, D-05-21 | variante = indice + traitements |
| QR-08 | SKU saisi ou proposé par le serveur | D-05-14 | saisi, avec proposition facultative |
| QR-09 | Règle d'arrondi du prix de vente HT | D-05-16 | centime, demi vers l'extérieur (§ 8.2) |
| QR-10 | Colonne `reserved` dès 05-B ; réservations limitées aux variantes `stocked` | D-05-17 | oui |
| QR-11 | Forme des pilotes réels (deux installations / une installation à deux magasins) ; magasins fictifs dans `mmv_v2_test` après G05-SQL | D-05-20 | tests : une base, deux magasins |
| QR-12 | Coefficient par défaut (famille ou modèle) proposé à la création d'une variante ; coefficient < 1 | D-05-16 | aucun défaut imposé ; < 1 accepté avec confirmation |
| QR-13 | Valorisation du stock hors Phase 05 | D-05-08 | aucune valorisation (BL-05-06) |

Ouverts hors du propriétaire seul (conseil fiscal / juridique) : taux et catégories fiscales par magasin, règle d'arrondi de la TVA (ADR-010) ; traçabilité des dispositifs médicaux, conservation des inventaires (G05-LEGAL).

---

## Formulaire de réponse (29/10/2026, historique)

Formulaire du cadrage initial, conservé tel quel ; les réponses Q01–Q20 du 30/10/2026 y répondent selon la correspondance ci-dessus. Formulaire des questions restantes : QR-01 à QR-13.

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
