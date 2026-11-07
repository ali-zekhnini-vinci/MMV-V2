# Livraison CSV-IMPORT-AI-01 — G05-SQL-0009 appliquée, correctif `check_stale`, recette GATE D (07/11/2026)

Mandat : [`prompts/MMV_V2_GO_CONDITIONNEL_G05_SQL_0009_GATE_D_2026-11-07.md`](../../../prompts/MMV_V2_GO_CONDITIONNEL_G05_SQL_0009_GATE_D_2026-11-07.md) (GO conditionnel du propriétaire). Rapport précédent : [`LIVRAISON_05_CSVAI_POST_AUDIT.md`](LIVRAISON_05_CSVAI_POST_AUDIT.md). Preuves : [`evidence/csvai/gate_d/`](evidence/csvai/gate_d/).

**Verdict : `GATE_D — READY_FOR_OWNER_FINAL_REVIEW`.** Tous les contrôles obligatoires réalisables sur ce poste sont PASS. Restent **NOT_TESTED**, et ne sont pas présentés comme validés : le fonctionnement sur **deux machines distinctes** (deux fenêtres sur un même poste ont été testées), des **catalogues fournisseurs réels anonymisés** (non fournis, aucun inventé), la touche Échap sur la modale de confirmation en natif (couverte par Vitest seulement) et la non-régression **native** des modules antérieurs (couverte par les suites automatisées). Branche `spike/csv-import-ollama-benchmark`, HEAD `55f021d`, **aucun commit, push, PR ni merge**. Phase 06 non commencée.

## 1. GATE 0 — précontrôles (07/11/2026, avant toute écriture)

| Contrôle | Constat | Statut |
| --- | --- | --- |
| Branche, HEAD, index | `spike/csv-import-ollama-benchmark`, `55f021d`, index vide ; `apps/desktop/.env.development` intact (`a215e1f0…`), fichiers non suivis du propriétaire préservés | PASS |
| Manifestes | `sha256sum -c sql/0009_MANIFEST.sha256` 7/7 OK ; `sql/0009_POST_AUDIT_MANIFEST.sha256` 2/2 OK ; script idempotent `30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0` | PASS |
| Script | uniquement additif : 5 `CREATE TABLE`, 13 index, 4 fonctions, 11 déclencheurs, 1 `INSERT` dans `__EFMigrationsHistory` ; aucun `DROP` / `DELETE` / `TRUNCATE` / `ALTER` d'objet existant | PASS |
| Instance et bases | PostgreSQL **18.6**, port **5433**, cluster `7696807267651395228` ; `mmv_v2_dev` et `mmv_v2_test` contrôlées séparément (session forcée en lecture seule) : 8 migrations 0001→0008, **0009 absente**, 0 table de 0009 | PASS |
| Dérive de schéma | empreinte `0009_restore_check.sql` comparée à la prévalidation du 06/11 : **aucune dérive de structure** ; seules différences : 1 session de connexion sur `mmv_v2_dev`, 3 séquences avancées sur `mmv_v2_test` (tests du 06/11) | PASS |
| Écrivains | API du propriétaire (PID 4808, port 7267) et poste `tauri dev` (PID 43444, Vite 1420) **en marche** → arrêt demandé au propriétaire (non arrêtés par l'agent) ; après son intervention : 0 processus, 0 connexion sur 5433, **0 autre session** sur chaque base | PASS (après intervention du propriétaire) |

## 2. Sauvegardes définitives (07/11/2026 00:10, heure locale ; 06/11 23:10:05 UTC)

Script [`backup_avant_0009.ps1`](evidence/csvai/gate_d/backup_avant_0009.ps1), journal [`SAUVEGARDES_avant_0009_20261106_231005.log`](evidence/csvai/gate_d/SAUVEGARDES_avant_0009_20261106_231005.log). Fichiers rangés hors du dépôt dans `%USERPROFILE%\Documents\MMV-V2-sauvegardes\` ; sauvegardes de prévalidation du 06/11 **conservées**.

| Base | Fichier | SHA-256 | `pg_dump` | `pg_restore --list` | Empreinte de cohérence | Restauration sur base distincte |
| --- | --- | --- | --- | --- | --- | --- |
| `mmv_v2_dev` | `mmv_v2_dev_avant_0009_20261106_231005.dump` (256 270 o) | `1d2470783a56cc9941e4f2edee9694d3c8f3f57893f2dc33844600459040cf7f` | exit 0 | exit 0, 454 entrées, 38 `TABLE DATA` | `.check.txt` `1a8f6f89…61dda7` | base **neuve** du cluster jetable (55434), `--single-transaction --exit-on-error` exit 0 : **IDENTIQUE** (38 tables, 56 déclencheurs tous actifs, 911 lignes) |
| `mmv_v2_test` | `mmv_v2_test_avant_0009_20261106_231005.dump` (219 587 o) | `bc7859744615293b26c2c6a8922e052bf448e2ed1bef73eca93492681300cbee` | exit 0 | exit 0, 454 entrées, 38 `TABLE DATA` | `.check.txt` `7c6986f3…c8583f` | **IDENTIQUE** (idem) |

Chaque base relue avant et après la sauvegarde : identité, 8 migrations, 0 autre session, état inchangé. Copies restaurées et rôles miroirs supprimés ensuite du cluster jetable. Aucun secret dans les journaux (mots de passe lus dans les user-secrets, transmis au seul `psql`).

## 3. Application de 0009

Outil officiel `scripts/Invoke-MmvSqlScript.ps1`, `-LockTimeout 5s`, empreinte imposée revérifiée avant chaque base.

| Étape | `mmv_v2_dev` (23:10:21 UTC) | `mmv_v2_test` (23:10:59 UTC, après DEV conforme) |
| --- | --- | --- |
| Application | `START TRANSACTION … COMMIT`, exit 0 ([journal](evidence/csvai/gate_d/APPLICATION_0009_dev.log)) | idem ([journal](evidence/csvai/gate_d/APPLICATION_0009_test.log)) |
| `0009_CsvImportAssistant.verify.sql` | **14/14 OK**, 0 ECART ([sortie](evidence/csvai/gate_d/VERIFY_0009_dev.txt)) | **14/14 OK** ([sortie](evidence/csvai/gate_d/VERIFY_0009_test.txt)) |
| Migrations | 9 (0001 → `20261106154105_CsvImportAssistant`) | 9 |
| Données et structures antérieures | **toutes conservées** : la seule ligne d'empreinte disparue est celle de `__EFMigrationsHistory` (8 → 9 lignes) ; ajoutés : 6 tables (5 + historique), 77 contraintes, 18 index, 4 fonctions, 11 déclencheurs, 6 propriétaires, 1 séquence | idem ; objets ajoutés **identiques** à ceux de DEV |

Empreintes avant / après : [`EMPREINTES_avant_apres_0009.txt`](evidence/csvai/gate_d/EMPREINTES_avant_apres_0009.txt). Aucun retour arrière effectué ni nécessaire ; la procédure de [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md) § 6.2 reste disponible sur décision distincte du propriétaire. Note : `0008_CatalogCorrections.verify.sql` rejoué sur DEV rend 3 ECART **attendus** (9 migrations au lieu de 8, tables `imports` de 0009, lignes écrites depuis 0008) — ce n'est pas un contrôle de 0009.

## 4. Correctif — protection contre les plans périmés (`check_stale`)

**Défaut confirmé** (audit indépendant) : `SameAsCheckAsync` ne comparait que (ligne, issue). Une ligne restée « mise à jour » dont le prix ou une caractéristique avait changé entre le contrôle et l'application était appliquée, **écrasant** la valeur de l'autre poste ; et entre la relecture transactionnelle et l'écriture, une écriture concurrente pouvait encore s'intercaler (le tarif n'était verrouillé que dans l'action d'écriture). Aucune migration créée (mécanismes existants : `jsonb` de synthèse, verrous de ligne, `xmin`).

| Mesure | Fichier |
| --- | --- |
| **Empreinte du plan métier** (SHA-256) enregistrée avec la synthèse du contrôle (`check_summary`, clé interne `planFingerprint`, **jamais servie au client**) et recalculée dans la transaction d'application. Articles : pour chaque ligne, issue, cible et valeurs écrites (montants compris) + **base** = état actuel de chaque champ **fourni** par le fichier pour les articles existants (libellé, références, code-barres, attributs, rattachement fournisseur, tarif du magasin : achat, vente, coefficient, mode, devise). Offres de verres : issue, identité, offre rapprochée, champs modifiés, revalidation + état actuel des caractéristiques **fournies**, statut « fiche complète », valeurs écrites ; références à créer. Écart → `409 check_stale` (`catalog_changed`). Contrôle antérieur au correctif (sans empreinte) → `409 check_stale` (`check_outdated`). | `AssistedImportService.cs`, `ImportService.Assisted.cs` (`BasisAsync`), `AssistedLensPlanner.cs` (`Basis`) |
| **Modification sans conséquence** : un champ que le fichier ne fournit pas n'entre pas dans l'empreinte (la mise à jour relit et conserve sa valeur) → pas de nouvelle validation. | idem |
| **Verrous avant la relecture** : variantes visées `FOR NO KEY UPDATE` puis leurs tarifs du magasin `FOR UPDATE` (ordre croissant des identifiants, comme les autres services) ; offres visées `FOR NO KEY UPDATE` (`ILensOfferStore.LockAsync`) ; puis plan **relu** sur l'état verrouillé. | `ImportService.Assisted.cs` (`LockAssistedTargetsAsync`), `LensOfferService.cs`, `CorrectionStores.cs` |
| Filet : conflit de version d'une offre ou `xmin` d'une ligne écrite (`VersionConflict`) → `409 check_stale` au lieu d'un refus générique. | `AssistedImportService.cs` |

Tests (PostgreSQL réel, `Phase05AssistedImportTests`) :

1. `Products_GateD_PriceChangedAfterCheckOrDuringApply_CheckStale_NeverOverwritten` — prix d'achat modifié par un autre acteur **après** le contrôle (API de tarif) → 409, prix conservé ; prix modifié **pendant** l'application par une transaction en cours (l'application attend son verrou, puis relit) → 409, prix conservé, lot non appliqué ; modification sans conséquence (référence fabricant absente du fichier) → import accepté, valeur conservée.
2. `Lenses_GateD_CharacteristicChangedAfterCheckOrDuringApply_CheckStale_UnrelatedChangeKept` — SPH d'une offre modifiée après le contrôle → 409 ; prix modifié pendant l'application → 409 ; note modifiée (non fournie) → import accepté, note conservée, fiche repassée « à revalider ».

**Contre-épreuves** ([journal](evidence/csvai/gate_d/CONTRE_EPREUVES_check_stale_20261107.log)) : protection neutralisée temporairement (fichier restauré, SHA-256 vérifié) → les deux tests **échouent** (application HTTP 200 qui écrase la valeur de l'autre poste) ; empreinte conservée mais verrous retirés → le cas « pendant l'application » **écrase** le prix d'un article (les offres restent protégées par `xmin`). Le correctif est donc démontré, cas par cas.

## 5. Défauts supplémentaires trouvés et corrigés pendant la recette

| Réf. | Constat | Correction | Preuve |
| --- | --- | --- | --- |
| GD-01 | **Effacement d'une valeur existante quand une colonne est absente** (contraire à la décision du mandat § 5) : la mise à jour d'un article existant remplaçait sa **référence fabricant**, son **code-barres** et ses **traitements** par « vide » si le fichier ne les fournissait pas (parcours actuel et nouveau parcours, plan commun). Révélé par le test GATE D n° 1 (étape 3). | `ImportService.cs` : champ non fourni → valeur de la fiche conservée (le libellé est toujours fourni : exigé par le validateur). | test n° 1 étape 3 ; suites complètes |
| GD-02 | **Annulation d'une analyse IA impossible depuis le poste Tauri** : la politique CORS du poste (`desktop-api`) n'admet jamais `DELETE` (décision de la phase 02) ; l'appel d'annulation en `DELETE` était refusé par WebView2 avant le serveur (invisible aux tests : client simulé, `HttpClient` sans CORS). Reproduit nativement (« running → done »), serveur vérifié correct en direct. | Route `POST /api/imports/{id}/assistant/analysis/cancel` (même traitement ; `DELETE` conservé pour les clients hors navigateur, politique CORS **inchangée**) ; client limité à GET / POST (type). | `DesktopCorsTests.Preflight_AssistantAnalysisCancel_AllowsPost_NeverDelete` ; `assistantApi.cors.test.ts` ; natif « running → cancelled » |
| GD-03 | **Contraste** mesuré nativement (WebView2) : textes partagés `.clients-field__hint` / `.clients-muted` (jeton global `--text-tertiary`) 3,03:1 sur la carte et 2,75:1 sur la zone de dépôt ; étape courante `--accent` sur `--accent-soft` 4,21:1 (clair), `--accent-hover` 4,15:1 (sombre) — le post-audit n'avait mesuré que sur la carte. | `importWizard.css` (portée `.iw-screen` / `.iw-steps` uniquement) : `--text-secondary` ; étape courante `--accent-pressed` (clair), `--text-primary` (sombre). Jeton global **inchangé** (décision du propriétaire). | `importWizard.contrast.test.ts` (+2) ; natif 5,29:1 (clair), 8,29:1 (sombre) |

## 6. Décisions métier (provisoires, mandat § 5 — pas des validations de production)

Conservées et vérifiées en recette : identité d'offre = fournisseur, gamme, type, indice, matériau, traitements, approvisionnement ; géométrie = caractéristique ; deux géométries incompatibles de même identité = conflit (`lens_offer_conflict`), jamais fusionnées ; capacité modifiée d'une fiche complète → revalidation ; devise absente → confirmation explicite de la devise du magasin, jamais EUR supposé, aucune conversion ; aucune valeur effacée par une colonne absente (renforcé par GD-01) ; aucune purge automatique des originaux ; ancien import CSV conservé (écran servi quand `WizardEnabled=false`, valeur par défaut). ADR-CSVAI-09 reste `PROPOSED`.

## 7. Activation contrôlée et recette GATE D

Activation **uniquement** sur l'API de banc (variable d'environnement du processus `CatalogInterpreter__WizardEnabled=true`, [`GD_bench_start-api.ps1`](evidence/csvai/gate_d/GD_bench_start-api.ps1)) ; `appsettings.json` (défaut `false`) et `.env.development` du propriétaire **inchangés** ; aucune activation globale. Modèle `qwen3.5:4b` ; le serveur confirme le modèle réellement exécuté (`model.active` lu dans la réponse d'Ollama, empreinte `d8b0f5e9760c…`, Ollama 0.40.14).

Chaîne testée : **Tauri (WebView2) → HTTPS (Kestrel, https://localhost:7367) → MMV.Api (build final) → PostgreSQL 18.6 `mmv_v2_test` (0009) → Ollama `qwen3.5:4b`**. Scénario [`apps/desktop/e2e/native-csvai.mjs`](../../../apps/desktop/e2e/native-csvai.mjs), **58/58 contrôles PASS** en une exécution finale ([journal](evidence/csvai/gate_d/GD_native_run_20261107.log), 26 captures `evidence/csvai/gate_d/captures/`). Données et comptes fictifs ([`GD_harness_seed.cs.txt`](evidence/csvai/gate_d/GD_harness_seed.cs.txt)).

| Scénario | Résultat | Détail |
| --- | --- | --- |
| R1 dépôt tolérant | PASS | Windows-1252, SKU en double accepté au dépôt, aperçu du texte d'origine, destination suggérée (verres) |
| R2 analyse IA réelle et progression | PASS | 17–18 s par fichier, activité horodatée, modèle **confirmé** affiché ; annulation depuis le poste → `cancelled` (après GD-02), puis choix explicite |
| R3 corrections individuelles et groupées | PASS | décisions append-only (révisions) ; poste B sur révision périmée → refus, sans écriture ; SKU en double corrigé et revalidé |
| R4 montants et devise | PASS | aucune présélection, exemples du serveur, aucune conversion ; EUR **confirmé** (`file_default`), `costs.write` |
| R5 contrôle, import, rejeu, `check_stale` | PASS | 3 variantes créées (relues du serveur) ; réimport : « Aucune donnée nouvelle », import verrouillé, application forcée par l'API refusée `409 already_applied`, toujours 3 variantes ; prix modifié par un autre poste après le contrôle → « Le contrôle est périmé : relancez-le. », prix conservé, puis nouveau contrôle et import |
| R6 offres de verres | PASS | circuits stocké (mis à jour) et sur commande (créé) distincts ; fiche complète → « à revalider » ; note saisie à la main conservée |
| R7 historique repliable | PASS | replié par défaut (liste non chargée), ouvert au clavier, lot importé en lecture seule |
| R8 clavier, contraste, thèmes | PASS | Tab jusqu'au dépôt, focus visible ; 1366×768 et 1920×1080, clair et sombre, sans défilement horizontal ; contraste minimal 5,29:1 / 8,29:1 (après GD-03) |
| R9 pannes | PASS | API coupée : indicateur temps réel déconnecté ; rétablie : reconnexion ; Ollama injoignable (simulé par une adresse injoignable, Ollama du propriétaire non arrêté) : `unavailable`, aucun repli silencieux, « Vérifier sans IA » tracé |
| Doublons de SKU | PASS | dépôt accepté, diagnostic, correction individuelle |
| Deux postes | PASS (même poste) / **NOT_TESTED (deux machines)** | deux fenêtres Tauri à profils distincts sur un seul Windows : ne prouve pas le fonctionnement sur deux machines |
| Catalogues fournisseurs réels anonymisés | **NOT_TESTED** | non fournis ; aucun inventé |
| Échap sur la modale de confirmation | NOT_TESTED en natif | couvert par `importWizard.test.tsx` (jsdom) |
| Non-régression des modules précédents | PASS (automatisée) / NOT_TESTED (natif) | suites complètes ci-dessous ; aucun scénario natif des autres modules rejoué |
| Résidus de test | PASS | `mmv_v2_test` purgée ([`GD_purge-test.ps1`](evidence/csvai/gate_d/GD_purge-test.ps1)) : empreinte identique à l'état post-0009 hors 7 séquences ; aucun compte ni donnée de banc. `mmv_v2_dev` : identique à l'état post-0009 hors **une** session `auth.sessions` créée à 00:26 (heure locale) par le propriétaire, qui a relancé son API et `tauri dev` à 00:24 |

## 8. Suites (rejouées après la dernière modification du code)

Synthèse : [`PG_suite_mmv_v2_test_20261107.txt`](evidence/csvai/gate_d/PG_suite_mmv_v2_test_20261107.txt).

| Suite | Résultat |
| --- | --- |
| `dotnet build MMV.slnx` | 0 avertissement, 0 erreur |
| MMV.Api.Tests | **670/670** (+1 : CORS de l'annulation) |
| MMV.Maintenance.Tests | 50/50 |
| MMV.PostgreSql.Tests sur `mmv_v2_test` (0009), Ollama réel | **107 PASS** + 1 NotExecuted (HTTPS en processus réel, rejoué ci-dessous) ; `TestDatabase_AllMigrationsApplied` et `DevDatabase_AllMigrationsApplied` (**BLOCKED** au post-audit) **PASS** ; inventaire `Phase05Schemas_MatchTheReviewedInventory` PASS ; Ollama réel 2 min 12 PASS ; 2 tests GATE D PASS |
| Processus HTTPS réel (API de banc, build final) | 8/8 (`Phase05RealHttpsTests` + 7 scénarios `Phase05PostgreSqlTests` par le processus Kestrel) |
| Front | `tsc` OK ; Vitest **341/341** (32 fichiers) ; ESLint 0 erreur, 1 avertissement préexistant (`ClientsTable.tsx`) |
| Natif | 58/58 (§ 7) |

## 9. Fichiers modifiés par ce mandat

Code : `src/MMV.Application/Catalog/Assisted/AssistedImportService.cs`, `src/MMV.Application/Catalog/Assisted/AssistedLensPlanner.cs`, `src/MMV.Application/Catalog/ImportService.Assisted.cs`, `src/MMV.Application/Catalog/ImportService.cs`, `src/MMV.Application/Catalog/LensOfferService.cs`, `src/MMV.Infrastructure/Catalog/CorrectionStores.cs`, `src/MMV.Api/Catalog/AssistedImportEndpoints.cs`, `apps/desktop/src/catalog/importWizard/{ImportWizard.tsx, importWizard.css, assistantApi.ts}`. Tests : `tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests.cs`, `tests/MMV.Api.Tests/DesktopCorsTests.cs`, `apps/desktop/src/catalog/importWizard/importWizard.contrast.test.ts`, `apps/desktop/src/catalog/importWizard/assistantApi.cors.test.ts` (nouveau), `apps/desktop/e2e/native-csvai.mjs` (nouveau). Documents : ce rapport, [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md), [`../05.md`](../05.md), [`SOURCE_MAP.md`](SOURCE_MAP.md), [`IMPLEMENTATION_SPEC.md`](IMPLEMENTATION_SPEC.md), [`QA_MATRIX.md`](QA_MATRIX.md), [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md), [`../../ADR_ET_DECISIONS.md`](../../ADR_ET_DECISIONS.md), [`../../PERIMETRE_ET_SEQUENCE.md`](../../PERIMETRE_ET_SEQUENCE.md), `README.md`, mandat dans `prompts/`. Preuves : `docs/phase/05/evidence/csvai/gate_d/`. Aucune migration nouvelle ; scripts 0009 **inchangés**.

## 10. Risques résiduels et observations

- **Libellé de la devise** : « Les montants sont en EUR … pour les N lignes » ne compte que les lignes déjà valides (les lignes en doublon sont exclues du nombre), alors que la confirmation vaut pour tout le fichier ; les montants écrits sont corrects (3/3 en EUR). Libellé à revoir (non bloquant, aucune écriture incorrecte).
- **Création concurrente d'une offre de même identité** après la relecture transactionnelle : aucune contrainte d'unicité sur l'identité d'une offre (plusieurs offres de même identité saisies à la main restent permises, 0008) ; fenêtre étroite (après les verrous) ; un nouveau contrôle la détecterait. Une garde d'unicité serait une évolution de schéma (hors mandat).
- **Jeton global `--text-tertiary`** (3,03:1 sur blanc) employé hors de l'assistant (clients, autres écrans) : corrigé seulement sur l'écran de l'assistant ; décision du propriétaire pour le reste de l'application.
- Règle post-audit `:root[data-theme="dark"] .cat-step--current .iw-step__sub` (mesurée sur la carte) supplantée par la règle GATE D (conservée pour l'historique).
- Le bandeau de bas de page (« Serveur MMV joignable ») n'est rafraîchi qu'à son contrôle périodique : pendant la coupure, c'est l'indicateur temps réel qui signale la déconnexion (capture 21).
- Le build de banc des postes remplace temporairement `apps/desktop/src-tauri/target/release/mmv-desktop.exe` ; l'exécutable d'origine a été restauré après chaque build (SHA-256 vérifié).

## 11. Décisions attendues du propriétaire

Revue finale de GATE D ; tests sur deux machines Windows distinctes et avec des catalogues fournisseurs réels anonymisés ; activation de l'assistant sur son installation (`CatalogInterpreter:WizardEnabled`, aujourd'hui `false` par défaut) ; ADR-CSVAI-09 ; retrait éventuel de l'ancien import ; jeton `--text-tertiary` ; libellé de la devise ; sort des sauvegardes (prévalidation et définitives) ; Q-CSVAI-07 ; autorisation de commit.
