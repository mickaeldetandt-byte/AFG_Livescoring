namespace AFG_Livescoring.Models
{
    public class TeamPlayer
    {
        public int Id { get; set; }

        public int TeamId { get; set; }
        public Team Team { get; set; } = default!;

        public int PlayerId { get; set; }
        public Player Player { get; set; } = default!;

        // 1 ou 2 pour garder un ordre simple dans l'équipe
        public int Order { get; set; }
    }
}