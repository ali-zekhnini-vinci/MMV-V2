# MMV V2 — Phase 00 : cadrage initial neuf

**Date :** 15 octobre 2026  
**Statut :** `PHASE_00_STARTED / OWNER_REVIEW_REQUIRED`  
**Effet :** documents de décision uniquement ; aucune initialisation du logiciel, aucun audit de l'ancien dépôt.

## Autorité des sources

1. Dernières décisions explicites du propriétaire.
2. Cahier des charges MMV V2 neuf, validé progressivement.
3. Maquettes Claude Design et handoffs : sources de **design** et d'hypothèses fonctionnelles, non contrats métier approuvés.
4. ADR MMV V2 ratifiés et documentation officielle.
5. Code du **nouveau** dépôt une fois créé ; ce code ne valide pas à lui seul une règle métier.

### Décisions acquises

- `D-000 APPROVED` : reconstruire MMV comme un produit **entièrement neuf**. Ne jamais consulter/réutiliser ancien code, ancien dépôt, règles, données ou migration. Pas de mapping legacy.
- `D-003 APPROVED` : **PostgreSQL est le seul SGBD**, partout, tests compris. Aucun SQLite ou remplacement « temporaire ».
- `D-004 APPROVED` : client/serveur réel, option **Serveur MMV** ou **Poste de travail MMV** et mode serveur+client pour une seule machine.
- `D-005 APPROVED` : **153 nouvelles vues et 23 compléments** au backlog des phases ultérieures, sans blocage automatique de la fondation technique. Les fonctionnalités critiques sous-jacentes ne peuvent être omises.
- `D-006 APPROVED` : aucun codage ni projet initialisé avant validation explicite de la phase 00.

### Contenu

- `docs/phase/00.md` : mission de phase 00, IN/OUT, portes de contrôle.
- `docs/ARCHITECTURE_ET_STACK.md` : architecture et comparaison des technologies.
- `docs/INSTALLATION_RESEAU_SECURITE.md` : parcours serveur/poste, TLS, auth, sauvegardes.
- `docs/ADR_ET_DECISIONS.md` : registre avec statuts distincts.
- `docs/PERIMETRE_ET_SEQUENCE.md` : découpage MVP, phases et backlog.
- `docs/PLAN_POC_ET_PREUVES.md` : tests de faisabilité obligatoires.
- `registres/DESIGN_SOURCES_SHA256.csv` : six sources de design principales, seules autorisées pour la traçabilité visuelle.
- `prompts/PHASE00_PREFLIGHT_ENVIRONNEMENT_AUDIT_ONLY.md` : audit facultatif de l'environnement Windows **sans accès à l'ancien dépôt**.

## Comment utiliser ces fichiers

Ce pack est un **addendum de remplacement pour la phase 00** du dossier maître du 14 octobre. Plusieurs documents originaux font référence au passé technique de MMV : ils sont **obsolètes** pour la phase 00 V2 et **ne doivent pas être envoyés tels quels à Claude Code**. Conserver le ZIP original exclusivement comme archive visuelle ; travailler à partir du présent dossier et des originaux `.dc.html` de design.

**Sortie de phase 00 :** décision du propriétaire après examen des ADR ouverts et des risques. Le GO actuel porte uniquement sur l'étude de phase 00, pas sur la phase 01.
