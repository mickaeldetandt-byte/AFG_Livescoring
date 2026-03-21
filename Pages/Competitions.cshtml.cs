using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;

namespace AFG_Livescoring.Pages
{
    [Authorize]
    public class CompetitionsModel : PageModel
    {
        private readonly AppDbContext _db;

        public CompetitionsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<Competition> Competitions { get; set; } = new();

        public Dictionary<int, CompetitionStateInfo> CompetitionStates { get; set; } = new();

        public List<Course> Courses { get; set; } = new();

        [BindProperty]
        public Competition NewCompetition { get; set; } = new();

        public class CompetitionStateInfo
        {
            public int CompetitionId { get; set; }
            public int PlayerCount { get; set; }
            public bool HasStarted { get; set; }
            public bool IsFinished { get; set; }
            public int CompletedRounds { get; set; }
        }

        public IActionResult OnGet()
        {
            LoadCourses();
            LoadCompetitionsAndStates();

            NewCompetition.Date = DateTime.Today;
            NewCompetition.ScoringMode = ScoringMode.SquadOnly;
            NewCompetition.Mode = "Competition";
            NewCompetition.CompetitionType = CompetitionType.IndividualStrokePlay;
            NewCompetition.Visibility = CompetitionVisibility.Public;
            NewCompetition.Status = CompetitionStatus.Draft;
            NewCompetition.IsActive = true;

            return Page();
        }

        private void LoadCourses()
        {
            Courses = _db.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();
        }

        private void LoadCompetitionsAndStates()
        {
            Competitions = _db.Competitions
                .Include(c => c.Course)
                .Include(c => c.Club)
                .AsNoTracking()
                .OrderByDescending(c => c.Date)
                .ThenBy(c => c.Name)
                .ToList();

            CompetitionStates = new Dictionary<int, CompetitionStateInfo>();

            var competitionIds = Competitions.Select(c => c.Id).ToList();

            var rounds = _db.Rounds
                .AsNoTracking()
                .Where(r => competitionIds.Contains(r.CompetitionId))
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            var scores = _db.Scores
                .AsNoTracking()
                .Where(s => roundIds.Contains(s.RoundId) && s.Strokes > 0)
                .ToList();

            var holesPlayedByRoundId = scores
                .GroupBy(s => s.RoundId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.HoleNumber).Distinct().Count()
                );

            foreach (var comp in Competitions)
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

                bool isFinished = playerCount > 0 && completedRounds == playerCount;

                CompetitionStates[comp.Id] = new CompetitionStateInfo
                {
                    CompetitionId = comp.Id,
                    PlayerCount = playerCount,
                    HasStarted = hasStarted,
                    IsFinished = isFinished,
                    CompletedRounds = completedRounds
                };
            }
        }

        public IActionResult OnPostAdd()
        {
            LoadCourses();

            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToPage("/Account/Login");

            if (!CanCreateCompetition())
                return Forbid();

            if (string.IsNullOrWhiteSpace(NewCompetition.Name))
            {
                ModelState.AddModelError(string.Empty, "Le nom de la compétition est obligatoire.");
            }

            if (NewCompetition.CourseId == null || !_db.Courses.Any(c => c.Id == NewCompetition.CourseId))
            {
                ModelState.AddModelError(string.Empty, "Veuillez sélectionner un parcours.");
            }

            if (!Enum.IsDefined(typeof(ScoringMode), NewCompetition.ScoringMode))
            {
                ModelState.AddModelError(string.Empty, "Mode invalide.");
            }

            if (!Enum.IsDefined(typeof(CompetitionType), NewCompetition.CompetitionType))
            {
                ModelState.AddModelError(string.Empty, "Format de jeu invalide.");
            }

            if (!Enum.IsDefined(typeof(CompetitionVisibility), NewCompetition.Visibility))
            {
                ModelState.AddModelError(string.Empty, "Visibilité invalide.");
            }

            if (string.IsNullOrWhiteSpace(NewCompetition.Mode))
            {
                NewCompetition.Mode = "Competition";
            }

            if (NewCompetition.Mode != "Competition" && NewCompetition.Mode != "Training")
            {
                NewCompetition.Mode = "Competition";
            }

            if (NewCompetition.Mode == "Training")
            {
                NewCompetition.ScoringMode = ScoringMode.IndividualAllowed;
            }
            else
            {
                NewCompetition.ScoringMode = ScoringMode.SquadOnly;
            }

            NewCompetition.Status = CompetitionStatus.Draft;
            NewCompetition.IsActive = true;

            var currentUser = GetCurrentUser();

            if (currentUser == null)
            {
                ModelState.AddModelError(string.Empty, "Utilisateur introuvable.");
            }
            else
            {
                NewCompetition.CreatedByUserId = currentUser.Id;

                if (string.Equals(currentUser.Role, "Club", StringComparison.OrdinalIgnoreCase))
                {
                    NewCompetition.ClubId = currentUser.ClubId;
                }
            }

            if (!ModelState.IsValid)
            {
                LoadCompetitionsAndStates();
                return Page();
            }

            _db.Competitions.Add(NewCompetition);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Compétition créée avec succès.";
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToPage("/Account/Login");

            var comp = _db.Competitions.FirstOrDefault(c => c.Id == id);
            if (comp == null)
            {
                return RedirectToPage();
            }

            if (!CanManageCompetition(comp))
                return Forbid();

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == id)
                .ToList();

            var roundIds = rounds.Select(r => r.Id).ToList();

            if (roundIds.Any())
            {
                var scores = _db.Scores
                    .Where(s => roundIds.Contains(s.RoundId))
                    .ToList();

                if (scores.Any())
                {
                    _db.Scores.RemoveRange(scores);
                    _db.SaveChanges();
                }
            }

            var matchPlayRounds = _db.MatchPlayRounds
                .Where(m => m.CompetitionId == id)
                .ToList();

            var matchPlayRoundIds = matchPlayRounds.Select(m => m.Id).ToList();

            if (matchPlayRoundIds.Any())
            {
                var matchPlayHoleResults = _db.MatchPlayHoleResults
                    .Where(h => matchPlayRoundIds.Contains(h.MatchPlayRoundId))
                    .ToList();

                if (matchPlayHoleResults.Any())
                {
                    _db.MatchPlayHoleResults.RemoveRange(matchPlayHoleResults);
                    _db.SaveChanges();
                }

                _db.MatchPlayRounds.RemoveRange(matchPlayRounds);
                _db.SaveChanges();
            }

            if (rounds.Any())
            {
                foreach (var round in rounds)
                {
                    round.SquadId = null;
                }

                _db.SaveChanges();
            }

            var teamRounds = _db.TeamRounds
                .Where(tr => tr.CompetitionId == id)
                .ToList();

            if (teamRounds.Any())
            {
                var teamRoundIds = teamRounds.Select(tr => tr.Id).ToList();

                var teamScores = _db.TeamScores
                    .Where(ts => teamRoundIds.Contains(ts.TeamRoundId))
                    .ToList();

                if (teamScores.Any())
                {
                    _db.TeamScores.RemoveRange(teamScores);
                    _db.SaveChanges();
                }

                _db.TeamRounds.RemoveRange(teamRounds);
                _db.SaveChanges();
            }

            var teams = _db.Teams
                .Where(t => t.CompetitionId == id)
                .ToList();

            if (teams.Any())
            {
                var teamIds = teams.Select(t => t.Id).ToList();

                var teamPlayers = _db.TeamPlayers
                    .Where(tp => teamIds.Contains(tp.TeamId))
                    .ToList();

                if (teamPlayers.Any())
                {
                    _db.TeamPlayers.RemoveRange(teamPlayers);
                    _db.SaveChanges();
                }

                _db.Teams.RemoveRange(teams);
                _db.SaveChanges();
            }

            if (rounds.Any())
            {
                _db.Rounds.RemoveRange(rounds);
                _db.SaveChanges();
            }

            var squads = _db.Squads
                .Where(s => s.CompetitionId == id)
                .ToList();

            if (squads.Any())
            {
                _db.Squads.RemoveRange(squads);
                _db.SaveChanges();
            }

            _db.Competitions.Remove(comp);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Compétition supprimée avec succès.";
            return RedirectToPage();
        }

        private bool CanCreateCompetition()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanManageCompetition(Competition competition)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(role, "Club", StringComparison.OrdinalIgnoreCase))
                return false;

            var currentUser = GetCurrentUser();
            if (currentUser == null)
                return false;

            if (competition.ClubId.HasValue && currentUser.ClubId == competition.ClubId)
                return true;

            if (competition.CreatedByUserId.HasValue && currentUser.Id == competition.CreatedByUserId.Value)
                return true;

            return false;
        }

        private AppUser? GetCurrentUser()
        {
            var email = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(email))
                return null;

            return _db.AppUsers.FirstOrDefault(u => u.Email == email);
        }
    }
}