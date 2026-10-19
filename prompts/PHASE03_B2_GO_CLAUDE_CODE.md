# MMV V2 — GO MÉTIER PHASE 03-B2 — RENDEZ-VOUS

Le propriétaire valide le développement métier du module Rendez-vous selon les décisions consolidées ci-dessous.

Le sous-lot 03-B1 a déjà livré l'interface. Il faut maintenant réaliser le backend, les contrats, les tests et la préparation de la migration.

Ne recommence pas l'interface existante.

## 1. Décisions D-03-13 à D-03-18

### D-03-13 — Fuseaux, durées et horaires

MMV cible initialement le Maroc et la Belgique.

- Fuseau IANA configuré côté serveur par magasin ou installation.
- Support de Africa/Casablanca et Europe/Brussels.
- Dates persistées en UTC dans PostgreSQL.
- Affichage dans le fuseau du magasin.
- Ne jamais utiliser implicitement le fuseau du poste client.
- Refuser les heures locales inexistantes ou ambiguës lors des changements d'heure.
- Durées autorisées : 15, 30, 45, 60, 90 et 120 minutes.
- Aucune durée présélectionnée par défaut.
- Début de rendez-vous au pas de cinq minutes.
- Aucun rendez-vous ne traverse minuit dans cette version.
- Création ou déplacement dans le passé interdits.
- Horaires d'affichage configurables.
- Ne pas imposer universellement 09:00–19:00 ni la fermeture le dimanche.
- Les règles d'ouverture bloquantes restent à développer dans le futur module Paramètres.

Le fuseau exact et les paramètres du magasin doivent être explicitement configurés avant l'activation réelle du service. Ne pas inventer une localisation unique valable pour tous les clients MMV.

### D-03-14 — Attribution des collaborateurs

Deux modes :

**Rendez-vous personnalisé**
- Responsable obligatoire.
- Conflits vérifiés par collaborateur.
- Un responsable doit être actif et explicitement éligible à recevoir des rendez-vous.
- L'éligibilité n'est pas déduite simplement des permissions d'administration de l'agenda.

**Rendez-vous d'accueil**
- Responsable facultatif.
- Capacité commune obligatoire lorsqu'aucun responsable n'est sélectionné.
- Aucun nombre illimité de réservations non attribuées.

Répartition initiale :

Responsable obligatoire :
- conseil_devis
- choix_equipement
- prise_mesures
- essayage_ajustement
- autre

Responsable facultatif :
- retrait_commande
- sav_reparation

Ne pas ajouter de motif « accueil » : c'est un mode d'organisation, pas un huitième motif.

### D-03-15 — Conflits et capacité

Pour un rendez-vous attribué :
- Refuser le chevauchement avec un autre rendez-vous planifié du même collaborateur.
- Autoriser les rendez-vous qui se suivent exactement.
- HTTP 409 en cas de conflit.

Pour un rendez-vous non attribué :
- Utiliser un pool de capacité d'accueil propre au magasin.
- Capacité configurable côté serveur.
- Pour le POC local, une capacité de 1 peut être explicitement configurée si une ressource d'accueil réelle lui est réservée.
- Aucune capacité implicite illimitée.
- Si la capacité n'est pas définie, refuser les réservations sans responsable.
- Réserver la capacité pour toute la durée du rendez-vous.
- Libérer la capacité lors de l'annulation.
- La restaurer uniquement si elle reste disponible.

IMPORTANT : calculer la capacité réellement occupée à chaque instant du créneau demandé. Un simple décompte de tous les rendez-vous qui chevauchent le créneau peut donner un résultat incorrect.

Implémenter des garanties transactionnelles PostgreSQL contre les réservations concurrentes, y compris avec deux postes effectuant une demande simultanée.

Éviter les interblocages en utilisant un ordre stable lorsque plusieurs ressources doivent être verrouillées.

Ne pas introduire FullCalendar Premium ou de microservice de réservation.

### D-03-16 — Annulation et rétablissement

- Statuts planned et cancelled.
- Annulation logique, sans DELETE physique.
- Motif d'annulation facultatif, 200 caractères maximum.
- Conservation de l'auteur et de la date d'annulation.
- Rétablissement après nouveau contrôle des conflits et de la capacité.
- Aucun nouveau rendez-vous pour un client archivé.
- Rendez-vous existants d'un client archivé consultables et annulables.
- Aucun rétablissement pour un client archivé.
- Statuts honoré / absent reportés.

### D-03-17 — Permissions et confidentialité

Permissions :
- appointments.read
- appointments.write
- appointments.cancel

La recherche et la sélection d'un client nécessitent clients.read.

system.admin ne donne pas implicitement ces permissions.

Conserver les contrôles d'autorisation côté API.

Dans l'agenda, minimiser les données client affichées. Ne pas exposer téléphone, email, données de santé ou informations sensibles inutiles.

Les notes de rendez-vous doivent rester des notes opérationnelles non médicales, limitées à 1 000 caractères. Ne journaliser aucun contenu de note ni nom de client.

Prévoir une gestion explicite de l'éligibilité des collaborateurs à être responsables, indépendante des droits permettant de consulter ou modifier l'agenda.

### D-03-18 — Motifs APPROUVÉS

Liste exacte :

- conseil_devis : Conseil / Devis
- choix_equipement : Choix de monture et verres
- prise_mesures : Prise de mesures
- essayage_ajustement : Essayage / Ajustement
- retrait_commande : Retrait de commande
- sav_reparation : SAV / Réparation
- autre : Autre motif

Motif obligatoire, codes stables, libellés provenant de l'API.

Pas de table de personnalisation des motifs dans cette première version.

## 2. Intégration dans le parcours métier

Depuis la fiche client :

- Proposer l'action « Prendre un rendez-vous ».
- Conserver automatiquement le client sélectionné.
- Ouvrir le formulaire avec le client prérempli.
- Permettre de revenir à sa fiche après l'opération.
- Ne jamais forcer une seconde recherche du même client.

L'accès reste soumis aux permissions serveur.

Conserver les écrans existants, le Design System et les références visuelles validées.

## 3. Stack obligatoire

Frontend :
- Tauri 2
- React / TypeScript / Vite
- Tailwind CSS 4
- shadcn/ui adapté et Radix
- Lucide
- React Router
- TanStack Query
- FullCalendar Standard
- React Hook Form / Zod
- Vitest / Testing Library

Backend :
- ASP.NET Core .NET 10
- Architecture modulaire actuelle
- EF Core / Npgsql
- PostgreSQL 18 exclusivement
- Authentification et permissions existantes
- OpenAPI avec schémas exploitables par TypeScript
- Tests xUnit et intégration PostgreSQL

N'introduis aucun nouveau framework sans nécessité démontrée.

## 4. Développement autorisé

Tu peux :
- Implémenter le module métier Rendez-vous côté serveur.
- Définir les contrats et endpoints.
- Implémenter les deux modes d'attribution.
- Implémenter les contrôles de conflits et de capacité.
- Adapter le frontend 03-B1 aux décisions approuvées.
- Intégrer la création de rendez-vous depuis la fiche client.
- Ajouter les tests automatisés nécessaires.
- Préparer la migration EF Core 0004 et ses scripts SQL.
- Mettre à jour les outils de maintenance et les permissions proposées.

Ne pas inventer de règles métier supplémentaires.

Toute nouvelle table ou modification de auth doit être justifiée dans le dossier SQL.

## 5. Limite PostgreSQL

Aucune application de la migration 0004 avant une revue du SQL.

Ne modifier aucune base réelle à cette étape.

Aucun changement de PostgreSQL 17, aucun DROP et aucune migration automatique au démarrage.

Les tests qui exigent le nouveau schéma doivent être notés NOT_TESTED tant que la migration n'est pas appliquée.

## 6. Tests attendus

Tester notamment :
- Rendez-vous avec responsable.
- Rendez-vous sans responsable.
- Capacité disponible et capacité saturée.
- Réservations simultanées.
- Chevauchements.
- Créneaux consécutifs.
- Modification et déplacement.
- Annulation et rétablissement.
- Droits insuffisants.
- Client archivé.
- Collaborateur désactivé.
- Changements d'heure Maroc et Belgique.
- Accessibilité clavier.
- Création depuis la fiche client.

Ne jamais déclarer un test réel PostgreSQL réussi s'il n'a pas été exécuté.

## 7. DOCUMENTATION GATE

Mettre à jour les documents concernés :

- README.md
- docs/phase/03/
- DECISIONS_03B_RDV_PROPOSITION.md
- DECISIONS_G03_METIER.md
- GATES_AND_DECISIONS.md
- QA_MATRIX.md
- IMPLEMENTATION_SPEC.md
- SOURCE_MAP.md
- Architecture et ADR concernés
- Documents de stratégie produit si impactés

Consigner les décisions D-03-13 à D-03-18 et distinguer les principes approuvés des paramètres par magasin encore à renseigner.

Préserver les décisions historiques.

## 8. Livraison

Travaille sur feat/phase-03-clients-rdv.

Commits locaux autorisés.

Aucun push, aucune PR, aucun merge, aucune Phase 04.

Produis une archive légère git archive avec les sources, tests, documentation, migration C# et SQL.

Présente un rapport consolidé et le SQL exact à vérifier avant application.

Évite les demandes de validation intermédiaires non indispensables.

**PHASE03_B2_READY — OWNER_SQL_REVIEW_REQUIRED**
