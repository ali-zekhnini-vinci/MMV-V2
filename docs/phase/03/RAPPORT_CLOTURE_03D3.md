# 03-D3 — Rapport de clôture : migration 0005, traçabilité, vérifications réelles

**Statut (19/10/2026) : `PHASE03_D3 — OWNER_FINAL_REVIEW_REQUIRED`.** Mandat : [`prompts/PHASE03_D3_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_D3_GO_CLAUDE_CODE.md) (GO conditionnel après audit indépendant de l'archive `b0f7abc`). Branche locale `feat/phase-03-clients-rdv` ; commits locaux seulement ; aucun push, PR ni merge ; Phase 04 non commencée. `PHASE03_COMPLETE` **non déclaré** : la décision finale appartient au propriétaire.

## 1. Migration `0005_AuditJournal`

| Contrôle (avant toute écriture) | Résultat | Preuve |
|---|---|---|
| SHA-256 complet du script autorisé | `1b42d82c85d21d18b58f465f681258641dbd2116d073ae58bbdf69cc8d41daee` = empreinte imposée | Q-03-235 |
| Correspondance avec la migration EF Core | `dotnet ef migrations script` (hors `src/`) : direct `12f98d1f…`, idempotent `1b42d82c…`, retour arrière `19f3f10b…` **octet pour octet identiques** aux fichiers du dépôt (fins de ligne LF, sans BOM) | Q-03-236 |
| PostgreSQL 18, port, rôles, bases | 18.6 (`180006`), 5433, `mmv_v2_dev`/`mmv_v2_dev` et `mmv_v2_test`/`mmv_v2_test`, rôles non superutilisateurs, sans `CREATEDB` | Q-03-237 |
| Migrations 0001 à 0004 présentes, 0005 absente | 4 migrations sur chaque base ; aucun objet `audit` ; contrainte de permissions à 7 valeurs | Q-03-237 |
| Absence de dérive du schéma | empreintes colonnes / contraintes / index **identiques** sur les deux bases et à la référence 03-B3 après 0004 (`de5378e8` / `2a9dbfd8` / `f9c2aaa0`) ; aucune fonction ni déclencheur | Q-03-237 |
| Sauvegarde vérifiée de `mmv_v2_dev` | `pg_dump` 18.6 (format custom) **hors dépôt** : `C:\Users\Ali Zekhnini\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0005_20261019T125255Z.dump` (26 249 octets, SHA-256 `95C602A80FCC9FAD98F87AF0843B8C6AAD188B3BEE5818363396509CEC183EBF`) ; TOC lisible (54 entrées), archive intégralement décodée, lignes **identiques table par table** (8 tables) | Q-03-238 |
| Processus du propriétaire | MMV.Api (`dotnet run --launch-profile https`, 7267, `mmv_v2_dev`), `tauri dev` / Vite (1420) et `mmv-desktop` ouverts : **laissés en marche** ; une connexion inactive (hors transaction) sur `mmv_v2_dev` ; application avec `lock_timeout` 10 s pour ne jamais bloquer son API | Q-03-234 |

Application séparée par `scripts/Invoke-MmvSqlScript.ps1` : `mmv_v2_dev` (12:53Z) puis `mmv_v2_test` — `PRECHECK OK`, 11 `DO`, `COMMIT`, exit 0, historique EF à 5 migrations. Après migration (lecture seule) : schéma `audit`, `audit.events` (11 colonnes), 4 `CHECK`, 2 clés étrangères `RESTRICT`, 3 index dont `ix_events_client_occurred`, fonction `audit.reject_event_mutation()` (PL/pgSQL, sans `SECURITY DEFINER`), 2 déclencheurs actifs, contrainte de permissions élargie à `audit.read` ; données existantes inchangées (`mmv_v2_dev` : aucun `audit.read` attribué) ; empreintes dev = test (`0f88f510` / `df095d7a` / `8e8f1ffb` / déclencheurs `94d7f30c` / fonctions `b3e45b0c`). Aucune anomalie, aucun retour arrière, PostgreSQL 17 non contacté. Preuves : `evidence/P03D3_0005_*.log`.

## 2. Correction de traçabilité (O-03-44)

`ClientService.UpdateAsync` : quand la saisie normalisée est identique au client courant, **rien n'est écrit** — ni `UpdatedAtUtc`, ni `UpdatedByUserId`, ni nouvelle version `xmin`, ni événement d'audit — et le client courant est renvoyé (HTTP 200, même version, même date). L'ordre des refus est inchangé (introuvable, archivé, version périmée, validation). Examen des autres opérations : **même défaut** dans `AppointmentService.UpdateAsync` (corps identique → auteur et date de dernière modification réécrits), corrigé de la même façon (transaction abandonnée, état courant renvoyé) ; archivage, réactivation, annulation et rétablissement sont des transitions d'état déjà gardées (`AlreadyArchived`, `AlreadyActive`, `AppointmentCancelled`, `AppointmentNotCancelled`) ; les créations écrivent toujours. Aucune migration modifiée. Tests : unitaires (client, rendez-vous avant et après le début), PostgreSQL en transaction annulée (`ctid`, date, journal inchangés), matrices HTTP à données validées (`xmin` relu inchangé), parcours natif (« Modifications enregistrées », version et date inchangées, aucun événement).

## 3. Vérifications réelles après 0005

| Suite | Résultat |
|---|---|
| `PostMigration` (dev et test) | **2/2** |
| `AuditWrite` (0005 réelle, plus simulée) | **6/6** |
| `ClientsWrite` | **7/7** |
| `AppointmentsWrite` (dont deux connexions et COMMIT réel) | **13/13** |
| `IdentityWrite` | **5/5** |
| PostgreSQL 18 réel, suite complète | **47/47**, résidu 0 (1re exécution : 4 échecs de nettoyage, voir O-03-42) |
| .NET `MMV.Api.Tests` / `MMV.Maintenance.Tests` | **351/351** / **50/50**, build 0 avertissement |
| Frontend : types, lint, Vitest, build | PASS ; lint 1 avertissement préexistant (O-03-02) ; **226/226** |
| Build release Tauri (`npx tauri build --no-bundle`) | **PASS** (1 min 31 s) |

## 4. Parcours natif de l'historique

Chaîne **Tauri (WebView2) → HTTPS → MMV.Api → PostgreSQL 18 → `audit.events`** sur `mmv_v2_test`, comptes temporaires `d3n-*` (accueil avec `audit.read`, autre poste **sans** `audit.read`, deux responsables) et clients fictifs créés par l'interface. Banc **isolé** à côté des processus du propriétaire : API de test sur 7367 (build hors `src/`), exécutable de test distinct (identifiant `com.mmv.v2.d3test`, profil WebView2 propre, origine API et CSP 7367, cible cargo hors dépôt, renommé `mmv-d3test.exe`). Scénario `historique-03d3` ([`apps/desktop/e2e/run-native.mjs`](../../../apps/desktop/e2e/run-native.mjs)) : création, modification, enregistrement sans changement, modification par l'autre poste, conflit 409, archivage, réactivation, rendez-vous, conflit horaire refusé, glisser-déposer, changement de responsable, annulation, rétablissement ; chronologie de 10 actions (du plus récent au plus ancien), auteurs réels (dont « D3N Autre poste »), champs cités, valeurs personnelles absentes, « Voir le rendez-vous », filtres type et période, vérité serveur relue ; compte sans `audit.read` : onglet absent et 403 serveur ; aucun événement après le 409 ni après le conflit horaire. **3 × 81 contrôles PASS** : clair 1920×1080 / 100 %, sombre 1440×900 / 125 %, clair 1440×900 / 150 %. Nettoyage : 3 suffixes (dont 2 passes interrompues), **résidu 0**, déclencheurs réactivés, fichier d'identifiants supprimé.

## 5. Défauts trouvés par les preuves réelles et corrigés

| ID | Défaut | Correction |
|---|---|---|
| O-03-42 | Après 0005, les tests à données validées ne pouvaient plus supprimer leurs comptes et clients fictifs (clés `RESTRICT`, journal en ajout seul) ; 1re exécution : utilitaire de purge en erreur (`42725`), données fictives restées dans `mmv_v2_test` | `CommittedAuditCleanup` (tests) : purge des seuls événements fictifs dans une transaction, déclencheur désactivé puis réactivé et contrôlé, `mmv_v2_test` seulement ; résidus purgés et recomptés (0) |
| O-03-43 | Notice « Modifications enregistrées » jamais affichée après la modification d'un client (régression du correctif O-03-15, 03-B : la navigation de React Router 8 est menée en transition) | `ClientsModule.tsx` : effacement au seul changement d'écran effectif ; test Vitest de non-régression |
| O-03-44 | Modification sans changement réel : auteur, date et `xmin` réécrits (clients, rendez-vous) | § 2 |
| O-03-45 | Création d'un rendez-vous affichée « Responsable : Non attribué → … » dans l'historique | `AuditEndpoints.cs` : aucun libellé « avant » pour une création ; test HTTP |

## 6. Politique d'audit (D-03-19 à D-03-23)

Retenus par le GO 03-D3 : schéma `audit` centralisé (D-03-19), écritures atomiques dans la transaction métier (D-03-19), permission `audit.read` séparée (D-03-21), minimisation (D-03-20). **Limite explicite** : les déclencheurs d'ajout seul protègent contre une erreur applicative, **pas** contre un superutilisateur PostgreSQL ni contre le rôle propriétaire de la table — aujourd'hui le rôle de connexion de MMV.Api (`mmv_v2_dev`) est aussi le rôle de migration et peut désactiver un déclencheur (démontré sur `mmv_v2_test` par O-03-42). À prévoir : rôle SQL applicatif distinct du rôle de migration (sans droit de propriétaire sur `audit`, O-B04) et politique de rétention contrôlée. **D-03-22 reste ouverte** : durées de conservation, obligations RGPD / APD (Belgique), loi 09-08 / CNDP (Maroc). D-03-23 : 0005 appliquée (§ 1). `audit.read` attribuée à **aucun** compte réel. Détail : [`AUDIT_JOURNAL_03D.md`](AUDIT_JOURNAL_03D.md) § 6 et § 9.

## 7. Incident O-03-41

`apps/desktop/desktop.zip` (endommagé, SHA-256 constaté `2f88b0ce…f8a1`, 18/10/2026 14:52) et `apps/desktop/desktop.recovered.zip` (`b89034a3…c932`) **conservés, non modifiés** ; aucun remplacement, suppression ni écrasement ; aucune commande de normalisation textuelle sur les archives, images ou fichiers non suivis (fins de ligne retouchées uniquement sur des fichiers suivis nommés, Python `newline=""`). O-03-41 maintenu au registre.

## 8. Restes à décider ou à faire par le propriétaire

- Revue finale et décision `PHASE03_COMPLETE` (D4) ; D3 (correctif O-03-16) toujours ouverte.
- D-03-22 (durées, conseil juridique) ; rôle SQL applicatif distinct ; politique de rétention.
- Attribution éventuelle d'`audit.read` à un compte réel (`MMV.Maintenance permissions grant <identifiant> audit.read`, confirmation interactive).
- Sort de `desktop.zip` / `desktop.recovered.zip`.
- Sauvegarde `mmv_v2_dev` avant 0005 : conservée hors dépôt ; restauration non testée (aucune base de restauration créée : rôle sans `CREATEDB`, PostgreSQL 17 et superutilisateur non utilisés).
- Limites : scénarios natifs antérieurs (`parcours-03c`, `matrice-03c`, `rdv-b3*`) `NOT_RERUN` ; garde-fou de fuseau Casablanca non rejoué.

Matrice détaillée : [`QA_MATRIX.md`](QA_MATRIX.md) § 03-D3 (Q-03-234 à Q-03-258).
