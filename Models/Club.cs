namespace AFG_Livescoring.Models
{
    public class Club
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
        public string? City { get; set; }
        public string? Country { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}