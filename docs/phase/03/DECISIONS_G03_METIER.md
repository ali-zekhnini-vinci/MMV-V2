# 03 — Dossier d'arbitrage G03-METIER (présenté le 17/10/2026)

**Statut : toutes les lignes sont `PROPOSED`.** Rien ici n'est approuvé tant que le propriétaire ne l'a pas ratifié explicitement. Les valeurs d'exemple des maquettes (personnes, téléphones, horaires, motifs, durées) ne sont ni des données de démonstration ni des défauts métier.

Une **seule validation** est demandée pour démarrer 03-A2 (domaine Clients, API, migration) : sections A, B et C. La section D (Rendez-vous) peut être validée maintenant ou au démarrage de 03-B ; elle ne bloque pas 03-A2.

Format de réponse suggéré : « G03-METIER : A, B, C acceptés » ou liste des écarts (ex. « D-03-03 : retirer les notes »).

## A. Modèle Clients

| ID | Question | Recommandation | Alternatives | Conséquence si accepté |
|---|---|---|---|---|
| D-03-01 | Identité et identifiants | Prénom **et** nom obligatoires (1–100 caractères, rognés). Identifiant technique UUID v7 généré par l'API. Référence lisible distincte `CLI-000123` attribuée par une **séquence PostgreSQL**, unique, jamais réutilisée. | Pas de référence lisible (UUID seul) ; référence par magasin/année | Colonnes `id`, `reference`, `first_name`, `last_name` ; contraintes `ck_clients_*_not_blank`, `ux_clients_reference` |
| D-03-02 | Coordonnées | Tous **facultatifs** : date de naissance (date civile, ≥ 01/01/1900, passée), téléphone (format libre, 8 à 15 chiffres, `+` initial admis), email (format valide, ≤ 254), adresse (une ligne ≤ 200), ville (≤ 100), code postal (≤ 10, lettres/chiffres/espace/tiret). Aucune obligation « au moins un contact ». | Exiger téléphone **ou** email ; normaliser le téléphone en E.164 (exige un pays par défaut, ADR-010 ouvert) | Formulaire actuel (`apps/desktop/src/clients/clientForm.ts`) ; validation serveur identique en 03-A2 |
| D-03-03 | Données sensibles exclues de la Phase 03 | **Exclus** : n° de sécurité sociale, assurance/mutuelle (visibles sur la maquette), toute donnée optique, médicale ou d'ordonnance (Phase 04), pièces jointes, consentements marketing. **Notes internes conservées** (≤ 1000 caractères), avec la consigne « aucune donnée de santé ». | Retirer aussi les notes en Phase 03 ; ajouter mutuelle maintenant (tiers payant non défini) | Champs absents du modèle, de l'API et de l'écran |
| D-03-04 | Recherche et doublons | Recherche **serveur** sur nom, prénom, email et chiffres du téléphone, insensible à la casse et aux accents, ≥ 2 caractères, via une colonne `search_text` normalisée par l'application (aucune extension PostgreSQL). **Aucune contrainte d'unicité** email/téléphone (familles, homonymes). Alerte de doublon possible non bloquante : `DEFERRED`. Fusion de fiches : hors périmètre. | Extensions `unaccent` / `pg_trgm` (installation par le propriétaire, recherche floue) ; unicité stricte de l'email | Colonne `search_text` ; index `ix_clients_active_name`, `ix_clients_created_at` |
| D-03-05 | Cycle de vie | **Archivage / réactivation** uniquement ; **aucune suppression physique** (ni écran ni API) en Phase 03. Client archivé : lecture seule, exclu des listes par défaut, non modifiable, non proposable pour un nouveau rendez-vous. Effacement légal des données personnelles : procédure d'administration ultérieure, à cadrer avec le pays d'exploitation (ADR-010). | Suppression autorisée sans historique (planche 23 « refus de suppression ») | Planche 23 (`refuseDelete`) sans objet ; colonne `archived_at_utc` |
| D-03-06 | Concurrence et audit | Concurrence **optimiste** par la colonne système `xmin` (jeton `version` renvoyé par l'API) : écriture avec une version périmée → **409** « modifié depuis un autre poste », jamais d'écrasement silencieux. Audit minimal : `created_at_utc`, `created_by_user_id`, `updated_at_utc`, `updated_by_user_id`, `archived_at_utc`. Historique détaillé des modifications : `DEFERRED`. | Dernier écrivain gagnant ; table d'historique dès 03-A2 | Comportement déjà implémenté côté interface (tests de conflit) |
| D-03-07 | Dates et heures | Horodatages en UTC (`timestamptz`), affichés dans le fuseau du poste ; date de naissance = date civile sans fuseau. | Fuseau du magasin imposé dès 03-A (voir D-03-13) | Aucun impact Clients si D-03-12 est tranché plus tard |

## B. Permissions et confidentialité

| ID | Question | Recommandation | Alternatives | Conséquence si accepté |
|---|---|---|---|---|
| D-03-08 | Droits Clients | Trois permissions **serveur** : `clients.read` (liste, fiche), `clients.write` (créer, modifier), `clients.archive` (archiver, réactiver). `system.admin` **n'implique aucun** droit Clients (testé côté client : 403 attendu). Aucun rôle créé à partir des profils visuels du Lot 1. | Permission unique `clients.manage` ; rôles prédéfinis | Contrainte `ck_user_permissions_permission` étendue (migration 0003) |
| D-03-09 | Attribution des droits | Ajouter une commande locale `MMV.Maintenance permissions grant|revoke` à **cible stricte** (même politique que `bootstrap-admin`), exécutée par le propriétaire ; écran d'administration des droits plus tard. Le propriétaire choisit les comptes bénéficiaires. | SQL manuel par le propriétaire ; écran Comptes en écriture dès 03-A2 | Code et tests en 03-A2 ; aucune attribution sans action du propriétaire |
| D-03-10 | Confidentialité | Journaux serveur sans nom, téléphone ni email (identifiants techniques uniquement) ; la liste ne renvoie que les colonnes affichées (pas de notes, adresse ni date de naissance) ; cache du client en mémoire seulement, vidé à la déconnexion (déjà implémenté) ; données fictives uniquement dans les tests. | — | Tests de confidentialité des journaux en 03-A2 |

## C. API et migration

| ID | Question | Recommandation | Alternatives | Conséquence si accepté |
|---|---|---|---|---|
| D-03-11 | Contrat HTTP | Contrat de [`API_CLIENTS_PROPOSED.md`](API_CLIENTS_PROPOSED.md) : `GET/POST /api/clients`, `GET/PUT /api/clients/{id}`, `POST /api/clients/{id}/archive|reactivate` ; pagination 20 (1–100), tri en liste blanche (`name`, `createdAt`, `city`), `status=active|archived|all` ; version dans le **corps** ; erreurs `{code, errors}`. | `ETag`/`If-Match` (412) au lieu de la version dans le corps ; filtre Ville (planche 06) dès 03-A2 | Contrat déjà consommé par l'interface et son double de test |
| D-03-12 | Migration | Appliquer `0003_ClientsModule` ([`sql/`](sql/)) à `mmv_v2_dev` puis `mmv_v2_test` par `scripts/Invoke-MmvSqlScript.ps1`, comme O-02-01 : schéma `clients`, séquence, table, index, extension de la contrainte de permissions. Jamais au démarrage. Retour arrière : script `rollback` **après** suppression des attributions `clients.*` (sinon la contrainte restaurée échoue) ; le schéma `clients` vide n'est pas supprimé par le script généré (même comportement que 0002). | Deux migrations séparées (permissions puis table) | GO ciblé « G03-METIER + migration 0003 » requis avant toute écriture en base |

## D. Rendez-vous (03-B) — peut être tranché plus tard

| ID | Question | Recommandation | Alternatives |
|---|---|---|---|
| D-03-13 | Temps | Un **fuseau unique du magasin** (identifiant IANA en paramètre), stockage UTC ; pas de grille 15 min ; durée choisie dans une liste (15 à 120 min), **sans durée par défaut métier** tant que non décidée ; plage affichée 09:00–19:00 **non bloquante** ; horaires d'ouverture et jours fermés : `DEFERRED` (module Paramètres). | Horaires bloquants dès 03-B |
| D-03-14 | Responsable | Le responsable est un **compte MMV actif** (référence `auth.users`), facultatif. Pas d'entité « opticien » ni de ressource indépendante en V1. | Ressource indépendante (salle, appareil) — FullCalendar Premium **non** requis pour autant |
| D-03-15 | Conflits | Conflit = chevauchement de deux rendez-vous **non annulés** du **même responsable** ; **rejet 409** côté serveur, sans contournement ; contrôle transactionnel avec verrou consultatif par responsable (aucune extension `btree_gist`). | Avertissement avec confirmation explicite ; contrainte d'exclusion PostgreSQL (extension) |
| D-03-16 | États | `planned` / `cancelled` (annulation logique, motif facultatif, rétablissement si le créneau est libre). Pas de modification d'un rendez-vous terminé ; « honoré / absent » `DEFERRED`. Client archivé : aucun nouveau rendez-vous ; ses rendez-vous futurs restent visibles et annulables. | Statuts honoré/absent dès 03-B |
| D-03-17 | Droits et notes | `appointments.read`, `appointments.write` (créer, modifier, déplacer), `appointments.cancel` ; notes visibles avec `appointments.read`, sans donnée de santé, sans journal de consultation en V1. | Notes réservées à un droit distinct |

## Points d'interface sans incidence métier (information, réversibles)

Écarts volontaires aux planches, détaillés dans [`STATE_MAP_47.md`](STATE_MAP_47.md) : bouton « Importer » absent (aucune décision d'import) ; filtre Ville et compteurs d'onglets absents ; boutons « Créer une vente » / « Ajouter une ordonnance » absents (phases ultérieures) ; « Dernière visite » absente (aucune source) ; notifications d'opération par bandeau `role="status"` au lieu de toasts (aucune bibliothèque ajoutée) ; bouton de confirmation d'archivage en accent plutôt qu'orange (blanc sur `#F79009` ≈ 2,3:1, sous le seuil WCAG AA 4,5:1).
