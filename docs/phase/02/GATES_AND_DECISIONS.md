# 02 — GATES_AND_DECISIONS

## GO et bornes

- 16/10/2026 : « GO OFFICIEL PHASE 02 » du propriétaire. GO pour développer, générer les migrations d'identité et tester sur doubles non persistants.
- **Borne unique :** appliquer la migration d'identité à `mmv_v2_dev` / `mmv_v2_test` et exécuter le bootstrap administrateur réel exigent **un seul** dossier de décision (O-02-01), puis un GO unique du propriétaire. Aucune modification de PG17/5432, des rôles `postgres`, des autres bases ni des certificats système.

## Gates Phase 02

| Gate | Critère | État |
|---|---|---|
| G-02-A | Login/Shell conformes aux références, états, clavier, thème, tests | implémenté ; tests DOM PASS ; preuve native `NOT_TESTED` (02-C) |
| G-02-B | Identité serveur, hachage Identity, policies, 401/403, bootstrap local refusé la 2e fois | NOT_STARTED |
| G-02-C | Tauri → HTTPS → API → PG réel, login/logout/révocation natifs, captures, résilience | NOT_STARTED |
| G-02-DOC | DOCUMENTATION GATE | en cours |

## Décisions ouvertes

| ID | Sujet | État | Bloque |
|---|---|---|---|
| O-02-01 | GO unique : migration d'identité sur `mmv_v2_dev` + `mmv_v2_test` et bootstrap administrateur réel | OPEN (dossier à présenter, 02-B) | G-02-B (partie PG), G-02-C |
| O-02-02 | Variante de session dev (jeton opaque volatile) — ADR-007 | PROPOSED (voir ci-dessous) | ratification ADR-007, distribution |
| O-02-03 | Login : mot de passe vidé après échec (variante recommandée §13) ; bandeau sous le sous-titre ; validation non « live » | PROPOSED | aucune (réversible) |
| O-02-04 | Bouton « Afficher le mot de passe » dans l'ordre de tabulation (écart à §11 du handoff) | PROPOSED | aucune |
| O-02-05 | Embarquement des polices Inter / Source Sans 3 (fichiers OFL locaux) | OPEN — non embarquées, repli Segoe UI | fidélité typographique |
| O-02-06 | Versionnement des 4 logos officiels dans `apps/desktop/src/assets/brand/` (droits de redistribution) | OPEN — versionnés localement, non poussés | push |
| O-02-07 | Recherche globale et notifications : contrôles visibles mais désactivés (« Disponible dans une phase suivante ») | PROPOSED | aucune |
| O-02-08 | Libellé « Accueil » (au lieu de « Tableau de bord ») tant qu'aucun tableau de bord réel n'existe ; 5 variantes Lot 1 au backlog | PROPOSED | aucune |

Héritées de 01B, inchangées : O-B04 (rôle unique propriétaire), O-B10 (écoute HTTP 5022), O-B11 (adresse serveur en production), O-B12 ; ADR-004/006/007 non ratifiés.

## O-02-02 — Session : comparaison et variante retenue pour le POC local (PROPOSED, provisoire dev)

| Option | Avantages | Risques / coûts |
|---|---|---|
| **A. Jeton opaque aléatoire (256 bits), session serveur révocable, en mémoire volatile du client** (retenue) | aucun cookie → pas de CSRF ni de cookie tiers (origine Tauri ≠ origine API) ; révocation immédiate (ligne de session) ; seul un **hachage SHA-256** du jeton est stocké en base ; aucune persistance poste ; CORS sans `credentials` | jeton lisible par du JavaScript exécuté dans la WebView (XSS) → atténué par CSP stricte `script-src 'self'` ; reconnexion à chaque lancement ; requêtes protégées = préflight CORS (`Authorization`) |
| B. Cookie `HttpOnly; Secure; SameSite=None` + session serveur + anti-CSRF | jeton inaccessible au JavaScript | cookie **tiers** entre `tauri.localhost`/`localhost:1420` et `localhost:7267` : comportement WebView2 à prouver ; CORS `AllowCredentials` + jeton anti-CSRF ; persistance dans le profil WebView2 |
| C. Jeton de rafraîchissement dans le coffre OS (Credential Manager) | session survivant au redémarrage, secret protégé par l'OS | plugin Tauri/Rust supplémentaire, persistance longue durée, rotation/révocation à concevoir ; hors POC 02 |

Paramètres proposés (dev) : durée absolue **8 h**, inactivité **30 min** (glissement à chaque requête authentifiée), aucun rafraîchissement ; révocation à la déconnexion, à la désactivation du compte (vérifiée à chaque requête) et par expiration. **Ce choix n'est pas une session distribuable approuvée** ; ADR-007 reste `PROPOSED`.
