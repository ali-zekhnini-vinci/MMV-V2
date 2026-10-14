# MMV V2 — Plan de preuves & tests de faisabilité (phase 01 et suivantes)

Ce document planifie des **tests futurs**. **Aucune ligne n'est PASS**. Phase 00 n'autorise pas leur exécution.

| ID | POC | Test précis | Preuve attendue | Si échec |
|---|---|---|---|---|
| P01-01 | Windows dev/runtime | `dotnet`, Node/npm, Rust/MSVC, WebView2, versions OS x64 | rapport de versions, chemin d'exécution, screenshots | STOP installation dépendances sans accord |
| P01-02 | React + Tauri | Fenêtre Tauri Windows qui charge UI et nav sans navigateur externe | build log, capture WebView2, binaire local | revoir prérequis Tauri et plugins |
| P01-03 | .NET 10 | API ASP.NET Core indépendante du client | build, test endpoint health + logs neutres | STOP backend et version |
| P01-04 | PG exclusif | Création PG de test isolée, EF Core 10 + Npgsql 10, migration et CRUD | versions réelles, migrations, test contre PG, logs | STOP si autre provider/PG absent |
| P01-05 | Monoposte | Tauri → HTTPS API localhost → PG local | capture UI API/PG, observation process/port | STOP si client appelle PG directement |
| P01-06 | Multi-poste | Poste distant non-admin → API du serveur LAN → même PG | IP/DNS masqués, requêtes autorisées, logs service | STOP si DB installée côté client |
| P01-07 | TLS confiance | Tester CA privée ET/OU domaine certifié, SAN DNS/IP, certificat invalide et rotation | preuve refus mismatch/expiration, connexion valide | STOP tout HTTP/non-validation cert |
| P01-08 | Auth / pairing | appareil non appairé refusé, utilisateur non autorisé refusé ; session révoquée | traces RBAC 401/403, test de révocation | STOP auth |
| P01-09 | Concurrence & retry | deux clients mettent à jour un objet de test ; 409 ou résultat serialisé, même clé idempotence | tests transactionnels PG | STOP avant ventes |
| P01-10 | Service Windows | démarrage automatique, arrêt/redémarrage, droits service dédiés | `sc query`, journaux Windows + health | STOP mode Serveur |
| P01-11 | Packaging | POC serveur/client unifié ou séparé, install/reinstall/uninstall conservant données | logs setup, screenshots, inventaire des services | NO-GO déploiement |
| P01-12 | WebView2 offline | installation sur PC de test sans accès Internet | capture runtime/version + installateur | choix package alternatif |
| P01-13 | Sauvegarde | backup cohérent, restauration **sur machine/test isolé**, comparaison données | checksum, logs et rapport, aucun client réel | NO-GO première exploitation |
| P01-14 | API compatible | client version N face serveur N/N-1 suivant politique retenue | tests version API et rejet explicite | NO-GO mise à jour |
| P01-15 | Graphify | version, construction graphe sur repo **neuf**, requête de dépendance utile | transcript commandes/MCP, stats graphe | STOP tâches dépendant du graphe |
| P01-16 | Serena | serveur MCP local chargé, projet TS/C# activé, recherche symbole/référence réelle | noms d'outils et résultats réellement observés | STOP tâches symboliques |
| P02-01 | Design | login/shell Light/Dark 1440×900, 1920×1080, zoom 125/150%, clavier | captures vraies sur Tauri, rapport diffs | correction visuelle avant approbation |
| PXX-01 | FullCalendar | semaine/jour et drag/resize sans plugin Premium ; clavier | liste exacte de packages et droits | remplacer UX spécifique/licence |
| PXX-02 | Impression | étiquette / ticket / A5 selon matériel du magasin | print réel et format vérifié | adapter spooler/driver et recette |
| PXX-03 | Vente/stock | vente concurrente, annulation, paiement, stock exact et trace | tests métier nouveaux + PG | NO-GO vente réelle |

## Gate 0 à chaque phase de code (post GO seulement)

1. Git : repo neuf, remote et SHA, branch, worktree, non-commit et secrets ; jamais de `reset --hard` ou force push.
2. Source design et document `APPROVED` du board réellement engagé ; les autres sont `DRAFT`.
3. Stack verrouillée et versionnée ; npm `package-lock.json`, verrouillage des NuGet selon l'outillage retenu.
4. Vérifier outils dev Graphify/Serena ; utiliser au moins un appel pertinent quand leur domaine s'applique ; garder trace de succès ou STOP documenté.
5. IN/OUT, contrat API, permissions, persistance PG, matrices QA et source-map.
6. Tests réels ; pas de captures fictives, pas de mock prétendu données métier.
7. Owner GO avant début du lot et owner validation avant PR merge.

## Sources documentaires à revérifier lors de P01

- Tauri Windows installer : https://v2.tauri.app/distribute/windows-installer/
- .NET LTS : https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- ASP.NET Core Windows Service : https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0
- Npgsql EF 10 : https://www.npgsql.org/efcore/release-notes/10.0.html
- PostgreSQL versions : https://www.postgresql.org/support/versioning/
- Serena : https://github.com/oraios/serena
- Graphify local : https://graphify.com/integrations/claude-code (bien identifier fournisseur/projet choisi)
- FullCalendar licensing : https://fullcalendar.io/license

## Configuration de Graphify & Serena : prudence sur le repo vide

Serena peut se connecter au client MCP, mais ne peut pas prouver une recherche de symboles pertinente **avant l'existence de code**. Graphify peut être installé, mais un graphe vide n'offre pas de cartographie exploitable. Le premier gate requiert donc la preuve d'installation/accessibilité, puis un usage réel après création des premiers composants en phase 01. Ne pas utiliser le prétexte du graphe vide pour inspecter l'ancien dépôt.

**Confidentialité :** privilégier des outils locaux. Aucun index/snapshot de code ni donnée optique envoyés à un service hébergé sans revue explicite.
