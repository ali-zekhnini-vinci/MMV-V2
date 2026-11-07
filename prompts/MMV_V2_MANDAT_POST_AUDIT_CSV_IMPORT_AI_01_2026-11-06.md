# MMV V2 — MANDAT POST-AUDIT CSV-IMPORT-AI-01
## Corrections ciblées, prévalidation SQL 0009 et préparation GATE D

Le propriétaire a transmis le ZIP `MMV-V2-CSVAI-IMPLEMENTATION.zip` à un auditeur externe.

L'archive est conforme : 109 fichiers et 109 empreintes vérifiées. La revue statique du SQL est favorable sous conditions.

Le développement C1, C2 et C3 existe déjà. Il est interdit de le recommencer ou de procéder à une refonte générale.

## 1. Précontrôle

Vérifie la branche, le HEAD, le worktree et les fichiers existants.

Préserve `apps/desktop/.env.development`, les changements non commitées et tous les documents du propriétaire.

Réutilise les implémentations actuelles et les preuves du lot livré.

## 2. Corrections ciblées

**COR-CSVAI-01 — Identité des offres de verres**

Audite `AssistedLensPlanner.cs` et `LensOfferService.cs`.

La clé actuelle de rapprochement ne contient pas les diamètres ni les plages SPH/CYL/ADD.

Vérifie le contrat métier réel des offres et détermine si deux lignes présentant des géométries différentes représentent deux offres distinctes ou les caractéristiques d'une même offre.

Corrige tout rapprochement incorrect, sans fusion ou création arbitraire.

Ajoute les tests nécessaires.

**COR-CSVAI-02 — Validation des offres complètes**

Vérifie le traitement de `RulesComplete` lors de la modification d'une offre existante.

Une mise à jour de plage, diamètre ou restriction ne doit pas conserver un statut de complétude devenu injustifié.

Respecte le cycle métier existant et exige une nouvelle validation lorsque nécessaire.

**COR-CSVAI-03 — Devise**

Une devise absente du CSV ne doit jamais être considérée comme une devise explicitement fournie.

La devise du magasin peut être proposée, mais doit être confirmée pour les montants concernés.

Ne pas convertir automatiquement les devises.

Ne jamais utiliser silencieusement EUR si la configuration du magasin est introuvable.

**COR-CSVAI-04 — Résultat du rejeu idempotent**

Corrige les compteurs de `AppliedAsync`.

Lors du rejeu avec le même identifiant de requête, le résultat doit être cohérent avec celui de la première application.

Tester notamment les offres de verres créées et modifiées.

**COR-CSVAI-05 — Procédure de restauration**

Corrige `SQL_0009_REVUE.md`.

Ne pas proposer `pg_restore --clean` sur une base active comme procédure générale de retour arrière.

Documenter une restauration sûre sur une base fraîche distincte, avec contrôles de cohérence et procédure de bascule explicite.

Tester une sauvegarde et une restauration contenant réellement des données synthétiques de l'assistant.

## 3. Vérifier la migration 0009

Ne modifie pas le script SQL existant si les corrections applicatives ne nécessitent aucun changement de schéma.

Son empreinte approuvable est :

`30dc6e0a4b3184e31e9afc12147aeb5df3e9d5901eabb4e85e5d94a16fa2e7e0`

Si une modification de 0009 devient nécessaire, STOP sur la partie SQL et produire un nouveau dossier de revue avec de nouvelles empreintes.

Avant toute application persistante, vérifier :

- PostgreSQL 18.x sur le port 5433.
- Identité exacte de chaque instance, base et rôle.
- Migrations 0001 à 0008 présentes et 0009 absente.
- Absence de dérive de schéma.
- Absence d'écriture concurrente.
- Sauvegardes séparées de `mmv_v2_dev` et `mmv_v2_test`.
- Empreintes et lisibilité des sauvegardes.
- Procédure de restauration sûre et vérifiée.
- Empreinte exacte du script.

L'application sur `mmv_v2_dev` et `mmv_v2_test` est réservée au GO SQL explicite du propriétaire.

Ne pas considérer ce mandat comme une autorisation d'exécution persistante.

## 4. Préparer et exécuter les tests non bloqués

Après corrections, rejouer :

- Build .NET.
- Suites API et Maintenance.
- Vitest.
- TypeScript et ESLint.
- Tests PostgreSQL jetables.
- Tests d'injection et de concurrence.
- Tests du référentiel des verres.
- Rejeu idempotent.
- Tests des montants et devises.
- Tests de sauvegarde et restauration avec données synthétiques.
- Vérification des scripts SQL et de leurs empreintes.
- Tests d'accessibilité clavier et contrastes.

Préparer les scénarios de recette réelle :

Tauri → HTTPS → MMV.Api → PostgreSQL 18 → Ollama `qwen3.5:4b`.

Si 0009 n'est pas encore appliquée aux bases autorisées, ne pas déclarer les tests qui en dépendent comme PASS.

## 5. Interface utilisateur

Conserver les cinq étapes et le design actuel.

Vérifier particulièrement :

- Affichage du modèle Qwen réellement confirmé.
- Analyse asynchrone avec activité réelle.
- Corrections sans perte d'état.
- Historique repliable.
- Confirmations commerciales explicites.
- Distinction entre montants lisibles et valeurs non interprétables.
- Navigation clavier.
- Contrastes clair/sombre.

Ne pas remplacer les tests natifs par des captures de navigateur.

## 6. Livraison unique

Produire un rapport final regroupant :

1. Correctifs réalisés.
2. Fichiers modifiés.
3. Tests et résultats.
4. Dossier SQL définitif.
5. Contrôles sur les bases persistantes en lecture seule.
6. Tests encore bloqués.
7. Risques résiduels.
8. Décisions exigées du propriétaire.

Générer un seul ZIP avec `git archive` et un index Git temporaire isolé si nécessaire.

Nom :

`MMV-V2-CSVAI-POST-AUDIT.zip`

Inclure les fichiers complets nécessaires à l'audit, les tests, le dossier SQL, les preuves essentielles et le rapport unique.

Fournir le SHA-256 de l'archive et l'inventaire des fichiers.

Ne pas recopier inutilement toutes les preuves historiques.

Aucun commit, push, PR ou merge.

Ne pas commencer la Phase 06.

## 7. Arrêt obligatoire

Exécute en un seul cycle toutes les corrections et vérifications non destructives réalisables.

Ne demande pas de validation intermédiaire pour les corrections couvertes par ce mandat.

Arrête-toi uniquement devant une opération nécessitant une autorisation explicite, notamment l'application de SQL 0009 sur les bases persistantes.

À la fin, présente le verdict :

`READY_FOR_G05_SQL_0009`

ou

`NO_GO — CORRECTIONS_REQUIRED`

Le statut `READY_FOR_G05_SQL_0009` ne vaut pas autorisation d'application.

**Objectif : finaliser l'import CSV intelligent sans multiplier les lots ni perdre les protections existantes.**
