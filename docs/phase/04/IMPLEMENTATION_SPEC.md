# 04 — IMPLEMENTATION_SPEC (cadrage 04-00, aucune implémentation)

**État : `PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`** (19/10/2026) ; *suite du 19/10/2026 :* **`PHASE04_04A_PREPARED — OWNER_GO_REQUIRED`** (§ 8) ; *04-A, 19/10/2026 :* **`PHASE04_A_UI_READY — OWNER_VISUAL_REVIEW_REQUIRED`** (§ 9, prototype sur banc isolé) ; *20/10/2026 :* **`PHASE04_A_UX_LOT3_READY — OWNER_VISUAL_REVIEW_REQUIRED`** (§ 10, refonte UX des formulaires, D-04-15) ; *20/10/2026 (simplification) :* **`PHASE04_A_SIMPLIFICATION_READY — OWNER_VISUAL_REVIEW_REQUIRED`** (§ 11, D-04-16) ; *20/10/2026 (clôture 04-A) :* **`PHASE04_A_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`** (§ 12, D-04-17 ; pas un GO pour 04-B). Aucune ligne de `src/` ni d'`apps/desktop/src/` modifiée en 04-00 ; aucun SQL ; aucune base touchée. Ce document fixe **comment** la Phase 04 sera construite une fois les décisions prises ; le **quoi** métier reste soumis à [`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md).

## 1. Base de départ vérifiée

- `origin/main` = `6924860ee0fb77678cdd959a1de9b39735785fcc` (merge de la PR #4, `feat/phase-03-clients-rdv`, 19/10/2026 16:23 +02:00) ; arbre identique à `abd0352` ; `93df475` inclus. Branche de travail `feat/phase-04-00-cadrage` créée depuis ce commit.
- Modules en place : `auth`, `clients`, `appointments`, `audit` ; migrations 0001–0005 ; permissions `clients.*`, `appointments.*`, `audit.read`.
- Fiche client : onglets Informations / Rendez-vous / Historique, et un onglet « Ordonnances » **reporté**, visible de tout compte ayant `clients.read`, sans donnée ([`apps/desktop/src/clients/ClientDetailView.tsx`](../../../apps/desktop/src/clients/ClientDetailView.tsx)) ; menu « Ordonnances » `later` ([`apps/desktop/src/shell/navigation.ts`](../../../apps/desktop/src/shell/navigation.ts)).

## 2. Architecture cible (aucun nouveau projet ni service)

| Couche | Emplacement prévu | Contenu |
|---|---|---|
| MMV.Domain | `src/MMV.Domain/Optical/` | `PrescriptionRecord`, `StoreMeasurement`, révisions, états par œil, codes de motif ; constantes de permissions dans `Identity/Permissions.cs` ; codes `AuditActions` du module `optical` |
| MMV.Application | `src/MMV.Application/Optical/` | services `PrescriptionService`, `MeasurementService`, `SourceDocumentService` ; validateurs **après ratification** de D-04-06 ; calcul des groupes modifiés pour l'audit (sans valeurs) |
| MMV.Infrastructure | `src/MMV.Infrastructure/Optical/` | modèle EF (schéma `optical`), stockage des pièces, migration `0006` |
| MMV.Api | `src/MMV.Api/Optical/` | routes `/api/optical/*`, policies, OpenAPI ; filtre des événements optiques dans `AuditEndpoints` |
| Interface | `apps/desktop/src/optical/` | onglet « Dossier optique », liste globale, formulaires RX / MES, détail, révisions, pièces ; `api/optical.ts`, contrat `api/contract/optical.openapi.json` ; double `test/fakeOpticalApi.ts` |

Monolithe modulaire inchangé (ADR-001 `PROPOSED`), PostgreSQL seul (ADR-003), aucune file de messages, aucun stockage cloud. Écriture métier + audit dans la même transaction (D-03-19 retenue).

## 3. Composants d'interface réutilisables (aucun nouveau framework)

| Besoin | Existant réutilisé |
|---|---|
| Onglets de la fiche | `components/ui/tabs.tsx` (Radix Tabs) |
| Confirmations (invalidation, sortie) | `components/ui/alert-dialog.tsx`, `shell/navigationGuard.ts` (`useLeaveGuard`) |
| Menu secondaire « Invalider » | `components/ui/dropdown-menu.tsx` |
| Dates de prescription / réception / mesure | `components/ui/date-picker.tsx` (react-day-picker), `popover.tsx` |
| Sélection du client (liste globale) | `appointments/ClientPicker.tsx` |
| Formulaires et validation | React Hook Form + Zod (`clients/clientForm.ts` comme modèle) |
| Données serveur, listes paginées | TanStack Query, TanStack Table (`clients/ClientsTable.tsx`) |
| États vide / chargement / erreur | `clients/ClientStates.tsx` (`StateBlock`) |
| Notices persistantes | `clients/ClientNoticeBanner.tsx` |
| Historique | `audit/ClientHistoryTab.tsx` (libellés des nouveaux codes) |
| Icônes, jetons, thèmes | Lucide, `styles/tokens.css`, thèmes clair / sombre existants |

À créer **sans dépendance nouvelle** : grille OD/OG (saisie décimale signée, unités visibles, empilement en fenêtre réduite), badges de type / statut, comparaison de révisions, aperçu de pièce (`<img>` / visionneuse PDF native de WebView2 via `blob:`, sous réserve d'O-04-01). Aucune bibliothèque PDF, d'OCR ou d'éditeur n'est prévue. Socle CSS commun aux modules (O-03-19) à extraire au début de 04-A (troisième module). Route optique chargée à la demande pour ne pas alourdir le bundle (O-03-03).

## 4. Sous-lots

| Sous-lot | Préalable | Contenu | Interdits |
|---|---|---|---|
| **04-00** (ce lot) | GO 04-00 | préflight, cartographie, propositions, décisions consolidées | code, SQL, bases |
| **04-A** — interface « source-aware » | D-04-01 (acquise) ; décisions des états concernés ; G04-DESIGN pour les états GAP | onglet, liste, formulaires RX / MES, détail, révisions, pièces, états, clavier, thèmes, zooms — sur **double de test** uniquement, service désactivé | fausse donnée en environnement réel ; module déclaré opérationnel |
| **04-B** — modèle et API | **G04-METIER** (D-04-02 à D-04-09 ratifiées, avis opticien reçu pour D-04-06) | domaine, services, routes, permissions, audit, OpenAPI, migration `0006` **préparée non appliquée** | application du SQL |
| **04-C** — intégration réelle | **G04-SQL** | application de 0006 (`mmv_v2_dev`, `mmv_v2_test`), tests PostgreSQL réels, deux connexions, natif Tauri → HTTPS → API → PG18 | données réelles ; PostgreSQL 17 |
| **04-D** — validation | 04-C | parcours du propriétaire, revue code / SQL / sécurité / UX, GO PR | push sans GO |

## 5. Sécurité — exigences à vérifier par sous-lot

| Exigence | Constat actuel (19/10/2026) | Prévu |
|---|---|---|
| Permissions optiques propres, contrôlées serveur | aucune ; `system.admin` n'implique rien | policies dédiées + combinaisons ([`PERMISSIONS_04_PROPOSED.md`](PERMISSIONS_04_PROPOSED.md)) ; tests 401 / 403 par route |
| Onglet visible sans droit | l'onglet reporté « Ordonnances » est visible de tous | remplacé par un onglet conditionné à `optical.read` |
| Pièces sécurisées, aucun lien public | — | flux authentifié, `no-store`, identifiants non devinables, aucune URL publique ni chemin exposé, type contrôlé par signature |
| Audit atomique des écritures | journal 03-D : même transaction, ajout seul | nouveaux codes, aucun événement en cas d'échec ; `document.viewed` écrit avant l'envoi |
| Aucune valeur médicale dans `audit.events` | liste blanche `AuditFieldPolicy` | codes de gestion seulement ; test de non-présence (Q-04-130) |
| Événements optiques dans l'historique | `audit.read` suffit pour l'historique d'un client | filtre serveur `audit.read` + `optical.read` (D-04-05.c) |
| Journaux techniques | pas de journalisation des corps ; EF `Warning` ; pas de `EnableSensitiveDataLogging` | inchangé ; erreurs 422 sans valeur ; test de capture des journaux (Q-04-140) |
| Cache du poste | cache TanStack Query vidé à la fermeture du Shell (`Shell.tsx`) | idem ; `Cache-Control: no-store` ; aucune donnée optique en `localStorage` |
| CSP du poste | `img-src 'self' data:` ; aucun `blob:` ni cadre | ouverture minimale à décider (O-04-01) |
| Rôle SQL applicatif distinct | absent (O-B04) | **à traiter avant un pilote** (données de santé) |
| Sauvegarde / restauration | restauration jamais testée | à prouver avant un pilote |

## 6. Stratégie de tests (détail : [`QA_MATRIX.md`](QA_MATRIX.md))

Unitaires domaine (après D-04-06), HTTP (matrice des droits, 409, 422), contrat OpenAPI, PostgreSQL réel (transactions, deux connexions, audit atomique, immutabilité des révisions), interface (double de test, clavier, états), natif Tauri (2 thèmes × 2 résolutions × 3 zooms). Données fictives **isolées** sur `mmv_v2_test`, nettoyées, jamais sur `mmv_v2_dev`.

## 7. Impacts anticipés sur le futur module Ventes / prise en charge

1. Une vente référence `(type, id, revisionNo)` d'un enregistrement **confirmé** ; la révision est figée ; une rectification ultérieure crée une révision n+1 sans modifier la vente.
2. Une mesure magasin reste typée comme telle dans la vente ; elle ne satisfait **jamais** une exigence « ordonnance médicale » (planche `MMV-L04-rxBlock`, `MMV-L05-orderRxRequired`).
3. Une invalidation postérieure à la vente est **affichée** sur la vente, sans la modifier (règle à ratifier avec Ventes).
4. La prise en charge (INAMI, mutuelles, AMO, CNSS / CNOPS, tiers payant) sera un domaine distinct, paramétré par pays ; aucune donnée d'éligibilité n'est déduite de l'ordonnance.
5. La fiche de travail atelier (`MMV-L05-wsRx`) lira la révision figée en lecture seule ; la transposition éventuelle ne modifiera jamais la révision.
6. Les écarts pupillaires / hauteurs appartiendront à l'équipement ou à la commande, pas à l'ordonnance.

## 8. Amendement daté — 19/10/2026 (suite de la revue indépendante)

Les §§ 1 à 7 sont conservés. Revue indépendante : G04-00 « cadrage techniquement acceptable ». Compléments :

- **04-A préparé** : périmètre classé IND / PROTO / BLOQUÉ, composition des 14 états complémentaires, grille OD / OG neutre, fichiers, séquence A1–A5, garantie « service non activé » et points bloquants — [`PREPARATION_04A.md`](PREPARATION_04A.md). Le préalable « décisions des états concernés » du § 4 se lit désormais ainsi : les éléments **IND** et **PROTO** peuvent être construits sur double de test dès le GO 04-A (G04-A) ; seuls les éléments **BLOQUÉ** (règles cliniques, D-04-06) attendent l'avis d'un opticien. 04-B reste soumis à G04-METIER.
- **Modèle non figé** : études E-01 à E-05 ([`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md)) — cycle de l'enregistrement séparé des révisions, un seul brouillon ouvert, pièces liées par révision, `document.content_served`, `optical.read` unique, contrôles des fichiers et limites.
- **§ 5 Sécurité, précisions** : l'audit des pièces enregistre une **transmission** (`document.content_served`), jamais une visualisation ; contrôle du type par signature et `Content-Type` détecté ; aucun fichier temporaire serveur ; ouverture de CSP reportée en 04-C (O-04-01) ; commandes de la visionneuse PDF à examiner en natif (O-04-18) ; sauvegardes : taille ≈ volume des pièces, chiffrement, restauration avec empreintes avant pilote (O-04-19).

## 9. Amendement daté — 19/10/2026 (04-A réalisé — UI ONLY)

Les §§ 1 à 8 sont conservés. **G04-A APPROVED — UI ONLY**, O-04-16 APPROVED. Réalisé (aucun SQL, aucune migration, aucune base, aucune route ni permission optique) :

| Couche | Emplacement | Contenu |
|---|---|---|
| Prototype d'interface | `apps/desktop/src/optical/` | contrat provisoire `opticalContract.ts` (types, sans HTTP), `opticalQueries.ts` (TanStack Query sur une passerelle fournie par contexte ; passerelle par défaut = « service non activé »), `opticalAccess.ts` (affichage selon droits simulés), onglet `ClientOpticalTab`, liste `OpticalListView`, choix `OpticalTypeChooser`, formulaire `OpticalRecordForm` + grille neutre `EyeGrid`, détail `OpticalRecordDetail`, `RevisionsPanel`, `SourceDocumentsPanel` *(supprimé le 20/10/2026, D-04-16)*, dialogues, `Optical.css` |
| Banc isolé | `apps/desktop/optical-bench.html`, `apps/desktop/src/bench/` | hôte `OpticalBench` (bannière permanente, profils simulés, latence et pannes simulées, autre poste simulé), double en mémoire `inMemoryOpticalGateway.ts`, jeu fictif `benchSeed.ts`, garde réseau `benchNetwork.ts`, aperçus synthétiques `benchImages.ts` *(supprimé le 20/10/2026, D-04-16)* |
| Code existant modifié | `apps/desktop/src/clients/ClientDetailView.tsx` | props facultatives `opticalTab` / `initialTab` ; sans elles (application réelle), rendu identique |
| Outils | `apps/desktop/e2e/bench-04a/` | serveur Vite isolé (port 1520, cache hors dépôt), plan et prise des captures (Edge sans interface, CDP) |

Réutilisés sans duplication : `ClientDetailView`, `ClientPicker`, `StateBlock` / `LoadingBlock`, `ClientNoticeBanner`, `Highlight`, `ConfirmDialog`, `Tabs`, `DropdownMenu`, `DatePicker`, `useLeaveGuard`, `createMmvQueryClient`, `ForbiddenView`, styles `Clients.css` / `overlays.css`. Aucune dépendance ajoutée (ADR-014). Activation réelle (04-C) : monter `OpticalModule` dans le Shell derrière `optical.read`, fournir une passerelle HTTP générée depuis l'OpenAPI de 04-B, extraire le socle CSS (O-04-11). Rapport : [`LIVRAISON_04A.md`](LIVRAISON_04A.md).

## 10. Amendement daté — 20/10/2026 (refonte UX Lot 3, D-04-15 APPROVED)

Les §§ 1 à 9 sont conservés. **D-04-15 APPROVED** : la maquette Claude Design du Lot 3 est la référence UX des formulaires optiques (contrôles rapides ± et saisie clavier complémentaire). Toujours dans le banc isolé ; aucun SQL, aucune migration, aucune base, aucune route ni permission optique ; aucune dépendance ajoutée (ADR-014).

| Couche | Emplacement | Contenu |
|---|---|---|
| Logique ± (pure, testée) | `apps/desktop/src/optical/opticalStep.ts` | `stepValue` (calcul décimal exact en entiers, aucun arrondi ni alignement, signe et séparateur conservés, `null` pour une valeur non numérique), `isSteppable`, `stepLabel`, `DEMO_STEP` (0,25, démonstration), `OpticalStepContext` (pas paramétrable), `STEPPER_FIELDS` |
| Grille OD / OG partagée | `apps/desktop/src/optical/EyeGrid.tsx` | `EyeGrid` → `EyeCard` (fieldset, légende en en-tête de carte) → `EyeRow` (libellé 118 px) → `StepperInput` (Sphère, Cylindre, Addition, Prisme) / `TextInput` (Axe « degrés », Acuité) / `BaseInput` (saisie libre + `DropdownMenu` de libellés proposés) ; contrôlé par react-hook-form (`Controller`) ; annonce `aria-live` ; boutons ± et « Effacer » hors de l'ordre de Tab, ↑ / ↓ et Alt+↓ au clavier |
| Formulaire | `apps/desktop/src/optical/OpticalRecordForm.tsx` | mise en page de la maquette : en-tête et actions (soumission par l'attribut `form`), `FormSection` en carte ou à nu, client concerné, `ReferenceField`, provenances **distinctes** par type (D-04-01) réutilisant les mêmes composants OD / OG |
| Styles | `apps/desktop/src/optical/Optical.css` | cotes de la maquette en jetons MMV ; requêtes de conteneur (`optical-form`, `optical-eye`) plutôt que de fenêtre : le formulaire s'adapte à la largeur que lui laisse le Shell ou le banc |
| Banc | `apps/desktop/src/bench/OpticalBench.tsx`, `main.tsx` | fournisseur `OpticalStepContext`, commande « Pas des boutons ± (démonstration) », paramètre `step` |

À l'activation (04-C) : le pas, les bornes, le format et les règles croisées viendront de D-04-06 (aucune valeur codée en dur ailleurs que `DEMO_STEP`) ; revérifier l'en-tête collant dans le Shell réel (O-04-28). Rapport : [`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 11.

## 11. Amendement daté — 20/10/2026 (simplification, D-04-16 APPROVED)

Les §§ 1 à 10 sont conservés. **D-04-16 APPROVED** ([`prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_SIMPLIFICATION_GO_CLAUDE_CODE.md)) : « État de l'œil » supprimé, gestion documentaire reportée au backlog permanent BL-04-01, révisions conservées. Effets sur la spécification :

| § | Changement |
|---|---|
| § 2 architecture | `src/MMV.Infrastructure/Optical/` : **aucun stockage de pièces** ; interface : plus de zone « pièces » (`SourceDocumentsPanel.tsx` supprimé) |
| § 3 composants | aperçu de pièce et sélecteur de fichier sans objet en 04 ; grille OD / OG sans sélecteur d'état |
| § 4 sous-lots | 04-B (après G04-METIER, G04-SQL) : en-têtes et révisions des deux types, 4 droits optiques, **aucune** table ni API de pièces ; 04-C : CSP, visionneuse et preuves de fichiers sans objet |
| § 5 sécurité | ligne « Pièces sécurisées, aucun lien public » sans objet en 04 (reprise avec BL-04-01) ; `no-store`, 403 / 404 indiscernables et absence de valeur optique dans l'audit maintenus |

Prototype : contrat `apps/desktop/src/optical/opticalContract.ts` (`EyeValues` sans état, `OpticalGateway` sans opération documentaire) ; double `apps/desktop/src/bench/inMemoryOpticalGateway.ts`. Rapport : [`LIVRAISON_04A.md`](LIVRAISON_04A.md) § 12.

## 12. Amendement daté — 20/10/2026 (enregistrement simplifié, D-04-17 APPROVED — clôture 04-A)

Les §§ 1 à 11 sont conservés. **D-04-17 APPROVED** ([`prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md`](../../../prompts/PHASE04_A_CLOTURE_GO_CLAUDE_CODE.md)) : action principale « Enregistrer », option secondaire discrète « Enregistrer comme brouillon », cycle de révisions conservé. Effets sur la spécification :

| § | Changement |
|---|---|
| § 3 composants | en-tête du formulaire : « Annuler » (secondaire), « Enregistrer comme brouillon » (tertiaire), « Enregistrer » (principal, Ctrl+S, Entrée) ; récapitulatif de confirmation réservé au détail (confirmation d'un brouillon existant) ; bandeau d'échec partiel avec « Laisser en brouillon » / « Recharger » |
| § 4 sous-lots | 04-B : le poste enchaîne `PUT …/draft` puis `POST …/confirm` ; décider à la conception de l'API définitive s'il faut une opération atomique (O-04-34) ; le critère de confirmation reste **serveur** et sera fixé par G04-METIER (la règle du banc n'est pas reprise sans décision, O-04-36) |
| § 6 tests | tests à reprendre en 04-B contre le vrai service : échec de la confirmation après brouillon enregistré (409, 422, communication), doubles déclenchements, absence de doublon à la reprise |

Prototype : `apps/desktop/src/optical/OpticalRecordForm.tsx`, `opticalUi.ts` (`describeConfirmFailure`) ; double `apps/desktop/src/bench/inMemoryOpticalGateway.ts` (règle minimale de démonstration, panne `nextConfirmFault`). Aucun fichier .NET ni SQL modifié ; migration `0006` non générée ; application réelle inchangée.
