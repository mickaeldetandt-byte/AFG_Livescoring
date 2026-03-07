using System;

namespace AFG_Livescoring.Models
{
    public class Competition
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Today;

        public bool IsActive { get; set; } = true;

        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        public ScoringMode ScoringMode { get; set; } = ScoringMode.SquadOnly;
    }

    public enum ScoringMode
    {
        SquadOnly = 0,
        IndividualAllowed = 1
    }
}