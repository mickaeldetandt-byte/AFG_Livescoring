using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Players
{
    [Authorize(Roles = "Admin")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;

        public DetailsModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int id { get; set; }

        public Player? Player { get; set; }

        public PlayerSummary Summary { get; set; } = new();

        public List<CompetitionHistoryRow> History { get; set; } = new();

        public class PlayerSummary
        {
            public int CompetitionsCount { get; set; }
            public int CompletedRoundsCount { get; set; }

            public double AverageStrokes { get; set; }
            public int BestStrokes { get; set; }

            public double AverageToPar { get; set; }

            public int Birdies { get; set; }
            public int Pars { get; set; }
            public int Bogeys { get; set; }
            public int DoubleBogeysOrMore { get; set; }
        }

        public class CompetitionHistoryRow
        {
            public int CompetitionId { get; set; }
            public string CompetitionName { get; set; } = "";
            public DateTime CompetitionDate { get; set; }
            public string Mode { get; set; } = "";
            public int HolesPlayed { get; set; }
            public int TotalStrokes { get; set; }
            public int TotalPar { get; set; }
            public int ToPar { get; set; }
            public bool IsComplete { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Player = await _db.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Player == null)
                return RedirectToPage("/Players/Index");

            var rounds = await _db.Rounds
                .AsNoTracking()
                .Include(r => r.Competition)
                .Where(r => r.PlayerId == id && r.Competition != null)
                .ToListAsync();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = await _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .ToListAsync();

            var courseIds = rounds
                .Where(r => r.Competition?.CourseId != null)
                .Select(r => r.Competition!.CourseId!.Value)
                .Distinct()
                .ToList();

            var holes = await _db.Holes
                .AsNoTracking()
                .Where(h => courseIds.Contains(h.CourseId))
                .ToListAsync();

            var holesByCourse = holes
                .GroupBy(h => h.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.HoleNumber, x => x.Par)
                );

            var scoresByRoundId = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.HoleNumber).ToList());

            var history = new List<CompetitionHistoryRow>();

            int birdies = 0;
            int pars = 0;
            int bogeys = 0;
            int doubleBogeysOrMore = 0;

            foreach (var round in rounds
                         .OrderByDescending(r => r.Competition!.Date)
                         .ThenByDescending(r => r.CompetitionId))
            {
                scoresByRoundId.TryGetValue(round.Id, out var playedScores);
                playedScores ??= new List<Score>();

                int holesPlayed = playedScores
                    .Select(s => s.HoleNumber)
                    .Distinct()
                    .Count();

                int totalStrokes = playedScores.Sum(s => s.Strokes);

                int totalPar = 0;

                if (round.Competition?.CourseId != null &&
                    holesByCourse.TryGetValue(round.Competition.CourseId.Value, out var parByHole))
                {
                    foreach (var sc in playedScores)
                    {
                        if (parByHole.TryGetValue(sc.HoleNumber, out int par))
                        {
                            totalPar += par;

                            int diff = sc.Strokes - par;

                            if (diff == -1)
                                birdies++;
                            else if (diff == 0)
                                pars++;
                            else if (diff == 1)
                                bogeys++;
                            else if (diff >= 2)
                                doubleBogeysOrMore++;
                        }
                    }
                }

                history.Add(new CompetitionHistoryRow
                {
                    CompetitionId = round.CompetitionId,
                    CompetitionName = round.Competition?.Name ?? $"Compétition #{round.CompetitionId}",
                    CompetitionDate = round.Competition?.Date ?? DateTime.MinValue,
                    Mode = round.Competition?.Mode ?? "",
                    HolesPlayed = holesPlayed,
                    TotalStrokes = totalStrokes,
                    TotalPar = totalPar,
                    ToPar = holesPlayed > 0 ? totalStrokes - totalPar : 0,
                    IsComplete = holesPlayed >= 18
                });
            }

            var completedHistory = history
                .Where(h => h.IsComplete)
                .ToList();

            Summary = new PlayerSummary
            {
                CompetitionsCount = history.Count,
                CompletedRoundsCount = completedHistory.Count,
                AverageStrokes = completedHistory.Any() ? completedHistory.Average(h => h.TotalStrokes) : 0,
                BestStrokes = completedHistory.Any() ? completedHistory.Min(h => h.TotalStrokes) : 0,
                AverageToPar = completedHistory.Any() ? completedHistory.Average(h => h.ToPar) : 0,
                Birdies = birdies,
                Pars = pars,
                Bogeys = bogeys,
                DoubleBogeysOrMore = doubleBogeysOrMore
            };

            History = history;

            return Page();
        }

        public string FormatToPar(double value, bool hasData)
        {
            if (!hasData) return "-";

            var rounded = Math.Round(value, 1);

            if (rounded == 0) return "E";
            if (rounded > 0) return $"+{rounded:0.0}";
            return rounded.ToString("0.0");
        }

        public string FormatToParInt(int value, bool hasData)
        {
            if (!hasData) return "-";
            if (value == 0) return "E";
            if (value > 0) return $"+{value}";
            return value.ToString();
        }

        public string GetToParCssClass(int value, bool hasData)
        {
            if (!hasData) return "";
            if (value < 0) return "par-under";
            if (value > 0) return "par-over";
            return "par-even";
        }
    }
}