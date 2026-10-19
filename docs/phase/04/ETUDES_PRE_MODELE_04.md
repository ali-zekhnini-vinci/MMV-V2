# 04 — Études préalables au modèle de données (complément 04-00, 19/10/2026)

**Statut : `PROPOSED`.** Mandat : [`prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_00_SUITE_GO_CLAUDE_CODE.md) (suite de la revue indépendante : G04-00 « cadrage techniquement acceptable »). Documentation seulement : aucun SQL, aucune migration, aucune base contactée, aucun code applicatif. Chaque étude se termine par une **question nouvelle** (D-04-10 à D-04-14, `PROPOSED`) ajoutée au formulaire consolidé ([`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md) § Complément). D-04-01 reste la seule décision approuvée ; D-04-02 à D-04-09 restent `PROPOSED` et ne sont pas modifiées.

Le modèle n'est **pas figé** : les amendements datés de [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md), [`API_OPTICAL_PROPOSED.md`](API_OPTICAL_PROPOSED.md) et [`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md) décrivent la forme qu'il prendrait si les recommandations ci-dessous étaient ratifiées.

---

## E-01 — Rattachement historisé des pièces source aux révisions (→ D-04-10)

**Constat.** Le modèle 04-00 rattache une pièce à l'**en-tête** de l'ordonnance (« pas à une révision », [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) § 2.4) pour satisfaire J-04-06 (« toute pièce liée conserve son rattachement même si une version est remplacée »). Ce choix ne dit pas **quelle pièce a servi à quelle révision**.

**Cas qui le révèlent.** (1) Révision 1 transcrite d'un recto ; le verso arrive plus tard et motive la révision 2 (« complément d'information ») : la révision 1 n'a jamais reposé sur le verso. (2) Une pièce est invalidée (jointe par erreur) après la confirmation d'une révision qui s'appuyait sur elle : il faut continuer à montrer ce qui fondait cette révision. (3) Une vente future référence `(type, id, revisionNo)` : la preuve documentaire de **cette** révision doit rester identifiable.

| Option | Description | Évaluation |
|---|---|---|
| A | rattachement à l'en-tête seulement (04-00) | simple ; **perd l'historique** du fondement de chaque révision |
| **B** | la pièce appartient à l'en-tête (stockée une fois) ; un **lien révision ↔ pièce** est enregistré pour chaque révision ; modifiable tant que la révision est un brouillon, **figé à la confirmation** avec la révision | conserve J-04-06 et l'historique ; aucun octet dupliqué |
| C | la pièce appartient à une révision ; copie à chaque nouvelle révision | duplication du contenu, sauvegardes alourdies (E-05), empreintes multiples pour un même document |

**Recommandation : B**, avec les règles suivantes.

1. **Contenu immuable** : une pièce n'est jamais remplacée ni modifiée ; empreinte SHA-256 calculée par le serveur à la réception. « Remplacer » = joindre une nouvelle pièce + invalider l'ancienne.
2. **Brouillon** : à l'ouverture d'une rectification, le brouillon reprend les **liens** de la révision de base (pas le contenu) ; l'utilisateur peut en ajouter ou en retirer. Retirer un lien ne touche pas la pièce : elle reste dans la liste « Pièces du dossier », marquée « non rattachée à la révision courante ».
3. **Confirmation** : l'ensemble des liens est figé avec la révision ; « Sans pièce source » est calculé **par révision** (D-04-03.c).
4. **Invalidation d'une pièce** : la pièce et ses liens historiques sont conservés ; sur une révision confirmée qui la citait, l'affichage indique « pièce invalidée le … — motif ». Elle ne peut plus être liée à un nouveau brouillon. Accès au contenu d'une pièce invalidée : **servi avec avertissement** (preuve d'une révision confirmée) ou refusé — à trancher (D-04-10.c).
5. **Doublon** : une pièce de même empreinte déjà présente sur le même enregistrement est refusée (`duplicate_document`) plutôt que stockée deux fois.
6. **Mesures magasin** : aucune pièce en 04 (D-04-03.c) ; la structure de lien resterait applicable plus tard sans refonte.

**Impacts.** Modèle : table de liens (révision, pièce, auteur et date du lien) — amendement de [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md). API : le corps du brouillon porte `linkedDocumentIds` (pièces du même enregistrement, non invalidées, contrôlées par le serveur) ; la lecture d'une révision renvoie ses pièces liées. Interface : liste des pièces par révision (GAP-04-06, GAP-04-14). Tests : Q-04-119 précisé, Q-04-121 (§ QA).

**Question D-04-10.** a. Option A / **B** / C. b. Brouillon initialisé avec les liens de la révision de base : **oui** / non. c. Contenu d'une pièce invalidée : **servi avec avertissement** / refusé. d. Doublon par empreinte sur le même enregistrement : **refusé** / accepté.

---

## E-02 — Coexistence d'une révision confirmée et d'une rectification en brouillon (→ D-04-11)

**Constat.** Le modèle 04-00 porte un **statut unique** sur l'en-tête (`draft` / `confirmed` / `invalidated`) et un `current_revision_no`. Avec D-04-04.b (une rectification « repasse par la confirmation »), ouvrir une rectification ferait soit repasser l'enregistrement entier en `draft` (la dernière révision confirmée cesserait d'être « courante » et le dossier perdrait son état confirmé), soit confirmer sans relecture. Les deux sont inacceptables.

**Proposition : séparer le cycle de l'enregistrement de celui des révisions.**

| Élément | Proposition |
|---|---|
| Cycle de l'enregistrement | `active` / `invalidated` (seul l'enregistrement s'invalide) |
| Statut d'une révision | `draft` (modifiable) / `confirmed` (immuable) / `discarded` (abandonnée, conservée, immuable) |
| Pointeurs de l'en-tête | `last_confirmed_revision` (vide tant que rien n'a été confirmé) et `open_draft` (vide ou **un seul** brouillon) |
| Numérotation | **numéro attribué à la confirmation** : les révisions confirmées sont numérotées 1, 2, 3… sans trou ; un brouillon est désigné par son identifiant et sa **révision de base** (vide pour une première saisie). Alternative : numéro attribué à la création (trous possibles après abandon) |
| Statut affiché (dérivé) | « Brouillon » (aucune révision confirmée) ; « Confirmé — révision n » ; « Confirmé — révision n · rectification en cours » ; « Invalidé » |
| Lecture par défaut | la **dernière révision confirmée** ; le brouillon est signalé par un bandeau (auteur, date de début, motif) et consultable séparément, libellé « brouillon non confirmé », avec comparaison brouillon ↔ révision de base |
| Usage futur (Ventes) | seules les révisions **confirmées** sont référençables ; un brouillon ouvert ne modifie jamais la révision n. Bloquer ou non « Préparer une vente » pendant une rectification : à décider avec Ventes (inerte en 04) |

**Règles de concurrence et de cycle.**

1. Un seul brouillon ouvert par enregistrement : une deuxième ouverture répond `409 draft_exists` (garantie applicative, puis contrainte d'unicité en 04-B — aucun SQL écrit ici).
2. Ouvrir une rectification exige `expectedRevisionNo` = dernière révision confirmée, sinon `409 revision_conflict`.
3. Modifier le brouillon exige son jeton de version ; confirmer exige que sa révision de base soit **toujours** la dernière confirmée (sinon 409, rien n'est écrit).
4. Brouillon **partagé** : tout compte détenant le droit d'écriture du type peut le poursuivre ; chaque enregistrement trace son auteur. Alternative : brouillon réservé à son créateur.
5. Abandon d'une rectification : la révision passe `discarded` (conservée, auditée, masquée par défaut) — cohérent avec l'absence de suppression physique (D-04-04.c). Abandon d'une **première** saisie jamais confirmée : invalidation de l'enregistrement (D-04-04.d).
6. Invalidation d'un enregistrement ayant un brouillon ouvert : brouillon **abandonné dans la même transaction** (la boîte de dialogue le mentionne), ou invalidation refusée tant que le brouillon existe.
7. Client archivé (D-04-07, recommandation A) : aucune nouvelle rectification ; un brouillon déjà ouvert est **gelé** (ni modification ni confirmation), son abandon reste possible.

**Audit (codes conformes au format `module.action` de 0005, aucune valeur).** `prescription.rectification_started`, `prescription.draft_updated`, `prescription.confirmed` (`revisionNo`), `prescription.rectification_discarded` ; équivalents `measurement.*`.

**Impacts.** Remplace, s'il est ratifié, `status` / `current_revision_no` du § 2.1 de [`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) ; routes de rectification précisées ([`API_OPTICAL_PROPOSED.md`](API_OPTICAL_PROPOSED.md) amendement) ; état d'interface supplémentaire GAP-04-13 « rectification en cours » ([`STATE_MAP_36.md`](STATE_MAP_36.md) amendement) ; tests Q-04-122 à Q-04-127.

**Question D-04-11.** a. Séparation cycle / révision : **oui** / non. b. Numéro à la **confirmation** / à la création. c. Brouillon **partagé** / réservé à son créateur. d. Abandon d'une rectification : **`discarded` conservé** / suppression. e. Invalidation avec brouillon ouvert : **abandon dans la même transaction** / refus. f. Brouillon visible de tout compte ayant `optical.read` : **oui** / auteurs seulement.

---

## E-03 — Événements d'accès aux documents, sans prétendre prouver une visualisation (→ D-04-12)

**Constat.** Le cadrage 04-00 nomme l'événement `document.viewed` et la journalisation « de consultation ». Or le serveur ne sait **pas** qu'une personne a vu une pièce : il sait seulement qu'une session authentifiée, détenant les droits, a **demandé** le contenu et qu'il a **commencé à le transmettre**. La réception complète, l'affichage effectif, la présence d'un humain devant l'écran, une capture d'écran ou une photo de l'écran lui sont inconnus ; un affichage répété depuis la mémoire du poste ne produit aucune requête.

**Proposition.**

| Sujet | Proposition |
|---|---|
| Code et libellé | `document.content_served` (au lieu de `document.viewed`) ; libellé de l'historique « Contenu de la pièce transmis au poste de <compte> » ; jamais « consultée », « vue » ou « lue » |
| Moment | événement écrit et **validé avant le premier octet** transmis ; échec d'audit ⇒ contenu non servi (inchangé). Une transmission interrompue après validation laisse l'événement : il signifie « transmission autorisée et commencée », pas « reçue » |
| Déclencheur | uniquement l'action explicite « Afficher la pièce » ; **aucun préchargement**, aucune vignette générée à partir du contenu dans les listes (une vignette serait elle-même une transmission) |
| Poste | contenu tenu en mémoire pendant l'aperçu seulement, URL `blob:` révoquée à la fermeture ; toute réouverture refait la requête (donc un nouvel événement) ; `Cache-Control: no-store` ; aucune écriture sur disque par MMV |
| Ce que MMV n'empêche pas | une fois affichée, une pièce peut être recopiée (capture, photo d'écran ; la visionneuse PDF de WebView2 propose en outre ses propres commandes d'impression et d'enregistrement — à vérifier et, si possible, désactiver en natif, O-04-18). La documentation et l'interface ne promettent **aucune** protection contre la copie |
| Métadonnées | la liste des pièces (type, taille, date, nombre) n'est pas journalisée ; le nom d'origine n'est servi qu'avec `optical.documents.read` |
| Lectures des dossiers | non journalisées en 04 (D-04-05.d, recommandation « pièces seulement ») ; options pour le pilote ci-dessous |
| Refus (401 / 403) | aucun événement dans `audit.events` (n'enregistre que des actions réussies et ne doit pas confirmer l'existence d'un dossier) ; journal de sécurité éventuel : backlog |
| Qui voit ces événements | historique de la fiche : `audit.read` **et** `optical.read` (D-04-05.c) ; aucun nom de fichier dans l'événement |

**Journalisation des lectures de dossiers (à décider avant un pilote, avec G04-LEGAL).** (A) pièces seulement (recommandé pour 04-A / 04-B) ; (B) pièces + détail d'un enregistrement (`prescription.record_served`…) dans `audit.events` — volume élevé, même limite de preuve, mélange avec le journal des actions ; (C) journal d'accès distinct avec sa propre conservation. Les recommandations des autorités sur la traçabilité des accès aux données de santé sont à examiner par le conseil juridique (D-04-09).

**Question D-04-12.** a. Code `document.content_served` et libellés « transmis » : **oui** / autre. b. Aucun préchargement ni vignette : **oui** / non. c. Lectures de dossiers : **A** en 04, décision B / C avant pilote. d. Refus non journalisés dans `audit.events` : **oui** / non.

---

## E-04 — Granularité d'`optical.read` et matrice de permissions par profil (→ D-04-13)

**Ce que couvre `optical.read` dans la proposition 04-00.** Onglet de la fiche ; liste globale et recherche transversale (tous clients, par prescripteur) ; détail des deux types ; révisions, comparaison et (E-02) brouillons ; métadonnées et nombre des pièces (pas leur contenu) ; condition, avec `audit.read`, pour voir les événements optiques de l'historique. Toujours avec `clients.read`.

| Découpage envisagé | Avantage | Inconvénient | Recommandation |
|---|---|---|---|
| lecture par type (`optical.prescriptions.read` / `optical.measurements.read`) | moindre privilège pour un poste qui n'a besoin que des mesures | **dossier partiel trompeur** : un compte verrait une mesure sans savoir qu'une ordonnance existe — risque d'erreur de délivrance ; D-04-01 sépare les types, pas leur lecture | **non** |
| liste globale séparée (`optical.browse`) | la recherche transversale énumère tous les clients ayant un dossier optique, exposition plus large que l'ouverture d'une fiche | un droit de plus ; la liste globale sert de file de travail « brouillons à confirmer » | **non en 04**, à reconsidérer si un compte doit lire des dossiers sans parcourir l'ensemble |
| révisions séparées (`optical.revisions.read`) | — | les révisions sont la preuve des rectifications ; les cacher fausse la lecture | **non** (D-04-04.f) |
| valeurs absentes des listes | minimisation : listes sans OD/OG, valeurs au détail seulement | écart avec l'état 1 du Lot 3 (résumé OD/OG) | **à trancher** : résumé dans la liste (maquette) ou listes sans valeurs |

**Recommandation : `optical.read` unique** pour la lecture (deux types, liste globale, révisions, brouillons, métadonnées des pièces), sans nouveau droit ; le contenu des pièces reste sous `optical.documents.read`. Le découpage en six droits de D-04-05 (option B) est inchangé. Matrice de permissions par **profil indicatif** : [`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md) § Amendement — un profil n'est **ni un rôle ni une attribution par défaut** (D-04-05.e).

**Question D-04-13.** a. `optical.read` unique : **oui** / découpage (préciser). b. Résumé OD/OG dans les listes : oui (maquette) / **non, détail seulement** — recommandation de minimisation, sans effet sur la structure. c. Matrice indicative des profils : à valider ou corriger compte par compte.

---

## E-05 — Contrôles de sécurité des fichiers, limites de stockage, impact sur les sauvegardes (→ D-04-14)

### 5.1 Contrôles à la réception (serveur, 04-B)

| Contrôle | Proposition | Limite connue |
|---|---|---|
| Type | liste blanche **PDF, JPEG, PNG** reconnue par la **signature du contenu** (`%PDF-`, `FF D8 FF`, `89 50 4E 47 0D 0A 1A 0A`) ; le type déclaré et l'extension doivent concorder, sinon `415` ; SVG, HTML, TIFF, HEIC, archives, Office refusés | — |
| Taille | limite par pièce (10 Mo proposés, D-04-03.c) appliquée **pendant la lecture** du flux (`413` dès le dépassement), limite de requête propre à la route d'envoi (Kestrel / multipart, O-04-03), limites des autres routes inchangées ; une seule partie par requête | — |
| Nombre | au plus **10 pièces actives** par ordonnance (proposé) | — |
| Image | lecture des seuls en-têtes (dimensions PNG `IHDR`, JPEG `SOF`) : refus au-delà d'un plafond (ex. 12 000 px de côté) contre les images « bombes » au décodage | sans bibliothèque nouvelle ; pas de réencodage |
| PDF | aucune « désinfection » possible sans dépendance ; recherche heuristique de marqueurs actifs (`/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile`, `/OpenAction`) → refus ou avertissement à décider | contournable (flux compressés) : défense d'appoint seulement ; la protection principale est l'affichage par la visionneuse PDF intégrée de WebView2 (bac à sable Chromium) et l'absence de toute exécution |
| Métadonnées (EXIF, GPS) | pièce conservée **telle que reçue** (c'est la source) ; aucune suppression des métadonnées (exigerait un réencodage, donc une altération de la preuve et une dépendance) | à mentionner dans l'analyse d'impact |
| Nom d'origine | métadonnée seulement : longueur bornée, caractères de contrôle retirés, **jamais** utilisé comme chemin ni renvoyé sans `optical.documents.read` | — |
| Intégrité | SHA-256 calculé par le serveur à la réception, conservé ; vérifié lors des tests de restauration | — |
| Écriture | contenu bufferisé en mémoire, borné à la limite ; **aucun fichier temporaire** sur le disque du serveur (pas de résidu de donnée de santé) ; écriture du contenu, des métadonnées et de l'événement dans **une** transaction | — |
| Antivirus | (A) aucun en 04, restrictions ci-dessus ; (B) analyse du tampon par l'interface AMSI de Windows (appel système, sans paquet nouveau), efficace seulement si Microsoft Defender ou un autre fournisseur AMSI est actif sur le serveur — à évaluer en preuve technique ; (C) moteur externe (ClamAV…) : composant et service nouveaux, écartés | **A en 04, B étudiée avant pilote** (O-04-13) |
| Service au poste | `Content-Type` = type **détecté** par le serveur (jamais celui du fichier) ; `nosniff`, `no-store` ; le poste crée l'objet `Blob` avec ce type, si bien qu'aucun contenu n'est interprété comme HTML ou script ; images par `<img>`, PDF par la visionneuse intégrée | ouverture de CSP nécessaire (`img-src blob:`, cadre `blob:` pour le PDF) : O-04-01, preuve native en 04-C |

### 5.2 Volume et limites de stockage

Hypothèses **à remplacer par les chiffres du propriétaire** (activité réelle du magasin) :

| Scénario | Ordonnances / an | Pièces / ordonnance | Taille moyenne | Volume / an | Sur 10 ans |
|---|---|---|---|---|---|
| bas (scans PDF) | 1 500 | 1,1 | 0,5 Mo | ≈ 0,8 Go | ≈ 8 Go |
| moyen | 2 500 | 1,3 | 1,5 Mo | ≈ 4,9 Go | ≈ 49 Go |
| haut (photos de téléphone) | 3 000 | 1,5 | 3 Mo | ≈ 13,5 Go | ≈ 135 Go |

Les autres données (clients, rendez-vous, journal) se comptent en dizaines de mégaoctets : **les pièces représenteraient l'essentiel de la base**. Conséquences pour 04-B : table du contenu séparée (aucune liste ni détail ne la lit) ; lecture en flux côté serveur ; contenu déjà compressé (JPEG / PDF) donc stockage sans recompression à prévoir dans le SQL de 04-B ; aucun quota par client. Proposition : **seuil d'alerte** sur le volume des pièces (ex. 20 Go) et sur l'espace libre du disque, déclenchant la réévaluation du stockage (O-04-02) ; une compression ou réduction des photos à l'envoi n'est **pas** proposée (altération de la source).

### 5.3 Impact sur les sauvegardes (ADR-008 `PROPOSED`, restauration jamais testée, O-04-09)

- Une sauvegarde logique (`pg_dump`) ne recompresse presque pas JPEG / PDF : **taille de la sauvegarde ≈ volume des pièces**, durée de sauvegarde, de copie hors serveur et de restauration croissant linéairement (scénario moyen : ≈ 15 Go après 3 ans à copier à chaque sauvegarde complète).
- Au-delà d'un seuil à mesurer en 04-C, une sauvegarde physique avec archivage des journaux de transactions (sauvegardes incrémentales) devient préférable : décision ADR-008, pas en 04.
- Les sauvegardes contiennent des **données de santé** : chiffrement du stockage et du transfert obligatoire (ADR-008), accès restreint, copie hors machine.
- Une pièce invalidée, ou effacée par une future procédure légale (D-04-09), **subsiste dans les sauvegardes** jusqu'à leur rotation : durée de rétention des sauvegardes à fixer avec le conseil juridique.
- **Préalable à un pilote** : une restauration complète dans une base indépendante, incluant les pièces, avec vérification des empreintes SHA-256 et mesure des durées (Q-04-145).
- Mesures à produire en 04-C sur `mmv_v2_test` avec des fichiers **synthétiques** (jamais des documents réels) : taille et durée de sauvegarde / restauration pour 1 Go puis 5 Go de pièces.

### 5.4 Alternative de stockage (rappel)

Fichiers sur le disque du serveur (option 2 de D-04-03.c) : base plus légère et sauvegardes séparables, mais cohérence base ↔ fichiers à garantir (sauvegarde en deux parties, fichiers orphelins), droits NTFS, risque d'exposition d'un chemin, contrôle d'accès dédoublé. **Recommandation inchangée : PostgreSQL**, réexaminée au seuil d'alerte.

**Question D-04-14.** a. Liste blanche par signature PDF / JPEG / PNG : **oui** / autre. b. 10 Mo par pièce, 10 pièces actives par ordonnance : **oui** / autres valeurs. c. Marqueurs PDF actifs : **refus** / avertissement / aucun contrôle. d. Pièce conservée telle que reçue (métadonnées comprises) : **oui** / non. e. Antivirus : **A en 04, B étudiée avant pilote** / autre. f. Seuil d'alerte de volume : **20 Go** / autre. g. Volumes réels du magasin (ordonnances / an, part de photos) : à fournir pour remplacer les hypothèses.
