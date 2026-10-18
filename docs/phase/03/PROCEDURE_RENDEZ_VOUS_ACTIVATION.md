# 03-B2 — Procédure d'activation du module Rendez-vous (à exécuter par le propriétaire)

**Statut (18/10/2026, 03-B3) : étapes 1 et 2 EXÉCUTÉES** (revue SQL par le propriétaire, GO 03-B3 ; application de 0004 à `mmv_v2_dev` puis `mmv_v2_test` le 17/10/2026 par l'agent, contrôles conformes — QA_MATRIX Q-03-141 à Q-03-145). **Étape 3 : valeurs fournies par le propriétaire** (Europe/Brussels, 09:00–19:00, aucun jour masqué, capacité d'accueil 1), **non enregistrées** dans la configuration de MMV.Api (choix du propriétaire : test natif sur `mmv_v2_test` avec ces valeurs passées au seul processus de test). **Étapes 3 (enregistrement), 4 et 5 : à exécuter par le propriétaire.** Étape 6 : réalisée sur `mmv_v2_test` (Q-03-160, Q-03-163). Après mise à jour du code, relancer MMV.Api **et** le poste ensemble (contrat élargi en 03-B3, O-03-31).

*Historique — statut (17/10/2026) : NON EXÉCUTÉE.* Aucune étape n'a été réalisée par l'agent : aucune base modifiée, aucune configuration du magasin renseignée, aucun droit ni éligibilité attribué. L'ordre est impératif ; chaque étape suppose la précédente réussie. La revue du SQL exact (étape 1) précède tout GO d'application.

## 1. Revue du SQL exact (propriétaire)

Scripts : [`sql/0004_AppointmentsModule.sql`](sql/0004_AppointmentsModule.sql) (lecture), [`sql/0004_AppointmentsModule.idempotent.sql`](sql/0004_AppointmentsModule.idempotent.sql) (application), [`sql/0004_AppointmentsModule.rollback.sql`](sql/0004_AppointmentsModule.rollback.sql) (retour arrière destructif, O-03-26). Empreintes et contenu : [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) § Dossier de migration 0004 — version 03-B2. Points d'attention : seule instruction `DROP` = `DROP CONSTRAINT ck_user_permissions_permission`, recréée immédiatement sous le même nom avec une liste élargie, dans la même transaction (même mécanisme que 0003) ; nouvelle table `appointments.responsibles` (justification dans le dossier).

## 2. Application (après GO explicite du propriétaire)

Même outil et mêmes contrôles que 0002 / 0003 (jamais au démarrage, jamais `EnsureCreated()`) :

```powershell
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev `
  -Script docs/phase/03/sql/0004_AppointmentsModule.idempotent.sql -Sha256 9bd07d90b2578e95226c290e9a3ef1fd3bb83f79a7cee24b0e00cbcd07122399
./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-postgresql-tests -Key ConnectionStrings:MmvTest -Database mmv_v2_test `
  -Script docs/phase/03/sql/0004_AppointmentsModule.idempotent.sql -Sha256 9bd07d90b2578e95226c290e9a3ef1fd3bb83f79a7cee24b0e00cbcd07122399
dotnet run --project src/MMV.Maintenance -- status        # 4 migrations appliquées, 0 en attente
dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=PostMigration"
dotnet test tests/MMV.PostgreSql.Tests --filter "Stage=AppointmentsWrite"
```

## 3. Paramètres du magasin (section `Appointments` de MMV.Api)

À renseigner hors Git (user-secrets `mmv-v2-api` ou variables d'environnement `Appointments__…`), avec les valeurs **réelles** du magasin. Aucune valeur n'est fournie par défaut : sans `TimeZone` ni plage affichée, l'agenda affiche « Agenda non configuré » (503).

Valeurs fournies par le propriétaire le 18/10/2026 (aucun jour masqué : ne pas renseigner `HiddenWeekdays`) :

```powershell
dotnet user-secrets set "Appointments:TimeZone" "Europe/Brussels" --id mmv-v2-api
dotnet user-secrets set "Appointments:DisplayStartTime" "09:00" --id mmv-v2-api
dotnet user-secrets set "Appointments:DisplayEndTime" "19:00" --id mmv-v2-api
dotnet user-secrets set "Appointments:ReceptionCapacity" "1" --id mmv-v2-api
```

Forme générale (référence 03-B2) :

```powershell
dotnet user-secrets set "Appointments:TimeZone" "<Africa/Casablanca | Europe/Brussels>" --id mmv-v2-api
dotnet user-secrets set "Appointments:DisplayStartTime" "<HH:mm>" --id mmv-v2-api
dotnet user-secrets set "Appointments:DisplayEndTime" "<HH:mm ou 24:00>" --id mmv-v2-api
# Facultatif : jours masqués de la grille (0 = dimanche … 6 = samedi), un par index
dotnet user-secrets set "Appointments:HiddenWeekdays:0" "<jour>" --id mmv-v2-api
# Facultatif : capacité d'accueil (places simultanées sans responsable). Absente = aucun rendez-vous sans responsable.
# POC local : 1 seulement si une ressource d'accueil réelle lui est réservée (D-03-15).
dotnet user-secrets set "Appointments:ReceptionCapacity" "<entier ≥ 1>" --id mmv-v2-api
```

Puis redémarrer MMV.Api : le journal indique `Rendez-vous : magasin configuré (fuseau …, capacité d'accueil …)` ou la liste des paramètres manquants (événement 3103).

## 4. Droits d'accès (comptes réels, propriétaire)

```powershell
dotnet run --project src/MMV.Maintenance -- permissions grant <identifiant> appointments.read
dotnet run --project src/MMV.Maintenance -- permissions grant <identifiant> appointments.write
dotnet run --project src/MMV.Maintenance -- permissions grant <identifiant> appointments.cancel
# La recherche du client exige aussi clients.read (PROCEDURE_PERMISSIONS_CLIENTS.md).
```

## 5. Collaborateurs éligibles (indépendant des droits, D-03-14)

```powershell
dotnet run --project src/MMV.Maintenance -- responsibles list
dotnet run --project src/MMV.Maintenance -- responsibles enable <identifiant>
dotnet run --project src/MMV.Maintenance -- responsibles disable <identifiant>   # retrait logique, jamais de suppression
```

Chaque écriture demande la confirmation du nom de la base et n'est admise que sur `mmv_v2_dev` (même politique de cible que `permissions grant`).

## 6. Vérifications après activation — *réalisées le 18/10/2026 sur `mmv_v2_test` avec les valeurs du magasin (03-B3, QA_MATRIX Q-03-160, Q-03-163) ; à refaire brièvement sur la configuration réelle après les étapes 3 à 5*

Parcours natif Tauri → HTTPS → API → PostgreSQL 18 : création personnalisée et d'accueil, conflit, capacité atteinte, deux postes simultanés, annulation / rétablissement, prise de rendez-vous depuis la fiche client, glisser-déposer (O-03-18), affichage dans le fuseau configuré.
