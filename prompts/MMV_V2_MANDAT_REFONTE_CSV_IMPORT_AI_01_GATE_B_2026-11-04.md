# MMV V2 — MANDAT DE REFONTE CSV-IMPORT-AI-01
## GATE B — Architecture intelligente et nouvelle expérience d'import

> Archivé le 04/11/2026 tel que reçu du propriétaire (texte intégral ci-dessous, sans modification).

**Projet :** Manage My Vision V2  
**Phase :** 05 — Catalogue, fournisseurs et stock  
**Contexte :** Lot A réalisé, Lot B0 PASS sur prototype synthétique  
**Décision :** L'interface actuelle est refusée. Une refonte fonctionnelle et UX du parcours d'import est requise.

---

# 1. OBJECTIF DU MANDAT

Tu dois transformer le module d'import CSV de MMV V2 en un véritable assistant d'import intelligent, utilisant l'IA locale dès l'analyse du fichier.

Le fonctionnement actuel présente plusieurs problèmes :

1. Le serveur signale trop tôt certaines anomalies comme des erreurs bloquantes.
2. L'utilisateur ne voit pas suffisamment ce que fait l'IA.
3. Le dépôt, l'analyse, les corrections et l'import sont regroupés sur un seul écran.
4. Les erreurs sont difficiles à comprendre et à corriger.
5. L'historique occupe trop d'espace.
6. L'interface indique toujours `llama3.2` alors que `qwen3.5:4b` a été retenu comme candidat pour la nouvelle architecture.

Le Lot B0 a démontré, sur corpus synthétique, que le contrat `mmv.csvai.v2` et son validateur prototype pouvaient bloquer les propositions commerciales incorrectes du modèle.

Ce résultat ne prouve pas encore le bon fonctionnement dans MMV.Api, PostgreSQL ou Tauri.

Tu dois utiliser ces preuves pour préparer et réaliser l'intégration contrôlée, sans simplifier ni contourner les protections existantes.

# 2. NOUVELLE RÈGLE FONDAMENTALE

**Un fichier CSV imparfait doit être accepté pour analyse.**

Le serveur ne doit pas refuser son dépôt pour les seules raisons suivantes :

- SKU en double.
- Colonnes inconnues.
- En-têtes manquants.
- Prix ambigus.
- Famille inconnue.
- Lignes irrégulières.
- Variantes difficiles à identifier.
- Valeurs métier invalides ou manquantes.

Ces anomalies doivent être remontées comme des problèmes à résoudre dans l'assistant.

**Attention : acceptation du dépôt ne signifie pas autorisation d'import.**

Les restrictions techniques et de sécurité restent applicables : taille maximale, ressources disponibles, fichier dangereux, droits insuffisants, corruption irrécupérable et autres limites justifiées.

Les contrôles métier finaux et le principe transactionnel tout ou rien restent impératifs.

# 3. ARCHITECTURE CIBLE

Mettre en place ce flux :

1. Fichier original reçu.
2. Conservation de ses octets et de son empreinte.
3. Lecture tolérante sans rejet métier prématuré.
4. Analyse automatique par Ollama.
5. Interprétation sémantique des colonnes et familles.
6. Reconstruction déterministe des produits et variantes par MMV.
7. Présentation des ambiguïtés et erreurs.
8. Corrections de l'utilisateur.
9. Revalidation déterministe.
10. Prévisualisation du résultat définitif.
11. Confirmation explicite.
12. Import transactionnel.

L'IA intervient automatiquement après la lecture technique minimale nécessaire pour exploiter le fichier.

Elle ne doit pas attendre un contrôle métier strict pour commencer à proposer son interprétation.

Pour les fichiers volumineux, utiliser un échantillonnage représentatif et des analyses complémentaires ciblées lorsque nécessaire. Ne pas prétendre qu'un modèle a examiné toutes les lignes si seules 40 lignes lui ont été transmises.

Le lecteur et le serveur doivent néanmoins inspecter toute la structure du fichier pour garantir la conservation des lignes.

**L'IA interprète. Le serveur valide. L'utilisateur arbitre.**

# 4. NOUVEAU DESIGN — ASSISTANT EN CINQ ÉTAPES

L'ancienne page unique est refusée.

Concevoir un véritable assistant avec une navigation horizontale en haut de page, indiquant les cinq étapes et leur état.

Respecter les références graphiques du Lot 6 et le design system actuel MMV V2.

Ne pas créer une interface générique étrangère à l'application.

## ÉTAPE 1 — DÉPOSER LE FICHIER

Interface simple :

- Zone glisser-déposer.
- Bouton de sélection.
- Nom du fichier.
- Taille.
- Fournisseur facultatif, selon les règles du module.
- Type de catalogue suggéré.
- Aperçu du fichier original.
- Action principale : « Analyser mon fichier ».

Ne pas demander à l'utilisateur de connaître la structure exacte du CSV.

Ne pas imposer la correspondance des colonnes avant l'analyse IA.

Le dépôt prépare automatiquement l'analyse.

## ÉTAPE 2 — ANALYSE AUTOMATIQUE PAR IA

C'est une étape centrale du nouveau parcours.

L'utilisateur doit voir l'activité réelle du système.

Afficher :

- Modèle Ollama réellement utilisé.
- État de connexion à Ollama.
- Nom du fichier analysé.
- Étape actuellement exécutée.
- Progression des opérations mesurables.
- Nombre de lignes et colonnes détectées.
- Familles identifiées lorsque l'information devient disponible.
- Anomalies structurelles détectées.
- Durée écoulée.
- Possibilité d'annulation.

Prévoir un panneau d'activité présentant des événements compréhensibles :

- Fichier reçu.
- Encodage détecté.
- Structure analysée.
- Échantillon préparé.
- Analyse Ollama en attente.
- Interprétation des colonnes en cours.
- Interprétation reçue.
- Validation métier en cours.
- Analyse terminée.

Ne jamais inventer un événement.

Ne jamais afficher de pourcentage fictif.

Pour une opération dont la progression exacte n'est pas mesurable, utiliser un indicateur indéterminé.

Pour les opérations mesurables, utiliser une barre de progression réelle.

Ne pas afficher le raisonnement interne ou les traces de pensée du modèle. Afficher les étapes observables, les résultats produits et les éventuelles explications structurées autorisées.

L'interface ne doit pas se figer pendant les 20 à 60 secondes que peut prendre l'analyse.

Prévoir un job asynchrone avec suivi de statut, annulation, traitement des délais et reconnexion.

La progression doit provenir d'états backend réels, via un mécanisme compatible avec l'architecture MMV (polling ou flux d'événements).

Ne pas ajouter une technologie temps réel simplement pour animer une barre de chargement.

## ÉTAPE 3 — VÉRIFIER ET CORRIGER

Afficher un tableau structuré et lisible.

Colonnes suggérées :

- Ligne d'origine.
- Famille.
- Produit.
- Variante.
- Référence.
- Prix ou attributs concernés.
- Résultat de l'analyse.
- Anomalies.
- Actions de correction.

Permettre de filtrer :

- Toutes les lignes.
- Lignes valides.
- Corrections nécessaires.
- Ambiguïtés.
- Valeurs non reconnues.
- Doublons potentiels.

Pour chaque anomalie :

1. Afficher la valeur originale.
2. Afficher l'interprétation proposée.
3. Expliquer précisément le problème.
4. Présenter les solutions possibles.
5. Permettre la correction.
6. Revalider la correction côté serveur.

Les corrections doivent pouvoir être effectuées directement dans l'assistant lorsque le type de correction le permet.

Ne pas obliger systématiquement l'utilisateur à ouvrir Excel et à modifier son fichier.

Préserver les octets de l'original.

Les corrections doivent être enregistrées dans une révision distincte ou une structure équivalente, avec leur provenance et leur auteur.

Ne pas présenter une correction non validée comme une résolution acquise.

### Cas particulier — SKU en double

La capture du propriétaire montre :

`Ligne 10 — SKU interne en double`

Le futur assistant doit expliquer si le conflit provient :

- Du fichier lui-même.
- D'une mauvaise correspondance de colonnes.
- D'un article déjà présent dans le catalogue.
- D'une variante mal associée.
- D'un véritable doublon.

Selon le diagnostic réel, proposer uniquement les actions licites et cohérentes :

- Corriger la correspondance.
- Associer à un article existant.
- Modifier une référence, après validation.
- Générer un SKU si le champ est réellement absent et que les règles MMV le permettent.

Ne jamais remplacer automatiquement un SKU explicite par un autre.

Ne jamais fusionner deux produits sur une simple intuition de l'IA.

### Corrections groupées

Permettre de corriger une correspondance commune à plusieurs lignes en une seule opération.

Exemple :

Une colonne fournisseur intitulée « Prix net HT » est reconnue comme prix d'achat HT.

L'utilisateur doit pouvoir confirmer cette correspondance pour l'ensemble des lignes concernées, après visualisation d'exemples représentatifs.

Les montants et les conséquences doivent rester clairement visibles.

Ne pas permettre une confirmation globale aveugle de champs commerciaux non vérifiés.

## ÉTAPE 4 — VÉRIFICATION AVANT IMPORT

Réaliser le véritable contrôle métier MMV.

Afficher :

- Produits à créer.
- Variantes à créer.
- Articles à modifier.
- Articles inchangés.
- Doublons.
- Lignes ignorées volontairement.
- Valeurs ambiguës.
- Erreurs bloquantes.
- Correspondances validées.
- Provenance des corrections.

Présenter une comparaison avant/après pour les modifications de produits existants.

Ne pas présenter « 0 création, 0 modification » comme un import réussi si l'utilisateur s'attendait à de nouvelles données.

Les erreurs restantes doivent être clairement identifiées.

Permettre un retour direct à l'étape 3 pour corriger le problème.

Le contrôle à blanc doit être calculé par le serveur et rester sans écriture métier.

## ÉTAPE 5 — CONFIRMATION ET IMPORT

N'autoriser l'import que lorsque les conditions métier sont satisfaites.

Présenter une synthèse définitive et demander une confirmation explicite.

Conserver :

- Import tout ou rien.
- Transactions PostgreSQL.
- Idempotence.
- Contrôles de permissions.
- Contrôle de concurrence.
- Audit.
- Gestion des réimports.

Pendant l'exécution, afficher le statut réel.

À la fin, afficher un résultat compréhensible avec les nombres réellement enregistrés.

Ne jamais afficher une réussite si l'opération n'a pas été confirmée par le backend.

# 5. HISTORIQUE DES IMPORTS

Refondre la section « Imports récents ».

Elle doit être :

- Compacte.
- Repliable et dépliable.
- Fermée par défaut.
- Accessible au clavier.
- Indépendante de l'étape active.
- Compatible avec les thèmes clair et sombre.

En position fermée, afficher seulement un titre et un bref résumé.

En position ouverte, afficher le tableau existant, avec ses actions « Reprendre » et les informations d'historique utiles.

Ne pas charger ou afficher inutilement un très grand tableau lorsque la section est fermée.

Ne supprimer aucun historique existant.

# 6. MODÈLE OLLAMA

La décision actuelle est :

**`qwen3.5:4b` comme modèle principal retenu pour la nouvelle architecture d'import catalogue, sous contrôle serveur.**

Ne plus coder en dur `llama3.2` dans l'interface.

La page doit afficher le modèle réellement configuré et réellement utilisé.

Différencier :

- Modèle configuré.
- Modèle actif.
- Modèle indisponible.
- Analyse par règles seules.
- Analyse IA terminée.
- Modèle de l'analyse historique.

L'intitulé « Qwen 3.5 4B » ne doit apparaître comme modèle actif que si le backend confirme que c'est bien le modèle exécuté.

La bascule de `llama3.2` vers `qwen3.5:4b` ne doit concerner que l'import catalogue prévu par ce mandat.

Ne pas modifier les autres usages éventuels d'Ollama.

# 7. DÉCISIONS COMPLÉMENTAIRES

Consigner les décisions de travail suivantes :

**Q-CSVAI-07 — Rétention des originaux**

La durée définitive reste à valider.

Prévoir une architecture permettant la purge contrôlée des octets sans suppression des preuves d'import.

Ne pas activer de purge automatique sur une durée inventée.

**Q-CSVAI-08 — Import pendant l'analyse**

Par défaut, ne pas appliquer le lot pendant qu'une analyse IA demandée est encore en cours.

Si Ollama échoue ou devient indisponible, proposer explicitement un mode de vérification sans IA, fondé sur les règles et les confirmations de l'utilisateur.

Aucun contournement silencieux.

**Q-CSVAI-09 — Rôle de l'IA**

L'IA est déclenchée par défaut pour les imports de catalogue pris en charge.

Cette règle répond à l'objectif d'expérience utilisateur, mais ne constitue pas une preuve de gain d'automatisation.

Mesurer les confirmations ajoutées, évitées et les erreurs détectées sur les catalogues réels.

Prévoir la possibilité de désactiver ou limiter cette assistance par configuration sans modifier les règles métier.

**Q-CSVAI-10 — Montures sans mot-clé**

Ne pas attribuer automatiquement une famille sur une simple supposition.

Présenter une suggestion de famille avec confirmation lorsque les preuves sont insuffisantes.

# 8. ARCHITECTURE TECHNIQUE

Conserver :

- Tauri 2.
- React et TypeScript.
- ASP.NET Core .NET 10.
- PostgreSQL 18 exclusivement.
- Ollama en local.
- Séparation Domain / Application / Infrastructure / API.
- Contrôle métier côté serveur.

Reprendre les composants prévus dans `CSV_IA_05_CONCEPTION.md` :

- `TolerantCsvReader`.
- `CsvDocument`.
- `CatalogSampler`.
- `ICatalogInterpreter`.
- `OllamaCatalogInterpreter`.
- `CatalogInterpretationValidator`.
- `CatalogMaterializer`.
- `ImportAnalysisQueue`.
- `ImportService`.
- `ImportView`.

Les noms finaux doivent respecter les conventions réelles du dépôt.

La transition doit être additive et compatible avec les routes et imports existants tant que le nouveau parcours n'est pas accepté.

Prévoir la reprise d'analyse et la protection contre les résultats arrivant après une correction utilisateur ou une annulation.

La validation d'un ancien résultat ne doit jamais écraser une correction plus récente.

# 9. MÉTHODE D'EXÉCUTION

## GATE 0 — AUDIT

Vérifier :

- Branche et HEAD.
- État Git.
- Worktree.
- Présence des résultats B0.
- Intégrité des preuves.
- Configuration Ollama.
- Code d'import actuel.
- Design Lot 6.
- Tests existants.
- État des migrations 0007 et 0008.

Protéger `apps/desktop/.env.development`.

Ne pas changer de branche de façon risquée si des modifications non commitées existent.

## GATE B — CONCEPTION VISUELLE ET TECHNIQUE

Produire les spécifications de la nouvelle architecture et des cinq écrans.

Créer des états visuels navigables ou un banc isolé si nécessaire :

- Dépôt.
- Analyse en attente.
- Analyse en cours.
- Analyse terminée.
- Correction SKU.
- Correction commerciale.
- Prévisualisation.
- Import réussi.
- Erreur technique.
- Historique replié et déplié.

Tester les états clair/sombre et différentes résolutions.

**Soumettre ce design au propriétaire avant intégration visuelle définitive.**

## GATE C1 — SOCLE TECHNIQUE

Après validation de la conception :

- Lecteur tolérant.
- Contrat v2.
- Validateur déterministe.
- Matérialisation.
- Gestion des jobs.
- Suivi de progression.
- Tests unitaires et de sécurité.

Ne pas modifier les bases persistantes.

## GATE C2 — API ET ASSISTANT

Mettre en œuvre les contrats API nécessaires dans le périmètre validé.

Intégrer les composants React et le nouveau parcours, derrière un mécanisme d'activation contrôlé si nécessaire.

Ne pas remplacer prématurément l'ancien parcours fonctionnel.

Les tests d'intégration doivent utiliser des environnements isolés ou les dispositifs QA autorisés.

## GATE SQL — MIGRATION 0009

Préparer un dossier SQL distinct si nécessaire.

Respecter les procédures historiques MMV :

- Migration additive.
- Vérification EF Core.
- SHA-256.
- Scripts idempotents.
- Verify SQL en lecture seule.
- Contrôles d'intégrité.
- Compatibilité avec 0007 et 0008.
- Préparation des sauvegardes.
- Analyse des risques.

**STOP avant application de 0009.**

L'application sur `mmv_v2_dev` et `mmv_v2_test` nécessite un GO SQL explicite et distinct du propriétaire.

Ne pas contourner cette interdiction par une écriture directe, un `EnsureCreated`, une migration automatique ou un changement manuel de schéma.

## GATE D — RECETTE

Après les autorisations nécessaires :

- Tests .NET.
- Tests Vitest.
- Tests PostgreSQL 18.
- Tests des droits.
- Tests d'idempotence.
- Tests d'annulation.
- Tests de concurrence.
- Tests de correction des valeurs.
- Tests des variantes.
- Tests des prix.
- Tests des injections.
- Tests du parcours natif Tauri.
- Tests de non-régression des autres modules.
- Tests sur les vrais catalogues anonymisés lorsqu'ils seront disponibles.

Contrôler que le nouveau parcours conserve les fonctionnalités déjà approuvées de la Phase 05.

# 10. CRITÈRES DE RECETTE PRIORITAIRES

Le parcours doit démontrer :

1. Un CSV avec un SKU en double est accepté pour analyse.
2. Le doublon est expliqué dans l'étape de correction.
3. L'utilisateur peut revenir en arrière sans perdre ses décisions.
4. Une correction groupée est revalidée par le serveur.
5. Qwen analyse automatiquement le catalogue lorsque l'option IA est active.
6. L'utilisateur voit des événements et états d'analyse réels.
7. L'interface ne se fige pas pendant l'inférence.
8. Le fichier original reste traçable.
9. Une anomalie métier empêche uniquement l'import non conforme, pas l'analyse préalable.
10. L'historique est replié par défaut.
11. Le modèle affiché correspond au modèle réellement utilisé.
12. Aucun prix ou attribut n'est inventé.
13. Aucun article n'est perdu silencieusement.
14. Aucun doublon n'est fusionné sans justification et contrôle.
15. Aucun import n'est effectué sans confirmation explicite.

# 11. LIVRABLES ATTENDUS

Fournir :

- Rapport d'audit initial.
- Architecture avant/après.
- Spécification des cinq étapes.
- États UI et captures.
- Matrice des interactions.
- Contrats API.
- Matrice des transitions de l'assistant.
- Plan d'implémentation par sous-lot.
- Tests et résultats.
- Liste des fichiers modifiés.
- Dossier SQL séparé si nécessaire.
- Risques et décisions ouvertes.
- Rapport final de livraison.

Distinguer strictement `PASS`, `FAIL`, `NOT_TESTED` et `BLOCKED`.

Mettre à jour les documents existants sans écraser l'historique.

# 12. RESTRICTIONS FINALES

Aucun reset ou clean Git.

Aucun changement de stack.

Aucun SQLite.

Aucune suppression de données.

Aucune modification des anciennes migrations.

Aucune application de 0009 sans GO.

Aucun changement des autres modules MMV hors dépendance démontrée et autorisée.

Aucun téléchargement de modèle sans autorisation.

Aucun commit, push, PR ou merge automatique.

Aucune déclaration de Phase 05 terminée.

## INSTRUCTION FINALE

Commence par l'audit du code réel et la conception de la nouvelle interface en cinq étapes.

Produis une proposition visuelle cohérente avec le Lot 6 ainsi que les contrats techniques nécessaires.

Puis présente le dossier GATE B pour revue du propriétaire.

**Ne réalise pas l'intégration applicative définitive avant validation de cette conception.**

L'objectif est un assistant d'import CSV réellement intelligent, transparent, ergonomique et sécurisé, et non l'ajout d'un bouton IA au formulaire existant.
