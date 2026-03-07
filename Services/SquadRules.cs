using AFG_Livescoring.Models;

namespace AFG_Livescoring.Services
{
    public static class SquadRules
    {
        public static (int Min, int Max) GetLimits(Competition c)
        {
            // Entraînement
            if (c.ScoringMode == ScoringMode.IndividualAllowed)
                return (1, 6);

            // Compétition
            return (3, 5);
        }
    }
}