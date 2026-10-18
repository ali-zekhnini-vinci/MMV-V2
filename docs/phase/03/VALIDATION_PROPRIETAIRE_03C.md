# 03-C — Dossier de validation fonctionnelle du propriétaire

**Statut (18/10/2026) : `PHASE03_C — OWNER_FUNCTIONAL_VALIDATION_REQUIRED`.** Mandat : [`prompts/PHASE03_C_GO_CLAUDE_CODE.md`](../../../prompts/PHASE03_C_GO_CLAUDE_CODE.md). Ce dossier réunit ce que le propriétaire doit exécuter, observer et décider avant toute déclaration `PHASE03_COMPLETE`. Aucune migration, aucune écriture de l'agent sur `mmv_v2_dev`, aucun compte réel modifié par l'agent.

Ce que l'agent a vérifié avant ce dossier (détail : [`QA_MATRIX.md`](QA_MATRIX.md) § 03-C) : régression .NET 314/314 + 50/50, PostgreSQL 18 réel 40/40, frontend 196/196, build release Tauri PASS ; parcours natif complet sur `mmv_v2_test` en clair 1920×1080 / 100 % et en sombre 1440×900 / 150 % (40 + 40 contrôles) ; matrice 2 thèmes × 2 résolutions × 3 zooms (250 contrôles) ; garde-fou de fuseau (56 contrôles) ; HTTPS ; correctif O-03-16 (15/15). Ces preuves portent sur des données **fictives** de `mmv_v2_test` : elles ne remplacent pas l'essai du propriétaire sur sa configuration réelle.

## 1. Activation guidée sur `mmv_v2_dev`

Ordre impératif. Commandes à lancer depuis la racine du dépôt, MMV.Api **arrêté**.

| Étape | Commande / action | Résultat attendu | État (18/10/2026) |
|---|---|---|---|
| 1. Paramètres du magasin (user-secrets `mmv-v2-api`) | `dotnet user-secrets set "Appointments:TimeZone" "Europe/Brussels" --id mmv-v2-api` ; idem `DisplayStartTime` `09:00`, `DisplayEndTime` `19:00`, `ReceptionCapacity` `1` ; **pas** de `HiddenWeekdays` | 4 clés `Appointments:*`, aucune autre clé modifiée | **FAIT par l'agent** sur confirmation du propriétaire (réponse de session : « Write the user-secrets for me ») ; relecture filtrée : 4 clés, valeurs conformes, aucun `HiddenWeekdays` (Q-03-185) |
| 2. Droits de votre compte | `dotnet run --project src/MMV.Maintenance -- permissions grant <votre identifiant> appointments.read` puis `appointments.write`, `appointments.cancel` (confirmation interactive du nom de la base) | `permissions list` : `appointments.read`, `appointments.write`, `appointments.cancel` en plus de `clients.*` | **À FAIRE par le propriétaire** |
| 3. Collaborateurs éligibles | `dotnet run --project src/MMV.Maintenance -- responsibles enable <identifiant>` pour chaque collaborateur pouvant recevoir des rendez-vous (vous-même au minimum pour l'essai) | `responsibles list` : éligibilité « oui » | **À FAIRE par le propriétaire** |
| 4. Contrôle | `dotnet run --project src/MMV.Maintenance -- status` | 4 migrations appliquées, 0 en attente | à faire |
| 5. Démarrage | `dotnet run --project src/MMV.Api --launch-profile https` puis, dans `apps/desktop`, `npm run tauri dev` (relancer **les deux** : contrat élargi en 03-B3, O-03-31) | journal : `Rendez-vous : magasin configuré (fuseau Europe/Brussels, capacité d'accueil 1 …)` | à faire |
| 6. Reconnexion | se déconnecter puis se reconnecter (les droits sont lus à la connexion) | menu Rendez-vous utilisable, « Prendre un rendez-vous » actif sur une fiche | à faire |

**Retour arrière (non destructif) :** `dotnet user-secrets remove "Appointments:TimeZone" --id mmv-v2-api` (et les trois autres clés) → « Agenda non configuré » (503) ; `permissions revoke <identifiant> <droit>` ; `responsibles disable <identifiant>` (retrait logique, jamais de suppression). Procédure de référence : [`PROCEDURE_RENDEZ_VOUS_ACTIVATION.md`](PROCEDURE_RENDEZ_VOUS_ACTIVATION.md).

**Données d'essai sur `mmv_v2_dev` :** tout client ou rendez-vous créé pendant l'essai est une donnée réelle de développement ; l'agent ne la nettoiera pas (GO : aucun nettoyage de données réelles). Conseil : un nom explicite (« Essai ») ; ensuite archiver le client et annuler ses rendez-vous plutôt que supprimer.

## 2. Parcours manuel complet

Cocher OK / KO ; pour tout KO, ouvrir une ligne au § 5 avec la capture.

| # | Étape | Attendu | OK / KO | Remarque |
|---|---|---|---|---|
| P1 | Connexion, **Accueil** | « Bonjour, … » ; barre d'état : serveur joignable, base disponible | | |
| P2 | **Clients** → rechercher le nom du futur client | « Aucun résultat pour … » | | |
| P3 | **Nouveau client** → Prénom, Nom (+ téléphone) → **Ctrl+S** ou Enregistrer | « Client créé », référence `CLI-…` | | |
| P4 | **Voir la fiche** | fiche du client, « Prendre un rendez-vous » actif | | |
| P5 | Retour à la liste → rechercher le client (minuscules, sans accents) | client retrouvé, termes surlignés | | |
| P6 | Fiche → **Prendre un rendez-vous** | formulaire ouvert, client présélectionné, aucune seconde recherche ; mention « Heures du magasin (Europe/Brussels) » | | |
| P7 | Date ouvrée à venir, 10:00, 30 min, motif, responsable → **Créer** | « Rendez-vous créé » ; rendez-vous à 10:00 ; lien « Retour à la fiche de … » | | |
| P8 | Ouvrir le rendez-vous → **Modifier** motif et notes → **Enregistrer** | « Rendez-vous modifié » ; « Dernière modification le … par … » | | |
| P9 | **Déplacer** → 12:00 → Entrée | « Rendez-vous déplacé » ; affiché à 12:00 | | |
| P10 | Fermer le détail → **glisser** le rendez-vous d'une heure vers le bas | « Rendez-vous déplacé » ; 13:00 | | |
| P11 | **Annuler le rendez-vous** → motif → confirmer | « Rendez-vous annulé », « Annulé le … » ; masqué sauf « Afficher les annulés » | | |
| P12 | **Rétablir le rendez-vous** | « Rendez-vous rétabli » ; de nouveau planifié à 13:00 | | |
| P13 | **Voir la fiche client** | retour sur la fiche du client | | |
| P14 | Second poste ou seconde session (facultatif) | le même rendez-vous, à la même heure | | |

Note : la fiche client ne liste **pas** encore les rendez-vous du client (voir § 6, décision D1).

## 3. Thèmes, résolutions, zooms, clavier, glisser-déposer

Vérifié par l'agent sur `mmv_v2_test` (§ 0) ; à confirmer à l'œil par le propriétaire, au minimum sur les cases en gras.

| Thème | Fenêtre | 100 % | 125 % | 150 % |
|---|---|---|---|---|
| Clair | 1440×900 | | | **☐** |
| Clair | 1920×1080 | **☐** | | |
| Sombre | 1440×900 | | | **☐** |
| Sombre | 1920×1080 | **☐** | | |

Zoom : Ctrl + / Ctrl − / Ctrl 0 dans la fenêtre. Points à regarder : aucun défilement horizontal ; boutons « Créer » / « Annuler » atteignables ; lisibilité du texte et des rendez-vous ; contraste en sombre. Clavier : Tab / Maj+Tab dans le formulaire et le détail, Entrée sur les boutons, Échap dans les confirmations, Ctrl+S sur la fiche client. Glisser-déposer : P10 à 100 % et 150 %.

Captures de référence de l'agent : `evidence/P03C_native_c3_matrice_*.png` (12), `evidence/P03C_native_c3_light_1920x1080_z100_*.png` (11), `evidence/P03C_native_c3_dark_1440x900_z150_*.png` (5).

## 4. Fuseaux horaires — protections conservées

- Le risque Casablanca **n'est pas considéré comme résolu** (O-03-22) : la divergence Windows / WebView2 du 01/01 au 15/02/2026 existe toujours sur ce poste ; le garde-fou a été rejoué le 18/10/2026 (semaine du 05/01/2026 bloquée, saisie du 15/01/2026 refusée par le serveur, rien d'écrit). Risque résiduel inchangé : données de fuseaux identiques mais toutes deux périmées.
- Configuration choisie : `Europe/Brussels`. Pendant l'essai, le bandeau **« Heure incohérente »** ne doit jamais apparaître ; s'il apparaît : défaut **bloquant**, capture et noter la semaine affichée.
- Semaine du changement d'heure (25/10/2026) : déjà éprouvée en 03-B3 ; à regarder si l'essai tombe sur cette semaine (créneau enjambant 02:00–03:00 refusé par conception, O-03-23).

## 5. Journal des constats

Classement : **BLOQUANT** (empêche `PHASE03_COMPLETE` : perte ou corruption de données, heure fausse, action impossible, régression) ; **UX** (amélioration, non bloquante, peut aller au backlog) ; **À DÉCIDER** (choix produit du propriétaire).

| ID | Source | Écran / étape | Constat | Classe | Proposition | État |
|---|---|---|---|---|---|---|
| C3-01 | agent, natif | Détail du rendez-vous, confirmation d'annulation | Après **Échap**, le focus clavier tombe sur la page (`BODY`) au lieu de revenir au bouton « Annuler le rendez-vous » : l'utilisateur au clavier perd sa position (2 tabulations pour revenir) | UX (accessibilité) | rendre le focus au bouton déclencheur à la fermeture de la confirmation | OPEN — O-03-33 |
| C3-02 | agent, natif | Barre d'état (Shell Phase 02), vue CSS 960 px (1440×900 à 150 %) | Libellé « Base de données disponible » tronqué (« Base de do… ») | UX | réduire les libellés ou passer en icônes avec info-bulle sous ~1000 px | OPEN — O-03-34 |
| C3-03 | agent, natif | Agenda, vue CSS 960×600 | Le formulaire passe sous l'agenda (mise en page étroite) : l'agenda sort de l'écran pendant la saisie ; tout reste accessible | UX | conserver ; ou panneau superposé à cette largeur | OPEN — O-03-35 |
| C3-04 | agent, natif | Agenda / fiche | Ordre du nom différent : agenda « Prénom Nom », fiche et sélecteur « NOM Prénom » | À DÉCIDER | choisir un ordre unique | OPEN — O-03-36 |
| C3-05 | agent (connu) | Shell, thème sombre | Logo sur fond clair en thème sombre (écart Shell Phase 02 déjà relevé dans [`STATE_MAP_47.md`](STATE_MAP_47.md)) | UX | variante de logo pour le thème sombre | préexistant |
| C3-06 | agent (connu) | Fiche client | La fiche ne liste pas les rendez-vous du client | À DÉCIDER | voir § 6, D1 | OPEN — O-03-37 |
| | propriétaire | | | | | |

Aucun constat **BLOQUANT** de l'agent. Les essais du propriétaire s'ajoutent sous la dernière ligne (date, écran, étape, capture).

## 6. Décisions du propriétaire avant `PHASE03_COMPLETE`

| ID | Question | Options |
|---|---|---|
| D1 | Liste des rendez-vous sur la fiche client (prévue au cadrage 03-C, absente du GO 03-C) | (a) exigée avant `PHASE03_COMPLETE` → nouveau GO de développement ; (b) reportée au backlog (la prise de rendez-vous depuis la fiche et le retour à la fiche existent) |
| D2 | Constats UX C3-01 à C3-04 | corriger avant clôture, ou backlog |
| D3 | Correctif O-03-16 (`scripts/Invoke-MmvSqlScript.ps1`, empreinte `78F6E8DC…0BBE`) | accepter comme nouvelle version de l'outil approuvé ; facultatif : contrôle en lecture seule ci-dessous |
| D4 | Résultat du parcours § 2 et de la matrice § 3 | GO final (`PHASE03_COMPLETE`, puis push / PR sur décision) ou liste de défauts bloquants |

Contrôle facultatif, en lecture seule, de la forme de requête du correctif O-03-16 (mot de passe demandé par psql ; aucune écriture) :

```powershell
'SELECT string_agg("MigrationId", '', '' ORDER BY "MigrationId") FROM "__EFMigrationsHistory";' |
  & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h localhost -p 5433 -U mmv_v2_dev -d mmv_v2_dev -X -A -t
```

Attendu : les 4 identifiants de migration sur une ligne, sans « la relation « __efmigrationshistory » n'existe pas ».
