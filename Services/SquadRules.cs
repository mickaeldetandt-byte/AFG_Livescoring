using AFG_Livescoring.Models;

namespace AFG_Livescoring.Services
{
    public static class SquadRules
    {
        public static (int Min, int Max) GetLimits(Competition c)
        {
            // Match Play individuel : 2 à 6 joueurs possibles dans un même squad
            // (plusieurs matchs 1v1 dans le même départ)
            if (c.CompetitionType == CompetitionType.MatchPlayIndividual)
                return (2, 6);

            // Match Play doubles : 4 joueurs = 2 équipes de 2
            if (c.CompetitionType == CompetitionType.MatchPlayScramble
                || c.CompetitionType == CompetitionType.MatchPlayFoursome
                || c.CompetitionType == CompetitionType.MatchPlayFourball)
            {
                return (4, 4);
            }

            // Entraînement
            if (c.ScoringMode == ScoringMode.IndividualAllowed)
                return (1, 6);

            // Compétition classique (stroke play)
            return (3, 5);
        }
    }
}