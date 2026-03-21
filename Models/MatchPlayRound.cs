using System.ComponentModel.DataAnnotations;

namespace AFG_Livescoring.Models
{
    public class MatchPlayRound
    {
        public int Id { get; set; }

        [Required]
        public int CompetitionId { get; set; }
        public Competition? Competition { get; set; }

        [Required]
        public int SquadId { get; set; }
        public Squad? Squad { get; set; }

        [Required]
        public int TeamAId { get; set; }
        public Team? TeamA { get; set; }

        [Required]
        public int TeamBId { get; set; }
        public Team? TeamB { get; set; }

        public int CurrentHole { get; set; } = 1;

        public bool IsFinished { get; set; } = false;

        public int? WinnerTeamId { get; set; }
        public Team? WinnerTeam { get; set; }

        public string StatusText { get; set; } = "AS";
        public string ResultText { get; set; } = "";

        public ICollection<MatchPlayHoleResult> HoleResults { get; set; } = new List<MatchPlayHoleResult>();
    }
}