namespace AFG_Livescoring.Models
{
    public class Round
    {
        public int Id { get; set; }

        public int PlayerId { get; set; }
        public Player? Player { get; set; }

        public int CompetitionId { get; set; }
        public Competition? Competition { get; set; }

        public int? SquadId { get; set; }
        public Squad? Squad { get; set; }

        public bool IsLocked { get; set; } = false;

        // ✅ Accès invité sécurisé
        public string? PublicToken { get; set; }
    }
}