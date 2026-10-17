# 03 — Procédure : attribuer les permissions Clients au compte du propriétaire (03-A3, 17/10/2026)

**À exécuter par le propriétaire lui-même**, sur le poste serveur, dans un terminal interactif. L'agent **n'a modifié aucun compte réel** : `permissions list` montre aujourd'hui sur `mmv_v2_dev` un seul compte, actif, avec `system.admin` seul (`evidence/P03A3_maintenance_permissions_readonly_20261017T1110Z.log`).

## Principes

- `system.admin` **n'accorde aucune** permission Clients : sans `clients.read`, l'écran Clients affiche « Accès non autorisé » (403 serveur).
- `clients.read` : consulter, rechercher. `clients.write` : créer, modifier. `clients.archive` : archiver, réactiver. Chacune est contrôlée **par le serveur** à chaque requête (policies `ClientsRead`, `ClientsWrite`, `ClientsArchive`) ; l'interface se contente de masquer (liste) ou désactiver avec motif (fiche) les actions non permises.
- La commande n'agit que sur la cible unique `mmv_v2_dev` / rôle `mmv_v2_dev` / port 5433 / PostgreSQL 18 / boucle locale, contrôlée avant connexion, après connexion et juste avant l'écriture ; elle refuse `system.admin` et toute permission inconnue sans se connecter, et exige une saisie interactive (aucune entrée redirigée).

## Étapes

Depuis la racine du dépôt (`MMV V2`) :

```powershell
# 1. État avant (lecture seule)
dotnet run --project src/MMV.Maintenance -- permissions list

# 2. Attribution (une commande par permission ; confirmer en tapant mmv_v2_dev à l'invite)
dotnet run --project src/MMV.Maintenance -- permissions grant <votre-identifiant> clients.read
dotnet run --project src/MMV.Maintenance -- permissions grant <votre-identifiant> clients.write
dotnet run --project src/MMV.Maintenance -- permissions grant <votre-identifiant> clients.archive

# 3. État après (lecture seule) : « system.admin, clients.archive, clients.read, clients.write » attendu
dotnet run --project src/MMV.Maintenance -- permissions list
```

Puis, dans l'application : **se déconnecter et se reconnecter** (la liste des permissions de la session affichée par l'interface est relue à la connexion ; le serveur, lui, applique la nouvelle permission dès la requête suivante).

Codes de sortie : 0 succès (ou « inchangée » si déjà attribuée) ; 1 usage ou permission non attribuable ; 4 confirmation incorrecte / saisie redirigée ; 5 cible non autorisée ; 6 compte introuvable — dans tous les cas d'erreur, **aucune modification**.

## Retrait

`dotnet run --project src/MMV.Maintenance -- permissions revoke <identifiant> clients.archive` (même confirmation). Retirer `clients.read` rend l'écran Clients inaccessible au compte (403), sans effet sur les données.

## Vérifications déjà faites par l'agent (sans écriture)

| Commande | Résultat |
|---|---|
| `status` | `mmv_v2_dev` (rôle `mmv_v2_dev`, port 5433, PostgreSQL 18.6), cible autorisée, **3 migrations appliquées, 0 en attente**, bootstrap effectué, 1 compte |
| `permissions list` | 1 compte actif : `system.admin` |
| `permissions grant x system.admin` | « Refusé : permission non attribuable… » exit 1, **aucune connexion** |
| `permissions grant compte-inexistant clients.read` (entrée redirigée) | « Refusé : saisie interactive obligatoire » exit 4, **aucune écriture** |

Les chemins `grant`/`revoke` réels sont couverts par `tests/MMV.Maintenance.Tests` (`BootstrapCommandProcessTests`) et `PermissionAdministrationServiceTests` ; les droits `clients.*` eux-mêmes ont été éprouvés de bout en bout sur `mmv_v2_test` avec des comptes éphémères (QA Q-03-86, Q-03-89, Q-03-90).
