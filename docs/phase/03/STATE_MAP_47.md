# 03 — Correspondance des 47 états visuels du Lot 2 (17/10/2026)

Source : `references/phase03/LOT2_ETATS_VISUELS.md` et `references/phase03/lot2/MMV-Lot2-Clients-RendezVous.dc.html` (**DRAFT**, empreintes dans [`SOURCE_MAP.md`](SOURCE_MAP.md)). Un état visuel n'est **pas** une règle métier.

Colonnes : **UI 03-A** = ce qui existe dans le code (`IMPLEMENTED` : écran réel testé ; `PENDING_SERVICE` : affiché seulement quand le service Clients sera activé, testé sur double ; `N/A` : sans objet par recommandation ; `03-B` : sous-lot Rendez-vous) ; **Décision** = référence de [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) ; **Statut** = `APPROVED` / `PROPOSED` / `DEFERRED`. Aucune ligne n'est `APPROVED` à ce jour.

> **Amendement daté 17/10/2026 (03-A2).** Tableau conservé tel qu'établi en 03-A. Depuis 03-A2 : le service Clients est **activé** (`CLIENTS_API_ENABLED = true`) et raccordé aux routes réelles `/api/clients` ; les lignes `PENDING_SERVICE` sont donc servies par la vraie API, mais restent **non démontrables en natif** tant que la migration 0003 n'est pas appliquée et que des droits `clients.*` ne sont pas attribués (QA Q-03-75 `NOT_TESTED`). Décisions : les lignes rattachées à D-03-01 à D-03-10 passent de `PROPOSED` à **ACCEPTED** (G03-METIER A, B) ; D-03-11 / D-03-12 **ACCEPTED sur le principe** ; les états Rendez-vous (D-03-13 à D-03-17) restent `PROPOSED` ; `DEFERRED` inchangés. Aucun écran visuellement modifié ; aucun état `APPROVED` (approbation visuelle non prononcée).

## Clients (29 états)

| ID | État | Fonctionnalité | UI 03-A | Tests | Décision | Statut |
|---|---|---|---|---|---|---|
| MMV-L02-listLight | Liste clair | liste paginée, colonnes Client/Téléphone/Email/Ville/Créé le/Statut/Actions | PENDING_SERVICE | `ClientsModule.test.tsx` « colonnes… » | D-03-01, D-03-02, D-03-11 | PROPOSED |
| MMV-L02-listDark | Liste sombre | jetons du thème sombre | PENDING_SERVICE | `styles.test.ts` (jetons sombres) | — | PROPOSED |
| MMV-L02-listArchived | Inclure les archivés | `status=all`, ligne atténuée + badge | PENDING_SERVICE | « inclure les archivés… » | D-03-05 | PROPOSED |
| MMV-L02-searchHit | Recherche — résultats | recherche serveur, surlignage | PENDING_SERVICE | « recherche différée… » | D-03-04 | PROPOSED |
| MMV-L02-searchEmpty | Recherche — aucun résultat | état + « Effacer la recherche » | PENDING_SERVICE | idem | D-03-04 | PROPOSED |
| MMV-L02-filters | Filtres et tri | statut (actifs/archivés/tous) + tri ; **filtre Ville absent** | PENDING_SERVICE (partiel) | « tri serveur… » | D-03-11 (Ville : alternative) | PROPOSED / Ville DEFERRED |
| MMV-L02-listEmpty | Vide | « Aucun client enregistré » | PENDING_SERVICE | « états : chargement puis vide » | — | PROPOSED |
| MMV-L02-listLoading | Chargement | squelette + « Chargement des clients… » | PENDING_SERVICE | idem | — | PROPOSED |
| MMV-L02-listError | Erreur technique | message borné + Réessayer | PENDING_SERVICE | « erreur technique (500)… » | — | PROPOSED |
| MMV-L02-formLight | Création clair | formulaire unique, 2 colonnes, sections | IMPLEMENTED (aperçu, enregistrement désactivé) | « validation… », « création… » | D-03-02, D-03-03 | PROPOSED |
| MMV-L02-formDark | Création sombre | jetons sombres | IMPLEMENTED | `styles.test.ts` | — | PROPOSED |
| MMV-L02-formErrors | Erreurs de validation | messages sous champs, bandeau, focus 1er champ | IMPLEMENTED | « validation… », « erreur de validation serveur… » | D-03-02 | PROPOSED |
| MMV-L02-formEdit | Modification | pré-rempli, sous-titre NOM Prénom · référence | PENDING_SERVICE | « modification : pré-remplie… » | D-03-06 | PROPOSED |
| MMV-L02-formUnsaved | Non enregistré | bandeau « Modifications non enregistrées » | IMPLEMENTED | « création au clavier… » | — | PROPOSED |
| MMV-L02-confirmClose | Fermeture | confirmation Annuler **et** menu du Shell | IMPLEMENTED | « modifications non enregistrées… » | — | PROPOSED |
| MMV-L02-createSuccess | Créé | carte « Client créé », Voir la fiche / Créer un autre ; **sans** « Ajouter une ordonnance » | PENDING_SERVICE | « création au clavier… » | D-03-03 | PROPOSED |
| MMV-L02-ficheLight | Fiche clair | en-tête, Coordonnées, Infos complémentaires, Notes, Repères ; **sans** compteurs ni « Dernière visite » | PENDING_SERVICE | « onglets au clavier… » | D-03-03 | PROPOSED |
| MMV-L02-ficheDark | Fiche sombre | jetons sombres | PENDING_SERVICE | `styles.test.ts` | — | PROPOSED |
| MMV-L02-ficheRx | Onglet Ordonnances | état explicite « phase ultérieure », aucune donnée | IMPLEMENTED (état reporté) | « onglets au clavier… » | Phase 04 | DEFERRED |
| MMV-L02-ficheSales | Onglet Ventes & commandes | état explicite « phase ultérieure » | IMPLEMENTED (état reporté) | idem | Phase ultérieure | DEFERRED |
| MMV-L02-ficheArchived | Fiche archivée | bandeau lecture seule, Modifier désactivé, Réactiver | PENDING_SERVICE | « client archivé : lecture seule… » | D-03-05 | PROPOSED |
| MMV-L02-ficheNoRx | Sans ordonnance | même état reporté que ficheRx | IMPLEMENTED (état reporté) | — | Phase 04 | DEFERRED |
| MMV-L02-ficheNoSales | Sans vente | même état reporté que ficheSales | IMPLEMENTED (état reporté) | — | Phase ultérieure | DEFERRED |
| MMV-L02-confirmArchive | Confirmer l'archivage | dialogue `alertdialog`, piège de focus, Échap | PENDING_SERVICE | « archivage depuis la liste… » | D-03-05, D-03-08 | PROPOSED |
| MMV-L02-refuseDelete | Refus de suppression | aucune suppression proposée | N/A | — | D-03-05 | PROPOSED |
| MMV-L02-archivedDone | Archivé | bandeau `status` (au lieu d'un toast) | PENDING_SERVICE | « archivage depuis la liste… » | — | PROPOSED |
| MMV-L02-confirmReactivate | Confirmer réactivation | dialogue | PENDING_SERVICE | « client archivé… » | D-03-05 | PROPOSED |
| MMV-L02-reactivateDone | Réactivé | bandeau `status` | PENDING_SERVICE | idem | — | PROPOSED |
| MMV-L02-bizError | Erreur métier (modifié ailleurs) | 409 → « Action impossible… autre poste », données rechargées | PENDING_SERVICE | « conflit de concurrence… » | D-03-06 | PROPOSED |

État transverse ajouté (hors planches) : **« Service Clients non activé »** (liste et fiche) tant que G03-METIER n'est pas ratifié — seul état visible en fonctionnement normal aujourd'hui, avec l'aperçu du formulaire.

## Rendez-vous (18 états) — préparation 03-B

Voir [`RDV_03B_PREPARATION.md`](RDV_03B_PREPARATION.md). Aucun écran implémenté ; entrée de menu « Rendez-vous » (`/rendez-vous`) affichée comme indisponible avec la mention du sous-lot 03-B.

| ID | État | Fonctionnalité | Solution envisagée | Décision | Statut |
|---|---|---|---|---|---|
| MMV-L02-calDayLight / calDayDark | Jour | rail horaire, repère heure courante | FullCalendar `timeGridDay`, `nowIndicator` | D-03-13 | 03-B / PROPOSED |
| MMV-L02-calWeekLight / calWeekDark | Semaine (lun–sam) | semaine chargée | `timeGridWeek`, `hiddenDays` selon D-03-13 | D-03-13 | 03-B / PROPOSED |
| MMV-L02-calEmpty | Journée vide | état vide | rendu FullCalendar + message | — | 03-B |
| MMV-L02-calSelected | Sélectionné | panneau détail latéral | `eventClick` + panneau React | D-03-17 | 03-B |
| MMV-L02-calMove | Déplacement | commande explicite + glisser | `interaction` (`editable`) **et** commande clavier « Déplacer » (formulaire date/heure) | D-03-15 | 03-B |
| MMV-L02-calConflict | Conflit | créneau indisponible | refus 409 serveur, retour visuel | D-03-15 | 03-B / PROPOSED |
| MMV-L02-rdvFormLight / rdvFormDark | Création | client, date, heure, durée, responsable, motif, notes | RHF + Zod, panneau latéral | D-03-13, D-03-14, D-03-17 | 03-B |
| MMV-L02-rdvDetailLight / rdvDetailDark | Détail | Modifier / Déplacer / Annuler | panneau | D-03-16 | 03-B |
| MMV-L02-rdvEdit | Modification | pré-rempli | idem création + `version` | D-03-06 (même principe) | 03-B |
| MMV-L02-rdvPicker | Sélection du client | recherche clients actifs | réutilise `GET /api/clients?status=active&q=` | D-03-04, D-03-05 | 03-B |
| MMV-L02-rdvErrors | Erreurs de validation | messages sous champs | RHF + Zod | — | 03-B |
| MMV-L02-rdvCancel | Annulation | confirmation, motif, rétablissement | dialogue `alertdialog` existant | D-03-16 | 03-B |
| MMV-L02-rdvConflict | Conflit horaire | bandeau d'erreur | 409 `appointment_conflict` | D-03-15 | 03-B |
| MMV-L02-rdvSuccess | Créé | confirmation | bandeau `status` | — | 03-B |

Total : 29 états Clients + 18 états Rendez-vous = **47**.

## Écarts de rendu volontaires (Clients)

| Planche | Écart | Raison |
|---|---|---|
| 01–09 | bouton « Importer » absent | aucune décision d'import |
| 06 | filtre Ville absent | exige une liste de villes serveur ; non ratifié |
| 10–12 | champs « N° de sécurité sociale » et « Assurance / mutuelle » absents | D-03-03 |
| 10 | date de naissance en saisie texte `jj/mm/aaaa` | contrôle clavier homogène, pas de sélecteur natif localisé |
| 13 | mention « Brouillon centralisé » absente | aucun brouillon serveur |
| 15 | bouton « Ajouter une ordonnance » absent | Phase 04 |
| 16 | cartes « 2 Ordonnances / 2 Ventes », « Dernière visite », boutons « Créer une vente » / « Ajouter une ordonnance » absents | aucune donnée réelle, phases ultérieures |
| 17–18 | tableaux d'ordonnances et ventes remplacés par un état « phase ultérieure » | pas de simulation de données métier |
| 22, 14, 38 | bouton de confirmation en accent (bleu) au lieu d'orange/rouge plein | contraste blanc/orange ≈ 2,3:1 < 4,5:1 (WCAG AA) ; ton porté par l'icône du dialogue |
| 24, 26 | bandeau de confirmation au lieu d'un toast flottant | aucune bibliothèque ajoutée (Sonner au backlog) |
| toutes | barre d'état et menu repris du Shell Phase 02 (pas de « Poste 1 », profils du Lot 1, badge de notification) | Shell Phase 02 inchangé |

## Amendement daté 17/10/2026 (03-A3) — démonstration native et comparaison aux planches

Les tableaux ci-dessus restent tels qu'établis. Depuis 03-A3, les lignes `PENDING_SERVICE` des états Clients sont **démontrées en natif** sur la chaîne réelle Tauri → HTTPS → MMV.Api → PostgreSQL 18 (`mmv_v2_test`, comptes et clients éphémères) : QA Q-03-90, captures `evidence/P03_native_clients_*_20261017T1115*.png`. Aucune ligne n'est `APPROVED` : l'approbation visuelle reste au propriétaire.

| Planche (data-mount) | Capture native | Constat |
|---|---|---|
| listLight / listDark | `list_light_1920x1080`, `list_dark_1920x1080`, `list_light_1440x900` | structure conforme (en-tête, recherche, « Inclure les clients archivés », Filtres, tri, colonnes, badges, pagination 20/page) ; sans « Importer » (import OUT) |
| listArchived | `list_with-archived_1920x1080` | conforme (puce active, statut serveur `all`) |
| searchHit / searchEmpty | `search_hit_1920x1080`, `search_empty_1920x1080` | conforme ; surlignage mot par mot (écart **corrigé** en 03-A3 : une requête à plusieurs mots n'était pas surlignée) |
| formLight / formErrors / formUnsaved / formEdit | `form_empty`, `form_errors`, `form_filled_unsaved`, `form_edit` | conforme (2 colonnes, sections, Ctrl+S, bandeaux, messages sous les champs, focus) ; sans NSS ni assurance/mutuelle (D-03-03) ; « Enregistrer » reste actif en état d'erreur (planche : grisé) — la validation est rejouée au clic |
| createSuccess | `create_success_1920x1080` | conforme, sans « Ajouter une ordonnance » (D-03-03) |
| ficheLight / ficheDark / ficheArchived | `detail_light`, `detail_dark`, `detail_archived` | conforme hors écarts volontaires (planche 16 ci-dessus) ; sur la fiche archivée, la notice « Client archivé » de l'opération précédente s'ajoute au bandeau permanent (O-03-15) |
| confirmArchive / archivedDone / reactivateDone | `archive_confirm`, `archived_notice` | conforme (bandeau au lieu d'un toast, écart 24/26) |
| bizError | `conflict_409_1920x1080` | même dialogue « Action impossible » ; depuis le formulaire, actions « Rester sur le formulaire » / « Recharger la fiche » (planche : depuis la liste, « Actualiser la liste ») |
| — (sans planche) | `forbidden_admin-only`, `list_read-only`, `detail_read-only`, `detail_write-no-archive` | états de droits ajoutés en 03-A3 (O-03-09) : « Accès non autorisé », motif « Consultation seule… », actions masquées (liste) ou désactivées (fiche) |

Écarts propres au Shell Phase 02 constatés sur toutes les captures (hors périmètre 03-A3) : entrées de navigation différentes du Lot 2, recherche globale désactivée, logo sur fond clair en thème sombre.

## Amendement daté 17/10/2026 (03-B) — états Rendez-vous implémentés sur double de test

Le tableau « Rendez-vous (18 états) » ci-dessus reste tel qu'établi. Depuis 03-B, les états sont **implémentés** dans `apps/desktop/src/appointments/` et éprouvés contre le double du contrat proposé (`AppointmentsModule.test.tsx`, QA Q-03-100 à Q-03-112) et sur un banc visuel (Vite + double, captures `evidence/P03B_bench_rdv_*_1440x900_20261017.jpg`). En fonctionnement normal, seul l'état « Service Rendez-vous non activé » est visible (décisions D-03-13 à D-03-18 `PROPOSED`). Aucune ligne n'est `APPROVED`.

| ID | Implémentation 03-B | Test | Écart volontaire |
|---|---|---|---|
| calDayLight / calDayDark | `AppointmentsCalendar.tsx` (`timeGridDay`, 64 px/h, repère d'heure courante, fuseau serveur) | « vue Jour… » | trait atténué à la demi-heure ; responsable affiché sur la grille à partir de 60 min (comme la planche, hauteur insuffisante en dessous) |
| calWeekLight / calWeekDark | `timeGridWeek`, jours masqués selon `hiddenWeekdays` | « vue Semaine… » | noms abrégés « Y. El Amrani » comme la planche |
| calEmpty | carte « Aucun rendez-vous ce jour » / « cette semaine » | « journée vide… » | — |
| calSelected | sélection + panneau détail | « ↑/↓ parcourent… » | — |
| calMove | glisser (souris, `startEditable`, aimant 15 min) **et** commande « Déplacer » (clavier) | « déplacer au clavier… » | glisser `NOT_TESTED` sous jsdom (géométrie absente) |
| calConflict | refus 409 → `revert()` + bandeau « Créneau indisponible » | via le formulaire : « déplacement en conflit… » | bandeau au lieu de la bande hachurée sur la grille |
| rdvFormLight / rdvFormDark | `AppointmentFormPanel.tsx` (RHF + Zod, listes serveur) | « au clavier : erreurs… » | date et heure en saisie texte `jj/mm/aaaa`, `hh:mm` (comme Clients) |
| rdvDetailLight / rdvDetailDark | `AppointmentDetailPanel.tsx` | « ↑/↓… », « lecture seule… » | badge « Planifié » au lieu de « Confirmé » (aucun circuit de confirmation) |
| rdvEdit | mode `edit`, version figée à l'ouverture | « modification pré-remplie… » | — |
| rdvPicker | `ClientPicker.tsx` (combobox, clients actifs) | « au clavier… », « écriture sans clients.read… » | « Créer un nouveau client » absent (03-C) |
| rdvErrors | messages sous les champs, focus sur le premier champ en erreur | « au clavier… », « heure inexistante… » | bouton Créer actif (validation rejouée au clic, comme Clients) |
| rdvCancel | `ConfirmDialog` + motif facultatif ; détail annulé + « Rétablir » | « annulation avec motif… », « rétablissement refusé… » | bouton de confirmation en accent (contraste, comme Clients) |
| rdvConflict | bandeau « Conflit horaire » dans le formulaire | « conflit horaire… » | texte = créneau demandé (aucune donnée de l'autre rendez-vous) |
| rdvSuccess | bandeau `status` « Rendez-vous créé » | « au clavier… » | bandeau au lieu d'un toast (comme Clients) |

