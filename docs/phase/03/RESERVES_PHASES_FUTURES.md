# 03 — Réserves restantes pour les phases futures (19/10/2026)

Établi lors du GO « PHASE 03 — PRÉPARATION DU MERGE » ([`prompts/PHASE03_MERGE_PREP_GO.md`](../../../prompts/PHASE03_MERGE_PREP_GO.md)), après le **GO technique** de l'audit indépendant de l'archive 03-D3. Ce document ne modifie aucun statut antérieur : il regroupe ce qui reste ouvert. Le détail et l'historique de chaque point restent dans [`GATES_AND_DECISIONS.md`](GATES_AND_DECISIONS.md) et [`RAPPORT_CLOTURE_03D3.md`](RAPPORT_CLOTURE_03D3.md) § 8. Aucun travail de Phase 04 n'est engagé.

## 1. Décisions du propriétaire encore ouvertes

| Réf. | Objet | État au 19/10/2026 |
|---|---|---|
| D4 | Validation finale de la Phase 03 | GO technique reçu ; **dernier essai manuel du propriétaire en attente** ; `PHASE03_COMPLETE` non déclaré ; push après confirmation, PR et merge par le propriétaire |
| D3 / O-03-16 | Correctif de `scripts/Invoke-MmvSqlScript.ps1` (affichage final après `COMMIT`) | `FIX_PREPARED` — à accepter comme nouvelle version de l'outil approuvé |
| D-03-22 | Durées de conservation du journal d'audit (RGPD / APD, loi 09-08 / CNDP) | **OPEN** — conseil juridique ; aucune purge automatique |
| O-03-36 | Ordre du nom « NOM Prénom » partout | RESOLVED par choix de l'agent, **à confirmer** |
| O-03-23 | Règles des rendez-vous commencés (rétablissement, déplacement) | ACCEPTED **provisoirement** |
| `audit.read` | Attribution à un compte réel | aucun compte réel ; `MMV.Maintenance permissions grant <identifiant> audit.read` (confirmation interactive) |
| O-03-41 | `apps/desktop/desktop.zip` (endommagé) et `desktop.recovered.zip` (reconstitué, 163/163 entrées) | conservés non suivis, **non modifiés** ; remplacement ou suppression par le propriétaire |

## 2. Socle à prévoir (`FOUNDATION_MUST`, backlog `docs/PERIMETRE_ET_SEQUENCE.md`)

- Rôle SQL applicatif distinct du rôle de migration (O-B04) : aujourd'hui `MMV.Api` se connecte avec le rôle de migration, propriétaire des tables ([`docs/ADR_ET_DECISIONS.md`](../../ADR_ET_DECISIONS.md)) ; les déclencheurs d'ajout seul de `audit.events` ne protègent pas contre un superutilisateur.
- Politique de rétention contrôlée du journal d'audit et procédure de purge revue (dépend de D-03-22).
- Restauration de la sauvegarde de `mmv_v2_dev` prise avant 0005 : **jamais testée** (rôle sans `CREATEDB`) ; une procédure de sauvegarde / restauration éprouvée reste à établir.

## 3. Réserves techniques reportées

| Réf. | Réserve | Échéance suggérée |
|---|---|---|
| O-03-38 | L'exécutable release ne porte aucune adresse d'API (`VITE_MMV_API_BASE_URL` limité au développement) | phase d'empaquetage |
| O-03-17 | FullCalendar figé à 7.1.0 (7.1.3 disponible) | réévaluer à partir du 30/10/2026 |
| O-03-03 | Bundle principal > 500 kB (prolonge O-02-13) | découpage au prochain module lourd |
| O-03-02 | Avertissement lint `react-hooks/incompatible-library` (`useReactTable`), conservé visible | suivi de React Compiler / TanStack Table |
| O-03-05 | Garde de sortie par registre maison (routeur mémoire) | refonte éventuelle du Shell (`createMemoryRouter` + `useBlocker`) |
| O-03-06 | Primitives Radix publiées < 2 semaines lors de l'installation | aligner à la prochaine montée de versions |
| O-03-19 | Rendez-vous réutilise les classes `clients-*` | extraire un socle CSS commun au 3e module |
| O-03-11 | Recherche `LIKE '%…%'` sans index (`pg_trgm` absent) | mesurer au-delà de quelques dizaines de milliers de fiches |
| O-03-12 | Séquence de référence client non transactionnelle (trous possibles) | information |
| O-03-25 | Désactivation d'un collaborateur concurrente d'une réservation (ligne `auth.users` non verrouillée) | information |
| O-03-27 | Clés de verrou consultatif sur 32 bits (collisions possibles, sérialisation excessive seulement) | information |
| O-03-13 / O-03-26 | Scripts de retour arrière 0003 / 0004 destructifs (suppriment les tables et leurs données) | information — jamais exécutés |

## 4. Preuves non rejouées

- Scénarios natifs antérieurs `parcours-03c`, `matrice-03c`, `rdv-b3*` : `NOT_RERUN` depuis 03-D3 ; garde-fou de fuseau Casablanca non rejoué (risque non clos).
- Build release Tauri et parcours natif de l'historique : PASS en 03-D3 sur le code de `d70df82`, `NOT_RERUN` le 19/10/2026 (préparation du merge) ; le seul commit postérieur (`93df475`) ne touche que la documentation.

## 5. Hérités des phases précédentes

Inchangés par la Phase 03 et suivis dans [`docs/PERIMETRE_ET_SEQUENCE.md`](../../PERIMETRE_ET_SEQUENCE.md) : réseau sécurisé (ADR-006), démarrage en service Windows (O-B12), tableaux de bord du Lot 1, recherche globale et panneau de notifications (`DEFERRED`), O-02-13 (bundle), O-02-14 (503 explicite sur `/api` si la base est injoignable).
