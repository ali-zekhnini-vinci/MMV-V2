# 03-B — Proposition métier consolidée Rendez-vous (présentée le 17/10/2026)

**Statut : toutes les lignes sont `PROPOSED`.** Rien n'est approuvé tant que le propriétaire ne l'a pas ratifié. Cette proposition consolide la section D de [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) (D-03-13 à D-03-17, conservée telle que présentée) après développement de l'interface 03-B, et ajoute **D-03-18 (motifs)**. Les valeurs visibles sur les maquettes du Lot 2 (personnes, horaires, motifs, couleurs) ne sont **ni des défauts ni des règles** : l'interface ne les reprend pas, elle lit tout paramètre métier depuis le serveur.

Mandat : [`prompts/PHASE03_B_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_B_GO_CLAUDE_CODE.md). Contrat proposé : [`API_APPOINTMENTS_PROPOSED.md`](API_APPOINTMENTS_PROPOSED.md). SQL proposé : [`sql/0004_AppointmentsModule.PROPOSED.sql`](sql/0004_AppointmentsModule.PROPOSED.sql) (non appliqué).

## Ce qui est déjà indépendant des décisions

L'interface (`apps/desktop/src/appointments/`) fonctionne quelle que soit la réponse : fuseau, durées, motifs, responsables, caractère obligatoire du responsable et plage affichée sont servis par `GET /api/appointments/options` ; conflits, passé, client archivé et droits sont **décidés par le serveur** et seulement traduits en messages. Seules exigences locales : un client, une date, une heure, une durée (sans quoi aucun créneau n'existe) et un motif (D-03-18). Le service reste **désactivé** (`APPOINTMENTS_API_ENABLED = false`) : aucun rendez-vous n'est créé ni affiché en fonctionnement normal.

## Synthèse à valider

| ID | Sujet | Recommandation consolidée | Écart avec la proposition initiale |
|---|---|---|---|
| D-03-13 | Fuseau, durées, horaires | Un fuseau IANA unique du magasin, **fourni par le propriétaire**, paramètre serveur obligatoire ; stockage UTC ; durées 15, 30, 45, 60, 90, 120 min sans défaut ; début au pas de 5 min ; un rendez-vous ne franchit pas minuit (heure du magasin) ; création, déplacement et rétablissement **dans le passé refusés** ; plage affichée 09:00–19:00, dimanche masqué, **non bloquants** ; horaires d'ouverture `DEFERRED` | précise le pas, minuit et le passé |
| D-03-14 | Responsable | Compte MMV **actif** disposant d'au moins un droit `appointments.*` ; **obligatoire** | initialement facultatif |
| D-03-15 | Conflits | Chevauchement = même responsable, deux rendez-vous **planifiés**, intervalles semi-ouverts [début, fin[ (bout à bout autorisé) ; **refus 409** sans contournement ; double réservation d'un même client **non bloquée** ; contrôle transactionnel sous verrou consultatif par responsable, sans extension | précise bout à bout et client |
| D-03-16 | Annulation, rétablissement | `planned` / `cancelled`, motif facultatif ≤ 200 ; rétablissement si créneau libre, non passé et client actif ; rendez-vous annulé non modifiable ; aucune suppression ; client archivé : aucun nouveau rendez-vous ni rétablissement, rendez-vous existants visibles et **seulement annulables** ; « honoré / absent » `DEFERRED` | précise le client archivé |
| D-03-17 | Permissions, confidentialité | `appointments.read`, `appointments.write` (créer, modifier, déplacer), `appointments.cancel` (annuler, rétablir) ; `system.admin` n'en implique aucun ; attribution par `MMV.Maintenance permissions` ; sélection du client = `clients.read` en plus ; agenda sans coordonnées client ; notes ≤ 1000 visibles avec `appointments.read` ; journaux à identifiants techniques | inchangé, complété |
| D-03-18 | Motifs | Liste fermée de codes + libellés, **paramètre serveur** (pas de table en V1), motif obligatoire ; libellés à fournir par le propriétaire | nouvelle ligne |

Format de réponse suggéré : « 03-B RDV : D-03-13 à D-03-18 acceptés, fuseau = <identifiant IANA>, motifs = <liste> » ou liste des écarts (ex. « D-03-14 : responsable facultatif »).

## D-03-13 — Fuseau horaire, durées et horaires

| Point | Recommandation | Alternatives | Conséquence |
|---|---|---|---|
| Fuseau | Un identifiant IANA (ex. `Africa/Casablanca`, `Europe/Paris`) **choisi par le propriétaire** (pays d'exploitation : ADR-010 ouvert), configuré côté serveur (`Appointments:TimeZone`) ; démarrage refusé s'il est absent ou inconnu une fois le service activé | fuseau du poste (incohérent entre postes) ; fuseau par magasin (multi-magasin hors V1) | affichage et saisie dans ce fuseau sur tous les postes ; Temporal gère les changements d'heure (heure inexistante refusée, heure ambiguë = première occurrence) |
| Stockage | `timestamptz` UTC, fin exclue | heure locale sans fuseau | colonnes `starts_at_utc`, `ends_at_utc` |
| Durées | Liste serveur 15, 30, 45, 60, 90, 120 min ; **aucune durée par défaut** ; pas de redimensionnement libre à la souris | durée libre ; durée par motif | `durationsMinutes` des options ; glisser = déplacement seul |
| Pas de début | Minutes multiples de 5 | 15 min (grille), libre | refus 422 `startsAtUtc: invalid_step` ; la grille aimante à 15 min, la saisie clavier accepte le pas de 5 |
| Minuit | Début et fin le même jour civil du magasin | autorisé | refus 422 `startsAtUtc: out_of_range` |
| Passé | Création, déplacement, modification et rétablissement refusés si le rendez-vous (cible ou existant) est terminé (`fin ≤ maintenant`) ; annulation d'un rendez-vous terminé refusée | tolérance (ex. 15 min) ; modification du passé par un droit dédié | 409 `appointment_past` ou 422 `startsAtUtc: in_past` |
| Plage et jours affichés | 09:00–19:00, lundi–samedi, **affichage seulement** | horaires bloquants dès 03-B | `displayStartTime`, `displayEndTime`, `hiddenWeekdays` ; horaires d'ouverture et jours fermés `DEFERRED` (module Paramètres) |

## D-03-14 — Responsable

**Recommandation : obligatoire.** La règle de conflit (D-03-15) est définie par responsable : un rendez-vous sans responsable ne serait protégé par aucune vérification de chevauchement. Liste proposée = comptes **actifs** disposant d'au moins un droit `appointments.*` (exclut les comptes purement techniques), servie avec l'identifiant et le nom affiché seulement. Pas d'entité « opticien », de salle ni d'appareil en V1 (aucune vue « ressources », donc aucun FullCalendar Premium). Compte désactivé ensuite : ses rendez-vous restent affichés avec son nom ; il n'est plus proposé.

Alternative : facultatif (proposition initiale) — l'interface le gère déjà (`responsibleRequired = false` : option « Non attribué »), mais les rendez-vous non attribués échappent au contrôle de conflit.

## D-03-15 — Conflits et chevauchements

| Point | Recommandation | Alternatives |
|---|---|---|
| Définition | Même responsable, statut `planned` des deux côtés, `a.début < b.fin ET b.début < a.fin` (10:00–10:30 puis 10:30–11:00 : pas de conflit) | chevauchement par client |
| Réponse | **409 `appointment_conflict`**, rien n'est écrit ; aucun contournement | avertissement avec confirmation explicite |
| Opérations contrôlées | création, modification, déplacement (glisser ou clavier), rétablissement | — |
| Même client, deux responsables | non bloqué (`DEFERRED` : avertissement éventuel) | refus |
| Mécanisme | transaction `READ COMMITTED`, `pg_advisory_xact_lock` sur une clé dérivée de l'identifiant du responsable, puis recherche des chevauchements planifiés (index partiel `ix_appointments_responsible_planned`), puis écriture ; aucune extension | contrainte d'exclusion `btree_gist` (installation d'extension par le propriétaire) |

Interface : bandeau « Conflit horaire — Le créneau 10:45 – 11:15 chevauche un autre rendez-vous de ce responsable » (planches 34, 42), le glisser est annulé (`revert`), la vérité serveur n'est jamais remplacée.

## D-03-16 — Annulation et rétablissement

- États : `planned`, `cancelled`. Annulation logique avec motif **facultatif** (≤ 200 caractères), auteur et date conservés ; aucune suppression physique (ni écran ni route `DELETE`).
- Un rendez-vous annulé est en lecture seule ; il est masqué de l'agenda par défaut et visible avec « Afficher les annulés ».
- Rétablissement (« Rétablir le rendez-vous ») : refusé si le créneau est désormais occupé (409 `appointment_conflict`), si le rendez-vous est passé (409 `appointment_past`) ou si le client est archivé (409 `client_archived`).
- Client archivé (D-03-05 acceptée) : aucun nouveau rendez-vous, aucun rétablissement, aucune modification ; ses rendez-vous restent visibles et **annulables** (badge « Client archivé » dans le détail).
- Statuts « honoré / absent », rappels et historique détaillé : `DEFERRED`.

## D-03-17 — Permissions et confidentialité

| Point | Recommandation |
|---|---|
| Droits | `appointments.read` (agenda, détail, options), `appointments.write` (créer, modifier, déplacer), `appointments.cancel` (annuler, rétablir) ; contrainte `ck_user_permissions_permission` étendue (migration 0004) ; `system.admin` sans droit implicite ; attribution par `MMV.Maintenance permissions grant` (liste `Grantable` étendue), par le propriétaire |
| Dépendance Clients | le sélecteur de client exige `clients.read` (recherche `GET /api/clients?status=active`) ; « Voir la fiche client » n'est proposé qu'avec ce droit |
| Minimisation | agenda : nom, prénom et référence du client seulement (ni téléphone ni email) ; notes et motif d'annulation dans le détail seulement |
| Notes | ≤ 1000 caractères, consigne « aucune donnée de santé », visibles avec `appointments.read` ; pas de journal de consultation en V1 (alternative : droit distinct pour les notes) |
| Journaux | événements d'écriture et de conflit avec identifiants techniques uniquement (comme D-03-10) |
| Interface | garde d'affichage : actions masquées (création) ou désactivées avec motif (détail) selon la session ; le serveur reste l'autorité |

## D-03-18 — Motifs (nouvelle ligne)

**Recommandation :** liste fermée de motifs (code stable `^[a-z0-9_-]{1,32}$` + libellé), paramètre serveur (`Appointments:Reasons`), motif obligatoire, ordre d'affichage = ordre de la liste. Les cinq libellés des maquettes (« Contrôle de vue », « Essayage / ajustement », « Retrait de commande », « Conseil / devis », « Réparation ») ne sont qu'une **base de discussion** : aucune n'est retenue sans accord. Un motif retiré de la liste reste affiché « Motif non répertorié » sur les rendez-vous existants. Couleurs de l'agenda : attribuées par position dans la liste (repère visuel, aucun sens métier). Durée par motif : `DEFERRED`.

Alternatives : table `appointments.reasons` administrable (migration et écran supplémentaires) ; motif libre (non recommandé : statistiques impossibles).

## Après ratification

1. Intégrer dans `src/` le code proposé ([`proposed/Appointment.cs.txt`](proposed/Appointment.cs.txt), [`proposed/AppointmentModel.cs.txt`](proposed/AppointmentModel.cs.txt), [`proposed/permissions_dbcontext_snapshot_0004.diff.txt`](proposed/permissions_dbcontext_snapshot_0004.diff.txt)) ajusté aux décisions, puis service, validation, routes et tests (comme 03-A2).
2. Régénérer la migration (l'horodatage `20261017141136` changera) et soumettre le SQL exact à la revue du propriétaire.
3. GO ciblé d'application sur `mmv_v2_dev` puis `mmv_v2_test` ; tests PostgreSQL ; activation du service (`APPOINTMENTS_API_ENABLED = true`) ; attribution des droits par le propriétaire.
