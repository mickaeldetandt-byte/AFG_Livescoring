# AFG LiveScoring

---

## V1.0.1 – Hotfix terrain (stabilisation)

Date : 25/03/2026

### Positionnement
Version dédiée à la correction des bugs critiques identifiés en conditions réelles sur le terrain.  
Objectif : fiabiliser le scoring et simplifier l’usage mobile avant toute évolution fonctionnelle.

### Fonctionnalités incluses
- Gestion des compétitions
- Gestion des joueurs
- Gestion des squads
- Gestion des rounds
- Scoring squad (mode principal)
- Scoring équipe
- Match play individuel
- Match play double
- Leaderboard live
- Display / ClubHouse
- Résultats
- Export CSV
- Historique compétitions
- Profils / stats joueurs

### Évolution importante
- Suppression de l’accès "Score Joueur" dans le leaderboard  
→ Unification du scoring :
  - Score Squad (individuel)
  - Score équipe (double)
  - Match Play dédié

### Correctifs terrain (prioritaires)
- Correction du blocage de modification des scores en individuel
- Correction du blocage en Match Play (édition impossible)
- Correction du Match Play qui s’arrêtait avant 18 trous
- Ajout des boutons + / - en Match Play
- Correction de la fin de match play
- Possibilité d’ajouter un joueur après démarrage (mode entraînement)
- Possibilité d’affecter un joueur à un squad existant
- Amélioration accès mobile au scoring (accès direct squads)

### UX / Mobile
- Ajout d’un accès direct au scoring depuis le leaderboard
- Réduction du nombre d’actions pour scorer
- Navigation simplifiée sur téléphone

### Sécurité / accès
- Rôles Admin / Club
- Accès public au live
- Token scoring
- Blocage du scoring si compétition en brouillon
- Blocage du scoring si compétition terminée
- Verrouillage des cartes
- Contrôle d’accès cohérent sur les pages critiques

### Statut
Version stabilisée validée après tests terrain.  
Prête pour usage réel en conditions.

---

## V1.0.0 – Version stable terrain initiale

Date : 21/03/2026

### Positionnement
Première version complète fonctionnelle permettant l’utilisation du scoring en conditions réelles.

### Fonctionnalités incluses
- Gestion des compétitions
- Gestion des joueurs
- Gestion des squads
- Gestion des rounds
- Scoring individuel
- Scoring squad
- Scoring équipe
- Match play individuel
- Match play double
- Leaderboard live
- Display / ClubHouse
- Résultats
- Export CSV
- Historique compétitions
- Profils / stats joueurs

### Sécurité / accès
- Rôles Admin / Club
- Accès public au live
- Token scoring
- Blocage du scoring si compétition en brouillon
- Blocage du scoring si compétition terminée
- Verrouillage des cartes
- Contrôle d’accès cohérent sur les pages critiques

### Correctifs validés avant gel
- Correction du retour au trou courant dans Score Squad
- Correction du verrouillage après fin de compétition
- Correction du blocage du scoring en brouillon
- Boutons de scoring grisés si scoring interdit
- Cohérence backend / frontend sur le cycle de compétition

### Statut
Version figée et stable pour usage terrain.

---

## Règle de version
- V1.0.X = corrections / stabilisation terrain
- V1.1.0 = amélioration UX / mobile / confort / export
- V2.0.0 = évolution majeure (multi-club, multi-round, structure)