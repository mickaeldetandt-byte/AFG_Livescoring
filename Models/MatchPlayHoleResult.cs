using System.ComponentModel.DataAnnotations;

namespace AFG_Livescoring.Models
{
    public class MatchPlayHoleResult
    {
        public int Id { get; set; }

        public int MatchPlayRoundId { get; set; }
        public MatchPlayRound? MatchPlayRound { get; set; }

        public int HoleNumber { get; set; }

        // Scores individuels
        public int? TeamAPlayer1Score { get; set; }
        public int? TeamAPlayer2Score { get; set; }

        public int? TeamBPlayer1Score { get; set; }
        public int? TeamBPlayer2Score { get; set; }

        // Scores calculés automatiquement
        public int? TeamAScore { get; set; }
        public int? TeamBScore { get; set; }

        public bool IsHalved { get; set; }

        public int? WinnerTeamId { get; set; }
    }
}