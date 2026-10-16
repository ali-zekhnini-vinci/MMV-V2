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
