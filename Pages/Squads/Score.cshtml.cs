using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages.Squads
{
    public class ScoreModel : PageModel
    {
        private readonly AppDbContext _db;

        public ScoreModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CompetitionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SquadId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Hole { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool Edit { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public Competition? Competition { get; set; }
        public Squad? Squad { get; set; }

        [BindProperty]
        public int CurrentHole { get; set; }

        public int? CurrentPar { get; set; }
        public bool IsHoleValidated { get; set; }
        public bool IsLastHole { get; set; }
        public bool IsSquadLocked { get; set; }
        public bool IsCompetitionFinished { get; set; }

        public List<PlayerScoreRow> Players { get; set; } = new();

        public class PlayerScoreRow
        {
            public int RoundId { get; set; }
            public int PlayerId { get; set; }
            public string PlayerName { get; set; } = "";
            public int Strokes { get; set; }
        }

        private IActionResult? GetAccessResult(bool isWrite)
        {
            if (CanAccessByToken(isWrite))
                return null;

            if (User.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Login");

            if (CanAccessAsUser(isWrite))
                return null;

            return Forbid();
        }

        private bool CanAccessByToken(bool isWrite)
        {
            if (string.IsNullOrWhiteSpace(Token))
                return false;

            var hasValidToken = _db.Rounds.Any(r =>
                r.CompetitionId == CompetitionId &&
                r.SquadId == SquadId &&
                r.PublicToken == Token);

            if (!hasValidToken)
                return false;

            var competition = _db.Competitions.FirstOrDefault(c => c.Id == CompetitionId);
            if (competition == null)
                return false;

            if (isWrite && competition.Status != CompetitionStatus.InProgress)
                return false;

            return true;
        }

        private bool CanAccessAsUser(bool isWrite)
        {
            var competition = _db.Competitions.FirstOrDefault(c => c.Id == CompetitionId);
            if (competition == null)
                return false;

            if (isWrite && competition.Status != CompetitionStatus.InProgress)
                return false;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(email))
                return false;

            if (role == "Admin")
                return true;

            var user = _db.AppUsers.FirstOrDefault(u => u.Email == email);
            if (user == null)
                return false;

            if (role == "Club")
            {
                if (competition.ClubId.HasValue && user.ClubId == competition.ClubId)
                    return true;
            }

            return false;
        }

        private bool ComputeIsSquadLocked()
        {
            return _db.Rounds.AsNoTracking().Any(r =>
                r.CompetitionId == CompetitionId &&
                r.SquadId == SquadId &&
                r.IsLocked);
        }

        private IActionResult? CheckCompetitionAllowsScoring()
        {
            var competition = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (competition == null)
                return RedirectToPage("/Competitions");

            if (competition.Status == CompetitionStatus.Draft)
            {
                TempData["Error"] = "La compétition est en brouillon. Le scoring n'est pas encore autorisé.";
                return RedirectToPage("/Competitions/Details", new { id = CompetitionId });
            }

            if (competition.Status == CompetitionStatus.Finished)
            {
                TempData["Error"] = "La compétition est terminée. Le scoring est verrouillé.";
                return RedirectToPage("/Competitions/Details", new { id = CompetitionId });
            }

            return null;
        }

        public IActionResult OnGet()
        {
            Competition = _db.Competitions
                .Include(c => c.Course)
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            var access = GetAccessResult(false);
            if (access != null)
                return access;

            var scoringCheck = CheckCompetitionAllowsScoring();
            if (scoringCheck != null)
                return scoringCheck;

            Squad = _db.Squads.FirstOrDefault(s => s.Id == SquadId);

            if (Squad == null)
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });

            IsCompetitionFinished = Competition.Status == CompetitionStatus.Finished;
            IsSquadLocked = ComputeIsSquadLocked();

            if (Hole.HasValue)
            {
                CurrentHole = Hole.Value;
            }
            else
            {
                var squadRoundIds = _db.Rounds
                    .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                    .Select(r => r.Id)
                    .ToList();

                if (squadRoundIds.Count == 0)
                {
                    CurrentHole = 1;
                }
                else
                {
                    CurrentHole = 1;

                    for (int i = 1; i <= 18; i++)
                    {
                        bool holeComplete = squadRoundIds.All(rid =>
                            _db.Scores.Any(s => s.RoundId == rid && s.HoleNumber == i));

                        if (!holeComplete)
                        {
                            CurrentHole = i;
                            break;
                        }

                        CurrentHole = 18;
                    }
                }
            }

            if (CurrentHole < 1) CurrentHole = 1;
            if (CurrentHole > 18) CurrentHole = 18;

            IsLastHole = (CurrentHole == 18);

            CurrentPar = _db.Holes
                .Where(h => h.CourseId == Competition.CourseId && h.HoleNumber == CurrentHole)
                .Select(h => (int?)h.Par)
                .FirstOrDefault();

            var rounds = _db.Rounds
                .Include(r => r.Player)
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var existingScores = _db.Scores
                .Where(s => roundIds.Contains(s.RoundId) && s.HoleNumber == CurrentHole)
                .ToList();

            IsHoleValidated = existingScores.Any();

            var dict = existingScores.ToDictionary(s => s.RoundId, s => s.Strokes);

            Players = rounds.Select(r => new PlayerScoreRow
            {
                RoundId = r.Id,
                PlayerId = r.PlayerId,
                PlayerName = r.Player != null
                    ? (r.Player.FirstName + " " + r.Player.LastName)
                    : $"Joueur #{r.PlayerId}",
                Strokes = dict.ContainsKey(r.Id) ? dict[r.Id] : 0
            }).ToList();

            return Page();
        }

        public IActionResult OnPost([FromForm] Dictionary<int, int> Scores)
        {
            Competition = _db.Competitions
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == CompetitionId);

            if (Competition == null)
                return RedirectToPage("/Competitions");

            var scoringCheck = CheckCompetitionAllowsScoring();
            if (scoringCheck != null)
                return scoringCheck;

            var access = GetAccessResult(true);
            if (access != null)
                return access;

            IsSquadLocked = ComputeIsSquadLocked();
            if (IsSquadLocked)
            {
                TempData["Error"] = "Carte verrouillée.";
                return RedirectToPage(new { CompetitionId, SquadId, Hole = CurrentHole, Token });
            }

            if (CurrentHole < 1 || CurrentHole > 18)
            {
                TempData["Error"] = "Trou invalide.";
                return RedirectToPage(new { CompetitionId, SquadId, Hole = 1, Token });
            }

            var roundIds = _db.Rounds
                .Where(r => r.CompetitionId == CompetitionId && r.SquadId == SquadId)
                .Select(r => r.Id)
                .ToHashSet();

            if (roundIds.Count == 0)
            {
                TempData["Error"] = "Aucun round trouvé pour ce squad.";
                return RedirectToPage("/Leaderboard", new { competitionId = CompetitionId });
            }

            var alreadyExists = _db.Scores.Any(s =>
                roundIds.Contains(s.RoundId) &&
                s.HoleNumber == CurrentHole);

            if (alreadyExists)
            {
                TempData["Error"] = "Les scores de ce trou sont déjà enregistrés.";
                return RedirectToPage(new { CompetitionId, SquadId, Hole = CurrentHole, Token });
            }

            foreach (var kvp in Scores)
            {
                if (!roundIds.Contains(kvp.Key))
                    continue;

                if (kvp.Value <= 0)
                    continue;

                _db.Scores.Add(new Score
                {
                    RoundId = kvp.Key,
                    HoleNumber = CurrentHole,
                    Strokes = kvp.Value
                });
            }

            _db.SaveChanges();

            return RedirectToPage(new
            {
                CompetitionId,
                SquadId,
                Hole = CurrentHole < 18 ? CurrentHole + 1 : 18,
                Token
            });
        }
    }
}