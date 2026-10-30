# references/phase05 — Lot 6 Catalogue, Stock, Fournisseurs (copie de travail)

Créé le 30/10/2026 (clôture du cadrage 05-00) comme copie de travail non versionnée, l'empreinte de chaque fichier étant reportée dans `docs/phase/05/SOURCE_MAP.md`. **Mise à jour du 30/10/2026 (mandat d'implémentation, § 3)** : dossier **partiellement versionné** — voir § « Versionnement » (la maquette HTML et ses captures restent hors Git, avec procédure de récupération).

## Statut

- Maquette **`DRAFT_NON_APPROVED`** (registre `registres/DESIGN_SOURCES_SHA256.csv`).
- **Référence visuelle et ergonomique uniquement** (D-05-01 `APPROVED`). Le Lot 6 a été dessiné à partir d'éléments de l'ancien logiciel ; son en-tête le déclare (O-05-01). Aucune liste de champs, énumération, règle, nom d'entité ou de DTO n'en est reprise : le modèle métier V2 dérive exclusivement des décisions approuvées (`docs/phase/05/DECISIONS_05_METIER_PROPOSED.md`) et de l'architecture MMV V2.
- L'ancien dépôt MMV, son code, ses DTO, son modèle de données et ses migrations ne sont ni consultés ni réutilisés.

## Contenu

| Chemin | Origine | Rôle |
|---|---|---|
| `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` | `Downloads/MMV initialisation projet V1/` (dossier du propriétaire), copie octet pour octet | maquette Lot 6 : 37 groupes, 107 états + notes de conception |
| `lot6/support.js` | même dossier | chargeur de la maquette (`./support.js`) ; identique à `references/phase03/lot2/support.js` |
| `lot6/_ds/mmv-managemyvision-design-system-90800da0-…/` (`styles.css`, `_ds_bundle.js`, `tokens/*.css` × 7, `assets/logo-dark.png`, `assets/logo-horizontal.png`) | même dossier, `_ds/` | design system chargé par la maquette ; **seuls les fichiers effectivement référencés** sont copiés (pas `readme.md`, `_ds_manifest.json`, `_adherence.oxlintrc.json`, `logo-monogram.png`) ; identiques aux copies de `references/phase03/lot2/_ds/` |
| `lot6/scraps/lot6-check.png`, `lot6/scraps/lot6-s1.png` | même dossier, `scraps/` | captures de contrôle de l'outil de conception (en-tête du Lot 6, liste des produits) ; identiques aux entrées de `MMV.zip` |
| `INDEX_LOT6.md` | généré | groupes, écrans, états, `data-mount`, module V2 indicatif |
| `SHA256SUMS.txt` | généré | manifeste SHA-256 des 15 fichiers de `lot6/` (format `sha256sum -c`) |

Non copiés : `MMV.zip` (3,6 Mo, archive regroupant les Lots 1 à 6 — doublon volumineux ; ses entrées Lot 6 ont la même empreinte que les fichiers copiés), `scraps/check-list.png` (non rattaché au Lot 6), maquettes des autres lots. Les registres `MMV_V2_Registre_134_Groupes.csv` / `MMV_V2_Registre_324_Etats.csv` restent dans `references/phase04/catalogue/` (non dupliqués ; l'index en extrait les lignes Lot 6). Les références des Phases 00 à 04 ne sont pas modifiées.

## Ce que le Lot 6 ne contient pas

Les états V2 sans planche (GAP-05-01 à GAP-05-09 du cadrage du 29/10/2026, puis GAP-05-10 et suivants issus de la validation métier) ne sont **pas fabriqués** : ils seront composés à partir des composants V2 existants et soumis à validation visuelle (G05-DESIGN). Le Lot 6 ne dessine ni variantes, ni coefficient multiplicateur, ni magasin / devise par magasin, ni catalogue des verres configurable, ni import CSV.

## Consultation

Servir le dossier `lot6/` par un serveur statique local hors dépôt (procédé de la Phase 04) et ouvrir le fichier `.dc.html`. La maquette charge depuis Internet React 18.3.1, Babel standalone 7.29.0 et Lucide 0.544.0 (unpkg.com) ainsi que les polices Google Fonts : hors ligne, le rendu est incomplet.

## Versionnement (mise à jour du 30/10/2026, mandat d'implémentation § 3)

Revue de sécurité avant versionnement (aucun code exécuté) : aucun bloc de code de l'ancien logiciel, aucun secret, aucune chaîne de connexion. En revanche, `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` **cite des identifiants de l'ancien logiciel** (noms de cas d'usage, de commandes et de DTO, notes de conception) et **contient des coordonnées de fournisseurs** (e-mails sur des domaines `.ma`, dont un nom de marque réelle, et numéros de téléphone marocains) dont le caractère fictif n'est pas établi ; les deux captures `lot6/scraps/*.png` reproduisent la liste. Ces trois fichiers ne sont donc **pas versionnés** (`references/phase05/.gitignore`).

| Versionné | Non versionné (récupération ci-dessous) |
|---|---|
| `README.md`, `INDEX_LOT6.md`, `SHA256SUMS.txt`, `.gitignore`, `lot6/support.js` (chargeur), `lot6/_ds/…` (design system MMV : jetons, feuille de style, bundle, logos MMV) | `lot6/MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html`, `lot6/scraps/lot6-check.png`, `lot6/scraps/lot6-s1.png` |

**Récupération après un nouveau clone** : copier, depuis le dossier du propriétaire `Downloads/MMV initialisation projet V1/`, le fichier `MMV-Lot6-Catalogue-Stock-Fournisseurs.dc.html` dans `references/phase05/lot6/` (et, au besoin, `scraps/lot6-check.png`, `scraps/lot6-s1.png` dans `references/phase05/lot6/scraps/`), puis vérifier l'intégrité par `sha256sum -c SHA256SUMS.txt` : les 15 empreintes doivent être `OK`. Sans la maquette, l'index `INDEX_LOT6.md` et la matrice `docs/phase/05/MATRICE_LOT6.md` restent consultables ; le rendu de la maquette exige le fichier récupéré. Une version expurgée (coordonnées et identifiants hérités retirés) pourrait être versionnée sur décision du propriétaire ; elle aurait une empreinte différente de l'original.

## Intégrité

```sh
cd references/phase05 && sha256sum -c SHA256SUMS.txt
```

Empreinte de la maquette : `6ca2cec033b8aa35b4ec4188c2bc95ab3cd5aaaa8c469e82a4a7e04b8bbc35e7` (175 994 octets), identique au registre `registres/DESIGN_SOURCES_SHA256.csv` et à l'original du propriétaire.
