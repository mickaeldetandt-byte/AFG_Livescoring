namespace AFG_Livescoring.Models
{
    public class Hole
    {
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int HoleNumber { get; set; } // 1..18

        public int Par { get; set; } = 3;   // 2..6 en pratique

        public bool IsActive { get; set; } = true;
    }
}