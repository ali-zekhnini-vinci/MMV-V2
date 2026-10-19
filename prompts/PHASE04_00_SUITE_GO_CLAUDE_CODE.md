# MMV V2 — PHASE 04 — SUITE DU CADRAGE ET PRÉPARATION 04-A

Le dossier 04-00 a fait l'objet d'une revue indépendante.

G04-00 : CADRAGE TECHNIQUEMENT ACCEPTABLE.

D-04-01 : APPROVED, décision définitive du propriétaire.

D-04-02 à D-04-09 : conserver leur statut PROPOSED tant que le propriétaire ne les a pas ratifiées.

Avant de figer le modèle de données :

1. Étudier le rattachement historisé des pièces source aux révisions.
2. Garantir la coexistence d'une dernière révision confirmée et d'une rectification en brouillon.
3. Clarifier les événements d'accès aux documents, sans prétendre prouver leur visualisation.
4. Examiner la granularité de optical.read et présenter une matrice de permissions par profil.
5. Prévoir les contrôles de sécurité des fichiers, les limites de stockage et l'impact sur les sauvegardes.

Préparer 04-A avec les composants V2 et le Lot 3 de Claude Design :
- Onglet Dossier optique.
- Choix explicite Ordonnance médicale / Mesure magasin.
- Liste globale et listes par client.
- Formulaires et grille OD/OG sans validations cliniques inventées.
- États vides, erreurs, chargement, accès refusé.
- Affichage des révisions et des pièces sous forme de prototypes.
- Parcours fluide avec retour à la fiche client.

Les 12 états complémentaires peuvent être conçus à partir des composants existants, sous réserve de validation visuelle propriétaire.

Aucun SQL, aucune nouvelle migration, aucune écriture de données de santé et aucune activation réelle du service.

Ne pas utiliser l'ancien dépôt MMV.

Respecter le DOCUMENTATION GATE, les fichiers non suivis du propriétaire et les décisions historiques.

Aucun push, aucune PR, aucun merge sans autorisation.

Présenter le périmètre 04-A réellement indépendant des décisions cliniques et les éventuels points encore bloquants.
