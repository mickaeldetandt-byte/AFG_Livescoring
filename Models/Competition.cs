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

        // Competition = mode strict
        // Training    = mode souple
        public string Mode { get; set; } = "Competition";

        public CompetitionType CompetitionType { get; set; } = CompetitionType.IndividualStrokePlay;

        // 🔥 NOUVEAU — VISIBILITÉ
        public CompetitionVisibility Visibility { get; set; } = CompetitionVisibility.Public;

        // 🔥 NOUVEAU — STATUT
        public CompetitionStatus Status { get; set; } = CompetitionStatus.Draft;

        // 🔥 NOUVEAU — CLUB ORGANISATEUR
        public int? ClubId { get; set; }
        public Club? Club { get; set; }

        // 🔥 NOUVEAU — UTILISATEUR CRÉATEUR
        public int? CreatedByUserId { get; set; }
        public AppUser? CreatedByUser { get; set; }
    }

    public enum ScoringMode
    {
        SquadOnly = 0,
        IndividualAllowed = 1
    }

    public enum CompetitionType
    {
        IndividualStrokePlay = 0,
        DoublesScramble = 1,
        DoublesFourball = 2,
        DoublesFoursome = 3,

        MatchPlayIndividual = 10,
        MatchPlayFourball = 11,
        MatchPlayFoursome = 12,
        MatchPlayScramble = 13
    }

    // 🔥 NOUVEAU
    public enum CompetitionVisibility
    {
        Private = 0,
        Club = 1,
        Public = 2
    }

    // 🔥 NOUVEAU
    public enum CompetitionStatus
    {
        Draft = 0,
        InProgress = 1,
        Finished = 2
    }
}