# MMV V2 — Mandat Claude Code : PHASE 04-00 (préflight et cadrage)

La Phase 03 est fusionnée sur `main` selon le propriétaire. **Ne présume pas du SHA du merge.**

## Décision métier déjà ratifiée (ne pas la redemander)
**D-04-01 — APPROVED le 19/10/2026, par le propriétaire :** MMV V2 conserve **les ordonnances médicales reçues** et **les mesures réalisées en magasin** comme **deux types métier distincts**, séparés dans le dossier optique et non interchangeables. Une mesure magasin ne devient jamais une ordonnance médicale par simple saisie, conversion automatique ou affichage. La provenance, l'auteur et le statut des deux catégories doivent rester explicites. Le détail des champs, les habilitations et les règles cliniques restent à ratifier dans D-04-02 à D-04-09. Cette décision n'autorise aucun SQL.

## Objectif
Préparer officiellement la Phase 04 (Ordonnances optiques / dossier optique) sans inventer les règles métier, les conditions réglementaires ni des champs médicaux. Ne pas coder de migrations ni commencer le module de Ventes.

1. Sur le dépôt neuf MMV V2, `git status`, `git fetch`, vérifier `origin/main` et l'inclusion de `93df475`, vérifier la propreté et préserver absolument tous les fichiers non suivis (dont `desktop.zip`/`desktop.recovered.zip`). Créer une branche dédiée depuis `main` une fois la situation connue. Le propriétaire réalisera la PR et le merge.
2. Lire `CLAUDE.md`, l'architecture, les ADR, Phase 03 et son journal `audit.events`, les APIs/permissions Clients et Rendez-vous.
3. Lire le cadrage joint `references/phase04/CADRAGE_OFFICIEL_PHASE04_ORDONNANCES.md` et le catalogue Lot 3 (17 groupes, 36 états). **Rechercher la maquette source originale** `MMV-Lot3-Ordonnances.dc.html`, SHA-256 attendu `c103df9e434245cdcba5b6246c5ec3032301e323c6ec1797826deaf8fd06c213`. Si absente, rapporter le manque et ne pas prétendre l'avoir examinée ; ne pas utiliser l'ancien code même si le HTML le cite.
4. Produire `docs/phase/04/STATE_MAP_36.md`, `DECISIONS_04_METIER_PROPOSED.md`, `PARCOURS_04.md`, `DATA_MODEL_PROPOSED.md`, `API_OPTICAL_PROPOSED.md`, `PERMISSIONS_04_PROPOSED.md`, `QA_MATRIX.md`, `SOURCE_MAP.md`, `GATES_AND_DECISIONS.md`, `IMPLEMENTATION_SPEC.md` avec la granularité appropriée. Identifier les impacts sur le futur module Vente/prise en charge et **appliquer la décision D-04-01 APPROVED** : document médical reçu, transcription et mesures faites en magasin sont distingués. Préparer la coexistence des deux types dans le dossier optique ; ne pas déduire leur schéma exact et ne pas faire valoir une mesure magasin comme prescription ouvrant droit à remboursement.
5. **Ne pas reposer D-04-01 au propriétaire** ; ne soumettre que D-04-02 à D-04-09, regroupées. Proposer précisément les validations OD/OG/ADD/axe/prisme/base, la provenance, la gestion partielle, l'archivage/invalidation, les versions et le traitement des documents sensibles **comme décisions à ratifier** et non des règles déjà approuvées. Prévoir les spécificités Maroc/Belgique sans déduire automatiquement les droits INAMI ou AMO.
6. Identifier les composants UI déjà disponibles et la stratégie d'intégration sans installer de nouveaux frameworks injustifiés. Préparer l'enchaînement « fiche client → ajout/consultation ordonnance → préparer vente », avec respect du besoin métier et des permissions serveur.
7. Vérifier la sécurité : permissions propres au dossier optique, accès aux fichiers sécurisés, audit des écritures atomique, absence de valeurs médicales dans `audit.events`, aucune fuite dans les logs, pas de lien document public.
8. Mettre à jour la documentation affectée (DOCUMENTATION GATE), préserver les états et décisions historiques.

## Périmètre et interdits
- **Documentation et cadrage 04-00 uniquement.** Maquette locale possible pour vérifier les composants, mais pas de faux module déclaré opérationnel.
- Aucun changement des tables ou migrations existantes, aucun SQL exécuté, aucun write dans `mmv_v2_dev` ou `mmv_v2_test`, aucune donnée médicale fictive hors tests isolés.
- Aucun accès à PostgreSQL 17 ni à l'ancien dépôt MMV.
- Aucun push, PR ou merge. Commits locaux des documents autorisés ; aucun fichier propriétaire non suivi touché.
- Ne pas interrompre les processus du propriétaire.
- Pas de phase 04-A ou 04-B avant les décisions métier consolidées.

## Livraison
Rapport court : état du merge réellement vérifié, couverture des 36 états, décisions à valider **en une seule demande consolidée**, risques majeurs, fichiers créés, tests/contrôles exécutés et statut documentaire. Archive git archive légère si nécessaire.

**PHASE04_00_READY — OWNER_METIER_REVIEW_REQUIRED**
