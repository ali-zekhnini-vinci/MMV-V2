# MMV V2 — MANDAT OFFICIEL : IMPLÉMENTATION PHASE 04-B, MIGRATION 0006 ET INTÉGRATION 04-C

**Décision propriétaire : GO au développement immédiat du module optique, sans nouvelle boucle de conception générale.**

Le propriétaire approuve les orientations produit et règles déjà décidées pour le dossier optique. Il souhaite maintenant un module effectivement fonctionnel, relié à .NET et PostgreSQL 18, puis clôturer la Phase 04 après migration et tests réels. **Ce GO autorise le code et la préparation du SQL, mais ne constitue PAS une approbation aveugle d'un SQL 0006 encore inconnu.** Une unique revue du script exact est obligatoire avant toute application sur `mmv_v2_dev` ou `mmv_v2_test`.

## 0. Préflight et discipline

- Travailler sur le dépôt MMV V2 neuf et la branche de Phase 04 (`feat/phase-04-00-cadrage`, sauf évolution locale constatée et expliquée). Confirmer l'ascendance du `main` contenant la Phase 03 fusionnée (PR #4, `6924860`). Retrouver le HEAD réel et les commits 04-A ; ne pas faire de reset destructif.
- Lire les décisions et spécifications à jour dans `docs/phase/04/`, le rapport de clôture 04-A, les contrats/API et l'audit Phase 03. La maquette HTML officielle du Lot 3 dans `references/phase04/lot3/` est la référence UX, pas une source de règles médicales importables.
- Préserver tous les fichiers non suivis, archives, secrets, processus du propriétaire, PostgreSQL 17 et dépôts antérieurs. Pas de SQLite. Ne pas recopier le code de l'ancien MMV.
- Aucun push, PR ou merge : seul le propriétaire les déclenche après audit. Commits locaux possibles. Ne pas créer artificiellement un nouveau lot documentaire.

## 1. Décisions APPROVED à mettre en œuvre

### Produit et UX

1. **Deux enregistrements métier distincts** : ordonnance médicale reçue/transcrite et mesure optique effectuée en magasin (D-04-01). Ne jamais décrire la mesure comme une prescription médicale.
2. **Design officiel Lot 3** : présentation OD/OG et boutons ± (D-04-15). Pas de 0,25 dioptrie pour les boutons Sphère, Cylindre, Addition ; la saisie manuelle n'est ni arrondie ni forcée au pas. Pas des prismes non arrêté.
3. **Suppression de « État de l'œil » et report de toutes les pièces sources** au backlog BL-04-01 : aucun upload, contenu binaire, table ou API documentaire dans `0006` (D-04-16).
4. Un bouton principal **« Enregistrer »** ; option discrète **« Enregistrer comme brouillon »** ; clavier cohérent (D-04-17). `Enregistrer` doit être atomique côté API et ne créer ni brouillon orphelin ni doublon sur réessai.
5. Les anciennes révisions confirmées sont immuables. Une rectification crée un brouillon distinct ; la dernière révision confirmée demeure lisible et demeure la référence jusqu'à nouvelle confirmation. L'historique affiche auteur/date/motif/champs modifiés sans divulguer inutilement les valeurs sensibles.

### Provenance

- Ordonnance : client ; date de prescription et nom du prescripteur obligatoires **pour confirmer**, facultatifs dans le brouillon ; qualité du prescripteur facultative et sans liste fermée imposée. Conserver date de réception et auteur de saisie correctement distingués.
- Mesure : client ; date de mesure et collaborateur l'ayant effectivement réalisée obligatoires **pour confirmer**, facultatifs dans le brouillon ; catégorie facultative et sans liste fermée imposée. Auteur de l'enregistrement rempli par le serveur, pouvant différer du collaborateur effectif.
- Dates métier sans décalage UTC indu, instants d'audit horodatés côté serveur ; ne pas présumer un seul fuseau pour la Belgique et le Maroc.

### Cycles, droits et audit

- Révisions confirmées immuables ; invalidation motivée et auditée, sans suppression physique ordinaire ; client archivé = consultation par personnes autorisées seulement, sauf invalidation autorisée.
- Quatre permissions, toutes explicites : `optical.read`, `optical.prescriptions.write`, `optical.measurements.write`, `optical.invalidate`, combinées selon l'opération avec `clients.read`. Pas de permissions implicites via `system.admin`, pas d'attribution aux comptes réels sans autorisation.
- Historique optique consultable uniquement avec `audit.read` **ET** `optical.read`. Contrôle serveur sur chaque requête ; listes et erreurs sans fuite de données médicales.
- Toute action métier réussie et son audit dans **la même transaction PostgreSQL**. Aucun événement de succès après échec/rollback.

### Règles optiques approuvées comme orientations de produit, avec validation professionnelle encore ouverte

- Un seul œil renseigné peut suffire ; l'autre œil reste entièrement vide, jamais converti en zéro.
- Une seule Sphère à `0,00` sans aucune autre information ne suffit pas à la confirmation ; mais `0,00` est une valeur explicite valide et ne doit jamais être confondue avec « absent ».
- Cylindre non nul et axe manquant : confirmation refusée. S'appuyer sur le design Lot 3 pour l'édition ; si le cylindre redevient nul, ne jamais effacer l'axe préexistant silencieusement. Présenter le contrôle et demander l'intervention de l'utilisateur.
- Axe `0°`/`180°` : conserver l'information saisie ; toute conversion `0° → 180°` inspirée du design doit être **explicite, visible et traçable**, jamais silencieuse. Ne pas prétendre à une validation clinique définitive.
- Prisme non nul : base requise pour confirmer. Les valeurs, conventions, incréments et composantes multiples restent **à vérifier avec un opticien** ; ne pas construire un schéma empêchant une extension sûre de ce domaine. Ne pas inventer de nouvelles règles cliniques.
- Acuité visuelle seule : **suffisante en principe pour une mesure magasin**, **insuffisante pour une ordonnance médicale** ; décision produit sous réserve de validation professionnelle.
- Champs optiques absents ≠ 0 ; pas de transposition de cylindre, de normalisation implicite, de remplissage automatique de l'autre œil, de limitation clinique chiffrée inventée.
- Ces orientations appartiennent à la recette produit locale sur données fictives ; elles **ne constituent ni certification médicale, ni autorisation d'exploitation sur données de santé réelles**. Consigner précisément les règles qui demeurent soumises à l'opticien.

## 2. Développer maintenant le vrai module 04-B

Implémenter sans attendre une nouvelle autorisation pour les choix techniques réversibles :

- Domaine/Application/Infrastructure/API .NET 10, EF Core 10/Npgsql, PostgreSQL 18 exclusivement ; séparation des entités ordonnance/mesure et mécanismes partagés de valeurs OD/OG, provenance, versions, brouillons et audit.
- Contrats d'API cohérents et documentés pour listes globales/par client, détail, dernière révision confirmée, historique/comparaison, création de brouillon, modification de brouillon, `save-and-confirm` atomique, ouverture de rectification, abandon, invalidation, lecture en compte archivé. Choisir des routes précises cohérentes avec le dépôt, sans inventer une seconde API parallèle.
- Garantie d'un seul brouillon ouvert par enregistrement ; concurrence via jeton de version et contrainte/règle PostgreSQL ; réponses 409 explicites sur éditions concurrentes ou révision de base obsolète.
- Enregistrement et confirmation **en une transaction** avec audit. Introduire une stratégie d'idempotence/relecture robuste contre double clic, délai réseau et réémission, sans multiplier les versions confirmées.
- Une même correction ne doit pas produire de fausses mises à jour : pas d'écriture, changement de version ou audit de modification lorsque les champs n'ont pas changé, sauf action explicite distincte justifiée.
- Stocker les valeurs numériques sans perte/arrondi silencieux et représenter les champs facultatifs par `null`. Pour tout choix de précision/échelle technique, justifier l'absence de perte de valeurs à saisie libre ; ne pas transformer un format clinique supposé en contrainte cachée.
- Permission registry / contrainte PostgreSQL `auth.user_permissions` mise à jour explicitement pour les quatre nouveaux droits ; préserver les droits existants et les données de Phase 03.
- Audit existant `audit.events` réutilisé, sans recopier les données médicales en clair dans les logs ou dans les événements ; ajouter les nouveaux codes contrôlés et tests d'accès.
- Produire la migration EF Core `0006_OpticalModule` et les scripts SQL normal/idempotent ; **ne pas écrire de migration de rollback destructif à exécuter automatiquement**. Manifeste SHA-256 complet, scripts de vérification de schéma et politique de mise à niveau / compatibilité API.
- Pas de pièces sources, pas de ventes, pas de mutuelles/tiers payant ni de commandes fournisseurs dans cette phase. Ne pas surcharger le domaine par des tables anticipées inutiles.

## 3. Brancher les écrans existants au vrai service

- Réutiliser et conserver les composants 04-A (Lot 3, boutons ±, formulaires, historique, listes) ; **ne pas les redessiner**.
- Remplacer les doubles simulés par des appels réels à MMV.Api pour un parcours de test explicitement activé. Prévoir une activation contrôlée/feature gate ; par défaut, les comptes réels continuent à voir « Ordonnances — phase ultérieure » tant que 04-C et la validation propriétaire ne sont pas achevées.
- Respecter les droits servis par le backend, y compris le refus d'accès, l'absence d'accès au dossier optique en cas de droits insuffisants et l'historique avec `audit.read` + `optical.read`.
- En cas de perte de connexion ou de retour ambigu du serveur, relire l'état réel avant de proposer un réessai ; aucun doublon ou fausse confirmation.
- Conserver le banc de démonstration en données fictives, isolé et hors production.

## 4. TESTS et preuves attendus

- .NET tests unitaires domaine, validations techniques, droits et gestion des erreurs ; tests PostgreSQL 18 réels sur `mmv_v2_test` dès autorisation SQL ; frontend TypeScript strict, lint, Vitest et build ; matrice HTTP HTTPS réelle.
- Cas obligatoires : deux types, chacun des deux yeux, uniquement OD/OG, valeur absente vs `0,00`, acuité seule selon type, sphère zéro seule, cylindre/axe, prisme/base, provenance confirmée vs brouillon, révisions immuables, abandon, invalidation, client archivé, droits par compte, `audit.read` sans `optical.read`, conflits concurrents, double requête/timeout/relecture, absence d'événement en cas d'échec, absence de fuite dans les logs.
- Parcours Tauri natif après migration : fiche client → dossier optique → nouveau document/mesure → boutons ± → Enregistrer → lire historique → rectifier → conflit de deux sessions/postes → invalider ; vérifier thèmes, zooms et retour à la fiche.
- Étiqueter sans ambiguïté PASS / FAIL / NOT_TESTED / NOT_RERUN. Ne jamais transformer des essais simulés ou les logs d'un outil en preuve de test natif réel.

## 5. Gate unique SQL avant toute migration

**Étape A : implémenter, générer et livrer le code + SQL 0006 NON APPLIQUÉ.** Vérifier compilation/tests ne nécessitant pas de migration, absence de secrets, idempotence de script et impact sur tables clients/auth/audit. Fournir une archive et le SQL exact, ses SHA-256 complets, le diff de schéma, la liste des commandes d'application et un mini dossier G04-SQL. Statut `PHASE04_B_READY — OWNER_SQL_REVIEW_REQUIRED`.

**STOP uniquement pour revue du SQL.** Le propriétaire soumettra le dossier à l'audit indépendant ; une fois **le script exact** approuvé, passer immédiatement à l'étape B via un court GO, sans redemander les décisions produit déjà approuvées.

**Étape B après GO G04-SQL explicite :**
- Confirmer PostgreSQL 18.6 port 5433, `mmv_v2_dev` et `mmv_v2_test`, rôles correspondants, migrations 0001–0005, dérive nulle ; jamais PostgreSQL 17/port 5432.
- Créer une sauvegarde de `mmv_v2_dev`, vérifier sa lisibilité, les objets et les empreintes ; si un vrai test de restauration n'est pas possible faute de privilège/base indépendante, le documenter honnêtement (prérequis avant pilote), ne pas créer de base ou élargir les droits sans accord.
- Vérifier de nouveau l'empreinte approuvée ; appliquer exclusivement le SQL approuvé avec `Invoke-MmvSqlScript.ps1`, transactions et timeout de verrou raisonnable ; confirmer COMMIT, historique EF, contraintes, index, droits, audit. En cas de dérive ou erreur : STOP sans rollback destructif.
- Exécuter toute la matrice PostgreSQL/HTTP/Tauri, nettoyage démontré des données fictives de `mmv_v2_test`, sans altérer comptes et données réels. Ne pas attribuer de nouveaux droits aux comptes du propriétaire sans GO spécifique.
- Proposer `PHASE04_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` uniquement si l'intégration réelle fonctionne et les contrôles sont attestés ; **la clôture commerciale/pilote reste distincte** et exige conformité Maroc/Belgique, séparation du compte applicatif SQL, sauvegarde restaurée et tests multi-postes.

## 6. DOCUMENTATION GATE et livraison

Mettre à jour sans réécrire l'historique : `README.md`, `docs/phase/04.md`, `docs/phase/04/IMPLEMENTATION_SPEC.md`, `DATA_MODEL_PROPOSED.md` (puis contrat approuvé), `API_OPTICAL_PROPOSED.md` (puis contrat approuvé), `PERMISSIONS_04_PROPOSED.md`, `QA_MATRIX.md`, `SOURCE_MAP.md`, `GATES_AND_DECISIONS.md`, `LIVRAISON_04A.md` si nécessaire, ADR et backlog, avec les décisions propriétaire G04-METIER et les réserves professionnelles encore ouvertes.

Ne pas déclarer approuvés des contrôles cliniques qui restent à confirmer par un opticien. Pas de refonte non demandée, pas de nouvelle matrice de 500 questions, pas de préparation de Phase 05, aucun push/PR/merge.

### Livraison attendue immédiatement

- Implémentation locale fonctionnelle prête à la revue SQL.
- Rapport synthétique des décisions appliquées et des règles professionnelles encore ouvertes.
- Scripts 0006 et SHA-256 complets + contrôle de schéma proposé.
- Résultats effectifs .NET/frontend et limites des tests sans migration.
- Commits locaux, ZIP complet ou léger auditables, aucun secret.
- **Statut : `PHASE04_B_READY — OWNER_SQL_REVIEW_REQUIRED`.**

**But : passer du banc simulé 04-A à un dossier optique réellement persistant, puis migrer et tester pour achever la Phase 04. Pas de faux GO sur un SQL non audité.**
