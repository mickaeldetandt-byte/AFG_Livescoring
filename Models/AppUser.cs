namespace AFG_Livescoring.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        // Admin / Organizer / Club / Player
        public string Role { get; set; } = "Player";

        public bool IsActive { get; set; } = true;

        // Lien optionnel vers un joueur
        public int? PlayerId { get; set; }
        public Player? Player { get; set; }

        // Lien optionnel vers un club
        public int? ClubId { get; set; }
        public Club? Club { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}