# MMV V2 — PHASE 03-C — VALIDATION FONCTIONNELLE FINALE

L'audit indépendant de 03-B3 est terminé.

VERDICT : GO TECHNIQUE. La Phase 03 complète reste en attente de validation fonctionnelle propriétaire.

Reprends la branche feat/phase-03-clients-rdv, sans réimplémenter Clients ni Rendez-vous.

Objectifs :

1. Préparer l'activation guidée de Rendez-vous sur mmv_v2_dev avec les paramètres de démonstration déjà choisis : Europe/Brussels, 09:00–19:00, aucun jour masqué et capacité 1. Ne change pas les paramètres ni les permissions du compte propriétaire sans confirmation.

2. Préparer le parcours manuel complet :
   accueil → recherche/création client → fiche client → prise de rendez-vous → modification → déplacement → annulation/rétablissement → retour à la fiche.

3. Vérifier les deux thèmes, les résolutions 1440×900 et 1920×1080, les zooms 100 %, 125 % et 150 %, le clavier et le glisser-déposer natif.

4. Vérifier le build natif Tauri, l'intégration HTTPS et la non-régression Clients.

5. Conserver les protections de fuseaux horaires. Ne considère pas le risque Casablanca comme définitivement résolu.

6. Examiner O-03-16, le défaut d'affichage du script SQL après COMMIT. Préparer une correction ciblée et ses tests sans réappliquer de migration ni modifier les bases.

7. Documenter les éventuels problèmes constatés pendant les essais du propriétaire, en distinguant les défauts bloquants des améliorations UX.

8. Exécuter les vérifications de régression et le DOCUMENTATION GATE habituel.

Aucune nouvelle migration, aucun nettoyage de données réelles, aucune Phase 04.

Commits locaux autorisés. Aucun push, aucune PR, aucun merge sans GO final.

Présente les éléments nécessaires à la validation humaine avant de déclarer PHASE03_COMPLETE.

PHASE03_C — OWNER_FUNCTIONAL_VALIDATION_REQUIRED
