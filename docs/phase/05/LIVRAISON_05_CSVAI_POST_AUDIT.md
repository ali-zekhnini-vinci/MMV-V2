# Livraison CSV-IMPORT-AI-01 — corrections post-audit, prévalidation G05-SQL-0009, préparation Gate D (06/11/2026)

Mandat : [`prompts/MMV_V2_MANDAT_POST_AUDIT_CSV_IMPORT_AI_01_2026-11-06.md`](../../../prompts/MMV_V2_MANDAT_POST_AUDIT_CSV_IMPORT_AI_01_2026-11-06.md) (archivé tel que reçu). Livraison précédente (inchangée, non réécrite) : [`LIVRAISON_05_CSVAI.md`](LIVRAISON_05_CSVAI.md). Dossier SQL : [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md).

**Verdict : `READY_FOR_G05_SQL_0009`** — ce statut **ne vaut pas autorisation d'application**. 0009 n'est appliquée ni à `mmv_v2_dev` ni à `mmv_v2_test` ; aucune écriture sur les bases du propriétaire ; Gate D **non déclarée**. Branche `spike/csv-import-ollama-benchmark`, HEAD `55f021d` ; **aucun commit, push, PR ni merge** ; Phase 06 non commencée. `apps/desktop/.env.development` intact (`a215e1f0…2e4e`, jamais indexé) ; aucun document du propriétaire modifié hors porte documentaire.

## 1. Correctifs réalisés

| Correctif | Constat | Correction | Preuve |
| --- | --- | --- | --- |
| **COR-CSVAI-01** identité des offres de verres | Contrat réel (COR-05-05, `LensOffer`) : une offre = capacités d'**un** fournisseur pour **une** gamme. Le mode avancé exprime les variations de géométrie par des règles, donc diamètres et plages SPH / CYL / ADD sont des **caractéristiques** de l'offre, pas son identité. La clé d'avant ignorait en revanche le **mode d'approvisionnement** : un circuit « stocké » et un circuit « sur commande » de mêmes type / indice / traitements étaient rapprochés, ce qui donnait une mise à jour du mauvais circuit ou une ambiguïté. | Identité = fournisseur + gamme + type + indice + matériau + traitements + **approvisionnement**. Deux lignes de même identité aux caractéristiques différentes sont **bloquées** (`lens_offer_conflict`, champs cités) : ni fusion, ni double création. Valeurs identiques : `lens_offer_duplicate`. Plusieurs offres existantes de même identité : on prend celle dont la géométrie fournie est identique si elle est unique, sinon `lens_offer_ambiguous_existing`. Comparaisons **sémantiques** (6 = 6,00). | `CsvAssistantPostAuditTests` (fusion, départage), PG `Lenses_PostAudit_…` |
| — mise à jour sans effacement | Une colonne absente (prix, délai, plages, note) **effaçait** la valeur existante. Les règles avancées saisies à la main étaient **remplacées** par celles du fichier. La note était annoncée « inchangée » au contrôle mais effacée à l'application. | Seules les caractéristiques **fournies et différentes** changent. Règles avancées conservées : seule la restriction combinée simple est remplacée, et si elle existe en plusieurs exemplaires, `lens_offer_rules_ambiguous`. Provenance et validité conservées. | idem |
| — défaut préexistant trouvé (lot 0008) | `LensOfferService.ChangedFields` comparait `(prix, devise)` et `(validFrom, validUntil)` en sérialisant des `ValueTuple`, ce qui donne toujours `{}`. Une modification du **seul prix** ou de la **seule validité** répondait `200` sans rien enregistrer, par l'écran comme par l'import. | Comparaison champ par champ. | PG : PUT du seul prix, révision 2 et prix relu |
| **COR-CSVAI-02** fiche complète | La mise à jour reprenait toujours `RulesComplete` de l'offre existante. | Si une **capacité** change (diamètres, plages, règles), une fiche complète repasse **incomplète**. Motif de révision : « capacités modifiées, fiche à revalider ». L'étape 4 l'affiche (`rulesComplete` : « fiche complète » → « fiche à revalider », compteur `offersToRevalidate`). Un changement de prix ou de délai seul conserve le statut. La revalidation reste celle du référentiel (mode avancé, déclaration « fiche complète »). | Théorie ×3 + test prix/délai ; PG (révision 2, `rules_complete = false`) |
| **COR-CSVAI-03** devise | La devise du magasin était appliquée **en silence** aux montants sans devise. `EUR` était utilisé si le magasin ou sa devise étaient introuvables. | Une devise absente du fichier n'est jamais tenue pour fournie. La devise du magasin est **proposée** par une confirmation groupée (`default:currency`, décision `file_default` existante, aucune évolution de schéma), soumise au droit des montants (`costs.write` / `pricing.manual`). Seule la devise du magasin est admise : aucune conversion. Si la devise change après confirmation, une nouvelle confirmation est exigée. Magasin ou devise introuvables : `store_currency_unknown`, montants non importables, **jamais d'EUR par défaut**. Produits et verres. | 5 tests unitaires ; PG produits (refus d'une autre devise, confirmation) et verres ; Vitest « devise introuvable » |
| **COR-CSVAI-04** rejeu | `AppliedAsync` renvoyait 0 offre, 0 référence, 0 tarif au rejeu. Pour les verres, les « produits » et « variantes » étaient en réalité des références et des offres. | Les compteurs réellement écrits sont enregistrés avec la synthèse du contrôle (`check_summary.applied`) **dans la transaction, avant la clôture du lot**. Première réponse et rejeu relisent ce même résultat (compteurs, date, durée). Nouvelle garde : le plan recalculé à l'application doit être **celui du contrôle**, sinon `409 check_stale` (`catalog_changed`) et rien n'est écrit. | PG produits et verres (`AssertSameApplied`), garde `catalog_changed` |
| **COR-CSVAI-05** restauration | Retour arrière proposé : `pg_restore --clean` sur la base **active**. | Commande retirée. Nouvelle procédure : base **neuve** distincte, `--exit-on-error --single-transaction`, propriétaires et droits conservés, empreinte de cohérence comparée à celle relevée à la sauvegarde, **bascule explicite** par renommage, base écartée conservée (`SQL_0009_REVUE.md` § 6.1–6.2). Nouveau script en lecture seule `sql/0009_restore_check.sql`. | Essais 7 et 8 de `SQL_0009_REVUE.md` § 5 |
| Dossier SQL (constat de la suite finale) | L'inventaire de revue 0009 porte les 4 corps de fonctions de garde en **CRLF** (relevé avant la mise en LF des scripts). Après une application conforme du script approuvé (LF), `Phase05Schemas_MatchTheReviewedInventory` aurait échoué sur `mmv_v2_test`. | Script et inventaire de revue **inchangés**. Ajout de l'inventaire **appliqué** `0009_CsvImportAssistant.schema-inventory.applied.txt` (précédent 0007, O-05-13) et d'un test hors connexion qui prouve que seules ces 4 empreintes diffèrent, LF / CRLF du corps du script approuvé. | `Phase05SchemaTests` 3/3 |
| Interface | Contrastes mesurés : `--text-tertiary` à 3,03:1 en clair, `--accent` à 3,62:1 en sombre pour de petits libellés. | Corrigé dans `importWizard.css` seulement (jetons globaux inchangés, design conservé) : `--text-secondary`, et `--accent-hover` (4,94:1) en sombre. | `importWizard.contrast.test.ts` |

## 2. Fichiers modifiés (ce cycle)

| Domaine | Fichiers |
| --- | --- |
| Application | `src/MMV.Application/Catalog/Assisted/AssistedLensPlanner.cs` (identité, fusion, complétude, devise), `src/MMV.Application/Catalog/Assisted/CatalogMaterializer.cs` (devise), `src/MMV.Application/Catalog/Assisted/AssistedImportService.cs` (devise sans EUR par défaut, décision devise, garde de plan, résultat enregistré, rejeu), `src/MMV.Application/Catalog/LensOfferService.cs` (comparaison prix / validité ; aide `Differences` du lot précédent retirée) |
| Domaine | `src/MMV.Domain/CsvImport/ImportAssistant.cs` (`BatchAssistant.RecordApplied`) |
| Poste | `apps/desktop/src/catalog/importWizard/assistantApi.ts`, `ImportWizard.tsx`, `importWizard.css` |
| Tests | `tests/MMV.Api.Tests/Catalog/Assisted/CsvAssistantPostAuditTests.cs` (nouveau), `CsvAssistantServiceTests.cs` ; `tests/MMV.PostgreSql.Tests/Phase05AssistedImportTests.cs` (nouveau test verres, devise, rejeu), `Phase05Harness.cs` (`MMV_PG05_KEEP_SYNTHETIC`, cluster jetable seulement) ; `apps/desktop/src/catalog/importWizard/importWizard.test.tsx`, `importWizard.contrast.test.ts` (nouveau) |
| SQL | `docs/phase/05/sql/0009_restore_check.sql`, `0009_CsvImportAssistant.schema-inventory.applied.txt` et `0009_POST_AUDIT_MANIFEST.sha256` (nouveaux) ; **scripts 0009, inventaire de revue et `0009_MANIFEST.sha256` inchangés** ; `tests/MMV.PostgreSql.Tests/Phase05SchemaTests.cs` (inventaire appliqué, test des fins de ligne) |
| Documents | ce rapport ; `SQL_0009_REVUE.md` ; `QA_MATRIX.md`, `GATES_AND_DECISIONS.md`, `SOURCE_MAP.md`, `IMPLEMENTATION_SPEC.md`, `LIVRAISON_05_CSVAI.md` (note datée), `docs/phase/05.md`, `README.md`, `docs/ADR_ET_DECISIONS.md`, `docs/PERIMETRE_ET_SEQUENCE.md` ; mandat archivé dans `prompts/` |
| Preuves | `docs/phase/05/evidence/csvai/postaudit/` |

Aucune migration, aucun instantané EF, aucun fichier de configuration modifié. Aucun changement de schéma.

## 3. Tests et résultats (06/11/2026, après la dernière modification)

| Suite | Commande | Résultat |
| --- | --- | --- |
| Build .NET | `dotnet build MMV.slnx --artifacts-path bin/csvai -p:NuGetAudit=false` | 0 avertissement, 0 erreur |
| API | `dotnet test tests/MMV.Api.Tests` | **669/669** (+13 : `CsvAssistantPostAuditTests`) |
| Maintenance | `dotnet test tests/MMV.Maintenance.Tests` | **50/50** |
| PostgreSQL (cluster jetable 18.6, 55434) | `MMV_PG05_SCRATCH=… dotnet test tests/MMV.PostgreSql.Tests` | **106 : 103 PASS**, 2 **BLOCKED** (`SchemaStateTests.*_AllMigrationsApplied` : 0009 absente des bases du propriétaire, échec attendu), 1 NOT_TESTED (`Phase05RealHttpsTests`, processus HTTPS non désigné) ; Ollama réel inclus (`MMV_CSVAI_REAL_OLLAMA`) ; `Phase05SchemaTests` 3/3 (inventaire appliqué) |
| — injection, concurrence, verres, rejeu, montants et devises | `Phase05AssistedImportTests` (7) | PASS 7/7 (dont `TechnicalRefusalsOnly_…AndInjectionNeverObeyed`, décisions concurrentes, `Lenses_*` ×2, rejeu identique produits et verres) |
| Ollama réel `qwen3.5:4b` | `MMV_CSVAI_REAL_OLLAMA=http://127.0.0.1:11434 … Phase05AssistedRealOllamaTests` | **PASS** (3 fichiers : 43 s, 36 s, 52 s ; modèle confirmé par la réponse) — [preuve](evidence/csvai/postaudit/OLLAMA_REEL_qwen3.5-4b_postaudit_20261106.txt) |
| Sauvegarde / restauration, données de l'assistant | `restore_trial.sh` (cluster jetable) | **PASS** : IDENTIQUE source / restaurée / basculée ; base basculée 7/7 |
| Sauvegardes réelles (prévalidation) | `prevalidation_owner_dumps.ps1` | **PASS** : `mmv_v2_dev` et `mmv_v2_test` restaurées sur base neuve jetable, IDENTIQUE |
| Scripts SQL et empreintes | `sha256sum -c docs/phase/05/sql/0009_MANIFEST.sha256` ; `… 0009_POST_AUDIT_MANIFEST.sha256` | 7/7 OK ; 2/2 OK — idempotent `30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0` inchangé |
| Vitest | `npx vitest run` | **337/337** (31 fichiers) |
| TypeScript, ESLint, build | `tsc --noEmit` ; `eslint src` ; `vite build` | PASS ; 0 erreur (1 avertissement préexistant `ClientsTable.tsx`) ; PASS |
| Clavier (jsdom, **pas** un test natif) | `importWizard.test.tsx` « clavier seul » | PASS (Tab, Espace, Entrée : choix et confirmation sans souris) |
| Contrastes (calcul WCAG statique des jetons, **pas** une mesure native) | `importWizard.contrast.test.ts` | PASS (clair et sombre, ≥ 4,5:1 pour les textes de l'assistant) |

## 4. Dossier SQL définitif

- Migration 0009 **inchangée** : `0009_CsvImportAssistant.idempotent.sql` `30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0`, `.sql` `61ed4627…`, `verify.sql` `a945b5df…`, inventaire `34748014…`, migration EF `eeab475f…`, `Designer` `cce025f9…`, instantané `a18a7067…` ([`sql/0009_MANIFEST.sha256`](sql/0009_MANIFEST.sha256), 7/7 OK). Les corrections n'exigent **aucun** changement de schéma : la confirmation de devise réutilise le type `file_default` (contrainte `ck_batch_decisions_type` inchangée), et le résultat d'application loge dans `check_summary` (jsonb existant, écrit avant la clôture du lot, conforme à `trg_batch_assistant_guard`).
- Ajout : inventaire appliqué [`sql/0009_CsvImportAssistant.schema-inventory.applied.txt`](sql/0009_CsvImportAssistant.schema-inventory.applied.txt) `1cdb101c3ead22cbdbc7ebe15a27a73f7fb65f13d67cfffa4708ca3040ec4e49` (l'inventaire de revue `34748014…` porte les corps de fonctions en CRLF ; voir § 1).
- Ajout (lecture seule) : [`sql/0009_restore_check.sql`](sql/0009_restore_check.sql) `47197a4a68021aaaf3540089e2e83a9863f96acaabd38c345e3d00e04488ce7a` ([`sql/0009_POST_AUDIT_MANIFEST.sha256`](sql/0009_POST_AUDIT_MANIFEST.sha256)).
- Procédure : [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md) § 6.0 (contrôles préalables), étapes 1–5 (sauvegarde **avec empreinte de cohérence**, application avec empreinte imposée, `verify` 14/14), § 6.1 (options retirées), § 6.2 (restauration sur base neuve, contrôles, bascule).

## 5. Contrôles sur les bases persistantes, en lecture seule (06/11/2026)

Session forcée `default_transaction_read_only=on`, mot de passe lu dans les user-secrets et jamais affiché : [`evidence/csvai/postaudit/BASES_PROPRIETAIRE_lecture_seule_20261106.txt`](evidence/csvai/postaudit/BASES_PROPRIETAIRE_lecture_seule_20261106.txt).

| Contrôle | `mmv_v2_dev` | `mmv_v2_test` |
| --- | --- | --- |
| PostgreSQL 18.x, port 5433, identité | 18.6, 5433, base et rôle `mmv_v2_dev`, cluster `7696807267651395228` | 18.6, 5433, `mmv_v2_test`, même cluster |
| Migrations 0001–0008 présentes, 0009 absente | 8 migrations, 0009 absente, 0 table de 0009 | idem |
| Dérive de schéma | aucune : 821 lignes de structure identiques à une référence 0001 → 0008 bâtie sur le jetable ; droits identiques | aucune |
| Écriture concurrente | 2 sessions **inactives** (API du propriétaire vraisemblablement), aucune écriture en 5 s | aucune session, aucune écriture |
| Sauvegarde de prévalidation (pas la définitive) | `effd978b…26f7`, lisible (454 entrées), restaurée : IDENTIQUE | `574c5082…06d6`, lisible, restaurée : IDENTIQUE |

À refaire au moment du GO : 0 session (API et postes arrêtés par le propriétaire), puis sauvegardes définitives séparées avec leur empreinte de cohérence (étape 1).

## 6. Tests encore bloqués ou non exécutés

| Test | Statut | Raison |
| --- | --- | --- |
| `SchemaStateTests.DevDatabase_AllMigrationsApplied`, `TestDatabase_AllMigrationsApplied` | **BLOCKED** (échec attendu) | 0009 absente des bases du propriétaire (GO SQL requis) |
| Suite de l'assistant sur `mmv_v2_test` | NOT_TESTED | 0009 non appliquée |
| `Phase05RealHttpsTests.RealProcess_…` | NOT_TESTED | processus MMV.Api HTTPS réel non désigné (`MMV_PG05_API_BASE`) |
| Recette réelle Tauri → HTTPS → MMV.Api → PostgreSQL 18 → Ollama `qwen3.5:4b`, deux postes | NOT_TESTED | exige 0009 sur une base servie et l'écran activé (scénarios prêts, § 9) |
| Clavier et contrastes **natifs** (Tauri, WebView2) | NOT_TESTED | seuls les tests jsdom et le calcul des jetons ont été faits ; aucune capture de navigateur ne les remplace |
| Catalogues réels anonymisés, mesure sur le matériel des magasins | NOT_TESTED | Gate D |

## 7. Risques résiduels

- **Écriture concurrente** sur `mmv_v2_dev` : l'API du propriétaire y reste connectée (2 sessions inactives). L'application exige son arrêt préalable.
- **Retour arrière** : les écritures postérieures à la sauvegarde sont perdues (procédure § 6.2, décision du propriétaire). La base écartée est conservée.
- **Comparaison des contraintes** : elle est **normalisée**, car `pg_dump` / `pg_restore` réécrit les CHECK `IN (...)`. Les regroupements logiques ne sont pas comparés ; la définition exacte se relit au besoin. Conséquence : sur une base restaurée, l'inventaire textuel de `Phase05SchemaTests` diffère. Constaté ici sur la base jetable basculée, qui a été reconstruite par les scripts versionnés avant la suite finale.
- **Encodage** : un script accentué appliqué par `psql` sans `PGCLIENTENCODING=UTF8` s'enregistre mal encodé (constaté sur une base de référence jetable). 0009 est entièrement ASCII. `Invoke-MmvSqlScript.ps1` ne fixe pas l'encodage du client : à durcir avant un futur script accentué.
- **Offres de verres** : deux lignes de même identité et de géométries différentes restent **bloquées**. Elles se saisissent comme règles avancées dans le référentiel ; le fichier ne peut pas les créer seul (choix conservateur).
- **Devise d'un fichier différente de celle du magasin** : elle est refusée pour les tarifs magasin (`currency_mismatch`). Pour une offre de verres, une devise **explicite** du fichier est enregistrée telle quelle, sans conversion (comportement du lot précédent).
- **Garde « plan = contrôle »** : si le catalogue change entre le contrôle et l'application, l'utilisateur relance la vérification (`check_stale`). Comportement voulu, dont l'effet sur l'ergonomie est à observer en recette.
- Natif non prouvé pour ce cycle (§ 6).

## 8. Décisions exigées du propriétaire

1. **GO / NO-GO SQL 0009** : script inchangé (`30dc6e0a…a2e7e0`), dossier révisé à revoir (inventaire appliqué ajouté, procédure de sauvegarde et de restauration révisée, § 6.0–6.2 ; manifeste des ajouts `0009_POST_AUDIT_MANIFEST.sha256`). Ce verdict n'est pas un GO.
2. Arrêt de l'API et des postes au moment de l'application, puis sauvegardes définitives (`mmv_v2_dev`, `mmv_v2_test`).
3. Contrat des offres (COR-CSVAI-01) : accepter l'identité « fournisseur + gamme + type + indice + matériau + traitements + approvisionnement », avec géométrie = caractéristique (ADR-CSVAI-09, `PROPOSED`).
4. Activation de l'écran (`CatalogInterpreter:WizardEnabled`) après 0009, puis recette Gate D (§ 9).
5. Questions restées ouvertes du lot précédent : Q-CSVAI-07 (rétention), retrait de l'ancien parcours, revue visuelle, autorisation de commit. La question « devise des prix d'offres sans colonne devise » est traitée par COR-CSVAI-03 (devise du magasin confirmée par fichier) ; elle reste à approuver.
6. Sort des sauvegardes de prévalidation (`*_prevalidation_0009_20261106_223601.dump`, rangées avec les sauvegardes 0007 et 0008).

## 9. Scénarios de recette réelle (Gate D, après GO SQL et activation)

Chaîne : poste Tauri (×2, profils distincts) → HTTPS (`https://localhost:7267`) → MMV.Api → PostgreSQL 18 `mmv_v2_test` (0009 appliquée) → Ollama local `qwen3.5:4b`. Données fictives ; aucun téléchargement de modèle.

| # | Scénario | Attendu |
| --- | --- | --- |
| R1 | Dépôt d'un catalogue de montures (F01) | inspection sans écriture ; destination suggérée ; original conservé (empreinte) |
| R2 | Analyse | activité réelle horodatée ; modèle **confirmé par la réponse** (`qwen3.5:4b`) ; durée mesurée par le serveur ; lignes vues sur total ; annulation possible |
| R3 | Corrections | décisions append-only, revalidation complète ; poste B voit la révision (aucune perte d'état ; conflit de révision affiché) |
| R4 | Montants | colonne non qualifiée : aucune présélection, exemples du serveur, montants lisibles et non interprétables distingués ; **devise du magasin à confirmer** ; droits `costs.write` / `pricing.manual` |
| R5 | Contrôle puis import | compteurs, avant / après, confirmation explicite en modale, transaction unique ; **rejeu = même résultat** ; catalogue modifié entre-temps : `check_stale` |
| R6 | Verres (F06 / F17) | offres de deux fournisseurs ; circuits stocké / sur commande distincts ; conflit de géométrie bloqué ; fiche complète repassée « à revalider » ; testeur « à vérifier » |
| R7 | Historique | replié par défaut ; reprise d'un lot ; lecture seule d'un lot importé |
| R8 | Clavier et contrastes natifs | parcours complet au clavier (Tab, flèches, Espace, Entrée, Échap) ; clair et sombre à 1366×768 et 1920×1080 ; zoom 125 % |
| R9 | Pannes | Ollama arrêté : « vérifier sans IA » proposé ; API coupée puis rétablie : reconnexion |

## 10. Archive d'audit

`MMV-V2-CSVAI-POST-AUDIT.zip` est produite par `git archive` d'un arbre temporaire (index Git isolé), hors dépôt, avec son inventaire. L'empreinte SHA-256 et l'arbre sont donnés à la remise, et non dans ce fichier, qui fait lui-même partie de l'archive.
