using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class LeaderboardModel : PageModel
    {
        private readonly AppDbContext _db;

        public LeaderboardModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        public Competition? Competition { get; set; }

        public bool IsTraining => Competition?.ScoringMode == ScoringMode.IndividualAllowed;
        public bool IsCompetition => Competition?.ScoringMode == ScoringMode.SquadOnly;

        public List<LeaderboardRow> Rows { get; set; } = new();

        public int CourseParTotal { get; set; }
        public int LeaderPlayedPar { get; set; }
        public LeaderboardRow? LeaderRow { get; set; }

        // ✅ Progression par squad (trou actuel / terminé)
        public Dictionary<int, SquadProgress> SquadProgressById { get; set; } = new();

        public class SquadProgress
        {
            public int SquadId { get; set; }
            public string SquadName { get; set; } = "";
            public int StartHole { get; set; }
            public int MinHolesPlayed { get; set; } // min des joueurs du squad
            public bool Finished => MinHolesPlayed >= 18;
            public int? CurrentHole { get; set; }   // null si terminé
        }

        public class LeaderboardRow
        {
            public int RoundId { get; set; }
            public string PlayerName { get; set; } = "";

            public int HolesPlayed { get; set; }
            public int TotalStrokes { get; set; }

            public int TotalPar { get; set; }
            public int DiffToPar { get; set; }

            public int Difference { get; set; } // écart leader
            public double Average { get; set; }

            public int? SquadId { get; set; }
            public string? SquadName { get; set; }
            public int? SquadStartHole { get; set; }
        }

        public IActionResult OnGet()
        {
            Competition = _db.Competitions
                .Include(c => c.Course)
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            if (Competition.CourseId == null)
            {
                TempData["Error"] = "Aucun parcours n'est associé à cette compétition. Le Live est désactivé.";
                return RedirectToPage("/Competitions");
            }

            // Par par trou
            var parByHole = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value)
                .ToDictionary(h => h.HoleNumber, h => h.Par);

            CourseParTotal = parByHole.Count > 0 ? parByHole.Values.Sum() : 0;

            // Rounds + player + squad
            var rounds = _db.Rounds
                .Include(r => r.Player)
                .Include(r => r.Squad)
                .AsNoTracking()
                .Where(r => r.CompetitionId == CompetitionId)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            // Scores > 0
            var scores = _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .ToList();

            // ✅ HolesPlayed robust (distinct hole numbers)
            var holesPlayedByRoundId = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.HoleNumber).Distinct().Count()
                );

            // Group scores by round
            var scoresByRound = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ✅ Progression squad
            BuildSquadProgress(rounds, holesPlayedByRoundId);

            Rows = new List<LeaderboardRow>();

            foreach (var r in rounds)
            {
                scoresByRound.TryGetValue(r.Id, out var playerScores);
                playerScores ??= new List<Score>();

                int holesPlayed = holesPlayedByRoundId.TryGetValue(r.Id, out int hp) ? hp : 0;

                int totalStrokes = playerScores.Sum(s => s.Strokes);

                int totalPar = 0;
                foreach (var sc in playerScores)
                {
                    if (parByHole.TryGetValue(sc.HoleNumber, out int par))
                        totalPar += par;
                }

                int diffToPar = (holesPlayed == 0) ? 0 : (totalStrokes - totalPar);

                double average = holesPlayed > 0 ? (double)totalStrokes / holesPlayed : 0;

                Rows.Add(new LeaderboardRow
                {
                    RoundId = r.Id,
                    PlayerName = r.Player != null
                        ? (r.Player.FirstName + " " + r.Player.LastName).Trim()
                        : ("PlayerId=" + r.PlayerId),

                    HolesPlayed = holesPlayed,
                    TotalStrokes = totalStrokes,
                    TotalPar = totalPar,
                    DiffToPar = diffToPar,
                    Average = average,

                    SquadId = r.SquadId,
                    SquadName = r.Squad?.Name,
                    SquadStartHole = r.Squad?.StartHole
                });
            }

            // ✅ Tri correct leaderboard :
            // 1. DiffToPar croissant
            // 2. Plus de trous joués devant à score égal
            // 3. TotalStrokes croissant
            // 4. Nom
            // joueurs sans score tout en bas
            Rows = Rows
                .OrderBy(x => x.HolesPlayed == 0 ? int.MaxValue : x.DiffToPar)
                .ThenByDescending(x => x.HolesPlayed)
                .ThenBy(x => x.TotalStrokes)
                .ThenBy(x => x.PlayerName)
                .ToList();

            // ✅ Leader réel
            var leader = Rows.FirstOrDefault(r => r.HolesPlayed > 0);
            LeaderRow = leader;
            LeaderPlayedPar = leader?.TotalPar ?? 0;

            // ✅ Écart au leader basé sur le ±Par
            foreach (var row in Rows)
            {
                row.Difference = (leader != null && row.HolesPlayed > 0)
                    ? row.DiffToPar - leader.DiffToPar
                    : 0;
            }

            return Page();
        }

        private void BuildSquadProgress(List<Round> rounds, Dictionary<int, int> holesPlayedByRoundId)
        {
            SquadProgressById = new Dictionary<int, SquadProgress>();

            var bySquad = rounds
                .Where(r => r.SquadId.HasValue && r.Squad != null)
                .GroupBy(r => r.SquadId!.Value);

            foreach (var g in bySquad)
            {
                int squadId = g.Key;
                var firstRound = g.First();
                int startHole = firstRound.Squad!.StartHole;
                string squadName = firstRound.Squad!.Name;

                int minHoles = int.MaxValue;

                foreach (var r in g)
                {
                    int hp = holesPlayedByRoundId.TryGetValue(r.Id, out int v) ? v : 0;
                    if (hp < minHoles) minHoles = hp;
                }

                if (minHoles == int.MaxValue) minHoles = 0;

                int? currentHole = null;
                if (minHoles < 18)
                    currentHole = NextHole(startHole, minHoles);

                SquadProgressById[squadId] = new SquadProgress
                {
                    SquadId = squadId,
                    SquadName = squadName,
                    StartHole = startHole,
                    MinHolesPlayed = minHoles,
                    CurrentHole = currentHole
                };
            }
        }

        private int NextHole(int startHole, int offset)
        {
            return ((startHole - 1 + offset) % 18) + 1;
        }
    }
}