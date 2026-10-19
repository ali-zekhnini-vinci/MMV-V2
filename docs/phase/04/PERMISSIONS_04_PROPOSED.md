# 04 — Permissions du dossier optique (PROPOSED, D-04-05)

**Statut : `PROPOSED`** — aucune permission créée dans le code ni dans la contrainte `ck_user_permissions_permission`. Base existante (lue le 19/10/2026) : [`src/MMV.Domain/Identity/Permissions.cs`](../../../src/MMV.Domain/Identity/Permissions.cs) (`system.admin`, `clients.*`, `appointments.*`, `audit.read`), policies [`src/MMV.Api/Auth/AuthSetup.cs`](../../../src/MMV.Api/Auth/AuthSetup.cs), attribution par `MMV.Maintenance permissions grant|revoke` ([`../03/PROCEDURE_PERMISSIONS_CLIENTS.md`](../03/PROCEDURE_PERMISSIONS_CLIENTS.md)).

## Principes repris de la Phase 03 (acquis)

- Autorisation **serveur** par policy et par opération ; l'interface ne fait que refléter les droits de la session (affichage), jamais les accorder.
- `system.admin` n'implique **aucun** droit métier ni d'audit ; il n'impliquera aucun droit optique.
- Les rôles visuels (profils du Lot 1, « technicien », « vendeur »…) ne confèrent **aucun** droit.
- Aucun droit n'implique un autre : lire un client ne donne pas son dossier optique ; lire le dossier optique ne donne pas son historique.

## Proposition recommandée (option B de D-04-05.a)

| Permission | Opérations | Remarque |
|---|---|---|
| `optical.read` | liste globale, onglet « Dossier optique », détail, révisions et comparaison, des **deux** types | n'ouvre **pas** les pièces source |
| `optical.prescriptions.write` | créer / modifier un brouillon d'ordonnance médicale, confirmer, rectifier (nouvelle révision) | transcription d'un document reçu ; l'utilisateur n'en devient pas le prescripteur |
| `optical.measurements.write` | créer / modifier un brouillon de mesure magasin, confirmer, rectifier | habilitation à mesurer ; le collaborateur désigné comme auteur de la mesure doit la détenir (D-04-03.b) |
| `optical.invalidate` | invalider une ordonnance ou une mesure (motif obligatoire) | aucune suppression physique |
| `optical.documents.read` | lister et afficher les pièces source | chaque affichage journalisé (`document.viewed`, D-04-05.d) |
| `optical.documents.write` | joindre une pièce, invalider une pièce jointe par erreur | ne permet pas de lire les autres pièces sans `optical.documents.read` |

Option A (cadrage minimal) : `optical.read`, `optical.write` (les deux types), `optical.invalidate`, `optical.document.read`. Plus simple, mais ne distingue pas l'habilitation à mesurer de la transcription.

## Combinaisons exigées par le serveur (proposées)

| Opération | Droits exigés |
|---|---|
| Lister / lire (dossier client, liste globale, détail) | `clients.read` **et** `optical.read` |
| Créer, modifier un brouillon, confirmer, rectifier — ordonnance | `clients.read`, `optical.read`, `optical.prescriptions.write` |
| Idem — mesure magasin | `clients.read`, `optical.read`, `optical.measurements.write` |
| Invalider | `clients.read`, `optical.read`, `optical.invalidate` |
| Afficher une pièce | `clients.read`, `optical.read`, `optical.documents.read` |
| Joindre une pièce | `clients.read`, `optical.read`, `optical.documents.write` |
| Voir les événements optiques dans « Historique » | `audit.read` **et** `optical.read` (sinon filtrés par le serveur, D-04-05.c) |

Le `403` ne révèle jamais l'existence d'un enregistrement : un identifiant inconnu et un identifiant non autorisé produisent la même réponse pour un compte sans `optical.read` (O-04-15).

## Matrice d'affichage (interface, dérivée des droits — jamais une autorisation)

| Élément | Condition d'affichage |
|---|---|
| Onglet « Dossier optique » de la fiche | `optical.read` (aujourd'hui l'onglet reporté « Ordonnances » est visible de tous : à remplacer) |
| Menu « Dossiers optiques » | `optical.read` (sinon entrée désactivée « accès non autorisé », pas « phase suivante ») |
| « Nouvelle ordonnance médicale » | `optical.prescriptions.write` et client actif |
| « Nouvelle mesure magasin » | `optical.measurements.write` et client actif |
| « Rectifier » / « Confirmer » | droit d'écriture du type de l'enregistrement |
| « Invalider » (menu secondaire) | `optical.invalidate` |
| Pièces source (liste, aperçu) | `optical.documents.read` ; sans ce droit : « N pièce(s) — accès non autorisé », sans nom de fichier |
| « Préparer une vente » | toujours désactivée en 04 (D-04-08.d) |

## Rôles visuels — aucune attribution par défaut

À titre **indicatif** pour le propriétaire (aucune règle) : un opticien diplômé pourrait détenir l'ensemble ; un collaborateur d'accueil aucun droit optique ; un technicien atelier `optical.read` seulement si son travail l'exige. Décision individuelle, compte par compte (D-04-05.e).

## Mise en œuvre prévue (04-B, après GO)

- Ajout des constantes dans `Permissions.All` et `Permissions.Grantable`, policies dédiées dans `AuthSetup`.
- Remplacement de la contrainte `ck_user_permissions_permission` par `DROP CONSTRAINT` + `ADD CONSTRAINT` dans la même transaction (procédé accepté pour 0004 / 0005, O-03-24) — migration proposée `0006` ([`DATA_MODEL_PROPOSED.md`](DATA_MODEL_PROPOSED.md) § 6).
- Tests : 401 anonyme, 403 pour chaque droit manquant (dont `system.admin` seul, `clients.*` seul, `audit.read` seul), 200 avec la combinaison exacte ; refus direct d'API, recherche, liste globale, pièce ; filtrage des événements optiques de l'historique ([`QA_MATRIX.md`](QA_MATRIX.md)).

## Amendement daté — 19/10/2026 : granularité d'`optical.read` et matrice par profil (D-04-13, `PROPOSED`)

Analyse : [`ETUDES_PRE_MODELE_04.md`](ETUDES_PRE_MODELE_04.md) E-04. Recommandation : **`optical.read` unique** (deux types, liste globale, révisions et brouillons, métadonnées des pièces) ; aucun découpage par type (un dossier partiel serait trompeur), aucun droit `optical.browse` en 04 ; contenu des pièces sous `optical.documents.read` (inchangé). Les sections précédentes sont conservées.

### Matrice indicative par profil fonctionnel

Un profil décrit une **fonction exercée au magasin**, pas un rôle MMV : il n'existe **aucun rôle**, aucune attribution automatique ni par défaut (D-04-05.e) ; chaque droit est accordé compte par compte par `MMV.Maintenance permissions grant`. Les libellés visuels du menu profil n'ont aucun effet. `●` suggéré si la fonction l'exige ; `○` selon la fonction exacte du collaborateur ; `—` non suggéré.

| Profil fonctionnel (indicatif) | `clients.read` | `optical.read` | `optical.prescriptions.write` | `optical.measurements.write` | `optical.invalidate` | `optical.documents.read` | `optical.documents.write` | `audit.read` |
|---|---|---|---|---|---|---|---|---|
| Administrateur système (technique seulement) | — | — | — | — | — | — | — | — |
| Responsable du magasin | ● | ● | ○ | ○ | ● | ● | ○ | ● |
| Opticien habilité à mesurer | ● | ● | ● | ● | ○ | ● | ● | ○ |
| Collaborateur chargé de la transcription | ● | ● | ● | — | — | ● | ● | — |
| Accueil / prise de rendez-vous | ● | — | — | — | — | — | — | — |
| Technicien atelier | ○ | ○ | — | — | — | — | — | — |

Notes : (1) `system.admin` n'implique rien ; un administrateur qui exerce aussi une fonction optique reçoit les droits de cette fonction explicitement. (2) `optical.measurements.write` suppose l'habilitation à mesurer (D-04-03.b) ; sa vérification est de la responsabilité du propriétaire. (3) Le technicien atelier ne reçoit `optical.read` que si son travail exige la lecture des valeurs avant le module Commandes ; il ne reçoit jamais les pièces. (4) Voir les événements optiques de l'historique exige `audit.read` **et** `optical.read`.

### Capacités résultantes (contrôlées par le serveur)

| Capacité | Combinaison exigée |
|---|---|
| voir l'onglet, la liste globale, le détail, les révisions et les brouillons | `clients.read` + `optical.read` |
| créer, poursuivre, confirmer, rectifier, abandonner une rectification — ordonnance | + `optical.prescriptions.write` |
| idem — mesure magasin | + `optical.measurements.write` |
| invalider un enregistrement | + `optical.invalidate` |
| recevoir le contenu d'une pièce, voir son nom d'origine | + `optical.documents.read` |
| joindre une pièce, la lier à un brouillon, l'invalider | + `optical.documents.write` (et le droit d'écriture du type pour la lier à un brouillon) |
| voir les événements optiques de l'historique | `audit.read` + `optical.read` |

*Précision (19/10/2026) :* la « matrice d'affichage » ci-dessus vaut à partir de l'activation réelle (04-C). Pendant 04-A / 04-B, les sessions sans `optical.read` (tous les comptes réels) conservent l'onglet reporté et l'entrée `later` actuels (O-04-16, [`PREPARATION_04A.md`](PREPARATION_04A.md) § 2).

## Amendement daté — 19/10/2026 (04-A)

Aucune permission créée ni attribuée. Le banc 04-A affiche le prototype selon des **profils simulés** (`apps/desktop/src/bench/benchProfiles.ts`) recopiant la matrice indicative ci-dessus, lus par le seul double en mémoire ; l'application réelle ignore ces noms (une session qui les porterait garde l'onglet reporté et l'entrée `later`, test `src/optical/realAppIsolation.test.tsx`). La matrice reste à valider compte par compte selon les profils réels du magasin (D-04-13.c), sans déduire un accès universel.
