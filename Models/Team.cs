using System.ComponentModel.DataAnnotations;

namespace AFG_Livescoring.Models
{
    public class Team
    {
        public int Id { get; set; }

        public int CompetitionId { get; set; }
        public Competition Competition { get; set; } = default!;

        [MaxLength(80)]
        public string Name { get; set; } = string.Empty;

        public int? SquadId { get; set; }
        public Squad? Squad { get; set; }

        public bool IsActive { get; set; } = true;

        public List<TeamPlayer> TeamPlayers { get; set; } = new();
        public List<TeamRound> TeamRounds { get; set; } = new();
    }
}