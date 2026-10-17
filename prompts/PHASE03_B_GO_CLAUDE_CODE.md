# MMV V2 — GO PHASE 03-B — RENDEZ-VOUS

L'audit indépendant du sous-lot 03-A3 est terminé.

VERDICT : GO TECHNIQUE. Aucun défaut bloquant identifié sur le module Clients.

La Phase 03 n'est pas encore terminée.

## 1. Préflight

Reprends le travail sur feat/phase-03-clients-rdv.

Vérifie l'état Git et conserve les commits déjà réalisés. Ne consulte jamais l'ancien MMV.

Lis :
- docs/phase/03/RDV_03B_PREPARATION.md
- docs/phase/03/DECISIONS_G03_METIER.md
- docs/phase/03/STATE_MAP_47.md
- Les références visuelles officielles du Lot 2.

## 2. Clients : conserver et stabiliser

Ne réimplémente pas le module Clients.

Corrige uniquement le bandeau « Client archivé » qui persiste après navigation.

Vérifie la non-régression des permissions et des tests. Ne touche pas aux clients réels du propriétaire.

## 3. Rendez-vous : commencer le développement

Prépare le sous-lot 03-B avec :

- FullCalendar Standard, version compatible et licence vérifiée.
- React, TypeScript, Tailwind CSS 4, shadcn/ui, Radix.
- TanStack Query pour les données serveur.
- React Hook Form et Zod pour les formulaires.
- Navigation React Router existante.
- Accessibilité clavier, y compris la création et le déplacement.

Implémente les vues Jour et Semaine, le panneau détail, les formulaires et les états d'interface présents dans les maquettes.

Ne crée pas de règles métier par simple déduction des maquettes.

## 4. Décisions métier

Les décisions D-03-13 à D-03-17 sont encore PROPOSED.

Présente une proposition consolidée sur :
- Fuseau horaire, durées et horaires.
- Responsable du rendez-vous.
- Conflits et chevauchements.
- Annulation et réactivation.
- Permissions et confidentialité.

Tu peux développer les composants indépendants de ces décisions.

Prépare les modèles, contrats API et migrations proposés, mais n'applique aucun nouveau SQL et ne crée aucun rendez-vous réel avant validation propriétaire.

## 5. Tests et documentation

Exécute les tests pertinents, sans perturber les processus du propriétaire.

DOCUMENTATION GATE obligatoire : synchronise README, spécifications, QA, gates, SOURCE_MAP, ADR et backlog concernés.

Commits locaux autorisés. Aucun push, PR ou merge.

Ne commence pas la Phase 04.

Livrable : interface Rendez-vous initiale, proposition métier consolidée, SQL préparé et rapport court.

PHASE03_B_UI_READY — OWNER_BUSINESS_REVIEW_REQUIRED
