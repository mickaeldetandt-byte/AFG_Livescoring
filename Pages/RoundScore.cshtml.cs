using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    public class RoundScoreModel : PageModel
    {
        private readonly AppDbContext _db;

        public RoundScoreModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int RoundId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public Round? Round { get; set; }

        [BindProperty]
        public List<int> HoleScores { get; set; } = new();

        public List<int> HolePars { get; set; } = new();

        public int TotalCoursePar { get; set; }

        public bool IsReadOnly { get; set; }

        public IActionResult OnGet()
        {
            Round = _db.Rounds
                .Include(r => r.Player)
                .Include(r => r.Competition)
                .Include(r => r.Squad)
                .FirstOrDefault(r => r.Id == RoundId);

            if (Round == null)
                return RedirectToPage("/Competitions");

            var accessResult = GetAccessResult(Round, isWriteAccess: false);
            if (accessResult != null)
                return accessResult;

            var competition = Round.Competition;
            if (competition == null)
                return RedirectToPage("/Competitions");

            if (competition.ScoringMode == ScoringMode.SquadOnly)
            {
                TempData["Error"] = "En compétition, la saisie individuelle est désactivée. Utilisez le scoring par squad.";
                if (Round.SquadId.HasValue)
                {
                    return RedirectToPage("/Squads/Score", new
                    {
                        competitionId = Round.CompetitionId,
                        squadId = Round.SquadId.Value,
                        hole = 1
                    });
                }

                return RedirectToPage("/Leaderboard", new { competitionId = Round.CompetitionId });
            }

            IsReadOnly = competition.Status == CompetitionStatus.Finished;

            LoadExistingScores();
            LoadHolePars(competition.CourseId);

            return Page();
        }

        public IActionResult OnPost()
        {
            var round = _db.Rounds
                .Include(r => r.Player)
                .Include(r => r.Competition)
                .Include(r => r.Squad)
                .FirstOrDefault(r => r.Id == RoundId);

            if (round == null)
                return RedirectToPage("/Competitions");

            var accessResult = GetAccessResult(round, isWriteAccess: true);
            if (accessResult != null)
                return accessResult;

            var competition = round.Competition;
            if (competition == null)
                return RedirectToPage("/Competitions");

            if (competition.Status == CompetitionStatus.Finished)
            {
                TempData["Error"] = "Cette compétition est terminée. Le scoring n'est plus modifiable.";
                return RedirectToPage(new { roundId = RoundId, token = Token });
            }

            if (competition.ScoringMode == ScoringMode.SquadOnly)
            {
                TempData["Error"] = "En compétition, la saisie individuelle est désactivée. Utilisez le scoring par squad.";
                if (round.SquadId.HasValue)
                {
                    return RedirectToPage("/Squads/Score", new
                    {
                        competitionId = round.CompetitionId,
                        squadId = round.SquadId.Value,
                        hole = 1
                    });
                }

                return RedirectToPage("/Leaderboard", new { competitionId = round.CompetitionId });
            }

            NormalizeHoleScores();

            for (int i = 0; i < 18; i++)
            {
                int holeNumber = i + 1;
                int strokes = HoleScores[i];

                if (strokes < 0) strokes = 0;
                if (strokes > 10) strokes = 10;

                var existing = _db.Scores
                    .FirstOrDefault(s => s.RoundId == RoundId && s.HoleNumber == holeNumber);

                if (strokes == 0)
                {
                    if (existing != null)
                        _db.Scores.Remove(existing);

                    continue;
                }

                if (existing == null)
                {
                    _db.Scores.Add(new Score
                    {
                        RoundId = RoundId,
                        HoleNumber = holeNumber,
                        Strokes = strokes
                    });
                }
                else
                {
                    existing.Strokes = strokes;
                }
            }

            _db.SaveChanges();

            TempData["Info"] = "Scores enregistrés avec succès.";
            return RedirectToPage(new { roundId = RoundId, token = Token });
        }

        private IActionResult? GetAccessResult(Round round, bool isWriteAccess)
        {
            if (CanAccessByToken(round, isWriteAccess))
                return null;

            if (User.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Login");

            if (CanAccessAsAuthenticatedUser(round, isWriteAccess))
                return null;

            return Forbid();
        }

        private bool CanAccessByToken(Round round, bool isWriteAccess)
        {
            if (string.IsNullOrWhiteSpace(Token))
                return false;

            if (string.IsNullOrWhiteSpace(round.PublicToken))
                return false;

            if (!string.Equals(round.PublicToken, Token, StringComparison.Ordinal))
                return false;

            var competition = round.Competition;
            if (competition == null)
                return false;

            if (isWriteAccess && competition.Status == CompetitionStatus.Finished)
                return false;

            return true;
        }

        private bool CanAccessAsAuthenticatedUser(Round round, bool isWriteAccess)
        {
            var competition = round.Competition;
            if (competition == null)
                return false;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
                return false;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return !isWriteAccess || competition.Status != CompetitionStatus.Finished;
            }

            var currentUser = _db.AppUsers.FirstOrDefault(u => u.Email == email);
            if (currentUser == null)
                return false;

            if (string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase))
            {
                if (competition.ClubId.HasValue && currentUser.ClubId == competition.ClubId)
                    return !isWriteAccess || competition.Status != CompetitionStatus.Finished;

                return false;
            }

            if (string.Equals(role, "Player", StringComparison.OrdinalIgnoreCase))
            {
                if (currentUser.PlayerId.HasValue && currentUser.PlayerId == round.PlayerId)
                    return !isWriteAccess || competition.Status != CompetitionStatus.Finished;

                return false;
            }

            return false;
        }

        private void LoadExistingScores()
        {
            var existingScores = _db.Scores
                .Where(s => s.RoundId == RoundId)
                .OrderBy(s => s.HoleNumber)
                .ToList();

            HoleScores = new List<int>();
            for (int i = 1; i <= 18; i++)
            {
                var score = existingScores.FirstOrDefault(s => s.HoleNumber == i);
                HoleScores.Add(score?.Strokes ?? 0);
            }
        }

        private void LoadHolePars(int? courseId)
        {
            HolePars = new List<int>();
            for (int i = 0; i < 18; i++)
                HolePars.Add(0);

            TotalCoursePar = 0;

            if (!courseId.HasValue)
                return;

            var holes = _db.Holes.AsNoTracking()
                .Where(h => h.CourseId == courseId.Value)
                .ToList();

            foreach (var h in holes)
            {
                if (h.HoleNumber >= 1 && h.HoleNumber <= 18)
                    HolePars[h.HoleNumber - 1] = h.Par;
            }

            TotalCoursePar = HolePars.Sum();
        }

        private void NormalizeHoleScores()
        {
            if (HoleScores == null)
                HoleScores = new List<int>();

            while (HoleScores.Count < 18)
                HoleScores.Add(0);

            if (HoleScores.Count > 18)
                HoleScores = HoleScores.Take(18).ToList();
        }
    }
}