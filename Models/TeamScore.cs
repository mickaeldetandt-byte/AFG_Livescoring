namespace AFG_Livescoring.Models
{
    public class TeamScore
    {
        public int Id { get; set; }

        public int TeamRoundId { get; set; }
        public TeamRound? TeamRound { get; set; }

        public int HoleNumber { get; set; }

        // Score retenu pour le classement / leaderboard
        public int Strokes { get; set; }

        // Utilisés pour le format Doubles Fourball
        public int? Player1Strokes { get; set; }
        public int? Player2Strokes { get; set; }
    }
}