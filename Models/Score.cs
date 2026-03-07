namespace AFG_Livescoring.Models
{
    public class Score
    {
        public int Id { get; set; }

        public int RoundId { get; set; }
        public Round? Round { get; set; }

        public int HoleNumber { get; set; }  // 1..18
        public int Strokes { get; set; }     // coups
    }
}