using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Players
{
    [Authorize(Roles = "Admin")]
    public class RankingModel : PageModel
    {
        private readonly AppDbContext _db;

        public RankingModel(AppDbContext db)
        {
            _db = db;
        }

        public List<RankingRow> Rankings { get; set; } = new();

        public class RankingRow
        {
            public int Rank { get; set; }
            public int PlayerId { get; set; }
            public string PlayerName { get; set; } = "";

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

        public async Task OnGetAsync()
        {
            var players = await _db.Players
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToListAsync();

            var playerIds = players.Select(p => p.Id).ToHashSet();

            var rounds = await _db.Rounds
                .AsNoTracking()
                .Include(r => r.Competition)
                .Where(r => playerIds.Contains(r.PlayerId) && r.Competition != null)
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

            var rows = new List<RankingRow>();

            foreach (var player in players)
            {
                var playerRounds = rounds
                    .Where(r => r.PlayerId == player.Id)
                    .ToList();

                int birdies = 0;
                int pars = 0;
                int bogeys = 0;
                int doubleBogeysOrMore = 0;

                var completedRounds = new List<(int TotalStrokes, int ToPar)>();

                foreach (var round in playerRounds)
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

                    if (holesPlayed >= 18)
                    {
                        completedRounds.Add((totalStrokes, totalStrokes - totalPar));
                    }
                }

                rows.Add(new RankingRow
                {
                    PlayerId = player.Id,
                    PlayerName = $"{player.FirstName} {player.LastName}".Trim(),
                    CompetitionsCount = playerRounds.Count,
                    CompletedRoundsCount = completedRounds.Count,
                    AverageStrokes = completedRounds.Any() ? completedRounds.Average(r => r.TotalStrokes) : 0,
                    BestStrokes = completedRounds.Any() ? completedRounds.Min(r => r.TotalStrokes) : 0,
                    AverageToPar = completedRounds.Any() ? completedRounds.Average(r => r.ToPar) : 0,
                    Birdies = birdies,
                    Pars = pars,
                    Bogeys = bogeys,
                    DoubleBogeysOrMore = doubleBogeysOrMore
                });
            }

            Rankings = rows
                .OrderBy(r => r.CompletedRoundsCount == 0 ? double.MaxValue : r.AverageToPar)
                .ThenBy(r => r.CompletedRoundsCount == 0 ? int.MaxValue : r.BestStrokes)
                .ThenByDescending(r => r.CompletedRoundsCount)
                .ThenBy(r => r.PlayerName)
                .ToList();

            int currentRank = 0;
            foreach (var row in Rankings)
            {
                currentRank++;
                row.Rank = currentRank;
            }
        }

        public string FormatToPar(double value, bool hasData)
        {
            if (!hasData) return "-";

            var rounded = Math.Round(value, 1);

            if (rounded == 0) return "E";
            if (rounded > 0) return $"+{rounded:0.0}";
            return rounded.ToString("0.0");
        }

        public string GetToParCssClass(double value, bool hasData)
        {
            if (!hasData) return "";
            if (value < 0) return "par-under";
            if (value > 0) return "par-over";
            return "par-even";
        }
    }
}