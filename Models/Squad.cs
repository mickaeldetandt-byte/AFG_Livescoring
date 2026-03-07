using System.ComponentModel.DataAnnotations;

namespace AFG_Livescoring.Models
{
    public class Squad
    {
        public int Id { get; set; }

        public int CompetitionId { get; set; }
        public Competition Competition { get; set; } = default!;

        [MaxLength(50)]
        public string Name { get; set; } = "Squad";

        [Range(1, 18)]
        public int StartHole { get; set; } = 1;

        public DateTime? StartTime { get; set; }
    }
}