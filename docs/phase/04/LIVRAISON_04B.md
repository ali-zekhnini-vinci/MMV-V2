# 04-B — Livraison : dossier optique réel (backend .NET, migration 0006 préparée, raccordement 04-C préparé)

**Statut (27/10/2026) : `PHASE04_B_READY — OWNER_SQL_REVIEW_REQUIRED`.** Mandat : [`prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md`](../../../prompts/MMV_V2_GO_IMPLEMENTATION_PHASE04_B_ET_C_2026-10-27.md) (GO officiel du propriétaire, 27/10/2026). Branche locale `feat/phase-04-00-cadrage` (ascendance de `main` `6924860` vérifiée ; commits locaux, aucun push, PR ni merge). Migration `0006_OpticalModule` **préparée, NON APPLIQUÉE** : dossier de revue [`G04_SQL_DOSSIER.md`](G04_SQL_DOSSIER.md). Aucune connexion d'écriture à `mmv_v2_dev` / `mmv_v2_test`, aucun droit attribué à un compte réel, PostgreSQL 17 non touché.

## 1. Décisions appliquées (APPROVED par le mandat, non redemandées)

| Décision | Mise en œuvre |
|---|---|
| D-04-01 — deux types distincts | deux familles de tables, de routes d'écriture (`/prescriptions`, `/measurements`), de services (`PrescriptionService`, `MeasurementService`) et de provenances ; aucune conversion ; mesure jamais présentée comme une prescription |
| D-04-15 — design Lot 3, ± | composants 04-A réutilisés sans refonte ; **pas de 0,25 approuvé** pour Sphère, Cylindre, Addition (`OPTICAL_STEP`) ; **Prisme en saisie libre** (pas non arrêté : boutons ± retirés) ; saisie manuelle jamais arrondie ni forcée au pas |
| D-04-16 — pas d'état de l'œil, pièces reportées | aucune colonne d'état ; aucune table, API, permission ni contenu binaire documentaire (BL-04-01) |
| D-04-17 — « Enregistrer » | **atomique côté API** : création + confirmation (`POST /api/optical/{type}` avec `confirm: true`) ou modification + confirmation (`POST …/draft/confirm` avec contenu) en **une transaction** ; refus = rien d'écrit, aucun brouillon orphelin ; « Enregistrer comme brouillon » inchangé |
| Révisions | confirmées immuables (service + déclencheurs) ; rectification = brouillon distinct, la dernière révision confirmée reste lisible et de référence jusqu'à la confirmation suivante ; historique : auteur, date, motif, groupes modifiés, sans valeurs dans l'audit |
| Provenance | ordonnance : nom et date de prescription exigés **pour confirmer** (facultatifs au brouillon), qualité **libre** facultative, date de réception distincte, auteur de saisie rempli par le serveur ; mesure : date et collaborateur ayant mesuré exigés pour confirmer, catégorie **libre** facultative, auteur de l'enregistrement distinct |
| Dates | dates métier civiles `AAAA-MM-JJ` (`date`, aucune conversion UTC) ; instants d'audit horodatés par le serveur ; aucun fuseau présumé |
| Cycles et droits | invalidation motivée, sans suppression physique ; client archivé : consultation, écriture refusée (409 `client_archived`), invalidation autorisée ; 4 permissions explicites combinées avec `clients.read` ; aucune implication par `system.admin` ; historique servi seulement avec `audit.read` **et** `optical.read` ; contrôle serveur avant toute lecture |
| Audit | `audit.events` réutilisé, module `optical`, 12 codes ; événements dans le **même `SaveChanges`** que l'action ; aucune valeur optique, aucun nom de prescripteur, aucune note ni commentaire (groupes masqués `provenance`, `values`, `notes`, `comment`) ; conservés : numéros de révision, motifs codifiés |

## 2. Règles optiques appliquées — orientations produit, VALIDATION PROFESSIONNELLE OUVERTE

Codées une seule fois côté serveur ([`OpticalConfirmationRules`](../../../src/MMV.Application/Optical/OpticalInputValidator.cs)), appliquées à la **confirmation** seulement (le brouillon n'en relève jamais), codes renvoyés en 422 `incomplete` :

| Règle | Code | Réserve opticien |
|---|---|---|
| au moins une valeur optique, sur un œil au moins ; l'autre œil reste entièrement vide (`NULL`), jamais converti en 0 | `optical_values_missing` | — |
| une **seule** Sphère à zéro (`0`, `0,00`, `-0`…), sans aucune autre valeur optique, ne suffit pas ; `0,00` reste une valeur explicite conservée | `sphere_zero_only` | cas « deux sphères à zéro sans autre valeur » **accepté** (lecture littérale « une seule Sphère ») : à confirmer |
| ordonnance : acuité seule insuffisante ; mesure magasin : suffisante | `acuity_only` | à confirmer |
| Cylindre non nul sans Axe : refusé ; Axe conservé si le Cylindre est absent ou nul (jamais effacé) — l'interface affiche un contrôle visible demandant l'intervention de l'utilisateur | `axis_required` | à confirmer ; aucune borne d'axe, `0` et `180` conservés tels quels, aucune conversion |
| Prisme non nul sans Base : refusé | `base_required` | valeurs, conventions de base, pas et composantes multiples **à vérifier** ; base en texte libre court (aucun schéma figé) |
| provenance exigée pour confirmer (§ 1) | `required` | qualité / catégorie : aucune liste imposée |

Jamais : transposition, normalisation, arrondi, remplissage de l'autre œil, borne clinique chiffrée, conversion d'axe. Forme technique seulement : nombre décimal (`+`, `-`, `−`, une espace, `.` ou `,`) pour Sphère / Cylindre / Axe / Addition / Prisme, 16 caractères au plus ; Base et Acuité texte libre. **Recette locale sur données fictives : ni certification médicale, ni autorisation d'exploitation sur des données de santé réelles.**

## 3. Architecture livrée

| Couche | Fichiers | Rôle |
|---|---|---|
| Domaine | [`src/MMV.Domain/Optical/`](../../../src/MMV.Domain/Optical/) | `EyeValues` / `OpticalValues` (valeurs exactes, `null` = absent), contenus et provenances par type, révision générique (`OpticalRevision<TContent>`), agrégat générique (`OpticalRecord<TRevision, TContent>` : brouillon unique, confirmation numérotée, rectification, abandon, invalidation), `Prescription`, `Measurement` |
| Application | [`src/MMV.Application/Optical/`](../../../src/MMV.Application/Optical/) | validation de forme, règles de confirmation, `OpticalRecordService` (idempotence, version, absence de fausse mise à jour, audit), lecture `OpticalQueryService` |
| Infrastructure | [`src/MMV.Infrastructure/Optical/`](../../../src/MMV.Infrastructure/Optical/) | modèle EF Core (schéma `optical`), stores ; enregistrement : en-tête verrouillé `FOR UPDATE` et version revérifiée **avant** écriture, puis un `SaveChanges` (agrégat + audit) dans une transaction ; unicité → conflit ou réémission ; migration `20261027134615_OpticalModule` |
| API | [`src/MMV.Api/Optical/`](../../../src/MMV.Api/Optical/), [`Auth/AuthSetup.cs`](../../../src/MMV.Api/Auth/AuthSetup.cs), [`Audit/AuditEndpoints.cs`](../../../src/MMV.Api/Audit/AuditEndpoints.cs) | 17 routes `/api/optical` ([`API_OPTICAL.md`](API_OPTICAL.md)), 4 policies cumulatives, `Cache-Control: no-store`, corps stricts, erreurs bornées, journaux à identifiants seulement ; historique client filtré (`optical` exclu sans `optical.read`) |
| Poste | [`apps/desktop/src/api/optical.ts`](../../../apps/desktop/src/api/optical.ts), [`src/optical/OpticalHost.tsx`](../../../apps/desktop/src/optical/OpticalHost.tsx), [`OpticalRoute.tsx`](../../../apps/desktop/src/optical/OpticalRoute.tsx), [`opticalActivation.ts`](../../../apps/desktop/src/optical/opticalActivation.ts) | passerelle HTTP réelle (contrat strict, `null` ↔ `""`, idempotence), hôte réel, activation contrôlée (§ 5) ; écrans 04-A réutilisés |

### Idempotence et concurrence

- Chaque écriture porte un `requestId`. **Réémission** (même identifiant) : création → enregistrement existant renvoyé (200) ; autre écriture → état courant renvoyé si c'est la dernière requête appliquée (`last_request_id`) — **rien n'est réécrit**, aucune seconde révision confirmée. Identifiant réutilisé pour un autre client / auteur → 409 `request_id_reused`.
- Poste : réponse incertaine (réseau, délai, 5xx) rejouée **une fois** automatiquement avec le même identifiant ; tant que l'issue reste incertaine, une nouvelle tentative identique le réutilise ; une création incertaine reste rattachée au client et au type (la tentative suivante retrouve l'enregistrement et y applique la saisie courante) ; en cas d'issue incertaine sur un enregistrement existant, l'état réel est **relu** avant de proposer une nouvelle tentative.
- Concurrence : `xmin` de l'en-tête (toute écriture le touche), verrou `FOR UPDATE` + revérification avant écriture, index uniques partiels (brouillon unique, numéros), déclencheurs d'immuabilité ; 409 `version_conflict` / `revision_conflict` / `draft_exists`, **sans écriture ni événement**.
- Contenu inchangé et aucune confirmation : aucune écriture, version, auteur ni événement.

## 4. Résultats (27/10/2026)

| Contrôle | Résultat |
|---|---|
| `dotnet build MMV.slnx` (avertissements = erreurs) | **PASS** — 0 avertissement, 0 erreur |
| `MMV.Api.Tests` | **PASS 452 / 452** (351 avant 04-B ; + règles, service, HTTP, contrat OpenAPI, filtre d'historique) |
| `MMV.Maintenance.Tests` | **PASS 50 / 50** |
| `MMV.PostgreSql.Tests` (réels) | **NOT_RUN** — exigent 0006 appliquée (G04-SQL) ; compilés ; tests `OpticalWrite` **NOT_TESTED** tant que le schéma `optical` est absent ; `…AllMigrationsApplied` échouera par construction avant application |
| Frontend `npm run check` (tsc ×3), `eslint .`, `vitest run`, `vite build` | **PASS** — 300 / 300 ; lint : 1 avertissement préexistant (`ClientsTable.tsx`, documenté) ; build OK |
| `scripts/tests/Invoke-MmvSqlScript.Tests.ps1` (psql simulé) | **PASS 16 / 16** (+ délai de verrou) |
| Reproductibilité SQL, modèle EF | **PASS** — scripts régénérés octet pour octet identiques ; aucun changement de modèle en attente |
| Pré-essais sur cluster PostgreSQL 18.6 **jetable** (hors bases du propriétaire) | **PASS** — direct, idempotent (×2), inventaire exact 0 différence, contrôle de schéma, HTTP 27 / 27 ; détails [`G04_SQL_DOSSIER.md`](G04_SQL_DOSSIER.md) § 4 — **pas une preuve d'application** |
| Matrice HTTP HTTPS réelle sur `mmv_v2_test` | **NOT_TESTED** (étape B) |
| Parcours Tauri natif | **NOT_TESTED** (étape B) |
| Captures navigateur du banc | **NOT_RERUN** (banc inchangé visuellement hormis : Prisme sans ±, champs libres qualité / catégorie, contrôle « axe sans cylindre ») |

Cas obligatoires du mandat § 4 couverts sans base réelle (doubles et pré-essai) : deux types, chaque œil, OD seul / OG seul, absent vs `0,00`, acuité seule selon le type, sphère zéro seule, cylindre / axe, prisme / base, provenance confirmée vs brouillon, révisions immuables, abandon, invalidation, client archivé, droits par compte, `audit.read` sans `optical.read`, conflits concurrents, double requête / délai / relecture, aucun événement après échec, aucune fuite dans les journaux. **À rejouer sur PostgreSQL réel après G04-SQL.**

## 5. Raccordement 04-C préparé (activation contrôlée)

- Interrupteur de build `VITE_MMV_OPTICAL_MODULE=enabled` **absent par défaut** (aucun `.env` ne le contient) ; activation effective seulement si la session porte aussi `clients.read` et `optical.read`. Sinon : onglet « Ordonnances — phase ultérieure » et menu `later` inchangés (test `realAppIsolation.test.tsx`).
- Activé : menu « Dossiers optiques » (`/optique`), onglet « Dossier optique » de la fiche, écrans 04-A sur la passerelle HTTP réelle ; droits affichés d'après la session, **décidés par le serveur** (403 affiché tel quel) ; historique masqué sans `audit.read` (`historyAvailable`).
- Banc 04-A conservé, isolé, données fictives ; son double implémente aussi l'enregistrement atomique.
- Écrans : aucune refonte ; ajustements imposés par le mandat seulement — Prisme sans ± (pas non arrêté), qualité / catégorie en texte libre, contrôle visible « axe sans cylindre », messages d'échec d'« Enregistrer » (rien n'est enregistré / issue incertaine), panneau d'historique selon les droits.

## 6. Limites et points ouverts (détail : [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md), O-04-38 à O-04-46)

- Création confirmée réémise avec une saisie **modifiée** après une issue incertaine : l'enregistrement déjà confirmé est renvoyé tel qu'enregistré (pas de doublon) ; l'utilisateur voit les valeurs réellement enregistrées sur le détail et rectifie si nécessaire (O-04-40).
- Listes : projection légère puis tri / pagination en mémoire du service (volume à mesurer en pilote, O-04-41).
- Déclencheurs inefficaces contre le rôle propriétaire (O-B04 : rôle SQL applicatif distinct, prérequis pilote).
- Clôture commerciale / pilote **distincte** : conformité Maroc / Belgique (G04-LEGAL), rôle SQL applicatif, sauvegarde **restaurée**, tests multi-postes.

## 7. Étape suivante

**STOP — revue du SQL exact par le propriétaire (G04-SQL).** Après GO explicite : étape B du mandat ([`G04_SQL_DOSSIER.md`](G04_SQL_DOSSIER.md) § 6), puis proposition éventuelle de `PHASE04_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED` seulement si l'intégration réelle fonctionne et les contrôles sont attestés.
