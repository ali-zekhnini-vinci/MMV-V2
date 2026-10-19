MMV V2 — GO OFFICIEL PHASE 04-A — INTERFACES DOSSIER OPTIQUE
Date de décision : 19 octobre 2026
Statut : G04-A APPROVED — UI ONLY.
Branche : feat/phase-04-00-cadrage (vérifier l'état Git et les commits présents avant tout changement).
Ce mandat n'approuve PAS D-04-02 à D-04-14 et n'autorise AUCUNE migration ni persistance de données optiques. D-04-01 est APPROVED et définit deux catégories distinctes : ordonnance médicale reçue et mesure magasin.
1. Préflight
- Lire intégralement docs/phase/04/, y compris PREPARATION_04A.md, ETUDES_PRE_MODELE_04.md, STATE_MAP_36.md, DECISIONS_04_METIER_PROPOSED.md, PARCOURS_04.md, DATA_MODEL_PROPOSED.md, PERMISSIONS_04_PROPOSED.md et SOURCE_MAP.md.
- Vérifier le HEAD local (dernier rapport : 1d18b81), l'ascendance depuis origin/main et l'absence de modifications non autorisées ; ne pas réécrire l'historique.
- Utiliser la maquette Lot 3 authentifiée et les références MMV V2 ; ne jamais consulter ni réutiliser l'ancien dépôt MMV. Les annotations de la maquette provenant de l'ancien projet ne sont pas des règles.
- Respecter tous les fichiers non suivis, archives ZIP, captures, desktop.zip, desktop.recovered.zip, dossiers references, etc. Ne jamais lancer de transformation globale de texte sur une arborescence contenant des binaires.
2. Décision O-04-16 — activation
APPROVED : conserver pour les comptes réels l'entrée actuelle « Ordonnances — phase ultérieure », et ne pas exposer de route fonctionnelle optique, de fausse fiche ou de faux historique dans les sessions réelles avant l'activation autorisée des API et du schéma. Les prototypes sont accessibles exclusivement dans un banc de démonstration/test isolé, explicitement étiqueté comme tel, sans contourner l'authentification ni les permissions réelles. Toute navigation directe hors du banc doit rester bloquée.
3. Implémentation UI autorisée
Développer réellement et tester les composants React/TypeScript du Lot 3, dans la stack actuelle (Tauri 2, Vite, Tailwind 4, shadcn/ui/Radix, Lucide, React Router, TanStack Query/Table lorsque pertinent, React Hook Form/Zod, Vitest/Testing Library). Ne pas ajouter de framework sans besoin justifié.

Livrables :
- Onglet « Dossier optique » dans les états du prototype de fiche client ; deux sections distinctes « Ordonnances médicales » et « Mesures magasin ».
- Liste globale et liste par client, recherche/tri/filtrage, état vide, aucun résultat, chargement, erreur et accès refusé.
- Choix explicite du type avant la saisie ; formulaire OD/OG neutre à saisie contrôlée sans valeurs silencieusement normalisées, arrondies ou converties ; état « non renseigné » clairement distingué de la valeur numérique zéro.
- Écrans de détails et états de brouillon/confirmation/rectification/version/invalidation comme prototypes réversibles uniquement, clairement identifiés comme reposant sur des décisions PROPOSED.
- Prototype de rattachement des pièces aux révisions ; représentation d'une révision confirmée toujours accessible malgré un brouillon plus récent ; aperçus de pièces uniquement synthétiques et locaux au banc de test.
- Parcours depuis une fiche client, conservation du contexte et retour à l'onglet ; action « Préparer une vente » visible, inerte et étiquetée « Phase ultérieure ».
- Écrans accessibles au clavier, thèmes clair/sombre, tailles normales/réduites, zoom 100/125/150 %.
- Éviter les doublons de composants avec Clients et Rendez-vous, et respecter les conventions de la Phase 03.
Couverture à tracer : 36 états du Lot 3, 12 états complémentaires répertoriés en 04-00, puis 2 états complémentaires issus des études. Distinguer les états réalisés, adaptés, représentés dans un autre composant, et en attente d'un avis métier. Ne pas inventer de règles cliniques pour atteindre artificiellement 100 %.
4. Limites strictes
- Aucune table optical, aucune migration 0006, aucun SQL à appliquer, aucune écriture sur PostgreSQL 18 ou 17.
- Aucune API optique réellement activée, aucune permission optical.* attribuée, aucune modification des comptes réels.
- Aucune donnée de santé réelle dans le dépôt, dans les fixtures ou dans les captures ; n'utiliser que des identités et valeurs de démonstration explicitement fictives.
- Aucune validation clinique bloquante, aucune borne ou pas de saisie clinique imposé, aucune normalisation automatique 0°→180°, aucun calcul implicite de validité d'ordonnance, aucune conversion mesure magasin → ordonnance.
- Ne pas choisir des règles de prescripteur, statut légal, prise en charge INAMI/AMO, durées de conservation ou détails d'habilitation encore PROPOSED.
- Ne pas commencer le backend du module optique, les ventes, les fournisseurs ou la Phase 05.
5. Points de conception à ne pas perdre
D-04-10 à D-04-14 demeurent PROPOSED ; leurs recommandations peuvent guider le prototype uniquement, sans devenir des décisions. Les points suivants doivent rester visibles pour 04-B :
- Une pièce source stockée une fois et historisée par liaison avec chaque révision confirmée (D-04-10).
- Dernière révision confirmée distincte du brouillon de rectification (D-04-11).
- Ne jamais présenter document.content_served comme preuve que l'humain a vu la pièce ; distinguer demande autorisée, début de transmission et réception non prouvée (D-04-12).
- Évaluer les permissions selon les profils réels sans déduire un accès universel aux données optiques (D-04-13).
- Les signatures de fichiers seules ne suffisent pas à la sécurité : scanner, parseurs, CSP et antivirus avant un pilote ; valider le streaming multipart sans fichiers temporaires involontaires (D-04-14).
- Préparer un protocole de sauvegarde/restauration éprouvé avant un pilote ; aucune promesse de conformité juridique ou de sécurité non vérifiée.
6. Qualité, preuves et documentation
- Exécuter typage, lint, tests frontend, build, et les non-régressions API/PostgreSQL pertinentes quand les modifications l'exigent ; déclarer précisément PASS, FAIL, NOT_RERUN, NOT_TESTED.
- Présenter de vraies captures du prototype isolé, clair/sombre et fenêtre réduite, ainsi qu'un état de chacune des catégories et les états de révisions. Distinguer capture navigateur et capture Tauri native ; ne pas prétendre avoir testé l'interface native si seule une simulation navigateur a été faite.
- Livrer un parcours UI démontrable sans accéder aux données réelles de mmv_v2_dev.
- DOCUMENTATION GATE : synchroniser README, docs/phase/04.md, docs/phase/04/* affectés, QA_MATRIX, IMPLEMENTATION_SPEC, SOURCE_MAP, GATES_AND_DECISIONS, ADR et backlog si besoin. Garder décisions et statut historique ; ne pas marquer D-04-02 à D-04-14 APPROVED.
- Prévoir l'examen métier consolidé avec un opticien pour D-04-02/D-04-06 avant de figer les règles et migrations.
7. Livraison
- Commits locaux autorisés sur la branche existante. Aucun push, aucune PR, aucun merge.
- Ne pas créer d'archive énorme si inutile ; préparer un git archive léger pour l'audit, avec captures et tests pertinents.
- Livrer un bilan clair : états réalisés, captures, parcours, tests, limites et questions métier véritablement bloquantes pour 04-B.
- Éviter les demandes de validation intermédiaires sur des choix purement visuels réversibles.
G04-A : GO DÉVELOPPEMENT FRONTEND — PHASE04_A_UI_READY / OWNER_VISUAL_REVIEW_REQUIRED
