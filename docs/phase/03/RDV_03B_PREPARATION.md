# 03 — Préparation technique du sous-lot 03-B Rendez-vous (17/10/2026)

> **Amendement daté 17/10/2026 (03-B, GO « PHASE 03-B — RENDEZ-VOUS »).** Le cadrage ci-dessous est conservé tel qu'établi. Réalisé depuis : **FullCalendar Standard 7.1.0** (`@fullcalendar/react`, MIT, publiée le 05/09/2026, seule version 7.x publiée depuis ≥ 2 semaines ; 7.1.1 à 7.1.3 écartées) et `temporal-polyfill` 1.0.5 (MIT) installés à versions figées ; vues `timeGridDay` / `timeGridWeek` dans un chunk séparé (`AppointmentsModule`, 370 kB) ; rendu par classes MMV (FullCalendar 7 ne fournit que `skeleton.css`, thèmes non utilisés) ; écran `/rendez-vous` (agenda, panneau détail, formulaire, sélecteur client, annulation / rétablissement, conflits, états) testé sur double ; **service désactivé** (`APPOINTMENTS_API_ENABLED = false`). Le fuseau nommé est géré nativement par FullCalendar 7 (Temporal). Le déplacement au clavier passe par la commande « Déplacer » (formulaire date / heure) ; la liste clavier parallèle envisagée est remplacée par la navigation ↑/↓ entre les rendez-vous de la grille, qui sont des boutons focalisables (Entrée / Espace = détail). Décisions : [`DECISIONS_03B_RDV_PROPOSITION.md`](DECISIONS_03B_RDV_PROPOSITION.md) ; contrat : [`API_APPOINTMENTS_PROPOSED.md`](API_APPOINTMENTS_PROPOSED.md) ; migration proposée `20261017141136_AppointmentsModule` (`sql/0004_AppointmentsModule.PROPOSED*.sql`, non appliquée). Séquence : étapes 2 et 3 réalisées, étape 1 (GO des décisions) en attente, étape 4 préparée (modèle et SQL proposés).

**Statut : cadrage `PROPOSED`.** Aucun paquet FullCalendar installé, aucun écran, aucune table, aucune migration. Seule l'entrée de menu « Rendez-vous » (`/rendez-vous`, `apps/desktop/src/shell/navigation.ts`) existe, affichée comme indisponible avec la mention « Prévu au sous-lot 03-B… ». Décisions métier : [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) section D (D-03-13 à D-03-17). Inventaire des 18 états : [`STATE_MAP_47.md`](STATE_MAP_47.md).

## FullCalendar Standard — faisabilité (vérifiée sur le registre npm le 17/10/2026)

| Constat | Détail |
|---|---|
| Version majeure courante | `@fullcalendar/react` **7.1.3** (16/10/2026), `@fullcalendar/core` 7.1.3, licence **MIT**. Pairs : `react`/`react-dom` `^17‖^18‖^19` (React 19.3 compatible), `temporal-polyfill ^1.0.1`. |
| Vues Standard en v7 | intégrées au paquet React par sous-chemins : `@fullcalendar/react/timegrid`, `/daygrid`, `/interaction`, `/list`, `/multimonth`, thèmes `themes/*` (vérifier l'API exacte au démarrage de 03-B). Les paquets séparés `@fullcalendar/timegrid` / `interaction` n'existent qu'en **6.1.22** (MIT). |
| Premium (exclu) | `@fullcalendar/resource*`, timeline : licence commerciale (« SEE LICENSE IN LICENSE.md »). **Non utilisés** : la décision D-03-14 (responsable = compte, filtrage par responsable) ne requiert pas de vue « ressources ». |
| Règle de prudence | Version retenue publiée depuis ≥ 2 semaines au moment de l'installation (7.1.3 le sera à partir du 30/10/2026 ; à défaut 7.1.0 du 05/09/2026), figée dans `package-lock.json`, licence consignée dans `SOURCE_MAP.md` (ADR-014). |

**Recommandation :** v7 (ligne active, React 19, Temporal pour les fuseaux) si l'essai de 03-B confirme : rendu stylable par les jetons MMV (thème `classic` neutralisé ou CSS propre), lisibilité clavier/lecteur d'écran, poids du bundle (chunk séparé comme le module Clients). Repli : 6.1.22 (paquets séparés, MIT).

## Vues et interactions

| Besoin (planches 28–43) | Mise en œuvre envisagée |
|---|---|
| Jour / semaine | `timeGridDay`, `timeGridWeek` ; `dayGridMonth` **non retenu** (aucune planche mois) |
| Heure courante | `nowIndicator` |
| Lun–sam, 09:00–19:00 | `hiddenDays`, `slotMinTime` / `slotMaxTime` paramétrés — **non bloquants** tant que D-03-13 ne fixe pas d'horaires d'ouverture |
| Création par sélection | `selectable` + `select` → panneau de création pré-rempli |
| Déplacement | `editable` (glisser) **et** commande explicite « Déplacer » au clavier (formulaire date/heure) ; toute modification passe par l'API, qui peut refuser (409) |
| Conflit | décidé **serveur** (D-03-15), `eventDrop` annulé (`revert()`) sur refus |
| Alternative clavier obligatoire | barre d'outils accessible (Aujourd'hui, Précédent/Suivant, Jour/Semaine), liste des rendez-vous du jour navigable (↑/↓, Entrée = détail) parallèle à la grille, toutes les actions (créer, modifier, déplacer, annuler) disponibles sans souris depuis le panneau détail |
| Sélecteur de client | réutilise `GET /api/clients?status=active&q=` (planche 40) ; clients archivés exclus (D-03-05) |

## Contrat et modèle envisagés (à valider avec D-03-13 à D-03-17)

`appointments.appointments` : `id` uuid, `client_id` → `clients.clients` (RESTRICT), `responsible_user_id` → `auth.users` (nullable), `starts_at_utc`, `ends_at_utc` (`ends > starts`), `reason` (liste à définir), `notes` (≤ 1000, sans donnée de santé), `status` (`planned|cancelled`), `cancelled_at_utc`, `cancellation_reason`, audit `created/updated_*`, concurrence `xmin`. Index `(responsible_user_id, starts_at_utc)` et `(client_id, starts_at_utc)`. Routes : `GET /api/appointments?from&to&responsible`, `POST`, `PUT /{id}`, `POST /{id}/cancel|restore`. Conflit : vérification transactionnelle avec verrou consultatif par responsable (aucune extension). **Migration indépendante** de 0003, soumise à son propre GO.

## Questions ouvertes (D-03-13 à D-03-17)

Fuseau du magasin (pays d'exploitation, ADR-010 ouvert) ; liste des motifs et durées ; responsable obligatoire ou non ; conflit bloquant ou avec confirmation ; modification des rendez-vous passés ; statuts « honoré / absent » ; visibilité des notes.

## Séquence proposée 03-B

1. GO D-03-13 à D-03-17 (+ droits `appointments.*`).
2. Installation FullCalendar (version figée) et essai d'intégration visuelle/clavier dans un chunk séparé ; décision v7/v6 consignée.
3. Écrans sur double de test (comme 03-A) : agenda jour/semaine, panneau détail, formulaire, sélecteur client, annulation, conflit.
4. Domaine, API, migration générée hors connexion → GO ciblé → application, tests PostgreSQL `mmv_v2_test`.
5. 03-C : intégration client ↔ rendez-vous (onglet ou bloc dans la fiche), preuves natives Tauri.
