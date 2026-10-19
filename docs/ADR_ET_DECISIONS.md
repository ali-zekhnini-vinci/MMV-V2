# Registre de décisions — MMV V2, 15/10/2026

**Nomenclature nouvelle :** les IDs ci-dessous remplacent les collisions des anciens documents. Toute proposition conserve le statut `PROPOSED` tant que le propriétaire ne la ratifie pas.

| ID | Sujet | Choix ou règle | Statut actuel | Validation encore requise |
|---|---|---|---|---|
| D-000 | Rupture avec l'ancien MMV | Produit V2 neuf, aucune réutilisation ni lecture de l'ancien dépôt | **APPROVED** | Aucune |
| D-003 | Base de données | PostgreSQL exclusivement dans tous les modes/environnements | **APPROVED** | POC technique EF+Npgsql+PG, pas revalidation métier |
| D-004 | Topologie | Serveur .NET/PG, clients Tauri sans DB ; mono = les deux sur un PC | **APPROVED** | POC réseau et Windows |
| D-005 | Interfaces absentes | 153 vues et 23 compléments en backlog pour phases suivantes | **APPROVED** | Peut changer par dépendance critique |
| D-006 | Gouvernance | Phase 00 documentaire ; aucune initialisation avant GO P01 explicite | **APPROVED** | Accord GO P01 futur |
| ADR-001 | Architecture applicative | Tauri 2/React + API ASP.NET Core centralisée, monolithe modulaire | **PROPOSED** | Évaluer POC |
| ADR-002 | Versions socle | .NET 10 LTS, EF Core 10, Npgsql 10, PostgreSQL 18.x | **PROPOSED** | Versions patch épinglées / build Windows |
| ADR-003 | Provider unique | PostgreSQL seul (aucun SQLite/InMemory provider persistance) | **APPROVED** | POC de conformité |
| ADR-004 | Connexion LAN | DNS/IP/port manuel obligatoire, découverte facultative, identité serveur | **PROPOSED** | POC TLS/DNS/découverte |
| ADR-005 | Déploiement | Sélection Serveur/Poste ; un launcher unifié, fallback deux packages | **PROPOSED** pour packaging ; modes **APPROVED** | POC installateur, rollback |
| ADR-006 | Sécurité transport | TLS validé ; CA privée contrôlée vs domaine + CA publique | **OPEN DECISION** | Choisir modèle de confiance/renouvellement |
| ADR-007 | Identités/sessions | Identity côté serveur + permissions par opération ; session révocable en coffre OS | **PROPOSED** | POC token vs cookie/WebView2 ; droits par rôle à définir |
| ADR-008 | Continuité | Backup PG cohérent chiffré, restauration testée, copie hors serveur | **PROPOSED** | RPO/RTO, protection et procédure |
| ADR-009 | Compatibilité cible | Windows 11 x64 supporté en premier ; Windows 10 cas par cas | **PROPOSED** | Inventaire OS réel ; coûts |
| ADR-010 | Fiscalité/métier | MAD/FR comme UX ; calculs, taxes, documents et réglementations à définir neuf | **OPEN DECISION** | Pays et obligations avant module ventes |
| ADR-011 | Parcours multi-postes | version API, concurrence, idempotence, intégrité du stock et paiement | **PROPOSED** | tests transactionnels P01/lot métier |
| ADR-012 | Outils agent | Graphify + Serena en local, appels vérifiés ; pas de fake usage | **APPROVED en principe** | Préflight environnement & symboles en P01 |
| ADR-013 | Périmètre V1 | Démarrer socle puis lots design ; extensions différées | **PROPOSED** | GO sur chaque lot, règles à valider |
| ADR-014 | Licence dépendances | FullCalendar Standard uniquement sauf licence premium ; Serena outil dev seulement | **PROPOSED** | SBOM et vérification des licences P01 |

## Critères d'arbitrage opérationnel

**ADR-005 installer**
- A : 1 launcher NSIS avec rôle Serveur ou Poste ; expérience utilisateur optimale, fort coût de provisioning.
- B : installer Serveur distinct et installer Client Tauri ; plus simple à tester et mettre à jour.
- Recommandation : un parcours unifié **comme UX cible**, B comme option initiale viable si le POC A est risqué. Ne pas déclarer un .exe unique possible sans installation réussie de PG, service et WebView2.

**ADR-006 identité TLS**
- A : autorité de certification privée propre au magasin, ancrage root sur chaque poste par procédure contrôlée ; renouvellement/revocation à administrer.
- B : domaine contrôlé et certificat public à renouvellement automatisable + DNS local ; dépend de la possession d'un domaine et de sa maintenance.
- Recommandation : **ne pas préjuger**. L'authentification d'un serveur n'est jamais la simple présence d'une IP. POC WebView2 requis pour chaque option.

**ADR-007 sessions**
- A : jetons d'accès à courte durée + renouvellement révocable dans coffre Windows.
- B : cookies HttpOnly + session serveur ; vérifier same-site/CORS/CSRF dans origine Tauri.
- Recommandation : comparer sur vrai runtime Tauri/WebView2. Ne pas stocker les tokens persistants dans localStorage.

**ADR-009 parc Windows**
- Windows 11 x64 recommandation de maintenance. Windows 10 fin de support standard ; extension ou environnement LTSC/ESU à étudier uniquement si matériel ancien attesté.

## Questions réellement bloquantes, sans reposer les choix acquis

1. **Pour le POC d'installation :** le parc cible sera-t-il Windows 11 x64, ou Windows 10 est-il impératif ?
2. **Pour choisir TLS :** le magasin possède-t-il un nom de domaine gérable, ou faut-il privilégier une CA privée administrée par MMV ? Si inconnu, mettre les deux au POC.
3. **Pour le modèle d'exploitation :** le serveur sera-t-il un PC dédié, ou aussi un poste de caisse ? L'architecture supporte les deux ; seule la recette d'installation change.

Question importante mais différable jusqu'au lot Vente : pays d'exploitation/règles fiscales et documents légaux.

## Registre des décisions résolues séparément

Les maquettes sont **DRAFT** ; aucun écran `APPROVED` par défaut. Les propositions métier tirées du dossier maître avec mention de classes C# historiques sont irrecevables. Le backlog des 153 + 23 reste préservé.

## Note datée — 16/10/2026 (phase 02, en cours)

- **ADR-007 reste `PROPOSED`.** La phase 02 implémente une **variante de développement provisoire** : jeton opaque 256 bits, session serveur révocable (seul le SHA-256 est stocké), jeton en mémoire volatile du client uniquement, durée absolue 8 h, inactivité 30 min, aucun rafraîchissement. Comparaison cookies HttpOnly + anti-CSRF / coffre OS et risques : `docs/phase/02/GATES_AND_DECISIONS.md` § O-02-02. Ce n'est pas une session distribuable approuvée.
- Matrice de permissions **techniques** minimale : être authentifié ; `system.admin` (administration technique). Aucun droit métier ; les cinq profils visuels du Lot 1 ne confèrent aucun droit.

## Note datée — 16/10/2026 (phase 02, corrections finales avant migration)

- Revue externe frontend + backend .NET : architecture d'authentification de la phase 02 **conforme**, conservée. **ADR-007 reste `PROPOSED`** (variante de session dev inchangée).
- `bootstrap-admin` (MMV.Maintenance) n'écrit que sur la cible de développement autorisée (`mmv_v2_dev`, rôle `mmv_v2_dev`, port 5433, PostgreSQL 18, connexion locale) ; tout écart est refusé sans modification. Aucune migration appliquée ni bootstrap réel avant le GO O-02-01.
- Pile frontend de `docs/ARCHITECTURE_ET_STACK.md` §3 intégrée pour les seules lignes ayant un usage réel (Tailwind CSS 4, shadcn/ui + Radix, React Hook Form + Zod, React Router, Lucide) ; polices Inter/Source Sans 3 embarquées (O-02-05 résolu). **ADR-014 (licences)** : dépendances ajoutées MIT / Apache-2.0 / SIL OFL 1.1, inventaire `docs/phase/02/SOURCE_MAP.md` ; ADR-014, ADR-001 restent `PROPOSED`.

## Note datée — 16/10/2026 (phase 02, exécution O-02-01 et finalisation)

- GO O-02-01 exécuté : migration d'identité appliquée à `mmv_v2_dev` et `mmv_v2_test`, premier administrateur créé par le propriétaire, second bootstrap refusé. **ADR-007 reste `PROPOSED`** : la variante de session dev (jeton opaque en mémoire volatile, session serveur révocable) est désormais **prouvée en natif** dans WebView2 (aucun cookie ni stockage de session ; révocation, désactivation et retrait de droit effectifs immédiatement ; session perdue au redémarrage du client). La comparaison avec cookies HttpOnly ou coffre OS reste à faire avant distribution (`docs/phase/02/QA_MATRIX.md` Q-02-52 à Q-02-55).
- Incident **SEC-02-01** : mots de passe PostgreSQL des rôles de développement et de test affichés dans une session agent ; absents du dépôt ; rotation requise par le propriétaire (`docs/phase/02/GATES_AND_DECISIONS.md` § Incident SEC-02-01). Aucune décision d'architecture modifiée.

## Note datée — 17/10/2026 (phase 03-A)

- Phase 02 mergée (`cb79d1f`) ; ADR-007 reste `PROPOSED` (variante de session dev inchangée, réutilisée par le module Clients).
- Permissions **métier** proposées, non créées : `clients.read`, `clients.write`, `clients.archive` (puis `appointments.*` pour 03-B). `system.admin` ne confère aucun droit métier. Dossier : `docs/phase/03/DECISIONS_G03_METIER.md` (D-03-01 à D-03-17, tous `PROPOSED`).
- **ADR-014 (licences)** : dépendances ajoutées MIT (`@tanstack/react-query`, `@tanstack/react-table`, `@radix-ui/react-alert-dialog`, `@radix-ui/react-tabs`) ; FullCalendar Standard MIT évalué pour 03-B, plugins Premium exclus. ADR-014 et ADR-001 restent `PROPOSED`.
- ADR-010 (pays/fiscalité) toujours `OPEN DECISION` : il conditionne le format téléphonique normalisé, le fuseau du magasin (D-03-13) et l'effacement légal des données clients.

## Note datée — 17/10/2026 (phase 03-A2)

- **G03-METIER** (décision du propriétaire, `prompts/PHASE03_A2_GO_CLAUDE_CODE.md`) : sections A (modèle Clients) et B (permissions) **ACCEPTED** ; C (API et migration) **ACCEPTED sur le principe**, SQL de `0003_ClientsModule` soumis à revue, **non appliqué** ; D (Rendez-vous) en attente de 03-B. Détail : `docs/phase/03/DECISIONS_G03_METIER.md`.
- Permissions **métier** `clients.read`, `clients.write`, `clients.archive` définies dans le code (`src/MMV.Domain/Identity/Permissions.cs`) et contrôlées par des policies serveur distinctes ; `system.admin` n'en implique aucune. Attribution exclusivement par `MMV.Maintenance permissions grant|revoke` (cible stricte, jamais `system.admin`). Aucune attribution tant que 0003 n'est pas appliquée.
- **ADR-003** (PostgreSQL seul) respecté : EF Core 10 / Npgsql ; recherche sans extension PostgreSQL. **ADR-007** reste `PROPOSED` (session dev réutilisée). **ADR-014** : aucune dépendance ajoutée en 03-A2. **ADR-010** toujours `OPEN DECISION` (format téléphonique libre, effacement légal à cadrer).

## Note datée — 17/10/2026 (phase 03-A3)

- **G03-METIER C** : la réserve « SQL non appliqué » est levée par le GO 03-A3 (`prompts/PHASE03_A3_GO_CLAUDE_CODE.md`) ; migration `0003_ClientsModule` appliquée à `mmv_v2_dev` puis `mmv_v2_test` par `scripts/Invoke-MmvSqlScript.ps1` (procédure O-02-01 réutilisée, empreinte imposée). Aucun nouvel ADR : **ADR-003** (PostgreSQL seul) et la règle « jamais de migration au démarrage » sont respectés ; **ADR-014** : aucune dépendance ajoutée ; **ADR-007** reste `PROPOSED`.
- L'interface reflète les permissions de la session pour **l'affichage** des actions Clients ; elle n'est jamais une source d'autorisation (`system.admin` n'accorde toujours aucun droit Clients).
- **ADR-014 (licences), 03-B (17/10/2026)** : FullCalendar **Standard** installé — `@fullcalendar/react` 7.1.0 (+ `@fullcalendar/core`, `@full-ui/headless-calendar`), MIT ; `temporal-polyfill` 1.0.5 MIT (dépendance `temporal-spec` Apache-2.0) ; aucun paquet Premium (`resource*`, timeline) ; versions figées publiées depuis ≥ 2 semaines (`docs/phase/03/SOURCE_MAP.md`). ADR-014 reste **PROPOSED** (SBOM non produit).
- **G03-METIER D (03-B, 17/10/2026)** : proposition consolidée D-03-13 à D-03-17 + D-03-18 (motifs), **PROPOSED** — `docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md` ; dépend aussi d'ADR-010 (pays d'exploitation → fuseau du magasin).

- **G03-METIER D (03-B2, 17/10/2026)** : D-03-13 à D-03-18 **ACCEPTED** par le propriétaire (`prompts/PHASE03_B2_GO_CLAUDE_CODE.md`, détail `docs/phase/03/DECISIONS_03B_RDV_PROPOSITION.md` § Décision) ; *historique ci-dessus : PROPOSED en 03-B*. Aucun ADR ratifié par ce GO. **ADR-003** (PostgreSQL seul) respecté : garanties de réservation par transactions PostgreSQL (`pg_advisory_xact_lock` ordonné, `FOR SHARE`), sans extension ni microservice ; migration `0004_AppointmentsModule` **non appliquée**, jamais au démarrage. **ADR-010** (pays / fiscalité) reste `OPEN DECISION`, mais le GO fixe les marchés initiaux visés par les rendez-vous : **Maroc et Belgique** (`Africa/Casablanca`, `Europe/Brussels` pris en charge) ; le fuseau réel est un **paramètre par magasin** à renseigner, aucune localisation unique n'est présumée. **ADR-014** : aucune dépendance ajoutée en 03-B2 (base de fuseaux embarquée côté serveur non introduite, O-03-22). **ADR-007** reste `PROPOSED`.
- **03-B3 (17–18/10/2026)** : O-03-24 **ACCEPTED** (remplacement de la contrainte de permissions par `DROP CONSTRAINT` + `ADD CONSTRAINT` dans la même transaction) et migration `0004_AppointmentsModule` appliquée ; O-03-23 **ACCEPTED provisoirement** (rendez-vous commencé : réservation figée, motif et notes corrigeables avec auteur et date) ; O-03-22 **maîtrisé** par la stratégie de cohérence des fuseaux (`docs/phase/03/GATES_AND_DECISIONS.md`) ; dette produit : historique détaillé des modifications (O-03-30). Fuseau du magasin fourni par le propriétaire : `Europe/Brussels` (paramètre d'installation, D-03-13) ; **ADR-010 (pays/fiscalité) reste `OPEN DECISION`** : la fourniture du fuseau ne tranche ni la fiscalité, ni le format téléphonique, ni l'effacement légal.
- **03-C (18/10/2026)** : aucune décision d'architecture nouvelle. O-03-16 (outil d'application SQL) : correction **préparée et testée**, acceptation du propriétaire requise ; O-03-22 : protections de fuseaux **conservées**, risque Casablanca **non clos** (GO 03-C) ; décisions produit attendues avant `PHASE03_COMPLETE` : rendez-vous sur la fiche client (O-03-37) et constats UX (O-03-33 à O-03-36) — `docs/phase/03/VALIDATION_PROPRIETAIRE_03C.md`.

## Note datée — 18/10/2026 (phase 03-D)

- **Journal d'audit métier (proposition D-03-19, 03-D2)** : schéma PostgreSQL `audit` unique, table `audit.events` en **ajout seul** (déclencheurs), événements écrits par le **backend** dans la transaction de la modification métier (même `SaveChanges` / même transaction verrouillée), jamais par l'interface ; extensible aux modules futurs par codes d'action, sans migration. Conforme à ADR-003 (PostgreSQL seul, aucune extension) et au monolithe MMV.Api (aucun microservice). Migration `0005_AuditJournal` **préparée, non appliquée** (revue du propriétaire) — `docs/phase/03/AUDIT_JOURNAL_03D.md`.
- **Permission dédiée `audit.read` (proposition D-03-21)** : séparation explicite du rôle visuel, des permissions effectives et du droit d'audit ; `system.admin` n'accorde toujours aucun droit métier ni d'audit.
- **Minimisation (proposition D-03-20)** et **conservation (D-03-22, durée ouverte)** : liste blanche des valeurs conservées ; aucune donnée personnelle client, de santé, ni secret ; durée de conservation à fixer avec un conseil juridique (Belgique : RGPD / APD ; Maroc : loi 09-08 / CNDP).
- **ADR-014 (licences), 03-D1** : `react-day-picker` 10.0.2 (+ `date-fns`, `@date-fns/tz`) et `@radix-ui/react-popover` 1.3.1, MIT.

## Note datée — 19/10/2026 (phase 03-D3)

- **Journal d'audit (D-03-19, D-03-20, D-03-21) : RETENUS** par le GO 03-D3 — schéma `audit` centralisé, écritures atomiques dans la transaction métier, permission `audit.read` séparée, minimisation. Migration `0005_AuditJournal` **appliquée** (`mmv_v2_dev`, `mmv_v2_test`, D-03-23).
- **Limite** : les déclencheurs d'ajout seul ne protègent pas contre un superutilisateur PostgreSQL ni contre le rôle propriétaire de la table (rôle de migration, aujourd'hui aussi rôle de connexion de MMV.Api). **À prévoir** : rôle SQL applicatif distinct du rôle de migration (O-B04) et politique de rétention contrôlée.
- **D-03-22 OUVERTE** : durées de conservation ; obligations RGPD / APD (Belgique) et loi 09-08 / CNDP (Maroc), avec un conseil juridique.
- **Modification sans changement** (O-03-44) : aucune écriture (ni auteur, ni date, ni version, ni événement) ; résultat HTTP 200 avec l'état courant.

## Note datée — 19/10/2026 (phase 04-00)

- Phase 03 **fusionnée** (`origin/main` = `6924860`, PR #4, vérifié par `git fetch`).
- **D-04-01 APPROVED** (propriétaire, 19/10/2026) : ordonnances médicales reçues et mesures réalisées en magasin = deux types métier distincts, sans conversion ni assimilation ; provenance, auteur et statut explicites ; aucun SQL autorisé par cette décision.
- **D-04-02 à D-04-09 PROPOSED** (complétude, provenance et pièces, révisions et invalidation, permissions optiques, validations optiques, client archivé, navigation, pays et confidentialité) — demande consolidée : `docs/phase/04/DECISIONS_04_METIER_PROPOSED.md`. D-04-06 exige l'avis d'un opticien ; D-04-09 un conseil juridique (Belgique : RGPD / APD ; Maroc : loi 09-08 / CNDP), et prolonge D-03-22 (conservation, OUVERTE).
- Aucun ADR modifié : ADR-001, ADR-007, ADR-014 restent `PROPOSED`, ADR-010 `OPEN DECISION`, ADR-003 respecté (dossier optique proposé en PostgreSQL seul). **ADR-014** : aucune dépendance ajoutée.

## Note datée — 19/10/2026 (Phase 04, suite de 04-00)

- Revue indépendante : G04-00 « cadrage techniquement acceptable ». **D-04-01 APPROVED** (définitive) ; D-04-02 à D-04-09 `PROPOSED` (inchangées) ; **D-04-10 à D-04-14 `PROPOSED`** ajoutées (`docs/phase/04/ETUDES_PRE_MODELE_04.md`).
- Aucun ADR modifié. **ADR-008** (`PROPOSED`) : les pièces source domineraient le volume de la base et des sauvegardes (taille ≈ volume des pièces) ; chiffrement, copie hors serveur et restauration avec vérification des empreintes deviennent des préalables explicites à un pilote (O-04-19) ; le choix sauvegarde logique / physique reste à prendre au seuil mesuré. **ADR-014** : aucune dépendance ajoutée ni proposée (contrôles de fichiers sans bibliothèque ; AMSI = appel système Windows, à évaluer, O-04-17).

## Note datée — 19/10/2026 (Phase 04-A, GO G04-A — UI ONLY)

- **G04-A APPROVED — UI ONLY** et **O-04-16 APPROVED** (propriétaire) : prototypes des écrans du dossier optique **uniquement** dans un banc de démonstration isolé (`apps/desktop/optical-bench.html`, hors `vite build`) ; comptes réels inchangés (« Ordonnances — phase ultérieure »). D-04-01 APPROVED ; **D-04-02 à D-04-14 restent `PROPOSED`** (illustrées par le prototype, non tranchées).
- Aucun ADR modifié. **ADR-014** respecté : aucune dépendance ajoutée (React Hook Form, TanStack Query, Radix, react-day-picker, Lucide existants). **ADR-003** : aucune base touchée, aucune migration. Bilan : `docs/phase/04/LIVRAISON_04A.md`.
