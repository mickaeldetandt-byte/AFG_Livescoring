using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

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
            public int TotalRounds { get; set; }

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

            var metricsByCompetition =
                await CompetitionMetricsCalculator.CalculateAsync(
                    _db,
                    competitions,
                    HttpContext.RequestAborted);

            var rows = new List<PublicCompetitionRow>();

            foreach (var comp in competitions)
            {
                var metrics = metricsByCompetition[comp.Id];

                rows.Add(new PublicCompetitionRow
                {
                    Id = comp.Id,
                    Name = comp.Name,
                    Date = comp.Date,
                    Mode = comp.Mode,
                    CourseName = comp.Course?.Name ?? "-",
                    CompetitionType = comp.CompetitionType,
                    Status = comp.Status,
                    PlayerCount = metrics.ParticipantsCount,
                    HasStarted = metrics.HasStarted,
                    CompletedRounds = metrics.CompletedRounds,
                    TotalRounds = metrics.TotalRounds
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
