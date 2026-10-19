# MMV V2 — PHASE 03-A2 — GO MÉTIER CLIENTS

Le rapport 03-A a été examiné.

**G03-METIER : A et B ACCEPTÉS. C ACCEPTÉ SUR LE PRINCIPE, SQL NON ENCORE AUTORISÉ À L'APPLICATION.**

Décisions :

1. Modèle Clients :
   - Prénom et nom obligatoires.
   - Autres champs facultatifs.
   - Référence CLI générée côté PostgreSQL, unique et non réutilisée.
   - Recherche insensible aux accents et à la casse.
   - Aucun email ou téléphone obligatoirement unique.
   - Aucune donnée médicale, sécurité sociale ou mutuelle en Phase 03.
   - Archivage et réactivation, aucune suppression définitive.
   - Gestion stricte de la concurrence par version, sans écrasement silencieux.

2. Permissions :
   - clients.read
   - clients.write
   - clients.archive
   - system.admin ne donne aucun droit Clients implicite.
   - Attribution contrôlée par l'outil de maintenance.

3. API et persistance :
   - Tu peux implémenter le backend Clients, les endpoints, validations, permissions et tests.
   - Utilise EF Core 10, Npgsql et PostgreSQL 18 exclusivement.
   - Prépare la migration 0003_ClientsModule et son SQL exact.
   - Ne l'applique pas avant la revue du SQL.
   - Complète OpenAPI avec les vrais schémas de requêtes/réponses et utilise des types TypeScript cohérents avec le contrat.
   - Branche TanStack Query et TanStack Table sur la vraie API.
   - Aucun client fictif dans la base de développement.

4. Interface :
   - Conserve les écrans réalisés et les maquettes validées.
   - Respecte Tailwind CSS 4, shadcn/ui, Radix, React Hook Form, Zod, React Router et Lucide.
   - Corrige les régressions éventuelles.
   - Maintiens les états de chargement, erreur, conflit et permission refusée.

5. Tests et environnement :
   - Ne modifie pas les processus Tauri/Vite ou API ouverts par le propriétaire sans son accord.
   - Travaille proprement avec npm et les dépendances.
   - Exécute les tests possibles sans migration.
   - Ne déclare pas les tests PostgreSQL d'écriture réussis avant leur exécution réelle.

6. Rendez-vous :
   - G03-METIER D reste en attente de la Phase 03-B.
   - Ne développe pas encore le module Rendez-vous.

7. DOCUMENTATION GATE :
   - Synchronise README, phase 03, décisions, spécifications, QA, SOURCE_MAP, ADR et architecture concernés.
   - Conserve les décisions historiques.

Commits locaux autorisés sur feat/phase-03-clients-rdv.

Aucun push, aucune PR, aucun merge.

À la fin, prépare un ZIP léger avec git archive comprenant frontend, backend, migration SQL, tests et documentation.

Présente un rapport consolidé et le SQL pour ma dernière revue avant application.

**PHASE03_A2_READY — OWNER_SQL_REVIEW_REQUIRED**
