# 04 — IMPLEMENTATION_SPEC (cadrage 04-00, aucune implémentation)

**État : `PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED`** (19/10/2026). Aucune ligne de `src/` ni d'`apps/desktop/src/` modifiée en 04-00 ; aucun SQL ; aucune base touchée. Ce document fixe **comment** la Phase 04 sera construite une fois les décisions prises ; le **quoi** métier reste soumis à [`DECISIONS_04_METIER_PROPOSED.md`](DECISIONS_04_METIER_PROPOSED.md).

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
