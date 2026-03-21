using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
    public class PublicModel : PageModel
    {
        private readonly AppDbContext _db;

        public PublicModel(AppDbContext db)
        {
            _db = db;
        }

        public List<PublicCompetitionRow> Competitions { get; set; } = new();

        public class PublicCompetitionRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public DateTime Date { get; set; }
            public string Mode { get; set; } = "";
            public string CourseName { get; set; } = "";
            public CompetitionType CompetitionType { get; set; }
            public CompetitionStatus Status { get; set; }

            public int PlayerCount { get; set; }
            public bool HasStarted { get; set; }
            public int CompletedRounds { get; set; }

            public bool IsTraining =>
                string.Equals(Mode, "Training", StringComparison.OrdinalIgnoreCase);
        }

        public async Task OnGetAsync()
        {
            var competitions = await _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .Where(c => c.Visibility == CompetitionVisibility.Public && c.IsActive)
                .OrderByDescending(c => c.Date)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var competitionIds = competitions.Select(c => c.Id).ToList();

            var rounds = await _db.Rounds
                .AsNoTracking()
                .Where(r => competitionIds.Contains(r.CompetitionId))
                .ToListAsync();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = roundIds.Any()
                ? await _db.Scores
                    .AsNoTracking()
                    .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                    .ToListAsync()
                : new List<Score>();

            var holesPlayedByRoundId = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.HoleNumber).Distinct().Count()
                );

            var rows = new List<PublicCompetitionRow>();

            foreach (var comp in competitions)
            {
                var compRounds = rounds
                    .Where(r => r.CompetitionId == comp.Id)
                    .ToList();

                int playerCount = compRounds.Count;
                bool hasStarted = false;
                int completedRounds = 0;

                foreach (var round in compRounds)
                {
                    int holesPlayed = holesPlayedByRoundId.TryGetValue(round.Id, out var hp) ? hp : 0;

                    if (holesPlayed > 0)
                        hasStarted = true;

                    if (holesPlayed >= 18)
                        completedRounds++;
                }

                rows.Add(new PublicCompetitionRow
                {
                    Id = comp.Id,
                    Name = comp.Name,
                    Date = comp.Date,
                    Mode = comp.Mode,
                    CourseName = comp.Course?.Name ?? "-",
                    CompetitionType = comp.CompetitionType,
                    Status = comp.Status,
                    PlayerCount = playerCount,
                    HasStarted = hasStarted,
                    CompletedRounds = completedRounds
                });
            }

            Competitions = rows;
        }

        public string FormatStatus(CompetitionStatus status)
        {
            return status switch
            {
                CompetitionStatus.Draft => "Brouillon",
                CompetitionStatus.InProgress => "En cours",
                CompetitionStatus.Finished => "Terminée",
                _ => status.ToString()
            };
        }
    }
}