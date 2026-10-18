# MMV V2 — GO PHASE 03-D — AMÉLIORATIONS UX ET HISTORIQUE CLIENT

La Phase 03-C a été testée manuellement par le propriétaire.

Le fonctionnement général Clients → Rendez-vous est jugé satisfaisant.

Trois améliorations ont été expressément demandées :
1. Sélection visuelle de la date et de l'heure.
2. Historique complet des rendez-vous dans la fiche client.
3. Historique des actions sur le dossier client, consultable par les utilisateurs habilités.

Ces besoins sont confirmés. Aucun redesign général n'est demandé.

## LOT 03-D1 — ERGONOMIE ET RENDEZ-VOUS

### 1. DatePicker

Remplacer la saisie textuelle de date par un calendrier compact dans un Popover, cohérent avec le Design System MMV.

Utiliser les composants shadcn/ui et Radix existants lorsque possible, avec React DayPicker si nécessaire.

Exigences :
- Navigation entre les mois et les années.
- Sélection de la date par clic.
- Accessibilité clavier.
- Affichage français JJ/MM/AAAA.
- Pas de saisie manuelle obligatoire.
- Respect du fuseau du magasin transmis par l'API.
- Dates passées désactivées selon les règles métier.
- Validation définitive côté serveur.
- Aucun décalage de date introduit par une conversion UTC incorrecte.

### 2. TimePicker

Remplacer la saisie manuelle de l'heure par une liste déroulante accessible et ergonomique.

Exigences :
- Heures au format HH:mm.
- Pas de cinq minutes, conformément aux décisions approuvées.
- Sélection rapide à la souris et au clavier.
- Liste défilante, sans panneau géant de centaines de lignes.
- Heure conservée pendant les modifications lorsque c'est possible.
- Validation des heures inexistantes ou ambiguës.
- Pas de réservation implicitement limitée aux heures affichées de la grille.
- Ne pas présenter un créneau comme disponible sans confirmation fiable du serveur.

Lorsqu'un rendez-vous est créé en sélectionnant un créneau depuis FullCalendar, préremplir sa date et son heure.

Le formulaire doit rester agréable à utiliser en thème clair et sombre, notamment aux zooms 100 %, 125 % et 150 %.

### 3. Historique des rendez-vous dans la fiche client

Ajouter un véritable onglet « Rendez-vous » dans la fiche client.

Afficher :
- Rendez-vous futurs.
- Rendez-vous passés.
- Rendez-vous annulés.
- Date et heure du rendez-vous.
- Motif.
- Responsable, s'il existe.
- Statut.
- Accès au détail du rendez-vous.

Les données proviennent exclusivement de PostgreSQL via l'API.

Privilégier une requête filtrée par client dans le module Appointments, propriétaire de ces données.

Prévoir tri, pagination, états vide, chargement et erreur.

Appliquer strictement les permissions appointments.read, sans fuite de données pour les utilisateurs non autorisés.

Conserver « Prendre un rendez-vous » depuis la fiche client avec client prérempli.

Ne créer aucune donnée fictive dans mmv_v2_dev.

## LOT 03-D2 — HISTORIQUE DES ACTIONS

### 4. Architecture d'audit

Concevoir un système centralisé de journal d'audit métier, réutilisable par les futurs modules.

Proposer un schéma PostgreSQL audit et des services .NET dédiés, sans créer de microservice.

Événements initiaux :

Clients :
- Création.
- Modification des informations.
- Archivage.
- Réactivation.

Rendez-vous :
- Création.
- Modification.
- Déplacement.
- Changement de responsable.
- Annulation.
- Rétablissement.

L'architecture doit permettre d'ajouter ultérieurement Ordonnances, Ventes, Commandes et Stock, sans inventer dès maintenant leur modèle métier.

Chaque événement doit identifier :
- Son identifiant unique.
- Le dossier client concerné.
- Le type d'action.
- La date UTC.
- L'utilisateur ayant réellement exécuté l'action.
- L'entité métier concernée.
- Les champs modifiés, lorsque pertinent.
- Les identifiants techniques nécessaires à la traçabilité.

Ne pas confondre le collaborateur responsable d'un rendez-vous avec l'utilisateur qui l'a créé.

### 5. Fiabilité et confidentialité

Les événements d'audit sont enregistrés par le backend dans la même transaction que les modifications métier.

Une opération échouée ne produit pas d'événement affirmant qu'elle a réussi.

Aucun historique généré uniquement côté React.

Aucun mot de passe, jeton de session, secret ou donnée médicale détaillée dans le journal.

Minimiser les valeurs personnelles enregistrées. Définir explicitement les champs dont les anciennes/nouvelles valeurs peuvent être conservées ou masquées.

Prévoir un accès en lecture via une permission dédiée audit.read, attribuable uniquement aux comptes habilités.

La séparation entre rôle visuel, permissions effectives et droits d'audit doit rester explicite.

Documenter les décisions concernant la conservation, la confidentialité et l'accès aux journaux pour les futurs marchés marocain et belge.

Ne pas inventer l'historique détaillé des opérations antérieures au déploiement de ce mécanisme.

### 6. Interface Historique

Ajouter un onglet « Historique » dans la fiche client, visible exclusivement avec la permission nécessaire.

Présenter une chronologie lisible :
- Date et heure locale.
- Utilisateur.
- Action réalisée.
- Champs concernés.
- Accès au détail lorsque permis.

Ajouter des filtres simples par type d'action et période si cela reste proportionné.

Prévoir pagination et chargement progressif.

### 7. Migration

Préparer la migration EF Core nécessaire pour le journal d'audit, probablement 0005.

Produire les scripts SQL directs et idempotents, ainsi que les tests correspondants.

Ne pas appliquer cette migration sans revue préalable du propriétaire.

Ne pas modifier les migrations 0001 à 0004 déjà appliquées.

Aucune migration automatique au démarrage.

PostgreSQL 18 uniquement.

## QUALITÉ ET VALIDATION

Préserver les fonctionnalités Clients et Rendez-vous validées.

Tester notamment :
- Date choisie via calendrier.
- Heure choisie par liste.
- Préremplissage depuis FullCalendar.
- Fuseaux Maroc et Belgique.
- Rendez-vous futurs, passés et annulés dans la fiche.
- Refus si appointments.read absent.
- Séparation entre permissions Clients, Appointments et Audit.
- Enregistrement atomique des événements.
- Absence d'événement de succès après rollback.
- Auteur et contenu des actions correctement associés.
- Absence de données sensibles dans les journaux.
- Interface Tauri native, clavier, zoom et thèmes.

Examiner également les petites anomalies 03-C ouvertes : O-03-33, O-03-34, O-03-35 et la cohérence d'affichage des noms O-03-36. Corriger les régressions et les défauts UX simples sans refonte inutile.

Ne pas modifier les comptes et données réels du propriétaire.

DOCUMENTATION GATE obligatoire : README, Phase 03, QA, SOURCE_MAP, GATES_AND_DECISIONS, spécification, architecture, ADR et stratégie produit si concernés.

Commits locaux autorisés sur feat/phase-03-clients-rdv.

Aucun push, aucune PR, aucun merge.

Ne pas commencer les modules métier de la Phase 04.

Procéder en autonomie :
- Livrer d'abord 03-D1 avec tests et preuve visuelle.
- Préparer puis implémenter 03-D2 dans le périmètre autorisé.
- Soumettre le SQL d'audit à la revue avant toute application.
- Fournir un rapport consolidé et une archive git archive.

Ne pas déclarer PHASE03_COMPLETE avant validation finale du propriétaire.

**GO PHASE 03-D — UX CLIENTS ET TRAÇABILITÉ**
