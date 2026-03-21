namespace AFG_Livescoring.Models
{
    public class TeamRound
    {
        public int Id { get; set; }

        public int CompetitionId { get; set; }
        public Competition Competition { get; set; } = default!;

        public int TeamId { get; set; }
        public Team Team { get; set; } = default!;

        public int? SquadId { get; set; }
        public Squad? Squad { get; set; }

        public bool IsLocked { get; set; } = false;

        public List<TeamScore> Scores { get; set; } = new();
    }
}