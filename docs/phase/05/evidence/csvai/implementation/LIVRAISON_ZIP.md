# CSV-IMPORT-AI-01 — constitution de l'archive d'audit (règle du 06/11/2026)

Archive : `MMV-V2-CSVAI-IMPLEMENTATION.zip`, placée hors du dépôt (`C:\Users\Ali Zekhnini\Documents\MMV-V2-AUDITS\`), produite **exclusivement par `git archive`** :

```bash
export GIT_INDEX_FILE=<fichier temporaire>      # index ISOLÉ : l'index normal du dépôt n'est jamais touché
git read-tree --empty                            # arbre de départ vide : seuls les fichiers listés entrent
git add -- <liste explicite des fichiers du lot> # contenu tel que Git le stocke (fins de ligne LF, attributs du dépôt)
git write-tree                                   # → SHA de l'arbre exporté
git archive --format=zip --output=MMV-V2-CSVAI-IMPLEMENTATION.zip <arbre>
```

- Aucun commit créé ; index normal et worktree contrôlés **avant et après** (empreinte de `git status --porcelain` et de `.git/index`).
- Contenu : code source créé ou modifié, tests et fixtures du lot, migration et scripts SQL 0009, documents de conception, de décisions et de suivi actualisés, mandats archivés du lot, rapport unique [`../../../LIVRAISON_05_CSVAI.md`](../../../LIVRAISON_05_CSVAI.md), synthèses de tests, essais SQL, résultats Ollama réel, 12 captures (pleine taille) avec `plan.json` et `report.json`.
- Exclus : `.env*` (dont `apps/desktop/.env.development`), secrets, bases, sauvegardes, `node_modules`, `bin`, `obj`, archives du propriétaire, fichiers temporaires, `.claude/`, `.serena/`, `references/`, preuves antérieures.
- Preuves antérieures **référencées, non réincluses** (empreintes au 06/11/2026) : `docs/phase/05/evidence/csvai/corpus/MANIFEST.json` `301f2150c168afc7e74605619d447f48160201f54fa0bb85c4032a507b08b7e0` ; `docs/phase/05/evidence/csvai/b0/corpus/MANIFEST_B0.json` `2430738cbed3feb706affc629fbcdde21c3bfd66f7413018cae457072c26b623` ; `docs/phase/05/evidence/csvai/gateb/B0_FREEZE_SHA256_20261104.txt` `b69728ea64fb6e07d817626113bbd9dcfbcc699ce79a9a054901bee311da3de6` ; `docs/phase/05/evidence/csvai/gateb/captures/report.json` `0c3b16fa65e0f9059ecab8a74101202281432f41e5ba6d68654b7b0bf0d45d1b` ; archive Gate B `MMV-V2-CSVAI-GATE-B-20261106.zip` `cf4de23452dc8df48c96a2cb9e055683c4cc3fd3f743f63efc6bb8588043c074`.
- Les empreintes de l'archive et de l'arbre exporté ne peuvent pas figurer dans l'archive elle-même : elles sont fournies **séparément** (fichier annexe `MMV-V2-CSVAI-IMPLEMENTATION.zip.sha256`, inventaire `MMV-V2-CSVAI-IMPLEMENTATION.inventory.txt` et message de livraison).
