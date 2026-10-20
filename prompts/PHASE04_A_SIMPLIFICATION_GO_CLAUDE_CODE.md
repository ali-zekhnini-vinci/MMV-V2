# MMV V2 — Phase 04-A — Simplification du dossier optique

Nouvelle décision du propriétaire concernant les formulaires « Ordonnance médicale » et « Mesure magasin ».

**Je conserve le système de révisions, mais je demande la suppression du champ « État de l'œil » et le report de la gestion des « Pièces sources ».**

Instructions :

1. Retirer « État de l'œil » des formulaires OD et OG, de la validation du prototype et du modèle métier proposé. Les champs optiques peuvent rester individuellement vides, sans être assimilés à zéro.
2. Retirer les interfaces de téléchargement, consultation, invalidation et rattachement des pièces sources aux révisions.
3. Conserver le système de révisions, la comparaison des valeurs, le motif de rectification et la traçabilité des auteurs.
4. Conserver les métadonnées pertinentes de provenance, notamment le prescripteur et la date de prescription pour les ordonnances médicales.
5. Conserver la séparation entre ordonnances médicales et mesures magasin ainsi que le design officiel Lot 3 avec boutons ±.
6. Reporter la gestion documentaire au backlog permanent. Ne pas créer de tables ni d'API pour les pièces sources pendant cette phase.
7. Mettre à jour les décisions proposées, le modèle de données, les API proposées, les tests et la documentation en conservant l'historique des choix.
8. Ne pas modifier les règles cliniques encore ouvertes.

Aucune migration PostgreSQL 0006, aucun backend optique et aucune activation réelle.

Commits locaux uniquement ; aucun push, PR ou merge.

**Objectif : simplifier le parcours quotidien de l'opticien sans compromettre la traçabilité des corrections.**
