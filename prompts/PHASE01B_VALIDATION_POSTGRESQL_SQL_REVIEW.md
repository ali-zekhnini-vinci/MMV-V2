# MMV V2 — PHASE 01B — VALIDATION POSTGRESQL

Les tests de connexion PostgreSQL ont été réalisés manuellement par le propriétaire et ont réussi.

**O-B01 — PASS :**
- PostgreSQL 18.6, port 5433.
- `mmv_v2_dev` : authentification réussie sur `mmv_v2_dev`.
- `mmv_v2_test` : authentification réussie sur `mmv_v2_test`.
- Les deux rôles ne disposent pas de privilèges administrateur globaux.
- Les privilèges PUBLIC ont été révoqués sur les deux bases.

**O-B03 — PASS pour la confiance du certificat local :**
- Certificat HTTPS `localhost` approuvé et vérifié par Windows.
- Empreinte : `6C48AE5E0BD7B0E67032257F893C94EC27CC3252`.
- La connexion HTTPS réelle de MMV.Api reste à tester.

## Instructions

Reprends la Phase 01B à partir de son état actuel, sans recommencer le GATE 0 ni recréer les bases.

1. Prépare les chaînes de connexion via .NET User Secrets. Fournis les instructions permettant au propriétaire de saisir les mots de passe localement, sans les afficher dans les commandes, logs ou fichiers Git.
2. Vérifie les identités des bases ciblées avant toute opération d'écriture.
3. Prépare la migration EF Core initiale vide et génère son script SQL complet pour revue.
4. Continue les travaux autorisés sur `/health/ready`, le client API TypeScript, la configuration CSP/CORS et les tests sans écriture sur PostgreSQL.
5. Utilise Serena C#/TypeScript et Graphify CLI, avec preuves réelles.
6. Synchronise systématiquement README, spécifications, QA, décisions, SOURCE_MAP et documentation de phase.

**Interdictions :**
- Ne pas appliquer de migration avant mon GO spécifique sur le script SQL.
- Ne pas effectuer de tests d'écriture en base avant mon GO O-B02.
- Ne jamais toucher à PostgreSQL 17 ou aux autres bases.
- Aucun secret dans Git.
- Aucun push, merge ou PR.
- Aucune fonctionnalité métier.

Arrête-toi lorsque le script SQL est prêt et présente-le avec son analyse d'impact.

Termine par :

`PHASE01B_SQL_REVIEW — OWNER_APPROVAL_REQUIRED`
