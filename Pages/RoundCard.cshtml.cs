using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using System.Security.Claims;

namespace AFG_Livescoring.Pages
{
    public class RoundCardModel : PageModel
    {
        private readonly AppDbContext _db;

        public RoundCardModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int RoundId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public Round? Round { get; set; }
        public Competition? Competition { get; set; }
        public Squad? Squad { get; set; }

        public bool IsTraining =>
            string.Equals(Competition?.Mode, "Training", StringComparison.OrdinalIgnoreCase);

        public bool IsCompetition => !IsTraining;

        public int CourseParTotal { get; set; }
        public int HolesPlayed { get; set; }
        public int TotalStrokes { get; set; }
        public int PlayedPar { get; set; }
        public int DiffToPar { get; set; }
        public double Average { get; set; }

        public int OutPlayedHoles { get; set; }
        public int OutTotalStrokes { get; set; }
        public int OutTotalPar { get; set; }
        public int OutDiffToPar { get; set; }

        public int InPlayedHoles { get; set; }
        public int InTotalStrokes { get; set; }
        public int InTotalPar { get; set; }
        public int InDiffToPar { get; set; }

        public int EaglesOrBetter { get; set; }
        public int Birdies { get; set; }
        public int Pars { get; set; }
        public int Bogeys { get; set; }
        public int DoubleBogeyOrWorse { get; set; }

        public int? Position { get; set; }
        public int PlayerCount { get; set; }

        public bool CanBeLocked { get; set; }

        public List<HoleRow> Holes { get; set; } = new();

        public class HoleRow
        {
            public int HoleNumber { get; set; }
            public int? Par { get; set; }
            public int Strokes { get; set; }
            public int? DiffToPar { get; set; }
            public string ResultLabel { get; set; } = "";
        }

        public IActionResult OnGet()
        {
            Round = _db.Rounds
                .Include(r => r.Player)
                .Include(r => r.Competition)
                .ThenInclude(c => c.Course)
                .AsNoTracking()
                .FirstOrDefault(r => r.Id == RoundId);

            if (Round == null)
                return RedirectToPage("/Competitions");

            Competition = Round.Competition;

            if (!CanAccessRound(Round))
                return Forbid();

            if (Round.SquadId.HasValue)
            {
                Squad = _db.Squads
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Id == Round.SquadId.Value);
            }

            if (Competition == null || Competition.CourseId == null)
            {
                TempData["Error"] = "Compétition ou parcours introuvable. Impossible d'afficher la carte.";
                return RedirectToPage("/Competitions");
            }

            var parByHole = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition.CourseId.Value)
                .ToDictionary(h => h.HoleNumber, h => h.Par);

            CourseParTotal = parByHole.Count > 0 ? parByHole.Values.Sum() : 0;

            var scores = _db.Scores
                .AsNoTracking()
                .Where(s => s.RoundId == RoundId && s.Strokes > 0)
                .ToList();

            var strokesByHole = scores
                .GroupBy(s => s.HoleNumber)
                .ToDictionary(g => g.Key, g => g.First().Strokes);

            Holes = new List<HoleRow>();
            for (int hole = 1; hole <= 18; hole++)
            {
                parByHole.TryGetValue(hole, out int par);
                int strokes = strokesByHole.TryGetValue(hole, out int st) ? st : 0;

                int? diff = null;
                string result = "";

                if (strokes > 0 && parByHole.ContainsKey(hole))
                {
                    diff = strokes - par;
                    result = GetResultLabel(diff.Value);
                }

                Holes.Add(new HoleRow
                {
                    HoleNumber = hole,
                    Par = parByHole.ContainsKey(hole) ? par : (int?)null,
                    Strokes = strokes,
                    DiffToPar = diff,
                    ResultLabel = result
                });
            }

            var played = Holes.Where(h => h.Strokes > 0).ToList();
            HolesPlayed = played.Count;
            CanBeLocked = (HolesPlayed == 18);

            TotalStrokes = played.Sum(h => h.Strokes);
            PlayedPar = played.Sum(h => h.Par ?? 0);
            DiffToPar = (HolesPlayed == 0) ? 0 : (TotalStrokes - PlayedPar);
            Average = HolesPlayed > 0 ? (double)TotalStrokes / HolesPlayed : 0;

            var outHoles = Holes.Where(h => h.HoleNumber >= 1 && h.HoleNumber <= 9 && h.Strokes > 0).ToList();
            OutPlayedHoles = outHoles.Count;
            OutTotalStrokes = outHoles.Sum(h => h.Strokes);
            OutTotalPar = outHoles.Sum(h => h.Par ?? 0);
            OutDiffToPar = OutPlayedHoles == 0 ? 0 : OutTotalStrokes - OutTotalPar;

            var inHoles = Holes.Where(h => h.HoleNumber >= 10 && h.HoleNumber <= 18 && h.Strokes > 0).ToList();
            InPlayedHoles = inHoles.Count;
            InTotalStrokes = inHoles.Sum(h => h.Strokes);
            InTotalPar = inHoles.Sum(h => h.Par ?? 0);
            InDiffToPar = InPlayedHoles == 0 ? 0 : InTotalStrokes - InTotalPar;

            EaglesOrBetter = played.Count(h => (h.DiffToPar ?? 99) <= -2);
            Birdies = played.Count(h => h.DiffToPar == -1);
            Pars = played.Count(h => h.DiffToPar == 0);
            Bogeys = played.Count(h => h.DiffToPar == 1);
            DoubleBogeyOrWorse = played.Count(h => (h.DiffToPar ?? -99) >= 2);

            ComputePosition();

            return Page();
        }

        private bool CanAccessRound(Round round)
        {
            var competition = round.Competition;

            if (competition == null)
                return false;

            if (!string.IsNullOrWhiteSpace(Token) &&
                !string.IsNullOrWhiteSpace(round.PublicToken) &&
                string.Equals(Token, round.PublicToken, StringComparison.Ordinal))
            {
                return true;
            }

            if (competition.Visibility == CompetitionVisibility.Public)
                return true;

            if (User.Identity?.IsAuthenticated != true)
                return false;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
                return false;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            var user = _db.AppUsers.FirstOrDefault(u => u.Email == email);
            if (user == null)
                return false;

            if (string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase))
            {
                return competition.ClubId.HasValue &&
                       user.ClubId == competition.ClubId;
            }

            if (string.Equals(role, "Player", StringComparison.OrdinalIgnoreCase))
            {
                return user.PlayerId.HasValue &&
                       user.PlayerId == round.PlayerId;
            }

            return false;
        }

        private void ComputePosition()
        {
            if (Round == null) return;

            var competitionRounds = _db.Rounds
                .AsNoTracking()
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == Round.CompetitionId)
                .ToList();

            var roundIds = competitionRounds.Select(r => r.Id).ToList();

            var compScores = _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .ToList();

            var scoreGroups = compScores
                .GroupBy(s => s.RoundId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var parByHole = _db.Holes
                .AsNoTracking()
                .Where(h => h.CourseId == Competition!.CourseId!.Value)
                .ToDictionary(h => h.HoleNumber, h => h.Par);

            var ranking = new List<(int RoundId, int HolesPlayed, int TotalStrokes, int DiffToPar, string PlayerName)>();

            foreach (var r in competitionRounds)
            {
                scoreGroups.TryGetValue(r.Id, out var playerScores);
                playerScores ??= new List<Score>();

                var distinctScores = playerScores
                    .GroupBy(x => x.HoleNumber)
                    .Select(g => g.First())
                    .ToList();

                int holesPlayed = distinctScores.Count;
                int totalStrokes = distinctScores.Sum(s => s.Strokes);

                int totalPar = 0;
                foreach (var sc in distinctScores)
                {
                    if (parByHole.TryGetValue(sc.HoleNumber, out int par))
                        totalPar += par;
                }

                int diffToPar = holesPlayed == 0 ? 0 : totalStrokes - totalPar;
                string playerName = r.Player != null
                    ? (r.Player.FirstName + " " + r.Player.LastName).Trim()
                    : $"PlayerId={r.PlayerId}";

                ranking.Add((r.Id, holesPlayed, totalStrokes, diffToPar, playerName));
            }

            var ordered = ranking
                .OrderBy(x => x.HolesPlayed == 0 ? int.MaxValue : x.DiffToPar)
                .ThenByDescending(x => x.HolesPlayed)
                .ThenBy(x => x.TotalStrokes)
                .ThenBy(x => x.PlayerName)
                .ToList();

            PlayerCount = ordered.Count;

            int index = ordered.FindIndex(x => x.RoundId == Round.Id);
            Position = index >= 0 ? index + 1 : null;
        }

        private string GetResultLabel(int diff)
        {
            if (diff <= -2) return "Eagle+";
            if (diff == -1) return "Birdie";
            if (diff == 0) return "Par";
            if (diff == 1) return "Bogey";
            return "Double+";
        }

        public string FormatDiffToPar(int diffToPar, bool hasScore)
        {
            if (!hasScore) return "-";
            if (diffToPar == 0) return "E";
            if (diffToPar > 0) return $"+{diffToPar}";
            return diffToPar.ToString();
        }

        public string GetScoreCssClass(int diff, bool hasScore)
        {
            if (!hasScore) return "";
            if (diff < 0) return "score-under";
            if (diff > 0) return "score-over";
            return "score-even";
        }

        public string GetHoleResultCssClass(string result)
        {
            return result switch
            {
                "Eagle+" => "hole-result-eagle",
                "Birdie" => "hole-result-birdie",
                "Par" => "hole-result-par",
                "Bogey" => "hole-result-bogey",
                "Double+" => "hole-result-double",
                _ => ""
            };
        }

        public IActionResult OnPostLock(int roundId)
        {
            var round = _db.Rounds
                .Include(r => r.Competition)
                .FirstOrDefault(r => r.Id == roundId);

            if (round == null)
            {
                TempData["Error"] = "Round introuvable.";
                return RedirectToPage("/Competitions");
            }

            if (!CanAccessRound(round))
                return Forbid();

            var competition = round.Competition;
            if (competition == null)
            {
                TempData["Error"] = "Compétition introuvable.";
                return RedirectToPage("/Competitions");
            }

            if (competition.Status == CompetitionStatus.Finished)
            {
                TempData["Error"] = "Cette compétition est terminée. Le verrouillage n'est plus autorisé.";
                return RedirectToPage("/RoundCard", new { roundId, token = Token });
            }

            bool isCompetition = !string.Equals(competition.Mode, "Training", StringComparison.OrdinalIgnoreCase);

            if (isCompetition)
            {
                TempData["Error"] = "En compétition, le verrouillage se fait via le scoring squad (fin de partie).";
                return RedirectToPage("/RoundCard", new { roundId, token = Token });
            }

            if (!round.SquadId.HasValue)
            {
                TempData["Error"] = "Ce joueur n'est pas affecté à un squad. Verrouillage impossible.";
                return RedirectToPage(new { roundId, token = Token });
            }

            int competitionId = round.CompetitionId;
            int squadId = round.SquadId.Value;

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == competitionId && r.SquadId == squadId)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var counts = _db.Scores
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .GroupBy(s => s.RoundId)
                .Select(g => new { RoundId = g.Key, Count = g.Select(x => x.HoleNumber).Distinct().Count() })
                .ToDictionary(x => x.RoundId, x => x.Count);

            var notComplete = rounds
                .Where(r => !counts.ContainsKey(r.Id) || counts[r.Id] != 18)
                .ToList();

            if (notComplete.Any())
            {
                TempData["Error"] = $"Verrouillage impossible : {notComplete.Count} carte(s) incomplète(s) dans le squad.";
                return RedirectToPage(new { roundId, token = Token });
            }

            foreach (var r in rounds)
                r.IsLocked = true;

            _db.SaveChanges();

            TempData["Info"] = "Squad verrouillé définitivement (toutes les cartes).";
            return RedirectToPage(new { roundId, token = Token });
        }
    }
}