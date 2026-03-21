using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Competitions
{
    [Authorize(Roles = "Admin,Club")]
    public class ResultsModel : PageModel
    {
        private readonly AppDbContext _db;

        public ResultsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<CompetitionRow> Competitions { get; set; } = new();

        public class CompetitionRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public DateTime Date { get; set; }
            public string Mode { get; set; } = "";
            public string CourseName { get; set; } = "";
            public int PlayerCount { get; set; }
            public bool HasStarted { get; set; }
            public bool IsFinished { get; set; }
        }

        public async Task OnGetAsync()
        {
            var competitions = await _db.Competitions
                .Include(c => c.Course)
                .OrderByDescending(c => c.Date)
                .ToListAsync();

            var rows = new List<CompetitionRow>();

            foreach (var comp in competitions)
            {
                var rounds = await _db.Rounds
                    .Where(r => r.CompetitionId == comp.Id)
                    .ToListAsync();

                var roundIds = rounds.Select(r => r.Id).ToList();

                var scores = await _db.Scores
                    .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                    .ToListAsync();

                int players = rounds.Count;
                bool hasStarted = scores.Any();
                bool isFinished = false;

                if (players > 0)
                {
                    int completedRounds = 0;

                    foreach (var round in rounds)
                    {
                        int playedHoles = scores.Count(s => s.RoundId == round.Id);
                        if (playedHoles >= 18)
                            completedRounds++;
                    }

                    isFinished = completedRounds == players;
                }

                rows.Add(new CompetitionRow
                {
                    Id = comp.Id,
                    Name = comp.Name,
                    Date = comp.Date,
                    Mode = comp.Mode,
                    CourseName = comp.Course?.Name ?? "-",
                    PlayerCount = players,
                    HasStarted = hasStarted,
                    IsFinished = isFinished
                });
            }

            Competitions = rows;
        }
    }
}