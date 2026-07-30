using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using AFG_Livescoring.Services;

namespace AFG_Livescoring.Pages
{
    [Authorize(Roles = "Admin,Club")]
    public class CompetitionsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ICompetitionAuthorizationService _authorizationService;

        public CompetitionsModel(
            AppDbContext db,
            ICompetitionAuthorizationService authorizationService)
        {
            _db = db;
            _authorizationService = authorizationService;
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
            public int TotalRounds { get; set; }
        }

        public IActionResult OnGet()
        {
            var scope = GetCompetitionListScope();
            if (scope == null)
                return Forbid();

            LoadCourses();
            LoadCompetitionsAndStates(scope);

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

        private void LoadCompetitionsAndStates(CompetitionListScope? authorizedScope = null)
        {
            var scope = authorizedScope ?? GetCompetitionListScope();
            if (scope == null)
            {
                Competitions = new List<Competition>();
                CompetitionStates = new Dictionary<int, CompetitionStateInfo>();
                return;
            }

            var competitionsQuery = _db.Competitions
                .AsNoTracking()
                .AsQueryable();

            if (!scope.IsAdmin)
            {
                competitionsQuery = competitionsQuery
                    .Where(competition => competition.ClubId == scope.ClubId);
            }

            Competitions = competitionsQuery
                .Include(c => c.Course)
                .Include(c => c.Club)
                .OrderByDescending(c => c.Date)
                .ThenBy(c => c.Name)
                .ToList();

            CompetitionStates = new Dictionary<int, CompetitionStateInfo>();

            var metricsByCompetition = CompetitionMetricsCalculator.Calculate(
                _db,
                Competitions);

            foreach (var comp in Competitions)
            {
                var metrics = metricsByCompetition[comp.Id];

                CompetitionStates[comp.Id] = new CompetitionStateInfo
                {
                    CompetitionId = comp.Id,
                    PlayerCount = metrics.ParticipantsCount,
                    HasStarted = metrics.HasStarted,
                    IsFinished = metrics.IsFinished,
                    CompletedRounds = metrics.CompletedRounds,
                    TotalRounds = metrics.TotalRounds
                };
            }
        }

        private CompetitionListScope? GetCompetitionListScope()
        {
            if (User.Identity?.IsAuthenticated != true
                || !int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out var userId))
            {
                return null;
            }

            var currentUser = _db.AppUsers
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Role,
                    user.ClubId
                })
                .SingleOrDefault();

            if (currentUser == null)
                return null;

            if (string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return new CompetitionListScope(true, null);

            if (string.Equals(currentUser.Role, "Club", StringComparison.OrdinalIgnoreCase)
                && currentUser.ClubId.HasValue)
            {
                return new CompetitionListScope(false, currentUser.ClubId.Value);
            }

            return null;
        }

        private sealed record CompetitionListScope(bool IsAdmin, int? ClubId);

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

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToPage("/Account/Login");

            var canManageCompetition = await _authorizationService
                .CanManageCompetitionAsync(User, id, HttpContext.RequestAborted);

            if (!canManageCompetition)
            {
                var competitionExists = await _db.Competitions
                    .AsNoTracking()
                    .AnyAsync(
                        competition => competition.Id == id,
                        HttpContext.RequestAborted);

                return competitionExists ? Forbid() : RedirectToPage();
            }

            var comp = await _db.Competitions
                .SingleOrDefaultAsync(
                    competition => competition.Id == id,
                    HttpContext.RequestAborted);
            if (comp == null)
            {
                return RedirectToPage();
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                HttpContext.RequestAborted);

            try
            {
                var hasIndividualScores = await _db.Scores
                    .AnyAsync(
                        score => score.Round != null
                                 && score.Round.CompetitionId == id,
                        HttpContext.RequestAborted);

                var hasTeamScores = await _db.TeamScores
                    .AnyAsync(
                        score => score.TeamRound != null
                                 && score.TeamRound.CompetitionId == id,
                        HttpContext.RequestAborted);

                var hasMatchPlayHoleResults = await _db.MatchPlayHoleResults
                    .AnyAsync(
                        result => result.MatchPlayRound != null
                                  && result.MatchPlayRound.CompetitionId == id,
                        HttpContext.RequestAborted);

                var hasRecordedMatchPlayResult = await _db.MatchPlayRounds
                    .AnyAsync(
                        match => match.CompetitionId == id
                                 && (match.IsFinished
                                     || match.WinnerTeamId.HasValue
                                     || match.CurrentHole > 1
                                     || match.StatusText != "AS"
                                     || match.ResultText != string.Empty),
                        HttpContext.RequestAborted);

                if (hasIndividualScores
                    || hasTeamScores
                    || hasMatchPlayHoleResults
                    || hasRecordedMatchPlayResult)
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["ErrorMessage"] =
                        "Impossible de supprimer cette compétition car elle contient déjà des scores ou des résultats.";
                    return RedirectToPage();
                }

            var rounds = _db.Rounds
                .Where(r => r.CompetitionId == id)
                .ToList();

            var matchPlayRounds = _db.MatchPlayRounds
                .Where(m => m.CompetitionId == id)
                .ToList();

            if (matchPlayRounds.Any())
            {
                _db.MatchPlayRounds.RemoveRange(matchPlayRounds);
            }

            var teamRounds = _db.TeamRounds
                .Where(tr => tr.CompetitionId == id)
                .ToList();

            if (teamRounds.Any())
            {
                _db.TeamRounds.RemoveRange(teamRounds);
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
                }

                _db.Teams.RemoveRange(teams);
            }

            if (rounds.Any())
            {
                _db.Rounds.RemoveRange(rounds);
            }

            var squads = _db.Squads
                .Where(s => s.CompetitionId == id)
                .ToList();

            if (squads.Any())
            {
                _db.Squads.RemoveRange(squads);
            }

            _db.Competitions.Remove(comp);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);

            TempData["SuccessMessage"] = "Compétition supprimée avec succès.";
            return RedirectToPage();
            }
            catch
            {
                await transaction.RollbackAsync(HttpContext.RequestAborted);
                throw;
            }
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
