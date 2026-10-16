# 03 — Correspondance des 47 états visuels du Lot 2 (17/10/2026)

Source : `references/phase03/LOT2_ETATS_VISUELS.md` et `references/phase03/lot2/MMV-Lot2-Clients-RendezVous.dc.html` (**DRAFT**, empreintes dans [`SOURCE_MAP.md`](SOURCE_MAP.md)). Un état visuel n'est **pas** une règle métier.

Colonnes : **UI 03-A** = ce qui existe dans le code (`IMPLEMENTED` : écran réel testé ; `PENDING_SERVICE` : affiché seulement quand le service Clients sera activé, testé sur double ; `N/A` : sans objet par recommandation ; `03-B` : sous-lot Rendez-vous) ; **Décision** = référence de [`DECISIONS_G03_METIER.md`](DECISIONS_G03_METIER.md) ; **Statut** = `APPROVED` / `PROPOSED` / `DEFERRED`. Aucune ligne n'est `APPROVED` à ce jour.

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
