# 04-C — Livraison : migration 0006 appliquée, intégration réelle PostgreSQL, HTTPS et Tauri natif

**Statut (28/10/2026) : `PHASE04_C_COMPLETE — OWNER_FINAL_REVIEW_REQUIRED`.** Mandat : [`prompts/MMV_V2_G04_SQL_GO_PHASE04_C_2026-10-28.md`](../../../prompts/MMV_V2_G04_SQL_GO_PHASE04_C_2026-10-28.md) — **G04-SQL : GO conditionnel** pour l'application locale du seul script `docs/phase/04/sql/0006_OpticalModule.idempotent.sql` (SHA-256 `927ed7ccb975b15eed18f6b2bcee41faf08ecb23c1c4f9f1f80a2efa1706c013`). Branche locale `feat/phase-04-00-cadrage` (commits locaux ; aucun push, PR ni merge). **La clôture globale de la Phase 04 reste soumise à l'audit final du propriétaire.** Aucun module supplémentaire commencé. Module non activé pour des données médicales réelles : tous les essais sur `mmv_v2_test`, comptes et clients **fictifs**, purgés (résidu 0).

## 1. Précontrôles (tous conformes avant toute écriture)

Preuve : [`evidence/P04C_preflight_20261028.log`](evidence/P04C_preflight_20261028.log).

| Précontrôle | Résultat |
|---|---|
| Empreinte du script | `927ed7cc…706c013` exacte ; manifeste [`sql/0006_MANIFEST.sha256`](sql/0006_MANIFEST.sha256) 12 / 12 OK |
| Serveur | PostgreSQL **18.6** sur **5433** (relu côté serveur : `server_version_num` 180006) ; PostgreSQL 17 (5432) **jamais contacté** |
| Bases et rôles | `mmv_v2_dev` / `mmv_v2_dev` et `mmv_v2_test` / `mmv_v2_test` (non superutilisateurs) |
| Historique | 0001 à 0005 présentes, 0006 absente, sur les deux bases |
| Dérive de schéma | `ContainsOnlyAKnownState` PASS sur les deux bases (0 objet inattendu) ; `AllMigrationsApplied` en échec **attendu par construction** avant application |
| Scripts ↔ migration EF Core | idempotent et direct **régénérés octet pour octet identiques** ; `has-pending-model-changes` : aucun changement ; `verify.sql` couvert par le manifeste |
| Processus du propriétaire | aucun `MMV.Api`, `tauri dev` ni banc actif ; aucune session sur les deux bases : fenêtre de maintenance libre |
| Sauvegarde de `mmv_v2_dev` | `pg_dump -Fc` (18.6) hors dépôt : `C:\Users\Ali Zekhnini\Documents\MMV-V2-sauvegardes\mmv_v2_dev_avant_0006_20261028_224659.dump`, 31 800 octets, SHA-256 `5FF611B387C422DCF52E8580CD0D8BD1B8E8CE27E00599C4C2152B57828E1066` ; `pg_restore --list` lisible (66 entrées, 9 tables avec données, aucun objet `optical`), inventaire à côté du fichier. **Restauration complète non éprouvée** (aucune base indépendante autorisée) : réserve maintenue |

## 2. Application (script approuvé seulement, `Invoke-MmvSqlScript.ps1 -LockTimeout 5s`)

| Base | Horodatage | Résultat | Contrôles |
|---|---|---|---|
| `mmv_v2_dev` | 28/10/2026 22:47:28 (+01:00) | `PRECHECK OK` (18.6, 5433), `LOCK_TIMEOUT : 5s`, `COMMIT`, `APPLIQUE : exit 0`, 6 migrations dont `20261027134615_OpticalModule` — [`evidence/P04C_0006_apply_mmv_v2_dev_20261028T2147Z.log`](evidence/P04C_0006_apply_mmv_v2_dev_20261028T2147Z.log) | `verify.sql` **18 / 18 OK** ; inventaire exact `OpticalModuleDifferences` = 0 ; comptages des tables existantes inchangés |
| `mmv_v2_test` (après contrôle conforme de dev) | 28/10/2026 22:48:28 (+01:00) | idem — [`evidence/P04C_0006_apply_mmv_v2_test_20261028T2148Z.log`](evidence/P04C_0006_apply_mmv_v2_test_20261028T2148Z.log) | `verify.sql` **18 / 18 OK** (tables, 31 index dont 4 uniques partiels, 46 CHECK, 19 FK `RESTRICT`, 20 colonnes générées, 8 déclencheurs, 2 fonctions, contrainte des permissions : 8 droits conservés + 4 optiques, aucun droit attribué) ; inventaire exact (propriétaires, droits) dans la suite PostgreSQL |

Contrôles : [`evidence/P04C_0006_verify_20261028.log`](evidence/P04C_0006_verify_20261028.log), [`evidence/P04C_counts_20261028.log`](evidence/P04C_counts_20261028.log). Aucun script alternatif, aucune modification du SQL, aucun retour arrière. Aucun droit `optical.*` attribué à un compte réel.

*Constat :* après l'application, le propriétaire a démarré son `MMV.Api` (7267, 22:59) et `tauri dev` (1420, 22:58) ; `mmv_v2_dev` compte ensuite +2 sessions et +2 événements `clients` (archivage puis réactivation d'un client, 23:14). Ces écritures ne viennent pas de l'agent, qui n'a rien écrit sur `mmv_v2_dev` hors du script 0006 ; processus du propriétaire laissés en marche.

## 3. Résultats (arbre livré)

| Contrôle | Résultat |
|---|---|
| `dotnet build MMV.slnx` (avertissements = erreurs) | **PASS** — 0 avertissement, 0 erreur |
| `MMV.Api.Tests` | **PASS 452 / 452** (dont `citedUsers` avec mesureur désactivé) |
| `MMV.Maintenance.Tests` | **PASS 50 / 50** |
| `MMV.PostgreSql.Tests` (PostgreSQL 18.6 réel) | **PASS 52 / 52** — dont `AllMigrationsApplied` ×2 et les 5 tests `OpticalWrite` (transaction annulée, concurrence réelle à deux connexions) ; résidu `mmv_v2_test` = 0 — [`evidence/P04C_postgresql_20261028.log`](evidence/P04C_postgresql_20261028.log) |
| Frontend (`npm run check`, `eslint .`, `vitest run`, `vite build`) | **PASS** — 301 / 301 ; lint : 1 avertissement préexistant (`ClientsTable.tsx`) ; build OK — [`evidence/P04C_frontend_20261028.log`](evidence/P04C_frontend_20261028.log) |
| `scripts/tests/Invoke-MmvSqlScript.Tests.ps1` | **PASS 16 / 16** |
| Matrice HTTPS réelle (`mmv_v2_test`) | **PASS 69 / 69** — [`evidence/P04C_http_matrix_20261028.log`](evidence/P04C_http_matrix_20261028.log), outil [`evidence/P04C_harness_c04http.cs.txt`](evidence/P04C_harness_c04http.cs.txt) |
| HTTP en clair | **PASS 6 / 6** — refus `403 https_required` de toute route `/api` (connexion, lectures, écriture), **rien écrit** ; même jeton accepté en HTTPS (O-04-55) |
| Parcours Tauri natif (WebView2) | **PASS — 123 contrôles**, 12 captures — [`evidence/P04C_native_optique-04c_20261028.log`](evidence/P04C_native_optique-04c_20261028.log) |
| Journal de l'API de banc (tous les passages) | 0 valeur optique, prescripteur, note, catégorie ni nom de client ; 0 erreur non traitée |

## 4. Matrice du mandat § 3

| Cas | HTTPS réel | Tauri natif | Statut |
|---|---|---|---|
| Ordonnance : création + confirmation atomiques | 1.1–1.9 (révision 1, aucun brouillon ; refus 422 = rien écrit ; valeurs exactes en base : texte + `numeric`) | étape 1 (valeurs à l'écran = valeurs en base) | **PASS** |
| Mesure magasin : création + confirmation | 2.1–2.6 | étape 4 | **PASS** |
| Brouillon : enregistrement et reprise | 3.1–3.5 (reprise sur un autre poste ; brouillon inchangé → aucune écriture) | étape 4 (brouillon de mesure repris puis confirmé) | **PASS** |
| Modification, révisions confirmées conservées | 4.1–4.8 (révision 1 intacte en base, abandon, révision dépassée → 409) | étapes 2–3 (révision 2, comparaison révision 1 / 2) | **PASS** |
| Invalidation motivée | 6.6–6.11 (motif exigé, aucune suppression, figé, exclu des listes par défaut) | étape 5 (motif exigé à l'écran) | **PASS** |
| Quatre permissions optiques | 6.1–6.13 (chaque droit isolé ; `clients.read` exigé ; `system.admin` seul et `audit.read` seul → 403) | étapes 6–7 | **PASS** |
| Anciennes révisions avec `audit.read` | 4.5, 4.9–4.11 | étape 3 (historique) | **PASS** |
| Refus sans permission, aucune fuite | 4.6, 4.12, 6.13–6.15, 11.1–11.2 | étape 6 (révision 1 absente de l'écran), étape 7 (onglet reporté) | **PASS** |
| Deux postes, même enregistrement | 7.1–7.5 (un 200 + un 409, aucun mélange, révisions 1,2,3 sans trou) | étape 2 (bandeau « Modifié depuis un autre poste », rien écrit, « Recharger » reprend le brouillon réel) | **PASS** |
| Réémission après réponse incertaine | 8.1–8.5 (même `requestId` → même enregistrement ; requête annulée côté poste puis réémise → 1 ligne ; réémission tardive → 409, rien écrit) | — | **PASS** (HTTP) |
| Archivage du client ‖ écriture optique | 9.1–9.4 (10 paires : jamais de 500 ; après archivage 409 `client_archived` ; consultation conservée) | — | **PASS** avec constat O-04-53 |
| Désactivation d'un collaborateur ‖ confirmation de mesure | 10.1–10.5 (10 paires décalées : 1 × 422 `not_eligible`, 9 × 201 ; ensuite 422 ; nom toujours servi ; rectification avec le même collaborateur admise) | — | **PASS** avec constat O-04-54 |
| Cohérence interface ↔ API réelle | — | chaque étape relue par l'API (révision, valeurs, version, cycle, auteur) | **PASS** |
| OD / OG et boutons ± dans Tauri natif | — | vraie souris (`Input.dispatchMouseEvent`) : −2,25 / −0,50 / +2,00 / +1,50 depuis le vide ; saisie clavier `−0,75` conservée ; champ non touché = `null` ; thème sombre 1024 × 700 zoom 125 % | **PASS** |

Aucun cas `FAIL` ni `NOT_TESTED` dans le périmètre du mandat. `NOT_TESTED` hors mandat : restauration de sauvegarde, rôle SQL applicatif distinct, multi-postes physiques (deux postes simulés par deux sessions), captures du banc 04-A (`NOT_RERUN`, banc inchangé en 04-C).

## 5. Défauts identifiés et corrigés

| Point | Défaut | Correction | Preuve |
|---|---|---|---|
| **O-04-48** (résout O-04-44) | nom du collaborateur ayant mesuré résolu **uniquement** par `/api/optical/measurers` (comptes actifs habilités) : après désactivation, une mesure confirmée affichait « Non renseigné » (provenance exigée présentée comme absente), la comparaison des révisions une valeur vide, et le formulaire de rectification perdait la sélection | réponse d'enregistrement : champ **additif** `citedUsers` (`id`, `displayName` des comptes cités dans la provenance des révisions servies, même inactifs) ; poste : `knownActors` complète la liste active (détail, comparaison, dialogue) ; formulaire : collaborateur cité conservé, signalé « (n'est plus habilité à mesurer) » ; contrat OpenAPI mis à jour (ajout seul) | `OpticalEndpointsTests.Measurement_AndInvalidation_FullHttpFlow`, `optical.test.ts`, HTTPS 10.4 |
| **O-04-50** | deux tests PostgreSQL restés sur l'inventaire 0005 (`SchemaStateTests.AllMigrationsApplied`, `AuditPostgreSqlTests.AuditJournalScript_ProducesExactlyTheExpectedSchema`) : **FAIL sur une base correctement migrée** | assertion sur `OpticalModuleDifferences` (0001 à 0006) ; le script 0005 reste vérifié sans effet sur une base 0006. Schéma inchangé | PostgreSQL 52 / 52 |

## 6. Constats (sans correction, détail [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md))

- **O-04-49** : course de double création réémise (même `requestId`, simultanée) → réponse correcte (201 + 200, un enregistrement) mais EF Core journalise l'unicité 23505 (`ux_*_creation_request_id`) au niveau `fail` (paramètres et détail masqués, aucune valeur).
- **O-04-51** : `vitest` lancé en parallèle des tests .NET : 4 délais de 5 s dépassés (écrans sans lien avec l'optique) ; seul : 300 / 300, puis 301 / 301.
- **O-04-52** : texte du formulaire « Boutons ± : pas de démonstration 0,25 (paramétrable, D-04-06 ouverte) » alors que le mandat 04-B fixe le pas 0,25 pour Sphère / Cylindre / Addition — libellé à réviser par le propriétaire (non modifié : texte produit).
- **O-04-53** : archivage concurrent : une écriture commencée avant l'archivage peut être validée après (9 révisions horodatées après `archived_at_utc` sur 10 paires) ; ordre sérialisable « écriture puis archivage », aucune écriture après archivage constaté par l'écriture ; **réserve maintenue** (verrou du client à envisager).
- **O-04-54** : désactivation concurrente : une confirmation dont le contrôle d'éligibilité précède la désactivation est acceptée (ordre sérialisable) ; **réserve maintenue**.
- **O-04-55** : l'API refuse tout `/api` en HTTP clair (403 `https_required`, conception phase 03) : la matrice complète est en HTTPS ; HTTP = contrôle de refus.
- Règle « deux sphères à zéro sans autre valeur » : **acceptée** (201) sur la base réelle, comme décrit en O-04-38 — validation opticien ouverte.

## 7. Réserves produit conservées au backlog (non résolues)

Validation professionnelle des règles optiques et des cas limites (dont deux sphères à zéro) ; composantes prismatiques, bases et conventions ; compte PostgreSQL applicatif distinct du compte de migration (O-B04) ; restauration complète d'une sauvegarde sur base indépendante ; confidentialité et conformité Maroc / Belgique (G04-LEGAL) ; robustesse des réémissions tardives et de l'archivage concurrent (O-04-40, O-04-53, O-04-54). Inscrites dans [`docs/PERIMETRE_ET_SEQUENCE.md`](../../PERIMETRE_ET_SEQUENCE.md) (BL-04-02 à BL-04-07).

## 8. Bancs (reproductibles, hors dépôt sauf l'outil versionné en preuve)

- API de banc : build du dépôt copié hors dépôt, `https://localhost:7367` / `http://localhost:5367`, `ConnectionStrings__Mmv` = `mmv_v2_test` passée au seul processus enfant, `Auth__LoginAttemptsPerMinute=120` (banc seulement ; défaut 10 inchangé).
- Natif : `npx tauri build --debug --no-bundle --config <banc>/tauri.c04test.json` (identifiant `com.mmv.v2.c04test`, CSP `connect-src` 7367) avec `VITE_MMV_API_BASE_URL=https://localhost:7367` et `VITE_MMV_OPTICAL_MODULE=enabled` **au build de banc seulement**, `CARGO_TARGET_DIR` hors dépôt ; exécutable renommé `mmv-c04test.exe`, `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9223` ; scénario versionné `apps/desktop/e2e/run-native.mjs optique-04c`.
- Processus de banc arrêtés en fin de session ; processus du propriétaire jamais touchés.

## 9. Étape suivante

**STOP — audit final du propriétaire.** `PHASE04_COMPLETE` n'est pas déclaré : la clôture globale de la Phase 04 (G04-FINAL) reste soumise à cet audit ; G04-LEGAL reste **OPEN** (aucune donnée médicale réelle).
