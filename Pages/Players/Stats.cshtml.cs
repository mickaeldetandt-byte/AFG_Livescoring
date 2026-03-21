using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Players
{
    [Authorize(Roles = "Admin")]
    public class StatsModel : PageModel
    {
        private readonly AppDbContext _db;

        public StatsModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int? playerId { get; set; }

        public List<PlayerStatsRow> PlayersStats { get; set; } = new();

        public class PlayerStatsRow
        {
            public int PlayerId { get; set; }
            public string PlayerName { get; set; } = "";
            public int CompetitionsCount { get; set; }
            public int CompletedRoundsCount { get; set; }
            public double AverageStrokes { get; set; }
            public int BestStrokes { get; set; }
            public double AverageToPar { get; set; }
            public List<PlayerHistoryRow> History { get; set; } = new();
        }

        public class PlayerHistoryRow
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

        public async Task OnGetAsync()
        {
            var playersQuery = _db.Players
                .AsNoTracking()
                .Where(p => p.IsActive);

            if (playerId.HasValue)
                playersQuery = playersQuery.Where(p => p.Id == playerId.Value);

            var players = await playersQuery
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToListAsync();

            var playerIds = players.Select(p => p.Id).ToHashSet();

            var rounds = await _db.Rounds
                .AsNoTracking()
                .Include(r => r.Player)
                .Include(r => r.Competition)
                .Where(r => r.Player != null && r.Competition != null && playerIds.Contains(r.PlayerId))
                .ToListAsync();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = await _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .ToListAsync();

            var competitions = await _db.Competitions
                .AsNoTracking()
                .ToListAsync();

            var courseIds = competitions
                .Where(c => c.CourseId != null)
                .Select(c => c.CourseId!.Value)
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

            var stats = new List<PlayerStatsRow>();

            foreach (var player in players)
            {
                var playerRounds = rounds
                    .Where(r => r.PlayerId == player.Id)
                    .OrderByDescending(r => r.Competition!.Date)
                    .ThenByDescending(r => r.CompetitionId)
                    .ToList();

                var history = new List<PlayerHistoryRow>();

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
                        totalPar = playedScores.Sum(s =>
                            parByHole.TryGetValue(s.HoleNumber, out var par) ? par : 0);
                    }

                    history.Add(new PlayerHistoryRow
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

                stats.Add(new PlayerStatsRow
                {
                    PlayerId = player.Id,
                    PlayerName = $"{player.FirstName} {player.LastName}".Trim(),
                    CompetitionsCount = history.Count,
                    CompletedRoundsCount = completedHistory.Count,
                    AverageStrokes = completedHistory.Any() ? completedHistory.Average(h => h.TotalStrokes) : 0,
                    BestStrokes = completedHistory.Any() ? completedHistory.Min(h => h.TotalStrokes) : 0,
                    AverageToPar = completedHistory.Any() ? completedHistory.Average(h => h.ToPar) : 0,
                    History = history
                });
            }

            PlayersStats = stats
                .OrderByDescending(p => p.CompetitionsCount)
                .ThenBy(p => p.AverageToPar == 0 && p.CompletedRoundsCount == 0 ? double.MaxValue : p.AverageToPar)
                .ThenBy(p => p.PlayerName)
                .ToList();
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
    }
}