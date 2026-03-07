namespace AFG_Livescoring.Models
{
    public class Course
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public List<Hole> Holes { get; set; } = new();
    }
}