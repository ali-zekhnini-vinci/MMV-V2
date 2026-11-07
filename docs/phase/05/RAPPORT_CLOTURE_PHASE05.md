# Phase 05 — Rapport de clôture contrôlée (07/11/2026)

Mandat : [`prompts/MMV_V2_CLOTURE_CONTROLEE_PHASE05_TRANSITION_PHASE06_2026-11-07.md`](../../../prompts/MMV_V2_CLOTURE_CONTROLEE_PHASE05_TRANSITION_PHASE06_2026-11-07.md). Preuves de clôture : [`evidence/cloture/`](evidence/cloture/). Rapport précédent : [`LIVRAISON_05_CSVAI_GATE_D.md`](LIVRAISON_05_CSVAI_GATE_D.md).

## Verdict

**`PHASE05_TECHNICALLY_CLOSED — CSV_ASSISTANT_OWNER_UX_REJECTED — READY_FOR_MANUAL_MERGE (sous réserve de la validation finale du propriétaire)`.**

- Le socle de la Phase 05 (catalogue, tarifs, fournisseurs, stock, inventaires, import CSV contrôlé, référentiel des verres, migrations 0007 → 0009) est **techniquement implémenté et testé** ; aucun défaut bloquant ouvert (§ 7).
- Le **nouvel assistant d'import CSV** est techniquement implémenté (GATE D) mais **refusé par le propriétaire sur le plan de l'interface et de la conception** (07/11/2026) : il reste **désactivé par défaut côté serveur**, sa refonte est **reportée** (BL-05-13) et **aucun déploiement commercial** n'est autorisé sans nouvelle recette.
- La Phase 05 **n'est pas déclarée entièrement approuvée** : la validation UX de l'assistant est refusée, l'acceptation visuelle G05-DESIGN reste ouverte, G05-FISCAL / ADR-010 et G05-LEGAL restent ouverts.
- `READY_FOR_MANUAL_MERGE` qualifie l'état **technique et Git** de la branche de livraison ; la fusion, la PR et le push appartiennent au propriétaire (§ 10). Aucun push, PR ni merge effectué.

## 1. Statuts utilisés

| Statut | Sens dans ce rapport |
| --- | --- |
| `TECHNICALLY_IMPLEMENTED` | code, migration éventuelle et tests livrés et au vert ; n'emporte aucune approbation métier ou visuelle |
| `OWNER_UX_REJECTED` | interface ou conception refusée par le propriétaire après essai |
| `DEFERRED` | reporté explicitement, inscrit au backlog, **jamais** compté comme PASS |
| `BLOCKED` | impossible sans décision ou élément externe (fiscalité, avis juridique, GO) |
| `READY_FOR_MANUAL_MERGE` | branche consolidée, suites au vert, documentation synchronisée ; fusion manuelle par le propriétaire |

## 2. Périmètre réalisé

| Sous-lot | Contenu | Migration | Statut |
| --- | --- | --- | --- |
| 05-00 | cadrage, validation métier Q01–Q20 ([`CADRAGE_05_00.md`](CADRAGE_05_00.md), [`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md)) | — | clos (30/10) |
| 05-A → 05-D | magasins / emplacements (préparés), catalogue six familles, modèles et variantes, tarifs par magasin (achat HT × coefficient, prix manuel, historique), fournisseurs, stock par emplacement, réceptions simples, ajustements, alertes, inventaires, import CSV contrôlé, contrats internes Phase 06 ([`LIVRAISON_05_FINAL.md`](LIVRAISON_05_FINAL.md)) | `0007_CatalogStockModule` | `TECHNICALLY_IMPLEMENTED` |
| G05-DESIGN | refonte fidèle au Lot 6 ([`LIVRAISON_05_DESIGN.md`](LIVRAISON_05_DESIGN.md)) | — | `TECHNICALLY_IMPLEMENTED` ; acceptation visuelle **OPEN** |
| COR-05-01 → 08 | magasin de l'installation, temps réel, recherche, combobox fournisseur, référentiel des verres simple / avancé, import intelligent ([`LIVRAISON_05_CORRECTIONS.md`](LIVRAISON_05_CORRECTIONS.md)) | `0008_CatalogCorrections` | `TECHNICALLY_IMPLEMENTED` |
| UX-IA | assistant produit à tarif obligatoire, seuil d'alerte, catalogue des verres unifié, import CSV avec IA locale ([`LIVRAISON_05_UX_IA.md`](LIVRAISON_05_UX_IA.md)) | — | `TECHNICALLY_IMPLEMENTED` ; revue visuelle **OPEN** (Q-UX-01 à 03) |
| CSV-IMPORT-AI-01 | banc Ollama, contrat v2, assistant en cinq étapes, offres de verres, `check_stale`, GATE D ([`CSV_IA_05.md`](CSV_IA_05.md), [`LIVRAISON_05_CSVAI_GATE_D.md`](LIVRAISON_05_CSVAI_GATE_D.md)) | `0009_CsvImportAssistant` | `TECHNICALLY_IMPLEMENTED` + **`OWNER_UX_REJECTED`** ; refonte `DEFERRED` |
| Clôture (ce mandat) | garde serveur de l'assistant désactivé prouvée par test (§ 6), consolidation Git, documentation | — | voir § 9 |

**Ancien import CSV** (parcours COR-05 / UX-IA) : conservé et servi par défaut (`CatalogInterpreter:WizardEnabled = false`) ; c'est la **solution de repli** officielle.

## 3. Décisions

### 3.1 Approuvées par le propriétaire (rappel, sources inchangées)

D-05-01 à D-05-06, D-05-10 à D-05-12 (en principe ou corrigées) et D-05-14 à D-05-21 ([`DECISIONS_05_METIER_PROPOSED.md`](DECISIONS_05_METIER_PROPOSED.md)) ; décisions UX-IA D-UX-01 / D-UX-02 (magasin unique, tarification obligatoire, seuil option A) ; GO G05-SQL (0007), G05-SQL-0008, G05-SQL-0009 (conditionnel) et leurs applications ; Q-CSVAI-01 à 06 et décisions de travail Q-CSVAI-07 à 10 consignées dans [`CSV_IA_05.md`](CSV_IA_05.md) § 5.

### 3.2 Enregistrées par ce mandat (propriétaire, 07/11/2026)

| Réf. | Décision |
| --- | --- |
| CL-05-01 | Assistant CSV : implémentation technique **terminée** ; GATE D **réalisé avec ses limites** (§ 5). |
| CL-05-02 | Assistant CSV : **validation UX du propriétaire REFUSÉE** (interface et conception). Le détail des défauts sera fourni par le propriétaire ; il n'est pas reconstitué ici. |
| CL-05-03 | Nouvelle refonte de l'assistant **REPORTÉE** (BL-05-13). |
| CL-05-04 | **Ancien import conservé** comme solution de repli ; `WizardEnabled = false` reste la valeur versionnée. |
| CL-05-05 | **Aucun GO de déploiement commercial** du nouvel assistant sans nouvelle recette (BL-05-14). |
| CL-05-06 | Contrôles non réalisés **reportés sans être déclarés PASS** (§ 5). |

### 3.3 Restant ouvertes (non résolues par ce rapport)

- **G05-FISCAL / ADR-010** (`OPEN DECISION`) : taux, catégories et arrondi de la taxe par magasin ; TTC indisponible ; **bloque toute vente réelle**.
- **G05-LEGAL** : traçabilité des dispositifs médicaux, conservation des pièces et inventaires, identifiants légaux.
- **Conservation des originaux CSV** : Q-CSVAI-07 (octets d'origine), Q-CSVAI-12, BL-05-12 / R-F-02 (`imports.batches.content`).
- **Rétention des sauvegardes** : sort des sauvegardes de prévalidation et définitives (avant 0007, 0008, 0009) — décision du propriétaire.
- **Contrastes globaux** : jeton `--text-tertiary` (3,03:1 sur blanc) hors de l'assistant (BL-05-16).
- ADR-CSVAI-04 et ADR-CSVAI-09 `PROPOSED` ; D-05-07, D-05-09, D-05-13, QR-01 à QR-13 (R-F-06) ; Q-UX-01 à 03 ; Q-CSVAI-11 à 14 ; G05-DESIGN (acceptation visuelle Lot 6) ; O-05-03, O-05-07, O-05-08, O-05-10 ; réserves R-F-01 à R-F-08 (§ 7).

## 4. Résultats des tests

### 4.1 Rejoués par ce mandat (07/11/2026, après le dernier changement de code)

Journal : [`evidence/cloture/CLOTURE_suites_20261107.txt`](evidence/cloture/CLOTURE_suites_20261107.txt).

| Suite | Résultat |
| --- | --- |
| `dotnet build MMV.slnx` | PASS — 0 avertissement, 0 erreur |
| MMV.Api.Tests | **PASS 685/685** (670 de GATE D + 15 de `CsvAssistantDisabledGateTests`) |
| MMV.Maintenance.Tests | **PASS 50/50** |
| MMV.PostgreSql.Tests `Stage=Phase05`, cluster **jetable** (PostgreSQL 18.6, migrations 0001 → 0009) | **PASS 38/38** ; 2 NotExecuted par conception (processus HTTPS réel, Ollama réel) → `NOT_RERUN` |
| Poste : `tsc -b --noEmit` | PASS (aucune erreur) |
| Vitest | **PASS 341/341** (32 fichiers) |
| ESLint | PASS — 0 erreur, 1 avertissement préexistant (`ClientsTable.tsx`, `react-hooks/incompatible-library`) |
| Contre-épreuves de la garde (§ 6) | PASS — la garde ouverte fait échouer 13/13 cas ([journal](evidence/cloture/CLOTURE_contre_epreuves_assistant_desactive_20261107.log)) |

### 4.2 Non rejoués (`NOT_RERUN`) — dernier résultat connu

| Suite | Dernier résultat | Pourquoi non rejouée |
| --- | --- | --- |
| MMV.PostgreSql.Tests complète sur `mmv_v2_test` | PASS 107 + 1 NotExecuted (GATE D, 07/11) | évite d'écrire dans la base du propriétaire pendant que son API est en marche ; code produit inchangé depuis GATE D (§ 4.3) |
| Processus HTTPS réel | PASS 8/8 (GATE D) | banc dédié requis ; code inchangé |
| Ollama réel `qwen3.5:4b` | PASS (GATE D) | code inchangé |
| Natif Tauri CSVAI | PASS 58/58, un seul poste (GATE D) | code inchangé ; l'assistant est refusé en UX |

### 4.3 Code inchangé depuis GATE D

Les 23 fichiers de code, de tests et SQL de l'archive GATE D (arbre `be60f20f3dca760b11a28bd132aa68e71439f904`) ont le **même blob** dans l'arbre de livraison. Fichiers de code modifiés depuis l'archive GATE D : `tests/MMV.Api.Tests/Catalog/Assisted/CsvAssistantDisabledGateTests.cs` (nouveau, test seulement) ; `src/MMV.Api/appsettings.json` (horodatage seul : contenu identique, blob `014e990`, à celui de l'archive d'implémentation `1d5cfd4e…`, `WizardEnabled: false`).

## 5. Contrôles non réalisés — `DEFERRED` (jamais PASS)

| Contrôle | État | Backlog |
| --- | --- | --- |
| Deux machines Windows distinctes | NOT_TESTED (deux fenêtres sur un même poste seulement) | BL-05-14 |
| Catalogues fournisseurs réels anonymisés | NOT_TESTED (non fournis, aucun inventé) | BL-05-14 |
| Touche Échap sur la modale de confirmation, en Tauri natif | NOT_TESTED en natif (jsdom seulement) | BL-05-14 |
| Non-régression **native** complète des modules antérieurs | NOT_TESTED en natif (suites automatisées PASS) | BL-05-14 |
| Performances de Qwen sur le matériel réel des magasins | NOT_TESTED (poste de développement seulement) | BL-05-U2 (existant) |

## 6. Statut UX et garde d'exploitation de l'assistant

- **Statut : `TECHNICALLY_IMPLEMENTED` / `OWNER_UX_REJECTED` / refonte `DEFERRED` / déploiement commercial `BLOCKED` (aucun GO).**
- **Garde serveur** : `CatalogInterpreter:WizardEnabled` vaut `false` dans `src/MMV.Api/appsettings.json` (versionné). Les **13 routes** du nouveau parcours (`/api/imports/summary`, `/assistant/inspect`, `/assistant`, `/{id}/assistant`, `/analysis` GET / POST / DELETE, `/analysis/cancel`, `/rows`, `/issues/{key}`, `/decisions`, `/check`, `/apply`) répondent `404 assistant_disabled` ; la file d'analyse n'accepte rien ; seul l'état du service (`/assistant/status`) reste servi ; le poste affiche alors l'ancien import.
- **Preuve ajoutée** : `tests/MMV.Api.Tests/Catalog/Assisted/CsvAssistantDisabledGateTests.cs` — (1) la valeur versionnée est `false` ; (2) chaque route répond `404 assistant_disabled` avec une base **injoignable** et un faux catalogue qui refuse tout appel autre que la lecture du magasin (aucune requête, aucune écriture) ; (3) l'inventaire des routes `/assistant` est entièrement couvert (une route ajoutée sans garde ferait échouer le test). Contre-épreuves : assistant ouvert → 13/13 échecs (6 réponses ≠ 404, 7 accès à la base).
- **Activation locale du propriétaire** : la copie de build `src/MMV.Api/bin/Debug/net10.0/appsettings.json` (ignorée par Git, modifiée par le propriétaire le 07/11 à 14:03) porte `WizardEnabled: true`. Elle n'a **pas** été modifiée. Recommandation : la remettre à `false` (ou relancer une compilation, qui recopie la valeur versionnée) tant que l'assistant n'est pas accepté.
- Constats ergonomiques déjà consignés par les recettes (non exhaustifs, **ne remplacent pas** la liste du propriétaire) : libellé du nombre de lignes dans la confirmation de devise ; bandeau « Serveur MMV joignable » rafraîchi seulement à son contrôle périodique ([`LIVRAISON_05_CSVAI_GATE_D.md`](LIVRAISON_05_CSVAI_GATE_D.md) § 10).

## 7. Risques résiduels et défauts

### 7.1 Création concurrente d'offres de verres de même identité — **NON BLOQUANT**

- **Scénario** : deux applications d'import assisté simultanées (ou une application et une saisie manuelle) créent chacune une offre de même identité (fournisseur, gamme, type, indice, matériau, traitements, approvisionnement) **absente** au moment du contrôle. Les verrous de GATE D portent sur les offres **existantes** ; une offre à créer n'a pas de ligne à verrouiller, et aucune contrainte d'unicité n'existe (plusieurs offres de même identité saisies à la main sont permises depuis 0008). Résultat possible : deux offres de même identité.
- **Effet** : aucune contrainte de base violée ; aucune valeur existante écrasée ; aucune règle commerciale contournée (chaque création passe par `LensOfferService` : validation, droit `costs.write` pour les prix, révision, audit). Un import ultérieur de la même identité départage par géométrie identique, sinon **refuse** la ligne (`lens_offer_ambiguous_existing`) : comportement fermé, jamais d'écriture silencieuse. Correction : archiver l'offre en trop dans le référentiel.
- **Exposition** : seul le **nouvel assistant** crée des offres par import (l'ancien import ne crée que des variantes de verres) ; ce chemin est **fermé côté serveur** par défaut (§ 6) et n'a aucun GO de déploiement.
- **Décision** : documenté et reporté (BL-05-15). Correction sans migration proposée pour la refonte : verrou consultatif transactionnel (`pg_advisory_xact_lock`) sur (fournisseur, gamme) avant la relecture du plan ; une contrainte d'unicité exigerait une migration et une revue SQL distinctes (**non introduite**).

### 7.2 Autres risques et réserves

| Réf. | Sujet | Classement |
| --- | --- | --- |
| R-F-01 / BL-05-11 | appartenance emplacement ↔ magasin garantie par l'application seulement | non bloquant (un magasin par serveur), OPEN |
| R-F-02 / BL-05-12, Q-CSVAI-07 | conservation des contenus et des originaux CSV, purge | OPEN (G05-LEGAL) |
| R-F-03 / BL-04-04 | rôle SQL applicatif à privilèges limités | OPEN — préalable à un pilote |
| R-F-04 / BL-04-05 | restauration complète d'une sauvegarde | OPEN — restauration **éprouvée** sur base distincte pour les sauvegardes avant 0009 (IDENTIQUE, GATE D) ; procédure d'exploitation et choix de rétention restent à décider |
| R-F-05 | fiscalité EUR / MAD, validations réglementaires | **BLOCKED** pour toute vente réelle (ADR-010, G05-FISCAL, G05-LEGAL) |
| R-F-06 | choix techniques non ratifiés du registre D-05 | OPEN |
| R-F-07 / BL-05-09 | exports CSV différés | DEFERRED |
| R-F-08 | références Lot 6 partiellement versionnées | OPEN (G05-DESIGN) |
| BL-05-16 | jeton global `--text-tertiary` (contraste) hors de l'assistant | DEFERRED, décision du propriétaire |
| BL-05-C2 à C4 | logo sombre, édition d'offre sans `costs.write`, lisibilité à 1366 px | DEFERRED |
| — | `scripts/Invoke-MmvSqlScript.ps1` sans `PGCLIENTENCODING=UTF8` (avant tout futur script accentué) | DEFERRED (note du 06/11) |

**Défauts bloquants ouverts : aucun.** Défauts non bloquants : § 7.1, ergonomie de l'assistant (BL-05-13), réserves ci-dessus.

## 8. État des migrations

| Migration | Dépôt | `mmv_v2_dev` | `mmv_v2_test` | Dossier |
| --- | --- | --- | --- | --- |
| `20261030005557_CatalogStockModule` (0007) | oui | appliquée (30/10) | appliquée (30/10) | [`G05_SQL_DOSSIER.md`](G05_SQL_DOSSIER.md) |
| `20261102183203_CatalogCorrections` (0008) | oui | appliquée (03/11) | appliquée (03/11) | [`SQL_0008_REVUE.md`](SQL_0008_REVUE.md) |
| `20261106154105_CsvImportAssistant` (0009) | oui | appliquée (06/11 23:10 UTC) | appliquée (06/11 23:10 UTC) | [`SQL_0009_REVUE.md`](SQL_0009_REVUE.md) |

Revérifié le 07/11/2026 en **lecture seule** (`default_transaction_read_only=on`) : 9 migrations (0001 → 0009) sur chaque base ([précontrôle](evidence/cloture/CLOTURE_precontrole_20261107.txt)). **Aucune migration nouvelle** dans ce mandat. Les tables de 0009 restent inutilisées tant que l'assistant est désactivé ; les retirer exigerait une migration et une revue distinctes (non proposé).

## 9. État Git

- **Précontrôle** : `spike/csv-import-ollama-benchmark` et `feat/phase-05-catalogue-stock` pointaient sur le **même** commit `55f021d` ; `main` = `origin/main` = `0a83a57`, ancêtre (19 commits Phase 05, 0 en retard) ; index vide ; tout le lot CSVAI (06–07/11) n'existait que dans l'arbre de travail. Aucune divergence, aucun conflit.
- **Consolidation** : passage sur `feat/phase-05-catalogue-stock` (même commit : arbre de travail et fichiers personnels inchangés), puis deux commits **locaux** : (1) `3528137f70c6a40d313baee31a06d281aef16904` — code, tests, migration 0009 et poste (79 fichiers sous `src/`, `tests/`, `apps/desktop/`) ; (2) documentation, scripts SQL 0009, preuves CSVAI et de clôture, mandats (`docs/`, `prompts/`, `README.md`) — son empreinte figure dans le rapport final du mandat et dans l'archive de clôture (un commit ne peut citer sa propre empreinte).
- **Jamais indexés** : `apps/desktop/.env.development` du propriétaire (SHA-256 `a215e1f0…d2e4e`, inchangé), `.claude/`, `.serena/`, archives `*.zip`, `references/phase02..04/`, pack `docs/phase/04/` du propriétaire (O-05-07), `apps/desktop/tsconfig.tsbuildinfo`.
- Aucun `reset --hard`, `clean`, `stash`, push, PR ni merge ; `main` non modifiée. La branche `spike/csv-import-ollama-benchmark` reste en place sur `55f021d` (sans commit propre) : la supprimer après fusion relève du propriétaire.
- Contrôle de secrets avant commit : PASS ([précontrôle](evidence/cloture/CLOTURE_precontrole_20261107.txt)).

## 10. Conditions de validation finale (propriétaire)

1. Examiner ce rapport et l'archive `MMV-V2-PHASE05-CLOTURE.zip` (empreintes fournies séparément).
2. Accepter explicitement la fusion **avec** les réserves : assistant CSV `OWNER_UX_REJECTED` et désactivé ; G05-DESIGN non accepté ; § 5 `DEFERRED` ; G05-FISCAL / G05-LEGAL ouverts.
3. Remettre `WizardEnabled` à `false` dans sa copie locale de build (§ 6), à sa convenance.
4. Autoriser le push de `feat/phase-05-catalogue-stock`, ouvrir la PR vers `main` et fusionner (opérations du propriétaire).
5. Ensuite seulement : GO de démarrage de la Phase 06 (cadrage préparé séparément, hors de cette branche).

## 11. Limites d'exploitation

- **Aucune vente réelle** possible ni autorisée (fiscalité non ratifiée, TTC indisponible ; Phase 06 non commencée).
- Nouvel assistant d'import : **désactivé** ; à ne pas activer sur une installation de magasin sans nouvelle recette et GO.
- Ancien import CSV : parcours de référence ; contrôle à blanc et application tout-ou-rien.
- Un serveur = un magasin ; transferts entre emplacements et magasins hors périmètre (BL-05-04) ; permissions par magasin non disponibles (BL-05-07).
- Données de santé et pilote : préalables `FOUNDATION_MUST` toujours ouverts (BL-04-04, BL-04-05, BL-04-06).
