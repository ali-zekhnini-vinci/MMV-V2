# MMV V2 — Installation, réseau, sécurité et données

**Statut :** proposition phase 00, aucun composant installé ni POC exécuté.

## 1. Modes d'installation fonctionnels

Le choix Serveur/Poste est une décision propriétaire. Il n'est PAS pris en charge automatiquement par l'installateur généré par Tauri.

| Mode | Installé | Usage | Postes / DB |
|---|---|---|---|
| `Serveur MMV` | ASP.NET Core Windows Service, PostgreSQL 18 (installation ou rattachement contrôlé), outils de backup/restauration, configuration initiale | PC serveur qui demeure allumé | PostgreSQL **local au serveur** ; pas de DB dans le client |
| `Poste de travail MMV` | Client Tauri/WebView2 seulement | PC réception, vente, opticien, atelier | Adresse/identité du serveur ; aucune DB et aucun backend serveur |
| `Serveur MMV + client sur même ordinateur` | Les deux précédents, côté serveur et client | Monoposte ou serveur utilisable comme poste | API `localhost` + PostgreSQL local |

Assistant initial proposé : préflight OS/droits/ports/données existantes → mode → chemins et espace disque → installation contrôlée → certificat serveur → identité du magasin et premier administrateur → configuration firewall LAN → health check → sauvegarde initiale → guide de connexion des postes. Les identifiants d'administration PostgreSQL ne sont jamais copiés dans le client.

## 2. Distribution Windows : deux stratégies

| Scénario | Avantage | Risques | Avis |
|---|---|---|---|
| A. **Launcher unifié** « Installer MMV » puis choix Serveur/Poste | Parcours facile à expliquer à un magasin | Orchestration d'installateurs, élévation, sécurité, installation PG, rollback complexes | Cible UX privilégiée **sous POC** |
| B. Deux installateurs indépendants « MMV Serveur » et « MMV Poste » | Packaging et maintenance plus simples, plus robuste initialement | Deux exécutables à distribuer, sélection moins centralisée | **Fallback technique** / piste de premier POC |

Tauri produit nativement un `-setup.exe` NSIS ou `.msi` WiX pour le client. Il ne suffit pas à provisionner .NET + PostgreSQL + service Windows. Pour installer WebView2 sur des machines sans Internet, évaluer `offlineInstaller`, qui augmente la taille du package ; sa maintenance reste à gérer. Versions, licence et signature du packaging à contrôler.

Désinstallation par défaut : conserver base de données, pièces jointes et sauvegardes ; suppression définitive seulement à partir d'une action distincte et clairement autorisée. Journal d'installation sans secrets ; rollback du service sans effacer le cluster PG.

## 3. Connexion LAN et identité serveur

- Client toujours configurable avec une adresse manuelle `https://nom-dns-ou-ip:port`. Découverte mDNS est seulement optionnelle et *ne constitue pas la confiance*.
- Conseiller un nom DNS local stable et réservation DHCP côté magasin ; en cas d'IP changée, retenter résolution DNS/connexion après vérification d'identité.
- **TLS validé de bout en bout**, jamais `accept-invalid-cert`, contournement permanent, certificat self-signed « accepter quand même » sans vérification d'empreinte fiable.
- `ADR-006` ouvert : comparer **certificat délivré par CA privée avec installation contrôlée de sa racine sur les postes**, ou **domaine propre + certificat public et DNS local** lorsque l'environnement le permet. Pinner l'identité serveur si approprié ; prévoir rotation/révocation et procédure de réinstallation. Les certificats développement ASP.NET (`dotnet dev-certs`) ne sont pas des certificats serveur de production.
- Pairing = admission **d'un appareil** par l'administrateur via code temporaire/policy ; *ne remplace jamais* l'authentification de l'utilisateur, l'autorisation par API et TLS.
- Statuts : recherche, serveur non trouvé, DNS invalide, TLS échoué, appairage refusé, session expirée, API incompatible, PG non disponible, reconnecté. Ne jamais laisser l'opérateur croire qu'une vente a été enregistrée pendant la panne.
- Accès Internet, cloud, distant/VPN, offline sync et multi-magasin exclus de la V1 jusqu'à nouvelle décision.

## 4. Authentification, rôles et sessions

- Solution proposée : **ASP.NET Core Identity + policies RBAC côté API**, première identité administrateur créée pendant l'installation, comptes individuels pour collaborateurs.
- Cinq familles de navigation : Propriétaire, Responsable/Opticien, Vendeur, Technicien atelier, Accueil/Réception. Une page de dashboard ne donne pas automatiquement un droit d'écriture.
- Sessions : jeton d'accès court + moyen de renouvellement révocable lié à l'appareil, à prototyper. Le secret long terme doit résider dans un coffre protégé Windows (Credential Manager/DPAPI, ou mécanisme Tauri démontré équivalent) **pas `localStorage`, `sessionStorage` ni fichiers de config en clair**.
- Alternative : cookies sécurisés HttpOnly + session serveur, à comparer avec origine Tauri/WebView2, SameSite/CSRF et CORS ; éviter une décision de facilité non testée.
- Jamais stocker mots de passe DB ni clés de signature dans Tauri ; logs avec identifiants techniques de corrélation plutôt que nom client ou valeur d'ordonnance.
- Permissions serveur par opération (read, create, update, delete, refund, finalize, override, export) définies par matrice future. Les cas de vente/stock ont une protection distincte ; le masquage UI seul est insuffisant.

## 5. PostgreSQL et sauvegardes

- PostgreSQL 18.x est installé dans le rôle serveur uniquement ; service Windows séparé, compte de service dédié, droits minimaux et trafic DB local si possible.
- ORM `EF Core 10` et provider **Npgsql exclusivement**. Migrations exécutées uniquement via mécanisme contrôlé et auditable de mise à niveau ; jamais depuis Tauri. Aucune migration automatique sauvage au démarrage de chaque client.
- Données persistantes dans stockage serveur protégé (ACL / chiffrement au repos selon analyse). Pièces jointes optiques avec métadonnées PostgreSQL et accès autorisé via API.
- Sauvegarde transactionnellement cohérente via `pg_dump`/`pg_restore` ou stratégie adaptée au volume et RPO/RTO ; versionner et chiffrer le transfert/stockage, protéger secrets et vérifier la réussite.
- Au moins une copie de sauvegarde **hors de la machine serveur** pour couvrir la perte du PC ; politique de rétention, restauration testée, intégrité, accès restreint. La simple création d'un fichier de backup n'est pas une preuve de récupérabilité.
- RPO/RTO (perte de données et durée d'indisponibilité tolérables) = décision d'exploitation à prendre avant première livraison, pas chiffre inventé.

## 6. Intégrité applicative, audit et impressions

- Argent `decimal` / `numeric`, règle d'arrondi et taxe décidées avec le pays d'exploitation ; jamais de `float`.
- Transactions PG, contrôle des quantités, concurrence et idempotency key pour ventes, paiements, remise et stock ; logs d'audit des événements critiques ne contenant pas de secrets ni de contenu médical complet.
- Dossiers clients, ordonnance et mesures optiques à considérer comme sensibles : contrôle par objet, minimisation et conservation définie, export soumis à permission.
- Impressions A4/A5/ticket et poste imprimant : POC réel matériel requis dans le lot concerné. Les API Tauri / navigateur ne suffisent pas à présumer qu'une imprimante thermique fonctionne.
- Signatures des packages, mises à jour compatibles serveur/client et bases, possibilité de rollback applicatif **sans rollback destructeur de DB**.

## 7. Menaces prioritaires et réponses

| Menace | Contrôle minimal / preuve |
|---|---|
| Poste frauduleux sur LAN | identité serveur TLS + pairing + login/RBAC ; test du refus |
| Interception Wi-Fi LAN | TLS vérifié et certificat renouvelable ; pas de fallback HTTP |
| Vol de poste client | tokens révocables en coffre OS, pas de DB locale, verrouillage session |
| Double vente / double mouvement après retry | idempotence serveur + tests requêtes répétées |
| Écrasement multi-poste | token de concurrence et gestion explicite de conflit |
| Panne serveur au paiement | état « incertain » géré sans confirmer deux fois ; reconciliation transactionnelle |
| Ransomware / disque mort | backup hors machine, droits restreints, restauration testée |
| Logs exposant ordonnance | masquage par défaut ; audit fonctionnel minimisé |
| Installation interrompue | reprise/rollback des binaires, préservation des données |
| Poste incompatible après MAJ serveur | version API + fenêtre de compatibilité documentée |

## 8. Conditions de revue

La stratégie **Tauri + .NET + PG** est validable par POC de phase 01, mais **aucun** test LAN / TLS / PG / Windows Service n'a été réalisé dans le cadre de ce pack. Seul le propriétaire peut approuver les ADR ouverts. Les considérations de fiscalité et conformité locale sont réexaminées avant les modules ventes et ordonnances.
